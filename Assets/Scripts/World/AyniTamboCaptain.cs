using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;
using Ayni.UI;
using Ayni.World.Fauna;

namespace Ayni.World
{
    /// <summary>
    /// El Capitán del Tambo (K3) y su mini Juicio Ayni.
    ///  - Espera junto a la salida del tambo, con penacho rojo y disco de oro, mirando cómo pelean sus Guardias.
    ///  - Cuando caen todos (o si Yari le pega antes) entra él solo. No muere a golpes: al romperle la postura (o al
    ///    dejarlo sin vida) se abre un Juicio como el de los jefes, a cámara lenta.
    ///  - <b>Ayni:</b> lo perdona. Se lleva a su hermano, uno de los Cazadores de la Portada (K4 tiene uno menos), le da a
    ///    Yari la chicha del tambo (vida llena) y lo manda a la apacheta del camino.
    ///  - <b>Venganza:</b> lo remata. Yari se queda su champi de bronce: golpea un 20 % más fuerte a Amaru, pero la
    ///    apacheta no aceptará su piedra.
    /// Lo construye todo en Play a partir de la plantilla de rival del propio tambo. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniTamboCaptain : MonoBehaviour
    {
        public enum Phase { None, Waiting, Fighting, Judging, Spared, Slain }

        /// <summary>En qué punto está el capitán.</summary>
        public static Phase Current { get; private set; }
        /// <summary>Mira la pelea de sus guardias, aún sin entrar.</summary>
        public static bool Waiting => Current == Phase.Waiting;
        /// <summary>Pelea con Yari o está en su mini Juicio.</summary>
        public static bool Fighting => Current == Phase.Fighting || Current == Phase.Judging;
        /// <summary>El capitán (o null si el tambo no existe).</summary>
        public static EnemyController Captain { get; private set; }
        /// <summary>Nombre del Cazador de la Portada que se llevó el capitán al ser perdonado (o null).</summary>
        public static string DeserterName { get; private set; }
        /// <summary>El capitán está hablando: el acecho de Amaru espera a que termine.</summary>
        public static bool Talking { get; private set; }
        /// <summary>Copia visual del capitán, para que la apacheta lo muestre dejando su piedra.</summary>
        public static GameObject Figure { get; private set; }

        public const string CaptainName = "Capitán del Tambo";

        [SerializeField] private float healthMultiplier = 1.6f;
        [SerializeField] private float structureMultiplier = 1.5f;
        [SerializeField] private float damageMultiplier = 1.15f;
        [Tooltip("Segundos sin guardias en pie antes de que entre (por si llegan refuerzos en ese momento).")]
        [SerializeField] private float engageDelay = 1f;

        private static AyniTamboCaptain instance;
        private AyniEncounterSite site;
        private YariCombatController yari;
        private float noGuardsFor;
        private bool resolved, subscribed;
        private float judgingSince;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = Phase.None;
            Captain = null;
            DeserterName = null;
            Talking = false;
            Figure = null;
            instance = null;
        }

        private void OnEnable()
        {
            instance = this;
            AyniEncounterSite.OnPlayerEntered += HandleEntered;
        }

        private void OnDisable()
        {
            AyniEncounterSite.OnPlayerEntered -= HandleEntered;
            if (subscribed && AyniPurificationManager.Instance != null) AyniPurificationManager.Instance.OnCombatResolved -= HandleResolved;
            subscribed = false;
        }

        private IEnumerator Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == 3) site = s;
            if (site == null) yield break;
            yield return null; // la kancha ya repartió sus rutinas y segundos alientos

            if (!Spawn()) yield break;
            if (AyniPurificationManager.Instance != null)
            {
                AyniPurificationManager.Instance.OnCombatResolved += HandleResolved;
                subscribed = true;
            }
        }

        // ───────────────────────── El capitán ─────────────────────────

        private bool Spawn()
        {
            // Espera en el punto de rival más cercano a la salida: les cierra el paso hacia la Portada
            Transform post = null;
            Vector3 exitPos = site.Exit != null ? site.Exit.position : site.transform.position + site.transform.forward * 14f;
            float best = float.MaxValue;
            foreach (Transform p in site.SpawnPoints)
            {
                if (p == null) continue;
                float d = (p.position - exitPos).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    post = p;
                }
            }
            Vector3 pos = post != null ? post.position : Vector3.Lerp(site.transform.position, exitPos, 0.6f);
            if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                pos = hit.point + Vector3.up * 0.05f;
            }
            Vector3 look = site.transform.position - pos;
            look.y = 0f;

            EnemyController captain = site.CreateRival(pos, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward), "Capitan_Tambo");
            if (captain == null)
            {
                Debug.LogWarning("[Ayni Capitán] El tambo no tiene plantilla de rival: no hay capitán");
                return false;
            }

            // Quieto, mirando, hasta que caigan sus guardias
            captain.ExternalControl = true;
            captain.Dormant = true;
            captain.SecondWinds = 0;
            captain.Unkillable = true;
            captain.JudgedLikeBoss = true;
            captain.transform.localScale *= 1.08f;

            SetField(captain, "characterName", CaptainName);
            float maxHealth = GetFloat(captain, "maxHealth", 150f) * healthMultiplier;
            SetField(captain, "maxHealth", maxHealth);
            SetField(captain, "currentHealth", maxHealth);
            SetField(captain, "attackDamage", GetFloat(captain, "attackDamage", 10f) * damageMultiplier);
            StructureSystem structure = captain.Structure != null ? captain.Structure : captain.GetComponent<StructureSystem>();
            if (structure != null)
            {
                structure.SetMaxStructure(structure.MaxStructure * structureMultiplier);
                structure.ResetStructure();
            }

            Dress(captain);
            // Copia limpia (sin el tinte de la derrota) para mostrarlo luego en la apacheta si lo perdonan
            Figure = AyniVisualClone.Create(captain.gameObject, "Capitan_Tambo_Apacheta");
            site.AddRival(captain.gameObject);
            captain.gameObject.SetActive(true); // a la vista desde el principio, como los guardias en su rutina
            captain.OnDamaged += HandleDamaged;
            captain.OnDefeatRefused += HandleRefused;

            Captain = captain;
            Current = Phase.Waiting;
            Debug.Log($"[Ayni Capitán] {CaptainName} espera en {site.SiteName} junto a la salida ({pos}) · vida {maxHealth:F0}");
            return true;
        }

        /// <summary>Penacho rojo con cinta de oro en la cabeza y un disco de oro en el pecho: se le reconoce de lejos.</summary>
        private static void Dress(EnemyController captain)
        {
            Animator anim = captain.GetComponentInChildren<Animator>();
            Transform head = null, chest = null;
            if (anim != null && anim.isHuman)
            {
                head = anim.GetBoneTransform(HumanBodyBones.Head);
                chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
                if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            }
            foreach (Transform t in captain.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (head == null && n.Contains("head") && !n.Contains("end")) head = t;
                if (chest == null && (n.Contains("spine2") || n.Contains("chest"))) chest = t;
            }

            Material gold = AyniFaunaPrimitives.Mat(new Color(0.86f, 0.66f, 0.22f), 0.55f);
            Material red = AyniFaunaPrimitives.Mat(new Color(0.72f, 0.08f, 0.06f), 0.12f);
            Material scarlet = AyniFaunaPrimitives.Mat(new Color(0.95f, 0.32f, 0.08f), 0.12f);

            if (head != null)
            {
                Transform plume = Attach("Penacho", head, captain.transform, head.position + captain.transform.up * 0.16f);
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Llawtu", plume, gold, new Vector3(0f, -0.04f, 0f), Vector3.zero, new Vector3(0.24f, 0.025f, 0.24f));
                for (int i = 0; i < 5; i++)
                {
                    float spread = (i - 2) * 11f;
                    AyniFaunaPrimitives.Primitive(PrimitiveType.Capsule, "Pluma_" + i, plume, i % 2 == 0 ? red : scarlet,
                        new Vector3(Mathf.Sin(spread * Mathf.Deg2Rad) * 0.06f, 0.12f, -0.05f), new Vector3(-18f, 0f, spread),
                        new Vector3(0.045f, 0.16f + (i == 2 ? 0.04f : 0f), 0.02f));
                }
            }
            if (chest != null)
            {
                Transform plate = Attach("Disco", chest, captain.transform,
                                         chest.position + captain.transform.forward * 0.17f + captain.transform.up * 0.04f);
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Disco_Oro", plate, gold, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.17f, 0.01f, 0.17f));
            }
        }

        /// <summary>Pivote pegado a un hueso, colocado en el mundo y con escala 1 aunque el modelo esté escalado.</summary>
        private static Transform Attach(string name, Transform bone, Transform body, Vector3 world)
        {
            var t = new GameObject(name).transform;
            t.SetParent(bone, false);
            t.position = world;
            t.rotation = body.rotation;
            float s = Mathf.Max(0.0001f, bone.lossyScale.x);
            t.localScale = Vector3.one / s;
            return t;
        }

        // ───────────────────────── La pelea ─────────────────────────

        private void Update()
        {
            EnemyController captain = Captain;
            if (captain == null || site == null) return;

            switch (Current)
            {
                case Phase.Waiting:
                    FaceYari(captain);
                    if (!site.PlayerEntered) break;
                    noGuardsFor = GuardsStanding() == 0 ? noGuardsFor + Time.deltaTime : 0f;
                    if (noGuardsFor >= engageDelay) Engage("«¡Basta! Este tambo lo defiendo yo.»");
                    break;

                case Phase.Fighting:
                    if (captain.IsDead) Resolve(false);
                    else if (captain.Structure != null && captain.Structure.IsBroken) OpenJudgment();
                    break;

                case Phase.Judging:
                    if (captain.IsDead)
                    {
                        // Lo remataron a golpes durante el Juicio sin pulsar Venganza: cuenta como Venganza
                        if (Time.unscaledTime - judgingSince > 0.2f && !resolved) Resolve(false);
                    }
                    else if (!AyniJudgment.Active && Time.unscaledTime - judgingSince > 0.5f)
                    {
                        // Se cerró sin decisión (no debería): vuelve a pelear y a poder juzgarse
                        if (!AyniJudgment.Open(captain, JudgmentTitle, JudgmentLine, VengeanceText, AyniText))
                        {
                            captain.Unkillable = true;
                            Current = Phase.Fighting;
                        }
                    }
                    break;

                case Phase.Spared:
                    // Cuando Yari se aleja, el capitán se va (lo espera en la apacheta)
                    if (captain.gameObject.activeSelf && yari != null && !Talking &&
                        Flat(yari.transform.position, captain.transform.position) > 30f)
                    {
                        captain.gameObject.SetActive(false);
                    }
                    break;
            }
        }

        private int GuardsStanding()
        {
            int n = 0;
            foreach (GameObject rival in site.Rivals)
            {
                if (rival == null || rival == Captain.gameObject) continue;
                var enemy = rival.GetComponent<EnemyController>();
                if (enemy != null && !enemy.IsDead) n++;
            }
            return n;
        }

        private void FaceYari(EnemyController captain)
        {
            if (yari == null) return;
            Vector3 to = yari.transform.position - captain.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 35f * 35f || to.sqrMagnitude < 0.01f) return;
            captain.transform.rotation = Quaternion.RotateTowards(captain.transform.rotation, Quaternion.LookRotation(to), 90f * Time.deltaTime);
        }

        private void HandleEntered(AyniEncounterSite s)
        {
            if (s == null) return;
            if (s == site && Current == Phase.Waiting)
            {
                StartCoroutine(SayLater(3.4f, "«Mis guardias primero, forastero. Si sigues en pie, me tendrás a mí.»", 4f));
            }
            else if (s.Order == 4 && DeserterName != null)
            {
                StartCoroutine(SayLater(3.6f, null, 4f,
                    "Falta un Cazador en la Portada: el Capitán del Tambo cumplió su palabra."));
            }
        }

        private IEnumerator SayLater(float delay, string captainLine, float seconds, string narration = null)
        {
            yield return new WaitForSeconds(delay);
            if (captainLine != null) Say(captainLine, seconds);
            else AyniScreenFX.Caption(narration, seconds);
        }

        private static void Say(string line, float seconds) => AyniScreenFX.Caption("CAPITÁN DEL TAMBO: " + line, seconds);

        private void Engage(string line)
        {
            if (Current != Phase.Waiting || Captain == null) return;
            Current = Phase.Fighting;
            Captain.ExternalControl = false;
            Captain.Dormant = false;
            Captain.HuntRange = 45f;
            AyniAudio.Play2D("titulo_golpe", 0.8f, 0.85f);
            AyniScreenFX.Title(CaptainName.ToUpperInvariant(), "Rompe su postura y decide su destino", 2.8f);
            Say(line, 3.6f);
            AyniVoice.Reply(this, "YARI: «Entonces defiéndelo de mí.»", 3.6f, 2.6f);
            Debug.Log($"[Ayni Capitán] Entra en la pelea");
        }

        private void HandleDamaged(EnemyController captain)
        {
            // Si Yari le pega mientras mira, entra antes de tiempo
            if (Current == Phase.Waiting) Engage("«¿Te atreves a tocarme? Ven, pues.»");
        }

        private void HandleRefused(EnemyController captain)
        {
            // Sin vida pero sin Juicio aún: se le rompe la postura para abrirlo
            if (Current != Phase.Fighting || captain.Structure == null) return;
            captain.Structure.AddStructureDamage(captain.Structure.MaxStructure * 2f);
        }

        private const string JudgmentTitle = "JUICIO AYNI";
        private const string JudgmentLine = "El Capitán del Tambo cae de rodillas. Sus guardias te miran desde el suelo.";
        private const string VengeanceText = "Lo rematas y te quedas su champi:\nmás daño contra Amaru (Hucha).";
        private const string AyniText = "Lo perdonas. Se llevará a un Cazador\nde la Portada: uno menos.";

        private void OpenJudgment()
        {
            EnemyController captain = Captain;
            captain.Structure.HoldBroken = true;
            if (!AyniJudgment.Open(captain, JudgmentTitle, JudgmentLine, VengeanceText, AyniText)) return;
            captain.Unkillable = false;
            Current = Phase.Judging;
            judgingSince = Time.unscaledTime;
            Debug.Log("[Ayni Capitán] Mini Juicio abierto");
        }

        private void HandleResolved(EnemyController enemy, bool spared)
        {
            if (enemy == null || enemy != Captain || resolved) return;
            Resolve(spared);
        }

        // ───────────────────────── Consecuencias ─────────────────────────

        private void Resolve(bool spared)
        {
            if (resolved || Captain == null) return;
            resolved = true;
            Captain.JudgedLikeBoss = false;
            Captain.Unkillable = false;
            if (spared)
            {
                Current = Phase.Spared;
                AyniPathRewards.Grant(AyniPathRewards.Reward.CaptainSpared);
                TakeDeserter();
                StartCoroutine(SparedTalk());
            }
            else
            {
                Current = Phase.Slain;
                AyniPathRewards.Grant(AyniPathRewards.Reward.CaptainSlain);
                foreach (EnemyController e in EnemyController.All)
                {
                    if (e != null && e.IsBoss) e.IncomingDamageMultiplier = AyniPathRewards.ChampiDamageBonus;
                }
                StartCoroutine(SlainTalk());
            }
            Debug.Log($"[Ayni Capitán] {(spared ? "Perdonado" : "Ejecutado")}" + (DeserterName != null ? $" · se lleva a {DeserterName} de la Portada" : ""));
        }

        /// <summary>Quita de la Portada del Cazador a uno de sus rivales (si Yari aún no ha entrado).</summary>
        private static void TakeDeserter()
        {
            AyniEncounterSite gate = null;
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == 4) gate = s;
            if (gate == null || gate.PlayerEntered) return;
            foreach (GameObject rival in gate.Rivals)
            {
                if (rival == null) continue;
                var enemy = rival.GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead || enemy.IsBoss) continue;
                DeserterName = enemy.CharacterName;
                Destroy(rival);
                return;
            }
        }

        private IEnumerator SparedTalk()
        {
            Talking = true;
            yield return new WaitForSecondsRealtime(2.2f);
            Say("«Servía a Sayri por miedo, no por lealtad. Nadie me había tendido la mano.»", 4.4f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(4.6f));
            AyniScreenFX.Caption("YARI: «El miedo se acaba hoy. Levántate, capitán.»", 3f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(3f));
            Say(DeserterName != null
                ? "«Uno de los Cazadores de la Portada es mi hermano. Me lo llevaré lejos: tendrás uno menos.»"
                : "«Me iré lejos de Amaru. No volveré a levantar la champi contra el pueblo.»", 4.6f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(4.8f));
            if (yari != null)
            {
                float healed = yari.Heal(9999f);
                if (healed > 0f) AyniAudio.Play2D("illa_brillo", 0.5f, 1.2f);
            }
            Say("«Bebe la chicha del tambo, guardián. Y deja una piedra en la apacheta del camino: el Apu escucha a quien camina con Ayni.»", 5.4f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(5.2f));
            Talking = false;
        }

        private IEnumerator SlainTalk()
        {
            Talking = true;
            yield return new WaitForSecondsRealtime(2f);
            AyniScreenFX.Caption("El capitán cae. Yari recoge su champi de bronce: pesa, y golpea fuerte.", 4f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(4.2f));
            AyniScreenFX.Caption("Con ella dolerán más tus golpes contra Amaru. Pero la Hucha se pega a quien la empuña.", 4.4f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(4.4f));
            Talking = false;
        }

        // ───────────────────────── Utilidades ─────────────────────────

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) f.SetValue(target, value);
            else Debug.LogWarning($"[Ayni Capitán] EnemyController no tiene el campo {name}");
        }

        private static float GetFloat(object target, string name, float fallback)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null && f.FieldType == typeof(float) ? (float)f.GetValue(target) : fallback;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static float BossMultiplier()
        {
            foreach (EnemyController e in EnemyController.All) if (e != null && e.IsBoss) return e.IncomingDamageMultiplier;
            return 1f;
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Derrota a los Guardias del tambo (no al capitán): el capitán entra en la pelea.</summary>
        public static void TestClearGuards()
        {
            if (instance == null || instance.site == null) { Debug.LogWarning("[Ayni Capitán] No hay tambo (¿estás en Play?)"); return; }
            int n = 0;
            foreach (GameObject rival in new List<GameObject>(instance.site.Rivals))
            {
                if (rival == null || (Captain != null && rival == Captain.gameObject)) continue;
                var enemy = rival.GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead) continue;
                enemy.Defeat(true);
                n++;
            }
            Debug.Log($"[Ayni Capitán] {n} guardias derrotados · fase {Current}");
        }

        /// <summary>Rompe la postura del capitán (abre el mini Juicio si ya está peleando).</summary>
        public static void TestBreak()
        {
            if (Captain == null || Captain.Structure == null) { Debug.LogWarning("[Ayni Capitán] No hay capitán"); return; }
            if (Current == Phase.Waiting) instance.Engage("«¿Te atreves a tocarme? Ven, pues.»");
            Captain.Structure.AddStructureDamage(Captain.Structure.MaxStructure * 2f);
            Debug.Log($"[Ayni Capitán] Postura rota · fase {Current}");
        }

        /// <summary>Resuelve el mini Juicio como si Yari pulsara el botón (1 Ayni, 0 Venganza).</summary>
        public static void TestResolve(int mercy)
        {
            if (Captain == null || AyniPurificationManager.Instance == null) { Debug.LogWarning("[Ayni Capitán] No hay capitán"); return; }
            AyniPurificationManager.Instance.TriggerExecutionAction(Captain.transform.position, mercy != 0);
        }

        /// <summary>Estado del capitán en la consola.</summary>
        public static void Report()
        {
            if (Captain == null) { Debug.Log($"[Ayni Capitán] Sin capitán · fase {Current}"); return; }
            Debug.Log($"[Ayni Capitán] Fase {Current} · vida {Captain.CurrentHealth:F0}/{Captain.MaxHealth:F0} · " +
                      $"postura {(Captain.Structure != null ? Captain.Structure.CurrentStructure : 0f):F0}/{(Captain.Structure != null ? Captain.Structure.MaxStructure : 0f):F0} · " +
                      $"en {Captain.transform.position} · perdonado {AyniPathRewards.CaptainSpared} · ejecutado {AyniPathRewards.CaptainSlain} · " +
                      $"desertor {DeserterName ?? "—"} · juicio {AyniJudgment.Active} · daño a Amaru x{BossMultiplier():F2}");
        }
    }
}
