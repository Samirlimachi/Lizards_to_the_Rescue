Shader "Lizards/SewerWater" {
 Properties { _BaseColor("Agua",Color)=(0.08,0.72,0.70,1) _LineColor("Líneas",Color)=(0.75,1,0.95,1) _Flow("Cascada (0/1)",Float)=0 _Scale("Escala de celdas",Float)=0.55 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;};
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float4 _LineColor;float _Flow;float _Scale;
 CBUFFER_END
 float2 hash2(float2 p){p=float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)));return frac(sin(p)*43758.5453);}
 float edgeDist(float2 x){
  float2 n=floor(x),f=frac(x),mg=0,mr=0;float md=8;
  for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++){float2 g=float2(i,j);float2 o=hash2(n+g);o=.5+.42*sin(_Time.y*.55+6.2831*o);float2 r=g+o-f;float d=dot(r,r);if(d<md){md=d;mr=r;mg=g;}}
  md=8;
  for(int j2=-2;j2<=2;j2++)for(int i2=-2;i2<=2;i2++){float2 g=mg+float2(i2,j2);float2 o=hash2(n+g);o=.5+.42*sin(_Time.y*.55+6.2831*o);float2 r=g+o-f;if(dot(mr-r,mr-r)>.00001)md=min(md,dot(.5*(mr+r),normalize(r-mr)));}
  return md;}
 Varyings vert(Attributes v){Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=v.uv;o.color=v.color;return o;}
 half4 frag(Varyings i):SV_Target{
  if(_Flow>.5){
   float s=frac(i.uv.y*2.2+_Time.y*1.6+sin(i.uv.x*17.)*.18);
   float streak=smoothstep(.55,.62,s)*(1-smoothstep(.8,.9,s));
   float edge=smoothstep(.75,1.,abs(i.uv.x*2-1));
   float foam=smoothstep(.75,1.,i.uv.y);
   half3 c=lerp(_BaseColor.rgb*1.15,_LineColor.rgb,saturate(streak*.7+edge*.5+foam));
   return half4(c,1);}
  float e=edgeDist(i.world.xz*_Scale);
  float e2=edgeDist(i.world.xz*_Scale*1.9+17.3);
  float lines=1-smoothstep(.015,.06,e);
  float fine=(1-smoothstep(.01,.04,e2))*.35;
  float wave=sin(i.world.x*.7+i.world.z*.15+_Time.y*.6)*sin(i.world.z*1.3-_Time.y*.5);
  half3 c=_BaseColor.rgb*(.92+.08*wave);
  c=lerp(c,_LineColor.rgb,saturate(lines*.85+fine));
  c=lerp(c,half3(1,1,1),saturate(i.color.a<.5?(.5-i.color.a)*2:0));
  return half4(c,1);}
 ENDHLSL
 }
 } }
