Shader "BlockPorters/Tabletop Background"
{
    Properties { _BaseMap("Background", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Background" }
        Pass
        {
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 background = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, saturate(uv));
                // 长屏余量平滑过渡至无角落装饰的边缘底色，不拉长整幅画和棋盘框。
                float outside = max(max(-uv.y, uv.y - 1), max(-uv.x, uv.x - 1));
                float edgeY = uv.y > .5 ? .98 : .02;
                half4 plain = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(.5, edgeY));
                return lerp(background, plain, smoothstep(0, .045, outside));
            }
            ENDHLSL
        }
    }
}
