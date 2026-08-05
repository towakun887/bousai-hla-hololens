using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class UISetupUtility : EditorWindow
{
    [MenuItem("Tools/Setup All AR App UI (Reset)")]
    public static void SetupUI()
    {
        Debug.Log("Starting UI Setup...");
        TMP_FontAsset defaultFont = GetDefaultFont();

        SetupCalibrationPanel(defaultFont);
        SetupStatusHUD(defaultFont);
        SetupMarkerPrefab(defaultFont);
        SetupPhoneOverlay(defaultFont);
        SetupSettingsPanel(defaultFont);
        SetupMarkerTableUI(defaultFont);

        Debug.Log("UI Setup Completed! Please adjust the positions and sizes of the UI elements visually.");
    }

    [MenuItem("Tools/Setup Settings Panel Only")]
    public static void SetupSettingsPanelOnly()
    {
        Debug.Log("Starting SettingsPanel Setup...");
        TMP_FontAsset defaultFont = GetDefaultFont();
        SetupSettingsPanel(defaultFont);
        Debug.Log("SettingsPanel Setup Completed!");
    }

    [MenuItem("Tools/Setup Marker Table UI")]
    public static void SetupMarkerTableUIOnly()
    {
        Debug.Log("Starting MarkerTableUI Setup...");
        TMP_FontAsset defaultFont = GetDefaultFont();
        SetupMarkerTableUI(defaultFont);
        Debug.Log("MarkerTableUI Setup Completed!");
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

    private static void SafeDestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            DestroyImmediate(child.gameObject);
        }
    }

    private static void SetupCalibrationPanel(TMP_FontAsset font)
    {
        GameObject panelObj = GameObject.Find("CalibrationPanel");
        if (panelObj == null) return;

        CalibrationUI uiScript = panelObj.GetComponent<CalibrationUI>();
        if (uiScript == null) return;

        Canvas canvas = GetOrCreateCanvas(panelObj);
        
        SafeDestroyChild(panelObj.transform, "StatusText");
        SafeDestroyChild(panelObj.transform, "ProgressText");
        SafeDestroyChild(panelObj.transform, "ForceButton");

        var statusText = CreateText(panelObj.transform, "StatusText", "QRコードを検出してください", font);
        var progressText = CreateText(panelObj.transform, "ProgressText", "サンプル: 0", font);
        
        progressText.rectTransform.anchoredPosition = new Vector2(0, -50);

        var buttonObj = new GameObject("ForceButton");
        buttonObj.transform.SetParent(panelObj.transform, false);
        var buttonText = CreateText(buttonObj.transform, "Text", "強制キャリブレーション", font);
        var button = buttonObj.AddComponent<Button>();

        SerializedObject so = new SerializedObject(uiScript);
        so.FindProperty("panel").objectReferenceValue = panelObj;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("progressText").objectReferenceValue = progressText;
        so.FindProperty("forceButton").objectReferenceValue = button;
        so.ApplyModifiedProperties();
    }

    private static void SetupStatusHUD(TMP_FontAsset font)
    {
        GameObject hudObj = GameObject.Find("StatusHUD");
        if (hudObj == null) return;

        StatusHUD hudScript = hudObj.GetComponent<StatusHUD>();
        if (hudScript == null) return;

        Canvas canvas = GetOrCreateCanvas(hudObj);
        
        SafeDestroyChild(hudObj.transform, "StatusText");
        var statusText = CreateText(hudObj.transform, "StatusText", "Status HUD", font);

        SerializedObject so = new SerializedObject(hudScript);
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.ApplyModifiedProperties();
    }

    private static void SetupMarkerPrefab(TMP_FontAsset font)
    {
        GameObject markerObj = GameObject.Find("MarkerPrefab_Base");
        if (markerObj == null) return;

        MarkerVisual visual = markerObj.GetComponent<MarkerVisual>();
        if (visual == null) return;

        SafeDestroyChild(markerObj.transform, "Cylinder");
        SafeDestroyChild(markerObj.transform, "Canvas");

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "Cylinder";
        cylinder.transform.SetParent(markerObj.transform, false);
        cylinder.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
        MeshRenderer pinRenderer = cylinder.GetComponent<MeshRenderer>();

        GameObject canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(markerObj.transform, false);
        Canvas canvas = GetOrCreateCanvas(canvasObj);
        canvas.transform.localPosition = new Vector3(0, 1.0f, 0);
        canvas.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var timeText = CreateText(canvasObj.transform, "TimeText", "Time", font);
        var kindText = CreateText(canvasObj.transform, "KindText", "Kind", font);
        var titleText = CreateText(canvasObj.transform, "TitleText", "Title", font);
        var detailText = CreateText(canvasObj.transform, "DetailText", "Detail", font);
        
        kindText.rectTransform.anchoredPosition = new Vector2(0, -30);
        titleText.rectTransform.anchoredPosition = new Vector2(0, -60);
        detailText.rectTransform.anchoredPosition = new Vector2(0, -90);

        GameObject imageObj = new GameObject("MarkerImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        RawImage rawImage = imageObj.AddComponent<RawImage>();
        rawImage.rectTransform.anchoredPosition = new Vector2(0, -150);
        rawImage.rectTransform.sizeDelta = new Vector2(200, 200);

        GameObject errorOverlay = new GameObject("ErrorOverlay");
        errorOverlay.transform.SetParent(canvasObj.transform, false);
        var errorText = CreateText(errorOverlay.transform, "ErrorText", "Error", font);

        GameObject debugPanel = new GameObject("DebugPanel");
        debugPanel.transform.SetParent(canvasObj.transform, false);
        var debugGpsText = CreateText(debugPanel.transform, "DebugGpsText", "GPS", font);
        var debugSlamText = CreateText(debugPanel.transform, "DebugSlamText", "SLAM", font);
        debugSlamText.rectTransform.anchoredPosition = new Vector2(0, -30);

        SerializedObject so = new SerializedObject(visual);
        so.FindProperty("timeText").objectReferenceValue = timeText;
        so.FindProperty("kindText").objectReferenceValue = kindText;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("detailText").objectReferenceValue = detailText;
        so.FindProperty("markerImage").objectReferenceValue = rawImage;
        so.FindProperty("errorOverlay").objectReferenceValue = errorOverlay;
        so.FindProperty("errorText").objectReferenceValue = errorText;
        so.FindProperty("debugPanel").objectReferenceValue = debugPanel;
        so.FindProperty("debugGpsText").objectReferenceValue = debugGpsText;
        so.FindProperty("debugSlamText").objectReferenceValue = debugSlamText;
        so.FindProperty("pinRenderer").objectReferenceValue = pinRenderer;
        so.ApplyModifiedProperties();
    }

    private static void SetupPhoneOverlay(TMP_FontAsset font)
    {
        GameObject phoneObj = GameObject.Find("PhoneOverlayPrefab_Base");
        if (phoneObj == null) return;

        PhoneVisualizer visual = phoneObj.GetComponent<PhoneVisualizer>();
        if (visual == null) return;

        SafeDestroyChild(phoneObj.transform, "Quad");
        SafeDestroyChild(phoneObj.transform, "OverlayContainer");

        GameObject frameObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        frameObj.name = "Quad";
        frameObj.transform.SetParent(phoneObj.transform, false);
        frameObj.transform.localScale = new Vector3(0.07f, 0.14f, 1f);
        MeshRenderer frameRenderer = frameObj.GetComponent<MeshRenderer>();

        GameObject overlayContainer = new GameObject("OverlayContainer");
        overlayContainer.transform.SetParent(phoneObj.transform, false);

        Canvas canvas = GetOrCreateCanvas(overlayContainer);
        canvas.transform.localPosition = new Vector3(0, 0.15f, 0);
        canvas.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

        var statusText = CreateText(overlayContainer.transform, "StatusText", "Status", font);
        var gpsText = CreateText(overlayContainer.transform, "GpsText", "GPS", font);
        var progressText = CreateText(overlayContainer.transform, "ProgressText", "Progress", font);
        gpsText.rectTransform.anchoredPosition = new Vector2(0, -30);
        progressText.rectTransform.anchoredPosition = new Vector2(0, -60);

        GameObject lostWarning = new GameObject("LostWarning");
        lostWarning.transform.SetParent(overlayContainer.transform, false);
        var lostText = CreateText(lostWarning.transform, "LostText", "WARNING: LOST", font);

        SerializedObject so = new SerializedObject(visual);
        so.FindProperty("overlayContainer").objectReferenceValue = overlayContainer;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("gpsText").objectReferenceValue = gpsText;
        so.FindProperty("progressText").objectReferenceValue = progressText;
        so.FindProperty("frameRenderer").objectReferenceValue = frameRenderer;
        so.FindProperty("lostWarning").objectReferenceValue = lostWarning;
        so.ApplyModifiedProperties();
    }

    private static void SetupSettingsPanel(TMP_FontAsset font)
    {
        GameObject panelObj = GameObject.Find("SettingsPanel");
        if (panelObj == null) return;

        SettingsPanel settingsScript = panelObj.GetComponent<SettingsPanel>();
        if (settingsScript == null) return;

        Canvas canvas = GetOrCreateCanvas(panelObj);

        SafeDestroyChild(panelObj.transform, "DebugToggle");
        SafeDestroyChild(panelObj.transform, "CsvUrlInput");
        SafeDestroyChild(panelObj.transform, "DistanceThresholdInput");
        SafeDestroyChild(panelObj.transform, "TimeThresholdInput");

        // Create Debug Toggle
        GameObject toggleObj = new GameObject("DebugToggle");
        toggleObj.transform.SetParent(panelObj.transform, false);
        var toggle = toggleObj.AddComponent<Toggle>();
        var toggleText = CreateText(toggleObj.transform, "Label", "Debug Mode", font);
        toggleText.rectTransform.anchoredPosition = new Vector2(50, 0);

        // Create CSV URL Input
        GameObject csvInputObj = new GameObject("CsvUrlInput");
        csvInputObj.transform.SetParent(panelObj.transform, false);
        var csvInput = csvInputObj.AddComponent<TMP_InputField>();
        ((RectTransform)csvInput.transform).anchoredPosition = new Vector2(0, -50);
        var csvLabel = CreateText(csvInputObj.transform, "Label", "CSV URL:", font);
        csvLabel.rectTransform.anchoredPosition = new Vector2(-150, 0);

        // Create Distance Input
        GameObject distInputObj = new GameObject("DistanceThresholdInput");
        distInputObj.transform.SetParent(panelObj.transform, false);
        var distInput = distInputObj.AddComponent<TMP_InputField>();
        ((RectTransform)distInput.transform).anchoredPosition = new Vector2(0, -100);
        var distLabel = CreateText(distInputObj.transform, "Label", "Drift Distance (m):", font);
        distLabel.rectTransform.anchoredPosition = new Vector2(-150, 0);

        // Create Time Input
        GameObject timeInputObj = new GameObject("TimeThresholdInput");
        timeInputObj.transform.SetParent(panelObj.transform, false);
        var timeInput = timeInputObj.AddComponent<TMP_InputField>();
        ((RectTransform)timeInput.transform).anchoredPosition = new Vector2(0, -150);
        var timeLabel = CreateText(timeInputObj.transform, "Label", "Drift Time (min):", font);
        timeLabel.rectTransform.anchoredPosition = new Vector2(-150, 0);

        // Find AppManager for CsvMarkerParser
        GameObject appManager = GameObject.Find("AppManager");
        CsvMarkerParser parser = appManager != null ? appManager.GetComponent<CsvMarkerParser>() : null;

        // Apply references
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
    }

    private static void SetupMarkerTableUI(TMP_FontAsset font)
    {
        GameObject tableObj = GameObject.Find("MarkerTableWindow");
        if (tableObj == null)
        {
            tableObj = new GameObject("MarkerTableWindow");
        }

        MarkerTableUI tableScript = tableObj.GetComponent<MarkerTableUI>();
        if (tableScript == null) tableScript = tableObj.AddComponent<MarkerTableUI>();

        Canvas canvas = GetOrCreateCanvas(tableObj);
        canvas.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f); // Make it a reasonable size for HoloLens
        
        SafeDestroyChild(tableObj.transform, "Scroll View");
        SafeDestroyChild(tableObj.transform, "RowPrefab");
        SafeDestroyChild(tableObj.transform, "HeaderRow"); // In case it existed

        // Header Row
        GameObject headerRow = new GameObject("HeaderRow");
        headerRow.transform.SetParent(tableObj.transform, false);
        RectTransform headerRect = headerRow.AddComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(0, 230);
        headerRect.sizeDelta = new Vector2(600, 40);
        Image headerBg = headerRow.AddComponent<Image>();
        headerBg.color = new Color(0.1f, 0.1f, 0.2f, 1f);
        
        HorizontalLayoutGroup headerHlg = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerHlg.childControlHeight = true;
        headerHlg.childControlWidth = true;
        headerHlg.childForceExpandHeight = true;
        headerHlg.childForceExpandWidth = true;
        headerHlg.padding = new RectOffset(10, 10, 5, 5);

        string[] headers = { "Title", "Latitude", "Longitude", "Altitude", "Distance" };
        foreach (string h in headers)
        {
            var txt = CreateText(headerRow.transform, h, h, font);
            txt.fontSize = 24;
            txt.fontStyle = FontStyles.Bold;
        }

        // Create Scroll View
        GameObject scrollView = new GameObject("Scroll View");
        scrollView.transform.SetParent(tableObj.transform, false);
        RectTransform scrollRectTransform = scrollView.AddComponent<RectTransform>();
        scrollRectTransform.sizeDelta = new Vector2(600, 400);
        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 10f;

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(1,1,1,0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        
        scrollRect.viewport = viewportRect;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 5;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        
        scrollRect.content = contentRect;

        // Create Row Prefab
        GameObject rowPrefab = new GameObject("RowPrefab");
        rowPrefab.transform.SetParent(tableObj.transform, false);
        rowPrefab.SetActive(false);
        RectTransform rowRect = rowPrefab.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(600, 60);
        Image rowBg = rowPrefab.AddComponent<Image>();
        rowBg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        HorizontalLayoutGroup hlg = rowPrefab.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.padding = new RectOffset(10, 10, 5, 5);

        MarkerTableRow rowScript = rowPrefab.AddComponent<MarkerTableRow>();

        rowScript.titleText = CreateText(rowPrefab.transform, "Title", "Title", font);
        rowScript.latText = CreateText(rowPrefab.transform, "Lat", "Lat", font);
        rowScript.lonText = CreateText(rowPrefab.transform, "Lon", "Lon", font);
        rowScript.altText = CreateText(rowPrefab.transform, "Alt", "Alt", font);
        rowScript.distText = CreateText(rowPrefab.transform, "Dist", "Dist", font);
        
        foreach(var t in new TextMeshProUGUI[] {rowScript.titleText, rowScript.latText, rowScript.lonText, rowScript.altText, rowScript.distText})
        {
            t.fontSize = 20;
            t.enableAutoSizing = true;
            t.margin = new Vector4(2, 2, 2, 2);
        }

        // Apply references
        SerializedObject so = new SerializedObject(tableScript);
        so.FindProperty("contentParent").objectReferenceValue = content.transform;
        so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        so.ApplyModifiedProperties();
    }

    private static Canvas GetOrCreateCanvas(GameObject obj)
    {
        Canvas canvas = obj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            obj.AddComponent<CanvasScaler>();
            obj.AddComponent<GraphicRaycaster>();
        }
        return canvas;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string defaultText, TMP_FontAsset font)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        
        tmp.rectTransform.sizeDelta = new Vector2(400, 50);
        return tmp;
    }
}
