using Bousai.Flood;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class SurfaceAlignedBillboard : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform target;
    [Tooltip("World Space Canvasなど、表面がTransform.forwardの反対側を向く場合にONにします。")]
    [SerializeField] private bool faceAwayFromTarget = true;

    [Header("Surface Alignment")]
    [Tooltip("地表面に対する角度です。90°で垂直、0°で地表面と平行です。")]
    [SerializeField, Range(0f, 90f)] private float surfaceAngleDegrees = 90f;
    [Tooltip("地表面法線の周りに加える向き補正です。")]
    [SerializeField, Range(-180f, 180f)] private float yawOffsetDegrees;

    [Header("Smoothing")]
    [Tooltip("0なら即時追従します。値を大きくすると素早く追従します。")]
    [SerializeField, Min(0f)] private float rotationFollowSpeed = 12f;

    private Vector3 surfaceNormal = Vector3.up;

    private void OnEnable()
    {
        ResolveTarget();
    }

    private void LateUpdate()
    {
        ResolveTarget();
        if (target == null)
            return;

        if (!SurfaceAlignedRotationMath.TryCalculate(
                transform.position, target.position, surfaceNormal,
                surfaceAngleDegrees, yawOffsetDegrees,
                faceAwayFromTarget, out Quaternion targetRotation))
            return;

        if (rotationFollowSpeed <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        float t = 1f - Mathf.Exp(-rotationFollowSpeed * Time.unscaledDeltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
    }

    public void SetSurfaceNormal(Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.000001f)
            return;
        surfaceNormal = Vector3.Dot(normal, Vector3.up) < 0f
            ? -normal.normalized
            : normal.normalized;
    }

    private void ResolveTarget()
    {
        if (target == null && Camera.main != null)
            target = Camera.main.transform;
    }
}
