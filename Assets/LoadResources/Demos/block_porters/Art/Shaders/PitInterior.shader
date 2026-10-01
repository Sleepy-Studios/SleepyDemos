Shader "SleepyDemos/BlockPorters/PitInterior"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.1, 0.08, 0.06, 1)
        _PitCenter("Aperture center / radius", Vector) = (0, 0, -1.5, 0.43)
        _ViewRay("View direction", Vector) = (0, -0.819, 0.574, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _PitCenter;
                float4 _ViewRay;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // 后景贴图不承担原地面遮挡；沿正交视线投回坑沿平面，只显示孔内深度。
                float rayY = abs(_ViewRay.y) > 0.0001 ? _ViewRay.y : -0.0001;
                float2 surface = input.positionWS.xz - _ViewRay.xz * ((input.positionWS.y - _PitCenter.y) / rayY);
                float2 delta = surface - _PitCenter.xz;
                clip(_PitCenter.w * _PitCenter.w - dot(delta, delta));
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
