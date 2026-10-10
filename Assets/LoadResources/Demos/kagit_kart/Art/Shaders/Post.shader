Shader "KagitKart/Post"
{
    Properties
    {
        tGrain("原作纸纤维", 2D) = "white" {}
        uInk("线性墨色", Vector) = (0.0262412229, 0.0231533662, 0.0318960331, 1)
        uOutlinePx("720p 整像素线宽", Float) = 1
        uEdgeLo("墨边下阈值", Float) = 0.012
        uEdgeHi("墨边上阈值", Float) = 0.03
        uGrain("纸纤维强度", Float) = 0.05
        uVignette("暗角强度", Float) = 0.16
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "OriginalPaperPrint"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(tGrain);
            SAMPLER(sampler_tGrain);
            float4 uInk;
            float4 uRes;
            float uThick;
            float uPR;
            float uEdgeLo;
            float uEdgeHi;
            float uGrain;
            float uVignette;

            float InvZ(float2 uv)
            {
                // Unity 的 _ZBufferParams 已区分 D3D/Vulkan reversed Z；原相机只使用透视投影。
                return rcp(max(LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams), 1e-3));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 pixel = uThick / uRes.xy;
                float2 mis = float2(pixel.x, -pixel.y);
                float2 paperPixel = input.positionCS.xy;
                #if UNITY_UV_STARTS_AT_TOP
                    paperPixel.y = uRes.y - paperPixel.y;
                #endif
                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + mis).rgb;
                float c = InvZ(uv);
                float l = InvZ(uv - float2(pixel.x, 0));
                float r = InvZ(uv + float2(pixel.x, 0));
                float u = InvZ(uv + float2(0, pixel.y));
                float d = InvZ(uv - float2(0, pixel.y));
                float lap = abs(l + r - 2 * c) + abs(u + d - 2 * c);
                float nearest = max(max(l, r), max(max(u, d), c));
                float rel = lap / nearest;
                float edge = smoothstep(uEdgeLo, uEdgeHi, rel) * (1 - smoothstep(160, 420, rcp(nearest)));
                float g = SAMPLE_TEXTURE2D(tGrain, sampler_tGrain, paperPixel / (256 * uPR)).r;
                float tooth = SAMPLE_TEXTURE2D(tGrain, sampler_tGrain, paperPixel / (1100 * uPR) + 0.37).r;
                col = lerp(col, uInk.rgb, edge * (0.8 + 0.14 * smoothstep(0.86, 0.98, g)));
                col *= 1 - uGrain * ((1 - g) * 3 + (1 - tooth) * 1.6);
                float2 q = uv - 0.5;
                float vig = 1 - smoothstep(0.25, 0.85, length(q * float2(1, 0.8)));
                col *= lerp(1 - uVignette, 1, vig);
                // 原作没有 tone mapping；URP 最终输出负责唯一一次 linear → sRGB。
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
