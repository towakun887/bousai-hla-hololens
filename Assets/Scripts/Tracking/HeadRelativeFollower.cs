using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Places an object at a configurable head-relative offset and optionally keeps
/// following the user's horizontal or full position. Yaw can be followed
/// independently from position.
/// </summary>
[DisallowMultipleComponent]
public sealed class HeadRelativeFollower : MonoBehaviour
{
    public enum PositionFollowMode
    {
        PlaceOnEnableOnly,
        FollowHorizontal,
        FollowFull
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [Tooltip("Optional placement marker. Its position and rotation relative to Target are read whenever this object is enabled.")]
    [SerializeField] private Transform placementReference;

    [Header("Relative Transform")]
    [Tooltip("Head yaw space offset in metres. This is the default used whenever the object is enabled.")]
    [SerializeField] private Vector3 relativePosition = new Vector3(0f, 0f, 2f);
    [SerializeField] private Vector3 relativeEulerAngles;

    [Header("Follow")]
    [SerializeField] private PositionFollowMode positionFollowMode = PositionFollowMode.FollowHorizontal;
    [Tooltip("Seconds used to cushion position changes. 0 snaps immediately.")]
    [SerializeField, Min(0f)] private float moveLerpTime = 0.2f;
    [Tooltip("Seconds used to cushion yaw following. 0 snaps immediately.")]
    [SerializeField, Min(0f)] private float rotateLerpTime = 0.2f;
    [Tooltip("Horizontal head rotation allowed before the object starts following.")]
    [SerializeField, Range(0f, 90f)] private float horizontalAngularDeadZone;
    [Tooltip("Follows target yaw while preserving the object's configured or manipulated relative orientation.")]
    [SerializeField] private bool followYawRotation;
    [Tooltip("When disabled, another component may continue controlling world Y (for example the flood ruler).")]
    [SerializeField] private bool applyVerticalPosition = true;
    [SerializeField] private bool resetOnEnable = true;

    [Header("Manipulation")]
    [Tooltip("Optional. While selected, following pauses; on release the manipulated position becomes the runtime offset.")]
    [SerializeField] private XRBaseInteractable manipulationSource;

    private Vector3 runtimeRelativePosition;
    private Vector3 runtimeRelativeEulerAngles;
    private bool initialized;
    private bool wasManipulating;
    private float followedYawDegrees;

    public Vector3 RelativePosition
    {
        get => relativePosition;
        set
        {
            relativePosition = value;
            runtimeRelativePosition = value;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (resetOnEnable || !initialized)
            ResetRelativePlacement();
    }

    private void Start()
    {
        // Camera.main can become available after OnEnable in some XR startup paths.
        if (!initialized)
            ResetRelativePlacement();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        if (target == null)
            return;

        UpdateFollowYaw();

        bool isManipulating = manipulationSource != null && manipulationSource.isSelected;
        if (isManipulating)
        {
            wasManipulating = true;
            return;
        }

        if (wasManipulating)
        {
            CaptureCurrentRelativePosition();
            CaptureCurrentRelativeRotation();
            wasManipulating = false;
        }

        if (positionFollowMode != PositionFollowMode.PlaceOnEnableOnly)
            ApplyPosition(false);

        if (followYawRotation)
            ApplyRotation(false);
    }

    public void ResetRelativePlacement()
    {
        ResolveReferences();
        if (target != null)
            followedYawDegrees = target.eulerAngles.y;
        CaptureConfiguredPlacement();
        initialized = target != null;
        if (!initialized)
            return;

        ApplyPosition(true);
        if (followYawRotation)
            ApplyRotation(true);
    }

    public void CaptureCurrentRelativePosition()
    {
        ResolveReferences();
        if (target == null)
            return;

        Quaternion inverseYaw = Quaternion.Inverse(GetFollowYaw());
        runtimeRelativePosition = inverseYaw * (transform.position - target.position);
        if (!applyVerticalPosition)
            runtimeRelativePosition.y = relativePosition.y;
    }

    private void ApplyPosition(bool immediate)
    {
        Vector3 desired = target.position + GetFollowYaw() * runtimeRelativePosition;
        Vector3 current = transform.position;
        Vector3 targetPosition = current;

        switch (positionFollowMode)
        {
            case PositionFollowMode.PlaceOnEnableOnly:
                targetPosition.x = desired.x;
                targetPosition.z = desired.z;
                if (applyVerticalPosition)
                    targetPosition.y = desired.y;
                break;

            case PositionFollowMode.FollowHorizontal:
                targetPosition.x = desired.x;
                targetPosition.z = desired.z;
                if (applyVerticalPosition)
                    targetPosition.y = desired.y;
                break;

            case PositionFollowMode.FollowFull:
                targetPosition = desired;
                break;
        }

        if (immediate || moveLerpTime <= 0f)
        {
            transform.position = targetPosition;
            return;
        }

        float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / moveLerpTime);
        transform.position = Vector3.Lerp(current, targetPosition, t);
    }

    private void CaptureCurrentRelativeRotation()
    {
        ResolveReferences();
        if (target == null)
            return;

        runtimeRelativeEulerAngles =
            (Quaternion.Inverse(GetFollowYaw()) * transform.rotation).eulerAngles;
    }

    private void ApplyRotation(bool immediate)
    {
        Quaternion desired =
            GetFollowYaw() * Quaternion.Euler(runtimeRelativeEulerAngles);
        if (immediate || rotateLerpTime <= 0f)
        {
            transform.rotation = desired;
            return;
        }

        float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / rotateLerpTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, t);
    }

    private void CaptureConfiguredPlacement()
    {
        runtimeRelativePosition = relativePosition;
        runtimeRelativeEulerAngles = relativeEulerAngles;
        if (target == null || placementReference == null)
            return;

        if (placementReference.parent == target)
        {
            runtimeRelativePosition = placementReference.localPosition;
            runtimeRelativeEulerAngles = placementReference.localEulerAngles;
            return;
        }

        Quaternion inverseYaw = Quaternion.Inverse(GetTargetYaw());
        runtimeRelativePosition = inverseYaw * (placementReference.position - target.position);
        runtimeRelativeEulerAngles =
            (inverseYaw * placementReference.rotation).eulerAngles;
    }

    private Quaternion GetTargetYaw()
    {
        return Quaternion.Euler(0f, target.eulerAngles.y, 0f);
    }

    private Quaternion GetFollowYaw()
    {
        return Quaternion.Euler(0f, followedYawDegrees, 0f);
    }

    private void UpdateFollowYaw()
    {
        float targetYaw = target.eulerAngles.y;
        float difference = Mathf.DeltaAngle(followedYawDegrees, targetYaw);
        float deadZone = Mathf.Max(0f, horizontalAngularDeadZone);
        if (Mathf.Abs(difference) <= deadZone)
            return;

        followedYawDegrees = targetYaw - Mathf.Sign(difference) * deadZone;
    }

    private void ResolveReferences()
    {
        if (target == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                target = mainCamera.transform;
        }

        if (manipulationSource == null)
            manipulationSource = GetComponent<XRBaseInteractable>();
    }
}
