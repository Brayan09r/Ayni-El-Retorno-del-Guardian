using System.Collections;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;

namespace Ayni.Story
{
    /// <summary>
    /// Presentación del jefe: Amaru el Cazador espera en la cabeza del puente colgante (sobre el estribo de piedra del
    /// lado de la plaza), cerrando el paso al puente por el que Yari cayó en el prólogo y que lleva a casa. Mientras
    /// espera camina de un lado a otro del estribo como una fiera enjaulada y se detiene a mirar el camino; cuando ve
    /// llegar a Yari se planta, lo encara y le habla. Al acercarse: plano por encima del hombro de Yari con Amaru en el
    /// puente, contrapicado de Amaru rugiendo con su título y, por fin, baja del estribo hacia Yari hablándole y empieza
    /// el combate. Sus frases salen como subtítulos con su nombre.
    /// SifuCombatHUD lo añade solo. Se salta con Tab / Esc / Menu.
    /// </summary>
    public class AyniBossIntro : MonoBehaviour
    {
        [Tooltip("Distancia (m) a la que Yari dispara la presentación.")]
        [SerializeField] private float triggerDistance = 26f;
        [Tooltip("Distancia a la que queda Yari de Amaru durante la presentación.")]
        [SerializeField] private float yariDistance = 10f;

        /// <summary>La presentación está en pantalla.</summary>
        public static bool Running { get; private set; }

        private EnemyController boss;
        private Animator bossAnim;
        private CharacterController bossMover;
        private YariCombatController yari;
        private Camera cam;
        private ThirdPersonSifuCamera gameplayCamera;
        private Vector3 head;       // pie de Amaru sobre el estribo
        private Vector3 toPlaza;    // horizontal, del puente hacia la plaza
        private bool ready, done, skip;

        // Espera en el puente: pasea de un lado a otro del estribo y se para a mirar el camino
        private static readonly int WalkState = Animator.StringToHash("Ambient_Walk");
        private Vector3 paceTarget;
        private float paceTimer = 1.5f, paceVertical, spottedAt;
        private bool roarDone;
        private bool pacing, spotted;

        /// <summary>Frase de Amaru como subtítulo con su nombre.</summary>
        public static void Say(string line, float seconds) => AyniScreenFX.Caption("AMARU: «" + line + "»", seconds);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Running = false;
        }

        private void Start()
        {
            foreach (EnemyController e in EnemyController.All)
            {
                if (e != null && e.IsBoss && (boss == null || e.CharacterName.Contains("Amaru"))) boss = e;
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            yari = player != null ? player.GetComponent<YariCombatController>() : null;
            if (boss == null || yari == null || !FindBridgeHead()) return;

            bossAnim = boss.Animator;
            bossMover = boss.GetComponent<CharacterController>();
            PlaceBoss(head, toPlaza);
            // Hasta la presentación lo mueve la espera (la IA no lo lanza contra Yari ni la música lo cuenta como pelea)
            boss.ExternalControl = true;
            boss.Dormant = true;
            paceTarget = head;
            ready = true;
        }

        /// <summary>
        /// Extremo del puente más cercano a la plaza del jefe, y sobre su estribo (la única parte con colisión: los
        /// tablones no la tienen) el punto donde espera Amaru.
        /// </summary>
        private bool FindBridgeHead()
        {
            var bridge = GameObject.Find("Puente_Colgante");
            Transform planks = bridge != null ? bridge.transform.Find("Tablones") : null;
            if (planks == null || planks.childCount < 3) return false;

            Vector3 a = planks.GetChild(0).position, b = planks.GetChild(planks.childCount - 1).position;
            Vector3 plaza = boss.transform.position;
            Vector3 end = (a - plaza).sqrMagnitude < (b - plaza).sqrMagnitude ? a : b;
            Vector3 other = end == a ? b : a;
            toPlaza = end - other;
            toPlaza.y = 0f;
            toPlaza.Normalize();

            // El estribo está 0.9 m por fuera del último tablón; se busca su cara de arriba
            Vector3 spot = end + toPlaza * 1.1f;
            if (Physics.Raycast(spot + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers,
                                QueryTriggerInteraction.Ignore))
            {
                spot = hit.point;
            }
            head = spot;
            return true;
        }

        private void PlaceBoss(Vector3 position, Vector3 facing)
        {
            bool wasEnabled = bossMover != null && bossMover.enabled;
            if (bossMover != null) bossMover.enabled = false;
            boss.transform.SetPositionAndRotation(position + Vector3.up * 0.05f, Quaternion.LookRotation(facing));
            if (bossMover != null) bossMover.enabled = wasEnabled;
        }

        private void Update()
        {
            // Primero la escena en curso: "done" ya es true mientras se reproduce, y si se mirara antes no se podría saltar
            if (Running)
            {
                if (AyniInput.Down(AyniInput.Action.Skip)) skip = true;
                return;
            }
            if (!ready || done) return;
            if (boss.IsDead || AyniGameState.CinematicPlaying) return;
            Vector3 d = yari.transform.position - boss.transform.position;
            d.y = 0f;
            UpdateWaiting(d);
            if (AyniGameState.InputLocked || yari.IsDead) return;
            if (d.magnitude <= triggerDistance) StartCoroutine(Intro());
        }

        /// <summary>
        /// Antes de la presentación: Amaru pasea por el estribo (poco más de un metro a cada lado, mirando al camino en
        /// las pausas). Cuando ve llegar a Yari por el camino se planta, lo encara y le habla.
        /// </summary>
        private void UpdateWaiting(Vector3 toYari)
        {
            if (!spotted && toYari.magnitude < 48f)
            {
                spotted = true;
                spottedAt = Time.time;
                StopPacing();
                Say("Huelo la Illa en ti, Yari. Sayri dijo que volverías.", 4f);
                AyniVoice.Reply(this, "YARI: «Y yo huelo en ti el humo de mi bosque, Amaru.»", 4f);
                if (bossAnim != null && bossAnim.HasState(0, Animator.StringToHash("Phase_Roar"))) bossAnim.CrossFadeInFixedTime("Phase_Roar", 0.2f);
            }
            if (spotted)
            {
                // Acaba el rugido y queda en guardia, siguiéndolo con la mirada
                if (!roarDone && Time.time - spottedAt > 2.6f)
                {
                    roarDone = true;
                    if (bossAnim != null && bossAnim.HasState(0, Animator.StringToHash("Idle"))) bossAnim.CrossFadeInFixedTime("Idle", 0.4f);
                }
                if (toYari.sqrMagnitude > 0.01f)
                {
                    boss.transform.rotation = Quaternion.RotateTowards(boss.transform.rotation, Quaternion.LookRotation(toYari), 120f * Time.deltaTime);
                }
                return;
            }

            Vector3 side = Vector3.Cross(Vector3.up, toPlaza);
            if (!pacing)
            {
                paceTimer -= Time.deltaTime;
                // En la pausa mira hacia el camino, por donde llegará Yari
                boss.transform.rotation = Quaternion.RotateTowards(boss.transform.rotation, Quaternion.LookRotation(toPlaza), 90f * Time.deltaTime);
                if (paceTimer > 0f) return;
                // Siguiente punto: al otro lado del estribo (si hay suelo de piedra debajo)
                float offset = (paceTarget - head).sqrMagnitude < 0.01f ? (Random.value < 0.5f ? -1.4f : 1.4f) : 0f;
                Vector3 next = head + side * offset;
                if (Physics.Raycast(next + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    Mathf.Abs(hit.point.y - head.y) < 0.6f)
                {
                    paceTarget = next;
                    pacing = true;
                    if (bossAnim != null && bossAnim.HasState(0, WalkState)) bossAnim.CrossFadeInFixedTime(WalkState, 0.3f);
                }
                else
                {
                    paceTimer = 2f;
                }
                return;
            }

            Vector3 to = paceTarget - boss.transform.position;
            to.y = 0f;
            if (to.magnitude < 0.12f)
            {
                StopPacing();
                return;
            }
            boss.transform.rotation = Quaternion.RotateTowards(boss.transform.rotation, Quaternion.LookRotation(to), 160f * Time.deltaTime);
            paceVertical = bossMover != null && bossMover.isGrounded ? -2f : paceVertical - 18f * Time.deltaTime;
            Vector3 step = to.normalized * Mathf.Min(1.0f * Time.deltaTime, to.magnitude) + Vector3.up * (paceVertical * Time.deltaTime);
            if (bossMover != null && bossMover.enabled) bossMover.Move(step);
            else boss.transform.position += step;
        }

        private void StopPacing()
        {
            pacing = false;
            paceTimer = Random.Range(2f, 3.5f);
            if (bossAnim != null && bossAnim.HasState(0, Animator.StringToHash("Idle"))) bossAnim.CrossFadeInFixedTime("Idle", 0.3f);
        }

        private IEnumerator Intro()
        {
            done = true;
            Running = true;
            cam = Camera.main;
            gameplayCamera = cam != null ? cam.GetComponent<ThirdPersonSifuCamera>() : null;

            // La IA de Amaru se congela pero su Animator sigue (con CinematicPlaying los rivales fuerzan Speed a 0
            // y no podría caminar); el HUD se aparta mirando Running
            boss.ExternalControl = true;
            AyniGameState.LockInput();
            if (gameplayCamera != null) gameplayCamera.enabled = false;
            AyniScreenFX.Letterbox(true);
            AyniScreenFX.Hint(AyniInput.UsingGamepad ? "[Menu] Saltar escena" : "[Tab / Esc] Saltar escena");
            AyniAudio.Play2D("juicio", 0.9f);

            // Yari se planta a unos metros, en guardia, mirando al puente
            yari.HoldForCinematic(boss.transform.position, yariDistance);
            PlaceBoss(head, toPlaza);
            if (bossAnim != null && bossAnim.HasState(0, Animator.StringToHash("Idle"))) bossAnim.CrossFadeInFixedTime("Idle", 0.2f);

            Vector3 yariPos = yari.transform.position;
            Vector3 toBoss = head - yariPos;
            toBoss.y = 0f;
            Vector3 dir = toBoss.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            // Plano 1: por encima del hombro de Yari; al fondo, Amaru en la cabeza del puente y la garganta detrás
            Say("Ningún guardián vuelve a cruzar este puente.", 3.0f);
            yield return Shot(3.2f, u =>
            {
                Vector3 pos = yariPos - dir * Mathf.Lerp(2.6f, 2.1f, u) + right * 0.75f + Vector3.up * 1.85f;
                return (pos, head + Vector3.up * 1.2f);
            });

            // Plano 2: contrapicado de Amaru, que ruge; su título
            if (!skip)
            {
                if (bossAnim != null && bossAnim.HasState(0, Animator.StringToHash("Phase_Roar")))
                {
                    bossAnim.CrossFadeInFixedTime("Phase_Roar", 0.12f);
                }
                AyniAudio.Play2D("titulo_golpe", 0.85f);
                AyniAudio.Play2D("postura_rota", 0.6f, 0.8f);
                AyniScreenFX.Title("AMARU EL CAZADOR", "Primer teniente de Sayri · Te cierra el camino a casa", 3.2f);
                Say("Quemé tu bosque y cacé a sus animales. Ahora te toca a ti.", 3.3f);
                Vector3 side = Vector3.Cross(Vector3.up, toPlaza);
                yield return Shot(3.4f, u =>
                {
                    Vector3 pos = head + toPlaza * Mathf.Lerp(3.2f, 2.6f, u) + side * 1.1f + Vector3.up * 0.75f;
                    return (pos, head + Vector3.up * 1.6f);
                });
            }

            // Plano 3: baja del estribo hacia Yari mientras la cámara se abre
            if (!skip)
            {
                Vector3 from = head;
                Vector3 to = head + toPlaza * 4f;
                if (Physics.Raycast(to + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    to = hit.point;
                }
                if (bossAnim != null) bossAnim.SetFloat("Speed", 0.5f);
                bool walk = bossAnim != null && bossAnim.HasState(0, WalkState);
                if (bossAnim != null && (walk || bossAnim.HasState(0, Animator.StringToHash("Move"))))
                {
                    bossAnim.CrossFadeInFixedTime(walk ? WalkState : Animator.StringToHash("Move"), 0.2f);
                }
                Say("Ven, viejo guardián. Que la Pachamama te vea caer.", 2.2f);
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                yield return Shot(2.0f, u =>
                {
                    PlaceBoss(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, u)), toPlaza);
                    Vector3 mid = Vector3.Lerp(yariPos, boss.transform.position, 0.5f);
                    Vector3 pos = mid + side * Mathf.Lerp(6.5f, 7.5f, u) + Vector3.up * 2.4f;
                    return (pos, mid + Vector3.up * 1.1f);
                });
                if (bossAnim != null) bossAnim.SetFloat("Speed", 0f);
            }

            Finish();
        }

        /// <summary>Plano de cámara: la función devuelve posición y punto de mira para cada instante (0..1).</summary>
        private IEnumerator Shot(float seconds, System.Func<float, (Vector3 pos, Vector3 look)> path)
        {
            float t = 0f;
            while (t < seconds && !skip)
            {
                var (pos, look) = path(Mathf.Clamp01(t / seconds));
                if (cam != null)
                {
                    cam.transform.position = pos;
                    Vector3 d = look - pos;
                    if (d.sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(d);
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void Finish()
        {
            // Si se saltó la escena, Amaru queda ya al pie del puente, listo para pelear
            if (skip) PlaceBoss(head + toPlaza * 4f, toPlaza);
            AyniScreenFX.ClearAll();
            AyniScreenFX.Letterbox(false);
            if (yari != null) yari.ReleaseFromCinematic();
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
                gameplayCamera.SnapBehindTarget();
            }
            if (boss != null)
            {
                boss.ExternalControl = false;
                boss.Dormant = false;
                if (bossAnim != null && bossAnim.HasState(0, Animator.StringToHash("Idle"))) bossAnim.CrossFadeInFixedTime("Idle", 0.2f);
            }
            AyniGameState.UnlockInput();
            Running = false;
        }

        private void OnDisable()
        {
            if (Running) Finish();
        }
    }
}
