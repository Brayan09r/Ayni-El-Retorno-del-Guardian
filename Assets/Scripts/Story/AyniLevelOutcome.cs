using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;

namespace Ayni.Story
{
    /// <summary>
    /// Desenlace del Nivel 1 (Biblia, 5.1 y 6.3) cuando se resuelve el Juicio Ayni del jefe:
    ///   Ayni (perdón)  → la lluvia apaga las llamas y vuelve a brotar el queñual (AyniReforestation): ODS 15, el Antisuyo empieza a sanar.
    ///   Venganza       → cae ceniza, el cielo se oscurece: el bosque sigue ardiendo y la Hucha pesa sobre Yari.
    /// Termina con la tarjeta de "Nivel completado" y sus datos (edad, caídas, decisión, tiempo).
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniLevelOutcome : MonoBehaviour
    {
        private static readonly Color RainColor = new Color(0.7f, 0.82f, 1f, 0.55f);

        private bool running;
        private bool showCard;
        private bool mercy;
        private string bossName = "Amaru el Cazador";
        private float levelStart;

        private YariCombatController yari;
        private IllaTalismanSystem talisman;
        private ThirdPersonSifuCamera gameplayCamera;
        private Camera cam;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private GUIStyle cardTitle, cardText, cardSmall;

        private void Start()
        {
            levelStart = Time.time;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.GetComponent<YariCombatController>();
                talisman = player.GetComponent<IllaTalismanSystem>();
            }
            cam = Camera.main;
            if (cam != null) gameplayCamera = cam.GetComponent<ThirdPersonSifuCamera>();
            if (AyniPurificationManager.Instance != null) AyniPurificationManager.Instance.OnCombatResolved += HandleResolved;
        }

        private void OnDestroy()
        {
            if (AyniPurificationManager.Instance != null) AyniPurificationManager.Instance.OnCombatResolved -= HandleResolved;
        }

        private void HandleResolved(EnemyController enemy, bool spared)
        {
            if (running || enemy == null || !enemy.IsBoss) return;
            running = true;
            mercy = spared;
            bossName = enemy.CharacterName;
            StartCoroutine(Ending(enemy));
        }

        private IEnumerator Ending(EnemyController boss)
        {
            // Deja que termine el remate o el gesto de perdón
            yield return new WaitForSecondsRealtime(2.6f);

            float levelTime = Time.time - levelStart;
            AyniGameState.CinematicPlaying = true;
            AyniGameState.LockInput();
            Time.timeScale = 1f;
            if (yari != null) yari.CinematicControl = true;
            if (gameplayCamera != null) gameplayCamera.enabled = false;
            AyniScreenFX.Letterbox(true);

            Vector3 yariPos = yari != null ? yari.transform.position : boss.transform.position;
            Vector3 center = Vector3.Lerp(yariPos, boss.transform.position, 0.5f);

            if (mercy)
            {
                AyniScreenFX.Tint(new Color(0.12f, 0.32f, 0.2f), 0.14f);
                spawned.Add(BuildRain(center));
                AyniReforestation.Begin(center, new[] { yariPos, boss.transform.position }, spawned);
            }
            else
            {
                AyniScreenFX.Tint(new Color(0.25f, 0.12f, 0.08f), 0.34f);
                spawned.Add(BuildAsh(center));
            }

            string[] lines = mercy
                ? new[]
                {
                    $"{bossName} deja caer sus hachas de piedra. Por primera vez en años, alguien le ofrece la mano.",
                    "La lluvia apaga las llamas del Antisuyo. Los comuneros liberados vuelven a sembrar queñuales.",
                }
                : new[]
                {
                    $"{bossName} cae. Nadie queda para detener las sierras de bronce.",
                    "El fuego sigue avanzando por el Antisuyo. La Hucha, la energía pesada, se aferra al alma de Yari.",
                };

            // La cámara gira lentamente alrededor de los dos mientras cambia el paisaje
            float duration = 11f;
            float t = 0f;
            float startAngle = cam != null ? Mathf.Atan2(cam.transform.position.x - center.x, cam.transform.position.z - center.z) * Mathf.Rad2Deg : 0f;
            int line = -1;
            while (t < duration)
            {
                int wanted = t < 5.4f ? 0 : 1;
                if (wanted != line)
                {
                    line = wanted;
                    AyniScreenFX.Caption(lines[line], 5f);
                }
                if (cam != null)
                {
                    float angle = (startAngle + t * 9f) * Mathf.Deg2Rad;
                    float radius = Mathf.Lerp(4.5f, 7.5f, t / duration);
                    Vector3 pos = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius + Vector3.up * Mathf.Lerp(1.8f, 3.6f, t / duration);
                    cam.transform.position = Vector3.Lerp(cam.transform.position, pos, 2f * Time.unscaledDeltaTime);
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                        Quaternion.LookRotation(center + Vector3.up * 1.1f - cam.transform.position), 3f * Time.unscaledDeltaTime);
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (mercy) AyniScreenFX.Title("ODS 15 · VIDA DE ECOSISTEMAS TERRESTRES", "El Antisuyo empieza a sanar. Yari restauró el Ayni.", 4.5f);
            else AyniScreenFX.Title("LA VÍA DE LA VENGANZA", "El bosque sigue ardiendo. Yari carga con la Hucha.", 4.5f);
            yield return new WaitForSecondsRealtime(4.6f);

            AyniScreenFX.FadeTo(Color.black, 0.82f, 1.2f);
            yield return new WaitForSecondsRealtime(1.2f);
            showCard = true;
            cardTime = levelTime;

            // Tarjeta final: volver a jugar o seguir explorando el nivel
            while (true)
            {
                if (AyniInput.Down(AyniInput.Action.Confirm))
                {
                    AyniGameState.ReloadLevel();
                    yield break;
                }
                if (AyniInput.Down(AyniInput.Action.Skip) || AyniInput.Down(AyniInput.Action.Back))
                {
                    break;
                }
                yield return null;
            }

            // Seguir explorando: el paisaje se queda como quedó tras la decisión
            showCard = false;
            AyniScreenFX.FadeTo(Color.black, 0f, 0.8f);
            AyniScreenFX.Letterbox(false);
            if (yari != null) yari.CinematicControl = false;
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
                gameplayCamera.SnapBehindTarget();
            }
            AyniGameState.CinematicPlaying = false;
            AyniGameState.UnlockInput();
        }

        private float cardTime;

        private void OnGUI()
        {
            if (!showCard) return;
            if (cardTitle == null)
            {
                cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                cardText = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                cardSmall = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = true };
            }
            GUI.depth = -90;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2f);
            float w = Screen.width / scale, h = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            Color gold = new Color(1f, 0.82f, 0.35f);
            GUI.color = gold;
            GUI.Label(new Rect(0, h * 0.16f, w, 50f), "NIVEL 1 COMPLETADO", cardTitle);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, h * 0.16f + 52f, w, 30f), "Antisuyo — Las Selvas Asediadas", cardSmall);

            int age = talisman != null ? talisman.CurrentAge : 20;
            int falls = talisman != null ? talisman.TotalResurrections : 0;
            int minutes = Mathf.FloorToInt(cardTime / 60f);
            int seconds = Mathf.FloorToInt(cardTime % 60f);
            string choice = mercy
                ? "<color=#7CFC9A>Ayni — perdonaste a " + bossName + "</color>"
                : "<color=#FF7A6A>Venganza — ejecutaste a " + bossName + "</color>";

            float y = h * 0.36f;
            GUI.Label(new Rect(w * 0.15f, y, w * 0.7f, 30f), "Decisión: " + choice, cardText); y += 36f;
            GUI.Label(new Rect(w * 0.15f, y, w * 0.7f, 30f), $"Edad de Yari: <b>{age} años</b>   ·   Veces que cayó: <b>{falls}</b>   ·   Tiempo: <b>{minutes}:{seconds:00}</b>", cardText); y += 36f;
            GUI.Label(new Rect(w * 0.15f, y, w * 0.7f, 30f), mercy
                ? "El Antisuyo vuelve a respirar. El primer paso del camino a casa está hecho."
                : "Ganaste el combate, pero el Antisuyo sigue en llamas. El final verdadero exige Ayni.", cardText); y += 60f;

            GUI.color = new Color(0.85f, 0.9f, 1f);
            GUI.Label(new Rect(w * 0.1f, y, w * 0.8f, 30f), "Continuará: <b>Nivel 2 — Las Terrazas Marchitas del Contisuyo</b> (ODS 2: Hambre Cero)", cardText);
            y += 60f;

            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f);
            GUI.color = new Color(1f, 1f, 1f, pulse);
            string again = AyniInput.UsingGamepad ? "A" : "Enter";
            string stay = AyniInput.UsingGamepad ? "B" : "Esc";
            GUI.Label(new Rect(0, y, w, 30f), $"[{again}] Volver a jugar el nivel      [{stay}] Seguir explorando", cardSmall);
            GUI.color = Color.white;
            GUI.matrix = previous;
        }

        // ───────────────────────── Paisaje tras la decisión ─────────────────────────

        private static Material particleMaterial;

        private static Material ParticleMaterial()
        {
            if (particleMaterial != null) return particleMaterial;
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
            particleMaterial = new Material(Ayni.Combat.CombatFeedback.SpriteMaterial) { mainTexture = tex };
            return particleMaterial;
        }

        /// <summary>Lluvia fina sobre la zona del combate (gotas estiradas que caen rápido).</summary>
        private GameObject BuildRain(Vector3 center)
        {
            var go = new GameObject("Desenlace_Lluvia");
            go.transform.position = center + Vector3.up * 14f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.startColor = RainColor;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 1800f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(46f, 1f, 46f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.6f, -0.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-15f, -12f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = ParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return go;
        }

        /// <summary>Ceniza gris y brasas que caen despacio.</summary>
        private GameObject BuildAsh(Vector3 center)
        {
            var go = new GameObject("Desenlace_Ceniza");
            go.transform.position = center + Vector3.up * 9f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.33f, 0.32f, 0.9f), new Color(1f, 0.45f, 0.15f, 0.9f));
            main.gravityModifier = 0.04f;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 160f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 2f, 40f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Simulate(5f, true, true);
            ps.Play();
            return go;
        }
    }
}
