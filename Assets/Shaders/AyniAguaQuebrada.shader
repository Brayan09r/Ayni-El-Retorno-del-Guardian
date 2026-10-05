// Agua de las quebradas de AYNI: superficie translúcida con ondas procedurales,
// reflejo del cielo según el ángulo de vista y brillo del sol. No necesita texturas.
Shader "Ayni/Agua Quebrada"
{
    Properties
    {
        _DeepColor ("Color profundo", Color) = (0.03, 0.16, 0.17, 1)
        _ShallowColor ("Color de superficie", Color) = (0.10, 0.34, 0.33, 1)
        _SkyColor ("Reflejo del cielo", Color) = (0.55, 0.68, 0.78, 1)
        _Alpha ("Opacidad", Range(0, 1)) = 0.86
        _WaveScale ("Tamano de las ondas", Float) = 1.6
        _WaveStrength ("Fuerza de las ondas", Range(0, 1)) = 0.18
        _FlowSpeed ("Velocidad", Float) = 0.6
        _FlowDir ("Direccion de la corriente (xz)", Vector) = (0.2, -1, 0, 0)
        _SunGloss ("Brillo del sol", Float) = 180
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Agua"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _SkyColor;
                float _Alpha;
                float _WaveScale;
                float _WaveStrength;
                float _FlowSpeed;
                float4 _FlowDir;
                float _SunGloss;
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

            // Pendiente (derivada) de una onda senoidal que viaja en la dirección d
            float2 WaveSlope(float2 p, float2 d, float freq, float speed, float t)
            {
                return d * cos(dot(p, d) * freq + t * speed) * freq;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                float2 flow = normalize(_FlowDir.xy + 1e-4);
                float2 p = i.positionWS.xz / max(_WaveScale, 0.01) + flow * t * 0.6;

                float2 g = 0;
                g += WaveSlope(p, normalize(float2( 0.92,  0.38)), 1.00, 1.3, t) * 0.50;
                g += WaveSlope(p, normalize(float2(-0.41,  0.91)), 1.70, 1.9, t) * 0.30;
                g += WaveSlope(p, normalize(float2( 0.63, -0.77)), 2.90, 2.6, t) * 0.18;
                g += WaveSlope(p, normalize(float2(-0.87, -0.49)), 4.70, 3.4, t) * 0.10;
                float3 N = normalize(float3(-g.x * _WaveStrength, 1.0, -g.y * _WaveStrength));

                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndv = saturate(dot(N, V));
                float fresnel = pow(1.0 - ndv, 4.0);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float lit = mainLight.shadowAttenuation * saturate(mainLight.direction.y);
                float3 ambient = SampleSH(float3(0, 1, 0));

                float3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, ndv * ndv);
                body *= ambient * 3.0 + mainLight.color * lit * 0.6;
                float3 sky = _SkyColor.rgb * (0.55 + 0.45 * saturate(ambient.g * 1.5 + lit * 0.5));
                float3 color = lerp(body, sky, saturate(0.20 + fresnel * 0.80));

                float3 H = normalize(mainLight.direction + V);
                float spec = pow(saturate(dot(N, H)), _SunGloss) * lit;
                color += mainLight.color * spec * 0.9;

                // Orilla: el agua se aclara y hace espuma donde toca la roca
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthDiff = sceneEye - i.positionCS.w;
                float foamNoise = 0.5 + 0.5 * sin(dot(p, float2(3.1, 2.3)) + t * 2.0);
                float foam = (1.0 - saturate(depthDiff / 1.1)) * (0.55 + 0.45 * foamNoise) * step(0.0, depthDiff);
                color = lerp(color, float3(0.82, 0.88, 0.86) * (ambient + mainLight.color * lit * 0.6), foam * 0.75);

                float alpha = saturate(lerp(_Alpha, 1.0, fresnel) + spec) * saturate(depthDiff / 0.25 + 0.15);
                color = MixFog(color, InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
