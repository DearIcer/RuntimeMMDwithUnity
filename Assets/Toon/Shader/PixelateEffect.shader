Shader "Custom/PixelateEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Range(1, 100)) = 10
        _ColorDepth ("Color Depth", Range(1, 32)) = 8
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _PixelSize;
            float _ColorDepth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 计算像素化后的UV坐标
                float2 pixelSize = _PixelSize / _ScreenParams.xy;
                float2 uv = floor(i.uv / pixelSize) * pixelSize;
                
                // 采样纹理
                fixed4 col = tex2D(_MainTex, uv);
                
                // 颜色深度量化
                float steps = pow(2, _ColorDepth);
                col.rgb = floor(col.rgb * steps) / steps;
                
                return col;
            }
            ENDCG
        }
    }
}