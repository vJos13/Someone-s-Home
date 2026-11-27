// Punto de luz suave para partículas (luciérnagas, polvo): aditivo, sin textura, respeta la niebla.
Shader "SomeonesHome/SoftGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.9, 0.5, 1)
        _Intensity ("Intensidad", Float) = 2
        _Core ("Núcleo", Range(1, 16)) = 6
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Core;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float r = saturate(1.0 - dot(d, d));
                float a = pow(r, _Core) + r * r * 0.25;          // núcleo brillante + halo
                half3 col = _Color.rgb * i.color.rgb * _Intensity;
                // Aditivo: la niebla apaga el brillo (se mezcla hacia negro) en vez de teñirlo.
                col = MixFogColor(col, half3(0, 0, 0), i.fog);
                return half4(col, saturate(a * i.color.a * _Color.a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
