Shader "UI/ImageExtend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        _Radius ("Corner Radius", Vector) = (0,0,0,0)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Width / Sprite Alpha", Vector) = (0,0,0,0)
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0)
        _Shadow ("Offset / Softness / Enabled", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            WriteMask [_StencilWriteMask]
            ReadMask [_StencilReadMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "Default"
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
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 local : TEXCOORD1;
                float4 bounds : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 local : TEXCOORD1;
                float4 bounds : TEXCOORD2;
                float4 mask : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half4 _TextureSampleAdd;
            fixed4 _Color;
            float4 _ClipRect, _Radius, _Outline, _Shadow;
            fixed4 _OutlineColor, _ShadowColor;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                output.local = input.local;
                output.bounds = input.bounds;
                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                return output;
            }

            float DistanceToPanel(float2 position, float2 size)
            {
                float2 radii = position.x > 0 ? _Radius.yz : _Radius.xw;
                float radius = min(position.y > 0 ? radii.x : radii.y, min(size.x, size.y) * 0.5);
                float2 distance = abs(position) - size * 0.5 + radius;
                return min(max(distance.x, distance.y), 0) + length(max(distance, 0)) - radius;
            }

            half Coverage(float distance)
            {
                return saturate(0.5 - distance / max(fwidth(distance), 0.0001));
            }

            half4 SampleSprite(float2 uv, float4 bounds)
            {
                float2 halfTexel = _MainTex_TexelSize.xy * 0.5;
                float2 safeUV = clamp(uv, bounds.xy + halfTexel, bounds.zw - halfTexel);
                half4 color = tex2D(_MainTex, safeUV) + _TextureSampleAdd;
                float2 coverage = saturate(min(uv - bounds.xy, bounds.zw - uv) * _MainTex_TexelSize.zw + 0.5);
                color.a *= coverage.x * coverage.y;
                return color;
            }

            half SpriteAlpha(float2 uv, v2f input, float2 uvPerUnit, float2 offset)
            {
                float2 local = input.local.xy - offset;
                return SampleSprite(uv, input.bounds).a * Coverage(DistanceToPanel(local, input.local.zw));
            }

            half ExpandedAlpha(float2 uv, v2f input, float2 uvPerUnit, float width)
            {
                float2 stepUV = uvPerUnit * width;
                half alpha = SpriteAlpha(uv, input, uvPerUnit, 0);
                alpha = max(alpha, SpriteAlpha(uv + float2(stepUV.x, 0), input, uvPerUnit, float2(-width, 0)));
                alpha = max(alpha, SpriteAlpha(uv - float2(stepUV.x, 0), input, uvPerUnit, float2(width, 0)));
                alpha = max(alpha, SpriteAlpha(uv + float2(0, stepUV.y), input, uvPerUnit, float2(0, -width)));
                alpha = max(alpha, SpriteAlpha(uv - float2(0, stepUV.y), input, uvPerUnit, float2(0, width)));
                float2 diagonal = float2(width, width) * 0.70710678;
                alpha = max(alpha, SpriteAlpha(uv + diagonal * uvPerUnit, input, uvPerUnit, -diagonal));
                alpha = max(alpha, SpriteAlpha(uv - diagonal * uvPerUnit, input, uvPerUnit, diagonal));
                alpha = max(alpha, SpriteAlpha(uv + diagonal * float2(1,-1) * uvPerUnit, input, uvPerUnit, diagonal * float2(-1,1)));
                alpha = max(alpha, SpriteAlpha(uv + diagonal * float2(-1,1) * uvPerUnit, input, uvPerUnit, diagonal * float2(1,-1)));
                return alpha;
            }

            half4 frag(v2f input) : SV_Target
            {
                float distance = DistanceToPanel(input.local.xy, input.local.zw);
                half panel = Coverage(distance);
                half4 color = 0;
                if (input.uv.z < 0.5)
                {
                    half4 sprite = (tex2D(_MainTex, input.uv.xy) + _TextureSampleAdd) * input.color;
                    sprite.a *= panel;
                    color = half4(sprite.rgb * sprite.a, sprite.a);
                }
                else
                {
                    half outline = 0, shadow = 0, sourceAlpha = 0;
                    half3 sourceRGB = 0;
                    if (input.uv.z > 1.5)
                    {
                        float2 uvPerUnit = (input.bounds.zw - input.bounds.xy) / input.local.zw;
                        half4 sprite = SampleSprite(input.uv.xy, input.bounds);
                        sourceAlpha = sprite.a * panel;
                        sourceRGB = sprite.rgb * input.color.rgb;
                        if (_Outline.x > 0)
                        {
                            outline = ExpandedAlpha(input.uv.xy, input, uvPerUnit, _Outline.x) * (1 - sourceAlpha);
                        }
                        if (_Shadow.w > 0)
                        {
                            float2 uv = input.uv.xy - _Shadow.xy * uvPerUnit;
                            v2f shifted = input;
                            shifted.local.xy -= _Shadow.xy;
                            float2 stepUV = uvPerUnit * _Shadow.z * 0.5;
                            // shortcut: 九次固定采样柔化，只用于细小轮廓阴影；大范围模糊使用预烘焙阴影图。
                            shadow = SpriteAlpha(uv, shifted, uvPerUnit, 0) * 4;
                            shadow += SpriteAlpha(uv + float2(stepUV.x,0), shifted, uvPerUnit, float2(-_Shadow.z * 0.5,0));
                            shadow += SpriteAlpha(uv - float2(stepUV.x,0), shifted, uvPerUnit, float2(_Shadow.z * 0.5,0));
                            shadow += SpriteAlpha(uv + float2(0,stepUV.y), shifted, uvPerUnit, float2(0,-_Shadow.z * 0.5));
                            shadow += SpriteAlpha(uv - float2(0,stepUV.y), shifted, uvPerUnit, float2(0,_Shadow.z * 0.5));
                            shadow += SpriteAlpha(uv + stepUV, shifted, uvPerUnit, -_Shadow.zz * 0.5);
                            shadow += SpriteAlpha(uv - stepUV, shifted, uvPerUnit, _Shadow.zz * 0.5);
                            shadow += SpriteAlpha(uv + stepUV * float2(1,-1), shifted, uvPerUnit, _Shadow.zz * float2(-0.5,0.5));
                            shadow += SpriteAlpha(uv + stepUV * float2(-1,1), shifted, uvPerUnit, _Shadow.zz * float2(0.5,-0.5));
                            shadow /= 12;
                        }
                    }
                    else
                    {
                        if (_Outline.x > 0)
                        {
                            outline = Coverage(distance - _Outline.x) * (1 - panel);
                        }
                        if (_Shadow.w > 0)
                        {
                            float shadowDistance = DistanceToPanel(input.local.xy - _Shadow.xy, input.local.zw);
                            shadow = _Shadow.z > 0 ? saturate(0.5 - shadowDistance / _Shadow.z) : Coverage(shadowDistance);
                            shadow *= 1 - panel;
                        }
                    }
                    outline *= _OutlineColor.a;
                    shadow *= _ShadowColor.a * saturate(1 - sourceAlpha - outline);
                    color.rgb = sourceRGB * sourceAlpha + _OutlineColor.rgb * outline + _ShadowColor.rgb * shadow;
                    color.a = sourceAlpha + outline + shadow;
                    color *= input.color.a;
                }
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color *= mask.x * mask.y;
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
