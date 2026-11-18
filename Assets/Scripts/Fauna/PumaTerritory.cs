using System.Collections.Generic;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;
using UnityEngine;

namespace Ayni.Fauna
{
    /// <summary>
    /// Puma andino, guardián del Kay Pacha. Vigila su territorio en los bordes del camino y nunca muere:
    ///  - Si Yari se acerca demasiado lo ataca; si se aleja, vuelve a su sitio.
    ///  - Al "derrotarlo" (o intentar rematarlo) no cae: huye herido a su guarida y se recupera.
    ///  - Si Yari golpea a un puma, la manada responde como las gallinas de Zelda: los pumas cercanos y otros que
    ///    bajan del cerro lo persiguen un rato (ODS 15: la Pachamama defiende a los suyos). Ver <see cref="AyniPumaPack"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(EnemyController), typeof(CharacterController))]
    public sealed class PumaTerritory : MonoBehaviour
    {
        private enum Mode { Home, Returning, Hunting, Fleeing }

        [SerializeField] private float disengageDistance = 12f;
        [SerializeField] private float territoryRadius = 18f;
        [SerializeField] private float returnSpeed = 3f;
        [Tooltip("Velocidad a la que corre hacia Yari cuando la manada está furiosa.")]
        [SerializeField] private float huntSpeed = 6.5f;
        [Tooltip("Segundos que tarda en recuperarse en su guarida tras huir herido.")]
        [SerializeField] private float recoverSeconds = 8f;

        private EnemyController enemy;
        private CharacterController mover;
        private Transform player;
        private YariCombatController yari;
        private Vector3 origin;
        private Quaternion originalRotation;
        private Mode mode;
        private float gravity, huntUntil, recoverAt;

        /// <summary>Puma de refuerzo que bajó del cerro: al acabar la persecución se va y desaparece.</summary>
        public bool Temporary { get; set; }
        public bool IsHunting => mode == Mode.Hunting;
        public bool IsFleeing => mode == Mode.Fleeing;
        public Vector3 Origin => origin;

        private void OnEnable() => AyniPumaPack.Register(this);
        private void OnDisable()
        {
            AyniPumaPack.Unregister(this);
            if (enemy != null) enemy.ExternalControl = false;
        }

        private void Awake()
        {
            enemy = GetComponent<EnemyController>();
            mover = GetComponent<CharacterController>();
            origin = transform.position;
            originalRotation = transform.rotation;
            enemy.Unkillable = true;
            enemy.OnDamaged += HandleDamaged;
            enemy.OnDefeatRefused += HandleDefeatRefused;
        }

        private void Start()
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target == null) return;
            player = target.transform;
            yari = target.GetComponent<YariCombatController>();
        }

        private void OnDestroy()
        {
            if (enemy == null) return;
            enemy.OnDamaged -= HandleDamaged;
            enemy.OnDefeatRefused -= HandleDefeatRefused;
        }

        private void HandleDamaged(EnemyController _)
        {
            if (mode != Mode.Fleeing) AyniPumaPack.Provoke(this);
        }

        private void HandleDefeatRefused(EnemyController _) => Flee();

        /// <summary>Corre hacia Yari durante <paramref name="seconds"/> s y, al alcanzarlo, pelea.</summary>
        public void Hunt(float seconds)
        {
            if (mode == Mode.Fleeing || enemy == null) return;
            huntUntil = Mathf.Max(huntUntil, Time.time + seconds);
            if (mode == Mode.Hunting) return;
            mode = Mode.Hunting;
            enemy.ExternalControl = true;
            PlayRun();
        }

        /// <summary>Herido de muerte: no cae, se va corriendo a su guarida (o se pierde en el cerro si es de refuerzo).</summary>
        public void Flee()
        {
            if (mode == Mode.Fleeing || enemy == null) return;
            mode = Mode.Fleeing;
            enemy.ExternalControl = true;
            recoverAt = -1f;
            PlayRun();
        }

        private void Update()
        {
            if (enemy == null || enemy.IsDead) return;
            if (AyniGameState.CinematicPlaying) return;

            switch (mode)
            {
                case Mode.Home: UpdateHome(); break;
                case Mode.Returning: UpdateReturning(); break;
                case Mode.Hunting: UpdateHunting(); break;
                case Mode.Fleeing: UpdateFleeing(); break;
            }
        }

        private void UpdateHome()
        {
            Vector3 home = Flat(origin - transform.position);
            Vector3 toPlayer = player == null ? Vector3.one * 10000f : Flat(player.position - transform.position);
            if (home.sqrMagnitude > .25f &&
                (toPlayer.sqrMagnitude > disengageDistance * disengageDistance || home.sqrMagnitude > territoryRadius * territoryRadius))
            {
                mode = Mode.Returning;
                enemy.ExternalControl = true;
                PlayRun();
            }
        }

        private void UpdateReturning()
        {
            // Respeta aturdimientos y derribos mientras regresa
            if (enemy.State == EnemyState.Stunned || enemy.State == EnemyState.Downed || enemy.State == EnemyState.Stagger)
            {
                mode = Mode.Home;
                enemy.ExternalControl = false;
                return;
            }
            if (RunTo(origin, returnSpeed, 0.35f)) Settle();
        }

        private void UpdateHunting()
        {
            if (player == null) Start();
            bool yariDown = player == null || (yari != null && yari.IsDead);
            if (Time.time > huntUntil || yariDown)
            {
                // Se acabó la furia: los de refuerzo se van al cerro; los demás vuelven a su territorio
                if (Temporary)
                {
                    Flee();
                    return;
                }
                mode = Mode.Returning;
                enemy.ExternalControl = true;
                PlayRun();
                return;
            }
            Vector3 toPlayer = Flat(player.position - transform.position);
            if (toPlayer.magnitude > 3.5f)
            {
                if (!enemy.ExternalControl) PlayRun();
                enemy.ExternalControl = true;
                RunTo(player.position, huntSpeed, 0f);
            }
            else if (enemy.ExternalControl)
            {
                // Lo alcanzó: la IA de pelea toma el control (ataques, esquivas, postura)
                enemy.ExternalControl = false;
            }
        }

        private void UpdateFleeing()
        {
            Vector3 lair = Temporary ? AwayFromYari() : origin;
            if (recoverAt < 0f)
            {
                if (!RunTo(lair, huntSpeed, 0.5f)) return;
                if (Temporary)
                {
                    Destroy(gameObject);
                    return;
                }
                recoverAt = Time.time + recoverSeconds;
                if (enemy.Animator != null) { enemy.Animator.SetFloat("Speed", 0f); enemy.Animator.CrossFadeInFixedTime("Idle", .2f); }
                return;
            }
            if (Time.time < recoverAt) return;
            enemy.RestoreHealth();
            Settle();
        }

        /// <summary>Corre hacia <paramref name="point"/>; devuelve true al llegar.</summary>
        private bool RunTo(Vector3 point, float speed, float arriveDistance)
        {
            Vector3 to = Flat(point - transform.position);
            if (to.magnitude <= Mathf.Max(arriveDistance, 0.05f)) return true;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 8f);
            gravity = mover.isGrounded ? -2f : gravity - 18f * Time.deltaTime;
            float step = Mathf.Min(speed * Time.deltaTime, to.magnitude);
            if (mover.enabled) mover.Move(to.normalized * step + Vector3.up * (gravity * Time.deltaTime));
            if (enemy.Animator != null) enemy.Animator.SetFloat("Speed", 1f);
            return false;
        }

        private Vector3 AwayFromYari()
        {
            if (player == null) return origin;
            Vector3 away = Flat(transform.position - player.position);
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            return transform.position + away.normalized * 30f;
        }

        private void Settle()
        {
            mode = Mode.Home;
            enemy.ExternalControl = false;
            transform.rotation = originalRotation;
            if (enemy.Animator != null) { enemy.Animator.SetFloat("Speed", 0f); enemy.Animator.CrossFadeInFixedTime("Idle", .15f); }
        }

        private void PlayRun()
        {
            if (enemy.Animator != null) enemy.Animator.CrossFadeInFixedTime("Run", .15f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }

    /// <summary>
    /// La manada de pumas del nivel. Golpear a uno enfurece a los que están cerca y hace bajar del cerro a otros
    /// (hasta <see cref="MaxReinforcements"/>), que persiguen a Yari un rato y luego se van. También sirve de límite del
    /// mapa: si Yari se empeña en salirse del camino, el puma más cercano sale a cortarle el paso.
    /// </summary>
    public static class AyniPumaPack
    {
        public const int MaxReinforcements = 3;
        private const float RageSeconds = 16f;
        private const float RageRadius = 70f;

        private static readonly List<PumaTerritory> All = new List<PumaTerritory>();
        private static float lastProvokeAt = -100f, lastWarnAt = -100f;
        private static bool toldStory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            lastProvokeAt = lastWarnAt = -100f;
            toldStory = false;
        }

        public static void Register(PumaTerritory puma)
        {
            if (!All.Contains(puma)) All.Add(puma);
        }

        public static void Unregister(PumaTerritory puma) => All.Remove(puma);

        /// <summary>Yari golpeó a <paramref name="victim"/>: la manada responde.</summary>
        public static void Provoke(PumaTerritory victim)
        {
            Transform yari = Yari();
            if (yari == null) return;
            bool fresh = Time.time - lastProvokeAt > RageSeconds;
            lastProvokeAt = Time.time;

            int hunting = 0;
            foreach (PumaTerritory puma in All.ToArray())
            {
                if (puma == null || puma.IsFleeing) continue;
                if ((puma.transform.position - yari.position).sqrMagnitude > RageRadius * RageRadius && puma != victim) continue;
                puma.Hunt(RageSeconds);
                hunting++;
            }

            if (!fresh) return;
            // Bajan otros del cerro, como las gallinas de Zelda
            int reinforcements = Mathf.Clamp(4 - hunting, 1, MaxReinforcements);
            for (int i = 0; i < reinforcements; i++) SpawnReinforcement(victim, yari, i, reinforcements);

            AyniAudio.Play("titulo_golpe", victim.transform.position, 0.8f, 0.05f, 0.6f);
            CombatFeedback.Shake(0.06f, 0.25f);
            if (!toldStory)
            {
                toldStory = true;
                AyniScreenFX.Caption("La Pachamama defiende a sus hijos: no dañes al puma, guardián del Kay Pacha (ODS 15).", 4.5f);
            }
        }

        /// <summary>Yari empuja el borde del mapa: el puma más cercano sale a cortarle el paso.</summary>
        public static void WarnAtBorder(Vector3 yariPosition)
        {
            if (Time.time - lastWarnAt < 10f) return;
            lastWarnAt = Time.time;
            PumaTerritory nearest = null;
            float best = 120f * 120f;
            foreach (PumaTerritory puma in All)
            {
                if (puma == null || puma.IsFleeing || puma.IsHunting) continue;
                float d = (puma.transform.position - yariPosition).sqrMagnitude;
                if (d < best) { best = d; nearest = puma; }
            }
            if (nearest != null) nearest.Hunt(7f);
        }

        private static void SpawnReinforcement(PumaTerritory template, Transform yari, int index, int count)
        {
            // Desde una ladera a 22-28 m, repartidos alrededor de Yari
            float angle = Random.Range(0f, 360f) + index * 360f / count;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 spot = yari.position + dir * Random.Range(22f, 28f);
            if (!Physics.Raycast(spot + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 140f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (Mathf.Abs(hit.point.y - yari.position.y) > 12f) return; // ni en el fondo de una quebrada ni en un risco

            GameObject clone = Object.Instantiate(template.gameObject, hit.point + Vector3.up * 0.1f, Quaternion.LookRotation(-dir));
            clone.name = template.name + " (manada)";
            var puma = clone.GetComponent<PumaTerritory>();
            puma.Temporary = true;
            var enemy = clone.GetComponent<EnemyController>();
            if (enemy != null) enemy.RestoreHealth();
            puma.Hunt(RageSeconds);
        }

        private static Transform Yari()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform : null;
        }
    }
}
