using System.Collections;
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
    /// Los quipus de Amaru: cuatro quipus escondidos dentro de una casa de cada kancha (uno por kancha) que cuentan quién
    /// era Amaru antes de ser el Cazador. Se leen manteniendo <c>V</c> / <c>LT</c> cuando no hay pelea cerca; la narradora
    /// lee los nudos y, en algunos, Yari responde.
    ///  - Con los cuatro (<see cref="KnowsStory"/>), Yari conoce a Killa, la hermana de Amaru: en el duelo la nombra y
    ///    Amaru duda (pierde postura y se tambalea, ver AyniBossBarks), y el desenlace cuenta qué será de ella.
    ///  - Con menos, la tarjeta final dice cuántos leyó.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniAmaruQuipus : MonoBehaviour
    {
        public const int Total = 4;

        /// <summary>Cuántos quipus ha leído Yari.</summary>
        public static int Read { get; private set; }
        /// <summary>Leyó los cuatro: conoce la historia de Amaru y el nombre de Killa.</summary>
        public static bool KnowsStory => Read >= Total;
        /// <summary>Cuántos se pudieron colocar en el nivel.</summary>
        public static int Placed { get; private set; }

        /// <summary>Lo que dicen los nudos, por kancha (1 a 4), en orden de la vida de Amaru.</summary>
        private static readonly string[] Knots =
        {
            "QUIPU DE AMARU I: Amaru nació en un ayllu del Antisuyo, al borde de la selva alta. De niño cuidaba las vicuñas y les silbaba para que bajaran del cerro.",
            "QUIPU DE AMARU II: Una sequía secó los ríos de la selva. Su ayllu pidió ayuda a los guardianes del Cusco, pero nadie subió. Amaru enterró solo a su madre.",
            "QUIPU DE AMARU III: Sayri sí subió. Le dio maíz, una cerbatana y un nombre nuevo: el Cazador. A cambio le pidió las pieles, las plumas y los bosques del Antisuyo.",
            "QUIPU DE AMARU IV: El último nudo lo ató su hermana Killa: «Vuelve a casa, Amaru. Ya no quiero maíz que huele a humo.»",
        };

        /// <summary>Lo que contesta Yari (vacío: nada).</summary>
        private static readonly string[] Replies =
        {
            "",
            "YARI: «Nadie subió... Yo era uno de esos guardianes.»",
            "",
            "YARI: «Amaru no caza por odio. Caza porque nadie volvió por él.»",
        };

        [SerializeField] private float interactRange = 2.2f;
        [SerializeField] private float holdSeconds = 0.8f;

        private class Quipu
        {
            public int index;            // 0..3 = kancha 1..4
            public AyniEncounterSite site;
            public Transform root;
            public Vector3 top;          // encima del quipu (para el aviso)
            public Light glow;
            public bool read;
        }

        private static AyniAmaruQuipus instance;
        private readonly List<Quipu> quipus = new List<Quipu>();
        private YariCombatController yari;
        private Quipu reading;
        private float progress;
        private static readonly Color MarkerColor = new Color(0.95f, 0.72f, 0.3f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Read = 0;
            Placed = 0;
            instance = null;
        }

        private void OnEnable() => instance = this;

        private IEnumerator Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            // Un fotograma: que la casa en llamas de las tejedoras ya esté elegida
            yield return null;
            for (int order = 1; order <= Total; order++)
            {
                AyniEncounterSite site = null;
                foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == order) site = s;
                if (site == null) continue;
                if (!FindSpot(site, out Vector3 spot, out Vector3 facing)) continue;
                quipus.Add(Build(order - 1, site, spot, facing));
            }
            Placed = quipus.Count;
            Debug.Log($"[Ayni Quipus] {Placed} quipus de Amaru colocados");
        }

        // ───────────────────────── Dónde ─────────────────────────

        /// <summary>
        /// Dentro de una casa de la kancha: el punto interior más lejano de la entrada (en la de las tejedoras, nunca la casa
        /// que arde), contra la pared del fondo. <paramref name="facing"/> mira hacia la puerta.
        /// </summary>
        private static bool FindSpot(AyniEncounterSite site, out Vector3 spot, out Vector3 facing)
        {
            spot = Vector3.zero;
            facing = Vector3.forward;
            Vector3 entry = site.Entry != null ? site.Entry.position : site.transform.position;
            float best = float.MinValue;
            bool found = false;
            foreach (Transform point in site.InteriorPoints)
            {
                if (point == null) continue;
                if (site.Order == 2 && AyniWeaversFire.HasBurningHouse)
                {
                    Bounds b = AyniWeaversFire.BurningHouse;
                    b.Expand(1.5f);
                    if (b.Contains(point.position + Vector3.up)) continue;
                }
                float score = Flat(point.position, entry);
                if (score <= best) continue;

                Vector3 toDoor = point.forward;
                toDoor.y = 0f;
                if (toDoor.sqrMagnitude < 0.01f) toDoor = Vector3.forward;
                toDoor.Normalize();
                // Hacia el fondo de la casa, sin meterse en la pared
                Vector3 eye = point.position + Vector3.up * 1f;
                float back = 0.9f;
                if (Physics.Raycast(eye, -toDoor, out RaycastHit wall, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    back = Mathf.Clamp(wall.distance - 0.45f, 0f, 0.9f);
                }
                Vector3 p = point.position - toDoor * back;
                if (Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out RaycastHit floor, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    p = floor.point;
                }
                best = score;
                spot = p;
                facing = toDoor;
                found = true;
            }
            if (!found) Debug.LogWarning($"[Ayni Quipus] {site.SiteName} no tiene puntos interiores para el quipu");
            return found;
        }

        // ───────────────────────── El quipu ─────────────────────────

        /// <summary>Un bastidor de palos con el quipu colgado del travesaño: cuerda madre y cuerdas de colores con nudos.</summary>
        private Quipu Build(int index, AyniEncounterSite site, Vector3 spot, Vector3 facing)
        {
            var root = new GameObject($"Quipu_de_Amaru_{index + 1}").transform;
            root.position = spot;
            root.rotation = Quaternion.LookRotation(facing);
            var rng = new System.Random(4401 + index * 17);

            Material wood = AyniFaunaPrimitives.Mat(new Color(0.42f, 0.3f, 0.18f), 0.05f);
            const float height = 1.05f, half = 0.32f;
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Palo_I", root, wood, new Vector3(-half, height / 2f, 0f), Vector3.zero, new Vector3(0.035f, height / 2f, 0.035f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Palo_D", root, wood, new Vector3(half, height / 2f, 0f), Vector3.zero, new Vector3(0.035f, height / 2f, 0.035f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Travesano", root, wood, new Vector3(0f, height, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.03f, half + 0.05f, 0.03f));

            // Cuerda madre y colgantes: colores del Antisuyo (verde de selva, rojo, ocre, blanco de vicuña, negro)
            Color[] colors =
            {
                new Color(0.16f, 0.48f, 0.26f), new Color(0.72f, 0.12f, 0.1f), new Color(0.82f, 0.62f, 0.25f),
                new Color(0.9f, 0.88f, 0.82f), new Color(0.12f, 0.1f, 0.09f), new Color(0.2f, 0.4f, 0.22f),
            };
            Material mother = AyniFaunaPrimitives.Mat(new Color(0.62f, 0.52f, 0.36f), 0.02f);
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Cuerda_Madre", root, mother, new Vector3(0f, height - 0.05f, 0.02f), new Vector3(0f, 0f, 90f), new Vector3(0.022f, half - 0.04f, 0.022f));
            int cords = 9;
            for (int i = 0; i < cords; i++)
            {
                float x = Mathf.Lerp(-half + 0.07f, half - 0.07f, i / (float)(cords - 1));
                float length = 0.32f + 0.18f * (float)rng.NextDouble();
                Material m = AyniFaunaPrimitives.Mat(colors[rng.Next(colors.Length)], 0.02f);
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Cuerda", root, m,
                    new Vector3(x, height - 0.05f - length / 2f, 0.02f), new Vector3(0f, 0f, ((float)rng.NextDouble() - 0.5f) * 6f),
                    new Vector3(0.014f, length / 2f, 0.014f));
                int knots = 1 + rng.Next(3);
                for (int k = 0; k < knots; k++)
                {
                    float y = height - 0.12f - (float)rng.NextDouble() * (length - 0.1f);
                    AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Nudo", root, m, new Vector3(x, y, 0.02f), Vector3.zero, Vector3.one * 0.032f);
                }
            }

            // Una luz cálida y tenue: dentro de las casas está oscuro
            var lightGo = new GameObject("Luz_Quipu");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, height + 0.25f, 0.5f);
            var glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.78f, 0.45f);
            glow.range = 3.2f;
            glow.intensity = 1.2f;
            glow.shadows = LightShadows.None;

            return new Quipu { index = index, site = site, root = root, top = spot + Vector3.up * (height + 0.45f), glow = glow };
        }

        // ───────────────────────── Leer ─────────────────────────

        private void Update()
        {
            if (yari == null) return;
            foreach (Quipu q in quipus)
            {
                if (q.read || q.glow == null) continue;
                q.glow.intensity = 1.1f + 0.25f * Mathf.Sin(Time.time * 2.3f + q.index);
            }

            Quipu near = Nearest(out float dist);
            if (near == null || reading != null || !CanInteract(dist))
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
                    StartCoroutine(ReadRoutine(near));
                }
            }
            else
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
            }
        }

        private Quipu Nearest(out float dist)
        {
            Quipu best = null;
            dist = float.MaxValue;
            foreach (Quipu q in quipus)
            {
                if (q.read || q.root == null) continue;
                float d = Flat(yari.transform.position, q.root.position);
                if (d < dist)
                {
                    dist = d;
                    best = q;
                }
            }
            return best;
        }

        private bool CanInteract(float dist)
        {
            if (dist > interactRange) return false;
            if (AyniGameState.InputLocked || AyniGameState.CinematicPlaying || AyniJudgment.Active) return false;
            if (yari.IsBeingRescued || yari.IsGameOver) return false;
            return !Fighting();
        }

        /// <summary>¿Hay un rival peleando cerca de Yari?</summary>
        private bool Fighting()
        {
            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null || e.IsDead || e.Dormant || e.ExternalControl) continue;
                if (Flat(e.transform.position, yari.transform.position) < 12f) return true;
            }
            return false;
        }

        private IEnumerator ReadRoutine(Quipu q)
        {
            reading = q;
            q.read = true;
            Read++;
            Debug.Log($"[Ayni Quipus] Leído el quipu {q.index + 1} ({q.site.SiteName}) · {Read}/{Total}");
            AyniAudio.Play2D("illa_brillo", 0.45f);
            if (q.glow != null) q.glow.enabled = false;

            AyniScreenFX.Caption(Knots[q.index], 6.5f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(6.5f));
            if (!string.IsNullOrEmpty(Replies[q.index]))
            {
                AyniScreenFX.Caption(Replies[q.index], 3.4f);
                yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(3.4f));
            }
            if (KnowsStory)
            {
                AyniScreenFX.Title("LA HISTORIA DE AMARU", "Conoces el nombre de Killa. En el duelo, Amaru la escuchará.", 3.6f);
                Debug.Log("[Ayni Quipus] Yari conoce la historia de Amaru");
            }
            reading = null;
        }

        // ───────────────────────── Dibujo ─────────────────────────

        private void OnGUI()
        {
            if (yari == null || quipus.Count == 0) return;
            if (AyniGameState.CinematicPlaying || AyniGameState.InputLocked || AyniJudgment.Active || AyniBossIntro.Running || AyniDeathAging.Active) return;
            if (yari.IsBeingRescued || yari.IsGameOver || reading != null) return;
            Quipu near = Nearest(out float dist);
            if (near == null) return;
            if (CanInteract(dist)) AyniHoldPrompt.Draw(near.top, "leer el quipu", progress);
            else if (dist < 16f && !Fighting()) AyniHoldPrompt.Marker(near.top, MarkerColor, "Quipu");
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Pone a Yari delante del quipu de la kancha <paramref name="order"/> (1 a 4), mirándolo.</summary>
        public static void TestGoTo(int order)
        {
            Quipu q = Find(order);
            if (q == null || instance.yari == null) return;
            Vector3 facing = q.root.forward;
            Vector3 p = q.root.position + facing * 1.4f;
            var cc = instance.yari.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = hit.point;
            instance.yari.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.LookRotation(-facing));
            if (cc != null) cc.enabled = true;
            Debug.Log($"[Ayni Quipus] Yari ante el quipu {order} ({p})");
        }

        /// <summary>Lee el quipu de la kancha <paramref name="order"/> como si Yari lo leyera.</summary>
        public static void TestRead(int order)
        {
            Quipu q = Find(order);
            if (q == null) return;
            if (q.read) { Debug.LogWarning($"[Ayni Quipus] El quipu {order} ya está leído"); return; }
            instance.StartCoroutine(instance.ReadRoutine(q));
        }

        /// <summary>Da por leídos todos los quipus, sin narración (para probar el duelo y el desenlace).</summary>
        public static void TestLearnAll()
        {
            if (instance == null) { Debug.LogWarning("[Ayni Quipus] No hay quipus (¿estás en Play?)"); return; }
            foreach (Quipu q in instance.quipus)
            {
                if (q.read) continue;
                q.read = true;
                if (q.glow != null) q.glow.enabled = false;
            }
            Read = Total;
            Debug.Log("[Ayni Quipus] Yari conoce la historia de Amaru (prueba)");
        }

        /// <summary>Resuelve el Juicio de Amaru como si Yari pulsara el botón (1 Ayni, 0 Venganza), para ver el desenlace.</summary>
        public static void TestJudge(int mercy)
        {
            if (AyniPurificationManager.Instance == null) { Debug.LogWarning("[Ayni Quipus] No hay AyniPurificationManager"); return; }
            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null || !e.IsBoss) continue;
                if (!e.CanBeJudged) { Debug.LogWarning($"[Ayni Quipus] {e.CharacterName} aún no puede ser juzgado"); return; }
                AyniPurificationManager.Instance.TriggerExecutionAction(e.transform.position, mercy != 0);
                return;
            }
        }

        /// <summary>Estado en la consola.</summary>
        public static void Report()
        {
            if (instance == null) { Debug.LogWarning("[Ayni Quipus] No hay quipus (¿estás en Play?)"); return; }
            var sb = new System.Text.StringBuilder($"[Ayni Quipus] Leídos {Read}/{Total} · colocados {Placed} · historia {KnowsStory}");
            foreach (Quipu q in instance.quipus)
            {
                float d = instance.yari != null ? Flat(instance.yari.transform.position, q.root.position) : -1f;
                sb.Append($"\n   {q.index + 1}: {q.site.SiteName} en {q.root.position} · leído {q.read} · a {d:F1} m de Yari");
            }
            Debug.Log(sb.ToString());
        }

        private static Quipu Find(int order)
        {
            if (instance == null) { Debug.LogWarning("[Ayni Quipus] No hay quipus (¿estás en Play?)"); return null; }
            foreach (Quipu q in instance.quipus) if (q.index == order - 1) return q;
            Debug.LogWarning($"[Ayni Quipus] No hay quipu en la kancha {order}");
            return null;
        }
    }
}
