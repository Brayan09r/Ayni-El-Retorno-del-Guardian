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
    /// El corral de las llamas del chasqui, en el Puesto de Chasquis (K1). Los Rastreadores de Amaru amarraron a tres
    /// llamas de carga a una estaca, dentro de un corral de palos con la tranca anudada.
    ///  - Yari suelta la tranca manteniendo <c>V</c> / <c>LT</c> junto a ella. Puede hacerlo en plena pelea, pero si le
    ///    golpean el nudo se le escapa a medias (como en las jaulas de la Portada).
    ///  - Al soltarlas, las llamas salen al trote por la entrada de la kancha y vuelven a su ayllu. La Pachamama sana a
    ///    Yari y, entre la carga de las llamas, hay una ofrenda que cuenta para las del camino (<see cref="AyniOfferings"/>).
    /// Todo se construye por código al empezar; SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniLlamaCorral : MonoBehaviour
    {
        /// <summary>Las llamas ya están libres.</summary>
        public static bool Freed { get; private set; }
        /// <summary>El corral está en el nivel.</summary>
        public static bool Exists { get; private set; }
        /// <summary>Punto de la tranca (para los avisos y las pruebas).</summary>
        public static Vector3 GatePoint { get; private set; }

        // Dónde va el corral en el patio de K1 (x a la derecha de la marcha, z en la marcha; ver
        // AyniVillageBuilder.LayoutChasquis): a la izquierda, entre el fogón y la casa, con la tranca hacia el centro.
        private static readonly Vector2 SlotLocal = new Vector2(-5.4f, -3.4f);
        private const float Width = 2.8f;   // a lo largo de la marcha
        private const float Depth = 3.4f;   // hacia el centro del patio
        private const float FenceHeight = 1.15f;

        [SerializeField] private float interactRange = 2.2f;
        [SerializeField] private float holdSeconds = 1.3f;

        private static AyniLlamaCorral instance;
        private YariCombatController yari;
        private AyniEncounterSite site;
        private Transform gate;
        private readonly List<GameObject> llamas = new List<GameObject>();
        private readonly List<GameObject> tethers = new List<GameObject>();
        private float progress, lastHealth;
        private bool toldHowTo;

        private static readonly Color Wood = new Color(0.34f, 0.25f, 0.16f);
        private static readonly Color WoodDark = new Color(0.24f, 0.18f, 0.12f);
        private static readonly Color Rope = new Color(0.55f, 0.45f, 0.3f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Freed = false;
            Exists = false;
            GatePoint = Vector3.zero;
            instance = null;
        }

        private void OnEnable() => instance = this;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            foreach (AyniEncounterSite s in AyniEncounterSite.All)
            {
                if (s != null && s.Order == 1) site = s;
            }
            if (site == null) return;
            Build();
            Exists = true;
            Debug.Log($"[Ayni Llamas] Corral de llamas en {site.SiteName} (tranca en {GatePoint})");
        }

        // ───────────────────────── Construcción ─────────────────────────

        private void Build()
        {
            // El corral mira con su +X (la tranca) hacia el eje del patio
            Vector3 world = site.transform.TransformPoint(new Vector3(SlotLocal.x, 0f, SlotLocal.y));
            world.y = GroundY(world, site.transform.position.y);
            var root = new GameObject("Corral_de_Llamas").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(world, site.transform.rotation);

            Material wood = AyniFaunaPrimitives.Mat(Wood, 0.05f);
            Material dark = AyniFaunaPrimitives.Mat(WoodDark, 0.05f);
            Material rope = AyniFaunaPrimitives.Mat(Rope, 0.05f);
            float hx = Depth * 0.5f, hz = Width * 0.5f;

            // Tres lados fijos (fondo y laterales) de postes con dos travesaños
            Fence(root, dark, wood, new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz));
            Fence(root, dark, wood, new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz));
            Fence(root, dark, wood, new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz));
            Wall(root, new Vector3(-hx, FenceHeight * 0.5f, 0f), new Vector3(0.12f, FenceHeight, Width));
            Wall(root, new Vector3(0f, FenceHeight * 0.5f, -hz), new Vector3(Depth, FenceHeight, 0.12f));
            Wall(root, new Vector3(0f, FenceHeight * 0.5f, hz), new Vector3(Depth, FenceHeight, 0.12f));

            // La tranca: el lado que da al patio, con bisagra en un extremo y el nudo en el otro
            Pole(root, dark, new Vector3(hx, 0f, -hz), new Vector3(hx, FenceHeight + 0.12f, -hz), 0.06f);
            Pole(root, dark, new Vector3(hx, 0f, hz), new Vector3(hx, FenceHeight + 0.12f, hz), 0.06f);
            gate = new GameObject("Tranca").transform;
            gate.SetParent(root, false);
            gate.localPosition = new Vector3(hx, 0f, -hz);
            foreach (float y in new[] { 0.42f, 0.78f, 1.08f })
            {
                Pole(gate, wood, new Vector3(0f, y, 0.06f), new Vector3(0f, y, Width - 0.06f), 0.045f);
            }
            Pole(gate, dark, new Vector3(0f, 0.3f, Width * 0.5f), new Vector3(0f, 1.15f, Width * 0.5f), 0.04f);
            AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Nudo", gate, rope,
                new Vector3(0.04f, 0.85f, Width - 0.1f), Vector3.zero, new Vector3(0.11f, 0.13f, 0.11f));
            Wall(gate, new Vector3(0f, FenceHeight * 0.5f, Width * 0.5f), new Vector3(0.12f, FenceHeight, Width));
            GatePoint = root.TransformPoint(new Vector3(hx + 0.9f, 0f, Width * 0.5f - 0.3f));

            // Estaca en el centro y las llamas amarradas a ella
            Vector3 stake = new Vector3(-0.35f, 0f, 0f);
            Pole(root, dark, stake, stake + Vector3.up * 0.95f, 0.06f);
            Vector3[] spots = { new Vector3(0.55f, 0f, -0.75f), new Vector3(-0.95f, 0f, 0.55f), new Vector3(0.75f, 0f, 0.7f) };
            float[] yaws = { 35f, -150f, 160f };
            for (int i = 0; i < spots.Length; i++)
            {
                AyniCamelidModel.Parts llama = AyniCamelidModel.Build(root, "Llama_del_Chasqui_" + (i + 1), AyniCamelidSpecies.Llama, 7100 + i * 13);
                Transform t = llama.root.transform;
                t.localPosition = spots[i];
                t.localRotation = Quaternion.Euler(0f, yaws[i], 0f);
                t.localScale = Vector3.one * 0.82f;
                llamas.Add(llama.root);
                // Soga de la estaca al cuello (a la altura del pecho de la llama)
                Vector3 neck = spots[i] + Quaternion.Euler(0f, yaws[i], 0f) * new Vector3(0f, 0.95f, 0.45f);
                var tether = new GameObject("Soga_" + (i + 1));
                tether.transform.SetParent(root, false);
                Pole(tether.transform, rope, stake + Vector3.up * 0.8f, neck, 0.015f);
                tethers.Add(tether);
            }
        }

        private static void Fence(Transform parent, Material post, Material rail, Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b);
            int count = Mathf.Max(2, Mathf.RoundToInt(length / 0.9f) + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 foot = Vector3.Lerp(a, b, i / (float)(count - 1));
                Pole(parent, post, foot, foot + Vector3.up * (FenceHeight + 0.1f), 0.055f);
            }
            foreach (float y in new[] { 0.5f, 1.02f })
            {
                Pole(parent, rail, a + Vector3.up * y, b + Vector3.up * y, 0.04f);
            }
        }

        private static void Pole(Transform parent, Material mat, Vector3 from, Vector3 to, float radius)
        {
            Vector3 mid = (from + to) * 0.5f;
            Vector3 dir = to - from;
            var pole = AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Palo", parent, mat, mid, Vector3.zero,
                                                     new Vector3(radius * 2f, dir.magnitude * 0.5f, radius * 2f));
            pole.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        }

        private static void Wall(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Colision");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private static float GroundY(Vector3 point, float reference)
        {
            float best = float.MaxValue, y = reference;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, reference + 4f, point.z), Vector3.down, 10f,
                                                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < 0.7f) continue;
                float d = Mathf.Abs(hit.point.y - reference);
                if (d < best)
                {
                    best = d;
                    y = hit.point.y;
                }
            }
            return y;
        }

        // ───────────────────────── Soltar ─────────────────────────

        private bool Blocked()
        {
            return yari == null || yari.IsDead || yari.IsGameOver || yari.IsBeingRescued || AyniGameState.InputLocked ||
                   AyniGameState.CinematicPlaying || AyniJudgment.Active || AyniDeathAging.Active;
        }

        private float Distance => yari != null ? Flat(yari.transform.position, GatePoint) : float.MaxValue;

        private void Update()
        {
            if (!Exists || Freed || yari == null) return;
            float d = Distance;
            if (!toldHowTo && d < 9f && !Blocked())
            {
                toldHowTo = true;
                AyniScreenFX.Caption($"Las llamas del chasqui, amarradas por los Rastreadores. Mantén [{AyniInput.Label(AyniInput.Action.Interact)}] junto a la tranca para soltarlas.", 4.5f);
            }

            float health = yari.CurrentHealth;
            bool hurt = health < lastHealth - 0.01f;
            lastHealth = health;

            if (d > interactRange || Blocked())
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
                return;
            }
            if (hurt && progress > 0f)
            {
                // Le golpearon: el nudo se le escapa a medias
                progress *= 0.5f;
                AyniAudio.Play("bloqueo", GatePoint + Vector3.up, 0.5f);
                return;
            }
            if (AyniInput.Held(AyniInput.Action.Interact))
            {
                if (progress <= 0f) AyniAudio.Play("ui_mover", GatePoint + Vector3.up, 0.6f);
                progress += Time.deltaTime / Mathf.Max(0.1f, holdSeconds);
                if (progress >= 1f)
                {
                    progress = 0f;
                    Release();
                }
            }
            else
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
            }
        }

        private void Release()
        {
            if (Freed) return;
            Freed = true;
            AyniAudio.Play("jaula_rota", GatePoint + Vector3.up * 0.8f, 0.9f);
            foreach (GameObject tether in tethers) if (tether != null) tether.SetActive(false);
            StartCoroutine(SwingGate());
            StartCoroutine(RunHome());
            if (yari != null) yari.Heal(yari.MaxHealth);
            AyniAudio.Play2D("illa_brillo", 0.5f, 1.25f);
            AyniScreenFX.Caption("Las llamas vuelven a su ayllu. La Pachamama te sana.", 3.6f);
            StartCoroutine(OfferingLater());
            Debug.Log("[Ayni Llamas] Llamas liberadas");
        }

        /// <summary>Entre la carga de las llamas había una ofrenda: cuenta para las del camino.</summary>
        private IEnumerator OfferingLater()
        {
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(3.6f));
            AyniScreenFX.Caption("Entre su carga había una ofrenda para la Pachamama.", 3.2f);
            AyniOfferings.GrantFromLlamas();
        }

        private IEnumerator SwingGate()
        {
            Quaternion from = gate.localRotation, to = Quaternion.Euler(0f, 100f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                float k = 1f - (1f - t) * (1f - t);
                gate.localRotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }
            gate.localRotation = to;
        }

        /// <summary>Salen por la tranca, cruzan el patio y dejan la kancha por la entrada, una detrás de otra.</summary>
        private IEnumerator RunHome()
        {
            yield return new WaitForSeconds(0.4f);
            Transform root = gate.parent;
            Vector3 outside = root.TransformPoint(new Vector3(Depth * 0.5f + 1.6f, 0f, 0f));
            Vector3 local = site.transform.InverseTransformPoint(outside);
            Vector3 axis = site.transform.TransformPoint(new Vector3(0f, 0f, local.z));
            Vector3 entry = site.Entry != null ? site.Entry.position : site.transform.TransformPoint(new Vector3(0f, 0f, -14f));
            for (int i = 0; i < llamas.Count; i++)
            {
                GameObject llama = llamas[i];
                if (llama == null) continue;
                llama.transform.SetParent(null, true);
                var path = new List<Vector3> { outside, axis, entry, entry - site.transform.forward * 40f };
                Vector3 side = site.transform.right * ((i - 1) * 0.7f);
                for (int k = 1; k < path.Count; k++) path[k] += side;
                float speed = 2.6f;
                var anim = llama.GetComponentInChildren<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    // El galope de la llama avanza ~1.3 m/s a escala 1 (AyniGrazingAnimal.LlamaRunGround)
                    float ground = 1.3f * anim.transform.lossyScale.x;
                    speed = Mathf.Min(3.2f, ground * 2.4f);
                    anim.CrossFadeInFixedTime("Run", 0.15f);
                    anim.speed = Mathf.Clamp(speed / Mathf.Max(0.1f, ground), 1f, 2.6f);
                }
                AyniFreedRunner.Run(llama, path, speed, null, null, i * 0.45f);
            }
        }

        // ───────────────────────── Interfaz ─────────────────────────

        private void OnGUI()
        {
            if (!Exists || Freed || yari == null || Blocked() || AyniBossIntro.Running) return;
            float d = Distance;
            Vector3 top = GatePoint + Vector3.up * 1.7f;
            if (d <= interactRange) AyniHoldPrompt.Draw(top, "soltar a las llamas", progress);
            else if (d < 24f) AyniHoldPrompt.Marker(top, AyniInkaUI.Jade, "Llamas");
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Pone a Yari junto a la tranca, mirándola.</summary>
        public static void TestGoTo()
        {
            if (instance == null || instance.yari == null || !Exists) { Debug.LogWarning("[Ayni Llamas] No hay corral (¿estás en Play?)"); return; }
            Transform root = instance.gate.parent;
            Vector3 p = root.TransformPoint(new Vector3(Depth * 0.5f + 1.8f, 0f, 0.3f));
            var cc = instance.yari.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = hit.point;
            Vector3 look = root.position - p;
            look.y = 0f;
            instance.yari.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.LookRotation(look));
            if (cc != null) cc.enabled = true;
            Debug.Log($"[Ayni Llamas] Yari junto al corral ({p})");
        }

        /// <summary>Suelta a las llamas como si Yari deshiciera el nudo.</summary>
        public static void TestFree()
        {
            if (instance == null || !Exists) { Debug.LogWarning("[Ayni Llamas] No hay corral (¿estás en Play?)"); return; }
            instance.Release();
        }

        public static void Report()
        {
            Debug.Log($"[Ayni Llamas] Corral {(Exists ? "en " + GatePoint : "no está")} · libres {Freed}");
        }
    }
}
