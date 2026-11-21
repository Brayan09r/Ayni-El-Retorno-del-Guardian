using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Fauna;
using Ayni.Player;
using Ayni.UI;
using Ayni.World.Fauna;

namespace Ayni.World
{
    /// <summary>Los trofeos vivos de Amaru que esperan enjaulados en la Portada del Cazador.</summary>
    public enum AyniCaptive
    {
        PumaCub,   // Cría de puma: su madre acude al duelo contra Amaru
        Condor,    // Cóndor: desde el cielo avisa de los dardos de Amaru
        Vicunas    // Vicuñas: al volver a la puna, la Pachamama sana a Yari y la Illa le devuelve años
    }

    /// <summary>
    /// Jaulas de la Portada del Cazador (K4). En el patio, entre los Cazadores de élite, hay tres jaulas de palos atados
    /// con soga: una cría de puma, un cóndor y dos vicuñas. Yari las abre manteniendo [V] / [LT] junto a la puerta
    /// (si le golpean, el nudo se le escapa y pierde la mitad de lo avanzado), así que liberar a un animal en plena pelea es un riesgo
    /// que se elige. Cada animal da algo:
    ///  - Vicuñas, en el momento: la Pachamama sana a Yari y la Illa le devuelve 2 años.
    ///  - Cóndor y cría de puma, en el duelo contra Amaru (ver <c>AyniFreedAllies</c>).
    /// Todo se construye por código al empezar: no hay que tocar la escena ni la aldea. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniCaptiveCages : MonoBehaviour
    {
        [Tooltip("Segundos que hay que mantener el botón para abrir una jaula.")]
        [SerializeField] private float holdSeconds = 1.4f;
        [Tooltip("Distancia a la puerta de la jaula desde la que se puede abrir.")]
        [SerializeField] private float interactRange = 2.1f;
        [Tooltip("Años que devuelve la Illa al liberar a las vicuñas.")]
        [SerializeField] private int vicunaYears = 2;

        /// <summary>Se libera un animal (para los aliados del duelo y el HUD).</summary>
        public static event Action<AyniCaptive> OnFreed;

        public static int Total { get; private set; }
        public static int Opened { get; private set; }
        public static bool PumaFreed { get; private set; }
        public static bool CondorFreed { get; private set; }
        public static bool VicunasFreed { get; private set; }
        /// <summary>Plantilla de puma de la escena (para la cría y para la madre que acude al duelo).</summary>
        public static GameObject PumaTemplate { get; private set; }

        private static AyniCaptiveCages instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnFreed = null;
            Total = Opened = 0;
            PumaFreed = CondorFreed = VicunasFreed = false;
            PumaTemplate = null;
            instance = null;
        }

        public static bool IsFreed(AyniCaptive kind) =>
            kind == AyniCaptive.PumaCub ? PumaFreed : kind == AyniCaptive.Condor ? CondorFreed : VicunasFreed;

        // Dónde va cada jaula en el patio de K4 (x a la derecha de la marcha, z en la marcha; ver AyniVillageBuilder.LayoutPortada).
        // La puerta mira al centro del patio.
        private struct Slot
        {
            public AyniCaptive kind;
            public Vector2 local;
            public float yaw;
            public Slot(AyniCaptive kind, float x, float z, float yaw)
            {
                this.kind = kind;
                local = new Vector2(x, z);
                this.yaw = yaw;
            }
        }

        private static readonly Slot[] Slots =
        {
            new Slot(AyniCaptive.PumaCub, -6.2f, 0.6f, 90f),
            new Slot(AyniCaptive.Condor, 6.0f, -5.6f, -90f),
            new Slot(AyniCaptive.Vicunas, 5.8f, -10.2f, -90f),
        };

        private readonly List<AyniCage> cages = new List<AyniCage>();
        private YariCombatController yari;
        private IllaTalismanSystem talisman;
        private AyniEncounterSite site;
        private AyniCage focus;
        private float progress, lastHealth;
        private bool toldHowTo;

        public static IReadOnlyList<AyniCage> Cages => instance != null ? instance.cages : (IReadOnlyList<AyniCage>)Array.Empty<AyniCage>();

        private void OnEnable() => instance = this;

        private void OnDisable()
        {
            if (instance == this) instance = null;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.GetComponent<YariCombatController>();
                talisman = player.GetComponent<IllaTalismanSystem>();
            }
            foreach (AyniEncounterSite s in AyniEncounterSite.All)
            {
                if (s != null && (site == null || s.Order > site.Order)) site = s; // la última kancha: la Portada del Cazador
            }
            if (site == null) return;

            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null) continue;
                var puma = e.GetComponent<PumaTerritory>();
                if (puma != null && !puma.Temporary) { PumaTemplate = e.gameObject; break; }
            }

            foreach (Slot slot in Slots)
            {
                if (slot.kind == AyniCaptive.PumaCub && PumaTemplate == null)
                {
                    Debug.LogWarning("[Ayni Jaulas] No hay pumas en la escena: la jaula de la cría no se pone");
                    continue;
                }
                Vector3 world = site.transform.TransformPoint(new Vector3(slot.local.x, 0f, slot.local.y));
                world.y = GroundY(world, site.transform.position.y);
                Quaternion rotation = site.transform.rotation * Quaternion.Euler(0f, slot.yaw, 0f);
                AyniCage cage = AyniCage.Build(slot.kind, world, rotation, transform);
                if (cage != null) cages.Add(cage);
            }
            Total = cages.Count;
            Debug.Log($"[Ayni Jaulas] {Total} jaulas en {site.SiteName}");
        }

        /// <summary>Altura del suelo del patio en ese punto (la más cercana a la del patio: ni tejados ni muros).</summary>
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

        // ───────────────────────── Abrir ─────────────────────────

        private bool Blocked()
        {
            return yari == null || yari.IsDead || yari.IsGameOver || yari.IsBeingRescued || AyniGameState.InputLocked ||
                   AyniGameState.CinematicPlaying || Ayni.Story.AyniJudgment.Active || AyniDeathAging.Active;
        }

        private void Update()
        {
            if (cages.Count == 0 || yari == null) return;

            // Cerca de una jaula cerrada por primera vez: cómo se abre
            Vector3 p = yari.transform.position;
            focus = null;
            float best = interactRange;
            foreach (AyniCage cage in cages)
            {
                if (cage == null || cage.IsOpen) continue;
                float d = Flat(p, cage.DoorPoint);
                if (!toldHowTo && d < 9f && !Blocked())
                {
                    toldHowTo = true;
                    AyniScreenFX.Caption($"Los trofeos vivos de Amaru, enjaulados. Mantén [{AyniInput.Label(AyniInput.Action.Interact)}] junto a una puerta para abrirla.", 4.5f);
                }
                if (d < best)
                {
                    best = d;
                    focus = cage;
                }
            }

            float health = yari.CurrentHealth;
            bool hurt = health < lastHealth - 0.01f;
            lastHealth = health;

            if (focus == null || Blocked())
            {
                progress = 0f;
                return;
            }
            if (hurt && progress > 0f)
            {
                // Le golpearon: el nudo se le escapa a medias
                progress *= 0.5f;
                AyniAudio.Play("bloqueo", focus.DoorPoint + Vector3.up, 0.5f);
                return;
            }
            if (AyniInput.Held(AyniInput.Action.Interact))
            {
                if (progress <= 0f) AyniAudio.Play("ui_mover", focus.DoorPoint + Vector3.up, 0.6f);
                progress += Time.deltaTime / Mathf.Max(0.1f, holdSeconds);
                if (progress >= 1f)
                {
                    progress = 0f;
                    Free(focus);
                }
            }
            else
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
            }
        }

        private void Free(AyniCage cage)
        {
            if (cage == null || cage.IsOpen) return;
            cage.Open(site);
            Opened++;
            switch (cage.Kind)
            {
                case AyniCaptive.PumaCub:
                    PumaFreed = true;
                    AyniScreenFX.Caption("La cría de puma corre hacia el cerro. Su madre no olvidará a quien la liberó.", 4f);
                    break;
                case AyniCaptive.Condor:
                    CondorFreed = true;
                    AyniScreenFX.Caption("El cóndor vuelve al cielo. Desde lo alto vigilará a Amaru.", 4f);
                    break;
                case AyniCaptive.Vicunas:
                    VicunasFreed = true;
                    int years = talisman != null ? talisman.Rejuvenate(vicunaYears) : 0;
                    if (yari != null) yari.Heal(yari.MaxHealth);
                    AyniAudio.Play2D("illa_brillo", 0.6f, 1.2f);
                    AyniScreenFX.Caption(years > 0
                        ? $"Las vicuñas vuelven a la puna. La Pachamama te sana y la Illa te devuelve {years} años."
                        : "Las vicuñas vuelven a la puna. La Pachamama te sana.", 4.5f);
                    break;
            }
            Debug.Log($"[Ayni Jaulas] Liberado: {cage.Kind} ({Opened}/{Total})");
            OnFreed?.Invoke(cage.Kind);
            if (Opened == Total && Total > 0) StartCoroutine(AllFreedLater());
        }

        private IEnumerator AllFreedLater()
        {
            yield return new WaitForSeconds(4.6f);
            AyniScreenFX.Caption("Ningún trofeo vivo queda en manos de Amaru.", 3f);
        }

        // ───────────────────────── Interfaz ─────────────────────────

        private void OnGUI()
        {
            if (cages.Count == 0 || yari == null || Blocked()) return;
            if (Ayni.Story.AyniBossIntro.Running) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (AyniCage cage in cages)
            {
                if (cage == null || cage.IsOpen) continue;
                float d = Flat(yari.transform.position, cage.DoorPoint);
                if (d > 22f) continue;
                Vector3 screen = cam.WorldToScreenPoint(cage.transform.position + Vector3.up * (cage.Height + 0.45f));
                if (screen.z <= 0f) continue;
                var p = new Vector2(screen.x, Screen.height - screen.y);

                if (cage == focus)
                {
                    // Aviso con el botón y la barra de lo que falta para soltar el nudo
                    var band = new Rect(p.x - 130f, p.y - 40f, 260f, 44f);
                    AyniInkaUI.Band(band, 0.9f);
                    AyniInkaUI.Label(new Rect(band.x, band.y + 4f, band.width, 18f),
                        $"<b>[{AyniInput.Label(AyniInput.Action.Interact)}]</b> Mantén: liberar {cage.DisplayName}",
                        AyniInkaUI.Style(13, FontStyle.Normal, TextAnchor.MiddleCenter), AyniInkaUI.Bone);
                    AyniInkaUI.Bar(new Rect(band.x + 30f, band.y + 28f, band.width - 60f, 5f), progress, progress, AyniInkaUI.Jade);
                }
                else
                {
                    // Desde lejos: un rombo de jade sobre la jaula
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f);
                    var rect = new Rect(p.x - 6f, p.y - 6f, 12f, 12f);
                    Matrix4x4 prev = GUI.matrix;
                    GUIUtility.RotateAroundPivot(45f, rect.center);
                    AyniInkaUI.Fill(new Rect(rect.x - 2f, rect.y - 2f, 16f, 16f), new Color(0f, 0f, 0f, 0.5f));
                    AyniInkaUI.Fill(rect, new Color(AyniInkaUI.Jade.r, AyniInkaUI.Jade.g, AyniInkaUI.Jade.b, pulse));
                    GUI.matrix = prev;
                }
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Abre la jaula de ese animal como si Yari la hubiera abierto (0 puma, 1 cóndor, 2 vicuñas).</summary>
        public static void TestFree(int kind)
        {
            if (instance == null) { Debug.LogWarning("[Ayni Jaulas] No hay jaulas (¿estás en Play?)"); return; }
            foreach (AyniCage cage in instance.cages)
            {
                if (cage != null && (int)cage.Kind == kind && !cage.IsOpen)
                {
                    instance.Free(cage);
                    return;
                }
            }
            Debug.LogWarning($"[Ayni Jaulas] No hay jaula cerrada del tipo {kind}");
        }

        /// <summary>Estado de las jaulas en la consola.</summary>
        public static void Report()
        {
            Debug.Log($"[Ayni Jaulas] Abiertas {Opened}/{Total} · puma {PumaFreed} · cóndor {CondorFreed} · vicuñas {VicunasFreed} · " +
                      $"plantilla de puma: {(PumaTemplate != null ? PumaTemplate.name : "ninguna")}");
            foreach (AyniCage cage in Cages)
            {
                if (cage != null) Debug.Log($"[Ayni Jaulas]   {cage.Kind} en {cage.transform.position} · abierta {cage.IsOpen}");
            }
        }
    }

    /// <summary>
    /// Una jaula de palos de queñua atados con soga (hecha por código, sin modelos) y el animal que guarda.
    /// Mira hacia +Z: la puerta está en esa cara y gira sobre su bisagra al abrirse.
    /// </summary>
    public class AyniCage : MonoBehaviour
    {
        private static readonly Color Wood = new Color(0.34f, 0.23f, 0.14f);
        private static readonly Color WoodDark = new Color(0.22f, 0.15f, 0.09f);
        private static readonly Color Rope = new Color(0.62f, 0.52f, 0.36f);

        public AyniCaptive Kind { get; private set; }
        public bool IsOpen { get; private set; }
        public float Height { get; private set; }
        public string DisplayName { get; private set; }
        /// <summary>Punto justo delante de la puerta (desde donde se abre).</summary>
        public Vector3 DoorPoint => transform.TransformPoint(new Vector3(0f, 0f, depth * 0.5f + 0.7f));

        private float width, depth;
        private Transform door;
        private GameObject animal;
        private Animator animalAnim;
        private AyniCamelidModel.Parts[] vicunas;
        private AyniCondorModel.Parts condor;

        public static AyniCage Build(AyniCaptive kind, Vector3 position, Quaternion rotation, Transform parent)
        {
            var go = new GameObject("Jaula_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var cage = go.AddComponent<AyniCage>();
            cage.Kind = kind;
            switch (kind)
            {
                case AyniCaptive.PumaCub: cage.Frame(1.5f, 1.1f, 1.05f, "a la cría de puma"); break;
                case AyniCaptive.Condor: cage.Frame(1.5f, 1.4f, 1.75f, "al cóndor"); break;
                default: cage.Frame(2.3f, 1.7f, 1.45f, "a las vicuñas"); break;
            }
            cage.FillAnimal();
            return cage;
        }

        // ───────────────────────── Construcción ─────────────────────────

        private void Frame(float w, float d, float h, string displayName)
        {
            width = w;
            depth = d;
            Height = h;
            DisplayName = displayName;
            Material wood = AyniFaunaPrimitives.Mat(Wood, 0.05f);
            Material dark = AyniFaunaPrimitives.Mat(WoodDark, 0.05f);
            Material rope = AyniFaunaPrimitives.Mat(Rope, 0.05f);
            float hw = w * 0.5f, hd = d * 0.5f;

            // Postes de las esquinas y largueros de abajo y de arriba
            foreach (float x in new[] { -hw, hw })
            {
                foreach (float z in new[] { -hd, hd })
                {
                    Pole(transform, dark, new Vector3(x, 0f, z), new Vector3(x, h, z), 0.07f);
                    Lash(transform, rope, new Vector3(x, h - 0.08f, z));
                    Lash(transform, rope, new Vector3(x, 0.15f, z));
                }
            }
            foreach (float y in new[] { 0.06f, h })
            {
                Pole(transform, dark, new Vector3(-hw, y, -hd), new Vector3(hw, y, -hd), 0.05f);
                Pole(transform, dark, new Vector3(-hw, y, hd), new Vector3(hw, y, hd), 0.05f);
                Pole(transform, dark, new Vector3(-hw, y, -hd), new Vector3(-hw, y, hd), 0.05f);
                Pole(transform, dark, new Vector3(hw, y, -hd), new Vector3(hw, y, hd), 0.05f);
            }
            // Barrotes de los lados y del fondo
            Bars(wood, new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd), h);
            Bars(wood, new Vector3(-hw, 0f, -hd), new Vector3(-hw, 0f, hd), h);
            Bars(wood, new Vector3(hw, 0f, -hd), new Vector3(hw, 0f, hd), h);
            // Techo de varas
            for (float x = -hw + 0.25f; x < hw - 0.1f; x += 0.3f)
            {
                Pole(transform, wood, new Vector3(x, h + 0.04f, -hd), new Vector3(x, h + 0.04f, hd), 0.03f);
            }

            // Puerta en la cara delantera, con bisagra en el poste izquierdo y el nudo de soga a la derecha
            door = new GameObject("Puerta").transform;
            door.SetParent(transform, false);
            door.localPosition = new Vector3(-hw, 0f, hd);
            float dw = w;
            for (float x = 0.12f; x < dw - 0.06f; x += 0.19f)
            {
                Pole(door, wood, new Vector3(x, 0.05f, 0f), new Vector3(x, h - 0.05f, 0f), 0.032f);
            }
            Pole(door, dark, new Vector3(0.05f, h * 0.3f, 0f), new Vector3(dw - 0.05f, h * 0.3f, 0f), 0.035f);
            Pole(door, dark, new Vector3(0.05f, h * 0.8f, 0f), new Vector3(dw - 0.05f, h * 0.8f, 0f), 0.035f);
            AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Nudo", door, rope,
                new Vector3(dw - 0.05f, h * 0.55f, 0.03f), Vector3.zero, new Vector3(0.16f, 0.2f, 0.16f));

            // Colisión: tres paredes y la puerta (sin techo ni suelo, para que nada tropiece al salir)
            Wall(transform, new Vector3(0f, h * 0.5f, -hd), new Vector3(w, h, 0.12f));
            Wall(transform, new Vector3(-hw, h * 0.5f, 0f), new Vector3(0.12f, h, d));
            Wall(transform, new Vector3(hw, h * 0.5f, 0f), new Vector3(0.12f, h, d));
            Wall(door, new Vector3(dw * 0.5f, h * 0.5f, 0f), new Vector3(dw, h, 0.12f));
        }

        private void Bars(Material mat, Vector3 a, Vector3 b, float h)
        {
            float length = Vector3.Distance(a, b);
            int count = Mathf.Max(2, Mathf.RoundToInt(length / 0.2f));
            for (int i = 1; i < count; i++)
            {
                Vector3 foot = Vector3.Lerp(a, b, i / (float)count);
                Pole(transform, mat, foot + Vector3.up * 0.05f, foot + Vector3.up * (h - 0.03f), 0.03f);
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

        private static void Lash(Transform parent, Material mat, Vector3 at)
        {
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Soga", parent, mat, at, Vector3.zero, new Vector3(0.17f, 0.05f, 0.17f));
        }

        private static void Wall(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Colision");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private void FillAnimal()
        {
            switch (Kind)
            {
                case AyniCaptive.PumaCub:
                    animal = AyniVisualClone.Create(AyniCaptiveCages.PumaTemplate, "Cria_de_Puma");
                    if (animal == null) return;
                    animal.transform.SetParent(transform, false);
                    animal.transform.localPosition = Vector3.zero;
                    animal.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    animal.transform.localScale = AyniCaptiveCages.PumaTemplate.transform.lossyScale * 0.55f;
                    animal.SetActive(true);
                    animalAnim = AyniVisualClone.AnimatorOf(animal);
                    AyniVisualClone.Play(animalAnim, "Idle", 0f);
                    break;

                case AyniCaptive.Condor:
                    condor = AyniCondorModel.Build(transform, "Condor_Cautivo", 0.55f);
                    animal = condor.root;
                    animal.transform.localPosition = new Vector3(0f, 0.82f, 0f);
                    animal.transform.localRotation = Quaternion.Euler(-12f, 70f, 0f);
                    // Alas recogidas a los costados, como posado
                    if (condor.leftWing != null) condor.leftWing.localRotation = Quaternion.AngleAxis(68f, Vector3.forward);
                    if (condor.rightWing != null) condor.rightWing.localRotation = Quaternion.AngleAxis(-68f, Vector3.forward);
                    // Posadero
                    Pole(transform, AyniFaunaPrimitives.Mat(WoodDark, 0.05f), new Vector3(-width * 0.45f, 0.66f, 0f), new Vector3(width * 0.45f, 0.66f, 0f), 0.04f);
                    break;

                default:
                    vicunas = new AyniCamelidModel.Parts[2];
                    animal = new GameObject("Vicunas");
                    animal.transform.SetParent(transform, false);
                    for (int i = 0; i < 2; i++)
                    {
                        vicunas[i] = AyniCamelidModel.Build(animal.transform, "Vicuna_" + (i + 1), AyniCamelidSpecies.Vicuna, 4400 + i);
                        Transform t = vicunas[i].root.transform;
                        t.localScale = Vector3.one * 0.72f;
                        t.localPosition = new Vector3(i == 0 ? -0.45f : 0.5f, 0f, i == 0 ? 0.1f : -0.15f);
                        t.localRotation = Quaternion.Euler(0f, i == 0 ? 80f : -100f, 0f);
                    }
                    break;
            }
        }

        // ───────────────────────── Abrir ─────────────────────────

        /// <summary>Rompe el nudo, abre la puerta y suelta al animal: corre fuera por la entrada de la kancha (o vuela).</summary>
        public void Open(AyniEncounterSite site)
        {
            if (IsOpen) return;
            IsOpen = true;
            AyniAudio.Play("jaula_rota", DoorPoint + Vector3.up * 0.8f, 1f);
            StartCoroutine(SwingDoor());
            StartCoroutine(Release(site));
        }

        private IEnumerator SwingDoor()
        {
            Quaternion from = door.localRotation, to = Quaternion.Euler(0f, -105f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                float k = 1f - (1f - t) * (1f - t);
                door.localRotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }
            door.localRotation = to;
        }

        private IEnumerator Release(AyniEncounterSite site)
        {
            yield return new WaitForSeconds(0.35f);
            if (Kind == AyniCaptive.Condor)
            {
                yield return FlyAway(site);
                yield break;
            }

            // Ruta de huida: sale por la puerta, va al eje del camino y deja la kancha por la entrada (lejos de Amaru)
            var path = new List<Vector3> { transform.TransformPoint(new Vector3(0f, 0f, depth * 0.5f + 1.2f)) };
            if (site != null)
            {
                Vector3 local = site.transform.InverseTransformPoint(transform.position);
                path.Add(site.transform.TransformPoint(new Vector3(0f, 0f, local.z)));
                Vector3 gate = site.Entry != null ? site.Entry.position : site.transform.TransformPoint(new Vector3(0f, 0f, -14f));
                path.Add(gate);
                path.Add(gate - site.transform.forward * 40f);
            }

            if (Kind == AyniCaptive.PumaCub && animal != null)
            {
                animal.transform.SetParent(null, true);
                if (animalAnim != null)
                {
                    foreach (AnimatorControllerParameter param in animalAnim.parameters)
                    {
                        if (param.name == "Speed" && param.type == AnimatorControllerParameterType.Float) animalAnim.SetFloat("Speed", 1f);
                    }
                    if (!AyniVisualClone.Play(animalAnim, "Run", 0.15f)) AyniVisualClone.Play(animalAnim, "Move", 0.15f);
                }
                AyniFreedRunner.Run(animal, path, 6.5f, null, null);
            }
            else if (vicunas != null)
            {
                for (int i = 0; i < vicunas.Length; i++)
                {
                    GameObject v = vicunas[i].root;
                    v.transform.SetParent(null, true);
                    // La segunda sale un instante después, un paso al lado
                    var own = new List<Vector3>(path);
                    Vector3 side = Vector3.Cross(Vector3.up, transform.forward) * (i == 0 ? -0.6f : 0.6f);
                    for (int k = 0; k < own.Count; k++) own[k] += side;
                    AyniFreedRunner.Run(v, own, 5.2f + i * 0.4f, vicunas[i].legs, vicunas[i].body, i * 0.4f);
                }
            }
        }

        /// <summary>El cóndor sale aleteando, sube y se queda planeando en círculos sobre la plaza de Amaru.</summary>
        private IEnumerator FlyAway(AyniEncounterSite site)
        {
            if (condor.root == null) yield break;
            Transform t = condor.root.transform;
            t.SetParent(null, true);
            if (condor.leftWing != null) condor.leftWing.localRotation = Quaternion.identity;
            if (condor.rightWing != null) condor.rightWing.localRotation = Quaternion.identity;
            AyniAudio.Play("condor_aviso", t.position, 0.9f);

            Vector3 start = t.position;
            Vector3 forward = transform.forward;
            Vector3 target = start + forward * 14f + Vector3.up * 16f;
            for (float u = 0f; u < 1f; u += Time.deltaTime / 2.6f)
            {
                float k = u * u * (3f - 2f * u);
                t.position = Vector3.Lerp(start, target, k);
                Vector3 vel = target - start;
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(vel.normalized + Vector3.up * 0.2f), 4f * Time.deltaTime);
                float flap = Mathf.Sin(u * 2.6f * Mathf.PI * 2f * 1.6f) * 45f * (1f - k * 0.6f);
                if (condor.leftWing != null) condor.leftWing.localRotation = Quaternion.AngleAxis(-flap, Vector3.forward);
                if (condor.rightWing != null) condor.rightWing.localRotation = Quaternion.AngleAxis(flap, Vector3.forward);
                yield return null;
            }
            if (condor.leftWing != null) condor.leftWing.localRotation = Quaternion.identity;
            if (condor.rightWing != null) condor.rightWing.localRotation = Quaternion.identity;

            // Planea sobre la plaza del jefe (o sobre la kancha si no hay jefe)
            Vector3 center = t.position;
            foreach (EnemyController e in EnemyController.All)
            {
                if (e != null && e.IsBoss) center = e.transform.position;
            }
            var flight = t.gameObject.AddComponent<AyniCondorFlight>();
            flight.Configure(center, 22f, 18f, 9f, false, condor.leftWing, condor.rightWing);
        }
    }

    /// <summary>
    /// Lleva a un animal liberado por una ruta, pegado al suelo, moviendo las patas al trote (o con su Animator), y lo
    /// quita cuando ya está lejos y fuera de la vista.
    /// </summary>
    internal class AyniFreedRunner : MonoBehaviour
    {
        private List<Vector3> path;
        private int index;
        private float speed, phase, delay;
        private Transform[] legs;
        private Quaternion[] legRest;
        private Transform body;
        private Vector3 bodyRest;
        private Renderer[] renderers;
        private float doneAt = -1f;

        public static void Run(GameObject animal, List<Vector3> path, float speed, Transform[] legs, Transform body, float delay = 0f)
        {
            if (animal == null || path == null || path.Count == 0) return;
            var runner = animal.AddComponent<AyniFreedRunner>();
            runner.path = path;
            runner.speed = speed;
            runner.delay = delay;
            runner.legs = legs;
            runner.body = body;
            if (legs != null)
            {
                runner.legRest = new Quaternion[legs.Length];
                for (int i = 0; i < legs.Length; i++) runner.legRest[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;
            }
            if (body != null) runner.bodyRest = body.localPosition;
            runner.renderers = animal.GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            if (delay > 0f)
            {
                delay -= Time.deltaTime;
                return;
            }
            if (index >= path.Count)
            {
                // Al final se va cuando nadie lo mira (o a los pocos segundos)
                if (doneAt < 0f) doneAt = Time.time;
                bool seen = false;
                foreach (Renderer r in renderers) if (r != null && r.isVisible) seen = true;
                if (!seen || Time.time - doneAt > 5f) Destroy(gameObject);
                return;
            }

            Vector3 pos = transform.position;
            Vector3 to = path[index] - pos;
            to.y = 0f;
            float step = speed * Time.deltaTime;
            if (to.magnitude <= step)
            {
                pos = new Vector3(path[index].x, pos.y, path[index].z);
                index++;
            }
            else
            {
                pos += to.normalized * step;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * Time.deltaTime);
            }
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                pos.y = Mathf.Lerp(pos.y, hit.point.y, 12f * Time.deltaTime);
            }
            transform.position = pos;

            // Trote en diagonal: delantera izquierda con trasera derecha, y al revés
            if (legs != null)
            {
                phase += Time.deltaTime * speed * 2.6f;
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] == null) continue;
                    float sign = i == 0 || i == 3 ? 1f : -1f;
                    legs[i].localRotation = legRest[i] * Quaternion.Euler(Mathf.Sin(phase) * 32f * sign, 0f, 0f);
                }
                if (body != null) body.localPosition = bodyRest + Vector3.up * Mathf.Abs(Mathf.Sin(phase)) * 0.05f;
            }
        }
    }
}
