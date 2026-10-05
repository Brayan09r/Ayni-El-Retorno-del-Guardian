#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ayni.Player;

namespace Ayni.Editor
{
    /// <summary>
    /// Ajustes de movimiento de Yari. Solo se ejecuta desde el menú (o desde el puente de agentes).
    ///
    /// Las velocidades salen de lo que cubren los pasos de cada animación con el cuerpo de Yari (mide 1.4 m), medido por
    /// AyniAttackTimingBaker, y de cuánto se pueden acelerar las piernas sin que se vean a cámara rápida:
    ///
    ///   clip            cubre a 1x   ciclo    tope de cadencia   cubre como mucho
    ///   correr          3.70 m/s     0.53 s   1.05x              3.9 m/s
    ///   sprint          3.93 m/s     0.53 s   1.30x              5.1 m/s
    ///   agachado        0.95 m/s     1.03 s   1.40x              1.3 m/s
    ///   lateral izq.    3.0 m/s      0.67 s   1.30x
    ///   lateral der.    2.3 m/s      0.67 s   1.30x              3.0 m/s
    ///   hacia atrás     1.66 m/s     0.80 s   1.30x              2.2 m/s
    ///
    /// Con la Illa joven Yari va un 15 % más rápido: estas son las velocidades base con las que los pies patinan
    /// como mucho un 10 % en ese caso (velocidad base = cubre como mucho × 1.10 / 1.15). Si se cambia un clip,
    /// hay que volver a medir (Ayni.Editor.AyniLocomotionProbe.MeasureClips) y rehacer esta cuenta.
    /// </summary>
    public static class AyniTuning
    {
        private const float JogSpeed = 3.6f;
        private const float SprintSpeed = 4.9f;
        private const float CrouchSpeed = 1.1f;
        private const float LockStrafeSpeed = 2.4f;
        private const float LockBackSpeed = 2.0f;
        private const float MaxRunCadence = 1.05f;
        // El clip de sprint ("Two Cycle Sprint") ya da 3.8 pasos por segundo; a 1.3x son casi 5, lo de un velocista
        private const float MaxSprintCadence = 1.3f;
        private const float MaxCrouchCadence = 1.4f;
        // Salto: antes subía 1.6 m con gravedad lunar (-9.81) y tardaba más de un segundo en caer
        private const float JumpHeight = 1.1f;
        private const float Gravity = -20f;
        // Distancia de combate: antes se pegaban a 2 m, con los puños lejos del cuerpo del rival
        private const float AttackRange = 1.7f;
        private const float LungeStopDistance = 1.15f;

        [MenuItem("Ayni/5. Aplicar Ajustes Recomendados (movimiento y combate)")]
        public static void ApplyMoveSpeeds()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Ajustes] Sal del modo Play antes de aplicar los ajustes.");
                return;
            }

            GameObject yari = GameObject.Find("Yari_Hero");
            var ctrl = yari != null ? yari.GetComponent<YariCombatController>() : null;
            if (ctrl == null)
            {
                Debug.LogWarning("[Ayni Ajustes] No se encontró Yari_Hero con YariCombatController en la escena.");
                return;
            }

            var so = new SerializedObject(ctrl);
            float oldJog = so.FindProperty("baseMoveSpeed").floatValue;
            float oldSprint = so.FindProperty("sprintSpeed").floatValue;
            so.FindProperty("baseMoveSpeed").floatValue = JogSpeed;
            so.FindProperty("sprintSpeed").floatValue = SprintSpeed;
            so.FindProperty("crouchSpeed").floatValue = CrouchSpeed;
            so.FindProperty("lockStrafeSpeed").floatValue = LockStrafeSpeed;
            so.FindProperty("lockBackSpeed").floatValue = LockBackSpeed;
            so.FindProperty("maxRunCadence").floatValue = MaxRunCadence;
            so.FindProperty("maxSprintCadence").floatValue = MaxSprintCadence;
            so.FindProperty("maxCrouchCadence").floatValue = MaxCrouchCadence;
            so.FindProperty("jumpHeight").floatValue = JumpHeight;
            so.FindProperty("gravity").floatValue = Gravity;
            so.FindProperty("attackRange").floatValue = AttackRange;
            so.FindProperty("lungeStopDistance").floatValue = LungeStopDistance;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ctrl);

            foreach (var enemy in Object.FindObjectsByType<Ayni.Enemy.EnemyController>(FindObjectsSortMode.None))
            {
                var soEnemy = new SerializedObject(enemy);
                soEnemy.FindProperty("attackRange").floatValue = AttackRange;
                soEnemy.ApplyModifiedProperties();
                EditorUtility.SetDirty(enemy);
            }

            EditorSceneManager.MarkSceneDirty(yari.scene);
            EditorSceneManager.SaveScene(yari.scene);

            Debug.Log($"<color=green>[Ayni Ajustes]</color> Velocidad de Yari: trote {oldJog:0.0} → {JogSpeed:0.0} m/s, " +
                      $"sprint {oldSprint:0.0} → {SprintSpeed:0.0} m/s, agachado {CrouchSpeed:0.0} m/s; salto de {JumpHeight:0.0} m con gravedad {Gravity:0}. Regenerando el Animator...");

            // Regenerar el Animator: vuelve a medir lo que cubre cada clip de locomoción
            AyniSceneSetup.CreateYariAnimatorController();
            AyniAmaruSetup.UpdateAmaruAnimations();
        }
    }
}
#endif
