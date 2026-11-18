using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.World;

namespace Ayni.Story
{
    /// <summary>
    /// Música del nivel 1 según lo que pasa: el camino (exploración), las peleas en las kanchas (combate) y el duelo con
    /// Amaru (jefe). Calla durante el prólogo, que trae su quena, y durante el Juicio Ayni.
    ///
    /// Las tres pistas (de Suno) tienen tempos distintos (~78, ~96 y ~76 bpm), así que no se mezclan con un fundido
    /// cruzado, que sonaría a dos canciones a la vez. En cambio:
    ///  - Entrar en combate: en el instante en que un rival da la voz (con el tambor del patio) el camino se corta en
    ///    medio segundo y el combate arranca directamente en su primer golpe fuerte (no en su introducción suave).
    ///  - Acabar la pelea: el combate se apaga despacio, queda un respiro solo con el viento y el camino vuelve
    ///    desde su comienzo, que es tranquilo.
    ///  - Amaru: su pista arranca en la presentación de modo que su primer golpe fuerte coincida con su título.
    /// Los segundos de cada pista se midieron con Herramientas/Audio (analizar_musica.py); si se cambia una pista,
    /// basta con ajustar los campos de "Entradas".
    ///
    /// Pistas en Assets/Resources/AyniAudio (mp3 o wav): musica_nivel1_exploracion · musica_nivel1_combate · musica_amaru.
    /// Si falta alguna, esa parte queda solo con el ambiente. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniLevelMusic : MonoBehaviour
    {
        public const string Exploration = "musica_nivel1_exploracion";
        public const string Combat = "musica_nivel1_combate";
        public const string Boss = "musica_amaru";

        [Header("Volumen")]
        [SerializeField] private float explorationVolume = 0.4f;
        [SerializeField] private float combatVolume = 0.7f;
        [SerializeField] private float bossVolume = 0.75f;

        [Header("Entradas (segundos dentro de cada pista)")]
        [Tooltip("Primer golpe fuerte de la pista de combate: ahí arranca al empezar una pelea.")]
        [SerializeField] private float combatEntry = 7.9f;
        [Tooltip("Primer golpe fuerte de la pista de Amaru.")]
        [SerializeField] private float bossHit = 11.87f;
        [Tooltip("Segundos de la presentación de Amaru hasta que aparece su título (el golpe fuerte cae ahí).")]
        [SerializeField] private float bossTitleAt = 3.2f;

        [Header("Ritmo")]
        [Tooltip("Un rival a menos de esta distancia que persigue o ataca a Yari mantiene la música de combate.")]
        [SerializeField] private float combatRadius = 18f;
        [Tooltip("Segundos sin rivales cerca antes de dar la pelea por terminada.")]
        [SerializeField] private float calmDelay = 3f;
        [Tooltip("Respiro (solo viento) entre el final de una pelea y la vuelta de la música del camino.")]
        [SerializeField] private float breath = 2.5f;

        private string current;
        private int handle = -1;
        private float lastThreatAt = -100f, silentUntil;
        private Transform yari;
        private bool bossIntroSeen;

        private void OnEnable()
        {
            AyniRivalAmbient.OnAlert += HandleAlert;
            AyniEncounterSite.OnPlayerEntered += HandleSiteEntered;
        }

        private void OnDisable()
        {
            AyniRivalAmbient.OnAlert -= HandleAlert;
            AyniEncounterSite.OnPlayerEntered -= HandleSiteEntered;
        }

        private void HandleAlert(AyniRivalAmbient who) => StartFight();
        private void HandleSiteEntered(AyniEncounterSite site) => StartFight();

        /// <summary>
        /// Una pelea empieza ya: el combate entra en su primer golpe fuerte, a la vez que el tambor que toca
        /// AyniLevelAmbience al empezar la pelea del patio.
        /// </summary>
        private void StartFight()
        {
            lastThreatAt = Time.time;
            if (current == Combat || current == Boss || AyniGameState.CinematicPlaying) return;
            Switch(Combat, 0.5f, 0.12f, combatEntry);
        }

        private void Update()
        {
            if (yari == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                yari = player.transform;
            }

            if (AyniGameState.CinematicPlaying || AyniJudgment.Active)
            {
                Switch(null, 1.2f, 0f, 0f);
                return;
            }

            // Presentación de Amaru: su pista con el golpe fuerte en el título
            if (AyniBossIntro.Running)
            {
                if (current != Boss)
                {
                    bossIntroSeen = true;
                    Switch(Boss, 0.6f, 0.4f, Mathf.Max(0f, bossHit - bossTitleAt));
                }
                return;
            }

            bool threat = false, boss = false;
            Vector3 p = yari.position;
            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null || e.IsDead || e.Dormant || !e.isActiveAndEnabled) continue;
                if (e.State == EnemyState.Idle && !(e.IsBoss && e.ExternalControl)) continue;
                Vector3 d = e.transform.position - p;
                d.y = 0f;
                if (d.magnitude > (e.IsBoss ? combatRadius * 2f : combatRadius)) continue;
                threat = true;
                if (e.IsBoss) boss = true;
            }
            if (threat) lastThreatAt = Time.time;

            if (boss)
            {
                if (current != Boss) Switch(Boss, 0.5f, 0.2f, bossIntroSeen ? 0f : bossHit);
                return;
            }
            if (threat)
            {
                if (current != Combat && current != Boss) StartFight();
                return;
            }

            // Sin rivales cerca: la pelea acabó. Respiro y vuelve el camino desde su comienzo
            if (Time.time - lastThreatAt < calmDelay) return;
            if (current == Combat || current == Boss)
            {
                Switch(null, 3f, 0f, 0f);
                silentUntil = Time.time + breath;
                return;
            }
            if (current == null && Time.time >= silentUntil) Switch(Exploration, 0f, 4f, 0f);
        }

        private void Switch(string wanted, float fadeOut, float fadeIn, float startAt)
        {
            if (wanted == current) return;
            AyniAudio.StopLoop(handle, fadeOut);
            current = wanted;
            handle = wanted == null ? -1 : AyniAudio.PlayMusicLoop(wanted, VolumeOf(wanted), fadeIn, startAt);
        }

        private float VolumeOf(string track) =>
            track == Boss ? bossVolume : track == Combat ? combatVolume : explorationVolume;

        private void OnDestroy()
        {
            AyniAudio.StopLoop(handle, 0.5f);
        }
    }
}
