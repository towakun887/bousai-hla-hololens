using System;
using System.Collections;
using Bousai.Dosha;
using Bousai.Flood;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class DoshaTileClient : MonoBehaviour
{
    public event Action StatusChanged;

    [Header("土砂災害タイル URL")]
    [SerializeField] private string debrisFlowTileUrlTemplate =
        "https://disaportaldata.gsi.go.jp/raster/05_dosekiryukeikaikuiki/{z}/{x}/{y}.png";
    [SerializeField] private string steepSlopeCollapseTileUrlTemplate =
        "https://disaportaldata.gsi.go.jp/raster/05_kyukeishakeikaikuiki/{z}/{x}/{y}.png";
    [SerializeField] private string landslideTileUrlTemplate =
        "https://disaportaldata.gsi.go.jp/raster/05_jisuberikeikaikuiki/{z}/{x}/{y}.png";

    [Header("取得設定")]
    [SerializeField, Range(2, 17)] private int zoomLevel = 17;
    [SerializeField, Range(0, 32)] private int colorTolerance = 9;
    [SerializeField, Min(1f)] private float requestTimeoutSeconds = 10f;

    [Header("自動更新")]
    [SerializeField] private Transform trackedPose;
    [SerializeField, Min(0f)] private float minimumRefreshSeconds = 2f;
    [SerializeField, Min(0f)] private float minimumRefreshDistanceMeters = 2f;

    [Header("区域に該当したときのイベント")]
    [SerializeField] private UnityEvent onWarningZone = new UnityEvent();
    [SerializeField] private UnityEvent onSpecialWarningZone = new UnityEvent();

    private readonly LayerCache debrisFlowCache = new LayerCache();
    private readonly LayerCache steepSlopeCollapseCache = new LayerCache();
    private readonly LayerCache landslideCache = new LayerCache();

    private bool locationSubscribed;
    private bool requestInFlight;
    private bool hasRequested;
    private float lastRequestTime;
    private Vector3 lastRequestPosition;
    private int requestGeneration;
    private Coroutine activeRequest;
    private MockModeSettings.DoshaHazardMockLevel lastMockLevel;
    private DoshaHazardResult currentResult;
    private string diagnosticStatus = "初期化待ち";

    public DoshaHazardResult CurrentResult => currentResult;
    public string DiagnosticStatus => diagnosticStatus;

    private void Start()
    {
        ResolveReferences();
        SubscribeLocationResolver();
        ResetRequestState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeLocationResolver();
    }

    private void OnDisable()
    {
        CancelActiveRequest();
        UnsubscribeLocationResolver();
    }

    private void Update()
    {
        ResolveReferences();
        SubscribeLocationResolver();

        MockModeSettings.DoshaHazardMockLevel mockLevel = MockModeSettings.Instance != null
            ? MockModeSettings.Instance.DoshaHazardMock
            : MockModeSettings.DoshaHazardMockLevel.None;
        if (mockLevel != lastMockLevel)
        {
            lastMockLevel = mockLevel;
            ResetRequestState();
        }

        if (requestInFlight)
            return;

        if (trackedPose == null)
        {
            SetDiagnosticStatus("Main Camera待ち");
            return;
        }
        if (GroundSurfaceResolver.Instance == null)
        {
            SetDiagnosticStatus("GroundSurfaceResolver待ち");
            return;
        }
        if (!GroundSurfaceResolver.Instance.HasGround)
        {
            SetDiagnosticStatus("Ground Plane確定待ち");
            return;
        }
        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            SetDiagnosticStatus("位置基準の確定待ち");
            return;
        }

        bool useMock = MockModeSettings.Instance != null && MockModeSettings.Instance.UseDoshaHazardMock;
        if (!hasRequested || (!useMock && FloodTileMath.ShouldRefresh(
                Time.unscaledTime - lastRequestTime,
                lastRequestPosition,
                trackedPose.position,
                minimumRefreshSeconds,
                minimumRefreshDistanceMeters)))
        {
            BeginAutomaticRequest();
        }
    }

    private void BeginAutomaticRequest()
    {
        if (!LocationResolver.Instance.TryUnityToGps(trackedPose.position, out LocationData location))
            return;

        requestInFlight = true;
        hasRequested = true;
        SetDiagnosticStatus("土砂災害情報を取得中");
        lastRequestTime = Time.unscaledTime;
        lastRequestPosition = trackedPose.position;
        int generation = ++requestGeneration;
        activeRequest = StartCoroutine(FetchAutomatically(location, generation));
    }

    private IEnumerator FetchAutomatically(LocationData location, int generation)
    {
        yield return Fetch(location.latitude, location.longitude);

        if (generation != requestGeneration)
            yield break;

        activeRequest = null;
        requestInFlight = false;
    }

    private void HandleCalibrationChanged()
    {
        ResetRequestState();
    }

    private void ResetRequestState()
    {
        CancelActiveRequest();
        hasRequested = false;
        currentResult = null;
        SetDiagnosticStatus("再判定待ち");
    }

    private void CancelActiveRequest()
    {
        requestGeneration++;
        if (activeRequest != null)
        {
            StopCoroutine(activeRequest);
            activeRequest = null;
        }
        requestInFlight = false;
    }

    private void ResolveReferences()
    {
        if (trackedPose == null && Camera.main != null)
            trackedPose = Camera.main.transform;
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

    private void SetDiagnosticStatus(string value)
    {
        value = value ?? string.Empty;
        if (diagnosticStatus == value)
            return;
        diagnosticStatus = value;
        StatusChanged?.Invoke();
    }

    public IEnumerator Fetch(double latitude, double longitude, Action<DoshaHazardResult> completed = null)
    {
        MockModeSettings mockSettings = MockModeSettings.Instance;
        if (mockSettings != null && mockSettings.UseDoshaHazardMock)
        {
            Complete(CreateMockResult(mockSettings.DoshaHazardMock, latitude, longitude), completed);
            yield break;
        }

        FloodTileCoordinate coordinate;
        try
        {
            coordinate = FloodTileMath.FromLatitudeLongitude(latitude, longitude, zoomLevel);
        }
        catch (Exception exception)
        {
            Complete(new DoshaHazardResult(
                DoshaHazardStatus.NetworkError,
                $"座標変換失敗: {exception.Message}",
                latitude,
                longitude,
                DoshaHazardType.None,
                DoshaHazardType.None), completed);
            yield break;
        }

        LayerResult debrisFlow = default;
        yield return FetchLayer(
            debrisFlowTileUrlTemplate, DoshaHazardType.DebrisFlow,
            debrisFlowCache, coordinate, result => debrisFlow = result);

        LayerResult steepSlopeCollapse = default;
        yield return FetchLayer(
            steepSlopeCollapseTileUrlTemplate, DoshaHazardType.SteepSlopeCollapse,
            steepSlopeCollapseCache, coordinate, result => steepSlopeCollapse = result);

        LayerResult landslide = default;
        yield return FetchLayer(
            landslideTileUrlTemplate, DoshaHazardType.Landslide,
            landslideCache, coordinate, result => landslide = result);

        LayerResult[] results = { debrisFlow, steepSlopeCollapse, landslide };
        DoshaHazardType warningTypes = DoshaHazardType.None;
        DoshaHazardType specialWarningTypes = DoshaHazardType.None;
        int failedCount = 0;
        string errors = string.Empty;

        for (int i = 0; i < results.Length; i++)
        {
            LayerResult result = results[i];
            if (!result.RequestSucceeded)
            {
                failedCount++;
                if (!string.IsNullOrEmpty(result.Error))
                    errors += (errors.Length == 0 ? string.Empty : " / ") + result.Error;
                continue;
            }

            if ((result.Zone & DoshaHazardZone.Warning) != 0)
                warningTypes |= result.Type;
            if ((result.Zone & DoshaHazardZone.SpecialWarning) != 0)
                specialWarningTypes |= result.Type;
        }

        DoshaHazardStatus status;
        if (failedCount == results.Length)
            status = DoshaHazardStatus.NetworkError;
        else if (failedCount > 0)
            status = DoshaHazardStatus.Partial;
        else if (warningTypes == DoshaHazardType.None && specialWarningTypes == DoshaHazardType.None)
            status = DoshaHazardStatus.NoData;
        else
            status = DoshaHazardStatus.Available;

        DoshaHazardResult aggregate = new DoshaHazardResult(
            status,
            failedCount > 0 ? errors : BuildMessage(warningTypes, specialWarningTypes),
            latitude,
            longitude,
            warningTypes,
            specialWarningTypes);

        Complete(aggregate, completed);
    }

    private void Complete(DoshaHazardResult result, Action<DoshaHazardResult> completed)
    {
        currentResult = result;
        diagnosticStatus = result != null ? result.Message : "取得結果なし";
        StatusChanged?.Invoke();

        // 複数災害種別に該当しても、各イベントは1回だけ発火する。
        // 警戒区域と特別警戒区域が重なる場合は両方を発火する。
        if (result.HasWarningZone)
            onWarningZone?.Invoke();
        if (result.HasSpecialWarningZone)
            onSpecialWarningZone?.Invoke();

        completed?.Invoke(result);
    }

    private static DoshaHazardResult CreateMockResult(
        MockModeSettings.DoshaHazardMockLevel mockLevel,
        double latitude,
        double longitude)
    {
        DoshaHazardType allTypes =
            DoshaHazardType.DebrisFlow |
            DoshaHazardType.SteepSlopeCollapse |
            DoshaHazardType.Landslide;
        DoshaHazardType warningTypes = mockLevel == MockModeSettings.DoshaHazardMockLevel.Level1WarningZone
            ? allTypes
            : DoshaHazardType.None;
        DoshaHazardType specialWarningTypes = mockLevel == MockModeSettings.DoshaHazardMockLevel.Level2SpecialWarningZone
            ? allTypes
            : DoshaHazardType.None;

        return new DoshaHazardResult(
            DoshaHazardStatus.Mock,
            specialWarningTypes != DoshaHazardType.None
                ? "土砂災害特別警戒区域 (Mock)"
                : "土砂災害警戒区域 (Mock)",
            latitude,
            longitude,
            warningTypes,
            specialWarningTypes);
    }

    private IEnumerator FetchLayer(
        string urlTemplate,
        DoshaHazardType type,
        LayerCache cache,
        FloodTileCoordinate coordinate,
        Action<LayerResult> completed)
    {
        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            completed(new LayerResult(type, false, DoshaHazardZone.None, "URLが未設定です"));
            yield break;
        }

        if (cache.CanRead(urlTemplate, coordinate))
        {
            completed(ReadCachedPixel(type, cache, coordinate));
            yield break;
        }

        string url = urlTemplate
            .Replace("{z}", coordinate.Zoom.ToString())
            .Replace("{x}", coordinate.X.ToString())
            .Replace("{y}", coordinate.Y.ToString());

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, false))
        {
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // 全国タイルの空白域では404になる。これは通信障害ではなく区域なしとして扱う。
                if (request.responseCode == 404)
                {
                    completed(new LayerResult(type, true, DoshaHazardZone.None, string.Empty));
                    yield break;
                }

                completed(new LayerResult(
                    type, false, DoshaHazardZone.None,
                    $"{GetTypeLabel(type)}取得失敗: {request.error}"));
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                completed(new LayerResult(
                    type, false, DoshaHazardZone.None,
                    $"{GetTypeLabel(type)}のPNGを展開できませんでした"));
                yield break;
            }

            try
            {
                cache.Store(urlTemplate, coordinate, texture);
            }
            catch (Exception exception)
            {
                cache.Clear();
                completed(new LayerResult(
                    type, false, DoshaHazardZone.None,
                    $"{GetTypeLabel(type)}のPNG読込失敗: {exception.Message}"));
                yield break;
            }
            finally
            {
                Destroy(texture);
            }
        }

        completed(ReadCachedPixel(type, cache, coordinate));
    }

    private LayerResult ReadCachedPixel(
        DoshaHazardType type,
        LayerCache cache,
        FloodTileCoordinate coordinate)
    {
        if (!cache.TryGetPixel(coordinate, out Color32 sampled))
        {
            return new LayerResult(
                type, false, DoshaHazardZone.None,
                $"{GetTypeLabel(type)}のPNG座標が範囲外です");
        }

        if (!DoshaHazardLegend.TryClassify(
                sampled, type, colorTolerance,
                out DoshaHazardZone zone, out _))
        {
            return new LayerResult(type, true, DoshaHazardZone.None, string.Empty);
        }

        return new LayerResult(type, true, zone, string.Empty);
    }

    private static string BuildMessage(
        DoshaHazardType warningTypes,
        DoshaHazardType specialWarningTypes)
    {
        if (warningTypes == DoshaHazardType.None && specialWarningTypes == DoshaHazardType.None)
            return "土砂災害警戒区域に該当しません";
        if (warningTypes != DoshaHazardType.None && specialWarningTypes != DoshaHazardType.None)
            return "土砂災害警戒区域・特別警戒区域に該当します";
        return specialWarningTypes != DoshaHazardType.None
            ? "土砂災害特別警戒区域に該当します"
            : "土砂災害警戒区域に該当します";
    }

    private static string GetTypeLabel(DoshaHazardType type)
    {
        switch (type)
        {
            case DoshaHazardType.DebrisFlow:
                return "土石流";
            case DoshaHazardType.SteepSlopeCollapse:
                return "急傾斜地の崩壊";
            case DoshaHazardType.Landslide:
                return "地すべり";
            default:
                return "土砂災害";
        }
    }

    private readonly struct LayerResult
    {
        public LayerResult(
            DoshaHazardType type,
            bool requestSucceeded,
            DoshaHazardZone zone,
            string error)
        {
            Type = type;
            RequestSucceeded = requestSucceeded;
            Zone = zone;
            Error = error ?? string.Empty;
        }

        public DoshaHazardType Type { get; }
        public bool RequestSucceeded { get; }
        public DoshaHazardZone Zone { get; }
        public string Error { get; }
    }

    private sealed class LayerCache
    {
        private string urlTemplate;
        private FloodTileCoordinate tile;
        private Color32[] pixels;
        private int width;
        private int height;

        public bool CanRead(string candidateUrlTemplate, FloodTileCoordinate coordinate)
        {
            return pixels != null &&
                   string.Equals(urlTemplate, candidateUrlTemplate, StringComparison.Ordinal) &&
                   coordinate.IsSameTile(tile);
        }

        public void Store(string sourceUrlTemplate, FloodTileCoordinate coordinate, Texture2D texture)
        {
            Color32[] readPixels = texture.GetPixels32();
            if (readPixels == null || readPixels.Length != texture.width * texture.height)
                throw new InvalidOperationException("PNGのピクセル数が不正です");

            urlTemplate = sourceUrlTemplate;
            tile = coordinate;
            pixels = readPixels;
            width = texture.width;
            height = texture.height;
        }

        public bool TryGetPixel(FloodTileCoordinate coordinate, out Color32 sampled)
        {
            sampled = default;
            if (pixels == null ||
                coordinate.PixelX < 0 || coordinate.PixelX >= width ||
                coordinate.PixelY < 0 || coordinate.PixelY >= height)
                return false;

            // Webタイルは左上原点、Texture2D.GetPixels32()は左下原点。
            int textureY = height - 1 - coordinate.PixelY;
            sampled = pixels[textureY * width + coordinate.PixelX];
            return true;
        }

        public void Clear()
        {
            urlTemplate = null;
            pixels = null;
            width = 0;
            height = 0;
        }
    }
}
