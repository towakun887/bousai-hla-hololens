using System;
using UnityEngine;

namespace Bousai.Flood
{
    public static class FloodTileMath
    {
        public const int DefaultTileSize = 256;
        public const double MaximumMercatorLatitude = 85.05112878d;

        public static FloodTileCoordinate FromLatitudeLongitude(double latitude, double longitude, int zoom, int tileSize = DefaultTileSize)
        {
            if (zoom < 0 || zoom > 30)
                throw new ArgumentOutOfRangeException(nameof(zoom));
            if (tileSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(tileSize));

            double clampedLatitude = Math.Max(-MaximumMercatorLatitude, Math.Min(MaximumMercatorLatitude, latitude));
            double wrappedLongitude = WrapLongitude(longitude);
            double mapSize = Math.Pow(2d, zoom);
            double globalTileX = (wrappedLongitude + 180d) / 360d * mapSize;
            double latitudeRadians = clampedLatitude * Math.PI / 180d;
            double globalTileY = (1d - Math.Log(Math.Tan(latitudeRadians) + 1d / Math.Cos(latitudeRadians)) / Math.PI) / 2d * mapSize;

            globalTileX = Math.Max(0d, Math.Min(BitDecrement(mapSize), globalTileX));
            globalTileY = Math.Max(0d, Math.Min(BitDecrement(mapSize), globalTileY));

            int tileX = (int)Math.Floor(globalTileX);
            int tileY = (int)Math.Floor(globalTileY);
            int pixelX = Math.Min(tileSize - 1, (int)Math.Floor((globalTileX - tileX) * tileSize));
            int pixelY = Math.Min(tileSize - 1, (int)Math.Floor((globalTileY - tileY) * tileSize));
            return new FloodTileCoordinate(zoom, tileX, tileY, pixelX, pixelY);
        }

        public static bool ShouldRefresh(float elapsedSeconds, Vector3 previousPosition, Vector3 currentPosition, float minimumSeconds, float minimumDistanceMeters)
        {
            if (elapsedSeconds < minimumSeconds)
                return false;

            float deltaX = currentPosition.x - previousPosition.x;
            float deltaZ = currentPosition.z - previousPosition.z;
            return deltaX * deltaX + deltaZ * deltaZ >= minimumDistanceMeters * minimumDistanceMeters;
        }

        public static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static double WrapLongitude(double longitude)
        {
            double wrapped = (longitude + 180d) % 360d;
            if (wrapped < 0d)
                wrapped += 360d;
            return wrapped - 180d;
        }

        private static double BitDecrement(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            return BitConverter.Int64BitsToDouble(bits - 1);
        }
    }
}
