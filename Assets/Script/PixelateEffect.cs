using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class PixelateEffect : MonoBehaviour
{
    [Tooltip("Pixelate 材质，使用 Custom/Pixelate Shader")]
    public Material pixelateMaterial;
    [Tooltip("像素块大小 (px)")]
    [Range(1, 100)]
    public float pixelSize = 8f;

    [Range(1, 32)] public int colorDepth;
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (pixelateMaterial != null)
        {
            pixelateMaterial.SetFloat("_PixelSize", pixelSize);
            pixelateMaterial.SetFloat("_ColorDepth", colorDepth);
            Graphics.Blit(src, dest, pixelateMaterial); // :contentReference[oaicite:3]{index=3}
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}