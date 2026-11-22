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
        private Animator anim;
        private string currentAnimState;

        // ── Modelo 3D riggeado (Resources/Fauna) ──
        // Velocidad a la que pasa el suelo bajo las patas apoyadas en cada clip, a escala 1 (medido con
        // Ayni.Editor.AyniFaunaProbe.GaitSpeed). Con ella se ajusta la velocidad de la animación para que no patinen.
        private const float LlamaWalkGround = 0.5f, LlamaRunGround = 1.8f;
        private const float VicunaWalkGround = 0.33f, VicunaRunGround = 0.85f;
        /// <summary>Más rápido que esto la animación se ve atropellada: el animal no corre más de lo que sus patas pueden.</summary>
        private const float MaxAnimSpeed = 2.6f;
        private float walkGround = LlamaWalkGround, runGround = LlamaRunGround;
        private float modelScale = 1f;
        // El clip "Graze" del FBX dobla el cuello hacia atrás, sobre el lomo. Al pastar se usa "Idle" y el cuello
        // se baja por código en LateUpdate (encima de la animación).
        private Transform neckBase, neckMid, headBone;
        private Quaternion lastNeckWrite;
        private float grazeWeight;
        private float lookYaw;
        private float groundPitch;
        // Al pastar: el cuello apunta recto hacia el suelo, por delante de las patas, y la cabeza baja el hocico hacia el
        // ichu sin seguir el giro del cuello (si lo siguiera, se "muerde" el cuello). Ángulos bajo la horizontal.
        private const float GrazeNeckDown = 58f, GrazeHeadDown = 72f;

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
            anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null) SetupRig();

            // Cada animal empieza en un estado y momento distinto para que el rebaño no se mueva sincronizado
            neckAngle = neckGrazeAngle;
            EnterState(Random.value < 0.7f ? State.Grazing : State.Looking);
            stateTimer *= Random.value;
            if (anim != null && !string.IsNullOrEmpty(currentAnimState))
            {
                anim.Play(currentAnimState, 0, Random.value);
            }
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
                    // Si el destino queda muy de lado, primero gira casi en el sitio (antes daba vueltas en círculo)
                    targetSpeed = walkSpeed * HeadingFactor(target, 0.25f);
                    if (SteerTowards(target, dt) || stateTimer <= 0f) EnterState(State.Grazing);
                    break;

                case State.Fleeing:
                    targetSpeed = fleeSpeed * HeadingFactor(target, 0.55f);
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

        /// <summary>1 si el destino está delante; baja hasta <paramref name="min"/> cuando queda de lado o detrás.</summary>
        private float HeadingFactor(Vector3 destination, float min)
        {
            Vector3 to = destination - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return 1f;
            float error = Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg));
            return Mathf.Lerp(min, 1f, Mathf.InverseLerp(100f, 25f, error));
        }

        /// <summary>Prepara el modelo 3D: medidas de su marcha, huesos del cuello y tope de velocidad al huir.</summary>
        private void SetupRig()
        {
            bool vicuna = anim.runtimeAnimatorController.name.IndexOf("Vicuna", System.StringComparison.OrdinalIgnoreCase) >= 0;
            walkGround = vicuna ? VicunaWalkGround : LlamaWalkGround;
            runGround = vicuna ? VicunaRunGround : LlamaRunGround;
            modelScale = Mathf.Max(0.05f, anim.transform.lossyScale.x);
            fleeSpeed = Mathf.Min(fleeSpeed, runGround * modelScale * MaxAnimSpeed);
            walkSpeed = Mathf.Min(walkSpeed, walkGround * modelScale * MaxAnimSpeed);

            foreach (Transform t in anim.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Neck_1") neckBase = t;
                else if (t.name == "Neck_2") neckMid = t;
                else if (t.name == "Head") headBone = t;
            }
        }

        protected virtual void LateUpdate()
        {
            if (anim == null || neckBase == null || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;

            // Al pastar baja el cuello hasta el ichu; al mirar gira la cabeza a los lados
            bool grazing = state == State.Grazing && currentSpeed < 0.05f;
            grazeWeight = Mathf.MoveTowards(grazeWeight, grazing ? 1f : 0f, dt * (grazing ? 0.8f : 1.6f));
            if (state == State.Looking)
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
            lookYaw = Mathf.MoveTowardsAngle(lookYaw, lookYawTarget, dt * 70f);

            float w = Mathf.SmoothStep(0f, 1f, grazeWeight);
            if (w < 0.001f && Mathf.Abs(lookYaw) < 0.05f) return;
            // Con el Animator fuera de cámara (CullUpdateTransforms) los huesos no se reescriben: no sumar dos veces
            if (Quaternion.Angle(neckBase.localRotation, lastNeckWrite) < 0.01f) return;

            Vector3 right = transform.right;
            Vector3 up = transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float nibble = Mathf.Sin(Time.time * 5.5f + GetInstanceID()) * 5f * w;
            // Orientación de la cabeza en el mundo según la animación (hocico al frente), antes de mover el cuello
            Quaternion headPose = headBone != null ? headBone.rotation : Quaternion.identity;

            // Cuello: casi todo el giro en la base y el resto en el medio, para que quede recto y no se doble sobre sí mismo
            if (headBone != null)
            {
                Vector3 down = Quaternion.AngleAxis(GrazeNeckDown, right) * forward;
                AimBone(neckBase, headBone.position, down, w * 0.85f);
                if (neckMid != null) AimBone(neckMid, headBone.position, Quaternion.AngleAxis(GrazeNeckDown + 6f, right) * forward, w);
            }
            if (Mathf.Abs(lookYaw) > 0.01f) neckBase.rotation = Quaternion.AngleAxis(lookYaw * 0.5f, up) * neckBase.rotation;

            // Cabeza: la de la animación, girada hacia abajo hasta el pasto (y hacia el lado al mirar)
            if (headBone != null)
            {
                headBone.rotation = Quaternion.AngleAxis(lookYaw, up) * Quaternion.AngleAxis((GrazeHeadDown + nibble) * w, right) * headPose;
            }
            lastNeckWrite = neckBase.localRotation;
        }

        /// <summary>Gira <paramref name="bone"/> para que apunte hacia <paramref name="dir"/> el tramo hasta <paramref name="childPos"/>.</summary>
        private static void AimBone(Transform bone, Vector3 childPos, Vector3 dir, float weight)
        {
            Vector3 now = childPos - bone.position;
            if (now.sqrMagnitude < 1e-6f || weight <= 0f) return;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, dir), weight) * bone.rotation;
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
            // Si el animal usa un modelo 3D riggeado con Animator, dirigir sus clips y sincronizar velocidad sin deslizamiento
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                // La velocidad de la animación sigue a la del animal: así las pezuñas apoyadas no patinan
                string desiredState;
                float animSpeed = 1f;
                if (currentSpeed > walkSpeed * 1.45f)
                {
                    desiredState = "Run";
                    animSpeed = Mathf.Clamp(currentSpeed / (runGround * modelScale), 0.7f, MaxAnimSpeed);
                }
                else if (currentSpeed > 0.08f)
                {
                    desiredState = "Walk";
                    animSpeed = Mathf.Clamp(currentSpeed / (walkGround * modelScale), 0.5f, MaxAnimSpeed);
                }
                else
                {
                    // Pastar también parte de "Idle": el cuello se baja en LateUpdate
                    desiredState = "Idle";
                }

                if (desiredState != currentAnimState)
                {
                    currentAnimState = desiredState;
                    anim.CrossFadeInFixedTime(desiredState, 0.25f);
                }
                anim.speed = animSpeed;
                return;
            }

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

            // El modelo 3D se inclina menos y de forma suave: en cuestas fuertes se veía echado hacia atrás y daba tirones
            float pitch = Mathf.Atan2(hBack - hFront, 1.2f) * Mathf.Rad2Deg;
            float limit = anim != null ? 16f : 25f;
            pitch = Mathf.Clamp(pitch, -limit, limit);
            groundPitch = anim != null && Time.deltaTime > 0f ? Mathf.MoveTowards(groundPitch, pitch, 45f * Time.deltaTime) : pitch;
            transform.rotation = flat * Quaternion.Euler(groundPitch, 0f, 0f);
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
