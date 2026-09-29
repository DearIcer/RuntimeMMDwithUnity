// Stencil-based fringe (bangs) shadow caster.
//
// Reference: https://zhuanlan.zhihu.com/p/416577141  ("刘海投影·改")
//
// The trick: after the opaque pass the depth buffer already holds the face, and the
// face has stamped its own pixels into the stencil buffer. We redraw the hair a second
// time with its clip-space XY pushed along the screen-space light direction. Two pieces
// of fixed-function state do all the work:
//
//   ZTest LEqual  - hair that sits BEHIND the face fails the depth test, so the back
//                   hair can never smear a shadow across the face. This replaces the
//                   manual depth comparison the older render-texture approach needed.
//   Stencil Equal - only pixels that the face actually owns receive the shadow.
//
// The fragment multiplies into the framebuffer (Blend DstColor Zero) instead of writing
// a flat colour, so the shadow keeps whatever toon shading the face already had and
// simply tints it. That is the "改良" step from the article, without needing to redraw
// the face a third time.
Shader "Hidden/Anime/HairShadowStencil"
{
    Properties
    {
        // Multiply tint applied to the face pixels under the fringe. A warm, slightly
        // pink value keeps the shadow in the same family as the UTS2 1st shade colour.
        _Color ("Shadow Tint (multiply)", Color) = (0.86, 0.72, 0.74, 1)

        // Screen-space displacement per unit of light tilt, in NDC units.
        // NDC spans -1..1 across the viewport, so 0.05 is 2.5% of the screen width.
        // The raw (non-normalised) light XY is used, so a light pointing straight down
        // the camera axis produces no displacement at all instead of an unstable one.
        _Offset ("Screen Offset (NDC)", Float) = 0.05

        // View-space light direction, filled in by StencilFringeShadow.cs each frame.
        _LightDirSS ("Light Direction (View Space)", Vector) = (0, -1, 0, 0)

        // Must match the value FaceStencilWriter.shader stamps onto the face.
        _StencilRef ("Stencil Ref", Float) = 128
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Name "HairShadowStencil"

            Stencil
            {
                Ref [_StencilRef]
                Comp Equal
                Pass Zero
            }

            ZTest LEqual
            ZWrite Off
            Cull Off
            Blend DstColor Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Offset;
            float4 _LightDirSS;

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

                float4 clipPos = UnityObjectToClipPos(input.vertex);

                // _ProjectionParams.x flips the sign when the projection matrix already
                // mirrors Y (D3D-style platforms), matching the article's note.
                float2 lightOffset = _LightDirSS.xy;
                lightOffset.y *= _ProjectionParams.x;

                // Multiplying by w cancels the perspective divide, so _Offset stays a
                // fixed fraction of the screen no matter how far the character is.
                clipPos.xy += lightOffset * _Offset * clipPos.w;

                output.pos = clipPos;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
    FallBack Off
}
