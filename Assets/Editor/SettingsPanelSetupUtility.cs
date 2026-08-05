using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelSetupUtility
{
    [MenuItem("Tools/Setup Settings Panel Only (New)")]
    public static void SetupSettingsPanel()
    {
        Debug.Log("Starting SettingsPanel Setup...");
        
        GameObject panelObj = GameObject.Find("SettingsPanel");
        if (panelObj == null)
        {
            Debug.LogError("GameObject 'SettingsPanel' not found in the scene.");
            return;
        }

        SettingsPanel settingsScript = panelObj.GetComponent<SettingsPanel>();
        if (settingsScript == null)
        {
            Debug.LogError("SettingsPanel component not found on 'SettingsPanel' GameObject.");
            return;
        }

        TMP_FontAsset font = GetDefaultFont();

        // SettingsPanelの直下にある古い子オブジェクトを完全に削除して重複を防ぐ
        for (int i = panelObj.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(panelObj.transform.GetChild(i).gameObject);
        }

        Canvas canvas = panelObj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = panelObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            panelObj.AddComponent<CanvasScaler>();
            panelObj.AddComponent<GraphicRaycaster>();
        }

        // 1. Debug Toggle の作成 (UIとしてRectTransformを明示して作成)
        GameObject toggleObj = new GameObject("DebugToggle", typeof(RectTransform));
        toggleObj.transform.SetParent(panelObj.transform, false);
        var toggle = toggleObj.AddComponent<Toggle>();
        var toggleText = CreateText(toggleObj.transform, "Label", "Debug Mode", font);
        ((RectTransform)toggleText.transform).anchoredPosition = new Vector2(50, 0);

        // 2. CSV URL Input の作成
        GameObject csvInputObj = new GameObject("CsvUrlInput", typeof(RectTransform));
        csvInputObj.transform.SetParent(panelObj.transform, false);
        var csvInput = csvInputObj.AddComponent<TMP_InputField>();
        ((RectTransform)csvInput.transform).anchoredPosition = new Vector2(0, -50);
        var csvLabel = CreateText(csvInputObj.transform, "Label", "CSV URL:", font);
        ((RectTransform)csvLabel.transform).anchoredPosition = new Vector2(-150, 0);

        // 3. Distance Input の作成
        GameObject distInputObj = new GameObject("DistanceThresholdInput", typeof(RectTransform));
        distInputObj.transform.SetParent(panelObj.transform, false);
        var distInput = distInputObj.AddComponent<TMP_InputField>();
        ((RectTransform)distInput.transform).anchoredPosition = new Vector2(0, -100);
        var distLabel = CreateText(distInputObj.transform, "Label", "Drift Distance (m):", font);
        ((RectTransform)distLabel.transform).anchoredPosition = new Vector2(-150, 0);

        // 4. Time Input の作成
        GameObject timeInputObj = new GameObject("TimeThresholdInput", typeof(RectTransform));
        timeInputObj.transform.SetParent(panelObj.transform, false);
        var timeInput = timeInputObj.AddComponent<TMP_InputField>();
        ((RectTransform)timeInput.transform).anchoredPosition = new Vector2(0, -150);
        var timeLabel = CreateText(timeInputObj.transform, "Label", "Drift Time (min):", font);
        ((RectTransform)timeLabel.transform).anchoredPosition = new Vector2(-150, 0);

        // AppManager から CsvMarkerParser の参照を取得
        GameObject appManager = GameObject.Find("AppManager");
        CsvMarkerParser parser = appManager != null ? appManager.GetComponent<CsvMarkerParser>() : null;

        // SettingsPanel コンポーネントに各UIオブジェクトの参照をアサイン
        SerializedObject so = new SerializedObject(settingsScript);
        so.FindProperty("debugToggle").objectReferenceValue = toggle;
        so.FindProperty("csvUrlInput").objectReferenceValue = csvInput;
        so.FindProperty("distanceThresholdInput").objectReferenceValue = distInput;
        so.FindProperty("timeThresholdInput").objectReferenceValue = timeInput;
        if (parser != null)
        {
            so.FindProperty("parser").objectReferenceValue = parser;
        }
        so.ApplyModifiedProperties();

        Debug.Log("SettingsPanel Setup Completed successfully!");
    }

    private static TMP_FontAsset GetDefaultFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        if (guids.Length > 0)
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string defaultText, TMP_FontAsset font)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform));
        textObj.transform.SetParent(parent, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        
        tmp.rectTransform.sizeDelta = new Vector2(400, 50);
        return tmp;
    }
}
