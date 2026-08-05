using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

public class CsvMarkerParser : MonoBehaviour
{
    [SerializeField]
    private string csvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSi8T3T1vmz4eQBrMOxWXYljtujKaNYg0IYOZI_fZxhH-l0diNpJfX2cjwjAVbVWds9g_d3_Y2iQfC9/pub?gid=1057226757&single=true&output=csv";

    public Action<List<MarkerData>> OnCsvParsed;
    public Action<string> OnError;

    public void LoadCsv()
    {
        StartCoroutine(DownloadAndParseCsvCoroutine());
    }

    public void SetCsvUrl(string url)
    {
        csvUrl = url;
    }

    private IEnumerator DownloadAndParseCsvCoroutine()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(csvUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CSV] Error downloading CSV: {www.error}");
                OnError?.Invoke(www.error);
                yield break;
            }

            string csvText = www.downloadHandler.text;
            Debug.Log($"[CSV] Downloaded CSV Data:\n{csvText}");
            List<MarkerData> markers = ParseCsv(csvText);
            Debug.Log($"[CSV] Successfully parsed {markers.Count} markers.");
            OnCsvParsed?.Invoke(markers);
        }
    }

    private List<MarkerData> ParseCsv(string csvText)
    {
        List<MarkerData> markers = new List<MarkerData>();
        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1) return markers;

        string[] headers = ParseCsvLine(lines[0]);
        
        Dictionary<string, int> headerMap = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
        {
            headerMap[headers[i]] = i;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                string[] values = ParseCsvLine(lines[i]);
                if (values.Length < headerMap.Count) continue;

                MarkerData marker = new MarkerData();
                marker.serializedTime = GetValue(values, headerMap, "serialized_time");
                marker.markerKind = GetValue(values, headerMap, "marker_kind");
                marker.title = GetValue(values, headerMap, "title");
                marker.detail = GetValue(values, headerMap, "detail");
                
                double.TryParse(GetValue(values, headerMap, "latitude"), out marker.latitude);
                double.TryParse(GetValue(values, headerMap, "longitude"), out marker.longitude);
                double.TryParse(GetValue(values, headerMap, "altitude"), out marker.altitude);
                
                float x = 0, y = 0, z = 0, w = 1;
                float.TryParse(GetValue(values, headerMap, "eun_rotation_x"), out x);
                float.TryParse(GetValue(values, headerMap, "eun_rotation_y"), out y);
                float.TryParse(GetValue(values, headerMap, "eun_rotation_z"), out z);
                float.TryParse(GetValue(values, headerMap, "eun_rotation_w"), out w);
                marker.eunRotation = new Quaternion(x, y, z, w);
                
                marker.driveUrl = GetValue(values, headerMap, "drive_url");
                marker.fileId = GetValue(values, headerMap, "file_id");
                marker.mediaType = GetValue(values, headerMap, "media_type");

                markers.Add(marker);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse CSV line {i}: {ex.Message}");
            }
        }

        return markers;
    }

    private string GetValue(string[] values, Dictionary<string, int> headerMap, string columnName)
    {
        if (headerMap.TryGetValue(columnName, out int index))
        {
            if (index >= 0 && index < values.Length)
            {
                return values[index];
            }
        }
        return "";
    }

    private string[] ParseCsvLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentValue = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    currentValue += '\"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentValue);
                currentValue = "";
            }
            else
            {
                currentValue += c;
            }
        }
        result.Add(currentValue);

        return result.ToArray();
    }
}
