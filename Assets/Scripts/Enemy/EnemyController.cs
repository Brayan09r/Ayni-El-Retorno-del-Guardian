using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Player;

namespace Ayni.Enemy
{
    public enum EnemyState
    {
        Idle,      // Yari está lejos o caído
        Chase,     // Persigue y encara a Yari
        Windup,    // Anticipación visible del golpe (momento de leer y preparar el parry)
        Recover,   // Recuperación tras golpear
        Stagger,   // Interrumpido por un golpe o por un parry
        Stunned,   // Postura rota: vulnerable al Juicio Ayni (F / X)
        Downed,    // Derribado: cae, queda en el suelo y se levanta
        Dead
    }

    [RequireComponent(typeof(StructureSystem))]
    public class EnemyController : MonoBehaviour
    {
        /// <summary>Todos los enemigos activos de la escena (para golpes, HUD y futura fijación de blanco).</summary>
        public static readonly List<EnemyController> All = new List<EnemyController>();

        [Header("Datos del Rival")]
        [SerializeField] private string characterName = "Rival";
        [SerializeField] private bool isBoss = false;
        [SerializeField] private float maxHealth = 150f;
        [SerializeField] private float currentHealth;

        [Header("Combate")]
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float structureDamageOnPlayer = 20f;
        [SerializeField] private float attackCooldown = 2.0f;
        [SerializeField] private float attackRange = 2.2f;
        [Tooltip("Segundos de anticipación antes de que el golpe conecte.")]
        [SerializeField] private float windupTime = 0.6f;
        [SerializeField] private float recoverTime = 0.7f;
        [Tooltip("Probabilidad de que el ataque sea un barrido bajo (se esquiva con Guardia + W).")]
        [Range(0f, 1f)] [SerializeField] private float lowAttackChance = 0.3f;
        [Tooltip("Daño a la propia postura cuando Yari hace un desvío perfecto.")]
        [SerializeField] private float parryStructureDamage = 35f;
        [Tooltip("Segundos que queda expuesto tras un parry de Yari.")]
        [SerializeField] private float parryStaggerTime = 1.0f;
        [SerializeField] private float hitStaggerTime = 0.3f;
        [Tooltip("Segundos extra que queda descolocado cuando Yari esquiva su golpe (ventana para contraatacar).")]
        [SerializeField] private float dodgedExtraRecover = 0.4f;
        [Tooltip("Velocidad de reproducción de las animaciones de ataque.")]
        [SerializeField] private float attackAnimSpeed = 1f;

        [Header("Jefe: Juicio Ayni")]
        [Tooltip("Fracción de vida por debajo de la cual empieza la fase final del jefe: romperle la postura abre el Juicio Ayni. " +
                 "Antes de eso, la postura rota solo lo deja expuesto. Los jefes no mueren a golpes: el final siempre es el Juicio.")]
        [Range(0f, 1f)] [SerializeField] private float judgmentHealthThreshold = 0.4f;
        [Tooltip("Multiplicador del daño que recibe mientras tiene la postura rota (fuera del Juicio).")]
        [SerializeField] private float brokenDamageMultiplier = 1.6f;

        [Header("Impacto")]
        [Tooltip("Frenado del retroceso al recibir un golpe (m/s²). Más alto = retroceso más seco.")]
        [SerializeField] private float knockbackDeceleration = 14f;
        [Tooltip("Segundos que permanece en el suelo tras ser derribado, antes de levantarse.")]
        [SerializeField] private float downedTime = 0.6f;

        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float turnSpeed = 8f;
        [Tooltip("Distancia a la que detecta a Yari y empieza a perseguirlo.")]
        [SerializeField] private float aggroRange = 18f;
        [SerializeField] private float gravity = -18f;
        [Tooltip("Ajuste fino de la altura del modelo respecto al suelo (metros). 0 = pies apoyados.")]
        [SerializeField] private float visualHeightOffset = 0f;

        private StructureSystem structure;
        private Animator animator;
        private CharacterController mover;
        private Transform playerTarget;
        private YariCombatController player;

        private EnemyState state = EnemyState.Idle;
        private float stateTimer;
        private float nextAttackTime;
        private float verticalVelocity;
        private bool isDead;
        private AttackHeight pendingHeight;

        // Animaciones de ataque sincronizadas con el instante del golpe
        private struct AttackAnim
        {
            public string state;          // Estado del Animator
            public string clip;           // Clip (para buscar su tiempo de impacto medido)
            public float fallbackContact; // Segundo de impacto si no hay medición

            public AttackAnim(string state, string clip, float fallbackContact)
            {
                this.state = state;
                this.clip = clip;
                this.fallbackContact = fallbackContact;
            }
        }

        private static readonly AttackAnim[] HighAttacks =
        {
            new AttackAnim("Atk_Overhand", "Heavy_Overhand", 0.30f),
            new AttackAnim("Atk_Elbow", "Heavy_Elbow", 0.68f),
            new AttackAnim("Atk_Uppercut", "Heavy_Uppercut", 0.52f),
        };

        private static readonly AttackAnim[] LowAttacks =
        {
            new AttackAnim("Atk_FrontKick", "Heavy_FrontKick", 0.67f),
        };

        private AttackTimingTable attackTimings;
        private int highAttackIndex;
        private bool attackAnimPending;
        private bool attackAnimPlaying;
        private float attackAnimStartTime;
        private string attackAnimState;
        private float attackAnimOffset;
        private Vector3 knockbackVelocity;
        private int downPhase;      // 0 = cayendo, 1 = en el suelo, 2 = levantándose
        private float downTimer;
        private FootIK footIK;
        private AndeanCombatStanceModifier andeanStance;

        // Feedback visual (sirve también para el placeholder sin animaciones)
        private Renderer[] renderers;
        private Color[] baseColors;
        private MaterialPropertyBlock propertyBlock;
        private float flashUntil;
        private Color lastTint = Color.clear;
        private readonly HashSet<int> animatorParams = new HashSet<int>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color HighAttackTint = new Color(1f, 0.15f, 0.1f);
        private static readonly Color LowAttackTint = new Color(1f, 0.85f, 0.1f);
        private static readonly Color StunnedTint = new Color(0.3f, 0.9f, 1f);
        private static readonly Color DeadTint = new Color(0.25f, 0.25f, 0.25f);

        public string CharacterName => characterName;
        public bool IsBoss => isBoss;
        public bool IsDead => isDead;
        public StructureSystem Structure => structure;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public EnemyState State => state;
        public bool IsWindingUp => state == EnemyState.Windup;
        public float HealthRatio => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        /// <summary>Un jefe está en su fase final (los rivales comunes siempre lo están).</summary>
        public bool InFinalPhase => !isBoss || HealthRatio <= judgmentHealthThreshold;
        /// <summary>Con la postura rota y en su fase final: se puede rematar (Venganza) o perdonar (Ayni).</summary>
        public bool CanBeJudged => !isDead && structure != null && structure.IsBroken && InFinalPhase;

        /// <summary>
        /// Un comportamiento especial (p. ej. AmaruHunter) controla al rival un momento: la IA normal se detiene,
        /// los golpes le hacen daño pero no lo interrumpen.
        /// </summary>
        public bool ExternalControl { get; set; }
        public Animator Animator => animator;
        public CharacterController Mover => mover;

        /// <summary>Un jefe en su fase final ha quedado con la postura rota: empieza el Juicio Ayni.</summary>
        public static event System.Action<EnemyController> OnJudgmentReady;
        public AttackHeight PendingAttackHeight => pendingHeight;

        private void Awake()
        {
            structure = GetComponent<StructureSystem>();
            animator = GetComponentInChildren<Animator>();
            currentHealth = maxHealth;

            attackTimings = Resources.Load<AttackTimingTable>(AttackTimingTable.ResourceName);

            SetupMover();
            HookRootMotion();
            footIK = FootIK.Attach(animator, mover);
            AyniFootsteps.Attach(gameObject, 0.4f);
            if (animator != null) andeanStance = animator.GetComponent<AndeanCombatStanceModifier>();
            ApplyVisualGroundOffset();
            CacheAnimatorParameters();
            CacheRenderers();
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
                player = playerObj.GetComponent<YariCombatController>();
            }

            structure.OnStructureBroken += HandleStructureBroken;
            structure.OnStructureRecovered += HandleStructureRecovered;
        }

        private void OnDestroy()
        {
            if (structure != null)
            {
                structure.OnStructureBroken -= HandleStructureBroken;
                structure.OnStructureRecovered -= HandleStructureRecovered;
            }
        }

        /// <summary>Usa un CharacterController para caminar sobre el terreno (sustituye al CapsuleCollider del placeholder).</summary>
        private void SetupMover()
        {
            mover = GetComponent<CharacterController>();
            if (mover != null) return;

            var capsule = GetComponent<CapsuleCollider>();
            mover = gameObject.AddComponent<CharacterController>();
            if (capsule != null)
            {
                mover.height = capsule.height;
                mover.radius = capsule.radius;
                mover.center = capsule.center;
                capsule.enabled = false;
            }
            else
            {
                mover.height = 2f;
                mover.radius = 0.5f;
                mover.center = new Vector3(0f, 1f, 0f);
            }
        }

        /// <summary>
        /// El CharacterController flota sobre el suelo la distancia de su "Skin Width" (8 cm por defecto).
        /// Se baja el modelo esa misma distancia para que los pies queden apoyados.
        /// </summary>
        private void ApplyVisualGroundOffset()
        {
            if (animator == null || mover == null || animator.transform == transform) return;
            Vector3 p = animator.transform.localPosition;
            p.y = -mover.skinWidth + visualHeightOffset;
            animator.transform.localPosition = p;
        }

        /// <summary>
        /// Los golpes traen su propio paso y traslado de peso: el rival se mueve lo que se mueve el clip,
        /// para que los pies no patinen al atacar.
        /// </summary>
        private void HookRootMotion()
        {
            if (animator == null) return;
            var relay = animator.GetComponent<RootMotionRelay>();
            if (relay == null) relay = animator.gameObject.AddComponent<RootMotionRelay>();
            relay.OnRootMotion = HandleRootMotion;
        }

        private void HandleRootMotion(Vector3 delta)
        {
            if (isDead || !attackAnimPlaying || mover == null || !mover.enabled) return;
            if (state != EnemyState.Windup && state != EnemyState.Recover) return;

            delta.y = 0f;
            if (delta.sqrMagnitude > 0f) mover.Move(delta);
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters) animatorParams.Add(p.nameHash);
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i].sharedMaterial;
                if (mat != null && mat.HasProperty(BaseColorId)) baseColors[i] = mat.GetColor(BaseColorId);
                else if (mat != null && mat.HasProperty(ColorId)) baseColors[i] = mat.GetColor(ColorId);
                else baseColors[i] = Color.white;
            }
        }

        private void Update()
        {
            if (isDead) return;

            // Durante el prólogo los rivales esperan quietos
            if (AyniGameState.CinematicPlaying)
            {
                SetAnimSpeed(0f);
                return;
            }

            // Un comportamiento especial lo controla (salto y dardos del Cazador)
            if (ExternalControl)
            {
                UpdateTint();
                return;
            }

            ApplyGravity();
            ApplyKnockback();
            UpdateTint();
            if (footIK != null) footIK.Suspended = state == EnemyState.Downed;
            // Postura andina: en pie y peleando; no mientras está derribado ni aturdido
            if (andeanStance != null) andeanStance.SetStanceWeight(state == EnemyState.Downed || state == EnemyState.Stunned ? 0f : 1f);

            if (state == EnemyState.Stunned) return; // Sale por el evento OnStructureRecovered
            if (state == EnemyState.Downed)
            {
                UpdateDowned();
                return;
            }

            bool playerAvailable = playerTarget != null && (player == null || !player.IsDead);
            float dist = playerTarget != null ? FlatDistanceToPlayer() : float.MaxValue;

            switch (state)
            {
                case EnemyState.Idle:
                    SetAnimSpeed(0f);
                    if (playerAvailable && dist <= aggroRange) state = EnemyState.Chase;
                    break;

                case EnemyState.Chase:
                    if (!playerAvailable || dist > aggroRange * 1.3f)
                    {
                        state = EnemyState.Idle;
                        break;
                    }

                    FacePlayer(turnSpeed);
                    if (dist > attackRange * 0.85f)
                    {
                        Vector3 dir = playerTarget.position - transform.position;
                        dir.y = 0f;
                        mover.Move(dir.normalized * (moveSpeed * Time.deltaTime));
                        SetAnimSpeed(1f);
                    }
                    else
                    {
                        SetAnimSpeed(0f);
                        if (Time.time >= nextAttackTime) BeginWindup();
                    }
                    break;

                case EnemyState.Windup:
                    // Sigue a Yari con la mirada al principio y luego se compromete con el golpe
                    if (stateTimer > windupTime * 0.4f) FacePlayer(turnSpeed * 0.5f);
                    UpdateAttackAnimation();
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f) Strike();
                    break;

                case EnemyState.Recover:
                case EnemyState.Stagger:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        // Si la animación del ataque sigue en curso, salir de ella antes de volver a moverse
                        if (attackAnimPlaying && HasAnimState("Idle")) animator.CrossFadeInFixedTime("Idle", 0.2f);
                        attackAnimPlaying = false;
                        state = EnemyState.Chase;
                    }
                    break;
            }
        }

        private void BeginWindup()
        {
            state = EnemyState.Windup;
            stateTimer = windupTime;
            pendingHeight = Random.value < lowAttackChance ? AttackHeight.Low : AttackHeight.High;
            SetAnimSpeed(0f);
            PrepareAttackAnimation();
        }

        /// <summary>
        /// Elige la animación del ataque (los bajos son patadas, los altos alternan puñetazo, codazo y gancho) y calcula
        /// cuándo y desde qué punto del clip reproducirla para que el miembro llegue a su extensión justo cuando el golpe conecta.
        /// </summary>
        private void PrepareAttackAnimation()
        {
            attackAnimPending = false;
            if (animator == null) return;

            AttackAnim anim;
            if (pendingHeight == AttackHeight.Low)
            {
                anim = LowAttacks[Random.Range(0, LowAttacks.Length)];
            }
            else
            {
                anim = HighAttacks[highAttackIndex % HighAttacks.Length];
                highAttackIndex++;
            }

            if (!HasAnimState(anim.state))
            {
                // Animator sin los estados nuevos: animación de ataque única
                TriggerAnim("Attack");
                return;
            }

            float contact = anim.fallbackContact;
            if (attackTimings != null && attackTimings.TryGetContact(anim.clip, out float measured)) contact = measured;

            float speed = Mathf.Max(0.1f, attackAnimSpeed);
            float timeToContact = contact / speed; // segundos reales que tarda el clip en llegar al impacto

            attackAnimState = anim.state;
            if (timeToContact <= windupTime)
            {
                // El clip es más corto que la anticipación: se espera y se lanza a tiempo
                attackAnimOffset = 0f;
                attackAnimStartTime = Time.time + (windupTime - timeToContact);
            }
            else
            {
                // El clip es más largo: se entra con la preparación ya avanzada
                attackAnimOffset = contact - windupTime * speed;
                attackAnimStartTime = Time.time;
            }
            attackAnimPending = true;
            SetAnimFloat("AttackSpeed", speed);
        }

        private void UpdateAttackAnimation()
        {
            if (!attackAnimPending || Time.time < attackAnimStartTime) return;
            attackAnimPending = false;
            if (animator != null)
            {
                animator.CrossFadeInFixedTime(attackAnimState, 0.08f, 0, attackAnimOffset);
                AyniAudio.Play("swing_fuerte", transform.position + Vector3.up * 1.3f, 0.65f, 0.1f, 0.92f);
                attackAnimPlaying = true;
            }
        }

        /// <summary>Instante en que el golpe conecta: Yari lo esquiva, desvía, bloquea o lo recibe.</summary>
        private void Strike()
        {
            nextAttackTime = Time.time + attackCooldown;
            state = EnemyState.Recover;
            stateTimer = recoverTime;

            if (player == null || playerTarget == null) return;

            // El golpe falla si Yari salió del alcance o se colocó a la espalda
            Vector3 toPlayer = playerTarget.position - transform.position;
            toPlayer.y = 0f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            bool inReach = toPlayer.magnitude <= attackRange + 0.3f &&
                           (toPlayer.sqrMagnitude < 0.0001f || Vector3.Angle(forward, toPlayer) <= 70f);
            if (!inReach) return;

            AttackResult result = player.ReceiveAttack(attackDamage, structureDamageOnPlayer, pendingHeight);
            switch (result)
            {
                case AttackResult.Parried:
                    Debug.Log($"[Parry Exitoso] ¡Yari desvió el golpe de {characterName}!");
                    structure.AddStructureDamage(parryStructureDamage);
                    if (!structure.IsBroken)
                    {
                        // Queda expuesto: ventana de contraataque
                        state = EnemyState.Stagger;
                        stateTimer = parryStaggerTime;
                        PlayHitReaction(HitReaction.Heavy);
                    }
                    break;
                case AttackResult.Dodged:
                    // El golpe al aire lo deja descolocado: es el momento de contraatacar
                    stateTimer = recoverTime + dodgedExtraRecover;
                    Debug.Log($"[Esquiva] Yari esquivó el golpe {(pendingHeight == AttackHeight.High ? "alto" : "bajo")} de {characterName}.");
                    break;
                case AttackResult.Blocked:
                    Debug.Log($"[Bloqueo] Yari bloqueó el golpe de {characterName}.");
                    break;
                case AttackResult.Hit:
                    Debug.Log($"[Impacto] {characterName} conectó un golpe directo a Yari.");
                    break;
            }
        }

        /// <summary>
        /// Recibe un golpe de Yari. <paramref name="reaction"/> decide la animación (cabeza, cuerpo, fuerte o derribo)
        /// y <paramref name="knockback"/> los metros que retrocede en la dirección del golpe.
        /// </summary>
        public void TakeHit(float healthDmg, float structDmg, Vector3 attackerPos, bool isHeavy = false,
                            HitReaction reaction = HitReaction.Head, float knockback = 0.2f)
        {
            if (isDead) return;

            // Con la postura rota (y fuera del Juicio) cada golpe duele más: es el momento de castigarlo
            if (structure.IsBroken && !CanBeJudged) healthDmg *= brokenDamageMultiplier;

            // Los jefes no mueren a golpes: su final se decide en el Juicio Ayni
            currentHealth = Mathf.Max(isBoss ? 1f : 0f, currentHealth - healthDmg);
            structure.AddStructureDamage(structDmg);
            flashUntil = Time.time + 0.1f;

            if (currentHealth <= 0f)
            {
                Defeat(killed: true);
                return;
            }

            // Mientras ejecuta una acción especial no se le interrumpe (pero el golpe cuenta)
            if (ExternalControl) return;

            // Aturdido o ya en el suelo: recibe el daño pero no cambia de reacción
            if (state == EnemyState.Stunned || state == EnemyState.Downed) return;

            // Los jefes aguantan los golpes ligeros mientras preparan su ataque (sin cortar su animación); el resto se interrumpe
            bool armored = isBoss && state == EnemyState.Windup && !isHeavy;
            if (armored) return;

            attackAnimPending = false;
            attackAnimPlaying = false;

            // Retroceso en la dirección del golpe, frenando solo (v = √(2·a·d))
            Vector3 away = transform.position - attackerPos;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f && knockback > 0f)
            {
                knockbackVelocity = away.normalized * Mathf.Sqrt(2f * knockbackDeceleration * knockback);
            }

            if (reaction == HitReaction.Knockdown && HasAnimState("Knockdown") && HasAnimState("GetUp"))
            {
                state = EnemyState.Downed;
                downPhase = 0;
                downTimer = 0f;
                SetAnimSpeed(0f);
                animator.CrossFadeInFixedTime("Knockdown", 0.06f, 0, 0f);
                return;
            }

            PlayHitReaction(reaction);
            state = EnemyState.Stagger;
            stateTimer = reaction == HitReaction.Heavy || reaction == HitReaction.Knockdown ? hitStaggerTime * 1.8f : hitStaggerTime;
        }

        /// <summary>Punto del cuerpo donde se dibuja el impacto, del lado del que viene el golpe.</summary>
        public Vector3 GetHitPoint(HitReaction reaction, Vector3 attackerPos)
        {
            float height = reaction == HitReaction.Body || reaction == HitReaction.Knockdown ? 1.05f : 1.5f;
            Vector3 toward = attackerPos - transform.position;
            toward.y = 0f;
            Vector3 offset = toward.sqrMagnitude > 0.0001f ? toward.normalized * 0.25f : Vector3.zero;
            return transform.position + Vector3.up * height + offset;
        }

        /// <summary>Reacción al golpe según dónde y con qué fuerza le dan.</summary>
        private void PlayHitReaction(HitReaction reaction)
        {
            if (animator == null) return;
            attackAnimPlaying = false;

            string stateName;
            switch (reaction)
            {
                case HitReaction.Body: stateName = "Hit"; break;         // estado "Hit" = golpe al estómago
                case HitReaction.Head: stateName = "Hit_Head"; break;
                default: stateName = "Hit_Heavy"; break;
            }

            if (HasAnimState(stateName)) animator.CrossFadeInFixedTime(stateName, 0.06f, 0, 0f);
            else TriggerAnim("Hit");
        }

        private void ApplyKnockback()
        {
            if (knockbackVelocity.sqrMagnitude < 0.0004f) return;
            if (mover != null && mover.enabled) mover.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDeceleration * Time.deltaTime);
        }

        /// <summary>Derribo: cae, permanece un instante en el suelo y se levanta.</summary>
        private void UpdateDowned()
        {
            downTimer += Time.deltaTime;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            bool inTransition = animator.IsInTransition(0);

            switch (downPhase)
            {
                case 0:
                    if ((info.IsName("Knockdown") && !inTransition && info.normalizedTime >= 0.95f) || downTimer > 4f)
                    {
                        downPhase = 1;
                        downTimer = 0f;
                    }
                    break;

                case 1:
                    if (downTimer >= downedTime)
                    {
                        downPhase = 2;
                        downTimer = 0f;
                        animator.CrossFade("GetUp", 0.05f, 0, 0.25f);
                    }
                    break;

                default:
                    if ((info.IsName("GetUp") && !inTransition && info.normalizedTime >= 0.88f) || downTimer > 4f)
                    {
                        if (HasAnimState("Idle")) animator.CrossFadeInFixedTime("Idle", 0.2f);
                        state = EnemyState.Chase;
                        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.5f);
                    }
                    break;
            }
        }

        private void HandleStructureBroken()
        {
            if (!isDead) AyniAudio.Play("postura_rota", transform.position + Vector3.up * 1.4f, 1f, 0.03f);
            if (isDead) return;
            attackAnimPending = false;
            attackAnimPlaying = false;
            state = EnemyState.Stunned;
            SetAnimSpeed(0f);
            SetAnimBool("IsStunned", true);

            if (isBoss && InFinalPhase)
            {
                // El Juicio espera a que Yari decida: la postura no se recupera sola
                structure.HoldBroken = true;
                Debug.Log($"[JUICIO AYNI] ¡{characterName} está a merced de Yari! Venganza o Ayni.");
                OnJudgmentReady?.Invoke(this);
            }
            else
            {
                Debug.Log($"[VULNERABLE] ¡La postura de {characterName} está ROTA!" +
                          (CanBeJudged ? " Venganza (F / B) o Ayni (X / A)." : " Castígalo ahora."));
            }
        }

        private void HandleStructureRecovered()
        {
            SetAnimBool("IsStunned", false);
            if (isDead) return;
            state = EnemyState.Recover;
            stateTimer = 0.5f;
        }

        /// <summary>
        /// Derrota al rival. Con <paramref name="reactionDelay"/> la caída (o el arrodillarse) espera a que
        /// conecte el remate o termine el gesto de Yari, para que los dos movimientos vayan a la vez.
        /// </summary>
        public void Defeat(bool killed, float reactionDelay = 0f)
        {
            if (isDead) return;

            isDead = true;
            state = EnemyState.Dead;
            if (structure != null) structure.HoldBroken = false;
            knockbackVelocity = Vector3.zero;
            attackAnimPending = false;
            if (footIK != null) footIK.Suspended = true;
            if (andeanStance != null) andeanStance.SetStanceWeight(0f);
            SetAnimSpeed(0f);

            if (reactionDelay > 0f && animator != null)
            {
                StartCoroutine(DelayedDefeatReaction(killed, reactionDelay));
            }
            else
            {
                SetAnimBool("IsStunned", false);
                TriggerAnim(killed ? "Die" : "MercyKneel");
            }
            Debug.Log($"[Resultado] {characterName} ha sido {(killed ? "ejecutado (Venganza)" : "purificado y desarmado (Ayni)")}.");

            float halfHeight = mover != null ? mover.height * 0.5f * transform.lossyScale.y : 1f;
            float radius = mover != null ? mover.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.5f;
            if (mover != null) mover.enabled = false;

            ApplyTint(killed ? DeadTint : StunnedTint, killed ? 0.75f : 0.35f);

            // Sin modelo animado (cápsula de prueba): se derrumba al morir o se arrodilla al ser perdonado
            if (animator == null)
            {
                StartCoroutine(PlaceholderDefeatRoutine(killed, halfHeight, radius));
            }
        }

        private IEnumerator DelayedDefeatReaction(bool killed, float delay)
        {
            yield return new WaitForSeconds(delay);

            SetAnimBool("IsStunned", false);
            TriggerAnim(killed ? "Die" : "MercyKneel");
            if (killed)
            {
                Vector3 from = playerTarget != null ? playerTarget.position : transform.position + transform.forward;
                CombatFeedback.Finisher(GetHitPoint(HitReaction.Head, from));
            }
        }

        private IEnumerator PlaceholderDefeatRoutine(bool killed, float halfHeight, float radius)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 startScale = transform.localScale;

            Vector3 endPos = startPos;
            Quaternion endRot = startRot;
            Vector3 endScale = startScale;

            if (killed)
            {
                endRot = startRot * Quaternion.Euler(-90f, 0f, 0f);
                endPos.y -= Mathf.Max(0f, halfHeight - radius);
            }
            else
            {
                endScale.y = startScale.y * 0.6f;
                endPos.y -= halfHeight * 0.4f;
            }

            const float duration = 0.5f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                transform.position = Vector3.Lerp(startPos, endPos, k);
                transform.rotation = Quaternion.Slerp(startRot, endRot, k);
                transform.localScale = Vector3.Lerp(startScale, endScale, k);
                yield return null;
            }

            transform.position = endPos;
            transform.rotation = endRot;
            transform.localScale = endScale;
        }

        // ───────────────────────── Utilidades ─────────────────────────

        private float FlatDistanceToPlayer()
        {
            Vector3 d = playerTarget.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void FacePlayer(float speed)
        {
            if (playerTarget == null) return;
            Vector3 dir = playerTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, speed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (mover == null || !mover.enabled) return;
            if (mover.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            mover.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
        }

        /// <summary>Color de aviso: rojo = ataque alto, amarillo = barrido bajo, cian = postura rota, blanco = golpe recibido.</summary>
        private void UpdateTint()
        {
            if (Time.time < flashUntil) ApplyTint(Color.white, 0.8f);
            else if (Time.time < telegraphUntil) ApplyTint(telegraphColor, 0.75f);
            else if (state == EnemyState.Windup) ApplyTint(pendingHeight == AttackHeight.High ? HighAttackTint : LowAttackTint, 0.75f);
            else if (state == EnemyState.Stunned) ApplyTint(StunnedTint, 0.6f);
            else ApplyTint(Color.clear, 0f);
        }

        private Color telegraphColor;
        private float telegraphUntil;

        /// <summary>Tiñe al rival unos segundos para avisar de un ataque especial (verde = dardo envenenado).</summary>
        public void Telegraph(Color color, float seconds)
        {
            telegraphColor = color;
            telegraphUntil = Time.time + seconds;
        }

        public bool HasAnimatorState(string stateName) => HasAnimState(stateName);

        /// <summary>Reproduce un estado de ataque desde el punto indicado del clip, a la velocidad dada.</summary>
        public void PlayAttackState(string stateName, float speed, float startOffset)
        {
            if (animator == null || !HasAnimState(stateName)) return;
            SetAnimFloat("AttackSpeed", speed);
            animator.CrossFadeInFixedTime(stateName, 0.06f, 0, startOffset);
        }

        private void ApplyTint(Color tint, float amount)
        {
            Color key = new Color(tint.r, tint.g, tint.b, amount);
            if (key == lastTint || renderers == null) return;
            lastTint = key;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = Color.Lerp(baseColors[i], tint, amount);
                c.a = baseColors[i].a;
                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, c);
                propertyBlock.SetColor(ColorId, c);
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }

        private bool HasAnimParam(string paramName)
        {
            return animator != null && animatorParams.Contains(Animator.StringToHash(paramName));
        }

        private void TriggerAnim(string paramName)
        {
            if (HasAnimParam(paramName)) animator.SetTrigger(paramName);
        }

        private void SetAnimBool(string paramName, bool value)
        {
            if (HasAnimParam(paramName)) animator.SetBool(paramName, value);
        }

        private void SetAnimFloat(string paramName, float value)
        {
            if (HasAnimParam(paramName)) animator.SetFloat(paramName, value);
        }

        private bool HasAnimState(string stateName)
        {
            return animator != null && animator.runtimeAnimatorController != null &&
                   animator.HasState(0, Animator.StringToHash(stateName));
        }

        private void SetAnimSpeed(float value)
        {
            if (HasAnimParam("Speed")) animator.SetFloat("Speed", value);
        }
    }
}
