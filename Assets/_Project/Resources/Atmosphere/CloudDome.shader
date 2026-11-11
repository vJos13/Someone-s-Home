// Capa de nubes procedural sobre el skybox (cúpula que sigue a la cámara).
// La maneja Weather.cs: cobertura, viento, colores según el sol/la luna.
Shader "SomeonesHome/CloudDome"
{
    Properties
    {
        _Cover ("Cobertura", Range(0, 1)) = 0.4
        _Density ("Opacidad", Range(0, 1)) = 0.9
        _Scale ("Escala", Float) = 0.55
        _Offset ("Desplazamiento (viento)", Vector) = (0, 0, 0, 0)
        _LitColor ("Color iluminado", Color) = (1, 0.8, 0.65, 1)
        _ShadeColor ("Color en sombra", Color) = (0.35, 0.33, 0.38, 1)
        _SunColor ("Color del sol", Color) = (1, 0.6, 0.35, 1)
        _SunDir ("Dirección hacia el sol", Vector) = (0, 0.2, -1, 0)
        _HorizonColor ("Color del horizonte (niebla)", Color) = (0.78, 0.58, 0.46, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Clouds"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Cover, _Density, _Scale;
                float4 _Offset, _LitColor, _ShadeColor, _SunColor, _SunDir, _HorizonColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;          // cúpula centrada en el objeto: la posición es la dirección
                return o;
            }

            float hash (float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm (float2 p)
            {
                float v = 0.0, a = 0.5;
                float2x2 r = float2x2(0.8, -0.6, 0.6, 0.8);
                [unroll] for (int k = 0; k < 5; k++) { v += a * noise(p); p = mul(r, p) * 2.03 + 11.7; a *= 0.5; }
                return v;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                if (d.y <= 0.0) return 0;
                // Proyección a un plano de nubes: cerca del horizonte las nubes se ven lejanas y comprimidas.
                float2 uv = d.xz / (d.y + 0.12) * _Scale + _Offset.xy;
                float n = fbm(uv);
                float shape = fbm(uv * 0.35 + _Offset.zw);
                float c = saturate(n * 0.75 + shape * 0.45);
                float th = 1.0 - _Cover;
                float dens = smoothstep(th - 0.08, th + 0.28, c);

                // Espesor aproximado: zonas densas más oscuras por debajo, bordes iluminados por el sol.
                float thick = smoothstep(th, th + 0.5, fbm(uv * 1.7 + 3.1));
                float3 sunDir = normalize(_SunDir.xyz);
                float toward = saturate(dot(d, sunDir));
                float silver = pow(toward, 6.0) * (1.0 - thick) * 1.4;
                float3 col = lerp(_LitColor.rgb, _ShadeColor.rgb, thick * 0.8);
                col += _SunColor.rgb * silver;

                // Hacia el horizonte se funden con la niebla.
                float horizon = smoothstep(0.0, 0.25, d.y);
                col = lerp(_HorizonColor.rgb, col, saturate(horizon * 0.85 + 0.15));
                float a = dens * _Density * smoothstep(0.0, 0.08, d.y);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
