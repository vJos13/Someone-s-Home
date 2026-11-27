// Haz visible de la linterna en el aire (dispersión simple): para cada píxel se integra cuánta luz del foco
// se dispersa a lo largo del rayo de la cámara, hasta la pared / objeto más cercano (profundidad de escena).
// Se dibujan las caras de atrás de un cono cerrado que envuelve el haz, así funciona también desde adentro
// (en primera persona la linterna sale de la cámara). Lo maneja FlashlightHaze.
Shader "SomeonesHome/LightCone"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.92, 0.8, 1)
        _Intensity ("Intensidad", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Cone"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Back      // la malla tiene las caras hacia adentro: se ve solo la pared del fondo
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float4 _LightPos;      // xyz posición, w alcance
                float4 _LightDir;      // xyz dirección, w = cos del borde exterior
                float _CosInner;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 screen : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs v = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = v.positionCS;
                o.positionWS = v.positionWS;
                o.screen = ComputeScreenPos(v.positionCS);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            half4 frag(Varyings i) : SV_Target
            {
                float3 cam = GetCameraPositionWS();
                float3 toSurf = i.positionWS - cam;
                float surfDist = length(toSurf);
                float3 ray = toSurf / surfDist;
                // Distancia por el rayo hasta lo que haya en la escena.
                float2 suv = i.screen.xy / i.screen.w;
                float eye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float sceneDist = eye / max(dot(ray, -UNITY_MATRIX_V[2].xyz), 1e-3);
                float t = min(surfDist, sceneDist);

                // Integración en pocos pasos (el foco está cerca de la cámara, pero no exactamente en ella).
                const int STEPS = 12;
                float stepLen = t / STEPS;
                float s = stepLen * Hash(suv * _ScreenParams.xy);
                float acc = 0.0;
                [unroll] for (int k = 0; k < STEPS; k++)
                {
                    float3 p = cam + ray * s;
                    float3 d = p - _LightPos.xyz;
                    float dist = length(d);
                    float cosA = dot(d / max(dist, 1e-4), _LightDir.xyz);
                    float spot = smoothstep(_LightDir.w, _CosInner, cosA);
                    float atten = 1.0 / (1.0 + dist * dist * 0.18);
                    float range = saturate(1.0 - dist / _LightPos.w);
                    float nearFade = saturate((s - 0.25) * 1.5);   // no velar la lente
                    acc += spot * atten * range * nearFade * stepLen;
                    s += stepLen;
                }
                return half4(_Color.rgb * acc * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
