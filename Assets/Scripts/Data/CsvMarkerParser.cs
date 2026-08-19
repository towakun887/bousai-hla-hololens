using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Serialization;

public class CsvMarkerParser : MonoBehaviour
{
    [SerializeField] private bool verboseDevelopmentLogs;
    [SerializeField, FormerlySerializedAs("csvUrl")]
    private string firstCsvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSi8T3T1vmz4eQBrMOxWXYljtujKaNYg0IYOZI_fZxhH-l0diNpJfX2cjwjAVbVWds9g_d3_Y2iQfC9/pub?gid=1057226757&single=true&output=csv";
    [SerializeField] private string secondCsvUrl;

    public Action<List<MarkerData>, List<MarkerData>> OnCsvParsed;
    public Action<string> OnError;

    public void LoadCsv()
    {
        MockModeSettings mockSettings = MockModeSettings.Instance;
        if (mockSettings != null && mockSettings.UseCsvMarkerMock)
        {
            List<MarkerData> secondMockMarkers = string.IsNullOrWhiteSpace(secondCsvUrl)
                ? new List<MarkerData>()
                : CreateMockMarkers(mockSettings.MarkerCount);
            OnCsvParsed?.Invoke(CreateMockMarkers(mockSettings.MarkerCount), secondMockMarkers);
            return;
        }
        StartCoroutine(DownloadAndParseCsvCoroutine());
    }

    public void SetCsvUrl(string url)
    {
        firstCsvUrl = url;
    }

    public void SetCsvUrls(string firstUrl, string secondUrl)
    {
        firstCsvUrl = firstUrl;
        secondCsvUrl = secondUrl;
    }

    private IEnumerator DownloadAndParseCsvCoroutine()
    {
        List<MarkerData> firstMarkers = new List<MarkerData>();
        List<MarkerData> secondMarkers = new List<MarkerData>();
        bool downloadedAnyCsv = false;

        string[] urls = { firstCsvUrl, secondCsvUrl };
        List<MarkerData>[] results = { firstMarkers, secondMarkers };
        for (int i = 0; i < urls.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(urls[i]))
            {
                continue;
            }

            using (UnityWebRequest www = UnityWebRequest.Get(urls[i]))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string message = $"CSV {i + 1}: {www.error}";
                    Debug.LogError($"[CSV] Error downloading {message}");
                    OnError?.Invoke(message);
                    continue;
                }

                results[i].AddRange(ParseCsv(www.downloadHandler.text));
                downloadedAnyCsv = true;
                LogVerbose($"[CSV] Successfully parsed {results[i].Count} markers from CSV {i + 1}.");
            }
        }

        if (downloadedAnyCsv)
        {
            OnCsvParsed?.Invoke(firstMarkers, secondMarkers);
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
                marker.serializedTime = FormatSerializedTime(
                    GetValue(values, headerMap, "serialized_time"));
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

    private static string FormatSerializedTime(string value)
    {
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out DateTimeOffset parsedTime))
        {
            return parsedTime.ToString("yyyy/MM/dd hh:mm:ss", CultureInfo.InvariantCulture);
        }

        return value;
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

    private List<MarkerData> CreateMockMarkers(int count)
    {
        MockModeSettings mockSettings = MockModeSettings.Instance;
        List<MarkerData> result = new List<MarkerData>(count);
        double originLatitude = 35.6895;
        double originLongitude = 139.6917;
        if (LocationResolver.Instance != null && LocationResolver.Instance.IsCalibrated())
        {
            LocationData origin = LocationResolver.Instance.GetOriginGps();
            originLatitude = origin.latitude;
            originLongitude = origin.longitude;
        }
        for (int i = 0; i < count; i++)
        {
            float distance = 20f + i * 4f;
            double latitudeOffset = distance / 111320.0;
            bool photo = mockSettings != null && mockSettings.MarkerMockIncludesPhotos && i % 3 == 0;
            result.Add(new MarkerData
            {
                serializedTime = "2026/08/05 12:00:00",
                markerKind = i % 5 == 0 ? "Warning" : "Info",
                title = $"Mock Marker {i + 1}",
                detail = $"Performance test marker at approximately {distance:F0}m",
                latitude = originLatitude + latitudeOffset,
                longitude = originLongitude,
                altitude = 0,
                eunRotation = Quaternion.identity,
                mediaType = photo ? "photo" : string.Empty,
                fileId = photo && mockSettings != null ? mockSettings.MarkerMockImageFileId : string.Empty
            });
        }
        return result;
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogVerbose(string message)
    {
        if (verboseDevelopmentLogs && Debug.isDebugBuild) Debug.Log(message);
    }
}
