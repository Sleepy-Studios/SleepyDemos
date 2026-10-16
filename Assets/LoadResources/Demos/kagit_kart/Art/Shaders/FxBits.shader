Shader "KagitKart/FxBits" {
 Properties { uInk("墨色",Vector)=(0,0,0,1) uSunDir("太阳方向",Vector)=(0,1,0,0) uSunCol("太阳色",Vector)=(2,2,2,1) uAmb("环境光",Vector)=(1,1,1,1) _FogColor("雾",Vector)=(1,1,1,1) _FogRange("雾范围",Vector)=(170,760,0,0) uColor("速度线",Vector)=(0,0,0,1) uEdge("速度线边",Vector)=(1,1,1,1) uOpacity("不透明度",Float)=0.72 }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float uTime,uIntensity,uAspect,uOpacity;float2 uRes;float4 uInk,uSunDir,uSunCol,uAmb,uColor,uEdge,_FogColor,_FogRange,_CameraRight,_CameraUp,_CameraBack,_CameraPosition;
 struct FxState { float4 P0,V0,Phys,Shape,Look,ColA,ColB; };
 #if SHADER_TARGET >= 45
 StructuredBuffer<FxState> _Fx;
 #endif
 struct Attributes
 {
  float4 positionOS:POSITION;
  #if SHADER_TARGET >= 45
  uint instanceID:SV_InstanceID;
  #else
  float4 p0:TEXCOORD0;
  float4 v0:TEXCOORD1;
  float4 phys:TEXCOORD2;
  float4 shape:TEXCOORD3;
  float4 look:TEXCOORD4;
  float4 colA:TEXCOORD5;
  float4 colB:TEXCOORD6;
  #endif
 };
 FxState ReadFxState(Attributes input)
 {
  #if SHADER_TARGET >= 45
  return _Fx[input.instanceID];
  #else
  FxState state;
  state.P0=input.p0;
  state.V0=input.v0;
  state.Phys=input.phys;
  state.Shape=input.shape;
  state.Look=input.look;
  state.ColA=input.colA;
  state.ColB=input.colB;
  return state;
  #endif
 }
 struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float4 colA:TEXCOORD2;float4 colB:TEXCOORD3;float3 info:TEXCOORD4;float depth:TEXCOORD5;};
 float fxMod(float a,float b){return a-b*floor(a/b);}
 float4 SourceView(float3 p){return float4(TransformWorldToView(float3(-p.x,p.y,p.z)),1);}
 















float3 hash3( float s ) {
  return frac( sin( float3( s * 127.1, s * 311.7, s * 74.7 ) ) * 43758.5453 );
}

float3x3 axisAngle( float3 a, float ang ) {
  float c = cos( ang ), s = sin( ang ), t = 1.0 - c;
  return transpose(float3x3(
    t * a.x * a.x + c,       t * a.x * a.y + s * a.z, t * a.x * a.z - s * a.y,
    t * a.x * a.y - s * a.z, t * a.y * a.y + c,       t * a.y * a.z + s * a.x,
    t * a.x * a.z + s * a.y, t * a.y * a.z - s * a.x, t * a.z * a.z + c ));
}













float hash1( float n ) { return frac( sin( n ) * 43758.5453 ); }

float sdTri( float2 p, float r ) {
  const float k = 1.7320508;
  p.x = abs( p.x ) - r;
  p.y = p.y + r / k;
  if ( p.x + k * p.y > 0.0 ) p = float2( p.x - k * p.y, -k * p.x - p.y ) / 2.0;
  p.x -= clamp( p.x, -2.0 * r, 0.0 );
  return -length( p ) * sign( p.y );
}

float sdStar5( float2 p, float r, float rf ) {
  const float2 k1 = float2( 0.809016994375, -0.587785252292 );
  const float2 k2 = float2( -0.809016994375, -0.587785252292 );
  p.x = abs( p.x );
  p -= 2.0 * max( dot( k1, p ), 0.0 ) * k1;
  p -= 2.0 * max( dot( k2, p ), 0.0 ) * k2;
  p.x = abs( p.x );
  p.y -= r;
  float2 ba = rf * float2( -k1.y, k1.x ) - float2( 0.0, 1.0 );
  float h = clamp( dot( p, ba ) / dot( ba, ba ), 0.0, r );
  return length( p - ba * h ) * sign( p.y * ba.x - p.x * ba.y );
}

// Crumpled puff: a cloud of six seeded lobes around a core, like the book's hanging clouds; the
// nearest lobe picks the facet tone and the seam between two lobes is a crease.
float sdPuff( float2 p, float seed, out float facet, out float crease ) {
  float best = 1e3;
  float second = 1e3;
  float bi = 0.0;
  for ( int i = 0; i < 6; i ++ ) {
    float fi = float( i );
    float a = fi * 1.0471976 + seed * 6.2831853;
    float rr = 0.34 + 0.12 * hash1( seed * 17.3 + fi * 2.1 );
    float2 c = float2( cos( a ), sin( a ) ) * ( 0.46 + 0.05 * hash1( seed * 5.1 + fi ) );
    float di = length( p - c ) - rr;
    if ( di < best ) { second = best; best = di; bi = fi; }
    else if ( di < second ) second = di;
  }
  facet = bi;
  crease = second - best;
  return min( length( p ) - 0.5, best );
}

// Cut-paper spark burst: eight spikes, long and short in turn, around a solid core.
float sdZap( float2 p, float seed ) {
  float r = length( p );
  float a = atan2( p.y, p.x ) / 6.2831853 * 8.0 + seed * 2.0;
  float f = abs( frac( a ) - 0.5 ) * 2.0;
  float tip = fxMod( floor( a ), 2.0 ) < 0.5 ? 0.98 : 0.74;
  float edge = 0.52 + ( tip - 0.52 ) * pow( 1.0 - f, 1.25 );
  return ( r - edge ) * 0.75;
}


 Varyings Vertex(Attributes input) {Varyings o=(Varyings)0;float3 position=input.positionOS.xyz;FxState itemState=ReadFxState(input); float4 aP0=itemState.P0,aV0=itemState.V0,aPhys=itemState.Phys,aShape=itemState.Shape,aLook=itemState.Look,aColA=itemState.ColA,aColB=itemState.ColB;

  float age = uTime - aP0.w;
  float life = aV0.w;
  if ( age < 0.0 || age >= life ) {
    o.positionCS = float4( 0.0, 0.0, 2.0, 1.0 ); // outside the clip volume
    return o;
  }
  float u = age / life;
  bool landed = age >= aPhys.w;
  float tl = min( age, aPhys.w );

  // v' = -k v - g y  →  closed form for position and velocity
  float k = max( aPhys.x, 0.01 );
  float g = aPhys.y;
  float e = exp( -k * tl );
  float fk = ( 1.0 - e ) / k;
  float3 p = aP0.xyz + aV0.xyz * fk;
  p.y -= g * ( tl - fk ) / k;
  float3 vel = aV0.xyz * e;
  vel.y -= g * fk;

  float seed = aShape.z;
  float flutter = aLook.w;
  if ( flutter > 0.0 ) {
    float ph = seed * 37.0;
    float amp = flutter * ( 1.0 - exp( -2.0 * tl ) );
    p.x += sin( tl * 4.3 + ph ) * amp;
    p.z += cos( tl * 3.7 + ph * 1.3 ) * amp;
  }
  if ( landed ) p.y = aPhys.z + 0.02;

  float sc;
  if ( aColB.a < 0.5 ) {
    sc = min( 1.0, 0.55 + age * 14.0 ) * ( 1.0 - smoothstep( 0.72, 1.0, u ) );
  } else {
    sc = ( 0.35 + 0.65 * ( 1.0 - exp( -age * 11.0 ) ) ) * ( 1.0 + 0.45 * u ) * ( 1.0 - smoothstep( 0.6, 1.0, u ) );
  }

  float size = aShape.y * sc;
  float aspect = aLook.z;
  float fold = landed ? 0.0 : aLook.y;
  float side = position.z;
  float a = fold * 0.5;
  float3 lp = float3( position.x * aspect * cos( a ), position.y, -abs( position.x ) * aspect * sin( a ) ) * size;
  float3 ln = float3( side * sin( a ), 0.0, cos( a ) );

  float3 camR = _CameraRight.xyz;
  float3 camU = _CameraUp.xyz;
  float3 camF = _CameraBack.xyz;

  float mode = aShape.w;
  float ang = aLook.x * tl + seed * 6.2831853;
  float3 wp;
  float3 wn;
  if ( mode < 0.5 ) {
    float3x3 R;
    if ( landed ) {
      float yw = seed * 6.2831853;
      float c = cos( yw ), s = sin( yw );
      R = transpose(float3x3( float3( c, 0.0, s ), float3( s, 0.0, -c ), float3( 0.0, 1.0, 0.0 ) ));
    } else {
      float3 ax = normalize( hash3( seed ) - 0.5 + float3( 0.0, 1e-3, 0.0 ) );
      R = axisAngle( ax, ang );
    }
    wp = mul(R,lp);
    wn = mul(R,ln);
  } else if ( mode < 1.5 ) {
    float c = cos( ang ), s = sin( ang );
    wp = camR * ( c * lp.x - s * lp.y ) + camU * ( s * lp.x + c * lp.y ) + camF * lp.z;
    wn = camR * ( c * ln.x - s * ln.y ) + camU * ( s * ln.x + c * ln.y ) + camF * ln.z;
  } else if ( mode < 2.5 ) {
    float c = cos( ang ), s = sin( ang );
    wp = camR * ( c * lp.x + s * lp.z ) + camU * lp.y + camF * ( -s * lp.x + c * lp.z );
    wn = camR * ( c * ln.x + s * ln.z ) + camU * ln.y + camF * ( -s * ln.x + c * ln.z );
  } else {
    float3 dir = dot( vel, vel ) > 1e-6 ? normalize( vel ) : camU;
    float3 toCam = normalize( _CameraPosition.xyz - p );
    float3 wide = cross( dir, toCam );
    wide = dot( wide, wide ) > 1e-8 ? normalize( wide ) : camR;
    wp = wide * lp.x + dir * lp.y;
    wn = toCam;
  }

  float4 mvPosition = SourceView( p + wp);
  o.positionCS = mul(UNITY_MATRIX_P,mvPosition);
  o.uv = position.xy;
  o.normal = wn;
  o.colA = aColA;
  o.colB = aColB;
  o.info = float3( aShape.x, seed, u );
  

o.depth=-mvPosition.z;return o;}
 float4 Fragment(Varyings i,FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target{float4 outputColor;
  float2 p = i.uv;
  float id = i.info.x;
  float seed = i.info.y;
  float d;
  float facet = -1.0;
  float crease = 1.0;
  float shade = 1.0;
  float alphaK = 1.0;
  if ( id < 0.5 ) {
    d = sdTri( p + float2( 0.0, 0.24 ), 0.95 );
  } else if ( id < 1.5 ) {
    d = abs( p.x ) - 0.92 * ( 1.0 - p.y * p.y );
  } else if ( id < 2.5 ) {
    d = sdStar5( p * 1.04, 0.95, 0.46 );
  } else if ( id < 3.5 ) {
    float2 q = abs( p );
    d = max( max( q.x, q.y ) - 0.9, q.x + q.y - 1.45 - 0.2 * hash1( seed * 3.1 ) );
  } else if ( id < 4.5 ) {
    d = sdPuff( p, seed, facet, crease );
    shade = 0.78 + 0.22 * hash1( seed * 7.7 + facet * 3.3 );
  } else if ( id < 5.5 ) {
    // ink stroke: thick head (+y), thin tail, fading tail
    float t = p.y * 0.5 + 0.5;
    d = abs( p.x ) - 0.95 * ( 0.2 + 0.8 * t * t ) * ( 1.0 - smoothstep( 0.86, 1.0, t ) * 0.9 );
    alphaK = 0.8 * smoothstep( 0.0, 0.45, t );
  } else {
    d = sdZap( p, seed );
  }
  float w = max( fwidth( d ), 1e-4 );
  float alpha = ( 1.0 - smoothstep( -w * 0.5, w * 0.5, d ) ) * alphaK;
  if ( alpha < 0.02 ) discard;

  float3 n = normalize( i.normal );
  float3 base = IS_FRONT_VFACE(frontFace,true,false) ? i.colA.rgb : i.colB.rgb;
  if ( !IS_FRONT_VFACE(frontFace,true,false) ) n = -n;
  float nl = dot( n, uSunDir.xyz );
  float band = nl >= 0.3333 ? 1.0 : ( nl >= -0.3333 ? 0.7 : 0.38 );
  float3 lit = base * ( uAmb.rgb + uSunCol.rgb * band ) * 0.31830989;
  float3 col = lerp( lit, base, i.colA.a ) * shade;

  // cut edge inked about 1.3 px wide; fades out when the piece is only a few pixels big
  float lineW = 1.3 * w;
  float edge = smoothstep( -lineW - w * 0.5, -lineW + w * 0.5, d ) * smoothstep( 0.4, 0.14, w );
  if ( id > 4.5 && id < 5.5 ) edge = 0.0;
  if ( facet >= 0.0 ) {
    float seam = ( 1.0 - smoothstep( w * 0.8, w * 2.2, crease ) ) * smoothstep( 0.0, -w * 3.0, d );
    edge = max( edge, seam * 0.45 * smoothstep( 0.4, 0.14, w ) );
  }
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
