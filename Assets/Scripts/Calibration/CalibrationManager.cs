using UnityEngine;
using System.Collections.Generic;
using System;

public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance { get; private set; }

    [SerializeField] private int minSamplesRequired = 5;
    [SerializeField] private int maxSamplesToCollect = 20;
    [SerializeField] private double accuracyThreshold = 3.0; // meters
    [SerializeField] private float timeoutSeconds = 60f;

    public enum CalibrationState { WaitingForQR, Connecting, Collecting, Calibrated, Failed, DriftWarning }
    public CalibrationState CurrentState { get; private set; } = CalibrationState.WaitingForQR;
    public event Action StatusChanged;

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
    private Camera mainCamera;
    private float driftDistanceThreshold;
    private float driftTimeThreshold;

    private double StrictAccuracyThreshold => System.Math.Max(accuracyThreshold, 10.0);
    private double RelaxedAccuracyThreshold => System.Math.Max(StrictAccuracyThreshold * 4.0, 40.0);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            mainCamera = Camera.main;
            ReloadDriftSettings();
        }
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
        if (IsFixedLocationMock)
        {
            ApplyFixedLocationMock();
            return;
        }
        StatusChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (QRCodeTracker.Instance != null)
        {
            QRCodeTracker.Instance.OnQRCodeDetected -= HandleQRDetected;
            QRCodeTracker.Instance.OnQRCodeLost -= HandleQRLost;
        }
        if (TcpLocationClient.Instance != null)
        {
            TcpLocationClient.Instance.OnLocationReceived -= HandleLocationReceived;
            TcpLocationClient.Instance.OnError -= HandleTcpError;
        }
        if (Instance == this) Instance = null;
    }

    private void HandleQRDetected(string ip, int port, string deviceId, Vector3 pos, Quaternion rot)
    {
        isQRVisible = true;
        // 検出されたスマホの正確な3D座標と回転を一時保存
        lastDetectedPhonePosition = pos;
        lastDetectedPhoneRotation = rot;

        if (CurrentState == CalibrationState.WaitingForQR || CurrentState == CalibrationState.Failed || CurrentState == CalibrationState.DriftWarning)
        {
            calibrationStartTime = Time.time; // Reset timeout counter when QR is first found!
            SetState(CalibrationState.Connecting);
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
            if (HasAnyLocationMock) return; // MockModeでは再キャリブレーション処理を行わない

            if (!isContinuousCalibrating)
            {
                isContinuousCalibrating = true;
                collectedSamples.Clear();
                strictSamplesCount = 0;
                NotifyStatusChanged();
                
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
            
            if (!IsFixedLocationMock)
            {
                TcpLocationClient.Instance.Disconnect();
            }
        }
    }

    private int strictSamplesCount = 0;

    private void HandleLocationReceived(LocationData data)
    {
        if (!IsUsableLocationSample(data))
        {
            if (PhoneVisualizer.Instance != null)
            {
                PhoneVisualizer.Instance.UpdateStatus(
                    "接続中...",
                    "GPS: 測位データ待機中",
                    $"サンプル: {collectedSamples.Count}/{minSamplesRequired}");
            }
            return;
        }

        if (CurrentState == CalibrationState.Connecting)
        {
            SetState(CalibrationState.Collecting);
            strictSamplesCount = 0; // reset
        }

        if (CurrentState == CalibrationState.Collecting)
        {
            bool isStrictHighAccuracy = data.isTracking && data.accuracy > 0 && data.accuracy <= StrictAccuracyThreshold;
            bool isRelaxedAccuracy = data.isTracking && data.accuracy > 0 && data.accuracy <= RelaxedAccuracyThreshold;
            bool ignoreAccuracy = ShouldIgnoreDirectConnectionAccuracy;
            
            if (ignoreAccuracy || isRelaxedAccuracy || forceCalibration)
            {
                collectedSamples.Add(data);
                if (maxSamplesToCollect > 0 && collectedSamples.Count > maxSamplesToCollect)
                {
                    collectedSamples.RemoveAt(0);
                }
                NotifyStatusChanged();
                if (ignoreAccuracy || isStrictHighAccuracy)
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
            bool isStrictHighAccuracy = data.isTracking && data.accuracy > 0 && data.accuracy <= StrictAccuracyThreshold;
            bool ignoreAccuracy = ShouldIgnoreDirectConnectionAccuracy;
            
            if (ignoreAccuracy || isStrictHighAccuracy)
            {
                collectedSamples.Add(data);
                if (collectedSamples.Count > minSamplesRequired)
                {
                    collectedSamples.RemoveAt(0); // Sliding window
                }
                NotifyStatusChanged();

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
                    CalibrateOriginFromCollectedSamples();
                    
                    if (MarkerManager.Instance != null)
                    {
                        MarkerManager.Instance.UpdateMarkerPositions();
                    }
                    
                    // Reset drift tracking relative to this new accurate position
                    if (mainCamera == null) mainCamera = Camera.main;
                    lastCalibratedPos = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
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
            if (PhoneVisualizer.Instance != null)
            {
                PhoneVisualizer.Instance.UpdateStatus("再接続中...", "GPS: 待機中", $"サンプル: {collectedSamples.Count}/{minSamplesRequired}");
            }
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
        if (IsDirectConnectionMock && !ShouldContinuouslyUpdateDirectConnectionOrigin)
        {
            TcpLocationClient.Instance?.Disconnect();
        }
        else if (!IsFixedLocationMock && !isQRVisible)
        {
            TcpLocationClient.Instance.Disconnect();
        }
        
        CalibrateOriginFromCollectedSamples();

        // SLAM/Cesium原点が確定してから、UIへキャリブレーション完了を通知する。
        SetState(CalibrationState.Calibrated);

        if (PhoneVisualizer.Instance != null)
        {
            PhoneVisualizer.Instance.UpdateStatus("✅ 完了！スマホを離してください", "GPS: 取得完了", "サンプル: 完了");
            Invoke(nameof(HideVisualizer), 2.0f);
        }
        
        MarkerManager.Instance.GenerateMarkers();
        
        // Setup drift tracking
        if (mainCamera == null) mainCamera = Camera.main;
        lastCalibratedPos = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
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
            if (!IsFixedLocationMock && Time.time - calibrationStartTime > timeoutSeconds)
            {
                SetState(CalibrationState.Failed);
                TcpLocationClient.Instance.Disconnect();
                if (PhoneVisualizer.Instance != null) PhoneVisualizer.Instance.Hide();
            }
        }
        else if (CurrentState == CalibrationState.Calibrated)
        {
            if (!HasAnyLocationMock)
                TrackDrift();
        }
    }

    private bool IsFixedLocationMock => MockModeSettings.Instance != null && MockModeSettings.Instance.UseFixedLocationMock;
    private bool IsDirectConnectionMock => MockModeSettings.Instance != null && MockModeSettings.Instance.UseDirectConnectionMock;
    private bool HasAnyLocationMock => MockModeSettings.Instance != null && MockModeSettings.Instance.HasAnyLocationMock;
    private bool ShouldIgnoreDirectConnectionAccuracy =>
        MockModeSettings.Instance != null && MockModeSettings.Instance.IgnoreLocationAccuracy;
    private bool ShouldContinuouslyUpdateDirectConnectionOrigin =>
        MockModeSettings.Instance != null && MockModeSettings.Instance.ContinuouslyUpdateOrigin;
    private bool ShouldBypassQrRelativePose =>
        !HasAnyLocationMock &&
        QRCodeTracker.Instance != null &&
        QRCodeTracker.Instance.BypassRelativePoseCalculation;

    private void CalibrateOriginFromCollectedSamples()
    {
        if (LocationResolver.Instance == null)
            return;

        if (IsDirectConnectionMock && MockModeSettings.Instance != null)
        {
            Vector3 connectionStartForward = lastDetectedPhoneRotation * Vector3.forward;
            float northOffset = MockModeSettings.Instance.GetNorthYRotationOffset(
                connectionStartForward, false);
            LocationResolver.Instance.CalibrateOrigin(
                collectedSamples, lastDetectedPhonePosition, northOffset);
            isContinuousCalibrating = ShouldContinuouslyUpdateDirectConnectionOrigin;
            return;
        }

        if (ShouldBypassQrRelativePose)
        {
            // QR姿勢には依存せず、SLAMワールド原点と+Z方向を基準にする。
            LocationResolver.Instance.CalibrateOrigin(collectedSamples, Vector3.zero, 0f);
            return;
        }

        LocationResolver.Instance.CalibrateOrigin(
            collectedSamples, lastDetectedPhonePosition, lastDetectedPhoneRotation);
    }

    private static bool IsUsableLocationSample(LocationData data)
    {
        if (data == null ||
            double.IsNaN(data.latitude) || double.IsInfinity(data.latitude) ||
            double.IsNaN(data.longitude) || double.IsInfinity(data.longitude) ||
            data.latitude < -90d || data.latitude > 90d ||
            data.longitude < -180d || data.longitude > 180d)
        {
            return false;
        }

        // 未測位時に端末から送られる既定値。精度値やisTrackingは無視しても、
        // 実座標ではない(0, 0)でOriginを上書きしてはいけない。
        return Math.Abs(data.latitude) > 0.000001d || Math.Abs(data.longitude) > 0.000001d;
    }

    private void ApplyFixedLocationMock()
    {
        MockModeSettings settings = MockModeSettings.Instance;
        if (settings == null || LocationResolver.Instance == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        Vector3 originPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        float northOffset = settings.GetNorthYRotationOffset(mainCamera != null ? mainCamera.transform : null, true);
        LocationData fixedLocation = new LocationData
        {
            isTracking = true,
            latitude = settings.FixedLatitude,
            longitude = settings.FixedLongitude,
            altitude = settings.FixedAltitude,
            accuracy = 0d,
            eunRotationW = 1f
        };

        collectedSamples.Clear();
        for (int i = 0; i < Mathf.Max(1, minSamplesRequired); i++)
            collectedSamples.Add(fixedLocation);

        lastDetectedPhonePosition = originPosition;
        lastDetectedPhoneRotation = mainCamera != null ? mainCamera.transform.rotation : Quaternion.identity;
        LocationResolver.Instance.CalibrateOrigin(fixedLocation, originPosition, northOffset);
        SetState(CalibrationState.Calibrated);
        MarkerManager.Instance?.GenerateMarkers();
        lastCalibratedPos = originPosition;
        accumulatedDistance = 0f;
        timeSinceCalibrated = 0f;
    }
    
    private void TrackDrift()
    {
        timeSinceCalibrated += Time.deltaTime;
        
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 currentPos = mainCamera.transform.position;
            accumulatedDistance += Vector3.Distance(lastCalibratedPos, currentPos);
            lastCalibratedPos = currentPos;
        }

        if (accumulatedDistance > driftDistanceThreshold || timeSinceCalibrated > driftTimeThreshold)
        {
            SetState(CalibrationState.DriftWarning);
            Debug.LogWarning("Drift warning triggered.");
        }
    }

    public void ReloadDriftSettings()
    {
        driftDistanceThreshold = PlayerPrefs.GetFloat("DriftDistanceThreshold", 200f);
        driftTimeThreshold = PlayerPrefs.GetFloat("DriftTimeThreshold", 30f) * 60f;
    }

    private void SetState(CalibrationState state)
    {
        if (CurrentState == state) return;
        CurrentState = state;
        StatusChanged?.Invoke();
    }

    private void NotifyStatusChanged()
    {
        StatusChanged?.Invoke();
    }
    
    public int GetCollectedSampleCount() => collectedSamples.Count;
    public float GetAccumulatedDistance() => accumulatedDistance;
    public float GetTimeSinceCalibrated() => timeSinceCalibrated;
}
