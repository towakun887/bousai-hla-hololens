using UnityEngine;

namespace Bousai.Flood
{
    public static class FloodDepthLegend
    {
        private readonly struct Entry
        {
            public Entry(byte r, byte g, byte b, FloodDepthBand band)
            {
                Color = new Color32(r, g, b, 255);
                Band = band;
            }

            public Color32 Color { get; }
            public FloodDepthBand Band { get; }
        }

        private static readonly Entry[] Entries =
        {
            new Entry(255, 255, 179, new FloodDepthBand("0.3m 未満", 0f, 0.3f, 0.3f, true)),
            new Entry(247, 245, 169, new FloodDepthBand("0.5m 未満", 0.3f, 0.5f, 0.5f, true)),
            new Entry(248, 225, 166, new FloodDepthBand("0.5m ～ 1.0m", 0.5f, 1f, 1f, true)),
            new Entry(255, 216, 192, new FloodDepthBand("0.5m ～ 3.0m", 1f, 3f, 3f, true)),
            new Entry(255, 183, 183, new FloodDepthBand("3.0m ～ 5.0m", 3f, 5f, 5f, true)),
            new Entry(255, 145, 145, new FloodDepthBand("5.0m ～ 10.0m", 5f, 10f, 10f, true)),
            new Entry(242, 133, 201, new FloodDepthBand("10.0m ～ 20.0m", 10f, 20f, 20f, true)),
            new Entry(220, 122, 220, new FloodDepthBand("20.0m 以上", 20f, 20f, 20f, false))
        };

        public static bool TryClassify(Color32 sampled, int tolerance, out FloodDepthBand band, out bool exactMatch)
        {
            band = default;
            exactMatch = false;
            if (sampled.a == 0)
                return false;

            for (int i = 0; i < Entries.Length; i++)
            {
                Color32 expected = Entries[i].Color;
                if (sampled.r == expected.r && sampled.g == expected.g && sampled.b == expected.b)
                {
                    band = Entries[i].Band;
                    exactMatch = true;
                    return true;
                }
            }

            int clampedTolerance = Mathf.Max(0, tolerance);
            int bestDistanceSquared = int.MaxValue;
            int bestIndex = -1;
            for (int i = 0; i < Entries.Length; i++)
            {
                Color32 expected = Entries[i].Color;
                int dr = Mathf.Abs(sampled.r - expected.r);
                int dg = Mathf.Abs(sampled.g - expected.g);
                int db = Mathf.Abs(sampled.b - expected.b);
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

            band = Entries[bestIndex].Band;
            return true;
        }
    }
}
