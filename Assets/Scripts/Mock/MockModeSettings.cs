using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-1000)]
public class MockModeSettings : MonoBehaviour
{
    public static MockModeSettings Instance { get; private set; }

    public enum MarkerMockCount
    {
        Zero = 0,
        Ten = 10,
        Thirty = 30,
        OneHundred = 100
    }

    [Header("Location Mock: Fixed Coordinates (no QR / no TCP)")]
    [Tooltip("入力座標を現在のSLAM位置へ直接割り当て、QR検出と位置情報通信を行いません。接続情報Mockと同時ONの場合はこちらを優先します。")]
    [SerializeField] private bool useFixedLocationMock;
    [SerializeField] private double fixedLatitude = 35.6895d;
    [SerializeField] private double fixedLongitude = 139.6917d;
    [SerializeField] private double fixedAltitude;
    [Tooltip("Mock開始時にHoloLensが向いている方角です。0=北、90=東。")]
    [SerializeField, Range(0f, 360f)] private float fixedHeadingDegrees;

    [Header("Location Mock: Direct Connection (no QR)")]
    [Tooltip("QRを検出せず、入力した接続情報を使ってTcpLocationClientから実際の位置情報を取得します。")]
    [SerializeField] private bool useDirectConnectionMock;
    [SerializeField] private string connectionIp = "127.0.0.1";
    [SerializeField] private int connectionPort = 5000;
    [SerializeField] private string connectionDeviceId = "MockDevice";
    [FormerlySerializedAs("ignoreAccuracyAndContinuouslyUpdateOrigin")]
    [InspectorName("位置精度を無視")]
    [Tooltip("受信した有効な位置情報をaccuracyとisTrackingの値に関係なく使用します。")]
    [SerializeField] private bool ignoreLocationAccuracy;
    [InspectorName("キャリブレーション後もOriginを継続更新")]
    [Tooltip("キャリブレーション完了後も接続を維持し、受信した位置情報でOriginを更新し続けます。")]
    [SerializeField] private bool continuouslyUpdateOrigin;

    [Header("CSV Marker Mock")]
    [SerializeField] private bool useCsvMarkerMock;
    [SerializeField] private MarkerMockCount markerCount = MarkerMockCount.Thirty;
    [SerializeField] private bool markerMockIncludesPhotos;
    [SerializeField] private string markerMockImageFileId;

    [Header("Flood Depth Mock")]
    [SerializeField] private bool useFloodDepthMock;
    [SerializeField, Min(0f)] private float floodDepthMeters = 1f;

    public bool UseFixedLocationMock => useFixedLocationMock;
    public bool UseDirectConnectionMock => useDirectConnectionMock && !useFixedLocationMock;
    public bool HasAnyLocationMock => useFixedLocationMock || useDirectConnectionMock;
    public double FixedLatitude => fixedLatitude;
    public double FixedLongitude => fixedLongitude;
    public double FixedAltitude => fixedAltitude;
    public float FixedHeadingDegrees => fixedHeadingDegrees;
    public string ConnectionIp => connectionIp;
    public int ConnectionPort => connectionPort;
    public string ConnectionDeviceId => connectionDeviceId;
    public bool IgnoreLocationAccuracy => UseDirectConnectionMock && ignoreLocationAccuracy;
    public bool ContinuouslyUpdateOrigin => UseDirectConnectionMock && continuouslyUpdateOrigin;
    public bool UseCsvMarkerMock => useCsvMarkerMock;
    public int MarkerCount => (int)markerCount;
    public bool MarkerMockIncludesPhotos => markerMockIncludesPhotos;
    public string MarkerMockImageFileId => markerMockImageFileId;
    public bool UseFloodDepthMock => useFloodDepthMock;
    public float FloodDepthMeters => Mathf.Max(0f, floodDepthMeters);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        if (Instance != this)
        {
            Debug.LogWarning("[MockModeSettings] 複数のComponentが存在するため、後から読み込まれた方を無効化します。");
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public float GetNorthYRotationOffset(Transform referenceTransform)
    {
        Vector3 forward = referenceTransform != null ? referenceTransform.forward : Vector3.forward;
        return GetNorthYRotationOffset(forward);
    }

    public float GetNorthYRotationOffset(Vector3 referenceForward)
    {
        Vector3 forward = referenceForward;
        forward.y = 0f;
        float slamYaw = forward.sqrMagnitude > 0.000001f
            ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
            : 0f;
        return Mathf.DeltaAngle(0f, slamYaw - fixedHeadingDegrees);
    }

    private void OnValidate()
    {
        connectionPort = Mathf.Clamp(connectionPort, 1, 65535);
        floodDepthMeters = Mathf.Max(0f, floodDepthMeters);
    }
}
