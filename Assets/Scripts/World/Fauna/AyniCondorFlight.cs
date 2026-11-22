using UnityEngine;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Vuelo ambiental del cóndor andino (Vultur gryphus, "Kuntur" en quechua).
    /// El cóndor casi no aletea: planea en círculos amplios aprovechando las corrientes térmicas
    /// que suben del cañón. Este componente reproduce ese comportamiento:
    ///  - Órbita circular alrededor de una térmica, con inclinación (alabeo) hacia el centro del giro.
    ///  - La térmica deriva lentamente dentro de un radio de paseo, para que el vuelo no sea repetitivo.
    ///  - Oscilación suave de altura (sube con la térmica y baja planeando).
    ///  - Aleteos ocasionales y lentos cada cierto tiempo, como hace el ave real.
    /// Es puramente decorativo: no tiene colisionadores ni interactúa con el combate.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniCondorFlight : MonoBehaviour
    {
        [Header("Térmica (centro del giro)")]
        [Tooltip("Centro de origen de la térmica en el mundo. Si es (0,0,0) se usa la posición inicial del cóndor.")]
        [SerializeField] private Vector3 homeCenter = Vector3.zero;
        [SerializeField] private float orbitRadius = 40f;
        [Tooltip("Distancia máxima que la térmica puede alejarse de su origen.")]
        [SerializeField] private float wanderRadius = 25f;
        [SerializeField] private float thermalDriftSpeed = 1.2f;

        [Header("Vuelo")]
        [Tooltip("Velocidad de planeo en metros por segundo (un cóndor real planea a unos 10-15 m/s).")]
        [SerializeField] private float glideSpeed = 11f;
        [Tooltip("Altura sobre el terreno bajo la térmica.")]
        [SerializeField] private float altitudeAboveGround = 38f;
        [SerializeField] private float altitudeBobAmplitude = 4f;
        [SerializeField] private float altitudeBobPeriod = 22f;
        [SerializeField] private float bankAngle = 22f;
        [SerializeField] private bool clockwise = false;

        [Header("Aleteo ocasional")]
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private float minFlapInterval = 9f;
        [SerializeField] private float maxFlapInterval = 18f;
        [SerializeField] private int flapsPerBurst = 2;
        [SerializeField] private float flapDuration = 0.9f;
        [SerializeField] private float flapAmplitude = 28f;
        [Tooltip("Ligera curvatura hacia arriba de las alas al planear (diedro).")]
        [SerializeField] private float glideDihedral = 6f;

        private float angle;
        private float bobPhase;
        private Vector3 currentCenter;
        private Vector3 thermalTarget;
        private float groundHeight;
        private float nextFlapTime;
        private float flapStartTime = -100f;
        private Quaternion leftWingRest;
        private Quaternion rightWingRest;
        private Animator anim;
        private bool isFlappingAnim;
        private bool joinFromHere;
        private Vector3 joinFrom;
        private float joinTime, joinDuration;

        /// <summary>Permite configurar el cóndor desde el menú de editor sin usar el Inspector.</summary>
        public void Configure(Vector3 center, float radius, float altitude, float speed, bool turnClockwise,
            Transform wingL, Transform wingR)
        {
            homeCenter = center;
            orbitRadius = Mathf.Max(5f, radius);
            altitudeAboveGround = altitude;
            glideSpeed = speed;
            clockwise = turnClockwise;
            leftWing = wingL;
            rightWing = wingR;
        }

        /// <summary>
        /// Para un cóndor que ya está volando en otro sitio (p. ej. el que sale de la jaula): entra en su giro desde
        /// donde está, sin saltar de golpe al círculo.
        /// </summary>
        public void JoinFromCurrentPosition()
        {
            joinFromHere = true;
        }

        private void Start()
        {
            if (homeCenter == Vector3.zero) homeCenter = transform.position;

            // Cada cóndor empieza en un punto distinto del giro para no volar sincronizados
            angle = Random.Range(0f, Mathf.PI * 2f);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            currentCenter = homeCenter;
            PickNewThermal();
            groundHeight = SampleGround(currentCenter);

            if (leftWing != null) leftWingRest = leftWing.localRotation;
            if (rightWing != null) rightWingRest = rightWing.localRotation;
            anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                // El modelo 3D está posado (cuerpo vertical): en vuelo va tendido, con la cabeza al frente
                AyniCondorModel.SetPitch(transform, AyniCondorModel.FlightPitch);
                anim.Play("Glide", 0, Random.value);
                isFlappingAnim = false;
            }

            ScheduleNextFlap();
            if (joinFromHere)
            {
                // Empieza en el punto del giro más cercano y llega hasta él volando a su velocidad
                Vector3 offset = transform.position - currentCenter;
                angle = Mathf.Atan2(offset.z, offset.x);
                joinFrom = transform.position;
                joinTime = 0f;
                joinDuration = Mathf.Clamp(Vector3.Distance(joinFrom, OrbitPoint(angle)) / Mathf.Max(1f, glideSpeed), 1.5f, 14f);
                return;
            }
            transform.position = OrbitPoint(angle);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. La térmica deriva lentamente hacia un nuevo punto
            currentCenter = Vector3.MoveTowards(currentCenter, thermalTarget, thermalDriftSpeed * dt);
            if ((currentCenter - thermalTarget).sqrMagnitude < 1f)
            {
                PickNewThermal();
            }
            groundHeight = Mathf.Lerp(groundHeight, SampleGround(currentCenter), dt * 0.2f);

            // 2. Avance angular a velocidad lineal constante sobre la circunferencia
            float dir = clockwise ? -1f : 1f;
            angle += dir * (glideSpeed / orbitRadius) * dt;

            Vector3 previous = transform.position;
            Vector3 next = OrbitPoint(angle);
            if (joinDuration > 0f && joinTime < joinDuration)
            {
                joinTime += dt;
                next = Vector3.Lerp(joinFrom, next, Mathf.SmoothStep(0f, 1f, joinTime / joinDuration));
            }
            transform.position = next;

            // 3. Orientación: mirar hacia donde vuela y alabear hacia el centro del giro
            Vector3 velocity = next - previous;
            if (velocity.sqrMagnitude > 0.000001f)
            {
                Quaternion look = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                Quaternion bank = Quaternion.AngleAxis(bankAngle * dir, Vector3.forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, look * bank, dt * 2.5f);
            }

            // 4. Alas: diedro de planeo y aleteos ocasionales
            UpdateWings();
        }

        private Vector3 OrbitPoint(float a)
        {
            float bob = Mathf.Sin(Time.time * (Mathf.PI * 2f / altitudeBobPeriod) + bobPhase) * altitudeBobAmplitude;
            return new Vector3(
                currentCenter.x + Mathf.Cos(a) * orbitRadius,
                groundHeight + altitudeAboveGround + bob,
                currentCenter.z + Mathf.Sin(a) * orbitRadius);
        }

        private void UpdateWings()
        {
            if (Time.time >= nextFlapTime)
            {
                flapStartTime = Time.time;
                ScheduleNextFlap();
            }

            float flapTime = Time.time - flapStartTime;
            float burstLength = flapDuration * flapsPerBurst;
            bool flapping = flapTime < burstLength;

            if (anim != null && anim.runtimeAnimatorController != null)
            {
                if (flapping != isFlappingAnim)
                {
                    isFlappingAnim = flapping;
                    anim.CrossFadeInFixedTime(flapping ? "Fly_Flap" : "Glide", 0.30f);
                }
                return;
            }

            if (leftWing == null && rightWing == null) return;

            float flap = 0f;
            if (flapping)
            {
                // Aleteo lento y profundo; se suaviza al empezar y al terminar
                float envelope = Mathf.Sin(Mathf.Clamp01(flapTime / burstLength) * Mathf.PI);
                flap = Mathf.Sin(flapTime / flapDuration * Mathf.PI * 2f) * flapAmplitude * envelope;
            }

            // Un giro positivo en Z sube la punta que apunta a +X (ala derecha) y baja la que apunta a -X
            // (ala izquierda, reflejada con escala X negativa). Por eso el ala izquierda usa el signo contrario.
            float wingAngle = glideDihedral + flap;
            if (leftWing != null) leftWing.localRotation = leftWingRest * Quaternion.AngleAxis(-wingAngle, Vector3.forward);
            if (rightWing != null) rightWing.localRotation = rightWingRest * Quaternion.AngleAxis(wingAngle, Vector3.forward);
        }

        private void ScheduleNextFlap()
        {
            nextFlapTime = Time.time + Random.Range(minFlapInterval, maxFlapInterval);
        }

        private void PickNewThermal()
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            thermalTarget = new Vector3(homeCenter.x + offset.x, homeCenter.y, homeCenter.z + offset.y);
        }

        private static float SampleGround(Vector3 worldPos)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return 0f;
            return terrain.SampleHeight(worldPos) + terrain.GetPosition().y;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 c = Application.isPlaying ? currentCenter : (homeCenter == Vector3.zero ? transform.position : homeCenter);
            c.y = transform.position.y;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * orbitRadius,
                                c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * orbitRadius);
            }
        }
    }
}
