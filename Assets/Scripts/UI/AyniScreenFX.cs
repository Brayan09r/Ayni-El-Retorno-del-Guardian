using UnityEngine;

namespace Ayni.UI
{
    /// <summary>
    /// Efectos de pantalla compartidos: fundidos de color, franjas de cine, subtítulos de narración, títulos
    /// y un tinte de ambiente (el resplandor del fuego en el prólogo). Funciona con el tiempo real, así que
    /// no le afecta la cámara lenta. Se crea solo la primera vez que se usa.
    /// </summary>
    public class AyniScreenFX : MonoBehaviour
    {
        private static AyniScreenFX instance;

        // Fundido a pantalla completa
        private Color fadeColor = Color.black;
        private float fadeAlpha, fadeFrom, fadeTo, fadeStart, fadeDuration;

        // Tinte de ambiente (se mezcla por encima, sin tapar)
        private Color tintColor = Color.clear;
        private float tintAlpha, tintTarget;

        // Franjas de cine
        private float letterbox, letterboxTarget;

        // Subtítulo de narración
        private string caption;
        private float captionStart, captionEnd;

        // Título centrado
        private string title, subtitle;
        private float titleStart, titleEnd;

        // Indicación fija abajo a la derecha ("Mantén A para saltar")
        private string hint;

        private GUIStyle captionStyle, titleStyle, subtitleStyle, hintStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private static AyniScreenFX Get()
        {
            if (instance == null)
            {
                var go = new GameObject("Ayni_ScreenFX");
                instance = go.AddComponent<AyniScreenFX>();
            }
            return instance;
        }

        // ───────────────────────── API ─────────────────────────

        /// <summary>Funde la pantalla hacia un color con la opacidad indicada (0 = transparente, 1 = tapada).</summary>
        public static void FadeTo(Color color, float alpha, float duration)
        {
            AyniScreenFX fx = Get();
            fx.fadeColor = color;
            fx.fadeFrom = fx.fadeAlpha;
            fx.fadeTo = Mathf.Clamp01(alpha);
            fx.fadeStart = Time.unscaledTime;
            fx.fadeDuration = Mathf.Max(0.0001f, duration);
        }

        /// <summary>Pone el fundido de golpe, sin transición.</summary>
        public static void SetFade(Color color, float alpha)
        {
            AyniScreenFX fx = Get();
            fx.fadeColor = color;
            fx.fadeAlpha = fx.fadeFrom = fx.fadeTo = Mathf.Clamp01(alpha);
            fx.fadeDuration = 0.0001f;
        }

        public static float FadeAlpha => instance != null ? instance.fadeAlpha : 0f;

        public static void Tint(Color color, float alpha)
        {
            AyniScreenFX fx = Get();
            fx.tintColor = color;
            fx.tintTarget = Mathf.Clamp01(alpha);
        }

        public static void Letterbox(bool on)
        {
            Get().letterboxTarget = on ? 1f : 0f;
        }

        /// <summary>Subtítulo de narración abajo, con aparición y desaparición suaves.</summary>
        public static void Caption(string text, float seconds)
        {
            AyniScreenFX fx = Get();
            fx.caption = text;
            fx.captionStart = Time.unscaledTime;
            fx.captionEnd = Time.unscaledTime + Mathf.Max(0.5f, seconds);
        }

        public static void ClearCaption()
        {
            if (instance != null) instance.caption = null;
        }

        /// <summary>Título grande centrado (nombre de capítulo, de jefe...).</summary>
        public static void Title(string text, string sub, float seconds)
        {
            AyniScreenFX fx = Get();
            fx.title = text;
            fx.subtitle = sub;
            fx.titleStart = Time.unscaledTime;
            fx.titleEnd = Time.unscaledTime + Mathf.Max(0.8f, seconds);
        }

        public static void Hint(string text)
        {
            Get().hint = text;
        }

        /// <summary>Quita todo de golpe (al saltar una escena).</summary>
        public static void ClearAll()
        {
            if (instance == null) return;
            instance.caption = null;
            instance.title = null;
            instance.hint = null;
            instance.letterboxTarget = 0f;
            instance.tintTarget = 0f;
        }

        // ───────────────────────── Dibujo ─────────────────────────

        private void Update()
        {
            float k = Mathf.Clamp01((Time.unscaledTime - fadeStart) / fadeDuration);
            fadeAlpha = Mathf.Lerp(fadeFrom, fadeTo, k * k * (3f - 2f * k));
            tintAlpha = Mathf.MoveTowards(tintAlpha, tintTarget, Time.unscaledDeltaTime * 0.8f);
            letterbox = Mathf.MoveTowards(letterbox, letterboxTarget, Time.unscaledDeltaTime * 1.6f);
        }

        private void EnsureStyles()
        {
            if (captionStyle != null) return;
            captionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, fontStyle = FontStyle.Italic
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46, alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, alignment = TextAnchor.MiddleRight, richText = true
            };
        }

        private void OnGUI()
        {
            GUI.depth = -80;
            EnsureStyles();

            float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2f);
            float w = Screen.width / scale, h = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (tintAlpha > 0.001f) Fill(new Rect(0, 0, w, h), new Color(tintColor.r, tintColor.g, tintColor.b, tintAlpha));

            if (letterbox > 0.001f)
            {
                float bar = h * 0.11f * Mathf.SmoothStep(0f, 1f, letterbox);
                Fill(new Rect(0, 0, w, bar), Color.black);
                Fill(new Rect(0, h - bar, w, bar), Color.black);
            }

            if (fadeAlpha > 0.001f) Fill(new Rect(0, 0, w, h), new Color(fadeColor.r, fadeColor.g, fadeColor.b, fadeAlpha));

            float now = Time.unscaledTime;
            if (!string.IsNullOrEmpty(title) && now < titleEnd)
            {
                float a = Envelope(now, titleStart, titleEnd, 0.8f, 0.9f);
                DrawShadowed(new Rect(0, h * 0.36f, w, 70f), title, titleStyle, new Color(1f, 0.85f, 0.45f, a), a);
                if (!string.IsNullOrEmpty(subtitle))
                {
                    DrawShadowed(new Rect(w * 0.1f, h * 0.36f + 72f, w * 0.8f, 60f), subtitle, subtitleStyle, new Color(1f, 1f, 1f, a * 0.92f), a);
                }
            }

            if (!string.IsNullOrEmpty(caption) && now < captionEnd)
            {
                float a = Envelope(now, captionStart, captionEnd, 0.5f, 0.6f);
                float bottom = letterbox > 0.01f ? h * (1f - 0.11f * letterbox) : h - 40f;
                DrawShadowed(new Rect(w * 0.12f, bottom - 92f, w * 0.76f, 84f), caption, captionStyle, new Color(1f, 0.97f, 0.9f, a), a);
            }

            if (!string.IsNullOrEmpty(hint))
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(now * 3f);
                GUI.color = new Color(1f, 1f, 1f, 0.75f * pulse);
                GUI.Label(new Rect(w - 420f, h - 34f, 400f, 26f), hint, hintStyle);
                GUI.color = Color.white;
            }

            GUI.matrix = previous;
        }

        private static float Envelope(float now, float start, float end, float fadeIn, float fadeOut)
        {
            float a = Mathf.Clamp01((now - start) / fadeIn);
            a = Mathf.Min(a, Mathf.Clamp01((end - now) / fadeOut));
            return a;
        }

        private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color, float alpha)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), StripColors(text), style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = prev;
        }

        /// <summary>La sombra no debe llevar los colores del texto.</summary>
        private static string StripColors(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text, "<color=[^>]*>|</color>", "");
        }

        private static void Fill(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
