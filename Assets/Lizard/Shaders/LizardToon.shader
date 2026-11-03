// Sombreado cartoon para URP: luz en bandas, sombra teñida, brillo de borde y contorno por casco invertido.
Shader "Lizards/Toon"
{
    Properties
    {
        _BaseMap ("Textura", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _VertexColorWeight ("Usar color de vértice", Range(0,1)) = 0
        _SkinTint ("Tinte de piel", Color) = (1,1,1,1)
        _SkinTintWeight ("Fuerza del tinte de piel", Range(0,1)) = 0
        _SkinSource ("Color de piel a sustituir", Color) = (0.36,0.76,0.2,1)
        _ShadowColor ("Tinte de sombra", Color) = (0.62,0.66,0.85,1)
        _ShadowThreshold ("Umbral de sombra", Range(-1,1)) = 0.05
        _ShadowSoftness ("Suavidad de sombra", Range(0.001,0.5)) = 0.03
        _RimColor ("Brillo de borde (A = intensidad)", Color) = (1,1,1,0.18)
        _RimPower ("Potencia del borde", Range(1,8)) = 3.5
        _SpecSize ("Tamaño del brillo", Range(0,1)) = 0.25
        _OutlineColor ("Color del contorno", Color) = (0.06,0.08,0.05,1)
        _OutlineWidth ("Grosor del contorno", Range(0,0.02)) = 0.0035
        [HideInInspector] _Smoothness ("Smoothness", Float) = 0.5
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _VertexColorWeight;
            half4 _SkinTint;
            half _SkinTintWeight;
            half4 _SkinSource;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half4 _RimColor;
            half _RimPower;
            half _SpecSize;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _Smoothness;
            half _Cutoff;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert (Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.color = i.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                albedo *= lerp(half3(1, 1, 1), i.color.rgb, _VertexColorWeight);
                // Tinte de piel: sustituye solo las zonas del color de piel (verde); respeta barriga, boca, etc.
                if (_SkinTintWeight > 0.001)
                {
                    half3 src = _VertexColorWeight > 0.5 ? i.color.rgb : _BaseColor.rgb;
                    half match = smoothstep(0.28, 0.12, distance(src, _SkinSource.rgb)) * _SkinTintWeight;
                    albedo = lerp(albedo, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _SkinTint.rgb, match);
                }
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light l = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                half ndl = dot(n, l.direction);
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl);
                lit *= smoothstep(0.25, 0.55, l.shadowAttenuation);

                half3 lightCol = max(l.color, half3(0.6, 0.6, 0.6));
                half3 col = albedo * lerp(_ShadowColor.rgb, half3(1, 1, 1), lit) * lightCol * 0.9;
                col += albedo * SampleSH(n) * 0.25;

                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimColor.a * (0.4 + 0.6 * lit);
                col += _RimColor.rgb * rim;

                if (_SpecSize > 0.001)
                {
                    float3 h = normalize(l.direction + v);
                    half edge = 1.0 - _SpecSize * 0.06;
                    col += smoothstep(edge - 0.004, edge, dot(n, h)) * 0.3 * lit * l.color;
                }

                col = MixFog(col, i.fog);
                return half4(col, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                float4 pos = TransformObjectToHClip(i.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(i.normalOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float2 off = normalize(nCS + 1e-5);
                off.x *= _ScreenParams.y / _ScreenParams.x;
                // Grosor constante en pantalla, limitado de cerca para que no se engorde.
                pos.xy += off * _OutlineWidth * 2.0 * min(pos.w, 12.0);
                o.positionCS = pos;
                return o;
            }

            half4 frag (Varyings i) : SV_Target { clip(_OutlineWidth - 1e-5); return _OutlineColor; } // sin grosor: no se dibuja
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}
