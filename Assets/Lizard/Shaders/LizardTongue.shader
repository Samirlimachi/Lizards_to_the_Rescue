// Lengua: rosado sólido con un sombreado suave. Dibuja las dos caras y no tiene contorno,
// así nunca puede verse negra (sea cual sea la orientación de los triángulos).
Shader "Lizards/Tongue"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.42, 0.6, 1)
        _ShadeColor ("Color en sombra", Color) = (0.93, 0.32, 0.5, 1)
        _HighlightColor ("Brillo", Color) = (1, 0.8, 0.88, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "TongueForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadeColor;
                half4 _HighlightColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            half4 frag (Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                // Usa la cara que mira a la cámara, esté como esté orientado el triángulo.
                if (dot(n, v) < 0) n = -n;
                half facing = saturate(dot(n, v));
                half3 col = lerp(_ShadeColor.rgb, _BaseColor.rgb, smoothstep(0.15, 0.55, facing));
                col = lerp(col, _HighlightColor.rgb, smoothstep(0.85, 0.97, facing) * 0.6);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
