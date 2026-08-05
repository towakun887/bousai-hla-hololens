using UnityEngine;
using System.Collections.Generic;

public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance { get; private set; }

    [SerializeField] private int minSamplesRequired = 5;
    [SerializeField] private int maxSamplesToCollect = 20;
    [SerializeField] private double accuracyThreshold = 3.0; // meters
    [SerializeField] private float timeoutSeconds = 60f;

    [Header("Mock Location Settings")]
    [SerializeField] private double mockLatitude = 35.6895;
    [SerializeField] private double mockLongitude = 139.6917;
    [SerializeField] private double mockAltitude = 0;

    public enum CalibrationState { WaitingForQR, Connecting, Collecting, Calibrated, Failed, DriftWarning }
    public CalibrationState CurrentState { get; private set; } = CalibrationState.WaitingForQR;

    private List<LocationData> collectedSamples = new List<LocationData>();
    private float calibrationStartTime;
    private bool forceCalibration = false;
    private Vector3 lastDetectedPhonePosition = Vector3.zero; // スマホの検出座標を保存する変数
    private Quaternion lastDetectedPhoneRotation = Quaternion.identity; // スマホの検出姿勢(回転)を保存する変数
    private bool isContinuousCalibrating = false;
    private bool isQRVisible = false;
    
    // Drift tracking
    private Vector3 lastCalibratedPos;
    private float timeSinceCalibrated;
    private float accumulatedDistance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (QRCodeTracker.Instance != null)
        {
            QRCodeTracker.Instance.OnQRCodeDetected += HandleQRDetected;
            QRCodeTracker.Instance.OnQRCodeLost += HandleQRLost;
        }

        if (TcpLocationClient.Instance != null)
        {
            TcpLocationClient.Instance.OnLocationReceived += HandleLocationReceived;
            TcpLocationClient.Instance.OnError += HandleTcpError;
        }
        
        calibrationStartTime = Time.time;
    }

    private void HandleQRDetected(string ip, int port, string deviceId, Vector3 pos, Quaternion rot)
    {
        isQRVisible = true;
        // 検出されたスマホの正確な3D座標と回転を一時保存
        lastDetectedPhonePosition = pos;
        lastDetectedPhoneRotation = rot;

        if (CurrentState == CalibrationState.WaitingForQR || CurrentState == CalibrationState.Failed || CurrentState == CalibrationState.DriftWarning)
        {
            bool isMock = QRCodeTracker.Instance != null && QRCodeTracker.Instance.IsMockMode;
            
            if (isMock)
            {
                CurrentState = CalibrationState.Collecting;
                
                LocationData mockData = new LocationData
                {
                    isTracking = true,
                    latitude = mockLatitude,
                    longitude = mockLongitude,
                    altitude = mockAltitude,
                    accuracy = 1.0,
                };

                // Calculate eunRotation so that North (Z) aligns with the current Camera forward
                Vector3 targetNorthInPhoneLocal = Quaternion.Inverse(rot) * Camera.main.transform.forward;
                targetNorthInPhoneLocal.y = 0; // flatten to horizontal plane
                
                if (targetNorthInPhoneLocal.sqrMagnitude > 0.001f)
                {
                    targetNorthInPhoneLocal.Normalize();
                    // We want: Quaternion.Inverse(phoneEarthRot) * Vector3.forward = targetNorthInPhoneLocal
                    Quaternion invPhoneEarthRot = Quaternion.LookRotation(targetNorthInPhoneLocal, Vector3.up);
                    Quaternion phoneEarthRot = Quaternion.Inverse(invPhoneEarthRot);
                    
                    mockData.eunRotationX = phoneEarthRot.x;
                    mockData.eunRotationY = phoneEarthRot.y;
                    mockData.eunRotationZ = phoneEarthRot.z;
                    mockData.eunRotationW = phoneEarthRot.w;
                }
                else
                {
                    mockData.eunRotationX = 0;
                    mockData.eunRotationY = 0;
                    mockData.eunRotationZ = 0;
                    mockData.eunRotationW = 1;
                }

                // Inject mock samples
                for (int i = 0; i < minSamplesRequired; i++)
                {
                    collectedSamples.Add(mockData);
                }

                CompleteCalibration();
                return;
            }

            calibrationStartTime = Time.time; // Reset timeout counter when QR is first found!
            CurrentState = CalibrationState.Connecting;
            TcpLocationClient.Instance.Connect(ip, port, deviceId);
            if (PhoneVisualizer.Instance != null)
            {
                PhoneVisualizer.Instance.UpdateTransform(pos, rot);
                PhoneVisualizer.Instance.UpdateStatus("接続中...", "GPS: 待機中", "サンプル: 0/" + minSamplesRequired);
            }
        }
        else if (CurrentState == CalibrationState.Connecting || CurrentState == CalibrationState.Collecting)
        {
            if (PhoneVisualizer.Instance != null)
            {
                PhoneVisualizer.Instance.UpdateTransform(pos, rot);
                PhoneVisualizer.Instance.SetLost(false);
            }
        }
        else if (CurrentState == CalibrationState.Calibrated)
        {
            bool isMock = QRCodeTracker.Instance != null && QRCodeTracker.Instance.IsMockMode;
            if (isMock) return; // MockModeでは再キャリブレーション処理を行わない

            if (!isContinuousCalibrating)
            {
                isContinuousCalibrating = true;
                collectedSamples.Clear();
                strictSamplesCount = 0;
                
                TcpLocationClient.Instance.Connect(ip, port, deviceId);
            }
            
            if (PhoneVisualizer.Instance != null)
            {
                PhoneVisualizer.Instance.UpdateTransform(pos, rot);
                PhoneVisualizer.Instance.SetLost(false);
                PhoneVisualizer.Instance.UpdateStatus("再キャリブレーション中...", "GPS: 待機中", $"最新サンプル: {collectedSamples.Count}/{minSamplesRequired}");
            }
        }
    }

    private void HandleQRLost()
    {
        isQRVisible = false;

        if (CurrentState == CalibrationState.Connecting || CurrentState == CalibrationState.Collecting)
        {
            if (PhoneVisualizer.Instance != null)
            {
                PhoneVisualizer.Instance.SetLost(true);
            }
        }
        else if (CurrentState == CalibrationState.Calibrated)
        {
            if (isContinuousCalibrating)
            {
                isContinuousCalibrating = false;
                
                if (PhoneVisualizer.Instance != null)
                {
                    PhoneVisualizer.Instance.SetLost(true);
                    PhoneVisualizer.Instance.UpdateStatus("中断...", "GPS: 切断", "サンプル: -");
                    Invoke(nameof(HideVisualizer), 2.0f);
                }
            }
            
            bool isMock = QRCodeTracker.Instance != null && QRCodeTracker.Instance.IsMockMode;
            if (!isMock)
            {
                TcpLocationClient.Instance.Disconnect();
            }
        }
    }

    private int strictSamplesCount = 0;

    private void HandleLocationReceived(LocationData data)
    {
        if (CurrentState == CalibrationState.Connecting)
        {
            CurrentState = CalibrationState.Collecting;
            strictSamplesCount = 0; // reset
        }

        if (CurrentState == CalibrationState.Collecting)
        {
            bool isStrictHighAccuracy = data.isTracking && data.accuracy > 0 && data.accuracy <= 10.0f;
            bool isRelaxedAccuracy = data.isTracking && data.accuracy > 0 && data.accuracy <= 40.0f;
            
            if (isRelaxedAccuracy || forceCalibration)
            {
                collectedSamples.Add(data);
                if (isStrictHighAccuracy)
                {
                    strictSamplesCount++;
                }
            }

            if (PhoneVisualizer.Instance != null)
            {
                PhoneVisualizer.Instance.UpdateStatus(
                    "接続中...", 
                    $"GPS: {(data.isTracking ? "🟢 Tracking" : "🔴 Not Tracking")} 精度: {data.accuracy:F1}m", 
                    $"サンプル: (良{strictSamplesCount}/全{collectedSamples.Count}) / 必要{minSamplesRequired}"
                );
            }

            // 厳格基準(10m以内)を満たすサンプルが規定数集まったら自動完了
            if (strictSamplesCount >= minSamplesRequired || forceCalibration)
            {
                CompleteCalibration();
            }
        }
        else if (CurrentState == CalibrationState.Calibrated && isContinuousCalibrating)
        {
            bool isStrictHighAccuracy = data.isTracking && data.accuracy > 0 && data.accuracy <= 10.0f;
            
            if (isStrictHighAccuracy)
            {
                collectedSamples.Add(data);
                if (collectedSamples.Count > minSamplesRequired)
                {
                    collectedSamples.RemoveAt(0); // Sliding window
                }

                if (PhoneVisualizer.Instance != null)
                {
                    PhoneVisualizer.Instance.UpdateStatus(
                        "再キャリブレーション中...", 
                        $"GPS: 🟢 Tracking 精度: {data.accuracy:F1}m", 
                        $"最新サンプル: {collectedSamples.Count}/{minSamplesRequired}"
                    );
                }

                if (collectedSamples.Count >= minSamplesRequired)
                {
                    // Trigger recalibration using the latest N samples and latest phone transform
                    LocationResolver.Instance.CalibrateOrigin(collectedSamples, lastDetectedPhonePosition, lastDetectedPhoneRotation);
                    
                    if (MarkerManager.Instance != null)
                    {
                        MarkerManager.Instance.UpdateMarkerPositions();
                    }
                    
                    // Reset drift tracking relative to this new accurate position
                    lastCalibratedPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                    accumulatedDistance = 0f;
                    timeSinceCalibrated = 0f;
                }
            }
            else 
            {
                if (PhoneVisualizer.Instance != null)
                {
                    PhoneVisualizer.Instance.UpdateStatus(
                        "再キャリブレーション中...", 
                        $"GPS: 🔴 低精度 精度: {data.accuracy:F1}m", 
                        $"最新サンプル: {collectedSamples.Count}/{minSamplesRequired}"
                    );
                }
            }
        }
    }

    /// <summary>
    /// UIの強制キャリブレーションボタン等から呼ばれるメソッド。
    /// 緩和基準のサンプルだけでも規定数に達していれば完了させます。
    /// </summary>
    public void ForceCompleteCalibration()
    {
        if (collectedSamples.Count >= minSamplesRequired)
        {
            CompleteCalibration();
        }
        else
        {
            Debug.LogWarning("強制キャリブレーションを開始できません。緩和基準のサンプル数すら不足しています。");
        }
    }

    private void HandleTcpError(string error)
    {
        Debug.LogWarning("TCP Error: " + error);
        if (CurrentState == CalibrationState.Connecting || CurrentState == CalibrationState.Collecting)
        {
            CurrentState = CalibrationState.Failed;
            TcpLocationClient.Instance.Disconnect();
            if (PhoneVisualizer.Instance != null) PhoneVisualizer.Instance.Hide();
        }
    }

    public void ForceCalibration()
    {
        if (collectedSamples.Count > 0)
        {
            forceCalibration = true;
            CompleteCalibration();
        }
    }

    private void CompleteCalibration()
    {
        CurrentState = CalibrationState.Calibrated;
        
        bool isMock = QRCodeTracker.Instance != null && QRCodeTracker.Instance.IsMockMode;
        if (!isMock && !isQRVisible)
        {
            TcpLocationClient.Instance.Disconnect();
        }
        
        // 保存したスマホの正確な検出位置と回転を渡す
        LocationResolver.Instance.CalibrateOrigin(collectedSamples, lastDetectedPhonePosition, lastDetectedPhoneRotation);

        if (PhoneVisualizer.Instance != null)
        {
            PhoneVisualizer.Instance.UpdateStatus("✅ 完了！スマホを離してください", "GPS: 取得完了", "サンプル: 完了");
            Invoke(nameof(HideVisualizer), 2.0f);
        }
        
        MarkerManager.Instance.GenerateMarkers();
        
        // Setup drift tracking
        lastCalibratedPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        accumulatedDistance = 0f;
        timeSinceCalibrated = 0f;
    }

    private void HideVisualizer()
    {
        if (PhoneVisualizer.Instance != null)
        {
            PhoneVisualizer.Instance.Hide();
        }
    }

    private void Update()
    {
        if (CurrentState == CalibrationState.Connecting || CurrentState == CalibrationState.Collecting)
        {
            bool isMock = QRCodeTracker.Instance != null && QRCodeTracker.Instance.IsMockMode;
            if (!isMock && Time.time - calibrationStartTime > timeoutSeconds)
            {
                CurrentState = CalibrationState.Failed;
                TcpLocationClient.Instance.Disconnect();
                if (PhoneVisualizer.Instance != null) PhoneVisualizer.Instance.Hide();
            }
        }
        else if (CurrentState == CalibrationState.Calibrated)
        {
            TrackDrift();
        }
    }
    
    private void TrackDrift()
    {
        float driftDistanceThreshold = PlayerPrefs.GetFloat("DriftDistanceThreshold", 200f);
        float driftTimeThreshold = PlayerPrefs.GetFloat("DriftTimeThreshold", 30f) * 60f;
        
        timeSinceCalibrated += Time.deltaTime;
        
        if (Camera.main != null)
        {
            Vector3 currentPos = Camera.main.transform.position;
            accumulatedDistance += Vector3.Distance(lastCalibratedPos, currentPos);
            lastCalibratedPos = currentPos;
        }

        if (accumulatedDistance > driftDistanceThreshold || timeSinceCalibrated > driftTimeThreshold)
        {
            CurrentState = CalibrationState.DriftWarning;
            Debug.LogWarning("Drift warning triggered.");
        }
    }
    
    public int GetCollectedSampleCount() => collectedSamples.Count;
    public float GetAccumulatedDistance() => accumulatedDistance;
    public float GetTimeSinceCalibrated() => timeSinceCalibrated;
}
