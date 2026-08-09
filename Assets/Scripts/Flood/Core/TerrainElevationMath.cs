namespace Bousai.Flood
{
    public static class TerrainElevationMath
    {
        public static float ToUnityY(
            float originUnityY, double originAltitudeMeters,
            double terrainElevationMeters, float floodDepthMeters)
        {
            return originUnityY +
                   (float)(terrainElevationMeters - originAltitudeMeters) +
                   floodDepthMeters;
        }
    }
}
