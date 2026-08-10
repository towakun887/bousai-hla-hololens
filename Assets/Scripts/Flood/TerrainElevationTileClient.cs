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
    [SerializeField] private string geoidEndpoint =
        "https://vldb.gsi.go.jp/sokuchi/surveycalc/geoid/calcgh/cgi/geoidcalc.pl";
    [SerializeField, Min(1f)] private float requestTimeoutSeconds = 10f;

    public IEnumerator Fetch(double latitude, double longitude, Action<TerrainElevationResult> completed)
    {
        string latitudeText = latitude.ToString("R", CultureInfo.InvariantCulture);
        string longitudeText = longitude.ToString("R", CultureInfo.InvariantCulture);
        string elevationUrl = $"{endpoint}?lon={longitudeText}&lat={latitudeText}&outtype=JSON";

        double terrainElevation;
        string source;

        using (UnityWebRequest request = UnityWebRequest.Get(elevationUrl))
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
                    json, out terrainElevation, out source, out bool isNoData))
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
        }

        string geoidUrl =
            $"{geoidEndpoint}?outputType=json&latitude={latitudeText}&longitude={longitudeText}";
        double geoidHeight;

        using (UnityWebRequest request = UnityWebRequest.Get(geoidUrl))
        {
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(new TerrainElevationResult(
                    TerrainElevationStatus.NetworkError,
                    $"ジオイド高取得失敗: {request.error}", latitude, longitude));
                yield break;
            }

            string json = request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;
            if (!TerrainElevationJsonParser.TryParseGeoidHeight(json, out geoidHeight))
            {
                completed?.Invoke(new TerrainElevationResult(
                    TerrainElevationStatus.NetworkError,
                    "ジオイド高JSONを解析できませんでした", latitude, longitude));
                yield break;
            }
        }

        double correctedElevation = TerrainElevationMath.AddGeoidHeight(
            terrainElevation, geoidHeight);
        string correctedSource = string.IsNullOrEmpty(source)
            ? "ジオイド高補正"
            : $"{source} + ジオイド高補正";
        completed?.Invoke(new TerrainElevationResult(
            TerrainElevationStatus.Available, "取得完了（ジオイド高補正済み）",
            latitude, longitude, correctedElevation, correctedSource));
    }
}
