Shader "Hidden/Anime/FringeShadowReceiver"
{
    Properties
    {
        _FringeShadowMap ("Fringe Shadow Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend DstColor Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FringeShadowMap;
            float4x4 _FringeWorldToShadow;
            float4x4 _FringeWorldToLight;
            float4 _FringeShadowParams;
            float4 _FringeShadowTint;
            float4 _FringeShadowTexelSize;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 shadowPosition : TEXCOORD0;
                float lightDepth : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float4 worldPosition = mul(unity_ObjectToWorld, input.vertex);
                output.position = UnityObjectToClipPos(input.vertex);
                output.shadowPosition = mul(_FringeWorldToShadow, worldPosition);
                output.lightDepth = -mul(_FringeWorldToLight, worldPosition).z;
                return output;
            }

            float IsOccluded(float2 uv, float receiverDepth)
            {
                float casterDepth = tex2D(_FringeShadowMap, uv).r;
                return step(casterDepth + _FringeShadowParams.x, receiverDepth);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 projection = input.shadowPosition.xyz / input.shadowPosition.w;
                float2 uv = projection.xy * 0.5 + 0.5;
                float inProjection = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                inProjection *= step(0.0, projection.z) * step(projection.z, 1.0);

                float2 texel = _FringeShadowTexelSize.xy * _FringeShadowTexelSize.z;
                float occlusion = IsOccluded(uv, input.lightDepth) * 0.4;
                occlusion += IsOccluded(uv + float2(texel.x, 0.0), input.lightDepth) * 0.15;
                occlusion += IsOccluded(uv - float2(texel.x, 0.0), input.lightDepth) * 0.15;
                occlusion += IsOccluded(uv + float2(0.0, texel.y), input.lightDepth) * 0.15;
                occlusion += IsOccluded(uv - float2(0.0, texel.y), input.lightDepth) * 0.15;
                occlusion *= inProjection * _FringeShadowParams.y;

                float3 multiplier = lerp(float3(1.0, 1.0, 1.0), _FringeShadowTint.rgb, saturate(occlusion));
                return fixed4(multiplier, 1.0);
            }
            ENDCG
        }
    }
}
