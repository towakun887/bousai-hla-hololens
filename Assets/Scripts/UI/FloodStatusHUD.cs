using Bousai.Flood;
using TMPro;
using UnityEngine;

public class FloodStatusHUD : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI floodStatusText;
    private FloodDepthController subscribedController;
    private string lastText;

    private void Awake()
    {
        if (floodStatusText == null)
            floodStatusText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Subscribe();
        Refresh();
    }

    private void Update()
    {
        if (subscribedController == null)
            Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        FloodDepthController controller = FloodDepthController.Instance;
        if (controller == null || controller == subscribedController)
            return;
        Unsubscribe();
        subscribedController = controller;
        subscribedController.StatusChanged += Refresh;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedController != null)
            subscribedController.StatusChanged -= Refresh;
        subscribedController = null;
    }

    private void Refresh()
    {
        if (floodStatusText == null)
            return;

        FloodDepthController controller = FloodDepthController.Instance;
        bool shouldShow = controller == null || controller.IsFloodDisplayEnabled;
        if (floodStatusText.enabled != shouldShow)
            floodStatusText.enabled = shouldShow;
        if (!shouldShow)
            return;

        FloodDepthResult result = controller != null ? controller.CurrentResult : null;
        string value;
        if (result == null)
        {
            value = $"洪水浸水高: N/A\n状態: 初期化待ち";
        }
        else
        {
            string location = result.HasLocation
                ? $"位置: {result.Latitude:F6}, {result.Longitude:F6}"
                : "位置: N/A";
            string time = result.RetrievedAt.ToString("HH:mm:ss");
            switch (result.Status)
            {
                case FloodDepthStatus.Available:
                    string reference = controller != null
                        ? controller.HeightReferenceStatus
                        : "Pane: N/A";
                    value = $"洪水浸水高: {result.DepthBand.Label}\n{location}\n取得: {time} | {reference}";
                    break;
                case FloodDepthStatus.Mock:
                    string mockReference = controller != null
                        ? controller.HeightReferenceStatus
                        : "Pane: N/A";
                    value = $"洪水浸水高: {result.DepthBand.DisplayHeightMeters:0.0}m (Mock)\n位置: N/A\n{mockReference}";
                    break;
                case FloodDepthStatus.Loading:
                    value = $"洪水浸水高: 取得中\n{location}\n更新: {time}";
                    break;
                case FloodDepthStatus.WaitingForCalibration:
                    value = $"洪水浸水高: N/A\n状態: {result.Message}";
                    break;
                default:
                    value = $"洪水浸水高: N/A\n{location}\n状態: {result.Message}\n更新: {time}";
                    break;
            }
        }

        if (lastText == value)
            return;
        lastText = value;
        floodStatusText.text = value;
    }
}
