using System;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;
using Ayni.UI;
using Ayni.World.Fauna;

namespace Ayni.World
{
    /// <summary>
    /// Ofrendas a la Pachamama escondidas a los lados del Qhapaq Ñan, entre kancha y kancha: un atado de tela con hojas
    /// de coca y una concha de mullu sobre una laja, con una lucecita dorada. Dan un motivo para salirse del camino.
    ///  - Se recogen manteniendo <c>V</c> / <c>LT</c> (sin rivales peleando cerca). Un rombo «Ofrenda» las marca a menos
    ///    de 28 m; de más lejos solo se ve su brillo.
    ///  - Cada una sana a Yari un 30 % de su vida. Cada tres, la Illa le devuelve un año.
    ///  - Las llamas del chasqui (<see cref="AyniLlamaCorral"/>) traen una más entre su carga.
    /// Se colocan solas al empezar, siguiendo el eje del camino; SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniOfferings : MonoBehaviour
    {
        /// <summary>Cuántas se esconden en el camino.</summary>
        public const int OnRoad = 6;
        /// <summary>Cuántas hay en total (las del camino que se pudieron poner y la de las llamas).</summary>
        public static int Total { get; private set; }
        public static int Collected { get; private set; }
        /// <summary>Cambió la cuenta (para el HUD).</summary>
        public static event Action OnChanged;

        public const float HealFraction = 0.3f;
        public const int EveryForYear = 3;

        /// <summary>Eje aproximado del camino, de la salida a la cabeza del puente (el mismo que usa la selva quemada).</summary>
        private static readonly Vector2[] RoadSpine =
        {
            new Vector2(247.9f, 539.5f), new Vector2(283.0f, 539.7f), new Vector2(294.5f, 506.0f), new Vector2(330.9f, 468.3f),
            new Vector2(360.3f, 441.7f), new Vector2(368.7f, 414.5f), new Vector2(392.1f, 387.7f), new Vector2(429.7f, 384.5f),
            new Vector2(450.4f, 386.0f), new Vector2(477.9f, 433.9f), new Vector2(484.6f, 475.6f), new Vector2(497.9f, 507.0f),
            new Vector2(556.1f, 519.1f), new Vector2(612.0f, 468.0f), new Vector2(634.0f, 470.0f),
        };

        [SerializeField] private float interactRange = 2.4f;
        [SerializeField] private float holdSeconds = 0.7f;

        private sealed class Offering
        {
            public int index;
            public Transform root;
            public Light glow;
            public ParticleSystem motes;
            public bool taken;
            public Vector3 Top => root.position + Vector3.up * 1.0f;
        }

        private static AyniOfferings instance;
        private readonly List<Offering> offerings = new List<Offering>();
        private YariCombatController yari;
        private IllaTalismanSystem talisman;
        private Offering focus;
        private float progress;
        private bool firstTaken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Total = 0;
            Collected = 0;
            OnChanged = null;
            instance = null;
        }

        private void OnEnable() => instance = this;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.GetComponent<YariCombatController>();
                talisman = player.GetComponent<IllaTalismanSystem>();
            }
            Place();
            Total = offerings.Count + 1; // + la que traen las llamas del chasqui
            Debug.Log($"[Ayni Ofrendas] {offerings.Count} ofrendas en el camino (+1 de las llamas)");
        }

        // ───────────────────────── Dónde ─────────────────────────

        private void Place()
        {
            // Repartidas a lo largo del camino, lejos de las kanchas, alternando el lado
            float total = 0f;
            for (int i = 0; i < RoadSpine.Length - 1; i++) total += Vector2.Distance(RoadSpine[i], RoadSpine[i + 1]);
            var rng = new System.Random(5309);
            for (int n = 0; n < OnRoad; n++)
            {
                float target = total * (n + 0.5f) / OnRoad;
                bool placed = false;
                // Si cae junto a una kancha, cerca de otra ofrenda o en mal terreno, se prueba un poco antes o después
                for (int attempt = 0; attempt < 24 && !placed; attempt++)
                {
                    float shift = (attempt % 2 == 0 ? 1f : -1f) * ((attempt + 1) / 2) * 7f;
                    if (!SpineAt(target + shift, out Vector2 p, out Vector2 dir)) continue;
                    if (NearSite(p, 34f)) continue;
                    Vector2 side = new Vector2(-dir.y, dir.x) * ((n + attempt) % 2 == 0 ? 1f : -1f);
                    // El empedrado lo pinta el terreno (no tiene colisionador propio): fuera de él, donde empiezan los árboles
                    float off = 14.5f + (float)rng.NextDouble() * 4f;
                    if (Candidate(p, p + side * off, out Vector3 spot) || Candidate(p, p - side * off, out spot))
                    {
                        offerings.Add(Build(offerings.Count, spot, rng));
                        placed = true;
                    }
                }
                if (!placed) Debug.LogWarning($"[Ayni Ofrendas] No encontré sitio para la ofrenda {n + 1}");
            }
        }

        private bool Candidate(Vector2 road, Vector2 at, out Vector3 spot)
        {
            spot = Vector3.zero;
            if (DistanceToSpine(at) < 13.5f || NearSite(at, 30f)) return false;
            foreach (Offering o in offerings)
            {
                if (Vector2.Distance(at, new Vector2(o.root.position.x, o.root.position.z)) < 55f) return false;
            }
            return TryGround(road, at, out spot);
        }

        private static float DistanceToSpine(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < RoadSpine.Length - 1; i++)
            {
                Vector2 a = RoadSpine[i], ab = RoadSpine[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        private static bool SpineAt(float distance, out Vector2 point, out Vector2 dir)
        {
            point = RoadSpine[0];
            dir = Vector2.right;
            if (distance < 0f) return false;
            for (int i = 0; i < RoadSpine.Length - 1; i++)
            {
                float length = Vector2.Distance(RoadSpine[i], RoadSpine[i + 1]);
                if (distance <= length)
                {
                    dir = (RoadSpine[i + 1] - RoadSpine[i]).normalized;
                    point = RoadSpine[i] + dir * distance;
                    return true;
                }
                distance -= length;
            }
            return false;
        }

        private static bool NearSite(Vector2 p, float radius)
        {
            foreach (AyniEncounterSite s in AyniEncounterSite.All)
            {
                if (s == null) continue;
                Vector3 c = s.transform.position;
                if (Vector2.Distance(p, new Vector2(c.x, c.z)) < radius) return true;
            }
            return Vector2.Distance(p, new Vector2(620f, 470f)) < radius + 6f; // plaza de Amaru
        }

        /// <summary>
        /// Suelo llano y alcanzable a pie: poca pendiente y no mucho más alto ni más bajo que el camino (nada de
        /// ofrendas en lo alto de una peña o al fondo de una quebrada).
        /// </summary>
        private static bool TryGround(Vector2 road, Vector2 at, out Vector3 spot)
        {
            spot = Vector3.zero;
            if (!Physics.Raycast(new Vector3(road.x, 400f, road.y), Vector3.down, out RaycastHit roadHit, 800f,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (!Physics.Raycast(new Vector3(at.x, 400f, at.y), Vector3.down, out RaycastHit hit, 800f,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.88f) return false;
            if (Mathf.Abs(hit.point.y - roadHit.point.y) > 3f) return false;
            if (hit.collider.GetComponentInParent<EnemyController>() != null) return false;
            spot = hit.point;
            return true;
        }

        // ───────────────────────── La ofrenda ─────────────────────────

        private Offering Build(int index, Vector3 spot, System.Random rng)
        {
            var root = new GameObject($"Ofrenda_{index + 1}").transform;
            root.SetParent(transform, false);
            root.position = spot;
            root.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            Material stone = AyniFaunaPrimitives.Mat(new Color(0.46f, 0.43f, 0.4f), 0.1f);
            Material cloth = AyniFaunaPrimitives.Mat(new Color(0.7f, 0.12f, 0.12f), 0.02f);
            Material band = AyniFaunaPrimitives.Mat(new Color(0.92f, 0.72f, 0.18f), 0.02f);
            Material coca = AyniFaunaPrimitives.Mat(new Color(0.25f, 0.5f, 0.2f), 0.05f);
            Material mullu = AyniFaunaPrimitives.Mat(new Color(0.95f, 0.55f, 0.45f), 0.4f);
            Material flower = AyniFaunaPrimitives.Mat(new Color(0.95f, 0.8f, 0.2f), 0.05f);

            // Laja, atado de tela (unkuña) con su franja, hojas de coca, concha de mullu y flores de qantu
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Laja", root, stone, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.75f, 0.05f, 0.6f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Atado", root, cloth, new Vector3(-0.08f, 0.2f, 0f), Vector3.zero, new Vector3(0.3f, 0.22f, 0.26f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cube, "Franja", root, band, new Vector3(-0.08f, 0.21f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.31f, 0.04f, 0.05f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Nudo", root, cloth, new Vector3(-0.08f, 0.33f, 0f), Vector3.zero, new Vector3(0.09f, 0.08f, 0.09f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + (float)rng.NextDouble() * 20f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0.24f, 0.1f, 0f);
                AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Coca", root, coca, p + new Vector3(0.1f, 0f, 0f), new Vector3(0f, a, 0f), new Vector3(0.1f, 0.012f, 0.045f));
            }
            AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Mullu", root, mullu, new Vector3(0.18f, 0.12f, -0.1f), new Vector3(0f, 30f, 0f), new Vector3(0.11f, 0.06f, 0.08f));
            // Detrás, una pirca de piedras apiladas con un palo y una tira de tela roja: se distingue de lejos
            Material pirca = AyniFaunaPrimitives.Mat(new Color(0.52f, 0.49f, 0.45f), 0.08f);
            Material pole = AyniFaunaPrimitives.Mat(new Color(0.3f, 0.22f, 0.14f), 0.05f);
            float[] sizes = { 0.5f, 0.4f, 0.3f, 0.2f };
            float stackY = 0f;
            for (int i = 0; i < sizes.Length; i++)
            {
                float s = sizes[i];
                stackY += s * 0.32f;
                AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Pirca", root, pirca,
                    new Vector3(-0.05f + i * 0.02f, stackY, -0.55f), new Vector3(0f, i * 37f, 0f), new Vector3(s, s * 0.6f, s * 0.85f));
                stackY += s * 0.28f;
            }
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Palo", root, pole, new Vector3(0.12f, 0.75f, -0.5f), new Vector3(0f, 0f, 6f), new Vector3(0.035f, 0.75f, 0.035f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cube, "Tela", root, cloth, new Vector3(0.22f, 1.32f, -0.5f), new Vector3(0f, 0f, -8f), new Vector3(0.2f, 0.12f, 0.015f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cube, "Tela2", root, band, new Vector3(0.21f, 1.24f, -0.5f), new Vector3(0f, 0f, -10f), new Vector3(0.18f, 0.03f, 0.016f));
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(0.15f + i * 0.06f, 0.13f, 0.14f - i * 0.05f);
                AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Flor", root, flower, p, Vector3.zero, Vector3.one * 0.045f);
            }

            // Brillo: luz cálida y motas doradas que suben (se ven desde lejos)
            var lightGo = new GameObject("Brillo");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.82f, 0.45f);
            glow.range = 4f;
            glow.intensity = 2.2f;
            glow.shadows = LightShadows.None;

            ParticleSystem motes = Motes(root);
            return new Offering { index = index, root = root, glow = glow, motes = motes };
        }

        private static Material moteMaterial;

        private static ParticleSystem Motes(Transform parent)
        {
            var go = new GameObject("Motas");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startColor = new Color(1f, 0.85f, 0.45f, 0.9f);
            main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (moteMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) moteMaterial = new Material(shader) { mainTexture = SoftDot() };
            }
            if (moteMaterial != null) renderer.material = moteMaterial;
            ps.Play();
            return ps;
        }

        private static Texture2D SoftDot()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ───────────────────────── Recoger ─────────────────────────

        private bool Blocked()
        {
            return yari == null || yari.IsDead || yari.IsGameOver || yari.IsBeingRescued || AyniGameState.InputLocked ||
                   AyniGameState.CinematicPlaying || AyniJudgment.Active || AyniDeathAging.Active;
        }

        private bool Fighting()
        {
            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null || e.IsDead || e.Dormant || e.ExternalControl) continue;
                if (Flat(e.transform.position, yari.transform.position) < 12f) return true;
            }
            return false;
        }

        private void Update()
        {
            if (yari == null || offerings.Count == 0) return;
            focus = null;
            float best = float.MaxValue;
            foreach (Offering o in offerings)
            {
                if (o.taken) continue;
                float d = Flat(yari.transform.position, o.root.position);
                if (d < best)
                {
                    best = d;
                    focus = o;
                }
                if (o.glow != null) o.glow.intensity = 2f + 0.5f * Mathf.Sin(Time.time * 2.6f + o.index);
            }
            if (focus == null || best > interactRange || Blocked() || Fighting())
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
                return;
            }
            if (AyniInput.Held(AyniInput.Action.Interact))
            {
                progress += Time.deltaTime / Mathf.Max(0.1f, holdSeconds);
                if (progress >= 1f)
                {
                    progress = 0f;
                    Take(focus);
                }
            }
            else
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
            }
        }

        private void Take(Offering o)
        {
            if (o == null || o.taken) return;
            o.taken = true;
            if (o.glow != null) o.glow.enabled = false;
            if (o.motes != null) o.motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            // La ofrenda queda en su laja: Yari la acepta, no se la lleva (se apaga y se hunde el atado)
            foreach (Transform child in o.root)
            {
                if (child.name == "Atado" || child.name == "Franja" || child.name == "Nudo") child.gameObject.SetActive(false);
            }
            AyniAudio.Play("illa_brillo", o.root.position + Vector3.up * 0.5f, 0.55f, 0.05f);
            Count(firstTaken
                ? "La Pachamama acepta la ofrenda y te devuelve fuerzas."
                : "Una ofrenda a la Pachamama: hojas de coca y mullu. La tierra te devuelve fuerzas.");
            firstTaken = true;
        }

        /// <summary>Las llamas del chasqui traían una ofrenda entre su carga.</summary>
        public static void GrantFromLlamas()
        {
            if (instance == null) return;
            instance.Count(null);
        }

        private void Count(string caption)
        {
            Collected++;
            if (yari != null) yari.Heal(yari.MaxHealth * HealFraction);
            bool year = Collected % EveryForYear == 0;
            int years = year && talisman != null ? talisman.Rejuvenate(1) : 0;
            if (years > 0) caption = "La Pachamama agradece tus ofrendas: la Illa te devuelve un año.";
            if (!string.IsNullOrEmpty(caption)) AyniScreenFX.Caption(caption, 4f);
            Debug.Log($"[Ayni Ofrendas] {Collected}/{Total}{(years > 0 ? " · un año menos" : "")}");
            OnChanged?.Invoke();
        }

        // ───────────────────────── Interfaz ─────────────────────────

        private void OnGUI()
        {
            if (yari == null || offerings.Count == 0 || Blocked() || AyniBossIntro.Running) return;
            if (focus == null || focus.taken) return;
            float d = Flat(yari.transform.position, focus.root.position);
            if (d <= interactRange && !Fighting()) AyniHoldPrompt.Draw(focus.Top, "aceptar la ofrenda", progress);
            else if (d < 28f) AyniHoldPrompt.Marker(focus.Top, AyniInkaUI.BrightGold, "Ofrenda");
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Pone a Yari junto a la ofrenda <paramref name="n"/> (1 a 6), mirándola.</summary>
        public static void TestGoTo(int n) => Place(n, 0f);

        /// <summary>Pone a Yari en el camino, frente a la ofrenda <paramref name="n"/>: así se ve desde donde pasa el jugador.</summary>
        public static void TestFromRoad(int n) => Place(n, 1f);

        private static void Place(int n, float fromRoad)
        {
            if (instance == null || instance.yari == null || n < 1 || n > instance.offerings.Count)
            {
                Debug.LogWarning("[Ayni Ofrendas] No hay esa ofrenda (¿estás en Play?)");
                return;
            }
            Offering o = instance.offerings[n - 1];
            Vector3 p = o.root.position + o.root.forward * 2.2f;
            if (fromRoad > 0f)
            {
                // El punto del eje del camino más cercano
                Vector2 at = new Vector2(o.root.position.x, o.root.position.z), best = at;
                float bestD = float.MaxValue;
                for (int i = 0; i < RoadSpine.Length - 1; i++)
                {
                    Vector2 a = RoadSpine[i], ab = RoadSpine[i + 1] - a;
                    Vector2 q = a + ab * Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude);
                    if (Vector2.Distance(at, q) < bestD) { bestD = Vector2.Distance(at, q); best = q; }
                }
                p = new Vector3(best.x, o.root.position.y + 3f, best.y);
            }
            var cc = instance.yari.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = hit.point;
            Vector3 look = o.root.position - p;
            look.y = 0f;
            instance.yari.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.LookRotation(look));
            if (cc != null) cc.enabled = true;
            Debug.Log($"[Ayni Ofrendas] Yari junto a la ofrenda {n} ({o.root.position})");
        }

        /// <summary>Acepta la ofrenda <paramref name="n"/> como si Yari la recogiera.</summary>
        public static void TestTake(int n)
        {
            if (instance == null || n < 1 || n > instance.offerings.Count) return;
            instance.Take(instance.offerings[n - 1]);
        }

        public static void Report()
        {
            if (instance == null) { Debug.LogWarning("[Ayni Ofrendas] No hay ofrendas (¿estás en Play?)"); return; }
            var sb = new System.Text.StringBuilder($"[Ayni Ofrendas] {Collected}/{Total}");
            foreach (Offering o in instance.offerings)
            {
                float d = instance.yari != null ? Flat(instance.yari.transform.position, o.root.position) : -1f;
                sb.Append($"\n   {o.index + 1}: {o.root.position} · tomada {o.taken} · a {d:F0} m");
            }
            Debug.Log(sb.ToString());
        }
    }
}
