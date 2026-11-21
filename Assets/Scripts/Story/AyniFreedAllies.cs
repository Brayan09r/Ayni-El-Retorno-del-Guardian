using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;
using Ayni.World;

namespace Ayni.Story
{
    /// <summary>
    /// Ayni con la fauna: los animales que Yari liberó en la Portada del Cazador (<see cref="AyniCaptiveCages"/>) le
    /// devuelven el favor en el duelo contra Amaru.
    ///  - La puma madre: cuando Amaru empieza su segunda fase y llama a sus cazadores, ella baja del cerro, se le lanza
    ///    encima y le rompe la postura (un momento para castigarlo); luego vuelve al monte.
    ///  - El cóndor: planea sobre la plaza y, cada vez que Amaru prepara una ráfaga de dardos, avisa con un bufido y
    ///    unos aletazos. Cada dardo tarda más en salir (<see cref="AmaruHunter.ExtraTelegraph"/>).
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniFreedAllies : MonoBehaviour
    {
        [Tooltip("Segundos de aviso extra por dardo con el cóndor liberado.")]
        [SerializeField] private float condorExtraWarning = 0.35f;
        [Tooltip("Segundos tras empezar la segunda fase de Amaru (y su escena) antes de que llegue la puma.")]
        [SerializeField] private float pumaDelay = 3.5f;
        [SerializeField] private float pumaSpeed = 9f;

        /// <summary>La puma madre ya acudió (para las sondas de prueba y la tarjeta final).</summary>
        public static bool PumaHelped { get; private set; }
        /// <summary>Veces que el cóndor avisó de una ráfaga.</summary>
        public static int CondorWarnings { get; private set; }

        private static AyniFreedAllies instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PumaHelped = false;
            CondorWarnings = 0;
            instance = null;
        }

        private EnemyController boss;
        private YariCombatController yari;
        private bool pumaComing;

        private void OnEnable()
        {
            instance = this;
            AyniCaptiveCages.OnFreed += HandleFreed;
            AmaruHunter.OnVolleyStarted += HandleVolley;
            EnemyController.OnPhaseStarted += HandlePhase;
        }

        private void OnDisable()
        {
            AyniCaptiveCages.OnFreed -= HandleFreed;
            AmaruHunter.OnVolleyStarted -= HandleVolley;
            EnemyController.OnPhaseStarted -= HandlePhase;
            if (instance == this) instance = null;
        }

        private void Start()
        {
            foreach (EnemyController e in EnemyController.All)
            {
                if (e != null && e.IsBoss && (boss == null || e.CharacterName.Contains("Amaru"))) boss = e;
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            AmaruHunter.ExtraTelegraph = AyniCaptiveCages.CondorFreed ? condorExtraWarning : 0f;
        }

        private void HandleFreed(AyniCaptive kind)
        {
            if (kind == AyniCaptive.Condor) AmaruHunter.ExtraTelegraph = condorExtraWarning;
        }

        // ───────────────────────── Cóndor ─────────────────────────

        private void HandleVolley(EnemyController who)
        {
            if (!AyniCaptiveCages.CondorFreed || who == null || who != boss) return;
            CondorWarnings++;
            AyniAudio.Play2D("condor_aviso", 0.75f);
            if (CondorWarnings == 1)
            {
                AyniScreenFX.Caption("El cóndor te avisa desde lo alto: tienes más tiempo para esquivar los dardos.", 3.2f);
            }
        }

        // ───────────────────────── Puma madre ─────────────────────────

        private void HandlePhase(EnemyController who, int phase)
        {
            if (who != boss || phase < 2) return;
            TrySendPuma();
        }

        private void Update()
        {
            // Jefe de una sola fase: la puma llega cuando le queda poco más de la mitad de la vida
            if (boss == null || boss.PhaseCount > 1 || boss.Dormant || boss.IsDead) return;
            if (boss.HealthRatio < 0.6f) TrySendPuma();
        }

        private void TrySendPuma()
        {
            if (pumaComing || PumaHelped || !AyniCaptiveCages.PumaFreed || AyniCaptiveCages.PumaTemplate == null) return;
            pumaComing = true;
            StartCoroutine(PumaMother());
        }

        private bool Busy()
        {
            return boss.InPhaseTransition || AyniGameState.CinematicPlaying || AyniJudgment.Active || AyniDeathAging.Active ||
                   yari == null || yari.IsDead || yari.CinematicControl;
        }

        private IEnumerator PumaMother()
        {
            // Que termine la escena del cambio de fase (y la llegada de los cazadores)
            float waited = 0f;
            while (Busy() && waited < 25f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(pumaDelay);
            while ((Busy() || boss.Structure.IsBroken) && waited < 40f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (boss == null || boss.IsDead) yield break;

            if (!FindSpawn(out Vector3 spawn))
            {
                Debug.Log("[Ayni Aliados] La puma madre no encontró por dónde bajar");
                yield break;
            }

            GameObject puma = AyniVisualClone.Create(AyniCaptiveCages.PumaTemplate, "Puma_Madre");
            if (puma == null) yield break;
            puma.transform.position = spawn;
            puma.transform.rotation = Quaternion.LookRotation(Flat(boss.transform.position - spawn));
            puma.SetActive(true);
            Animator anim = AyniVisualClone.AnimatorOf(puma);
            SetSpeed(anim, 1f);
            if (!AyniVisualClone.Play(anim, "Run", 0.1f)) AyniVisualClone.Play(anim, "Move", 0.1f);

            AyniAudio.Play("puma_rugido", spawn, 1f);
            AyniAudio.Play2D("puma_rugido", 0.55f);
            AyniScreenFX.Caption("¡La puma madre baja del cerro! Devuelve el Ayni a quien liberó a su cría.", 3.5f);
            Debug.Log($"[Ayni Aliados] La puma madre baja en {spawn}");

            // Corre hacia Amaru (sigue su posición) hasta tenerlo a un salto
            Transform t = puma.transform;
            float run = 0f;
            while (run < 6f && boss != null && !boss.IsDead)
            {
                Vector3 to = Flat(boss.transform.position - t.position);
                float dist = to.magnitude;
                if (dist < 2.6f) break;
                Vector3 step = to.normalized * Mathf.Min(dist, pumaSpeed * Time.deltaTime);
                Vector3 pos = t.position + step;
                pos.y = Ground(pos, t.position.y);
                t.position = pos;
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(to), 720f * Time.deltaTime);
                run += Time.deltaTime;
                yield return null;
            }
            if (boss == null || boss.IsDead) { Destroy(puma); yield break; }

            // Salto sobre él
            if (!AyniVisualClone.Play(anim, "Atk_Overhand", 0.05f)) AyniVisualClone.Play(anim, "Jump", 0.05f);
            Vector3 from = t.position;
            Vector3 land = boss.transform.position - Flat(boss.transform.position - from).normalized * 0.9f;
            land.y = Ground(land, from.y);
            for (float u = 0f; u < 1f; u += Time.deltaTime / 0.32f)
            {
                t.position = Vector3.Lerp(from, land, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 1.1f);
                yield return null;
            }
            t.position = land;

            if (!boss.IsDead)
            {
                Vector3 hit = boss.transform.position + Vector3.up * 1.3f;
                boss.TakeHit(18f, boss.Structure.MaxStructure * 1.2f, from, true, HitReaction.Knockdown, 1.4f);
                CombatFeedback.HeavyHit(hit, Flat(boss.transform.position - from).normalized);
                CombatFeedback.Shake(0.12f, 0.35f);
                AyniAudio.Play("puma_rugido", hit, 0.9f, 0.05f, 1.15f);
                PumaHelped = true;
                Debug.Log($"[Ayni Aliados] La puma madre derriba a {boss.CharacterName}: postura rota {boss.Structure.IsBroken}");
            }

            // Vuelve al monte
            yield return new WaitForSeconds(0.35f);
            if (!AyniVisualClone.Play(anim, "Run", 0.15f)) AyniVisualClone.Play(anim, "Move", 0.15f);
            Vector3 away = Flat(spawn - t.position).normalized;
            var path = new List<Vector3> { t.position + away * 8f, spawn, spawn + away * 25f };
            AyniFreedRunner.Run(puma, path, pumaSpeed, null, null);
        }

        /// <summary>
        /// Sitio a unos metros de Amaru, con suelo a su altura y camino libre hasta él; mejor si se ve en pantalla (para
        /// que el jugador la vea llegar) y si no está detrás de Yari.
        /// </summary>
        private bool FindSpawn(out Vector3 spawn)
        {
            spawn = Vector3.zero;
            Vector3 b = boss.transform.position;
            Vector3 toYari = yari != null ? Flat(yari.transform.position - b).normalized : boss.transform.forward;
            if (toYari.sqrMagnitude < 0.01f) toYari = Vector3.forward;
            Camera cam = Camera.main;
            float best = float.MinValue;
            for (float a = 0f; a < 360f; a += 20f)
            {
                for (float d = 16f; d >= 9f; d -= 3.5f)
                {
                    Vector3 p = b + Quaternion.Euler(0f, a, 0f) * toYari * d;
                    if (!Physics.Raycast(p + Vector3.up * 12f, Vector3.down, out RaycastHit hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(hit.point.y - b.y) > 2.5f || hit.normal.y < 0.7f) continue;
                    // Que pueda correr en línea recta hasta él (nada en medio a la altura del pecho)
                    if (Physics.Linecast(hit.point + Vector3.up * 0.7f, b + Vector3.up * 0.7f, out RaycastHit block, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        !block.transform.IsChildOf(boss.transform)) continue;

                    float score = d / 16f;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 40f) score -= 3f; // no por la espalda de Yari
                    if (cam != null)
                    {
                        Vector3 v = cam.WorldToViewportPoint(hit.point + Vector3.up * 0.6f);
                        if (v.z > 0f && v.x > 0.08f && v.x < 0.92f && v.y > 0.1f && v.y < 0.9f) score += 5f;
                    }
                    if (score > best)
                    {
                        best = score;
                        spawn = hit.point;
                    }
                }
            }
            return best > float.MinValue;
        }

        private static float Ground(Vector3 p, float fallback)
        {
            return Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? Mathf.Lerp(fallback, hit.point.y, 0.5f) : fallback;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static void SetSpeed(Animator anim, float value)
        {
            if (anim == null) return;
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.name == "Speed" && param.type == AnimatorControllerParameterType.Float) anim.SetFloat("Speed", value);
            }
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Manda a la puma madre ya (si la cría está libre). En Play: <c>call Ayni.Story.AyniFreedAllies.TestPuma</c>.</summary>
        public static void TestPuma()
        {
            if (instance == null) { Debug.LogWarning("[Ayni Aliados] No hay AyniFreedAllies (¿estás en Play?)"); return; }
            instance.pumaComing = false;
            PumaHelped = false;
            instance.TrySendPuma();
        }

        public static void Report()
        {
            Debug.Log($"[Ayni Aliados] Puma ayudó: {PumaHelped} · avisos del cóndor: {CondorWarnings} · aviso extra por dardo: {AmaruHunter.ExtraTelegraph:F2} s");
        }
    }
}
