#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// Herramienta de iluminación escénica y volumétrica del Nivel 1 (menú Ayni > Iluminación):
    ///  1. Construye antorchas y braseros ceremoniales incas en las portadas de los 4 conjuntos (K1-K4),
    ///     en los estribos del puente colgante y alrededor de la plaza de Amaru.
    ///  2. Enciende los fogones exteriores (FirePit) de los patios de la aldea y convierte las luces
    ///     interiores estáticas (Luz_Fogon / Luz_Lampara) en luces vivas con AyniFireLight.
    ///  3. Coloca haces de rayos de sol volumétricos (shader Ayni/Rayos de Sol) atravesando la garganta,
    ///     el puente colgante, las portadas y la arena principal.
    ///  4. Instala el controlador AyniDynamicLighting y permite previsualizar y capturar los estados
    ///     climáticos y narrativos (Hora Dorada, Fase 2 de Brasas, Amanecer Ayni y Ocaso de Venganza).
    /// </summary>
    public static class AyniLightingSetup
    {
        private const string EnvFolder = "Assets/Art/Environment";
        private const string SunbeamMatPath = EnvFolder + "/Ayni_RayosSol.mat";
        private const string BrazierMeshPath = EnvFolder + "/Ayni_BraseroInca.asset";
        private const string SunbeamMeshPath = EnvFolder + "/Ayni_HazSolar.asset";
        private const string LightingRootName = "Iluminacion_Andina";

        // Coordenadas locales de los fogones exteriores (FirePit) definidos en AyniVillageBuilder
        private static readonly (string prefix, Vector3 localPos)[] CourtyardFirePits =
        {
            ("K1_", new Vector3(-7.4f, 0.38f, -7.6f)),
            ("K2_", new Vector3(-8.4f, 0.38f, -0.4f)),
            ("K3_", new Vector3(-6.2f, 0.38f,  4.6f)),
            ("K4_", new Vector3(-4.4f, 0.38f,  9.2f)),
            ("K4_", new Vector3( 4.4f, 0.38f,  9.2f)),
        };

        // Semilargos (HalfL) de cada conjunto K1..K4 para situar braseros junto a sus portadas
        private static readonly (string prefix, float halfL)[] CompoundGates =
        {
            ("K1_", 12f),
            ("K2_", 14f),
            ("K3_", 16f),
            ("K4_", 13f),
        };

        [MenuItem("Ayni/Iluminación/1. Construir Iluminación Andina (Antorchas, Fogones y Rayos de Sol)")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Iluminación] Sal del modo Play antes de construir la iluminación.");
                return;
            }

            BuildAll();
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(EnvFolder);
            GameObject envRoot = AyniEnvironmentBuilder.GetOrCreateRoot();

            // Asegurar que la atmósfera base de Hora Dorada esté activa y sincronizada
            AyniAtmosphere.Apply(envRoot);

            Transform old = envRoot.transform.Find(LightingRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(LightingRootName);
            root.transform.SetParent(envRoot.transform, false);

            // Controlador dinámico para el modo Play (Fase 2 y Juicio Ayni)
            if (root.GetComponent<AyniDynamicLighting>() == null)
            {
                root.AddComponent<AyniDynamicLighting>();
            }

            int upgradedHearths = AyniDynamicLighting.UpgradeStaticHearthLights();
            int courtyardFires = BuildCourtyardFirePits(root.transform, envRoot.transform);
            int braziers = BuildBraziersAndTorches(root.transform, envRoot.transform);
            int sunbeams = BuildVolumetricSunbeams(root.transform);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=green>[Ayni Iluminación]</color> Iluminación andina integrada: " +
                      $"{braziers} braseros ceremoniales, {courtyardFires} fogones de patio encendidos, " +
                      $"{upgradedHearths} luces interiores mejoradas y {sunbeams} haces de sol volumétricos.");
        }

        [MenuItem("Ayni/Iluminación/2. Previsualizar Clima: Hora Dorada (Por defecto)")]
        public static void PreviewGoldenHour() => ApplyPreview(AyniLightingPreset.HoraDorada, "Hora Dorada (Por defecto)");

        [MenuItem("Ayni/Iluminación/3. Previsualizar Clima: Segunda Fase del Jefe (Brasas)")]
        public static void PreviewBossPhaseTwo() => ApplyPreview(AyniLightingPreset.FaseDosBrasas, "Segunda Fase del Jefe (Brasas)");

        [MenuItem("Ayni/Iluminación/4. Previsualizar Clima: Amanecer Sagrado (Perdón / Ayni)")]
        public static void PreviewMercyDawn() => ApplyPreview(AyniLightingPreset.AmanecerAyni, "Amanecer Sagrado (Perdón / Ayni)");

        [MenuItem("Ayni/Iluminación/5. Previsualizar Clima: Ocaso de Cenizas (Venganza / Hucha)")]
        public static void PreviewVengeanceDusk() => ApplyPreview(AyniLightingPreset.OcasoVenganza, "Ocaso de Cenizas (Venganza / Hucha)");

        [MenuItem("Ayni/Iluminación/6. Capturar Vistas de Iluminación (DebugCaptures)")]
        public static void CaptureLightingGallery()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            Directory.CreateDirectory(dir);

            // Vista 1: Portada Monumental K4 y fogones en Hora Dorada
            AyniDynamicLighting.ApplyPresetImmediate(AyniLightingPreset.HoraDorada);
            RenderView(new Vector3(549.5f, 12.2f, 527.5f), new Vector3(556.1f, 10.6f, 515.0f), 55f, Path.Combine(dir, "iluminacion_1_hora_dorada.png"));

            // Vista 2: Puente colgante y rayos de sol volumétricos sobre la garganta
            RenderView(new Vector3(629.0f, 8.8f, 454.0f), new Vector3(650.0f, 3.2f, 466.5f), 52f, Path.Combine(dir, "iluminacion_2_rayos_puente.png"));

            // Vista 3: Plaza en Segunda Fase (Brasas)
            AyniDynamicLighting.ApplyPresetImmediate(AyniLightingPreset.FaseDosBrasas);
            RenderView(new Vector3(604.5f, 6.2f, 458.5f), new Vector3(614.0f, 4.2f, 470.0f), 50f, Path.Combine(dir, "iluminacion_3_fase2_brasas.png"));

            // Vista 4: Amanecer Sagrado (Ayni / Perdón)
            AyniDynamicLighting.ApplyPresetImmediate(AyniLightingPreset.AmanecerAyni);
            RenderView(new Vector3(604.5f, 6.2f, 458.5f), new Vector3(614.0f, 4.2f, 470.0f), 50f, Path.Combine(dir, "iluminacion_4_amanecer_ayni.png"));

            // Vista 5: Ocaso de Cenizas (Venganza / Hucha)
            AyniDynamicLighting.ApplyPresetImmediate(AyniLightingPreset.OcasoVenganza);
            RenderView(new Vector3(604.5f, 6.2f, 458.5f), new Vector3(614.0f, 4.2f, 470.0f), 50f, Path.Combine(dir, "iluminacion_5_ocaso_venganza.png"));

            // Restaurar Hora Dorada por defecto
            AyniDynamicLighting.ApplyPresetImmediate(AyniLightingPreset.HoraDorada);
            Debug.Log("<color=green>[Ayni Iluminación]</color> 5 capturas de iluminación guardadas en DebugCaptures/iluminacion_*.png");
        }

        [MenuItem("Ayni/Iluminación/Quitar Iluminación Andina")]
        public static void RemoveLighting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject envRoot = GameObject.Find("Ayni_Entorno");
            if (envRoot != null)
            {
                Transform old = envRoot.transform.Find(LightingRootName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            AyniDynamicLighting.ApplyPresetImmediate(AyniLightingPreset.HoraDorada);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Ayni Iluminación] Iluminación adicional retirada y Hora Dorada restaurada.");
        }

        private static void ApplyPreview(AyniLightingPreset preset, string label)
        {
            AyniDynamicLighting.ApplyPresetImmediate(preset);
            SceneView.RepaintAll();
            Debug.Log($"<color=cyan>[Ayni Iluminación]</color> Previsualización aplicada: <b>{label}</b>.");
        }

        // ───────────────────────── 1. Fogones Exteriores de la Aldea ─────────────────────────

        private static int BuildCourtyardFirePits(Transform lightingRoot, Transform envRoot)
        {
            Transform village = envRoot.Find("Aldea_Inca");
            if (village == null) return 0;

            var parent = new GameObject("Fogones_Patios");
            parent.transform.SetParent(lightingRoot, false);

            int count = 0;
            foreach (var (prefix, localPos) in CourtyardFirePits)
            {
                Transform compound = FindChildStartingWith(village, prefix);
                if (compound == null) continue;

                count++;
                var fireGo = new GameObject($"Luz_FogonPatio_{prefix.TrimEnd('_')}_{count}");
                fireGo.transform.SetParent(parent.transform, false);
                fireGo.transform.position = compound.TransformPoint(localPos);

                var fireLight = fireGo.AddComponent<AyniFireLight>();
                fireLight.Configure(4.2f, 11.5f, true, true, 1.05f);
            }
            return count;
        }

        // ───────────────────────── 2. Braseros y Antorchas Ceremoniales ─────────────────────────

        private static int BuildBraziersAndTorches(Transform lightingRoot, Transform envRoot)
        {
            var parent = new GameObject("Antorchas_Y_Braseros");
            parent.transform.SetParent(lightingRoot, false);

            Mesh brazierMesh = GetOrCreateBrazierMesh();
            Material stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Aldea/Aldea_Losa.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Ayni_Piedra.mat");

            Terrain terrain = Terrain.activeTerrain;
            int count = 0;

            // A. Braseros flanqueando las portadas de los 4 conjuntos incas (K1..K4)
            Transform village = envRoot.Find("Aldea_Inca");
            if (village != null)
            {
                foreach (var (prefix, halfL) in CompoundGates)
                {
                    Transform compound = FindChildStartingWith(village, prefix);
                    if (compound == null) continue;

                    // Entrada y Salida por el exterior de cada portada, con alcance acotado para no saturar
                    // el límite de 4 luces por malla de URP dentro del patio
                    foreach (float zSign in new[] { -1f, 1f })
                    {
                        float zLocal = zSign * (halfL + 2.8f);
                        foreach (float xSide in new[] { -3.25f, 3.25f })
                        {
                            Vector3 worldPos = compound.TransformPoint(new Vector3(xSide, 0f, zLocal));
                            if (terrain != null)
                            {
                                worldPos.y = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
                            }
                            SpawnBrazier(parent.transform, $"Brasero_{prefix.TrimEnd('_')}_{(zSign < 0 ? "Entrada" : "Salida")}_{(xSide < 0 ? "Izq" : "Der")}",
                                worldPos, brazierMesh, stoneMat, 3.2f, 6.2f);
                            count++;
                        }
                    }
                }
            }

            // B. Braseros en los estribos del puente colgante sobre la garganta
            Vector2[] bridgeEnds = { new Vector2(638.3f, 466.5f), new Vector2(663.7f, 466.5f) };
            for (int b = 0; b < bridgeEnds.Length; b++)
            {
                foreach (float zOff in new[] { -2.2f, 2.2f })
                {
                    Vector3 pos = new Vector3(bridgeEnds[b].x, 0f, bridgeEnds[b].y + zOff);
                    if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
                    SpawnBrazier(parent.transform, $"Brasero_Puente_{(b == 0 ? "Oeste" : "Este")}_{(zOff < 0 ? "Sur" : "Norte")}",
                        pos, brazierMesh, stoneMat);
                    count++;
                }
            }

            // C. Cuatro braseros ceremoniales alrededor de la plaza de Amaru (612, 468)
            Vector2 plazaCenter = new Vector2(612f, 468f);
            Vector2[] plazaOffsets =
            {
                new Vector2(-9.5f, -9.5f),
                new Vector2( 9.5f, -9.5f),
                new Vector2(-9.5f,  9.5f),
                new Vector2( 9.5f,  9.5f),
            };
            for (int i = 0; i < plazaOffsets.Length; i++)
            {
                Vector3 pos = new Vector3(plazaCenter.x + plazaOffsets[i].x, 0f, plazaCenter.y + plazaOffsets[i].y);
                if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
                SpawnBrazier(parent.transform, $"Brasero_Plaza_{i + 1}", pos, brazierMesh, stoneMat);
                count++;
            }

            return count;
        }

        private static void SpawnBrazier(Transform parent, string name, Vector3 position, Mesh mesh, Material mat,
            float intensity = 3.6f, float range = 10.5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                if (mat != null) mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
            }

            var lightGo = new GameObject("Luz_Brasero");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.48f, 0f);
            var fire = lightGo.AddComponent<AyniFireLight>();
            fire.Configure(intensity, range, true, true, 0.85f);
        }

        /// <summary>Genera una malla de pedestal trapezoidal inca con cuenco ceremonial superior.</summary>
        private static Mesh GetOrCreateBrazierMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(BrazierMeshPath);
            if (existing != null) return existing;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Cuerpo trapezoidal (base 0.42m -> cima 0.28m, altura 1.15m) + cuenco superior (1.15m -> 1.32m, ancho 0.38m)
            AddFrustumSection(verts, norms, uvs, tris, -0.25f, 1.15f, 0.24f, 0.15f);
            AddFrustumSection(verts, norms, uvs, tris, 1.15f, 1.32f, 0.15f, 0.22f);

            var mesh = new Mesh { name = "Ayni_BraseroInca" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, BrazierMeshPath);
            return mesh;
        }

        private static void AddFrustumSection(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            float y0, float y1, float r0, float r1)
        {
            Vector3[] c0 =
            {
                new Vector3(-r0, y0, -r0), new Vector3(r0, y0, -r0),
                new Vector3( r0, y0,  r0), new Vector3(-r0, y0,  r0)
            };
            Vector3[] c1 =
            {
                new Vector3(-r1, y1, -r1), new Vector3(r1, y1, -r1),
                new Vector3( r1, y1,  r1), new Vector3(-r1, y1,  r1)
            };

            for (int side = 0; side < 4; side++)
            {
                int next = (side + 1) % 4;
                Vector3 a = c0[side], b = c0[next], c = c1[next], d = c1[side];
                Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                int baseIdx = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(new Vector2(0f, y0)); uvs.Add(new Vector2(1f, y0));
                uvs.Add(new Vector2(1f, y1)); uvs.Add(new Vector2(0f, y1));
                tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
            }

            // Tapa superior
            int topIdx = verts.Count;
            verts.Add(c1[0]); verts.Add(c1[1]); verts.Add(c1[2]); verts.Add(c1[3]);
            norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.one); uvs.Add(Vector2.up);
            tris.Add(topIdx); tris.Add(topIdx + 2); tris.Add(topIdx + 1);
            tris.Add(topIdx); tris.Add(topIdx + 3); tris.Add(topIdx + 2);
        }

        // ───────────────────────── 3. Rayos de Sol Volumétricos (God Rays) ─────────────────────────

        private static int BuildVolumetricSunbeams(Transform lightingRoot)
        {
            Shader shader = Shader.Find("Ayni/Rayos de Sol");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[Ayni Iluminación] El shader 'Ayni/Rayos de Sol' no está disponible aún.");
                return 0;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SunbeamMatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Ayni_RayosSol" };
                AssetDatabase.CreateAsset(mat, SunbeamMatPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetColor("_Color", new Color(1.0f, 0.85f, 0.56f, 1.0f));
            mat.SetFloat("_Intensity", 0.38f);
            mat.SetFloat("_RayScale", 6.5f);
            mat.SetFloat("_Speed", 0.35f);
            mat.SetFloat("_SoftDistance", 4.5f);
            EditorUtility.SetDirty(mat);

            Mesh beamMesh = GetOrCreateSunbeamMesh();

            var parent = new GameObject("Rayos_Sol_Volumetricos");
            parent.transform.SetParent(lightingRoot, false);

            // Posiciones estratégicas donde los rayos del sol poniente atraviesan el cañón, el puente y las portadas
            (string name, Vector3 pos, Vector3 scale, float yawOffset)[] beams =
            {
                ("Haz_Garganta_Puente",   new Vector3(654f, 15.0f, 474.0f), new Vector3(28f, 36f, 1f), -25f),
                ("Haz_Cañon_Norte",       new Vector3(638f, 14.0f, 495.0f), new Vector3(34f, 40f, 1f), -18f),
                ("Haz_Cañon_Sur",         new Vector3(645f, 12.5f, 434.0f), new Vector3(30f, 36f, 1f), -30f),
                ("Haz_Plaza_Amaru",       new Vector3(618f, 15.5f, 478.0f), new Vector3(24f, 30f, 1f), -22f),
                ("Haz_Portada_Cazador",   new Vector3(562f, 16.5f, 524.0f), new Vector3(20f, 26f, 1f), -15f),
                ("Haz_Tambo_Camino",      new Vector3(482f, 17.5f, 438.0f), new Vector3(22f, 26f, 1f), -20f),
            };

            // Inclinados siguiendo la dirección de los rayos solares de Hora Dorada
            Quaternion sunBeamRot = Quaternion.Euler(14f, 22f, -38f);

            for (int i = 0; i < beams.Length; i++)
            {
                var go = new GameObject(beams[i].name);
                go.transform.SetParent(parent.transform, false);
                go.transform.position = beams[i].pos;
                go.transform.rotation = Quaternion.Euler(0f, beams[i].yawOffset, 0f) * sunBeamRot;
                go.transform.localScale = beams[i].scale;

                go.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            return beams.Length;
        }

        /// <summary>Plano suave para cada haz volumétrico de luz solar.</summary>
        private static Mesh GetOrCreateSunbeamMesh()
        {
            if (AssetDatabase.LoadMainAssetAtPath(SunbeamMeshPath) != null)
            {
                AssetDatabase.DeleteAsset(SunbeamMeshPath);
            }

            var mesh = new Mesh { name = "Ayni_HazSolar" };
            var verts = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            var norms = new Vector3[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            };
            var uvs = new Vector2[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            var tris = new int[] { 0, 2, 1, 0, 3, 2 };

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, SunbeamMeshPath);
            return mesh;
        }

        private static Transform FindChildStartingWith(Transform parent, string prefix)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(prefix)) return child;
            }
            return null;
        }

        private static void RenderView(Vector3 camPos, Vector3 lookAt, float fov, string filePath)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 prevPos = mainCam.transform.position;
            Quaternion prevRot = mainCam.transform.rotation;
            float prevFov = mainCam.fieldOfView;

            mainCam.transform.position = camPos;
            mainCam.transform.rotation = Quaternion.LookRotation(lookAt - camPos);
            mainCam.fieldOfView = fov;

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            var prevTarget = mainCam.targetTexture;
            var prevActive = RenderTexture.active;
            mainCam.targetTexture = rt;
            mainCam.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            mainCam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            mainCam.transform.SetPositionAndRotation(prevPos, prevRot);
            mainCam.fieldOfView = prevFov;

            Object.DestroyImmediate(rt);
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
#endif
