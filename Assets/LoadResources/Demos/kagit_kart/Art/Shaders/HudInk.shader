Shader "KagitKart/HudInk"
{
    Properties
    {
        [PerRendererData] _MainTex("动态合成墨水", 2D) = "white" {}
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Pass
        {
            Name "InkComposite"
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            Cull Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Blend One OneMinusSrcAlpha
            ColorMask [_ColorMask]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            float4 _ClipRect;
            struct Attributes { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 local : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                output.local = input.vertex.xy;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float4 ink = tex2D(_MainTex, input.uv);
                float3 straight = ink.rgb / max(ink.a, 1e-5);
                #ifndef UNITY_COLORSPACE_GAMMA
                    straight = GammaToLinearSpace(straight);
                #endif
                float alpha = ink.a * input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                    alpha *= UnityGet2DClipping(input.local, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(alpha - 0.001);
                #endif
                return float4(straight * alpha, alpha);
            }
            ENDHLSL
        }
        Pass
        {
            Name "InkFill"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4x4 _InkClipMatrix;
            struct Attributes { float4 vertex : POSITION; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = mul(_InkClipMatrix, input.vertex);
                output.color = input.color;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
