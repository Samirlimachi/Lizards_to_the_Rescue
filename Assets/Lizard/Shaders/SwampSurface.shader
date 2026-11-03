Shader "Lizards/SwampSurface" {
 Properties { _BaseColor("Color",Color)=(1,1,1,1) _VertexColorWeight("Vertex colors",Range(0,1))=0 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass { Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;};
 struct V{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float4 color:COLOR;};
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _VertexColorWeight;
 CBUFFER_END
 V vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.pos=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;return o;}
 half4 frag(V i):SV_Target{Light light=GetMainLight(TransformWorldToShadowCoord(i.world));float nd=saturate(dot(normalize(i.normal),light.direction));float shade=lerp(.52,1,smoothstep(.1,.6,nd))*lerp(.75,1,light.shadowAttenuation);half3 col=_BaseColor.rgb*lerp(half3(1,1,1),i.color.rgb,_VertexColorWeight);return half4(col*shade,1);}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 } }
