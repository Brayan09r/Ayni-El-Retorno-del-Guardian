using System.Collections;
using UnityEngine;

namespace Ayni.Combat
{
    /// <summary>
    /// Modificador procedural de postura andina (Takanakuy / Rumi Maki).
    /// Ajusta en tiempo de ejecución las articulaciones del Avatar Humanoid de Unity
    /// para transformar animaciones genéricas de combate en el estilo andino ritual:
    /// - Centro de gravedad bajo (flexión de rodillas, peso en los pies y cadera descendida).
    /// - Inclinación agresiva de tronco hacia adelante.
    /// - Codos y hombros abiertos (guardia abierta y ancha de Takanakuy).
    /// - Hitstop procedural (micro-congelación de frames al impactar) estilo Sifu / Rumi Maki.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AndeanCombatStanceModifier : MonoBehaviour
    {
        [Header("Configuración de Postura Takanakuy")]
        [Tooltip("Activa o desactiva la modificación procedural de postura.")]
        [SerializeField] private bool enableAndeanStance = true;

        [Tooltip("Descenso de la cadera (Hips) en metros para clavar el peso en la tierra.")]
        [Range(0f, 0.25f)]
        [SerializeField] private float hipsDrop = 0.12f;

        [Tooltip("Inclinación hacia adelante del torso/espina en grados.")]
        [Range(0f, 25f)]
        [SerializeField] private float spineForwardLean = 8.0f;

        [Tooltip("Apertura lateral de los codos hacia afuera en grados (guardia ancha ritual).")]
        [Range(0f, 30f)]
        [SerializeField] private float elbowFlare = 14.0f;

        [Tooltip("Velocidad de transición suave al entrar o salir de la postura de combate.")]
        [SerializeField] private float blendSpeed = 5.0f;

        [Header("Hitstop (Impacto Puño de Piedra)")]
        [Tooltip("Duración por defecto de la micro-pausa de impacto en segundos.")]
        [SerializeField] private float defaultHitstopDuration = 0.065f;

        private Animator animator;
        private Transform hipsBone;
        private Transform spineBone;
        private Transform leftUpperArm;
        private Transform rightUpperArm;

        private float currentWeight = 1.0f;
        private float targetWeight = 1.0f;
        private Coroutine hitstopCoroutine;
        private FootIK footIK;
        private float preHitstopAnimSpeed = 1.0f;

        public bool IsAndeanStanceActive => enableAndeanStance && currentWeight > 0.01f;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            CacheBones();
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;

            hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        /// <summary>
        /// Controla el peso de la postura andina (por ejemplo, reducir a 0 durante carreras rápidas o caídas).
        /// </summary>
        public void SetStanceWeight(float weight)
        {
            targetWeight = Mathf.Clamp01(weight);
        }

        private void Update()
        {
            // Transición suave del peso de la postura
            currentWeight = Mathf.MoveTowards(currentWeight, enableAndeanStance ? targetWeight : 0f, blendSpeed * Time.deltaTime);

            // Con IK de pies, la cadera baja dentro del pase de IK: los pies se quedan en el suelo y las rodillas
            // se flexionan. Mover el hueso de la cadera a mano arrastraba las piernas y hundía los pies 12 cm.
            if (footIK == null) footIK = GetComponent<FootIK>();
            if (footIK != null) footIK.ExtraPelvisDrop = hipsDrop * currentWeight;
        }

        private void LateUpdate()
        {
            if (currentWeight <= 0.001f || hipsBone == null) return;

            // 1. Bajar centro de masa (Hips / Cadera) hacia el suelo (solo si no hay IK de pies que lo haga bien)
            if (hipsBone != null && footIK == null)
            {
                Vector3 hipsPos = hipsBone.position;
                hipsPos.y -= hipsDrop * currentWeight;
                hipsBone.position = hipsPos;
            }

            // 2. Inclinar la columna ligeramente hacia adelante (postura agresiva de embestida)
            if (spineBone != null)
            {
                spineBone.rotation *= Quaternion.Euler(spineForwardLean * currentWeight, 0f, 0f);
            }

            // 3. Abrir los codos lateralmente (guardia ancha de pelea andina)
            if (leftUpperArm != null)
            {
                leftUpperArm.rotation *= Quaternion.Euler(0f, 0f, -elbowFlare * currentWeight);
            }
            if (rightUpperArm != null)
            {
                rightUpperArm.rotation *= Quaternion.Euler(0f, 0f, elbowFlare * currentWeight);
            }
        }

        /// <summary>
        /// Aplica una micro-pausa en la animación (Hitstop) para transmitir el peso demoledor
        /// de un golpe de piedra o bloqueo perfecto.
        /// </summary>
        public void ApplyHitstop(float duration = -1f)
        {
            if (duration <= 0f) duration = defaultHitstopDuration;
            if (hitstopCoroutine != null) StopCoroutine(hitstopCoroutine);
            hitstopCoroutine = StartCoroutine(HitstopRoutine(duration));
        }

        private IEnumerator HitstopRoutine(float duration)
        {
            if (animator != null)
            {
                preHitstopAnimSpeed = animator.speed > 0.01f ? animator.speed : 1.0f;
                animator.speed = 0.05f; // Casi congelado para evitar artefactos visuales
            }

            yield return new WaitForSecondsRealtime(duration);

            if (animator != null)
            {
                animator.speed = preHitstopAnimSpeed;
            }
            hitstopCoroutine = null;
        }
    }
}
