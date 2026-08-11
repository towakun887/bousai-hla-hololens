using MixedReality.Toolkit;
using TMPro;
using UnityEngine;

public class CalibrationUI : MonoBehaviour
{
    public const string OsmBuildingsVisiblePreferenceKey = "OsmBuildingsVisible";

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private StatefulInteractable forceButton;
    [SerializeField] private GameObject cesiumOsmBuildings;
    [Tooltip("キャリブレーション完了後にOSM Buildingsを表示します。")]
    [SerializeField] private bool osmBuildingsVisible = true;

    public bool OsmBuildingsVisible => osmBuildingsVisible;

    private void Awake()
    {
        osmBuildingsVisible = PlayerPrefs.GetInt(
            OsmBuildingsVisiblePreferenceKey,
            osmBuildingsVisible ? 1 : 0) == 1;
    }

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
            ApplyOsmBuildingsVisibility(true);
            if (panel != null && panel.activeSelf)
                panel.SetActive(false);
            return;
        }

        ApplyOsmBuildingsVisibility(false);
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

    /// <summary>
    /// OSM Buildingsの表示設定を変更します。未キャリブレーション中は、
    /// ONにしてもキャリブレーション完了まで非表示のままです。
    /// </summary>
    public void SetOsmBuildingsVisible(bool visible)
    {
        osmBuildingsVisible = visible;
        PlayerPrefs.SetInt(OsmBuildingsVisiblePreferenceKey, visible ? 1 : 0);
        PlayerPrefs.Save();
        Refresh();
    }

    public void ToggleOsmBuildingsVisibility()
    {
        SetOsmBuildingsVisible(!osmBuildingsVisible);
    }

    private void ApplyOsmBuildingsVisibility(bool calibrated)
    {
        bool shouldBeActive = calibrated && osmBuildingsVisible;
        if (cesiumOsmBuildings != null && cesiumOsmBuildings.activeSelf != shouldBeActive)
            cesiumOsmBuildings.SetActive(shouldBeActive);
    }
}
