using System;
using System.Collections;
using System.Globalization;
using Bousai.Flood;
using UnityEngine;
using UnityEngine.Networking;

// 既存シーン参照との互換性のためクラス名は維持するが、取得方式は国土地理院JSON API。
public class TerrainElevationTileClient : MonoBehaviour
{
    [SerializeField] private string endpoint =
        "https://cyberjapandata2.gsi.go.jp/general/dem/scripts/getelevation.php";
    [SerializeField, Min(1f)] private float requestTimeoutSeconds = 10f;

    public IEnumerator Fetch(double latitude, double longitude, Action<TerrainElevationResult> completed)
    {
        string latitudeText = latitude.ToString("R", CultureInfo.InvariantCulture);
        string longitudeText = longitude.ToString("R", CultureInfo.InvariantCulture);
        string url = $"{endpoint}?lon={longitudeText}&lat={latitudeText}&outtype=JSON";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(new TerrainElevationResult(
                    TerrainElevationStatus.NetworkError,
                    $"標高取得失敗: {request.error}", latitude, longitude));
                yield break;
            }

            string json = request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;
            if (!TerrainElevationJsonParser.TryParse(
                    json, out double elevation, out string source, out bool isNoData))
            {
                completed?.Invoke(new TerrainElevationResult(
                    TerrainElevationStatus.NetworkError,
                    "標高JSONを解析できませんでした", latitude, longitude));
                yield break;
            }

            if (isNoData)
            {
                completed?.Invoke(new TerrainElevationResult(
                    TerrainElevationStatus.NoData,
                    "標高データなし", latitude, longitude));
                yield break;
            }

            completed?.Invoke(new TerrainElevationResult(
                TerrainElevationStatus.Available, "取得完了",
                latitude, longitude, elevation, source));
        }
    }
}
