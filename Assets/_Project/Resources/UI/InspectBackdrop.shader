// Velo detrás del objeto que se inspecciona: oscurece la escena con una viñeta suave (más oscuro en los bordes).
Shader "SomeonesHome/InspectBackdrop"
{
    Properties
    {
        _Color ("Color", Color) = (0.0, 0.0, 0.0, 0.72)
        _Edge ("Viñeta", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Edge;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w - 0.5;
                uv.x *= _ScreenParams.x / _ScreenParams.y;
                float v = saturate(dot(uv, uv) * 1.6);
                return half4(_Color.rgb, saturate(_Color.a + v * _Edge));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
