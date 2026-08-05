using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MixedReality.Toolkit; // MRTK3

public class CalibrationUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private StatefulInteractable forceButton; // MRTK3のStatefulInteractable（ボタン）

    private void Update()
    {
        if (CalibrationManager.Instance == null) return;

        var state = CalibrationManager.Instance.CurrentState;
        
        if (state == CalibrationManager.CalibrationState.Calibrated)
        {
            if (panel.activeSelf) panel.SetActive(false);
            return;
        }

        if (!panel.activeSelf) panel.SetActive(true);

        switch (state)
        {
            case CalibrationManager.CalibrationState.WaitingForQR:
                statusText.text = "QRコードを検出してください";
                break;
            case CalibrationManager.CalibrationState.Connecting:
                statusText.text = "スマートフォンに接続中...";
                break;
            case CalibrationManager.CalibrationState.Collecting:
                statusText.text = "GPSデータ収集中...";
                break;
            case CalibrationManager.CalibrationState.Failed:
                statusText.text = "タイムアウトまたはエラー。再試行してください";
                break;
            case CalibrationManager.CalibrationState.DriftWarning:
                statusText.text = "⚠ 精度低下の可能性。再キャリブレーション推奨";
                break;
        }

        progressText.text = $"サンプル: {CalibrationManager.Instance.GetCollectedSampleCount()}";
        
        if (forceButton != null)
        {
            // MRTK3のインタラクタブルは .enabled を切り替えることで有効/無効を制御し、ビジュアルも連動します
            forceButton.enabled = CalibrationManager.Instance.GetCollectedSampleCount() > 0;
        }
    }

    public void OnForceCalibrationClicked()
    {
        if (CalibrationManager.Instance != null)
        {
            CalibrationManager.Instance.ForceCalibration();
        }
    }
}
