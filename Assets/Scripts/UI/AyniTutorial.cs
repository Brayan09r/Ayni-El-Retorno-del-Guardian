using UnityEngine;
using Ayni.Core;
using Ayni.Story;

namespace Ayni.UI
{
    /// <summary>
    /// Tutorial de inicio: pausa la partida y muestra, en páginas cortas, los botones de ataque, los combos,
    /// la defensa estilo Sifu y el Juicio Ayni. Los botones que enseña son los del dispositivo en uso
    /// (mando de Xbox o teclado y ratón) y cambian solos si el jugador cambia de uno a otro.
    /// Se abre solo al empezar (después del prólogo) y se puede volver a ver con F1 / View.
    /// SifuCombatHUD lo añade automáticamente al mismo objeto.
    /// </summary>
    public class AyniTutorial : MonoBehaviour
    {
        private struct Row
        {
            public string keys;
            public string text;
            public Row(string keys, string text) { this.keys = keys; this.text = text; }
        }

        private struct Page
        {
            public string title;
            public string intro;
            public Row[] rows;
            public string tip;
        }

        private static string K(AyniInput.Action action) => AyniInput.Label(action);

        /// <summary>Las páginas se construyen con los botones del dispositivo en uso.</summary>
        private static Page[] BuildPages()
        {
            bool pad = AyniInput.UsingGamepad;
            string light = K(AyniInput.Action.LightAttack);
            string heavy = K(AyniInput.Action.HeavyAttack);
            string guard = K(AyniInput.Action.Guard);
            string lightShort = pad ? "X" : "Clic";
            string heavyShort = pad ? "Y" : "Q";
            string guardShort = pad ? "LB" : "Guardia";

            return new[]
            {
                new Page
                {
                    title = "ATAQUES",
                    intro = "Yari pelea con los puños, al estilo de la lucha ritual andina (Rumi Maki).",
                    rows = new[]
                    {
                        new Row(light, "<b>Golpe ligero.</b> Rápido; se puede encadenar hasta 4 veces."),
                        new Row(heavy, "<b>Golpe pesado.</b> Más lento, pero daña mucho la postura del rival."),
                        new Row(K(AyniInput.Action.LockOn), "<b>Fijar al rival.</b> Yari lo encara siempre y la cámara los encuadra."),
                        new Row(pad ? "Stick + golpe" : "WASD + golpe", "<b>Elegir rival.</b> El golpe va al que señalas; si está a tu espalda, <b>patada hacia atrás</b>."),
                        new Row(K(AyniInput.Action.Sprint), "<b>Correr.</b> " + (pad ? "Mantén RT (o pulsa el stick izquierdo)." : "Mantén Shift.")),
                    },
                    tip = "Sin dirección, Yari se gira solo hacia el rival más cercano y da un paso hacia él."
                },
                new Page
                {
                    title = "COMBOS",
                    intro = "Pulsa el siguiente golpe justo cuando conecta el anterior.",
                    rows = new[]
                    {
                        new Row($"{lightShort} · {lightShort} · {lightShort} · {lightShort}", "<b>Cadena de puños:</b> directo, directo, gancho y remate (+30 % de daño)."),
                        new Row($"{heavyShort} · {heavyShort} · {heavyShort}", "<b>Cadena pesada:</b> puñetazo descendente, gancho ascendente y codazo."),
                        new Row($"{lightShort} · {lightShort} · {heavyShort}", "<b>Cabezazo.</b> Remate tras 2 o 3 golpes ligeros; castiga la postura."),
                        new Row($"{lightShort} ×4 · {heavyShort}", "<b>Patada de empuje.</b> Remate tras la cadena completa; el que más postura rompe."),
                    },
                    tip = "Si dejas pasar más de medio segundo sin atacar, el combo vuelve a empezar."
                },
                new Page
                {
                    title = "DEFENSA ESTILO SIFU",
                    intro = "En guardia Yari <b>se planta</b>: no camina. La dirección sirve para esquivar sin moverse del sitio.\nRojo = golpe alto · amarillo = barrido.",
                    rows = new[]
                    {
                        new Row(guard + " (mantener)", "<b>Guardia.</b> Si la subes justo antes del impacto, desvías el golpe (parry)."),
                        new Row(pad ? $"{guardShort} + Stick ↓" : "Guardia + S", "<b>Agacharse:</b> esquiva los golpes altos (aviso rojo)."),
                        new Row(pad ? $"{guardShort} + Stick ↑ / A" : "Guardia + W / Espacio", "<b>Saltito:</b> esquiva los barridos (aviso amarillo)."),
                        new Row(pad ? $"{guardShort} + Stick ← →" : "Guardia + A / D", "<b>Balanceo:</b> esquiva cualquier golpe, pero con menos margen."),
                    },
                    tip = "Tras esquivar o desviar, tu siguiente golpe es un CONTRAATAQUE que rompe mucho más la postura."
                },
                new Page
                {
                    title = "JUICIO AYNI Y LA ILLA",
                    intro = "Cuando la postura del rival se rompe, el combate se detiene y tú decides.",
                    rows = new[]
                    {
                        new Row(K(AyniInput.Action.Execute), "<b>Venganza:</b> lo rematas. La violencia deja la tierra marchita."),
                        new Row(K(AyniInput.Action.Mercy), "<b>Ayni:</b> lo desarmas y perdonas. Restauras el equilibrio y la Illa te cobra menos años."),
                        new Row("Caer en combate", "El Talismán Illa resucita a Yari... a cambio de años de vida."),
                        new Row("Caer a la quebrada", "La Illa te devuelve al camino, pero te cuesta vida."),
                    },
                    tip = pad ? "Botón rojo (B) = Venganza · Botón verde (A) = Ayni." : "Más adelante también podrás jugar con un mando de Xbox."
                },
            };
        }

        private static bool shownThisSession;

        private bool open;
        private bool pendingOpen;
        private int page;
        private float previousTimeScale = 1f;

        private GUIStyle titleStyle, introStyle, keyStyle, textStyle, tipStyle, footerStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            shownThisSession = false;
        }

        private void Start()
        {
            // Solo la primera vez de cada partida (no se repite al reintentar tras un Game Over)
            if (!shownThisSession)
            {
                shownThisSession = true;
                // Si el prólogo está en marcha, el tutorial espera a que termine
                if (AyniPrologue.IsRunning) pendingOpen = true;
                else Open();
            }
        }

        private void OnDestroy()
        {
            if (open) Close();
        }

        /// <summary>Abre el tutorial desde el menú de pausa.</summary>
        public void OpenFromMenu()
        {
            if (!open) Open();
        }

        private void Open()
        {
            open = true;
            page = 0;
            previousTimeScale = 1f;
            Time.timeScale = 0f;
            AyniGameState.LockInput();
        }

        private void Close()
        {
            open = false;
            Time.timeScale = previousTimeScale;
            AyniGameState.UnlockInput();
        }

        private void Update()
        {
            if (pendingOpen)
            {
                if (!AyniPrologue.IsRunning)
                {
                    pendingOpen = false;
                    Open();
                }
                return;
            }

            if (!open)
            {
                if (AyniInput.Down(AyniInput.Action.Tutorial) && !AyniGameState.InputLocked) Open();
                return;
            }

            int count = BuildPages().Length;
            if (AyniInput.Down(AyniInput.Action.Confirm))
            {
                if (page < count - 1)
                {
                    page++;
                    AyniAudio.Play2D("ui_mover", 0.7f);
                }
                else
                {
                    AyniAudio.Play2D("ui_confirmar", 0.8f);
                    Close();
                }
            }
            else if (AyniInput.Down(AyniInput.Action.Back))
            {
                page = Mathf.Max(0, page - 1);
                AyniAudio.Play2D("ui_mover", 0.6f, 0.85f);
            }
            else if (AyniInput.Down(AyniInput.Action.Skip) || AyniInput.Down(AyniInput.Action.Tutorial))
            {
                Close();
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            introStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
            keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = true };
            tipStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
            footerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true };
        }

        private void OnGUI()
        {
            if (!open) return;
            EnsureStyles();
            GUI.depth = -100; // por encima del HUD

            // Todo el panel se escala con la altura de la pantalla para que se lea igual en cualquier resolución
            float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2f);
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            Color gold = new Color(1f, 0.82f, 0.3f);
            Page[] pages = BuildPages();
            page = Mathf.Clamp(page, 0, pages.Length - 1);
            Page p = pages[page];

            // Fondo oscurecido
            Fill(new Rect(0, 0, screenW, screenH), new Color(0f, 0f, 0f, 0.72f));

            const float rowHeight = 46f;
            float panelW = Mathf.Min(860f, screenW - 60f);
            float panelH = 70f + 52f + p.rows.Length * rowHeight + 20f + 44f + 40f;
            var panel = new Rect((screenW - panelW) / 2f, (screenH - panelH) / 2f, panelW, panelH);

            Fill(new Rect(panel.x - 2, panel.y - 2, panel.width + 4, panel.height + 4), gold);
            Fill(panel, new Color(0.09f, 0.075f, 0.06f, 0.98f));

            float y = panel.y + 14f;
            GUI.color = gold;
            GUI.Label(new Rect(panel.x, y, panel.width, 40f), p.title, titleStyle);
            GUI.color = Color.white;
            y += 46f;

            GUI.Label(new Rect(panel.x + 30f, y, panel.width - 60f, 48f), p.intro, introStyle);
            y += 56f;

            float keyW = Mathf.Min(250f, panel.width * 0.34f);
            foreach (Row row in p.rows)
            {
                var keyRect = new Rect(panel.x + 28f, y + 4f, keyW, rowHeight - 10f);
                Fill(keyRect, gold);
                Fill(new Rect(keyRect.x + 1.5f, keyRect.y + 1.5f, keyRect.width - 3f, keyRect.height - 3f), new Color(0.17f, 0.14f, 0.1f, 1f));
                GUI.color = gold;
                GUI.Label(keyRect, row.keys, keyStyle);
                GUI.color = Color.white;

                GUI.Label(new Rect(keyRect.xMax + 18f, y, panel.xMax - keyRect.xMax - 46f, rowHeight), row.text, textStyle);
                y += rowHeight;
            }

            y += 12f;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            GUI.Label(new Rect(panel.x + 30f, y, panel.width - 60f, 40f), p.tip, tipStyle);
            GUI.color = Color.white;

            string next = page < pages.Length - 1 ? "Siguiente" : "¡A pelear!";
            string confirm = AyniInput.UsingGamepad ? "A" : "Enter / Clic";
            string footer = $"<b>{page + 1} / {pages.Length}</b>      [{confirm}] {next}" +
                            (page > 0 ? $"      [{K(AyniInput.Action.Back)}] Anterior" : "") +
                            $"      [{K(AyniInput.Action.Skip)}] Saltar tutorial";
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(panel.x, panel.yMax - 36f, panel.width, 28f), footer, footerStyle);
            GUI.color = Color.white;

            GUI.matrix = previousMatrix;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
