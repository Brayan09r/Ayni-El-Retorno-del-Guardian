using System;
using UnityEngine;
using Ayni.Core;
using Ayni.Combat;

namespace Ayni.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(StructureSystem))]
    [RequireComponent(typeof(IllaTalismanSystem))]
    public class YariCombatController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float baseMoveSpeed = 5.5f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Combate Rumi Maki")]
        [SerializeField] private float lightAttackDamage = 18f;
        [SerializeField] private float heavyAttackDamage = 35f;
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private float parryWindow = 0.22f; // Ventana para desvío perfecto (parry)

        [Header("Referencias")]
        [SerializeField] private Transform cameraTransform;

        private CharacterController characterController;
        private StructureSystem structure;
        private IllaTalismanSystem talisman;
        private Animator animator;

        private Vector3 velocity;
        private bool isGuarding;
        private float guardStartTime;
        private bool isAttacking;
        private float attackCooldown;

        public bool IsGuarding => isGuarding;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            structure = GetComponent<StructureSystem>();
            talisman = GetComponent<IllaTalismanSystem>();
            animator = GetComponentInChildren<Animator>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleDefense();
            HandleAttacks();
            HandleDilemmaInputs();
        }

        private void HandleMovement()
        {
            if (isAttacking) return;

            if (cameraTransform == null)
            {
                if (Camera.main != null) cameraTransform = Camera.main.transform;
                else return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

            if (direction.magnitude >= 0.1f)
            {
                // Mover relativo a la cámara estilo Sifu
                Vector3 camForward = cameraTransform.forward;
                Vector3 camRight = cameraTransform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 moveDir = camForward * direction.z + camRight * direction.x;
                float currentSpeed = baseMoveSpeed * talisman.GetSpeedMultiplier();

                // Si está defendiendo, reduce la velocidad de paso
                if (isGuarding) currentSpeed *= 0.4f;

                characterController.Move(moveDir * (currentSpeed * Time.deltaTime));

                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                if (animator) animator.SetFloat("Speed", direction.magnitude);
            }
            else
            {
                if (animator) animator.SetFloat("Speed", 0f);
            }

            // Aplicar gravedad básica
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void HandleDefense()
        {
            // Bloqueo / Guardia (LShift o Botón derecho sostenido)
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
            {
                isGuarding = true;
                guardStartTime = Time.time;
                if (animator) animator.SetBool("IsGuarding", true);
            }
            else if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetMouseButtonUp(1))
            {
                isGuarding = false;
                if (animator) animator.SetBool("IsGuarding", false);
            }

            // Esquiva rápida sin guardia (Espacio): Yari se agacha estilo Sifu
            if (!isGuarding && Input.GetKeyDown(KeyCode.Space))
            {
                if (animator) animator.SetTrigger("DuckAvoid");
            }

            // Esquivas Direccionales estilo Sifu (Duck / Jump Avoid)
            if (isGuarding)
            {
                if (Input.GetKeyDown(KeyCode.S))
                {
                    Debug.Log("[Sifu Evade] Yari se agacha (Duck) para esquivar ataque alto.");
                    if (animator) animator.SetTrigger("DuckAvoid");
                }
                else if (Input.GetKeyDown(KeyCode.W))
                {
                    Debug.Log("[Sifu Evade] Yari salta (Jump Avoid) para esquivar barrido de piernas.");
                    if (animator) animator.SetTrigger("JumpAvoid");
                }
            }
        }

        private void HandleAttacks()
        {
            if (isGuarding) return;

            // Golpe Ligero de Rumi Maki (Click izquierdo)
            if (Input.GetMouseButtonDown(0) && Time.time >= attackCooldown)
            {
                ExecuteAttack(isHeavy: false);
            }
            // Golpe Fuerte de Rumi Maki (Tecla Q o E)
            else if (Input.GetKeyDown(KeyCode.Q) && Time.time >= attackCooldown)
            {
                ExecuteAttack(isHeavy: true);
            }
        }

        private void ExecuteAttack(bool isHeavy)
        {
            attackCooldown = Time.time + (isHeavy ? 0.7f : 0.4f);
            if (animator) animator.SetTrigger(isHeavy ? "HeavyAttack" : "LightAttack");

            float dmg = (isHeavy ? heavyAttackDamage : lightAttackDamage) * talisman.GetDamageMultiplier();
            float structDmg = (isHeavy ? 30f : 15f) * talisman.GetDamageMultiplier();

            // Detectar impacto frontal
            // Si enemyLayer quedó en "Nothing" (valor por defecto), se revisan todas las capas;
            // el TryGetComponent de abajo ya filtra solo a los enemigos.
            int mask = enemyLayer.value == 0 ? Physics.AllLayers : enemyLayer.value;
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.2f, attackRange, mask);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<Enemy.EnemyController>(out var enemy))
                {
                    enemy.TakeHit(dmg, structDmg, transform.position);
                }
            }
        }

        public bool TryParry()
        {
            // Retorna true si el golpe impacta dentro de la ventana de desvío perfecto
            return isGuarding && (Time.time - guardStartTime <= parryWindow);
        }

        // Llamado por los enemigos cuando conectan un golpe directo
        public void PlayHitReaction()
        {
            if (animator) animator.SetTrigger("Hit");
        }

        // Llamado cuando Yari es derrotado
        public void PlayDefeat()
        {
            if (animator) animator.SetTrigger("Die");
            enabled = false;
        }

        private void HandleDilemmaInputs()
        {
            // Interacción de ejecución o perdón cuando un jefe/rival tiene la postura rota
            if (Input.GetKeyDown(KeyCode.F))
            {
                AyniPurificationManager.Instance?.TriggerExecutionAction(transform.position, isAyniMercy: false);
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                AyniPurificationManager.Instance?.TriggerExecutionAction(transform.position, isAyniMercy: true);
            }
        }
    }
}
