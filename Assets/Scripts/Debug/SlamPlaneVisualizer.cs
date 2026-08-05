using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class SlamPlaneVisualizer : MonoBehaviour
{
    public static SlamPlaneVisualizer Instance { get; private set; }

    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Material horizontalMaterial;
    [SerializeField] private Material verticalMaterial;

    private bool isVisible = false;

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
        if (!isVisible) return;

        foreach (var plane in args.added)
        {
            ApplyMaterial(plane);
        }
        foreach (var plane in args.updated)
        {
            ApplyMaterial(plane);
        }
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
