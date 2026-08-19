using System;
using System.Collections;
using Bousai.Flood;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Resolves the application's ground independently of the flood display and
/// maintains a collider on the resolved horizontal surface.
/// </summary>
[DisallowMultipleComponent]
public class GroundSurfaceResolver : MonoBehaviour
{
    public static GroundSurfaceResolver Instance { get; private set; }

    public event Action GroundChanged;

    [Header("References")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private TerrainElevationTileClient elevationClient;
    [SerializeField] private Transform observer;

    [Header("SLAM floor selection")]
    [SerializeField, Min(0f)] private float maximumHorizontalDistance = 2f;
    [SerializeField, Min(0f)] private float minimumVerticalDrop = 0.2f;
    [SerializeField, Min(0f)] private float maximumVerticalDrop = 4f;
    [SerializeField, Min(0f)] private float expectedEyeHeight = 1.6f;

    [Header("Ground collider")]
    [Tooltip("地面確定時に有効化して位置を合わせる、作成済みの Collider オブジェクトです。")]
    [SerializeField] private GameObject groundColliderObject;
    [Tooltip("地面の World Y に加える、Collider オブジェクトの Pivot 補正値です。")]
    [SerializeField] private float groundColliderHeightOffset;
    [Tooltip("ON の場合、Collider の XZ 位置を観測者へ追従させます。")]
    [SerializeField] private bool followObserverHorizontally = true;
    [SerializeField, Min(0.02f)] private float updateIntervalSeconds = 0.1f;

    [Header("Terrain elevation refresh")]
    [SerializeField, Min(0f)] private float minimumElevationRefreshSeconds = 10f;
    [SerializeField, Min(0f)] private float minimumElevationRefreshDistance = 5f;

    private FloodHeightReferenceMode mode;
    private float currentGroundY;
    private bool hasGround;
    private float nextUpdateTime;
    private float lastElevationRequestTime = float.NegativeInfinity;
    private Vector3 lastElevationRequestPosition;
    private bool elevationRequestInFlight;
    private int elevationRequestGeneration;
    private static bool sceneLoadHookInstalled;

    public FloodHeightReferenceMode Mode => mode;
    public bool HasGround => hasGround;
    public float CurrentGroundY => currentGroundY;
    public Collider GroundCollider => groundColliderObject != null
        ? groundColliderObject.GetComponent<Collider>()
        : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (!sceneLoadHookInstalled)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneLoadHookInstalled = true;
        }

        if (FindFirstObjectByType<GroundSurfaceResolver>() != null)
            return;

        new GameObject(nameof(GroundSurfaceResolver)).AddComponent<GroundSurfaceResolver>();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsureInstanceExists();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        mode = (FloodHeightReferenceMode)Mathf.Clamp(
            PlayerPrefs.GetInt(FloodDepthController.HeightReferenceModePreferenceKey, 0), 0, 1);
        ResolveReferences();
        SetColliderActive(false);
    }

    private void OnDestroy()
    {
        elevationRequestGeneration++;
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextUpdateTime)
            return;

        nextUpdateTime = Time.unscaledTime + updateIntervalSeconds;
        ResolveReferences();

        if (observer == null)
        {
            SetGroundUnavailable();
            return;
        }

        if (mode == FloodHeightReferenceMode.GroundSurface)
        {
            if (TryResolveSlamGround(observer.position, out float slamGroundY))
                SetGround(slamGroundY);
            else
                SetGroundUnavailable();
            return;
        }

        UpdateTerrainElevationGround();
    }

    public void SetMode(FloodHeightReferenceMode value)
    {
        if (mode == value)
            return;

        mode = value;
        PlayerPrefs.SetInt(FloodDepthController.HeightReferenceModePreferenceKey, (int)value);
        PlayerPrefs.Save();
        elevationRequestGeneration++;
        elevationRequestInFlight = false;
        lastElevationRequestTime = float.NegativeInfinity;
        SetGroundUnavailable();
        nextUpdateTime = 0f;
    }

    public bool TryGetGroundY(Vector3 observerPosition, out float groundY)
    {
        if (mode == FloodHeightReferenceMode.GroundSurface &&
            TryResolveSlamGround(observerPosition, out groundY))
        {
            SetGround(groundY);
            return true;
        }

        groundY = currentGroundY;
        return hasGround;
    }

    private bool TryResolveSlamGround(Vector3 observerPosition, out float groundY)
    {
        groundY = 0f;
        if (planeManager == null)
            return false;

        bool found = false;
        float bestScore = float.PositiveInfinity;
        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane == null || plane.trackingState != TrackingState.Tracking ||
                plane.classification != PlaneClassification.Floor)
                continue;

            Ray ray = new Ray(observerPosition + Vector3.up * 0.05f, Vector3.down);
            if (!plane.infinitePlane.Raycast(ray, out float enter))
                continue;

            Vector3 hit = ray.GetPoint(enter);
            float verticalDrop = observerPosition.y - hit.y;
            if (verticalDrop < minimumVerticalDrop || verticalDrop > maximumVerticalDrop)
                continue;

            Vector3 local = plane.transform.InverseTransformPoint(hit);
            Vector2 halfSize = plane.size * 0.5f;
            float outsideX = Mathf.Max(0f, Mathf.Abs(local.x) - halfSize.x);
            float outsideZ = Mathf.Max(0f, Mathf.Abs(local.z) - halfSize.y);
            float horizontalDistance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
            if (horizontalDistance > maximumHorizontalDistance)
                continue;

            float score = horizontalDistance * 10f + Mathf.Abs(verticalDrop - expectedEyeHeight);
            if (score >= bestScore)
                continue;

            bestScore = score;
            groundY = hit.y;
            found = true;
        }

        return found;
    }

    private void UpdateTerrainElevationGround()
    {
        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            SetGroundUnavailable();
            return;
        }

        if (hasGround)
            UpdateCollider();

        if (elevationRequestInFlight || elevationClient == null ||
            !LocationResolver.Instance.TryUnityToGps(observer.position, out LocationData location))
            return;

        float elapsed = Time.unscaledTime - lastElevationRequestTime;
        float distance = Vector3.Distance(observer.position, lastElevationRequestPosition);
        if (hasGround && elapsed < minimumElevationRefreshSeconds &&
            distance < minimumElevationRefreshDistance)
            return;

        lastElevationRequestTime = Time.unscaledTime;
        lastElevationRequestPosition = observer.position;
        elevationRequestInFlight = true;
        int generation = ++elevationRequestGeneration;
        StartCoroutine(FetchTerrainElevation(location, generation));
    }

    private IEnumerator FetchTerrainElevation(LocationData location, int generation)
    {
        TerrainElevationResult result = null;
        yield return elevationClient.Fetch(location.latitude, location.longitude, value => result = value);

        if (generation != elevationRequestGeneration)
            yield break;

        elevationRequestInFlight = false;
        if (result == null || !result.HasElevation || LocationResolver.Instance == null ||
            !LocationResolver.Instance.IsCalibrated())
        {
            SetGroundUnavailable();
            yield break;
        }

        LocationData originGps = LocationResolver.Instance.GetOriginGps();
        Vector3 originUnity = LocationResolver.Instance.GetOriginUnity();
        SetGround(TerrainElevationMath.ToUnityY(
            originUnity.y, originGps.altitude, result.ElevationMeters, 0f));
    }

    private void SetGround(float groundY)
    {
        bool changed = !hasGround || !Mathf.Approximately(currentGroundY, groundY);
        hasGround = true;
        currentGroundY = groundY;
        UpdateCollider();
        if (changed)
            GroundChanged?.Invoke();
    }

    private void SetGroundUnavailable()
    {
        if (!hasGround)
            return;

        hasGround = false;
        SetColliderActive(false);
        GroundChanged?.Invoke();
    }

    private void UpdateCollider()
    {
        if (groundColliderObject == null)
            return;

        Vector3 position = groundColliderObject.transform.position;
        if (followObserverHorizontally && observer != null)
        {
            position.x = observer.position.x;
            position.z = observer.position.z;
        }
        position.y = currentGroundY + groundColliderHeightOffset;
        groundColliderObject.transform.position = position;
        SetColliderActive(true);
    }

    private void SetColliderActive(bool active)
    {
        if (groundColliderObject != null && groundColliderObject.activeSelf != active)
            groundColliderObject.SetActive(active);
    }

    private void ResolveReferences()
    {
        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (observer == null && Camera.main != null)
            observer = Camera.main.transform;
        if (elevationClient == null)
            elevationClient = GetComponent<TerrainElevationTileClient>();
        if (elevationClient == null)
            elevationClient = gameObject.AddComponent<TerrainElevationTileClient>();
    }
}
