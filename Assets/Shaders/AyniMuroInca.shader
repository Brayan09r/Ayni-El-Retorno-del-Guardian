// Muro inca de AYNI: mampostería de piedra generada en el propio shader, sin texturas.
// El aparejo se dibuja sobre las UV de la malla, que el generador de la aldea escribe en METROS
// (u a lo largo del muro, v hacia arriba): las piedras miden lo mismo en un muro recto, en uno curvo o en un hastial.
//   _Fine = 0  pirca: piedra de campo irregular asentada en barro (casas y cercos)
//   _Fine = 1  sillería: bloques tallados en hiladas, de junta fina y cara almohadillada (portadas y dinteles)
Shader "Ayni/Muro Inca"
{
    Properties
    {
        _StoneColor ("Color de la piedra", Color) = (0.56, 0.52, 0.46, 1)
        _StoneColorB ("Segundo tono de piedra", Color) = (0.42, 0.40, 0.37, 1)
        _MortarColor ("Color de la junta (barro)", Color) = (0.20, 0.16, 0.12, 1)
        _MossColor ("Color del liquen", Color) = (0.34, 0.38, 0.20, 1)
        _StoneLength ("Largo de las piedras (m)", Float) = 0.42
        _StoneHeight ("Alto de las piedras (m)", Float) = 0.26
        _Joint ("Ancho de la junta", Range(0.005, 0.2)) = 0.075
        _Fine ("Sillería fina (0 = pirca, 1 = bloques tallados)", Range(0, 1)) = 0
        _Bulge ("Relieve de cada piedra", Range(0, 1.5)) = 0.75
        _Moss ("Liquen y humedad", Range(0, 1)) = 0.35
        _Smoothness ("Suavidad", Range(0, 1)) = 0.08
        _Solid ("Bloque único, sin juntas (dinteles, losas)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _StoneColor;
                float4 _StoneColorB;
                float4 _MortarColor;
                float4 _MossColor;
                float _StoneLength;
                float _StoneHeight;
                float _Joint;
                float _Fine;
                float _Bulge;
                float _Moss;
                float _Smoothness;
                float _Solid;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                float3 tangentWS  : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangentOS.w * GetOddNegativeScale();
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float MuroHash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float2 MuroHash2(float2 p)
            {
                return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453);
            }

            float MuroNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                f = f * f * (3.0 - 2.0 * f);
                float a = MuroHash1(i);
                float b = MuroHash1(i + float2(1, 0));
                float c = MuroHash1(i + float2(0, 1));
                float d = MuroHash1(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Pirca: piedras irregulares (Voronoi). Devuelve la distancia a la junta, el número de la piedra
            // y el vector hacia su centro.
            void MuroPirca(float2 p, out float edge, out float id, out float2 toCenter)
            {
                float2 cell = floor(p);
                float2 f = p - cell;
                float d1 = 8.0, d2 = 8.0;
                float2 best = 0;
                float bestId = 0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = 0.5 + 0.36 * (MuroHash2(cell + g) * 2.0 - 1.0);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; best = r; bestId = MuroHash1(cell + g + 17.0); }
                        else if (d < d2) { d2 = d; }
                    }
                }
                edge = sqrt(d2) - sqrt(d1);
                id = bestId;
                toCenter = best;
            }

            // Sillería: hiladas horizontales de bloques de largo desigual, cada hilada desplazada respecto a la de abajo.
            void MuroSillar(float2 p, out float edge, out float id, out float2 toCenter)
            {
                float row = floor(p.y);
                float fy = p.y - row;
                float x = p.x * 0.55 + MuroHash1(float2(row, 3.1)) * 7.0;
                float cell = floor(x);
                // Las juntas verticales no caen a intervalos iguales
                float left = cell + (MuroHash1(float2(cell, row)) - 0.5) * 0.5;
                float right = cell + 1.0 + (MuroHash1(float2(cell + 1.0, row)) - 0.5) * 0.5;
                if (x < left) { right = left; left = cell - 1.0 + (MuroHash1(float2(cell - 1.0, row)) - 0.5) * 0.5; cell -= 1.0; }
                else if (x > right) { left = right; right = cell + 2.0 + (MuroHash1(float2(cell + 2.0, row)) - 0.5) * 0.5; cell += 1.0; }
                float dx = min(x - left, right - x) / 0.55;
                float dy = min(fy, 1.0 - fy);
                edge = min(dx, dy) * 1.6;
                id = MuroHash1(float2(cell, row) + 5.0);
                toCenter = float2(((left + right) * 0.5 - x) / 0.55, 0.5 - fy);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 wp = i.positionWS;

                // Coronaciones, umbrales y bancos (caras que miran arriba) crían más liquen
                float isTop = smoothstep(0.75, 0.9, abs(N.y));
                float2 wallP = i.uv;
                float wallU = i.uv.x;
                float3 axisU = normalize(i.tangentWS + float3(1e-5, 0, 0));
                float3 axisV = normalize(i.bitangentWS + float3(0, 1e-5, 0));

                float2 scale = float2(max(_StoneLength, 0.02), max(_StoneHeight, 0.02));
                float2 p = wallP / scale;
                // Las hiladas de la pirca no son rectas: se ondulan un poco
                p.y += (1.0 - _Fine) * 0.35 * sin(p.x * 0.7 + MuroNoise(p * 0.3) * 4.0);

                float edgeA, idA, edgeB, idB;
                float2 toA, toB;
                MuroPirca(p + 11.0, edgeA, idA, toA);
                MuroSillar(p, edgeB, idB, toB);
                float edge = lerp(edgeA, edgeB, _Fine);
                float id = lerp(idA, idB, _Fine);
                float2 toCenter = lerp(toA, toB, _Fine);

                float jointWidth = _Joint * lerp(1.0, 0.35, _Fine);
                half joint = (1.0 - smoothstep(jointWidth * 0.45, jointWidth, edge)) * (1.0 - _Solid);
                id = lerp(id, 0.6, _Solid);

                // Grano de la piedra, vetas y variación entre piedras
                float grain = MuroNoise(wallP * 34.0) * 0.5 + MuroNoise(wallP * 11.0) * 0.5;
                float blotch = MuroNoise(wallP * 1.7 + id * 31.0);
                half3 stone = lerp(_StoneColorB.rgb, _StoneColor.rgb, saturate(id * 1.25));
                stone *= 0.72 + 0.46 * grain;
                stone *= 0.88 + 0.24 * blotch;
                // Unas piedras son más claras que otras, pero todas del mismo granito
                stone *= lerp(0.84 + 0.3 * frac(id * 13.7), 1.0, _Solid);
                // Alguna piedra tira a rojiza o a azulada, como el granito y la andesita del Cusco
                stone = lerp(stone, stone * half3(1.06, 0.98, 0.91), step(0.84, frac(id * 7.31)) * (1.0 - _Solid));
                stone = lerp(stone, stone * half3(0.93, 0.98, 1.04), step(0.88, frac(id * 3.77)) * (1.0 - _Solid));

                // Liquen y humedad: más cerca del suelo y en las caras que miran arriba
                float damp = MuroNoise(wallP * 0.9 + 4.0) * MuroNoise(wallP * 3.1 + 9.0);
                float lichen = saturate((damp * 2.4 - 0.55) + isTop * 0.35) * _Moss;
                stone = lerp(stone, _MossColor.rgb * (0.7 + 0.5 * grain), lichen * 0.75);
                // Churretes de lluvia que oscurecen el muro de arriba abajo
                float streak = MuroNoise(float2(wallU * 2.3, wp.y * 0.25));
                stone *= 1.0 - 0.22 * smoothstep(0.55, 0.9, streak) * (1.0 - isTop);

                // El canto de cada piedra queda a la sombra de la junta
                float cavity = (1.0 - smoothstep(jointWidth * 0.5, jointWidth * 3.0 + 0.1, edge)) * (1.0 - _Solid);
                stone *= 1.0 - 0.4 * cavity;

                half3 mortar = _MortarColor.rgb * (0.7 + 0.5 * grain);
                half3 albedo = lerp(stone, mortar, joint);

                // Relieve: cada piedra se abomba hacia su centro y la junta queda hundida
                float bevel = (1.0 - smoothstep(jointWidth * 0.4, jointWidth * 2.6 + 0.12, edge)) * _Bulge * (1.0 - _Solid);
                float2 dir = normalize(toCenter + 1e-4);
                float3 bump = (axisU * dir.x + axisV * dir.y) * bevel;
                // Rugosidad fina de la cara
                float2 g = float2(MuroNoise(wallP * 34.0 + float2(0.07, 0)) - MuroNoise(wallP * 34.0 - float2(0.07, 0)),
                                  MuroNoise(wallP * 34.0 + float2(0, 0.07)) - MuroNoise(wallP * 34.0 - float2(0, 0.07)));
                bump += (axisU * g.x + axisV * g.y) * 1.6 * (1.0 - joint);
                float3 normalWS = normalize(N - bump * 0.55);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = saturate(albedo);
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = _Smoothness * (1.0 - joint);
                surface.occlusion = 1.0 - 0.6 * joint - 0.25 * bevel * (1.0 - joint);
                surface.emission = half3(0, 0, 0);
                surface.alpha = 1.0;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
