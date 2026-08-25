using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Projects a selected fringe mesh onto the face only.  It is deliberately
/// separate from UTS so the face material keeps its normal toon bands.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(500)]
public sealed class FringeShadowController : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Uses Camera.main when left empty.")]
    public Camera targetCamera;

    [Tooltip("Uses RenderSettings.sun when left empty.")]
    public Light shadowLight;

    [Tooltip("Uses the renderer whose name contains the face marker when left empty.")]
    public Renderer faceRenderer;

    [Tooltip("Uses the default front-fringe renderer when left empty. Add more renderers for split bangs.")]
    public Renderer[] fringeRenderers;

    [Tooltip("Template material for the internal fringe-depth pass. Keep this assigned for player builds.")]
    public Material casterTemplate;

    [Tooltip("Template material for the internal face overlay pass. Keep this assigned for player builds.")]
    public Material receiverTemplate;

    [Header("Shadow Shape")]
    [Range(64, 1024)]
    public int resolution = 512;

    [Min(0.02f)]
    [Tooltip("Half-width of the light-space region around the face, in world units.")]
    public float projectionExtent = 0.28f;

    [Min(0.02f)]
    [Tooltip("Light-space depth covered by the projection.")]
    public float projectionDepth = 0.8f;

    [Min(0f)]
    [Tooltip("Prevents the face from shadowing itself due to depth precision.")]
    public float depthBias = 0.004f;

    [Range(0f, 3f)]
    [Tooltip("PCF radius in shadow-map pixels. Use 1-2 for a soft anime edge.")]
    public float softness = 1.2f;

    [Header("Toon Appearance")]
    [Range(0f, 1f)]
    public float strength = 0.34f;

    [Tooltip("Neutral/cool multiplier used instead of a warm, realistic shadow tint.")]
    public Color shadowTint = new Color(0.86f, 0.87f, 0.91f, 1f);

    private const string FaceName = "\u984F";
    private const string DefaultFringeName = "019_\u9AEA-\u4E2D\u524D\u9AEA";

    private static readonly int ShadowMapId = Shader.PropertyToID("_FringeShadowMap");
    private static readonly int WorldToShadowId = Shader.PropertyToID("_FringeWorldToShadow");
    private static readonly int WorldToLightId = Shader.PropertyToID("_FringeWorldToLight");
    private static readonly int ShadowParamsId = Shader.PropertyToID("_FringeShadowParams");
    private static readonly int ShadowTintId = Shader.PropertyToID("_FringeShadowTint");
    private static readonly int TexelSizeId = Shader.PropertyToID("_FringeShadowTexelSize");

    private Camera commandCamera;
    private CommandBuffer commandBuffer;
    private RenderTexture fringeDepth;
    private Material casterMaterial;
    private Material receiverMaterial;

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

    private void OnValidate()
    {
        resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 64, 1024));
        projectionExtent = Mathf.Max(0.02f, projectionExtent);
        projectionDepth = Mathf.Max(0.02f, projectionDepth);
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

    private void FindRenderers()
    {
        if (faceRenderer == null)
        {
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
            {
                if (candidate.name.Contains(FaceName))
                {
                    faceRenderer = candidate;
                    break;
                }
            }
        }

        if (fringeRenderers == null || fringeRenderers.Length == 0)
        {
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
            {
                if (candidate.name.Contains(DefaultFringeName))
                {
                    fringeRenderers = new[] { candidate };
                    break;
                }
            }
        }
    }

    private void EnsureResources()
    {
        Camera desiredCamera = targetCamera != null ? targetCamera : Camera.main;
        if (desiredCamera != commandCamera)
        {
            RemoveCommandBuffer();
            commandCamera = desiredCamera;
        }

        if (casterMaterial == null)
        {
            if (casterTemplate != null)
            {
                casterMaterial = new Material(casterTemplate) { hideFlags = HideFlags.HideAndDontSave };
            }
            else
            {
                Shader shader = Shader.Find("Hidden/Anime/FringeDepth");
                if (shader != null)
                {
                    casterMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
        }

        if (receiverMaterial == null)
        {
            if (receiverTemplate != null)
            {
                receiverMaterial = new Material(receiverTemplate) { hideFlags = HideFlags.HideAndDontSave };
            }
            else
            {
                Shader shader = Shader.Find("Hidden/Anime/FringeShadowReceiver");
                if (shader != null)
                {
                    receiverMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
        }

        if (fringeDepth == null || fringeDepth.width != resolution)
        {
            ReleaseDepthTexture();
            fringeDepth = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
            {
                name = "Fringe Shadow Depth",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            fringeDepth.Create();
        }

        if (commandCamera != null && commandBuffer == null && casterMaterial != null && receiverMaterial != null)
        {
            commandBuffer = new CommandBuffer { name = "Anime Fringe Shadow" };
            commandCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);
        }
    }

    private void RebuildCommandBuffer()
    {
        if (commandBuffer == null || commandCamera == null || faceRenderer == null || fringeDepth == null ||
            fringeRenderers == null || fringeRenderers.Length == 0)
        {
            return;
        }

        Light light = shadowLight != null ? shadowLight : RenderSettings.sun;
        if (light == null)
        {
            return;
        }

        Vector3 center = faceRenderer.bounds.center;
        Vector3 lightDirection = light.type == LightType.Directional
            ? light.transform.forward
            : (center - light.transform.position).normalized;
        if (lightDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 up = Mathf.Abs(Vector3.Dot(lightDirection, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        Vector3 lightPosition = center - lightDirection * (projectionDepth * 0.5f);
        Matrix4x4 worldToLight = Matrix4x4.LookAt(lightPosition, center, up);
        Matrix4x4 projection = Matrix4x4.Ortho(
            -projectionExtent, projectionExtent, -projectionExtent, projectionExtent, 0.01f, projectionDepth);
        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(projection, true);

        receiverMaterial.SetTexture(ShadowMapId, fringeDepth);
        receiverMaterial.SetMatrix(WorldToShadowId, gpuProjection * worldToLight);
        receiverMaterial.SetMatrix(WorldToLightId, worldToLight);
        receiverMaterial.SetVector(ShadowParamsId, new Vector4(depthBias, strength, projectionDepth, 0f));
        receiverMaterial.SetColor(ShadowTintId, shadowTint);
        receiverMaterial.SetVector(TexelSizeId, new Vector4(1f / resolution, 1f / resolution, softness, 0f));

        commandBuffer.Clear();
        commandBuffer.SetRenderTarget(fringeDepth);
        commandBuffer.ClearRenderTarget(true, true, Color.white);
        commandBuffer.SetViewProjectionMatrices(worldToLight, gpuProjection);
        foreach (Renderer fringe in fringeRenderers)
        {
            if (fringe != null && fringe.enabled && fringe.gameObject.activeInHierarchy)
            {
                commandBuffer.DrawRenderer(fringe, casterMaterial);
            }
        }

        commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        commandBuffer.SetViewProjectionMatrices(commandCamera.worldToCameraMatrix, commandCamera.projectionMatrix);
        int subMeshCount = Mathf.Max(1, faceRenderer.sharedMaterials.Length);
        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            commandBuffer.DrawRenderer(faceRenderer, receiverMaterial, subMesh);
        }
    }

    private void RemoveCommandBuffer()
    {
        if (commandCamera != null && commandBuffer != null)
        {
            commandCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);
        }

        if (commandBuffer != null)
        {
            commandBuffer.Release();
            commandBuffer = null;
        }
    }

    private void ReleaseResources()
    {
        ReleaseDepthTexture();
        DestroyObject(casterMaterial);
        DestroyObject(receiverMaterial);
        casterMaterial = null;
        receiverMaterial = null;
    }

    private void ReleaseDepthTexture()
    {
        if (fringeDepth != null)
        {
            fringeDepth.Release();
            DestroyObject(fringeDepth);
            fringeDepth = null;
        }
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
