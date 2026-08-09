using System;
using UnityEngine;

namespace Bousai.Flood
{
    public enum FloodHeightReferenceMode
    {
        GroundSurface = 0,
        TerrainElevation = 1
    }

    public enum FloodDepthStatus
    {
        WaitingForCalibration,
        Loading,
        Available,
        NoData,
        TileUnavailable,
        UnknownColor,
        NetworkError,
        Mock
    }

    public readonly struct FloodTileCoordinate : IEquatable<FloodTileCoordinate>
    {
        public FloodTileCoordinate(int zoom, int x, int y, int pixelX, int pixelY)
        {
            Zoom = zoom;
            X = x;
            Y = y;
            PixelX = pixelX;
            PixelY = pixelY;
        }

        public int Zoom { get; }
        public int X { get; }
        public int Y { get; }
        public int PixelX { get; }
        public int PixelY { get; }

        public bool IsSameTile(FloodTileCoordinate other)
        {
            return Zoom == other.Zoom && X == other.X && Y == other.Y;
        }

        public bool Equals(FloodTileCoordinate other)
        {
            return Zoom == other.Zoom && X == other.X && Y == other.Y &&
                   PixelX == other.PixelX && PixelY == other.PixelY;
        }

        public override bool Equals(object obj)
        {
            return obj is FloodTileCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Zoom;
                hash = (hash * 397) ^ X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ PixelX;
                hash = (hash * 397) ^ PixelY;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"z{Zoom}/{X}/{Y} px({PixelX},{PixelY})";
        }
    }

    public readonly struct FloodDepthBand
    {
        public FloodDepthBand(string label, float minimumMeters, float maximumMeters, float displayHeightMeters, bool hasFiniteMaximum)
        {
            Label = label;
            MinimumMeters = minimumMeters;
            MaximumMeters = maximumMeters;
            DisplayHeightMeters = displayHeightMeters;
            HasFiniteMaximum = hasFiniteMaximum;
        }

        public string Label { get; }
        public float MinimumMeters { get; }
        public float MaximumMeters { get; }
        public float DisplayHeightMeters { get; }
        public bool HasFiniteMaximum { get; }
    }

    public sealed class FloodDepthResult
    {
        public FloodDepthResult(
            FloodDepthStatus status,
            string message,
            bool hasLocation = false,
            double latitude = 0d,
            double longitude = 0d,
            FloodTileCoordinate tile = default,
            Color32 sampledColor = default,
            FloodDepthBand depthBand = default,
            DateTime? retrievedAt = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            HasLocation = hasLocation;
            Latitude = latitude;
            Longitude = longitude;
            Tile = tile;
            SampledColor = sampledColor;
            DepthBand = depthBand;
            RetrievedAt = retrievedAt ?? DateTime.Now;
        }

        public FloodDepthStatus Status { get; }
        public string Message { get; }
        public bool HasLocation { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public FloodTileCoordinate Tile { get; }
        public Color32 SampledColor { get; }
        public FloodDepthBand DepthBand { get; }
        public DateTime RetrievedAt { get; }
        public bool HasDepth => Status == FloodDepthStatus.Available || Status == FloodDepthStatus.Mock;
    }

    public enum TerrainElevationStatus
    {
        Available,
        NoData,
        NetworkError
    }

    public sealed class TerrainElevationResult
    {
        public TerrainElevationResult(
            TerrainElevationStatus status,
            string message,
            double latitude,
            double longitude,
            double elevationMeters = 0d,
            string sourceId = "")
        {
            Status = status;
            Message = message ?? string.Empty;
            Latitude = latitude;
            Longitude = longitude;
            ElevationMeters = elevationMeters;
            SourceId = sourceId ?? string.Empty;
        }

        public TerrainElevationStatus Status { get; }
        public string Message { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public double ElevationMeters { get; }
        public string SourceId { get; }
        public bool HasElevation => Status == TerrainElevationStatus.Available;
    }
}
