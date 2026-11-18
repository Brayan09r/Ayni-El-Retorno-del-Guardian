using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;

namespace Ayni.Story
{
    /// <summary>
    /// Música del nivel 1 según lo que pasa: el camino (exploración), las peleas en las kanchas (combate) y el duelo con
    /// Amaru (jefe), con fundidos entre pistas. Calla durante el prólogo, que trae su quena, y durante el Juicio Ayni.
    ///
    /// Las pistas son tres archivos en Assets/Resources/AyniAudio (mp3 o wav):
    ///   musica_nivel1_exploracion · musica_nivel1_combate · musica_amaru
    /// Si falta alguna, esa parte queda solo con el ambiente; no hay que tocar nada más. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniLevelMusic : MonoBehaviour
    {
        public const string Exploration = "musica_nivel1_exploracion";
        public const string Combat = "musica_nivel1_combate";
        public const string Boss = "musica_amaru";

        [SerializeField] private float explorationVolume = 0.45f;
        [SerializeField] private float combatVolume = 0.6f;
        [SerializeField] private float bossVolume = 0.7f;
        [Tooltip("Un rival a menos de esta distancia que persigue o ataca a Yari pone la música de combate.")]
        [SerializeField] private float combatRadius = 18f;
        [Tooltip("Segundos sin rivales cerca antes de volver a la música del camino.")]
        [SerializeField] private float calmDelay = 4f;

        private string current;
        private int handle = -1;
        private float lastThreatAt = -100f;
        private Transform yari;

        private void Update()
        {
            if (yari == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                yari = player.transform;
            }
            Switch(Wanted());
        }

        private string Wanted()
        {
            if (AyniGameState.CinematicPlaying || AyniJudgment.Active) return null;
            if (AyniBossIntro.Running) return Boss;

            bool threat = false, boss = false;
            Vector3 p = yari.position;
            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null || e.IsDead || !e.isActiveAndEnabled) continue;
                bool hunting = e.State != EnemyState.Idle || (e.IsBoss && e.ExternalControl);
                if (!hunting) continue;
                Vector3 d = e.transform.position - p;
                d.y = 0f;
                if (d.magnitude > (e.IsBoss ? combatRadius * 2f : combatRadius)) continue;
                threat = true;
                if (e.IsBoss) boss = true;
            }
            if (boss) return Boss;
            if (threat) lastThreatAt = Time.time;
            return Time.time - lastThreatAt < calmDelay ? (current == Boss ? Boss : Combat) : Exploration;
        }

        private void Switch(string wanted)
        {
            if (wanted == current) return;
            AyniAudio.StopLoop(handle, wanted == null ? 1.2f : 2f);
            current = wanted;
            handle = wanted == null ? -1 : AyniAudio.PlayMusicLoop(wanted, VolumeOf(wanted), wanted == Exploration ? 3f : 1.2f);
        }

        private float VolumeOf(string track) =>
            track == Boss ? bossVolume : track == Combat ? combatVolume : explorationVolume;

        private void OnDestroy()
        {
            AyniAudio.StopLoop(handle, 0.5f);
        }
    }
}
