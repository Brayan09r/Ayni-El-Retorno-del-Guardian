using System;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;

namespace Ayni.Combat
{
    /// <summary>
    /// Sensación de impacto del combate: micro-pausa al conectar (hitstop), empujón y sacudida de cámara y destello
    /// en el punto de contacto. Se crea solo la primera vez que se usa; no hay que añadirlo a la escena.
    /// Los valores de cada tipo de golpe están en los métodos LightHit / HeavyHit / Finisher / Parry / Block / PlayerHurt.
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        /// <summary>Multiplicadores globales por si se quiere todo más suave o más fuerte.</summary>
        public static float HitstopScale = 1f;
        public static float ShakeScale = 1f;

        /// <summary>La cámara se suscribe: (amplitud en metros, duración en segundos).</summary>
        public static event Action<float, float> OnShake;
        /// <summary>
        /// La cámara se suscribe: empujón en la dirección del golpe (dirección en el mundo, fuerza en metros).
        /// Con dirección cero la cámara elige un lado al azar.
        /// </summary>
        public static event Action<Vector3, float> OnKick;
        /// <summary>La cámara se suscribe: acercamiento de remate durante los segundos indicados.</summary>
        public static event Action<float> OnFinisherCamera;

        private const float HitstopTimeScale = 0.03f;

        private class FlashInstance
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public bool active;
            public float start;
            public float life;
            public float size;
            public float roll;
            public Color color;
        }

        private static CombatFeedback instance;
        private static Sprite burstSprite;
        private static Material spriteMaterial;
        private static Texture2D softDot;
        private static readonly Dictionary<string, float> lastGrunt = new Dictionary<string, float>();
        private ParticleSystem debris;

        private readonly List<FlashInstance> flashes = new List<FlashInstance>();
        private float hitstopUntil;
        private bool inHitstop;
        private float slowUntil;
        private float slowScale = 1f;
        private bool inSlowMotion;
        private Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            burstSprite = null;
            spriteMaterial = null;
            softDot = null;
            lastGrunt.Clear();
            OnShake = null;
            OnKick = null;
            OnFinisherCamera = null;
        }

        private static CombatFeedback Get()
        {
            if (instance == null)
            {
                var go = new GameObject("Ayni_CombatFeedback");
                instance = go.AddComponent<CombatFeedback>();
            }
            return instance;
        }

        // ───────────────────────── Tipos de impacto ─────────────────────────

        // La dirección (opcional) es hacia dónde va el golpe: la cámara se empuja hacia ese lado

        public static void LightHit(Vector3 point, Vector3 direction = default)
        {
            AyniAudio.Play("golpe_ligero", point, 0.85f);
            Hitstop(0.045f);
            Kick(direction, 0.03f);
            Shake(0.015f, 0.10f);
            AyniRumble.Pulse(0.12f, 0.35f, 0.08f);
            Flash(point, Color.white, 0.75f);
            Burst(point, direction, 5, 2.2f, new Color(1f, 0.95f, 0.85f, 0.85f), 0.035f);
        }

        public static void HeavyHit(Vector3 point, Vector3 direction = default)
        {
            AyniAudio.Play("golpe_fuerte", point, 0.95f);
            Hitstop(0.085f);
            Kick(direction, 0.07f);
            Shake(0.035f, 0.16f);
            AyniRumble.Pulse(0.55f, 0.4f, 0.15f);
            Flash(point, new Color(1f, 0.95f, 0.8f), 1.15f, 0.14f);
            Burst(point, direction, 12, 3.2f, new Color(1f, 0.92f, 0.78f, 0.9f), 0.05f);
            Burst(point, direction, 6, 1.6f, new Color(0.62f, 0.5f, 0.38f, 0.7f), 0.09f);
            Grunt("dolor_rival", point, 0.8f, 0.7f);
        }

        public static void Finisher(Vector3 point, Vector3 direction = default)
        {
            AyniAudio.Play("remate", point, 1f, 0.03f);
            Hitstop(0.14f);
            Kick(direction, 0.12f);
            Shake(0.06f, 0.24f);
            AyniRumble.Pulse(0.9f, 0.55f, 0.28f);
            Flash(point, new Color(1f, 0.85f, 0.5f), 1.7f, 0.18f);
            Burst(point, direction, 22, 4.2f, new Color(1f, 0.9f, 0.7f, 0.95f), 0.055f);
            Burst(point, direction, 10, 2f, new Color(0.62f, 0.5f, 0.38f, 0.75f), 0.12f);
            Grunt("dolor_rival", point, 0.95f, 1f);
        }

        public static void Parry(Vector3 point, Vector3 direction = default)
        {
            AyniAudio.Play("parry", point, 0.95f, 0.04f);
            Hitstop(0.10f);
            Kick(direction, 0.045f);
            Shake(0.02f, 0.12f);
            AyniRumble.Pulse(0.2f, 0.75f, 0.1f);
            Flash(point, new Color(1f, 0.9f, 0.4f), 1.25f, 0.16f);
        }

        public static void Block(Vector3 point, Vector3 direction = default)
        {
            AyniAudio.Play("bloqueo", point, 0.8f);
            Hitstop(0.03f);
            Kick(direction, 0.03f);
            Shake(0.012f, 0.08f);
            AyniRumble.Pulse(0.3f, 0.15f, 0.08f);
            Flash(point, new Color(0.8f, 0.9f, 1f), 0.55f);
        }

        /// <summary>Esquiva lograda: destello frío donde pasa el golpe y un instante a cámara lenta.</summary>
        public static void Avoid(Vector3 point)
        {
            AyniAudio.Play("esquiva", point, 0.9f, 0.05f);
            SlowMotion(0.22f, 0.3f);
            Shake(0.02f, 0.08f);
            AyniRumble.Pulse(0f, 0.25f, 0.06f);
            Flash(point, new Color(0.65f, 0.9f, 1f, 0.85f), 0.9f, 0.16f);
        }

        public static void PlayerHurt(Vector3 point, Vector3 direction = default)
        {
            // Golpe recibido: más grave que los que da Yari
            AyniAudio.Play("golpe_fuerte", point, 1f, 0.04f, 0.82f);
            Hitstop(0.06f);
            Kick(direction, 0.08f);
            Shake(0.045f, 0.18f);
            AyniRumble.Pulse(0.8f, 0.3f, 0.22f);
            Flash(point, new Color(1f, 0.3f, 0.25f), 0.95f, 0.14f);
            Burst(point, direction, 8, 2.4f, new Color(0.95f, 0.85f, 0.72f, 0.85f), 0.045f);
            Grunt("dolor_yari", point, 0.85f, 0.9f);
        }

        // ───────────────────────── Gritos y polvo ─────────────────────────

        /// <summary>
        /// Grito de esfuerzo o de dolor (clips <c>esfuerzo_yari</c>, <c>dolor_yari</c>, <c>dolor_rival</c>). No suena más de
        /// uno de cada tipo cada <paramref name="gap"/> segundos y, con <paramref name="chance"/> &lt; 1, no siempre: si cada
        /// golpe gritara, cansaría.
        /// </summary>
        public static void Grunt(string clip, Vector3 point, float volume, float chance = 1f, float gap = 0.35f)
        {
            if (lastGrunt.TryGetValue(clip, out float last) && Time.unscaledTime - last < gap) return;
            if (UnityEngine.Random.value > chance) return;
            lastGrunt[clip] = Time.unscaledTime;
            AyniAudio.Play(clip, point, volume, 0.06f);
        }

        /// <summary>
        /// Chispas de sudor y polvo que saltan del punto de contacto hacia donde va el golpe. Se congelan con la
        /// micro-pausa del golpe (van en tiempo de juego), que es lo que da la sensación de fotograma clavado.
        /// </summary>
        public static void Burst(Vector3 point, Vector3 direction, int count, float speed, Color color, float size)
        {
            ParticleSystem ps = Get().Debris();
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 v = dir * 0.85f + UnityEngine.Random.insideUnitSphere * 0.75f + Vector3.up * 0.4f;
                p.position = point;
                p.velocity = v.normalized * speed * UnityEngine.Random.Range(0.45f, 1.15f);
                p.startSize = size * UnityEngine.Random.Range(0.6f, 1.35f);
                p.startLifetime = UnityEngine.Random.Range(0.22f, 0.5f);
                p.startColor = color;
                ps.Emit(p, 1);
            }
        }

        private ParticleSystem Debris()
        {
            if (debris != null) return debris;
            var go = new GameObject("ImpactDebris");
            go.transform.SetParent(transform, false);
            debris = go.AddComponent<ParticleSystem>();
            debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = debris.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.3f;
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule emission = debris.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = debris.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule fade = debris.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule shrink = debris.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            ParticleSystem.LimitVelocityOverLifetimeModule drag = debris.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = new Material(SpriteMaterial) { mainTexture = SoftDot() };
            debris.Play();
            return debris;
        }

        /// <summary>Punto redondo de borde suave (textura de las chispas).</summary>
        private static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int size = 32;
            softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            softDot.SetPixels(pixels);
            softDot.Apply();
            return softDot;
        }

        // ───────────────────────── Piezas ─────────────────────────

        /// <summary>Congela casi por completo la acción durante unos milisegundos reales.</summary>
        public static void Hitstop(float seconds)
        {
            seconds *= HitstopScale;
            if (seconds <= 0f) return;

            CombatFeedback fb = Get();
            fb.hitstopUntil = Mathf.Max(fb.hitstopUntil, Time.unscaledTime + seconds);
            if (!fb.inHitstop && !AyniGameState.InputLocked)
            {
                fb.inHitstop = true;
                Time.timeScale = HitstopTimeScale;
            }
        }

        /// <summary>Cámara lenta durante unos segundos reales (más suave que el hitstop, para las esquivas).</summary>
        public static void SlowMotion(float seconds, float timeScale)
        {
            seconds *= HitstopScale;
            if (seconds <= 0f) return;

            CombatFeedback fb = Get();
            fb.slowUntil = Mathf.Max(fb.slowUntil, Time.unscaledTime + seconds);
            fb.slowScale = Mathf.Clamp(timeScale, 0.05f, 1f);
            if (!fb.inHitstop && !AyniGameState.InputLocked)
            {
                fb.inSlowMotion = true;
                Time.timeScale = fb.slowScale;
            }
        }

        public static void Shake(float amplitude, float duration)
        {
            Get();
            OnShake?.Invoke(amplitude * ShakeScale, duration);
        }

        /// <summary>Empujón de cámara en la dirección del golpe, que vuelve con un pequeño rebote.</summary>
        public static void Kick(Vector3 direction, float strength)
        {
            Get();
            OnKick?.Invoke(direction, strength * ShakeScale);
        }

        public static void FinisherCamera(float duration)
        {
            Get();
            OnFinisherCamera?.Invoke(duration);
        }

        /// <summary>Destello en estrella que crece y se desvanece en el punto de contacto.</summary>
        public static void Flash(Vector3 worldPoint, Color color, float size, float life = 0.12f)
        {
            CombatFeedback fb = Get();
            FlashInstance f = fb.GetFreeFlash();

            Camera c = fb.GetCamera();
            if (c != null)
            {
                // Hacia la cámara, para que no quede tapado dentro de los cuerpos
                Vector3 toCam = c.transform.position - worldPoint;
                if (toCam.sqrMagnitude > 0.01f) worldPoint += toCam.normalized * 0.35f;
            }

            f.transform.position = worldPoint;
            f.active = true;
            f.start = Time.unscaledTime;
            f.life = Mathf.Max(0.03f, life);
            f.size = size;
            f.color = color;
            f.roll = UnityEngine.Random.Range(0f, 360f);
            f.renderer.enabled = true;
            fb.UpdateFlash(f);
        }

        /// <summary>Material sin iluminación para destellos y estelas (no depende de ningún asset del proyecto).</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        spriteMaterial = new Material(shader);
                    }
                    else
                    {
                        var temp = new GameObject("tmp_sprite_material");
                        spriteMaterial = temp.AddComponent<SpriteRenderer>().sharedMaterial;
                        Destroy(temp);
                    }
                }
                return spriteMaterial;
            }
        }

        private FlashInstance GetFreeFlash()
        {
            for (int i = 0; i < flashes.Count; i++)
            {
                if (!flashes[i].active) return flashes[i];
            }

            var go = new GameObject("ImpactFlash");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetBurstSprite();
            sr.sharedMaterial = SpriteMaterial;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.sortingOrder = 100;
            sr.enabled = false;

            var f = new FlashInstance { transform = go.transform, renderer = sr };
            flashes.Add(f);
            return f;
        }

        private Camera GetCamera()
        {
            if (cam == null) cam = Camera.main;
            return cam;
        }

        private void Update()
        {
            if (inHitstop && Time.unscaledTime >= hitstopUntil)
            {
                inHitstop = false;
                if (!AyniGameState.InputLocked)
                {
                    // Si queda cámara lenta pendiente (una esquiva), se continúa con ella
                    inSlowMotion = Time.unscaledTime < slowUntil;
                    Time.timeScale = inSlowMotion ? slowScale : 1f;
                }
            }
            else if (inSlowMotion && !inHitstop && Time.unscaledTime >= slowUntil)
            {
                inSlowMotion = false;
                if (!AyniGameState.InputLocked) Time.timeScale = 1f;
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < flashes.Count; i++)
            {
                if (flashes[i].active) UpdateFlash(flashes[i]);
            }
        }

        private void UpdateFlash(FlashInstance f)
        {
            float k = (Time.unscaledTime - f.start) / f.life;
            if (k >= 1f)
            {
                f.active = false;
                f.renderer.enabled = false;
                return;
            }

            float scale = f.size * Mathf.Lerp(0.45f, 1.3f, Mathf.Sqrt(k));
            f.transform.localScale = new Vector3(scale, scale, scale);

            Color c = f.color;
            c.a = 1f - k * k;
            f.renderer.color = c;

            Camera camera = GetCamera();
            if (camera != null)
            {
                f.transform.rotation = camera.transform.rotation * Quaternion.Euler(0f, 0f, f.roll);
            }
        }

        private void OnDestroy()
        {
            if ((inHitstop || inSlowMotion) && !AyniGameState.InputLocked) Time.timeScale = 1f;
            if (instance == this) instance = null;
        }

        /// <summary>Estrella de 8 puntas con núcleo brillante, generada por código.</summary>
        private static Sprite GetBurstSprite()
        {
            if (burstSprite != null) return burstSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 8f);
                    float falloff = Mathf.Clamp01(1f - r);
                    float core = Mathf.Clamp01(1f - r * 3.2f);
                    float alpha = Mathf.Max(core, falloff * falloff * (0.18f + 0.82f * rays));

                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha * 1.4f));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);

            burstSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return burstSprite;
        }
    }
}
