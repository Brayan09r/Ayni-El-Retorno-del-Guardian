using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ayni.World
{
    /// <summary>
    /// Gradación de color cinematográfica que acompaña cada clima de <see cref="AyniDynamicLighting"/>.
    ///
    /// Hasta ahora el posprocesado (perfil Ayni_PostProcess_Profile) era el mismo en todos los climas: solo cambiaban
    /// el sol, el cielo y la niebla. Este componente añade cuatro volúmenes globales en tiempo de ejecución, uno por clima
    /// distinto de la Hora Dorada, y sube o baja su peso según <see cref="AyniClimateBlend"/>:
    ///  - Combate en Kancha:  algo más de contraste y viñeta, la mirada se cierra sobre el patio.
    ///  - Fase 2 de Amaru:    tono cobrizo, bloom de brasas y viñeta rojiza.
    ///  - Amanecer Ayni:      luz limpia y fresca, verdes más vivos (ODS 15), viñeta abierta.
    ///  - Ocaso de Venganza:  color apagado y ceniciento, mucho contraste y viñeta de sangre.
    /// Cada volumen parte de los valores del perfil existente y solo suma la diferencia, por lo que la Hora Dorada
    /// se ve exactamente igual que antes. No modifica ningún asset: los perfiles se crean en memoria.
    /// Lo instala <see cref="AyniAtmosphereRuntime"/> en las escenas que tienen el objeto Ayni_Entorno.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniClimateGrade : MonoBehaviour
    {
        private struct Grade
        {
            public float exposure, contrast, saturation;
            public Color filter;
            public float temperature, tint;
            public float bloomIntensity, bloomThreshold;
            public Color bloomTint;
            public float vignette;
            public Color vignetteColor;
        }

        [Tooltip("Velocidad con que cada volumen sigue su peso objetivo (por segundo).")]
        [SerializeField] private float followSpeed = 2.5f;

        private static AyniClimateGrade instance;
        private readonly List<VolumeProfile> runtimeProfiles = new List<VolumeProfile>();
        private readonly Volume[] volumes = new Volume[AyniClimateBlend.PresetCount];
        private Grade baseGrade;

        // ------------------------------------------------------------------------------------------------
        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            int basePriority = ReadBaseGrade(out baseGrade);

            for (int i = 1; i < AyniClimateBlend.PresetCount; i++)
            {
                var preset = (AyniLightingPreset)i;
                Grade g = Apply(baseGrade, preset);

                var go = new GameObject("Gradacion_" + preset);
                go.transform.SetParent(transform, false);
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = basePriority + i;
                volume.weight = 0f;

                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Ayni_Gradacion_" + preset + " (runtime)";
                runtimeProfiles.Add(profile);

                var color = profile.Add<ColorAdjustments>();
                color.postExposure.Override(g.exposure);
                color.contrast.Override(Mathf.Clamp(g.contrast, -100f, 100f));
                color.saturation.Override(Mathf.Clamp(g.saturation, -100f, 100f));
                color.colorFilter.Override(g.filter);

                var balance = profile.Add<WhiteBalance>();
                balance.temperature.Override(Mathf.Clamp(g.temperature, -100f, 100f));
                balance.tint.Override(Mathf.Clamp(g.tint, -100f, 100f));

                var bloom = profile.Add<Bloom>();
                bloom.intensity.Override(Mathf.Max(0f, g.bloomIntensity));
                bloom.threshold.Override(Mathf.Max(0f, g.bloomThreshold));
                bloom.tint.Override(g.bloomTint);

                var vignette = profile.Add<Vignette>();
                vignette.intensity.Override(Mathf.Clamp01(g.vignette));
                vignette.color.Override(g.vignetteColor);

                volume.sharedProfile = profile;
                volumes[i] = volume;
            }
        }

        private void Update()
        {
            // Tiempo sin escalar: la cámara lenta de la muerte de Yari no debe congelar la gradación
            float step = followSpeed * Time.unscaledDeltaTime;
            for (int i = 1; i < AyniClimateBlend.PresetCount; i++)
            {
                if (volumes[i] == null) continue;
                float target = AyniClimateBlend.Weight((AyniLightingPreset)i);
                volumes[i].weight = Mathf.MoveTowards(volumes[i].weight, target, step);
            }
        }

        private void OnDestroy()
        {
            foreach (VolumeProfile p in runtimeProfiles)
            {
                if (p != null) Destroy(p);
            }
            runtimeProfiles.Clear();
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Suma a la gradación base la diferencia propia de cada clima.</summary>
        private static Grade Apply(Grade b, AyniLightingPreset preset)
        {
            Grade g = b;
            switch (preset)
            {
                case AyniLightingPreset.CombateKancha:
                    g.contrast += 6f;
                    g.saturation += 4f;
                    g.temperature += 3f;
                    g.bloomIntensity += 0.05f;
                    g.vignette += 0.06f;
                    break;

                case AyniLightingPreset.FaseDosBrasas:
                    g.exposure -= 0.1f;
                    g.contrast += 12f;
                    g.saturation += 6f;
                    g.filter = b.filter * new Color(1f, 0.9f, 0.8f);
                    g.temperature += 16f;
                    g.tint += 4f;
                    g.bloomIntensity += 0.25f;
                    g.bloomThreshold -= 0.1f;
                    g.bloomTint = new Color(1f, 0.62f, 0.36f);
                    g.vignette += 0.12f;
                    g.vignetteColor = new Color(0.22f, 0.03f, 0f);
                    break;

                case AyniLightingPreset.AmanecerAyni:
                    g.exposure += 0.12f;
                    g.contrast += 2f;
                    g.saturation += 14f;
                    g.filter = b.filter * new Color(0.97f, 1f, 0.99f);
                    g.temperature -= 8f;
                    g.tint -= 3f;
                    g.bloomIntensity += 0.15f;
                    g.bloomThreshold -= 0.1f;
                    g.bloomTint = new Color(1f, 0.95f, 0.86f);
                    g.vignette -= 0.08f;
                    break;

                case AyniLightingPreset.OcasoVenganza:
                    g.exposure -= 0.22f;
                    g.contrast += 18f;
                    g.saturation -= 30f;
                    g.filter = b.filter * new Color(1f, 0.84f, 0.76f);
                    g.temperature += 10f;
                    g.tint += 6f;
                    g.bloomIntensity += 0.1f;
                    g.bloomTint = new Color(1f, 0.45f, 0.25f);
                    g.vignette += 0.2f;
                    g.vignetteColor = new Color(0.12f, 0f, 0f);
                    break;
            }
            return g;
        }

        /// <summary>
        /// Lee los valores del volumen global existente (el de AyniVisualPolish / AyniAtmosphere) para usarlos como base.
        /// Devuelve su prioridad, para colocar los volúmenes de clima justo encima.
        /// </summary>
        private int ReadBaseGrade(out Grade g)
        {
            g = new Grade
            {
                exposure = 0f, contrast = 0f, saturation = 0f, filter = Color.white,
                temperature = 0f, tint = 0f,
                bloomIntensity = 0f, bloomThreshold = 0.9f, bloomTint = Color.white,
                vignette = 0f, vignetteColor = Color.black
            };

            Volume best = null;
            foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v == null || !v.isGlobal || v.transform.IsChildOf(transform)) continue;
                if (v.sharedProfile == null) continue;
                if (best == null || v.priority > best.priority) best = v;
            }
            if (best == null) return 1;

            VolumeProfile p = best.sharedProfile;
            if (p.TryGet(out ColorAdjustments c))
            {
                if (c.postExposure.overrideState) g.exposure = c.postExposure.value;
                if (c.contrast.overrideState) g.contrast = c.contrast.value;
                if (c.saturation.overrideState) g.saturation = c.saturation.value;
                if (c.colorFilter.overrideState) g.filter = c.colorFilter.value;
            }
            if (p.TryGet(out WhiteBalance w))
            {
                if (w.temperature.overrideState) g.temperature = w.temperature.value;
                if (w.tint.overrideState) g.tint = w.tint.value;
            }
            if (p.TryGet(out Bloom bl))
            {
                if (bl.intensity.overrideState) g.bloomIntensity = bl.intensity.value;
                if (bl.threshold.overrideState) g.bloomThreshold = bl.threshold.value;
                if (bl.tint.overrideState) g.bloomTint = bl.tint.value;
            }
            if (p.TryGet(out Vignette vg))
            {
                if (vg.intensity.overrideState) g.vignette = vg.intensity.value;
                if (vg.color.overrideState) g.vignetteColor = vg.color.value;
            }
            return Mathf.RoundToInt(best.priority) + 1;
        }
    }
}
