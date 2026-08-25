using UnityEngine;

[ExecuteInEditMode]
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public sealed class CustomToneMapping : MonoBehaviour
{
    [Tooltip("Material using the Custom/NeutralToneMapping shader.")]
    public Material toneMappingMaterial;

    [Header("Tone Mapping")]
    [Tooltip("Exposure in stops applied before tone mapping.")]
    [Range(-4f, 4f)]
    public float exposure;

    [Tooltip("Highlight shoulder strength. 4 is the neutral default; lower values roll highlights off earlier.")]
    [Min(0.01f)]
    public float whitePoint = 4f;

    [Tooltip("Controls how early highlights are compressed. Use 0.25-0.5 for toon materials.")]
    [Range(0f, 1f)]
    public float curveStrength = 0.35f;

    [Header("Color")]
    [Range(0f, 2f)]
    public float contrast = 1f;

    [Range(0f, 2f)]
    public float saturation = 1f;

    [Tooltip("Positive values cool the image; negative values warm it.")]
    [Range(-1f, 1f)]
    public float temperature;

    [Tooltip("Positive values add magenta; negative values add green.")]
    [Range(-1f, 1f)]
    public float tint;

    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int WhitePointId = Shader.PropertyToID("_WhitePoint");
    private static readonly int CurveStrengthId = Shader.PropertyToID("_CurveStrength");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int TemperatureId = Shader.PropertyToID("_Temperature");
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (toneMappingMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        toneMappingMaterial.SetFloat(ExposureId, exposure);
        toneMappingMaterial.SetFloat(WhitePointId, Mathf.Max(0.01f, whitePoint));
        toneMappingMaterial.SetFloat(CurveStrengthId, curveStrength);
        toneMappingMaterial.SetFloat(ContrastId, contrast);
        toneMappingMaterial.SetFloat(SaturationId, saturation);
        toneMappingMaterial.SetFloat(TemperatureId, temperature);
        toneMappingMaterial.SetFloat(TintId, tint);
        Graphics.Blit(source, destination, toneMappingMaterial);
    }
}
