using System.Collections;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.UI;
using Ayni.World;

namespace Ayni.Story
{
    /// <summary>
    /// Amaru habla durante el duelo (subtítulos con su nombre, ver <see cref="AyniBossIntro.Say"/>): provoca a Yari de
    /// vez en cuando mientras pelean de cerca, grita al pasar a su segunda fase y llama a dos cazadores que llegan por
    /// la espalda de Yari, se burla cuando Yari cae y la Illa le cobra años, y flaquea cuando le queda poca vida. Cada
    /// frase espera su turno para no pisarse con otra.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniBossBarks : MonoBehaviour
    {
        private static readonly string[] Taunts =
        {
            "¡Lento, guardián! La Illa no te devolvió los reflejos.",
            "El Antisuyo ya es ceniza. Tú eres lo último que queda.",
            "Sayri te arrojó al abismo una vez. Yo terminaré su trabajo.",
            "Cada golpe que fallas, tu aldea lo paga.",
            "¿Eso es el Ayni? Golpes de anciano.",
        };

        [SerializeField] private float tauntMin = 16f;
        [SerializeField] private float tauntMax = 26f;

        private EnemyController boss;
        private Transform yari;
        private float nextTaunt, quietUntil;
        private int lastTaunt = -1;
        private bool wasAging, weakSaid, killaSaid;

        private void OnEnable() => EnemyController.OnPhaseTransitionStarted += HandlePhase;
        private void OnDisable() => EnemyController.OnPhaseTransitionStarted -= HandlePhase;

        private void Start()
        {
            foreach (EnemyController e in EnemyController.All)
            {
                if (e != null && e.IsBoss && e.CharacterName.Contains("Amaru")) boss = e;
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.transform;
            nextTaunt = Time.time + tauntMax;
        }

        private void HandlePhase(EnemyController who)
        {
            if (who != boss) return;
            Say("¡Cazadores, a mí! Que el guardián no salga vivo de esta plaza.", 3.2f, true);
            StartCoroutine(CallHunters());
        }

        /// <summary>
        /// Dos cazadores (como los de la Portada del Cazador) llegan corriendo por la espalda de Yari, un segundo después
        /// del grito (y fuera del golpe que lo provocó: crear rivales mientras se recorre la lista de enemigos la rompe).
        /// </summary>
        private IEnumerator CallHunters()
        {
            yield return new WaitForSeconds(1.2f);
            AyniEncounterSite kennel = null;
            foreach (AyniEncounterSite site in AyniEncounterSite.All)
            {
                if (site != null && (kennel == null || site.Order > kennel.Order)) kennel = site;
            }
            if (kennel == null || yari == null) yield break;
            Vector3 back = yari.position - boss.transform.position;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : -boss.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, back);
            for (int i = 0; i < 2; i++)
            {
                Vector3 spot = yari.position + back * 9f + side * (i == 0 ? 3.5f : -3.5f);
                if (!Physics.Raycast(spot + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.point.y - yari.position.y) > 3f) continue; // ni en la quebrada ni sobre un muro
                EnemyController hunter = kennel.CreateRival(hit.point + Vector3.up * 0.05f, Quaternion.LookRotation(-back), $"Cazador_de_Amaru_{i + 1}");
                if (hunter != null) hunter.HuntRange = 60f;
            }
        }

        private void Update()
        {
            if (boss == null || yari == null || boss.IsDead || boss.Dormant || AyniJudgment.Active) return;
            bool engaged = (boss.transform.position - yari.position).sqrMagnitude < 30f * 30f && boss.State != EnemyState.Idle;

            // Yari cayó y la Illa le cobró años: al terminar la cuenta, Amaru se burla
            bool aging = AyniDeathAging.Active;
            if (wasAging && !aging && engaged) Say("Cada caída te cuesta años. Yo tengo paciencia.", 3.2f, true);
            wasAging = aging;
            if (aging || !engaged) return;

            // Con la historia de los quipus, Yari nombra a Killa y Amaru duda
            if (!killaSaid && AyniAmaruQuipus.KnowsStory && boss.Phase >= boss.PhaseCount && !boss.InPhaseTransition &&
                boss.HealthRatio < 0.8f && !AyniVoice.Speaking)
            {
                killaSaid = true;
                quietUntil = Time.time + 9f;
                StartCoroutine(KillaMoment());
                return;
            }

            if (!weakSaid && boss.InFinalPhase && boss.HealthRatio < 0.55f)
            {
                weakSaid = true;
                Say("No... Sayri me prometió que ningún guardián volvería.", 3.2f, true);
                AyniVoice.Reply(this, "YARI: «Sayri te mintió, como nos mintió a todos.»", 3.2f);
                quietUntil = Time.time + 8f;
                return;
            }

            if (Time.time < nextTaunt) return;
            nextTaunt = Time.time + Random.Range(tauntMin, tauntMax);
            if ((boss.transform.position - yari.position).sqrMagnitude > 8f * 8f) return;
            int pick = Random.Range(0, Taunts.Length);
            if (pick == lastTaunt) pick = (pick + 1) % Taunts.Length;
            lastTaunt = pick;
            Say(Taunts[pick], 2.8f, false);
        }

        /// <summary>
        /// Yari grita el nombre de la hermana de Amaru (lo leyó en el último quipu). Amaru se queda helado: se tambalea y
        /// pierde un 40 % de su postura, una ocasión de oro para romperla.
        /// </summary>
        private IEnumerator KillaMoment()
        {
            AyniScreenFX.Caption("YARI: «¡Killa te espera en el Antisuyo, Amaru!»", 2.8f);
            yield return new WaitForSecondsRealtime(AyniVoice.HoldFor(2.6f));
            if (boss == null || boss.IsDead || AyniJudgment.Active || boss.InPhaseTransition) yield break;
            Say("¿Killa...? ¿Quién te dio ese nombre?", 3f, true);
            float posture = boss.Structure != null ? boss.Structure.MaxStructure * 0.4f : 0f;
            boss.TakeHit(0f, posture, yari.position, true, HitReaction.Heavy, 0.3f);
            CombatFeedback.Flash(boss.transform.position + Vector3.up * 1.6f, new Color(1f, 0.85f, 0.55f), 1.4f, 0.4f);
            Debug.Log("[Ayni Quipus] Amaru duda al oír el nombre de Killa");
        }

        private void Say(string line, float seconds, bool important)
        {
            if (!important && Time.time < quietUntil) return;
            AyniBossIntro.Say(line, seconds);
            quietUntil = Time.time + seconds + 1f;
            nextTaunt = Mathf.Max(nextTaunt, Time.time + tauntMin);
        }
    }
}
