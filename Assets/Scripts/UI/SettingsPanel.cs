using UnityEngine;
using TMPro;
using MixedReality.Toolkit; // MRTK3
using Bousai.Flood;
using UnityEngine.Serialization;

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private StatefulInteractable debugToggle; // MRTK3のトグルボタンコンポーネント

    [Header("QR / Map Display")]
    [Tooltip("ON: QRコードの相対位置・回転計算をバイパス")]
    [SerializeField] private StatefulInteractable qrRelativePoseBypassToggle;
    [SerializeField] private QRCodeTracker qrCodeTracker;
    [Tooltip("ON: キャリブレーション完了後にOSM Buildingsを表示")]
    [SerializeField] private StatefulInteractable osmBuildingsToggle;
    [SerializeField] private CalibrationUI calibrationUI;

    [Header("Flood Display")]
    [SerializeField] private StatefulInteractable floodDisplayToggle;
    [FormerlySerializedAs("terrainElevationModeToggle")]
    [Tooltip("ON: 国土地理院の標高JSONを使用 / OFF: 空間認識した地表面を使用")]
    [SerializeField] private StatefulInteractable elevationModeToggle;
    [SerializeField] private FloodDepthController floodController;
    [SerializeField] private GroundSurfaceResolver groundSurfaceResolver;

    [Header("Data / Drift")]
    [SerializeField] private TMP_InputField csvUrlInput;
    [SerializeField] private TMP_InputField distanceThresholdInput;
    [SerializeField] private TMP_InputField timeThresholdInput;
    
    [SerializeField] private CsvMarkerParser parser;

    private void Start()
    {
        if (parser == null)
        {
            parser = FindFirstObjectByType<CsvMarkerParser>();
        }

        if (floodController == null)
            floodController = FloodDepthController.Instance != null
                ? FloodDepthController.Instance
                : FindFirstObjectByType<FloodDepthController>();
        if (groundSurfaceResolver == null)
            groundSurfaceResolver = GroundSurfaceResolver.Instance != null
                ? GroundSurfaceResolver.Instance
                : FindFirstObjectByType<GroundSurfaceResolver>();

        if (qrCodeTracker == null)
            qrCodeTracker = QRCodeTracker.Instance != null
                ? QRCodeTracker.Instance
                : FindFirstObjectByType<QRCodeTracker>();
        if (calibrationUI == null)
            calibrationUI = FindFirstObjectByType<CalibrationUI>();

        if (debugToggle != null)
        {
            // 初期状態を設定（イベントを発火させずに値をセット）
            debugToggle.ForceSetToggled(PlayerPrefs.GetInt("DebugMode", 0) == 1, false);
            // MRTK3のクリックイベント（トグル切り替え時に発生）を購読
            debugToggle.OnClicked.AddListener(OnDebugToggleClicked);
        }

        InitializeFloodSettings();
        InitializeQrAndMapSettings();

        if (csvUrlInput != null)
        {
            string defaultUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSi8T3T1vmz4eQBrMOxWXYljtujKaNYg0IYOZI_fZxhH-l0diNpJfX2cjwjAVbVWds9g_d3_Y2iQfC9/pub?gid=1057226757&single=true&output=csv";
            csvUrlInput.text = PlayerPrefs.GetString("CsvUrl", defaultUrl);
            if (parser != null) parser.SetCsvUrl(csvUrlInput.text);
        }

        if (distanceThresholdInput != null)
        {
            distanceThresholdInput.text = PlayerPrefs.GetFloat("DriftDistanceThreshold", 200f).ToString();
        }

        if (timeThresholdInput != null)
        {
            timeThresholdInput.text = PlayerPrefs.GetFloat("DriftTimeThreshold", 30f).ToString();
        }
    }

    private void OnDestroy()
    {
        if (debugToggle != null)
            debugToggle.OnClicked.RemoveListener(OnDebugToggleClicked);
        if (qrRelativePoseBypassToggle != null)
            qrRelativePoseBypassToggle.OnClicked.RemoveListener(OnQrRelativePoseBypassClicked);
        if (osmBuildingsToggle != null)
            osmBuildingsToggle.OnClicked.RemoveListener(OnOsmBuildingsToggleClicked);
        if (floodDisplayToggle != null)
            floodDisplayToggle.OnClicked.RemoveListener(OnFloodDisplayToggleClicked);
        if (elevationModeToggle != null)
            elevationModeToggle.OnClicked.RemoveListener(OnElevationModeClicked);
    }

    private void InitializeFloodSettings()
    {
        bool displayEnabled = PlayerPrefs.GetInt(
            FloodDepthController.DisplayEnabledPreferenceKey, 1) == 1;
        FloodHeightReferenceMode mode = (FloodHeightReferenceMode)Mathf.Clamp(
            PlayerPrefs.GetInt(FloodDepthController.HeightReferenceModePreferenceKey, 0), 0, 1);

        if (floodDisplayToggle != null)
        {
            floodDisplayToggle.ForceSetToggled(displayEnabled, false);
            floodDisplayToggle.OnClicked.AddListener(OnFloodDisplayToggleClicked);
        }
        if (elevationModeToggle != null)
        {
            elevationModeToggle.ForceSetToggled(
                mode == FloodHeightReferenceMode.TerrainElevation, false);
            elevationModeToggle.OnClicked.AddListener(OnElevationModeClicked);
        }

        floodController?.SetFloodDisplayEnabled(displayEnabled);
        floodController?.SetHeightReferenceMode(mode);
        groundSurfaceResolver?.SetMode(mode);
    }

    private void InitializeQrAndMapSettings()
    {
        bool bypassQrPose = qrCodeTracker != null
            ? qrCodeTracker.BypassRelativePoseCalculation
            : PlayerPrefs.GetInt(QRCodeTracker.RelativePoseBypassPreferenceKey, 0) == 1;
        bool showOsmBuildings = calibrationUI != null
            ? calibrationUI.OsmBuildingsVisible
            : PlayerPrefs.GetInt(CalibrationUI.OsmBuildingsVisiblePreferenceKey, 1) == 1;

        if (qrRelativePoseBypassToggle != null)
        {
            qrRelativePoseBypassToggle.ForceSetToggled(bypassQrPose, false);
            qrRelativePoseBypassToggle.OnClicked.AddListener(OnQrRelativePoseBypassClicked);
        }
        if (osmBuildingsToggle != null)
        {
            osmBuildingsToggle.ForceSetToggled(showOsmBuildings, false);
            osmBuildingsToggle.OnClicked.AddListener(OnOsmBuildingsToggleClicked);
        }

        qrCodeTracker?.SetRelativePoseCalculationBypassed(bypassQrPose);
        calibrationUI?.SetOsmBuildingsVisible(showOsmBuildings);
    }

    private void OnDebugToggleClicked()
    {
        if (debugToggle != null && DebugOverlay.Instance != null)
        {
            // MRTK3トグルの IsToggled.Active プロパティから現在の状態を取得
            DebugOverlay.Instance.SetDebugMode(debugToggle.IsToggled.Active);
        }
    }

    private void OnQrRelativePoseBypassClicked()
    {
        if (qrRelativePoseBypassToggle != null)
            SetQrRelativePoseCalculationBypassed(qrRelativePoseBypassToggle.IsToggled.Active);
    }

    private void OnOsmBuildingsToggleClicked()
    {
        if (osmBuildingsToggle != null)
            SetOsmBuildingsVisible(osmBuildingsToggle.IsToggled.Active);
    }

    public void SetQrRelativePoseCalculationBypassed(bool bypass)
    {
        qrCodeTracker?.SetRelativePoseCalculationBypassed(bypass);
    }

    public void SetOsmBuildingsVisible(bool visible)
    {
        calibrationUI?.SetOsmBuildingsVisible(visible);
    }

    private void OnFloodDisplayToggleClicked()
    {
        if (floodDisplayToggle == null)
            return;
        floodController?.SetFloodDisplayEnabled(floodDisplayToggle.IsToggled.Active);
    }

    private void OnElevationModeClicked()
    {
        if (elevationModeToggle == null)
            return;

        FloodHeightReferenceMode mode = elevationModeToggle.IsToggled.Active
            ? FloodHeightReferenceMode.TerrainElevation
            : FloodHeightReferenceMode.GroundSurface;
        groundSurfaceResolver?.SetMode(mode);
        floodController?.SetHeightReferenceMode(mode);
    }

    public void OnApplySettingsClicked()
    {
        if (csvUrlInput != null)
        {
            PlayerPrefs.SetString("CsvUrl", csvUrlInput.text);
            if (parser != null) parser.SetCsvUrl(csvUrlInput.text);
        }

        if (distanceThresholdInput != null && float.TryParse(distanceThresholdInput.text, out float dist))
        {
            PlayerPrefs.SetFloat("DriftDistanceThreshold", dist);
        }

        if (timeThresholdInput != null && float.TryParse(timeThresholdInput.text, out float time))
        {
            PlayerPrefs.SetFloat("DriftTimeThreshold", time);
        }

        PlayerPrefs.Save();
        CalibrationManager.Instance?.ReloadDriftSettings();
        
        if (MarkerManager.Instance != null)
        {
            MarkerManager.Instance.GenerateMarkers();
        }
    }
}
