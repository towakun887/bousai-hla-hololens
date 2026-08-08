using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class SlamPlaneVisualizer : MonoBehaviour
{
    public static SlamPlaneVisualizer Instance { get; private set; }

    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Material horizontalMaterial;
    [SerializeField] private Material verticalMaterial;
    [SerializeField] private bool logDevelopmentDiagnostics = true;

    private bool isVisible = false;
    private int addedThisSecond;
    private int updatedThisSecond;
    private int removedThisSecond;
    private float nextDiagnosticTime;

    public bool DevelopmentDiagnosticsEnabled => logDevelopmentDiagnostics;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>();

        if (planeManager != null)
        {
            planeManager.planesChanged += OnPlanesChanged;
        }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        UpdateAllPlanesVisibility();
    }

    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
{
    foreach (var plane in args.added)
    {
        ApplyMaterial(plane);
    }

    foreach (var plane in args.updated)
    {
        ApplyMaterial(plane);
    }
}

    private void Update()
    {
#if DEVELOPMENT_BUILD
        if (!logDevelopmentDiagnostics || Time.unscaledTime < nextDiagnosticTime) return;
        nextDiagnosticTime = Time.unscaledTime + 1f;
        int trackableCount = planeManager != null ? planeManager.trackables.count : 0;
        bool hasSubsystem = planeManager != null && planeManager.subsystem != null;
        Debug.Log($"[PlaneDiagnostics] subsystem={hasSubsystem}, trackables={trackableCount}, added/s={addedThisSecond}, updated/s={updatedThisSecond}, removed/s={removedThisSecond}");
        addedThisSecond = 0;
        updatedThisSecond = 0;
        removedThisSecond = 0;
#endif
    }

    private void ApplyMaterial(ARPlane plane)
    {
        var meshRenderer = plane.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = isVisible;
            if (isVisible)
            {
                if (plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.HorizontalUp ||
                    plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.HorizontalDown)
                {
                    meshRenderer.material = horizontalMaterial;
                }
                else
                {
                    meshRenderer.material = verticalMaterial;
                }
            }
        }
    }

    private void OnDestroy()
{
    if (planeManager != null)
    {
        planeManager.planesChanged -= OnPlanesChanged;
    }

    if (Instance == this)
    {
        Instance = null;
    }
}

    private void UpdateAllPlanesVisibility()
    {
        if (planeManager == null) return;

        foreach (var plane in planeManager.trackables)
        {
            var meshRenderer = plane.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = isVisible;
            }
        }
    }
}
