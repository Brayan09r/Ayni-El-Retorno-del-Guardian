using System.Collections;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Player;

namespace Ayni.Enemy
{
    /// <summary>
    /// Amaru el Cazador (Biblia, 4.3): en su segunda fase salta hacia atrás para tomar distancia y lanza una ráfaga
    /// de dardos envenenados con la cerbatana. Cada dardo se anuncia tiñendo a Amaru de verde: se esquiva agachándose
    /// o con el balanceo lateral, se desvía con un parry y se bloquea con la guardia. Si alcanza a Yari, lo envenena.
    /// SifuCombatHUD lo añade solo al jefe que se llame Amaru.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class AmaruHunter : MonoBehaviour
    {
        public static readonly Color PoisonGreen = new Color(0.35f, 1f, 0.25f);

        [Header("Cuándo")]
        [Tooltip("Fracción de vida por debajo de la cual Amaru empieza a usar la cerbatana.")]
        [Range(0f, 1f)] [SerializeField] private float activateBelowHealth = 0.75f;
        [Tooltip("Segundos entre ráfagas (mínimo y máximo).")]
        [SerializeField] private Vector2 cooldown = new Vector2(8f, 12f);

        [Header("Salto hacia atrás")]
        [SerializeField] private float leapDistance = 5.5f;
        [SerializeField] private float leapHeight = 1.1f;
        [SerializeField] private float leapTime = 0.55f;

        [Header("Dardos")]
        [SerializeField] private int dartsPerVolley = 3;
        [Tooltip("Segundos de aviso (Amaru se tiñe de verde) antes de cada dardo.")]
        [SerializeField] private float telegraphTime = 0.5f;
        [SerializeField] private float dartInterval = 0.35f;
        [SerializeField] private float dartSpeed = 20f;
        [SerializeField] private float dartDamage = 8f;
        [SerializeField] private float dartStructureDamage = 12f;
        [SerializeField] private float poisonSeconds = 3f;
        [SerializeField] private float poisonDamagePerSecond = 2.5f;

        /// <summary>True mientras Amaru apunta un dardo (el HUD muestra el aviso).</summary>
        public static bool DartWarning { get; private set; }

        private EnemyController enemy;
        private YariCombatController yari;
        private float nextVolley;
        private bool busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DartWarning = false;
        }

        private void Awake()
        {
            enemy = GetComponent<EnemyController>();
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            nextVolley = Time.time + 3f;
        }

        private void OnDisable()
        {
            Release();
        }

        private void Update()
        {
            if (busy || yari == null || enemy.IsDead || yari.IsDead || AyniGameState.CinematicPlaying) return;
            if (enemy.HealthRatio > activateBelowHealth || Time.time < nextVolley) return;
            if (enemy.State != EnemyState.Chase || enemy.Structure.IsBroken) return;

            float dist = FlatDistance(transform.position, yari.transform.position);
            if (dist < 1.2f || dist > 9f) return;

            StartCoroutine(Volley());
        }

        private bool ShouldAbort()
        {
            return enemy.IsDead || enemy.Structure.IsBroken || AyniGameState.CinematicPlaying || yari == null || yari.IsDead;
        }

        private IEnumerator Volley()
        {
            busy = true;
            enemy.ExternalControl = true;

            // 1. Salto hacia atrás, buscando suelo firme (nunca hacia la garganta)
            if (FindLeapTarget(out Vector3 landing))
            {
                if (enemy.HasAnimatorState("Hunter_Leap")) enemy.Animator.CrossFadeInFixedTime("Hunter_Leap", 0.05f);
                AyniAudio.Play("salto", transform.position + Vector3.up, 0.7f, 0.05f, 0.9f);
                Vector3 start = transform.position;
                float t = 0f;
                Vector3 previous = start;
                while (t < leapTime)
                {
                    if (ShouldAbort()) { Release(); yield break; }
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / leapTime);
                    Vector3 target = Vector3.Lerp(start, landing, 1f - (1f - k) * (1f - k)) + Vector3.up * (leapHeight * 4f * k * (1f - k));
                    Move(target - previous);
                    previous = transform.position;
                    FaceYari(30f);
                    yield return null;
                }
                // Asentarse en el suelo
                Move(Vector3.down * 0.6f);
                AyniAudio.Play("aterrizaje", transform.position, 0.8f);
            }

            // 2. Ráfaga de dardos, cada uno anunciado con el destello verde
            for (int i = 0; i < dartsPerVolley; i++)
            {
                enemy.Telegraph(PoisonGreen, telegraphTime);
                DartWarning = true;
                float t = 0f;
                while (t < telegraphTime)
                {
                    if (ShouldAbort()) { Release(); yield break; }
                    FaceYari(12f);
                    t += Time.deltaTime;
                    yield return null;
                }

                enemy.PlayAttackState("Atk_Overhand", 1.8f, 0.25f);
                yield return new WaitForSeconds(0.12f);
                if (ShouldAbort()) { Release(); yield break; }
                DartWarning = false;
                LaunchDart();

                yield return new WaitForSeconds(dartInterval);
            }

            // 3. Vuelve a la carga
            yield return new WaitForSeconds(0.3f);
            if (enemy.HasAnimatorState("Idle")) enemy.Animator.CrossFadeInFixedTime("Idle", 0.2f);
            Release();
        }

        private void Release()
        {
            DartWarning = false;
            if (enemy != null) enemy.ExternalControl = false;
            if (busy) nextVolley = Time.time + Random.Range(cooldown.x, cooldown.y);
            busy = false;
        }

        private void LaunchDart()
        {
            Vector3 origin = transform.position + Vector3.up * 1.45f + transform.forward * 0.5f;
            if (enemy.Animator != null && enemy.Animator.isHuman)
            {
                Transform hand = enemy.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null) origin = hand.position;
            }
            AmaruDart.Launch(origin, yari, dartSpeed, dartDamage, dartStructureDamage, poisonSeconds, poisonDamagePerSecond);
            AyniAudio.Play("dardo_lanzado", origin, 0.9f, 0.08f);
        }

        /// <summary>Punto de aterrizaje alejándose de Yari; prueba también en diagonal si detrás no hay suelo firme.</summary>
        private bool FindLeapTarget(out Vector3 landing)
        {
            Vector3 away = transform.position - yari.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;

            float[] angles = { 0f, 40f, -40f, 80f, -80f };
            foreach (float a in angles)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * away;
                Vector3 candidate = transform.position + dir * leapDistance;
                if (!Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 7f,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (Mathf.Abs(hit.point.y - transform.position.y) > 1.5f) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > 35f) continue;
                landing = hit.point;
                return true;
            }
            landing = transform.position;
            return false;
        }

        private void Move(Vector3 delta)
        {
            if (enemy.Mover != null && enemy.Mover.enabled) enemy.Mover.Move(delta);
            else transform.position += delta;
        }

        private void FaceYari(float speed)
        {
            Vector3 to = yari.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), speed * Time.deltaTime);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }

    /// <summary>Dardo envenenado de la cerbatana de Amaru: vuela en línea recta hacia donde estaba el pecho de Yari.</summary>
    public class AmaruDart : MonoBehaviour
    {
        private YariCombatController target;
        private Vector3 velocity;
        private float damage, structureDamage, poisonSeconds, poisonDps;
        private bool resolved;
        private float travelled;

        public static void Launch(Vector3 origin, YariCombatController target, float speed, float damage, float structureDamage,
                                  float poisonSeconds, float poisonDps)
        {
            if (target == null) return;
            var go = new GameObject("Dardo_Envenenado");
            go.transform.position = origin;
            var dart = go.AddComponent<AmaruDart>();
            dart.target = target;
            Vector3 aim = target.transform.position + Vector3.up * 1.3f;
            dart.velocity = (aim - origin).normalized * speed;
            dart.damage = damage;
            dart.structureDamage = structureDamage;
            dart.poisonSeconds = poisonSeconds;
            dart.poisonDps = poisonDps;
            dart.BuildVisual();
        }

        private void BuildVisual()
        {
            transform.rotation = Quaternion.LookRotation(velocity);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(transform, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.025f, 0.22f, 0.025f);
            shaft.GetComponent<Renderer>().sharedMaterial = ShaftMaterial();

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = CombatFeedback.SpriteMaterial;
            trail.time = 0.18f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.06f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(AmaruHunter.PoisonGreen, 0f), new GradientColorKey(new Color(0.1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material shaftMaterial;

        private static Material ShaftMaterial()
        {
            if (shaftMaterial == null)
            {
                shaftMaterial = new Material(CombatFeedback.SpriteMaterial) { color = new Color(0.2f, 0.45f, 0.12f) };
            }
            return shaftMaterial;
        }

        private void Update()
        {
            Vector3 step = velocity * Time.deltaTime;
            transform.position += step;
            travelled += step.magnitude;

            if (!resolved && target != null)
            {
                Vector3 chest = target.transform.position + Vector3.up * 1.3f;
                if (Vector3.Distance(transform.position, chest) < 0.75f || Vector3.Dot(chest - transform.position, velocity) < 0f)
                {
                    Resolve(chest);
                }
            }

            if (travelled > 30f) Destroy(gameObject);
        }

        private void Resolve(Vector3 chest)
        {
            resolved = true;
            AttackResult result = target.ReceiveAttack(damage, structureDamage, AttackHeight.High);
            switch (result)
            {
                case AttackResult.Hit:
                    AyniAudio.Play("dardo_impacto", chest, 0.9f);
                    target.ApplyPoison(poisonSeconds, poisonDps);
                    CombatFeedback.Flash(chest, AmaruHunter.PoisonGreen, 0.9f, 0.18f);
                    Destroy(gameObject);
                    break;
                case AttackResult.Parried:
                case AttackResult.Blocked:
                    // Desviado o detenido por la guardia
                    Destroy(gameObject);
                    break;
                default:
                    // Esquivado (o Yari era invulnerable): el dardo pasa de largo
                    break;
            }
        }
    }
}
