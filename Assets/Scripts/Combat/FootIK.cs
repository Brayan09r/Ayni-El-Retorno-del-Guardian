using UnityEngine;

namespace Ayni.Combat
{
    /// <summary>
    /// Apoya los pies sobre el terreno: en pendientes y desniveles cada pie sube o baja lo que cambia el suelo
    /// bajo él, y la cadera baja lo necesario para que la pierna llegue. Conserva el movimiento de la animación
    /// (solo le suma la diferencia de altura del terreno). Requiere "IK Pass" activo en la capa base del Animator.
    /// Lo añaden solos YariCombatController y EnemyController sobre el objeto que tiene el Animator.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FootIK : MonoBehaviour
    {
        [Tooltip("Altura desde la que se lanza el rayo hacia abajo, sobre la base del personaje.")]
        [SerializeField] private float raycastUp = 0.6f;
        [SerializeField] private float raycastDown = 0.7f;
        [Tooltip("Máximo que un pie puede subir o bajar respecto a la animación.")]
        [SerializeField] private float maxOffset = 0.35f;
        [SerializeField] private float smooth = 14f;
        [Tooltip("Cuánto se inclina el pie para seguir la pendiente.")]
        [Range(0f, 1f)] [SerializeField] private float rotationWeight = 0.6f;

        /// <summary>El dueño lo activa cuando el personaje salta, está derribado o muerto.</summary>
        public bool Suspended;

        /// <summary>
        /// Etiqueta de los estados del Animator en los que no se apoyan los pies (esquivas, caídas, aterrizaje...).
        /// Los clips generados por la Fragua no traen "metas de IK" de los pies como los de Mixamo: si se apoyaran,
        /// los pies irían a parar a cualquier sitio (se veían recogidos en el aire). La pone AyniAnimatorUpgrade.
        /// </summary>
        public const string NoIKTag = "SinIK";

        /// <summary>El Animator está en (o entrando a) un estado sin apoyo de pies.</summary>
        public static bool IsNoIKState(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag(NoIKTag)) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag(NoIKTag);
        }

        /// <summary>
        /// Metros extra que baja la cadera con los pies clavados en el suelo (las rodillas se flexionan).
        /// Lo usa la postura andina para bajar el centro de gravedad sin hundir los pies.
        /// </summary>
        public float ExtraPelvisDrop;

        private Animator animator;
        private CharacterController controller;
        private float weight;
        private float leftOffset, rightOffset, pelvisOffset;
        private Vector3 leftNormal = Vector3.up, rightNormal = Vector3.up;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        public static FootIK Attach(Animator animator, CharacterController controller)
        {
            if (animator == null) return null;
            FootIK ik = animator.GetComponent<FootIK>();
            if (ik == null) ik = animator.gameObject.AddComponent<FootIK>();
            ik.animator = animator;
            ik.controller = controller;
            return ik;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || animator == null || !animator.isHuman) return;

            // Estados sin metas de IK (clips generados): los pies se quedan donde los pone la animación, sin transición
            if (IsNoIKState(animator))
            {
                weight = 0f;
                leftOffset = rightOffset = pelvisOffset = 0f;
                return;
            }

            bool active = !Suspended && (controller == null || (controller.enabled && controller.isGrounded));
            weight = Mathf.MoveTowards(weight, active ? 1f : 0f, Time.deltaTime * 6f);
            if (weight <= 0.001f)
            {
                leftOffset = rightOffset = pelvisOffset = 0f;
                return;
            }

            float rootY = transform.position.y;
            Vector3 leftPos = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 rightPos = animator.GetIKPosition(AvatarIKGoal.RightFoot);

            float leftTarget = Probe(leftPos, rootY, ref leftNormal);
            float rightTarget = Probe(rightPos, rootY, ref rightNormal);

            float k = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            leftOffset = Mathf.Lerp(leftOffset, leftTarget, k);
            rightOffset = Mathf.Lerp(rightOffset, rightTarget, k);

            // La cadera solo baja (para que llegue el pie más bajo); nunca se estira hacia arriba
            float pelvisTarget = Mathf.Min(0f, Mathf.Min(leftOffset, rightOffset));
            pelvisOffset = Mathf.Lerp(pelvisOffset, pelvisTarget, k);
            animator.bodyPosition += Vector3.up * ((pelvisOffset - ExtraPelvisDrop) * weight);

            Apply(AvatarIKGoal.LeftFoot, leftPos, leftOffset, leftNormal);
            Apply(AvatarIKGoal.RightFoot, rightPos, rightOffset, rightNormal);
        }

        /// <summary>Diferencia de altura entre el suelo bajo el pie y la base del personaje.</summary>
        private float Probe(Vector3 footPos, float rootY, ref Vector3 normal)
        {
            Vector3 origin = new Vector3(footPos.x, rootY + raycastUp, footPos.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, raycastUp + raycastDown,
                                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            float groundY = rootY;
            Vector3 groundNormal = Vector3.up;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider is CharacterController) continue;            // otros personajes no son suelo
                if (hit.collider.transform.IsChildOf(transform.root)) continue; // ni uno mismo
                if (hit.distance < best)
                {
                    best = hit.distance;
                    groundY = hit.point.y;
                    groundNormal = hit.normal;
                    found = true;
                }
            }

            if (!found)
            {
                normal = Vector3.Slerp(normal, Vector3.up, 0.2f);
                return 0f;
            }

            normal = Vector3.Slerp(normal, groundNormal, 0.25f);
            return Mathf.Clamp(groundY - rootY, -maxOffset, maxOffset);
        }

        private void Apply(AvatarIKGoal goal, Vector3 animatedPos, float offset, Vector3 normal)
        {
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKPosition(goal, animatedPos + Vector3.up * offset);

            animator.SetIKRotationWeight(goal, weight * rotationWeight);
            animator.SetIKRotation(goal, Quaternion.FromToRotation(Vector3.up, normal) * animator.GetIKRotation(goal));
        }
    }
}
