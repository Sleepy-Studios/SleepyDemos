Shader "KagitKart/PaperSky"
{
    Properties
    {
        _Top("天空上色（线性）", Vector) = (0.3,0.6,0.9,1)
        _Bottom("天空下色（线性）", Vector) = (1,0.9,0.7,1)
        _Grain("纸纹", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Grain); SAMPLER(sampler_Grain);
            CBUFFER_START(UnityPerMaterial)
            float4 _Top, _Bottom;
            CBUFFER_END
            struct Attributes { float4 position : POSITION; };
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.direction = float3(-input.position.x, input.position.y, input.position.z);
                return output;
            }
            float4 Fragment(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float az = atan2(d.z, d.x) / 6.2831853 + 0.5;
                float h = saturate(d.y * 1.4 + 0.02);
                float t = smoothstep(0, 1, sqrt(h));
                float wob = 0.02*sin(az*18.85+1.3) + 0.013*sin(az*43.98+0.4) + 0.007*sin(az*106.8+2.1);
                float tb = clamp(t+wob*(1-t), 0, 0.9999) * 7;
                float bi = floor(tb), bf = frac(tb);
                float up = smoothstep(1-max(fwidth(tb),1e-4), 1, bf);
                float tq = (bi+up+0.25+0.5*bf*(1-up))/7;
                float3 col = lerp(_Bottom.rgb, _Top.rgb, saturate(tq));
                col *= 1-0.05*(1-smoothstep(0,0.07,bf))*step(0.5,bi);
                float streak = SAMPLE_TEXTURE2D_LOD(_Grain,sampler_Grain,float2(az*5,d.y*26),0).r;
                float fibre = SAMPLE_TEXTURE2D_LOD(_Grain,sampler_Grain,float2(az*24,d.y*20),0).r;
                col *= 1+(streak-0.92)*0.5+(fibre-0.92)*0.35;
                return float4(col,1);
            }
            ENDHLSL
        }
    }
}
