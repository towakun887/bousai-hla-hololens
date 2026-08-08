using System.Collections.Generic;
using UnityEngine;

public class MarkerManager : MonoBehaviour
{
    public static MarkerManager Instance { get; private set; }

    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private CsvMarkerParser csvParser;

    [Header("LOD")]
    [SerializeField, Min(0.02f)] private float lodInterval = 0.1f;
    [SerializeField, Min(0.1f)] private float fullEnterDistance = 95f;
    [SerializeField, Min(0.1f)] private float fullExitDistance = 105f;
    [SerializeField, Min(0.1f)] private float visibleEnterDistance = 290f;
    [SerializeField, Min(0.1f)] private float visibleExitDistance = 310f;

    private sealed class MarkerRuntime
    {
        public MarkerData Data;
        public Vector3 Position;
        public MarkerVisual Visual;
        public MarkerVisual.LodState Lod = MarkerVisual.LodState.Hidden;
    }

    private readonly List<MarkerRuntime> markers = new List<MarkerRuntime>();
    private readonly List<MarkerVisual> activeMarkers = new List<MarkerVisual>();
    private readonly Stack<MarkerVisual> pool = new Stack<MarkerVisual>();
    private Camera mainCamera;
    private float nextLodTime;
    private bool isDebugMode;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            mainCamera = Camera.main;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (csvParser != null)
        {
            csvParser.OnCsvParsed += HandleCsvParsed;
        }
    }

    private void OnDestroy()
    {
        if (csvParser != null)
        {
            csvParser.OnCsvParsed -= HandleCsvParsed;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        Transform cameraTransform = mainCamera.transform;
        for (int i = 0; i < activeMarkers.Count; i++)
        {
            MarkerVisual visual = activeMarkers[i];
            if (visual != null)
            {
                visual.FaceCamera(cameraTransform);
            }
        }

        if (Time.unscaledTime < nextLodTime)
        {
            return;
        }
        nextLodTime = Time.unscaledTime + lodInterval;
        EvaluateLods(cameraTransform.position);
    }

    public void GenerateMarkers()
    {
        if (csvParser != null)
        {
            csvParser.LoadCsv();
        }
    }

    private void HandleCsvParsed(List<MarkerData> parsedMarkers)
    {
        ClearMarkers();
        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            Debug.LogWarning("LocationResolver is not calibrated yet.");
            return;
        }

        for (int i = 0; i < parsedMarkers.Count; i++)
        {
            MarkerData data = parsedMarkers[i];
            markers.Add(new MarkerRuntime
            {
                Data = data,
                Position = ResolvePosition(data)
            });
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        if (mainCamera != null)
        {
            EvaluateLods(mainCamera.transform.position);
        }
    }

    private void EvaluateLods(Vector3 cameraPosition)
    {
        float fullEnterSqr = fullEnterDistance * fullEnterDistance;
        float fullExitSqr = fullExitDistance * fullExitDistance;
        float visibleEnterSqr = visibleEnterDistance * visibleEnterDistance;
        float visibleExitSqr = visibleExitDistance * visibleExitDistance;

        for (int i = 0; i < markers.Count; i++)
        {
            MarkerRuntime runtime = markers[i];
            float distanceSqr = (runtime.Position - cameraPosition).sqrMagnitude;
            MarkerVisual.LodState next = DetermineLod(
                runtime.Lod, distanceSqr, fullEnterSqr, fullExitSqr, visibleEnterSqr, visibleExitSqr);

            if (next != runtime.Lod)
            {
                ApplyLod(runtime, next);
            }

            if (runtime.Visual != null)
            {
                runtime.Visual.UpdateDistanceAndScale(Mathf.Sqrt(distanceSqr));
                runtime.Visual.UpdateDebugPosition(runtime.Position);
            }
        }
    }

    public static MarkerVisual.LodState DetermineLod(
        MarkerVisual.LodState current,
        float distanceSqr,
        float fullEnterSqr = 95f * 95f,
        float fullExitSqr = 105f * 105f,
        float visibleEnterSqr = 290f * 290f,
        float visibleExitSqr = 310f * 310f)
    {
        switch (current)
        {
            case MarkerVisual.LodState.Hidden:
                if (distanceSqr <= fullEnterSqr) return MarkerVisual.LodState.Full;
                if (distanceSqr <= visibleEnterSqr) return MarkerVisual.LodState.Text;
                return current;
            case MarkerVisual.LodState.Text:
                if (distanceSqr <= fullEnterSqr) return MarkerVisual.LodState.Full;
                if (distanceSqr > visibleExitSqr) return MarkerVisual.LodState.Hidden;
                return current;
            case MarkerVisual.LodState.Full:
                if (distanceSqr > visibleExitSqr) return MarkerVisual.LodState.Hidden;
                if (distanceSqr > fullExitSqr) return MarkerVisual.LodState.Text;
                return current;
            default:
                return MarkerVisual.LodState.Hidden;
        }
    }

    private void ApplyLod(MarkerRuntime runtime, MarkerVisual.LodState next)
    {
        if (next == MarkerVisual.LodState.Hidden)
        {
            ReturnToPool(runtime);
            runtime.Lod = next;
            return;
        }

        if (runtime.Visual == null)
        {
            runtime.Visual = GetFromPool(runtime.Position);
            if (runtime.Visual == null)
            {
                return;
            }
            runtime.Visual.Setup(runtime.Data);
            runtime.Visual.SetDebugMode(isDebugMode);
            activeMarkers.Add(runtime.Visual);
        }

        runtime.Visual.transform.position = runtime.Position;
        runtime.Visual.SetLod(next);
        runtime.Lod = next;
    }

    private MarkerVisual GetFromPool(Vector3 position)
    {
        MarkerVisual visual = null;
        while (pool.Count > 0 && visual == null)
        {
            visual = pool.Pop();
        }

        if (visual == null)
        {
            if (markerPrefab == null)
            {
                return null;
            }
            GameObject markerObject = Instantiate(markerPrefab, position, Quaternion.identity, transform);
            visual = markerObject.GetComponent<MarkerVisual>();
            if (visual == null)
            {
                Destroy(markerObject);
                return null;
            }
        }
        else
        {
            visual.transform.SetPositionAndRotation(position, Quaternion.identity);
            visual.gameObject.SetActive(true);
        }
        return visual;
    }

    private void ReturnToPool(MarkerRuntime runtime)
    {
        if (runtime.Visual == null)
        {
            return;
        }
        runtime.Visual.PrepareForPool();
        activeMarkers.Remove(runtime.Visual);
        runtime.Visual.gameObject.SetActive(false);
        pool.Push(runtime.Visual);
        runtime.Visual = null;
    }

    public void ClearMarkers()
    {
        for (int i = 0; i < markers.Count; i++)
        {
            ReturnToPool(markers[i]);
        }
        markers.Clear();
        activeMarkers.Clear();
    }

    public void SetDebugMode(bool debug)
    {
        isDebugMode = debug;
        for (int i = 0; i < activeMarkers.Count; i++)
        {
            if (activeMarkers[i] != null)
            {
                activeMarkers[i].SetDebugMode(debug);
            }
        }
    }

    public List<MarkerVisual> GetMarkers()
    {
        return activeMarkers;
    }

    public void UpdateMarkerPositions()
    {
        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            return;
        }

        for (int i = 0; i < markers.Count; i++)
        {
            MarkerRuntime runtime = markers[i];
            runtime.Position = ResolvePosition(runtime.Data);
            if (runtime.Visual != null)
            {
                runtime.Visual.transform.position = runtime.Position;
            }
        }
        nextLodTime = 0f;
    }

    private static Vector3 ResolvePosition(MarkerData data)
    {
        return LocationResolver.Instance.GpsToUnityPosition(data.latitude, data.longitude, data.altitude);
    }
}
