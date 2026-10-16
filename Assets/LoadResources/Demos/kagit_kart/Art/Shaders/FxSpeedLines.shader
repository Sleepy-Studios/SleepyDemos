Shader "KagitKart/FxSpeedLines" {
 Properties { uInk("墨色",Vector)=(0,0,0,1) uSunDir("太阳方向",Vector)=(0,1,0,0) uSunCol("太阳色",Vector)=(2,2,2,1) uAmb("环境光",Vector)=(1,1,1,1) _FogColor("雾",Vector)=(1,1,1,1) _FogRange("雾范围",Vector)=(170,760,0,0) uColor("速度线",Vector)=(0,0,0,1) uEdge("速度线边",Vector)=(1,1,1,1) uOpacity("不透明度",Float)=0.72 }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float uTime,uIntensity,uAspect,uOpacity;float2 uRes;float4 uInk,uSunDir,uSunCol,uAmb,uColor,uEdge,_FogColor,_FogRange,_CameraRight,_CameraUp,_CameraBack,_CameraPosition;
  
 struct Attributes
 {
  float4 positionOS:POSITION;
  #if SHADER_TARGET >= 45
  uint instanceID:SV_InstanceID;
  #else
  float4 index:TEXCOORD0;
  #endif
 };
 struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1;float depth:TEXCOORD5;};
 float fxMod(float a,float b){return a-b*floor(a/b);}
 float4 SourceView(float3 p){return float4(TransformWorldToView(float3(-p.x,p.y,p.z)),1);}
 







float hash( float n ) { return frac( sin( n * 12.9898 + 4.1414 ) * 43758.5453 ); }









 Varyings Vertex(Attributes input) {Varyings o=(Varyings)0;float3 position=input.positionOS.xyz;
 #if SHADER_TARGET >= 45
 float aIdx=input.instanceID;
 #else
 float aIdx=input.index.x;
 #endif

  float i = aIdx;
  float period = 0.26 + 0.14 * hash( i + 3.1 );
  float t = uTime / period + hash( i + 7.7 );
  float cyc = floor( t );
  float ph = frac( t );
  float ang = ( i + 0.35 + 0.5 * hash( i * 1.7 + cyc * 3.3 ) ) / 34.0 * 6.2831853;
  float2 dir = float2( cos( ang ), sin( ang ) );
  // distance from the centre to the screen border along dir, in aspect-corrected units (x spans ±aspect)
  float border = 1.0 / max( abs( dir.x ) / uAspect, abs( dir.y ) );
  float outer = border * ( 1.04 - 0.3 * ph );
  float len = ( 0.16 + 0.22 * hash( i * 3.3 + cyc ) ) * ( 0.55 + 0.45 * uIntensity );
  float inner = outer - len;
  float2 along = dir * lerp( outer, inner, position.y );
  float w = ( 0.009 + 0.01 * hash( i + cyc * 1.3 ) ) * ( 1.0 - position.y * 0.8 );
  float2 q = along + float2( -dir.y, dir.x ) * position.x * w;
  o.positionCS = float4( q.x / uAspect, q.y, 0.0, 1.0 );
  o.uv = position.xy;
  o.alpha = uIntensity * sin( 3.14159265 * ph ) * ( 0.55 + 0.45 * hash( i * 5.9 + cyc ) );

return o;}
 float4 Fragment(Varyings i,FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target{float4 outputColor;
  float x = abs( i.uv.x );
  float a = ( 1.0 - smoothstep( 0.78, 1.0, x ) ) * i.alpha * uOpacity;
  if ( a < 0.01 ) discard;
  outputColor = float4( lerp( uColor.rgb, uEdge.rgb, smoothstep( 0.4, 0.56, x ) ), a );
  
  

return outputColor;}
 ENDHLSL
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha ZTest Always
  Pass
  {
   HLSLPROGRAM
   #pragma target 4.5
   #pragma multi_compile_instancing
   #pragma vertex Vertex
   #pragma fragment Fragment
   ENDHLSL
  }
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha ZTest Always
  Pass
  {
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vertex
   #pragma fragment Fragment
   ENDHLSL
  }
 }
}
