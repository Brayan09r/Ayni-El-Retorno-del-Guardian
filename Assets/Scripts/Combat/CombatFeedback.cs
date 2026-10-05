using System;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;

namespace Ayni.Combat
{
    /// <summary>
    /// Sensación de impacto del combate: micro-pausa al conectar (hitstop), sacudida de cámara y destello
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

        private readonly List<FlashInstance> flashes = new List<FlashInstance>();
        private float hitstopUntil;
        private bool inHitstop;
        private Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            burstSprite = null;
            spriteMaterial = null;
            OnShake = null;
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

        public static void LightHit(Vector3 point)
        {
            Hitstop(0.045f);
            Shake(0.03f, 0.10f);
            Flash(point, Color.white, 0.75f);
        }

        public static void HeavyHit(Vector3 point)
        {
            Hitstop(0.085f);
            Shake(0.07f, 0.16f);
            Flash(point, new Color(1f, 0.95f, 0.8f), 1.15f, 0.14f);
        }

        public static void Finisher(Vector3 point)
        {
            Hitstop(0.14f);
            Shake(0.13f, 0.24f);
            Flash(point, new Color(1f, 0.85f, 0.5f), 1.7f, 0.18f);
        }

        public static void Parry(Vector3 point)
        {
            Hitstop(0.10f);
            Shake(0.05f, 0.12f);
            Flash(point, new Color(1f, 0.9f, 0.4f), 1.25f, 0.16f);
        }

        public static void Block(Vector3 point)
        {
            Hitstop(0.03f);
            Shake(0.025f, 0.08f);
            Flash(point, new Color(0.8f, 0.9f, 1f), 0.55f);
        }

        public static void PlayerHurt(Vector3 point)
        {
            Hitstop(0.06f);
            Shake(0.09f, 0.18f);
            Flash(point, new Color(1f, 0.3f, 0.25f), 0.95f, 0.14f);
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

        public static void Shake(float amplitude, float duration)
        {
            Get();
            OnShake?.Invoke(amplitude * ShakeScale, duration);
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
            if (inHitstop && !AyniGameState.InputLocked) Time.timeScale = 1f;
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
