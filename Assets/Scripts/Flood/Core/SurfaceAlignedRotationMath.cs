using UnityEngine;

namespace Bousai.Flood
{
    public static class SurfaceAlignedRotationMath
    {
        public static bool TryCalculate(
            Vector3 objectPosition, Vector3 targetPosition, Vector3 surfaceNormal,
            float surfaceAngleDegrees, float yawOffsetDegrees,
            bool faceAwayFromTarget, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (surfaceNormal.sqrMagnitude < 0.000001f)
                return false;

            Vector3 up = surfaceNormal.normalized;
            Vector3 direction = Vector3.ProjectOnPlane(targetPosition - objectPosition, up);
            if (direction.sqrMagnitude < 0.000001f)
                return false;

            if (faceAwayFromTarget)
                direction = -direction;

            Quaternion faceRotation = Quaternion.LookRotation(direction.normalized, up);
            Quaternion yawOffset = Quaternion.AngleAxis(yawOffsetDegrees, Vector3.up);
            Quaternion surfaceTilt = Quaternion.AngleAxis(
                90f - Mathf.Clamp(surfaceAngleDegrees, 0f, 90f), Vector3.right);
            rotation = faceRotation * yawOffset * surfaceTilt;
            return true;
        }
    }
}
