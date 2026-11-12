using UnityEngine;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Comportamiento ambiental de un camélido andino (llama, alpaca o vicuña) que pasta cerca de la aldea.
    /// Alterna tres estados de forma tranquila:
    ///  - Pastar: el cuello baja hasta el ichu y la cabeza mordisquea.
    ///  - Mirar: levanta el cuello y gira la cabeza a los lados, atento.
    ///  - Caminar: avanza despacio hacia otro punto de pasto dentro de su zona.
    /// Los camélidos caminan en "paso de andadura": mueven a la vez las dos patas del mismo lado.
    /// El animal sigue la altura e inclinación del terreno y solo elige destinos sobre el terreno abierto
    /// (nunca dentro de casas, muros o rocas). Es decorativo: no tiene colisionadores.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniGrazingAnimal : MonoBehaviour
    {
        protected enum State { Grazing, Looking, Walking }

        [Header("Zona de pastoreo")]
        [SerializeField] protected Vector3 home;
        [SerializeField] protected float wanderRadius = 12f;
        [SerializeField] protected float walkSpeed = 0.9f;
        [SerializeField] protected float turnSpeed = 90f;
        [Tooltip("Inclinación máxima (grados) del terreno donde el animal acepta ir a pastar.")]
        [SerializeField] protected float maxSlope = 28f;

        [Header("Esqueleto simple (lo asigna AyniCamelidModel)")]
        [SerializeField] protected Transform body;
        [SerializeField] protected Transform neck;
        [SerializeField] protected Transform head;
        [SerializeField] protected Transform[] legs = new Transform[4];

        [Header("Poses")]
        [SerializeField] protected float neckUpAngle = 12f;
        [SerializeField] protected float neckGrazeAngle = 118f;
        [SerializeField] protected float legSwing = 24f;
        [SerializeField] protected float strideLength = 0.85f;

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

        protected virtual void Start()
        {
            if (home == Vector3.zero) home = transform.position;
            yaw = transform.eulerAngles.y;
            if (body != null) bodyRestPos = body.localPosition;

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
            }
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

            // Patas en paso de andadura: izquierdas (0 y 2) juntas, derechas (1 y 3) en oposición
            float swing = Mathf.Clamp01(currentSpeed / Mathf.Max(0.1f, walkSpeed)) * legSwing;
            float s = Mathf.Sin(gaitPhase) * swing;
            if (legs != null && legs.Length == 4)
            {
                SetLeg(0, s);
                SetLeg(2, s);
                SetLeg(1, -s);
                SetLeg(3, -s);
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
