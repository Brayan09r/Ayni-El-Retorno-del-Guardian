#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// Menú Ayni > Iluminación > En Play para recorrer los climas con su transición completa mientras el juego corre,
    /// y así ver juntos la luz de AyniDynamicLighting y los efectos atmosféricos (gradación de color, motas de luz y
    /// sombras de nubes), que solo existen en modo Play.
    /// </summary>
    public static class AyniClimateLiveMenu
    {
        private const string Root = "Ayni/Iluminación/En Play/";
        private const float Duration = 3f;

        [MenuItem(Root + "Clima: Hora Dorada", false, 200)]
        private static void HoraDorada() => Go(AyniLightingPreset.HoraDorada);

        [MenuItem(Root + "Clima: Combate en Kancha", false, 201)]
        private static void Combate() => Go(AyniLightingPreset.CombateKancha);

        [MenuItem(Root + "Clima: Fase 2 de Amaru (Brasas)", false, 202)]
        private static void FaseDos() => Go(AyniLightingPreset.FaseDosBrasas);

        [MenuItem(Root + "Clima: Amanecer Ayni (Perdón)", false, 203)]
        private static void Amanecer() => Go(AyniLightingPreset.AmanecerAyni);

        [MenuItem(Root + "Clima: Ocaso de Venganza (Hucha)", false, 204)]
        private static void Ocaso() => Go(AyniLightingPreset.OcasoVenganza);

        [MenuItem(Root + "Mostrar Pesos del Clima en Consola", false, 230)]
        private static void LogWeights()
        {
            string line = "[Ayni Clima] Pesos actuales:";
            for (int i = 0; i < AyniClimateBlend.PresetCount; i++)
            {
                var preset = (AyniLightingPreset)i;
                line += $"  {preset} {AyniClimateBlend.Weight(preset):0.00}";
            }
            Debug.Log(line);
        }

        [MenuItem(Root + "Clima: Hora Dorada", true)]
        [MenuItem(Root + "Clima: Combate en Kancha", true)]
        [MenuItem(Root + "Clima: Fase 2 de Amaru (Brasas)", true)]
        [MenuItem(Root + "Clima: Amanecer Ayni (Perdón)", true)]
        [MenuItem(Root + "Clima: Ocaso de Venganza (Hucha)", true)]
        [MenuItem(Root + "Mostrar Pesos del Clima en Consola", true)]
        private static bool OnlyInPlay() => EditorApplication.isPlaying;

        private static void Go(AyniLightingPreset preset)
        {
            AyniDynamicLighting lighting = AyniDynamicLighting.Instance;
            if (lighting == null)
            {
                Debug.LogWarning("[Ayni Clima] No hay AyniDynamicLighting en la escena. Ejecuta antes Ayni > Iluminación > 1. Construir Iluminación Andina.");
                return;
            }
            lighting.TransitionTo(preset, Duration);
            Debug.Log($"[Ayni Clima] Transición a {preset} en {Duration:0.#} s.");
        }
    }
}
#endif
