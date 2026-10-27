// Rayos de sol volumétricos (God Rays) para cañones, puentes y portadas andinas en URP.
// Se funden suavemente con el terreno y los muros leyendo la textura de profundidad de la cámara,
// y adaptan su color e intensidad a la luz direccional principal (Hora Dorada, Fase 2 o Desenlace Ayni).
Shader "Ayni/Rayos de Sol"
{
    Properties
    {
        _Color ("Tinte solar", Color) = (1.0, 0.84, 0.55, 1.0)
        _Intensity ("Intensidad", Range(0, 2)) = 0.42
        _RayScale ("Frecuencia de haces", Float) = 6.5
        _Speed ("Velocidad de bruma solar", Float) = 0.35
        _SoftDistance ("Fundido con terreno y muros (m)", Float) = 4.0
        _EdgeFade ("Suavizado lateral", Range(0.05, 0.5)) = 0.25
        _Seed ("Semilla de variacion", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RayosSol"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _RayScale;
                float _Speed;
                float _SoftDistance;
                float _EdgeFade;
                float _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * Noise(p);
                    p = p * 2.07 + 7.3;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;

                // Estrías longitudinales de los rayos solares + motas de polvo suspendidas en el aire andino
                float2 rayUV = float2(i.uv.x * _RayScale + _Seed * 5.7, i.uv.y * 0.65 - t * 0.12);
                float streaks = Fbm(rayUV) * 0.65 + Fbm(rayUV * 2.1 + float2(t * 0.08, -t * 0.18)) * 0.35;
                streaks = smoothstep(0.32, 0.78, streaks);

                // Atenuación suave en los bordes laterales y longitudinales del haz
                float sideFade = smoothstep(0.0, _EdgeFade, i.uv.x) * smoothstep(1.0, 1.0 - _EdgeFade, i.uv.x);
                float verticalFade = smoothstep(0.0, 0.28, i.uv.y) * smoothstep(1.0, 0.55, i.uv.y);

                // Atenuación según el ángulo de visión para ocultar por completo cualquier plano visto de canto
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 N = normalize(i.normalWS);
                float ndv = abs(dot(N, viewDir));
                float viewFade = smoothstep(0.22, 0.58, ndv) * ndv;

                // Fundido suave con la geometría de la escena (terreno, acantilados, puente) y con la cámara cercana
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragEye = i.positionCS.w;
                float softDepth = saturate((sceneEye - fragEye) / max(_SoftDistance, 0.01));
                float nearFade = saturate((fragEye - 2.0) / 7.5);
                float farFade = 1.0 - saturate((fragEye - 140.0) / 90.0);

                Light mainLight = GetMainLight();
                float3 sunColor = lerp(_Color.rgb, _Color.rgb * mainLight.color, 0.65);

                float alpha = streaks * sideFade * verticalFade * viewFade * softDepth * nearFade * farFade * _Intensity * _Color.a;
                return half4(sunColor, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
