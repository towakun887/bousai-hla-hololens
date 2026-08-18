using MixedReality.Toolkit;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// MRTK3 で追跡している手の関節位置へ、この GameObject を追従させます。
/// </summary>
public class HandJointFollower : MonoBehaviour
{
    [Header("Hand Tracking")]
    [SerializeField]
    [Tooltip("追従する手を指定します。Left Hand または Right Hand を選択してください。")]
    private XRNode hand = XRNode.RightHand;

    [SerializeField]
    [Tooltip("追従する手の関節を指定します。手の中心付近なら Palm を使用します。")]
    private TrackedHandJoint joint = TrackedHandJoint.Palm;

    [Header("Transform")]
    [SerializeField]
    [Tooltip("関節の向きを基準にした位置オフセットです。")]
    private Vector3 positionOffset = Vector3.zero;

    [SerializeField]
    [Tooltip("有効にすると、位置だけでなく関節の向きにも追従します。")]
    private bool followRotation;

    [Header("Visibility")]
    [SerializeField]
    [Tooltip("手を追跡できない間、このオブジェクト以下の Renderer を非表示にします。")]
    private bool hideWhenNotTracked = true;

    private Renderer[] cachedRenderers;
    private bool[] rendererEnabledStates;
    private bool isVisible = true;

    private void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        rendererEnabledStates = new bool[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            rendererEnabledStates[i] = cachedRenderers[i].enabled;
        }
    }

    private void OnEnable()
    {
        SetVisible(!hideWhenNotTracked);
    }

    private void LateUpdate()
    {
        if (TryGetJointPose(out HandJointPose pose))
        {
            Vector3 targetPosition = pose.Position + pose.Rotation * positionOffset;

            if (followRotation)
            {
                transform.SetPositionAndRotation(targetPosition, pose.Rotation);
            }
            else
            {
                transform.position = targetPosition;
            }

            SetVisible(true);
        }
        else
        {
            SetVisible(!hideWhenNotTracked);
        }
    }

    private void OnDisable()
    {
        SetVisible(true);
    }

    private bool TryGetJointPose(out HandJointPose pose)
    {
        pose = default;

        if (hand != XRNode.LeftHand && hand != XRNode.RightHand)
        {
            return false;
        }

        return XRSubsystemHelpers.HandsAggregator != null
            && XRSubsystemHelpers.HandsAggregator.TryGetJoint(joint, hand, out pose);
    }

    private void SetVisible(bool visible)
    {
        if (isVisible == visible)
        {
            return;
        }

        isVisible = visible;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible && rendererEnabledStates[i];
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hand != XRNode.LeftHand && hand != XRNode.RightHand)
        {
            hand = XRNode.RightHand;
        }
    }
#endif
}
