using UnityEngine;
using System.Collections.Generic;

public class MarkerManager : MonoBehaviour
{
    public static MarkerManager Instance { get; private set; }

    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private CsvMarkerParser csvParser;
    
    private List<MarkerVisual> instantiatedMarkers = new List<MarkerVisual>();
    private bool isDebugMode = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (csvParser != null)
        {
            csvParser.OnCsvParsed += HandleCsvParsed;
        }
    }

    public void GenerateMarkers()
    {
        if (csvParser != null)
        {
            csvParser.LoadCsv();
        }
    }

    private void HandleCsvParsed(List<MarkerData> markers)
    {
        ClearMarkers();

        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
        {
            Debug.LogWarning("LocationResolver is not calibrated yet.");
            return;
        }

        foreach (var data in markers)
        {
            if (markerPrefab == null) break;

            Vector3 unityPos = LocationResolver.Instance.GpsToUnityPosition(
                data.latitude, 
                data.longitude, 
                data.altitude
            );

            // 1000m以上離れているマーカーは非表示（生成スキップ）
            if (Camera.main != null)
            {
                float dist = Vector3.Distance(Camera.main.transform.position, unityPos);
                if (dist > 1000f) continue;
            }

            GameObject markerObj = Instantiate(markerPrefab, unityPos, Quaternion.identity, transform);
            MarkerVisual visual = markerObj.GetComponent<MarkerVisual>();
            
            if (visual != null)
            {
                visual.Setup(data);
                visual.SetDebugMode(isDebugMode);
                instantiatedMarkers.Add(visual);
            }
        }
    }

    public void ClearMarkers()
    {
        foreach (var m in instantiatedMarkers)
        {
            if (m != null) Destroy(m.gameObject);
        }
        instantiatedMarkers.Clear();
    }
    
    public void SetDebugMode(bool debug)
    {
        isDebugMode = debug;
        foreach (var m in instantiatedMarkers)
        {
            if (m != null) m.SetDebugMode(debug);
        }
    }

    public List<MarkerVisual> GetMarkers()
    {
        return instantiatedMarkers;
    }

    public void UpdateMarkerPositions()
    {
        if (LocationResolver.Instance == null || !LocationResolver.Instance.IsCalibrated())
            return;

        foreach (var m in instantiatedMarkers)
        {
            if (m == null || m.Data == null) continue;

            Vector3 unityPos = LocationResolver.Instance.GpsToUnityPosition(
                m.Data.latitude, 
                m.Data.longitude, 
                m.Data.altitude
            );

            m.transform.position = unityPos;
        }
    }
}
