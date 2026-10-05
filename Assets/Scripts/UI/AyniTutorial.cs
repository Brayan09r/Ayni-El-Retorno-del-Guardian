using UnityEngine;
using Ayni.Core;

namespace Ayni.UI
{
    /// <summary>
    /// Tutorial de inicio: pausa la partida y muestra, en tres páginas cortas, los botones de ataque,
    /// los combos y la defensa. Se abre solo al empezar y se puede volver a ver con F1.
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

        private static readonly Page[] Pages =
        {
            new Page
            {
                title = "ATAQUES",
                intro = "Yari pelea con los puños, al estilo de la lucha ritual andina.",
                rows = new[]
                {
                    new Row("Clic Izquierdo", "<b>Golpe ligero.</b> Rápido; se puede encadenar hasta 4 veces."),
                    new Row("Q  o  E", "<b>Golpe pesado.</b> Más lento, pero daña mucho la postura del rival."),
                    new Row("Tab  o  Clic central", "<b>Fijar al rival.</b> Yari lo encara siempre y la cámara los encuadra."),
                },
                tip = "Al golpear, Yari se gira solo hacia el rival más cercano y da un paso hacia él."
            },
            new Page
            {
                title = "COMBOS",
                intro = "Pulsa el siguiente golpe justo cuando conecta el anterior.",
                rows = new[]
                {
                    new Row("Clic · Clic · Clic · Clic", "<b>Cadena de puños:</b> directo, directo, gancho y remate (+30 % de daño)."),
                    new Row("Q · Q · Q", "<b>Cadena pesada:</b> puñetazo descendente, gancho ascendente y codazo."),
                    new Row("Clic · Clic · Q", "<b>Cabezazo.</b> Remate tras 2 o 3 golpes ligeros; castiga la postura."),
                    new Row("Clic ×4 · Q", "<b>Patada de empuje.</b> Remate tras la cadena completa; el que más postura rompe."),
                },
                tip = "Si dejas pasar más de medio segundo sin atacar, el combo vuelve a empezar."
            },
            new Page
            {
                title = "DEFENSA Y JUICIO AYNI",
                intro = "Cuando el rival se tiñe de color, va a atacar: rojo es golpe alto, amarillo es barrido.",
                rows = new[]
                {
                    new Row("Clic Derecho  o  G", "<b>Guardia.</b> Si la levantas justo antes del impacto, desvías el golpe (parry)."),
                    new Row("Guardia + S", "<b>Agacharse:</b> esquiva los golpes altos (aviso rojo)."),
                    new Row("Guardia + W", "<b>Saltar:</b> esquiva los barridos (aviso amarillo)."),
                    new Row("F   /   X", "Con la postura del rival rota: <b>F</b> lo remata, <b>X</b> lo perdona (Ayni)."),
                },
                tip = "Si Yari cae, el Talismán Illa lo resucita... a cambio de años de vida."
            },
        };

        private static bool shownThisSession;

        private bool open;
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
                Open();
            }
        }

        private void OnDestroy()
        {
            if (open) Close();
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
            if (!open)
            {
                if (Input.GetKeyDown(KeyCode.F1) && !AyniGameState.InputLocked) Open();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                if (page < Pages.Length - 1) page++;
                else Close();
            }
            else if (Input.GetKeyDown(KeyCode.Backspace))
            {
                page = Mathf.Max(0, page - 1);
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
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
            Page p = Pages[page];

            // Fondo oscurecido
            Fill(new Rect(0, 0, screenW, screenH), new Color(0f, 0f, 0f, 0.72f));

            const float rowHeight = 46f;
            float panelW = Mathf.Min(820f, screenW - 60f);
            float panelH = 70f + 40f + p.rows.Length * rowHeight + 20f + 44f + 40f;
            var panel = new Rect((screenW - panelW) / 2f, (screenH - panelH) / 2f, panelW, panelH);

            Fill(new Rect(panel.x - 2, panel.y - 2, panel.width + 4, panel.height + 4), gold);
            Fill(panel, new Color(0.09f, 0.075f, 0.06f, 0.98f));

            float y = panel.y + 14f;
            GUI.color = gold;
            GUI.Label(new Rect(panel.x, y, panel.width, 40f), p.title, titleStyle);
            GUI.color = Color.white;
            y += 46f;

            GUI.Label(new Rect(panel.x + 30f, y, panel.width - 60f, 36f), p.intro, introStyle);
            y += 44f;

            float keyW = Mathf.Min(230f, panel.width * 0.34f);
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

            string next = page < Pages.Length - 1 ? "Siguiente" : "¡A pelear!";
            string footer = $"<b>{page + 1} / {Pages.Length}</b>      [Enter / Clic] {next}" +
                            (page > 0 ? "      [Retroceso] Anterior" : "") + "      [Tab] Saltar tutorial";
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
