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
        private enum ObstacleKind { Tronco, TroncoDoble, Derrumbe, Empalizada, MuroCaido }

        private sealed class Obstacle
        {
            public string id;
            public ObstacleKind kind;
            public Vector2 center; // sobre el eje del camino
            public float yaw;      // sentido de la marcha
            public int seed;
            public bool Flip => seed % 2 == 0; // a qué lado cae el paso
        }

        private static readonly Obstacle[] Obstacles =
        {
            new Obstacle { id = "O1", kind = ObstacleKind.Tronco, center = new Vector2(283.0f, 539.7f), yaw = 105.1f, seed = 11 },      // a 592 m de la plaza
            new Obstacle { id = "O2", kind = ObstacleKind.Derrumbe, center = new Vector2(294.5f, 506.0f), yaw = 145.0f, seed = 18 },    // 550 m
            new Obstacle { id = "O3", kind = ObstacleKind.Empalizada, center = new Vector2(360.3f, 441.7f), yaw = 155.5f, seed = 25 },  // 450 m
            new Obstacle { id = "O4", kind = ObstacleKind.TroncoDoble, center = new Vector2(368.7f, 414.5f), yaw = 155.3f, seed = 32 }, // 420 m
            new Obstacle { id = "O5", kind = ObstacleKind.MuroCaido, center = new Vector2(429.7f, 384.5f), yaw = 76.4f, seed = 39 },    // 335 m
            new Obstacle { id = "O6", kind = ObstacleKind.Derrumbe, center = new Vector2(450.4f, 386.0f), yaw = 65.4f, seed = 46 },     // 312 m
            new Obstacle { id = "O7", kind = ObstacleKind.Empalizada, center = new Vector2(484.6f, 475.6f), yaw = 9.9f, seed = 53 },    // 205 m
            new Obstacle { id = "O8", kind = ObstacleKind.TroncoDoble, center = new Vector2(497.9f, 507.0f), yaw = 37.2f, seed = 60 },  // 168 m
        };

        private const float RoadHalf = 7f;   // media anchura del camino que el obstáculo cierra del todo
        private const float FlankEnd = 17f;  // hasta dónde suben los peñascos por los taludes
        private const float SlideRadius = 1.25f;
        private const float SlideRowZ = 4.6f;  // separación entre las tres hileras del derrumbe
        private const float SlideGap = 2.6f;   // ancho del paso que deja cada hilera
        private const float ChicaneGap = 2.5f;
        private const float ChicaneZ = 2.7f;

        private sealed class ObstacleBuild
        {
            public Obstacle def;
            public Terrain terrain;
            public float y0;
            public System.Random rng;
            public readonly MeshBuf roca = new MeshBuf(), quemado = new MeshBuf(), ramas = new MeshBuf();
            public readonly MeshBuf madera = new MeshBuf(), pirca = new MeshBuf(), textil = new MeshBuf();
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
                case ObstacleKind.TroncoDoble:
                    Trunk(o, -2.6f, o.Rand(-0.9f, 0.2f));
                    Trunk(o, 2.6f, o.Rand(-0.2f, 0.9f));
                    break;
                case ObstacleKind.Derrumbe:
                    RockSlide(o);
                    break;
                case ObstacleKind.Empalizada:
                    Chicane(o);
                    break;
                default:
                    BrokenWall(o);
                    break;
            }
            Snags(o, 7);
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
                AddPart(go.transform, "Estacas", o.madera, mats.madera, false, true, def.id, meshes);
                AddPart(go.transform, "Muro", o.pirca, mats.pirca, true, true, def.id, meshes);
                AddPart(go.transform, "Tela", o.textil, mats.textil, false, true, def.id, meshes);
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

        /// <summary>Muro de pirca a medio caer: cada tramo tiene una altura y uno está casi en el suelo.</summary>
        private static void BrokenWall(ObstacleBuild o)
        {
            float s = o.def.Flip ? -1f : 1f;
            float[] edges = { -8.2f, -5.4f, -2.6f, 0.4f, 2.6f, 4.8f, 8.2f };
            float[] heights = { 1.25f, 0.95f, 1.15f, 0.8f, 0.5f, 1.2f }; // el quinto tramo está casi caído: es por donde más fácil se salta
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
                    thick = 0.76f,
                    batterOut = 0.04f,
                    batterIn = 0.04f,
                    capStart = true,
                    capEnd = true,
                    uvShift = o.Rand(0f, 30f),
                };
                VillageGeo.Wall(o.pirca, null, spec);
            }
            Flanks(o, 0f);
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
                bool jumpOnly = def.kind == ObstacleKind.Tronco || def.kind == ObstacleKind.TroncoDoble;
                string verdict;
                if (jumpOnly) verdict = length < 0f ? "cerrado a pie, hay que saltar (bien)" : $"SE PUEDE RODEAR A PIE ({length:F1} m): revisar";
                else if (def.kind == ObstacleKind.MuroCaido) verdict = length < 0f ? "cerrado a pie, hay que saltar (bien)" : $"SE PUEDE RODEAR A PIE ({length:F1} m): revisar";
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
