#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Añade a los controladores de los rivales el estado "Ambient_Walk" (caminata con Walk_Forward_InPlace) que usa
    /// AyniRivalAmbient para patrullar antes de la pelea. Se puede repetir: si el estado ya existe no hace nada.
    /// </summary>
    public static class AyniRivalAmbientSetup
    {
        private const string WalkClipPath = "Assets/Art/Characters/Animations/Walk_Forward_InPlace.fbx";
        private static readonly string[] Controllers =
        {
            "Assets/Art/Characters/Rastreador_AnimatorController.controller",
            "Assets/Art/Characters/Saqueador_AnimatorController.controller",
            "Assets/Art/Characters/Guardia_AnimatorController.controller",
            "Assets/Art/Characters/CazadorElite_AnimatorController.controller",
        };

        [MenuItem("Ayni/Rivales/Añadir caminata de patrulla")]
        public static void AddAmbientWalk()
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(WalkClipPath).OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null)
            {
                Debug.LogError("[Ayni Rivales] No se encontró el clip de caminata en " + WalkClipPath);
                return;
            }
            foreach (string path in Controllers)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) continue;
                var machine = controller.layers[0].stateMachine;
                if (machine.states.Any(s => s.state.name == "Ambient_Walk")) continue;
                var state = machine.AddState("Ambient_Walk", new Vector3(520f, 300f, 0f));
                state.motion = clip;
                state.writeDefaultValues = true;
                EditorUtility.SetDirty(controller);
                Debug.Log($"[Ayni Rivales] Ambient_Walk añadido a {controller.name}");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
