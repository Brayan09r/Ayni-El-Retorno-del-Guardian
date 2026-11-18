using UnityEngine;

namespace Ayni.World
{
    /// <summary>
    /// Lector del clima actual para los efectos atmosféricos (gradación de color, polvo de luz, sombras de nubes).
    ///
    /// No modifica <see cref="AyniDynamicLighting"/> ni se suscribe a eventos del juego: observa la luz que ese sistema
    /// ya aplica (niebla, color del sol, exposición del cielo y luz ambiente) y estima qué tan cerca está de cada uno de
    /// los cinco climas (<see cref="AyniLightingPreset"/>). Como AyniDynamicLighting interpola esos valores durante sus
    /// transiciones, los pesos también cambian de forma suave, y los efectos acompañan el cambio de clima en sincronía,
    /// incluso con los menús de previsualización del Editor.
    /// </summary>
    public static class AyniClimateBlend
    {
        public const int PresetCount = 5;

        private const int FeatureCount = 11;
        private static float[][] presetFeatures;
        private static readonly float[] Weights = new float[PresetCount];
        private static readonly float[] Current = new float[FeatureCount];
        private static int lastFrame = -1;
        private static Light cachedSun;

        /// <summary>Peso (0..1) de un clima en el instante actual. Los cinco pesos suman 1.</summary>
        public static float Weight(AyniLightingPreset preset)
        {
            Refresh();
            return Weights[(int)preset];
        }

        /// <summary>Combina un valor por clima según los pesos actuales (orden de <see cref="AyniLightingPreset"/>).</summary>
        public static float Blend(float horaDorada, float combate, float faseDos, float amanecer, float ocaso)
        {
            Refresh();
            return Weights[0] * horaDorada + Weights[1] * combate + Weights[2] * faseDos +
                   Weights[3] * amanecer + Weights[4] * ocaso;
        }

        /// <summary>Clima más cercano al estado actual.</summary>
        public static AyniLightingPreset Dominant
        {
            get
            {
                Refresh();
                int best = 0;
                for (int i = 1; i < PresetCount; i++) if (Weights[i] > Weights[best]) best = i;
                return (AyniLightingPreset)best;
            }
        }

        private static void Refresh()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;

            if (presetFeatures == null)
            {
                presetFeatures = new float[PresetCount][];
                for (int i = 0; i < PresetCount; i++)
                {
                    AyniDynamicLighting.LightingState s = AyniDynamicLighting.GetPresetState((AyniLightingPreset)i);
                    presetFeatures[i] = new float[FeatureCount];
                    Fill(presetFeatures[i], s.fogColor, s.fogDensity, s.skyExposure, s.sunColor, s.sunIntensity, s.ambientSky);
                }
            }

            Material sky = RenderSettings.skybox;
            float exposure = sky != null && sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1.25f;
            Light sun = FindSun();
            Color sunColor = sun != null ? sun.color : new Color(1f, 0.8f, 0.58f);
            float sunIntensity = sun != null ? sun.intensity : 1.7f;
            Fill(Current, RenderSettings.fogColor, RenderSettings.fogDensity, exposure, sunColor, sunIntensity, RenderSettings.ambientSkyColor);

            // Ponderación por distancia inversa: en un clima exacto su peso es ~1; a mitad de una transición se reparte.
            float total = 0f;
            for (int i = 0; i < PresetCount; i++)
            {
                float d2 = 0f;
                float[] f = presetFeatures[i];
                for (int k = 0; k < FeatureCount; k++)
                {
                    float diff = Current[k] - f[k];
                    d2 += diff * diff;
                }
                Weights[i] = 1f / (d2 + 1e-5f);
                total += Weights[i];
            }
            for (int i = 0; i < PresetCount; i++) Weights[i] /= total;
        }

        private static void Fill(float[] dst, Color fog, float fogDensity, float skyExposure, Color sun, float sunIntensity, Color ambientSky)
        {
            // Escalas elegidas para que cada rasgo pese parecido (la densidad de niebla es muy pequeña en bruto)
            dst[0] = fog.r;
            dst[1] = fog.g;
            dst[2] = fog.b;
            dst[3] = fogDensity * 300f;
            dst[4] = skyExposure * 0.5f;
            dst[5] = sun.r;
            dst[6] = sun.g;
            dst[7] = sun.b;
            dst[8] = sunIntensity * 0.3f;
            dst[9] = ambientSky.r;
            dst[10] = ambientSky.b;
        }

        /// <summary>Sol de la escena (la misma regla que usa AyniDynamicLighting).</summary>
        public static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            if (cachedSun != null) return cachedSun;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && l.type == LightType.Directional && l.shadows != LightShadows.None)
                {
                    cachedSun = l;
                    return l;
                }
            }
            return null;
        }
    }
}
