Shader "KagitKart/FxCards" {
 Properties { uInk("墨色",Vector)=(0,0,0,1) uSunDir("太阳方向",Vector)=(0,1,0,0) uSunCol("太阳色",Vector)=(2,2,2,1) uAmb("环境光",Vector)=(1,1,1,1) _FogColor("雾",Vector)=(1,1,1,1) _FogRange("雾范围",Vector)=(170,760,0,0) uColor("速度线",Vector)=(0,0,0,1) uEdge("速度线边",Vector)=(1,1,1,1) uOpacity("不透明度",Float)=0.72 }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float uTime,uIntensity,uAspect,uOpacity;float2 uRes;float4 uInk,uSunDir,uSunCol,uAmb,uColor,uEdge,_FogColor,_FogRange,_CameraRight,_CameraUp,_CameraBack,_CameraPosition;
 struct FxState { float4 A,B,Color,Variant; };
 #if SHADER_TARGET >= 45
 StructuredBuffer<FxState> _Fx;
 #endif
 struct Attributes
 {
  float4 positionOS:POSITION;
  #if SHADER_TARGET >= 45
  uint instanceID:SV_InstanceID;
  #else
  float4 a:TEXCOORD0;
  float4 b:TEXCOORD1;
  float4 color:TEXCOORD2;
  float4 variant:TEXCOORD3;
  #endif
 };
 FxState ReadFxState(Attributes input)
 {
  #if SHADER_TARGET >= 45
  return _Fx[input.instanceID];
  #else
  FxState state;
  state.A=input.a;
  state.B=input.b;
  state.Color=input.color;
  state.Variant=input.variant;
  return state;
  #endif
 }
 struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:TEXCOORD1;float4 variant:TEXCOORD2;float shape:TEXCOORD3;float depth:TEXCOORD5;};
 float fxMod(float a,float b){return a-b*floor(a/b);}
 float4 SourceView(float3 p){return float4(TransformWorldToView(float3(-p.x,p.y,p.z)),1);}
 



















float sdUnevenCapsule( float2 p, float2 pa, float2 pb, float ra, float rb ) {
  p -= pa;
  pb -= pa;
  float h = dot( pb, pb );
  float2 q = float2( dot( p, float2( pb.y, -pb.x ) ), dot( p, pb ) ) / h;
  q.x = abs( q.x );
  float b = ra - rb;
  float2 c = float2( sqrt( h - b * b ), b );
  float k = c.x * q.y - c.y * q.x;
  float m = dot( c, q );
  float n = dot( q, q );
  if ( k < 0.0 ) return sqrt( h * n ) - ra;
  else if ( k > c.x ) return sqrt( h * ( n + 1.0 - 2.0 * q.y ) ) - rb;
  return m - ra;
}

// A flame is a fat main tongue plus two side licks; the variant moves the tips, so swapping the
// variant reads as a cut-paper flame being replaced, not as a blur.
float sdFlame( float2 p, float variant ) {
  float v = floor( variant + 0.5 );
  float2 q = float2( p.x * 0.5, p.y );
  float lean = ( v == 0.0 ? 0.05 : v == 1.0 ? -0.07 : v == 2.0 ? 0.1 : -0.03 );
  float l1y = ( v == 0.0 ? 0.62 : v == 1.0 ? 0.74 : v == 2.0 ? 0.56 : 0.68 );
  float l2y = ( v == 0.0 ? 0.7 : v == 1.0 ? 0.55 : v == 2.0 ? 0.66 : 0.78 );
  float d = sdUnevenCapsule( q, float2( 0.0, 0.2 ), float2( lean, 0.99 ), 0.2, 0.0 );
  d = min( d, sdUnevenCapsule( q, float2( -0.12, 0.26 ), float2( -0.31 + lean * 0.5, l1y ), 0.13, 0.0 ) );
  d = min( d, sdUnevenCapsule( q, float2( 0.12, 0.28 ), float2( 0.29 + lean * 0.5, l2y ), 0.12, 0.0 ) );
  return d;
}

// End-on flame: six tongues around the nozzle; the variant turns them and changes which is longest.
float sdBurst( float2 p, float variant ) {
  float v = floor( variant + 0.5 );
  float r = length( p );
  float s = atan2( p.y, p.x ) / 6.2831853 * 6.0 + v * 0.29;
  float tongue = floor( s );
  float tri = abs( frac( s ) - 0.5 ) * 2.0;
  float tip = 0.78 + 0.2 * frac( sin( ( tongue + v * 3.0 ) * 12.9898 ) * 43758.5453 );
  float edge = 0.42 + ( tip - 0.42 ) * pow( 1.0 - tri, 1.7 );
  return ( r - edge ) * 0.8;
}


 Varyings Vertex(Attributes input) {Varyings o=(Varyings)0;float3 position=input.positionOS.xyz;FxState itemState=ReadFxState(input); float4 aA=itemState.A,aB=itemState.B,aCol=itemState.Color,aVar=itemState.Variant;

  float3 A = aA.xyz;
  float3 B = aB.xyz;
  if ( aB.w > 1.5 ) {
    float3 camR = _CameraRight.xyz;
    float3 camU = _CameraUp.xyz;
    float2 q = float2( position.x, position.y * 2.0 - 1.0 );
    float4 mvB = SourceView( A + ( camR * q.x + camU * q.y ) * aA.w);
    o.positionCS = mul(UNITY_MATRIX_P,mvB);
    o.positionCS.xy += float2( 1.0, -1.0 ) * aVar.w * 2.0 / uRes * o.positionCS.w;
    o.uv = q;
    o.color = aCol;
    o.variant = aVar;
    o.shape = aB.w;
    float4 mvPosition = mvB; o.depth=-mvPosition.z;
    
    return o;
  }
  float3 axis = B - A;
  float len = length( axis );
  float3 dir = len > 1e-5 ? axis / len : float3( 0.0, 1.0, 0.0 );
  float3 mid = lerp( A, B, position.y );
  float3 toCam = _CameraPosition.xyz - mid;
  float dist = length( toCam );
  float3 side = cross( dir, toCam / max( dist, 1e-4 ) );
  side = dot( side, side ) > 1e-8 ? normalize( side ) : float3( 1.0, 0.0, 0.0 );
  // never thinner than ~1.2 px so a string stays a line at any distance
  float pxWorld = 2.0 * dist / ( uRes.y * abs(UNITY_MATRIX_P._m11) );
  float w = max( aA.w, pxWorld * 1.2 );
  float4 mvPosition = SourceView( mid + side * position.x * w);
  o.positionCS = mul(UNITY_MATRIX_P,mvPosition);
  o.positionCS.xy += float2( 1.0, -1.0 ) * aVar.w * 2.0 / uRes * o.positionCS.w;
  o.uv = position.xy;
  o.color = aCol;
  o.variant = aVar;
  o.shape = aB.w;
  

o.depth=-mvPosition.z;return o;}
 float4 Fragment(Varyings i,FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target{float4 outputColor;
  float d;
  if ( i.shape < 0.5 ) d = sdFlame( i.uv, i.variant.x );
  else if ( i.shape < 1.5 ) d = abs( i.uv.x ) - 0.85;
  else d = sdBurst( i.uv, i.variant.x );
  float w = max( fwidth( d ), 1e-4 );
  float alpha = ( 1.0 - smoothstep( -w * 0.5, w * 0.5, d ) ) * i.variant.z;
  if ( alpha < 0.02 ) discard;
  float3 col = i.color.rgb;
  float lineW = 1.4 * w;
  float edge = smoothstep( -lineW - w * 0.5, -lineW + w * 0.5, d ) * i.variant.y * smoothstep( 0.25, 0.08, w );
  col = lerp( col, uInk.rgb, edge * 0.9 );
  outputColor = float4( col, alpha );
  
  
  

outputColor.rgb=lerp(outputColor.rgb,_FogColor.rgb,smoothstep(_FogRange.x,_FogRange.y,i.depth));return outputColor;}
 ENDHLSL
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
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
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
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
