using UnityEngine;
using Ayni.Core;
using Ayni.World;

namespace Ayni.Story
{
    /// <summary>
    /// Ambiente sonoro del nivel: el viento de la cordillera de fondo (más bajo durante el prólogo, que trae su propio
    /// fuego y su quena) y un golpe de tambor cuando Yari entra en un patio con rivales o los derrota a todos.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniLevelAmbience : MonoBehaviour
    {
        [SerializeField] private float windVolume = 0.55f;

        private int wind = -1;
        private bool lowered;

        private void Start()
        {
            wind = AyniAudio.PlayLoop("amb_viento", AyniGameState.CinematicPlaying ? windVolume * 0.4f : windVolume, 3f);
            lowered = AyniGameState.CinematicPlaying;
            AyniEncounterSite.OnPlayerEntered += HandleEntered;
            AyniEncounterSite.OnCleared += HandleCleared;
        }

        private void OnDestroy()
        {
            AyniEncounterSite.OnPlayerEntered -= HandleEntered;
            AyniEncounterSite.OnCleared -= HandleCleared;
            AyniAudio.StopLoop(wind, 0.5f);
        }

        private void Update()
        {
            bool cinematic = AyniGameState.CinematicPlaying;
            if (cinematic != lowered)
            {
                lowered = cinematic;
                AyniAudio.SetLoopVolume(wind, cinematic ? windVolume * 0.4f : windVolume, 2f);
            }
        }

        private void HandleEntered(AyniEncounterSite site)
        {
            AyniAudio.Play2D("titulo_golpe", 0.7f);
        }

        private void HandleCleared(AyniEncounterSite site)
        {
            AyniAudio.Play2D("ui_confirmar", 0.8f);
        }
    }
}
