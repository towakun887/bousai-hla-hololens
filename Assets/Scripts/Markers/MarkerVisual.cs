using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MarkerVisual : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI kindText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI detailText;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private RawImage markerImage;
    [SerializeField] private GameObject errorOverlay;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI errorDetailText;
    [SerializeField] private GameObject imageContainer;
    
    [Header("Debug UI")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private TextMeshProUGUI debugGpsText;
    [SerializeField] private TextMeshProUGUI debugSlamText;

    [Header("Visuals")]
    [SerializeField] private MeshRenderer pinRenderer;
    [SerializeField] private Color normalColor = Color.blue;
    [SerializeField] private Color warningColor = new Color(1f, 0.5f, 0f); // Orange

    public MarkerData Data { get; private set; }
    private Camera mainCamera;
    private float distUpdateTimer = 0f;

    private void Start()
    {
        mainCamera = Camera.main;

        // 背景のQuad（pinRenderer等）とテキストが全く同じZ座標にあると
        // Zファイティング（斜めに切れるバグ）が起きるため、Canvasをわずかに手前(Zマイナス方向)にずらす
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.transform.localPosition += new Vector3(0, 0, -0.01f);
        }
    }

    public void Setup(MarkerData data)
    {
        Data = data;
        
        if (timeText) timeText.text = data.serializedTime;
        if (kindText) kindText.text = data.markerKind;
        if (titleText) titleText.text = string.IsNullOrEmpty(data.title) ? "<i>No Title<i>" : data.title;
        if (detailText) detailText.text = string.IsNullOrEmpty(data.detail) ? "<color=#888888>説明はありません</color>" : data.detail;

        if (pinRenderer)
        {
            pinRenderer.material.color = data.markerKind == "Warning" ? warningColor : normalColor;
        }

        if (debugPanel) debugPanel.SetActive(false); // default off
        if (debugGpsText) debugGpsText.text = $"GPS: {data.latitude:F6}, {data.longitude:F6}, {data.altitude:F1}m";
        if (debugSlamText) debugSlamText.text = $"SLAM: {transform.position.x:F2}, {transform.position.y:F2}, {transform.position.z:F2}";

        if (markerImage)
        {
            markerImage.texture = null;
            markerImage.gameObject.SetActive(true);
        }
        if (errorOverlay) errorOverlay.SetActive(false);
        if (imageContainer) imageContainer.SetActive(true);

        bool isPhoto = data.mediaType == "photo";

        if (!isPhoto)
        {
            // photo以外の場合は画像コンテナごと非表示にする
            if (imageContainer) imageContainer.SetActive(false);
            if (markerImage) markerImage.gameObject.SetActive(false);
            if (errorOverlay) errorOverlay.SetActive(false);
        }
        else if (!string.IsNullOrEmpty(data.fileId))
        {
            if (GoogleDriveImageLoader.Instance != null)
            {
                GoogleDriveImageLoader.Instance.LoadImage(data.fileId,
                    onSuccess: (tex) => {
                        if (markerImage)
                        {
                            markerImage.texture = tex;
                        }
                    },
                    onError: (err) => {
                        if (errorOverlay)
                        {
                            errorOverlay.SetActive(true);
                        }
                        if (errorDetailText)
                        {
                            errorDetailText.text = err;
                        }
                    }
                );
            }
        }
    }

    public void SetDebugMode(bool isDebug)
    {
        if (debugPanel) debugPanel.SetActive(isDebug);
    }

    private void Update()
    {
        if (mainCamera != null)
        {
            // 距離に応じたスケール変更
            // 【要件】
            // 1. 3m地点での実際のScaleは 2.0
            // 2. 100m地点での「見た目の大きさ」が、3m地点でScale 1.0を置いた時と同じになるようにする
            //    (見た目は Scale / 距離 に比例。つまり100mでのScaleは 100/3 = 33.333...)
            float dist = Vector3.Distance(transform.position, mainCamera.transform.position);
            dist = Mathf.Max(dist, 0.1f);

            // S(d) = A * d^B のカーブを事前計算 (本来は定数ですが分かりやすさのため記載)
            float d1 = 3.0f, s1 = 2.0f;
            float d2 = 100.0f, s2 = 100.0f / 3.0f; 
            float exponent = Mathf.Log(s2 / s1) / Mathf.Log(d2 / d1); // 約 0.802
            float A = s1 / Mathf.Pow(d1, exponent); // 約 0.828

            float scaleMultiplier = A * Mathf.Pow(dist, exponent);
            
            // 極端な縮小・拡大を防ぐクランプ (1000m地点ではScale約210になるため上限に余裕を持たせる)
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.2f, 500f);
            
            transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

            if (distanceText != null)
            {
                distUpdateTimer -= Time.deltaTime;
                if (distUpdateTimer <= 0f)
                {
                    distanceText.text = $"{dist:F1}m";
                    distUpdateTimer = 0.5f;
                }
            }
        }
        
        if (debugPanel != null && debugPanel.activeSelf && debugSlamText != null)
        {
            debugSlamText.text = $"SLAM: {transform.position.x:F2}, {transform.position.y:F2}, {transform.position.z:F2}";
        }
    }
}
