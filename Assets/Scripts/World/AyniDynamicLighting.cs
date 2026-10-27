using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Ayni.Core;
using Ayni.Enemy;

namespace Ayni.World
{
    public enum AyniLightingPreset
    {
        HoraDorada = 0,       // Estado base del nivel (sol bajo cálido desde el oeste)
        CombateKancha = 1,    // Ligero aumento de contraste cálido al combatir en un patio inca
        FaseDosBrasas = 2,    // Segunda fase del jefe (Amaru Desatado: sol cobrizo y bruma de brasas)
        AmanecerAyni = 3,     // Desenlace Perdón (ODS 15: luz cristalina de amanecer tras la lluvia)
        OcasoVenganza = 4     // Desenlace Venganza (Hucha: cielo oscurecido, sol rojizo y niebla de ceniza)
    }

    /// <summary>
    /// Sistema de iluminación dinámica y narrativa del Nivel 1.
    /// Escucha los eventos del juego sin modificar otros scripts:
    ///  - Entrada/limpieza de conjuntos incas (AyniEncounterSite).
    ///  - Cambio a Segunda Fase del jefe (EnemyController.OnPhaseTransitionStarted).
    ///  - Resolución moral del Juicio Ayni (AyniPurificationManager.OnCombatResolved).
    /// Además convierte automáticamente las luces estáticas de los fogones interiores (Luz_Fogon) en luces vivas con AyniFireLight.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniDynamicLighting : MonoBehaviour
    {
        [System.Serializable]
        public struct LightingState
        {
            public Vector3 sunEuler;
            public Color sunColor;
            public float sunIntensity;
            public float shadowStrength;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public Color fogColor;
            public float fogDensity;
            public float skyExposure;
            public Color skyTint;
            public float outdoorFireMultiplier;
        }

        public static AyniDynamicLighting Instance { get; private set; }

        [SerializeField] private AyniLightingPreset currentPreset = AyniLightingPreset.HoraDorada;
        private Coroutine transitionRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnPlay()
        {
            if (Instance != null) return;
            var existing = FindFirstObjectByType<AyniDynamicLighting>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }
            GameObject root = GameObject.Find("Ayni_Entorno");
            var host = new GameObject("Ayni_DynamicLighting_Runtime");
            if (root != null) host.transform.SetParent(root.transform, false);
            Instance = host.AddComponent<AyniDynamicLighting>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            AyniFireLight.GlobalOutdoorMultiplier = 1f;
            UpgradeStaticHearthLights();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            AyniFireLight.GlobalOutdoorMultiplier = 1f;
            if (Instance == this) Instance = null;
        }

        private void SubscribeEvents()
        {
            AyniEncounterSite.OnPlayerEntered += HandleSiteEntered;
            AyniEncounterSite.OnCleared += HandleSiteCleared;
            EnemyController.OnPhaseTransitionStarted += HandleBossPhaseTwo;
            if (AyniPurificationManager.Instance != null)
            {
                AyniPurificationManager.Instance.OnCombatResolved += HandleCombatResolved;
            }
        }

        private void UnsubscribeEvents()
        {
            AyniEncounterSite.OnPlayerEntered -= HandleSiteEntered;
            AyniEncounterSite.OnCleared -= HandleSiteCleared;
            EnemyController.OnPhaseTransitionStarted -= HandleBossPhaseTwo;
            if (AyniPurificationManager.Instance != null)
            {
                AyniPurificationManager.Instance.OnCombatResolved -= HandleCombatResolved;
            }
        }

        /// <summary>
        /// Busca las luces puntuales de las casas incas (Luz_Fogon y Luz_Lampara) y les añade AyniFireLight
        /// para que parpadeen orgánicamente como brasas reales.
        /// </summary>
        public static int UpgradeStaticHearthLights()
        {
            int upgraded = 0;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || l.type != LightType.Point) continue;
                if (l.name.StartsWith("Luz_Fogon") || l.name.StartsWith("Luz_Lampara"))
                {
                    var fire = l.GetComponent<AyniFireLight>();
                    if (fire == null)
                    {
                        bool isHearth = l.name.StartsWith("Luz_Fogon");
                        fire = l.gameObject.AddComponent<AyniFireLight>();
                        fire.Configure(l.intensity, l.range, false, isHearth, isHearth ? 0.65f : 0.35f);
                        upgraded++;
                    }
                }
            }
            return upgraded;
        }

        private void HandleSiteEntered(AyniEncounterSite site)
        {
            if (currentPreset == AyniLightingPreset.HoraDorada)
            {
                TransitionTo(AyniLightingPreset.CombateKancha, 2.2f);
            }
        }

        private void HandleSiteCleared(AyniEncounterSite site)
        {
            if (currentPreset == AyniLightingPreset.CombateKancha)
            {
                TransitionTo(AyniLightingPreset.HoraDorada, 2.8f);
            }
        }

        private void HandleBossPhaseTwo(EnemyController boss)
        {
            if (boss != null && boss.IsBoss)
            {
                TransitionTo(AyniLightingPreset.FaseDosBrasas, 2.4f);
            }
        }

        private void HandleCombatResolved(EnemyController enemy, bool spared)
        {
            if (enemy == null || !enemy.IsBoss) return;
            TransitionTo(spared ? AyniLightingPreset.AmanecerAyni : AyniLightingPreset.OcasoVenganza, 5.5f);
        }

        public void TransitionTo(AyniLightingPreset preset, float duration = 3.0f)
        {
            currentPreset = preset;
            if (!Application.isPlaying || duration <= 0.01f)
            {
                ApplyStateImmediate(GetPresetState(preset));
                return;
            }
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionRoutine(GetPresetState(preset), duration));
        }

        public static void ApplyPresetImmediate(AyniLightingPreset preset)
        {
            ApplyStateImmediate(GetPresetState(preset));
        }

        public static LightingState GetPresetState(AyniLightingPreset preset)
        {
            switch (preset)
            {
                case AyniLightingPreset.CombateKancha:
                    return new LightingState
                    {
                        sunEuler = new Vector3(16f, 108f, 0f),
                        sunColor = new Color(1.0f, 0.76f, 0.52f),
                        sunIntensity = 1.82f,
                        shadowStrength = 0.82f,
                        ambientSky = new Color(0.50f, 0.55f, 0.72f),
                        ambientEquator = new Color(0.62f, 0.48f, 0.39f),
                        ambientGround = new Color(0.28f, 0.22f, 0.18f),
                        fogColor = new Color(0.82f, 0.70f, 0.60f),
                        fogDensity = 0.0020f,
                        skyExposure = 1.22f,
                        skyTint = new Color(0.54f, 0.54f, 0.65f),
                        outdoorFireMultiplier = 1.15f
                    };

                case AyniLightingPreset.FaseDosBrasas:
                    return new LightingState
                    {
                        sunEuler = new Vector3(12f, 112f, 0f),
                        sunColor = new Color(1.0f, 0.58f, 0.34f),
                        sunIntensity = 1.95f,
                        shadowStrength = 0.86f,
                        ambientSky = new Color(0.46f, 0.42f, 0.58f),
                        ambientEquator = new Color(0.66f, 0.38f, 0.28f),
                        ambientGround = new Color(0.28f, 0.16f, 0.13f),
                        fogColor = new Color(0.76f, 0.54f, 0.44f),
                        fogDensity = 0.0024f,
                        skyExposure = 1.08f,
                        skyTint = new Color(0.62f, 0.44f, 0.48f),
                        outdoorFireMultiplier = 1.45f
                    };

                case AyniLightingPreset.AmanecerAyni:
                    // Perdón (ODS 15): la lluvia purifica el valle, el sol se eleva con luz clara y dorada
                    return new LightingState
                    {
                        sunEuler = new Vector3(26f, 98f, 0f),
                        sunColor = new Color(1.0f, 0.92f, 0.76f),
                        sunIntensity = 1.85f,
                        shadowStrength = 0.72f,
                        ambientSky = new Color(0.56f, 0.68f, 0.84f),
                        ambientEquator = new Color(0.58f, 0.64f, 0.58f),
                        ambientGround = new Color(0.28f, 0.32f, 0.25f),
                        fogColor = new Color(0.76f, 0.84f, 0.86f),
                        fogDensity = 0.0014f,
                        skyExposure = 1.38f,
                        skyTint = new Color(0.46f, 0.64f, 0.78f),
                        outdoorFireMultiplier = 0.35f // la lluvia atenúa las llamas exteriores
                    };

                case AyniLightingPreset.OcasoVenganza:
                    // Venganza (Hucha): el cielo se oscurece y el humo/ceniza cubre el Antisuyo
                    return new LightingState
                    {
                        sunEuler = new Vector3(8.5f, 118f, 0f),
                        sunColor = new Color(0.92f, 0.36f, 0.20f),
                        sunIntensity = 1.05f,
                        shadowStrength = 0.88f,
                        ambientSky = new Color(0.28f, 0.22f, 0.26f),
                        ambientEquator = new Color(0.42f, 0.22f, 0.16f),
                        ambientGround = new Color(0.16f, 0.10f, 0.09f),
                        fogColor = new Color(0.36f, 0.24f, 0.20f),
                        fogDensity = 0.0034f,
                        skyExposure = 0.62f,
                        skyTint = new Color(0.45f, 0.25f, 0.22f),
                        outdoorFireMultiplier = 1.65f // el fuego se aviva en el camino de la venganza
                    };

                default: // HoraDorada (idéntico al canon del GDD y AyniAtmosphere)
                    return new LightingState
                    {
                        sunEuler = new Vector3(17f, 106f, 0f),
                        sunColor = new Color(1.0f, 0.80f, 0.58f),
                        sunIntensity = 1.70f,
                        shadowStrength = 0.78f,
                        ambientSky = new Color(0.52f, 0.57f, 0.74f),
                        ambientEquator = new Color(0.58f, 0.49f, 0.42f),
                        ambientGround = new Color(0.26f, 0.22f, 0.19f),
                        fogColor = new Color(0.80f, 0.71f, 0.63f),
                        fogDensity = 0.0019f,
                        skyExposure = 1.25f,
                        skyTint = new Color(0.52f, 0.56f, 0.66f),
                        outdoorFireMultiplier = 1.0f
                    };
            }
        }

        private static LightingState CaptureCurrentState()
        {
            Light sun = FindSun();
            Material sky = RenderSettings.skybox;
            return new LightingState
            {
                sunEuler = sun != null ? sun.transform.eulerAngles : new Vector3(17f, 106f, 0f),
                sunColor = sun != null ? sun.color : new Color(1f, 0.8f, 0.58f),
                sunIntensity = sun != null ? sun.intensity : 1.7f,
                shadowStrength = sun != null ? sun.shadowStrength : 0.78f,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                fogColor = RenderSettings.fogColor,
                fogDensity = RenderSettings.fogDensity,
                skyExposure = sky != null && sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1.25f,
                skyTint = sky != null && sky.HasProperty("_SkyTint") ? sky.GetColor("_SkyTint") : new Color(0.52f, 0.56f, 0.66f),
                outdoorFireMultiplier = AyniFireLight.GlobalOutdoorMultiplier
            };
        }

        private static void ApplyStateImmediate(LightingState s)
        {
            Light sun = FindSun();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(s.sunEuler);
                sun.color = s.sunColor;
                sun.intensity = s.sunIntensity;
                sun.shadowStrength = s.shadowStrength;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.ambientSky;
            RenderSettings.ambientEquatorColor = s.ambientEquator;
            RenderSettings.ambientGroundColor = s.ambientGround;

            RenderSettings.fog = true;
            RenderSettings.fogColor = s.fogColor;
            RenderSettings.fogDensity = s.fogDensity;

            Material sky = RenderSettings.skybox;
            if (sky != null)
            {
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", s.skyExposure);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", s.skyTint);
            }

            AyniFireLight.GlobalOutdoorMultiplier = s.outdoorFireMultiplier;
        }

        private IEnumerator TransitionRoutine(LightingState target, float duration)
        {
            LightingState start = CaptureCurrentState();
            Quaternion rotStart = Quaternion.Euler(start.sunEuler);
            Quaternion rotEnd = Quaternion.Euler(target.sunEuler);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / duration);
                float t = u * u * (3f - 2f * u);

                LightingState step = new LightingState
                {
                    sunEuler = Quaternion.Slerp(rotStart, rotEnd, t).eulerAngles,
                    sunColor = Color.Lerp(start.sunColor, target.sunColor, t),
                    sunIntensity = Mathf.Lerp(start.sunIntensity, target.sunIntensity, t),
                    shadowStrength = Mathf.Lerp(start.shadowStrength, target.shadowStrength, t),
                    ambientSky = Color.Lerp(start.ambientSky, target.ambientSky, t),
                    ambientEquator = Color.Lerp(start.ambientEquator, target.ambientEquator, t),
                    ambientGround = Color.Lerp(start.ambientGround, target.ambientGround, t),
                    fogColor = Color.Lerp(start.fogColor, target.fogColor, t),
                    fogDensity = Mathf.Lerp(start.fogDensity, target.fogDensity, t),
                    skyExposure = Mathf.Lerp(start.skyExposure, target.skyExposure, t),
                    skyTint = Color.Lerp(start.skyTint, target.skyTint, t),
                    outdoorFireMultiplier = Mathf.Lerp(start.outdoorFireMultiplier, target.outdoorFireMultiplier, t)
                };

                ApplyStateImmediate(step);
                yield return null;
            }

            ApplyStateImmediate(target);
            transitionRoutine = null;
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && l.type == LightType.Directional && l.shadows != LightShadows.None)
                {
                    return l;
                }
            }
            return null;
        }
    }
}
