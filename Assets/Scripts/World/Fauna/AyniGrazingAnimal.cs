using UnityEngine;
using Ayni.Player;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Comportamiento ambiental de un camélido andino (llama, alpaca o vicuña) que pasta cerca de la aldea.
    /// Alterna tres estados de forma tranquila:
    ///  - Pastar: el cuello baja hasta el ichu y la cabeza mordisquea.
    ///  - Mirar: levanta el cuello y gira la cabeza a los lados, atento.
    ///  - Caminar: avanza despacio hacia otro punto de pasto dentro de su zona.
    /// Los camélidos caminan en "paso de andadura": mueven a la vez las dos patas del mismo lado.
    /// También sirve para el zorro andino, que trota en diagonal y "pasta" olfateando el suelo.
    /// El animal sigue la altura e inclinación del terreno y solo elige destinos sobre el terreno abierto
    /// (nunca dentro de casas, muros o rocas). Es decorativo: no tiene colisionadores.
    /// Si Yari se acerca demasiado, el animal levanta la cabeza y se aleja trotando; la vicuña, que es silvestre,
    /// se espanta desde más lejos que la llama, acostumbrada a convivir con los pastores.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniGrazingAnimal : MonoBehaviour
    {
        protected enum State { Grazing, Looking, Walking, Fleeing }

        [Header("Zona de pastoreo")]
        [SerializeField] protected Vector3 home;
        [SerializeField] protected float wanderRadius = 12f;
        [SerializeField] protected float walkSpeed = 0.9f;
        [SerializeField] protected float turnSpeed = 90f;
        [Tooltip("Inclinación máxima (grados) del terreno donde el animal acepta ir a pastar.")]
        [SerializeField] protected float maxSlope = 28f;

        [Header("Reacción ante Yari")]
        [Tooltip("Distancia a la que el animal se espanta. Llama ~5 m, alpaca ~7 m, vicuña ~16 m.")]
        [SerializeField] protected float alertRadius = 6f;
        [SerializeField] protected float fleeSpeed = 3.2f;
        [SerializeField] protected float fleeDistance = 12f;

        [Header("Esqueleto simple (lo asigna AyniCamelidModel)")]
        [SerializeField] protected Transform body;
        [SerializeField] protected Transform neck;
        [SerializeField] protected Transform head;
        [SerializeField] protected Transform[] legs = new Transform[4];
        [SerializeField] protected Transform tail;

        [Header("Poses")]
        [SerializeField] protected float neckUpAngle = 12f;
        [SerializeField] protected float neckGrazeAngle = 118f;
        [SerializeField] protected float legSwing = 24f;
        [SerializeField] protected float strideLength = 0.85f;
        [Tooltip("Paso de andadura (patas del mismo lado juntas, camélidos). Desactivado = trote diagonal (zorro).")]
        [SerializeField] protected bool pacingGait = true;

        protected State state = State.Grazing;
        protected float stateTimer;
        protected Vector3 target;
        protected float yaw;
        protected float gaitPhase;
        protected float currentSpeed;

        private float neckAngle;
        private float headPitch;
        private float headYaw;
        private float lookYawTarget;
        private float nextLookSwitch;
        private Vector3 bodyRestPos;
        private float nextAlertCheck;
        private Quaternion tailRest = Quaternion.identity;

        private static Transform cachedPlayer;
        private static float nextPlayerSearch;

        public void Configure(Vector3 homePosition, float radius, float speed, AyniCamelidModel.Parts parts)
        {
            home = homePosition;
            wanderRadius = radius;
            walkSpeed = speed;
            body = parts.body;
            neck = parts.neck;
            head = parts.head;
            legs = parts.legs;
        }

        /// <summary>Configuración genérica para cualquier cuadrúpedo con cuello, cabeza y cuatro patas.</summary>
        public void Configure(Vector3 homePosition, float radius, float speed,
            Transform bodyPivot, Transform neckPivot, Transform headPivot, Transform[] legPivots, Transform tailPivot)
        {
            home = homePosition;
            wanderRadius = radius;
            walkSpeed = speed;
            body = bodyPivot;
            neck = neckPivot;
            head = headPivot;
            legs = legPivots;
            tail = tailPivot;
        }

        /// <summary>Ajusta las poses y la marcha (por ejemplo, para el zorro: cuello corto y trote diagonal).</summary>
        public void ConfigurePose(float neckUp, float neckDown, float swing, float stride, bool pacing)
        {
            neckUpAngle = neckUp;
            neckGrazeAngle = neckDown;
            legSwing = swing;
            strideLength = stride;
            pacingGait = pacing;
        }

        /// <summary>Ajusta qué tan asustadizo es el animal ante Yari.</summary>
        public void ConfigureAlert(float radius, float runSpeed)
        {
            alertRadius = radius;
            fleeSpeed = runSpeed;
        }

        protected virtual void Start()
        {
            if (home == Vector3.zero) home = transform.position;
            yaw = transform.eulerAngles.y;
            if (body != null) bodyRestPos = body.localPosition;
            if (tail != null) tailRest = tail.localRotation;

            // Cada animal empieza en un estado y momento distinto para que el rebaño no se mueva sincronizado
            neckAngle = neckGrazeAngle;
            EnterState(Random.value < 0.7f ? State.Grazing : State.Looking);
            stateTimer *= Random.value;
            SnapToGround();
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            stateTimer -= dt;
            float targetSpeed = 0f;

            if (state != State.Fleeing && Time.time >= nextAlertCheck)
            {
                nextAlertCheck = Time.time + 0.25f;
                CheckForYari();
            }

            switch (state)
            {
                case State.Grazing:
                    if (stateTimer <= 0f) EnterState(Random.value < 0.55f ? State.Looking : State.Walking);
                    break;

                case State.Looking:
                    if (stateTimer <= 0f) EnterState(Random.value < 0.6f ? State.Grazing : State.Walking);
                    break;

                case State.Walking:
                    targetSpeed = walkSpeed;
                    if (SteerTowards(target, dt) || stateTimer <= 0f) EnterState(State.Grazing);
                    break;

                case State.Fleeing:
                    targetSpeed = fleeSpeed;
                    // Al terminar de huir se queda mirando, todavía atento
                    if (SteerTowards(target, dt) || stateTimer <= 0f) EnterState(State.Looking);
                    break;
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, dt * 1.5f);
            Move(dt);
            AnimatePose(dt);
        }

        // ------------------------------------------------------------------------------------------------
        protected virtual void EnterState(State next)
        {
            state = next;
            switch (next)
            {
                case State.Grazing:
                    stateTimer = Random.Range(5f, 14f);
                    break;
                case State.Looking:
                    stateTimer = Random.Range(2.5f, 6f);
                    nextLookSwitch = 0f;
                    break;
                case State.Walking:
                    if (!TryPickDestination(home, wanderRadius, out target))
                    {
                        state = State.Grazing;
                        stateTimer = Random.Range(4f, 8f);
                        return;
                    }
                    stateTimer = 20f; // límite de seguridad por si algo bloquea el camino
                    break;
                case State.Fleeing:
                    stateTimer = 6f;
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------
        private void CheckForYari()
        {
            Transform player = FindPlayer();
            if (player == null) return;

            Vector3 away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude > alertRadius * alertRadius) return;

            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();

            // Huir en sentido contrario a Yari, con algo de variación, sin alejarse demasiado de su zona
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-35f, 35f) * (attempt + 1) * 0.5f, 0f) * away;
                Vector3 candidate = transform.position + dir * fleeDistance;
                Vector3 fromHome = candidate - home;
                fromHome.y = 0f;
                float maxRange = wanderRadius * 1.8f;
                if (fromHome.magnitude > maxRange) candidate = home + fromHome.normalized * maxRange;

                if (IsOpenGround(candidate, maxSlope, out float groundY))
                {
                    candidate.y = groundY;
                    target = candidate;
                    EnterState(State.Fleeing);
                    return;
                }
            }

            // Sin salida despejada: al menos levanta la cabeza y vigila
            if (state == State.Grazing) EnterState(State.Looking);
        }

        private static Transform FindPlayer()
        {
            if (cachedPlayer == null && Time.time >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.time + 2f;
                var yari = FindFirstObjectByType<YariCombatController>();
                cachedPlayer = yari != null ? yari.transform : null;
            }
            return cachedPlayer;
        }

        /// <summary>Gira hacia el destino y devuelve true al llegar.</summary>
        protected bool SteerTowards(Vector3 destination, float dt)
        {
            Vector3 to = destination - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.36f) return true;

            float desiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, turnSpeed * dt);
            return false;
        }

        protected void Move(float dt)
        {
            if (currentSpeed > 0.001f)
            {
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                transform.position += forward * currentSpeed * dt;
                gaitPhase += currentSpeed / strideLength * Mathf.PI * 2f * dt;
            }
            SnapToGround();
        }

        private void AnimatePose(float dt)
        {
            // Cuello: abajo al pastar, arriba al mirar o caminar
            float neckTarget = state == State.Grazing ? neckGrazeAngle : neckUpAngle;
            neckAngle = Mathf.MoveTowards(neckAngle, neckTarget, dt * 70f);

            // Cabeza: mordisquea al pastar, mira a los lados cuando está atenta
            float pitchTarget = 0f;
            if (state == State.Grazing)
            {
                pitchTarget = 55f + Mathf.Sin(Time.time * 5.5f + GetInstanceID()) * 6f;
                lookYawTarget = 0f;
            }
            else if (state == State.Looking)
            {
                if (Time.time >= nextLookSwitch)
                {
                    lookYawTarget = Random.Range(-40f, 40f);
                    nextLookSwitch = Time.time + Random.Range(0.8f, 2f);
                }
            }
            else
            {
                lookYawTarget = 0f;
            }
            headPitch = Mathf.MoveTowards(headPitch, pitchTarget, dt * 60f);
            headYaw = Mathf.MoveTowardsAngle(headYaw, lookYawTarget, dt * 70f);

            if (neck != null) neck.localRotation = Quaternion.Euler(neckAngle, 0f, 0f);
            // La cabeza compensa el giro del cuello para que el hocico mire al frente (o al pasto)
            if (head != null) head.localRotation = Quaternion.Euler(-neckAngle + headPitch, headYaw, 0f);

            // Paso de andadura: izquierdas (0 y 2) juntas y derechas (1 y 3) en oposición.
            // Trote diagonal: del-izq con tras-der (0 y 3) y del-der con tras-izq (1 y 2).
            // Al trotar (huida) las patas se abren más que al caminar
            float swing = Mathf.Clamp(currentSpeed / Mathf.Max(0.1f, walkSpeed), 0f, 1.6f) * legSwing;
            float s = Mathf.Sin(gaitPhase) * swing;
            if (legs != null && legs.Length == 4)
            {
                SetLeg(0, s);
                SetLeg(1, -s);
                SetLeg(2, pacingGait ? s : -s);
                SetLeg(3, pacingGait ? -s : s);
            }

            // Cola: vaivén suave en reposo, más marcado al moverse
            if (tail != null)
            {
                float tailAmp = 4f + swing * 0.4f;
                float tailSway = Mathf.Sin(Time.time * (1.2f + currentSpeed) + GetInstanceID() * 0.1f) * tailAmp;
                tail.localRotation = tailRest * Quaternion.Euler(0f, 0f, tailSway);
            }

            // Leve balanceo del cuerpo al caminar
            if (body != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.025f * (swing / Mathf.Max(1f, legSwing));
                float roll = Mathf.Sin(gaitPhase) * 2.5f * (swing / Mathf.Max(1f, legSwing));
                body.localPosition = bodyRestPos + new Vector3(0f, bob, 0f);
                body.localRotation = Quaternion.Euler(0f, 0f, roll);
            }
        }

        private void SetLeg(int index, float angle)
        {
            if (legs[index] != null) legs[index].localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Apoya al animal en el terreno y lo inclina según la pendiente bajo sus patas.</summary>
        protected void SnapToGround()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return;
            }

            Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pos = transform.position;
            Vector3 fwd = flat * Vector3.forward * 0.6f;
            float hFront = GroundHeight(terrain, pos + fwd);
            float hBack = GroundHeight(terrain, pos - fwd);

            pos.y = (hFront + hBack) * 0.5f;
            transform.position = pos;

            float pitch = Mathf.Atan2(hBack - hFront, 1.2f) * Mathf.Rad2Deg;
            transform.rotation = flat * Quaternion.Euler(Mathf.Clamp(pitch, -25f, 25f), 0f, 0f);
        }

        protected static float GroundHeight(Terrain terrain, Vector3 worldPos)
        {
            return terrain.SampleHeight(worldPos) + terrain.GetPosition().y;
        }

        /// <summary>
        /// Busca un punto de pasto dentro de la zona: terreno descubierto (sin casas, muros ni rocas encima)
        /// y con pendiente suave.
        /// </summary>
        protected bool TryPickDestination(Vector3 center, float radius, out Vector3 result)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = new Vector3(center.x + offset.x, 0f, center.z + offset.y);
                if (IsOpenGround(candidate, maxSlope, out float groundY))
                {
                    candidate.y = groundY;
                    result = candidate;
                    return true;
                }
            }
            result = transform.position;
            return false;
        }

        /// <summary>True si el primer objeto bajo el punto es el terreno y la pendiente es aceptable.</summary>
        public static bool IsOpenGround(Vector3 point, float maxSlopeDegrees, out float groundY)
        {
            groundY = 0f;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return false;

            groundY = GroundHeight(terrain, point);

            Vector3 local = point - terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            float nx = local.x / size.x;
            float nz = local.z / size.z;
            if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return false;
            if (terrain.terrainData.GetSteepness(nx, nz) > maxSlopeDegrees) return false;

            Vector3 from = new Vector3(point.x, groundY + 60f, point.z);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.collider is TerrainCollider;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 c = home == Vector3.zero ? transform.position : home;
            Gizmos.color = new Color(0.55f, 0.85f, 0.35f, 0.8f);
            const int segments = 40;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * wanderRadius,
                                c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * wanderRadius);
            }
        }
    }
}
