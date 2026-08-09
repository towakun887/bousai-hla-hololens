using UnityEngine;

[DisallowMultipleComponent]
public sealed class FloodPaneFaceSelector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("水面との上下判定に使用する視点です。未設定時はMain Cameraを使用します。")]
    [SerializeField] private Transform trackedPose;
    [Tooltip("水面より上側から見るときに表示するオブジェクトの親枠です。")]
    [SerializeField] private GameObject frontObjects;
    [Tooltip("水面より下側から見るときに表示するオブジェクトの親枠です。")]
    [SerializeField] private GameObject backObjects;

    [Header("Switching")]
    [Tooltip("視点が水面からこの距離以内にある場合は、frontとbackの両方を表示します。")]
    [SerializeField, Min(0f)] private float bothVisibleRangeMeters = 0.1f;

    public Transform TrackedPose => trackedPose;
    public GameObject FrontObjects => frontObjects;
    public GameObject BackObjects => backObjects;
    public float BothVisibleRangeMeters => bothVisibleRangeMeters;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        UpdateVisibility();
    }

    private void LateUpdate()
    {
        UpdateVisibility();
    }

    private void OnValidate()
    {
        bothVisibleRangeMeters = Mathf.Max(0f, bothVisibleRangeMeters);
        ResolveObjectRoots();
    }

    public void UpdateVisibility()
    {
        if (trackedPose == null)
        {
            ResolveReferences();
            if (trackedPose == null)
                return;
        }

        float heightFromSurface = trackedPose.position.y - transform.position.y;
        SetActiveIfNeeded(frontObjects, heightFromSurface >= -bothVisibleRangeMeters);
        SetActiveIfNeeded(backObjects, heightFromSurface <= bothVisibleRangeMeters);
    }

    private void ResolveReferences()
    {
        if (trackedPose == null && Camera.main != null)
            trackedPose = Camera.main.transform;

        ResolveObjectRoots();
    }

    private void ResolveObjectRoots()
    {
        if (frontObjects == null)
            frontObjects = FindDirectChild("FrontObjects", "FloodPaneFront");
        if (backObjects == null)
            backObjects = FindDirectChild("BackObjects", "FloodPaneBack");
    }

    private GameObject FindDirectChild(string preferredName, string fallbackName)
    {
        Transform child = transform.Find(preferredName);
        if (child == null)
            child = transform.Find(fallbackName);
        return child != null ? child.gameObject : null;
    }

    private void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target == null || target == gameObject || target.activeSelf == active)
            return;

        target.SetActive(active);
    }
}
