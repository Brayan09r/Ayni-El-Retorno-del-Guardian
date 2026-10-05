// Terreno andino de AYNI.
// Reproduce la mezcla del terreno original (pasto, tierra, roca y caminos con sus máscaras)
// y le añade lo que hace falta para acantilados y quebradas:
//   · roca automática en las pendientes fuertes, proyectada en triplanar (no se estira en las paredes)
//   · estratos horizontales en la roca
//   · un mapa de control (R = sin camino, G = lecho húmedo, B = roca forzada) que genera la herramienta de entorno
Shader "Ayni/Terreno Andino"
{
    Properties
    {
        [Header(Mascaras)]
        [NoScaleOffset] _MaskRock ("Mascara de roca", 2D) = "black" {}
        [NoScaleOffset] _MaskGrass ("Mascara pasto (blanco) / tierra (negro)", 2D) = "white" {}
        [NoScaleOffset] _MaskPaths ("Mascara de caminos", 2D) = "black" {}
        [NoScaleOffset] _AyniControl ("Control Ayni (R sin camino, G lecho, B roca)", 2D) = "black" {}
        [NoScaleOffset] _AyniControl2 ("Control Ayni 2 (R andenes)", 2D) = "black" {}

        [Header(Capas)]
        [NoScaleOffset] _GrassTex ("Pasto", 2D) = "white" {}
        [NoScaleOffset] [Normal] _GrassNorm ("Pasto normal", 2D) = "bump" {}
        [NoScaleOffset] _GroundTex ("Tierra", 2D) = "white" {}
        [NoScaleOffset] [Normal] _GroundNorm ("Tierra normal", 2D) = "bump" {}
        [NoScaleOffset] _RockTex ("Roca", 2D) = "white" {}
        [NoScaleOffset] [Normal] _RockNorm ("Roca normal", 2D) = "bump" {}
        [NoScaleOffset] _PathTex ("Camino", 2D) = "white" {}
        [NoScaleOffset] [Normal] _PathNorm ("Camino normal", 2D) = "bump" {}

        _GrassTiling ("Repeticion pasto", Float) = 250
        _GroundTiling ("Repeticion tierra", Float) = 90
        _RockTiling ("Repeticion roca (plana)", Float) = 30
        _PathTiling ("Repeticion camino", Float) = 50
        _GroundBias ("Menos tierra (como el original)", Range(0, 1)) = 0.115

        [Header(Acantilados)]
        _RockWorldScale ("Tamano de la roca en paredes (m)", Float) = 9
        _SlopeStart ("Pendiente donde empieza la roca", Range(0, 1)) = 0.30
        _SlopeEnd ("Pendiente de roca completa", Range(0, 1)) = 0.44
        _RockTint ("Tinte de la roca", Color) = (0.86, 0.82, 0.76, 1)
        _StrataStrength ("Fuerza de los estratos", Range(0, 1)) = 0.28
        _StrataScale ("Frecuencia de los estratos", Float) = 1.3
        _WetColor ("Color del lecho humedo", Color) = (0.42, 0.40, 0.36, 1)

        [Header(Andenes y calzada inca)]
        _TerrainRect ("Origen XZ y tamano XZ del terreno", Vector) = (0, 0, 1000, 1000)
        _Paved ("Calzada empedrada (0 = camino de tierra)", Range(0, 1)) = 1
        _PaveSize ("Tamano de las losas (m)", Float) = 1.15
        _PaveColor ("Color de las losas", Color) = (0.60, 0.56, 0.50, 1)
        _WallColor ("Color de los muros de anden", Color) = (0.64, 0.59, 0.52, 1)
        _WallRow ("Alto de las piedras del muro (m)", Float) = 0.7
        _WallStone ("Largo de las piedras del muro (m)", Float) = 0.95
        _TerraceTint ("Tinte del cultivo en los andenes", Color) = (0.72, 1.0, 0.55, 1)

        [Header(Superficie)]
        _Metallic ("Metalico", Range(0, 1)) = 0.12
        _Smoothness ("Suavidad", Range(0, 1)) = 0.32
        _NormalStrength ("Fuerza del relieve", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-100" }
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

            TEXTURE2D(_MaskRock);     SAMPLER(sampler_MaskRock);
            TEXTURE2D(_MaskGrass);
            TEXTURE2D(_MaskPaths);
            TEXTURE2D(_AyniControl);
            TEXTURE2D(_AyniControl2);
            TEXTURE2D(_GrassTex);     SAMPLER(sampler_GrassTex);
            TEXTURE2D(_GrassNorm);
            TEXTURE2D(_GroundTex);
            TEXTURE2D(_GroundNorm);
            TEXTURE2D(_RockTex);
            TEXTURE2D(_RockNorm);
            TEXTURE2D(_PathTex);
            TEXTURE2D(_PathNorm);

            CBUFFER_START(UnityPerMaterial)
                float _GrassTiling;
                float _GroundTiling;
                float _RockTiling;
                float _PathTiling;
                float _GroundBias;
                float _RockWorldScale;
                float _SlopeStart;
                float _SlopeEnd;
                float4 _RockTint;
                float _StrataStrength;
                float _StrataScale;
                float4 _WetColor;
                float _Metallic;
                float _Smoothness;
                float _NormalStrength;
                float4 _TerrainRect;
                float _Paved;
                float _PaveSize;
                float4 _PaveColor;
                float4 _WallColor;
                float _WallRow;
                float _WallStone;
                float4 _TerraceTint;
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
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
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

            // Mismo nodo "Contrast" de Shader Graph que usaba el terreno original
            half3 AyniContrast(half3 c, half contrast)
            {
                half midpoint = 0.2176376; // pow(0.5, 2.2)
                return (c - midpoint) * contrast + midpoint;
            }

            half3 AyniUnpack(TEXTURE2D_PARAM(tex, smp), float2 uv)
            {
                half3 n = UnpackNormal(SAMPLE_TEXTURE2D(tex, smp, uv));
                n.xy *= _NormalStrength;
                return n;
            }

            float AyniHash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float2 AyniHash2(float2 p)
            {
                return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453);
            }

            // Losas irregulares (Voronoi): devuelve el centro de la losa, su número y la distancia a la junta
            void AyniFlagstones(float2 p, out float2 center, out float id, out float edge)
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
                        float2 o = 0.5 + 0.38 * (AyniHash2(cell + g) * 2.0 - 1.0);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; best = cell + g + o; bestId = AyniHash1(cell + g + 17.0); }
                        else if (d < d2) { d2 = d; }
                    }
                }
                center = best;
                id = bestId;
                edge = sqrt(d2) - sqrt(d1);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float2 uv = i.uv;

                // ---------- máscaras ----------
                half maskRock  = SAMPLE_TEXTURE2D(_MaskRock, sampler_MaskRock, uv).r;
                half maskGrass = SAMPLE_TEXTURE2D(_MaskGrass, sampler_MaskRock, uv).r;
                half maskPath  = SAMPLE_TEXTURE2D(_MaskPaths, sampler_MaskRock, uv).r;
                half3 ctrl     = SAMPLE_TEXTURE2D(_AyniControl, sampler_MaskRock, uv).rgb;
                half anden     = SAMPLE_TEXTURE2D(_AyniControl2, sampler_MaskRock, uv).r;

                half slope = 1.0 - N.y;
                half slopeRock = smoothstep(_SlopeStart, _SlopeEnd, slope);
                // Dentro de las quebradas (B) casi cualquier pendiente es roca
                half forcedRock = ctrl.b * smoothstep(0.05, 0.16, slope);
                half cliff = saturate(max(slopeRock, forcedRock)) * (1.0 - anden);
                half rockT = saturate(maskRock * (1.0 - anden) + cliff);
                // En los andenes: lo empinado es muro de piedra, lo llano es cultivo
                half wall = anden * smoothstep(0.10, 0.24, slope);
                half groundT = 1.0 - saturate(maskGrass + _GroundBias);
                half pathBlock = (1.0 - ctrl.r) * (1.0 - cliff) * (1.0 - anden);

                // ---------- calzada inca: losas enteras, decididas por la máscara en el centro de cada losa ----------
                float2 paveCenter; float paveId; float paveEdge;
                AyniFlagstones(i.positionWS.xz / max(_PaveSize, 0.05), paveCenter, paveId, paveEdge);
                float2 centerUV = (paveCenter * _PaveSize - _TerrainRect.xy) / max(_TerrainRect.zw, 1.0);
                half maskAtStone = SAMPLE_TEXTURE2D_LOD(_MaskPaths, sampler_MaskRock, centerUV, 0).r;
                half stone = step(0.30, maskAtStone);
                half pathSoft = saturate(maskPath) * pathBlock;
                half pathT = lerp(pathSoft, stone * pathBlock, _Paved);
                half joint = 1.0 - smoothstep(0.025, 0.085, paveEdge);
                half wet = ctrl.g * (1.0 - cliff);

                // ---------- capas planas (como el terreno original) ----------
                float2 uvGrass = uv * _GrassTiling;
                float2 uvGround = uv * _GroundTiling;
                float2 uvRock = uv * _RockTiling;
                float2 uvPath = uv * _PathTiling;

                half3 grass  = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uvGrass).rgb;
                half3 ground = AyniContrast(SAMPLE_TEXTURE2D(_GroundTex, sampler_GrassTex, uvGround).rgb, 1.3);
                half3 rockTop = AyniContrast(SAMPLE_TEXTURE2D(_RockTex, sampler_GrassTex, uvRock).rgb, 1.42);
                half3 path   = AyniContrast(SAMPLE_TEXTURE2D(_PathTex, sampler_GrassTex, uvPath).rgb, 1.48);

                half3 nGrass  = AyniUnpack(TEXTURE2D_ARGS(_GrassNorm, sampler_GrassTex), uvGrass);
                half3 nGround = AyniUnpack(TEXTURE2D_ARGS(_GroundNorm, sampler_GrassTex), uvGround);
                half3 nRockTop = AyniUnpack(TEXTURE2D_ARGS(_RockNorm, sampler_GrassTex), uvRock);
                half3 nPath   = AyniUnpack(TEXTURE2D_ARGS(_PathNorm, sampler_GrassTex), uvPath);

                // ---------- roca de pared (proyección por los lados, en metros del mundo) ----------
                float3 wp = i.positionWS / max(_RockWorldScale, 0.01);
                float3 bw = pow(abs(N), 3.0);
                bw /= (bw.x + bw.y + bw.z);
                half sideW = saturate((bw.x + bw.z) * 1.25) * cliff;

                half3 rockSide = 0;
                half3 nSideWS = N;
                {
                    half3 cx = SAMPLE_TEXTURE2D(_RockTex, sampler_GrassTex, wp.zy).rgb;
                    half3 cz = SAMPLE_TEXTURE2D(_RockTex, sampler_GrassTex, wp.xy).rgb;
                    half wsum = max(bw.x + bw.z, 1e-4);
                    rockSide = AyniContrast((cx * bw.x + cz * bw.z) / wsum, 1.42);

                    half3 tx = AyniUnpack(TEXTURE2D_ARGS(_RockNorm, sampler_GrassTex), wp.zy);
                    half3 tz = AyniUnpack(TEXTURE2D_ARGS(_RockNorm, sampler_GrassTex), wp.xy);
                    tx = half3(tx.xy + N.zy, abs(tx.z) * N.x);
                    tz = half3(tz.xy + N.xy, abs(tz.z) * N.z);
                    nSideWS = normalize(tx.zyx * bw.x + tz.xyz * bw.z + N * 1e-3);

                    // Estratos: bandas horizontales irregulares
                    float wob = sin(i.positionWS.x * 0.13 + i.positionWS.z * 0.11) * 2.0 + sin(i.positionWS.x * 0.041 - i.positionWS.z * 0.057) * 3.0;
                    half band = 0.5 + 0.5 * sin(i.positionWS.y * _StrataScale + wob);
                    half band2 = 0.5 + 0.5 * sin(i.positionWS.y * _StrataScale * 3.7 + wob * 1.7);
                    half strata = 1.0 - _StrataStrength * (0.65 * band * band + 0.35 * band2);
                    rockSide *= strata * _RockTint.rgb;
                }

                half3 rock = lerp(rockTop, rockSide, sideW);

                // Losas: granito con variación por piedra y el grano de la textura del camino; juntas con tierra y pasto
                half pathLum = dot(path, half3(0.3, 0.5, 0.2));
                half3 slab = _PaveColor.rgb * (0.78 + 0.34 * paveId) * (0.75 + 0.75 * pathLum);
                slab = lerp(slab, slab * half3(1.06, 1.0, 0.9), step(0.6, paveId));
                half3 jointCol = lerp(ground * 0.45, grass * 0.55, step(0.5, frac(paveId * 7.3)));
                half3 paved = lerp(slab, jointCol, joint);
                path = lerp(path, paved, _Paved);

                // Muro de andén: sillería poligonal inca (piedras irregulares encajadas, junta fina)
                float wallU = (abs(N.x) > abs(N.z)) ? i.positionWS.z : i.positionWS.x;
                float2 wallP = float2(wallU, i.positionWS.y);
                float2 wallCenter; float wallId; float wallEdge;
                AyniFlagstones(wallP / float2(max(_WallStone, 0.05), max(_WallRow, 0.05)) + 37.0, wallCenter, wallId, wallEdge);
                half wallJoint = 1.0 - smoothstep(0.02, 0.07, wallEdge);
                half3 wallCol = _WallColor.rgb * (0.82 + 0.28 * wallId) * (0.55 + 1.0 * dot(rockSide, half3(0.33, 0.34, 0.33)));
                wallCol = lerp(wallCol, wallCol * 0.30, wallJoint);

                // ---------- mezcla ----------
                half3 albedo = lerp(grass, ground, groundT);
                albedo = lerp(albedo, ground * _WetColor.rgb, wet);
                albedo = lerp(albedo, albedo * _TerraceTint.rgb, anden * (1.0 - wall));
                albedo = lerp(albedo, rock, rockT);
                albedo = lerp(albedo, path, pathT);
                albedo = lerp(albedo, wallCol, wall);

                half3 nTS = lerp(nGrass, nGround, saturate(groundT + wet));
                nTS = lerp(nTS, nRockTop, rockT);
                nTS = lerp(nTS, nPath, pathT);
                // Normal de las capas planas (proyección desde arriba) llevada al mundo
                half3 nTopWS = normalize(half3(nTS.x + N.x, abs(nTS.z) * N.y, nTS.y + N.z));
                half3 normalWS = normalize(lerp(nTopWS, nSideWS, sideW * rockT));

                // Relieve de las losas (borde biselado hacia la junta) y de las piedras del muro
                float2 toCenter = normalize(paveCenter * _PaveSize - i.positionWS.xz + 1e-4);
                half bevel = (1.0 - smoothstep(0.03, 0.2, paveEdge)) * pathT * _Paved;
                normalWS = normalize(normalWS - half3(toCenter.x, 0, toCenter.y) * bevel * 0.55);
                float3 wallAxis = (abs(N.x) > abs(N.z)) ? float3(0, 0, 1) : float3(1, 0, 0);
                float2 wallTo = normalize((wallCenter - 37.0) * float2(_WallStone, _WallRow) - wallP + 1e-4);
                half wallBevel = (1.0 - smoothstep(0.02, 0.18, wallEdge)) * wall;
                normalWS = normalize(normalWS - (wallAxis * wallTo.x + float3(0, 1, 0) * wallTo.y) * wallBevel * 0.5);

                // ---------- iluminación ----------
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
                surface.metallic = _Metallic * (1.0 - cliff) * (1.0 - wall) * (1.0 - pathT * _Paved);
                surface.specular = half3(0, 0, 0);
                surface.smoothness = lerp(_Smoothness, 0.55, wet) * (1.0 - 0.45 * cliff);
                surface.occlusion = 1.0 - 0.55 * max(joint * pathT * _Paved, wallJoint * wall);
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
