Shader "UI/CinematicCurtain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,1)
        _Progress ("Covered Progress", Range(0,1)) = 0
        _Pattern ("Transition Pattern", Float) = 0
        _Direction ("Wipe Direction", Float) = 0
        _ViewportRect ("Local Container Rect", Vector) = (0,0,1920,1080)
        _Segments ("Blind Count", Float) = 12
        _PixelColumns ("Pixel Columns", Float) = 48
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                float2 maskUv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _ViewportRect;
            float _Progress, _Pattern, _Direction, _Segments, _PixelColumns;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.texcoord = input.texcoord;
                output.maskUv = (input.vertex.xy - _ViewportRect.xy) / max(_ViewportRect.zw, float2(1,1));
                return output;
            }

            float covered(float2 uv)
            {
                if (_Progress <= 0) return 0;
                if (_Progress >= 1) return 1;
                if (_Pattern < 0.5) return _Progress;

                float coordinate = _Direction < 1.5 ? uv.x : uv.y;
                if (_Direction > 0.5 && _Direction < 1.5 || _Direction > 2.5) coordinate = 1 - coordinate;
                if (_Pattern < 1.5) return 1 - step(_Progress, coordinate);
                if (_Pattern < 2.5) return step(0.5 * (1 - _Progress), abs(uv.x - 0.5));
                if (_Pattern < 3.5) return step(0.5 * (1 - _Progress), abs(uv.y - 0.5));
                if (_Pattern < 4.5)
                {
                    float2 size = max(_ViewportRect.zw, float2(1,1));
                    float radius = length(size * 0.5) * (1 - _Progress);
                    return step(radius, length((uv - 0.5) * size));
                }

                if (_Pattern < 5.5) return 1 - step(_Progress, frac(coordinate * max(2, _Segments)));
                float2 grid = float2(_PixelColumns, max(1, _PixelColumns * _ViewportRect.w / max(1, _ViewportRect.z)));
                float2 cell = floor(uv * grid);
                float randomOrder = frac(sin(dot(cell, float2(127.1,311.7))) * 43758.5453);
                return 1 - step(_Progress, randomOrder);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half4 color = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                color.a *= covered(input.maskUv);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
