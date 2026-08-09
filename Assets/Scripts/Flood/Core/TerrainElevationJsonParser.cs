using System.Globalization;
using System.Text.RegularExpressions;

namespace Bousai.Flood
{
    public static class TerrainElevationJsonParser
    {
        private static readonly Regex ElevationPattern = new Regex(
            @"""elevation""\s*:\s*(?:""(?<quoted>[^""]*)""|(?<number>-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?))");

        private static readonly Regex SourcePattern = new Regex(
            @"""hsrc""\s*:\s*""(?<value>[^""]*)""");

        public static bool TryParse(
            string json, out double elevationMeters, out string source,
            out bool isNoData)
        {
            elevationMeters = 0d;
            source = string.Empty;
            isNoData = false;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            Match elevationMatch = ElevationPattern.Match(json);
            if (!elevationMatch.Success)
                return false;

            string rawElevation = elevationMatch.Groups["quoted"].Success
                ? elevationMatch.Groups["quoted"].Value
                : elevationMatch.Groups["number"].Value;
            Match sourceMatch = SourcePattern.Match(json);
            if (sourceMatch.Success)
                source = DecodeJsonString(sourceMatch.Groups["value"].Value);

            if (rawElevation == "-----")
            {
                isNoData = true;
                return true;
            }

            return double.TryParse(
                rawElevation, NumberStyles.Float,
                CultureInfo.InvariantCulture, out elevationMeters);
        }

        private static string DecodeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return value;

            try
            {
                // Regex.UnescapeはJSONのUnicodeエスケープを復号できる。
                // JSON固有のエスケープ済みスラッシュだけ先に通常化する。
                return Regex.Unescape(value.Replace("\\/", "/"));
            }
            catch
            {
                return value;
            }
        }
    }
}
