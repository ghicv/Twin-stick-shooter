// Old-TV (CRT) look over the whole screen: curved glass, color fringing, scanlines, RGB phosphor columns,
// darker corners. Used by the "CRT Screen" Full Screen Pass renderer feature
// (material Assets/Materials/CRTScreen.mat). All values are global and set every frame by CRTScreen.cs,
// so nothing on the material asset changes at runtime. _CRT_Intensity 0 = the picture is left untouched.
Shader "Hidden/CRTScreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "CRTScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _CRT_Intensity;
            float _CRT_Curvature;
            float _CRT_ScanlineCount;
            float _CRT_ScanlineDarkness;
            float _CRT_MaskStrength;
            float _CRT_ColorFringe;
            float _CRT_Vignette;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float k = saturate(_CRT_Intensity);
                if (k <= 0.0)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Curved glass: the picture bulges toward the corners; outside the tube is black.
                float2 centered = uv * 2.0 - 1.0;
                centered *= 1.0 + _CRT_Curvature * k * dot(centered, centered);
                float2 screenUV = centered * 0.5 + 0.5;
                if (screenUV.x < 0.0 || screenUV.x > 1.0 || screenUV.y < 0.0 || screenUV.y > 1.0)
                    return float4(0.0, 0.0, 0.0, 1.0);

                // Color fringing: red and blue are sampled slightly to the sides.
                float fringe = _CRT_ColorFringe * k;
                float4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV);
                float red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV + float2(fringe, 0.0)).r;
                float blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV - float2(fringe, 0.0)).b;
                float3 color = float3(red, center.g, blue);

                // Scanlines: bright lines with dark gaps between them.
                float scan = 0.5 + 0.5 * cos(screenUV.y * _CRT_ScanlineCount * 2.0 * PI);
                color *= 1.0 - _CRT_ScanlineDarkness * k * (1.0 - scan);

                // Phosphor mask: screen pixel columns alternate red, green, blue.
                uint column = (uint)input.positionCS.x % 3u;
                float3 phosphor = column == 0u ? float3(1.0, 0.0, 0.0) : (column == 1u ? float3(0.0, 1.0, 0.0) : float3(0.0, 0.0, 1.0));
                float3 mask = lerp(1.0 - _CRT_MaskStrength, 1.0 + _CRT_MaskStrength * 0.25, phosphor);
                color *= lerp(float3(1.0, 1.0, 1.0), mask, k);

                // Darker corners
                color *= saturate(1.0 - _CRT_Vignette * k * dot(centered, centered) * 0.5);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
