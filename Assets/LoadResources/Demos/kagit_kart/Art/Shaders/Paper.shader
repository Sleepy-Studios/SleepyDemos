Shader "KagitKart/Paper"
{
 Properties
 {
  _BaseColor("纸色（线性）", Vector) = (1,1,1,1)
  _InstanceColor("实例色", Vector) = (1,1,1,1)
  _EmissionColor("角色发光（线性）", Vector) = (0,0,0,0)
  _ReceiveShadows("接收阴影", Float) = 1
  _AmbientColor("环境光（线性）", Vector) = (0.3,0.3,0.3,1)
  _FogColor("雾色（线性）", Vector) = (1,1,1,1)
  _FogRange("雾距离", Vector) = (170,760,0,0)
  _Gradient("三阶色调", Vector) = (0.5,0.8,1,0)
  uInk("uInk", Vector) = (0,0,0,0)
  uScreen("uScreen", Vector) = (0,0,0,0)
  uOverlayColor("uOverlayColor", Vector) = (0,0,0,0)
  uOverlayRepeat("uOverlayRepeat", Vector) = (0,0,0,0)
  uPlateR("uPlateR", Vector) = (0,0,0,0)
  uPlateG("uPlateG", Vector) = (0,0,0,0)
  uPlateB("uPlateB", Vector) = (0,0,0,0)
  uPlateOn("uPlateOn", Vector) = (0,0,0,0)
  uKkC0("uKkC0", Vector) = (0,0,0,0)
  uKkC1("uKkC1", Vector) = (0,0,0,0)
  uKkC2("uKkC2", Vector) = (0,0,0,0)
  uKkC3("uKkC3", Vector) = (0,0,0,0)
  uKkC4("uKkC4", Vector) = (0,0,0,0)
  uKkC5("uKkC5", Vector) = (0,0,0,0)
  uKkV0("uKkV0", Vector) = (0,0,0,0)
  uKkV1("uKkV1", Vector) = (0,0,0,0)
  uKkV2("uKkV2", Vector) = (0,0,0,0)
  _BaseMap("_BaseMap", 2D) = "white" {}
  uGrainTex("uGrainTex", 2D) = "white" {}
  uOverlay("uOverlay", 2D) = "white" {}
  uPlates("uPlates", 2D) = "white" {}
  uKkTex0("uKkTex0", 2D) = "white" {}
  uKkTex1("uKkTex1", 2D) = "white" {}
  uDotPx("uDotPx", Float) = 0
  uTime("uTime", Float) = 0
  uGrainAmount("uGrainAmount", Float) = 0
  uScroll("uScroll", Float) = 0
  _UseMap("_UseMap", Float) = 0
  _UseVertexColor("_UseVertexColor", Float) = 0
  _UseHalftone("_UseHalftone", Float) = 0
  _UseScreen("_UseScreen", Float) = 0
  _UseOverlay("_UseOverlay", Float) = 0
  _UsePlates("_UsePlates", Float) = 0
  _Unlit("_Unlit", Float) = 0
  _Flat("_Flat", Float) = 0
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("剔除", Float) = 2
  [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("混合源", Float) = 1
  [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("混合目标", Float) = 0
  _ZWrite("深度写入", Float) = 1
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Cull [_Cull] ZWrite [_ZWrite] Blend [_SrcBlend] [_DstBlend]
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(uGrainTex); SAMPLER(sampleruGrainTex);
  TEXTURE2D(uOverlay); SAMPLER(sampleruOverlay);
  TEXTURE2D(uPlates); SAMPLER(sampleruPlates);
  TEXTURE2D(uKkTex0); SAMPLER(sampleruKkTex0);
  TEXTURE2D(uKkTex1); SAMPLER(sampleruKkTex1);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseColor, _BaseMap_ST, _AmbientColor, _FogColor, _FogRange, _Gradient, _EmissionColor;
  float _ReceiveShadows;
  float4 uInk;
  float4 uScreen;
  float4 uOverlayColor;
  float4 uOverlayRepeat;
  float4 uPlateR;
  float4 uPlateG;
  float4 uPlateB;
  float4 uPlateOn;
  float4 uKkC0;
  float4 uKkC1;
  float4 uKkC2;
  float4 uKkC3;
  float4 uKkC4;
  float4 uKkC5;
  float4 uKkV0;
  float4 uKkV1;
  float4 uKkV2;
  float uDotPx;
  float uTime;
  float uGrainAmount;
  float uScroll;
  float _UseMap;
  float _UseVertexColor;
  float _UseHalftone;
  float _UseScreen;
  float _UseOverlay;
  float _UsePlates;
  float _Unlit;
  float _Flat;
  CBUFFER_END
  UNITY_INSTANCING_BUFFER_START(PaperInstances)
   UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
  UNITY_INSTANCING_BUFFER_END(PaperInstances)
  struct Attributes
  {
   float4 positionOS : POSITION; float3 normalOS : NORMAL;
   float2 uv : TEXCOORD0; float4 a : TEXCOORD1; float4 b : TEXCOORD2; float4 color : COLOR;
   UNITY_VERTEX_INPUT_INSTANCE_ID
  };
  struct Varyings
  {
   float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normalWS : TEXCOORD1;
   float2 uv : TEXCOORD2; float4 a : TEXCOORD3; float4 b : TEXCOORD4; float4 color : TEXCOORD5;
   UNITY_VERTEX_INPUT_INSTANCE_ID
   UNITY_VERTEX_OUTPUT_STEREO
  };
  Varyings PaperVertex(Attributes input)
  {
   Varyings o = (Varyings)0;
   UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
   o.world = TransformObjectToWorld(input.positionOS.xyz);
   o.positionCS = TransformWorldToHClip(o.world);
   o.normalWS = TransformObjectToWorldNormal(input.normalOS);
   o.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
   o.a = input.a; o.b = input.b;
   o.color = lerp(float4(1,1,1,1), input.color, _UseVertexColor) * UNITY_ACCESS_INSTANCED_PROP(PaperInstances,_InstanceColor);
   return o;
  }
  ENDHLSL
  Pass
  {
   Name "PaperForward" Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex PaperVertex
   #pragma fragment PaperFragment
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma shader_feature_local _ KK_ROAD KK_BAND KK_PAGE KK_FOIL KK_WAVE KK_HOLE
   #if defined(KK_ROAD) || defined(KK_BAND) || defined(KK_PAGE) || defined(KK_FOIL) || defined(KK_WAVE) || defined(KK_HOLE)
   #define KK_KIND
   #endif
   float kkMod(float x, float y) { return x-y*floor(x/y); }
   
float kkHash( float2 p ) {
  p = frac( p * float2( 123.34, 456.21 ) );
  p += dot( p, p + 45.32 );
  return frac( p.x * p.y );
}
float kkNoise( float2 p ) {
  float2 i = floor( p ), f = frac( p );
  f = f * f * ( 3.0 - 2.0 * f );
  float a = kkHash( i ), b = kkHash( i + float2( 1.0, 0.0 ) ), c = kkHash( i + float2( 0.0, 1.0 ) ), d = kkHash( i + float2( 1.0, 1.0 ) );
  return lerp( lerp( a, b, f.x ), lerp( c, d, f.x ), f.y );
}
// Pixel-antialiased coverage of x inside [a, b].
float kkBand( float x, float a, float b ) {
  float w = max( fwidth( x ), 1e-4 );
  return clamp( min( x - a, b - x ) / w + 0.5, 0.0, 1.0 );
}
// Amplitude-modulated print screen at 45 deg: ink coverage for tone 0..1 with cells of 'cell' metres.
// Above 50 % it flips to paper dots on ink; below ~3 px per cell it settles to the flat tone.
float kkScreen( float2 p, float cell, float tone ) {
  float2 sp = p / cell;
  sp = float2( sp.x + sp.y, sp.y - sp.x ) * 0.70710678;
  float d = length( frac( sp ) - 0.5 );
  float inv = step( 0.5, tone );
  float r = sqrt( lerp( tone, 1.0 - tone, inv ) / 3.14159 );
  float w = max( fwidth( d ), 1e-4 );
  float m = 1.0 - smoothstep( r - w, r + w, d );
  m = lerp( m, 1.0 - m, inv );
  return lerp( m, tone, smoothstep( 0.18, 0.45, length( fwidth( sp ) ) ) );
}
// The same screen as printed by a worn press: each dot's size and centre wander a little (ink spread).
float kkScreenRough( float2 p, float cell, float tone ) {
  float2 sp = p / cell;
  sp = float2( sp.x + sp.y, sp.y - sp.x ) * 0.70710678;
  float2 ci = floor( sp );
  float2 f = frac( sp ) - 0.5 - ( float2( kkHash( ci + 7.1 ), kkHash( ci + 3.3 ) ) - 0.5 ) * 0.14;
  float d = length( f );
  float r = sqrt( min( tone, 0.5 ) / 3.14159 ) * ( 0.82 + 0.34 * kkHash( ci ) );
  float w = max( fwidth( d ), 1e-4 );
  float m = 1.0 - smoothstep( r - w, r + w, d );
  // settles to flat tone a little earlier than kkScreen: the road scrolls fast under the camera
  return lerp( m, tone, smoothstep( 0.14, 0.32, length( fwidth( sp ) ) ) );
}
// An engraver's printedLine screen across x (metres): lines every period covering tone of the width;
// below ~4 px between lines it settles to the flat tone. Read along the lines it never forms the
// ellipses a dot screen does at grazing angles.
float kkLines( float x, float period, float tone ) {
  float q = x / period;
  float f = abs( frac( q ) - 0.5 ) * 2.0;
  float w = max( fwidth( q ), 1e-4 ) * 2.0;
  float tt = tone * ( 1.0 + 2.0 * w );
  float m = 1.0 - smoothstep( tt - w, tt + w, f );
  return lerp( m, tone, smoothstep( 0.18, 0.4, fwidth( q ) ) );
}
float kkBox( float2 p, float2 b, float r ) {
  float2 q = abs( p ) - b + r;
  return length( max( q, 0.0 ) ) + min( max( q.x, q.y ), 0.0 ) - r;
}

   
#ifdef KK_KIND
float kkGutterT( float2 wp ) {
  if ( uKkV0.w <= 0.0 ) return 0.0;
  float g = 1.0 - smoothstep( 0.0, uKkV0.w, abs( dot( wp - uKkV1.xy, uKkV1.zw ) ) );
  return g * g;
}
float3 kkGutterFold( float2 wp, float3 col ) {
  if ( uKkV0.w <= 0.0 ) return col;
  float gx = abs( dot( wp - uKkV1.xy, uKkV1.zw ) );
  col = lerp( col, col * 1.07, kkBand( gx, 0.3, 0.7 ) * 0.8 );
  return lerp( col, uInk.rgb, kkBand( gx, -1.0, 0.1 ) * 0.8 );
}
float3 kkPagePrint( float2 wp, float3 paper ) {
  float3 col = paper;
  float b = kkNoise( wp * 0.011 ) * 0.65 + kkNoise( wp * 0.037 + 5.0 ) * 0.35;
  col = lerp( col, lerp( paper, uInk.rgb, 0.14 ), kkScreen( wp, 0.9, kkBand( b, 0.6, 9.0 ) * 0.22 ) );
  float hm = SAMPLE_TEXTURE2D(uKkTex0, sampleruKkTex0, wp / 6.0 ).r * kkBand( kkNoise( wp * 0.045 + 11.0 ), 0.64, 9.0 );
  col = lerp( col, lerp( paper, uInk.rgb, 0.3 ), hm * 0.45 );
  col = lerp( col, uInk.rgb, kkScreen( wp, 0.75, kkGutterT( wp ) * 0.7 ) * 0.55 );
  return kkGutterFold( wp, col );
}
#endif

   struct FragmentOutput { float4 color : SV_Target; float depth : SV_Depth; };
   FragmentOutput PaperFragment(Varyings i, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC)
   {
    UNITY_SETUP_INSTANCE_ID(i);
    float3 vPaperWorld = float3(-i.world.x,i.world.y,i.world.z);
    float3 cameraPosition = float3(-_WorldSpaceCameraPos.x,_WorldSpaceCameraPos.y,_WorldSpaceCameraPos.z);
    float4 vKkA = i.a, vKkB = i.b;
    float4 diffuseColor = _BaseColor;
    if (_UseMap > 0.5) diffuseColor *= SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
    float3 kkGlow = 0;
    float depth = i.positionCS.z;
    
#ifdef KK_ROAD
{
  float lat = vKkA.x, dist = vKkA.y, hw = vKkA.z, curv = vKkA.w;
  float side = lat < 0.0 ? -1.0 : 1.0;
  float e = hw - abs( lat );
  float2 wp = vPaperWorld.xz;
  bool notebook = uKkV0.y > 0.5;
  // pen-pressure wobble shared by the edge lines
  float wob = 0.045 * sin( dist * 0.11 + side * 1.7 ) + 0.025 * sin( dist * 0.47 + side ) + 0.02 * kkNoise( float2( dist * 0.8, side * 5.0 ) );
  // One flat layer of wash dragged along the strip by a wide brush (long faint streaks, a slow bloom), a
  // little deeper where it pooled against the edge. No second glaze: its blotches read as wet asphalt.
  // Near the camera the paper itself shows through: its fibres (Tex0: R took more ink, G stayed light) and
  // the cloudy formation of the sheet; both fade out before they could shimmer in the distance.
  float nearCoverage = 1.0 - smoothstep( 0.015, 0.06, length( fwidth( wp ) ) );
  float drag = kkNoise( float2( lat * 1.1, dist * 0.045 ) ) - 0.5;
  float bloom = kkNoise( wp * 0.03 + 7.0 ) * 0.5 + kkNoise( wp * 0.21 + 2.0 ) * 0.5 - 0.5;
  float cloud = ( kkNoise( wp * 1.2 ) - 0.5 ) * nearCoverage;
  float2 fib = SAMPLE_TEXTURE2D(uKkTex0, sampleruKkTex0, wp / 4.5 ).rg;
  float3 col = uKkC0.rgb * ( 1.0 + ( notebook ? 0.02 : 0.06 ) * drag + ( notebook ? 0.015 : 0.07 ) * bloom + 0.04 * cloud );
  // fibres mostly lift the wash (paper showing through); dark ones alone would read as cracks
  col *= 1.0 - 0.06 * fib.r + ( notebook ? 0.06 : 0.12 ) * fib.g;
  float edgeT = 1.0 - smoothstep( 0.35, 1.6, e - wob );
  col *= 1.0 - ( notebook ? 0.03 : 0.07 ) * edgeT;
  if ( notebook ) {
    // ruled paper: rules along the strip that stop short of the cut, a red double margin on the left
    float rl = abs( frac( ( lat + hw ) / uKkV0.z + 0.5 ) - 0.5 ) * uKkV0.z;
    float rule = ( 1.0 - smoothstep( 0.025, 0.025 + max( fwidth( rl ), 1e-4 ) * 1.5, rl ) ) * smoothstep( 0.5, 0.9, e );
    col = lerp( col, uKkC4.rgb, rule * 0.62 );
    float mx = lat + hw - 2.4;
    col = lerp( col, uKkC5.rgb, min( 1.0, kkBand( mx, -0.035, 0.035 ) + kkBand( mx, 0.14, 0.2 ) ) * 0.85 );
  }
  // The printer's screen: a flat tint of about 6 % on open road; dots only where the tone rises (dips,
  // the edge falloff, the gutter), so the open road never shows a regular dot grid.
  float gut = kkGutterT( wp );
  float tone = ( notebook ? 0.03 : 0.06 ) + 0.2 * edgeT + 0.3 * vKkB.x + 0.24 * gut;
  float dotsOn = smoothstep( 0.1, 0.45, max( max( edgeT, vKkB.x * 1.5 ), gut * 1.5 ) );
  float cov = lerp( tone, kkScreenRough( wp, 0.24, clamp( tone, 0.0, 0.5 ) ), dotsOn );
  col = lerp( col, col * float3( 0.74, 0.77, 0.86 ), cov );
  if ( vKkB.y > 0.01 ) {
    // ice: short printed glint strokes at 45 deg, a few per square metre
    float2 gp = float2( wp.x + wp.y, wp.x - wp.y ) * 0.70710678;
    float row = floor( gp.y / 1.3 ), ph = kkHash( float2( row, 5.0 ) );
    float cellX = floor( gp.x / 2.6 + ph );
    float glint = kkBand( frac( gp.y / 1.3 ), 0.0, 0.05 ) * kkBand( frac( gp.x / 2.6 + ph ), 0.15, 0.5 )
      * step( 0.84, kkHash( float2( row, cellX ) ) );
    col = lerp( col, uKkC2.rgb, 0.65 * vKkB.y );
    col = lerp( col, float3(1.0, 1.0, 1.0), glint * 0.6 * vKkB.y );
  }
  if ( vKkB.z > 0.01 ) {
    col = lerp( col, uKkC3.rgb, 0.62 * vKkB.z );
    col *= 1.0 - 0.25 * vKkB.z * kkScreen( wp + kkNoise( wp * 0.7 ) * 0.4, 0.22, 0.14 );
  }
  // tyre scuffs on the racing printedLine through corners, printed through a fine screen
  float ac = smoothstep( 0.22, 0.8, abs( curv ) );
  if ( ac > 0.001 ) {
    float x = lat + sign( curv ) * 0.36 * hw;
    float t1 = kkBand( abs( abs( x ) - 0.66 ), 0.0, 0.13 );
    float x2 = x - sign( curv ) * 1.9;
    float t2 = kkBand( abs( abs( x2 ) - 0.66 ), 0.0, 0.09 );
    float brk = smoothstep( 0.28, 0.55, kkNoise( float2( dist * 0.3, floor( x * 2.0 ) ) ) );
    float dry = 0.4 + 0.6 * SAMPLE_TEXTURE2D(uGrainTex, sampleruGrainTex, float2( lat * 1.7, dist * 0.017 ) ).r;
    float scuff = ( t1 + 0.55 * t2 ) * ac * brk * dry;
    col = lerp( col, lerp( uInk.rgb, col, 0.3 ), scuff * 0.62 * kkScreenRough( wp, 0.11, 0.45 ) );
  }
  // centre printedLine: tapered pen strokes, each one a little different
  float k = floor( dist / uKkV0.x ), s = frac( dist / uKkV0.x );
  if ( s < 0.34 ) {
    float q = s / 0.34;
    float w = ( notebook ? 0.1 : 0.17 ) * pow( sin( 3.14159 * q ), 0.35 ) * ( 0.85 + 0.3 * kkHash( float2( k, 1.0 ) ) );
    float c = 0.12 * ( kkHash( float2( k, 2.0 ) ) - 0.5 ) + ( q - 0.5 ) * 0.28 * ( kkHash( float2( k, 3.0 ) ) - 0.5 );
    float lc = kkBand( lat - c, -w, w ) * smoothstep( 0.1, 0.3, kkNoise( float2( dist * 2.1, lat * 8.0 ) ) + 0.12 );
    col = lerp( col, uKkC1.rgb, lc );
  }
  // edges: the painter's wash stops short of the inked border, leaving a margin of bare paper whose
  // width wanders (the edge printedLine), with a faint dried rim where the wash ended (notebook: the inked cut
  // alone). Both sit clear of the strip's edge so the wobble never clips them.
  if ( !notebook ) {
    float stop = 0.3 + 0.12 * sin( dist * 0.07 + side * 2.3 ) + 0.1 * ( kkNoise( float2( dist * 0.21, side * 7.0 ) ) - 0.5 );
    float bare = 1.0 - kkBand( e - wob, stop, 99.0 );
    col = lerp( col, col * 0.92, kkBand( e - wob, stop, stop + 0.06 ) * 0.8 );
    col = lerp( col, uKkC1.rgb, bare * ( 0.82 + 0.18 * kkNoise( float2( dist * 3.1, lat ) ) ) );
  }
  float press = notebook ? 0.06 + 0.015 * sin( dist * 0.13 + side * 2.0 ) : 0.19 + 0.06 * sin( dist * 0.13 + side * 2.0 );
  col = lerp( col, uInk.rgb, kkBand( e - wob * 0.8, 0.08, 0.08 + press ) * 0.95 );
  if ( !notebook ) {
    // now and then the pen went round twice: a thinner stroke wandering just inside the first
    float o2 = 0.08 + press + 0.1 + 0.07 * sin( dist * 0.05 + side * 3.1 );
    float on = smoothstep( 0.5, 0.62, kkNoise( float2( dist * 0.06, side * 11.0 ) ) );
    col = lerp( col, uInk.rgb, kkBand( e - wob, o2, o2 + 0.05 ) * on * 0.7 );
  }
  diffuseColor.rgb = kkGutterFold( wp, col );
}
#endif

#ifdef KK_BAND
{
  float t = vKkA.x;
  float2 wp = vPaperWorld.xz;
  float tone = 1.0 - smoothstep( 0.45, 0.96, t + ( kkNoise( wp * 0.09 ) - 0.5 ) * 0.1 );
  // across-band metres; each printedLine wanders a little along its length, like a hand-cut plate
  float x = t * vKkA.z;
  x += 0.07 * ( kkNoise( float2( vKkA.y * 0.12, floor( x / 0.55 ) * 3.1 ) ) - 0.5 );
  float cov = kkLines( x, 0.55, tone );
  float3 col = lerp( kkPagePrint( wp, uKkC1.rgb ), uKkC0.rgb, cov );
  float h = SAMPLE_TEXTURE2D(uKkTex0, sampleruKkTex0, wp / 4.6 ).r * ( 1.0 - smoothstep( 0.3, 0.9, t ) );
  col = lerp( col, uKkC2.rgb, h * 0.55 * cov );
  // a dotted cut printedLine just inside the outer edge keeps the offroad's end readable on pale chapters
  float along = vKkA.y / 0.9;
  float2 q = float2( ( frac( along ) - 0.5 ) * 0.9, ( t - 0.975 ) * vKkA.z );
  float dotL = 1.0 - smoothstep( 0.08, 0.08 + max( fwidth( q.y ), 1e-3 ) * 1.5, length( q ) );
  col = lerp( col, uInk.rgb, dotL * 0.45 * ( 1.0 - smoothstep( 0.15, 0.4, fwidth( along ) ) ) );
  col = lerp( col, uInk.rgb, kkScreen( wp, 0.75, kkGutterT( wp ) * 0.7 ) * 0.55 * cov );
  diffuseColor.rgb = kkGutterFold( wp, col );
}
#endif

#ifdef KK_PAGE
  diffuseColor.rgb = kkPagePrint( vPaperWorld.xz, uKkC0.rgb );
#endif

#ifdef KK_FOIL
{
  float2 hs = float2( vKkA.z, vKkA.w * 0.5 );
  float2 p = float2( vKkA.x, vKkA.y - hs.y );
  float sd = kkBox( p, hs, 0.55 );
  float sdS = kkBox( p - float2( 0.22, -0.26 ), hs, 0.55 );
  if ( sd > 0.0 && sdS > 0.0 ) discard;
  float3 V = normalize( cameraPosition - vPaperWorld );
  float f = dot( V.xz, float2( 0.8, 0.6 ) ) * 2.4 + V.y * 1.6 + p.x * 0.16 + p.y * 0.1;
  float band = floor( frac( f ) * 5.0 ) / 5.0;
  float3 pal = 0.55 + 0.45 * cos( 6.2832 * ( band + float3( 0.0, 0.33, 0.67 ) ) );
  float3 foil = lerp( uKkC0.rgb, uKkC0.rgb * pal * 1.6, 0.35 );
  float spec = kkBand( frac( f * 0.5 + 0.1 ), 0.0, 0.07 );
  foil = lerp( foil, float3(1.0, 1.0, 1.0), spec * 0.65 );
  float ch = frac( p.y / 1.5 - uTime * uKkV0.x + abs( p.x ) * 0.3 );
  float arrow = kkBand( ch, 0.0, 0.34 ) * kkBand( abs( p.x ), -1.0, hs.x - 0.6 ) * kkBand( abs( p.y ), -1.0, hs.y - 0.5 );
  float3 col = lerp( foil, uKkC1.rgb, arrow );
  float inner = 1.0 - smoothstep( -0.3, -0.27, sd );
  kkGlow = uKkC0.rgb * ( 0.18 + 0.45 * spec ) * inner * ( 1.0 - arrow );
  col = lerp( col, uKkC2.rgb, 1.0 - inner );
  col = lerp( col, uInk.rgb, kkBand( sd, -0.07, 0.0 ) * 0.75 );
  col = lerp( col, uKkC3.rgb, step( 0.0, sd ) );
  diffuseColor.rgb = col;
}
#endif

#ifdef KK_WAVE
{
  float along = vKkA.x, h = vKkA.y * vKkA.w, row = vKkA.z;
  float dir = kkMod( row, 2.0 ) < 0.5 ? 1.0 : -1.0;
  float ph = along / ( 3.4 + row * 1.2 ) + uTime * dir * ( 0.11 + 0.05 * row );
  float top = vKkA.w * ( 0.68 + 0.32 * pow( abs( sin( 3.14159 * ph ) ), 0.55 ) );
  if ( h > top ) discard;
  float3 col = row < 0.5 ? uKkC0.rgb : row < 1.5 ? uKkC1.rgb : uKkC2.rgb;
  col = lerp( col, uKkC3.rgb, kkBand( top - h, 0.0, 0.08 ) * 0.9 );
  col = lerp( col, uInk.rgb, kkBand( top - h, -1.0, 0.025 ) * 0.6 );
  diffuseColor.rgb = col;
}
#endif

#ifdef KK_HOLE
{
  if ( cameraPosition.y < vPaperWorld.y + 0.05 ) discard;
  float3 P = vPaperWorld;
  float3 V = normalize( P - cameraPosition );
  float2 ad = normalize( vKkB.xy );
  float a0 = vKkA.x, W = vKkA.y;
  float sF = ( uKkV0.x - P.y ) / min( V.y, -1e-4 );
  float va = dot( V.xz, ad );
  float sW = va < 0.0 ? -a0 / min( va, -1e-5 ) : ( W - a0 ) / max( va, 1e-5 );
  float3 col;
  float3 hit;
  if ( sW < sF ) {
    hit = P + V * sW;
    float depth = P.y - hit.y;
    float printedLine = SAMPLE_TEXTURE2D(uKkTex1, sampleruKkTex1, float2( dot( hit.xz, float2( -ad.y, ad.x ) ) / 4.0, depth / 8.0 ) ).r;
    col = lerp( uKkC2.rgb, uKkC3.rgb, printedLine * 0.85 );
    col *= ( va < 0.0 ? 0.78 : 1.0 ) * ( 1.0 - 0.3 * smoothstep( 0.0, 7.0, depth ) );
  } else {
    hit = P + V * sF;
    col = lerp( uKkC0.rgb, uKkC1.rgb, SAMPLE_TEXTURE2D(uKkTex0, sampleruKkTex0, hit.xz / 40.0 ).r * 0.8 );
    float up = ( P.y - hit.y ) / max( uKkV1.y, 0.05 );
    float aTop = a0 + dot( hit.xz + uKkV1.xz * up - P.xz, ad );
    float lit = step( 0.0, aTop ) * step( aTop, W );
    col = lerp( lerp( col, uInk.rgb, 0.45 * kkScreen( hit.xz, 0.5, 0.5 ) ) * 0.7, col, lit );
  }
  float4 hitClip = TransformWorldToHClip(float3(-hit.x, hit.y, hit.z));
  depth = hitClip.z / hitClip.w;
  if (UNITY_NEAR_CLIP_VALUE < 0.0) depth = depth * 0.5 + 0.5;
  depth = saturate(depth);
  diffuseColor.rgb = col;
}
#endif

    float gA = SAMPLE_TEXTURE2D(uGrainTex,sampleruGrainTex,vPaperWorld.xz*0.14+vPaperWorld.y*0.09).r;
    float gB = SAMPLE_TEXTURE2D(uGrainTex,sampleruGrainTex,vPaperWorld.xz*0.021).r;
    diffuseColor.rgb *= 1 + ((gA-0.92)*0.9+(gB-0.92)*0.6)*uGrainAmount;
    if (_UseScreen > 0.5)
    {
     float2 sp = vPaperWorld.xz/uScreen.x;
     sp = float2(sp.x+sp.y,sp.y-sp.x)*0.70710678;
     float d = length(frac(sp)-0.5), w = max(fwidth(d),1e-4);
     float m = 1-smoothstep(uScreen.y-w,uScreen.y+w,d);
     m = lerp(m,3.14159*uScreen.y*uScreen.y,smoothstep(0.18,0.45,length(fwidth(sp))));
     diffuseColor.rgb *= 1-uScreen.z*m;
    }
    if (_UseOverlay > 0.5)
    {
     float2 uv = i.uv*uOverlayRepeat.xy; uv.y -= uTime*uScroll;
     diffuseColor.rgb = lerp(diffuseColor.rgb,uOverlayColor.rgb,SAMPLE_TEXTURE2D(uOverlay,sampleruOverlay,uv).r);
    }
    diffuseColor *= i.color;
    if (_UsePlates > 0.5)
    {
     float3 pl = SAMPLE_TEXTURE2D(uPlates,sampleruPlates,i.uv).rgb*uPlateOn.rgb;
     diffuseColor.rgb = lerp(diffuseColor.rgb,uPlateB.rgb,pl.b);
     diffuseColor.rgb = lerp(diffuseColor.rgb,uPlateG.rgb,pl.g);
     diffuseColor.rgb = lerp(diffuseColor.rgb,uPlateR.rgb,pl.r);
    }
    float3 normal = normalize(i.normalWS);
    if (_Flat > 0.5) normal = normalize(cross(ddy(i.world),ddx(i.world)));
    normal *= IS_FRONT_VFACE(frontFace,1.0,-1.0);
    Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
    float nl = dot(normal,sun.direction);
    float shade = (nl*0.5+0.5 < 1.0/3.0) ? _Gradient.x : ((nl*0.5+0.5 < 2.0/3.0) ? _Gradient.y : _Gradient.z);
    float shadow = lerp(1,smoothstep(0.4,0.6,sun.shadowAttenuation),_ReceiveShadows);
    // 原作 Three.js 的 Toon BRDF 使用 Lambert 1/π，保留它才能匹配相同灯光强度。
    float3 outgoing = diffuseColor.rgb * lerp((_AmbientColor.rgb+sun.color*shade*shadow)*0.31830988618, float3(1,1,1),_Unlit) + kkGlow + _EmissionColor.rgb;
    float dark = max(1-step(-0.3333,nl),1-shadow);
    if (_UseHalftone > 0.5 && dark > 0.01)
    {
     float2 p = i.positionCS.xy/uDotPx; p=float2(p.x+p.y,p.y-p.x)*0.70710678;
     float d=length(frac(p)-0.5), r=0.36*dark, aa=0.7/uDotPx+0.02;
     outgoing = lerp(outgoing,uInk.rgb*0.9+outgoing*0.25,(1-smoothstep(r-aa,r+aa,d))*0.42);
    }
    float fog = smoothstep(_FogRange.x,_FogRange.y,-TransformWorldToView(i.world).z);
    FragmentOutput o; o.color=float4(lerp(outgoing,_FogColor.rgb,fog),diffuseColor.a); o.depth=depth; return o;
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex ShadowVertex
   #pragma fragment ShadowFragment
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection, _LightPosition;
   float4 ShadowVertex(Attributes i) : SV_POSITION
   {
    UNITY_SETUP_INSTANCE_ID(i);
    float3 w = TransformObjectToWorld(i.positionOS.xyz), n=TransformObjectToWorldNormal(i.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 l = normalize(_LightPosition-w);
    #else
    float3 l = _LightDirection;
    #endif
    float4 p=TransformWorldToHClip(ApplyShadowBias(w,n,l));
    #if UNITY_REVERSED_Z
    p.z=min(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
    #else
    p.z=max(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
    #endif
    return p;
   }
   float4 ShadowFragment() : SV_Target { return 0; }
   ENDHLSL
  }
 }
}
