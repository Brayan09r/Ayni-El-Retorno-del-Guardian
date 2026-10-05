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
        [Tooltip("Velocidades base (m/s); con la Illa joven Yari va un 15 % más rápido. Están ajustadas a lo que cubren " +
                 "los pasos de las animaciones (ver AyniTuning): si se suben, los pies patinan.")]
        [SerializeField] private float baseMoveSpeed = 3.6f;
        [SerializeField] private float sprintSpeed = 4.9f;
        [SerializeField] private float crouchSpeed = 1.1f;
        [SerializeField] private float jumpHeight = 1.6f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -18f;

        [Header("Caminar con el stick")]
        [Tooltip("Por debajo de esta inclinación del stick Yari camina; por encima, corre como con el teclado.")]
        [Range(0.3f, 0.95f)] [SerializeField] private float runStickThreshold = 0.75f;
        [Tooltip("Velocidad al caminar con el stick apenas inclinado (m/s).")]
        [SerializeField] private float walkPaceMin = 0.85f;
        [Tooltip("Velocidad al caminar con el stick justo por debajo del umbral de correr (m/s).")]
        [SerializeField] private float walkPaceMax = 1.2f;
        [Tooltip("Parte de la velocidad de carrera con el stick recién pasado el umbral; a fondo (o con el teclado) corre al 100 %.")]
        [Range(0.5f, 1f)] [SerializeField] private float runPaceAtThreshold = 0.72f;

        [Header("Cadencia de los pasos")]
        [Tooltip("La animación se acelera o se frena para que los pies pisen a la velocidad a la que Yari avanza de verdad. " +
                 "Estos son los topes: por encima las piernas se verían a cámara rápida y se prefiere que patinen un poco.")]
        [SerializeField] private float maxRunCadence = 1.05f;
        [SerializeField] private float maxSprintCadence = 1.3f;
        [SerializeField] private float maxCrouchCadence = 1.4f;
        [Tooltip("Tope para los pasos laterales y hacia atrás con el rival fijado.")]
        [SerializeField] private float maxStrafeCadence = 1.3f;

        [Header("Salto en carrera")]
        [Tooltip("A partir de este ritmo (1 = corriendo a fondo) el salto se convierte en un salto largo hacia delante.")]
        [SerializeField] private float leapMinPace = 0.7f;
        [Tooltip("Cuánto se acelera Yari en el aire respecto a la velocidad a la que venía corriendo.")]
        [SerializeField] private float leapSpeedBoost = 1.3f;
        [Tooltip("Altura del salto largo (m). Algo más bajo que el salto parado: se va lejos, no alto.")]
        [SerializeField] private float leapHeight = 1.05f;
        [Tooltip("Grados por segundo que se puede corregir la dirección en el aire.")]
        [SerializeField] private float leapSteer = 50f;

        [Header("Postura y Agachado")]
        [SerializeField] private float combatStanceDuration = 4.5f;
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private Vector3 standingCenter = new Vector3(0f, 1f, 0f);
        [SerializeField] private float crouchHeight = 1.15f;
        [SerializeField] private Vector3 crouchCenter = new Vector3(0f, 0.575f, 0f);
        [SerializeField] private float crouchLerpSpeed = 10f;

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
        [Tooltip("Segundos de preparación del clip que se ven antes del impacto en los golpes ligeros (menos = más seco).")]
        [SerializeField] private float lightWindup = 0.26f;
        [Tooltip("Segundos de preparación del clip que se ven antes del impacto en los golpes pesados.")]
        [SerializeField] private float heavyWindup = 0.46f;
        [Tooltip("Velocidad de reproducción de los golpes ligeros.")]
        [SerializeField] private float lightStrikeSpeed = 1.3f;
        [Tooltip("Velocidad de reproducción de los golpes pesados.")]
        [SerializeField] private float heavyStrikeSpeed = 1.2f;
        [Tooltip("Segundos que el golpe ligero sigue su recorrido tras el impacto antes de poder encadenar el siguiente. " +
                 "Con poco tiempo el golpe siguiente corta al anterior nada más conectar.")]
        [SerializeField] private float lightFollowThrough = 0.20f;
        [Tooltip("Segundos que el golpe pesado sigue su recorrido tras el impacto antes de poder encadenar el siguiente.")]
        [SerializeField] private float heavyFollowThrough = 0.30f;
        [Tooltip("Segundos tras el impacto en que Yari recupera el control si no encadena (ligero).")]
        [SerializeField] private float lightRecoverTime = 0.46f;
        [Tooltip("Segundos tras el impacto en que Yari recupera el control si no encadena (pesado).")]
        [SerializeField] private float heavyRecoverTime = 0.66f;
        [Tooltip("Segundos de mezcla al entrar en cada golpe. Muy corto se ve como un salto de pose.")]
        [SerializeField] private float attackBlendTime = 0.09f;
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
        [Tooltip("Segundos de invulnerabilidad del balanceo lateral (Guardia + izquierda/derecha): evita golpes altos y bajos.")]
        [SerializeField] private float swayWindow = 0.36f;
        [Tooltip("Segundos tras una esquiva antes de poder hacer la siguiente.")]
        [SerializeField] private float avoidCooldown = 0.38f;
        [Tooltip("Cuánto hay que inclinar el stick (o la tecla) en guardia para que cuente como esquiva.")]
        [Range(0.2f, 0.95f)] [SerializeField] private float avoidStickThreshold = 0.55f;
        [Tooltip("Segundos tras una esquiva o desvío logrados en los que el siguiente golpe es un contraataque (más daño a la postura).")]
        [SerializeField] private float counterWindow = 0.8f;
        [Tooltip("Multiplicador del daño a la postura del contraataque.")]
        [SerializeField] private float counterStructureBonus = 1.6f;
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
        [Tooltip("Velocidad al retroceder con el rival fijado (m/s). El clip de paso atrás cubre menos que los laterales.")]
        [SerializeField] private float lockBackSpeed = 2.0f;

        [Header("Salto")]
        [Tooltip("Segundos de mezcla al aterrizar para volver a la locomoción.")]
        [SerializeField] private float landBlendTime = 0.15f;

        [Header("Caídas")]
        [Tooltip("Velocidad de caída (m/s, negativa) a partir de la que Yari pasa a la animación de caída en el aire.")]
        [SerializeField] private float fallAnimSpeed = -7.5f;
        [Tooltip("Metros que tiene que haber caído desde su punto más alto para usar la animación de caída.")]
        [SerializeField] private float fallAnimMinDrop = 1.2f;
        [Tooltip("Desde esta altura de caída Yari aterriza pesado (rodilla y mano al suelo) y tarda un instante en recuperarse.")]
        [SerializeField] private float hardLandingHeight = 3.5f;
        [Tooltip("Desde esta altura de caída el golpe contra el suelo le quita vida.")]
        [SerializeField] private float fallDamageHeight = 9f;
        [SerializeField] private float fallDamagePerMeter = 6f;
        [Tooltip("Cámara lenta mientras Yari cae a la quebrada (1 = velocidad normal).")]
        [Range(0.2f, 1f)] [SerializeField] private float abyssFallTimeScale = 0.55f;
        [Tooltip("Segundos (reales) que la pantalla queda en negro antes de que el Illa lo devuelva al camino.")]
        [SerializeField] private float abyssBlackoutTime = 1.7f;

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

        private Vector3 velocity;
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

        // Cadencia de los pasos: la animación se reproduce al ritmo justo para que los pies no patinen
        // Metros por segundo que cubren los pasos de cada clip a velocidad normal. Los mide AyniAttackTimingBaker al
        // generar el Animator y se leen de YariAttackTimings; estos valores solo valen si falta esa tabla.
        private float walkStride = 1.07f;
        private float runStride = 2.37f;
        private float sprintStride = 2.95f;
        private float crouchStride = 0.95f;
        private float strafeLeftStride = 3.0f;
        private float strafeRightStride = 2.3f;
        private float backStride = 1.66f;
        private float runBlend = 1f;      // 0 = caminando, 1 = corriendo (suavizado)
        private float lastGroundedAt = -10f;
        private float jumpPressedAt = -10f;
        // Salto en carrera: en el aire Yari conserva la dirección y la velocidad con las que despegó
        private bool leaping;
        private Vector3 leapDir;
        private float leapSpeed;
        private Vector3 lastMoveDir;
        private float lastMoveSpeed;
        private float paceRatio;
        private int paceFrame = -10;
        private float locomotionCadence = 1f;
        private float appliedCadence = 1f;

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
        private bool dodgeEvadesAll;

        // Esquivas estilo Sifu: en guardia Yari no se desplaza; la dirección del stick elige la esquiva
        private enum AvoidKind { None, Duck, Jump, SwayLeft, SwayRight }
        private int lastAvoidDir;          // 0 = neutro, 1 = abajo, 2 = arriba, 3 = izquierda, 4 = derecha
        private float avoidReadyTime;
        private float counterUntil;

        // Caídas
        private bool airborne;
        private float airPeakY;
        private bool fallAnimPlaying;
        private bool beingRescued;

        public bool IsGuarding => isGuarding;
        /// <summary>Yari cae por el aire con la animación de caída.</summary>
        public bool IsFalling => fallAnimPlaying;
        /// <summary>Yari cae a la quebrada y muere; el Illa lo devolverá al camino. Durante la secuencia no hay control.</summary>
        public bool IsBeingRescued => beingRescued;
        /// <summary>Una escena cinemática controla a Yari: no lee la entrada ni aplica la física.</summary>
        public bool CinematicControl { get; set; }
        /// <summary>Se dispara cuando Yari esquiva un golpe (para sonido, cámara lenta, etc.).</summary>
        public event Action OnAvoided;
        public bool IsCrouching => isCrouching;
        public bool IsSprinting => isSprinting;
        /// <summary>
        /// Ritmo al que Yari se desplaza por su propio pie en este fotograma: 0 parado, alrededor de 0.4 caminando,
        /// 1 corriendo a fondo y más de 1 en sprint. Lo usa la cámara para dar sensación de velocidad.
        /// </summary>
        public float PaceRatio => Time.frameCount - paceFrame <= 1 ? paceRatio : 0f;
        public bool InCombatStance => inCombatStance;

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

            attackTimings = Resources.Load<AttackTimingTable>(AttackTimingTable.ResourceName);
            if (attackTimings != null)
            {
                walkStride = attackTimings.GetGroundSpeed("Walk_Forward_InPlace", walkStride);
                // Si no está el clip de carrera, el Animator usa el de trote en su lugar
                runStride = attackTimings.GetGroundSpeed("Run_Forward_InPlace",
                            attackTimings.GetGroundSpeed("Jog_Forward_InPlace", runStride));
                sprintStride = attackTimings.GetGroundSpeed("Sprint_Run_InPlace", sprintStride);
                crouchStride = attackTimings.GetGroundSpeed("Crouch_Walk_InPlace", crouchStride);
                strafeLeftStride = attackTimings.GetGroundSpeed("Strafe_Left", strafeLeftStride);
                strafeRightStride = attackTimings.GetGroundSpeed("Strafe_Right", strafeRightStride);
                backStride = attackTimings.GetGroundSpeed("Walk_Back", backStride);
            }
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

        private void Update()
        {
            // El componente Animator puede residir en el hijo Visual_Yari_3D
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            HookRootMotion();

            if (footIK != null) footIK.Suspended = isDead || jumpInAir || fallAnimPlaying || CinematicControl;

            // Postura andina: solo en guardia de pelea, no al pasear, correr, agacharse, saltar o caer
            if (andeanStance == null && animator != null) andeanStance = animator.GetComponent<AndeanCombatStanceModifier>();
            if (andeanStance != null)
            {
                bool fighting = inCombatStance && !isDead && !jumpInAir && !fallAnimPlaying && !isCrouching && !isSprinting && !CinematicControl;
                andeanStance.SetStanceWeight(fighting ? 1f : 0f);
            }

            // Escena cinemática: la escena mueve y anima a Yari
            if (CinematicControl) return;

            // Tutorial o pausa: no se procesa ninguna entrada
            if (AyniGameState.InputLocked) return;

            if (isGameOver)
            {
                if (AyniInput.Down(AyniInput.Action.Restart)) RestartScene();
                ApplyGravity();
                return;
            }

            // Rescate del Illa en curso: la rutina mueve a Yari
            if (beingRescued) return;

            // Cayendo por el aire: solo un poco de control de dirección
            if (fallAnimPlaying)
            {
                // Si la caída viene de un salto en carrera, conserva su impulso
                if (leaping) LeapAirMove();
                else HandleAirControl();
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
            HandleCombatStanceTimer();
            HandleDilemmaInputs();
            UpdateUpperGuardLayer();
        }

        /// <summary>
        /// Aplica la cadencia de los pasos (velocidad del Animator) solo mientras Yari está en un estado de
        /// locomoción. En cualquier otro caso la devuelve a 1 y no vuelve a tocarla: los golpes, las escenas
        /// y la micro-pausa de los impactos usan su propia velocidad.
        /// </summary>
        private void LateUpdate()
        {
            if (animator == null || CinematicControl) return;
            if (animator.speed < 0.1f) return; // micro-pausa de un impacto en curso

            // En las cuestas el CharacterController pierde el suelo un fotograma de vez en cuando: se da un margen
            // para que la cadencia no salte a 1 y vuelva
            if (characterController.isGrounded) lastGroundedAt = Time.time;
            bool onGround = Time.time - lastGroundedAt < 0.2f;

            bool paced = locomotionCadence < 0.999f || locomotionCadence > 1.001f;
            bool canScale = paced && !isAttacking && !isDead && !isGuarding && !jumpInAir && !fallAnimPlaying &&
                            !beingRescued && !IsStunned && !AyniGameState.InputLocked &&
                            onGround && InLocomotionState();

            if (canScale)
            {
                appliedCadence = Mathf.MoveTowards(appliedCadence, locomotionCadence, 3f * Time.deltaTime);
                animator.speed = appliedCadence;
            }
            else if (appliedCadence != 1f)
            {
                // Solo se restaura si la velocidad sigue siendo la que puso este script
                if (Mathf.Abs(animator.speed - appliedCadence) < 0.002f) animator.speed = 1f;
                appliedCadence = 1f;
            }
        }

        private static readonly int[] LocomotionStates =
        {
            Animator.StringToHash("Relaxed_Locomotion"), Animator.StringToHash("Combat_Locomotion"),
            Animator.StringToHash("Crouch_Locomotion"), Animator.StringToHash("LockOn_Locomotion")
        };

        private bool InLocomotionState()
        {
            int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            int next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).shortNameHash : current;
            bool currentOk = false, nextOk = false;
            for (int i = 0; i < LocomotionStates.Length; i++)
            {
                if (LocomotionStates[i] == current) currentOk = true;
                if (LocomotionStates[i] == next) nextOk = true;
            }
            return currentOk && nextOk;
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

            if (AyniInput.Down(AyniInput.Action.LockOn))
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
            if (isAttacking) return;

            // Alternar con C (B en el mando) o mantener con Control Izquierdo.
            // Con un rival listo para el Juicio Ayni, B del mando es "rematar" y no agacha.
            if (AyniInput.Down(AyniInput.Action.CrouchToggle) && !isGuarding &&
                !(AyniInput.UsingGamepad && HasJudgeableEnemy()))
            {
                SetCrouch(!isCrouching);
            }
            else if (AyniInput.Held(AyniInput.Action.CrouchHold) && !isCrouching)
            {
                SetCrouch(true);
            }
            else if (AyniInput.Up(AyniInput.Action.CrouchHold) && isCrouching)
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
            if (leaping)
            {
                LeapAirMove();
                return;
            }
            if (isAttacking) return;

            if (cameraTransform == null)
            {
                if (Camera.main != null) cameraTransform = Camera.main.transform;
                else return;
            }

            // En guardia Yari se planta como en Sifu: no camina; la dirección solo sirve para esquivar (HandleDefense)
            if (isGuarding)
            {
                HoldGuardStance();
                return;
            }

            Vector2 move = AyniInput.Move;
            float analog = Mathf.Clamp01(move.magnitude); // con el stick, inclinarlo poco = caminar despacio
            Vector3 direction = analog > 0.001f ? new Vector3(move.x, 0f, move.y) / analog : Vector3.zero;

            bool hasMoveInput = analog >= 0.1f;

            bool locked = lockTarget != null;
            Vector3 toTarget = Vector3.zero;
            if (locked)
            {
                toTarget = lockTarget.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f) toTarget.Normalize();
            }

            Vector3 animMoveDir = Vector3.zero;
            locomotionCadence = 1f;

            // Con el stick, la inclinación decide entre caminar y correr y RT / L3 es el sprint.
            // Con el teclado se camina, Shift hace correr y no hay sprint.
            bool stick = AyniInput.MoveFromStick;
            bool runHeld = AyniInput.Held(AyniInput.Action.Sprint);
            if (hasMoveInput && analog > 0.5f && runHeld && !locked)
            {
                if (isCrouching) SetCrouch(false); // Salir de cuclillas al correr
                isSprinting = stick;
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

                // Con el rival fijado: hacia él a velocidad normal, de lado o hacia atrás más despacio. La velocidad y
                // lo que cubren los pasos se reparten según cuánto del movimiento va en cada dirección (como el
                // BlendTree de LockOn_Locomotion, que mezcla correr, retroceder y los dos laterales).
                float lockStride = runStride;
                if (locked && !isCrouching)
                {
                    Vector3 local = toTarget.sqrMagnitude > 0.5f
                        ? Quaternion.Inverse(Quaternion.LookRotation(toTarget)) * moveDir
                        : transform.InverseTransformDirection(moveDir);
                    float fwd = Mathf.Max(0f, local.z), back = Mathf.Max(0f, -local.z);
                    float left = Mathf.Max(0f, -local.x), right = Mathf.Max(0f, local.x);
                    float sum = Mathf.Max(0.001f, fwd + back + left + right);
                    speedToUse = (fwd * baseMoveSpeed + back * lockBackSpeed + (left + right) * lockStrafeSpeed) / sum;
                    lockStride = (fwd * runStride + back * backStride + left * strafeLeftStride + right * strafeRightStride) / sum;
                }

                float speedMul = talisman.GetSpeedMultiplier();
                float currentSpeed = speedToUse * speedMul;

                // La animación conserva siempre la zancada completa; lo que cambia es la cadencia de los pasos
                // (velocidad real / lo que cubre el clip), para que los pies pisen donde Yari avanza de verdad.
                if (isSprinting)
                {
                    runBlend = 1f;
                    locomotionCadence = Mathf.Clamp(currentSpeed / sprintStride, 0.9f, maxSprintCadence);
                }
                else if (isCrouching)
                {
                    // Agachado: más despacio con el stick a medias
                    currentSpeed *= Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(0.1f, 0.9f, analog));
                    locomotionCadence = Mathf.Clamp(currentSpeed / crouchStride, 0.6f, maxCrouchCadence);
                }
                else if (!locked)
                {
                    // Dos marchas: caminar y correr. Con el stick, pasado el umbral corre suave y acelera hasta el
                    // tope con el stick a fondo; con el teclado camina y corre a fondo mientras se mantiene Shift.
                    bool wantsRun = stick ? analog >= runStickThreshold : runHeld;
                    runBlend = Mathf.MoveTowards(runBlend, wantsRun ? 1f : 0f, 4.5f * Time.deltaTime);
                    float smooth = runBlend * runBlend * (3f - 2f * runBlend);

                    float walkSpeed = Mathf.Lerp(walkPaceMin, walkPaceMax, Mathf.InverseLerp(0.1f, runStickThreshold, analog)) * speedMul;
                    float runSpeed = currentSpeed * Mathf.Lerp(runPaceAtThreshold, 1f, Mathf.InverseLerp(runStickThreshold, 0.97f, analog));
                    float walkCadence = Mathf.Clamp(walkSpeed / walkStride, 0.7f, 1.3f);
                    float runCadence = Mathf.Clamp(runSpeed / runStride, 0.7f, maxRunCadence);

                    currentSpeed = Mathf.Lerp(walkSpeed, runSpeed, smooth);
                    locomotionCadence = Mathf.Lerp(walkCadence, runCadence, smooth);
                    targetAnimSpeedVal = Mathf.Lerp(0.5f, 1f, smooth);
                }
                else
                {
                    // Con el rival fijado: más despacio con el stick a medias, y los pasos al ritmo de esa velocidad
                    currentSpeed *= Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(0.1f, 0.9f, analog));
                    locomotionCadence = Mathf.Clamp(currentSpeed / Mathf.Max(0.1f, lockStride), 0.6f, maxStrafeCadence);
                }

                characterController.Move(moveDir * (currentSpeed * Time.deltaTime));
                paceRatio = currentSpeed / Mathf.Max(0.1f, baseMoveSpeed * speedMul);
                paceFrame = Time.frameCount;
                lastMoveDir = moveDir;
                lastMoveSpeed = currentSpeed;

                if (!locked)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
                animMoveDir = moveDir;

                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, targetAnimSpeedVal, 8f * Time.deltaTime);
            }
            else
            {
                currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, 0f, 10f * Time.deltaTime);
                // Desde parado siempre se arranca caminando y, si toca correr, se acelera
                runBlend = 0f;
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

        /// <summary>
        /// Guardia plantada estilo Sifu: los pies no se mueven del sitio. Yari solo gira para encarar al rival
        /// (el fijado o el más cercano) y la locomoción se detiene.
        /// </summary>
        private void HoldGuardStance()
        {
            isSprinting = false;
            locomotionCadence = 1f;
            currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, 0f, 14f * Time.deltaTime);
            if (animator)
            {
                animator.SetFloat("Speed", currentAnimSpeed);
                if (HasParam("MoveX"))
                {
                    animator.SetFloat("MoveX", 0f, 0.1f, Time.deltaTime);
                    animator.SetFloat("MoveY", 0f, 0.1f, Time.deltaTime);
                }
            }

            EnemyController target = lockTarget != null && !lockTarget.IsDead ? lockTarget : NearestEnemy(autoFaceRange + 1f);
            if (target == null) return;
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), rotationSpeed * 0.8f * Time.deltaTime);
            }
        }

        private EnemyController NearestEnemy(float range)
        {
            EnemyController nearest = null;
            float best = range;
            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
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
            return nearest;
        }

        /// <summary>Hay un rival con la postura rota a distancia de Juicio Ayni (A / B del mando cambian de función).</summary>
        public bool HasJudgeableEnemy()
        {
            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.CanBeJudged) continue;
                if (Vector3.Distance(transform.position, enemy.transform.position) <= (enemy.IsBoss ? 9f : 3.2f)) return true;
            }
            return false;
        }

        /// <summary>En plena caída Yari puede corregir un poco la dirección, pero no correr ni girar en seco.</summary>
        private void HandleAirControl()
        {
            if (cameraTransform == null) return;
            Vector2 move = AyniInput.Move;
            if (move.sqrMagnitude < 0.01f) return;

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            Vector3 dir = camForward.normalized * move.y + camRight.normalized * move.x;
            characterController.Move(dir * (baseMoveSpeed * 0.35f * Time.deltaTime));
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * 0.25f * Time.deltaTime);
            }
        }

        private void HandleJump()
        {
            // La pulsación se recuerda un instante y el suelo también: corriendo por terreno irregular el
            // CharacterController pierde el suelo algún fotograma suelto, y si coincidía con la pulsación el salto se perdía.
            if (AyniInput.Down(AyniInput.Action.Jump)) jumpPressedAt = Time.time;
            bool onGround = characterController.isGrounded || Time.time - lastGroundedAt < 0.12f;
            if (!onGround || jumpInAir || velocity.y > 0.5f) return;

            // Salto estándar (Espacio / A sin estar en guardia). Con un rival para el Juicio Ayni, A del mando es "perdonar".
            if (Time.time - jumpPressedAt < 0.15f && !isGuarding && !isAttacking &&
                !(AyniInput.UsingGamepad && HasJudgeableEnemy()))
            {
                jumpPressedAt = -10f;
                if (isCrouching)
                {
                    SetCrouch(false);
                }

                // Corriendo (sin rival fijado) el salto es largo: Yari no se frena, sale lanzado hacia donde corría
                bool runningJump = lockTarget == null && Time.frameCount == paceFrame && paceRatio >= leapMinPace &&
                                   lastMoveDir.sqrMagnitude > 0.5f;

                velocity.y = Mathf.Sqrt((runningJump ? leapHeight : jumpHeight) * -2f * gravity);
                float airTime = 2f * velocity.y / Mathf.Max(0.01f, -gravity);
                PlayJumpAnimation(airTime, runningJump);
                jumpInAir = true;
                jumpStartTime = Time.time;

                if (runningJump)
                {
                    leaping = true;
                    leapDir = lastMoveDir.normalized;
                    leapSpeed = lastMoveSpeed * leapSpeedBoost;
                    runBlend = 1f;
                }
            }
        }

        /// <summary>
        /// En el aire durante un salto en carrera: Yari sigue en la dirección y a la velocidad del despegue.
        /// La dirección se puede corregir un poco, pero no frenar ni dar media vuelta.
        /// </summary>
        private void LeapAirMove()
        {
            if (cameraTransform != null)
            {
                Vector2 move = AyniInput.Move;
                if (move.sqrMagnitude > 0.04f)
                {
                    Vector3 camForward = cameraTransform.forward;
                    Vector3 camRight = cameraTransform.right;
                    camForward.y = 0f;
                    camRight.y = 0f;
                    Vector3 wanted = camForward.normalized * move.y + camRight.normalized * move.x;
                    if (wanted.sqrMagnitude > 0.01f)
                    {
                        leapDir = Vector3.RotateTowards(leapDir, wanted.normalized, leapSteer * Mathf.Deg2Rad * Time.deltaTime, 0f);
                    }
                }
            }

            characterController.Move(leapDir * (leapSpeed * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(leapDir), rotationSpeed * Time.deltaTime);
            locomotionCadence = 1f;
            paceFrame = Time.frameCount; // la cámara mantiene la sensación de velocidad durante el salto
        }

        /// <summary>
        /// Reproduce el clip de salto desde el instante del despegue (sin la preparación larga) y a la velocidad
        /// justa para que el aterrizaje del clip coincida con el del salto real.
        /// </summary>
        private void PlayJumpAnimation(float airTime, bool running)
        {
            if (!animator) return;

            // Salto en carrera: su propio clip, si está en el Animator y se pudo medir
            if (running && attackTimings != null && attackTimings.runJumpLand > attackTimings.runJumpTakeoff && HasState("Run_Jump"))
            {
                float clipAir = attackTimings.runJumpLand - attackTimings.runJumpTakeoff;
                float runSpeed = Mathf.Clamp(clipAir / Mathf.Max(0.05f, airTime), 0.4f, 3f);
                if (HasParam("JumpSpeed")) animator.SetFloat("JumpSpeed", runSpeed);
                animator.CrossFadeInFixedTime("Run_Jump", 0.06f, 0, attackTimings.runJumpTakeoff);
                return;
            }

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
            leaping = false;
            if (!animator) return;

            AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            bool inJump = now.IsName("Jump") || next.IsName("Jump") || now.IsName("Run_Jump") || next.IsName("Run_Jump");
            if (!inJump) return;

            string landState = lockTarget != null && HasState("LockOn_Locomotion") ? "LockOn_Locomotion"
                             : inCombatStance ? "Combat_Locomotion" : "Relaxed_Locomotion";
            if (HasState(landState)) animator.CrossFadeInFixedTime(landState, landBlendTime);
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded && velocity.y < 0) velocity.y = -2f;
            if (characterController.isGrounded) lastGroundedAt = Time.time;

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            if (animator) animator.SetBool("IsGrounded", characterController.isGrounded);
            HandleLanding();
            UpdateFalling();
        }

        // ───────────────────────── Caídas ─────────────────────────

        /// <summary>
        /// Al caer de una cornisa (o al final de un salto largo) Yari pasa a la animación de caída en el aire;
        /// al tocar el suelo aterriza normal, pesado (rodilla y mano al suelo) o se hace daño según la altura.
        /// </summary>
        private void UpdateFalling()
        {
            float y = transform.position.y;
            if (!characterController.isGrounded)
            {
                if (!airborne)
                {
                    airborne = true;
                    airPeakY = y;
                }
                airPeakY = Mathf.Max(airPeakY, y);

                if (!fallAnimPlaying && !isDead && velocity.y < fallAnimSpeed && airPeakY - y > fallAnimMinDrop && HasState("Fall_Loop"))
                {
                    fallAnimPlaying = true;
                    CancelPendingAttack();
                    if (isGuarding) SetGuard(false);
                    if (isCrouching) SetCrouch(false);
                    isSprinting = false;
                    animator.CrossFadeInFixedTime("Fall_Loop", 0.25f);
                }
                return;
            }

            if (!airborne) return;
            airborne = false;
            float drop = airPeakY - y;
            if (!fallAnimPlaying) return;

            fallAnimPlaying = false;
            jumpInAir = false;
            leaping = false;
            Land(drop);
        }

        private void Land(float drop)
        {
            if (isDead) return;

            if (drop >= hardLandingHeight && HasState("Land_Hard"))
            {
                // Aterrizaje pesado: queda clavado un instante, como en Sifu al saltar desde lo alto
                animator.CrossFadeInFixedTime("Land_Hard", 0.04f);
                stunnedUntil = Mathf.Max(stunnedUntil, Time.time + Mathf.Lerp(0.35f, 0.7f, Mathf.InverseLerp(hardLandingHeight, fallDamageHeight, drop)));
                CombatFeedback.Shake(Mathf.Lerp(0.05f, 0.14f, Mathf.InverseLerp(hardLandingHeight, 14f, drop)), 0.22f);
                CombatFeedback.Flash(transform.position + Vector3.up * 0.1f, new Color(0.85f, 0.75f, 0.6f, 0.8f), 1.6f, 0.2f);
            }
            else
            {
                string landState = lockTarget != null && HasState("LockOn_Locomotion") ? "LockOn_Locomotion"
                                 : inCombatStance ? "Combat_Locomotion" : "Relaxed_Locomotion";
                if (HasState(landState)) animator.CrossFadeInFixedTime(landState, 0.12f);
            }

            if (drop > fallDamageHeight)
            {
                float damage = (drop - fallDamageHeight) * fallDamagePerMeter;
                currentHealth = Mathf.Max(0f, currentHealth - damage);
                CombatFeedback.PlayerHurt(transform.position + Vector3.up * 0.5f);
                Debug.Log($"[Ayni] Yari cae desde {drop:F1} m y pierde {damage:F0} de vida.");
                if (currentHealth <= 0f) Die();
            }
        }

        private Coroutine poisonRoutine;

        /// <summary>Veneno de los dardos del Cazador: pierde vida poco a poco (sin llegar a matarlo) y la vista se nubla de verde.</summary>
        public void ApplyPoison(float seconds, float damagePerSecond)
        {
            if (isDead) return;
            if (poisonRoutine != null) StopCoroutine(poisonRoutine);
            poisonRoutine = StartCoroutine(PoisonRoutine(seconds, damagePerSecond));
        }

        private IEnumerator PoisonRoutine(float seconds, float damagePerSecond)
        {
            Ayni.UI.AyniScreenFX.Tint(new Color(0.18f, 0.55f, 0.12f), 0.22f);
            float t = 0f;
            while (t < seconds && !isDead)
            {
                currentHealth = Mathf.Max(1f, currentHealth - damagePerSecond * Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
            }
            Ayni.UI.AyniScreenFX.Tint(new Color(0.18f, 0.55f, 0.12f), 0f);
            poisonRoutine = null;
        }

        /// <summary>
        /// Yari cae a la quebrada y muere. La cámara se queda arriba, en el borde, y lo ve caer de espaldas hasta
        /// el agua; la pantalla se va a negro y el Illa lo resucita en el último suelo firme a cambio de años de
        /// vida, igual que cuando muere en combate (si la edad llega al límite, es el final).
        /// Lo llama AyniAbyssRescue.
        /// </summary>
        /// <param name="safePosition">Último suelo firme que pisó: ahí reaparece.</param>
        /// <param name="rimY">Altura del borde desde el que cayó (para colocar la cámara).</param>
        /// <param name="waterY">Altura del agua del fondo.</param>
        public void FallToDeath(Vector3 safePosition, float rimY, float waterY)
        {
            if (beingRescued) return;

            if (isDead)
            {
                // Ya había muerto en combate y el cuerpo rodó al vacío: resucitará arriba, no en el fondo
                Teleport(safePosition);
                return;
            }
            StartCoroutine(AbyssDeathRoutine(safePosition, rimY, waterY));
        }

        private IEnumerator AbyssDeathRoutine(Vector3 safePosition, float rimY, float waterY)
        {
            beingRescued = true;
            leaping = false;
            isDead = true;
            CancelPendingAttack();
            SetGuard(false);
            SetCrouch(false);
            isSprinting = false;
            lockTarget = null;
            currentAnimSpeed = 0f;
            currentHealth = 0f;
            fallAnimPlaying = true;   // sin apoyo de pies ni postura de pelea mientras cae
            if (poisonRoutine != null)
            {
                StopCoroutine(poisonRoutine);
                poisonRoutine = null;
                Ayni.UI.AyniScreenFX.Tint(Color.clear, 0f);
            }

            if (animator)
            {
                animator.speed = 1f;
                animator.SetFloat("Speed", 0f);
                animator.ResetTrigger("Hit");
                string fallState = HasState("Fall_Back") ? "Fall_Back" : "Fall_Loop";
                if (HasState(fallState)) animator.CrossFadeInFixedTime(fallState, 0.18f);
            }

            // La cámara deja de seguirlo: se queda sobre el vacío, a la altura del borde, mirándolo caer
            var cam = cameraTransform != null ? cameraTransform.GetComponent<ThirdPersonSifuCamera>() : null;
            if (cam != null) cam.WatchFall(rimY + 2.2f, 30f);
            Ayni.UI.AyniScreenFX.Letterbox(true);
            CombatFeedback.Shake(0.06f, 0.25f);

            // Caída a cámara lenta hasta el agua (o el fondo)
            float startTime = Time.unscaledTime;
            bool splashed = false;
            while (Time.unscaledTime - startTime < 3.5f)
            {
                Time.timeScale = abyssFallTimeScale; // cada fotograma: la micro-pausa de un golpe la devolvería a 1
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, 4f * Time.deltaTime);
                velocity.z = Mathf.MoveTowards(velocity.z, 0f, 4f * Time.deltaTime);
                velocity.y += gravity * Time.deltaTime;
                characterController.Move(velocity * Time.deltaTime);

                if (transform.position.y <= waterY + 0.9f)
                {
                    splashed = true;
                    break;
                }
                if (characterController.isGrounded && Time.unscaledTime - startTime > 0.25f) break;
                yield return null;
            }

            Time.timeScale = 1f;
            Vector3 impact = transform.position;
            if (splashed)
            {
                impact.y = waterY + 0.15f;
                CombatFeedback.Flash(impact, new Color(0.82f, 0.95f, 1f, 0.9f), 5.5f, 0.45f);
            }
            else
            {
                CombatFeedback.Flash(impact + Vector3.up * 0.2f, new Color(1f, 0.3f, 0.25f, 0.9f), 2.6f, 0.3f);
            }
            CombatFeedback.Shake(0.2f, 0.4f);

            Ayni.UI.AyniScreenFX.FadeTo(Color.black, 1f, 0.45f);
            yield return new WaitForSecondsRealtime(0.5f);
            Ayni.UI.AyniScreenFX.Title("YARI HA CAÍDO", "El abismo reclama su cuerpo... y el Illa, sus años.", abyssBlackoutTime + 0.4f);
            yield return new WaitForSecondsRealtime(abyssBlackoutTime);

            // La muerte se paga igual que en combate
            Teleport(safePosition);
            airborne = false;
            fallAnimPlaying = false;
            jumpInAir = false;
            leaping = false;
            if (cam != null)
            {
                cam.StopWatchingFall();
                cam.SnapBehindTarget();
            }
            Ayni.UI.AyniScreenFX.Letterbox(false);

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
                    animator.CrossFade("GetUp", 0.02f, 0, GetUpStartNormalized);
                    stunnedUntil = Time.time + getUpDuration;
                    invulnerableUntil = Time.time + getUpDuration + reviveInvulnerability;
                }
                else
                {
                    stunnedUntil = 0f;
                    invulnerableUntil = Time.time + reviveInvulnerability;
                    if (animator && HasState("Combat_Locomotion")) animator.CrossFadeInFixedTime("Combat_Locomotion", 0.25f);
                }
                EnterCombatStance();

                Ayni.UI.AyniScreenFX.FadeTo(Color.black, 0f, 1.1f);
                Ayni.UI.AyniScreenFX.Caption("El Illa te devuelve al camino.  <color=#ffcf6a>Ahora tienes " + talisman.CurrentAge + " años.</color>", 3.2f);
            }
            else
            {
                // El talismán se rompió por exceso de edad: muerte definitiva (el HUD ofrece reintentar)
                if (animator)
                {
                    if (HasState("Defeat_Death")) animator.CrossFadeInFixedTime("Defeat_Death", 0.02f);
                    else animator.SetTrigger("Die");
                }
                isGameOver = true;
                Ayni.UI.AyniScreenFX.FadeTo(Color.black, 0f, 1.6f);
            }

            yield return new WaitForSecondsRealtime(0.2f);
            beingRescued = false;
        }

        private void Teleport(Vector3 position)
        {
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
            velocity = Vector3.zero;
        }

        private void HandleDefense()
        {
            // Guardia mientras se mantiene pulsada (clic derecho, G o LB del mando)
            bool guardHeld = AyniInput.Held(AyniInput.Action.Guard);
            if (guardHeld && !isGuarding && !isAttacking)
            {
                if (isCrouching) SetCrouch(false);
                isGuarding = true;
                guardStartTime = Time.time;
                EnterCombatStance();
                if (animator) animator.SetBool("IsGuarding", true);
                // Si ya venía moviéndose, esa dirección no cuenta como esquiva: hay que volver a inclinar el stick
                lastAvoidDir = AvoidDirection();
            }
            else if (!guardHeld && isGuarding)
            {
                SetGuard(false);
            }

            if (!isGuarding) return;
            EnterCombatStance();

            // Esquivas estilo Sifu, sin moverse del sitio:
            //   abajo (S)            agacharse   → evita los golpes altos
            //   arriba (W) o salto   saltito     → evita los barridos
            //   izquierda / derecha  balanceo    → evita cualquier golpe, con una ventana más corta
            int dir = AvoidDirection();
            AvoidKind kind = AvoidKind.None;
            if (AyniInput.Down(AyniInput.Action.Jump)) kind = AvoidKind.Jump;
            else if (dir != 0 && dir != lastAvoidDir)
            {
                kind = dir == 1 ? AvoidKind.Duck : dir == 2 ? AvoidKind.Jump : dir == 3 ? AvoidKind.SwayLeft : AvoidKind.SwayRight;
            }
            lastAvoidDir = dir;

            if (kind != AvoidKind.None && Time.time >= avoidReadyTime) StartAvoid(kind);
        }

        /// <summary>Dirección dominante del stick o de WASD: 0 neutro, 1 abajo, 2 arriba, 3 izquierda, 4 derecha.</summary>
        private int AvoidDirection()
        {
            Vector2 move = AyniInput.Move;
            if (move.magnitude < avoidStickThreshold) return 0;
            if (Mathf.Abs(move.y) >= Mathf.Abs(move.x)) return move.y < 0f ? 1 : 2;
            return move.x < 0f ? 3 : 4;
        }

        private void StartAvoid(AvoidKind kind)
        {
            bool sway = kind == AvoidKind.SwayLeft || kind == AvoidKind.SwayRight;
            dodgeUntil = Time.time + (sway ? swayWindow : dodgeWindow);
            dodgeEvadesAll = sway;
            dodgeEvades = kind == AvoidKind.Jump ? AttackHeight.Low : AttackHeight.High;
            avoidReadyTime = Time.time + avoidCooldown;
            guardReactionUntil = Time.time + 0.7f;

            if (!animator) return;
            string state = kind == AvoidKind.Duck ? "Avoid_Duck"
                         : kind == AvoidKind.Jump ? "Avoid_Jump"
                         : kind == AvoidKind.SwayLeft ? "Avoid_SwayL" : "Avoid_SwayR";
            if (HasState(state))
            {
                animator.CrossFadeInFixedTime(state, 0.05f, 0, 0f);
            }
            else
            {
                // Animator sin las esquivas generadas: Ayni > Animaciones > 1. Generar Caídas y Esquivas
                animator.SetTrigger(kind == AvoidKind.Jump ? "JumpAvoid" : "DuckAvoid");
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
                int pressed = AyniInput.Down(AyniInput.Action.LightAttack) ? 1
                            : AyniInput.Down(AyniInput.Action.HeavyAttack) ? 2 : 0;
                if (pressed != 0)
                {
                    bufferedAttack = pressed;
                    bufferedUntil = Time.time + inputBufferTime;
                    // Pulsado en mitad de un golpe: queda en cola hasta que ese golpe termine su recorrido,
                    // así el siguiente sale encadenado en vez de cortarlo (o de perderse la pulsación)
                    if (isAttacking) bufferedUntil = Mathf.Max(bufferedUntil, attackCancelTime + 0.08f);
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
                        animator.CrossFadeInFixedTime(idleState, 0.2f);
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
            float leadIn = def.heavy ? heavyWindup : lightWindup;
            float speed = Mathf.Max(0.1f, def.heavy ? heavyStrikeSpeed : lightStrikeSpeed);
            float startOffset = Mathf.Max(0f, contact - leadIn);

            currentAttack = def;
            isAttacking = true;
            attackHitDone = false;
            attackHitTime = Time.time + (contact - startOffset) / speed;
            // El golpe conecta, sigue su recorrido y solo entonces deja paso al siguiente
            attackCancelTime = attackHitTime + (def.heavy ? heavyFollowThrough : lightFollowThrough);
            attackEndTime = attackHitTime + (def.heavy ? heavyRecoverTime : lightRecoverTime);

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
                    animator.CrossFadeInFixedTime(def.state, attackBlendTime, 0, startOffset);
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

            // Contraataque: el primer golpe justo después de esquivar o desviar castiga mucho más la postura
            bool counter = Time.time < counterUntil;
            if (counter)
            {
                structDmg *= counterStructureBonus;
                counterUntil = 0f;
            }

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
                else if (attack.impact == 1 || counter) CombatFeedback.HeavyHit(hitPoint);
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
            if (inCombatStance && !isGuarding)
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

            if (Time.time < dodgeUntil && (dodgeEvadesAll || dodgeEvades == height))
            {
                // Esquiva lograda: el golpe pasa rozando, un instante a cámara lenta y ventana de contraataque
                result = AttackResult.Dodged;
                counterUntil = Time.time + counterWindow;
                CombatFeedback.Avoid(impactPoint);
                OnAvoided?.Invoke();
            }
            else if (TryParry())
            {
                EnterCombatStance();
                guardReactionUntil = Time.time + 0.6f;
                counterUntil = Time.time + counterWindow;
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
            AyniGameState.ReloadLevel();
        }

        private void HandleDilemmaInputs()
        {
            // Interacción de ejecución o perdón cuando un jefe/rival tiene la postura rota
            // Teclado: F / X. Mando: B (botón rojo, Venganza) / A (botón verde, Ayni), como en la Biblia del juego.
            if (AyniInput.Down(AyniInput.Action.Execute))
            {
                AyniPurificationManager.Instance?.TriggerExecutionAction(transform.position, isAyniMercy: false);
            }
            else if (AyniInput.Down(AyniInput.Action.Mercy))
            {
                AyniPurificationManager.Instance?.TriggerExecutionAction(transform.position, isAyniMercy: true);
            }
        }
    }
}
