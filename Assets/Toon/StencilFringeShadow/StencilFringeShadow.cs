using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Stencil-based fringe (bangs) shadow, adapted to the built-in render pipeline.
///
/// Reference: https://zhuanlan.zhihu.com/p/416577141 ("刘海投影·改")
///
/// The article drives this from a URP ScriptableRendererFeature injected at
/// BeforeRenderingTransparents. The built-in pipeline has no render features, so the
/// same work is issued through a CommandBuffer attached at CameraEvent.BeforeForwardAlpha,
/// which fires at the same point: every opaque object has been drawn, nothing transparent
/// has been drawn yet.
///
/// Sequence per frame:
///   1. FaceStencilWriter redraws the face colourlessly, stamping _StencilRef onto its
///      pixels. The depth buffer already holds the face, so ZTest LEqual confines the
///      write to the face pixels that survived the opaque pass.
///   2. HairShadowStencil redraws the hair with its clip-space XY pushed along the
///      screen-space light direction. ZTest LEqual discards hair that is behind the face,
///      the stencil test keeps the shadow on the face, and the multiply blend tints
///      whatever toon shading the face already had.
///
/// The character shader is never touched, so UTS2 keeps its normal two-tone bands and
/// all 34 materials behave exactly as before.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(600)]
public sealed class StencilFringeShadow : MonoBehaviour
{
    [Header("Renderers")]
    [Tooltip("Meshes that receive the shadow. Auto-detected from the name below when left empty.")]
    public Renderer[] faceRenderers;

    [Tooltip("Meshes that cast the shadow. Auto-detected from the name below when left empty.")]
    public Renderer[] fringeRenderers;

    [Tooltip("Substring used to pick the face mesh out of the hierarchy. Defaults to the character's face mesh, 顏.")]
    public string faceNameMarker = "\u984F";

    [Tooltip("Substring used to pick the hair meshes out of the hierarchy. Defaults to 髪, which every hair mesh of this character carries.")]
    public string fringeNameMarker = "\u9AEA";

    [Header("Shadow")]
    [Range(1, 255)]
    [Tooltip("Stencil value the face stamps and the hair tests against. Must be non-zero.")]
    public int stencilRef = 128;

    [Tooltip("Multiply tint applied to the face under the fringe. Warm and slightly pink keeps it in the UTS2 1st shade family.")]
    public Color shadowTint = new Color(0.86f, 0.72f, 0.74f, 1f);

    [Min(0f)]
    [Tooltip("Screen displacement per unit of light tilt, in NDC units. NDC spans -1..1, so 0.05 is 2.5% of the viewport width. Raise it for longer shadows.")]
    public float offset = 0.05f;

    [Tooltip("Directional light driving the shadow direction. Falls back to the brightest enabled directional light.")]
    public Light shadowLight;

    [Header("Templates")]
    [Tooltip("Material using Hidden/Anime/HairShadowStencil. Keep assigned so player builds include the shader.")]
    public Material casterTemplate;

    [Tooltip("Material using Hidden/Anime/FaceStencilWriter. Keep assigned so player builds include the shader.")]
    public Material writerTemplate;

    private const int PlayerBuildStencilRef = 128;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
    private static readonly int LightDirSSId = Shader.PropertyToID("_LightDirSS");
    private static readonly int StencilRefPropertyId = Shader.PropertyToID("_StencilRef");

    private Camera commandCamera;
    private CommandBuffer commandBuffer;
    private Material casterMaterial;
    private Material writerMaterial;
    private readonly List<Renderer> drawList = new List<Renderer>();

    private void OnEnable()
    {
        FindRenderers();
        EnsureResources();
    }

    private void LateUpdate()
    {
        FindRenderers();
        EnsureResources();
        RebuildCommandBuffer();
    }

    private void OnDisable()
    {
        RemoveCommandBuffer();
        ReleaseResources();
    }

    private void OnDestroy()
    {
        RemoveCommandBuffer();
        ReleaseResources();
    }

    private void OnValidate()
    {
        stencilRef = Mathf.Clamp(stencilRef == 0 ? PlayerBuildStencilRef : stencilRef, 1, 255);
        offset = Mathf.Max(0f, offset);
    }

    /// <summary>
    /// The model has no dedicated hair layer, and the meshes carry stable MMD names, so
    /// the casters are picked by name instead of asking for manual layer setup.
    /// </summary>
    private void FindRenderers()
    {
        bool needFace = faceRenderers == null || faceRenderers.Length == 0;
        bool needFringe = fringeRenderers == null || fringeRenderers.Length == 0;
        if (!needFace && !needFringe)
        {
            return;
        }

        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        if (needFace && !string.IsNullOrEmpty(faceNameMarker))
        {
            List<Renderer> found = new List<Renderer>();
            foreach (Renderer candidate in all)
            {
                if (candidate.name.Contains(faceNameMarker))
                {
                    found.Add(candidate);
                }
            }

            if (found.Count > 0)
            {
                faceRenderers = found.ToArray();
            }
        }

        if (needFringe && !string.IsNullOrEmpty(fringeNameMarker))
        {
            List<Renderer> found = new List<Renderer>();
            foreach (Renderer candidate in all)
            {
                if (candidate.name.Contains(fringeNameMarker))
                {
                    found.Add(candidate);
                }
            }

            if (found.Count > 0)
            {
                fringeRenderers = found.ToArray();
            }
        }
    }

    private void EnsureResources()
    {
        Camera desiredCamera = Camera.main;
        if (desiredCamera != commandCamera)
        {
            RemoveCommandBuffer();
            commandCamera = desiredCamera;
        }

        if (casterMaterial == null)
        {
            casterMaterial = CreateMaterial(casterTemplate, "Hidden/Anime/HairShadowStencil");
        }

        if (writerMaterial == null)
        {
            writerMaterial = CreateMaterial(writerTemplate, "Hidden/Anime/FaceStencilWriter");
        }

        if (commandCamera != null && commandBuffer == null && casterMaterial != null && writerMaterial != null)
        {
            commandBuffer = new CommandBuffer { name = "Stencil Fringe Shadow" };
            commandCamera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commandBuffer);
        }
    }

    private static Material CreateMaterial(Material template, string shaderName)
    {
        if (template != null)
        {
            return new Material(template) { hideFlags = HideFlags.HideAndDontSave };
        }

        Shader shader = Shader.Find(shaderName);
        return shader != null ? new Material(shader) { hideFlags = HideFlags.HideAndDontSave } : null;
    }

    private void RebuildCommandBuffer()
    {
        if (commandBuffer == null || commandCamera == null)
        {
            return;
        }

        commandBuffer.Clear();

        if (faceRenderers == null || fringeRenderers == null || faceRenderers.Length == 0 || fringeRenderers.Length == 0)
        {
            return;
        }

        Light light = ResolveLight();
        if (light == null)
        {
            return;
        }

        // A directional light shines along its forward axis, which is exactly the
        // direction the fringe shadow should be displaced towards.
        Vector3 directionWorld = light.transform.forward;
        if (directionWorld.sqrMagnitude < 1e-6f)
        {
            return;
        }

        Vector3 directionView = commandCamera.worldToCameraMatrix.MultiplyVector(directionWorld);

        writerMaterial.SetFloat(StencilRefPropertyId, stencilRef);
        casterMaterial.SetFloat(StencilRefPropertyId, stencilRef);
        casterMaterial.SetColor(ColorId, shadowTint);
        casterMaterial.SetFloat(OffsetId, offset);
        casterMaterial.SetVector(LightDirSSId, new Vector4(directionView.x, directionView.y, 0f, 0f));

        // 1) Stamp the face into the stencil buffer (colourless, depth-tested).
        CollectDrawable(faceRenderers);
        for (int i = 0; i < drawList.Count; i++)
        {
            commandBuffer.DrawRenderer(drawList[i], writerMaterial);
        }

        // 2) Project the hair along the light and tint the face pixels it lands on.
        CollectDrawable(fringeRenderers);
        for (int i = 0; i < drawList.Count; i++)
        {
            commandBuffer.DrawRenderer(drawList[i], casterMaterial);
        }
    }

    private void CollectDrawable(Renderer[] source)
    {
        drawList.Clear();
        for (int i = 0; i < source.Length; i++)
        {
            Renderer candidate = source[i];
            if (candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                drawList.Add(candidate);
            }
        }
    }

    private Light ResolveLight()
    {
        if (shadowLight != null && shadowLight.enabled && shadowLight.type == LightType.Directional)
        {
            return shadowLight;
        }

        // RenderSettings.sun is unassigned in this project, so fall back to the brightest
        // enabled directional light, which is the one the shaders treat as the main light.
        Light best = null;
        float bestScore = 0f;
        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (!candidate.enabled || !candidate.gameObject.activeInHierarchy || candidate.type != LightType.Directional)
            {
                continue;
            }

            float score = candidate.intensity * Mathf.Max(candidate.color.r, Mathf.Max(candidate.color.g, candidate.color.b));
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private void RemoveCommandBuffer()
    {
        if (commandCamera != null && commandBuffer != null)
        {
            commandCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commandBuffer);
        }

        if (commandBuffer != null)
        {
            commandBuffer.Release();
            commandBuffer = null;
        }
    }

    private void ReleaseResources()
    {
        DestroyObject(casterMaterial);
        DestroyObject(writerMaterial);
        casterMaterial = null;
        writerMaterial = null;
    }

    private static void DestroyObject(Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(value);
        }
        else
        {
            Object.DestroyImmediate(value);
        }
    }
}
