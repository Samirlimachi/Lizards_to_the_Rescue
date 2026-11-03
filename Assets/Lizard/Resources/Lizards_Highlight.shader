// Contorno blanco de resaltado (casco invertido): se añade como material extra al objeto que la lengua puede agarrar.
Shader "Lizards/Highlight"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) _Width ("Grosor", Float) = 0.035 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            Name "Highlight"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; float _Width; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 pos : SV_POSITION; };
            V vert (A i)
            {
                V o;
                float3 w = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = normalize(TransformObjectToWorldNormal(i.normalOS));
                float pulse = 1.0 + 0.25 * sin(_Time.y * 8.0);
                o.pos = TransformWorldToHClip(w + n * _Width * pulse);
                return o;
            }
            half4 frag (V i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
