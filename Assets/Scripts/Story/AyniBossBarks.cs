using UnityEngine;
using Ayni.Enemy;
using Ayni.UI;

namespace Ayni.Story
{
    /// <summary>
    /// Amaru habla durante el duelo (subtítulos con su nombre, ver <see cref="AyniBossIntro.Say"/>): provoca a Yari de
    /// vez en cuando mientras pelean de cerca, grita al pasar a su segunda fase, se burla cuando Yari cae y la Illa le
    /// cobra años, y flaquea cuando le queda poca vida. Cada frase espera su turno para no pisarse con otra.
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
            Say("¡Me has herido, guardián! Ahora conocerás al Cazador.", 3.2f, true);
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
