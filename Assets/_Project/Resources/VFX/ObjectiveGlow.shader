// Resaltado de objetos de misión: borde (fresnel) cálido y aditivo, un poco inflado sobre la malla.
// Lo maneja ObjectiveHighlighter (intensidad pulsante). Respeta la niebla: de lejos se apaga hacia negro.
Shader "SomeonesHome/ObjectiveGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.82, 0.5, 1)
        _Intensity ("Intensidad", Float) = 0
        _Inflate ("Inflado (m)", Float) = 0.004
        _Power ("Potencia del borde", Float) = 2.0
        _Fill ("Relleno", Range(0, 1)) = 0.16
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Inflate;
                float _Power;
                float _Fill;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                float3 p = TransformObjectToWorld(i.positionOS.xyz) + n * _Inflate;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = n;
                o.viewWS = GetWorldSpaceViewDir(p);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), _Power);
                half3 c = _Color.rgb * (rim + _Fill) * _Intensity;
                c = MixFogColor(c, half3(0, 0, 0), i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
