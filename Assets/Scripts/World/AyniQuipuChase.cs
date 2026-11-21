using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;
using Ayni.World.Fauna;

namespace Ayni.World
{
    /// <summary>
    /// El quipu del chasqui (Puesto de Chasquis, K1). Al empezar la pelea, un Rastreador agarra el quipu de los
    /// mensajeros y huye por el camino hacia Amaru. Si Yari lo alcanza y le pega, el Rastreador se gira a pelear; al
    /// derrotarlo, Yari recupera el quipu y aprende a leer los dardos de Amaru (ver <c>AyniDartReadCue</c>). Si el
    /// Rastreador se aleja lo bastante sin que lo toquen, escapa: Amaru queda avisado y siembra trampas en su plaza
    /// (ver <c>AyniHunterTraps</c>). SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniQuipuChase : MonoBehaviour
    {
        [Tooltip("Segundos desde que empieza la pelea hasta que el Rastreador sale corriendo.")]
        [SerializeField] private float startDelay = 1.2f;
        [Tooltip("Velocidad de huida (Yari corre a ~4 m/s).")]
        [SerializeField] private float fleeSpeed = 3.1f;
        [Tooltip("Metros desde donde empezó a huir a partir de los cuales escapa.")]
        [SerializeField] private float escapeDistance = 60f;
        [Tooltip("Segundos de huida a partir de los cuales escapa aunque no haya llegado tan lejos.")]
        [SerializeField] private float maxSeconds = 30f;

        /// <summary>El Rastreador está huyendo con el quipu.</summary>
        public static bool Fleeing { get; private set; }
        /// <summary>Metros que le faltan al Rastreador para escapar.</summary>
        public static float MetersToEscape { get; private set; }

        private static AyniQuipuChase instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Fleeing = false;
            MetersToEscape = 0f;
            instance = null;
        }

        private AyniEncounterSite site;
        private EnemyController runner;     // huyendo
        private EnemyController caught;     // alcanzado: hay que derrotarlo
        private YariCombatController yari;
        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIndex;
        private Vector3 origin;
        private float startedAt, verticalSpeed;
        private GameObject quipu;
        private bool started, resolved;

        private void OnEnable()
        {
            instance = this;
            AyniEncounterSite.OnPlayerEntered += HandleEntered;
        }

        private void OnDisable()
        {
            AyniEncounterSite.OnPlayerEntered -= HandleEntered;
            if (instance == this) instance = null;
            Fleeing = false;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
        }

        private void HandleEntered(AyniEncounterSite entered)
        {
            if (started || entered == null || entered.Order != 1) return;
            site = entered;
            started = true;
            StartCoroutine(BeginLater());
        }

        private IEnumerator BeginLater()
        {
            yield return new WaitForSeconds(startDelay);
            while (AyniGameState.InputLocked || AyniGameState.CinematicPlaying) yield return null; // el tutorial de la primera pelea
            Begin();
        }

        /// <summary>Elige al Rastreador más alejado de Yari (que no sea el del segundo aliento) y lo echa a correr.</summary>
        private void Begin()
        {
            if (site == null || yari == null) return;
            EnemyController best = null;
            float bestScore = float.MinValue;
            foreach (GameObject go in site.Rivals)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var e = go.GetComponent<EnemyController>();
                if (e == null || e.IsDead || e.SecondWinds > 0) continue;
                float score = (e.transform.position - yari.transform.position).sqrMagnitude;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            if (best == null) return;

            runner = best;
            runner.ExternalControl = true;
            runner.OnDamaged += HandleRunnerHit;
            origin = runner.transform.position;
            startedAt = Time.time;

            // Por la salida de la kancha y luego por el camino, hacia la siguiente
            path.Clear();
            if (site.Exit != null) path.Add(site.Exit.position);
            AyniEncounterSite next = null;
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == site.Order + 1) next = s;
            if (next != null) path.Add(next.Entry != null ? next.Entry.position : next.transform.position);
            if (path.Count == 0) path.Add(origin + site.transform.forward * 80f);
            pathIndex = 0;

            quipu = BuildQuipu(runner);
            Fleeing = true;
            AyniAudio.Play2D("titulo_golpe", 0.5f, 1.2f);
            AyniScreenFX.Caption("¡Un Rastreador huye con el quipu de los chasquis para avisar a Amaru! Alcánzalo y golpéalo.", 4f);
            Debug.Log($"[Ayni Quipu] {runner.name} huye con el quipu");
        }

        private void HandleRunnerHit(EnemyController who)
        {
            if (who != runner || !Fleeing) return;
            // Alcanzado: deja de huir y pelea; hay que derrotarlo para recuperar el quipu
            runner.OnDamaged -= HandleRunnerHit;
            runner.ExternalControl = false;
            runner.HuntRange = 40f;
            caught = runner;
            runner = null;
            Fleeing = false;
            AyniScreenFX.Caption("¡Lo alcanzaste! Derrótalo para recuperar el quipu.", 2.8f);
            Debug.Log("[Ayni Quipu] Rastreador alcanzado");
        }

        private void Update()
        {
            if (resolved) return;
            if (caught != null && caught.IsDead)
            {
                Recover();
                return;
            }
            if (!Fleeing || runner == null) return;
            if (runner.IsDead)
            {
                Recover();
                return;
            }

            // Corre por la ruta pegado al suelo, con la animación de carrera
            Transform t = runner.transform;
            Vector3 target = path[Mathf.Min(pathIndex, path.Count - 1)];
            Vector3 to = target - t.position;
            to.y = 0f;
            if (to.magnitude < 1.5f && pathIndex < path.Count - 1) pathIndex++;
            Vector3 dir = Unstick(t.position, to.sqrMagnitude > 0.01f ? to.normalized : t.forward);
            CharacterController mover = runner.Mover;
            verticalSpeed = mover != null && mover.isGrounded ? -2f : verticalSpeed - 18f * Time.deltaTime;
            Vector3 step = dir * (fleeSpeed * Time.deltaTime) + Vector3.up * (verticalSpeed * Time.deltaTime);
            if (mover != null && mover.enabled) mover.Move(step);
            else t.position += step;
            t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
            if (runner.Animator != null && HasFloat(runner.Animator, "Speed")) runner.Animator.SetFloat("Speed", 1f);

            Vector3 flat = t.position - origin;
            flat.y = 0f;
            MetersToEscape = Mathf.Max(0f, escapeDistance - flat.magnitude);
            if (MetersToEscape <= 0f || Time.time - startedAt > maxSeconds) Escape();
        }

        private Vector3 lastCheckPos;
        private float lastCheckTime, sidestepUntil, sidestepSign = 1f;

        /// <summary>
        /// Si se queda atascado (en una empalizada, un peñasco o un tronco), se aparta de lado un momento y vuelve a
        /// intentarlo; cada vez prueba por el otro lado. Así acaba encontrando el paso, como haría alguien con prisa.
        /// </summary>
        private Vector3 Unstick(Vector3 position, Vector3 wanted)
        {
            if (Time.time < sidestepUntil)
            {
                return (Vector3.Cross(Vector3.up, wanted) * sidestepSign + wanted * 0.25f).normalized;
            }
            if (Time.time - lastCheckTime >= 0.5f)
            {
                Vector3 moved = position - lastCheckPos;
                moved.y = 0f;
                if (lastCheckTime > 0f && moved.magnitude < fleeSpeed * 0.5f * 0.3f)
                {
                    sidestepSign = -sidestepSign;
                    sidestepUntil = Time.time + 0.9f;
                }
                lastCheckPos = position;
                lastCheckTime = Time.time;
            }
            return wanted;
        }

        private void Recover()
        {
            resolved = true;
            Fleeing = false;
            if (quipu != null) Destroy(quipu);
            AyniPathRewards.Grant(AyniPathRewards.Reward.Quipu);
            AyniAudio.Play2D("illa_brillo", 0.55f, 1.15f);
            AyniScreenFX.Caption("Recuperas el quipu de los chasquis. Sus nudos dicen: «El Cazador se tiñe de verde antes de soplar». Ya sabes leer sus dardos.", 5f);
        }

        private void Escape()
        {
            resolved = true;
            Fleeing = false;
            AyniPathRewards.Grant(AyniPathRewards.Reward.AmaruWarned);
            AyniAudio.Play2D("titulo_golpe", 0.6f, 0.8f);
            AyniScreenFX.Caption("El Rastreador escapó con el quipu: Amaru sabrá que vienes y te tenderá trampas.", 4.5f);
            if (runner != null)
            {
                runner.OnDamaged -= HandleRunnerHit;
                Destroy(runner.gameObject); // fuera de la pelea: ya no cuenta para liberar la kancha
            }
            runner = null;
        }

        /// <summary>Quipu de cuerdas de colores colgando de la cintura del Rastreador.</summary>
        private static GameObject BuildQuipu(EnemyController who)
        {
            // Colgado del cuerpo a la altura de la cadera (se mide en el hueso, pero cuelga de la raíz para quedar vertical)
            Transform anchor = who.transform;
            Vector3 local = new Vector3(0.12f, 0.95f, -0.12f);
            if (who.Animator != null && who.Animator.isHuman)
            {
                Transform hips = who.Animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) local = anchor.InverseTransformPoint(hips.position) + new Vector3(0.1f, 0f, -0.12f);
            }
            var root = new GameObject("Quipu");
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = local;
            root.transform.localRotation = Quaternion.identity;
            // El quipu no hereda la escala del esqueleto
            Vector3 s = anchor.lossyScale;
            root.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, s.x), 1f / Mathf.Max(0.001f, s.y), 1f / Mathf.Max(0.001f, s.z));

            Color[] colors =
            {
                new Color(0.75f, 0.1f, 0.1f), new Color(0.95f, 0.8f, 0.2f), new Color(0.92f, 0.9f, 0.85f),
                new Color(0.15f, 0.5f, 0.3f), new Color(0.1f, 0.1f, 0.1f), new Color(0.2f, 0.35f, 0.7f)
            };
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Cuerda_Madre", root.transform, AyniFaunaPrimitives.Mat(new Color(0.62f, 0.52f, 0.36f)),
                new Vector3(0.16f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.025f, 0.16f, 0.025f));
            for (int i = 0; i < colors.Length; i++)
            {
                float x = 0.04f + i * 0.045f;
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Cuerda", root.transform, AyniFaunaPrimitives.Mat(colors[i]),
                    new Vector3(x, -0.13f, 0f), Vector3.zero, new Vector3(0.016f, 0.13f, 0.016f));
                AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Nudo", root.transform, AyniFaunaPrimitives.Mat(colors[i]),
                    new Vector3(x, -0.08f - (i % 3) * 0.05f, 0f), Vector3.zero, Vector3.one * 0.03f);
            }
            return root;
        }

        private static bool HasFloat(Animator anim, string name)
        {
            foreach (AnimatorControllerParameter p in anim.parameters)
            {
                if (p.name == name && p.type == AnimatorControllerParameterType.Float) return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!Fleeing || runner == null) return;
            if (AyniGameState.CinematicPlaying || AyniGameState.InputLocked || Ayni.Story.AyniJudgment.Active || AyniDeathAging.Active) return;
            AyniHoldPrompt.Marker(runner.transform.position + Vector3.up * 2.2f, new Color(0.95f, 0.75f, 0.25f), "Quipu");
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Hace escapar ya al Rastreador (o, si no ha empezado, marca a Amaru como avisado).</summary>
        public static void TestEscape()
        {
            if (instance != null && Fleeing) instance.Escape();
            else AyniPathRewards.Grant(AyniPathRewards.Reward.AmaruWarned);
        }

        /// <summary>Da el quipu por recuperado.</summary>
        public static void TestRecover()
        {
            if (instance != null && !instance.resolved) instance.Recover();
            else AyniPathRewards.Grant(AyniPathRewards.Reward.Quipu);
        }

        public static void Report()
        {
            string who = instance != null && instance.runner != null ? instance.runner.name : "-";
            Debug.Log($"[Ayni Quipu] Huye: {Fleeing} ({who}) · le faltan {MetersToEscape:F0} m · quipu {AyniPathRewards.QuipuRecovered} · Amaru avisado {AyniPathRewards.AmaruWarned}");
        }
    }
}
