using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarkerVisual : MonoBehaviour
{
    public enum LodState
    {
        Hidden,
        Text,
        Full
    }

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
    [Tooltip("Color used for marker_kind=other and unknown marker kinds.")]
    [SerializeField] private Color normalColor = Color.blue;
    [Tooltip("Color used for marker_kind=hazard and the legacy Warning mock value.")]
    [SerializeField] private Color warningColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color fireColor = new Color(0.85f, 0.08f, 0.05f);
    [SerializeField] private Color buildingDamageColor = new Color(1f, 0.85f, 0.1f);

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly float ScaleExponent = Mathf.Log((100f / 3f) / 2f) / Mathf.Log(100f / 3f);
    private static readonly float ScaleFactor = 2f / Mathf.Pow(3f, ScaleExponent);

    private MaterialPropertyBlock propertyBlock;
    private GoogleDriveImageLoader.ImageRequest imageRequest;
    private LodState lod = LodState.Hidden;
    private bool debugEnabled;
    private int setupVersion;
    private float nextTextUpdateTime;
    private float nextDebugUpdateTime;

    public MarkerData Data { get; private set; }
    public LodState CurrentLod => lod;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.transform.localPosition += new Vector3(0f, 0f, -0.01f);
        }
    }

    public void Setup(MarkerData data)
    {
        setupVersion++;
        CancelImageRequest();
        Data = data;

        SetTextIfChanged(timeText, data.serializedTime);
        SetTextIfChanged(kindText, data.markerKind);
        SetTextIfChanged(titleText, string.IsNullOrEmpty(data.title) ? "<i>No Title</i>" : data.title);
        SetTextIfChanged(detailText, string.IsNullOrEmpty(data.detail) ? "<color=#888888>説明はありません</color>" : data.detail);
        SetTextIfChanged(debugGpsText, $"GPS: {data.latitude:F6}, {data.longitude:F6}, {data.altitude:F1}m");

        if (pinRenderer != null)
        {
            Color markerColor = GetMarkerColor(data.markerKind);
            pinRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorId, markerColor);
            propertyBlock.SetColor(LegacyColorId, markerColor);
            pinRenderer.SetPropertyBlock(propertyBlock);
        }

        if (markerImage != null)
        {
            markerImage.texture = null;
        }
        if (errorOverlay != null)
        {
            errorOverlay.SetActive(false);
        }
        SetDebugMode(debugEnabled);
    }

    private Color GetMarkerColor(string markerKind)
    {
        switch (markerKind?.Trim().ToLowerInvariant())
        {
            case "fire":
                return fireColor;
            case "hazard":
            case "warning":
                return warningColor;
            case "building_damage":
                return buildingDamageColor;
            case "other":
            default:
                return normalColor;
        }
    }

    public void SetLod(LodState state)
    {
        if (lod == state)
        {
            return;
        }
        lod = state;
        if (debugPanel != null)
        {
            debugPanel.SetActive(debugEnabled && state != LodState.Hidden);
        }

        bool full = state == LodState.Full;
        bool hasPhoto = Data != null && Data.mediaType == "photo" && !string.IsNullOrEmpty(Data.fileId);
        if (imageContainer != null)
        {
            imageContainer.SetActive(full && hasPhoto);
        }
        if (markerImage != null)
        {
            markerImage.gameObject.SetActive(full && hasPhoto);
        }

        if (!full)
        {
            CancelImageRequest();
            if (markerImage != null)
            {
                markerImage.texture = null;
            }
            if (errorOverlay != null)
            {
                errorOverlay.SetActive(false);
            }
        }
        else if (hasPhoto)
        {
            RequestImage();
        }
    }

    public void UpdateDistanceAndScale(float distance)
    {
        distance = Mathf.Max(distance, 0.1f);
        float scale = Mathf.Clamp(ScaleFactor * Mathf.Pow(distance, ScaleExponent), 0.2f, 500f);
        transform.localScale = new Vector3(scale, scale, scale);
        if (distanceText != null && Time.unscaledTime >= nextTextUpdateTime)
        {
            nextTextUpdateTime = Time.unscaledTime + 0.5f;
            distanceText.SetText("{0:0.0}m", distance);
        }
    }

    public void UpdateDebugPosition(Vector3 position)
    {
        if (debugEnabled && debugSlamText != null && Time.unscaledTime >= nextDebugUpdateTime)
        {
            nextDebugUpdateTime = Time.unscaledTime + 0.5f;
            debugSlamText.SetText("SLAM: {0:0.00}, {1:0.00}, {2:0.00}", position.x, position.y, position.z);
        }
    }

    public void FaceCamera(Transform cameraTransform)
    {
        Vector3 direction = cameraTransform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-direction);
        }
    }

    public void SetDebugMode(bool isDebug)
    {
        debugEnabled = isDebug;
        if (debugPanel != null)
        {
            debugPanel.SetActive(isDebug && lod != LodState.Hidden);
        }
    }

    public void PrepareForPool()
    {
        setupVersion++;
        CancelImageRequest();
        if (markerImage != null)
        {
            markerImage.texture = null;
        }
        if (errorOverlay != null)
        {
            errorOverlay.SetActive(false);
        }
        lod = LodState.Hidden;
        Data = null;
    }

    private void RequestImage()
    {
        CancelImageRequest();
        if (GoogleDriveImageLoader.Instance == null || Data == null)
        {
            return;
        }

        int version = setupVersion;
        imageRequest = GoogleDriveImageLoader.Instance.LoadImage(Data.fileId,
            texture =>
            {
                if (version == setupVersion && lod == LodState.Full && markerImage != null)
                {
                    markerImage.texture = texture;
                }
            },
            error =>
            {
                if (version != setupVersion || lod != LodState.Full)
                {
                    return;
                }
                if (errorOverlay != null)
                {
                    errorOverlay.SetActive(true);
                }
                SetTextIfChanged(errorDetailText, error);
            });
    }

    private void CancelImageRequest()
    {
        imageRequest?.Dispose();
        imageRequest = null;
    }

    private static void SetTextIfChanged(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
        {
            target.text = value;
        }
    }
}
