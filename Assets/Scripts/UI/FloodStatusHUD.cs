using Bousai.Dosha;
using Bousai.Flood;
using TMPro;
using UnityEngine;

public class FloodStatusHUD : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI floodStatusText;
    private FloodDepthController subscribedController;
    private DoshaTileClient subscribedDoshaClient;
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
        if (subscribedController == null || subscribedDoshaClient == null)
            Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        FloodDepthController controller = FloodDepthController.Instance;
        if (controller != null && controller != subscribedController)
        {
            if (subscribedController != null)
                subscribedController.StatusChanged -= Refresh;
            subscribedController = controller;
            subscribedController.StatusChanged += Refresh;
        }

        DoshaTileClient doshaClient = FindFirstObjectByType<DoshaTileClient>();
        if (doshaClient != null && doshaClient != subscribedDoshaClient)
        {
            if (subscribedDoshaClient != null)
                subscribedDoshaClient.StatusChanged -= Refresh;
            subscribedDoshaClient = doshaClient;
            subscribedDoshaClient.StatusChanged += Refresh;
        }
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedController != null)
            subscribedController.StatusChanged -= Refresh;
        if (subscribedDoshaClient != null)
            subscribedDoshaClient.StatusChanged -= Refresh;
        subscribedController = null;
        subscribedDoshaClient = null;
    }

    private void Refresh()
    {
        if (floodStatusText == null)
            return;

        FloodDepthController controller = FloodDepthController.Instance;
        bool shouldShow = subscribedDoshaClient != null || controller == null || controller.IsFloodDisplayEnabled;
        if (floodStatusText.enabled != shouldShow)
            floodStatusText.enabled = shouldShow;
        if (!shouldShow)
            return;

        FloodDepthResult result = controller != null ? controller.CurrentResult : null;
        string value;
        if (controller != null && !controller.IsFloodDisplayEnabled)
        {
            value = "洪水表示: OFF\nFlood Pane: 非表示";
        }
        else if (result == null)
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

        value += "\n\n" + BuildDoshaStatus();

        if (lastText == value)
            return;
        lastText = value;
        floodStatusText.text = value;
    }

    private string BuildDoshaStatus()
    {
        DoshaTileClient client = subscribedDoshaClient;
        if (client == null)
            return "土砂災害: Client待ち";

        DoshaHazardResult result = client.CurrentResult;
        if (result == null)
            return $"土砂災害: N/A\n状態: {client.DiagnosticStatus}";

        string time = result.RetrievedAt.ToString("HH:mm:ss");
        string types = GetHazardTypesLabel(result.HazardTypes);
        return $"土砂災害: {result.Message}\n種別: {types}\n位置: {result.Latitude:F6}, {result.Longitude:F6}\n状態: {result.Status} | 更新: {time}";
    }

    private static string GetHazardTypesLabel(DoshaHazardType types)
    {
        if (types == DoshaHazardType.None)
            return "該当なし";

        string value = string.Empty;
        if ((types & DoshaHazardType.DebrisFlow) != 0)
            value = "土石流";
        if ((types & DoshaHazardType.SteepSlopeCollapse) != 0)
            value += (value.Length > 0 ? " / " : string.Empty) + "急傾斜地の崩壊";
        if ((types & DoshaHazardType.Landslide) != 0)
            value += (value.Length > 0 ? " / " : string.Empty) + "地すべり";
        return value;
    }
}
