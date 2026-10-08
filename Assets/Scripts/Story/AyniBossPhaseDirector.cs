using System.Collections;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;

namespace Ayni.Story
{
    /// <summary>
    /// Escena de cambio de fase de un jefe (como en Sifu): cuando el jefe vacía su primera barra, la acción se detiene,
    /// cae a cámara lenta, la cámara lo rodea mientras se levanta y su barra se rellena, ruge y empieza la segunda fase
    /// con un aura de brasas. Yari queda apartado, en guardia, a unos metros. La lógica del jefe está en
    /// EnemyController (PhaseTransitionRoutine); esto pone la cámara, el título, el sonido y el aura.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniBossPhaseDirector : MonoBehaviour
    {
        private EnemyController boss;
        private YariCombatController yari;
        private ThirdPersonSifuCamera gameplayCamera;
        private Camera cam;
        private bool active;
        private float startedAt;
        private float orbitStart;

        private void OnEnable()
        {
            EnemyController.OnPhaseTransitionStarted += Begin;
            EnemyController.OnPhaseStarted += End;
        }

        private void OnDisable()
        {
            EnemyController.OnPhaseTransitionStarted -= Begin;
            EnemyController.OnPhaseStarted -= End;
            if (active) Restore();
        }

        private void Begin(EnemyController enemy)
        {
            boss = enemy;
            active = true;
            startedAt = Time.unscaledTime;

            var player = GameObject.FindGameObjectWithTag("Player");
            yari = player != null ? player.GetComponent<YariCombatController>() : null;
            cam = Camera.main;
            gameplayCamera = cam != null ? cam.GetComponent<ThirdPersonSifuCamera>() : null;

            AyniGameState.CinematicPlaying = true;
            AyniGameState.LockInput();
            if (gameplayCamera != null) gameplayCamera.enabled = false;
            AyniScreenFX.Letterbox(true);
            AyniScreenFX.SetFade(Color.white, 0.75f);
            AyniScreenFX.FadeTo(Color.white, 0f, 0.6f);
            AyniAudio.Play2D("juicio", 0.95f);
            AyniAudio.Play2D("postura_rota", 0.8f, 0.9f);

            // Yari se aparta a unos metros, en guardia, mirando al jefe
            if (yari != null) yari.HoldForCinematic(boss.transform.position, 4.5f);

            Vector3 toCam = cam != null ? cam.transform.position - boss.transform.position : -boss.transform.forward;
            orbitStart = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;

            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            // Cámara lenta mientras cae
            Time.timeScale = 0.4f;
            yield return new WaitForSecondsRealtime(1.1f);
            Time.timeScale = 1f;

            yield return new WaitForSecondsRealtime(1.3f);
            AyniAudio.Play2D("titulo_golpe", 0.85f);
            AyniScreenFX.Title("SEGUNDA FASE", boss != null ? boss.CharacterName + " · Desatado" : "", 3f);
        }

        private void LateUpdate()
        {
            if (!active || boss == null || cam == null) return;

            // Plano: la cámara rodea al jefe mientras cae y se levanta, y termina delante de él (entre él y Yari,
            // algo de lado) para ver el grito de frente; baja y se acerca cuando ruge
            float t = Time.unscaledTime - startedAt;
            float finalAngle = orbitStart;
            if (yari != null)
            {
                Vector3 toYari = yari.transform.position - boss.transform.position;
                finalAngle = Mathf.Atan2(toYari.x, toYari.z) * Mathf.Rad2Deg + 35f;
            }
            float angle = Mathf.LerpAngle(orbitStart, finalAngle, Mathf.SmoothStep(0f, 1f, t / 3.8f)) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(5.2f, 3.2f, Mathf.InverseLerp(2.5f, 4.5f, t));
            float height = Mathf.Lerp(2.4f, 1.15f, Mathf.InverseLerp(0f, 4.5f, t));
            Vector3 center = boss.transform.position;
            Vector3 wanted = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius + Vector3.up * height;
            cam.transform.position = Vector3.Lerp(cam.transform.position, wanted, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            Vector3 look = center + Vector3.up * Mathf.Lerp(0.6f, 1.45f, Mathf.InverseLerp(1.5f, 3.5f, t));
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position),
                                                      1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        }

        private void End(EnemyController enemy, int phase)
        {
            if (!active || enemy != boss) return;
            Restore();
            AyniAudio.Play2D("swing_fuerte", 0.8f, 0.8f);

            // Aura de brasas: el jefe está desatado
            if (enemy.GetComponent<AyniEnrageAura>() == null) enemy.gameObject.AddComponent<AyniEnrageAura>();
        }

        private void Restore()
        {
            active = false;
            Time.timeScale = 1f;
            AyniScreenFX.Letterbox(false);
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
                gameplayCamera.SnapBehindTarget();
            }
            if (yari != null) yari.ReleaseFromCinematic();
            AyniGameState.CinematicPlaying = false;
            AyniGameState.UnlockInput();
        }
    }

    /// <summary>Brasas y un resplandor rojizo alrededor de un jefe en su segunda fase.</summary>
    public class AyniEnrageAura : MonoBehaviour
    {
        private Light glow;
        private ParticleSystem embers;
        private static Material material;

        private void Start()
        {
            var go = new GameObject("Aura_Desatado");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.9f;

            embers = go.AddComponent<ParticleSystem>();
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = embers.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.1f), new Color(1f, 0.75f, 0.25f));
            main.gravityModifier = -0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var emission = embers.emission;
            emission.rateOverTime = 32f;
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, 1.8f, 0.6f);
            var colorOverLife = embers.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.2f), 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            embers.Play();

            var lightGo = new GameObject("Aura_Luz");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.35f, 0.12f);
            glow.range = 4f;
        }

        private void Update()
        {
            var enemy = GetComponent<EnemyController>();
            if (enemy != null && enemy.IsDead)
            {
                if (embers != null) embers.Stop();
                if (glow != null) glow.intensity = Mathf.MoveTowards(glow.intensity, 0f, Time.deltaTime);
                return;
            }
            if (glow != null) glow.intensity = 1.2f + 0.6f * Mathf.PerlinNoise(Time.time * 3f, 0.5f);
        }

        private static Material Material()
        {
            if (material != null) return material;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            material = new Material(Ayni.Combat.CombatFeedback.SpriteMaterial) { mainTexture = tex };
            return material;
        }
    }
}
