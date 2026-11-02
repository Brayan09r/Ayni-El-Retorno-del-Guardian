using UnityEngine;

namespace Ayni.UI
{
    /// <summary>
    /// Estilo visual de la interfaz de Ayni: minimalista y con motivos incas. Paleta de oro, piedra, hueso, ocre y
    /// rojo de grana; anillo y disco de la Illa, la chakana (cruz escalonada andina) y franjas de tocapu (el motivo
    /// escalonado de los textiles incas). Todo se genera por código, sin imágenes del proyecto.
    /// Lo usan el HUD de combate (SifuCombatHUD) y la cuenta de años al caer (AyniDeathAging).
    /// </summary>
    public static class AyniInkaUI
    {
        public static readonly Color Gold = new Color(0.86f, 0.66f, 0.3f);
        public static readonly Color BrightGold = new Color(1f, 0.85f, 0.48f);
        public static readonly Color Stone = new Color(0.08f, 0.065f, 0.055f, 0.82f);
        public static readonly Color Bone = new Color(0.94f, 0.89f, 0.8f);
        public static readonly Color Ochre = new Color(0.84f, 0.5f, 0.18f);
        public static readonly Color Grana = new Color(0.68f, 0.13f, 0.1f);
        public static readonly Color Jade = new Color(0.36f, 0.78f, 0.66f);

        private static Texture2D disc, ring, chakana, tocapu, step;

        // ───────────────────────── Texturas ─────────────────────────

        /// <summary>Círculo lleno con borde suavizado.</summary>
        public static Texture2D Disc => disc != null ? disc : disc = Radial(128, r => Mathf.Clamp01((1f - r) * 64f));

        /// <summary>Anillo fino con borde suavizado.</summary>
        public static Texture2D Ring => ring != null ? ring : ring = Radial(128, r => Mathf.Clamp01((1f - r) * 64f) * Mathf.Clamp01((r - 0.86f) * 64f));

        /// <summary>
        /// Chakana: cruz escalonada de tres peldaños por lado con el hueco circular del centro (el Cusco, ombligo del mundo).
        /// </summary>
        public static Texture2D Chakana
        {
            get
            {
                if (chakana != null) return chakana;
                const int n = 96;
                chakana = NewTexture(n, n);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        // Coordenadas en celdas de una cuadrícula de 7x7 centrada
                        float cx = (x + 0.5f) / n * 7f - 3.5f, cy = (y + 0.5f) / n * 7f - 3.5f;
                        float ax = Mathf.Abs(cx), ay = Mathf.Abs(cy);
                        // Escalones: brazo central de 3 de ancho hasta el borde; luego 5 hasta 2.5; luego 7 hasta 1.5
                        bool inside = (ax <= 1.5f && ay <= 3.5f) || (ay <= 1.5f && ax <= 3.5f) ||
                                      (ax <= 2.5f && ay <= 2.5f);
                        float hole = Mathf.Sqrt(cx * cx + cy * cy);
                        float a = inside && hole > 0.85f ? 1f : 0f;
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                chakana.SetPixels(px);
                chakana.Apply();
                return chakana;
            }
        }

        /// <summary>
        /// Franja de tocapu: escalones que suben y bajan (motivo de grecas incas). Se repite en horizontal.
        /// </summary>
        public static Texture2D Tocapu
        {
            get
            {
                if (tocapu != null) return tocapu;
                const int w = 32, h = 8;
                tocapu = NewTexture(w, h);
                tocapu.wrapMode = TextureWrapMode.Repeat;
                var px = new Color[w * h];
                // Perfil escalonado: alturas por columna de 4 px
                int[] steps = { 1, 3, 5, 7, 5, 3, 1, 0 };
                for (int x = 0; x < w; x++)
                {
                    int top = steps[(x / 4) % steps.Length];
                    for (int y = 0; y < h; y++)
                    {
                        bool on = y <= top && y >= top - 1;
                        px[y * w + x] = new Color(1f, 1f, 1f, on ? 1f : 0f);
                    }
                }
                tocapu.SetPixels(px);
                tocapu.Apply();
                return tocapu;
            }
        }

        /// <summary>Remate escalonado para el extremo de las barras (tres peldaños).</summary>
        public static Texture2D Step
        {
            get
            {
                if (step != null) return step;
                const int n = 12;
                step = NewTexture(n, n);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int level = x / 4; // 0, 1, 2
                        int half = 6 - level * 2;
                        bool on = Mathf.Abs(y - 5.5f) <= half;
                        px[y * n + x] = new Color(1f, 1f, 1f, on ? 1f : 0f);
                    }
                }
                step.SetPixels(px);
                step.Apply();
                return step;
            }
        }

        private static Texture2D Radial(int n, System.Func<float, float> alpha)
        {
            Texture2D t = NewTexture(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    px[y * n + x] = new Color(1f, 1f, 1f, alpha(Mathf.Sqrt(dx * dx + dy * dy)));
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        // ───────────────────────── Piezas ─────────────────────────

        public static void Draw(Rect r, Texture tex, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, tex);
            GUI.color = prev;
        }

        public static void Fill(Rect r, Color color) => Draw(r, Texture2D.whiteTexture, color);

        /// <summary>Franja de tocapu repetida a lo ancho del rectángulo.</summary>
        public static void TocapuBand(Rect r, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            float repeat = r.width / (r.height * 4f);
            GUI.DrawTextureWithTexCoords(r, Tocapu, new Rect(0f, 0f, repeat, 1f));
            GUI.color = prev;
        }

        /// <summary>
        /// Barra fina con remates escalonados de oro en los extremos. <paramref name="ghost"/> es el valor de hace un
        /// instante: el tramo perdido se ve claro y se va consumiendo (así se lee cuánto quitó el golpe).
        /// </summary>
        public static void Bar(Rect r, float ratio, float ghost, Color fill, float alpha = 1f)
        {
            ratio = Mathf.Clamp01(ratio);
            ghost = Mathf.Clamp01(Mathf.Max(ghost, ratio));
            Fill(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), new Color(0f, 0f, 0f, 0.55f * alpha));
            Fill(r, new Color(Stone.r, Stone.g, Stone.b, 0.9f * alpha));
            Fill(new Rect(r.x, r.y, r.width * ghost, r.height), new Color(Bone.r, Bone.g, Bone.b, 0.55f * alpha));
            Fill(new Rect(r.x, r.y, r.width * ratio, r.height), new Color(fill.r, fill.g, fill.b, alpha));
            // Brillo de la mitad superior: le da volumen sin dejar de ser plana
            Fill(new Rect(r.x, r.y, r.width * ratio, Mathf.Max(1f, r.height * 0.35f)), new Color(1f, 1f, 1f, 0.12f * alpha));

            float cap = Mathf.Max(8f, r.height + 6f);
            Color gold = new Color(Gold.r, Gold.g, Gold.b, alpha);
            Rect left = new Rect(r.x - cap, r.center.y - cap / 2f, cap, cap);
            Rect right = new Rect(r.xMax, r.center.y - cap / 2f, cap, cap);
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(180f, left.center);
            Draw(left, Step, gold);
            GUI.matrix = m;
            Draw(right, Step, gold);
        }

        /// <summary>Medallón de la Illa: disco de piedra, anillo de oro, chakana al fondo y el número de años encima.</summary>
        public static void Medallion(Rect r, string number, string caption, float glow, float alpha = 1f)
        {
            if (glow > 0.01f)
            {
                float g = r.width * (0.35f + 0.25f * glow);
                Draw(new Rect(r.x - g, r.y - g, r.width + 2f * g, r.height + 2f * g), Disc,
                     new Color(BrightGold.r, BrightGold.g, BrightGold.b, 0.22f * glow * alpha));
            }
            Draw(r, Disc, new Color(Stone.r, Stone.g, Stone.b, 0.92f * alpha));
            float inset = r.width * 0.16f;
            Draw(new Rect(r.x + inset, r.y + inset, r.width - 2f * inset, r.height - 2f * inset), Chakana,
                 new Color(Gold.r, Gold.g, Gold.b, (0.16f + 0.25f * glow) * alpha));
            Draw(r, Ring, new Color(Gold.r, Gold.g, Gold.b, alpha));
            float inner = r.width * 0.08f;
            Draw(new Rect(r.x + inner, r.y + inner, r.width - 2f * inner, r.height - 2f * inner), Ring,
                 new Color(Gold.r, Gold.g, Gold.b, 0.35f * alpha));

            var num = Style(Mathf.RoundToInt(r.height * 0.36f), FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(new Rect(r.x, r.y - r.height * 0.06f, r.width, r.height), number, num, Bone, alpha);
            if (!string.IsNullOrEmpty(caption))
            {
                var cap = Style(Mathf.Max(9, Mathf.RoundToInt(r.height * 0.1f)), FontStyle.Bold, TextAnchor.MiddleCenter);
                Label(new Rect(r.x, r.y + r.height * 0.6f, r.width, r.height * 0.2f), caption, cap, Gold, alpha);
            }
        }

        // ───────────────────────── Texto ─────────────────────────

        private static readonly System.Collections.Generic.Dictionary<int, GUIStyle> styles =
            new System.Collections.Generic.Dictionary<int, GUIStyle>();

        public static GUIStyle Style(int size, FontStyle fontStyle, TextAnchor anchor)
        {
            int key = size * 100 + (int)fontStyle * 10 + (int)anchor;
            if (!styles.TryGetValue(key, out GUIStyle s) || s == null)
            {
                s = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    fontStyle = fontStyle,
                    alignment = anchor,
                    richText = true,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                styles[key] = s;
            }
            return s;
        }

        /// <summary>Texto con una sombra fina debajo (se lee sobre cualquier fondo sin necesidad de recuadro).</summary>
        public static void Label(Rect r, string text, GUIStyle style, Color color, float alpha = 1f)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), text, style);
            GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
            GUI.Label(r, text, style);
            GUI.color = prev;
        }

        /// <summary>Mayúsculas espaciadas, como las inscripciones: "AMARU" → "A M A R U".</summary>
        public static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder(text.Length * 2);
            string upper = text.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                sb.Append(upper[i]);
                if (i < upper.Length - 1) sb.Append(upper[i] == ' ' ? "  " : " ");
            }
            return sb.ToString();
        }

        /// <summary>Banda horizontal de aviso: piedra translúcida con filetes de oro arriba y abajo.</summary>
        public static void Band(Rect r, float alpha = 1f)
        {
            Fill(r, new Color(Stone.r, Stone.g, Stone.b, 0.78f * alpha));
            Fill(new Rect(r.x, r.y, r.width, 1.5f), new Color(Gold.r, Gold.g, Gold.b, 0.9f * alpha));
            Fill(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), new Color(Gold.r, Gold.g, Gold.b, 0.9f * alpha));
        }
    }
}
