using UnityEngine;
using Ayni.Enemy;

namespace Ayni.Player
{
    /// <summary>
    /// Movimiento por el escenario estilo Sifu: no hay salto libre. Con el botón de salto (A / Espacio) Yari salta el
    /// obstáculo bajo que tiene delante apoyando una mano (troncos, muros a medio caer) o trepa a la cornisa (hasta
    /// 2.4 m). Si no hay nada que saltar ni trepar, el botón no hace nada (salvo el salto largo sobre un hueco, que
    /// decide YariCombatController con <see cref="CanLeapGap"/>).
    ///
    /// Detecta con rayos: la cara del obstáculo (a la altura de las rodillas), su parte de arriba y si hay sitio para
    /// caer al otro lado o para estar de pie encima. Durante el movimiento el CharacterController se apaga y el
    /// personaje sigue un recorrido calculado; el cuerpo lo anima el clip de la Fragua (Yari_Vault / Yari_Climb)
    /// reproducido a la velocidad justa para que sus fases coincidan con las del recorrido.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class YariTraversal : MonoBehaviour
    {
        public enum Kind { None, Vault, Mount, Climb }

        [Header("Alcance")]
        [Tooltip("Distancia máxima (m) a la cara del obstáculo para saltarlo o treparlo andando.")]
        [SerializeField] private float reach = 1.3f;
        [Tooltip("Distancia extra por cada m/s de velocidad: corriendo se engancha antes, sin frenar.")]
        [SerializeField] private float reachPerSpeed = 0.18f;

        [Header("Alturas (m sobre los pies)")]
        [Tooltip("Por debajo de esto es un escalón: lo sube el CharacterController solo.")]
        [SerializeField] private float minObstacle = 0.3f;
        [Tooltip("Hasta esta altura se salta apoyando la mano; por encima, se trepa.")]
        [SerializeField] private float maxVault = 1.3f;
        [SerializeField] private float maxClimb = 2.4f;

        [Header("Duraciones")]
        [Tooltip("Saltar un obstáculo, en segundos: más rápido cuanto más rápido llega Yari.")]
        [SerializeField] private float vaultMinTime = 0.45f;
        [SerializeField] private float vaultMaxTime = 0.72f;
        [Tooltip("Trepar 1.3 m; cada metro más de altura suma Climb Time Per Meter.")]
        [SerializeField] private float climbTime = 0.9f;
        [SerializeField] private float climbTimePerMeter = 0.2f;

        [Header("Salto largo sobre un hueco")]
        [Tooltip("Distancia del borde al otro lado que se puede cruzar de un salto en carrera.")]
        [SerializeField] private float maxGap = 3.6f;

        /// <summary>Saltando o trepando: YariCombatController no lee la entrada ni aplica la física mientras dura.</summary>
        public bool Active => kind != Kind.None;
        /// <summary>Velocidad (m/s) con la que sale Yari al terminar, en la dirección del recorrido (0 al trepar).</summary>
        public float ExitSpeed { get; private set; }
        public Vector3 Direction => dir;
        /// <summary>Se dispara al terminar el movimiento.</summary>
        public event System.Action OnFinished;
        /// <summary>Por qué no se pudo saltar ni trepar la última vez que se miró (para las pruebas).</summary>
        public string LastReason { get; private set; } = "";

        // Alturas (m sobre los pies) a las que se busca la cara del obstáculo
        private static readonly float[] FaceHeights = { 0.25f, 0.45f, 0.7f };

        private CharacterController mover;
        private Animator animator;
        private Kind kind;
        private Vector3 start, face, landing, dir;
        private float topY, faceDist;
        private float duration, elapsed;

        private struct Plan
        {
            public Kind kind;
            public Vector3 dir;
            public float faceDist;
            public float topY;
            public Vector3 landing;
        }

        private void Awake()
        {
            mover = GetComponent<CharacterController>();
        }

        // ───────────────────────── Detección ─────────────────────────

        /// <summary>
        /// Qué se puede hacer hacia <paramref name="direction"/> (horizontal) yendo a <paramref name="speed"/> m/s:
        /// Vault (saltar apoyando la mano), Mount (subirse a algo bajo y ancho), Climb (trepar) o None.
        /// </summary>
        public Kind Probe(Vector3 direction, float speed)
        {
            return TryPlan(direction, speed, out Plan plan) ? plan.kind : Kind.None;
        }

        private bool TryPlan(Vector3 direction, float speed, out Plan plan)
        {
            plan = default;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return Fail("sin dirección");
            direction.Normalize();

            Vector3 feet = transform.position;
            float radius = mover != null ? mover.radius : 0.35f;
            float maxDist = radius + reach + reachPerSpeed * Mathf.Max(0f, speed);

            // 1. Cara del obstáculo: tiene que empezar cerca del suelo. Se mira a tres alturas y se queda la cara más
            //    vertical (en un tronco redondo, a media altura el rayo da en la curva de arriba)
            bool found = false;
            RaycastHit low = default;
            foreach (float h in FaceHeights)
            {
                if (!Cast(feet + Vector3.up * h, direction, maxDist, out RaycastHit hit)) continue;
                if (!found || hit.normal.y < low.normal.y) low = hit;
                found = true;
            }
            if (!found) return Fail($"nada delante a menos de {maxDist:F1} m");
            // Es obstáculo todo lo que el CharacterController no puede subir andando (más inclinado que su Slope Limit)
            float walkable = Mathf.Cos((mover != null ? mover.slopeLimit : 45f) * Mathf.Deg2Rad);
            if (low.normal.y >= walkable) return Fail($"rampa ({low.collider.name}, normal.y {low.normal.y:F2})");
            if (Vector3.Angle(-new Vector3(low.normal.x, 0f, low.normal.z), direction) > 55f) return Fail($"{low.collider.name} muy de lado");
            float dist = Vector3.Dot(low.point - feet, direction);

            // 2. Parte de arriba, un poco dentro del obstáculo
            Vector3 over = feet + direction * (dist + 0.2f);
            if (!Physics.SphereCast(over + Vector3.up * (maxClimb + 0.6f), 0.12f, Vector3.down, out RaycastHit top,
                                    maxClimb + 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return Fail("no se encuentra la parte de arriba");
            if (IsCharacter(top.collider)) return Fail("encima hay un personaje");
            float height = top.point.y - feet.y;
            if (height < minObstacle || height > maxClimb) return Fail($"{top.collider.name}: altura {height:F2} m fuera de rango");
            // Pared más alta que el punto de agarre, justo encima del borde: no cabe
            if (Physics.Raycast(top.point + Vector3.up * 0.05f, Vector3.up, 1.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return Fail("algo encima del borde");

            plan.dir = direction;
            plan.faceDist = Mathf.Max(0f, dist);
            plan.topY = top.point.y;

            if (height <= maxVault)
            {
                // 3a. Saltar: buscar dónde vuelve a bajar el suelo al otro lado
                for (float k = dist + 0.3f; k <= dist + 2.4f; k += 0.15f)
                {
                    Vector3 probe = feet + direction * k;
                    if (!GroundBelow(probe, plan.topY + 0.6f, 3.2f + height, out float groundY)) continue;
                    if (groundY > plan.topY - 0.25f) continue; // sigue encima del obstáculo

                    Vector3 land = feet + direction * (k + radius + 0.15f);
                    if (!GroundBelow(land, plan.topY + 0.6f, 3.2f + height, out float landY)) return Fail("sin suelo al otro lado");
                    if (landY > plan.topY - 0.2f || landY < feet.y - 2.5f) return Fail($"el otro lado está a {landY - feet.y:F2} m");
                    land.y = landY;
                    if (!Fits(land)) return Fail("no cabe al otro lado");
                    plan.kind = Kind.Vault;
                    plan.landing = land;
                    LastReason = $"saltar {low.collider.name} ({height:F2} m)";
                    return true;
                }

                // 3b. Obstáculo bajo y ancho: subirse encima
                Vector3 onTop = feet + direction * (dist + radius + 0.35f);
                onTop.y = plan.topY;
                if (!Fits(onTop)) return Fail("no cabe encima");
                plan.kind = Kind.Mount;
                plan.landing = onTop;
                LastReason = $"subirse a {low.collider.name} ({height:F2} m)";
                return true;
            }

            // 3c. Trepar: tiene que caber de pie arriba, un poco pasado el borde
            Vector3 ledge = feet + direction * (dist + radius + 0.3f);
            ledge.y = plan.topY;
            if (!Fits(ledge)) return Fail("no cabe encima de la cornisa");
            plan.kind = Kind.Climb;
            plan.landing = ledge;
            LastReason = $"trepar a {low.collider.name} ({height:F2} m)";
            return true;
        }

        private bool Fail(string reason)
        {
            LastReason = reason;
            return false;
        }

        /// <summary>
        /// Corriendo hacia un borde con suelo al otro lado (a menos de <see cref="maxGap"/> m y a una altura parecida):
        /// el salto largo de YariCombatController lo cruza.
        /// </summary>
        public bool CanLeapGap(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();
            Vector3 feet = transform.position;

            // Delante, a 1 m, no hay suelo cerca: es un borde
            if (GroundBelow(feet + direction * 1.0f, feet.y + 0.5f, 1.7f, out _)) return false;
            for (float k = 1.8f; k <= maxGap + 0.6f; k += 0.3f)
            {
                if (GroundBelow(feet + direction * k, feet.y + 1.0f, 2.2f, out float y) && y > feet.y - 1.2f)
                {
                    return true;
                }
            }
            return false;
        }

        private bool Cast(Vector3 from, Vector3 direction, float distance, out RaycastHit hit)
        {
            if (!Physics.SphereCast(from, 0.1f, direction, out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            // No se saltan ni se trepan los rivales ni el propio Yari
            return !IsCharacter(hit.collider);
        }

        private bool IsCharacter(Collider c)
        {
            if (c == null) return false;
            if (c.transform.IsChildOf(transform)) return true;
            return c.GetComponentInParent<EnemyController>() != null || c.GetComponentInParent<YariCombatController>() != null;
        }

        private static bool GroundBelow(Vector3 point, float fromY, float depth, out float groundY)
        {
            groundY = 0f;
            Vector3 from = new Vector3(point.x, fromY, point.z);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, depth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            groundY = hit.point.y;
            return hit.normal.y > 0.55f;
        }

        /// <summary>Cabe Yari de pie en ese punto (sin chocar con nada, ni rivales).</summary>
        private bool Fits(Vector3 feet)
        {
            float radius = (mover != null ? mover.radius : 0.35f) * 0.9f;
            float height = mover != null ? mover.height : 2f;
            Vector3 a = feet + Vector3.up * (radius + 0.08f);
            Vector3 b = feet + Vector3.up * Mathf.Max(radius + 0.1f, height - radius);
            foreach (Collider c in Physics.OverlapCapsule(a, b, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!c.transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        // ───────────────────────── Movimiento ─────────────────────────

        /// <summary>Intenta saltar o trepar hacia esa dirección. Devuelve true si empieza.</summary>
        public bool TryStart(Vector3 direction, float speed, Animator anim)
        {
            if (Active || !TryPlan(direction, speed, out Plan plan)) return false;

            animator = anim;
            kind = plan.kind;
            dir = plan.dir;
            start = transform.position;
            faceDist = plan.faceDist;
            face = start + dir * faceDist;
            topY = plan.topY;
            landing = plan.landing;
            elapsed = 0f;

            float horizontal = Vector3.Dot(landing - start, dir);
            if (kind == Kind.Climb)
            {
                duration = climbTime + climbTimePerMeter * Mathf.Max(0f, topY - start.y - maxVault);
                ExitSpeed = 0f;
            }
            else
            {
                float pace = Mathf.Max(speed, 2.6f);
                duration = Mathf.Clamp(horizontal / pace * 1.15f, vaultMinTime, vaultMaxTime);
                // Saltando en carrera sigue corriendo; desde parado o andando sale andando
                ExitSpeed = speed;
            }

            transform.rotation = Quaternion.LookRotation(dir);
            mover.enabled = false;
            PlayClip();
            Ayni.Core.AyniAudio.Play(kind == Kind.Climb ? "salto" : "esquiva_mov", transform.position + Vector3.up, 0.6f, 0.08f);
            return true;
        }

        private void PlayClip()
        {
            if (animator == null) return;
            bool climb = kind == Kind.Climb;
            string state = climb ? "Traverse_Climb" : "Traverse_Vault";
            if (!animator.HasState(0, Animator.StringToHash(state))) return;
            // Clips de la Fragua: Yari_Vault dura 0.6 s y Yari_Climb 1 s
            float clipLength = climb ? 1.0f : 0.6f;
            animator.SetFloat("JumpSpeed", clipLength / Mathf.Max(0.1f, duration));
            animator.CrossFadeInFixedTime(state, 0.06f, 0, 0f);
        }

        private void Update()
        {
            if (!Active) return;
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            transform.position = kind == Kind.Climb ? ClimbPath(u) : VaultPath(u);
            if (u >= 1f) Finish();
        }

        /// <summary>
        /// Saltar: avanza a ritmo constante (conserva la carrera) y por altura sube hasta rozar el borde con las piernas
        /// recogidas y cae hasta el suelo del otro lado. Fases del clip: apoyo 0.2 · encima 0.45 · aterriza 0.87.
        /// </summary>
        private Vector3 VaultPath(float u)
        {
            Vector3 p = Vector3.Lerp(start, landing, u);
            // Con las piernas recogidas los pies van ~0.45 m por encima de la raíz: así pasan por encima del borde
            float apex = topY + (kind == Kind.Mount ? 0.15f : 0.05f);
            const float peakAt = 0.45f;
            if (u < peakAt)
            {
                float k = u / peakAt;
                p.y = Mathf.Lerp(start.y, apex, Mathf.Sin(k * Mathf.PI * 0.5f));
            }
            else
            {
                float k = (u - peakAt) / (1f - peakAt);
                p.y = Mathf.Lerp(apex, landing.y, k * k);
            }
            return p;
        }

        /// <summary>
        /// Trepar: se acerca a la pared, se estira hasta el borde, tira hasta tener el pecho arriba, apoya la rodilla y se
        /// levanta ya encima. Fases del clip: impulso 0.14 · agarre 0.32 · tirón 0.55 · rodilla 0.74 · de pie 0.88.
        /// </summary>
        private Vector3 ClimbPath(float u)
        {
            float wall = Mathf.Max(0f, faceDist - 0.32f);
            float hang = Mathf.Max(start.y, topY - 2.05f);
            // (fase, avance desde el inicio, altura)
            float[] t = { 0f, 0.14f, 0.32f, 0.55f, 0.74f, 0.88f, 1f };
            float landDist = Vector3.Dot(landing - start, dir);
            float[] d = { 0f, wall, wall + 0.04f, faceDist - 0.22f, faceDist + 0.1f, faceDist + 0.35f, landDist };
            float[] y = { start.y, start.y, hang, topY - 1.05f, topY - 0.35f, topY - 0.05f, topY };

            int i = 0;
            while (i < t.Length - 2 && u > t[i + 1]) i++;
            float k = Mathf.InverseLerp(t[i], t[i + 1], u);
            k = k * k * (3f - 2f * k);
            Vector3 p = start + dir * Mathf.Lerp(d[i], d[i + 1], k);
            p.y = Mathf.Lerp(y[i], y[i + 1], k);
            return p;
        }

        private void Finish()
        {
            transform.position = landing;
            kind = Kind.None;
            mover.enabled = true;
            // Pegarlo al suelo: el siguiente Move del controlador ya lo encuentra apoyado
            mover.Move(Vector3.down * 0.05f);
            OnFinished?.Invoke();
        }

        /// <summary>Corta el movimiento (muerte, escena cinemática): Yari se queda donde está y recupera la física.</summary>
        public void Abort()
        {
            if (!Active) return;
            kind = Kind.None;
            mover.enabled = true;
        }

        private void OnDisable()
        {
            Abort();
        }
    }
}
