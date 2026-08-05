using UnityEngine;
using TMPro;

public class StatusHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusText;

    private void Update()
    {
        if (CalibrationManager.Instance == null) return;
        
        var state = CalibrationManager.Instance.CurrentState;
        
        string connectionStr = "未接続";
        string gpsStr = "Unavailable";
        string driftStr = "";

        if (state == CalibrationManager.CalibrationState.Connecting || state == CalibrationManager.CalibrationState.Collecting)
        {
            connectionStr = "通信中";
            gpsStr = "取得中";
        }
        else if (state == CalibrationManager.CalibrationState.Calibrated || state == CalibrationManager.CalibrationState.DriftWarning)
        {
            connectionStr = "完了 (切断)";
            gpsStr = "N/A";
            
            if (state == CalibrationManager.CalibrationState.DriftWarning)
            {
                driftStr = "\n!: recalibration needed";
            }
        }

        int count = CalibrationManager.Instance.GetCollectedSampleCount();
        string calibStr = state == CalibrationManager.CalibrationState.Calibrated || state == CalibrationManager.CalibrationState.DriftWarning 
            ? $"完了 ({count} samples)" 
            : $"prog: {count} samples";

        string qrStr = "未検知";
        string qrDebug = "";
        if (QRCodeTracker.Instance != null)
        {
            qrDebug = QRCodeTracker.Instance.DebugStatus;
            if (QRCodeTracker.Instance.IsQRCodeLocated)
            {
                qrStr = "反映済み";
            }
            else if (QRCodeTracker.Instance.IsQRCodeDetected)
            {
                qrStr = "位置計算中";
            }
        }

        if (statusText != null)
        {
            statusText.text = $"QR API: {qrDebug}\nQR: {qrStr} | スマホ: {connectionStr} | GPS: {gpsStr}\n補正: {calibStr}{driftStr}";
        }
    }
}
