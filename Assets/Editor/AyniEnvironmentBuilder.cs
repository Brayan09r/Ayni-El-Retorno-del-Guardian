#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// Entorno andino alrededor de la zona de combate: acantilados escalonados, una garganta profunda
    /// con agua, dos quebradas laterales y un puente colgante sobre el Qhapaq Ñan.
    ///
    /// El relieve nuevo viene en Assets/Art/Environment/Ayni_HeightPatch.bytes (solo la zona modificada).
    /// Como un terreno de Unity no puede bajar de su altura cero, se sube todo el mapa de alturas 40 m
    /// y se baja el objeto Terrain esos mismos 40 m: el mundo queda igual, pero ahora se puede excavar.
    ///
    /// Antes de tocar nada se guarda una copia del terreno en la carpeta EnvironmentBackups del proyecto;
    /// "Restaurar Terreno Original" lo deja todo como estaba. Solo se ejecuta desde el menú.
    /// </summary>
    public static class AyniEnvironmentBuilder
    {
        private const string EnvFolder = "Assets/Art/Environment";
        private const string PatchPath = EnvFolder + "/Ayni_HeightPatch.bytes";
        private const string ControlPath = EnvFolder + "/Ayni_TerrainControl.png";
        private const string Control2Path = EnvFolder + "/Ayni_TerrainControl2.png";
        private const string TerrainMatPath = EnvFolder + "/Ayni_Terreno.mat";
        private const string WaterMatPath = EnvFolder + "/Ayni_Agua.mat";
        private const string WoodMatPath = EnvFolder + "/Ayni_Madera.mat";
        private const string RopeMatPath = EnvFolder + "/Ayni_Soga.mat";
        private const string StoneMatPath = EnvFolder + "/Ayni_Piedra.mat";
        private const string OriginalMatPath = "Assets/network of paths/Materials/terrain material.mat";
        private const string TexFolder = "Assets/network of paths/Textures/";
        private const string BackupFolder = "EnvironmentBackups";
        private const string BackupFile = BackupFolder + "/Terrain.asset.original";
        private const string RootName = "Ayni_Entorno";
        private const string CaptureFolder = "DebugCaptures";

        // Datos del relieve (los mismos con los que se generó el parche)
        private const float WaterY = -18f;
        private static readonly Rect WaterArea = new Rect(560f, 270f, 150f, 365f); // x, z, ancho, largo
        private static readonly Vector2 BridgeWest = new Vector2(638.3f, 466.5f);
        private static readonly Vector2 BridgeEast = new Vector2(663.7f, 466.5f);

        // ------------------------------------------------------------------------------------------------
        [MenuItem("Ayni/Entorno/Crear Acantilados y Quebradas")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Entorno] Sal del modo Play antes de modificar el terreno.");
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[Ayni Entorno] No hay un terreno activo en la escena.");
                return;
            }
            if (!File.Exists(PatchPath))
            {
                Debug.LogError("[Ayni Entorno] Falta el archivo de relieve: " + PatchPath);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Ayni Entorno", "Esculpiendo acantilados y quebradas...", 0.2f);
                if (!ApplyHeights(terrain)) return;

                EditorUtility.DisplayProgressBar("Ayni Entorno", "Material del terreno...", 0.6f);
                ApplyTerrainMaterial(terrain);

                EditorUtility.DisplayProgressBar("Ayni Entorno", "Agua, puente y red de seguridad...", 0.8f);
                GameObject root = GetOrCreateRoot();
                BuildWater(root);
                BuildBridge(root, terrain);
                var rescue = root.GetComponent<AyniAbyssRescue>();
                if (rescue == null) rescue = root.AddComponent<AyniAbyssRescue>();
                rescue.Configure(-8f, 0.5f);
                EditorUtility.SetDirty(rescue);

                terrain.heightmapPixelError = Mathf.Min(terrain.heightmapPixelError, 3f);

                EditorUtility.DisplayProgressBar("Ayni Entorno", "Atmósfera: sol, niebla y cordillera...", 0.9f);
                AyniAtmosphere.Apply(root);
                EditorUtility.SetDirty(terrain);

                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene);
                Debug.Log("<color=green>[Ayni Entorno]</color> Acantilados, garganta, quebradas y puente colgante creados. " +
                          "Copia del terreno original en " + BackupFile);
            }
            catch (Exception e)
            {
                Debug.LogError("[Ayni Entorno] Error al crear el entorno: " + e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ValidateColliders();
            CaptureViews();
        }

        /// <summary>Comprueba con rayos que el suelo del puente y el fondo de la garganta tienen colisión donde deben.</summary>
        private static void ValidateColliders()
        {
            Physics.SyncTransforms();
            int floorHits = 0;
            float minY = float.MaxValue, maxY = float.MinValue;
            const int samples = 12;
            for (int i = 0; i < samples; i++)
            {
                float t = (i + 0.5f) / samples;
                Vector2 xz = Vector2.Lerp(BridgeWest, BridgeEast, t);
                if (Physics.Raycast(new Vector3(xz.x, 30f, xz.y), Vector3.down, out RaycastHit hit, 100f) && hit.collider.name == "Piso")
                {
                    floorHits++;
                    minY = Mathf.Min(minY, hit.point.y);
                    maxY = Mathf.Max(maxY, hit.point.y);
                }
            }

            Vector2 mid = (BridgeWest + BridgeEast) * 0.5f;
            string bottom = "sin colisión";
            if (Physics.Raycast(new Vector3(mid.x, 30f, mid.y + 6f), Vector3.down, out RaycastHit bed, 200f))
            {
                bottom = bed.collider.name + " a y=" + bed.point.y.ToString("F1");
            }
            Debug.Log($"[Ayni Entorno] Colisión del puente: {floorHits}/{samples} puntos sobre el piso (altura {minY:F2} a {maxY:F2}). " +
                      $"Fondo de la garganta junto al puente: {bottom}.");
        }

        // ------------------------------------------------------------------------------------------------
        private static bool ApplyHeights(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            byte[] patch = File.ReadAllBytes(PatchPath);
            if (patch.Length < 28 || patch[0] != 'A' || patch[1] != 'Y' || patch[2] != 'N' || patch[3] != 'H')
            {
                Debug.LogError("[Ayni Entorno] El archivo de relieve no es válido.");
                return false;
            }

            int x0 = BitConverter.ToInt32(patch, 4);
            int z0 = BitConverter.ToInt32(patch, 8);
            int w = BitConverter.ToInt32(patch, 12);
            int h = BitConverter.ToInt32(patch, 16);
            float offset = BitConverter.ToSingle(patch, 20);
            float sizeY = BitConverter.ToSingle(patch, 24);
            const int header = 28;

            int res = data.heightmapResolution;
            if (res != 4097 || Mathf.Abs(data.size.y - sizeY) > 0.01f || Mathf.Abs(data.size.x - 1000f) > 0.5f ||
                x0 < 0 || z0 < 0 || x0 + w > res || z0 + h > res || patch.Length != header + w * h * 2)
            {
                Debug.LogError($"[Ayni Entorno] El terreno no coincide con el relieve preparado (resolución {res}, tamaño {data.size}).");
                return false;
            }

            float offsetNorm = offset / sizeY;
            float[,] heights = data.GetHeights(0, 0, res, res);

            // ¿Ya se subió el mapa de alturas en una ejecución anterior? (la esquina del terreno original vale 0)
            bool heightsRaised = heights[0, 0] > offsetNorm * 0.5f;
            bool transformLowered = terrain.transform.position.y < -offset * 0.5f;

            // Copia de seguridad del terreno original, una sola vez
            string dataPath = AssetDatabase.GetAssetPath(data);
            if (!File.Exists(BackupFile))
            {
                if (heightsRaised)
                {
                    Debug.LogWarning("[Ayni Entorno] El terreno ya estaba modificado y no hay copia del original.");
                }
                else
                {
                    Directory.CreateDirectory(BackupFolder);
                    File.Copy(dataPath, BackupFile);
                    AddToGitIgnore();
                }
            }

            if (!heightsRaised)
            {
                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++) heights[z, x] += offsetNorm;
                }
            }

            int i = header;
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    int v = patch[i] | (patch[i + 1] << 8);
                    heights[z0 + z, x0 + x] = v / 65535f;
                    i += 2;
                }
            }

            data.SetHeights(0, 0, heights);

            if (!transformLowered)
            {
                Vector3 p = terrain.transform.position;
                terrain.transform.position = new Vector3(p.x, p.y - offset, p.z);
            }

            terrain.Flush();
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain.transform);
            return true;
        }

        private static void AddToGitIgnore()
        {
            try
            {
                const string path = ".gitignore";
                if (!File.Exists(path)) return;
                string text = File.ReadAllText(path);
                if (text.Contains(BackupFolder)) return;
                File.AppendAllText(path, "\n# Copia local del terreno original (Ayni > Entorno)\n/" + BackupFolder + "/\n");
            }
            catch (Exception)
            {
                // No es crítico
            }
        }

        // ------------------------------------------------------------------------------------------------
        private static void ApplyTerrainMaterial(Terrain terrain)
        {
            Shader shader = Shader.Find("Ayni/Terreno Andino");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[Ayni Entorno] El shader 'Ayni/Terreno Andino' no compiló; se mantiene el material anterior.");
                return;
            }

            // Mapas de control: datos, no color
            ConfigureControlMap(ControlPath);
            ConfigureControlMap(Control2Path);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Ayni_Terreno" };
                AssetDatabase.CreateAsset(mat, TerrainMatPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            Material original = AssetDatabase.LoadAssetAtPath<Material>(OriginalMatPath);
            CopyTexture(original, "Texture2D_5ACF75DD", mat, "_MaskRock", "Texture mask rock_2.png");
            CopyTexture(original, "Texture2D_8049C8A0", mat, "_MaskGrass", "Height_bushes_map.png");
            CopyTexture(original, "Texture2D_D263B98B", mat, "_MaskPaths", "mask_path_2.png");
            CopyTexture(original, "Texture2D_A333D269", mat, "_GrassTex", "Grass 5.png");
            CopyTexture(original, "Texture2D_8CF25222", mat, "_GrassNorm", "Grass 5_Norm.png");
            CopyTexture(original, "Texture2D_D33EE4CE", mat, "_GroundTex", "ground_stone_ground_0020_01_Diff.png");
            CopyTexture(original, "Texture2D_26BB0DDA", mat, "_GroundNorm", "ground_stone_ground_0020_01_Norm.png");
            CopyTexture(original, "Texture2D_AA21F3C4", mat, "_RockTex", "rock_Diff.png");
            CopyTexture(original, "Texture2D_1BD6E0F2", mat, "_RockNorm", "rock_Norm.png");
            CopyTexture(original, "Texture2D_5BE772F2", mat, "_PathTex", "paths_Diff.png");
            CopyTexture(original, "Texture2D_98C4FF69", mat, "_PathNorm", "paths_Norm.png");
            if (original != null && original.HasProperty("Vector1_C78D4A64"))
            {
                mat.SetFloat("_GroundBias", original.GetFloat("Vector1_C78D4A64"));
            }

            Texture control = AssetDatabase.LoadAssetAtPath<Texture2D>(ControlPath);
            if (control == null) Debug.LogWarning("[Ayni Entorno] Falta el mapa de control: " + ControlPath);
            mat.SetTexture("_AyniControl", control);
            Texture control2 = AssetDatabase.LoadAssetAtPath<Texture2D>(Control2Path);
            if (control2 == null) Debug.LogWarning("[Ayni Entorno] Falta el mapa de control de andenes: " + Control2Path);
            mat.SetTexture("_AyniControl2", control2);

            // El shader necesita saber dónde está el terreno para decidir, losa por losa, qué es calzada
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            mat.SetVector("_TerrainRect", new Vector4(origin.x, origin.z, size.x, size.z));
            mat.SetFloat("_PaveSize", 1.0f);
            mat.SetFloat("_WallRow", 0.7f);
            mat.SetFloat("_WallStone", 0.95f);
            mat.SetColor("_WallColor", new Color(0.64f, 0.59f, 0.52f));
            mat.SetColor("_WetColor", new Color(0.62f, 0.58f, 0.53f));

            EditorUtility.SetDirty(mat);
            terrain.materialTemplate = mat;
        }

        private static void ConfigureControlMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (!importer.sRGBTexture && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == 2048) return;

            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void CopyTexture(Material from, string fromProp, Material to, string toProp, string fallbackFile)
        {
            Texture tex = null;
            if (from != null && from.HasProperty(fromProp)) tex = from.GetTexture(fromProp);
            if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + fallbackFile);
            if (tex == null) Debug.LogWarning("[Ayni Entorno] No se encontró la textura para " + toProp + " (" + fallbackFile + ").");
            to.SetTexture(toProp, tex);
        }

        // ------------------------------------------------------------------------------------------------
        internal static GameObject GetOrCreateRoot()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static Transform ResetChild(GameObject root, string name)
        {
            Transform old = root.transform.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        private static Material GetLitMaterial(string path, Color color, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(lit != null ? lit : Shader.Find("Standard")) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildWater(GameObject root)
        {
            Shader shader = Shader.Find("Ayni/Agua Quebrada");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[Ayni Entorno] El shader 'Ayni/Agua Quebrada' no compiló; no se crea el agua.");
                return;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Ayni_Agua" };
                AssetDatabase.CreateAsset(mat, WaterMatPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            Transform old = root.transform.Find("Agua_Quebrada");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            // Un solo plano a la altura del agua: el terreno lo tapa en todas partes menos dentro de la garganta
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Agua_Quebrada";
            UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(root.transform, false);
            water.transform.position = new Vector3(WaterArea.center.x, WaterY, WaterArea.center.y);
            water.transform.localScale = new Vector3(WaterArea.width / 10f, 1f, WaterArea.height / 10f);
            var renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Puente colgante de sogas (estilo Q'eswachaka) que cruza la garganta siguiendo el camino.</summary>
        private static void BuildBridge(GameObject root, Terrain terrain)
        {
            Material wood = GetLitMaterial(WoodMatPath, new Color(0.34f, 0.23f, 0.14f), 0.12f);
            Material rope = GetLitMaterial(RopeMatPath, new Color(0.66f, 0.54f, 0.28f), 0.08f);
            Material stone = GetLitMaterial(StoneMatPath, new Color(0.78f, 0.75f, 0.70f), 0.1f);
            // La piedra de los estribos usa la misma roca del terreno
            var rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "rock_Diff.png");
            var rockNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "rock_Norm.png");
            if (rockTex != null && stone.HasProperty("_BaseMap")) stone.SetTexture("_BaseMap", rockTex);
            if (rockNorm != null && stone.HasProperty("_BumpMap"))
            {
                stone.SetTexture("_BumpMap", rockNorm);
                stone.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(stone);

            Transform bridge = ResetChild(root, "Puente_Colgante");

            Vector3 a = GroundPoint(terrain, BridgeWest) + Vector3.up * 0.12f;
            Vector3 b = GroundPoint(terrain, BridgeEast) + Vector3.up * 0.12f;
            Vector3 along = (b - a);
            float length = along.magnitude;
            Vector3 flat = new Vector3(along.x, 0f, along.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, flat).normalized;

            const float deckWidth = 1.7f;
            const float sag = 1.15f;
            const float railHeight = 1.05f;
            const float postHeight = 1.7f;

            Vector3 Deck(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            // El pasamanos sale de lo alto de los postes y cuelga un poco menos que el piso
            Vector3 Rail(float t, float s)
            {
                Vector3 baseLine = Vector3.Lerp(a, b, t) + Vector3.up * postHeight * 0.92f;
                float drop = (postHeight * 0.92f - railHeight + sag) * 4f * t * (1f - t);
                return baseLine + Vector3.down * drop + side * (s * (deckWidth * 0.5f + 0.05f));
            }

            // --- tablones ---
            Transform planks = NewChild(bridge, "Tablones");
            var random = new System.Random(11);
            int plankCount = Mathf.RoundToInt(length / 0.42f);
            for (int i = 0; i < plankCount; i++)
            {
                float t = (i + 0.5f) / plankCount;
                Vector3 p = Deck(t);
                Vector3 dir = (Deck(Mathf.Min(1f, t + 0.01f)) - Deck(Mathf.Max(0f, t - 0.01f))).normalized;
                GameObject plank = Primitive(PrimitiveType.Cube, "Tablon", planks, wood, false);
                plank.transform.position = p;
                plank.transform.rotation = Quaternion.LookRotation(dir, Vector3.up) *
                                           Quaternion.Euler(0f, (float)(random.NextDouble() * 6.0 - 3.0), (float)(random.NextDouble() * 3.0 - 1.5));
                plank.transform.localScale = new Vector3(deckWidth + (float)(random.NextDouble() * 0.16 - 0.08), 0.07f, 0.34f);
            }

            // --- sogas ---
            Transform ropes = NewChild(bridge, "Sogas");
            const int ropeSegments = 26;
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < ropeSegments; i++)
                {
                    float t0 = (float)i / ropeSegments;
                    float t1 = (float)(i + 1) / ropeSegments;
                    // soga del piso, bajo el borde de los tablones
                    Vector3 f0 = Deck(t0) + side * (s * deckWidth * 0.46f) + Vector3.down * 0.07f;
                    Vector3 f1 = Deck(t1) + side * (s * deckWidth * 0.46f) + Vector3.down * 0.07f;
                    Segment(ropes, rope, f0, f1, 0.13f);
                    // pasamanos
                    Segment(ropes, rope, Rail(t0, s), Rail(t1, s), 0.09f);
                    // tirantes entre el pasamanos y el piso
                    if (i > 0)
                    {
                        Vector3 foot = Deck(t0) + side * (s * deckWidth * 0.5f);
                        Segment(ropes, rope, Rail(t0, s), foot, 0.035f);
                    }
                }
            }

            // --- estribos y postes de piedra ---
            Transform stones = NewChild(bridge, "Estribos");
            for (int end = 0; end < 2; end++)
            {
                Vector3 anchor = end == 0 ? a : b;
                Vector3 outward = end == 0 ? -flat : flat;

                GameObject baseBlock = Primitive(PrimitiveType.Cube, "Estribo", stones, stone, true);
                baseBlock.transform.position = anchor + outward * 0.9f + Vector3.down * 0.42f;
                baseBlock.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                baseBlock.transform.localScale = new Vector3(deckWidth + 1.3f, 0.9f, 2.6f);

                for (int s = -1; s <= 1; s += 2)
                {
                    // Poste de tres bloques de piedra apilados, cada uno algo más pequeño y girado
                    Vector3 postBase = anchor + side * (s * (deckWidth * 0.5f + 0.3f)) + outward * 0.25f + Vector3.down * 0.1f;
                    float[] blockHeights = { 0.66f, 0.58f, 0.50f };
                    float[] blockWidths = { 0.70f, 0.60f, 0.50f };
                    float y = 0f;
                    for (int k = 0; k < blockHeights.Length; k++)
                    {
                        GameObject block = Primitive(PrimitiveType.Cube, "Poste", stones, stone, true);
                        block.transform.position = postBase + Vector3.up * (y + blockHeights[k] * 0.5f);
                        block.transform.rotation = Quaternion.LookRotation(flat, Vector3.up) *
                                                   Quaternion.Euler(0f, (float)(random.NextDouble() * 14.0 - 7.0), 0f);
                        block.transform.localScale = new Vector3(blockWidths[k], blockHeights[k] + 0.02f, blockWidths[k] + 0.06f);
                        y += blockHeights[k];
                    }

                    // Pretil de piedra que sale del poste en abanico: encauza hacia el puente y evita caer junto a la entrada
                    Vector3 wallDir = (side * s + outward * 0.75f).normalized;
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 c = postBase + wallDir * (0.95f + k * 1.08f);
                        float groundY = GroundPoint(terrain, new Vector2(c.x, c.z)).y;
                        if (groundY < anchor.y - 1.2f) break;
                        float wallHeight = 1.0f - k * 0.09f;
                        GameObject wall = Primitive(PrimitiveType.Cube, "Pretil", stones, stone, true);
                        wall.transform.position = new Vector3(c.x, groundY + wallHeight * 0.5f - 0.22f, c.z);
                        wall.transform.rotation = Quaternion.LookRotation(wallDir, Vector3.up) *
                                                  Quaternion.Euler(0f, (float)(random.NextDouble() * 10.0 - 5.0), 0f);
                        wall.transform.localScale = new Vector3(0.55f, wallHeight, 1.12f);
                    }

                    // la soga del pasamanos baja desde el poste hasta su anclaje en el suelo
                    Vector3 top = anchor + Vector3.up * postHeight * 0.92f + side * (s * (deckWidth * 0.5f + 0.05f));
                    Vector3 ground = anchor + outward * 2.6f + side * (s * (deckWidth * 0.5f + 0.35f)) + Vector3.down * 0.1f;
                    Segment(ropes, rope, top, ground, 0.09f);
                }
            }

            // --- colisión: piso continuo y barandas invisibles ---
            Transform colliders = NewChild(bridge, "Colision");
            const int colSegments = 14;
            for (int i = 0; i < colSegments; i++)
            {
                Vector3 p0 = Deck((float)i / colSegments);
                Vector3 p1 = Deck((float)(i + 1) / colSegments);
                Vector3 mid = (p0 + p1) * 0.5f;
                Quaternion rot = Quaternion.LookRotation((p1 - p0).normalized, Vector3.up);
                float segLen = (p1 - p0).magnitude + 0.08f;

                AddBox(colliders, "Piso", mid + Vector3.down * 0.11f, rot, new Vector3(deckWidth, 0.3f, segLen));
                for (int s = -1; s <= 1; s += 2)
                {
                    AddBox(colliders, "Baranda", mid + side * (s * (deckWidth * 0.5f + 0.06f)) + Vector3.up * 0.7f, rot,
                        new Vector3(0.12f, 1.5f, segLen));
                }
            }
        }

        private static Vector3 GroundPoint(Terrain terrain, Vector2 xz)
        {
            var p = new Vector3(xz.x, 0f, xz.y);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return p;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void Segment(Transform parent, Material mat, Vector3 from, Vector3 to, float diameter)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            GameObject seg = Primitive(PrimitiveType.Cylinder, "Soga", parent, mat, false);
            seg.transform.position = (from + to) * 0.5f;
            seg.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            seg.transform.localScale = new Vector3(diameter, d.magnitude * 0.5f + diameter * 0.2f, diameter);
        }

        private static void AddBox(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------------------------------------
        [MenuItem("Ayni/Entorno/Restaurar Terreno Original")]
        public static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Entorno] Sal del modo Play antes de restaurar el terreno.");
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[Ayni Entorno] No hay un terreno activo en la escena.");
                return;
            }
            if (!File.Exists(BackupFile))
            {
                Debug.LogWarning("[Ayni Entorno] No hay copia del terreno original (" + BackupFile + "); no se cambia nada.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Restaurar terreno original",
                    "Se quitarán los acantilados, la garganta, el agua y el puente, y el terreno volverá a su forma original.",
                    "Restaurar", "Cancelar"))
            {
                return;
            }

            string dataPath = AssetDatabase.GetAssetPath(terrain.terrainData);
            File.Copy(BackupFile, dataPath, true);
            AssetDatabase.ImportAsset(dataPath, ImportAssetOptions.ForceUpdate);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (data != null) terrain.terrainData = data;

            Vector3 p = terrain.transform.position;
            terrain.transform.position = new Vector3(p.x, 0f, p.z);
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null && data != null) collider.terrainData = data;

            Material original = AssetDatabase.LoadAssetAtPath<Material>(OriginalMatPath);
            if (original != null) terrain.materialTemplate = original;
            terrain.Flush();

            GameObject root = GameObject.Find(RootName);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            RenderSettings.fog = false;

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=green>[Ayni Entorno]</color> Terreno original restaurado.");
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Guarda en DebugCaptures unas vistas fijas del entorno, para revisarlo sin entrar en Play.</summary>
        [MenuItem("Ayni/Entorno/Capturar Vistas del Entorno")]
        public static void CaptureViews()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            Directory.CreateDirectory(CaptureFolder);
            float Ground(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

            Shot("env_1_inicio", new Vector3(612f, Ground(612f, 461f) + 2.6f, 461f), new Vector3(613f, 5f, 490f), 60f);
            Shot("env_2_borde_norte", new Vector3(617f, Ground(617f, 492f) + 1.7f, 492f), new Vector3(648f, -6f, 505f), 70f);
            Shot("env_3_puente", new Vector3(628f, Ground(628f, 471f) + 1.9f, 471f), new Vector3(652f, 2f, 466f), 60f);
            Shot("env_4_aerea_norte", new Vector3(590f, 60f, 620f), new Vector3(640f, 0f, 480f), 65f);
            Shot("env_5_aerea_suroeste", new Vector3(560f, 70f, 400f), new Vector3(640f, 0f, 500f), 65f);
            Shot("env_6_desde_el_puente", new Vector3(651f, 3.6f, 466.5f), new Vector3(640f, -6f, 500f), 70f);
            Shot("env_8_andenes", new Vector3(598f, Ground(598f, 486f) + 1.8f, 486f), new Vector3(560f, 14f, 620f), 45f);
            Shot("env_9_andenes_arena", new Vector3(613f, Ground(613f, 474f) + 1.8f, 474f), new Vector3(590f, 10f, 440f), 60f);
            Shot("env_10_calzada", new Vector3(604f, Ground(604f, 470f) + 1.7f, 470f), new Vector3(640f, 3f, 466.5f), 55f);
            Shot("env_7_acantilado", new Vector3(606f, Ground(606f, 478f) + 1.8f, 478f), new Vector3(660f, 22f, 530f), 60f);
            Debug.Log("[Ayni Entorno] Vistas guardadas en la carpeta " + CaptureFolder + ".");
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
                Debug.LogWarning("[Ayni Entorno] No se pudo capturar " + name + ": " + e.Message);
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
