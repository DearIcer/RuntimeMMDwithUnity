// Stamps the face into the stencil buffer so HairShadowStencil.shader knows which
// pixels are allowed to receive the fringe shadow.
//
// Reference: https://zhuanlan.zhihu.com/p/416577141  ("刘海投影·改")
//
// The article adds a Stencil block to the character shader itself. Doing that here would
// mean editing UTS2's shared FORWARD pass, which every one of the 34 character materials
// runs through. Instead this is a separate colourless pass that StencilFringeShadow.cs
// replays at CameraEvent.BeforeForwardAlpha: by then the depth buffer already contains
// the face, so ZTest LEqual selects exactly the face pixels that survived the opaque
// pass. Nothing about the character's own shading is touched.
Shader "Hidden/Anime/FaceStencilWriter"
{
    Properties
    {
        // Must match _StencilRef on HairShadowStencil.shader.
        _StencilRef ("Stencil Ref", Float) = 128
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Name "FaceStencilWriter"

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            ColorMask 0
            ZTest LEqual
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
    FallBack Off
}
