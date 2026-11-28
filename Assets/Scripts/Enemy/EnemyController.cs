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

        [Header("Jefe: fases (como en Sifu)")]
        [Tooltip("Vida de cada barra del jefe. Al vaciar una (que no sea la última) cae, se levanta con un grito y empieza la siguiente fase.")]
        [SerializeField] private float[] bossPhaseHealth = { 460f, 560f };
        [Tooltip("Aguante de la postura del jefe (los rivales comunes usan el de su StructureSystem).")]
        [SerializeField] private float bossStructure = 195f;
        [Tooltip("En cada fase nueva: ritmo de ataque (menos espera entre golpes), daño y velocidad.")]
        [SerializeField] private float phaseCooldownMultiplier = 0.68f;
        [SerializeField] private float phaseDamageMultiplier = 1.22f;
        [SerializeField] private float phaseWindupMultiplier = 0.86f;
        [SerializeField] private float phaseSpeedMultiplier = 1.2f;

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

        private struct ComboStep
        {
            public AttackAnim anim;
            public AttackHeight height;
            public float windupScale;
            public float damageScale;
            public float structureScale;
            public float animSpeedScale;

            public ComboStep(AttackAnim anim, AttackHeight height, float windupScale = 1f,
                             float damageScale = 1f, float structureScale = 1f, float animSpeedScale = 1f)
            {
                this.anim = anim;
                this.height = height;
                this.windupScale = windupScale;
                this.damageScale = damageScale;
                this.structureScale = structureScale;
                this.animSpeedScale = animSpeedScale;
            }
        }

        private static readonly AttackAnim AtkLight1    = new AttackAnim("Atk_Light1",    "Light_Punch_1_L",    0.26f);
        private static readonly AttackAnim AtkLight2    = new AttackAnim("Atk_Light2",    "Light_Punch_2_R",    0.28f);
        private static readonly AttackAnim AtkLight3    = new AttackAnim("Atk_Light3",    "Light_Punch_3_L",    0.34f);
        private static readonly AttackAnim AtkLight4    = new AttackAnim("Atk_Light4",    "Light_Punch_4_R",    0.36f);
        private static readonly AttackAnim AtkOverhand  = new AttackAnim("Atk_Overhand",  "Heavy_Overhand",     0.30f);
        private static readonly AttackAnim AtkElbow     = new AttackAnim("Atk_Elbow",     "Heavy_Elbow",        0.68f);
        private static readonly AttackAnim AtkUppercut  = new AttackAnim("Atk_Uppercut",  "Heavy_Uppercut",     0.52f);
        private static readonly AttackAnim AtkHeadbutt  = new AttackAnim("Atk_Headbutt",  "Heavy_Headbutt",     0.44f);
        private static readonly AttackAnim AtkFrontKick = new AttackAnim("Atk_FrontKick", "Heavy_FrontKick",    0.67f);
        private static readonly AttackAnim AtkHeavyKick = new AttackAnim("Atk_HeavyKick", "RumiMaki_HeavyKick", 0.52f);
        private static readonly AttackAnim AtkShove     = new AttackAnim("Atk_Shove",     "Shove_Push",         0.38f);

        private static readonly AttackAnim[] HighAttacks =
        {
            AtkOverhand, AtkElbow, AtkUppercut, AtkHeadbutt, AtkLight3, AtkLight4, AtkShove
        };

        private static readonly AttackAnim[] LowAttacks =
        {
            AtkFrontKick, AtkHeavyKick
        };

        // Combos diferenciados por arquetipo para que cada tipo de rival pelee con su propio estilo
        private static readonly ComboStep[][] RastreadorCombos =
        {
            new[] { new ComboStep(AtkLight1, AttackHeight.High, 0.85f, 0.65f, 0.7f, 1.15f), new ComboStep(AtkLight2, AttackHeight.High, 0.72f, 0.8f, 0.85f, 1.15f) },
            new[] { new ComboStep(AtkLight1, AttackHeight.High, 0.85f, 0.65f, 0.7f, 1.15f), new ComboStep(AtkFrontKick, AttackHeight.Low, 0.88f, 0.9f, 1f, 1.05f) },
            new[] { new ComboStep(AtkLight3, AttackHeight.High, 0.9f, 0.7f, 0.75f, 1.1f), new ComboStep(AtkLight4, AttackHeight.High, 0.74f, 0.75f, 0.8f, 1.1f), new ComboStep(AtkElbow, AttackHeight.High, 0.86f, 0.95f, 1f, 1.05f) },
            new[] { new ComboStep(AtkFrontKick, AttackHeight.Low, 0.95f, 0.85f, 0.9f, 1.05f), new ComboStep(AtkLight2, AttackHeight.High, 0.75f, 0.8f, 0.85f, 1.15f) },
            new[] { new ComboStep(AtkLight2, AttackHeight.High, 0.88f, 0.75f, 0.8f, 1.1f), new ComboStep(AtkUppercut, AttackHeight.High, 0.92f, 1.0f, 1.05f, 1f) },
        };

        private static readonly ComboStep[][] SaqueadorCombos =
        {
            new[] { new ComboStep(AtkShove, AttackHeight.High, 0.92f, 0.7f, 1.15f, 1.05f), new ComboStep(AtkOverhand, AttackHeight.High, 0.88f, 1.05f, 1.1f, 1f) },
            new[] { new ComboStep(AtkLight3, AttackHeight.High, 0.9f, 0.75f, 0.8f, 1.05f), new ComboStep(AtkHeadbutt, AttackHeight.High, 0.82f, 0.95f, 1.15f, 1.05f) },
            new[] { new ComboStep(AtkLight2, AttackHeight.High, 0.88f, 0.7f, 0.75f, 1.1f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.95f, 1.0f, 1.1f, 1f) },
            new[] { new ComboStep(AtkLight1, AttackHeight.High, 0.86f, 0.65f, 0.7f, 1.1f), new ComboStep(AtkLight3, AttackHeight.High, 0.76f, 0.75f, 0.8f, 1.05f), new ComboStep(AtkOverhand, AttackHeight.High, 0.95f, 1.05f, 1.15f, 1f) },
            new[] { new ComboStep(AtkFrontKick, AttackHeight.Low, 0.95f, 0.85f, 0.95f, 1f), new ComboStep(AtkElbow, AttackHeight.High, 0.84f, 0.95f, 1.05f, 1.05f) },
        };

        private static readonly ComboStep[][] GuardiaCombos =
        {
            new[] { new ComboStep(AtkElbow, AttackHeight.High, 0.95f, 0.85f, 1.0f, 1f), new ComboStep(AtkUppercut, AttackHeight.High, 0.86f, 1.05f, 1.15f, 1f) },
            new[] { new ComboStep(AtkShove, AttackHeight.High, 0.9f, 0.65f, 1.25f, 1.05f), new ComboStep(AtkHeadbutt, AttackHeight.High, 0.84f, 0.85f, 1.1f, 1f), new ComboStep(AtkOverhand, AttackHeight.High, 0.95f, 1.15f, 1.25f, 0.95f) },
            new[] { new ComboStep(AtkLight4, AttackHeight.High, 0.92f, 0.75f, 0.85f, 1.05f), new ComboStep(AtkElbow, AttackHeight.High, 0.82f, 0.85f, 1.0f, 1f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.96f, 1.1f, 1.2f, 1f) },
            new[] { new ComboStep(AtkFrontKick, AttackHeight.Low, 0.96f, 0.85f, 1.0f, 1f), new ComboStep(AtkOverhand, AttackHeight.High, 0.9f, 1.1f, 1.2f, 1f) },
            new[] { new ComboStep(AtkHeadbutt, AttackHeight.High, 0.92f, 0.9f, 1.15f, 1f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.9f, 1.05f, 1.15f, 1f) },
        };

        private static readonly ComboStep[][] CazadorEliteCombos =
        {
            new[] { new ComboStep(AtkLight1, AttackHeight.High, 0.84f, 0.65f, 0.75f, 1.15f), new ComboStep(AtkFrontKick, AttackHeight.Low, 0.82f, 0.85f, 0.95f, 1.1f), new ComboStep(AtkUppercut, AttackHeight.High, 0.86f, 1.05f, 1.1f, 1.05f) },
            new[] { new ComboStep(AtkElbow, AttackHeight.High, 0.88f, 0.8f, 0.95f, 1.1f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.86f, 0.9f, 1.05f, 1.05f), new ComboStep(AtkOverhand, AttackHeight.High, 0.92f, 1.1f, 1.2f, 1.05f) },
            new[] { new ComboStep(AtkHeadbutt, AttackHeight.High, 0.86f, 0.8f, 1.05f, 1.1f), new ComboStep(AtkFrontKick, AttackHeight.Low, 0.82f, 0.85f, 1.0f, 1.05f), new ComboStep(AtkElbow, AttackHeight.High, 0.85f, 1.0f, 1.1f, 1.05f) },
            new[] { new ComboStep(AtkLight2, AttackHeight.High, 0.82f, 0.65f, 0.75f, 1.15f), new ComboStep(AtkLight3, AttackHeight.High, 0.74f, 0.75f, 0.85f, 1.15f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.88f, 1.05f, 1.15f, 1.05f) },
            new[] { new ComboStep(AtkShove, AttackHeight.High, 0.85f, 0.65f, 1.15f, 1.1f), new ComboStep(AtkUppercut, AttackHeight.High, 0.82f, 0.95f, 1.05f, 1.05f) },
        };

        private static readonly ComboStep[][] BossPhase1Combos =
        {
            new[] { new ComboStep(AtkLight1, AttackHeight.High, 0.86f, 0.7f, 0.8f, 1.15f), new ComboStep(AtkLight2, AttackHeight.High, 0.72f, 0.75f, 0.85f, 1.15f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.9f, 1.05f, 1.15f, 1.05f) },
            new[] { new ComboStep(AtkElbow, AttackHeight.High, 0.9f, 0.85f, 1.0f, 1.08f), new ComboStep(AtkHeadbutt, AttackHeight.High, 0.8f, 0.9f, 1.1f, 1.08f), new ComboStep(AtkOverhand, AttackHeight.High, 0.94f, 1.15f, 1.25f, 1.02f) },
            new[] { new ComboStep(AtkFrontKick, AttackHeight.Low, 0.92f, 0.85f, 0.95f, 1.08f), new ComboStep(AtkUppercut, AttackHeight.High, 0.82f, 1.0f, 1.1f, 1.08f) },
            new[] { new ComboStep(AtkShove, AttackHeight.High, 0.86f, 0.7f, 1.2f, 1.1f), new ComboStep(AtkLight4, AttackHeight.High, 0.74f, 0.8f, 0.9f, 1.12f), new ComboStep(AtkFrontKick, AttackHeight.Low, 0.88f, 1.0f, 1.15f, 1.05f) },
            new[] { new ComboStep(AtkLight3, AttackHeight.High, 0.88f, 0.75f, 0.85f, 1.12f), new ComboStep(AtkElbow, AttackHeight.High, 0.8f, 0.9f, 1.0f, 1.08f), new ComboStep(AtkUppercut, AttackHeight.High, 0.9f, 1.1f, 1.2f, 1.05f) },
        };

        private static readonly ComboStep[][] BossPhase2Combos =
        {
            new[] { new ComboStep(AtkLight1, AttackHeight.High, 0.8f, 0.65f, 0.8f, 1.2f), new ComboStep(AtkLight2, AttackHeight.High, 0.68f, 0.7f, 0.85f, 1.2f), new ComboStep(AtkFrontKick, AttackHeight.Low, 0.8f, 0.9f, 1.05f, 1.12f), new ComboStep(AtkOverhand, AttackHeight.High, 0.9f, 1.15f, 1.3f, 1.08f) },
            new[] { new ComboStep(AtkShove, AttackHeight.High, 0.82f, 0.7f, 1.25f, 1.15f), new ComboStep(AtkElbow, AttackHeight.High, 0.74f, 0.85f, 1.0f, 1.15f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.84f, 1.0f, 1.2f, 1.1f), new ComboStep(AtkUppercut, AttackHeight.High, 0.86f, 1.1f, 1.25f, 1.1f) },
            new[] { new ComboStep(AtkFrontKick, AttackHeight.Low, 0.86f, 0.8f, 1.0f, 1.15f), new ComboStep(AtkHeadbutt, AttackHeight.High, 0.76f, 0.85f, 1.15f, 1.12f), new ComboStep(AtkHeavyKick, AttackHeight.Low, 0.86f, 1.05f, 1.2f, 1.1f) },
            new[] { new ComboStep(AtkLight3, AttackHeight.High, 0.82f, 0.7f, 0.85f, 1.18f), new ComboStep(AtkLight4, AttackHeight.High, 0.7f, 0.75f, 0.9f, 1.18f), new ComboStep(AtkHeadbutt, AttackHeight.High, 0.78f, 0.9f, 1.1f, 1.12f), new ComboStep(AtkOverhand, AttackHeight.High, 0.88f, 1.15f, 1.3f, 1.08f) },
            new[] { new ComboStep(AtkElbow, AttackHeight.High, 0.84f, 0.85f, 1.0f, 1.15f), new ComboStep(AtkFrontKick, AttackHeight.Low, 0.8f, 0.9f, 1.05f, 1.12f), new ComboStep(AtkUppercut, AttackHeight.High, 0.84f, 1.1f, 1.2f, 1.1f) },
        };

        private AttackTimingTable attackTimings;
        private int highAttackIndex;
        private ComboStep[] activeCombo;
        private int comboStepIndex;
        private int lastComboIndex = -1;
        private float currentStepWindup;
        private float currentStepDamageScale = 1f;
        private float currentStepStructureScale = 1f;
        private float currentStepAnimSpeedScale = 1f;
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
        private static readonly Color EnragedTint = new Color(1f, 0.3f, 0.1f);

        public string CharacterName => characterName;
        public bool IsBoss => isBoss;
        public bool IsDead => isDead;
        public StructureSystem Structure => structure;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public EnemyState State => state;
        public bool IsWindingUp => state == EnemyState.Windup;
        public float HealthRatio => maxHealth > 0f ? currentHealth / maxHealth : 0f;

        /// <summary>Fase del jefe (1, 2...). Los rivales comunes tienen una sola.</summary>
        public int Phase { get; private set; } = 1;
        public int PhaseCount => isBoss && bossPhaseHealth != null && bossPhaseHealth.Length > 0 ? bossPhaseHealth.Length : 1;
        /// <summary>El jefe está cayendo y levantándose entre una fase y la siguiente (es invulnerable).</summary>
        public bool InPhaseTransition { get; private set; }

        /// <summary>Un jefe está en su fase final (los rivales comunes siempre lo están).</summary>
        public bool InFinalPhase => !isBoss || (Phase >= PhaseCount && HealthRatio <= judgmentHealthThreshold);

        /// <summary>Un jefe ha vaciado una barra y empieza la transición a la siguiente fase.</summary>
        public static event System.Action<EnemyController> OnPhaseTransitionStarted;
        /// <summary>El jefe se ha levantado y empieza la fase indicada.</summary>
        public static event System.Action<EnemyController, int> OnPhaseStarted;
        /// <summary>Con la postura rota y en su fase final: se puede rematar (Venganza) o perdonar (Ayni).</summary>
        public bool CanBeJudged => !isDead && structure != null && structure.IsBroken && InFinalPhase;

        /// <summary>
        /// Un comportamiento especial (p. ej. AmaruHunter) controla al rival un momento: la IA normal se detiene,
        /// los golpes le hacen daño pero no lo interrumpen.
        /// </summary>
        public bool ExternalControl { get; set; }

        /// <summary>
        /// No puede morir (la fauna: los pumas). Su vida no baja de 1 y, al "derrotarlo" o rematarlo, en vez de caer
        /// se lanza <see cref="OnDefeatRefused"/> para que su comportamiento decida (el puma huye herido).
        /// </summary>
        public bool Unkillable { get; set; }
        /// <summary>Está en su rutina antes de la pelea (AyniRivalAmbient): aún no ha visto a Yari.</summary>
        public bool Dormant { get; set; }
        /// <summary>Multiplica el daño a la vida que recibe de Yari (p. ej. la champi del Capitán del Tambo contra Amaru).</summary>
        public float IncomingDamageMultiplier { get; set; } = 1f;
        /// <summary>
        /// Rival común que se juzga como un jefe: su Juicio se resuelve desde la misma distancia que el de un jefe
        /// (el Capitán del Tambo y su mini Juicio).
        /// </summary>
        public bool JudgedLikeBoss { get; set; }

        /// <summary>
        /// Veces que puede levantarse al caer derrotado (segundo aliento). Cae, se queda en el suelo y se levanta furioso
        /// con más vida, más aguante y golpes más fuertes. Lo reparte AyniEncounterSite.
        /// </summary>
        public int SecondWinds { get; set; }
        /// <summary>
        /// Distancia desde la que va a buscar a Yari aunque esté más lejos que su alcance normal (los refuerzos de una
        /// kancha y los cazadores de Amaru llegan desde lejos sabiendo dónde está).
        /// </summary>
        public float HuntRange { get; set; }
        /// <summary>Se levantó con su segundo aliento: más vida, aguante, daño y velocidad (el HUD lo marca).</summary>
        public bool Enraged { get; private set; }
        /// <summary>Un rival acaba de levantarse furioso.</summary>
        public static event System.Action<EnemyController> OnSecondWind;
        private bool risingEnraged;

        /// <summary>
        /// Como en Sifu, solo unos pocos rivales atacan a la vez; el resto rodea a Yari en guardia a unos metros y espera
        /// su turno. Los jefes no esperan turno.
        /// </summary>
        public const int MaxSimultaneousAttackers = 2;

        /// <summary>
        /// Antes de esto ningún rival (salvo el jefe) puede empezar un golpe: cuando uno ataca, el siguiente espera un
        /// momento. Así los rivales se turnan en vez de golpear todos en el mismo instante.
        /// </summary>
        private static float nextGroupAttackAt;
        private const float GroupAttackGapMin = 0.7f, GroupAttackGapMax = 1.15f;
        /// <summary>
        /// Mientras un rival encadena su combo, los demás no empiezan el suyo: el turno se reserva en cada golpe del combo
        /// hasta este margen después de su impacto (antes solo se reservaba al empezar y un segundo rival se colaba a mitad).
        /// </summary>
        private const float ComboTurnTail = 0.4f;
        /// <summary>
        /// Tiempo mínimo para reaccionar al siguiente golpe del combo cuando Yari sigue aturdida por el anterior. Sin esto,
        /// con preparaciones de 0,32 s y 0,35 s de aturdimiento, recibir el primer golpe significaba recibir el combo entero.
        /// </summary>
        private const float ComboReactionWindow = 0.42f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAttackTurns() => nextGroupAttackAt = 0f;
        private const float WaitRing = 3.6f;
        /// <summary>Recibió un golpe de Yari (con el daño ya aplicado).</summary>
        public event System.Action<EnemyController> OnDamaged;
        /// <summary>Un rival que no puede morir ha llegado al final de su vida o lo han intentado rematar.</summary>
        public event System.Action<EnemyController> OnDefeatRefused;

        /// <summary>Vuelve a tener toda la vida y la postura (un puma que se ha recuperado en su guarida).</summary>
        public void RestoreHealth()
        {
            if (isDead) return;
            currentHealth = maxHealth;
            if (structure != null) structure.ResetStructure();
        }
        public Animator Animator => animator;
        public CharacterController Mover => mover;

        /// <summary>Un jefe en su fase final ha quedado con la postura rota: empieza el Juicio Ayni.</summary>
        public static event System.Action<EnemyController> OnJudgmentReady;
        public AttackHeight PendingAttackHeight => pendingHeight;

        private void Awake()
        {
            structure = GetComponent<StructureSystem>();
            animator = GetComponentInChildren<Animator>();
            if (isBoss && bossPhaseHealth != null && bossPhaseHealth.Length > 0) maxHealth = bossPhaseHealth[0];
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

            if (isBoss) structure.SetMaxStructure(bossStructure);
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
                    if (playerAvailable && dist <= Mathf.Max(aggroRange, HuntRange)) state = EnemyState.Chase;
                    break;

                case EnemyState.Chase:
                    if (!playerAvailable || dist > Mathf.Max(aggroRange, HuntRange) * 1.3f)
                    {
                        state = EnemyState.Idle;
                        break;
                    }

                    FacePlayer(turnSpeed);
                    // Sin turno de ataque se queda en el anillo de espera, en guardia, sin amontonarse sobre Yari
                    bool myTurn = isBoss || AttackersNow() < MaxSimultaneousAttackers;
                    // Aunque tenga sitio, espera a que pase un momento desde el último golpe de otro rival
                    bool canSwing = isBoss || Time.time >= nextGroupAttackAt;
                    float stopAt = myTurn ? attackRange * 0.85f : WaitRing;
                    if (dist > stopAt)
                    {
                        Vector3 dir = playerTarget.position - transform.position;
                        dir.y = 0f;
                        mover.Move((dir.normalized + Separation()) * (moveSpeed * Time.deltaTime));
                        SetAnimSpeed(1f);
                    }
                    else
                    {
                        SetAnimSpeed(0f);
                        Vector3 apart = Separation();
                        if (apart.sqrMagnitude > 0.01f) mover.Move(apart * (1.2f * Time.deltaTime));
                        if (myTurn && canSwing && Time.time >= nextAttackTime) BeginWindup();
                    }
                    break;

                case EnemyState.Windup:
                    // Sigue a Yari con la mirada al principio y luego se compromete con el golpe
                    if (stateTimer > currentStepWindup * 0.35f) FacePlayer(turnSpeed * (comboStepIndex > 0 ? 1.35f : 0.6f));
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

        /// <summary>Rivales (no jefes) que están preparando o soltando un golpe ahora mismo.</summary>
        private static int AttackersNow()
        {
            int count = 0;
            for (int i = 0; i < All.Count; i++)
            {
                EnemyController e = All[i];
                if (e == null || e.isDead || e.isBoss) continue;
                if (e.state == EnemyState.Windup || e.state == EnemyState.Recover) count++;
            }
            return count;
        }

        /// <summary>Empuje suave para separarse de los rivales que tiene pegados (que no se encimen unos en otros).</summary>
        private Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < All.Count; i++)
            {
                EnemyController e = All[i];
                if (e == null || e == this || e.isDead) continue;
                Vector3 away = transform.position - e.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d < 1.6f && d > 0.001f) push += away / d * (1.6f - d);
            }
            return Vector3.ClampMagnitude(push, 1f);
        }

        /// <summary>Se levanta furioso: más vida, más aguante y golpes más fuertes y seguidos.</summary>
        private void RiseEnraged()
        {
            risingEnraged = false;
            Enraged = true;
            maxHealth *= 1.25f;
            currentHealth = maxHealth * 0.75f;
            if (structure != null)
            {
                structure.SetMaxStructure(structure.MaxStructure * 1.3f);
                structure.ResetStructure();
            }
            attackDamage *= 1.2f;
            attackCooldown *= 0.8f;
            moveSpeed *= 1.1f;
            AyniAudio.Play("postura_rota", transform.position, 0.9f, 0.05f, 0.7f);
            CombatFeedback.Flash(transform.position + Vector3.up * 1.2f, new Color(1f, 0.35f, 0.15f, 0.9f), 2.2f, 0.35f);
            CombatFeedback.Shake(0.05f, 0.2f);
            OnSecondWind?.Invoke(this);
        }

        private ComboStep[][] GetComboPool()
        {
            if (isBoss) return Phase >= 2 ? BossPhase2Combos : BossPhase1Combos;
            string low = (characterName ?? "").ToLowerInvariant();
            if (low.Contains("rastreador") || low.Contains("explorador")) return RastreadorCombos;
            if (low.Contains("saqueador")) return SaqueadorCombos;
            if (low.Contains("guardia") || low.Contains("capit")) return GuardiaCombos;
            if (low.Contains("cazador") || low.Contains("elite") || low.Contains("élite")) return CazadorEliteCombos;
            return SaqueadorCombos;
        }

        private void BeginWindup()
        {
            if (!isBoss) nextGroupAttackAt = Time.time + Random.Range(GroupAttackGapMin, GroupAttackGapMax);

            // Solo los rivales humanoides encadenan combos variados; la fauna (pumas) mantiene su golpe único
            bool canCombo = !Unkillable && animator != null && animator.isHuman;
            if (canCombo)
            {
                ComboStep[][] pool = GetComboPool();
                int idx = Random.Range(0, pool.Length);
                if (pool.Length > 1 && idx == lastComboIndex) idx = (idx + 1 + Random.Range(0, pool.Length - 1)) % pool.Length;
                lastComboIndex = idx;
                activeCombo = pool[idx];
                comboStepIndex = 0;
                BeginComboStep(0);
                return;
            }

            activeCombo = null;
            comboStepIndex = 0;
            currentStepWindup = windupTime;
            currentStepDamageScale = 1f;
            currentStepStructureScale = 1f;
            currentStepAnimSpeedScale = 1f;
            state = EnemyState.Windup;
            stateTimer = currentStepWindup;
            pendingHeight = Random.value < lowAttackChance ? AttackHeight.Low : AttackHeight.High;
            SetAnimSpeed(0f);
            PrepareAttackAnimation(null);
        }

        private void BeginComboStep(int index)
        {
            ComboStep step = activeCombo[index];
            comboStepIndex = index;
            float enragedBoost = Enraged ? 0.88f : 1f;
            currentStepWindup = Mathf.Max(0.32f, windupTime * step.windupScale * enragedBoost);
            // Si el golpe anterior la dejó aturdida, el siguiente espera a que pueda esquivarlo o desviarlo
            if (index > 0 && player != null && player.StunRemaining > 0f)
            {
                currentStepWindup = Mathf.Max(currentStepWindup, player.StunRemaining + ComboReactionWindow);
            }
            if (!isBoss) nextGroupAttackAt = Mathf.Max(nextGroupAttackAt, Time.time + currentStepWindup + ComboTurnTail);
            currentStepDamageScale = step.damageScale;
            currentStepStructureScale = step.structureScale;
            currentStepAnimSpeedScale = step.animSpeedScale * (Enraged ? 1.1f : 1f);
            pendingHeight = step.height;
            state = EnemyState.Windup;
            stateTimer = currentStepWindup;
            SetAnimSpeed(0f);
            PrepareAttackAnimation(step.anim);
        }

        /// <summary>
        /// Elige la animación del ataque (respetando el paso del combo si existe) y calcula cuándo y desde qué punto
        /// del clip reproducirla para que el miembro llegue a su extensión justo cuando el golpe conecta.
        /// </summary>
        private void PrepareAttackAnimation(AttackAnim? forcedAnim)
        {
            attackAnimPending = false;
            if (animator == null) return;

            AttackAnim anim;
            if (forcedAnim.HasValue && HasAnimState(forcedAnim.Value.state))
            {
                anim = forcedAnim.Value;
            }
            else if (pendingHeight == AttackHeight.Low)
            {
                anim = LowAttacks[Random.Range(0, LowAttacks.Length)];
                if (!HasAnimState(anim.state)) anim = AtkFrontKick;
            }
            else
            {
                anim = HighAttacks[highAttackIndex % HighAttacks.Length];
                highAttackIndex++;
                if (!HasAnimState(anim.state)) anim = AtkOverhand;
            }

            if (!HasAnimState(anim.state))
            {
                // Animator sin los estados nuevos: animación de ataque única
                TriggerAnim("Attack");
                return;
            }

            float contact = anim.fallbackContact;
            if (attackTimings != null && attackTimings.TryGetContact(anim.clip, out float measured)) contact = measured;

            float speed = Mathf.Max(0.1f, attackAnimSpeed * currentStepAnimSpeedScale);
            float timeToContact = contact / speed; // segundos reales que tarda el clip en llegar al impacto

            attackAnimState = anim.state;
            if (timeToContact <= currentStepWindup)
            {
                // El clip es más corto que la anticipación: se espera y se lanza a tiempo
                attackAnimOffset = 0f;
                attackAnimStartTime = Time.time + (currentStepWindup - timeToContact);
            }
            else
            {
                // El clip es más largo: se entra con la preparación ya avanzada
                attackAnimOffset = contact - currentStepWindup * speed;
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
            bool hasNextStep = activeCombo != null && comboStepIndex + 1 < activeCombo.Length;
            bool parried = false;
            bool dodged = false;

            if (player != null && playerTarget != null)
            {
                // El golpe falla si Yari salió del alcance o se colocó a la espalda
                Vector3 toPlayer = playerTarget.position - transform.position;
                toPlayer.y = 0f;
                Vector3 forward = transform.forward;
                forward.y = 0f;
                bool inReach = toPlayer.magnitude <= attackRange + 0.35f &&
                               (toPlayer.sqrMagnitude < 0.0001f || Vector3.Angle(forward, toPlayer) <= 75f);
                if (inReach)
                {
                    float dmg = attackDamage * currentStepDamageScale;
                    float sdmg = structureDamageOnPlayer * currentStepStructureScale;
                    AttackResult result = player.ReceiveAttack(dmg, sdmg, pendingHeight);
                    switch (result)
                    {
                        case AttackResult.Parried:
                            parried = true;
                            Debug.Log($"[Parry Exitoso] ¡Yari desvió el golpe de {characterName}!");
                            structure.AddStructureDamage(parryStructureDamage);
                            if (!structure.IsBroken)
                            {
                                // Queda expuesto: el parry corta el combo al instante
                                activeCombo = null;
                                state = EnemyState.Stagger;
                                stateTimer = parryStaggerTime;
                                PlayHitReaction(HitReaction.Heavy);
                            }
                            break;
                        case AttackResult.Dodged:
                            dodged = true;
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
            }

            if (parried || state == EnemyState.Stunned || state == EnemyState.Stagger || isDead)
            {
                activeCombo = null;
                nextAttackTime = Time.time + attackCooldown * Random.Range(0.85f, 1.25f);
                return;
            }

            // Encadenar el siguiente golpe del combo si Yari sigue cerca y vivo
            bool playerAlive = playerTarget != null && (player == null || !player.IsDead);
            if (hasNextStep && playerAlive && FlatDistanceToPlayer() <= attackRange + 1.35f)
            {
                // En combos de rivales comunes no siempre completan el último golpe si Yari ya esquivó; Amaru y furiosos sí insisten
                if (!dodged || isBoss || Enraged || Random.value < 0.72f)
                {
                    BeginComboStep(comboStepIndex + 1);
                    return;
                }
            }

            // Fin del combo: entra en recuperación (más larga si Yari esquivó el último golpe)
            activeCombo = null;
            nextAttackTime = Time.time + attackCooldown * Random.Range(0.85f, 1.2f);
            state = EnemyState.Recover;
            stateTimer = dodged ? recoverTime + dodgedExtraRecover : recoverTime;
        }

        /// <summary>
        /// Recibe un golpe de Yari. <paramref name="reaction"/> decide la animación (cabeza, cuerpo, fuerte o derribo)
        /// y <paramref name="knockback"/> los metros que retrocede en la dirección del golpe.
        /// </summary>
        public void TakeHit(float healthDmg, float structDmg, Vector3 attackerPos, bool isHeavy = false,
                            HitReaction reaction = HitReaction.Head, float knockback = 0.2f)
        {
            if (isDead || InPhaseTransition) return;
            if (risingEnraged) return; // en el suelo reuniendo fuerzas: no se le puede rematar

            // Con la postura rota (y fuera del Juicio) cada golpe duele más: es el momento de castigarlo
            if (structure.IsBroken && !CanBeJudged) healthDmg *= brokenDamageMultiplier;
            healthDmg *= IncomingDamageMultiplier;

            // Los jefes no mueren a golpes: al vaciar una barra pasan a la siguiente fase y en la última
            // su final se decide en el Juicio Ayni
            bool morePhases = isBoss && Phase < PhaseCount;
            currentHealth = Mathf.Max((isBoss && !morePhases) || Unkillable ? 1f : 0f, currentHealth - healthDmg);
            flashUntil = Time.time + 0.1f;
            OnDamaged?.Invoke(this);

            if (Unkillable && currentHealth <= 1f)
            {
                OnDefeatRefused?.Invoke(this);
                return;
            }

            if (morePhases && currentHealth <= 0f)
            {
                StartCoroutine(PhaseTransitionRoutine());
                return;
            }
            structure.AddStructureDamage(structDmg);

            if (currentHealth <= 0f && SecondWinds > 0 && !isBoss && !ExternalControl && HasAnimState("Knockdown") && HasAnimState("GetUp"))
            {
                // Segundo aliento: cae como derrotado, pero se levantará furioso
                SecondWinds--;
                risingEnraged = true;
                currentHealth = 1f;
                if (structure != null) structure.HoldBroken = false;
                attackAnimPending = false;
                attackAnimPlaying = false;
                state = EnemyState.Downed;
                downPhase = 0;
                downTimer = 0f;
                SetAnimSpeed(0f);
                SetAnimBool("IsStunned", false);
                animator.CrossFadeInFixedTime("Knockdown", 0.06f, 0, 0f);
                return;
            }

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
            activeCombo = null;

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
                    if (downTimer >= (risingEnraged ? downedTime + 1.4f : downedTime))
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
                        if (risingEnraged) RiseEnraged();
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
            activeCombo = null;
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
            if (Unkillable)
            {
                OnDefeatRefused?.Invoke(this);
                return;
            }

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
                CombatFeedback.Finisher(GetHitPoint(HitReaction.Head, from), transform.position - from);
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
            else if (risingEnraged || Enraged) ApplyTint(EnragedTint, risingEnraged ? 0.25f + 0.2f * Mathf.Sin(Time.time * 9f) : 0.22f);
            else ApplyTint(Color.clear, 0f);
        }

        private Color telegraphColor;
        private float telegraphUntil;

        /// <summary>
        /// Transición entre fases (como en Sifu): el jefe cae, se queda en el suelo, se levanta mientras su barra
        /// se rellena y lanza un grito de guerra. Es invulnerable mientras dura. AyniBossPhaseDirector pone la cámara,
        /// el título y el sonido.
        /// </summary>
        private IEnumerator PhaseTransitionRoutine()
        {
            InPhaseTransition = true;
            ExternalControl = true;
            attackAnimPending = false;
            attackAnimPlaying = false;
            activeCombo = null;
            knockbackVelocity = Vector3.zero;
            state = EnemyState.Stagger;
            structure.HoldBroken = false;
            structure.ResetStructure();
            SetAnimBool("IsStunned", false);
            SetAnimSpeed(0f);
            if (footIK != null) footIK.Suspended = true;
            if (andeanStance != null) andeanStance.SetStanceWeight(0f);
            OnPhaseTransitionStarted?.Invoke(this);

            // 1. Cae al suelo
            if (HasAnimState("Knockdown")) animator.CrossFadeInFixedTime("Knockdown", 0.08f, 0, 0f);
            else TriggerAnim("Hit");
            yield return new WaitForSeconds(2.0f);

            // 2. Se levanta mientras la barra de la fase nueva se llena
            Phase++;
            maxHealth = bossPhaseHealth[Mathf.Min(Phase - 1, bossPhaseHealth.Length - 1)];
            if (HasAnimState("GetUp")) animator.CrossFade("GetUp", 0.1f, 0, 0.25f);
            float t = 0f;
            while (t < 1.7f)
            {
                t += Time.deltaTime;
                currentHealth = Mathf.Lerp(0f, maxHealth, Mathf.SmoothStep(0f, 1f, t / 1.7f));
                yield return null;
            }
            currentHealth = maxHealth;

            // Se vuelve hacia Yari antes de rugirle
            for (float turn = 0f; turn < 0.35f; turn += Time.deltaTime)
            {
                if (playerTarget != null) FacePlayer(turnSpeed * 1.5f);
                yield return null;
            }

            // 3. Grito de guerra: la fase nueva es más agresiva
            if (HasAnimState("Phase_Roar")) animator.CrossFadeInFixedTime("Phase_Roar", 0.12f, 0, 0f);
            Telegraph(new Color(1f, 0.35f, 0.15f), 1.4f);
            attackCooldown *= phaseCooldownMultiplier;
            attackDamage *= phaseDamageMultiplier;
            windupTime *= phaseWindupMultiplier;
            moveSpeed *= phaseSpeedMultiplier;
            yield return new WaitForSeconds(1.5f);

            if (HasAnimState("Idle")) animator.CrossFadeInFixedTime("Idle", 0.2f);
            if (footIK != null) footIK.Suspended = false;
            state = EnemyState.Chase;
            nextAttackTime = Time.time + 0.6f;
            ExternalControl = false;
            InPhaseTransition = false;
            OnPhaseStarted?.Invoke(this, Phase);
        }

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
