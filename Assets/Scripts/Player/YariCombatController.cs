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
        [SerializeField] private float baseMoveSpeed = 5.2f;
        [SerializeField] private float sprintSpeed = 8.8f;
        [SerializeField] private float crouchSpeed = 2.4f;
        [SerializeField] private float jumpHeight = 1.6f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -18f;

        [Header("Postura y Agachado")]
        [SerializeField] private float combatStanceDuration = 4.5f;
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private Vector3 standingCenter = new Vector3(0f, 1f, 0f);
        [SerializeField] private float crouchHeight = 1.15f;
        [SerializeField] private Vector3 crouchCenter = new Vector3(0f, 0.575f, 0f);
        [SerializeField] private float crouchLerpSpeed = 10f;

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

        // Nuevos estados
        private bool isCrouching;
        private bool isSprinting;
        private bool inCombatStance;
        private float combatStanceTimer;
        private float currentAnimSpeed;

        public bool IsGuarding => isGuarding;
        public bool IsCrouching => isCrouching;
        public bool IsSprinting => isSprinting;
        public bool InCombatStance => inCombatStance;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            structure = GetComponent<StructureSystem>();
            talisman = GetComponent<IllaTalismanSystem>();
            animator = GetComponentInChildren<Animator>();

            if (characterController != null)
            {
                standingHeight = characterController.height;
                standingCenter = characterController.center;
                crouchHeight = standingHeight * 0.58f;
                crouchCenter = new Vector3(standingCenter.x, standingCenter.y * 0.58f, standingCenter.z);
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            // El componente Animator puede residir en el hijo Visual_Yari_3D
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (isAttacking && Time.time >= attackCooldown)
            {
                isAttacking = false;
            }

            HandleCrouch();
            HandleMovement();
            HandleJump();
            HandleDefense();
            HandleAttacks();
            HandleCombatStanceTimer();
            HandleDilemmaInputs();
        }

        private void HandleCrouch()
        {
            if (isAttacking) return;

            // Alternar con tecla C o mantener con Control Izquierdo
            if (Input.GetKeyDown(KeyCode.C))
            {
                SetCrouch(!isCrouching);
            }
            else if (Input.GetKey(KeyCode.LeftControl) && !isCrouching)
            {
                SetCrouch(true);
            }
            else if (Input.GetKeyUp(KeyCode.LeftControl) && isCrouching)
            {
                SetCrouch(false);
            }

            // Suavizar la altura y el centro del CharacterController al agacharse
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            Vector3 targetCenter = isCrouching ? crouchCenter : standingCenter;

            characterController.height = Mathf.Lerp(characterController.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            characterController.center = Vector3.Lerp(characterController.center, targetCenter, crouchLerpSpeed * Time.deltaTime);
        }

        private void SetCrouch(bool crouch)
        {
            if (isCrouching == crouch) return;
            isCrouching = crouch;
            if (animator) animator.SetBool("IsCrouching", isCrouching);
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

            bool hasMoveInput = direction.magnitude >= 0.1f;

            // Sprint con LeftShift cuando se mueve, sin estar en guardia
            if (hasMoveInput && Input.GetKey(KeyCode.LeftShift) && !isGuarding)
            {
                if (isCrouching) SetCrouch(false); // Salir de cuclillas al correr
                isSprinting = true;
            }
            else
            {
                isSprinting = false;
            }

            if (hasMoveInput)
            {
                // Mover relativo a la cámara estilo Sifu
                Vector3 camForward = cameraTransform.forward;
                Vector3 camRight = cameraTransform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 moveDir = camForward * direction.z + camRight * direction.x;

                float speedToUse = baseMoveSpeed;
                float targetAnimSpeedVal = 1f;

                if (isCrouching)
                {
                    speedToUse = crouchSpeed;
                    targetAnimSpeedVal = 1f;
                }
                else if (isSprinting)
                {
                    speedToUse = sprintSpeed;
                    targetAnimSpeedVal = 2f;
                }
                else
                {
                    speedToUse = baseMoveSpeed;
                    targetAnimSpeedVal = 1f;
                }

                // Si está defendiendo, reduce la velocidad de paso
                if (isGuarding)
                {
                    speedToUse *= 0.45f;
                    targetAnimSpeedVal = 0.5f;
                }

                float currentSpeed = speedToUse * talisman.GetSpeedMultiplier();
                characterController.Move(moveDir * (currentSpeed * Time.deltaTime));

                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, targetAnimSpeedVal, 8f * Time.deltaTime);
            }
            else
            {
                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, 0f, 10f * Time.deltaTime);
            }

            if (animator) animator.SetFloat("Speed", currentAnimSpeed);
        }

        private void HandleJump()
        {
            if (characterController.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -2f;

                // Salto estándar (Espacio sin estar en guardia)
                if (Input.GetKeyDown(KeyCode.Space) && !isGuarding && !isAttacking)
                {
                    if (isCrouching)
                    {
                        SetCrouch(false);
                    }
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    if (animator) animator.SetTrigger("Jump");
                }
            }

            // Aplicar gravedad
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            if (animator) animator.SetBool("IsGrounded", characterController.isGrounded);
        }

        private void HandleDefense()
        {
            // Bloqueo / Guardia (Click derecho o tecla G)
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.G))
            {
                if (isCrouching) SetCrouch(false);
                isGuarding = true;
                guardStartTime = Time.time;
                EnterCombatStance();
                if (animator) animator.SetBool("IsGuarding", true);
            }
            else if (Input.GetMouseButtonUp(1) || Input.GetKeyUp(KeyCode.G))
            {
                isGuarding = false;
                if (animator) animator.SetBool("IsGuarding", false);
            }

            if (isGuarding)
            {
                EnterCombatStance();

                // Esquivas Direccionales estilo Sifu (Duck / Jump Avoid)
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.S))
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
                if (isCrouching) SetCrouch(false);
                ExecuteAttack(isHeavy: false);
            }
            // Golpe Fuerte de Rumi Maki (Tecla Q o E)
            else if ((Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)) && Time.time >= attackCooldown)
            {
                if (isCrouching) SetCrouch(false);
                ExecuteAttack(isHeavy: true);
            }
        }

        private void ExecuteAttack(bool isHeavy)
        {
            EnterCombatStance();
            isAttacking = true;
            attackCooldown = Time.time + (isHeavy ? 0.7f : 0.4f);
            if (animator) animator.SetTrigger(isHeavy ? "HeavyAttack" : "LightAttack");

            float dmg = (isHeavy ? heavyAttackDamage : lightAttackDamage) * talisman.GetDamageMultiplier();
            float structDmg = (isHeavy ? 30f : 15f) * talisman.GetDamageMultiplier();

            // Detectar impacto frontal
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

        public void EnterCombatStance()
        {
            inCombatStance = true;
            combatStanceTimer = combatStanceDuration;
            if (animator) animator.SetBool("InCombatStance", true);
        }

        private void HandleCombatStanceTimer()
        {
            if (inCombatStance && !isGuarding)
            {
                combatStanceTimer -= Time.deltaTime;
                if (combatStanceTimer <= 0f)
                {
                    inCombatStance = false;
                    if (animator) animator.SetBool("InCombatStance", false);
                    Debug.Log("[Ayni] Yari relaja su postura de combate y regresa al descanso con brazos abajo.");
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
            EnterCombatStance();
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
