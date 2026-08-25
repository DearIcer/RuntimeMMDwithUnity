Shader "Custom/NeutralToneMapping"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure", Float) = 0
        _WhitePoint ("White Point", Float) = 4
        _CurveStrength ("Curve Strength", Range(0, 1)) = 0.35
        _Contrast ("Contrast", Float) = 1
        _Saturation ("Saturation", Float) = 1
        _Temperature ("Temperature", Range(-1, 1)) = 0
        _Tint ("Tint", Range(-1, 1)) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure;
            float _WhitePoint;
            float _CurveStrength;
            float _Contrast;
            float _Saturation;
            float _Temperature;
            float _Tint;

            float ToonShoulder(float luminance)
            {
                // Keep the texture's shadows and midtones unmodified. A soft knee prevents HDR
                // highlights from clipping without adding the contrasty S-curve of ACES.
                float x = max(luminance * (4.0 / max(_WhitePoint, 0.01)), 0.0);
                float kneeStart = lerp(0.9, 0.5, saturate(_CurveStrength));

                if (x <= kneeStart)
                    return x;

                float range = max(1.0 - kneeStart, 0.001);
                return kneeStart + range * (1.0 - exp(-(x - kneeStart) / range));
            }

            float3 ApplyNeutralToneMapping(float3 color)
            {
                float luminance = max(dot(color, float3(0.2126, 0.7152, 0.0722)), 0.0);
                float mappedLuminance = ToonShoulder(luminance);

                // Scale by luminance so RGB ratios, and therefore texture hue, are preserved.
                float scale = luminance > 0.0001 ? mappedLuminance / luminance : 1.0;
                return max(color * scale, 0.0);
            }

            float3 ApplyChromaticCorrection(float3 color)
            {
                // A restrained channel balance is applied after tone mapping, where it cannot
                // change highlight roll-off or produce a yellow cast by itself.
                color.r *= 1.0 - _Temperature * 0.08;
                color.b *= 1.0 + _Temperature * 0.08;
                color.r *= 1.0 + _Tint * 0.04;
                color.g *= 1.0 - _Tint * 0.04;
                return max(color, 0.0);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float4 source = tex2D(_MainTex, input.uv);
                float3 color = source.rgb;
                color *= exp2(_Exposure);
                color = ApplyNeutralToneMapping(color);
                color = ApplyChromaticCorrection(color);

                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(luminance.xxx, color, _Saturation);
                color = lerp(0.5.xxx, color, _Contrast);
                return float4(saturate(color), source.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
