#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Ayni.Story;

namespace Ayni.Editor
{
    /// <summary>Opciones de la historia en el menú Ayni > Historia.</summary>
    public static class AyniStoryMenu
    {
        private const string PrologueMenu = "Ayni/Historia/Prólogo al dar Play";

        [MenuItem(PrologueMenu)]
        private static void TogglePrologue()
        {
            bool enabled = PlayerPrefs.GetInt(AyniPrologue.PrefKey, 1) == 0;
            PlayerPrefs.SetInt(AyniPrologue.PrefKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[Ayni Historia] Prólogo \"La Noche de las Cenizas\" al dar Play: {(enabled ? "ACTIVADO" : "desactivado (directo al combate)")}");
        }

        [MenuItem(PrologueMenu, true)]
        private static bool TogglePrologueValidate()
        {
            Menu.SetChecked(PrologueMenu, PlayerPrefs.GetInt(AyniPrologue.PrefKey, 1) == 1);
            return true;
        }

        /// <summary>Para el puente de agentes: activa (1) o desactiva (0) el prólogo sin abrir el menú.</summary>
        public static void SetPrologue(int on)
        {
            PlayerPrefs.SetInt(AyniPrologue.PrefKey, on != 0 ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
#endif
