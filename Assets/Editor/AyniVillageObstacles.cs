#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// Obstáculos del camino entre un conjunto de casas y el siguiente: lo que dejó Amaru a su paso por el bosque.
    /// Cada uno se supera con algo que Yari ya sabe hacer:
    ///   Tronco / TroncoDoble   árbol quemado caído a través del camino: se salta (en carrera o en parado).
    ///   Derrumbe               peñascos que solo dejan un pasillo en zigzag.
    ///   Empalizada             dos filas de estacas con el paso en lados opuestos: obliga a hacer una ese.
    ///   MuroCaido              muro de pirca a medio caer: se salta, mejor por el tramo más bajo.
    /// A los lados, una hilera de peñascos sube por el talud para que no baste con salirse un paso del camino
    /// (el mapa es abierto: quien dé un rodeo largo por el cerro puede evitarlos).
    /// Forma parte de <see cref="AyniVillageBuilder"/>: se construyen y se borran con la aldea.
    ///
    /// Cada árbol quemado (en pie o caído) lleva además una marca <see cref="AyniBurntTree"/> con los puntos por donde
    /// rebrota: si Yari perdona a Amaru, el queñual renace también en ellos (AyniReforestation).
    /// </summary>
    public static partial class AyniVillageBuilder
    {
        private enum ObstacleKind
        {
            Tronco,
            CarretaVolcada,
            TrincheraCazador,
            PortadaRuina,
            MuroCaido,
            Derrumbe,
            PilaVigas,
            Empalizada
        }

        private const string ObstaclesMeshPath = Folder + "/Obstaculos_Mallas.asset";

        private sealed class Obstacle
        {
            public string id;
            public ObstacleKind kind;
            public Vector2 center; // sobre el eje del camino
            public float yaw;      // sentido de la marcha
            public int seed;
            public bool Flip => seed % 2 == 0; // a qué lado cae el paso
        }

        // 8 obstáculos distintos a lo largo del Qhapaq Ñan (sin repetir la misma piedra + cercas)
        private static readonly Obstacle[] Obstacles =
        {
            new Obstacle { id = "O1", kind = ObstacleKind.Tronco,           center = new Vector2(283.0f, 539.7f), yaw = 105.1f, seed = 11 }, // a 592 m de la plaza
            new Obstacle { id = "O2", kind = ObstacleKind.CarretaVolcada,   center = new Vector2(294.5f, 506.0f), yaw = 145.0f, seed = 18 }, // 550 m
            new Obstacle { id = "O3", kind = ObstacleKind.TrincheraCazador, center = new Vector2(360.3f, 441.7f), yaw = 155.5f, seed = 25 }, // 450 m
            new Obstacle { id = "O4", kind = ObstacleKind.PortadaRuina,     center = new Vector2(368.7f, 414.5f), yaw = 155.3f, seed = 32 }, // 420 m
            new Obstacle { id = "O5", kind = ObstacleKind.MuroCaido,        center = new Vector2(429.7f, 384.5f), yaw = 76.4f,  seed = 39 }, // 335 m
            new Obstacle { id = "O6", kind = ObstacleKind.Derrumbe,         center = new Vector2(450.4f, 386.0f), yaw = 65.4f,  seed = 46 }, // 312 m
            new Obstacle { id = "O7", kind = ObstacleKind.PilaVigas,        center = new Vector2(484.6f, 475.6f), yaw = 9.9f,   seed = 53 }, // 205 m
            new Obstacle { id = "O8", kind = ObstacleKind.Empalizada,       center = new Vector2(497.9f, 507.0f), yaw = 37.2f,  seed = 60 }, // 168 m
        };

        private const float RoadHalf = 7f;   // media anchura del camino que el obstáculo cierra del todo
        private const float FlankEnd = 17f;  // hasta dónde suben los peñascos por los taludes
        private const float SlideRadius = 1.25f;
        private const float SlideRowZ = 4.6f;  // separación entre las tres hileras del derrumbe
        private const float SlideGap = 2.6f;   // ancho del paso que deja cada hilera
        private const float ChicaneGap = 2.5f;
        private const float ChicaneZ = 2.7f;

        private static readonly Vector2[] RoadJarProfile =
        {
            new Vector2(0.08f, 0.00f),
            new Vector2(0.24f, 0.14f),
            new Vector2(0.28f, 0.36f),
            new Vector2(0.18f, 0.56f),
            new Vector2(0.10f, 0.68f),
            new Vector2(0.14f, 0.78f),
        };

        private sealed class ObstacleBuild
        {
            public Obstacle def;
            public Terrain terrain;
            public float y0;
            public System.Random rng;
            public readonly MeshBuf roca = new MeshBuf(), quemado = new MeshBuf(), ramas = new MeshBuf();
            public readonly MeshBuf madera = new MeshBuf(), pirca = new MeshBuf(), fina = new MeshBuf();
            public readonly MeshBuf textil = new MeshBuf(), ceramica = new MeshBuf(), paja = new MeshBuf(), fleco = new MeshBuf();
            public readonly List<(Vector3 center, Vector3 size)> boxes = new List<(Vector3, Vector3)>();
            public readonly List<BurntTree> trees = new List<BurntTree>();
            /// <summary>Azar propio de los rebrotes: así no altera la forma de los obstáculos ya construidos.</summary>
            public System.Random life;

            public float Life(float min, float max) => VillageGeo.Range(life, min, max);

            public float Rand(float min, float max) => VillageGeo.Range(rng, min, max);

            /// <summary>Altura del terreno en un punto del obstáculo (x a la derecha, z en el sentido de la marcha).</summary>
            public float GroundY(float lx, float lz)
            {
                Vector2 f = Forward(def.yaw), r = Right(def.yaw);
                return Ground(terrain, def.center.x + r.x * lx + f.x * lz, def.center.y + r.y * lx + f.y * lz) - y0;
            }

            public Vector3 P(float lx, float above, float lz) => new Vector3(lx, GroundY(lx, lz) + above, lz);
        }

        /// <summary>Un árbol quemado del obstáculo y los puntos por los que rebrota (en coordenadas del obstáculo).</summary>
        private sealed class BurntTree
        {
            public bool fallen;
            public Vector3 foot;
            public readonly List<Vector3> points = new List<Vector3>();
            public readonly List<Vector3> directions = new List<Vector3>();
            public readonly List<float> sizes = new List<float>();
            public readonly List<int> kinds = new List<int>();

            public void Shoot(Vector3 point, Vector3 direction, float size, int kind)
            {
                points.Add(point);
                directions.Add(direction.normalized);
                sizes.Add(size);
                kinds.Add(kind);
            }
        }

        /// <summary>Calcula en memoria todas las piezas de un obstáculo (mallas, bloqueos y árboles quemados).</summary>
        private static ObstacleBuild Compose(Obstacle def, Terrain terrain)
        {
            var o = new ObstacleBuild
            {
                def = def,
                terrain = terrain,
                y0 = Ground(terrain, def.center.x, def.center.y),
                rng = new System.Random(def.seed),
                life = new System.Random(def.seed * 7919 + 13),
            };

            switch (def.kind)
            {
                case ObstacleKind.Tronco:
                    Trunk(o, 0f, o.Rand(-0.8f, 0.8f));
                    break;
                case ObstacleKind.CarretaVolcada:
                    OverturnedCart(o);
                    break;
                case ObstacleKind.TrincheraCazador:
                    HunterTrench(o);
                    break;
                case ObstacleKind.PortadaRuina:
                    RuinedArch(o);
                    break;
                case ObstacleKind.MuroCaido:
                    BrokenWall(o);
                    break;
                case ObstacleKind.Derrumbe:
                    RockSlide(o);
                    break;
                case ObstacleKind.PilaVigas:
                    TimberDepot(o);
                    break;
                default:
                    Chicane(o);
                    break;
            }
            Snags(o, def.kind == ObstacleKind.Derrumbe ? 5 : 4);
            return o;
        }

        /// <summary>Cuelga del obstáculo una marca por cada árbol quemado, con sus puntos de rebrote.</summary>
        private static int AddBurntTrees(GameObject go, ObstacleBuild o)
        {
            var wood = new List<Renderer>();
            foreach (string part in new[] { "Troncos", "Ramas" })
            {
                Transform piece = go.transform.Find(part);
                Renderer renderer = piece != null ? piece.GetComponent<Renderer>() : null;
                if (renderer != null) wood.Add(renderer);
            }

            for (int i = 0; i < o.trees.Count; i++)
            {
                BurntTree tree = o.trees[i];
                var marker = new GameObject((tree.fallen ? "ArbolCaido_" : "ArbolQuemado_") + (i + 1));
                marker.transform.SetParent(go.transform, false);
                marker.transform.localPosition = tree.foot;
                var points = new Vector3[tree.points.Count];
                for (int p = 0; p < points.Length; p++) points[p] = tree.points[p] - tree.foot;
                marker.AddComponent<AyniBurntTree>().Setup(tree.fallen, wood.ToArray(), points, tree.directions.ToArray(),
                    tree.sizes.ToArray(), tree.kinds.ToArray());
            }
            return o.trees.Count;
        }

        /// <summary>
        /// Pone (o renueva) las marcas de árbol quemado en los obstáculos que ya están en la escena, sin reconstruir la aldea.
        /// </summary>
        [UnityEditor.MenuItem("Ayni/Entorno/Marcar Árboles Quemados del Camino")]
        public static void MarkBurntTrees()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Aldea] Sal del modo Play antes de marcar los árboles.");
                return;
            }
            Terrain terrain = Terrain.activeTerrain;
            GameObject envRoot = AyniEnvironmentBuilder.GetOrCreateRoot();
            Transform parent = envRoot != null ? envRoot.transform.Find(RootName + "/Obstaculos") : null;
            if (terrain == null || parent == null)
            {
                Debug.LogError("[Ayni Aldea] No están los obstáculos en la escena: construye antes la aldea.");
                return;
            }

            int standing = 0, fallen = 0, shoots = 0;
            foreach (Obstacle def in Obstacles)
            {
                Transform go = parent.Find(def.id + "_" + def.kind);
                if (go == null) continue;
                foreach (AyniBurntTree old in go.GetComponentsInChildren<AyniBurntTree>(true)) Object.DestroyImmediate(old.gameObject);

                ObstacleBuild o = Compose(def, terrain);
                AddBurntTrees(go.gameObject, o);
                foreach (BurntTree tree in o.trees)
                {
                    if (tree.fallen) fallen++; else standing++;
                    shoots += tree.points.Count;
                }
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[Ayni Aldea]</color> Árboles quemados marcados: {standing} en pie y {fallen} caídos, con {shoots} puntos de rebrote.");
        }

        [UnityEditor.MenuItem("Ayni/Entorno/Reconstruir Solo Obstáculos del Camino")]
        public static void RebuildObstaclesOnly()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Aldea] Sal del modo Play antes de reconstruir los obstáculos.");
                return;
            }
            Terrain terrain = Terrain.activeTerrain;
            GameObject envRoot = AyniEnvironmentBuilder.GetOrCreateRoot();
            Transform village = envRoot != null ? envRoot.transform.Find(RootName) : null;
            if (terrain == null || village == null)
            {
                Debug.LogError("[Ayni Aldea] No se encontró Aldea_Inca en la escena.");
                return;
            }

            Transform old = village.Find("Obstaculos");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(ObstaclesMeshPath) != null)
            {
                UnityEditor.AssetDatabase.DeleteAsset(ObstaclesMeshPath);
            }

            EnsureFolder();
            Materials mats = BuildMaterials();
            var meshes = new List<Mesh>();
            BuildObstacles(terrain, village, mats, meshes);

            if (meshes.Count > 0)
            {
                UnityEditor.AssetDatabase.CreateAsset(meshes[0], ObstaclesMeshPath);
                for (int i = 1; i < meshes.Count; i++) UnityEditor.AssetDatabase.AddObjectToAsset(meshes[i], ObstaclesMeshPath);
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[Ayni Aldea]</color> {Obstacles.Length} obstáculos variados reconstruidos ({meshes.Count} mallas en {ObstaclesMeshPath}).");
            CheckObstacles();
        }

        private static void BuildObstacles(Terrain terrain, Transform root, Materials mats, List<Mesh> meshes)
        {
            var parent = new GameObject("Obstaculos");
            parent.transform.SetParent(root, false);

            foreach (Obstacle def in Obstacles)
            {
                ObstacleBuild o = Compose(def, terrain);

                var go = new GameObject(def.id + "_" + def.kind);
                go.transform.SetParent(parent.transform, false);
                go.transform.SetPositionAndRotation(new Vector3(def.center.x, o.y0, def.center.y), Quaternion.Euler(0f, def.yaw, 0f));
                AddPart(go.transform, "Rocas", o.roca, mats.roca, true, true, def.id, meshes);
                AddPart(go.transform, "Troncos", o.quemado, mats.quemado, true, true, def.id, meshes);
                AddPart(go.transform, "Ramas", o.ramas, mats.quemado, false, true, def.id, meshes);
                AddPart(go.transform, "Madera", o.madera, mats.madera, true, true, def.id, meshes);
                AddPart(go.transform, "Muro", o.pirca, mats.pirca, true, true, def.id, meshes);
                AddPart(go.transform, "Tallado", o.fina, mats.fina, true, true, def.id, meshes);
                AddPart(go.transform, "Tela", o.textil, mats.textil, false, true, def.id, meshes);
                AddPart(go.transform, "Ceramica", o.ceramica, mats.ceramica, true, true, def.id, meshes);
                AddPart(go.transform, "Paja", o.paja, mats.paja, false, true, def.id, meshes);
                AddPart(go.transform, "Fleco", o.fleco, mats.fleco, false, false, def.id, meshes);
                foreach (var (center, size) in o.boxes)
                {
                    var box = new GameObject("Bloqueo");
                    box.transform.SetParent(go.transform, false);
                    box.transform.localPosition = center;
                    box.AddComponent<BoxCollider>().size = size;
                }
                AddBurntTrees(go, o);
            }
        }

        // ───────────────────────── Piezas ─────────────────────────

        /// <summary>Hilera de peñascos a cada lado del camino, talud arriba.</summary>
        private static void Flanks(ObstacleBuild o, float z)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (float x = RoadHalf + 0.9f; x < FlankEnd; x += 1.9f)
                {
                    float ry = o.Rand(1.5f, 2.1f);
                    float lx = side * (x + o.Rand(-0.2f, 0.2f)), lz = z + o.Rand(-0.45f, 0.45f);
                    VillageGeo.Rock(o.roca, o.P(lx, ry * 0.3f, lz), new Vector3(o.Rand(1.15f, 1.45f), ry, o.Rand(1.1f, 1.4f)), o.Rand(0f, 360f), o.rng);
                    // Alguna piedra menor rodada al pie, para que no parezca una fila puesta a cordel
                    if (x > RoadHalf + 2.5f && o.rng.Next(3) == 0)
                    {
                        float small = o.Rand(0.35f, 0.7f);
                        float sx = lx + o.Rand(-0.8f, 0.8f), sz = lz + (o.rng.Next(2) == 0 ? -1f : 1f) * o.Rand(1.5f, 2.2f);
                        VillageGeo.Rock(o.roca, o.P(sx, small * 0.35f, sz), new Vector3(small * o.Rand(0.9f, 1.3f), small, small * o.Rand(0.9f, 1.2f)), o.Rand(0f, 360f), o.rng);
                    }
                }
            }
        }

        /// <summary>Árbol quemado caído a través del camino, partido por el medio para que asiente sobre el suelo.</summary>
        private static void Trunk(ObstacleBuild o, float z, float skew)
        {
            float half = RoadHalf + 1.4f;
            Vector3 a = o.P(-half, 0.27f, z - skew);
            Vector3 mid = o.P(o.Rand(-1.2f, 1.2f), 0.27f, z + o.Rand(-0.15f, 0.15f));
            Vector3 b = o.P(half, 0.27f, z + skew);
            VillageGeo.Tube(o.quemado, a, mid, 0.33f, 0.3f, 9, true, 0.6f);
            VillageGeo.Tube(o.quemado, mid, b, 0.3f, 0.25f, 9, true, 0.6f);

            // Si el bosque renace, el tronco caído rebrota: de cada muñón y de la propia corteza salen varas nuevas
            var tree = new BurntTree { fallen = true, foot = mid };
            o.trees.Add(tree);

            // Muñones de ramas (no chocan: son finos y engancharían el salto)
            for (int i = 0; i < 7; i++)
            {
                float t = o.Rand(0.06f, 0.94f);
                Vector3 p = t < 0.5f ? Vector3.Lerp(a, mid, t * 2f) : Vector3.Lerp(mid, b, t * 2f - 1f);
                Vector3 dir = new Vector3(o.Rand(-0.35f, 0.35f), o.Rand(0.25f, 1f), o.Rand(-1f, 1f)).normalized;
                float length = o.Rand(0.45f, 1.0f);
                VillageGeo.Tube(o.ramas, p, p + dir * length, 0.075f, 0.025f, 5, true);
                tree.Shoot(p + dir * (length - 0.04f), Vector3.Slerp(dir, Vector3.up, 0.6f), o.Life(1.0f, 1.6f), AyniBurntTree.Shoot);
            }
            for (int i = 0; i < 4; i++)
            {
                float t = (i + o.Life(0.15f, 0.85f)) / 4f;
                Vector3 p = t < 0.5f ? Vector3.Lerp(a, mid, t * 2f) : Vector3.Lerp(mid, b, t * 2f - 1f);
                Vector3 dir = new Vector3(o.Life(-0.2f, 0.2f), 1f, o.Life(-0.35f, 0.35f));
                tree.Shoot(p + Vector3.up * 0.24f, dir, o.Life(0.9f, 1.4f), AyniBurntTree.Shoot);
            }
            // Raíces arrancadas en un extremo
            for (int i = 0; i < 6; i++)
            {
                float ang = (i * 60f + o.Rand(-15f, 15f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(-0.45f, Mathf.Cos(ang), Mathf.Sin(ang)).normalized;
                VillageGeo.Tube(o.ramas, a, a + dir * o.Rand(0.6f, 1.1f), 0.11f, 0.03f, 5, true);
            }
            Flanks(o, z);
        }

        /// <summary>
        /// Derrumbe: tres hileras de peñascos que cruzan el camino. Cada una deja un paso, y los pasos alternan de lado:
        /// hay que serpentear (derecha, izquierda, derecha, o al revés).
        /// </summary>
        private static void RockSlide(ObstacleBuild o)
        {
            float s = o.def.Flip ? -1f : 1f;
            for (int row = -1; row <= 1; row++)
            {
                float z = row * SlideRowZ;
                float side = row == 0 ? -s : s;         // lado del camino por el que se pasa en esta hilera
                float wallEnd = 1.6f;                   // la hilera llega hasta aquí pasando del centro
                // Tramo largo: desde el talud contrario hasta más allá del eje
                const int count = 6;
                for (int i = 0; i < count; i++)
                {
                    float x = -side * Mathf.Lerp(RoadHalf + 0.6f, -wallEnd, (float)i / (count - 1));
                    SlideRock(o, x, z);
                }
                // Tramo corto: del paso al talud de su lado
                SlideRock(o, side * (wallEnd + SlideRadius + SlideGap + SlideRadius), z);
                Flanks(o, z);
            }
        }

        private static void SlideRock(ObstacleBuild o, float x, float z)
        {
            float ry = o.Rand(1.5f, 2.1f);
            Vector3 at = o.P(x + o.Rand(-0.1f, 0.1f), ry * 0.3f, z + o.Rand(-0.12f, 0.12f));
            VillageGeo.Rock(o.roca, at, new Vector3(SlideRadius + o.Rand(-0.03f, 0.1f), ry, SlideRadius + o.Rand(-0.05f, 0.1f)), o.Rand(0f, 360f), o.rng);
        }

        /// <summary>Fila de estacas afiladas con sus dos travesaños.</summary>
        private static void Palisade(ObstacleBuild o, float x0, float x1, float z, float height)
        {
            float width = Mathf.Abs(x1 - x0);
            int count = Mathf.Max(2, Mathf.RoundToInt(width / 0.27f));
            for (int i = 0; i <= count; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)i / count);
                float h = height + o.Rand(-0.18f, 0.2f);
                float lean = o.Rand(-0.05f, 0.05f);
                Vector3 foot = o.P(x, -0.4f, z + o.Rand(-0.04f, 0.04f));
                Vector3 neck = o.P(x, h - 0.32f, z) + new Vector3(lean, 0f, o.Rand(-0.05f, 0.05f));
                Vector3 tip = neck + new Vector3(lean * 0.3f, 0.34f, 0f);
                VillageGeo.Tube(o.madera, foot, neck, 0.085f, 0.075f, 5, false);
                VillageGeo.Tube(o.madera, neck, tip, 0.075f, 0.012f, 5, true);
            }
            int pieces = Mathf.Max(1, Mathf.RoundToInt(width / 3f));
            foreach (float railHeight in new[] { 0.65f, 1.45f })
            {
                for (int j = 0; j < pieces; j++)
                {
                    float xa = Mathf.Lerp(x0, x1, (float)j / pieces), xb = Mathf.Lerp(x0, x1, (float)(j + 1) / pieces);
                    VillageGeo.Tube(o.madera, o.P(xa, railHeight, z - 0.11f), o.P(xb, railHeight, z - 0.11f), 0.045f, 0.045f, 5, true);
                }
            }
            float mid = (x0 + x1) * 0.5f;
            o.boxes.Add((o.P(mid, height * 0.5f, z), new Vector3(width + 0.2f, height + 2.4f, 0.34f)));
        }

        /// <summary>Lado del camino por el que se entra a la empalizada (el otro paso queda en el lado contrario).</summary>
        private static float ChicaneSide(Obstacle def) => def.Flip ? -1f : 1f;

        /// <summary>Empalizada doble: se entra por un lado y se sale por el contrario.</summary>
        private static void Chicane(ObstacleBuild o)
        {
            float s = ChicaneSide(o.def);
            float open = RoadHalf - ChicaneGap;
            Palisade(o, -s * (RoadHalf + 1f), s * open, -ChicaneZ, 2.1f);
            Palisade(o, -s * open, s * (RoadHalf + 1f), ChicaneZ, 2.1f);
            Flanks(o, -ChicaneZ);
            Flanks(o, ChicaneZ);

            // Estandarte de la banda junto al primer paso
            Vector3 foot = o.P(s * (open - 0.5f), -0.3f, -ChicaneZ - 0.35f);
            Vector3 top = foot + Vector3.up * 3.9f;
            VillageGeo.Tube(o.madera, foot, top, 0.07f, 0.05f, 6, true);
            Vector3 barA = top + new Vector3(-0.55f, -0.18f, 0f), barB = top + new Vector3(0.55f, -0.18f, 0f);
            VillageGeo.Tube(o.madera, barA, barB, 0.035f, 0.035f, 5, true);
            VillageGeo.Cloth(o.textil, barA + new Vector3(0.06f, -0.03f, 0f), barB + new Vector3(-0.06f, -0.03f, 0f),
                barB + new Vector3(-0.06f, -1.55f, 0f), barA + new Vector3(0.06f, -1.55f, 0f), TextileRect(0), Vector3.back, 0.04f, o.rng);
        }

        /// <summary>Caja o bloque de madera/pirca asentado sobre el terreno (permite saltar o trepar por encima).</summary>
        private static void Crate(ObstacleBuild o, MeshBuf buf, float cx, float cz, float width, float depth, float height, float yawDeg, bool drapeCloth = false, int clothTex = 0)
        {
            float baseY = o.GroundY(cx, cz) - 0.12f;
            var f = new Frame(new Vector3(cx, baseY, cz), yawDeg);
            VillageGeo.Frustum(buf, f, 0f, 0f, 0f, height + 0.12f, width * 0.5f, depth * 0.5f, width * 0.48f, depth * 0.48f);
            if (drapeCloth)
            {
                float top = height + 0.14f;
                Vector3 tl = f.P(-width * 0.44f, top, -depth * 0.44f);
                Vector3 tr = f.P( width * 0.44f, top, -depth * 0.44f);
                Vector3 br = f.P( width * 0.44f, top,  depth * 0.44f);
                Vector3 bl = f.P(-width * 0.44f, top,  depth * 0.44f);
                VillageGeo.Cloth(o.textil, tl, tr, br, bl, TextileRect(clothTex), Vector3.up, 0.025f, o.rng);
            }
        }

        /// <summary>Caballete de estacas cruzadas en aspa (caballo de frisa andino).</summary>
        private static void CrossStakes(ObstacleBuild o, float x0, float x1, float z, float height)
        {
            float width = Mathf.Abs(x1 - x0);
            int pairs = Mathf.Max(2, Mathf.RoundToInt(width / 0.85f));
            // Viga central horizontal que une las aspas
            VillageGeo.Tube(o.madera, o.P(x0, height * 0.48f, z), o.P(x1, height * 0.48f, z), 0.11f, 0.11f, 7, true);
            for (int i = 0; i <= pairs; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)i / pairs);
                float h = height + o.Rand(-0.12f, 0.16f);
                float spread = 0.65f;
                // Aspa delantera-trasera inclinada hacia el caminante
                VillageGeo.Tube(o.madera, o.P(x, -0.15f, z + spread), o.P(x + o.Rand(-0.06f, 0.06f), h, z - spread), 0.08f, 0.02f, 6, true);
                VillageGeo.Tube(o.madera, o.P(x, -0.15f, z - spread), o.P(x + o.Rand(-0.06f, 0.06f), h, z + spread), 0.08f, 0.02f, 6, true);
            }
            float mid = (x0 + x1) * 0.5f;
            o.boxes.Add((o.P(mid, height * 0.5f, z), new Vector3(width + 0.25f, height + 2.0f, 1.1f)));
        }

        /// <summary>Flancos de muro roto de pirca para obstáculos arquitectónicos (sin repetir rocas genéricas).</summary>
        private static void WallFlanks(ObstacleBuild o, float z)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side * (RoadHalf + 0.4f);
                float x1 = side * (FlankEnd - 1.5f);
                float ga = o.GroundY(x0, z), gb = o.GroundY(x1, z);
                var spec = new WallSpec
                {
                    a = new Vector3(Mathf.Min(x0, x1), Mathf.Min(ga, gb) - 0.6f, z + 0.35f),
                    b = new Vector3(Mathf.Max(x0, x1), Mathf.Min(ga, gb) - 0.6f, z + 0.35f),
                    outward = Vector3.forward,
                    height = 2.1f + Mathf.Abs(ga - gb),
                    thick = 0.72f,
                    batterOut = 0.06f,
                    batterIn = 0.06f,
                    capStart = true,
                    capEnd = true,
                    uvShift = o.Rand(0f, 25f),
                };
                VillageGeo.Wall(o.pirca, o.fina, spec);
            }
        }

        /// <summary>
        /// O2 · Cargamento de tambo volcado: carreta/parihuela de madera atravesada, fardos de aguayo y cántaros rotos.
        /// Deja un tramo bajo (0.70 m) en el centro para saltar en carrera y fardos altos a los costados.
        /// </summary>
        private static void OverturnedCart(ObstacleBuild o)
        {
            float s = o.def.Flip ? -1f : 1f;
            // Costado izquierdo: armazón de carreta volcada con toldo de aguayo y fardos apilados (altos, bloquean)
            Crate(o, o.madera, -s * 4.9f, -0.4f, 3.6f, 1.8f, 1.55f, s * 12f, true, 1);
            Crate(o, o.madera, -s * 2.4f,  0.3f, 2.2f, 1.5f, 1.25f, -s * 8f, true, 2);
            // Varas largas de la carreta sobresaliendo en diagonal
            VillageGeo.Tube(o.madera, o.P(-s * 6.2f, 0.25f, -1.6f), o.P(-s * 2.8f, 1.45f, 1.2f), 0.09f, 0.07f, 6, true);
            VillageGeo.Tube(o.madera, o.P(-s * 5.6f, 0.25f,  1.2f), o.P(-s * 2.2f, 1.35f, -1.5f), 0.09f, 0.07f, 6, true);
            // Tramo central saltable: fardos y cajón bajo de 0.72 m cubierto de tela andina
            Crate(o, o.madera,  s * 0.2f,  0.0f, 3.4f, 1.0f, 0.72f, s * 4f, true, 0);
            // Costado derecho: cajones apilados + tinajas de cerámica (aríbalos) derramadas
            Crate(o, o.madera,  s * 3.8f, -0.3f, 3.2f, 1.7f, 1.45f, -s * 14f, true, 3);
            Crate(o, o.madera,  s * 6.4f,  0.2f, 2.8f, 1.6f, 1.65f, s * 9f, false);
            for (int i = 0; i < 6; i++)
            {
                float jx = o.Rand(-5.5f, 5.5f);
                float jz = (i % 2 == 0 ? -1.45f : 1.45f) + o.Rand(-0.45f, 0.45f);
                float sc = o.Rand(0.85f, 1.35f);
                VillageGeo.Lathe(o.ceramica, o.P(jx, -0.05f, jz), RoadJarProfile, 10, sc, o.Rand(0f, 1f));
            }
            // Cierre lateral con rocas del talud (orgánico, sin bloques negros)
            Flanks(o, 0f);
        }

        /// <summary>
        /// O3 · Trinchera de caza de Amaru: caballos de frisa de estacas cruzadas, pendón de guerra y viga central baja saltable.
        /// </summary>
        private static void HunterTrench(ObstacleBuild o)
        {
            float s = o.def.Flip ? -1f : 1f;
            // Caballos de frisa altos a ambos lados
            CrossStakes(o, -RoadHalf - 1.2f, -2.0f, -0.3f, 1.75f);
            CrossStakes(o,  2.2f, RoadHalf + 1.2f,   0.3f, 1.75f);
            // Paso central con travesaño bajo de madera (0.76 m) para saltar apoyando la mano (Vault)
            VillageGeo.Tube(o.madera, o.P(-2.2f, 0.74f, 0f), o.P(2.4f, 0.74f, 0f), 0.14f, 0.14f, 8, true);
            VillageGeo.Tube(o.madera, o.P(-2.2f, 0.32f, 0f), o.P(2.4f, 0.32f, 0f), 0.10f, 0.10f, 6, true);
            o.boxes.Add((o.P(0.1f, 0.38f, 0f), new Vector3(4.6f, 0.76f, 0.48f)));
            // Dos estandartes de los cazadores flanqueando el paso central
            foreach (float bx in new[] { -2.1f, 2.3f })
            {
                Vector3 foot = o.P(bx, -0.3f, -0.55f);
                Vector3 top = foot + Vector3.up * 4.1f;
                VillageGeo.Tube(o.madera, foot, top, 0.08f, 0.05f, 6, true);
                Vector3 barA = top + new Vector3(-0.55f, -0.2f, 0f), barB = top + new Vector3(0.55f, -0.2f, 0f);
                VillageGeo.Tube(o.madera, barA, barB, 0.04f, 0.04f, 5, true);
                VillageGeo.Cloth(o.textil, barA + new Vector3(0.05f, -0.04f, 0f), barB + new Vector3(-0.05f, -0.04f, 0f),
                    barB + new Vector3(-0.05f, -1.65f, 0f), barA + new Vector3(0.05f, -1.65f, 0f), TextileRect(bx < 0 ? 0 : 2), Vector3.back, 0.05f, o.rng);
            }
            // Empalizada de aspas hacia los taludes
            CrossStakes(o, -FlankEnd, -RoadHalf - 1.0f, -0.5f * s, 1.9f);
            CrossStakes(o,  RoadHalf + 1.0f, FlankEnd,   0.5f * s, 1.9f);
        }

        /// <summary>
        /// O4 · Portada Inca derrumbada: dos pilares trapezoidales altos en pie, dintel tallado caído (0.68 m, saltable)
        /// y bloques de sillar dispersos.
        /// </summary>
        private static void RuinedArch(ObstacleBuild o)
        {
            // Dos pilares trapezoidales altos flanqueando el centro del camino
            foreach (float side in new[] { -1f, 1f })
            {
                float px = side * 3.1f;
                float py = o.GroundY(px, 0f) - 0.5f;
                var f = new Frame(new Vector3(px, py, 0f), 0f);
                VillageGeo.Frustum(o.pirca, f, 0f, 0f, 0f, 3.9f, 1.15f, 0.95f, 0.85f, 0.72f);
                // Cornisa de piedra fina arriba
                VillageGeo.Frustum(o.fina, f, 0f, 0f, 3.85f, 4.18f, 0.96f, 0.82f, 0.92f, 0.78f);
                // Muros laterales desde cada pilar hacia afuera
                float outerX = side * (RoadHalf + 0.9f);
                float ga = o.GroundY(px + side * 0.9f, 0f), gb = o.GroundY(outerX, 0f);
                var spec = new WallSpec
                {
                    a = new Vector3(Mathf.Min(px + side * 0.9f, outerX), Mathf.Min(ga, gb) - 0.5f, 0.42f),
                    b = new Vector3(Mathf.Max(px + side * 0.9f, outerX), Mathf.Min(ga, gb) - 0.5f, 0.42f),
                    outward = Vector3.forward,
                    height = 2.0f + Mathf.Abs(ga - gb),
                    thick = 0.84f,
                    batterOut = 0.06f,
                    batterIn = 0.06f,
                    capStart = true,
                    capEnd = true,
                    uvShift = o.Rand(0f, 20f),
                };
                VillageGeo.Wall(o.pirca, o.fina, spec);
            }

            // Dintel monumental tallado caído en el vano central: 0.68 m de altura, perfecto para saltar con apoyo de mano
            Crate(o, o.fina, 0f, 0.15f, 4.4f, 0.95f, 0.68f, 3.5f);
            // Bloques de sillar caídos delante/detrás del vano como detalle
            Crate(o, o.fina, -1.6f, -1.5f, 0.85f, 0.65f, 0.36f, 22f);
            Crate(o, o.fina,  1.8f,  1.6f, 0.95f, 0.7f,  0.34f, -18f);
            WallFlanks(o, 0f);
        }

        /// <summary>
        /// O7 · Depósito incendiado de vigas y fardos: dos obstáculos escalonados seguidos (viga carbonizada 0.62 m +
        /// plataforma de cajones y techo de ichu derrumbado 0.85 m) para encadenar dos saltos de parkour.
        /// </summary>
        private static void TimberDepot(ObstacleBuild o)
        {
            // Primera línea (-2.3 m): viga gruesa de cumbrera caída sobre todo el camino (0.62 m, saltable)
            float half = RoadHalf + 1.2f;
            VillageGeo.Tube(o.quemado, o.P(-half, 0.30f, -2.5f), o.P(0f, 0.32f, -2.2f), 0.32f, 0.30f, 8, true);
            VillageGeo.Tube(o.quemado, o.P(0f, 0.32f, -2.2f), o.P(half, 0.30f, -2.6f), 0.30f, 0.28f, 8, true);
            var burntBeam = new BurntTree { fallen = true, foot = o.P(0f, 0.32f, -2.2f) };
            o.trees.Add(burntBeam);
            for (int i = 0; i < 4; i++)
            {
                float lx = Mathf.Lerp(-4.5f, 4.5f, i / 3f);
                burntBeam.Shoot(o.P(lx, 0.55f, -2.35f), new Vector3(o.Life(-0.2f, 0.2f), 1f, o.Life(-0.2f, 0.2f)), o.Life(0.9f, 1.4f), AyniBurntTree.Shoot);
            }

            // Segunda línea (+2.4 m): bloques de acopio y techo de paja desplomado en un lado
            Crate(o, o.madera, -4.6f, 2.4f, 4.8f, 1.5f, 1.45f, -5f, true, 1);
            Crate(o, o.madera,  0.0f, 2.4f, 4.4f, 1.15f, 0.82f, 2f, true, 3); // tramo central saltable/montable
            Crate(o, o.madera,  4.8f, 2.4f, 4.8f, 1.5f, 1.55f, 7f, false);
            // Techo de ichu vencido apoyado sobre los cajones de la derecha
            var roofFrame = new Frame(o.P(4.8f, 1.4f, 2.4f), 8f);
            VillageGeo.GableRoof(o.paja, o.fleco, o.madera, roofFrame, 4.6f, 2.4f, 0f, 0.9f, 0.1f, 0.35f, 0.2f, o.rng);
            // Tinajas en los bordes
            for (int i = 0; i < 4; i++)
            {
                float jx = (i < 2 ? -5.8f : 5.8f) + o.Rand(-0.7f, 0.7f);
                float jz = 0.2f + o.Rand(-1.0f, 1.0f);
                VillageGeo.Lathe(o.ceramica, o.P(jx, -0.05f, jz), RoadJarProfile, 10, o.Rand(0.9f, 1.25f), o.Rand(0f, 1f));
            }
            WallFlanks(o, 2.4f);
            Flanks(o, -2.4f);
        }

        /// <summary>Muro de pirca a medio caer: cada tramo tiene una altura y uno está casi en el suelo.</summary>
        private static void BrokenWall(ObstacleBuild o)
        {
            float s = o.def.Flip ? -1f : 1f;
            float[] edges = { -8.2f, -5.4f, -2.6f, 0.4f, 2.6f, 4.8f, 8.2f };
            float[] heights = { 1.45f, 1.15f, 1.35f, 0.95f, 0.55f, 1.35f }; // tramo bajo de 0.55 m para saltar y andén de 1.15 m para trepar
            for (int i = 0; i < heights.Length; i++)
            {
                if (heights[i] <= 0f) continue;
                float xa = s * edges[i], xb = s * edges[i + 1];
                float ga = o.GroundY(xa, 0f), gb = o.GroundY(xb, 0f), gm = o.GroundY((xa + xb) * 0.5f, 0f);
                float low = Mathf.Min(ga, Mathf.Min(gb, gm)), high = Mathf.Max(ga, Mathf.Max(gb, gm));
                var spec = new WallSpec
                {
                    a = new Vector3(xa, low - 0.5f, 0.38f),
                    b = new Vector3(xb, low - 0.5f, 0.38f),
                    outward = Vector3.forward,
                    height = heights[i] + 0.5f + (high - low) * 0.5f + o.Rand(-0.05f, 0.05f),
                    thick = 0.85f,
                    batterOut = 0.05f,
                    batterIn = 0.05f,
                    capStart = true,
                    capEnd = true,
                    uvShift = o.Rand(0f, 30f),
                };
                VillageGeo.Wall(o.pirca, o.fina, spec);
            }
            WallFlanks(o, 0f);
        }

        /// <summary>Árboles quemados aún en pie alrededor: el bosque que arrasó el Cazador.</summary>
        private static void Snags(ObstacleBuild o, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float side = o.rng.Next(2) == 0 ? -1f : 1f;
                float x = side * o.Rand(RoadHalf + 3.5f, 23f), z = o.Rand(-12f, 12f);
                float h = o.Rand(2.4f, 5.4f);
                Vector3 foot = o.P(x, -0.4f, z);
                Vector3 top = foot + new Vector3(o.Rand(-0.5f, 0.5f), h + 0.4f, o.Rand(-0.5f, 0.5f));
                float baseRadius = o.Rand(0.2f, 0.3f), topRadius = o.Rand(0.05f, 0.1f);
                VillageGeo.Tube(o.quemado, foot, top, baseRadius, topRadius, 7, true, 0.6f);

                // Si el bosque renace: copa nueva en la punta, y varas en cada rama rota y a lo largo del tronco
                Vector3 axis = (top - foot).normalized;
                var tree = new BurntTree { fallen = false, foot = foot + axis * (0.4f / Mathf.Max(0.2f, axis.y)) };
                o.trees.Add(tree);
                tree.Shoot(top - axis * 0.12f, axis, Mathf.Lerp(0.8f, 1.15f, Mathf.InverseLerp(2.4f, 5.4f, h)), AyniBurntTree.Crown);

                int branches = 1 + o.rng.Next(3);
                for (int b = 0; b < branches; b++)
                {
                    Vector3 p = Vector3.Lerp(foot, top, o.Rand(0.45f, 0.88f));
                    Vector3 dir = new Vector3(o.Rand(-1f, 1f), o.Rand(0.15f, 0.7f), o.Rand(-1f, 1f)).normalized;
                    float length = o.Rand(0.6f, 1.4f);
                    VillageGeo.Tube(o.ramas, p, p + dir * length, 0.06f, 0.02f, 5, true);
                    tree.Shoot(p + dir * (length - 0.05f), Vector3.Slerp(dir, Vector3.up, 0.45f), o.Life(1.1f, 1.7f), AyniBurntTree.Shoot);
                }
                for (int e = 0; e < 3; e++)
                {
                    float along = o.Life(0.3f, 0.82f);
                    float angle = o.Life(0f, Mathf.PI * 2f);
                    Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 p = Vector3.Lerp(foot, top, along) + outward * (Mathf.Lerp(baseRadius, topRadius, along) * 0.8f);
                    tree.Shoot(p, outward + Vector3.up * o.Life(0.7f, 1.3f), o.Life(0.9f, 1.5f), AyniBurntTree.Shoot);
                }
            }
        }

        // ───────────────────────── Comprobación y pruebas ─────────────────────────

        private static Vector3 ObstacleWorld(Obstacle def, Terrain terrain, float lx, float lz, float above)
        {
            Vector2 f = Forward(def.yaw), r = Right(def.yaw);
            float x = def.center.x + r.x * lx + f.x * lz, z = def.center.y + r.y * lx + f.y * lz;
            return new Vector3(x, Ground(terrain, x, z) + above, z);
        }

        /// <summary>¿Cabe la cápsula de Yari (medio metro de radio) en este punto, sin contar el terreno?</summary>
        private static bool Fits(Vector3 feet)
        {
            foreach (Collider hit in Physics.OverlapSphere(feet + Vector3.up * 1.0f, 0.58f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!(hit is TerrainCollider)) return false;
            }
            return true;
        }

        /// <summary>
        /// Comprueba con la física cada obstáculo: busca el camino más corto A PIE (sin saltar) de un lado al otro,
        /// dentro de la franja que cierran los peñascos. Los troncos no deben tener ninguno (se saltan); el derrumbe,
        /// la empalizada y el muro deben tener uno, y más largo que ir recto. Se ejecuta al construir la aldea.
        /// </summary>
        [UnityEditor.MenuItem("Ayni/Entorno/Comprobar Obstáculos del Camino")]
        public static void CheckObstacles()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            Physics.SyncTransforms();
            var sb = new System.Text.StringBuilder("[Ayni Aldea] Obstáculos (camino a pie de 10 m antes a 10 m después):");
            foreach (Obstacle def in Obstacles)
            {
                float length = WalkAcross(def, terrain);
                bool zigzagOnFoot = def.kind == ObstacleKind.Derrumbe || def.kind == ObstacleKind.Empalizada;
                string verdict;
                if (!zigzagOnFoot) verdict = length < 0f ? "cerrado a pie, hay que saltar/trepar (bien)" : $"SE PUEDE RODEAR A PIE ({length:F1} m): revisar";
                else if (length < 0f) verdict = "NO SE PUEDE PASAR: revisar";
                else verdict = $"se pasa a pie recorriendo {length:F1} m (recto serían 20)";
                sb.Append("\n   ").Append(def.id).Append(' ').Append(def.kind).Append(": ").Append(verdict);
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>Longitud del camino más corto a pie a través del obstáculo, o -1 si no hay.</summary>
        private static float WalkAcross(Obstacle def, Terrain terrain)
        {
            const float step = 0.5f, halfX = 16f, halfZ = 10f;
            int nx = Mathf.RoundToInt(halfX * 2f / step) + 1, nz = Mathf.RoundToInt(halfZ * 2f / step) + 1;
            var free = new bool[nx, nz];
            var dist = new float[nx, nz];
            var done = new bool[nx, nz];
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    free[ix, iz] = Fits(ObstacleWorld(def, terrain, -halfX + ix * step, -halfZ + iz * step, 0f));
                    dist[ix, iz] = float.MaxValue;
                }
            }
            // Se parte de cualquier punto del camino 10 m antes del obstáculo
            for (int ix = 0; ix < nx; ix++)
            {
                if (free[ix, 0] && Mathf.Abs(-halfX + ix * step) <= RoadHalf) dist[ix, 0] = 0f;
            }
            while (true)
            {
                int bx = -1, bz = -1;
                float best = float.MaxValue;
                for (int ix = 0; ix < nx; ix++)
                {
                    for (int iz = 0; iz < nz; iz++)
                    {
                        if (!done[ix, iz] && dist[ix, iz] < best) { best = dist[ix, iz]; bx = ix; bz = iz; }
                    }
                }
                if (bx < 0) return -1f;
                if (bz == nz - 1) return best;
                done[bx, bz] = true;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = bx + dx, z = bz + dz;
                        if (x < 0 || z < 0 || x >= nx || z >= nz || !free[x, z] || done[x, z]) continue;
                        if (dx != 0 && dz != 0 && (!free[bx + dx, bz] || !free[bx, bz + dz])) continue; // sin cortar esquinas
                        float cost = best + step * (dx != 0 && dz != 0 ? 1.4142f : 1f);
                        if (cost < dist[x, z]) dist[x, z] = cost;
                    }
                }
            }
        }

        /// <summary>Solo en Play: lleva a Yari junto a un obstáculo (x a la derecha, z en el sentido de la marcha).</summary>
        public static void TeleportObstacle(string id, float lx, float lz, float localYaw)
        {
            if (!Application.isPlaying) { Debug.Log("[Ayni Aldea] TeleportObstacle solo funciona en Play."); return; }
            Terrain terrain = Terrain.activeTerrain;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (terrain == null || player == null) return;
            foreach (Obstacle def in Obstacles)
            {
                if (def.id != id) continue;
                Vector3 p = ObstacleWorld(def, terrain, lx, lz, 0.1f);
                var mover = player.GetComponent<CharacterController>();
                if (mover != null) mover.enabled = false;
                player.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, def.yaw + localYaw, 0f));
                if (mover != null) mover.enabled = true;
                Camera cam = Camera.main;
                var follow = cam != null ? cam.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
                if (follow != null) follow.SnapBehindTarget();
                Debug.Log($"[Ayni Aldea] Yari en {id} ({lx:F1}, {lz:F1})");
                return;
            }
        }

        /// <summary>Solo en Play: dónde está Yari respecto a un obstáculo.</summary>
        public static string WhereObstacle(string id)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return "sin jugador";
            foreach (Obstacle def in Obstacles)
            {
                if (def.id != id) continue;
                Vector2 f = Forward(def.yaw), r = Right(def.yaw);
                Vector2 off = new Vector2(player.transform.position.x, player.transform.position.z) - def.center;
                string text = $"{id} local ({Vector2.Dot(off, r):F2}, {Vector2.Dot(off, f):F2}) y {player.transform.position.y:F2}";
                Debug.Log("[Ayni Aldea] Yari: " + text);
                return text;
            }
            return "obstáculo desconocido";
        }

        /// <summary>Foto desde un punto relativo a un obstáculo (fuera de Play).</summary>
        public static void ShotObstacle(string id, float px, float py, float pz, float tx, float ty, float tz, float fov, string name)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            foreach (Obstacle def in Obstacles)
            {
                if (def.id != id) continue;
                System.IO.Directory.CreateDirectory(CaptureFolder);
                Vector3 from = ObstacleWorld(def, terrain, px, pz, 0f), to = ObstacleWorld(def, terrain, tx, tz, 0f);
                float baseY = Ground(terrain, def.center.x, def.center.y);
                from.y = baseY + py;
                to.y = baseY + ty;
                Shot(name, from, to, fov);
                return;
            }
        }
    }
}
#endif
