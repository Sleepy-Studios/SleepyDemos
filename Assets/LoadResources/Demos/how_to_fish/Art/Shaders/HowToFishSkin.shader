Shader "HowToFish/SelfAuthoredSkin"
{
    Properties
    {
        _BaseColor("Cooking Tint", Color) = (1,1,1,1)
        _ColorA("Primary", Color) = (1,1,1,1)
        _ColorB("Secondary", Color) = (0,0,0,1)
        _Pattern("Pattern", Float) = 0
        _PatternScale("Pattern Scale", Float) = 16
        _Rainbow("Animated Rainbow", Float) = 0
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        // 所有 Pass 必须共享材质布局，不能借用 Lit 的不同 UnityPerMaterial。
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ColorA, _ColorB;
                float _Pattern, _PatternScale, _Rainbow, _Metallic, _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half fog : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.shadowCoord = GetShadowCoord(position);
                return output;
            }
            float Hash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
            float Noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x),
                                 lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), f.x),
                                 lerp(Hash(cell + float3(0,1,1)), Hash(cell + 1), f.x), f.y), f.z);
            }
            half3 Pattern(float3 p)
            {
                float n = Noise(p), mask = 0;
                if (_Pattern < .5) mask = 0;
                else if (_Pattern < 1.5) mask = .5 + .5 * sin(p.z * 9 + Noise(p * float3(2,2,.2)) * 5);
                else if (_Pattern < 2.5) mask = fmod(abs(floor(p.x) + floor(p.y) + floor(p.z)), 2);
                else if (_Pattern < 3.5) mask = step(.5, frac(p.x + p.y + p.z));
                else if (_Pattern < 4.5) mask = smoothstep(.65, .78, .5 + .5 * sin(p.z * 6 + n * 7));
                else if (_Pattern < 5.5) mask = smoothstep(.47, .53, Noise(p * 1.6));
                else if (_Pattern < 6.5) mask = 1 - smoothstep(.26, .33, length(frac(p) - .5));
                else if (_Pattern < 7.5) mask = floor(n * 4) / 3;
                else if (_Pattern < 8.5) mask = saturate(n * n + step(.995, Hash(floor(p * 12))));
                else if (_Pattern < 9.5) mask = Hash(floor(p * 2));
                else if (_Pattern < 10.5) mask = .5 + .5 * sin(p.z * .45);
                else if (_Pattern < 11.5) mask = smoothstep(.55, .7, Noise(p * 3));
                else if (_Pattern < 12.5) mask = smoothstep(.25, .8, Noise(p * float3(1,2,.25)) + sin(p.z * 2) * .2);
                else mask = step(.55, frac(p.x + floor(p.z) * .37));
                half3 color = lerp(_ColorA.rgb, _ColorB.rgb, mask);
                if (_Rainbow > .5)
                    color *= .5 + .5 * cos(6.283185 * (p.z * .045 + _Time.y * .12 + float3(0,.333,.667)));
                return color * _BaseColor.rgb;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = input.shadowCoord;
                #else
                    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.vertexLighting = VertexLighting(input.positionWS, lighting.normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = Pattern(input.positionOS * _PatternScale);
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, input.fog);
                return color;
            }
            float3 _LightDirection;
            float3 _LightPosition;
            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
            }
            half4 ShadowFrag() : SV_TARGET { return 0; }
            float4 DepthVert(Attributes input) : SV_POSITION { return TransformObjectToHClip(input.positionOS.xyz); }
            half DepthFrag(float4 positionCS : SV_POSITION) : SV_TARGET { return positionCS.z; }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            ENDHLSL
        }
    }
    FallBack Off
}
