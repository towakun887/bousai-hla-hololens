using System;
using UnityEngine;

namespace Bousai.Flood
{
    public static class GeoCoordinateMath
    {
        public const double MetersPerDegreeLatitude = 111320d;

        public static Vector3 GpsToUnity(
            double latitude,
            double longitude,
            double altitude,
            double originLatitude,
            double originLongitude,
            double originAltitude,
            Vector3 originUnity,
            float northYRotationOffset)
        {
            double metersPerDegreeLongitude = GetMetersPerDegreeLongitude(originLatitude);
            Vector3 localOffset = new Vector3(
                (float)((longitude - originLongitude) * metersPerDegreeLongitude),
                (float)(altitude - originAltitude),
                (float)((latitude - originLatitude) * MetersPerDegreeLatitude));
            return originUnity + Quaternion.Euler(0f, northYRotationOffset, 0f) * localOffset;
        }

        public static bool TryUnityToGps(
            Vector3 unityPosition,
            double originLatitude,
            double originLongitude,
            double originAltitude,
            Vector3 originUnity,
            float northYRotationOffset,
            out double latitude,
            out double longitude,
            out double altitude)
        {
            double metersPerDegreeLongitude = GetMetersPerDegreeLongitude(originLatitude);
            if (Math.Abs(metersPerDegreeLongitude) < 0.000001d)
            {
                latitude = longitude = altitude = 0d;
                return false;
            }

            Vector3 localOffset = Quaternion.Euler(0f, -northYRotationOffset, 0f) * (unityPosition - originUnity);
            latitude = originLatitude + localOffset.z / MetersPerDegreeLatitude;
            longitude = originLongitude + localOffset.x / metersPerDegreeLongitude;
            altitude = originAltitude + localOffset.y;
            return true;
        }

        public static bool TryGetHeadingDegrees(Vector3 unityForward, float northYRotationOffset, out float headingDegrees)
        {
            unityForward.y = 0f;
            if (unityForward.sqrMagnitude < 0.000001f)
            {
                headingDegrees = 0f;
                return false;
            }

            Vector3 north = Quaternion.Euler(0f, northYRotationOffset, 0f) * Vector3.forward;
            north.y = 0f;
            headingDegrees = Mathf.Repeat(Vector3.SignedAngle(north.normalized, unityForward.normalized, Vector3.up), 360f);
            return true;
        }

        private static double GetMetersPerDegreeLongitude(double latitude)
        {
            return MetersPerDegreeLatitude * Math.Cos(latitude * Math.PI / 180d);
        }
    }
}
