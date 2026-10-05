using System;
using UnityEngine;

namespace Ayni.Combat
{
    /// <summary>
    /// Va en el objeto que tiene el Animator (el modelo) y reenvía el desplazamiento de la animación al controlador
    /// del personaje, que es quien lo mueve con su CharacterController. Lo añaden solos YariCombatController y EnemyController.
    /// Al existir OnAnimatorMove, Unity no aplica el desplazamiento por su cuenta.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class RootMotionRelay : MonoBehaviour
    {
        public Action<Vector3> OnRootMotion;

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            if (animator != null && OnRootMotion != null) OnRootMotion(animator.deltaPosition);
        }
    }
}
