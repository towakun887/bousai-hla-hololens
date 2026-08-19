using System;
using UnityEngine;

namespace Bousai.Dosha
{
    [Flags]
    public enum DoshaHazardType
    {
        None = 0,
        DebrisFlow = 1 << 0,
        SteepSlopeCollapse = 1 << 1,
        Landslide = 1 << 2
    }

    [Flags]
    public enum DoshaHazardZone
    {
        None = 0,
        Warning = 1 << 0,
        SpecialWarning = 1 << 1
    }

    public enum DoshaHazardStatus
    {
        Available,
        NoData,
        Partial,
        NetworkError,
        Mock
    }

    public sealed class DoshaHazardResult
    {
        public DoshaHazardResult(
            DoshaHazardStatus status,
            string message,
            double latitude,
            double longitude,
            DoshaHazardType warningTypes,
            DoshaHazardType specialWarningTypes,
            DateTime? retrievedAt = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            Latitude = latitude;
            Longitude = longitude;
            WarningTypes = warningTypes;
            SpecialWarningTypes = specialWarningTypes;
            RetrievedAt = retrievedAt ?? DateTime.Now;
        }

        public DoshaHazardStatus Status { get; }
        public string Message { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public DateTime RetrievedAt { get; }
        public DoshaHazardType WarningTypes { get; }
        public DoshaHazardType SpecialWarningTypes { get; }
        public DoshaHazardType HazardTypes => WarningTypes | SpecialWarningTypes;
        public DoshaHazardZone Zones =>
            (WarningTypes != DoshaHazardType.None ? DoshaHazardZone.Warning : DoshaHazardZone.None) |
            (SpecialWarningTypes != DoshaHazardType.None ? DoshaHazardZone.SpecialWarning : DoshaHazardZone.None);
        public bool HasWarningZone => WarningTypes != DoshaHazardType.None;
        public bool HasSpecialWarningZone => SpecialWarningTypes != DoshaHazardType.None;
        public bool HasHazard => HazardTypes != DoshaHazardType.None;
    }

    public static class DoshaHazardLegend
    {
        private readonly struct Entry
        {
            public Entry(DoshaHazardType type, DoshaHazardZone zone, byte r, byte g, byte b)
            {
                Type = type;
                Zone = zone;
                Color = new Color32(r, g, b, 255);
            }

            public DoshaHazardType Type { get; }
            public DoshaHazardZone Zone { get; }
            public Color32 Color { get; }
        }

        // 国土地理院「重ねるハザードマップ」のPNGタイル／公式凡例から取得した色。
        // 指定予定区域は薄い塗りと青い点線で表示されるため、薄い塗り色を同じ区分として扱う。
        private static readonly Entry[] Entries =
        {
            new Entry(DoshaHazardType.DebrisFlow, DoshaHazardZone.SpecialWarning, 165, 0, 33),
            new Entry(DoshaHazardType.DebrisFlow, DoshaHazardZone.SpecialWarning, 183, 51, 77),
            new Entry(DoshaHazardType.DebrisFlow, DoshaHazardZone.Warning, 230, 200, 50),
            new Entry(DoshaHazardType.DebrisFlow, DoshaHazardZone.Warning, 235, 211, 91),

            new Entry(DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.SpecialWarning, 250, 40, 0),
            new Entry(DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.SpecialWarning, 251, 83, 51),
            new Entry(DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.Warning, 250, 230, 0),
            new Entry(DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.Warning, 251, 235, 51),

            new Entry(DoshaHazardType.Landslide, DoshaHazardZone.SpecialWarning, 180, 0, 40),
            new Entry(DoshaHazardType.Landslide, DoshaHazardZone.SpecialWarning, 195, 51, 83),
            new Entry(DoshaHazardType.Landslide, DoshaHazardZone.Warning, 255, 153, 0),
            new Entry(DoshaHazardType.Landslide, DoshaHazardZone.Warning, 255, 173, 51)
        };

        public static bool TryClassify(
            Color32 sampled,
            DoshaHazardType expectedType,
            int tolerance,
            out DoshaHazardZone zone,
            out bool exactMatch)
        {
            zone = DoshaHazardZone.None;
            exactMatch = false;
            if (sampled.a == 0 || expectedType == DoshaHazardType.None)
                return false;

            int clampedTolerance = Mathf.Max(0, tolerance);
            int bestDistanceSquared = int.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < Entries.Length; i++)
            {
                Entry entry = Entries[i];
                if (entry.Type != expectedType)
                    continue;

                int dr = Mathf.Abs(sampled.r - entry.Color.r);
                int dg = Mathf.Abs(sampled.g - entry.Color.g);
                int db = Mathf.Abs(sampled.b - entry.Color.b);
                if (dr == 0 && dg == 0 && db == 0)
                {
                    zone = entry.Zone;
                    exactMatch = true;
                    return true;
                }

                if (dr > clampedTolerance || dg > clampedTolerance || db > clampedTolerance)
                    continue;

                int distanceSquared = dr * dr + dg * dg + db * db;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return false;

            zone = Entries[bestIndex].Zone;
            return true;
        }
    }
}
