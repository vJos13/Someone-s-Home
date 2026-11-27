// Rayos de luz (god rays) en pantalla: desde la posición del sol en la imagen se acumula lo que es cielo
// (profundidad lejana) y brillante, a lo largo de rayos radiales. Va antes del post: el bloom los suaviza.
// Lo maneja LightShafts (posición del sol, color, intensidad). Sin el sol a la vista, el pase no se encola.
Shader "SomeonesHome/SunShafts"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "SunShafts"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _SunScreenPos;      // xy = uv del sol, z = visibilidad (0–1)
            float4 _SunShaftColor;     // rgb = color, a = intensidad
            #define SAMPLES 20         // antes 28 (Semana 15): con el dither no aparecen bandas

            float SkyMask(float2 uv)
            {
                float d = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                        bool far = d <= 0.00001;
                #else
                    bool far = d >= 0.99999;
                #endif
                // El color solo se lee donde hay cielo: árboles, terreno y casa cuestan una lectura, no dos.
                float m = 0.0;
                [branch] if (far)
                {
                    float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                    m = saturate(dot(c, float3(0.299, 0.587, 0.114)) * 1.5);
                }
                return m;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float vis = _SunScreenPos.z;
                if (vis <= 0.001) return src;

                float2 toSun = _SunScreenPos.xy - uv;
                // Lejos del sol el rayo ya no suma nada: no se recorre (antes se calculaban los 28 pasos y se multiplicaba por 0).
                float falloff = saturate(1.0 - length(toSun * float2(_ScreenParams.x / _ScreenParams.y, 1.0)) * 0.9);
                [branch] if (falloff <= 0.001) return src;
                float2 stepUv = toSun / SAMPLES * 0.85;
                float2 p = uv + stepUv * Hash(uv * _ScreenParams.xy + _Time.y);   // dither: sin bandas
                float decay = 1.0, acc = 0.0;
                [unroll] for (int i = 0; i < SAMPLES; i++)
                {
                    acc += SkyMask(p) * decay;
                    decay *= 0.937;          // 0,955^(28/20): el rayo se apaga a la misma distancia que con 28 pasos
                    p += stepUv;
                }
                acc /= SAMPLES;
                float3 shafts = _SunShaftColor.rgb * acc * falloff * _SunShaftColor.a * vis;
                return half4(src.rgb + shafts, src.a);
            }
            ENDHLSL
        }
    }
}
