#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Ayni.Enemy;
using Ayni.Story;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// Aldea inca del camino: cuatro conjuntos de casas (kanchas) a caballo sobre el Qhapaq Ñan, entre el punto donde
    /// despierta Yari y la plaza donde espera Amaru. El camino atraviesa el patio de cada conjunto por dos portadas,
    /// así que el patio es el lugar de la pelea, como las salas de Sifu.
    ///
    /// Todo se genera por código: muros de pirca inclinados hacia dentro, vanos trapezoidales con dintel tallado,
    /// hastiales, techos de paja de ichu, colcas redondas y enseres. El terreno se aplana bajo cada conjunto y se
    /// guarda lo que había (Aldea_TerrenoOriginal.bytes) para poder reconstruir o quitar la aldea sin dejar huella.
    ///
    ///   Ayni > Entorno > Construir Aldea Inca     (se puede repetir: borra la anterior y la vuelve a hacer)
    ///   Ayni > Entorno > Quitar Aldea Inca
    /// </summary>
    public static partial class AyniVillageBuilder
    {
        private const string Folder = "Assets/Art/Environment/Aldea";
        private const string MeshPath = Folder + "/Aldea_Mallas.asset";
        private const string BackupPath = Folder + "/Aldea_TerrenoOriginal.bytes";
        private const string EnvRootName = "Ayni_Entorno";
        private const string RootName = "Aldea_Inca";
        private const string CaptureFolder = "DebugCaptures";

        private const float FlatMargin = 1.8f; // terreno llano más allá de los muros
        private const float Blend = 7f;        // y desde ahí se funde con el relieve original
        private const float Buried = 0.6f;     // lo que se hunden los cimientos, por si el suelo no queda perfecto
        private const float PlatformY = 0.26f; // altura de la plataforma de las casas sobre el patio

        // Dónde despierta Yari (sobre el camino, a unos 400 m de la plaza) y hacia dónde mira
        private static readonly Vector2 StartPoint = new Vector2(247.9f, 539.5f);
        private const float StartYaw = 85.9f;
        // Donde estaba antes de existir la aldea (lo usa "Quitar Aldea Inca")
        private static readonly Vector2 OldStartPoint = new Vector2(612f, 468f);

        private sealed class Site
        {
            public string id, title;
            public Vector2 center;   // x, z del mundo, sobre el eje del camino
            public float yaw;        // dirección de la marcha de Yari (hacia la plaza)
            public float length, width;
            public int seed;
            public float y;          // altura del patio (se mide en el terreno)
            public float HalfL => length * 0.5f;
            public float HalfW => width * 0.5f;
        }

        private static readonly Site[] Sites =
        {
            // Distancia a la plaza siguiendo el camino: inicio 630 m, K1 495, K2 378, K3 250, K4 87.
            // Entre un conjunto y el siguiente hay de 115 a 165 m, con los obstáculos de AyniVillageObstacles.cs.
            new Site { id = "K1", title = "Puesto de Chasquis", center = new Vector2(330.9f, 468.3f), yaw = 144.2f, length = 24f, width = 24f, seed = 101 },
            new Site { id = "K2", title = "Kancha de los Tejedores", center = new Vector2(392.1f, 387.7f), yaw = 116.1f, length = 28f, width = 26f, seed = 202 },
            new Site { id = "K3", title = "Tambo del Camino", center = new Vector2(477.9f, 433.9f), yaw = 18.3f, length = 32f, width = 28f, seed = 303 },
            new Site { id = "K4", title = "Portada del Cazador", center = new Vector2(556.1f, 519.1f), yaw = 141.1f, length = 26f, width = 26f, seed = 404 },
        };

        private sealed class Compound
        {
            public Site site;
            public System.Random rng;
            public readonly MeshBuf pirca = new MeshBuf(), fina = new MeshBuf(), losa = new MeshBuf(), paja = new MeshBuf(), fleco = new MeshBuf();
            public readonly MeshBuf madera = new MeshBuf(), textil = new MeshBuf(), ceramica = new MeshBuf(), barro = new MeshBuf();
            public readonly List<Pose> spawns = new List<Pose>();
            public readonly List<Pose> interiors = new List<Pose>();
            public readonly List<(Vector3 center, Vector3 size, float yaw)> blockers = new List<(Vector3, Vector3, float)>();
            public readonly List<HouseBuild> houses = new List<HouseBuild>();
            // Tramos de los muros laterales que ocupa una casa (su trasera hace de cerco): ahí el muro se interrumpe
            public readonly List<(bool left, float z0, float z1)> sideGaps = new List<(bool, float, float)>();
            public float Rand(float min, float max) => VillageGeo.Range(rng, min, max);
        }

        /// <summary>
        /// Una casa es un objeto aparte dentro de su conjunto: así cada una recibe solo la luz de su fogón y su colisión
        /// puede ser distinta de lo que se ve (ver <see cref="House"/>).
        /// </summary>
        private sealed class HouseBuild
        {
            public string name;
            public readonly MeshBuf pirca = new MeshBuf(), losa = new MeshBuf(), paja = new MeshBuf(), fleco = new MeshBuf();
            public readonly MeshBuf madera = new MeshBuf(), textil = new MeshBuf(), ceramica = new MeshBuf(), barro = new MeshBuf();
            public readonly MeshBuf tierra = new MeshBuf(), colision = new MeshBuf();
            public readonly List<Vector3> lights = new List<Vector3>();
        }

        private sealed class Materials
        {
            public Material pirca, fina, losa, roca, paja, fleco, madera, quemado, textil, ceramica, barro, tierra;
        }

        // ═════════════════════════ Menús ═════════════════════════

        [MenuItem("Ayni/Entorno/Construir Aldea Inca")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Aldea] Sal del modo Play antes de construir la aldea.");
                return;
            }
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[Ayni Aldea] No hay un terreno activo en la escena.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Ayni Aldea", "Preparando el terreno...", 0.1f);
                EnsureFolder();
                GameObject envRoot = AyniEnvironmentBuilder.GetOrCreateRoot();
                Transform old = envRoot.transform.Find(RootName);
                var keptRivals = RescueRivals(old);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);

                RestoreTerrain(terrain);
                foreach (Site site in Sites) site.y = MeasureRoadHeight(terrain, site);
                FlattenTerrain(terrain);

                EditorUtility.DisplayProgressBar("Ayni Aldea", "Materiales y texturas...", 0.3f);
                Materials mats = BuildMaterials();

                var root = new GameObject(RootName);
                root.transform.SetParent(envRoot.transform, false);
                var meshes = new List<Mesh>();
                int movedRivals = 0;
                for (int i = 0; i < Sites.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Ayni Aldea", "Levantando " + Sites[i].title + "...", 0.4f + 0.12f * i);
                    AyniEncounterSite encounter = BuildCompound(Sites[i], i + 1, root.transform, mats, meshes);
                    if (keptRivals.TryGetValue(i + 1, out var kept))
                    {
                        encounter.SetRivals(kept.rivals, kept.hide);
                        EditorUtility.SetDirty(encounter);
                        // Cada rival vuelve al punto que ocupaba (si el conjunto ha cambiado de sitio, se muda con él)
                        for (int r = 0; r < kept.rivals.Count; r++)
                        {
                            GameObject rival = kept.rivals[r];
                            if (rival == null) continue;
                            Transform slot = kept.slots[r].interior ? encounter.GetInteriorPoint(kept.slots[r].index) : encounter.GetSpawnPoint(kept.slots[r].index);
                            rival.transform.SetPositionAndRotation(slot.position, slot.rotation);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(rival.transform);
                            EditorUtility.SetDirty(rival.transform);
                            movedRivals++;
                        }
                    }
                }
                BuildStartMarker(terrain, root.transform, mats, meshes);
                EditorUtility.DisplayProgressBar("Ayni Aldea", "Obstáculos del camino...", 0.92f);
                BuildObstacles(terrain, root.transform, mats, meshes);

                AssetDatabase.CreateAsset(meshes[0], MeshPath);
                for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], MeshPath);

                PlaceYari(terrain, StartPoint, StartYaw);

                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"<color=green>[Ayni Aldea]</color> {Sites.Length} conjuntos y {Obstacles.Length} obstáculos levantados sobre el camino ({meshes.Count} mallas). " +
                          $"{movedRivals} rivales recolocados en sus puntos. Yari despierta ahora en ({StartPoint.x:F0}, {StartPoint.y:F0}).");
                CheckObstacles();
            }
            catch (Exception e)
            {
                Debug.LogError("[Ayni Aldea] Error al construir la aldea: " + e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Ayni/Entorno/Quitar Aldea Inca")]
        public static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            GameObject envRoot = GameObject.Find(EnvRootName);
            Transform old = envRoot != null ? envRoot.transform.Find(RootName) : null;
            RescueRivals(old);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
            RestoreTerrain(terrain);
            PlaceYari(terrain, OldStartPoint, 0f);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=green>[Ayni Aldea]</color> Aldea quitada: terreno restaurado y Yari de vuelta en la plaza.");
        }

        /// <summary>
        /// Antes de borrar la aldea vieja: recuerda qué rivales tenía asignados cada conjunto y saca de ella a cualquier
        /// rival que alguien hubiera colgado dentro, para que reconstruir nunca se lleve por delante el trabajo de poblarla.
        /// </summary>
        private static Dictionary<int, (List<GameObject> rivals, bool hide, List<(bool interior, int index)> slots)> RescueRivals(Transform oldRoot)
        {
            var kept = new Dictionary<int, (List<GameObject> rivals, bool hide, List<(bool interior, int index)> slots)>();
            if (oldRoot == null) return kept;
            foreach (AyniEncounterSite site in oldRoot.GetComponentsInChildren<AyniEncounterSite>(true))
            {
                var rivals = new List<GameObject>(site.Rivals);
                var slots = new List<(bool interior, int index)>();
                foreach (GameObject rival in rivals)
                {
                    // ¿En qué punto de aparición estaba? (el más cercano, del patio o de dentro de una casa)
                    bool interior = false;
                    int index = 0;
                    float best = float.MaxValue;
                    if (rival != null)
                    {
                        for (int i = 0; i < site.SpawnPoints.Count; i++)
                        {
                            if (site.SpawnPoints[i] == null) continue;
                            float d = (site.SpawnPoints[i].position - rival.transform.position).sqrMagnitude;
                            if (d < best) { best = d; index = i; interior = false; }
                        }
                        for (int i = 0; i < site.InteriorPoints.Count; i++)
                        {
                            if (site.InteriorPoints[i] == null) continue;
                            float d = (site.InteriorPoints[i].position - rival.transform.position).sqrMagnitude;
                            if (d < best) { best = d; index = i; interior = true; }
                        }
                    }
                    slots.Add((interior, index));
                }
                kept[site.Order] = (rivals, site.HideRivalsUntilEntered, slots);
            }
            foreach (EnemyController enemy in oldRoot.GetComponentsInChildren<EnemyController>(true))
            {
                enemy.transform.SetParent(null, true);
                Debug.LogWarning("[Ayni Aldea] " + enemy.name + " estaba dentro de Aldea_Inca: se ha sacado a la raíz de la escena para no borrarlo.");
            }
            return kept;
        }

        // ═════════════════════════ Terreno ═════════════════════════

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Environment")) AssetDatabase.CreateFolder("Assets/Art", "Environment");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Environment", "Aldea");
        }

        private static float Ground(Terrain terrain, float x, float z)
        {
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        private static Vector2 Forward(float yaw) => new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
        private static Vector2 Right(float yaw) => new Vector2(Mathf.Cos(yaw * Mathf.Deg2Rad), -Mathf.Sin(yaw * Mathf.Deg2Rad));

        /// <summary>Altura media del camino a lo largo del conjunto: ahí queda el patio.</summary>
        private static float MeasureRoadHeight(Terrain terrain, Site site)
        {
            Vector2 f = Forward(site.yaw);
            float sum = 0f;
            const int samples = 9;
            for (int i = 0; i < samples; i++)
            {
                float along = Mathf.Lerp(-site.HalfL, site.HalfL, i / (samples - 1f));
                sum += Ground(terrain, site.center.x + f.x * along, site.center.y + f.y * along);
            }
            return sum / samples;
        }

        private static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Aplana el suelo bajo cada conjunto y guarda antes lo que había.</summary>
        private static void FlattenTerrain(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            int res = data.heightmapResolution;
            Vector3 size = data.size;
            Vector3 origin = terrain.transform.position;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)'A'); writer.Write((byte)'Y'); writer.Write((byte)'N'); writer.Write((byte)'2');
                writer.Write(res);
                writer.Write(origin.y); // si alguien restaura el terreno original (que está 40 m más arriba), esta copia ya no vale
                writer.Write(Sites.Length);

                foreach (Site site in Sites)
                {
                    float reachX = site.HalfW + FlatMargin + Blend, reachZ = site.HalfL + FlatMargin + Blend;
                    float radius = Mathf.Sqrt(reachX * reachX + reachZ * reachZ) + 1f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt((site.center.x - radius - origin.x) / size.x * (res - 1)), 0, res - 1);
                    int x1 = Mathf.Clamp(Mathf.CeilToInt((site.center.x + radius - origin.x) / size.x * (res - 1)), 0, res - 1);
                    int z0 = Mathf.Clamp(Mathf.FloorToInt((site.center.y - radius - origin.z) / size.z * (res - 1)), 0, res - 1);
                    int z1 = Mathf.Clamp(Mathf.CeilToInt((site.center.y + radius - origin.z) / size.z * (res - 1)), 0, res - 1);
                    int w = x1 - x0 + 1, h = z1 - z0 + 1;

                    float[,] heights = data.GetHeights(x0, z0, w, h);
                    writer.Write(x0); writer.Write(z0); writer.Write(w); writer.Write(h);
                    for (int z = 0; z < h; z++)
                    {
                        for (int x = 0; x < w; x++) writer.Write(heights[z, x]);
                    }

                    Vector2 f = Forward(site.yaw), r = Right(site.yaw);
                    float target = (site.y - origin.y) / size.y;
                    for (int z = 0; z < h; z++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float wx = origin.x + (x0 + x) * size.x / (res - 1) - site.center.x;
                            float wz = origin.z + (z0 + z) * size.z / (res - 1) - site.center.y;
                            float lx = wx * r.x + wz * r.y, lz = wx * f.x + wz * f.y;
                            float dx = Mathf.Max(0f, Mathf.Abs(lx) - (site.HalfW + FlatMargin));
                            float dz = Mathf.Max(0f, Mathf.Abs(lz) - (site.HalfL + FlatMargin));
                            float t = 1f - Smooth(0f, Blend, Mathf.Sqrt(dx * dx + dz * dz));
                            if (t <= 0f) continue;
                            heights[z, x] = Mathf.Lerp(heights[z, x], target, t);
                        }
                    }
                    data.SetHeights(x0, z0, heights);
                }

                writer.Flush();
                File.WriteAllBytes(BackupPath, stream.ToArray());
            }

            terrain.Flush();
            EditorUtility.SetDirty(data);
            AssetDatabase.ImportAsset(BackupPath);
        }

        /// <summary>Devuelve el terreno a como estaba antes de la aldea (si hay copia guardada).</summary>
        private static void RestoreTerrain(Terrain terrain)
        {
            if (!File.Exists(BackupPath)) return;
            TerrainData data = terrain.terrainData;
            byte[] bytes = File.ReadAllBytes(BackupPath);
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                byte version = 0;
                if (bytes.Length < 16 || reader.ReadByte() != 'A' || reader.ReadByte() != 'Y' || reader.ReadByte() != 'N' ||
                    ((version = reader.ReadByte()) != 'V' && version != '2'))
                {
                    Debug.LogWarning("[Ayni Aldea] La copia del terreno no es válida; no se restaura.");
                    return;
                }
                int res = reader.ReadInt32();
                float savedOriginY = version == '2' ? reader.ReadSingle() : terrain.transform.position.y;
                if (res != data.heightmapResolution || Mathf.Abs(savedOriginY - terrain.transform.position.y) > 0.01f)
                {
                    Debug.LogWarning("[Ayni Aldea] La copia del terreno es de otro relieve (¿se restauró el terreno original?); se descarta.");
                    reader.Close();
                    AssetDatabase.DeleteAsset(BackupPath);
                    return;
                }
                int count = reader.ReadInt32();
                // Se restauran en orden inverso por si dos zonas se solapan
                var regions = new List<(int x0, int z0, float[,] heights)>();
                for (int i = 0; i < count; i++)
                {
                    int x0 = reader.ReadInt32(), z0 = reader.ReadInt32(), w = reader.ReadInt32(), h = reader.ReadInt32();
                    var heights = new float[h, w];
                    for (int z = 0; z < h; z++)
                    {
                        for (int x = 0; x < w; x++) heights[z, x] = reader.ReadSingle();
                    }
                    regions.Add((x0, z0, heights));
                }
                for (int i = regions.Count - 1; i >= 0; i--) data.SetHeights(regions[i].x0, regions[i].z0, regions[i].heights);
            }
            terrain.Flush();
            EditorUtility.SetDirty(data);
            AssetDatabase.DeleteAsset(BackupPath);
        }

        // ═════════════════════════ Materiales ═════════════════════════

        private static Material GetMaterial(string name, string shaderName)
        {
            string path = Folder + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LitMaterial(string name, Texture2D texture, Color tint, bool doubleSided, bool cutout)
        {
            Material material = GetMaterial(name, "Universal Render Pipeline/Lit");
            AyniReforestation.ConfigureMaterial(material, doubleSided, cutout);
            material.SetColor("_BaseColor", tint);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
            }
            return material;
        }

        private static Materials BuildMaterials()
        {
            var m = new Materials();

            m.pirca = GetMaterial("Aldea_Pirca", "Ayni/Muro Inca");
            m.pirca.SetColor("_StoneColor", new Color(0.50f, 0.48f, 0.445f));
            m.pirca.SetColor("_StoneColorB", new Color(0.385f, 0.37f, 0.345f));
            m.pirca.SetColor("_MortarColor", new Color(0.20f, 0.165f, 0.13f));
            m.pirca.SetColor("_MossColor", new Color(0.33f, 0.36f, 0.19f));
            // Yari mide 1,30 m: las piedras se dimensionan a su escala, no a la de un adulto
            m.pirca.SetFloat("_StoneLength", 0.27f);
            m.pirca.SetFloat("_StoneHeight", 0.175f);
            m.pirca.SetFloat("_Joint", 0.13f);
            m.pirca.SetFloat("_Fine", 0f);
            m.pirca.SetFloat("_Bulge", 0.85f);
            m.pirca.SetFloat("_Moss", 0.4f);
            m.pirca.SetFloat("_Smoothness", 0.06f);

            m.fina = GetMaterial("Aldea_Silleria", "Ayni/Muro Inca");
            m.fina.SetColor("_StoneColor", new Color(0.60f, 0.58f, 0.545f));
            m.fina.SetColor("_StoneColorB", new Color(0.52f, 0.50f, 0.47f));
            m.fina.SetColor("_MortarColor", new Color(0.13f, 0.12f, 0.11f));
            m.fina.SetColor("_MossColor", new Color(0.36f, 0.38f, 0.22f));
            m.fina.SetFloat("_StoneLength", 0.52f);
            m.fina.SetFloat("_StoneHeight", 0.30f);
            m.fina.SetFloat("_Joint", 0.05f);
            m.fina.SetFloat("_Fine", 1f);
            m.fina.SetFloat("_Bulge", 0.6f);
            m.fina.SetFloat("_Moss", 0.22f);
            m.fina.SetFloat("_Smoothness", 0.14f);

            // Piedra de una sola pieza: dinteles, umbrales, escalones y poyos
            m.losa = GetMaterial("Aldea_Losa", "Ayni/Muro Inca");
            m.losa.SetColor("_StoneColor", new Color(0.50f, 0.485f, 0.455f));
            m.losa.SetColor("_StoneColorB", new Color(0.44f, 0.425f, 0.40f));
            m.losa.SetColor("_MortarColor", new Color(0.13f, 0.12f, 0.11f));
            m.losa.SetColor("_MossColor", new Color(0.36f, 0.38f, 0.22f));
            m.losa.SetFloat("_Fine", 1f);
            m.losa.SetFloat("_Solid", 1f);
            m.losa.SetFloat("_Moss", 0.3f);
            m.losa.SetFloat("_Smoothness", 0.12f);

            Texture2D thatch = VillageTextures.Thatch(Folder + "/Aldea_Paja.png", Folder + "/Aldea_Paja_Normal.png", out Texture2D thatchNormal);
            m.paja = LitMaterial("Aldea_Paja", thatch, Color.white, false, false);
            if (thatchNormal != null)
            {
                m.paja.SetTexture("_BumpMap", thatchNormal);
                m.paja.SetFloat("_BumpScale", 1.1f);
                m.paja.EnableKeyword("_NORMALMAP");
            }
            m.fleco = LitMaterial("Aldea_PajaFleco", VillageTextures.Fringe(Folder + "/Aldea_PajaFleco.png"), Color.white, true, true);
            m.madera = LitMaterial("Aldea_Madera", VillageTextures.Wood(Folder + "/Aldea_Madera.png"), Color.white, false, false);
            m.textil = LitMaterial("Aldea_Textil", VillageTextures.Textiles(Folder + "/Aldea_Textiles.png"), new Color(0.86f, 0.84f, 0.8f), true, false);
            m.ceramica = LitMaterial("Aldea_Ceramica", VillageTextures.Ceramic(Folder + "/Aldea_Ceramica.png"), Color.white, false, false);
            m.barro = LitMaterial("Aldea_Barro", null, new Color(0.115f, 0.095f, 0.075f), false, false);
            m.tierra = LitMaterial("Aldea_Tierra", null, new Color(0.30f, 0.235f, 0.17f), false, false); // suelo apisonado de los cuartos

            // Obstáculos del camino: peñascos (piedra natural, sin aparejo) y madera quemada
            m.roca = GetMaterial("Aldea_Roca", "Ayni/Muro Inca");
            m.roca.SetColor("_StoneColor", new Color(0.43f, 0.40f, 0.36f));
            m.roca.SetColor("_StoneColorB", new Color(0.36f, 0.335f, 0.30f));
            m.roca.SetColor("_MortarColor", new Color(0.13f, 0.12f, 0.11f));
            m.roca.SetColor("_MossColor", new Color(0.31f, 0.35f, 0.19f));
            m.roca.SetFloat("_Fine", 1f);
            m.roca.SetFloat("_Solid", 1f);
            m.roca.SetFloat("_Moss", 0.55f);
            m.roca.SetFloat("_Smoothness", 0.05f);
            m.quemado = LitMaterial("Aldea_MaderaQuemada", m.madera.mainTexture as Texture2D, new Color(0.19f, 0.165f, 0.15f), false, false);
            return m;
        }

        // ═════════════════════════ Conjuntos ═════════════════════════

        private static AyniEncounterSite BuildCompound(Site site, int order, Transform root, Materials mats, List<Mesh> meshes)
        {
            var c = new Compound { site = site, rng = new System.Random(site.seed) };
            switch (site.id)
            {
                case "K1": LayoutChasquis(c); break;
                case "K2": LayoutTejedores(c); break;
                case "K3": LayoutTambo(c); break;
                default: LayoutPortada(c); break;
            }

            var go = new GameObject(site.id + "_" + site.title.Replace(' ', '_'));
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(new Vector3(site.center.x, site.y, site.center.y), Quaternion.Euler(0f, site.yaw, 0f));

            AddPart(go.transform, "Muros", c.pirca, mats.pirca, true, true, site.id, meshes);
            AddPart(go.transform, "Canteria", c.fina, mats.fina, true, true, site.id, meshes);
            AddPart(go.transform, "Losas", c.losa, mats.losa, true, true, site.id, meshes);
            AddPart(go.transform, "Techos", c.paja, mats.paja, false, true, site.id, meshes);
            AddPart(go.transform, "Flecos", c.fleco, mats.fleco, false, false, site.id, meshes);
            AddPart(go.transform, "Madera", c.madera, mats.madera, false, true, site.id, meshes);
            AddPart(go.transform, "Textiles", c.textil, mats.textil, false, true, site.id, meshes);
            AddPart(go.transform, "Ceramica", c.ceramica, mats.ceramica, true, true, site.id, meshes);
            AddPart(go.transform, "Suelos", c.barro, mats.barro, false, false, site.id, meshes);

            // Las casas: se puede entrar en ellas
            foreach (HouseBuild house in c.houses)
            {
                var houseGo = new GameObject(house.name);
                houseGo.transform.SetParent(go.transform, false);
                string prefix = site.id + "_" + house.name;
                AddPart(houseGo.transform, "Muros", house.pirca, mats.pirca, false, true, prefix, meshes);
                AddPart(houseGo.transform, "Dinteles", house.losa, mats.losa, false, true, prefix, meshes);
                AddPart(houseGo.transform, "Techo", house.paja, mats.paja, false, true, prefix, meshes);
                AddPart(houseGo.transform, "Flecos", house.fleco, mats.fleco, false, false, prefix, meshes);
                AddPart(houseGo.transform, "Madera", house.madera, mats.madera, false, true, prefix, meshes);
                AddPart(houseGo.transform, "Textiles", house.textil, mats.textil, false, true, prefix, meshes);
                AddPart(houseGo.transform, "Ceramica", house.ceramica, mats.ceramica, false, true, prefix, meshes);
                AddPart(houseGo.transform, "Suelo", house.tierra, mats.tierra, false, false, prefix, meshes);
                AddPart(houseGo.transform, "Ceniza", house.barro, mats.barro, false, false, prefix, meshes);
                AddCollision(houseGo.transform, house.colision, prefix, meshes);
                for (int i = 0; i < house.lights.Count; i++)
                {
                    var lightGo = new GameObject(i == 0 ? "Luz_Fogon" : "Luz_Lampara_" + i);
                    lightGo.transform.SetParent(houseGo.transform, false);
                    lightGo.transform.localPosition = house.lights[i];
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.62f, 0.32f);
                    light.intensity = i == 0 ? 3f : 2.2f;
                    light.range = 7.5f;
                    light.shadows = LightShadows.None;
                }
            }

            // Tendederos y leña: estorbos del patio
            var blockers = new GameObject("Bloqueos");
            blockers.transform.SetParent(go.transform, false);
            foreach (var (center, size, yaw) in c.blockers)
            {
                var box = new GameObject("Bloqueo");
                box.transform.SetParent(blockers.transform, false);
                box.transform.localPosition = center;
                box.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                box.AddComponent<BoxCollider>().size = size;
            }

            // Puntos donde pueden esperar los rivales, para quien pueble el nivel
            var encounter = new GameObject("Encuentro_" + site.id);
            encounter.transform.SetParent(go.transform, false);
            var siteComponent = encounter.AddComponent<AyniEncounterSite>();
            Transform entry = Marker(encounter.transform, "Entrada", new Vector3(0f, 0f, -site.HalfL - 2f), 0f);
            Transform exit = Marker(encounter.transform, "Salida", new Vector3(0f, 0f, site.HalfL + 2f), 0f);
            var points = new Transform[c.spawns.Count];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = Marker(encounter.transform, "PuntoRival_" + (i + 1), c.spawns[i].position, c.spawns[i].rotation.eulerAngles.y);
            }
            siteComponent.Setup(site.title, order, new Vector3(site.width - 1.6f, 5f, site.length - 1.6f), entry, exit, points);
            var inside = new Transform[c.interiors.Count];
            for (int i = 0; i < inside.Length; i++)
            {
                inside[i] = Marker(encounter.transform, "PuntoInterior_" + (i + 1), c.interiors[i].position, c.interiors[i].rotation.eulerAngles.y);
            }
            siteComponent.SetInteriorPoints(inside);
            return siteComponent;
        }

        private static Transform Marker(Transform parent, string name, Vector3 localPosition, float localYaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            return go.transform;
        }

        private static void AddPart(Transform parent, string name, MeshBuf buf, Material material, bool collider, bool castShadows,
            string prefix, List<Mesh> meshes)
        {
            if (buf.IsEmpty) return;
            Mesh mesh = buf.ToMesh(prefix + "_" + name);
            meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        /// <summary>Malla que solo sirve para chocar (no se dibuja).</summary>
        private static void AddCollision(Transform parent, MeshBuf buf, string prefix, List<Mesh> meshes)
        {
            if (buf.IsEmpty) return;
            Mesh mesh = buf.ToMesh(prefix + "_Colision");
            meshes.Add(mesh);
            var go = new GameObject("Colision");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
        }

        // ───────────────────────── Trazados ─────────────────────────

        /// <summary>K1 · Puesto de chasquis: dos casas, una colca y el fogón de los mensajeros.</summary>
        private static void LayoutChasquis(Compound c)
        {
            float hw = c.site.HalfW, hl = c.site.HalfL;
            Gate(c, -hl, false);
            Gate(c, hl, true);

            House(c, -hw + 2.0f, 0.5f, 90f, 7.6f, 4.4f, 2.3f, 1, 0);
            House(c, hw - 2.0f, 4.2f, -90f, 6.6f, 4.4f, 2.3f, 1, 1);
            Enclosure(c, 1.9f); // después de las casas: el cerco se interrumpe donde hay una
            Qolqa(c, 8.8f, -6.3f, 1.6f, 2.5f, -90f);

            FirePit(c, -7.4f, -7.6f);
            Bench(c, -10.95f, -7.8f, 90f, 1.9f);
            Jar(c, 7.1f, 0.2f, 0.95f, true);
            Jar(c, 7.5f, -0.7f, 0.7f, false);
            Jar(c, -7.2f, 5.6f, 0.85f, true);
            Firewood(c, 9.6f, 8.8f, 0f);
            Rack(c, -6.6f, 8.6f, 78f, 2.3f, 3);
            Spawn(c, 0f, hl - 3.2f, 180f);
        }

        /// <summary>K2 · Kancha de los tejedores: tres casas y los tendederos de tejidos.</summary>
        private static void LayoutTejedores(Compound c)
        {
            float hw = c.site.HalfW, hl = c.site.HalfL;
            Gate(c, -hl, false);
            Gate(c, hl, true);

            House(c, -hw + 2.0f, -7.3f, 90f, 7.2f, 4.4f, 2.3f, 1, 2);
            House(c, -hw + 2.0f, 6.6f, 90f, 7.8f, 4.4f, 2.3f, 1, 0);
            House(c, hw - 2.0f, -1.2f, -90f, 9.6f, 4.4f, 2.35f, 2, 1);
            Enclosure(c, 1.9f); // después de las casas: el cerco se interrumpe donde hay una

            Rack(c, 7.4f, 9.6f, 96f, 2.6f, 0);
            Rack(c, 9.4f, 11.2f, 8f, 2.2f, 2);
            Rack(c, 7.2f, -10.4f, 84f, 2.4f, 1);
            FirePit(c, -8.4f, -0.4f);
            Bench(c, -11.95f, -0.4f, 90f, 1.7f);
            Jar(c, -7.4f, -3.2f, 0.9f, true);
            Jar(c, -7.9f, 2.4f, 0.75f, false);
            Jar(c, 7.6f, 4.4f, 0.95f, true);
            Jar(c, 8.2f, 5.1f, 0.6f, false);
            Mortar(c, -7.0f, 11.6f, 30f);
            Spawn(c, 0f, hl - 3.2f, 180f);
        }

        /// <summary>K3 · Tambo: una kallanka (nave larga de tres puertas), dos casas y dos colcas.</summary>
        private static void LayoutTambo(Compound c)
        {
            float hw = c.site.HalfW, hl = c.site.HalfL;
            Gate(c, -hl, false);
            Gate(c, hl, true);

            House(c, hw - 2.5f, 0f, -90f, 16.5f, 5.4f, 2.6f, 3, 1);
            House(c, -hw + 2.0f, -9.6f, 90f, 7.0f, 4.4f, 2.3f, 1, 0);
            House(c, -hw + 2.0f, 9.2f, 90f, 7.4f, 4.4f, 2.3f, 1, 2);
            Enclosure(c, 1.9f); // después de las casas: el cerco se interrumpe donde hay una
            Qolqa(c, -10.9f, -2.3f, 1.5f, 2.4f, 90f);
            Qolqa(c, -10.9f, 1.7f, 1.5f, 2.4f, 90f);

            FirePit(c, -6.2f, 4.6f);
            Bench(c, 12.95f, -11.6f, 90f, 2.0f);
            Bench(c, 12.95f, 11.6f, 90f, 2.0f);
            Jar(c, 7.9f, -2.6f, 1.0f, true);
            Jar(c, 7.7f, 2.9f, 0.9f, true);
            Jar(c, 7.7f, 3.75f, 0.65f, false);
            Jar(c, -7.6f, -5.4f, 0.8f, true);
            Firewood(c, -11.6f, 14.1f, 90f);
            Rack(c, -6.8f, -13.4f, 90f, 2.4f, 3);
            Mortar(c, -7.4f, -1.0f, -20f);
            Spawn(c, 0f, hl - 3.4f, 180f);
        }

        /// <summary>K4 · Portada del Cazador: la salida es una portada monumental de doble jamba entre dos torreones.</summary>
        private static void LayoutPortada(Compound c)
        {
            float hw = c.site.HalfW, hl = c.site.HalfL;
            Gate(c, -hl, false);
            MonumentalGate(c, hl);

            House(c, -hw + 2.0f, -4.6f, 90f, 7.6f, 4.4f, 2.3f, 1, 0);
            House(c, hw - 2.0f, -3.2f, -90f, 8.6f, 4.4f, 2.35f, 2, 2);
            Enclosure(c, 2.3f); // después de las casas: el cerco se interrumpe donde hay una
            Tower(c, -8.1f, hl - 0.4f, 2.2f, 4.4f, 160f);
            Tower(c, 8.1f, hl - 0.4f, 2.2f, 4.4f, -160f);

            FirePit(c, -4.4f, 9.2f);
            FirePit(c, 4.4f, 9.2f);
            Rack(c, -6.4f, 5.2f, 90f, 2.2f, 0);
            Rack(c, 6.4f, 5.2f, 90f, 2.2f, 0);
            Jar(c, -7.3f, -9.6f, 0.95f, true);
            Jar(c, 7.4f, 2.4f, 0.85f, true);
            Bench(c, -11.95f, 3.0f, 90f, 1.8f);
            Spawn(c, 0f, hl - 4.5f, 180f);
            Spawn(c, -2.6f, hl - 6.5f, 170f);
            Spawn(c, 2.6f, hl - 6.5f, 190f);
        }

        // ───────────────────────── Piezas de un conjunto ─────────────────────────

        private static void Spawn(Compound c, float x, float z, float yaw)
        {
            c.spawns.Add(new Pose(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f)));
        }

        /// <summary>Muro suelto de pirca, con hornacinas trapezoidales por la cara que da al patio.</summary>
        private static void FreeWall(Compound c, Vector2 from, Vector2 to, Vector3 outward, float height, float thick, bool niches)
        {
            var spec = new WallSpec
            {
                a = new Vector3(from.x, -Buried, from.y),
                b = new Vector3(to.x, -Buried, to.y),
                outward = outward,
                height = height + Buried,
                thick = thick,
                batterOut = 0.05f,
                batterIn = 0.05f,
                capStart = true,
                capEnd = true,
                uvShift = c.Rand(0f, 40f),
            };
            float length = (to - from).magnitude;
            if (niches && length > 3f)
            {
                int count = Mathf.FloorToInt((length - 1.2f) / 2.4f);
                for (int i = 0; i < count; i++)
                {
                    spec.openings.Add(new Opening
                    {
                        u = length * (i + 0.5f) / count,
                        sill = Buried + 0.8f,
                        height = 0.66f,
                        wBottom = 0.52f,
                        wTop = 0.4f,
                        depth = -0.26f,
                        lintelHeight = 0.16f,
                        lintelOver = 0.14f,
                    });
                }
            }
            VillageGeo.Wall(c.pirca, c.losa, spec);
        }

        /// <summary>
        /// Los dos muros largos del cerco, paralelos al camino. Donde hay una casa no hay muro: su trasera cierra el
        /// conjunto (si el muro siguiera, asomaría por dentro del cuarto).
        /// </summary>
        private static void Enclosure(Compound c, float height)
        {
            float hw = c.site.HalfW, hl = c.site.HalfL;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                float x = left ? -hw : hw;
                Vector3 outward = left ? Vector3.left : Vector3.right;
                var gaps = c.sideGaps.FindAll(g => g.left == left);
                gaps.Sort((p, q) => p.z0.CompareTo(q.z0));
                float cursor = -hl;
                foreach (var gap in gaps)
                {
                    // Cada tramo muere dentro del grueso del hastial de la casa
                    float end = gap.z0 + 0.3f;
                    if (end - cursor > 0.5f) FreeWall(c, new Vector2(x, cursor), new Vector2(x, end), outward, height, 0.7f, true);
                    cursor = Mathf.Max(cursor, gap.z1 - 0.3f);
                }
                if (hl - cursor > 0.5f) FreeWall(c, new Vector2(x, cursor), new Vector2(x, hl), outward, height, 0.7f, true);
            }
        }

        /// <summary>Muro que cruza el camino con una portada abierta entre dos pilones de sillería.</summary>
        private static void Gate(Compound c, float z, bool exit)
        {
            float hw = c.site.HalfW;
            const float gateHalf = 2.3f, pylonHalf = 0.8f;
            float wallHeight = 1.9f;
            Vector3 outward = exit ? Vector3.forward : Vector3.back;
            float side = pylonHalf * 2f + gateHalf;
            // Arrancan dentro del grueso de los muros laterales, para que no haya dos caras en el mismo plano
            FreeWall(c, new Vector2(-hw + 0.5f, z), new Vector2(-side + 0.2f, z), outward, wallHeight, 0.7f, false);
            FreeWall(c, new Vector2(side - 0.2f, z), new Vector2(hw - 0.5f, z), outward, wallHeight, 0.7f, false);

            var frame = new Frame(Vector3.zero, 0f);
            float zc = z + (exit ? -0.35f : 0.35f);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (gateHalf + pylonHalf);
                VillageGeo.Frustum(c.fina, frame, x, zc, -Buried, 3.0f, pylonHalf, 0.72f, pylonHalf - 0.15f, 0.58f);
                VillageGeo.Frustum(c.losa, frame, x, zc, 3.0f, 3.2f, pylonHalf - 0.07f, 0.66f, pylonHalf - 0.1f, 0.62f);
                // Un escalón en el muro junto al pilón, como rematan los cercos incas
                VillageGeo.Frustum(c.pirca, frame, s * (side + 0.9f), zc, 1.8f, 2.4f, 1.0f, 0.22f, 0.94f, 0.18f);
            }
        }

        /// <summary>Portada monumental de doble jamba, en sillería fina: anuncia que al otro lado espera el Cazador.</summary>
        private static void MonumentalGate(Compound c, float z)
        {
            float hw = c.site.HalfW;
            const float half = 5.0f, thick = 1.3f, height = 4.7f;
            FreeWall(c, new Vector2(-hw + 0.5f, z), new Vector2(-half + 0.2f, z), Vector3.forward, 2.3f, 0.7f, false);
            FreeWall(c, new Vector2(half - 0.2f, z), new Vector2(hw - 0.5f, z), Vector3.forward, 2.3f, 0.7f, false);

            var main = new WallSpec
            {
                a = new Vector3(-half, -Buried, z),
                b = new Vector3(half, -Buried, z),
                outward = Vector3.forward,
                height = height + Buried,
                thick = thick,
                batterOut = 0.05f,
                batterIn = 0.05f,
                capStart = true,
                capEnd = true,
                uvShift = 3f,
            };
            main.openings.Add(new Opening { u = half, sill = Buried, height = 3.3f, wBottom = 2.7f, wTop = 2.15f, depth = 0f, lintelHeight = 0.52f, lintelOver = 0.5f });
            // Hornacinas altas a los lados del vano, por las dos caras
            foreach (float u in new[] { half - 4.15f, half + 4.15f })
            {
                main.openings.Add(new Opening { u = u, sill = Buried + 1.5f, height = 1.1f, wBottom = 0.6f, wTop = 0.46f, depth = -0.3f, lintelHeight = 0.2f });
                main.openings.Add(new Opening { u = u, sill = Buried + 1.5f, height = 1.1f, wBottom = 0.6f, wTop = 0.46f, depth = 0.3f, lintelHeight = 0.2f });
            }
            VillageGeo.Wall(c.fina, c.losa, main);

            // Segunda jamba: un marco más ancho adosado por la cara del patio
            const float frameHalf = 3.3f;
            float zFace = z - thick; // base de la cara que da al patio
            var jamb = new WallSpec
            {
                a = new Vector3(frameHalf, -Buried, zFace - 0.24f),
                b = new Vector3(-frameHalf, -Buried, zFace - 0.24f),
                outward = Vector3.back,
                height = height + Buried - 0.35f,
                thick = 0.55f,
                batterOut = 0.05f,
                batterIn = 0f,
                capStart = true,
                capEnd = true,
                uvShift = 21f,
            };
            jamb.openings.Add(new Opening { u = frameHalf, sill = Buried, height = 3.78f, wBottom = 3.5f, wTop = 2.9f, depth = 0f, lintelHeight = 0.42f, lintelOver = 0.3f });
            VillageGeo.Wall(c.fina, c.losa, jamb);

            // Umbral de losas
            VillageGeo.Frustum(c.losa, new Frame(Vector3.zero, 0f), 0f, z - thick * 0.5f - 0.1f, -0.3f, 0.07f, 1.75f, 1.1f, 1.7f, 1.05f);
        }

        /// <summary>
        /// Casa inca (wasi): planta rectangular, muros inclinados, hastiales y techo de paja. Se puede entrar.
        /// La fachada mira hacia <paramref name="frontYaw"/> (grados dentro del conjunto: 90 = hacia +x, -90 = hacia -x).
        ///
        /// Lo que se ve y lo que choca son mallas distintas, porque la cápsula de Yari y de los rivales (1 m de ancho y
        /// 2 m de alto) es bastante mayor que su cuerpo: el hueco de paso de la puerta es más ancho y alto que la puerta
        /// dibujada, y el techo solo choca por dentro (el alero no, o no dejaría llegar a la puerta).
        /// </summary>
        private static void House(Compound c, float cx, float cz, float frontYaw, float length, float depth, float wallHeight, int doors, int textile)
        {
            const float thick = 0.62f, batter = 0.06f;
            const float doorBottom = 1.3f, doorTop = 1.04f, doorHeight = 1.8f;
            const float passWidth = 1.56f;
            float y0 = PlatformY;
            var f = new Frame(new Vector3(cx, 0f, cz), frontYaw);
            float hx = length * 0.5f, hz = depth * 0.5f;
            float inX = hx - thick, inZ = hz - thick; // media planta interior

            var h = new HouseBuild { name = "Casa_" + (c.houses.Count + 1) };
            c.houses.Add(h);
            c.sideGaps.Add((f.z.x > 0f, cz - hx, cz + hx));

            // Plataforma de piedra sobre la que se asienta (y que es el suelo del cuarto)
            VillageGeo.Frustum(h.pirca, f, 0f, 0f, -Buried - 0.3f, y0, hx + 0.62f, hz + 0.62f, hx + 0.5f, hz + 0.5f);
            VillageGeo.Frustum(h.colision, f, 0f, 0f, -Buried - 0.3f, y0, hx + 0.62f, hz + 0.62f, hx + 0.5f, hz + 0.5f);

            float runIn = batter * wallHeight;
            float halfSpan = hz - runIn;
            float peak = halfSpan * Mathf.Tan(48f * Mathf.Deg2Rad);
            float yWall = y0 + wallHeight;
            float yRidge = yWall + peak + 0.04f;

            // Fachada: puertas trapezoidales y hornacinas entre ellas
            var front = HouseWall(f.P(-hx, y0, hz), f.P(hx, y0, hz), f.z, wallHeight, thick, batter, 0f, c);
            var frontSolid = HouseWall(f.P(-hx, y0, hz), f.P(hx, y0, hz), f.z, wallHeight, thick, batter, 0f, c);
            var doorCenters = new List<float>();
            for (int i = 0; i < doors; i++) doorCenters.Add(length * (i + 0.5f) / doors);
            foreach (float u in doorCenters)
            {
                front.openings.Add(new Opening { u = u, sill = 0f, height = doorHeight, wBottom = doorBottom, wTop = doorTop, depth = 0f, lintelHeight = 0.26f, lintelOver = 0.26f });
                frontSolid.openings.Add(new Opening { u = u, sill = 0f, height = wallHeight - 0.02f, wBottom = passWidth, wTop = passWidth, depth = 0f, lintel = false });
            }
            float bay = length / doors;
            if (bay > 5.2f)
            {
                foreach (float u in doorCenters)
                {
                    foreach (float off in new[] { -bay * 0.32f, bay * 0.32f })
                    {
                        front.openings.Add(new Opening { u = u + off, sill = 0.95f, height = 0.62f, wBottom = 0.48f, wTop = 0.37f, depth = 0.24f, lintelHeight = 0.15f, lintelOver = 0.12f });
                    }
                }
            }
            VillageGeo.Wall(h.pirca, h.losa, front);
            VillageGeo.Wall(h.colision, null, frontSolid);

            // Trasera: ciega por fuera, con una fila de hornacinas por dentro
            var back = HouseWall(f.P(hx, y0, -hz), f.P(-hx, y0, -hz), -f.z, wallHeight, thick, batter, 0f, c);
            int nicheCount = Mathf.Max(2, Mathf.FloorToInt((length - 2.2f) / 1.45f));
            for (int i = 0; i < nicheCount; i++)
            {
                back.openings.Add(new Opening { u = thick + (length - 2f * thick) * (i + 0.5f) / nicheCount, sill = 0.9f, height = 0.64f, wBottom = 0.5f, wTop = 0.38f, depth = -0.26f, lintelHeight = 0.14f, lintelOver = 0.1f });
            }
            VillageGeo.Wall(h.pirca, h.losa, back);
            VillageGeo.Wall(h.colision, null, HouseWall(f.P(hx, y0, -hz), f.P(-hx, y0, -hz), -f.z, wallHeight, thick, batter, 0f, c));

            // Hastiales: ventana trapezoidal arriba, bajo la cumbrera
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = s > 0 ? f.P(hx, y0, hz) : f.P(-hx, y0, -hz);
                Vector3 b = s > 0 ? f.P(hx, y0, -hz) : f.P(-hx, y0, hz);
                var gable = HouseWall(a, b, f.x * s, wallHeight, thick, batter, peak, c);
                gable.openings.Add(new Opening { u = hz, sill = wallHeight + 0.2f, height = 0.74f, wBottom = 0.56f, wTop = 0.42f, depth = 0f, lintelHeight = 0.16f, lintelOver = 0.14f });
                foreach (float off in new[] { -hz * 0.48f, hz * 0.48f })
                {
                    gable.openings.Add(new Opening { u = hz + off, sill = 0.95f, height = 0.62f, wBottom = 0.48f, wTop = 0.37f, depth = 0.24f, lintelHeight = 0.15f, lintelOver = 0.12f });
                }
                VillageGeo.Wall(h.pirca, h.losa, gable);
                VillageGeo.Wall(h.colision, null, HouseWall(a, b, f.x * s, wallHeight, thick, batter, peak, c));

                // Clavos de piedra en el hastial, donde se ataba el techo
                Vector3 along = (b - a).normalized;
                float slopeRun = hz - runIn;
                foreach (float t in new[] { 0.3f, 0.62f })
                {
                    foreach (int sideSign in new[] { -1, 1 })
                    {
                        float u = hz + sideSign * slopeRun * (1f - t);
                        float v = wallHeight + peak * t - 0.5f;
                        Vector3 at = a + along * u + Vector3.up * v - f.x * (s * batter * v);
                        VillageGeo.Tube(h.losa, at - f.x * (s * 0.12f), at + f.x * (s * 0.3f), 0.07f, 0.055f, 6, true);
                    }
                }
            }

            // Techo. Por dentro choca (para que la cámara no lo atraviese); el alero, no
            VillageGeo.GableRoof(h.paja, h.fleco, h.madera, f, length, depth, yWall, peak, runIn, 0.42f, 0.4f, c.rng);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 r0 = f.P(-hx, yRidge, 0f), r1 = f.P(hx, yRidge, 0f);
                Vector3 e0 = f.P(-hx, yWall + 0.04f, sgn * halfSpan), e1 = f.P(hx, yWall + 0.04f, sgn * halfSpan);
                h.colision.Quad(r0, r1, e1, e0, Vector3.up);
                h.colision.Quad(r0, r1, e1, e0, Vector3.down);
            }

            // Armazón visto desde dentro: tirantes de muro a muro y pares hasta la cumbrera
            int frames = Mathf.Max(2, Mathf.RoundToInt(length / 2.2f));
            for (int i = 1; i < frames; i++)
            {
                float x = -hx + length * i / frames;
                VillageGeo.Tube(h.madera, f.P(x, yWall - 0.05f, -hz + 0.25f), f.P(x, yWall - 0.05f, hz - 0.25f), 0.07f, 0.065f, 6, false);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    VillageGeo.Tube(h.madera, f.P(x, yWall - 0.02f, sgn * (halfSpan - 0.06f)), f.P(x, yRidge - 0.12f, 0f), 0.05f, 0.045f, 6, false);
                }
            }

            // Suelo de tierra apisonada
            h.tierra.Quad(f.P(-inX, y0 + 0.01f, -inZ), f.P(-inX, y0 + 0.01f, inZ), f.P(inX, y0 + 0.01f, inZ), f.P(inX, y0 + 0.01f, -inZ), Vector3.up);

            // Cada puerta: escalón, el tejido recogido para dejar paso, y un sitio fuera y otro dentro para un rival
            for (int d = 0; d < doorCenters.Count; d++)
            {
                float x = doorCenters[d] - hx;
                VillageGeo.Frustum(h.losa, f, x, hz + 0.64f, -0.3f, 0.13f, 0.9f, 0.42f, 0.84f, 0.36f);
                VillageGeo.Frustum(h.colision, f, x, hz + 0.64f, -0.3f, 0.13f, 0.9f, 0.42f, 0.84f, 0.36f);

                float zCloth = hz - 0.3f;
                float top = y0 + doorHeight - 0.03f;
                float HalfAt(float v) => Mathf.Lerp(doorBottom, doorTop, v / doorHeight) * 0.5f - 0.02f;
                Rect pattern = TextileRect((textile + d) % 3);
                // Cenefa arriba...
                float valance = 0.36f;
                VillageGeo.Cloth(h.textil, f.P(x - HalfAt(doorHeight), top, zCloth), f.P(x + HalfAt(doorHeight), top, zCloth),
                    f.P(x + HalfAt(doorHeight - valance), top - valance, zCloth), f.P(x - HalfAt(doorHeight - valance), top - valance, zCloth),
                    new Rect(pattern.x, pattern.y + pattern.height * 0.7f, pattern.width, pattern.height * 0.3f), f.z, 0.03f, c.rng);
                // ...y el resto de la cortina recogido contra una jamba
                float sideSign = d % 2 == 0 ? -1f : 1f;
                float low = 0.62f;
                float outerTop = HalfAt(doorHeight - valance), outerLow = HalfAt(low);
                Vector3 TL = f.P(x + sideSign * outerTop, top - valance + 0.04f, zCloth - 0.03f), TR = f.P(x + sideSign * (outerTop - 0.2f), top - valance + 0.04f, zCloth - 0.03f);
                Vector3 BL = f.P(x + sideSign * outerLow, y0 + low, zCloth - 0.03f), BR = f.P(x + sideSign * (outerLow - 0.15f), y0 + low, zCloth - 0.03f);
                VillageGeo.Cloth(h.textil, TL, TR, BR, BL, new Rect(pattern.x, pattern.y, pattern.width * 0.3f, pattern.height * 0.7f), f.z, 0.045f, c.rng);
                VillageGeo.Tube(h.madera, f.P(x - HalfAt(doorHeight) - 0.14f, top + 0.015f, zCloth), f.P(x + HalfAt(doorHeight) + 0.14f, top + 0.015f, zCloth), 0.03f, 0.03f, 5, true);

                c.spawns.Add(new Pose(f.P(x, 0f, hz + 2.1f), Quaternion.Euler(0f, frontYaw, 0f)));
                c.interiors.Add(new Pose(f.P(x, y0, -0.35f), Quaternion.Euler(0f, frontYaw, 0f)));
            }

            // ── El cuarto por dentro ──
            bool hall = length > 12f;

            // Poyo para dormir en un extremo, con su manta y unos cántaros al pie
            {
                float xb = inX - 0.62f;
                VillageGeo.Frustum(h.pirca, f, xb, 0f, y0, y0 + 0.38f, 0.62f, inZ - 0.12f, 0.6f, inZ - 0.14f);
                VillageGeo.Frustum(h.colision, f, xb, 0f, y0, y0 + 0.38f, 0.62f, inZ - 0.12f, 0.6f, inZ - 0.14f);
                VillageGeo.Cloth(h.textil, f.P(xb - 0.5f, y0 + 0.395f, inZ - 0.5f), f.P(xb + 0.5f, y0 + 0.395f, inZ - 0.5f),
                    f.P(xb + 0.5f, y0 + 0.395f, -inZ + 0.6f), f.P(xb - 0.5f, y0 + 0.395f, -inZ + 0.6f), TextileRect(3), Vector3.up, 0.012f, c.rng);
                float xj = inX - 1.75f;
                VillageGeo.Lathe(h.ceramica, f.P(xj, y0 - 0.01f, -inZ + 0.42f), AribaloProfile, 12, c.Rand(0.72f, 0.88f), c.Rand(0f, 1f));
                VillageGeo.Lathe(h.ceramica, f.P(xj - 0.62f, y0 - 0.01f, -inZ + 0.46f), OllaProfile, 12, c.Rand(0.48f, 0.58f), c.Rand(0f, 1f));
            }

            // Fogón en la esquina del otro extremo: tres piedras, ceniza, leños y la luz de las brasas
            float xh = -inX + 1.05f, zh = -inZ + 0.95f;
            for (int i = 0; i < 5; i++)
            {
                float ang = (i * 72f + c.Rand(-10f, 10f)) * Mathf.Deg2Rad;
                var stone = new Frame(f.P(xh + Mathf.Sin(ang) * 0.46f, 0f, zh + Mathf.Cos(ang) * 0.46f), frontYaw + i * 72f + c.Rand(-20f, 20f));
                float w = c.Rand(0.13f, 0.18f), dd = c.Rand(0.1f, 0.14f);
                VillageGeo.Frustum(h.pirca, stone, 0f, 0f, y0 - 0.02f, y0 + c.Rand(0.14f, 0.22f), w, dd, w * 0.72f, dd * 0.7f);
            }
            var ash = new Vector3[10];
            for (int i = 0; i < ash.Length; i++)
            {
                float ang = i * Mathf.PI * 2f / ash.Length;
                ash[i] = f.P(xh + Mathf.Sin(ang) * 0.4f, y0 + 0.02f, zh + Mathf.Cos(ang) * 0.4f);
            }
            h.barro.Poly(ash, Vector3.up);
            for (int i = 0; i < 3; i++)
            {
                float ang = (i * 61f + c.Rand(0f, 25f)) * Mathf.Deg2Rad;
                Vector3 dir = f.D(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                VillageGeo.Tube(h.madera, f.P(xh, y0 + 0.07f + 0.04f * i, zh) - dir * 0.34f, f.P(xh, y0 + 0.1f + 0.04f * i, zh) + dir * 0.3f, 0.05f, 0.04f, 6, true);
            }
            h.lights.Add(f.P(xh + 0.35f, y0 + 0.85f, zh + 0.35f));

            // La kallanka es larga: dos lamparillas de barro sobre pedestales de piedra reparten la luz
            if (hall)
            {
                foreach (float xl in new[] { 0f, inX - 3.4f })
                {
                    VillageGeo.Frustum(h.pirca, f, xl, -inZ + 0.3f, y0, y0 + 0.62f, 0.2f, 0.2f, 0.16f, 0.16f);
                    VillageGeo.Lathe(h.ceramica, f.P(xl, y0 + 0.62f, -inZ + 0.3f), OllaProfile, 10, 0.2f, c.Rand(0f, 1f));
                    h.lights.Add(f.P(xl, y0 + 1.15f, -inZ + 0.75f));
                }
            }
        }

        private static WallSpec HouseWall(Vector3 a, Vector3 b, Vector3 outward, float height, float thick, float batter, float peak, Compound c)
        {
            return new WallSpec
            {
                a = a,
                b = b,
                outward = outward,
                height = height,
                thick = thick,
                batterOut = batter,
                batterIn = 0f,
                endBatter = batter,
                innerInset = thick,
                peak = peak,
                uvShift = c.Rand(0f, 40f),
            };
        }

        /// <summary>Los cuatro tejidos de la textura (2 x 2), con un pequeño margen para que no se mezclen.</summary>
        private static Rect TextileRect(int index)
        {
            const float m = 0.012f;
            switch (index)
            {
                case 0: return new Rect(m, m, 0.5f - 2f * m, 0.5f - 2f * m);
                case 1: return new Rect(0.5f + m, m, 0.5f - 2f * m, 0.5f - 2f * m);
                case 2: return new Rect(m, 0.5f + m, 0.5f - 2f * m, 0.5f - 2f * m);
                default: return new Rect(0.5f + m, 0.5f + m, 0.5f - 2f * m, 0.5f - 2f * m);
            }
        }

        /// <summary>Colca: depósito redondo de piedra con techo cónico de paja y una ventanilla alta.</summary>
        private static void Qolqa(Compound c, float x, float z, float radius, float height, float doorAngle)
        {
            const float batter = 0.05f;
            var center = new Vector3(x, -Buried, z);
            VillageGeo.RoundWall(c.pirca, c.losa, center, radius, height + Buried, 0.42f, batter, 20, doorAngle, 2, Buried + 0.8f, 0.78f, c.Rand(0f, 30f));
            float topRadius = radius - batter * (height + Buried);
            VillageGeo.ConeRoof(c.paja, c.fleco, c.madera, new Vector3(x, height, z), topRadius, 52f, 0.34f, c.rng);
            Disc(c.barro, new Vector3(x, 0.72f, z), radius - 0.4f, 12);
        }

        /// <summary>Torreón: como una colca grande, de muro alto, que flanquea la portada.</summary>
        private static void Tower(Compound c, float x, float z, float radius, float height, float windowAngle)
        {
            const float batter = 0.045f;
            var center = new Vector3(x, -Buried, z);
            VillageGeo.RoundWall(c.pirca, c.losa, center, radius, height + Buried, 0.6f, batter, 24, windowAngle, 2, Buried + 2.2f, 0.9f, c.Rand(0f, 30f));
            float topRadius = radius - batter * (height + Buried);
            VillageGeo.ConeRoof(c.paja, c.fleco, c.madera, new Vector3(x, height, z), topRadius, 50f, 0.4f, c.rng);
            Disc(c.barro, new Vector3(x, 2.1f, z), radius - 0.55f, 12);
        }

        private static void Disc(MeshBuf buf, Vector3 center, float radius, int sides)
        {
            var points = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                points[i] = center + new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            }
            buf.Poly(points, Vector3.up);
        }

        // ───────────────────────── Enseres ─────────────────────────

        private static readonly Vector2[] AribaloProfile =
        {
            new Vector2(0.03f, 0f), new Vector2(0.15f, 0.06f), new Vector2(0.29f, 0.22f), new Vector2(0.345f, 0.40f), new Vector2(0.31f, 0.56f),
            new Vector2(0.19f, 0.68f), new Vector2(0.105f, 0.75f), new Vector2(0.09f, 0.88f), new Vector2(0.125f, 0.97f), new Vector2(0.165f, 1.0f),
            new Vector2(0.13f, 1.0f), new Vector2(0.075f, 0.9f),
        };

        private static readonly Vector2[] OllaProfile =
        {
            new Vector2(0.08f, 0f), new Vector2(0.3f, 0.12f), new Vector2(0.42f, 0.36f), new Vector2(0.4f, 0.6f), new Vector2(0.28f, 0.78f),
            new Vector2(0.25f, 0.88f), new Vector2(0.33f, 1.0f), new Vector2(0.28f, 1.0f), new Vector2(0.21f, 0.88f),
        };

        /// <summary>Cántaro (aríbalo) o una olla ancha.</summary>
        private static void Jar(Compound c, float x, float z, float height, bool aribalo)
        {
            VillageGeo.Lathe(c.ceramica, new Vector3(x, -0.02f, z), aribalo ? AribaloProfile : OllaProfile, 12, height, c.Rand(0f, 1f));
        }

        /// <summary>Fogón: corro de piedras, ceniza y unos leños a medio quemar.</summary>
        private static void FirePit(Compound c, float x, float z)
        {
            const int stones = 9;
            for (int i = 0; i < stones; i++)
            {
                float a = i * 360f / stones + c.Rand(-8f, 8f);
                float r = 0.72f + c.Rand(-0.05f, 0.05f);
                var f = new Frame(new Vector3(x + Mathf.Sin(a * Mathf.Deg2Rad) * r, 0f, z + Mathf.Cos(a * Mathf.Deg2Rad) * r), a + c.Rand(-15f, 15f));
                float w = c.Rand(0.17f, 0.24f), d = c.Rand(0.12f, 0.17f), h = c.Rand(0.16f, 0.27f);
                VillageGeo.Frustum(c.pirca, f, 0f, 0f, -0.15f, h, w, d, w * 0.72f, d * 0.7f);
            }
            Disc(c.barro, new Vector3(x, 0.035f, z), 0.62f, 10);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 47f + c.Rand(0f, 30f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                VillageGeo.Tube(c.madera, new Vector3(x, 0.09f + 0.05f * i, z) - dir * 0.5f, new Vector3(x, 0.13f + 0.05f * i, z) + dir * 0.45f, 0.06f, 0.05f, 6, true);
            }
        }

        /// <summary>Poyo de piedra para sentarse.</summary>
        private static void Bench(Compound c, float x, float z, float yaw, float length)
        {
            var f = new Frame(new Vector3(x, 0f, z), yaw);
            VillageGeo.Frustum(c.pirca, f, 0f, 0f, -0.3f, 0.4f, length * 0.5f, 0.3f, length * 0.5f - 0.04f, 0.27f);
            VillageGeo.Frustum(c.losa, f, 0f, 0f, 0.4f, 0.5f, length * 0.5f + 0.03f, 0.33f, length * 0.5f + 0.02f, 0.32f);
        }

        /// <summary>Batán: la piedra plana de moler y su mano.</summary>
        private static void Mortar(Compound c, float x, float z, float yaw)
        {
            var f = new Frame(new Vector3(x, 0f, z), yaw);
            VillageGeo.Frustum(c.losa, f, 0f, 0f, -0.2f, 0.2f, 0.42f, 0.3f, 0.38f, 0.26f);
            VillageGeo.Frustum(c.losa, f, 0.05f, 0f, 0.2f, 0.36f, 0.2f, 0.11f, 0.16f, 0.07f);
        }

        /// <summary>Leña apilada.</summary>
        private static void Firewood(Compound c, float x, float z, float yaw)
        {
            var f = new Frame(new Vector3(x, 0f, z), yaw);
            for (int row = 0; row < 4; row++)
            {
                int count = 6 - row;
                for (int i = 0; i < count; i++)
                {
                    float px = (i - (count - 1) * 0.5f) * 0.17f + c.Rand(-0.015f, 0.015f);
                    float py = 0.08f + row * 0.145f;
                    float len = c.Rand(0.42f, 0.52f);
                    float r = c.Rand(0.065f, 0.085f);
                    VillageGeo.Tube(c.madera, f.P(px, py, -len), f.P(px, py, len), r, r * 0.92f, 6, true);
                }
            }
            c.blockers.Add((f.P(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 1.0f), yaw));
        }

        /// <summary>Tendedero: dos postes, un travesaño y tejidos puestos a secar.</summary>
        private static void Rack(Compound c, float x, float z, float yaw, float width, int textile)
        {
            var f = new Frame(new Vector3(x, 0f, z), yaw);
            float h = 1.85f;
            float half = width * 0.5f;
            VillageGeo.Tube(c.madera, f.P(-half, -0.3f, 0f), f.P(-half + 0.03f, h + 0.12f, 0f), 0.055f, 0.04f, 6, true);
            VillageGeo.Tube(c.madera, f.P(half, -0.3f, 0f), f.P(half - 0.02f, h + 0.1f, 0f), 0.055f, 0.04f, 6, true);
            VillageGeo.Tube(c.madera, f.P(-half - 0.18f, h, 0f), f.P(half + 0.18f, h - 0.02f, 0f), 0.035f, 0.03f, 6, true);

            float w1 = width * 0.52f;
            float x0 = -half + 0.14f;
            float drop = c.Rand(1.2f, 1.45f);
            VillageGeo.Cloth(c.textil, f.P(x0, h - 0.03f, 0.02f), f.P(x0 + w1, h - 0.035f, 0.02f), f.P(x0 + w1, h - drop, 0.03f), f.P(x0, h - drop, 0.03f),
                TextileRect(textile), f.z, 0.03f, c.rng);
            float x1 = x0 + w1 + 0.12f, w2 = half - 0.12f - x1;
            if (w2 > 0.3f)
            {
                float drop2 = c.Rand(0.75f, 1.05f);
                VillageGeo.Cloth(c.textil, f.P(x1, h - 0.04f, 0.02f), f.P(x1 + w2, h - 0.045f, 0.02f), f.P(x1 + w2, h - drop2, 0.03f), f.P(x1, h - drop2, 0.03f),
                    TextileRect((textile + 1) % 4), f.z, 0.03f, c.rng);
            }
            c.blockers.Add((f.P(0f, 0.95f, 0f), new Vector3(width + 0.2f, 1.9f, 0.22f), yaw));
        }

        // ═════════════════════════ Inicio del recorrido ═════════════════════════

        /// <summary>Una apacheta (montón de piedras de los caminantes) marca el punto donde despierta Yari.</summary>
        private static void BuildStartMarker(Terrain terrain, Transform root, Materials mats, List<Mesh> meshes)
        {
            Vector2 r = Right(StartYaw), fwd = Forward(StartYaw);
            Vector2 at = StartPoint + r * 6.4f - fwd * 1.5f;
            float y = Ground(terrain, at.x, at.y);
            var stone = new MeshBuf();
            var rng = new System.Random(7);
            float radius = 0.95f, height = 0f;
            for (int layer = 0; layer < 6; layer++)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(radius * 6.5f));
                float layerHeight = VillageGeo.Range(rng, 0.2f, 0.28f);
                for (int i = 0; i < count; i++)
                {
                    float a = (i * 360f / count + VillageGeo.Range(rng, -12f, 12f)) * Mathf.Deg2Rad;
                    float d = count == 1 ? 0f : radius * VillageGeo.Range(rng, 0.55f, 0.8f);
                    var f = new Frame(new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d), VillageGeo.Range(rng, 0f, 360f));
                    float w = VillageGeo.Range(rng, 0.2f, 0.32f), dd = VillageGeo.Range(rng, 0.16f, 0.24f);
                    VillageGeo.Frustum(stone, f, 0f, 0f, height - 0.25f, height + layerHeight, w, dd, w * 0.75f, dd * 0.72f);
                }
                height += layerHeight * 0.85f;
                radius *= 0.74f;
            }

            Mesh mesh = stone.ToMesh("Inicio_Apacheta");
            meshes.Add(mesh);
            var go = new GameObject("Inicio_Apacheta");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(at.x, y, at.y);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mats.pirca;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void PlaceYari(Terrain terrain, Vector2 point, float yaw)
        {
            GameObject yari = GameObject.Find("Yari_Hero");
            if (yari == null) yari = GameObject.FindGameObjectWithTag("Player");
            if (yari == null)
            {
                Debug.LogWarning("[Ayni Aldea] No se encontró a Yari en la escena; colócalo a mano en el inicio del camino.");
                return;
            }
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            var position = new Vector3(point.x, Ground(terrain, point.x, point.y) + 0.05f, point.y);
            yari.transform.SetPositionAndRotation(position, rotation);
            EditorUtility.SetDirty(yari.transform);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(position + rotation * new Vector3(0.5f, 2f, -3.5f), Quaternion.Euler(9.6f, yaw - 8f, 0f));
                EditorUtility.SetDirty(cam.transform);
            }
        }

        // ═════════════════════════ Pruebas y capturas de revisión ═════════════════════════

        /// <summary>
        /// Solo en Play: lleva a Yari a un punto de un conjunto (x a la derecha, z en el sentido de la marcha) mirando
        /// hacia <paramref name="localYaw"/>, con la cámara detrás. Para probar el recorrido sin caminarlo entero.
        /// </summary>
        public static void TeleportLocal(string siteId, float lx, float lz, float localYaw)
        {
            if (!Application.isPlaying) { Debug.Log("[Ayni Aldea] TeleportLocal solo funciona en Play."); return; }
            Terrain terrain = Terrain.activeTerrain;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (terrain == null || player == null) return;
            foreach (Site site in Sites)
            {
                if (site.id != siteId) continue;
                Vector2 f = Forward(site.yaw), r = Right(site.yaw);
                float x = site.center.x + r.x * lx + f.x * lz, z = site.center.y + r.y * lx + f.y * lz;
                var mover = player.GetComponent<CharacterController>();
                if (mover != null) mover.enabled = false;
                player.transform.SetPositionAndRotation(new Vector3(x, Ground(terrain, x, z) + 0.1f, z), Quaternion.Euler(0f, site.yaw + localYaw, 0f));
                if (mover != null) mover.enabled = true;
                Camera cam = Camera.main;
                var follow = cam != null ? cam.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
                if (follow != null) follow.SnapBehindTarget();
                Debug.Log($"[Ayni Aldea] Yari en {siteId} ({lx:F1}, {lz:F1}) -> mundo ({x:F1}, {z:F1})");
                return;
            }
        }

        /// <summary>
        /// Estado de todos los personajes con EnemyController (también los ocultos) y de Yari: dónde están, cuánto miden
        /// de verdad (de los pies a la coronilla), su cápsula, su vida y a qué conjunto están asignados.
        /// </summary>
        public static void Rivals()
        {
            var sb = new System.Text.StringBuilder("[Ayni Aldea] Personajes:");
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) sb.Append("\n   Yari: ").Append(Describe(player));
            foreach (EnemyController enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string site = "sin conjunto";
                foreach (AyniEncounterSite s in UnityEngine.Object.FindObjectsByType<AyniEncounterSite>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    foreach (GameObject r in s.Rivals)
                    {
                        if (r == enemy.gameObject) site = s.SiteName + (s.PlayerEntered ? " (Yari dentro)" : "") + (s.Cleared ? " (despejado)" : "");
                    }
                }
                sb.Append("\n   ").Append(enemy.name).Append(enemy.gameObject.activeInHierarchy ? "" : " [oculto]").Append(enemy.IsBoss ? " [jefe]" : "")
                  .Append(": ").Append(Describe(enemy.gameObject))
                  .Append($" vida {enemy.CurrentHealth:F0}/{enemy.MaxHealth:F0} estado {enemy.State} muerto={enemy.IsDead} | {site}")
                  .Append(enemy.transform.parent != null ? " | padre " + enemy.transform.parent.name : "");
            }
            Debug.Log(sb.ToString());
        }

        private static string Describe(GameObject go)
        {
            Vector3 p = go.transform.position;
            string text = $"pos ({p.x:F1}, {p.y:F2}, {p.z:F1})";
            var cc = go.GetComponent<CharacterController>();
            if (cc != null) text += $" cápsula {cc.radius * 2f:F2} x {cc.height:F2}";
            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman && animator.avatar != null)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (head != null && foot != null) text += $" cabeza a {head.position.y - p.y:F2} m del suelo (pie {foot.position.y - p.y:F2})";
                text += $" escala {animator.transform.lossyScale.y:F2}";
            }
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null) text += $" malla alto {smr.bounds.size.y:F2} tris {(smr.sharedMesh != null ? smr.sharedMesh.triangles.Length / 3 : 0)}";
            return text;
        }

        /// <summary>Solo en Play: dónde está Yari respecto al conjunto más cercano (para comprobar por dónde pasa y dónde choca).</summary>
        public static string Where()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return "sin jugador";
            Vector3 p = player.transform.position;
            Site best = null;
            float bestDist = float.MaxValue;
            foreach (Site site in Sites)
            {
                float d = Vector2.Distance(site.center, new Vector2(p.x, p.z));
                if (d < bestDist) { bestDist = d; best = site; }
            }
            Vector2 f = Forward(best.yaw), r = Right(best.yaw);
            Vector2 off = new Vector2(p.x, p.z) - best.center;
            string inside = "";
            foreach (AyniEncounterSite s in AyniEncounterSite.All)
            {
                if (s.PlayerEntered) inside += " [entró en " + s.SiteName + "]";
            }
            string text = $"{best.id} local ({Vector2.Dot(off, r):F2}, {Vector2.Dot(off, f):F2}) mundo ({p.x:F1}, {p.y:F2}, {p.z:F1}){inside}";
            Debug.Log("[Ayni Aldea] Yari: " + text);
            return text;
        }

        /// <summary>Vistas fijas de cada conjunto en DebugCaptures (aldea_K1_a.png...). Para revisar sin entrar en Play.</summary>
        [MenuItem("Ayni/Entorno/Capturar Vistas de la Aldea")]
        public static void CaptureViews()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            Directory.CreateDirectory(CaptureFolder);
            foreach (Site site in Sites)
            {
                float y = MeasureRoadHeight(terrain, site);
                Vector3 W(float lx, float ly, float lz)
                {
                    Vector2 f = Forward(site.yaw), r = Right(site.yaw);
                    return new Vector3(site.center.x + r.x * lx + f.x * lz, y + ly, site.center.y + r.y * lx + f.y * lz);
                }
                Shot("aldea_" + site.id + "_a_llegada", W(1.5f, 1.9f, -site.HalfL - 13f), W(0f, 1.8f, 0f), 60f);
                Shot("aldea_" + site.id + "_b_patio", W(2.5f, 1.7f, -site.HalfL + 2.5f), W(-4f, 1.6f, site.HalfL - 4f), 68f);
                Shot("aldea_" + site.id + "_c_aerea", W(site.HalfW + 13f, 17f, -site.HalfL - 12f), W(0f, 1f, 0f), 55f);
                Shot("aldea_" + site.id + "_d_salida", W(-2f, 1.7f, 1f), W(0.5f, 2.2f, site.HalfL), 68f);
            }
            Debug.Log("[Ayni Aldea] Vistas guardadas en la carpeta " + CaptureFolder + ".");
        }

        /// <summary>Vista libre en coordenadas de un conjunto (x a la derecha, y arriba, z en el sentido de la marcha).</summary>
        public static void ShotLocal(string siteId, float px, float py, float pz, float tx, float ty, float tz, float fov, string name)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            foreach (Site site in Sites)
            {
                if (site.id != siteId) continue;
                float y = MeasureRoadHeight(terrain, site);
                Vector2 f = Forward(site.yaw), r = Right(site.yaw);
                var from = new Vector3(site.center.x + r.x * px + f.x * pz, y + py, site.center.y + r.y * px + f.y * pz);
                var to = new Vector3(site.center.x + r.x * tx + f.x * tz, y + ty, site.center.y + r.y * tx + f.y * tz);
                Directory.CreateDirectory(CaptureFolder);
                Shot(name, from, to, fov);
                return;
            }
        }

        /// <summary>Vista libre en coordenadas del mundo.</summary>
        public static void ShotWorld(float px, float py, float pz, float tx, float ty, float tz, float fov, string name)
        {
            Directory.CreateDirectory(CaptureFolder);
            Shot(name, new Vector3(px, py, pz), new Vector3(tx, ty, tz), fov);
        }

        private static void Shot(string name, Vector3 position, Vector3 lookAt, float fov)
        {
            const int width = 1280, height = 720;
            var go = new GameObject("Ayni_CapturaTemporal") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = position;
                cam.transform.LookAt(lookAt);
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.2f;
                cam.farClipPlane = 6000f;
                Camera main = Camera.main;
                if (main != null)
                {
                    cam.clearFlags = main.clearFlags;
                    cam.backgroundColor = main.backgroundColor;
                }
                cam.allowHDR = true;

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                }

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(CaptureFolder, name + ".png"), tex.EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ayni Aldea] No se pudo capturar " + name + ": " + e.Message);
            }
            finally
            {
                if (rt != null) rt.Release();
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
