using UnityEngine;
using TMPro;

public class PhoneVisualizer : MonoBehaviour
{
    public static PhoneVisualizer Instance { get; private set; }

    [SerializeField] private GameObject overlayContainer;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI gpsText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private MeshRenderer frameRenderer;
    [SerializeField] private GameObject lostWarning;

    [Header("Colors")]
    [SerializeField] private Color colorDetect = Color.white;
    [SerializeField] private Color colorConnect = Color.yellow;
    [SerializeField] private Color colorGps = Color.cyan;
    [SerializeField] private Color colorDone = Color.green;

    private Vector3 targetPos;
    private Quaternion targetRot;
    private bool isVisible = false;
    private bool isLost = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        // else Destroy(gameObject);
        
        if (overlayContainer != null) overlayContainer.SetActive(false);
        if (frameRenderer != null) 
        {
            frameRenderer.enabled = false;
            // 照明の影響を受けず、裏側から見ても透明にならないように UI/Default シェーダーを適用
            Material unlitMat = new Material(Shader.Find("UI/Default"));
            frameRenderer.material = unlitMat;
        }
    }

    private void Update()
    {
        if (isVisible)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }
    }

    public void UpdateTransform(Vector3 pos, Quaternion rot)
    {
        // 1cm手前（-Z方向）に浮かせることで、スマホ画面（QRコード）とのZファイティングや埋没を防ぐ
        targetPos = pos + rot * new Vector3(0f, 0f, -0.015f);
        targetRot = rot;
        if (!isVisible)
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
            if (overlayContainer != null) overlayContainer.SetActive(true);
            if (frameRenderer != null) frameRenderer.enabled = true;
            isVisible = true;
            isLost = false;
            if (lostWarning != null) lostWarning.SetActive(false);
        }
    }

    public void SetLost(bool lost)
    {
        isLost = lost;
        if (lostWarning != null) lostWarning.SetActive(lost);
        if (lost && frameRenderer != null)
        {
            frameRenderer.material.color = Color.red;
        }
    }

    public void UpdateStatus(string state, string gpsInfo, string progress)
    {
        if (statusText != null) statusText.text = state;
        if (gpsText != null) gpsText.text = gpsInfo;
        if (progressText != null) progressText.text = progress;

        Color targetColor = colorDetect;
        if (state.Contains("接続中")) targetColor = colorConnect;
        else if (state.Contains("完了")) targetColor = colorDone;
        else if (gpsInfo.Contains("Tracking")) targetColor = colorGps;

        if (frameRenderer != null && !isLost)
        {
            frameRenderer.material.color = targetColor;
        }
    }

    public void Hide()
    {
        isVisible = false;
        if (overlayContainer != null) overlayContainer.SetActive(false);
        if (frameRenderer != null) frameRenderer.enabled = false;
    }
}
