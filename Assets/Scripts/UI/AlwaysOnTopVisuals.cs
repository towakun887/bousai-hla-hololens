using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AlwaysOnTopVisuals : MonoBehaviour
{
    [SerializeField] private int canvasSortingOrder = 32000;
    [SerializeField] private bool includeMeshRenderers = true;

    [Header("Shaders")]
    [SerializeField] private Shader uiOverlayShader;
    [SerializeField] private Shader unlitOverlayShader;
    [SerializeField] private Shader distanceFieldOverlayShader;
    [SerializeField] private Shader mobileDistanceFieldOverlayShader;

    private readonly List<Material> ownedMaterials = new List<Material>();
    private bool applied;

    private void Awake()
    {
        ResolveShaders();
        Apply();
    }

    public void Apply()
    {
        if (applied)
            return;

        ResolveShaders();
        applied = true;
        ConfigureCanvases();
        ConfigureText();
        ConfigureGraphics();
        if (includeMeshRenderers)
            ConfigureMeshRenderers();
    }

    private void ConfigureCanvases()
    {
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].overrideSorting = true;
            canvases[i].sortingOrder = canvasSortingOrder;
        }
    }

    private void ConfigureText()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Material source = texts[i].fontSharedMaterial;
            if (source == null)
                continue;

            Shader overlayShader = source.shader != null &&
                                   source.shader.name.Contains("Mobile")
                ? mobileDistanceFieldOverlayShader
                : distanceFieldOverlayShader;
            if (overlayShader == null)
                continue;

            Material overlay = new Material(source)
            {
                name = source.name + " (Always On Top)",
                shader = overlayShader,
                renderQueue = 4000
            };
            ownedMaterials.Add(overlay);
            texts[i].fontSharedMaterial = overlay;
        }
    }

    private void ConfigureGraphics()
    {
        if (uiOverlayShader == null)
            return;

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] is TMP_Text)
                continue;

            Material overlay = new Material(uiOverlayShader)
            {
                name = graphics[i].name + " (Always On Top)",
                renderQueue = 4000
            };
            ownedMaterials.Add(overlay);
            graphics[i].material = overlay;
        }
    }

    private void ConfigureMeshRenderers()
    {
        if (unlitOverlayShader == null)
            return;

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] sourceMaterials = renderers[i].sharedMaterials;
            Material[] overlayMaterials = new Material[sourceMaterials.Length];
            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material source = sourceMaterials[materialIndex];
                if (source == null)
                    continue;

                Material overlay = new Material(source)
                {
                    name = source.name + " (Always On Top)",
                    shader = unlitOverlayShader,
                    renderQueue = 4000
                };
                ownedMaterials.Add(overlay);
                overlayMaterials[materialIndex] = overlay;
            }
            renderers[i].sharedMaterials = overlayMaterials;
        }
    }

    private void ResolveShaders()
    {
        if (uiOverlayShader == null)
            uiOverlayShader = Shader.Find("Bousai/UI/Always On Top");
        if (unlitOverlayShader == null)
            unlitOverlayShader = Shader.Find("Bousai/Unlit Always On Top");
        if (distanceFieldOverlayShader == null)
            distanceFieldOverlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (mobileDistanceFieldOverlayShader == null)
            mobileDistanceFieldOverlayShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
    }

    private void OnDestroy()
    {
        for (int i = 0; i < ownedMaterials.Count; i++)
        {
            if (ownedMaterials[i] != null)
                Destroy(ownedMaterials[i]);
        }
        ownedMaterials.Clear();
    }
}
