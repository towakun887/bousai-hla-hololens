using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class GroundSurfaceResolver : MonoBehaviour
{
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField, Min(0f)] private float maximumHorizontalDistance = 2f;
    [SerializeField, Min(0f)] private float minimumVerticalDrop = 0.2f;
    [SerializeField, Min(0f)] private float maximumVerticalDrop = 4f;
    [SerializeField, Min(0f)] private float expectedEyeHeight = 1.6f;

    private void Awake()
    {
        ResolveReferences();
    }

    public bool TryGetGroundY(Vector3 observerPosition, out float groundY)
    {
        groundY = 0f;
        ResolveReferences();
        if (planeManager == null)
            return false;

        bool found = false;
        float bestScore = float.PositiveInfinity;
        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane == null || plane.trackingState != TrackingState.Tracking ||
                plane.classification != PlaneClassification.Floor)
                continue;

            Ray ray = new Ray(observerPosition + Vector3.up * 0.05f, Vector3.down);
            if (!plane.infinitePlane.Raycast(ray, out float enter))
                continue;

            Vector3 hit = ray.GetPoint(enter);
            float verticalDrop = observerPosition.y - hit.y;
            if (verticalDrop < minimumVerticalDrop || verticalDrop > maximumVerticalDrop)
                continue;

            Vector3 local = plane.transform.InverseTransformPoint(hit);
            Vector2 halfSize = plane.size * 0.5f;
            float outsideX = Mathf.Max(0f, Mathf.Abs(local.x) - halfSize.x);
            float outsideZ = Mathf.Max(0f, Mathf.Abs(local.z) - halfSize.y);
            float horizontalDistance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
            if (horizontalDistance > maximumHorizontalDistance)
                continue;

            float score = horizontalDistance * 10f + Mathf.Abs(verticalDrop - expectedEyeHeight);
            if (score >= bestScore)
                continue;

            bestScore = score;
            groundY = hit.y;
            found = true;
        }

        return found;
    }

    private void ResolveReferences()
    {
        if (planeManager == null)
            planeManager = FindObjectOfType<ARPlaneManager>();
    }
}
