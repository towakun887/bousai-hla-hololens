using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MixedReality.Toolkit; // MRTK3

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private StatefulInteractable debugToggle; // MRTK3のトグルボタンコンポーネント
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

        if (debugToggle != null)
        {
            // 初期状態を設定（イベントを発火させずに値をセット）
            debugToggle.ForceSetToggled(PlayerPrefs.GetInt("DebugMode", 0) == 1, false);
            // MRTK3のクリックイベント（トグル切り替え時に発生）を購読
            debugToggle.OnClicked.AddListener(OnDebugToggleClicked);
        }

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

    private void OnDebugToggleClicked()
    {
        if (debugToggle != null && DebugOverlay.Instance != null)
        {
            // MRTK3トグルの IsToggled.Active プロパティから現在の状態を取得
            DebugOverlay.Instance.SetDebugMode(debugToggle.IsToggled.Active);
        }
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
        
        if (MarkerManager.Instance != null)
        {
            MarkerManager.Instance.GenerateMarkers();
        }
    }
}
