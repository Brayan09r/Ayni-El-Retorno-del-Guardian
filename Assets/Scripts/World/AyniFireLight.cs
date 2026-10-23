using UnityEngine;

namespace Ayni.World
{
    /// <summary>
    /// Controlador de iluminación orgánica para antorchas, braseros ceremoniales y fogones incas (Luz_Fogon / FirePit).
    /// Simula el parpadeo natural del fuego mediante dos octavas de ruido Perlin (intensidad, temperatura de color y
    /// micro-desplazamiento), genera brasas ascendentes opcionales y aplica atenuación por distancia (Distance Culling)
    /// para respetar el límite de luces adicionales por objeto de Universal Render Pipeline (URP).
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniFireLight : MonoBehaviour
    {
        /// <summary>
        /// Multiplicador global para atenuar las llamas exteriores cuando llueve en el desenlace Ayni (Perdón)
        /// o avivarlas durante el desenlace de Venganza.
        /// </summary>
        public static float GlobalOutdoorMultiplier { get; set; } = 1f;

        [Header("Configuración de Luz")]
        [SerializeField] private float baseIntensity = 3.2f;
        [SerializeField] private float baseRange = 8.5f;
        [SerializeField] private float flickerAmplitude = 0.28f;
        [SerializeField] private float flickerSpeed = 3.6f;
        [SerializeField] private float positionJitter = 0.025f;
        [SerializeField] private bool isOutdoor = true;

        [Header("Paleta Térmica del Fuego")]
        [SerializeField] private Color warmFlameColor = new Color(1.0f, 0.68f, 0.32f, 1f);
        [SerializeField] private Color deepEmberColor = new Color(1.0f, 0.42f, 0.14f, 1f);

        [Header("Optimización URP (Distancia a Cámara)")]
        [SerializeField] private float fadeStartDistance = 36f;
        [SerializeField] private float cullDistance = 52f;

        [Header("Partículas de Fuego y Brasas")]
        [SerializeField] private bool spawnFireParticles = false;
        [SerializeField] private float flameScale = 1.0f;

        private Light pointLight;
        private Vector3 initialLocalPos;
        private float noiseSeed;
        private ParticleSystem emberSystem;
        private static Material sharedFlameMat;

        public void Configure(float intensity, float range, bool outdoor, bool withParticles, float scale = 1f)
        {
            baseIntensity = intensity;
            baseRange = range;
            isOutdoor = outdoor;
            spawnFireParticles = withParticles;
            flameScale = scale;

            EnsureLight();
            if (spawnFireParticles && emberSystem == null)
            {
                BuildFlameParticles();
            }
        }

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            noiseSeed = Mathf.Abs(transform.position.x * 1.37f + transform.position.z * 2.91f) % 100f;
            EnsureLight();
            if (spawnFireParticles && emberSystem == null)
            {
                BuildFlameParticles();
            }
        }

        private void EnsureLight()
        {
            pointLight = GetComponent<Light>();
            if (pointLight == null) pointLight = gameObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = baseRange;
            pointLight.intensity = baseIntensity;
            pointLight.color = Color.Lerp(deepEmberColor, warmFlameColor, 0.6f);
            pointLight.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (pointLight == null) return;

            Camera cam = Camera.main;
            float distFactor = 1f;
            if (cam != null && cullDistance > fadeStartDistance)
            {
                float sqrDist = (cam.transform.position - transform.position).sqrMagnitude;
                if (sqrDist >= cullDistance * cullDistance)
                {
                    if (pointLight.enabled) pointLight.enabled = false;
                    return;
                }
                if (!pointLight.enabled) pointLight.enabled = true;

                float dist = Mathf.Sqrt(sqrDist);
                if (dist > fadeStartDistance)
                {
                    distFactor = 1f - Mathf.InverseLerp(fadeStartDistance, cullDistance, dist);
                }
            }

            float t = Time.time * flickerSpeed;
            float n1 = Mathf.PerlinNoise(t + noiseSeed, noiseSeed * 0.37f);
            float n2 = Mathf.PerlinNoise(t * 2.3f + noiseSeed * 1.9f, 4.2f);
            float combined = (n1 * 0.68f + n2 * 0.32f) * 2f - 1f; // -1..1

            float weatherMult = isOutdoor ? GlobalOutdoorMultiplier : 1f;
            pointLight.intensity = Mathf.Max(0f, baseIntensity * (1f + combined * flickerAmplitude) * distFactor * weatherMult);
            pointLight.color = Color.Lerp(deepEmberColor, warmFlameColor, Mathf.Clamp01(0.5f + combined * 0.45f));

            if (positionJitter > 0.001f)
            {
                float ox = (Mathf.PerlinNoise(t * 1.7f, noiseSeed + 1.1f) - 0.5f) * 2f * positionJitter;
                float oy = (Mathf.PerlinNoise(t * 2.1f, noiseSeed + 5.3f) - 0.5f) * 2f * positionJitter;
                float oz = (Mathf.PerlinNoise(t * 1.5f, noiseSeed + 9.7f) - 0.5f) * 2f * positionJitter;
                transform.localPosition = initialLocalPos + new Vector3(ox, oy, oz);
            }
        }

        private void BuildFlameParticles()
        {
            Transform existing = transform.Find("Brasas_Fuego");
            if (existing != null)
            {
                emberSystem = existing.GetComponent<ParticleSystem>();
                return;
            }

            var go = new GameObject("Brasas_Fuego");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.down * 0.18f;

            emberSystem = go.AddComponent<ParticleSystem>();
            emberSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = emberSystem.main;
            main.duration = 3f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * flameScale, 1.1f * flameScale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f * flameScale, 0.22f * flameScale);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.78f, 0.32f, 0.9f),
                new Color(1f, 0.38f, 0.10f, 0.85f));
            main.gravityModifier = -0.18f;
            main.maxParticles = 45;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = emberSystem.emission;
            emission.rateOverTime = 24f;

            var shape = emberSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.14f * flameScale;

            var colorOverLife = emberSystem.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.42f), 0f),
                    new GradientColorKey(new Color(1f, 0.48f, 0.15f), 0.55f),
                    new GradientColorKey(new Color(0.65f, 0.18f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.2f),
                    new GradientAlphaKey(0.55f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLife.color = grad;

            var sizeOverLife = emberSystem.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetSharedFlameMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            emberSystem.Play();
        }

        private static Material GetSharedFlameMaterial()
        {
            if (sharedFlameMat != null) return sharedFlameMat;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Ayni_FlameGlow",
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            sharedFlameMat = new Material(shader)
            {
                name = "Ayni_FlameParticle_Mat",
                mainTexture = tex
            };
            if (sharedFlameMat.HasProperty("_BaseMap")) sharedFlameMat.SetTexture("_BaseMap", tex);
            if (sharedFlameMat.HasProperty("_Surface")) sharedFlameMat.SetFloat("_Surface", 1f);
            if (sharedFlameMat.HasProperty("_Blend")) sharedFlameMat.SetFloat("_Blend", 2f); // Additive
            return sharedFlameMat;
        }
    }
}
