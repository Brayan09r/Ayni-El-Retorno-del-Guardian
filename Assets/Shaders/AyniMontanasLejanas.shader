// Cordillera lejana: roca y nieve iluminadas por el sol, hundidas en la bruma del horizonte.
// No usa la niebla de la escena (a esa distancia las borraría): aplica su propia bruma por altura.
Shader "Ayni/Montanas Lejanas"
{
    Properties
    {
        _RockColor ("Roca", Color) = (0.36, 0.33, 0.33, 1)
        _RockColor2 ("Roca en sombra / vegetacion", Color) = (0.24, 0.28, 0.24, 1)
        _SnowColor ("Nieve", Color) = (0.95, 0.96, 1.0, 1)
        _HazeLow ("Bruma en la base", Range(0, 1)) = 0.86
        _HazeHigh ("Bruma en las cumbres", Range(0, 1)) = 0.30
        _HazeHeight ("Altura donde aclara la bruma (m)", Float) = 380
        _HazeColor ("Color de la bruma lejana", Color) = (0.70, 0.70, 0.78, 1)
        _HazeFogMix ("Cuanto toma del color de la niebla", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+50" }

        Pass
        {
            Name "Montanas"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RockColor;
                float4 _RockColor2;
                float4 _SnowColor;
                float _HazeLow;
                float _HazeHigh;
                float _HazeHeight;
                float4 _HazeColor;
                float _HazeFogMix;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                if (N.y < 0) N = -N;

                Light mainLight = GetMainLight();
                float ndl = saturate(dot(N, mainLight.direction));
                float3 ambient = SampleSH(N);

                float snow = saturate(i.color.r) * smoothstep(0.25, 0.6, N.y);
                float3 rock = lerp(_RockColor2.rgb, _RockColor.rgb, saturate(i.color.g));
                float3 albedo = lerp(rock, _SnowColor.rgb, snow);
                float3 color = albedo * (ambient + mainLight.color * ndl);

                float haze = lerp(_HazeLow, _HazeHigh, saturate(i.positionWS.y / max(_HazeHeight, 1.0)));
                color = lerp(color, lerp(_HazeColor.rgb, unity_FogColor.rgb, _HazeFogMix), haze);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
