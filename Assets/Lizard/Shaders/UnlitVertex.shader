Shader "Lizards/UnlitVertex" {
 Properties { _BaseColor("Color",Color)=(1,1,1,1) }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass { Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 positionOS:POSITION;float4 color:COLOR;};
 struct V{float4 pos:SV_POSITION;float4 color:COLOR;};
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;
 CBUFFER_END
 V vert(A i){V o;o.pos=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;return o;}
 half4 frag(V i):SV_Target{return half4(_BaseColor.rgb*i.color.rgb,1);}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 } }
