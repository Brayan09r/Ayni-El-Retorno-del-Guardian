// Niebla de montaña en capas: un plano translúcido con nubes procedurales que se mueven despacio.
// Se funde con el terreno usando la profundidad de la escena, así no se ve el corte contra las paredes.
Shader "Ayni/Niebla"
{
    Properties
    {
        _Color ("Color de la niebla", Color) = (0.92, 0.90, 0.88, 1)
        _Density ("Densidad", Range(0, 1)) = 0.55
        _Scale ("Tamano de los jirones (m)", Float) = 38
        _Speed ("Velocidad", Float) = 0.6
        _SoftDistance ("Fundido con el terreno (m)", Float) = 5
        _Seed ("Variacion", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }

        Pass
        {
            Name "Niebla"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
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
                float _Density;
                float _Scale;
                float _Speed;
                float _SoftDistance;
                float _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
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
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * Noise(p);
                    p = p * 2.03 + 11.7;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.positionWS.xz / max(_Scale, 0.1) + _Seed * 13.1;
                float n = Fbm(p + float2(t * 0.021, t * 0.013)) * 0.65 + Fbm(p * 2.3 - float2(t * 0.017, t * 0.027)) * 0.35;
                float cloud = smoothstep(0.38, 0.74, n);

                // Fundido donde el plano se acerca al terreno o a la cámara
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragEye = i.positionCS.w;
                float soft = saturate((sceneEye - fragEye) / max(_SoftDistance, 0.01));
                float nearFade = saturate((fragEye - 1.5) / 8.0);

                Light mainLight = GetMainLight();
                float3 ambient = SampleSH(float3(0, 1, 0));
                float3 color = _Color.rgb * (ambient * 0.9 + mainLight.color * 0.35);

                float alpha = cloud * _Density * soft * nearFade;
                color = MixFog(color, InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
