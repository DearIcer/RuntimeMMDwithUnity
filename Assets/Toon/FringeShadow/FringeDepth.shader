Shader "Hidden/Anime/FringeDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float lightDepth : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float4 worldPosition = mul(unity_ObjectToWorld, input.vertex);
                output.position = mul(UNITY_MATRIX_VP, worldPosition);
                output.lightDepth = -mul(UNITY_MATRIX_V, worldPosition).z;
                return output;
            }

            half frag(v2f input) : SV_Target
            {
                return input.lightDepth;
            }
            ENDCG
        }
    }
}
