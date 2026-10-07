using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ayni.Story
{
    /// <summary>
    /// El bosque que vuelve a brotar cuando Yari restaura el Ayni (Biblia, 5.1: ODS 15).
    /// Alrededor del lugar del Juicio crecen, en una ola que sale del centro hacia fuera:
    ///   - brotes y plantones de queñua cerca de los dos personajes,
    ///   - queñuas jóvenes y árboles de tronco retorcido y corteza rojiza al fondo,
    ///   - matas de ichu y flores de cantuta entre ellos,
    ///   - y motas de polen dorado flotando sobre todo el claro.
    /// Todo se genera por código (mallas y texturas), sin assets: no hay nada que importar ni que pueda faltar.
    /// A la vez renacen todos los árboles quemados del camino, estén en pie o caídos (AyniReforestationRevival.cs).
    /// Lo lanza AyniLevelOutcome; el paisaje se queda así si el jugador sigue explorando.
    /// </summary>
    public partial class AyniReforestation : MonoBehaviour
    {
        private enum Kind { Tree, YoungTree, Sapling, Sprout, Grass, Flower, Mound }

        private sealed class Plant
        {
            public Transform root;
            public Transform crown;      // copa (solo en los árboles): brota después que el tronco
            public Kind kind;
            public float delay, duration, size;
            public float swayAmp, swaySpeed, phase;
            public Quaternion baseRotation;
            public bool sprouted, leafed;
            public bool fromWood;        // rebrota de un árbol quemado: no levanta tierra
        }

        private readonly List<Plant> plants = new List<Plant>();
        private readonly List<Vector3> keepClear = new List<Vector3>();
        private Vector3 center;
        private float startTime;
        private ParticleSystem bursts;

        private static readonly Vector3 Wind = new Vector3(0.8f, 0f, 0.6f);

        /// <summary>Empieza a reforestar alrededor de <paramref name="center"/>. Los puntos de <paramref name="clear"/> se dejan libres.</summary>
        public static AyniReforestation Begin(Vector3 center, IList<Vector3> clear, List<GameObject> spawned)
        {
            var go = new GameObject("Desenlace_Reforestacion");
            go.transform.position = center;
            if (spawned != null) spawned.Add(go);

            var forest = go.AddComponent<AyniReforestation>();
            forest.center = center;
            if (clear != null) forest.keepClear.AddRange(clear);
            forest.startTime = Time.unscaledTime;
            forest.StartCoroutine(forest.PlantAll());
            return forest;
        }

        // ───────────────────────── Plantación ─────────────────────────

        private IEnumerator PlantAll()
        {
            Assets.Build();
            bursts = BuildBursts();
            BuildPollen();
            yield return null;

            var rng = new System.Random(15);
            var taken = new List<Vector4>(); // xyz = posición, w = radio que ocupa
            int made = 0;

            // De mayor a menor: los árboles eligen sitio primero y lo demás rellena los huecos.
            // Los árboles altos van lejos del centro para no tapar a los personajes ni cruzarse con la cámara.
            for (int i = 0; i < 9; i++)
            {
                if (TryPlace(rng, 10.5f, 18f, 3.0f, taken, out Vector3 p, out Vector3 n))
                    AddTree(rng, Kind.Tree, Assets.Trees[i % Assets.Trees.Length], p, n, Range(rng, 0.85f, 1.2f), 3.2f);
                if (++made % 6 == 0) yield return null;
            }
            for (int i = 0; i < 11; i++)
            {
                if (TryPlace(rng, 8.5f, 15f, 2.0f, taken, out Vector3 p, out Vector3 n))
                    AddTree(rng, Kind.YoungTree, Assets.YoungTrees[i % Assets.YoungTrees.Length], p, n, Range(rng, 0.85f, 1.2f), 2.6f);
                if (++made % 6 == 0) yield return null;
            }
            for (int i = 0; i < 18; i++)
            {
                if (TryPlace(rng, 2.4f, 9.5f, 1.0f, taken, out Vector3 p, out Vector3 n))
                    AddTree(rng, Kind.Sapling, Assets.Saplings[i % Assets.Saplings.Length], p, n, Range(rng, 0.8f, 1.25f), 2.0f);
                if (++made % 6 == 0) yield return null;
            }
            for (int i = 0; i < 26; i++)
            {
                if (TryPlace(rng, 1.6f, 12f, 0.45f, taken, out Vector3 p, out _))
                    AddSimple(rng, Kind.Sprout, Assets.Sprouts[i % Assets.Sprouts.Length], Assets.Foliage, Assets.Leaves, p, Vector3.up, Range(rng, 0.8f, 1.4f), 1.4f);
                if (++made % 8 == 0) yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                if (TryPlace(rng, 2f, 14.5f, 0.5f, taken, out Vector3 p, out Vector3 n))
                {
                    int v = i % Assets.Flowers.Length;
                    AddSimple(rng, Kind.Flower, Assets.Flowers[v], Assets.Foliage, Assets.Petals[v % Assets.Petals.Length], p, n, Range(rng, 0.8f, 1.3f), 1.6f);
                }
                if (++made % 8 == 0) yield return null;
            }
            for (int i = 0; i < 170; i++)
            {
                if (TryPlace(rng, 1.1f, 17.5f, 0.22f, taken, out Vector3 p, out Vector3 n))
                    AddSimple(rng, Kind.Grass, Assets.Tufts[i % Assets.Tufts.Length], Assets.Grass, null, p, n, Range(rng, 0.7f, 1.5f), 1.2f);
                if (++made % 14 == 0) yield return null;
            }

            // Y la vida vuelve también a los árboles quemados de todo el camino
            yield return ReviveBurntTrees();
        }

        /// <summary>Busca un sitio libre en el anillo indicado, sobre el terreno y sin demasiada pendiente.</summary>
        private bool TryPlace(System.Random rng, float minRadius, float maxRadius, float footprint, List<Vector4> taken,
            out Vector3 position, out Vector3 normal)
        {
            position = Vector3.zero;
            normal = Vector3.up;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                // Raíz cuadrada: reparte por superficie y no amontona junto al radio interior
                float dist = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, (float)rng.NextDouble()));
                Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;

                if (!Physics.Raycast(p + Vector3.up * 25f, Vector3.down, out RaycastHit hit, 80f,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (!(hit.collider is TerrainCollider)) continue;             // ni sobre el puente, ni sobre rocas, ni sobre nadie
                if (Mathf.Abs(hit.point.y - center.y) > 5f) continue;          // ni dentro de la quebrada
                if (Vector3.Angle(hit.normal, Vector3.up) > 32f) continue;

                bool free = true;
                for (int i = 0; i < keepClear.Count && free; i++)
                {
                    Vector3 d = hit.point - keepClear[i];
                    d.y = 0f;
                    if (d.magnitude < 1.3f + footprint) free = false;
                }
                for (int i = 0; i < taken.Count && free; i++)
                {
                    float dx = hit.point.x - taken[i].x, dz = hit.point.z - taken[i].z;
                    float min = footprint + taken[i].w;
                    if (dx * dx + dz * dz < min * min) free = false;
                }
                if (!free) continue;

                taken.Add(new Vector4(hit.point.x, hit.point.y, hit.point.z, footprint));
                position = hit.point;
                normal = hit.normal;
                return true;
            }
            return false;
        }

        private void AddTree(System.Random rng, Kind kind, Assets.TreeModel model, Vector3 position, Vector3 groundNormal, float size, float duration)
        {
            var root = new GameObject(kind == Kind.Sapling ? "Planton_Quenua" : "Quenua");
            root.transform.SetParent(transform, true);
            root.transform.position = position - Vector3.up * 0.03f;
            root.transform.rotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
            root.transform.localScale = Vector3.zero;
            AddRenderer(root, model.bark, Assets.Bark, null, true);

            var crown = new GameObject("Copa");
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = model.crownPivot;
            crown.transform.localScale = Vector3.zero;
            AddRenderer(crown, model.leaves, Assets.Foliage, Assets.Leaves, true);

            Register(rng, root.transform, crown.transform, kind, position, size, duration,
                     kind == Kind.Sapling ? 2.6f : 1.5f, kind == Kind.Sapling ? 1.5f : 1.0f);
            float sproutsAt = plants[plants.Count - 1].delay;

            // La tierra se levanta un instante antes de que asome el tallo, y el ichu la rodea enseguida
            float reach = (kind == Kind.Tree ? 0.62f : kind == Kind.YoungTree ? 0.36f : 0.15f) * size;
            AddSimple(rng, Kind.Mound, Assets.Mounds[rng.Next(Assets.Mounds.Length)], Assets.Soil, null, position, groundNormal, reach, 0.9f);
            plants[plants.Count - 1].delay = Mathf.Max(0f, sproutsAt - 0.25f);

            int tufts = kind == Kind.Tree ? 5 : kind == Kind.YoungTree ? 3 : 1;
            for (int i = 0; i < tufts; i++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                Vector3 p = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach * Range(rng, 0.75f, 1.5f);
                if (!Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 12f,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (!(hit.collider is TerrainCollider)) continue;
                AddSimple(rng, Kind.Grass, Assets.Tufts[rng.Next(Assets.Tufts.Length)], Assets.Grass, null, hit.point, hit.normal,
                          Range(rng, 0.7f, 1.25f), 1.2f);
                plants[plants.Count - 1].delay = sproutsAt + Range(rng, 0.5f, 1.6f);
            }
        }

        private void AddSimple(System.Random rng, Kind kind, Mesh mesh, Material material, Material second, Vector3 position,
            Vector3 groundNormal, float size, float duration)
        {
            var root = new GameObject(kind == Kind.Grass ? "Ichu" : kind == Kind.Flower ? "Cantuta" : kind == Kind.Mound ? "Tierra" : "Brote_Quenua");
            root.transform.SetParent(transform, true);
            root.transform.position = position - Vector3.up * 0.02f;
            // Un poco inclinado con la pendiente, sin llegar a tumbarse
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, groundNormal, kind == Kind.Mound ? 1f : 0.6f));
            root.transform.rotation = tilt * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
            root.transform.localScale = Vector3.zero;
            AddRenderer(root, mesh, material, second, false);

            Register(rng, root.transform, null, kind, position, size, duration,
                     kind == Kind.Mound ? 0f : kind == Kind.Grass ? 5f : 3.5f, 1.7f);
        }

        private void Register(System.Random rng, Transform root, Transform crown, Kind kind, Vector3 position, float size,
            float duration, float swayAmp, float swaySpeed)
        {
            Vector3 flat = position - center;
            flat.y = 0f;
            plants.Add(new Plant
            {
                root = root,
                crown = crown,
                kind = kind,
                size = size,
                duration = duration,
                // La vida avanza desde el lugar del perdón hacia fuera
                delay = 0.5f + flat.magnitude * 0.3f + Range(rng, 0f, 0.9f),
                swayAmp = swayAmp * Range(rng, 0.7f, 1.3f),
                swaySpeed = swaySpeed * Range(rng, 0.85f, 1.15f),
                // La fase depende de la posición a lo largo del viento: las rachas recorren el claro
                phase = Vector3.Dot(position, Wind) * 0.45f + Range(rng, 0f, 0.6f),
                baseRotation = crown != null ? Quaternion.identity : root.rotation
            });
        }

        private static void AddRenderer(GameObject go, Mesh mesh, Material material, Material second, bool shadows)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = second != null ? new[] { material, second } : new[] { material };
            renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        // ───────────────────────── Crecimiento y viento ─────────────────────────

        private void Update()
        {
            float t = Time.unscaledTime - startTime;
            UpdateWood(t);
            Camera view = Camera.main;
            Vector3 eye = view != null ? view.transform.position : center;
            for (int i = 0; i < plants.Count; i++)
            {
                Plant p = plants[i];
                if (p.root == null) continue;

                float k = Mathf.Clamp01((t - p.delay) / p.duration);
                if (k <= 0f) continue;

                if (!p.sprouted)
                {
                    p.sprouted = true;
                    if (p.kind != Kind.Grass && p.kind != Kind.Mound && !p.fromWood) EmitSoil(p.root.position, p.kind);
                }

                bool growing = k < 1f || p.root.localScale.y < p.size * 0.999f;
                // Lo que ya ha crecido y queda lejos no se mece: nadie lo ve y son cientos de plantas por todo el camino
                if (!growing && (p.root.position - eye).sqrMagnitude > SwayDistance * SwayDistance) continue;

                if (growing)
                {
                    if (p.crown != null)
                    {
                        // El tallo sube primero, fino, y va engordando; la copa abre cuando ya hay ramas
                        float up = 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.75f), 3f);
                        float thick = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(k / 0.9f)) * up;
                        p.root.localScale = new Vector3(p.size * thick, p.size * up, p.size * thick);

                        float kc = Mathf.Clamp01((k - 0.38f) / 0.62f);
                        if (kc > 0f && !p.leafed)
                        {
                            p.leafed = true;
                            EmitLeaves(p.crown.position, p.kind);
                        }
                        float safe = Mathf.Max(0.001f, thick);
                        float open = Pop(kc);
                        // Se compensa el grosor del tronco para que la copa no nazca aplastada
                        p.crown.localScale = new Vector3(open * up / safe, open, open * up / safe);
                    }
                    else
                    {
                        p.root.localScale = Vector3.one * (p.size * Pop(k));
                    }
                }

                // Viento: solo mueve lo que ya ha brotado
                float gust = Mathf.Sin(t * p.swaySpeed + p.phase) + 0.35f * Mathf.Sin(t * p.swaySpeed * 2.7f + p.phase * 1.3f);
                float gust2 = Mathf.Cos(t * p.swaySpeed * 0.83f + p.phase);
                Quaternion sway = Quaternion.Euler(gust * p.swayAmp * Wind.z, 0f, -gust2 * p.swayAmp * Wind.x);
                if (p.crown != null) p.crown.localRotation = sway;
                else p.root.rotation = p.baseRotation * sway;
            }
        }

        /// <summary>Crece pasándose un poco de tamaño y asentándose, como algo vivo que se despereza.</summary>
        private static float Pop(float k)
        {
            if (k <= 0f) return 0f;
            if (k >= 1f) return 1f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        // ───────────────────────── Partículas ─────────────────────────

        private ParticleSystem BuildBursts()
        {
            var go = new GameObject("Brotes_Particulas");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.55f;
            main.maxParticles = 4000;
            var emission = ps.emission;
            emission.enabled = false;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Assets.Dot;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private void EmitSoil(Vector3 at, Kind kind)
        {
            if (bursts == null) return;
            int count = kind == Kind.Tree ? 12 : kind == Kind.YoungTree ? 9 : kind == Kind.Sapling ? 6 : 3;
            float power = kind == Kind.Tree ? 1.6f : kind == Kind.YoungTree ? 1.3f : 0.9f;
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle;
                var p = new ParticleSystem.EmitParams
                {
                    position = at + new Vector3(ring.x, 0f, ring.y) * 0.12f + Vector3.up * 0.03f,
                    velocity = new Vector3(ring.x * 0.9f, Random.Range(1.1f, 2.2f), ring.y * 0.9f) * power,
                    startLifetime = Random.Range(0.45f, 0.8f),
                    startSize = Random.Range(0.03f, 0.07f),
                    startColor = Color.Lerp(new Color(0.33f, 0.22f, 0.13f, 0.95f), new Color(0.5f, 0.38f, 0.22f, 0.95f), Random.value)
                };
                bursts.Emit(p, 1);
            }
        }

        private void EmitLeaves(Vector3 at, Kind kind)
        {
            if (bursts == null) return;
            int count = kind == Kind.Tree ? 18 : kind == Kind.YoungTree ? 12 : 6;
            float spread = kind == Kind.Tree ? 0.9f : kind == Kind.YoungTree ? 0.6f : 0.25f;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                var p = new ParticleSystem.EmitParams
                {
                    position = at + dir * spread * 0.4f,
                    velocity = dir * Random.Range(0.8f, 1.9f) * (0.6f + spread),
                    startLifetime = Random.Range(0.8f, 1.5f),
                    startSize = Random.Range(0.035f, 0.08f),
                    startColor = Color.Lerp(new Color(0.3f, 0.55f, 0.2f, 0.95f), new Color(0.72f, 0.82f, 0.32f, 0.95f), Random.value)
                };
                bursts.Emit(p, 1);
            }
        }

        /// <summary>Motas de polen que suben despacio por todo el claro: se nota que el aire vuelve a estar vivo.</summary>
        private void BuildPollen()
        {
            var go = new GameObject("Polen");
            go.transform.SetParent(transform, false);
            go.transform.position = center + Vector3.up * 0.4f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startDelay = 1.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f, 0.85f), new Color(0.75f, 1f, 0.6f, 0.85f));
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 34f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.6f, 30f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.02f, 0.22f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.35f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Assets.Dot;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        public const string TemplateFolder = "AyniVegetacion";

        public static string TemplateName(bool doubleSided, bool cutout)
        {
            return cutout ? "HojasRecortadas" : doubleSided ? "MateDosCaras" : "Mate";
        }

        /// <summary>
        /// Deja un material URP/Lit mate (las plantas no brillan como plástico). <paramref name="doubleSided"/> para
        /// láminas sin grosor (hojas, hierba) y <paramref name="cutout"/> para recortar la textura por su transparencia.
        /// </summary>
        public static void ConfigureMaterial(Material material, bool doubleSided, bool cutout)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            if (material.HasProperty("_EnvironmentReflections"))
            {
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (doubleSided && material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (cutout)
            {
                if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.38f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            material.enableInstancing = true;
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>
        /// Escalón suave: 0 antes de <paramref name="from"/>, 1 después de <paramref name="to"/>.
        /// (Mathf.SmoothStep no sirve para esto: interpola entre dos valores, no entre dos bordes.)
        /// </summary>
        private static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        // ───────────────────────── Mallas, texturas y materiales ─────────────────────────

        /// <summary>Modelos compartidos: se generan una sola vez y todas las plantas los reutilizan.</summary>
        private static class Assets
        {
            public sealed class TreeModel
            {
                public Mesh bark, leaves;
                public Vector3 crownPivot;
            }

            public static TreeModel[] Trees, YoungTrees, Saplings;
            public static Mesh[] Sprouts, Tufts, Flowers, Mounds;
            public static Material Bark, Foliage, Leaves, Grass, Soil, Dot;
            public static Material[] Petals;

            public static void Build()
            {
                if (Bark != null && Trees != null && Trees.Length > 0 && Trees[0].bark != null) return;

                Bark = Lit(BarkTexture(), false, false);
                Foliage = Lit(FoliageTexture(), true, false);
                Leaves = Lit(LeafAtlas(), true, true);
                Grass = Lit(GrassTexture(), true, false);
                Soil = Lit(SoilTexture(), false, false);
                Petals = new[]
                {
                    // Cantuta: la flor sagrada de los incas, roja con la boca amarilla; también las hay rosadas y amarillas
                    Lit(PetalTexture(new Color(0.78f, 0.06f, 0.14f), new Color(1f, 0.74f, 0.18f)), true, false),
                    Lit(PetalTexture(new Color(0.9f, 0.3f, 0.5f), new Color(1f, 0.86f, 0.6f)), true, false),
                    Lit(PetalTexture(new Color(0.95f, 0.62f, 0.1f), new Color(1f, 0.9f, 0.45f)), true, false)
                };
                Dot = DotMaterial();

                // Queñua (Polylepis): baja y ancha, de tronco grueso y retorcido que suele dividirse desde abajo,
                // con la copa irregular, en capas
                Trees = new TreeModel[3];
                for (int i = 0; i < Trees.Length; i++)
                {
                    Trees[i] = TreeBuilder.Build(101 + i * 17, new TreeBuilder.Spec
                    {
                        height = 3.0f + i * 0.3f, stems = i == 1 ? 2 : 1, depth = 2, radius = 0.2f, bend = 0.62f,
                        clump = 0.5f, cards = 15
                    });
                }
                YoungTrees = new TreeModel[3];
                for (int i = 0; i < YoungTrees.Length; i++)
                {
                    YoungTrees[i] = TreeBuilder.Build(211 + i * 13, new TreeBuilder.Spec
                    {
                        height = 1.7f + i * 0.25f, stems = i == 2 ? 2 : 1, depth = 2, radius = 0.1f, bend = 0.55f,
                        clump = 0.32f, cards = 13
                    });
                }
                Saplings = new TreeModel[3];
                for (int i = 0; i < Saplings.Length; i++)
                {
                    Saplings[i] = TreeBuilder.Build(307 + i * 11, new TreeBuilder.Spec
                    {
                        height = 0.75f + i * 0.16f, stems = 1, depth = 1, radius = 0.035f, bend = 0.4f,
                        clump = 0.17f, cards = 10
                    });
                }

                Sprouts = new Mesh[3];
                for (int i = 0; i < Sprouts.Length; i++) Sprouts[i] = SmallPlants.Sprout(401 + i * 7);
                Tufts = new Mesh[4];
                for (int i = 0; i < Tufts.Length; i++) Tufts[i] = SmallPlants.Tuft(503 + i * 5);
                Flowers = new Mesh[3];
                for (int i = 0; i < Flowers.Length; i++) Flowers[i] = SmallPlants.Cantuta(601 + i * 9);
                Mounds = new Mesh[3];
                for (int i = 0; i < Mounds.Length; i++) Mounds[i] = SmallPlants.Mound(701 + i * 3);
            }

            /// <summary>
            /// Material mate con la textura dada. Parte de las plantillas de Assets/Resources/AyniVegetacion (las crea
            /// AyniVegetationMaterials en el editor): al existir como assets, Unity incluye en la compilación del juego las
            /// variantes del shader que usan (recorte por transparencia, sin brillos). Si faltan, se configura al vuelo.
            /// </summary>
            private static Material Lit(Texture2D texture, bool doubleSided, bool cutout)
            {
                Material template = Resources.Load<Material>(TemplateFolder + "/" + TemplateName(doubleSided, cutout));
                Material material;
                if (template != null)
                {
                    material = new Material(template);
                }
                else
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    material = new Material(shader);
                    ConfigureMaterial(material, doubleSided, cutout);
                }
                material.mainTexture = texture;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                return material;
            }

            /// <summary>
            /// Cuatro ramitas de queñua pintadas a mano por código (hojuelas pequeñas y redondeadas, en abanico), cada una
            /// con un tono: hoja vieja, madura, olivácea y brote nuevo. Fuera de las hojas la textura es transparente.
            /// </summary>
            private static Texture2D LeafAtlas()
            {
                const int cell = 128, size = cell * 2;
                var pixels = new Color32[size * size];
                // El color de fondo es el de las hojas: así el borde no se oscurece al reducir la textura
                var background = new Color32(46, 76, 32, 0);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

                Color[] dark = { new Color(0.11f, 0.24f, 0.12f), new Color(0.16f, 0.31f, 0.13f), new Color(0.24f, 0.34f, 0.13f), new Color(0.36f, 0.48f, 0.15f) };
                Color[] light = { new Color(0.2f, 0.4f, 0.17f), new Color(0.28f, 0.49f, 0.18f), new Color(0.4f, 0.5f, 0.19f), new Color(0.6f, 0.7f, 0.25f) };

                var rng = new System.Random(77);
                for (int c = 0; c < 4; c++)
                {
                    int ox = (c % 2) * cell, oy = (c / 2) * cell;
                    Vector2 middle = new Vector2(ox + cell * 0.5f, oy + cell * 0.5f);
                    int leaflets = 46 + rng.Next(8);
                    for (int i = 0; i < leaflets; i++)
                    {
                        // Repartidas por un disco, apuntando hacia fuera: una ramita tupida de hoja menuda
                        float angle = Range(rng, 0f, Mathf.PI * 2f);
                        float half = cell * Range(rng, 0.06f, 0.092f);
                        float width = half * Range(rng, 0.46f, 0.6f);
                        float away = cell * 0.36f * Mathf.Sqrt((float)rng.NextDouble());
                        float skew = angle + Range(rng, -0.7f, 0.7f);
                        Vector2 along = new Vector2(Mathf.Cos(skew), Mathf.Sin(skew));
                        Vector2 across = new Vector2(-along.y, along.x);
                        Vector2 at = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * away;
                        Color tone = Color.Lerp(dark[c], light[c], (float)rng.NextDouble());

                        int reach = Mathf.CeilToInt(half) + 1;
                        for (int y = Mathf.Max(oy + 2, (int)at.y - reach); y <= Mathf.Min(oy + cell - 3, (int)at.y + reach); y++)
                        {
                            for (int x = Mathf.Max(ox + 2, (int)at.x - reach); x <= Mathf.Min(ox + cell - 3, (int)at.x + reach); x++)
                            {
                                Vector2 d = new Vector2(x + 0.5f - at.x, y + 0.5f - at.y);
                                float lx = Vector2.Dot(d, along) / half;   // -1 en la base, 1 en la punta
                                float ly = Vector2.Dot(d, across);
                                if (lx < -1f || lx > 1f) continue;
                                // Más ancha hacia la punta, como la hojuela de la queñua
                                float limit = width * Mathf.Sqrt(1f - lx * lx) * (0.82f + 0.22f * lx);
                                float edge = Mathf.Abs(ly) / Mathf.Max(0.001f, limit);
                                if (edge > 1f) continue;

                                float shade = 0.78f + 0.26f * (lx * 0.5f + 0.5f);       // más clara hacia la punta
                                shade *= 1f - 0.22f * edge * edge;                        // borde algo más oscuro
                                if (Mathf.Abs(ly) < 0.6f) shade *= 1.18f;                 // nervio central
                                Color col = tone * shade;
                                pixels[y * size + x] = new Color32((byte)Mathf.Clamp(col.r * 255f, 0f, 255f),
                                    (byte)Mathf.Clamp(col.g * 255f, 0f, 255f), (byte)Mathf.Clamp(col.b * 255f, 0f, 255f), 255);
                            }
                        }
                    }
                }

                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, mipMapBias = -0.8f };
                tex.SetPixels32(pixels);
                tex.Apply(true);
                return tex;
            }

            private static Material DotMaterial()
            {
                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }
                tex.Apply();
                return new Material(Ayni.Combat.CombatFeedback.SpriteMaterial) { mainTexture = tex };
            }

            /// <summary>Corteza de queñua: rojiza, en láminas de papel que se pelan, con grietas oscuras entre ellas.</summary>
            private static Texture2D BarkTexture()
            {
                const int w = 64, h = 128;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
                Color deep = new Color(0.17f, 0.085f, 0.055f);
                Color mid = new Color(0.31f, 0.15f, 0.085f);
                Color flake = new Color(0.5f, 0.29f, 0.16f);
                Color crack = new Color(0.07f, 0.04f, 0.03f);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // Ruido que se repite en horizontal para que no se note la costura del tronco
                        float u = x / (float)w;
                        float ax = Mathf.Cos(u * Mathf.PI * 2f) * 1.6f, az = Mathf.Sin(u * Mathf.PI * 2f) * 1.6f;
                        float v = y / (float)h;
                        float sheets = Mathf.PerlinNoise(ax * 1.3f + 7.1f, az * 1.3f + v * 5.5f);        // láminas anchas
                        float fibers = Mathf.PerlinNoise(ax * 5f + 3.3f + v * 1.2f, az * 5f + v * 26f);   // fibras verticales finas
                        float edges = Mathf.PerlinNoise(ax * 2.6f + 11f, az * 2.6f + v * 12f);

                        Color c = Color.Lerp(deep, mid, Smooth(0.25f, 0.7f, sheets));
                        c = Color.Lerp(c, flake, Smooth(0.62f, 0.85f, sheets) * 0.85f);
                        c *= 0.82f + fibers * 0.36f;
                        c = Color.Lerp(c, crack, Smooth(0.44f, 0.36f, edges) * 0.75f);
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                }
                tex.Apply(true);
                return tex;
            }

            /// <summary>
            /// Paleta de las hojas. En horizontal, del verde oscuro de la hoja vieja al verde amarillo del brote nuevo;
            /// en vertical, de la sombra de dentro de la copa a la luz de arriba.
            /// </summary>
            private static Texture2D FoliageTexture()
            {
                const int w = 64, h = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                Color old = new Color(0.07f, 0.16f, 0.08f);
                Color mid = new Color(0.14f, 0.27f, 0.11f);
                Color young = new Color(0.4f, 0.52f, 0.17f);
                for (int y = 0; y < h; y++)
                {
                    float v = y / (float)(h - 1);
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (float)(w - 1);
                        Color c = u < 0.55f ? Color.Lerp(old, mid, u / 0.55f) : Color.Lerp(mid, young, (u - 0.55f) / 0.45f);
                        c *= Mathf.Lerp(0.55f, 1.2f, v);
                        c = Color.Lerp(c, new Color(0.5f, 0.56f, 0.24f), Smooth(0.8f, 1f, v) * 0.25f);
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                }
                tex.Apply();
                return tex;
            }

            /// <summary>Ichu: verde en la base y pajizo en la punta.</summary>
            private static Texture2D GrassTexture()
            {
                const int w = 8, h = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                {
                    float v = y / (float)(h - 1);
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (float)(w - 1);
                        Color root = Color.Lerp(new Color(0.1f, 0.17f, 0.06f), new Color(0.17f, 0.19f, 0.07f), u);
                        Color body = Color.Lerp(new Color(0.24f, 0.36f, 0.11f), new Color(0.44f, 0.42f, 0.15f), u);
                        Color tip = Color.Lerp(new Color(0.55f, 0.56f, 0.22f), new Color(0.78f, 0.66f, 0.3f), u);
                        Color c = v < 0.45f ? Color.Lerp(root, body, v / 0.45f) : Color.Lerp(body, tip, (v - 0.45f) / 0.55f);
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                }
                tex.Apply();
                return tex;
            }

            /// <summary>Tierra removida en el centro y musgo hacia el borde: el pie de cada árbol.</summary>
            private static Texture2D SoilTexture()
            {
                const int w = 32, h = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                Color earth = new Color(0.2f, 0.13f, 0.08f);
                Color damp = new Color(0.12f, 0.08f, 0.05f);
                Color moss = new Color(0.2f, 0.3f, 0.1f);
                Color grass = new Color(0.3f, 0.38f, 0.12f);
                for (int y = 0; y < h; y++)
                {
                    float v = y / (float)(h - 1);
                    for (int x = 0; x < w; x++)
                    {
                        float n = Mathf.PerlinNoise(x * 0.45f + 3f, y * 0.45f + 9f);
                        Color c = Color.Lerp(damp, earth, n);
                        c = Color.Lerp(c, moss, Smooth(0.3f, 0.62f, v + (n - 0.5f) * 0.3f));
                        c = Color.Lerp(c, grass, Smooth(0.72f, 1f, v + (n - 0.5f) * 0.2f) * 0.8f);
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                }
                tex.Apply();
                return tex;
            }

            private static Texture2D PetalTexture(Color tube, Color mouth)
            {
                const int w = 4, h = 16;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                {
                    float v = y / (float)(h - 1);
                    Color c = Color.Lerp(tube * 0.75f, tube, Smooth(0f, 0.4f, v));
                    c = Color.Lerp(c, mouth, Smooth(0.86f, 0.98f, v));
                    c.a = 1f;
                    for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
                }
                tex.Apply();
                return tex;
            }
        }

        /// <summary>Acumula vértices para construir una malla (con una segunda lista de triángulos para un segundo material).</summary>
        private sealed class MeshBuffer
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            public readonly List<int> second = new List<int>();

            public int Add(Vector3 position, Vector3 normal, Vector2 uv)
            {
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
                return vertices.Count - 1;
            }

            public Mesh ToMesh(string name, Vector3 pivot)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                if (pivot != Vector3.zero)
                {
                    for (int i = 0; i < vertices.Count; i++) vertices[i] -= pivot;
                }
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                if (second.Count > 0)
                {
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(triangles, 0);
                    mesh.SetTriangles(second, 1);
                }
                else
                {
                    mesh.SetTriangles(triangles, 0);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static class Shapes
        {
            /// <summary>Tubo que sigue una línea de puntos, con un radio en cada uno (troncos, ramas y tallos).</summary>
            public static void Tube(MeshBuffer buffer, List<Vector3> points, List<float> radii, int sides, float uRepeat, float vPerMeter,
                Vector2 uvOffset, bool flatV)
            {
                if (points.Count < 2) return;
                int first = buffer.vertices.Count;
                int ring = sides + 1;
                Vector3 normal = Vector3.zero;
                float along = 0f;

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 tangent = i == 0 ? points[1] - points[0]
                                    : i == points.Count - 1 ? points[i] - points[i - 1]
                                    : points[i + 1] - points[i - 1];
                    tangent.Normalize();

                    // El marco se arrastra de un anillo al siguiente para que el tubo no se retuerza sobre sí mismo
                    normal = i == 0 ? Vector3.Cross(tangent, Mathf.Abs(tangent.x) > 0.9f ? Vector3.forward : Vector3.right)
                                    : Vector3.ProjectOnPlane(normal, tangent);
                    if (normal.sqrMagnitude < 0.0001f) normal = Vector3.Cross(tangent, Vector3.forward);
                    normal.Normalize();
                    Vector3 binormal = Vector3.Cross(tangent, normal);

                    if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                    float v = flatV ? i / (float)(points.Count - 1) : along * vPerMeter;

                    for (int j = 0; j <= sides; j++)
                    {
                        float angle = j / (float)sides * Mathf.PI * 2f;
                        Vector3 dir = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                        buffer.Add(points[i] + dir * radii[i], dir, uvOffset + new Vector2(j / (float)sides * uRepeat, v));
                    }
                }

                for (int i = 0; i < points.Count - 1; i++)
                {
                    for (int j = 0; j < sides; j++)
                    {
                        int a = first + i * ring + j, b = a + 1, c = a + ring, d = c + 1;
                        buffer.triangles.Add(a); buffer.triangles.Add(b); buffer.triangles.Add(c);
                        buffer.triangles.Add(b); buffer.triangles.Add(d); buffer.triangles.Add(c);
                    }
                }
            }

            private static Vector3[] sphereVertices;
            private static int[] sphereTriangles;

            /// <summary>Esfera de 80 caras (icosaedro subdividido una vez): la base de cada mata de hojas.</summary>
            private static void EnsureSphere()
            {
                if (sphereVertices != null) return;
                float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
                var verts = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
                };
                for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
                int[] faces =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
                };

                var tris = new List<int>();
                var midpoints = new Dictionary<long, int>();
                System.Func<int, int, int> mid = (a, b) =>
                {
                    long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (midpoints.TryGetValue(key, out int index)) return index;
                    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                    midpoints[key] = verts.Count - 1;
                    return verts.Count - 1;
                };
                for (int i = 0; i < faces.Length; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = mid(a, b), bc = mid(b, c), ca = mid(c, a);
                    tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                sphereVertices = verts.ToArray();
                sphereTriangles = tris.ToArray();
            }

            /// <summary>
            /// Mata de hojas: una masa interior oscura (da volumen y tapa los huecos) rodeada de ramitas de hojas
            /// recortadas, que son lo que se ve y lo que dibuja la silueta.
            /// </summary>
            public static void LeafClump(MeshBuffer buffer, System.Random rng, Vector3 center, float radius, int cards, bool newGrowth = false)
            {
                EnsureSphere();
                float tint = (float)rng.NextDouble();
                tint *= tint; // casi todo hoja madura; alguna mata de brote nuevo, más claro
                if (newGrowth) tint = Range(rng, 0.7f, 1f);
                float seedA = (float)rng.NextDouble() * 50f, seedB = (float)rng.NextDouble() * 50f;
                Quaternion turn = Quaternion.Euler(Range(rng, -20f, 20f), Range(rng, 0f, 360f), Range(rng, -20f, 20f));
                // Achatada: la copa de la queñua va en capas
                Vector3 squash = new Vector3(Range(rng, 0.95f, 1.25f), Range(rng, 0.5f, 0.68f), Range(rng, 0.95f, 1.25f));

                int first = buffer.vertices.Count;
                // (un brote recién nacido es solo un puñado de hojas: no lleva masa interior)
                for (int i = 0; i < sphereVertices.Length && !newGrowth; i++)
                {
                    Vector3 u = sphereVertices[i];
                    float lump = Mathf.PerlinNoise(u.x * 1.9f + seedA, u.y * 1.9f + seedB) +
                                 Mathf.PerlinNoise(u.z * 2.3f + seedB, u.x * 2.3f + seedA) * 0.6f;
                    Vector3 local = turn * Vector3.Scale(u * (0.5f + lump * 0.22f), squash);
                    Vector3 normal = (local.normalized + Vector3.up * 0.35f).normalized;
                    float light = Mathf.InverseLerp(-0.6f, 0.6f, local.y);
                    buffer.Add(center + local * radius, normal, new Vector2(tint * 0.5f, Mathf.Lerp(0.02f, 0.4f, light)));
                }
                for (int i = 0; i < sphereTriangles.Length && !newGrowth; i++) buffer.triangles.Add(first + sphereTriangles[i]);

                int cell = tint < 0.3f ? 0 : tint < 0.6f ? 1 : tint < 0.85f ? 2 : 3;
                for (int i = 0; i < cards; i++)
                {
                    Vector3 dir = RandomDirection(rng);
                    dir.y = dir.y * 0.75f + 0.15f;
                    dir.Normalize();
                    Vector3 outward = (turn * Vector3.Scale(dir, squash)).normalized;
                    Vector3 at = center + turn * Vector3.Scale(dir, squash) * radius * Range(rng, 0.55f, 0.9f);
                    // Unas miran hacia fuera y otras quedan casi de canto: desde cualquier lado se ven hojas de frente
                    Vector3 facing = (outward * Range(rng, 0.2f, 1f) + RandomDirection(rng) * 0.9f).normalized;
                    // De vez en cuando una ramita de otro tono, para que la copa no sea de un solo color
                    int tone = rng.NextDouble() < 0.25 ? Mathf.Clamp(cell + (rng.NextDouble() < 0.5 ? -1 : 1), 0, 3) : cell;
                    LeafCard(buffer, rng, at, facing, outward, radius * Range(rng, 0.62f, 0.95f), tone);
                }
            }

            /// <summary>Lámina cuadrada con una ramita de hojas del atlas. Va al segundo material (recortado).</summary>
            public static void LeafCard(MeshBuffer buffer, System.Random rng, Vector3 at, Vector3 facing, Vector3 shade, float half, int cell)
            {
                Vector3 side = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                side = Quaternion.AngleAxis(Range(rng, 0f, 360f), facing) * side;
                Vector3 up = Vector3.Cross(facing, side).normalized;
                Vector3 normal = (shade + Vector3.up * 0.45f).normalized;

                const float inset = 0.012f;
                float u0 = (cell % 2) * 0.5f + inset, v0 = (cell / 2) * 0.5f + inset;
                float u1 = u0 + 0.5f - inset * 2f, v1 = v0 + 0.5f - inset * 2f;

                int a = buffer.Add(at - side * half - up * half, normal, new Vector2(u0, v0));
                int b = buffer.Add(at + side * half - up * half, normal, new Vector2(u1, v0));
                int c = buffer.Add(at + side * half + up * half, normal, new Vector2(u1, v1));
                int d = buffer.Add(at - side * half + up * half, normal, new Vector2(u0, v1));
                buffer.second.Add(a); buffer.second.Add(b); buffer.second.Add(c);
                buffer.second.Add(a); buffer.second.Add(c); buffer.second.Add(d);
            }

            /// <summary>Una hoja: un rombo alargado. <paramref name="shade"/> es la dirección con la que se ilumina.</summary>
            public static void Leaf(MeshBuffer buffer, Vector3 at, Vector3 facing, Vector3 shade, float size, Vector2 uv)
            {
                Vector3 side = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 length = Vector3.Cross(side, facing).normalized;
                Vector3 normal = (shade + Vector3.up * 0.4f).normalized;
                int a = buffer.Add(at - length * size, normal, uv);
                int b = buffer.Add(at + side * size * 0.48f, normal, uv);
                int c = buffer.Add(at + length * size, normal, new Vector2(uv.x, Mathf.Min(1f, uv.y + 0.08f)));
                int d = buffer.Add(at - side * size * 0.48f, normal, uv);
                buffer.triangles.Add(a); buffer.triangles.Add(b); buffer.triangles.Add(c);
                buffer.triangles.Add(a); buffer.triangles.Add(c); buffer.triangles.Add(d);
            }

            public static Vector3 RandomDirection(System.Random rng)
            {
                float z = Range(rng, -1f, 1f);
                float a = Range(rng, 0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
            }
        }

        /// <summary>Construye una queñua: tronco y ramas retorcidos, y una mata de hojas en cada rama.</summary>
        private static class TreeBuilder
        {
            public struct Spec
            {
                public float height;   // altura total aproximada (m)
                public int stems;      // troncos que salen del suelo
                public int depth;      // niveles de ramas
                public float radius;   // radio del tronco en la base
                public float bend;     // cuánto se retuerce
                public float clump;    // tamaño de las matas de hojas
                public int cards;      // ramitas de hojas por mata
            }

            private sealed class Work
            {
                public Spec spec;
                public System.Random rng;
                public MeshBuffer bark = new MeshBuffer();
                public MeshBuffer leaves = new MeshBuffer();
                public List<Vector3> clumps = new List<Vector3>();
            }

            public static Assets.TreeModel Build(int seed, Spec spec)
            {
                var work = new Work { spec = spec, rng = new System.Random(seed) };

                for (int s = 0; s < spec.stems; s++)
                {
                    float turn = (s / (float)spec.stems) * Mathf.PI * 2f + Range(work.rng, 0f, 1.5f);
                    float lean = spec.stems > 1 ? 0.55f : Range(work.rng, 0.12f, 0.36f);
                    Vector3 dir = new Vector3(Mathf.Cos(turn) * lean, 1f, Mathf.Sin(turn) * lean).normalized;
                    Vector3 start = spec.stems > 1 ? new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * spec.radius * 0.7f : Vector3.zero;
                    float length = spec.height * (spec.stems > 1 ? Range(work.rng, 0.44f, 0.54f) : Range(work.rng, 0.46f, 0.56f));
                    Branch(work, start, dir, length, spec.radius * (spec.stems > 1 ? 0.8f : 1f), 0);
                }

                // La copa crece desde donde se abren las ramas
                Vector3 pivot = Vector3.zero;
                if (work.clumps.Count > 0)
                {
                    float low = float.MaxValue;
                    foreach (Vector3 c in work.clumps)
                    {
                        pivot += c;
                        low = Mathf.Min(low, c.y);
                    }
                    pivot /= work.clumps.Count;
                    pivot.y = Mathf.Lerp(low, pivot.y, 0.35f);
                }

                return new Assets.TreeModel
                {
                    bark = work.bark.ToMesh("Quenua_Tronco", Vector3.zero),
                    leaves = work.leaves.ToMesh("Quenua_Copa", pivot),
                    crownPivot = pivot
                };
            }

            private static void Branch(Work work, Vector3 start, Vector3 direction, float length, float radius, int depth)
            {
                Spec spec = work.spec;
                System.Random rng = work.rng;
                int segments = depth == 0 ? 6 : depth == 1 ? 4 : 3;
                bool last = depth >= spec.depth;

                var points = new List<Vector3> { start };
                var radii = new List<float> { depth == 0 ? radius * 1.3f : radius };
                Vector3 position = start;
                Vector3 dir = direction;

                for (int s = 1; s <= segments; s++)
                {
                    float f = s / (float)segments;
                    // Cada tramo se desvía del anterior: de ahí sale el tronco retorcido de la queñua
                    dir = (dir + Shapes.RandomDirection(rng) * spec.bend * (depth == 0 ? 0.62f : 0.8f) +
                           Vector3.up * (depth == 0 ? 0.3f : 0.07f)).normalized;
                    position += dir * (length / segments);
                    float r = radius * Mathf.Lerp(1f, last ? 0.22f : 0.58f, f);
                    points.Add(position);
                    radii.Add(r);

                    if (!last && s >= 2)
                    {
                        int children = s == segments ? 2 : (rng.NextDouble() < (depth == 0 ? 0.8 : 0.55) ? 1 : 0);
                        for (int c = 0; c < children; c++)
                        {
                            Vector3 side = Vector3.Cross(dir, Shapes.RandomDirection(rng)).normalized;
                            float angle = Range(rng, 42f, 80f) * Mathf.Deg2Rad;
                            Vector3 childDir = (dir * Mathf.Cos(angle) + side * Mathf.Sin(angle)).normalized;
                            Branch(work, position, childDir, length * Range(rng, 0.46f, 0.7f), r * 0.74f, depth + 1);
                        }
                    }

                    if (depth >= 1 && f > 0.45f)
                    {
                        float size = spec.clump * Range(rng, 0.62f, 1f) * (last ? 1f : 0.85f);
                        Clump(work, position + Shapes.RandomDirection(rng) * size * 0.35f + Vector3.up * size * 0.2f, size);
                    }
                }

                // Remate de la rama
                float tip = spec.clump * Range(rng, 0.8f, 1.15f) * (depth == 0 ? 1.15f : 1f);
                if (depth >= 1 || spec.depth == 0) Clump(work, position + dir * tip * 0.35f, tip);

                Shapes.Tube(work.bark, points, radii, depth == 0 ? 7 : 5, depth == 0 ? 2f : 1f, 1.4f, Vector2.zero, false);
            }

            private static void Clump(Work work, Vector3 center, float size)
            {
                work.clumps.Add(center);
                Shapes.LeafClump(work.leaves, work.rng, center, size, work.spec.cards);
            }
        }

        /// <summary>Lo pequeño: brotes recién nacidos, matas de ichu y cantutas.</summary>
        private static class SmallPlants
        {
            /// <summary>Brote: un tallo verde curvado con pares de hojas y la yema arriba.</summary>
            public static Mesh Sprout(int seed)
            {
                var rng = new System.Random(seed);
                var buffer = new MeshBuffer();
                float height = Range(rng, 0.2f, 0.34f);
                Vector3 dir = (Vector3.up + Shapes.RandomDirection(rng) * 0.25f).normalized;
                var points = new List<Vector3> { Vector3.zero };
                var radii = new List<float> { 0.012f };
                Vector3 position = Vector3.zero;
                const int segments = 4;
                for (int s = 1; s <= segments; s++)
                {
                    dir = (dir + Shapes.RandomDirection(rng) * 0.22f + Vector3.up * 0.15f).normalized;
                    position += dir * (height / segments);
                    points.Add(position);
                    radii.Add(Mathf.Lerp(0.012f, 0.004f, s / (float)segments));

                    if (s >= 2)
                    {
                        // Hojas de dos en dos, una a cada lado del tallo
                        Vector3 side = Vector3.Cross(dir, Shapes.RandomDirection(rng)).normalized;
                        float size = Range(rng, 0.045f, 0.07f) * (s == segments ? 0.75f : 1f);
                        for (int k = -1; k <= 1; k += 2)
                        {
                            Vector3 outward = (side * k + Vector3.up * 0.45f).normalized;
                            Vector3 facing = Vector3.Cross(outward, dir).normalized;
                            Shapes.Leaf(buffer, position + outward * size * 0.9f, facing, Vector3.up, size,
                                        new Vector2(Range(rng, 0.7f, 1f), Range(rng, 0.7f, 0.95f)));
                        }
                    }
                }
                // Tallo verde: usa una franja clara y joven de la paleta de hojas, algo más luminosa hacia la punta
                int before = buffer.vertices.Count;
                Shapes.Tube(buffer, points, radii, 4, 0f, 0f, Vector2.zero, true);
                for (int i = before; i < buffer.vertices.Count; i++)
                {
                    buffer.uvs[i] = new Vector2(0.62f, 0.45f + buffer.uvs[i].y * 0.25f);
                }
                Shapes.LeafClump(buffer, rng, position + dir * 0.02f, 0.07f, 6, true);
                return buffer.ToMesh("Brote_Quenua", Vector3.zero);
            }

            /// <summary>Montículo bajo e irregular: la tierra que levanta el árbol al brotar, ya cubierta de musgo en el borde.</summary>
            public static Mesh Mound(int seed)
            {
                var rng = new System.Random(seed);
                var buffer = new MeshBuffer();
                const int sides = 12;
                float[] ring = { 0.3f, 0.62f, 0.86f, 1f };
                float[] height = { 0.15f, 0.1f, 0.035f, -0.05f };
                var bulge = new float[sides];
                var lumps = new float[sides];
                for (int j = 0; j < sides; j++)
                {
                    bulge[j] = Range(rng, 0.82f, 1.18f);
                    lumps[j] = Range(rng, 0.8f, 1.2f);
                }

                int top = buffer.Add(new Vector3(0f, 0.17f, 0f), Vector3.up, new Vector2(0.5f, 0f));
                for (int i = 0; i < ring.Length; i++)
                {
                    for (int j = 0; j <= sides; j++)
                    {
                        float angle = j / (float)sides * Mathf.PI * 2f;
                        Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        float r = ring[i] * bulge[j % sides];
                        float lift = height[i] * (i < 2 ? lumps[(j + i * 5) % sides] : 1f);
                        buffer.Add(outward * r + Vector3.up * lift, (Vector3.up + outward * (0.25f + ring[i] * 0.5f)).normalized,
                                   new Vector2(j / (float)sides, ring[i]));
                    }
                }
                int line = sides + 1;
                for (int j = 0; j < sides; j++)
                {
                    buffer.triangles.Add(top); buffer.triangles.Add(1 + j + 1); buffer.triangles.Add(1 + j);
                }
                for (int i = 0; i < ring.Length - 1; i++)
                {
                    for (int j = 0; j < sides; j++)
                    {
                        int a = 1 + i * line + j, b = a + 1, c = a + line, d = c + 1;
                        buffer.triangles.Add(a); buffer.triangles.Add(b); buffer.triangles.Add(c);
                        buffer.triangles.Add(b); buffer.triangles.Add(d); buffer.triangles.Add(c);
                    }
                }
                return buffer.ToMesh("Monticulo", Vector3.zero);
            }

            /// <summary>Mata de ichu: briznas finas que salen de un punto y se abren hacia fuera.</summary>
            public static Mesh Tuft(int seed)
            {
                var rng = new System.Random(seed);
                var buffer = new MeshBuffer();
                int blades = 26 + rng.Next(12);
                for (int i = 0; i < blades; i++)
                {
                    float angle = Range(rng, 0f, Mathf.PI * 2f);
                    Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 side = new Vector3(-outward.z, 0f, outward.x);
                    Vector3 foot = outward * Range(rng, 0f, 0.09f);
                    float height = Range(rng, 0.25f, 0.62f);
                    float lean = Range(rng, 0.1f, 0.6f);
                    float width = Range(rng, 0.009f, 0.016f);
                    float tint = Range(rng, 0f, 1f);
                    Vector3 normal = (Vector3.up + outward * 0.45f).normalized;

                    // Tres tramos: recta abajo, vencida hacia fuera arriba
                    Vector3 mid1 = foot + Vector3.up * height * 0.4f + outward * height * lean * 0.12f;
                    Vector3 mid2 = foot + Vector3.up * height * 0.75f + outward * height * lean * 0.45f;
                    Vector3 top = foot + Vector3.up * height * 0.95f + outward * height * lean;

                    int a0 = buffer.Add(foot - side * width, normal, new Vector2(tint, 0f));
                    int b0 = buffer.Add(foot + side * width, normal, new Vector2(tint, 0f));
                    int a1 = buffer.Add(mid1 - side * width * 0.85f, normal, new Vector2(tint, 0.4f));
                    int b1 = buffer.Add(mid1 + side * width * 0.85f, normal, new Vector2(tint, 0.4f));
                    int a2 = buffer.Add(mid2 - side * width * 0.55f, normal, new Vector2(tint, 0.75f));
                    int b2 = buffer.Add(mid2 + side * width * 0.55f, normal, new Vector2(tint, 0.75f));
                    int t = buffer.Add(top, normal, new Vector2(tint, 1f));

                    buffer.triangles.AddRange(new[] { a0, b0, a1, b0, b1, a1, a1, b1, a2, b1, b2, a2, a2, b2, t });
                }
                return buffer.ToMesh("Ichu", Vector3.zero);
            }

            /// <summary>Cantuta: tallo fino con hojitas y un racimo de campanillas colgando.</summary>
            public static Mesh Cantuta(int seed)
            {
                var rng = new System.Random(seed);
                var buffer = new MeshBuffer();
                int stems = 3 + rng.Next(3);
                for (int s = 0; s < stems; s++)
                {
                    float height = Range(rng, 0.45f, 0.8f);
                    float turn = Range(rng, 0f, Mathf.PI * 2f);
                    Vector3 dir = (Vector3.up + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * 0.3f).normalized;
                    var points = new List<Vector3> { Vector3.zero };
                    var radii = new List<float> { 0.008f };
                    Vector3 position = Vector3.zero;
                    const int segments = 5;
                    for (int i = 1; i <= segments; i++)
                    {
                        // Se arquea por el peso de las flores
                        dir = (dir + new Vector3(Mathf.Cos(turn), -0.12f * i, Mathf.Sin(turn)) * 0.14f + Shapes.RandomDirection(rng) * 0.1f).normalized;
                        position += dir * (height / segments);
                        points.Add(position);
                        radii.Add(Mathf.Lerp(0.008f, 0.004f, i / (float)segments));

                        if (i <= 3)
                        {
                            Vector3 side = Vector3.Cross(dir, Shapes.RandomDirection(rng)).normalized;
                            Shapes.Leaf(buffer, position + side * 0.035f, Vector3.Cross(side, dir).normalized, Vector3.up, 0.04f,
                                        new Vector2(Range(rng, 0.3f, 0.7f), Range(rng, 0.55f, 0.85f)));
                        }
                        if (i >= 3)
                        {
                            int bells = i == segments ? 3 : 2;
                            for (int b = 0; b < bells; b++)
                            {
                                Vector3 side = Vector3.Cross(dir, Shapes.RandomDirection(rng)).normalized;
                                Bell(buffer, position, (Vector3.down + side * Range(rng, 0.25f, 0.6f)).normalized, Range(rng, 0.09f, 0.13f));
                            }
                        }
                    }
                    int before = buffer.vertices.Count;
                    Shapes.Tube(buffer, points, radii, 4, 0f, 0f, new Vector2(0.4f, 0.4f), true);
                    for (int i = before; i < buffer.vertices.Count; i++) buffer.uvs[i] = new Vector2(0.4f, 0.4f);
                }
                return buffer.ToMesh("Cantuta", Vector3.zero);
            }

            /// <summary>Campanilla: un tubo estrecho que se abre en la boca. Va al segundo material (los pétalos).</summary>
            private static void Bell(MeshBuffer buffer, Vector3 at, Vector3 dir, float length)
            {
                const int sides = 6;
                Vector3 normal = Vector3.Cross(dir, Mathf.Abs(dir.x) > 0.9f ? Vector3.forward : Vector3.right).normalized;
                Vector3 binormal = Vector3.Cross(dir, normal);
                float[] along = { 0f, 0.3f, 0.86f, 1f };
                float[] radius = { 0.005f, 0.011f, 0.014f, 0.022f };
                int first = buffer.vertices.Count;
                for (int i = 0; i < along.Length; i++)
                {
                    for (int j = 0; j <= sides; j++)
                    {
                        float angle = j / (float)sides * Mathf.PI * 2f;
                        Vector3 outward = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                        buffer.Add(at + dir * length * along[i] + outward * radius[i], (outward + Vector3.up * 0.3f).normalized,
                                   new Vector2(0.5f, along[i]));
                    }
                }
                int ring = sides + 1;
                for (int i = 0; i < along.Length - 1; i++)
                {
                    for (int j = 0; j < sides; j++)
                    {
                        int a = first + i * ring + j, b = a + 1, c = a + ring, d = c + 1;
                        buffer.second.Add(a); buffer.second.Add(b); buffer.second.Add(c);
                        buffer.second.Add(b); buffer.second.Add(d); buffer.second.Add(c);
                    }
                }
            }
        }
    }
}
