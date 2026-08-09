using MixedReality.Toolkit;
using MixedReality.Toolkit.SpatialManipulation;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ObjectManipulator))]
[RequireComponent(typeof(MoveAxisConstraint))]
[RequireComponent(typeof(SurfaceAlignedBillboard))]
public class RulerHorizontalManipulator : MonoBehaviour
{
    private void Reset()
    {
        ConfigureManipulation();
    }

    private void Awake()
    {
        ConfigureManipulation();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            ConfigureManipulation();
    }

    private void ConfigureManipulation()
    {
        ObjectManipulator manipulator = GetComponent<ObjectManipulator>();
        if (manipulator != null)
        {
            manipulator.HostTransform = transform;
            manipulator.AllowedManipulations = TransformFlags.Move;
            manipulator.ReleaseBehavior = ObjectManipulator.ReleaseBehaviorType.None;
        }

        MoveAxisConstraint moveConstraint = GetComponent<MoveAxisConstraint>();
        if (moveConstraint != null)
        {
            moveConstraint.ConstraintOnMovement = AxisFlags.YAxis;
            moveConstraint.UseLocalSpaceForConstraint = false;
        }
    }
}
