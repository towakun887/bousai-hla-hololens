using System;
using System.Collections;
using Bousai.Flood;
using UnityEngine;
using UnityEngine.Networking;

public class FloodDepthTileClient : MonoBehaviour
{
    [SerializeField] private string tileUrlTemplate =
        "https://disaportaldata.gsi.go.jp/raster/01_flood_l2_shinsuishin_data/{z}/{x}/{y}.png";
    [SerializeField, Range(0, 22)] private int zoomLevel = 17;
    [SerializeField, Range(0, 32)] private int colorTolerance = 9;
    [SerializeField, Min(1f)] private float requestTimeoutSeconds = 10f;

    private bool hasCachedTile;
    private FloodTileCoordinate cachedTile;
    private Color32[] cachedPixels;
    private int cachedWidth;
    private int cachedHeight;

    public IEnumerator Fetch(double latitude, double longitude, Action<FloodDepthResult> completed)
    {
        FloodTileCoordinate coordinate;
        try
        {
            coordinate = FloodTileMath.FromLatitudeLongitude(latitude, longitude, zoomLevel);
        }
        catch (Exception exception)
        {
            completed?.Invoke(CreateResult(
                FloodDepthStatus.NetworkError, $"座標変換失敗: {exception.Message}",
                latitude, longitude, default, default));
            yield break;
        }

        if (hasCachedTile && coordinate.IsSameTile(cachedTile) && cachedPixels != null)
        {
            completed?.Invoke(ReadCachedPixel(latitude, longitude, coordinate));
            yield break;
        }

        string url = tileUrlTemplate
            .Replace("{z}", coordinate.Zoom.ToString())
            .Replace("{x}", coordinate.X.ToString())
            .Replace("{y}", coordinate.Y.ToString());

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, false))
        {
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                FloodDepthStatus status = request.responseCode == 404
                    ? FloodDepthStatus.TileUnavailable
                    : FloodDepthStatus.NetworkError;
                completed?.Invoke(CreateResult(
                    status,
                    request.responseCode == 404 ? "該当タイルなし" : $"取得失敗: {request.error}",
                    latitude, longitude, coordinate, default));
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                completed?.Invoke(CreateResult(
                    FloodDepthStatus.NetworkError, "PNGを展開できませんでした",
                    latitude, longitude, coordinate, default));
                yield break;
            }

            try
            {
                cachedPixels = texture.GetPixels32();
                cachedWidth = texture.width;
                cachedHeight = texture.height;
                cachedTile = coordinate;
                hasCachedTile = cachedPixels.Length == cachedWidth * cachedHeight;
            }
            catch (Exception exception)
            {
                hasCachedTile = false;
                cachedPixels = null;
                Debug.LogWarning($"[FloodDepthTileClient] PNGピクセル読込失敗: {exception.Message}");
            }
            finally
            {
                Destroy(texture);
            }
        }

        if (!hasCachedTile)
        {
            completed?.Invoke(CreateResult(
                FloodDepthStatus.NetworkError, "PNGピクセルを読み取れませんでした",
                latitude, longitude, coordinate, default));
            yield break;
        }

        completed?.Invoke(ReadCachedPixel(latitude, longitude, coordinate));
    }

    private FloodDepthResult ReadCachedPixel(double latitude, double longitude, FloodTileCoordinate coordinate)
    {
        if (coordinate.PixelX < 0 || coordinate.PixelX >= cachedWidth ||
            coordinate.PixelY < 0 || coordinate.PixelY >= cachedHeight)
        {
            return CreateResult(
                FloodDepthStatus.NetworkError, "PNGピクセル座標が範囲外です",
                latitude, longitude, coordinate, default);
        }

        // Webタイルは左上原点、Texture2D.GetPixels32()は左下原点。
        int textureY = cachedHeight - 1 - coordinate.PixelY;
        Color32 sampled = cachedPixels[textureY * cachedWidth + coordinate.PixelX];
        if (sampled.a == 0)
        {
            return CreateResult(
                FloodDepthStatus.NoData, "浸水深データなし",
                latitude, longitude, coordinate, sampled);
        }

        if (!FloodDepthLegend.TryClassify(sampled, colorTolerance, out FloodDepthBand band, out bool exactMatch))
        {
            return CreateResult(
                FloodDepthStatus.UnknownColor,
                $"未定義色 RGB({sampled.r},{sampled.g},{sampled.b})",
                latitude, longitude, coordinate, sampled);
        }

        string match = exactMatch ? "完全一致" : $"近似一致 ±{colorTolerance}";
        return new FloodDepthResult(
            FloodDepthStatus.Available, match, true, latitude, longitude,
            coordinate, sampled, band, DateTime.Now);
    }

    private static FloodDepthResult CreateResult(
        FloodDepthStatus status,
        string message,
        double latitude,
        double longitude,
        FloodTileCoordinate coordinate,
        Color32 sampled)
    {
        return new FloodDepthResult(
            status, message, true, latitude, longitude,
            coordinate, sampled, default, DateTime.Now);
    }
}
