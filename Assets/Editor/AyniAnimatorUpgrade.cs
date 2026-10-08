#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Añade al Animator de Yari los estados de las animaciones generadas por la Fragua (AyniAnimationForge):
    /// esquivas en el sitio, caída en el aire, caída de espaldas y aterrizaje pesado.
    /// No regenera el Animator: solo quita y vuelve a poner estos estados, así se puede repetir sin romper nada.
    /// YariCombatController los reproduce por código con CrossFade (si no existen, usa los estados antiguos).
    /// </summary>
    public static class AyniAnimatorUpgrade
    {
        private const string ControllerPath = "Assets/Art/Characters/Yari_AnimatorController.controller";
        private const string AmaruControllerPath = "Assets/Art/Characters/Amaru_AnimatorController.controller";

        // Nombres de estado que usa YariCombatController
        public const string StateAvoidDuck = "Avoid_Duck";
        public const string StateAvoidJump = "Avoid_Jump";
        public const string StateAvoidSwayL = "Avoid_SwayL";
        public const string StateAvoidSwayR = "Avoid_SwayR";
        public const string StateFallLoop = "Fall_Loop";
        public const string StateFallBack = "Fall_Back";
        public const string StateLandHard = "Land_Hard";
        public const string StateBackKick = "Atk_BackKick";

        [MenuItem("Ayni/Animaciones/3. Añadir Estados Nuevos al Animator de Yari")]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogWarning("[Ayni Animator] No existe " + ControllerPath + ". Ejecuta antes Ayni > 1. Generar Animator Controller.");
                return;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState guard = Find(sm, "Guard_Stance");
            AnimatorState combat = Find(sm, "Combat_Locomotion");
            if (guard == null || combat == null)
            {
                Debug.LogWarning("[Ayni Animator] El Animator no tiene Guard_Stance o Combat_Locomotion; regenéralo con Ayni > 1.");
                return;
            }

            int added = 0;
            added += AddState(sm, StateAvoidDuck, AyniAnimationForge.AvoidDuck, guard, 1.0f, 0.88f, false);
            added += AddState(sm, StateAvoidJump, AyniAnimationForge.AvoidJump, guard, 1.0f, 0.88f, false);
            added += AddState(sm, StateAvoidSwayL, AyniAnimationForge.AvoidSwayL, guard, 1.0f, 0.88f, false);
            added += AddState(sm, StateAvoidSwayR, AyniAnimationForge.AvoidSwayR, guard, 1.0f, 0.88f, false);
            added += AddState(sm, StateFallLoop, AyniAnimationForge.FallLoop, null, 1.0f, 0f, true);
            added += AddState(sm, StateFallBack, AyniAnimationForge.FallBack, null, 1.0f, 0f, false);
            added += AddState(sm, StateLandHard, AyniAnimationForge.LandHard, combat, 1.0f, 0.9f, false);
            // Patada hacia atrás: la lanza el combo por código, con la velocidad del parámetro AttackSpeed
            AnimatorState backKick = null;
            added += AddState(sm, StateBackKick, AyniAnimationForge.BackKick, combat, 1.0f, 0.92f, false);
            backKick = Find(sm, StateBackKick);
            if (backKick != null)
            {
                backKick.speedParameterActive = true;
                backKick.speedParameter = "AttackSpeed";
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[Ayni Animator]</color> {added} estados nuevos en el Animator de Yari (esquivas en el sitio, caídas y aterrizaje).");

            ApplyAmaru();
        }

        /// <summary>Amaru el Cazador: salto hacia atrás antes de la ráfaga de dardos (usa el saltito generado; es Humanoid).</summary>
        public static void ApplyAmaru()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AmaruControllerPath);
            if (controller == null) return;
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = Find(sm, "Idle");
            if (idle == null) return;

            int added = AddState(sm, "Hunter_Leap", AyniAnimationForge.AvoidJump, idle, 0.85f, 0.9f, false);
            // Grito al empezar la segunda fase
            added += AddState(sm, "Phase_Roar", AyniAnimationForge.Roar, idle, 1f, 0.95f, false);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            if (added > 0) Debug.Log("<color=green>[Ayni Animator]</color> Amaru: estado Hunter_Leap (salto del Cazador) añadido.");
        }

        private static AnimatorState Find(AnimatorStateMachine sm, string name)
        {
            foreach (ChildAnimatorState child in sm.states)
            {
                if (child.state.name == name) return child.state;
            }
            return null;
        }

        private static int AddState(AnimatorStateMachine sm, string stateName, string clipName, AnimatorState returnTo,
                                    float speed, float exitTime, bool loop)
        {
            AnimationClip clip = AyniAnimationForge.LoadGenerated(clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[Ayni Animator] Falta el clip {clipName}: ejecuta Ayni > Animaciones > 1. Generar Caídas y Esquivas.");
                return 0;
            }

            AnimatorState old = Find(sm, stateName);
            Vector3 position = new Vector3(650f, 40f + 55f * sm.states.Length, 0f);
            if (old != null)
            {
                foreach (ChildAnimatorState child in sm.states)
                {
                    if (child.state == old) position = child.position;
                }
                sm.RemoveState(old);
            }

            AnimatorState state = sm.AddState(stateName, position);
            state.motion = clip;
            state.speed = speed;
            state.writeDefaultValues = true;
            // Los clips generados no traen metas de IK de los pies: en estos estados no se apoyan (ver FootIK)
            state.tag = Ayni.Combat.FootIK.NoIKTag;

            if (returnTo != null && !loop)
            {
                AnimatorStateTransition back = state.AddTransition(returnTo);
                back.hasExitTime = true;
                back.exitTime = exitTime;
                back.duration = 0.12f;
            }
            return 1;
        }
    }
}
#endif
