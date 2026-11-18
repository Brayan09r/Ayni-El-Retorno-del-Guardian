using UnityEngine;
using UnityEngine.Rendering;

namespace Ayni.World
{
    /// <summary>
    /// Partículas de luz en el aire alrededor de la cámara, según el clima (<see cref="AyniClimateBlend"/>):
    ///  - Hora Dorada / Combate: motas doradas de polvo y polen flotando en el sol bajo de la tarde.
    ///  - Fase 2 de Amaru:      brasas naranjas que suben desde la plaza en llamas.
    ///  - Amanecer Ayni:        pocas motas frías y brillantes en el aire lavado por la lluvia.
    ///  - Ocaso de Venganza:    algunas brasas sueltas (la ceniza que cae ya la pone AyniLevelOutcome).
    /// Las partículas viven en coordenadas del mundo: el volumen de emisión sigue a la cámara, pero cada mota queda
    /// en su lugar, así se percibe la profundidad al moverse. Es puramente visual.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniLightMotes : MonoBehaviour
    {
        [Header("Volumen alrededor de la cámara")]
        [SerializeField] private Vector3 dustVolume = new Vector3(26f, 9f, 26f);
        [SerializeField] private Vector3 emberVolume = new Vector3(30f, 2f, 30f);

        [Header("Densidad máxima (partículas por segundo)")]
        [SerializeField] private float maxDustRate = 24f;
        [SerializeField] private float maxEmberRate = 32f;

        private static readonly Color DustGold = new Color(1f, 0.82f, 0.5f, 0.85f);
        private static readonly Color DustCool = new Color(0.86f, 0.95f, 1f, 0.9f);
        private static readonly Color EmberOrange = new Color(1f, 0.55f, 0.18f, 1f);

        private static Material sharedMat;

        private ParticleSystem dust;
        private ParticleSystem embers;
        private Transform follow;

        private void Start()
        {
            dust = CreateDust();
            embers = CreateEmbers();
        }

        private void LateUpdate()
        {
            if (follow == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                follow = cam.transform;
            }

            // El volumen de polvo rodea a la cámara; el de brasas, un poco por debajo (salen del suelo)
            dust.transform.position = follow.position;
            embers.transform.position = follow.position + Vector3.down * 3f;

            float dustRate = AyniClimateBlend.Blend(1f, 0.6f, 0f, 0.4f, 0f) * maxDustRate;
            float emberRate = AyniClimateBlend.Blend(0f, 0f, 1f, 0f, 0.25f) * maxEmberRate;
            float coolness = AyniClimateBlend.Weight(AyniLightingPreset.AmanecerAyni);

            var dustEmission = dust.emission;
            dustEmission.rateOverTime = dustRate;
            var dustMain = dust.main;
            dustMain.startColor = Color.Lerp(DustGold, DustCool, coolness);

            var emberEmission = embers.emission;
            emberEmission.rateOverTime = emberRate;
        }

        // ------------------------------------------------------------------------------------------------
        private ParticleSystem CreateDust()
        {
            ParticleSystem ps = NewSystem("Motas_Doradas", 400);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = DustGold;
            main.gravityModifier = -0.004f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = dustVolume;

            // Deriva lenta y caprichosa, como polvo en un rayo de sol
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(0.15f, 0.8f, 1f);

            ps.Play();
            return ps;
        }

        private ParticleSystem CreateEmbers()
        {
            ParticleSystem ps = NewSystem("Brasas_Flotantes", 300);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = EmberOrange;
            main.gravityModifier = -0.06f; // el calor las empuja hacia arriba

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emberVolume;
            shape.rotation = new Vector3(-90f, 0f, 0f); // emiten hacia arriba

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.4f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.12f), 0.5f),
                    new GradientColorKey(new Color(0.55f, 0.12f, 0.04f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.7f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            ps.Play();
            return ps;
        }

        private ParticleSystem NewSystem(string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.playOnAwake = false;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.02f; // las motas lejanas no crecen en pantalla
            return ps;
        }

        private static Gradient FadeInOut(float fadeIn, float fadeOutStart, float peak)
        {
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peak, fadeIn),
                    new GradientAlphaKey(peak, fadeOutStart),
                    new GradientAlphaKey(0f, 1f)
                });
            return grad;
        }

        /// <summary>Material aditivo de partículas URP con un punto de luz suave.</summary>
        private static Material GetMaterial()
        {
            if (sharedMat != null) return sharedMat;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Ayni_MotaLuz",
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
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * a));
                }
            }
            tex.Apply();

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            sharedMat = new Material(shader) { name = "Ayni_MotasLuz_Mat", mainTexture = tex };
            if (sharedMat.HasProperty("_BaseMap")) sharedMat.SetTexture("_BaseMap", tex);
            // Transparente aditivo (mismas propiedades que fija el inspector de URP)
            if (sharedMat.HasProperty("_Surface")) sharedMat.SetFloat("_Surface", 1f);
            if (sharedMat.HasProperty("_Blend")) sharedMat.SetFloat("_Blend", 2f);
            if (sharedMat.HasProperty("_SrcBlend")) sharedMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (sharedMat.HasProperty("_DstBlend")) sharedMat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (sharedMat.HasProperty("_ZWrite")) sharedMat.SetFloat("_ZWrite", 0f);
            sharedMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedMat.renderQueue = (int)RenderQueue.Transparent;
            return sharedMat;
        }
    }
}
