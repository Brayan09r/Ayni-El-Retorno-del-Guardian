using UnityEngine;
using Ayni.Core;

namespace Ayni.UI
{
    /// <summary>
    /// Panel para comprobar el mando en Play: mandos conectados, sticks, gatillos y qué acción del juego se activa.
    /// Se abre y cierra con F9 (o desde Ayni > Mando > Probar Mando en Play). SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniGamepadTester : MonoBehaviour
    {
        public const string PrefKey = "AyniGamepadTester";

        private bool visible;
        private GUIStyle style;

        private void Start()
        {
            visible = PlayerPrefs.GetInt(PrefKey, 0) == 1;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            GUI.depth = -50;
            var area = new Rect(Screen.width - 330f, 20f, 310f, 420f);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = prev;

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));
            GUILayout.Label("<b>PRUEBA DEL MANDO</b>  (F9 cerrar)", style);

            string[] pads = Input.GetJoystickNames();
            int connected = 0;
            foreach (string n in pads) if (!string.IsNullOrEmpty(n)) connected++;
            GUILayout.Label(connected > 0
                ? $"<color=#7CFC7C>Mandos conectados: {connected}</color>"
                : "<color=#FF8080>No se detecta ningún mando</color>", style);
            foreach (string n in pads)
            {
                if (!string.IsNullOrEmpty(n)) GUILayout.Label("  · " + n, style);
            }

            GUILayout.Label($"Dispositivo en uso: <b>{(AyniInput.UsingGamepad ? "MANDO" : "Teclado y ratón")}</b>", style);
            Vector2 m = AyniInput.Move;
            Vector2 l = AyniInput.LookStick;
            GUILayout.Label($"Mover (stick izq.):  {m.x:+0.00;-0.00} , {m.y:+0.00;-0.00}", style);
            GUILayout.Label($"Cámara (stick der.): {l.x:+0.00;-0.00} , {l.y:+0.00;-0.00}", style);
            GUILayout.Space(6);

            foreach (AyniInput.Action action in System.Enum.GetValues(typeof(AyniInput.Action)))
            {
                bool on = AyniInput.Held(action);
                GUILayout.Label($"{(on ? "<color=#FFD24A>●</color>" : "○")}  {action}  <color=#AAAAAA>[{AyniInput.Label(action)}]</color>", style);
            }
            GUILayout.EndArea();
        }
    }
}
