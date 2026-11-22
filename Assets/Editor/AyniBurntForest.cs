using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// La selva quemada del Antisuyo: troncos calcinados en pie, algunos partidos o tumbados, por los taludes a ambos
    /// lados del Qhapaq Ñan, y columnas de humo que aún suben del bosque. Antes el valle se veía seco y vacío, sin
    /// rastro de la selva que Amaru quemó.
    ///  - Los troncos salen de 14 a 60 m del eje del camino (nunca en el camino ni dentro de una kancha) y en laderas
    ///    no demasiado empinadas. Se juntan por trozos de 40 m en una malla cada uno (pocas llamadas de dibujo).
    ///  - No tienen colisión: son decorado; los obstáculos del camino siguen siendo los de la aldea.
    ///  - Si Yari perdona a Amaru, renacen con el resto (AyniBurntTree): los cercanos al camino echan copa y varas;
    ///    los lejanos solo recuperan el color de la corteza, para que la ola de vida no pese demasiado.
    /// Menú: Ayni > Entorno > Construir Selva Quemada (se puede repetir; sustituye la anterior).
    /// </summary>
    public static partial class AyniVillageBuilder
    {
        private const string ForestRootName = "Selva_Quemada";
        private const string ForestMeshPath = Folder + "/Selva_Mallas.asset";
        private const float ForestCell = 40f;

        /// <summary>Eje aproximado del camino, de la salida a la cabeza del puente (kanchas y obstáculos en orden).</summary>
        private static readonly Vector2[] RoadSpine =
        {
            new Vector2(247.9f, 539.5f), new Vector2(283.0f, 539.7f), new Vector2(294.5f, 506.0f), new Vector2(330.9f, 468.3f),
            new Vector2(360.3f, 441.7f), new Vector2(368.7f, 414.5f), new Vector2(392.1f, 387.7f), new Vector2(429.7f, 384.5f),
            new Vector2(450.4f, 386.0f), new Vector2(477.9f, 433.9f), new Vector2(484.6f, 475.6f), new Vector2(497.9f, 507.0f),
            new Vector2(556.1f, 519.1f), new Vector2(612.0f, 468.0f), new Vector2(634.0f, 470.0f),
        };

        [MenuItem("Ayni/Entorno/Construir Selva Quemada")]
        public static void BuildBurntForest() => BuildBurntForestWith(1f);

        /// <summary>Para el puente de agentes: densidad relativa (1 = normal).</summary>
        public static void BuildBurntForestWith(float density)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Selva] Sal del modo Play antes de construir la selva quemada.");
                return;
            }
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[Ayni Selva] No hay un terreno activo en la escena.");
                return;
            }
            var burnt = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Aldea_MaderaQuemada.mat");
            if (burnt == null)
            {
                Debug.LogError("[Ayni Selva] Falta el material Aldea_MaderaQuemada: construye antes la aldea.");
                return;
            }

            GameObject envRoot = AyniEnvironmentBuilder.GetOrCreateRoot();
            Transform old = envRoot.transform.Find(ForestRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (AssetDatabase.LoadMainAssetAtPath(ForestMeshPath) != null) AssetDatabase.DeleteAsset(ForestMeshPath);

            var root = new GameObject(ForestRootName);
            root.transform.SetParent(envRoot.transform, false);
            var rng = new System.Random(7703);
            var life = new System.Random(9157);

            // Candidatos en una rejilla con jitter; se quedan los que caen en la franja junto al camino
            var cells = new Dictionary<Vector2Int, ForestCellBuild>();
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;
            float spacing = 8f / Mathf.Sqrt(Mathf.Max(0.2f, density));
            int standing = 0, fallen = 0, revived = 0;
            for (float x = tPos.x + 4f; x < tPos.x + tSize.x - 4f; x += spacing)
            {
                for (float z = tPos.z + 4f; z < tPos.z + tSize.z - 4f; z += spacing)
                {
                    float px = x + VillageGeo.Range(rng, -spacing * 0.45f, spacing * 0.45f);
                    float pz = z + VillageGeo.Range(rng, -spacing * 0.45f, spacing * 0.45f);
                    var p2 = new Vector2(px, pz);
                    float toRoad = DistanceToSpine(p2);
                    if (toRoad < 14f || toRoad > 60f) continue;
                    // Más espeso cerca del camino, más ralo hacia arriba
                    if (rng.NextDouble() > Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(14f, 60f, toRoad))) continue;
                    if (NearSite(p2, 30f)) continue;
                    Vector3 normal = terrain.terrainData.GetInterpolatedNormal((px - tPos.x) / tSize.x, (pz - tPos.z) / tSize.z);
                    if (Vector3.Angle(normal, Vector3.up) > 36f) continue;

                    var key = new Vector2Int(Mathf.FloorToInt(px / ForestCell), Mathf.FloorToInt(pz / ForestCell));
                    if (!cells.TryGetValue(key, out ForestCellBuild cell))
                    {
                        Vector2 c = new Vector2((key.x + 0.5f) * ForestCell, (key.y + 0.5f) * ForestCell);
                        cell = new ForestCellBuild { origin = new Vector3(c.x, Ground(terrain, c.x, c.y), c.y) };
                        cells[key] = cell;
                    }
                    Vector3 foot = new Vector3(px, Ground(terrain, px, pz), pz) - cell.origin;
                    bool near = toRoad < 32f;
                    if (rng.NextDouble() < 0.14) { ForestFallen(cell, foot, rng); fallen++; }
                    else { ForestSnag(cell, foot, rng, life, near); standing++; }
                }
            }

            // Una malla por trozo; las marcas de rebrote cuelgan de su trozo
            var meshes = new List<Mesh>();
            int index = 0;
            foreach (KeyValuePair<Vector2Int, ForestCellBuild> pair in cells)
            {
                ForestCellBuild cell = pair.Value;
                var go = new GameObject($"Trozo_{pair.Key.x}_{pair.Key.y}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = cell.origin;
                string prefix = "Selva_" + (index++);
                AddPart(go.transform, "Troncos", cell.trunks, burnt, false, true, prefix, meshes);
                AddPart(go.transform, "Ramas", cell.branches, burnt, false, false, prefix, meshes);

                var wood = new List<Renderer>();
                foreach (string part in new[] { "Troncos", "Ramas" })
                {
                    Transform piece = go.transform.Find(part);
                    Renderer r = piece != null ? piece.GetComponent<Renderer>() : null;
                    if (r != null) wood.Add(r);
                }
                int t = 0;
                foreach (BurntTree tree in cell.trees)
                {
                    var marker = new GameObject("ArbolQuemado_" + (++t));
                    marker.transform.SetParent(go.transform, false);
                    marker.transform.localPosition = tree.foot;
                    var points = new Vector3[tree.points.Count];
                    for (int p = 0; p < points.Length; p++) points[p] = tree.points[p] - tree.foot;
                    marker.AddComponent<AyniBurntTree>().Setup(false, wood.ToArray(), points, tree.directions.ToArray(), tree.sizes.ToArray(), tree.kinds.ToArray());
                    revived++;
                }
                // Si en el trozo no hay ninguno cercano al camino, una marca sin rebrotes: la madera recupera el color igual
                if (cell.trees.Count == 0 && wood.Count > 0)
                {
                    var marker = new GameObject("Corteza");
                    marker.transform.SetParent(go.transform, false);
                    marker.AddComponent<AyniBurntTree>().Setup(false, wood.ToArray(), new Vector3[0], new Vector3[0], new float[0], new int[0]);
                }
            }

            int smoke = BuildForestSmoke(terrain, root.transform, rng);

            if (meshes.Count > 0)
            {
                AssetDatabase.CreateAsset(meshes[0], ForestMeshPath);
                for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], ForestMeshPath);
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[Ayni Selva]</color> Selva quemada: {standing} troncos en pie y {fallen} caídos en {cells.Count} trozos " +
                      $"({meshes.Count} mallas), {revived} con rebrotes si Yari perdona, {smoke} columnas de humo.");
        }

        [MenuItem("Ayni/Entorno/Quitar Selva Quemada")]
        public static void RemoveBurntForest()
        {
            GameObject envRoot = GameObject.Find(EnvRootName);
            Transform old = envRoot != null ? envRoot.transform.Find(ForestRootName) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (AssetDatabase.LoadMainAssetAtPath(ForestMeshPath) != null) AssetDatabase.DeleteAsset(ForestMeshPath);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Ayni Selva] Selva quemada quitada.");
        }

        private sealed class ForestCellBuild
        {
            public Vector3 origin;
            public readonly MeshBuf trunks = new MeshBuf(), branches = new MeshBuf();
            public readonly List<BurntTree> trees = new List<BurntTree>();
        }

        /// <summary>Tronco calcinado en pie: la selva alta tenía árboles altos y rectos; quedan fustes negros y ramas rotas.</summary>
        private static void ForestSnag(ForestCellBuild cell, Vector3 foot, System.Random rng, System.Random life, bool withShoots)
        {
            float h = VillageGeo.Range(rng, 3.2f, 9.5f);
            // Algunos partidos a media altura
            if (rng.NextDouble() < 0.25) h *= 0.45f;
            Vector3 lean = new Vector3(VillageGeo.Range(rng, -0.08f, 0.08f), 1f, VillageGeo.Range(rng, -0.08f, 0.08f)).normalized;
            Vector3 baseP = foot + Vector3.down * 0.5f;
            Vector3 top = baseP + lean * (h + 0.5f);
            // Gruesos: de lejos un fuste fino parece un palo clavado, no un árbol de la selva
            float r0 = VillageGeo.Range(rng, 0.24f, 0.5f) * Mathf.Lerp(0.8f, 1.25f, h / 9.5f);
            float r1 = r0 * VillageGeo.Range(rng, 0.25f, 0.45f);
            VillageGeo.Tube(cell.trunks, baseP, top, r0, r1, 6, true, 0.6f);
            // Raíces de contrafuerte (típicas de la selva alta), tres o cuatro
            int roots = 2 + rng.Next(3);
            for (int i = 0; i < roots; i++)
            {
                float a = VillageGeo.Range(rng, 0f, Mathf.PI * 2f);
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                VillageGeo.Tube(cell.branches, foot + Vector3.up * 0.7f, foot + outward * (r0 * 3.2f) + Vector3.down * 0.25f, r0 * 0.45f, r0 * 0.15f, 4, true);
            }

            BurntTree tree = null;
            if (withShoots)
            {
                tree = new BurntTree { fallen = false, foot = foot };
                cell.trees.Add(tree);
                tree.Shoot(top - lean * 0.15f, lean, Mathf.Lerp(0.9f, 1.3f, h / 9.5f), AyniBurntTree.Crown);
            }
            int branches = rng.Next(4);
            for (int b = 0; b < branches; b++)
            {
                Vector3 p = Vector3.Lerp(baseP, top, VillageGeo.Range(rng, 0.5f, 0.92f));
                Vector3 dir = new Vector3(VillageGeo.Range(rng, -1f, 1f), VillageGeo.Range(rng, 0.1f, 0.8f), VillageGeo.Range(rng, -1f, 1f)).normalized;
                float length = VillageGeo.Range(rng, 0.7f, 2.2f);
                VillageGeo.Tube(cell.branches, p, p + dir * length, r1 * 0.8f, 0.025f, 4, true);
                if (tree != null && b == 0)
                    tree.Shoot(p + dir * (length - 0.05f), Vector3.Slerp(dir, Vector3.up, 0.45f), VillageGeo.Range(life, 1.1f, 1.6f), AyniBurntTree.Shoot);
            }
        }

        /// <summary>Tronco tumbado en la ladera (sin colisión: es decorado).</summary>
        private static void ForestFallen(ForestCellBuild cell, Vector3 foot, System.Random rng)
        {
            float a = VillageGeo.Range(rng, 0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float length = VillageGeo.Range(rng, 3f, 7f);
            float r = VillageGeo.Range(rng, 0.18f, 0.3f);
            VillageGeo.Tube(cell.trunks, foot + Vector3.up * (r * 0.6f), foot + dir * length + Vector3.up * (r * 0.4f), r, r * 0.6f, 6, true, 0.6f);
        }

        /// <summary>Columnas de humo gris que suben de la selva, lejos del camino (se ven desde él).</summary>
        private static int BuildForestSmoke(Terrain terrain, Transform root, System.Random rng)
        {
            Material smokeMat = ForestSmokeMaterial();
            if (smokeMat == null) return 0;
            var parent = new GameObject("Humo");
            parent.transform.SetParent(root, false);
            int made = 0;
            for (int i = 1; i < RoadSpine.Length - 1 && made < 9; i += 1)
            {
                if (rng.NextDouble() < 0.35) continue;
                Vector2 a = RoadSpine[i], b = RoadSpine[i + 1];
                Vector2 dir = (b - a).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x) * (rng.Next(2) == 0 ? -1f : 1f);
                Vector2 p = Vector2.Lerp(a, b, 0.5f) + side * VillageGeo.Range(rng, 45f, 75f);
                if (NearSite(p, 35f)) continue;
                var go = new GameObject("Columna_" + (++made));
                go.transform.SetParent(parent.transform, false);
                go.transform.position = new Vector3(p.x, Ground(terrain, p.x, p.y) + 0.5f, p.y);
                Debug.Log($"[Ayni Selva] Humo {made} en {go.transform.position}");
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                var ps = go.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
                main.prewarm = true;
                main.duration = 10f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 20f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.34f, 0.32f, 0.3f, 0.5f), new Color(0.46f, 0.43f, 0.4f, 0.4f));
                main.maxParticles = 80;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = 3.5f;
                ParticleSystem.ShapeModule shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 6f;
                shape.radius = 1.5f;
                ParticleSystem.SizeOverLifetimeModule grow = ps.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
                ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                ParticleSystem.VelocityOverLifetimeModule wind = ps.velocityOverLifetime;
                wind.enabled = true;
                wind.space = ParticleSystemSimulationSpace.World;
                wind.x = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                wind.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                wind.z = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = smokeMat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingFudge = 10f;
            }
            return made;
        }

        /// <summary>Material de humo: textura de bocanada suave generada aquí (sin assets de terceros).</summary>
        private static Material ForestSmokeMaterial()
        {
            string texPath = Folder + "/Selva_Humo.png";
            string matPath = Folder + "/Selva_Humo.mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                const int size = 128;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var rnd = new System.Random(31);
                float ox = (float)rnd.NextDouble() * 50f, oy = (float)rnd.NextDouble() * 50f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float n = Mathf.PerlinNoise(ox + x * 0.05f, oy + y * 0.05f) * 0.6f + Mathf.PerlinNoise(ox + x * 0.13f, oy + y * 0.13f) * 0.4f;
                    float a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(n * 1.6f - 0.25f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                t.Apply();
                System.IO.File.WriteAllBytes(texPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(texPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) return null;
                mat = new Material(shader) { name = "Selva_Humo" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            // Transparente con mezcla alfa
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static float DistanceToSpine(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < RoadSpine.Length - 1; i++)
            {
                Vector2 a = RoadSpine[i], b = RoadSpine[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        private static bool NearSite(Vector2 p, float radius)
        {
            foreach (Site site in Sites) if (Vector2.Distance(p, site.center) < radius) return true;
            return Vector2.Distance(p, new Vector2(620f, 470f)) < radius + 8f; // plaza y cabeza del puente
        }

        /// <summary>
        /// Tamaño de las losas de la calzada y los patios (shader del terreno). Antes eran de 1 m y se veían enormes y
        /// repetidas; las calzadas incas tienen losas de 40-80 cm.
        /// </summary>
        public static void SetPaveSize(float meters)
        {
            Terrain terrain = Terrain.activeTerrain;
            Material mat = terrain != null ? terrain.materialTemplate : null;
            if (mat == null || !mat.HasProperty("_PaveSize"))
            {
                Debug.LogError("[Ayni Selva] El terreno no usa el shader con losas (_PaveSize).");
                return;
            }
            mat.SetFloat("_PaveSize", meters);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ayni Selva] Losas de la calzada: {meters:F2} m");
        }
    }
}
