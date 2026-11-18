using UnityEngine;
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
        private bool wasAging, weakSaid;

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
            CallHunters();
        }

        /// <summary>Dos cazadores (como los de la Portada del Cazador) llegan corriendo por la espalda de Yari.</summary>
        private void CallHunters()
        {
            AyniEncounterSite kennel = null;
            foreach (AyniEncounterSite site in AyniEncounterSite.All)
            {
                if (site != null && (kennel == null || site.Order > kennel.Order)) kennel = site;
            }
            if (kennel == null || yari == null) return;
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

            if (!weakSaid && boss.InFinalPhase && boss.HealthRatio < 0.55f)
            {
                weakSaid = true;
                Say("No... Sayri me prometió que ningún guardián volvería.", 3.2f, true);
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

        private void Say(string line, float seconds, bool important)
        {
            if (!important && Time.time < quietUntil) return;
            AyniBossIntro.Say(line, seconds);
            quietUntil = Time.time + seconds + 1f;
            nextTaunt = Mathf.Max(nextTaunt, Time.time + tauntMin);
        }
    }
}
