using System.Collections;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;

namespace Ayni.Story
{
    /// <summary>
    /// Presentación del jefe: Amaru el Cazador espera de pie en la cabeza del puente colgante (sobre el estribo de
    /// piedra del lado de la plaza), cerrando el paso al puente por el que Yari cayó en el prólogo y que lleva a casa.
    /// Cuando Yari se acerca por primera vez: plano por encima de su hombro con Amaru en el puente, contrapicado de
    /// Amaru rugiendo con su título y, por fin, Amaru baja del estribo hacia Yari y empieza el combate.
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
            if (!ready || done) return;
            if (Running)
            {
                if (AyniInput.Down(AyniInput.Action.Skip)) skip = true;
                return;
            }
            if (AyniGameState.CinematicPlaying || AyniGameState.InputLocked || yari.IsDead || boss.IsDead) return;

            Vector3 d = yari.transform.position - boss.transform.position;
            d.y = 0f;
            if (d.magnitude <= triggerDistance) StartCoroutine(Intro());
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
            AyniScreenFX.Caption("En la cabeza del puente, alguien le cierra el paso.", 3.0f);
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
                if (bossAnim != null && bossAnim.HasState(0, Animator.StringToHash("Move"))) bossAnim.CrossFadeInFixedTime("Move", 0.2f);
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
            if (boss != null) boss.ExternalControl = false;
            AyniGameState.UnlockInput();
            Running = false;
        }

        private void OnDisable()
        {
            if (Running) Finish();
        }
    }
}
