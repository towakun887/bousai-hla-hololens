using MixedReality.Toolkit;
using TMPro;
using UnityEngine;

public class CalibrationUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private StatefulInteractable forceButton;
    [SerializeField] private GameObject cesiumOsmBuildings;

    private void OnEnable()
    {
        if (CalibrationManager.Instance != null)
            CalibrationManager.Instance.StatusChanged += Refresh;
        Refresh();
    }

    private void Start()
    {
        if (CalibrationManager.Instance != null)
        {
            CalibrationManager.Instance.StatusChanged -= Refresh;
            CalibrationManager.Instance.StatusChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (CalibrationManager.Instance != null)
            CalibrationManager.Instance.StatusChanged -= Refresh;
    }

    private void Refresh()
    {
        if (CalibrationManager.Instance == null) return;
        CalibrationManager.CalibrationState state = CalibrationManager.Instance.CurrentState;
        bool calibrated = state == CalibrationManager.CalibrationState.Calibrated;
        if (calibrated)
        {
            if (cesiumOsmBuildings != null && !cesiumOsmBuildings.activeSelf)
                cesiumOsmBuildings.SetActive(true);
            if (panel != null && panel.activeSelf)
                panel.SetActive(false);
            return;
        }

        if (cesiumOsmBuildings != null && cesiumOsmBuildings.activeSelf)
            cesiumOsmBuildings.SetActive(false);
        if (panel != null && !panel.activeSelf)
            panel.SetActive(true);

        string status = state switch
        {
            CalibrationManager.CalibrationState.WaitingForQR => "QRコードを検出してください",
            CalibrationManager.CalibrationState.Connecting => "スマートフォンに接続中...",
            CalibrationManager.CalibrationState.Collecting => "GPSデータ収集中...",
            CalibrationManager.CalibrationState.Failed => "タイムアウトまたはエラー。再試行してください",
            CalibrationManager.CalibrationState.DriftWarning => "⚠ 精度低下の可能性。再キャリブレーション推奨",
            _ => string.Empty
        };
        if (statusText != null && statusText.text != status) statusText.text = status;

        int count = CalibrationManager.Instance.GetCollectedSampleCount();
        if (progressText != null) progressText.SetText("サンプル: {0}", count);
        if (forceButton != null && forceButton.enabled != (count > 0))
            forceButton.enabled = count > 0;
    }

    public void OnForceCalibrationClicked()
    {
        CalibrationManager.Instance?.ForceCalibration();
    }
}
