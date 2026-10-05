#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Ayni.Core;

namespace Ayni.Editor
{
    /// <summary>
    /// Crea en el Input Manager los ejes del mando de Xbox que usa AyniInput: sticks, gatillos y cruceta.
    /// Corre sola la primera vez que se abre el proyecto sin ellos; también desde Ayni > Mando.
    /// Numeración de ejes del mando de Xbox en Windows (Input Manager clásico):
    ///   X = stick izq. horizontal · Y = stick izq. vertical · 3º = gatillos juntos · 4º/5º = stick der.
    ///   6º/7º = cruceta · 9º = LT · 10º = RT
    /// </summary>
    public static class AyniGamepadSetup
    {
        private struct AxisDef
        {
            public string name;
            public int axis;     // índice 0 = "X axis", 1 = "Y axis", 2 = "3rd axis"...
            public bool invert;

            public AxisDef(string name, int axis, bool invert)
            {
                this.name = name;
                this.axis = axis;
                this.invert = invert;
            }
        }

        private static readonly AxisDef[] Axes =
        {
            new AxisDef(AyniInput.AxisLeftX, 0, false),
            new AxisDef(AyniInput.AxisLeftY, 1, true),   // arriba = +1
            new AxisDef(AyniInput.AxisTriggers, 2, false),
            new AxisDef(AyniInput.AxisRightX, 3, false),
            new AxisDef(AyniInput.AxisRightY, 4, true),  // arriba = +1
            new AxisDef(AyniInput.AxisDPadX, 5, false),
            new AxisDef(AyniInput.AxisDPadY, 6, false),
            new AxisDef(AyniInput.AxisLT, 8, false),
            new AxisDef(AyniInput.AxisRT, 9, false),
        };

        [InitializeOnLoadMethod]
        private static void AutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (CountMissing() > 0) ConfigureAxes();
            };
        }

        [MenuItem("Ayni/Mando/Configurar Ejes del Mando Xbox")]
        public static void ConfigureAxes()
        {
            SerializedObject inputManager = LoadInputManager();
            if (inputManager == null)
            {
                Debug.LogWarning("[Ayni Mando] No se encontró ProjectSettings/InputManager.asset.");
                return;
            }

            SerializedProperty axes = inputManager.FindProperty("m_Axes");
            int added = 0, updated = 0;

            foreach (AxisDef def in Axes)
            {
                SerializedProperty entry = Find(axes, def.name);
                if (entry == null)
                {
                    axes.arraySize++;
                    entry = axes.GetArrayElementAtIndex(axes.arraySize - 1);
                    added++;
                }
                else
                {
                    updated++;
                }

                entry.FindPropertyRelative("m_Name").stringValue = def.name;
                entry.FindPropertyRelative("descriptiveName").stringValue = "Mando Xbox (Ayni)";
                entry.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
                entry.FindPropertyRelative("negativeButton").stringValue = "";
                entry.FindPropertyRelative("positiveButton").stringValue = "";
                entry.FindPropertyRelative("altNegativeButton").stringValue = "";
                entry.FindPropertyRelative("altPositiveButton").stringValue = "";
                entry.FindPropertyRelative("gravity").floatValue = 0f;
                entry.FindPropertyRelative("dead").floatValue = 0.05f; // la zona muerta la aplica AyniInput
                entry.FindPropertyRelative("sensitivity").floatValue = 1f;
                entry.FindPropertyRelative("snap").boolValue = false;
                entry.FindPropertyRelative("invert").boolValue = def.invert;
                entry.FindPropertyRelative("type").intValue = 2; // Joystick Axis
                entry.FindPropertyRelative("axis").intValue = def.axis;
                entry.FindPropertyRelative("joyNum").intValue = 0; // cualquier mando
            }

            inputManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[Ayni Mando]</color> Ejes del mando de Xbox listos en el Input Manager ({added} nuevos, {updated} actualizados).");
        }

        private static int CountMissing()
        {
            SerializedObject inputManager = LoadInputManager();
            if (inputManager == null) return 0;
            SerializedProperty axes = inputManager.FindProperty("m_Axes");
            int missing = 0;
            foreach (AxisDef def in Axes)
            {
                if (Find(axes, def.name) == null) missing++;
            }
            return missing;
        }

        private static SerializedObject LoadInputManager()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            return assets != null && assets.Length > 0 ? new SerializedObject(assets[0]) : null;
        }

        private static SerializedProperty Find(SerializedProperty axes, string name)
        {
            for (int i = 0; i < axes.arraySize; i++)
            {
                SerializedProperty entry = axes.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("m_Name").stringValue == name) return entry;
            }
            return null;
        }

        [MenuItem("Ayni/Mando/Probar Mando en Play (F9)")]
        private static void ToggleTester()
        {
            bool enabled = PlayerPrefs.GetInt(Ayni.UI.AyniGamepadTester.PrefKey, 0) == 0;
            PlayerPrefs.SetInt(Ayni.UI.AyniGamepadTester.PrefKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[Ayni Mando] Panel de prueba del mando: {(enabled ? "ACTIVADO (también con F9 en Play)" : "desactivado")}");
        }

        [MenuItem("Ayni/Mando/Probar Mando en Play (F9)", true)]
        private static bool ToggleTesterValidate()
        {
            Menu.SetChecked("Ayni/Mando/Probar Mando en Play (F9)", PlayerPrefs.GetInt(Ayni.UI.AyniGamepadTester.PrefKey, 0) == 1);
            return true;
        }
    }
}
#endif
