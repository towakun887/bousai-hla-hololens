using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class MarkerManager : MonoBehaviour
{
    public static MarkerManager Instance { get; private set; }

    [SerializeField, FormerlySerializedAs("markerPrefab")] private GameObject firstMarkerPrefab;
    [SerializeField] private GameObject secondMarkerPrefab;
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
        public int CsvIndex;
        public MarkerVisual.LodState Lod = MarkerVisual.LodState.Hidden;
    }

    private readonly List<MarkerRuntime> markers = new List<MarkerRuntime>();
    private readonly List<MarkerVisual> activeMarkers = new List<MarkerVisual>();
    private readonly Stack<MarkerVisual> firstPool = new Stack<MarkerVisual>();
    private readonly Stack<MarkerVisual> secondPool = new Stack<MarkerVisual>();
    private Camera mainCamera;
    private float nextLodTime;
    private bool isDebugMode;
    private int activeCsvIndex;

    public bool IsShowingSecondMarkers => activeCsvIndex == 1;

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

    public void OnMarkerSetToggleClicked()
    {
        SetActiveCsvIndex(activeCsvIndex == 0 ? 1 : 0);
    }

    public void OnShowFirstMarkersClicked()
    {
        SetActiveCsvIndex(0);
    }

    public void OnShowSecondMarkersClicked()
    {
        SetActiveCsvIndex(1);
    }

    private void SetActiveCsvIndex(int csvIndex)
    {
        csvIndex = Mathf.Clamp(csvIndex, 0, 1);
        if (activeCsvIndex == csvIndex)
        {
            return;
        }

        activeCsvIndex = csvIndex;
        for (int i = 0; i < markers.Count; i++)
        {
            MarkerRuntime runtime = markers[i];
            if (runtime.CsvIndex != activeCsvIndex)
            {
                ReturnToPool(runtime);
                runtime.Lod = MarkerVisual.LodState.Hidden;
            }
        }

        nextLodTime = 0f;
        if (mainCamera != null)
        {
            EvaluateLods(mainCamera.transform.position);
        }
    }

    private void HandleCsvParsed(List<MarkerData> firstMarkers, List<MarkerData> secondMarkers)
    {
        ClearMarkers();
        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            Debug.LogWarning("LocationResolver is not calibrated yet.");
            return;
        }

        AddMarkerRuntimes(firstMarkers, 0);
        AddMarkerRuntimes(secondMarkers, 1);

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        if (mainCamera != null)
        {
            EvaluateLods(mainCamera.transform.position);
        }
    }

    private void AddMarkerRuntimes(List<MarkerData> parsedMarkers, int csvIndex)
    {
        for (int i = 0; i < parsedMarkers.Count; i++)
        {
            MarkerData data = parsedMarkers[i];
            markers.Add(new MarkerRuntime
            {
                Data = data,
                Position = ResolvePosition(data),
                CsvIndex = csvIndex
            });
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
            if (runtime.CsvIndex != activeCsvIndex)
            {
                if (runtime.Visual != null || runtime.Lod != MarkerVisual.LodState.Hidden)
                {
                    ApplyLod(runtime, MarkerVisual.LodState.Hidden);
                }
                continue;
            }

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
            runtime.Visual = GetFromPool(runtime.Position, runtime.CsvIndex);
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

    private MarkerVisual GetFromPool(Vector3 position, int csvIndex)
    {
        Stack<MarkerVisual> pool = csvIndex == 0 ? firstPool : secondPool;
        GameObject markerPrefab = csvIndex == 0 ? firstMarkerPrefab : secondMarkerPrefab;
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
        Stack<MarkerVisual> pool = runtime.CsvIndex == 0 ? firstPool : secondPool;
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
