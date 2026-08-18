using UnityEngine;

/// <summary>
/// オブジェクトを Main Camera の下方へ追従させ、SLAM 床面からカメラ位置までの
/// 高さに合うよう全体を均一スケールし、カメラの水平な正面方向へ向きを揃えます。
/// </summary>
public class CameraFloorSpanScaler : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    [Tooltip("未設定の場合は Main Camera を自動取得します。")]
    private Camera targetCamera;

    [SerializeField]
    [Tooltip("未設定の場合はシーン内の GroundSurfaceResolver を自動取得します。")]
    private GroundSurfaceResolver groundSurfaceResolver;

    [Header("Placement And Orientation")]
    [SerializeField]
    [Tooltip("カメラの水平な正面方向に沿った距離です。正数は前方、負数は後方です。")]
    private float forwardOffset = -0.4f;

    [SerializeField]
    [Tooltip("モデル自身の正面がローカル +Z でない場合に、Y軸方向の補正角度を指定します。")]
    private float modelYawOffset;

    [Header("Edge Offsets")]
    [SerializeField, Min(0f)]
    [Tooltip("オブジェクト上端をカメラ位置から何 m 下げるかを指定します。")]
    private float cameraClearance;

    [SerializeField, Min(0f)]
    [Tooltip("オブジェクト下端を床面から何 m 上げるかを指定します。")]
    private float floorClearance;

    [Header("Tracking")]
    [SerializeField, Min(0.001f)]
    [Tooltip("これより小さい高さではスケールを更新しません。")]
    private float minimumHeight = 0.05f;

    [SerializeField]
    [Tooltip("床を検出できるまで Renderer を非表示にします。")]
    private bool hideUntilFloorDetected = true;

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

        ResolveReferences();
    }

    private void OnEnable()
    {
        SetVisible(!hideUntilFloorDetected);
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (targetCamera == null || groundSurfaceResolver == null ||
            !groundSurfaceResolver.TryGetGroundY(targetCamera.transform.position, out float groundY))
        {
            SetVisible(!hideUntilFloorDetected);
            return;
        }

        Vector3 horizontalCameraForward = Vector3.ProjectOnPlane(
            targetCamera.transform.forward, Vector3.up);
        if (horizontalCameraForward.sqrMagnitude <= Mathf.Epsilon)
        {
            SetVisible(!hideUntilFloorDetected);
            return;
        }

        horizontalCameraForward.Normalize();
        Quaternion cameraFacingRotation = Quaternion.LookRotation(horizontalCameraForward, Vector3.up);
        transform.rotation = cameraFacingRotation * Quaternion.Euler(0f, modelYawOffset, 0f);

        if (!TryGetRendererBounds(out Bounds currentBounds))
        {
            SetVisible(!hideUntilFloorDetected);
            return;
        }

        float bottomY = groundY + floorClearance;
        float topY = targetCamera.transform.position.y - cameraClearance;
        float targetHeight = topY - bottomY;

        if (targetHeight < minimumHeight || currentBounds.size.y <= Mathf.Epsilon)
        {
            SetVisible(!hideUntilFloorDetected);
            return;
        }

        // 現在の形状比を維持しながら、World Bounds の高さを目標値へ合わせます。
        float scaleFactor = targetHeight / currentBounds.size.y;
        transform.localScale *= scaleFactor;

        if (!TryGetRendererBounds(out Bounds scaledBounds))
        {
            SetVisible(!hideUntilFloorDetected);
            return;
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 targetHorizontalPosition = cameraPosition + horizontalCameraForward * forwardOffset;
        Vector3 desiredBoundsCenter = new Vector3(
            targetHorizontalPosition.x,
            (bottomY + topY) * 0.5f,
            targetHorizontalPosition.z);

        // Pivot の位置に依存せず、Renderer 全体の中心をオフセット後の目標位置へ合わせます。
        transform.position += desiredBoundsCenter - scaledBounds.center;
        SetVisible(true);
    }

    private void OnDisable()
    {
        SetVisible(true);
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (groundSurfaceResolver == null)
        {
            groundSurfaceResolver = FindFirstObjectByType<GroundSurfaceResolver>();
        }
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer currentRenderer = cachedRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            if (!found)
            {
                bounds = currentRenderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(currentRenderer.bounds);
            }
        }

        return found;
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
}
