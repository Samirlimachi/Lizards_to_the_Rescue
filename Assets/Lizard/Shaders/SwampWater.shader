Shader "Lizards/SwampWater" {
 Properties { _BaseColor("Water",Color)=(0.025,0.58,0.53,1) }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes {float4 positionOS:POSITION;};
 struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;
 CBUFFER_END
 Varyings vert(Attributes v){Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
 half4 frag(Varyings i):SV_Target{
 float wave=sin(i.world.x*.7+i.world.z*.15+_Time.y*.35)*sin(i.world.z*1.5-_Time.y*.4);
 float caustic=pow(abs(sin(i.world.x*.8+sin(i.world.z*.65+_Time.y*.2))*sin(i.world.z*.9+sin(i.world.x*.6))),10);
 return half4(_BaseColor.rgb*(.94+.06*wave)+caustic*half3(.08,.17,.14),1);
 }
 ENDHLSL
 } }
}
