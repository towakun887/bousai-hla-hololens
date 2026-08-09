using UnityEngine;
using TMPro;

public class DebugOverlay : MonoBehaviour
{
    public static DebugOverlay Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI originText;
    
    private bool isDebugMode = false;
    private float nextUpdateTime;
    private string lastText;
    private Transform trackedPose;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        bool debug = PlayerPrefs.GetInt("DebugMode", 0) == 1;
        SetDebugMode(debug);
    }

    public void SetDebugMode(bool debug)
    {
        isDebugMode = debug;
        PlayerPrefs.SetInt("DebugMode", debug ? 1 : 0);
        PlayerPrefs.Save();
        
        if (originText != null) originText.gameObject.SetActive(debug);
        
        if (MarkerManager.Instance != null)
        {
            MarkerManager.Instance.SetDebugMode(debug);
        }
        
        if (SlamPlaneVisualizer.Instance != null)
        {
            SlamPlaneVisualizer.Instance.SetVisible(debug);
        }
    }

    private void Update()
    {
        if (!isDebugMode || Time.unscaledTime < nextUpdateTime) return;
        nextUpdateTime = Time.unscaledTime + 0.5f;
        if (originText == null)
            return;

        if (trackedPose == null && Camera.main != null)
            trackedPose = Camera.main.transform;

        string currentSlam = trackedPose != null
            ? $"Current SLAM: {trackedPose.position.x:F2}, {trackedPose.position.y:F2}, {trackedPose.position.z:F2}m"
            : "Current SLAM: N/A";
        string value = $"{currentSlam}\nOrigin: N/A";
        if (LocationResolver.Instance != null && LocationResolver.Instance.IsCalibrated())
        {
            var gps = LocationResolver.Instance.GetOriginGps();
            var unity = LocationResolver.Instance.GetOriginUnity();
            var heading = LocationResolver.Instance.GetNorthYRotationOffset();
            value = $"{currentSlam}\n" +
                    $"Origin GPS: {gps.latitude:F6}, {gps.longitude:F6}, {gps.altitude:F1}m\n" +
                    $"Origin SLAM: {unity.x:F2}, {unity.y:F2}, {unity.z:F2}\n" +
                    $"Estimated North Heading: {heading:F1}°";
        }

        if (lastText != value)
        {
            lastText = value;
            originText.text = value;
        }
    }
}
