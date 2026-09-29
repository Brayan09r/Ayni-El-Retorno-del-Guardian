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
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundStickForce = -6.5f;

        [Header("Postura y Agachado")]
        [SerializeField] private float combatStanceDuration = 4.5f;
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private Vector3 standingCenter = new Vector3(0f, 1f, 0f);
        [SerializeField] private float crouchHeight = 1.15f;
        [SerializeField] private Vector3 crouchCenter = new Vector3(0f, 0.575f, 0f);
        [SerializeField] private float crouchLerpSpeed = 12f;

        [Header("Salud y Vitalidad")]
        [SerializeField] private float baseMaxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

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

        private float verticalVelocity;
        private Vector3 horizontalVelocity;
        private bool isGuarding;
        private float guardStartTime;
        [SerializeField] private bool isAttacking;
        [SerializeField] private float attackCooldown;
        [SerializeField] private float attackRecoveryTime;
        [SerializeField] private bool isStunned;
        [SerializeField] private bool isDead;

        // Estados de Locomoción
        private bool isCrouching;
        private bool isSprinting;
        private bool inCombatStance;
        private float combatStanceTimer;
        private float currentAnimSpeed;

        public bool IsGuarding => isGuarding;
        public bool IsCrouching => isCrouching;
        public bool IsSprinting => isSprinting;
        public bool InCombatStance => inCombatStance;
        public bool IsAttacking => isAttacking;
        public bool IsStunned => isStunned;
        public bool IsDead => isDead;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => baseMaxHealth * (talisman != null ? talisman.GetMaxHealthMultiplier() : 1f);
        public float HealthRatio => MaxHealth > 0f ? Mathf.Clamp01(currentHealth / MaxHealth) : 0f;
        public StructureSystem Structure => structure != null ? structure : (structure = GetComponent<StructureSystem>());
        public IllaTalismanSystem Talisman => talisman != null ? talisman : (talisman = GetComponent<IllaTalismanSystem>());
        public CharacterController Controller => characterController != null ? characterController : (characterController = GetComponent<CharacterController>());

        private void Awake()
        {
            EnsureComponentReferences();
        }

        private void OnEnable()
        {
            EnsureComponentReferences();
            BindStructureEvents();
        }

        private void OnDisable()
        {
            UnbindStructureEvents();
        }

        public void EnsureComponentReferences()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (structure == null) structure = GetComponent<StructureSystem>();
            if (talisman == null) talisman = GetComponent<IllaTalismanSystem>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (characterController != null)
            {
                if (!isCrouching && characterController.height >= 1.6f)
                {
                    standingHeight = characterController.height;
                    standingCenter = characterController.center;
                    crouchHeight = Mathf.Max(0.8f, standingHeight * 0.58f);
                    crouchCenter = new Vector3(standingCenter.x, crouchHeight * 0.5f, standingCenter.z);
                }
                else if (standingHeight < 1.6f)
                {
                    standingHeight = 2.0f;
                    standingCenter = new Vector3(0f, 1f, 0f);
                    crouchHeight = 1.15f;
                    crouchCenter = new Vector3(0f, 0.575f, 0f);
                }

                // Configuración óptima para terreno irregular y pendientes
                characterController.stepOffset = 0.4f;
                characterController.slopeLimit = 55f;
                characterController.skinWidth = 0.08f;
                characterController.minMoveDistance = 0f;
            }

            if (currentHealth <= 0f && !isDead)
            {
                currentHealth = MaxHealth;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        public void BindStructureEvents()
        {
            var s = Structure;
            if (s != null)
            {
                s.OnStructureBroken -= HandleStructureBroken;
                s.OnStructureRecovered -= HandleStructureRecovered;
                s.OnStructureBroken += HandleStructureBroken;
                s.OnStructureRecovered += HandleStructureRecovered;
            }
        }

        public void UnbindStructureEvents()
        {
            if (structure != null)
            {
                structure.OnStructureBroken -= HandleStructureBroken;
                structure.OnStructureRecovered -= HandleStructureRecovered;
            }
        }

        private void OnDestroy()
        {
            UnbindStructureEvents();
        }

        private void HandleStructureBroken()
        {
            isStunned = true;
            isGuarding = false;
            isAttacking = false;
            isSprinting = false;
            if (animator)
            {
                animator.SetBool("IsGuarding", false);
                animator.SetBool("IsStunned", true);
                animator.ResetTrigger("Hit");
                animator.SetTrigger("Hit");
            }
            Debug.Log("[Ayni] ¡Estructura de Yari ROTA! Yari está aturdido y vulnerable.");
        }

        private void HandleStructureRecovered()
        {
            isStunned = false;
            if (animator)
            {
                animator.SetBool("IsStunned", false);
            }
            Debug.Log("[Ayni] Yari recupera su postura y equilibrio.");
        }

        /// <summary>
        /// Comprueba si hay espacio vertical libre para pararse sin atravesar techos u obstáculos.
        /// Ignora los propios colliders de Yari y a los enemigos para evitar falsos positivos.
        /// </summary>
        public bool CanStandUp()
        {
            var cc = Controller;
            if (cc == null) return true;
            float castRadius = cc.radius * 0.85f;
            float castDist = standingHeight - cc.height;
            if (castDist <= 0.01f) return true;

            Vector3 origin = transform.position + Vector3.up * (cc.height - castRadius);
            int layerMask = ~LayerMask.GetMask("Ignore Raycast");

            RaycastHit[] hits = Physics.SphereCastAll(origin, castRadius, Vector3.up, castDist, layerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.root == transform.root) continue;
                if (hit.collider.GetComponentInParent<Enemy.EnemyController>() != null) continue;

                // Obstáculo sólido detectado sobre la cabeza de Yari
                return false;
            }
            return true;
        }

        private void Update()
        {
            // El componente Animator puede residir en el hijo Visual_Yari_3D
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            // Descongelar ataque cuando expire el cooldown
            if (isAttacking && Time.time >= attackCooldown)
            {
                isAttacking = false;
            }

            HandleCrouch();
            HandleDefense();
            HandleAttacks();
            HandleMovementAndGravity();
            HandleCombatStanceTimer();
            HandleDilemmaInputs();
        }

        private void HandleCrouch()
        {
            var cc = Controller;
            if (cc == null) return;
            if (isStunned || isDead) return;

            // Si se presiona agachado mientras se atacaba, permitir cancelación limpia de ataque a cuclillas
            if (isAttacking && Time.time < attackRecoveryTime)
            {
                if (Input.GetKeyDown(KeyCode.C) || Input.GetKey(KeyCode.LeftControl))
                {
                    CancelAttackForDefenseOrCrouch();
                    SetCrouch(true);
                }
                return;
            }

            // Alternar con tecla C o mantener con Control Izquierdo
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (isCrouching)
                {
                    if (CanStandUp()) SetCrouch(false);
                    else Debug.Log("[Ayni] Techo u obstáculo bajo encima: permanece agachado.");
                }
                else
                {
                    SetCrouch(true);
                }
            }
            else if (Input.GetKey(KeyCode.LeftControl) && !isCrouching)
            {
                SetCrouch(true);
            }
            else if (Input.GetKeyUp(KeyCode.LeftControl) && isCrouching)
            {
                if (CanStandUp()) SetCrouch(false);
                else Debug.Log("[Ayni] Techo u obstáculo bajo encima: permanece agachado.");
            }

            // Comprobar espacio vertical antes de pararse si hay obstáculos arriba
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            if (!isCrouching && cc.height < standingHeight)
            {
                if (!CanStandUp())
                {
                    // Techo bajo o roca encima: permanecer agachado y sincronizar animator
                    targetHeight = crouchHeight;
                    SetCrouch(true);
                }
            }

            // Ajustar suavemente la altura manteniendo siempre la BASE DEL COLLIDER en Y = 0 (suelo)
            float newHeight = Mathf.MoveTowards(cc.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            cc.height = newHeight;
            cc.center = new Vector3(standingCenter.x, newHeight * 0.5f, standingCenter.z);
        }

        private void SetCrouch(bool crouch)
        {
            if (isCrouching == crouch) return;
            isCrouching = crouch;
            if (animator) animator.SetBool("IsCrouching", isCrouching);
        }

        private void HandleMovementAndGravity()
        {
            var cc = Controller;
            if (cc == null) return;

            if (cameraTransform == null)
            {
                if (Camera.main != null) cameraTransform = Camera.main.transform;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

            bool hasMoveInput = direction.magnitude >= 0.1f;

            // Sprint con LeftShift cuando se mueve, sin estar en guardia, aturdido ni muerto
            if (hasMoveInput && Input.GetKey(KeyCode.LeftShift) && !isGuarding && !isStunned && !isDead)
            {
                if (isCrouching)
                {
                    if (CanStandUp())
                    {
                        SetCrouch(false);
                        isSprinting = true;
                    }
                    else
                    {
                        isSprinting = false; // Techo bajo bloquea pararse: no puede sprintar agachado
                    }
                }
                else
                {
                    isSprinting = true;
                }
            }
            else
            {
                isSprinting = false;
            }

            // 1. Cálculo de Desplazamiento Horizontal
            bool isLockedInStrike = isAttacking && Time.time < attackRecoveryTime;

            if (hasMoveInput && !isLockedInStrike && !isDead)
            {
                // Mover relativo a la cámara estilo Sifu (o espacio mundial si la cámara no está asignada)
                Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
                Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 moveDir = camForward * direction.z + camRight * direction.x;
                if (moveDir.sqrMagnitude < 0.001f) moveDir = transform.forward;

                float speedToUse = baseMoveSpeed;
                float targetAnimSpeedVal = 1f;

                if (isStunned)
                {
                    speedToUse = 1.4f; // Tambaleo con postura rota
                    targetAnimSpeedVal = 0.4f;
                }
                else if (isCrouching)
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

                // Reducción de velocidad si está en guardia
                if (isGuarding && !isStunned)
                {
                    speedToUse *= 0.45f;
                    targetAnimSpeedVal = 0.5f;
                }

                float currentSpeed = speedToUse * (talisman != null ? talisman.GetSpeedMultiplier() : 1f);
                horizontalVelocity = moveDir * currentSpeed;

                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, targetAnimSpeedVal, 8f * Time.deltaTime);
            }
            else
            {
                horizontalVelocity = Vector3.zero;
                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, 0f, 10f * Time.deltaTime);
            }

            if (animator) animator.SetFloat("Speed", currentAnimSpeed);

            // 2. Manejo de Gravedad y Salto
            if (cc.isGrounded)
            {
                // Fuerza de anclaje descendente constante para adherencia en terreno irregular y sprint
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = groundStickForce;
                }

                // Salto estándar (Espacio sin estar en guardia, ni en golpe activo, ni aturdido, ni muerto)
                if (Input.GetKeyDown(KeyCode.Space) && !isGuarding && !isLockedInStrike && !isStunned && !isDead)
                {
                    if (isCrouching)
                    {
                        if (CanStandUp())
                        {
                            SetCrouch(false);
                            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                            if (animator) animator.SetTrigger("Jump");
                        }
                    }
                    else
                    {
                        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                        if (animator) animator.SetTrigger("Jump");
                    }
                }
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, -35f); // Terminal velocity clamp
            }

            // 3. UNIFICACIÓN: Una sola llamada a CharacterController.Move() por frame
            // Combina vector horizontal y gravedad para evitar penetración o despegue del terreno en sprint
            Vector3 finalMovement = (horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
            cc.Move(finalMovement);

            if (animator) animator.SetBool("IsGrounded", cc.isGrounded);
        }

        /// <summary>
        /// Cancela de forma limpia un ataque en curso cuando el jugador transiciona a guardia, esquiva o agachado.
        /// </summary>
        public void CancelAttackForDefenseOrCrouch()
        {
            if (isAttacking)
            {
                isAttacking = false;
                attackRecoveryTime = 0f;
                attackCooldown = Time.time + 0.12f;
                if (animator)
                {
                    animator.ResetTrigger("LightAttack");
                    animator.ResetTrigger("HeavyAttack");
                }
            }
        }

        private void HandleDefense()
        {
            if (isStunned || isDead)
            {
                if (isGuarding)
                {
                    isGuarding = false;
                    if (animator) animator.SetBool("IsGuarding", false);
                }
                return;
            }

            bool wantsGuard = Input.GetMouseButton(1) || Input.GetKey(KeyCode.G);

            if (wantsGuard)
            {
                if (!isGuarding)
                {
                    if (isCrouching)
                    {
                        if (CanStandUp()) SetCrouch(false);
                        else return;
                    }

                    // Cancelar inmediatamente ataque si estaba en golpe para entrar en guardia o parry reactivo
                    CancelAttackForDefenseOrCrouch();

                    isGuarding = true;
                    guardStartTime = Time.time;
                    EnterCombatStance();
                    if (animator) animator.SetBool("IsGuarding", true);
                }
            }
            else
            {
                if (isGuarding)
                {
                    isGuarding = false;
                    if (animator) animator.SetBool("IsGuarding", false);
                }
            }

            if (isGuarding)
            {
                EnterCombatStance();

                // Esquivas Direccionales estilo Sifu (Duck / Jump Avoid)
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.S))
                {
                    CancelAttackForDefenseOrCrouch();
                    Debug.Log("[Sifu Evade] Yari se agacha (Duck) para esquivar ataque alto.");
                    if (animator)
                    {
                        animator.ResetTrigger("DuckAvoid");
                        animator.SetTrigger("DuckAvoid");
                    }
                }
                else if (Input.GetKeyDown(KeyCode.W))
                {
                    CancelAttackForDefenseOrCrouch();
                    Debug.Log("[Sifu Evade] Yari salta (Jump Avoid) para esquivar barrido de piernas.");
                    if (animator)
                    {
                        animator.ResetTrigger("JumpAvoid");
                        animator.SetTrigger("JumpAvoid");
                    }
                }
            }
        }

        private void HandleAttacks()
        {
            if (isGuarding || isStunned || isDead) return;

            // Golpe Ligero de Rumi Maki (Click izquierdo)
            if (Input.GetMouseButtonDown(0) && Time.time >= attackCooldown)
            {
                if (isCrouching)
                {
                    if (CanStandUp()) SetCrouch(false);
                    else return;
                }
                ExecuteAttack(isHeavy: false);
            }
            // Golpe Fuerte de Rumi Maki (Tecla Q o E)
            else if ((Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)) && Time.time >= attackCooldown)
            {
                if (isCrouching)
                {
                    if (CanStandUp()) SetCrouch(false);
                    else return;
                }
                ExecuteAttack(isHeavy: true);
            }
        }

        private void ExecuteAttack(bool isHeavy)
        {
            EnterCombatStance();
            isAttacking = true;

            // Ventana activa de impacto y cooldown total ajustados para combate fluido y responsivo
            attackRecoveryTime = Time.time + (isHeavy ? 0.42f : 0.22f);
            attackCooldown = Time.time + (isHeavy ? 0.68f : 0.38f);

            if (animator)
            {
                animator.ResetTrigger("LightAttack");
                animator.ResetTrigger("HeavyAttack");
                animator.SetTrigger(isHeavy ? "HeavyAttack" : "LightAttack");
            }

            float dmgMult = talisman != null ? talisman.GetDamageMultiplier() : 1f;
            float dmg = (isHeavy ? heavyAttackDamage : lightAttackDamage) * dmgMult;
            float structDmg = (isHeavy ? 32f : 16f) * dmgMult;

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
            if (inCombatStance && !isGuarding && !isAttacking)
            {
                combatStanceTimer -= Time.deltaTime;
                if (combatStanceTimer <= 0f)
                {
                    inCombatStance = false;
                    if (animator)
                    {
                        animator.SetBool("InCombatStance", false);
                        animator.ResetTrigger("LightAttack");
                        animator.ResetTrigger("HeavyAttack");
                        animator.ResetTrigger("DuckAvoid");
                        animator.ResetTrigger("JumpAvoid");
                        animator.ResetTrigger("Hit");
                    }
                    Debug.Log("[Ayni] Yari relaja su postura de combate y regresa al descanso.");
                }
            }
        }

        public bool TryParry()
        {
            // Retorna true si el golpe impacta dentro de la ventana de desvío perfecto
            return isGuarding && (Time.time - guardStartTime <= parryWindow);
        }

        // Llamado al recibir daño directo o a la postura
        public void TakeDamage(float healthDmg, float structDmg = 0f)
        {
            if (isDead) return;

            if (structure != null && structure.IsBroken)
            {
                healthDmg *= 1.5f; // Mayor daño si la postura está rota
            }

            currentHealth = Mathf.Max(0f, currentHealth - healthDmg);
            if (structDmg > 0f && structure != null)
            {
                structure.AddStructureDamage(structDmg);
            }

            // Si estaba en medio de un ataque, interrumpirlo
            if (isAttacking)
            {
                isAttacking = false;
                attackRecoveryTime = 0f;
                attackCooldown = Time.time + 0.2f;
            }

            if (currentHealth <= 0f)
            {
                HandleDeathAndResurrection();
            }
            else
            {
                PlayHitReaction();
            }
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f) return;
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        }

        private void HandleDeathAndResurrection()
        {
            isAttacking = false;
            isGuarding = false;
            attackRecoveryTime = 0f;
            attackCooldown = 0f;

            if (talisman != null)
            {
                bool revived = talisman.TriggerResurrection();
                if (revived)
                {
                    isDead = false;
                    currentHealth = MaxHealth;
                    if (structure != null) structure.ResetStructure();
                    isStunned = false;
                    if (animator)
                    {
                        animator.ResetTrigger("Hit");
                        animator.SetBool("IsStunned", false);
                        animator.SetBool("IsGuarding", false);
                    }
                    Debug.Log($"[Illa Sagrada] ¡Yari resucita! Nueva edad: {talisman.CurrentAge} años.");
                    return;
                }
            }

            PlayDefeat();
        }

        // Llamado por los enemigos cuando conectan un golpe directo
        public void PlayHitReaction()
        {
            if (isDead) return;
            EnterCombatStance();
            if (animator)
            {
                animator.ResetTrigger("Hit");
                animator.SetTrigger("Hit");
            }
        }

        // Llamado cuando Yari es derrotado definitivamente
        public void PlayDefeat()
        {
            isDead = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            isAttacking = false;
            isGuarding = false;
            isStunned = false;
            if (animator)
            {
                animator.ResetTrigger("Hit");
                animator.SetBool("IsStunned", false);
                animator.SetTrigger("Die");
            }
            enabled = false;
        }

        private void HandleDilemmaInputs()
        {
            if (isStunned || isDead) return;

            // Interacción de ejecución o perdón cuando un jefe/rival tiene la postura rota
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (isCrouching)
                {
                    if (CanStandUp()) SetCrouch(false);
                    else return;
                }

                if (AyniPurificationManager.Instance != null &&
                    AyniPurificationManager.Instance.TriggerExecutionAction(transform.position, isAyniMercy: false, out var executedEnemy))
                {
                    if (executedEnemy != null)
                    {
                        Vector3 targetLook = new Vector3(executedEnemy.transform.position.x, transform.position.y, executedEnemy.transform.position.z);
                        transform.LookAt(targetLook);
                    }
                    ExecuteAttack(isHeavy: true);
                }
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                if (isCrouching)
                {
                    if (CanStandUp()) SetCrouch(false);
                    else return;
                }

                if (AyniPurificationManager.Instance != null &&
                    AyniPurificationManager.Instance.TriggerExecutionAction(transform.position, isAyniMercy: true, out var executedEnemy))
                {
                    if (executedEnemy != null)
                    {
                        Vector3 targetLook = new Vector3(executedEnemy.transform.position.x, transform.position.y, executedEnemy.transform.position.z);
                        transform.LookAt(targetLook);
                    }
                    ExecuteAttack(isHeavy: false);
                    if (structure != null) structure.ResetStructure();
                    Heal(25f);
                }
            }
        }

        /// <summary>
        /// Restablece completamente el estado de combate, salud, colisiones y animaciones de Yari.
        /// </summary>
        public void ResetCombatState()
        {
            isDead = false;
            enabled = true;
            isAttacking = false;
            isGuarding = false;
            isStunned = false;
            isCrouching = false;
            isSprinting = false;
            attackRecoveryTime = 0f;
            attackCooldown = 0f;
            verticalVelocity = 0f;
            horizontalVelocity = Vector3.zero;
            currentHealth = MaxHealth;
            if (structure != null) structure.ResetStructure();
            if (characterController != null)
            {
                characterController.height = standingHeight;
                characterController.center = standingCenter;
            }
            if (animator)
            {
                animator.SetBool("IsGuarding", false);
                animator.SetBool("IsStunned", false);
                animator.SetBool("IsCrouching", false);
                animator.SetBool("InCombatStance", false);
                animator.SetFloat("Speed", 0f);
                animator.ResetTrigger("LightAttack");
                animator.ResetTrigger("HeavyAttack");
                animator.ResetTrigger("DuckAvoid");
                animator.ResetTrigger("JumpAvoid");
                animator.ResetTrigger("Hit");
            }
        }

        /// <summary>
        /// Simula el inicio de un ataque para pruebas automatizadas y validación de cancelación.
        /// </summary>
        public void SimulateAttackForTest(float recoveryOffset = 0.42f, float cooldownOffset = 0.68f)
        {
            isAttacking = true;
            attackRecoveryTime = Time.time + recoveryOffset;
            attackCooldown = Time.time + cooldownOffset;
        }
    }
}
