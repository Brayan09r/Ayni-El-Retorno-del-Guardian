#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ayni.Player;

namespace Ayni.Editor
{
    /// <summary>
    /// Ajustes de movimiento de Yari. Las velocidades originales (5.5 m/s al trote y 8.8 m/s en sprint) eran mucho
    /// mayores que las que cubren las animaciones, y los pies patinaban. Solo se ejecuta desde el menú.
    /// </summary>
    public static class AyniTuning
    {
        private const float JogSpeed = 4.0f;
        private const float SprintSpeed = 6.5f;
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
                      $"sprint {oldSprint:0.0} → {SprintSpeed:0.0} m/s; salto de {JumpHeight:0.0} m con gravedad {Gravity:0}. Regenerando el Animator para ajustar la cadencia...");

            // Regenerar el Animator: recalcula la cadencia de las piernas con las nuevas velocidades
            AyniSceneSetup.CreateYariAnimatorController();
            AyniAmaruSetup.UpdateAmaruAnimations();
        }
    }
}
#endif
