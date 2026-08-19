using Bousai.Flood;
using Bousai.Dosha;
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

        [TestCase(165, 0, 33, DoshaHazardType.DebrisFlow, DoshaHazardZone.SpecialWarning)]
        [TestCase(183, 51, 77, DoshaHazardType.DebrisFlow, DoshaHazardZone.SpecialWarning)]
        [TestCase(230, 200, 50, DoshaHazardType.DebrisFlow, DoshaHazardZone.Warning)]
        [TestCase(235, 211, 91, DoshaHazardType.DebrisFlow, DoshaHazardZone.Warning)]
        [TestCase(250, 40, 0, DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.SpecialWarning)]
        [TestCase(251, 83, 51, DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.SpecialWarning)]
        [TestCase(250, 230, 0, DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.Warning)]
        [TestCase(251, 235, 51, DoshaHazardType.SteepSlopeCollapse, DoshaHazardZone.Warning)]
        [TestCase(180, 0, 40, DoshaHazardType.Landslide, DoshaHazardZone.SpecialWarning)]
        [TestCase(195, 51, 83, DoshaHazardType.Landslide, DoshaHazardZone.SpecialWarning)]
        [TestCase(255, 153, 0, DoshaHazardType.Landslide, DoshaHazardZone.Warning)]
        [TestCase(255, 173, 51, DoshaHazardType.Landslide, DoshaHazardZone.Warning)]
        public void DoshaLegend_ClassifiesEachHazardAndZone(
            int r, int g, int b,
            DoshaHazardType type,
            DoshaHazardZone expected)
        {
            Assert.IsTrue(DoshaHazardLegend.TryClassify(
                new Color32((byte)r, (byte)g, (byte)b, 255),
                type,
                9,
                out DoshaHazardZone actual,
                out bool exact));
            Assert.AreEqual(expected, actual);
            Assert.IsTrue(exact);
        }

        [Test]
        public void DoshaLegend_DoesNotApplyAnotherHazardTypesColors()
        {
            Assert.IsFalse(DoshaHazardLegend.TryClassify(
                new Color32(255, 150, 0, 255),
                DoshaHazardType.DebrisFlow,
                9,
                out _,
                out _));
        }

        [Test]
        public void DoshaResult_OrsHazardTypesAndKeepsBothZones()
        {
            DoshaHazardResult result = new DoshaHazardResult(
                DoshaHazardStatus.Available,
                string.Empty,
                35d,
                139d,
                DoshaHazardType.DebrisFlow | DoshaHazardType.Landslide,
                DoshaHazardType.SteepSlopeCollapse);

            Assert.AreEqual(
                DoshaHazardType.DebrisFlow |
                DoshaHazardType.SteepSlopeCollapse |
                DoshaHazardType.Landslide,
                result.HazardTypes);
            Assert.IsTrue(result.HasWarningZone);
            Assert.IsTrue(result.HasSpecialWarningZone);
            Assert.AreEqual(
                DoshaHazardZone.Warning | DoshaHazardZone.SpecialWarning,
                result.Zones);
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
        public void TerrainElevationJson_ParsesGeoidHeight()
        {
            const string json =
                "{\"OutputData\":{\"latitude\":\"35.389690000\",\"longitude\":\"139.433600000\",\"geoidHeight\":\"37.7172\",\"HeightReferenceConversion\":\"0.0000\",\"geoidHeight+HeightReferenceConversion\":\"37.7172\"}}";

            Assert.IsTrue(TerrainElevationJsonParser.TryParseGeoidHeight(
                json, out double geoidHeight));
            Assert.AreEqual(37.7172d, geoidHeight, 0.00001d);
        }

        [Test]
        public void TerrainElevation_AddsGeoidHeight()
        {
            double correctedElevation = TerrainElevationMath.AddGeoidHeight(
                12.5d, 37.7172d);

            Assert.AreEqual(50.2172d, correctedElevation, 0.00001d);
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
