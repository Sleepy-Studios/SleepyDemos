Shader "HowToFish/CoastalWater"
{
    Properties
    {
        _ShallowColor ("Shallow Water", Color) = (0.12, 0.43, 0.49, 0.9)
        _DeepColor ("Deep Water", Color) = (0.025, 0.16, 0.27, 0.97)
        _FoamColor ("Shore Foam", Color) = (0.78, 0.87, 0.84, 1)
        _ReflectionColor ("Sky Reflection", Color) = (0.55, 0.68, 0.76, 1)
        _WaveScale ("Wave Scale", Range(0.05, 2)) = 0.4
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.85
        _NormalStrength ("Ripple Strength", Range(0, 0.5)) = 0.12
        _FoamWidth ("Shore Foam Depth", Range(0.05, 2)) = 0.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "CoastalWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor, _ReflectionColor;
                float _WaveScale, _WaveSpeed, _NormalStrength, _FoamWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
                half fog : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.eyeDepth = -position.positionVS.z;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float depth = max(0, LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams) - input.eyeDepth);
                float2 p = input.positionWS.xz * _WaveScale;
                float t = _Time.y * _WaveSpeed;
                float2 ripple = float2(sin(p.x + t) + .5 * sin(p.y * 1.7 - t),
                    cos(p.y + t * .73) + .5 * cos(p.x * 1.4 + t));
                half3 normal = normalize(half3(-ripple.x * _NormalStrength, 1, -ripple.y * _NormalStrength));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();
                half fresnel = pow(1 - saturate(dot(normal, view)), 3);
                half4 water = lerp(_ShallowColor, _DeepColor, saturate(depth * .12));
                half3 color = water.rgb * (.65 + .35 * saturate(dot(normal, sun.direction)));
                color = lerp(color, _ReflectionColor.rgb, fresnel * .45);
                color += sun.color * pow(saturate(dot(normal, normalize(sun.direction + view))), 96) * .5;
                half foam = saturate(1 - depth / max(.05, _FoamWidth));
                foam *= .55 + .45 * sin(p.x * 2.5 + p.y * 1.8 - t * 2);
                color = lerp(color, _FoamColor.rgb, foam);
                return half4(MixFog(color, input.fog), lerp(water.a, _FoamColor.a, foam));
            }
            ENDHLSL
        }
    }
}
