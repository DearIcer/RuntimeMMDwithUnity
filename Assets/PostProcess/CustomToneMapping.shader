Shader "Custom/NeutralToneMapping"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure (stops)", Float) = 0
        _Knee ("Highlight Knee", Range(0, 1)) = 1
        _WhitePoint ("Highlight Ceiling", Float) = 1
        _HighlightDesaturation ("Highlight Desaturation", Range(0, 1)) = 0.15
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
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure;
            float _Knee;
            float _WhitePoint;
            float _HighlightDesaturation;
            float _Contrast;
            float _Saturation;
            float _Temperature;
            float _Tint;

            static const float3 LumaWeights = float3(0.2126, 0.7152, 0.0722);

            // Linear-space mid grey. Used as the contrast pivot so the Contrast knob
            // does not double as a brightness knob.
            static const float MidGrey = 0.18;

            // Highlight shoulder, evaluated on luminance only.
            //
            // Below _Knee this returns its input exactly, so with the default knee of 1
            // every colour a texture can actually produce passes straight through: an
            // 8-bit sRGB texel lit at full strength comes out as that very same texel.
            // That is the difference from ACES/Filmic, whose S-curve reaches into the
            // mid-tones and is what gives an image its characteristic warm/yellow cast.
            //
            // Above the knee the curve is tangent-continuous (slope 1) and rolls off
            // towards _WhitePoint, so HDR highlights fade into white instead of clipping
            // into hard-edged blobs.
            float HighlightShoulder(float luminance)
            {
                float knee = clamp(_Knee, 0.0, 1.0);
                if (luminance <= knee)
                    return luminance;

                float ceiling = max(_WhitePoint, knee + 0.001);
                float span = ceiling - knee;
                return knee + span * (1.0 - exp(-(luminance - knee) / span));
            }

            // Tone map on luminance only. Every channel is scaled by the same factor, so
            // the RGB ratios - and therefore the hue and saturation of the source texture
            // - survive the operation untouched.
            float3 ApplyNeutralToneMapping(float3 color)
            {
                float luminance = max(dot(color, LumaWeights), 0.0);
                float mapped = HighlightShoulder(luminance);

                float3 result = luminance > 1e-5 ? color * (mapped / luminance) : color;

                // Only the part of the signal that travelled past the knee loses
                // saturation, and it does so around its own luminance so nothing rotates
                // towards orange. Pixels at or below the knee keep 100% of their
                // saturation, which is what keeps flat toon shading looking clean.
                float overshoot = max(luminance - clamp(_Knee, 0.0, 1.0), 0.0);
                if (overshoot > 0.0 && _HighlightDesaturation > 0.0)
                {
                    float blend = 1.0 - 1.0 / (_HighlightDesaturation * overshoot + 1.0);
                    result = lerp(result, mapped.xxx, saturate(blend));
                }

                return max(result, 0.0);
            }

            // von Kries style channel gains in linear space. The gains are normalised by
            // their own luminance so white balance never doubles as a brightness knob.
            // At the default of 0/0 the gains are exactly 1, i.e. a true pass-through.
            float3 ApplyWhiteBalance(float3 color)
            {
                float3 gain;
                gain.r = 1.0 + _Temperature * 0.30 + _Tint * 0.15;
                gain.g = 1.0 - _Tint * 0.30;
                gain.b = 1.0 - _Temperature * 0.30 + _Tint * 0.15;
                gain = max(gain, 0.0);
                gain /= max(dot(gain, LumaWeights), 1e-4);

                return max(color * gain, 0.0);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float4 source = tex2D(_MainTex, input.uv);

                float3 color = source.rgb * exp2(_Exposure);
                color = ApplyWhiteBalance(color);
                color = ApplyNeutralToneMapping(color);

                // Saturation around the pixel's own luminance: hue never rotates.
                if (_Saturation != 1.0)
                {
                    float luminance = dot(color, LumaWeights);
                    color = luminance.xxx + (color - luminance.xxx) * _Saturation;
                }

                if (_Contrast != 1.0)
                    color = (color - MidGrey) * _Contrast + MidGrey;

                return float4(saturate(color), source.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
