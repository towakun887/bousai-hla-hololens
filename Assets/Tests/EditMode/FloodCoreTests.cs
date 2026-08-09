using Bousai.Flood;
using NUnit.Framework;
using UnityEngine;

namespace Bousai.Flood.Tests
{
    public class FloodCoreTests
    {
        [Test]
        public void TileCoordinate_EquatorAndPrimeMeridian_IsExpectedTile()
        {
            FloodTileCoordinate coordinate = FloodTileMath.FromLatitudeLongitude(0d, 0d, 1);

            Assert.AreEqual(1, coordinate.X);
            Assert.AreEqual(1, coordinate.Y);
            Assert.AreEqual(0, coordinate.PixelX);
            Assert.AreEqual(0, coordinate.PixelY);
        }

        [Test]
        public void TileCoordinate_AlwaysStaysWithinTileBounds()
        {
            FloodTileCoordinate coordinate = FloodTileMath.FromLatitudeLongitude(90d, 180d, 17);

            Assert.That(coordinate.X, Is.InRange(0, (1 << 17) - 1));
            Assert.That(coordinate.Y, Is.InRange(0, (1 << 17) - 1));
            Assert.That(coordinate.PixelX, Is.InRange(0, 255));
            Assert.That(coordinate.PixelY, Is.InRange(0, 255));
        }

        [Test]
        public void Legend_ExactAndToleranceMatchesReturnExpectedBand()
        {
            Assert.IsTrue(FloodDepthLegend.TryClassify(new Color32(255, 183, 183, 255), 9, out FloodDepthBand exact, out bool exactMatch));
            Assert.IsTrue(exactMatch);
            Assert.AreEqual(5f, exact.DisplayHeightMeters);

            Assert.IsTrue(FloodDepthLegend.TryClassify(new Color32(250, 180, 187, 255), 9, out FloodDepthBand tolerant, out bool tolerantExact));
            Assert.IsFalse(tolerantExact);
            Assert.AreEqual("3.0m ～ 5.0m", tolerant.Label);
        }

        [Test]
        public void Legend_TransparentAndUnknownColorsDoNotMatch()
        {
            Assert.IsFalse(FloodDepthLegend.TryClassify(new Color32(255, 183, 183, 0), 9, out _, out _));
            Assert.IsFalse(FloodDepthLegend.TryClassify(new Color32(0, 255, 0, 255), 9, out _, out _));
        }

        [TestCase(1.99f, 2f, 3f, false)]
        [TestCase(2f, 1.99f, 3f, false)]
        [TestCase(2f, 2f, 0f, true)]
        public void RefreshGate_RequiresElapsedTimeAndHorizontalDistance(float elapsed, float distance, float y, bool expected)
        {
            bool actual = FloodTileMath.ShouldRefresh(elapsed, Vector3.zero, new Vector3(distance, y, 0f), 2f, 2f);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GeoConversion_RoundTripsThroughRotatedSlamFrame()
        {
            const double latitude = 35.681236d;
            const double longitude = 139.767125d;
            const double altitude = 12.5d;
            Vector3 originUnity = new Vector3(3f, -1f, 5f);
            Vector3 unity = GeoCoordinateMath.GpsToUnity(
                latitude + 0.0002d, longitude - 0.0003d, altitude + 4d,
                latitude, longitude, altitude, originUnity, 37f);

            Assert.IsTrue(GeoCoordinateMath.TryUnityToGps(
                unity, latitude, longitude, altitude, originUnity, 37f,
                out double resultLatitude, out double resultLongitude, out double resultAltitude));
            Assert.AreEqual(latitude + 0.0002d, resultLatitude, 0.0000001d);
            Assert.AreEqual(longitude - 0.0003d, resultLongitude, 0.0000001d);
            Assert.AreEqual(altitude + 4d, resultAltitude, 0.001d);
        }

        [Test]
        public void Heading_UsesCalibratedNorthDirection()
        {
            Assert.IsTrue(GeoCoordinateMath.TryGetHeadingDegrees(Vector3.right, 0f, out float east));
            Assert.AreEqual(90f, east, 0.001f);
            Assert.IsTrue(GeoCoordinateMath.TryGetHeadingDegrees(Vector3.right, 90f, out float north));
            Assert.AreEqual(0f, north, 0.001f);
        }

        [Test]
        public void TerrainElevationJson_ParsesValueSourceAndNoData()
        {
            Assert.IsTrue(TerrainElevationJsonParser.TryParse(
                "{\"elevation\":123.4,\"hsrc\":\"5m（レーザ）\"}",
                out double elevation, out string source, out bool noData));
            Assert.AreEqual(123.4d, elevation, 0.001d);
            Assert.AreEqual("5m（レーザ）", source);
            Assert.IsFalse(noData);

            Assert.IsTrue(TerrainElevationJsonParser.TryParse(
                "{\"elevation\":123.4,\"hsrc\":\"5m\\uff08\\u30ec\\u30fc\\u30b6\\uff09\"}",
                out _, out source, out noData));
            Assert.AreEqual("5m（レーザ）", source);
            Assert.IsFalse(noData);

            Assert.IsTrue(TerrainElevationJsonParser.TryParse(
                "{\"elevation\":\"-----\",\"hsrc\":\"-----\"}",
                out _, out _, out noData));
            Assert.IsTrue(noData);
        }

        [Test]
        public void TerrainElevation_MapsRelativeAltitudeAndFloodDepthToUnityY()
        {
            float y = TerrainElevationMath.ToUnityY(
                1.6f, 40d, 42.5d, 3f);

            Assert.AreEqual(7.1f, y, 0.001f);
        }

        [Test]
        public void SurfaceAlignedRotation_IgnoresTargetHeightAndMaintainsSurfaceAngle()
        {
            Assert.IsTrue(SurfaceAlignedRotationMath.TryCalculate(
                Vector3.zero, new Vector3(0f, 4f, 3f), Vector3.up,
                90f, 0f, false, out Quaternion upright));
            Assert.AreEqual(0f, Vector3.Angle(upright * Vector3.forward, Vector3.forward), 0.001f);
            Assert.AreEqual(0f, Vector3.Angle(upright * Vector3.up, Vector3.up), 0.001f);

            Assert.IsTrue(SurfaceAlignedRotationMath.TryCalculate(
                Vector3.zero, Vector3.forward, Vector3.up,
                30f, 0f, false, out Quaternion tilted));
            float normalAngle = Vector3.Angle(tilted * Vector3.forward, Vector3.up);
            float planeAngle = Mathf.Min(normalAngle, 180f - normalAngle);
            Assert.AreEqual(30f, planeAngle, 0.001f);
        }
    }
}
