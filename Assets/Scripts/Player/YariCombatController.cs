using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ayni.Core;
using Ayni.Combat;
using Ayni.Enemy;

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

        [Header("Vida y Talismán Illa")]
        [SerializeField] private float baseMaxHealth = 100f;
        [Tooltip("Segundos que Yari permanece caído antes de que el talismán lo resucite.")]
        [SerializeField] private float reviveDelay = 2.2f;
        [Tooltip("Segundos de invulnerabilidad tras resucitar.")]
        [SerializeField] private float reviveInvulnerability = 1.5f;

        [Header("Combate Rumi Maki")]
        [SerializeField] private float lightAttackDamage = 18f;
        [SerializeField] private float heavyAttackDamage = 35f;
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private LayerMask enemyLayer; // Ya no se usa: los golpes se resuelven contra EnemyController.All
        [SerializeField] private float parryWindow = 0.22f; // Ventana para desvío perfecto (parry)

        [Header("Ritmo del Combo")]
        [Tooltip("Segundos de preparación del clip que se conservan antes del impacto en los golpes ligeros (menos = más seco).")]
        [SerializeField] private float lightLeadIn = 0.20f;
        [Tooltip("Segundos de preparación del clip que se conservan antes del impacto en los golpes pesados.")]
        [SerializeField] private float heavyLeadIn = 0.40f;
        [Tooltip("Velocidad de reproducción de los golpes ligeros.")]
        [SerializeField] private float lightAnimSpeed = 1.4f;
        [Tooltip("Velocidad de reproducción de los golpes pesados.")]
        [SerializeField] private float heavyAnimSpeed = 1.25f;
        [Tooltip("Segundos tras el impacto a partir de los cuales se puede encadenar el siguiente golpe.")]
        [SerializeField] private float comboCancelAfterContact = 0.10f;
        [Tooltip("Segundos tras el impacto en que Yari recupera el control si no encadena (ligero).")]
        [SerializeField] private float lightRecovery = 0.32f;
        [Tooltip("Segundos tras el impacto en que Yari recupera el control si no encadena (pesado).")]
        [SerializeField] private float heavyRecovery = 0.50f;
        [Tooltip("Segundos que una pulsación de ataque queda en memoria para encadenar el combo.")]
        [SerializeField] private float inputBufferTime = 0.30f;
        [Tooltip("Segundos sin atacar tras los que el combo vuelve al primer golpe.")]
        [SerializeField] private float comboResetTime = 0.6f;
        [Tooltip("Cuánto del desplazamiento propio de cada animación de golpe se aplica a Yari (1 = el paso real del clip, 0 = golpea sin moverse).")]
        [SerializeField] private float attackRootMotionScale = 1f;
        [Tooltip("El paso del golpe no acerca a Yari al rival más allá de esta distancia.")]
        [SerializeField] private float lungeStopDistance = 1.3f;
        [Tooltip("Si el rival queda fuera de alcance por menos de esta distancia, Yari la cubre durante el golpe para que conecte.")]
        [SerializeField] private float attackReachAssist = 0.4f;
        [Tooltip("Estela que dibujan los golpes fuertes y los remates.")]
        [SerializeField] private bool attackTrails = true;
        [Tooltip("Ángulo total del arco frontal en el que conectan los golpes.")]
        [SerializeField] private float attackArc = 120f;
        [Tooltip("Al atacar, Yari se gira hacia el enemigo más cercano dentro de esta distancia.")]
        [SerializeField] private float autoFaceRange = 3.5f;

        [Header("Defensa")]
        [Tooltip("Segundos de invulnerabilidad de cada esquiva (Duck evita ataques altos, Jump evita bajos).")]
        [SerializeField] private float dodgeWindow = 0.45f;
        [Tooltip("Segundos sin control tras recibir un golpe directo.")]
        [SerializeField] private float hitStunDuration = 0.35f;
        [Tooltip("Segundos sin control cuando la guardia de Yari se rompe.")]
        [SerializeField] private float guardBreakStun = 1.5f;

        [Header("Fijación de Blanco (Lock-On)")]
        [Tooltip("Distancia máxima para fijar a un rival con Tab o clic central.")]
        [SerializeField] private float lockOnRange = 14f;
        [Tooltip("La fijación se suelta sola si el rival se aleja más de esta distancia.")]
        [SerializeField] private float lockOnBreakRange = 20f;
        [Tooltip("Velocidad al moverse de lado o hacia atrás con el rival fijado.")]
        [SerializeField] private float lockStrafeSpeed = 2.4f;

        [Header("Salto")]
        [Tooltip("Segundos de mezcla al aterrizar para volver a la locomoción.")]
        [SerializeField] private float landBlendTime = 0.15f;

        [Header("Resurrección")]
        [Tooltip("Segundos que tarda Yari en levantarse del suelo (debe coincidir con el estado GetUp del Animator).")]
        [SerializeField] private float getUpDuration = 2.0f;

        [Header("Referencias")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Ajuste fino de la altura del modelo respecto al suelo (metros). 0 = pies apoyados.")]
        [SerializeField] private float visualHeightOffset = 0f;

        private CharacterController characterController;
        private StructureSystem structure;
        private IllaTalismanSystem talisman;
        private Animator animator;

        private float verticalVelocity;
        private Vector3 horizontalVelocity;
        private bool isGuarding;
        private float guardStartTime;
        private bool isAttacking;

        // Combo Rumi Maki
        private struct AttackDef
        {
            public string state;          // Estado del Animator
            public string clip;           // Nombre del clip (para buscar su tiempo de impacto medido)
            public bool heavy;
            public float fallbackContact; // Segundo de impacto si no hay medición
            public float damageMul;
            public float structureMul;
            public HitReaction reaction;  // Cómo reacciona el rival
            public float knockback;       // Metros que lo hace retroceder
            public int impact;            // 0 = ligero, 1 = fuerte, 2 = remate (pausa, sacudida y destello)

            public AttackDef(string state, string clip, bool heavy, float fallbackContact, float damageMul = 1f, float structureMul = 1f,
                             HitReaction reaction = HitReaction.Head, float knockback = 0.2f, int impact = 0)
            {
                this.state = state;
                this.clip = clip;
                this.heavy = heavy;
                this.fallbackContact = fallbackContact;
                this.damageMul = damageMul;
                this.structureMul = structureMul;
                this.reaction = reaction;
                this.knockback = knockback;
                this.impact = impact;
            }
        }

        // Cadena ligera: directo izq. → directo der. → gancho izq. → remate der.
        private static readonly AttackDef[] LightChain =
        {
            new AttackDef("Atk_Light1", "Light_Punch_1_L", false, 0.56f, 1f, 1f, HitReaction.Head, 0.15f, 0),
            new AttackDef("Atk_Light2", "Light_Punch_2_R", false, 0.88f, 1f, 1f, HitReaction.Head, 0.20f, 0),
            new AttackDef("Atk_Light3", "Light_Punch_3_L", false, 0.62f, 1f, 1f, HitReaction.Body, 0.20f, 0),
            new AttackDef("Atk_Light4", "Light_Punch_4_R", false, 0.50f, 1.3f, 1.3f, HitReaction.Heavy, 0.60f, 1),
        };

        // Cadena pesada: puñetazo descendente → gancho ascendente → codazo
        private static readonly AttackDef[] HeavyChain =
        {
            new AttackDef("Atk_Overhand", "Heavy_Overhand", true, 0.57f, 1f, 1f, HitReaction.Heavy, 0.50f, 1),
            new AttackDef("Atk_Uppercut", "Heavy_Uppercut", true, 0.60f, 1.1f, 1.1f, HitReaction.Head, 0.40f, 1),
            new AttackDef("Atk_Elbow", "Heavy_Elbow", true, 0.69f, 1.2f, 1.2f, HitReaction.Heavy, 0.70f, 1),
        };

        // Remates: pesado tras 2-3 ligeros = cabezazo; pesado tras los 4 ligeros = patada frontal de empuje
        private static readonly AttackDef HeadbuttFinisher = new AttackDef("Atk_Headbutt", "Heavy_Headbutt", true, 0.97f, 1.2f, 1.5f, HitReaction.Heavy, 0.80f, 2);
        private static readonly AttackDef FrontKickFinisher = new AttackDef("Atk_FrontKick", "Heavy_FrontKick", true, 0.83f, 1.3f, 1.7f, HitReaction.Knockdown, 1.60f, 2);

        /// <summary>Punto del clip GetUp (0-1) desde el que Yari empieza a levantarse; el Animator usa el mismo valor.</summary>
        public const float GetUpStartNormalized = 0.25f;
        /// <summary>Punto del clip GetUp (0-1) en el que ya está de pie y vuelve a la locomoción.</summary>
        public const float GetUpEndNormalized = 0.9f;

        private AttackTimingTable attackTimings;
        private AttackDef currentAttack;
        private float attackHitTime;
        private float attackCancelTime;
        private float attackEndTime;
        private bool attackHitDone;
        private int lightCount;
        private int heavyIndex;
        private float comboExpireTime;
        private int bufferedAttack; // 0 = nada, 1 = ligero, 2 = pesado
        private float bufferedUntil;
        private bool nextHitToBody;
        private EnemyController lungeTarget;
        private Animator rootMotionHookedFor;
        private float reachAssistRemaining;
        private float reachAssistSpeed;
        private TrailRenderer activeTrail;
        private float trailOffTime;
        private readonly System.Collections.Generic.Dictionary<HumanBodyBones, TrailRenderer> trails =
            new System.Collections.Generic.Dictionary<HumanBodyBones, TrailRenderer>();
        private FootIK footIK;
        private AndeanCombatStanceModifier andeanStance;
        private bool jumpInAir;
        private float jumpStartTime;

        // Fijación de blanco y capa de brazos en guardia
        private EnemyController lockTarget;
        private float upperGuardWeight;
        private float guardReactionUntil;
        private Animator cachedParamsFor;
        private readonly System.Collections.Generic.HashSet<int> animatorParams = new System.Collections.Generic.HashSet<int>();

        private bool isCrouching;
        private bool isSprinting;
        private bool inCombatStance;
        private float combatStanceTimer;
        private float currentAnimSpeed;

        // Vida, muerte y estados de reacción
        private float currentHealth;
        private bool isDead;
        private bool isGameOver;
        private float invulnerableUntil;
        private float stunnedUntil;
        private float dodgeUntil;
        private AttackHeight dodgeEvades;

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

        public float CurrentHealth => currentHealth;
        public float MaxHealth => baseMaxHealth * (talisman != null ? talisman.GetMaxHealthMultiplier() : 1f);
        public bool IsDead => isDead;
        public bool IsGameOver => isGameOver;
        public bool IsStunned => Time.time < stunnedUntil;
        /// <summary>Rival fijado con Lock-On (null si no hay ninguno).</summary>
        public EnemyController LockTarget => lockTarget;

        /// <summary>Se dispara cada vez que un ataque enemigo se resuelve contra Yari (para feedback, sonido, etc.).</summary>
        public event Action<AttackResult> OnAttackReceived;
        /// <summary>Se dispara cuando un golpe de Yari conecta con un enemigo. bool = golpe pesado.</summary>
        public event Action<EnemyController, bool> OnAttackLanded;

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

            attackTimings = Resources.Load<AttackTimingTable>(AttackTimingTable.ResourceName);
            ApplyVisualGroundOffset();
            footIK = FootIK.Attach(animator, characterController);
        }

        /// <summary>
        /// El CharacterController flota sobre el suelo la distancia de su "Skin Width" (8 cm por defecto).
        /// Se baja el modelo esa misma distancia para que los pies queden apoyados.
        /// </summary>
        private void ApplyVisualGroundOffset()
        {
            if (animator == null || characterController == null || animator.transform == transform) return;
            Vector3 p = animator.transform.localPosition;
            p.y = -characterController.skinWidth + visualHeightOffset;
            animator.transform.localPosition = p;
        }

        private void Start()
        {
            currentHealth = MaxHealth;
            if (AyniPurificationManager.Instance != null)
            {
                AyniPurificationManager.Instance.OnCombatResolved += HandleCombatResolved;
            }
        }

        private void OnDestroy()
        {
            if (AyniPurificationManager.Instance != null)
            {
                AyniPurificationManager.Instance.OnCombatResolved -= HandleCombatResolved;
            }
        }

        /// <summary>Juicio Ayni resuelto: Yari remata con un puñetazo descendente o hace el gesto de perdón.</summary>
        private void HandleCombatResolved(EnemyController enemy, bool mercy)
        {
            if (isDead) return;
            CancelPendingAttack();

            // Remate en pareja: Yari se coloca a distancia de golpe y la cámara se acerca
            if (enemy != null) StartCoroutine(AlignForFinisher(enemy, mercy ? 1.5f : 1.15f, 0.12f));
            CombatFeedback.FinisherCamera(mercy ? 1.6f : 1.2f);

            if (enemy != null)
            {
                Vector3 dir = enemy.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
            }

            if (mercy)
            {
                inCombatStance = false;
                if (animator) animator.SetBool("InCombatStance", false);
                PlayState("Mercy_Offer", 0.2f);
                stunnedUntil = Mathf.Max(stunnedUntil, Time.time + 1.6f);
            }
            else
            {
                if (animator) animator.SetFloat("AttackSpeed", 1.2f);
                PlayState("Atk_Overhand", 0.08f);
                stunnedUntil = Mathf.Max(stunnedUntil, Time.time + 0.7f);
            }
        }

        /// <summary>Lleva a Yari a la distancia justa del rival antes del remate, para que el golpe le llegue.</summary>
        private IEnumerator AlignForFinisher(EnemyController enemy, float distance, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && enemy != null && !isDead)
            {
                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.y = 0f;
                float dist = toEnemy.magnitude;
                if (dist < 0.001f) break;

                float remainingTime = Mathf.Max(Time.deltaTime, duration - elapsed);
                float step = (dist - distance) * Mathf.Clamp01(Time.deltaTime / remainingTime);
                characterController.Move(toEnemy / dist * step);
                transform.rotation = Quaternion.LookRotation(toEnemy);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void OnEnable()
        {
            if (structure == null) structure = GetComponent<StructureSystem>();
            structure.OnStructureBroken += HandleGuardBroken;
        }

        private void OnDisable()
        {
            if (structure != null) structure.OnStructureBroken -= HandleGuardBroken;
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
            HookRootMotion();

            if (footIK != null) footIK.Suspended = isDead || jumpInAir;

            // Postura andina: solo en guardia de pelea, no al pasear, correr, agacharse, saltar o caer
            if (andeanStance == null && animator != null) andeanStance = animator.GetComponent<AndeanCombatStanceModifier>();
            if (andeanStance != null)
            {
                bool fighting = inCombatStance && !isDead && !jumpInAir && !isCrouching && !isSprinting;
                andeanStance.SetStanceWeight(fighting ? 1f : 0f);
            }

            // Tutorial o pausa: no se procesa ninguna entrada
            if (AyniGameState.InputLocked) return;

            if (isGameOver)
            {
                if (Input.GetKeyDown(KeyCode.R)) RestartScene();
                ApplyGravity();
                return;
            }

            if (isDead)
            {
                UpdateUpperGuardLayer();
                ApplyGravity();
                return;
            }

            // Aturdido por un golpe o por rotura de guardia: sin control
            if (IsStunned)
            {
                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, 0f, 10f * Time.deltaTime);
                if (animator) animator.SetFloat("Speed", currentAnimSpeed);
                UpdateUpperGuardLayer();
                ApplyGravity();
                return;
            }

            HandleLockOn();
            HandleCrouch();
            HandleMovement();
            HandleJump();
            ApplyGravity();
            HandleDefense();
            HandleAttacks();
            HandleMovementAndGravity();
            HandleCombatStanceTimer();
            HandleDilemmaInputs();
            UpdateUpperGuardLayer();
        }

        // ───────────────────────── Fijación de blanco ─────────────────────────

        /// <summary>Tab o clic central fijan al rival más cercano; se suelta al pulsar de nuevo, si muere o si se aleja.</summary>
        private void HandleLockOn()
        {
            if (lockTarget != null)
            {
                Vector3 to = lockTarget.transform.position - transform.position;
                to.y = 0f;
                if (lockTarget.IsDead || !lockTarget.isActiveAndEnabled || to.magnitude > lockOnBreakRange) lockTarget = null;
            }

            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(2))
            {
                lockTarget = lockTarget != null ? null : FindLockCandidate();
            }

            if (lockTarget != null) EnterCombatStance();
            SetAnimBool("IsLockedOn", lockTarget != null);
        }

        /// <summary>Mejor candidato a fijar: el rival vivo dentro del alcance más centrado en la cámara y más cercano.</summary>
        private EnemyController FindLockCandidate()
        {
            EnemyController best = null;
            float bestScore = float.MaxValue;

            Vector3 viewDir = cameraTransform != null ? cameraTransform.forward : transform.forward;
            viewDir.y = 0f;

            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;

                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > lockOnRange) continue;

                float angle = dist > 0.01f ? Vector3.Angle(viewDir, to) : 0f;
                float score = dist + angle * 0.1f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        /// <summary>
        /// Capa de brazos en guardia: al caminar con la guardia alta, las piernas siguen la locomoción
        /// y los brazos mantienen la guardia, en lugar de deslizarse con la pose estática.
        /// </summary>
        private void UpdateUpperGuardLayer()
        {
            if (animator == null || animator.layerCount < 2) return;

            bool wantUpperGuard = isGuarding && currentAnimSpeed > 0.1f && !isAttacking && Time.time >= guardReactionUntil;
            upperGuardWeight = Mathf.MoveTowards(upperGuardWeight, wantUpperGuard ? 1f : 0f, 8f * Time.deltaTime);
            animator.SetLayerWeight(1, upperGuardWeight);
        }

        private bool HasParam(string paramName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (cachedParamsFor != animator)
            {
                animatorParams.Clear();
                foreach (var p in animator.parameters) animatorParams.Add(p.nameHash);
                cachedParamsFor = animator;
            }
            return animatorParams.Contains(Animator.StringToHash(paramName));
        }

        private void SetAnimBool(string paramName, bool value)
        {
            if (HasParam(paramName)) animator.SetBool(paramName, value);
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

            bool locked = lockTarget != null;
            Vector3 toTarget = Vector3.zero;
            if (locked)
            {
                toTarget = lockTarget.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f) toTarget.Normalize();
            }

            Vector3 animMoveDir = Vector3.zero;

            // Sprint con LeftShift cuando se mueve, sin estar en guardia ni con el rival fijado
            if (hasMoveInput && Input.GetKey(KeyCode.LeftShift) && !isGuarding && !locked)
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

                // Con el rival fijado: hacia él a velocidad normal, de lado o hacia atrás más despacio
                if (locked && !isCrouching)
                {
                    speedToUse = Mathf.Lerp(lockStrafeSpeed, baseMoveSpeed, Mathf.Clamp01(Vector3.Dot(moveDir, toTarget)));
                }

                // Reducción de velocidad si está en guardia
                if (isGuarding && !isStunned)
                {
                    speedToUse *= 0.45f;
                    targetAnimSpeedVal = 0.5f;
                }

                float currentSpeed = speedToUse * (talisman != null ? talisman.GetSpeedMultiplier() : 1f);
                horizontalVelocity = moveDir * currentSpeed;

                if (!locked)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
                animMoveDir = moveDir * (isGuarding ? 0.6f : 1f);

                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, targetAnimSpeedVal, 8f * Time.deltaTime);
            }
            else
            {
                horizontalVelocity = Vector3.zero;
                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, 0f, 10f * Time.deltaTime);
            }

            if (animator) animator.SetFloat("Speed", currentAnimSpeed);

            // Con el rival fijado Yari siempre lo encara y la animación depende de hacia dónde se mueve respecto a él
            if (locked && toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion faceTarget = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, faceTarget, rotationSpeed * Time.deltaTime);
            }
            if (animator && HasParam("MoveX"))
            {
                Vector3 local = locked ? transform.InverseTransformDirection(animMoveDir) : Vector3.zero;
                animator.SetFloat("MoveX", local.x, 0.1f, Time.deltaTime);
                animator.SetFloat("MoveY", local.z, 0.1f, Time.deltaTime);
            }
        }

        private void HandleJump()
        {
            if (!characterController.isGrounded) return;

            // Salto estándar (Espacio sin estar en guardia)
            if (Input.GetKeyDown(KeyCode.Space) && !isGuarding && !isAttacking)
            {
                if (isCrouching)
                {
                    SetCrouch(false);
                }
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                float airTime = 2f * velocity.y / Mathf.Max(0.01f, -gravity);
                PlayJumpAnimation(airTime);
                jumpInAir = true;
                jumpStartTime = Time.time;
            }
        }

        /// <summary>
        /// Reproduce el clip de salto desde el instante del despegue (sin la preparación larga) y a la velocidad
        /// justa para que el aterrizaje del clip coincida con el del salto real.
        /// </summary>
        private void PlayJumpAnimation(float airTime)
        {
            if (!animator) return;

            bool hasTimings = attackTimings != null && attackTimings.jumpLand > attackTimings.jumpTakeoff;
            if (hasTimings && HasState("Jump"))
            {
                float clipAirTime = attackTimings.jumpLand - attackTimings.jumpTakeoff;
                float speed = Mathf.Clamp(clipAirTime / Mathf.Max(0.05f, airTime), 0.4f, 3f);
                if (HasParam("JumpSpeed")) animator.SetFloat("JumpSpeed", speed);
                animator.CrossFadeInFixedTime("Jump", 0.05f, 0, attackTimings.jumpTakeoff);
            }
            else
            {
                animator.SetTrigger("Jump");
            }
        }

        /// <summary>Al tocar el suelo se sale del clip de salto hacia la locomoción, sin esperar a que termine.</summary>
        private void HandleLanding()
        {
            if (!jumpInAir || Time.time - jumpStartTime < 0.15f || !characterController.isGrounded) return;
            jumpInAir = false;
            if (!animator) return;

            bool inJump = animator.GetCurrentAnimatorStateInfo(0).IsName("Jump") ||
                          animator.GetNextAnimatorStateInfo(0).IsName("Jump");
            if (!inJump) return;

            string landState = lockTarget != null && HasState("LockOn_Locomotion") ? "LockOn_Locomotion"
                             : inCombatStance ? "Combat_Locomotion" : "Relaxed_Locomotion";
            if (HasState(landState)) animator.CrossFadeInFixedTime(landState, landBlendTime);
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded && velocity.y < 0) velocity.y = -2f;

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            if (animator) animator.SetBool("IsGrounded", characterController.isGrounded);
            HandleLanding();
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
                SetGuard(false);
            }

            if (isGuarding)
            {
                EnterCombatStance();

                // Esquivas Direccionales estilo Sifu (Duck / Jump Avoid)
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.S))
                {
                    // Agacharse evita los ataques ALTOS durante la ventana de esquiva
                    dodgeUntil = Time.time + dodgeWindow;
                    dodgeEvades = AttackHeight.High;
                    guardReactionUntil = Time.time + 0.7f;
                    if (animator) animator.SetTrigger("DuckAvoid");
                }
                else if (Input.GetKeyDown(KeyCode.W))
                {
                    // Saltar evita los ataques BAJOS (barridos) durante la ventana de esquiva
                    dodgeUntil = Time.time + dodgeWindow;
                    dodgeEvades = AttackHeight.Low;
                    guardReactionUntil = Time.time + 0.7f;
                    if (animator) animator.SetTrigger("JumpAvoid");
                }
            }
        }

        private void SetGuard(bool guarding)
        {
            isGuarding = guarding;
            if (animator) animator.SetBool("IsGuarding", guarding);
        }

        private void HandleAttacks()
        {
            // 1. Leer la entrada y guardarla un instante (buffer) para poder encadenar golpes con fluidez
            if (!isGuarding)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    bufferedAttack = 1;
                    bufferedUntil = Time.time + inputBufferTime;
                }
                else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
                {
                    bufferedAttack = 2;
                    bufferedUntil = Time.time + inputBufferTime;
                }
            }
            if (bufferedAttack != 0 && Time.time > bufferedUntil) bufferedAttack = 0;

            // 2. Golpe en curso: impacto, encadenado o recuperación
            if (activeTrail != null && Time.time >= trailOffTime)
            {
                activeTrail.emitting = false;
                activeTrail = null;
            }

            if (isAttacking)
            {
                if (!attackHitDone) ApplyReachAssist();

                if (!attackHitDone && Time.time >= attackHitTime)
                {
                    attackHitDone = true;
                    ResolveAttackHit(currentAttack);
                }

                if (attackHitDone && Time.time >= attackCancelTime && bufferedAttack != 0 && !isGuarding)
                {
                    StartAttack(bufferedAttack == 2);
                }
                else if (Time.time >= attackEndTime)
                {
                    isAttacking = false;
                    comboExpireTime = Time.time + comboResetTime;
                    string idleState = lockTarget != null && HasState("LockOn_Locomotion") ? "LockOn_Locomotion" : "Combat_Locomotion";
                    if (animator && HasState(currentAttack.state) && HasState(idleState))
                    {
                        animator.CrossFadeInFixedTime(idleState, 0.15f);
                    }
                }
                return;
            }

            // 3. Sin golpe en curso
            if (Time.time > comboExpireTime)
            {
                lightCount = 0;
                heavyIndex = 0;
            }

            if (bufferedAttack != 0 && !isGuarding)
            {
                StartAttack(bufferedAttack == 2);
            }
        }

        private void StartAttack(bool isHeavy)
        {
            bufferedAttack = 0;
            if (isCrouching) SetCrouch(false);
            EnterCombatStance();
            FaceNearestEnemy();

            // Elegir el golpe según el punto del combo
            AttackDef def;
            if (!isHeavy)
            {
                if (lightCount >= LightChain.Length) lightCount = 0;
                def = LightChain[lightCount];
                lightCount++;
                heavyIndex = 0;
            }
            else
            {
                if (lightCount >= LightChain.Length) def = FrontKickFinisher;
                else if (lightCount >= 2) def = HeadbuttFinisher;
                else
                {
                    def = HeavyChain[heavyIndex % HeavyChain.Length];
                    heavyIndex++;
                }
                lightCount = 0;
            }

            // Tiempo de impacto del clip: medido si existe, estimado si no
            float contact = def.fallbackContact;
            if (attackTimings != null && attackTimings.TryGetContact(def.clip, out float measured)) contact = measured;

            // Se entra al clip poco antes del impacto (sin la preparación larga) y se reproduce acelerado
            float leadIn = def.heavy ? heavyLeadIn : lightLeadIn;
            float speed = Mathf.Max(0.1f, def.heavy ? heavyAnimSpeed : lightAnimSpeed);
            float startOffset = Mathf.Max(0f, contact - leadIn);

            currentAttack = def;
            isAttacking = true;
            attackHitDone = false;
            attackHitTime = Time.time + (contact - startOffset) / speed;
            attackCancelTime = attackHitTime + comboCancelAfterContact;
            attackEndTime = attackHitTime + (def.heavy ? heavyRecovery : lightRecovery);

            // Ayuda de alcance: solo cuando el rival queda justo fuera, para que el golpe no falle por centímetros
            reachAssistRemaining = 0f;
            if (lungeTarget != null && !lungeTarget.IsDead)
            {
                Vector3 toTarget = lungeTarget.transform.position - transform.position;
                toTarget.y = 0f;
                float reach = attackRange - 0.1f;
                float gap = toTarget.magnitude - reach;
                if (gap > 0f && gap <= attackReachAssist)
                {
                    reachAssistRemaining = gap;
                    reachAssistSpeed = gap / Mathf.Max(0.05f, attackHitTime - Time.time);
                }
            }

            if (def.impact >= 1) StartAttackTrail(def);

            if (animator)
            {
                if (HasState(def.state))
                {
                    animator.SetFloat("AttackSpeed", speed);
                    animator.CrossFadeInFixedTime(def.state, 0.05f, 0, startOffset);
                }
                else
                {
                    // Animator antiguo sin los estados del combo: ejecuta Ayni > 1. Generar Animator Controller
                    animator.SetTrigger(def.heavy ? "HeavyAttack" : "LightAttack");
                }
            }
        }

        /// <summary>
        /// Desplazamiento que trae la propia animación (el paso y el traslado de peso de cada golpe). Yari se mueve
        /// exactamente lo que se mueve el clip, así los pies pisan donde deben en vez de arrastrarse.
        /// Lo envía RootMotionRelay desde el objeto que tiene el Animator.
        /// </summary>
        private void HandleRootMotion(Vector3 delta)
        {
            if (!isAttacking || isDead || IsStunned || characterController == null) return;

            delta.y = 0f;
            delta *= attackRootMotionScale;
            if (delta.sqrMagnitude <= 0f) return;

            // No empujar al rival: si ya está pegado a él, se descarta la parte del paso que lo acerca más
            if (lungeTarget != null && !lungeTarget.IsDead)
            {
                Vector3 to = lungeTarget.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > 0.001f && dist <= lungeStopDistance)
                {
                    Vector3 dir = to / dist;
                    float toward = Vector3.Dot(delta, dir);
                    if (toward > 0f) delta -= dir * toward;
                }
            }

            characterController.Move(delta);
        }

        /// <summary>Conecta el desplazamiento de las animaciones (el Animator vive en el modelo hijo).</summary>
        private void HookRootMotion()
        {
            if (animator == null || rootMotionHookedFor == animator) return;
            rootMotionHookedFor = animator;

            var relay = animator.GetComponent<RootMotionRelay>();
            if (relay == null) relay = animator.gameObject.AddComponent<RootMotionRelay>();
            relay.OnRootMotion = HandleRootMotion;
        }

        /// <summary>Cubre el último tramo cuando el rival está justo fuera de alcance, para que el golpe no se quede corto.</summary>
        private void ApplyReachAssist()
        {
            if (reachAssistRemaining <= 0f) return;
            float step = Mathf.Min(reachAssistSpeed * Time.deltaTime, reachAssistRemaining);
            reachAssistRemaining -= step;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            characterController.Move(forward.normalized * step);
        }

        /// <summary>Estela en el miembro que golpea (mano, codo o pie), como en los golpes fuertes de Sifu.</summary>
        private void StartAttackTrail(AttackDef def)
        {
            if (!attackTrails || animator == null || !animator.isHuman || attackTimings == null) return;

            string boneName = attackTimings.GetStrikingBone(def.clip);
            if (string.IsNullOrEmpty(boneName) || !Enum.TryParse(boneName, out HumanBodyBones boneId)) return;
            if (boneId == HumanBodyBones.Head) return;

            if (!trails.TryGetValue(boneId, out TrailRenderer trail) || trail == null)
            {
                Transform bone = animator.GetBoneTransform(boneId);
                if (bone == null) return;

                var go = new GameObject("AttackTrail_" + boneId);
                go.transform.SetParent(bone, false);
                trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = CombatFeedback.SpriteMaterial;
                trail.time = 0.16f;
                trail.minVertexDistance = 0.02f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(1f, 0f));
                trail.numCapVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.9f, 0.7f), 1f) },
                    new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = gradient;
                trail.emitting = false;
                trails[boneId] = trail;
            }

            if (activeTrail != null && activeTrail != trail) activeTrail.emitting = false;
            trail.Clear();
            trail.emitting = true;
            activeTrail = trail;
            trailOffTime = attackHitTime + 0.1f;
        }

        private void CancelPendingAttack()
        {
            reachAssistRemaining = 0f;
            if (activeTrail != null)
            {
                activeTrail.emitting = false;
                activeTrail = null;
            }
            isAttacking = false;
            attackHitDone = true;
            bufferedAttack = 0;
            lightCount = 0;
            heavyIndex = 0;
        }

        private bool HasState(string stateName)
        {
            return animator != null && animator.runtimeAnimatorController != null &&
                   animator.HasState(0, Animator.StringToHash(stateName));
        }

        /// <summary>Reproduce un estado del Animator si existe; si no, dispara el trigger de respaldo.</summary>
        private void PlayState(string stateName, float fade, string fallbackTrigger = null)
        {
            if (!animator) return;
            if (HasState(stateName)) animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
            else if (!string.IsNullOrEmpty(fallbackTrigger)) animator.SetTrigger(fallbackTrigger);
        }

        /// <summary>Aplica el golpe a los enemigos que estén dentro del alcance y del arco frontal.</summary>
        private void ResolveAttackHit(AttackDef attack)
        {
            bool isHeavy = attack.heavy;
            float dmg = (isHeavy ? heavyAttackDamage : lightAttackDamage) * attack.damageMul * talisman.GetDamageMultiplier();
            float structDmg = (isHeavy ? 30f : 15f) * attack.structureMul * talisman.GetDamageMultiplier();

            Vector3 forward = transform.forward;
            forward.y = 0f;

            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;

                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.y = 0f;
                if (toEnemy.magnitude > attackRange) continue;
                if (toEnemy.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toEnemy) > attackArc * 0.5f) continue;

                Vector3 hitPoint = enemy.GetHitPoint(attack.reaction, transform.position);
                enemy.TakeHit(dmg, structDmg, transform.position, isHeavy, attack.reaction, attack.knockback);

                if (attack.impact >= 2) CombatFeedback.Finisher(hitPoint);
                else if (attack.impact == 1) CombatFeedback.HeavyHit(hitPoint);
                else CombatFeedback.LightHit(hitPoint);

                OnAttackLanded?.Invoke(enemy, isHeavy);
            }
        }

        /// <summary>Gira a Yari hacia el enemigo vivo más cercano si está a distancia de pelea.</summary>
        private void FaceNearestEnemy()
        {
            EnemyController nearest = lockTarget != null && !lockTarget.IsDead ? lockTarget : null;
            float best = autoFaceRange;

            var enemies = EnemyController.All;
            for (int i = 0; nearest == null && i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;

                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < best)
                {
                    best = dist;
                    nearest = enemy;
                }
            }

            lungeTarget = nearest;
            if (nearest == null) return;

            Vector3 dir = nearest.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
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
                    if (animator) animator.SetBool("InCombatStance", false);
                }
            }
        }

        public bool TryParry()
        {
            // Retorna true si el golpe impacta dentro de la ventana de desvío perfecto
            return isGuarding && (Time.time - guardStartTime <= parryWindow);
        }

        /// <summary>
        /// Los enemigos llaman a este método en el instante en que su golpe conecta.
        /// Resuelve esquiva, parry, bloqueo o golpe directo y aplica el daño correspondiente a Yari.
        /// </summary>
        public AttackResult ReceiveAttack(float damage, float structureDamage, AttackHeight height)
        {
            if (isDead || Time.time < invulnerableUntil) return AttackResult.Missed;

            AttackResult result;
            Vector3 impactPoint = transform.position + Vector3.up * (height == AttackHeight.High ? 1.4f : 0.6f) + transform.forward * 0.3f;

            if (Time.time < dodgeUntil && dodgeEvades == height)
            {
                result = AttackResult.Dodged;
            }
            else if (TryParry())
            {
                EnterCombatStance();
                guardReactionUntil = Time.time + 0.6f;
                PlayState("Parry_Deflect", 0.04f);
                CombatFeedback.Parry(impactPoint);
                result = AttackResult.Parried;
            }
            else if (isGuarding)
            {
                // Bloqueo pasivo: sin daño de vida, pero la estructura de Yari sufre
                structure.AddStructureDamage(structureDamage);
                guardReactionUntil = Time.time + 0.5f;
                if (!structure.IsBroken) PlayState("Guard_BlockHit", 0.05f);
                CombatFeedback.Block(impactPoint);
                result = AttackResult.Blocked;
            }
            else
            {
                // Golpe directo
                CancelPendingAttack();
                currentHealth = Mathf.Max(0f, currentHealth - damage);
                structure.AddStructureDamage(structureDamage * 0.5f);
                CombatFeedback.PlayerHurt(impactPoint);

                if (currentHealth <= 0f)
                {
                    Die();
                }
                else
                {
                    PlayHitReaction();
                    stunnedUntil = Mathf.Max(stunnedUntil, Time.time + hitStunDuration);
                }
                result = AttackResult.Hit;
            }

            OnAttackReceived?.Invoke(result);
            return result;
        }

        public void PlayHitReaction()
        {
            if (isDead) return;
            EnterCombatStance();
            // Alterna golpe a la cabeza y al cuerpo
            nextHitToBody = !nextHitToBody;
            if (nextHitToBody) PlayState("Impact_HitBody", 0.06f, "Hit");
            else if (animator) animator.SetTrigger("Hit");
        }

        /// <summary>La estructura de Yari se llenó: pierde la guardia y queda expuesto unos instantes.</summary>
        private void HandleGuardBroken()
        {
            if (isDead) return;

            Debug.Log("[Ayni] ¡La guardia de Yari se ha roto! Queda expuesto.");
            CancelPendingAttack();
            SetGuard(false);
            stunnedUntil = Time.time + guardBreakStun;
            EnterCombatStance();
            PlayState("Impact_HitHeavy", 0.08f, "Hit");
            StartCoroutine(RecoverGuardRoutine());
        }

        private IEnumerator RecoverGuardRoutine()
        {
            yield return new WaitForSeconds(guardBreakStun);
            if (!isDead && structure.IsBroken) structure.ResetStructure();
        }

        private void Die()
        {
            isDead = true;
            CancelPendingAttack();
            isSprinting = false;
            SetGuard(false);
            SetCrouch(false);
            currentAnimSpeed = 0f;
            if (animator)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetTrigger("Die");
            }
            StartCoroutine(DeathRoutine());
        }

        /// <summary>Yari cae; tras unos segundos el Talismán Illa lo resucita a cambio de años de vida.</summary>
        private IEnumerator DeathRoutine()
        {
            yield return new WaitForSeconds(reviveDelay);

            if (talisman.TriggerResurrection())
            {
                structure.ResetStructure();
                currentHealth = MaxHealth; // La vida máxima baja con la edad
                isDead = false;

                if (animator)
                {
                    animator.ResetTrigger("Die");
                    animator.ResetTrigger("Hit");
                }

                if (HasState("GetUp"))
                {
                    // Se levanta del suelo: sin control e invulnerable mientras dura
                    animator.CrossFade("GetUp", 0.04f, 0, GetUpStartNormalized);
                    stunnedUntil = Time.time + getUpDuration;
                    invulnerableUntil = Time.time + getUpDuration + reviveInvulnerability;
                }
                else
                {
                    stunnedUntil = 0f;
                    invulnerableUntil = Time.time + reviveInvulnerability;
                    if (animator) animator.CrossFadeInFixedTime("Combat_Locomotion", 0.25f);
                }
                EnterCombatStance();
            }
            else
            {
                // El talismán se rompió por exceso de edad: muerte definitiva
                isGameOver = true;
            }
        }

        private void RestartScene()
        {
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // En el Editor la escena puede no estar en Build Settings
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
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
