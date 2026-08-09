using System;
using System.Collections;
using Bousai.Flood;
using UnityEngine;

public class FloodDepthController : MonoBehaviour
{
    public const string DisplayEnabledPreferenceKey = "FloodDisplayEnabled";
    public const string HeightReferenceModePreferenceKey = "FloodHeightReferenceMode";
    private const float FloodSurfaceAdjustmentMeters = 0.1f;

    public static FloodDepthController Instance { get; private set; }
    public event Action StatusChanged;

    [Header("References")]
    [SerializeField] private FloodDepthTileClient tileClient;
    [SerializeField] private TerrainElevationTileClient elevationClient;
    [SerializeField] private GroundSurfaceResolver groundResolver;
    [SerializeField] private GameObject floodPane;
    [Tooltip("FloodPaneと同時に表示・非表示を切り替える物差しオブジェクトです。")]
    [SerializeField] private GameObject rulerObject;
    [Tooltip("Rulerの0m位置です。未設定時はRuler配下RendererのWorld Bounds下端を使用します。")]
    [SerializeField] private Transform rulerZeroPoint;
    [SerializeField] private Transform trackedPose;

    [Header("Refresh")]
    [SerializeField, Min(0f)] private float minimumRefreshSeconds = 2f;
    [SerializeField, Min(0f)] private float minimumRefreshDistanceMeters = 2f;
    [SerializeField, Min(0.02f)] private float groundCheckIntervalSeconds = 0.1f;

    [Header("Pane")]
    [SerializeField, Min(0.05f)] private float heightTransitionSeconds = 1f;
    [SerializeField, Min(0f)] private float heightChangeEpsilon = 0.01f;
    [Tooltip("Rulerの0m位置へ加えるWorld Y補正値です。通常は0です。")]
    [SerializeField] private float rulerZeroHeightOffsetMeters;

    private FloodDepthResult currentResult;
    private TerrainElevationResult currentElevation;
    private bool floodDisplayEnabled = true;
    private FloodHeightReferenceMode heightReferenceMode = FloodHeightReferenceMode.GroundSurface;
    private bool locationSubscribed;
    private bool requestInFlight;
    private bool hasRequested;
    private float lastRequestTime;
    private Vector3 lastRequestPosition;
    private int requestGeneration;
    private Coroutine activeRequest;

    private bool hasDepth;
    private float currentDepthMeters;
    private bool paneReady;
    private float nextGroundCheckTime;
    private bool hasPositionedPane;
    private bool isTransitioning;
    private float transitionStartY;
    private float transitionTargetY;
    private float transitionStartTime;
    private SurfaceAlignedBillboard rulerOrientation;

    private bool lastMockMode;
    private float lastMockHeight = float.NaN;

    public FloodDepthResult CurrentResult => currentResult;
    public TerrainElevationResult CurrentElevation => currentElevation;
    public bool IsPaneReady => paneReady;
    public bool IsFloodDisplayEnabled => floodDisplayEnabled;
    public FloodHeightReferenceMode HeightReferenceMode => heightReferenceMode;
    public bool UseMockMode => MockModeSettings.Instance != null && MockModeSettings.Instance.UseFloodDepthMock;

    public string HeightReferenceStatus
    {
        get
        {
            if (heightReferenceMode == FloodHeightReferenceMode.GroundSurface)
                return paneReady ? "Pane: 表示中 (地表面)" : "Pane: 地表面を検出中";

            if (currentElevation == null)
                return "Pane: 標高取得待ち";
            if (!currentElevation.HasElevation)
                return $"Pane: {currentElevation.Message}";

            string pane = paneReady ? "Pane: 表示中" : "Pane: 配置待ち";
            return $"標高: {currentElevation.ElevationMeters:0.0}m ({currentElevation.SourceId}) | {pane}";
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        floodDisplayEnabled = PlayerPrefs.GetInt(DisplayEnabledPreferenceKey, 1) == 1;
        heightReferenceMode = (FloodHeightReferenceMode)Mathf.Clamp(
            PlayerPrefs.GetInt(HeightReferenceModePreferenceKey, 0), 0, 1);
        ResolveReferences();
        SetPaneActive(false);
        currentResult = new FloodDepthResult(
            FloodDepthStatus.WaitingForCalibration, "位置基準の確定待ち");
    }

    private void Start()
    {
        SubscribeLocationResolver();
        ApplyMockSettingsIfChanged(true);
        NotifyStatusChanged();
    }

    private void OnEnable()
    {
        SubscribeLocationResolver();
    }

    private void OnDisable()
    {
        CancelActiveRequest();
        UnsubscribeLocationResolver();
    }

    private void OnDestroy()
    {
        UnsubscribeLocationResolver();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveReferences();
        SubscribeLocationResolver();
        UpdatePaneTransition();
        ApplyMockSettingsIfChanged(false);

        if (!floodDisplayEnabled)
            return;

        bool useMock = UseMockMode;
        if (useMock && heightReferenceMode == FloodHeightReferenceMode.GroundSurface)
        {
            CheckGroundAndUpdatePane();
            return;
        }

        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            SetPaneActive(false);
            SetPaneReady(false);
            if (!useMock && (currentResult == null || currentResult.Status != FloodDepthStatus.WaitingForCalibration))
            {
                ClearDepthAndPane();
                SetResult(new FloodDepthResult(
                    FloodDepthStatus.WaitingForCalibration, "位置基準の確定待ち"));
            }
            return;
        }

        if (trackedPose == null)
            return;

        if (useMock)
        {
            if (!requestInFlight && !hasRequested)
                BeginMockElevationRequest();
            return;
        }

        if (tileClient == null)
            return;

        if (!requestInFlight && !hasRequested)
        {
            BeginRequest();
        }
        else if (!requestInFlight && FloodTileMath.ShouldRefresh(
                     Time.unscaledTime - lastRequestTime,
                     lastRequestPosition,
                     trackedPose.position,
                     minimumRefreshSeconds,
                     minimumRefreshDistanceMeters))
        {
            BeginRequest();
        }

        if (hasDepth && heightReferenceMode == FloodHeightReferenceMode.GroundSurface)
            CheckGroundAndUpdatePane();
    }

    public void SetFloodDisplayEnabled(bool enabledValue)
    {
        if (floodDisplayEnabled == enabledValue)
            return;

        floodDisplayEnabled = enabledValue;
        PlayerPrefs.SetInt(DisplayEnabledPreferenceKey, enabledValue ? 1 : 0);
        PlayerPrefs.Save();
        ResetRequestsAndPane();

        if (enabledValue)
            ApplyMockSettingsIfChanged(true);
        else
            SetResult(new FloodDepthResult(FloodDepthStatus.WaitingForCalibration, "洪水表示OFF"));

        NotifyStatusChanged();
    }

    public void SetHeightReferenceMode(FloodHeightReferenceMode mode)
    {
        if (heightReferenceMode == mode)
            return;

        heightReferenceMode = mode;
        PlayerPrefs.SetInt(HeightReferenceModePreferenceKey, (int)mode);
        PlayerPrefs.Save();
        ResetRequestsAndPane();
        ApplyMockSettingsIfChanged(true);
        NotifyStatusChanged();
    }

    private void BeginRequest()
    {
        if (!TryGetCurrentLocation(out LocationData location))
            return;

        StartTrackedRequest(location);
        activeRequest = StartCoroutine(FetchFloodAndElevation(location, requestGeneration));
    }

    private void BeginMockElevationRequest()
    {
        if (heightReferenceMode != FloodHeightReferenceMode.TerrainElevation ||
            !TryGetCurrentLocation(out LocationData location))
            return;

        if (elevationClient == null)
        {
            hasRequested = true;
            currentElevation = CreateMissingElevationClientResult(location);
            HidePaneForMissingReference();
            NotifyStatusChanged();
            return;
        }

        StartTrackedRequest(location);
        activeRequest = StartCoroutine(FetchMockElevation(location, requestGeneration));
    }

    private void StartTrackedRequest(LocationData location)
    {
        requestInFlight = true;
        hasRequested = true;
        lastRequestTime = Time.unscaledTime;
        lastRequestPosition = trackedPose.position;

        if (!UseMockMode)
        {
            SetResult(new FloodDepthResult(
                FloodDepthStatus.Loading, "取得中", true,
                location.latitude, location.longitude));
        }
    }

    private IEnumerator FetchFloodAndElevation(LocationData location, int generation)
    {
        FloodDepthResult result = null;
        yield return tileClient.Fetch(location.latitude, location.longitude, value => result = value);

        TerrainElevationResult elevation = null;
        if (result != null && result.HasDepth &&
            heightReferenceMode == FloodHeightReferenceMode.TerrainElevation && elevationClient != null)
        {
            yield return elevationClient.Fetch(
                location.latitude, location.longitude, value => elevation = value);
        }

        activeRequest = null;
        if (generation != requestGeneration || UseMockMode || !floodDisplayEnabled)
        {
            requestInFlight = false;
            yield break;
        }

        requestInFlight = false;
        if (result == null)
        {
            result = new FloodDepthResult(
                FloodDepthStatus.NetworkError, "取得結果がありません", true,
                location.latitude, location.longitude);
        }

        ApplyFetchResult(result, elevation);
    }

    private IEnumerator FetchMockElevation(LocationData location, int generation)
    {
        TerrainElevationResult elevation = null;
        yield return elevationClient.Fetch(
            location.latitude, location.longitude, value => elevation = value);

        activeRequest = null;
        if (generation != requestGeneration || !UseMockMode || !floodDisplayEnabled ||
            heightReferenceMode != FloodHeightReferenceMode.TerrainElevation)
        {
            requestInFlight = false;
            yield break;
        }

        requestInFlight = false;
        currentElevation = elevation ?? new TerrainElevationResult(
            TerrainElevationStatus.NetworkError, "標高取得結果がありません",
            location.latitude, location.longitude);
        if (currentElevation.HasElevation)
            UpdatePaneFromElevation();
        else
            HidePaneForMissingReference();
        NotifyStatusChanged();
    }

    private void ApplyFetchResult(FloodDepthResult result, TerrainElevationResult elevation)
    {
        SetResult(result);
        if (!result.HasDepth)
        {
            ClearDepthAndPane();
            return;
        }

        hasDepth = true;
        currentDepthMeters = GetAdjustedFloodHeight(result.DepthBand.DisplayHeightMeters);
        currentElevation = elevation;

        if (heightReferenceMode == FloodHeightReferenceMode.TerrainElevation)
        {
            if (currentElevation == null)
                currentElevation = CreateMissingElevationClientResult(result.Latitude, result.Longitude);
            if (currentElevation != null && currentElevation.HasElevation)
                UpdatePaneFromElevation();
            else
                HidePaneForMissingReference();
            NotifyStatusChanged();
            return;
        }

        nextGroundCheckTime = 0f;
        CheckGroundAndUpdatePane();
    }

    private void CheckGroundAndUpdatePane()
    {
        if (!hasDepth || trackedPose == null || floodPane == null || groundResolver == null ||
            Time.unscaledTime < nextGroundCheckTime)
            return;

        nextGroundCheckTime = Time.unscaledTime + groundCheckIntervalSeconds;
        if (!groundResolver.TryGetGroundY(trackedPose.position, out float groundY))
        {
            HidePaneForMissingReference();
            return;
        }

        rulerOrientation?.SetSurfaceNormal(Vector3.up);
        SetPaneTargetY(groundY + currentDepthMeters, groundY);
    }

    private void UpdatePaneFromElevation()
    {
        if (!hasDepth || currentElevation == null || !currentElevation.HasElevation ||
            LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            HidePaneForMissingReference();
            return;
        }

        LocationData originGps = LocationResolver.Instance.GetOriginGps();
        Vector3 originUnity = LocationResolver.Instance.GetOriginUnity();
        float referenceY = TerrainElevationMath.ToUnityY(
            originUnity.y, originGps.altitude,
            currentElevation.ElevationMeters, 0f);
        float targetY = referenceY + currentDepthMeters;
        rulerOrientation?.SetSurfaceNormal(Vector3.up);
        SetPaneTargetY(targetY, referenceY);
    }

    private void SetPaneTargetY(float targetY, float referenceY)
    {
        if (floodPane == null)
            return;

        SetRulerReferenceY(referenceY);

        if (!hasPositionedPane || !floodPane.activeSelf)
        {
            SetPaneY(targetY);
            transitionTargetY = targetY;
            hasPositionedPane = true;
            isTransitioning = false;
            SetPaneActive(true);
            SetPaneReady(true);
            return;
        }

        if (Mathf.Abs(targetY - transitionTargetY) > heightChangeEpsilon)
        {
            transitionStartY = floodPane.transform.position.y;
            transitionTargetY = targetY;
            transitionStartTime = Time.unscaledTime;
            isTransitioning = true;
        }

        SetPaneReady(true);
    }

    private void UpdatePaneTransition()
    {
        if (!isTransitioning || floodPane == null)
            return;

        float duration = Mathf.Max(0.05f, heightTransitionSeconds);
        float t = FloodTileMath.SmoothStep01((Time.unscaledTime - transitionStartTime) / duration);
        SetPaneY(Mathf.LerpUnclamped(transitionStartY, transitionTargetY, t));
        if (t >= 1f)
            isTransitioning = false;
    }

    private void ApplyMockSettingsIfChanged(bool force)
    {
        bool useMock = UseMockMode;
        float mockHeight = MockModeSettings.Instance != null
            ? MockModeSettings.Instance.FloodDepthMeters
            : 0f;
        if (!force && lastMockMode == useMock && Mathf.Approximately(lastMockHeight, mockHeight))
            return;

        lastMockMode = useMock;
        lastMockHeight = mockHeight;
        ResetRequestsAndPane();

        if (!floodDisplayEnabled)
            return;

        if (!useMock)
        {
            SetResult(new FloodDepthResult(
                FloodDepthStatus.WaitingForCalibration, "位置基準の確定待ち"));
            return;
        }

        hasDepth = true;
        currentDepthMeters = GetAdjustedFloodHeight(mockHeight);
        FloodDepthBand mockBand = new FloodDepthBand(
            $"{mockHeight:0.0}m (Mock)", mockHeight, mockHeight,
            mockHeight, true);
        SetResult(new FloodDepthResult(
            FloodDepthStatus.Mock, "MockModeSettings固定値", false,
            depthBand: mockBand, retrievedAt: DateTime.Now));

        if (heightReferenceMode == FloodHeightReferenceMode.GroundSurface)
        {
            nextGroundCheckTime = 0f;
            CheckGroundAndUpdatePane();
        }
    }

    private void HandleCalibrationChanged()
    {
        ResetRequestsAndPane();
        if (!UseMockMode)
        {
            SetResult(new FloodDepthResult(
                FloodDepthStatus.WaitingForCalibration, "再キャリブレーション反映中"));
        }
        else
        {
            ApplyMockSettingsIfChanged(true);
        }
    }

    private bool TryGetCurrentLocation(out LocationData location)
    {
        location = default;
        return trackedPose != null && LocationResolver.Instance != null &&
               LocationResolver.Instance.TryUnityToGps(trackedPose.position, out location);
    }

    private static TerrainElevationResult CreateMissingElevationClientResult(LocationData location)
    {
        return CreateMissingElevationClientResult(location.latitude, location.longitude);
    }

    private static TerrainElevationResult CreateMissingElevationClientResult(
        double latitude, double longitude)
    {
        return new TerrainElevationResult(
            TerrainElevationStatus.NoData, "TerrainElevationTileClient未設定",
            latitude, longitude);
    }

    private void ResolveReferences()
    {
        if (tileClient == null)
            tileClient = GetComponent<FloodDepthTileClient>();
        if (elevationClient == null)
            elevationClient = GetComponent<TerrainElevationTileClient>();
        if (groundResolver == null)
            groundResolver = FindObjectOfType<GroundSurfaceResolver>();
        if (trackedPose == null && Camera.main != null)
            trackedPose = Camera.main.transform;
        if (rulerOrientation == null && rulerObject != null)
            rulerOrientation = rulerObject.GetComponentInChildren<SurfaceAlignedBillboard>(true);
        if (floodPane == null)
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != "FloodPane")
                    continue;
                floodPane = transforms[i].gameObject;
                break;
            }
        }
    }

    private void SubscribeLocationResolver()
    {
        if (locationSubscribed || LocationResolver.Instance == null)
            return;
        LocationResolver.Instance.CalibrationChanged += HandleCalibrationChanged;
        locationSubscribed = true;
    }

    private void UnsubscribeLocationResolver()
    {
        if (!locationSubscribed)
            return;
        if (LocationResolver.Instance != null)
            LocationResolver.Instance.CalibrationChanged -= HandleCalibrationChanged;
        locationSubscribed = false;
    }

    private void ResetRequestsAndPane()
    {
        requestGeneration++;
        CancelActiveRequest();
        hasRequested = false;
        ClearDepthAndPane();
        currentElevation = null;
    }

    private void ClearDepthAndPane()
    {
        hasDepth = false;
        currentDepthMeters = 0f;
        HidePaneForMissingReference();
    }

    private static float GetAdjustedFloodHeight(float predictedHeightMeters)
    {
        return Mathf.Max(0f, predictedHeightMeters - FloodSurfaceAdjustmentMeters);
    }

    private void HidePaneForMissingReference()
    {
        hasPositionedPane = false;
        isTransitioning = false;
        SetPaneActive(false);
        SetPaneReady(false);
    }

    private void CancelActiveRequest()
    {
        if (activeRequest != null)
        {
            StopCoroutine(activeRequest);
            activeRequest = null;
        }
        requestInFlight = false;
    }

    private void SetPaneY(float y)
    {
        Vector3 position = floodPane.transform.position;
        position.y = y;
        floodPane.transform.position = position;
    }

    private void SetRulerReferenceY(float referenceY)
    {
        if (rulerObject == null || rulerObject == floodPane)
            return;

        float zeroY;
        if (rulerZeroPoint != null && rulerZeroPoint.IsChildOf(rulerObject.transform))
        {
            zeroY = rulerZeroPoint.position.y;
        }
        else
        {
            Renderer[] renderers = rulerObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                zeroY = rulerObject.transform.position.y;
            else
            {
                zeroY = renderers[0].bounds.min.y;
                for (int i = 1; i < renderers.Length; i++)
                    zeroY = Mathf.Min(zeroY, renderers[i].bounds.min.y);
            }
        }

        Vector3 rulerPosition = rulerObject.transform.position;
        rulerPosition.y += referenceY + rulerZeroHeightOffsetMeters - zeroY;
        rulerObject.transform.position = rulerPosition;
    }

    private void SetPaneActive(bool active)
    {
        if (floodPane != null && floodPane.activeSelf != active)
            floodPane.SetActive(active);
        if (rulerObject != null && rulerObject.activeSelf != active)
            rulerObject.SetActive(active);
    }

    private void SetPaneReady(bool ready)
    {
        if (paneReady == ready)
            return;
        paneReady = ready;
        NotifyStatusChanged();
    }

    private void SetResult(FloodDepthResult result)
    {
        currentResult = result;
        NotifyStatusChanged();
    }

    private void NotifyStatusChanged()
    {
        StatusChanged?.Invoke();
    }
}
