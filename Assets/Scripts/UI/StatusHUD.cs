using TMPro;
using UnityEngine;

public class StatusHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField, Min(0.1f)] private float poseRefreshIntervalSeconds = 0.5f;
    private string lastText;
    private Transform trackedPose;
    private float nextPoseRefreshTime;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Unsubscribe();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPoseRefreshTime)
            return;
        nextPoseRefreshTime = Time.unscaledTime + poseRefreshIntervalSeconds;
        Refresh();
    }

    private void Subscribe()
    {
        if (CalibrationManager.Instance != null)
            CalibrationManager.Instance.StatusChanged += Refresh;
        if (QRCodeTracker.Instance != null)
        {
            QRCodeTracker.Instance.OnStatusChanged += Refresh;
            QRCodeTracker.Instance.OnQRCodeDetected += HandleQrDetected;
            QRCodeTracker.Instance.OnQRCodeLost += Refresh;
        }
    }

    private void Unsubscribe()
    {
        if (CalibrationManager.Instance != null)
            CalibrationManager.Instance.StatusChanged -= Refresh;
        if (QRCodeTracker.Instance != null)
        {
            QRCodeTracker.Instance.OnStatusChanged -= Refresh;
            QRCodeTracker.Instance.OnQRCodeDetected -= HandleQrDetected;
            QRCodeTracker.Instance.OnQRCodeLost -= Refresh;
        }
    }

    private void HandleQrDetected(string ip, int port, string id, Vector3 position, Quaternion rotation)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (CalibrationManager.Instance == null || statusText == null) return;
        CalibrationManager.CalibrationState state = CalibrationManager.Instance.CurrentState;
        string connection = "未接続";
        string gps = "Unavailable";
        string drift = string.Empty;

        if (state == CalibrationManager.CalibrationState.Connecting || state == CalibrationManager.CalibrationState.Collecting)
        {
            connection = "通信中";
            gps = "取得中";
        }
        else if (state == CalibrationManager.CalibrationState.Calibrated || state == CalibrationManager.CalibrationState.DriftWarning)
        {
            connection = "完了 (切断)";
            gps = "N/A";
            if (state == CalibrationManager.CalibrationState.DriftWarning)
                drift = "\n!: recalibration needed";
        }

        int count = CalibrationManager.Instance.GetCollectedSampleCount();
        string calibration = state == CalibrationManager.CalibrationState.Calibrated || state == CalibrationManager.CalibrationState.DriftWarning
            ? $"完了 ({count} samples)"
            : $"prog: {count} samples";
        string qr = "未検知";
        string qrDebug = string.Empty;
        if (QRCodeTracker.Instance != null)
        {
            qrDebug = QRCodeTracker.Instance.DebugStatus;
            if (MockModeSettings.Instance != null && MockModeSettings.Instance.UseFixedLocationMock)
                qr = "Mock固定座標 (不使用)";
            else if (MockModeSettings.Instance != null && MockModeSettings.Instance.UseDirectConnectionMock)
                qr = "Mock直接接続 (省略)";
            else
                qr = QRCodeTracker.Instance.IsQRCodeLocated ? "反映済み" :
                    QRCodeTracker.Instance.IsQRCodeDetected ? "位置計算中" : "未検知";
        }

        if (trackedPose == null && Camera.main != null)
            trackedPose = Camera.main.transform;

        string estimatedLocation = "推定位置: N/A";
        string heading = "方位: N/A";
        if (trackedPose != null && LocationResolver.Instance != null)
        {
            if (LocationResolver.Instance.TryUnityToGps(trackedPose.position, out LocationData location))
            {
                estimatedLocation = $"推定位置: {location.latitude:F6}, {location.longitude:F6}, alt {location.altitude:F1}m";
            }
            if (LocationResolver.Instance.TryGetHeadingDegrees(trackedPose.forward, out float headingDegrees))
            {
                heading = $"方位: {headingDegrees:F1}° (0°=北)";
            }
        }

        string value = $"QR API: {qrDebug}\nQR: {qr} | スマホ: {connection} | GPS: {gps}\n補正: {calibration}{drift}\n{estimatedLocation}\n{heading}";
        if (lastText != value)
        {
            lastText = value;
            statusText.text = value;
        }
    }
}
