using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ayni.World
{
    /// <summary>
    /// Sombras de nubes que el viento arrastra sobre el valle, la aldea y los personajes.
    /// Usa una "cookie" de luz en el sol (URP admite cookies en la luz direccional; el asset de URP del proyecto las tiene
    /// activadas): una textura de nubes generada por código, que se repite sin costuras y se desplaza con el viento.
    /// La cobertura y la oscuridad de las nubes siguen el clima (<see cref="AyniClimateBlend"/>):
    ///  - Hora Dorada / Combate: pocas nubes sueltas.
    ///  - Fase 2 de Amaru:       humo y nubes bajas algo más densas.
    ///  - Amanecer Ayni:         nubes que se abren tras la lluvia.
    ///  - Ocaso de Venganza:     cielo cargado, sombras grandes y oscuras, viento más fuerte.
    /// Al destruirse devuelve el sol a su estado original (sin cookie).
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniCloudShadows : MonoBehaviour
    {
        [Header("Nubes")]
        [Tooltip("Tamaño en metros que cubre la textura de nubes antes de repetirse.")]
        [SerializeField] private float cloudTileSize = 220f;
        [SerializeField] private Vector2 windDirection = new Vector2(0.8f, 0.35f);
        [SerializeField] private int textureSize = 256;
        [Tooltip("Semilla fija para que la forma de las nubes sea siempre la misma.")]
        [SerializeField] private int seed = 2026;

        private Texture2D cookie;
        private float[] noise;
        private Color32[] pixels;
        private float appliedCoverage = -1f;
        private float appliedStrength = -1f;
        private float nextRebuild;
        private Vector2 offset;

        private Light sun;
        private UniversalAdditionalLightData sunData;
        private Texture originalCookie;
        private Vector2 originalCookieSize;
        private Vector2 originalCookieOffset;
        private bool installed;

        private void Start()
        {
            sun = AyniClimateBlend.FindSun();
            if (sun == null) return;

            // Si el sol ya tiene una cookie propia (de otro sistema), se respeta y no se instalan nubes
            if (sun.cookie != null)
            {
                sun = null;
                return;
            }

            sunData = sun.GetComponent<UniversalAdditionalLightData>();
            if (sunData == null) sunData = sun.gameObject.AddComponent<UniversalAdditionalLightData>();

            originalCookie = sun.cookie;
            originalCookieSize = sunData.lightCookieSize;
            originalCookieOffset = sunData.lightCookieOffset;

            BuildNoise();
            cookie = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true)
            {
                name = "Ayni_SombrasNubes",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            pixels = new Color32[textureSize * textureSize];
            RebuildTexture(CurrentCoverage(), CurrentStrength());

            sun.cookie = cookie;
            sunData.lightCookieSize = new Vector2(cloudTileSize, cloudTileSize);
            offset = new Vector2(Random.value, Random.value) * cloudTileSize;
            sunData.lightCookieOffset = offset;
            installed = true;
        }

        private void Update()
        {
            if (!installed || sun == null) return;

            // Viento: más fuerte en el Ocaso de Venganza
            float windSpeed = AyniClimateBlend.Blend(2.2f, 2.4f, 3.2f, 1.8f, 4.2f);
            offset += windDirection.normalized * windSpeed * Time.deltaTime;
            offset.x = Mathf.Repeat(offset.x, cloudTileSize);
            offset.y = Mathf.Repeat(offset.y, cloudTileSize);
            sunData.lightCookieOffset = offset;

            // Regenerar la textura solo cuando el clima cambió lo suficiente (y como mucho dos veces por segundo)
            if (Time.unscaledTime >= nextRebuild)
            {
                float coverage = CurrentCoverage();
                float strength = CurrentStrength();
                if (Mathf.Abs(coverage - appliedCoverage) > 0.015f || Mathf.Abs(strength - appliedStrength) > 0.015f)
                {
                    RebuildTexture(coverage, strength);
                    nextRebuild = Time.unscaledTime + 0.5f;
                }
            }
        }

        private void OnDestroy()
        {
            if (installed && sun != null)
            {
                if (sun.cookie == cookie) sun.cookie = originalCookie;
                if (sunData != null)
                {
                    sunData.lightCookieSize = originalCookieSize;
                    sunData.lightCookieOffset = originalCookieOffset;
                }
            }
            if (cookie != null) Destroy(cookie);
        }

        // ------------------------------------------------------------------------------------------------
        private static float CurrentCoverage() => AyniClimateBlend.Blend(0.38f, 0.40f, 0.48f, 0.52f, 0.72f);
        private static float CurrentStrength() => AyniClimateBlend.Blend(0.34f, 0.36f, 0.38f, 0.28f, 0.52f);

        /// <summary>
        /// Convierte el ruido en luz: 1 = sol pleno, menos de 1 = sombra de nube.
        /// La cobertura decide cuánto cielo tapan las nubes y la fuerza qué tan oscura es su sombra.
        /// </summary>
        private void RebuildTexture(float coverage, float strength)
        {
            float threshold = 1f - coverage;
            for (int i = 0; i < noise.Length; i++)
            {
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.12f, threshold + 0.12f, noise[i]));
                byte v = (byte)Mathf.RoundToInt(Mathf.Lerp(1f, 1f - strength, cloud) * 255f);
                pixels[i] = new Color32(v, v, v, 255);
            }
            cookie.SetPixels32(pixels);
            cookie.Apply(true);
            appliedCoverage = coverage;
            appliedStrength = strength;
        }

        /// <summary>Ruido fractal periódico (se repite sin costuras en ambos ejes), normalizado a 0..1.</summary>
        private void BuildNoise()
        {
            int n = textureSize;
            noise = new float[n * n];
            var rng = new System.Random(seed);

            int[] periods = { 3, 6, 12, 24 };
            float[] amplitudes = { 1f, 0.5f, 0.25f, 0.12f };
            float min = float.MaxValue, max = float.MinValue;

            float[][] lattices = new float[periods.Length][];
            for (int o = 0; o < periods.Length; o++)
            {
                int p = periods[o];
                lattices[o] = new float[p * p];
                for (int k = 0; k < p * p; k++) lattices[o][k] = (float)rng.NextDouble();
            }

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float value = 0f;
                    for (int o = 0; o < periods.Length; o++)
                    {
                        value += PeriodicValueNoise(lattices[o], periods[o], x / (float)n, y / (float)n) * amplitudes[o];
                    }
                    noise[y * n + x] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            float range = Mathf.Max(0.0001f, max - min);
            for (int i = 0; i < noise.Length; i++) noise[i] = (noise[i] - min) / range;
        }

        private static float PeriodicValueNoise(float[] lattice, int period, float u, float v)
        {
            float fx = u * period;
            float fy = v * period;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            int xa = ((x0 % period) + period) % period;
            int ya = ((y0 % period) + period) % period;
            int xb = (xa + 1) % period;
            int yb = (ya + 1) % period;

            float a = lattice[ya * period + xa];
            float b = lattice[ya * period + xb];
            float c = lattice[yb * period + xa];
            float d = lattice[yb * period + xb];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
    }
}
