using UnityEngine;
using Ayni.Core;
using Ayni.Player;
using Ayni.Story;

namespace Ayni.UI
{
    /// <summary>
    /// Menú de pausa con las opciones de sonido: volumen general, de efectos y de música y ambiente (se guardan entre
    /// partidas), volver a ver el tutorial y reiniciar el nivel. Se abre con Esc o con Menu del mando durante el juego.
    /// Se maneja con el ratón, con las flechas / WASD + Enter, o con el stick / cruceta + A (B o Menu para cerrar).
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniPauseMenu : MonoBehaviour
    {
        private const string MasterKey = "Ayni_VolumenGeneral";
        private const string SfxKey = "Ayni_VolumenEfectos";
        private const string MusicKey = "Ayni_VolumenMusica";

        // Valores de fábrica algo por debajo del máximo: los efectos sintetizados suenan fuertes
        private const float DefaultMaster = 0.75f;
        private const float DefaultSfx = 0.8f;
        private const float DefaultMusic = 0.7f;

        private static readonly string[] Rows = { "Continuar", "Volumen general", "Efectos", "Música y ambiente", "Ver tutorial", "Reiniciar nivel" };

        /// <summary>True mientras el menú está abierto (el juego está en pausa).</summary>
        public static bool IsOpen { get; private set; }

        private float master, sfx, music;
        private int selected;
        private float previousTimeScale = 1f;
        private float nextStickMove;
        private YariCombatController yari;
        private AyniTutorial tutorial;
        private GUIStyle titleStyle, rowStyle, valueStyle, hintStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySavedVolumes()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
            AyniAudio.Volume = PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
            AyniAudio.AmbienceVolume = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
        }

        private void Start()
        {
            master = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
            sfx = PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
            music = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
            Apply();
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            tutorial = GetComponent<AyniTutorial>();
        }

        private void OnDestroy()
        {
            if (IsOpen) Close(false);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                bool canPause = !AyniGameState.InputLocked && !AyniGameState.CinematicPlaying && !AyniJudgment.Active &&
                                (yari == null || (!yari.IsGameOver && !yari.IsBeingRescued));
                if (canPause && AyniInput.Down(AyniInput.Action.Pause)) Open();
                return;
            }

            // Cerrar
            if (AyniInput.Down(AyniInput.Action.Pause) || AyniInput.Down(AyniInput.Action.Back))
            {
                Close(true);
                return;
            }

            // Moverse entre filas y cambiar valores (stick, cruceta, flechas o WASD)
            Vector2 move = AyniInput.Move;
            bool ready = Time.unscaledTime >= nextStickMove;
            if (move.magnitude < 0.4f) nextStickMove = 0f;
            else if (ready)
            {
                nextStickMove = Time.unscaledTime + 0.18f;
                if (Mathf.Abs(move.y) > Mathf.Abs(move.x))
                {
                    selected = (selected + (move.y < 0f ? 1 : Rows.Length - 1)) % Rows.Length;
                    AyniAudio.Play2D("ui_mover", 0.6f);
                }
                else ChangeValue(move.x > 0f ? 0.05f : -0.05f);
            }

            if (AyniInput.Down(AyniInput.Action.Confirm) && !Input.GetMouseButton(0)) Activate(selected);
        }

        private void ChangeValue(float delta)
        {
            switch (selected)
            {
                case 1: master = Mathf.Clamp01(master + delta); break;
                case 2: sfx = Mathf.Clamp01(sfx + delta); break;
                case 3: music = Mathf.Clamp01(music + delta); break;
                default: return;
            }
            Apply();
            AyniAudio.Play2D("golpe_ligero", 0.7f); // para oír el volumen nuevo
        }

        private void Activate(int row)
        {
            switch (row)
            {
                case 0:
                    Close(true);
                    break;
                case 4:
                    Close(true);
                    if (tutorial != null) tutorial.OpenFromMenu();
                    break;
                case 5:
                    Close(false);
                    AyniGameState.ReloadLevel();
                    break;
            }
        }

        private void Apply()
        {
            AudioListener.volume = master;
            AyniAudio.Volume = sfx;
            AyniAudio.AmbienceVolume = music;
            PlayerPrefs.SetFloat(MasterKey, master);
            PlayerPrefs.SetFloat(SfxKey, sfx);
            PlayerPrefs.SetFloat(MusicKey, music);
        }

        private void Open()
        {
            IsOpen = true;
            selected = 0;
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AyniGameState.LockInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AyniAudio.Play2D("ui_mover", 0.7f);
        }

        private void Close(bool sound)
        {
            IsOpen = false;
            PlayerPrefs.Save();
            Time.timeScale = previousTimeScale;
            AyniGameState.UnlockInput();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (sound) AyniAudio.Play2D("ui_confirmar", 0.7f);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, richText = true };
            valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleRight };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true };
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();
            GUI.depth = -120;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2f);
            float w = Screen.width / scale, h = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            Color gold = new Color(1f, 0.82f, 0.3f);
            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.65f));
            float panelW = 560f, rowH = 46f;
            float panelH = 90f + Rows.Length * rowH + 60f;
            var panel = new Rect((w - panelW) / 2f, (h - panelH) / 2f, panelW, panelH);
            Fill(new Rect(panel.x - 2, panel.y - 2, panel.width + 4, panel.height + 4), gold);
            Fill(panel, new Color(0.09f, 0.075f, 0.06f, 0.98f));

            GUI.color = gold;
            GUI.Label(new Rect(panel.x, panel.y + 14f, panel.width, 44f), "PAUSA", titleStyle);
            GUI.color = Color.white;

            float y = panel.y + 76f;
            for (int i = 0; i < Rows.Length; i++)
            {
                var row = new Rect(panel.x + 24f, y, panel.width - 48f, rowH - 6f);
                if (i == selected) Fill(row, new Color(1f, 0.82f, 0.3f, 0.18f));

                // Con el ratón: pasar por encima selecciona, clic activa o arrastra el deslizador
                if (Event.current.type == EventType.MouseMove && row.Contains(Event.current.mousePosition)) selected = i;

                GUI.Label(new Rect(row.x + 12f, row.y, 220f, row.height), (i == selected ? "<b>" : "") + Rows[i] + (i == selected ? "</b>" : ""), rowStyle);

                if (i >= 1 && i <= 3)
                {
                    float value = i == 1 ? master : i == 2 ? sfx : music;
                    var sliderRect = new Rect(row.x + 240f, row.y + row.height * 0.5f - 6f, 200f, 12f);
                    float newValue = GUI.HorizontalSlider(sliderRect, value, 0f, 1f);
                    GUI.Label(new Rect(row.xMax - 70f, row.y, 58f, row.height), Mathf.RoundToInt(newValue * 100f) + " %", valueStyle);
                    if (!Mathf.Approximately(newValue, value))
                    {
                        selected = i;
                        if (i == 1) master = newValue; else if (i == 2) sfx = newValue; else music = newValue;
                        Apply();
                    }
                }
                else if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    selected = i;
                    Activate(i);
                }
                y += rowH;
            }

            bool pad = AyniInput.UsingGamepad;
            string hint = pad ? "Stick: elegir y ajustar   ·   [A] Aceptar   ·   [B] / [Menu] Volver al juego"
                              : "Flechas / WASD: elegir y ajustar   ·   [Enter] Aceptar   ·   [Esc] Volver al juego";
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(panel.x, panel.yMax - 40f, panel.width, 28f), hint, hintStyle);
            GUI.color = Color.white;
            GUI.matrix = previous;
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
