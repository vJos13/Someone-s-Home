// Filtro de cinta VHS / camcorder de fines de los 90 (pase de pantalla completa, después del post).
// Lo aplica VHSRendererFeature solo en la última cámara de la pila (así también afecta a lo que se tiene en mano).
// Intensidad global: _VHSAmount (0 = imagen limpia, 1 = cinta gastada). Pulso de tracking: _VHSGlitch (0..1).
//
//  · Croma de cinta: la luminancia se conserva nítida y el color (YIQ) se ensancha horizontalmente y se corre a la derecha.
//  · Inestabilidad: cada línea se mueve un poco; de vez en cuando baja una banda de tracking con ruido.
//  · Ruido de cabezal abajo, grano de cinta, puntos blancos (dropouts), líneas de barrido muy suaves.
//  · Color: negros levantados, saturación algo baja, blancos que "sangran" (como las cámaras de consumo de 1998).
Shader "SomeonesHome/VHS"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "VHS"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float _VHSAmount;
            float _VHSGlitch;

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise1(float x) { float i = floor(x); float f = frac(x); return lerp(Hash(float2(i, 0.37)), Hash(float2(i + 1, 0.37)), smoothstep(0, 1, f)); }

            float3 ToYIQ(float3 c) { return float3(dot(c, float3(0.299, 0.587, 0.114)), dot(c, float3(0.596, -0.274, -0.322)), dot(c, float3(0.211, -0.523, 0.312))); }
            float3 FromYIQ(float3 y) { return float3(y.x + 0.956 * y.y + 0.621 * y.z, y.x - 0.272 * y.y - 0.647 * y.z, y.x - 1.106 * y.y + 1.703 * y.z); }

            // El búfer está en espacio lineal: los efectos de cinta se calculan en sRGB (como la señal de video real).
            float3 Tex(float2 uv) { return LinearToSRGB(saturate(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb)); }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float a = saturate(_VHSAmount);
                if (a <= 0.001 && _VHSGlitch <= 0.001) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float t = _Time.y;
                float2 res = _ScreenParams.xy;
                float px = 1.0 / res.x;
                // Las "líneas" de la cinta: ~480 visibles, sin importar la resolución.
                float row = floor(uv.y * 480.0);

                // ── Inestabilidad horizontal ──
                float jitter = (Hash(float2(row, floor(t * 30.0))) - 0.5) * 0.0009;
                float wobble = sin(uv.y * 9.0 + t * 1.3) * 0.0004;
                // Banda de tracking que baja despacio (más fuerte con _VHSGlitch).
                float bandPos = frac(t * 0.045 + Noise1(t * 0.07) * 0.3);
                float band = smoothstep(0.035, 0.0, abs(uv.y - (1.0 - bandPos))) * (0.35 + _VHSGlitch * 1.8);
                float bandShift = band * (Hash(float2(row, floor(t * 60.0))) - 0.3) * 0.02;
                // Ruido de cabezal: las últimas líneas de abajo se tuercen.
                float head = smoothstep(0.03, 0.0, uv.y);
                float headShift = head * (0.012 + Hash(float2(row, floor(t * 24.0))) * 0.02);
                // Pulso de tracking (al empezar, al saltar escenas): toda la imagen se sacude.
                float glitchShift = _VHSGlitch * (Hash(float2(floor(uv.y * 60.0), floor(t * 40.0))) - 0.5) * 0.03;
                float2 suv = uv + float2((jitter + wobble) * a + bandShift + headShift + glitchShift, 0);

                // ── Luma nítida (con un pelo de suavizado) y croma ancho, corrido a la derecha ──
                float3 c0 = Tex(suv);
                float3 cl = Tex(suv - float2(px, 0));
                float3 cr = Tex(suv + float2(px, 0));
                float luma = ToYIQ(c0 * 0.6 + (cl + cr) * 0.2).x;
                float2 chroma = 0;
                float off = 2.5 * a;
                [unroll] for (int k = -3; k <= 3; k++)
                    chroma += ToYIQ(Tex(suv + float2((k * 1.6 - off) * px * (1.0 + a), 0))).yz;
                chroma /= 7.0;
                // Saturación de cinta de consumo: algo apagada.
                chroma *= lerp(1.0, 0.82, a);
                float3 col = FromYIQ(float3(luma, chroma));

                // ── Sangrado de blancos: lo muy claro se corre un poco a la derecha ──
                float3 smear = max(Tex(suv - float2(5.0 * px, 0)) - 0.75, 0);
                col += smear * 0.35 * a;

                // ── Color de cinta: negros levantados, curva suave, leve tinte frío en sombras ──
                col = lerp(col, col * 0.95 + float3(0.018, 0.02, 0.026), a);

                // ── Grano, dropouts, banda y cabezal ──
                // Grano de cinta: grumos de ~2 px, más visible en los medios tonos que en los negros.
                float grain = Hash(floor(uv * res * 0.5) + frac(t * 13.0) * 100.0) - 0.5;
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col += grain * 0.03 * a * (0.35 + lum);
                float drop = step(0.9985, Hash(float2(row * 0.37, floor(t * 20.0)) + floor(uv.x * 90.0) * 0.013));
                col = lerp(col, float3(0.85, 0.85, 0.85), drop * 0.6 * a);
                col += band * (Hash(uv * res + t) - 0.35) * 0.25;
                col = lerp(col, float3(1, 1, 1) * Hash(float2(uv.x * 200.0, row + t * 50.0)), head * 0.55 * a);

                // ── Líneas de barrido muy suaves y viñeta del lente ──
                float scan = 0.5 + 0.5 * sin(uv.y * res.y * 3.14159);
                col *= lerp(1.0, 0.94 + 0.06 * scan, a);
                float2 v = uv - 0.5;
                col *= lerp(1.0, saturate(1.0 - dot(v, v) * 0.55), a);

                return half4(SRGBToLinear(saturate(col)), 1);
            }
            ENDHLSL
        }
    }
}
