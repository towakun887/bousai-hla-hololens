using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MarkerTableUI : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private float refreshInterval = 1.0f;

    private float timer = 0f;
    private List<MarkerTableRow> activeRows = new List<MarkerTableRow>();

    private void Update()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = refreshInterval;
            RefreshTable();
        }
    }

    public void RefreshTable()
    {
        if (MarkerManager.Instance == null || contentParent == null || rowPrefab == null) return;
        
        var markers = MarkerManager.Instance.GetMarkers();
        if (markers == null) return;

        // Adjust row count
        int spawnFailsafe = 0;
        while (activeRows.Count < markers.Count && spawnFailsafe < 50)
        {
            spawnFailsafe++;
            GameObject newRow = Instantiate(rowPrefab, contentParent);
            newRow.SetActive(true);
            
            MarkerTableRow rowScript = newRow.GetComponent<MarkerTableRow>();
            if (rowScript == null)
            {
                rowScript = newRow.AddComponent<MarkerTableRow>();
            }
            
            // Auto-wire if not assigned
            if (rowScript.titleText == null)
            {
                var texts = newRow.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 5)
                {
                    rowScript.titleText = texts[0];
                    rowScript.latText = texts[1];
                    rowScript.lonText = texts[2];
                    rowScript.altText = texts[3];
                    rowScript.distText = texts[4];
                }
            }
            
            activeRows.Add(rowScript);
        }
        
        while (activeRows.Count > markers.Count)
        {
            MarkerTableRow excessRow = activeRows[activeRows.Count - 1];
            activeRows.RemoveAt(activeRows.Count - 1);
            if (excessRow != null) Destroy(excessRow.gameObject);
        }

        Camera camera = Camera.main;
        Vector3 cameraPos = camera != null ? camera.transform.position : Vector3.zero;

        // Update rows
        for (int i = 0; i < markers.Count; i++)
        {
            MarkerVisual visual = markers[i];
            MarkerTableRow row = activeRows[i];
            
            if (visual != null && visual.Data != null && row != null)
            {
                float dist = Vector3.Distance(cameraPos, visual.transform.position);
                row.UpdateData(visual.Data, dist);
            }
        }
    }
}

public class MarkerTableRow : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI latText;
    public TextMeshProUGUI lonText;
    public TextMeshProUGUI altText;
    public TextMeshProUGUI distText;

    public void UpdateData(MarkerData data, float distance)
    {
        if (titleText) titleText.SetText(string.IsNullOrEmpty(data.title) ? "<color=#888888>No Title</color>" : data.title);
        if (latText) latText.SetText("{0:F5}", (float)data.latitude);
        if (lonText) lonText.SetText("{0:F5}", (float)data.longitude);
        if (altText) altText.SetText("{0:F1}m", (float)data.altitude);
        if (distText) distText.SetText("{0:F1}m", distance);
    }
}
