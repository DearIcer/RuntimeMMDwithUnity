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

    [Tooltip("Everything at or below this luminance is passed through untouched. Keep at 1 so texture colours are reproduced exactly; lower it (try 0.8) only if you want a filmic highlight roll-off.")]
    [Range(0f, 1f)]
    public float knee = 1f;

    [Tooltip("Luminance the shoulder rolls off towards. 1 is display white, which means HDR highlights fade into white instead of clipping. Only matters when knee is below 1.")]
    [Min(0.01f)]
    public float whitePoint = 1f;

    [Tooltip("How much rolled-off highlights lose saturation. Only affects pixels brighter than the knee, so the SDR range stays fully saturated.")]
    [Range(0f, 1f)]
    public float highlightDesaturation = 0.15f;

    [Header("Color")]
    [Tooltip("1 is a pass-through. Applied around linear mid grey so the pivot does not shift brightness.")]
    [Range(0f, 2f)]
    public float contrast = 1f;

    [Tooltip("1 is a pass-through. Applied around each pixel's own luminance, so hue is preserved.")]
    [Range(0f, 2f)]
    public float saturation = 1f;

    [Tooltip("Positive values warm the image, negative values cool it. Luminance-normalised, so it does not change brightness. Use it to cancel a tinted light instead of re-lighting the scene.")]
    [Range(-1f, 1f)]
    public float temperature;

    [Tooltip("Positive values add magenta, negative values add green. Luminance-normalised.")]
    [Range(-1f, 1f)]
    public float tint;

    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int KneeId = Shader.PropertyToID("_Knee");
    private static readonly int WhitePointId = Shader.PropertyToID("_WhitePoint");
    private static readonly int HighlightDesaturationId = Shader.PropertyToID("_HighlightDesaturation");
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
        // Exactly 1 keeps the identity range all the way up to display white, so a pure
        // white texel stays pure white. The shader guards the degenerate
        // whitePoint <= knee case on its own.
        toneMappingMaterial.SetFloat(KneeId, Mathf.Clamp01(knee));
        toneMappingMaterial.SetFloat(WhitePointId, Mathf.Max(whitePoint, knee + 0.001f));
        toneMappingMaterial.SetFloat(HighlightDesaturationId, highlightDesaturation);
        toneMappingMaterial.SetFloat(ContrastId, contrast);
        toneMappingMaterial.SetFloat(SaturationId, saturation);
        toneMappingMaterial.SetFloat(TemperatureId, temperature);
        toneMappingMaterial.SetFloat(TintId, tint);
        Graphics.Blit(source, destination, toneMappingMaterial);
    }
}
