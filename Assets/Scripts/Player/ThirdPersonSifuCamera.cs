using System.Collections.Generic;
using UnityEngine;

namespace Ayni.Player
{
    /// <summary>
    /// Cámara en tercera persona con encuadre cinematográfico cercano estilo Sifu.
    /// - Empieza detrás de Yari (no de frente ni de lado).
    /// - Sigue al personaje sobre el hombro y gira con el ratón.
    /// - Si hay una montaña/roca/muro entre la cámara y Yari, la cámara se acerca al instante (sin atravesarlo) y vuelve
    ///   a alejarse con suavidad cuando hay sitio. Los rivales y los animales no la empujan.
    /// - Bajo techo (dentro de una casa) se acerca al hombro para no meterse en los muros.
    /// - Con un rival fijado (Lock-On) se coloca sola detrás de Yari mirando hacia el rival y encuadra a los dos.
    /// - Al correr y al esprintar abre un poco el campo de visión y se aleja: Yari se siente más rápido sin serlo.
    /// </summary>
    public class ThirdPersonSifuCamera : MonoBehaviour
    {
        [Header("Objetivo")]
        [SerializeField] private Transform target;

        // v0.17: varios campos cambiaron de nombre para que la escena tome los valores nuevos en vez de los guardados
        [Header("Distancia y Ángulos")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.5f, 1.5f, -3.2f);   // Desplazamiento sobre el hombro
        [SerializeField] private float lookHeight = 1.5f;                         // Altura del pecho/cabeza de Yari
        [Tooltip("Inclinación inicial de la cámara (y al volver de una escena). Baja: se ve más horizonte y menos suelo.")]
        [SerializeField] private float defaultPitch = 8f;
        [SerializeField] private float mouseSensitivity = 3.0f;
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Mando (stick derecho)")]
        [Tooltip("Grados por segundo que gira la cámara con el stick derecho al fondo.")]
        [SerializeField] private float stickYawSpeed = 170f;
        [SerializeField] private float stickPitchSpeed = 110f;
        [SerializeField] private bool invertStickY = false;

        [Header("Colisión con el escenario")]
        [SerializeField] private float wallRadius = 0.3f;
        [Tooltip("Distancia mínima a Yari cuando un muro empuja la cámara (si el muro está aún más cerca, manda el muro).")]
        [SerializeField] private float minWallDistance = 0.45f;
        [Tooltip("Plano cercano de la cámara: bajo, para que no se vea el interior de un muro pegado a la cámara.")]
        [SerializeField] private float nearClip = 0.08f;
        [Tooltip("Si un muro deja la cámara a menos de estos metros, sube por encima del hombro de Yari.")]
        [SerializeField] private float craneBelow = 1.1f;
        [SerializeField] private float craneHeight = 0.75f;
        [Tooltip("A menos de estos metros de la cabeza de Yari, su cuerpo deja de dibujarse (solo su sombra).")]
        [SerializeField] private float hideBelow = 0.3f;

        [Header("Bajo techo")]
        [Tooltip("Encuadre dentro de una casa: más cerca del hombro y más bajo.")]
        [SerializeField] private Vector3 indoorOffset = new Vector3(0.38f, 1.35f, -2.2f);
        [Tooltip("Altura máxima del techo (sobre los pies de Yari) para considerar que está bajo techo.")]
        [SerializeField] private float roofCheckHeight = 4.5f;

        [Header("Fijación de Blanco (Lock-On)")]
        [Tooltip("Rapidez con la que la cámara se coloca detrás de Yari mirando al rival fijado.")]
        [SerializeField] private float lockYawSpeed = 6f;
        [Tooltip("Inclinación de la cámara con un rival fijado.")]
        [SerializeField] private float lockTiltDeg = 10f;
        [Tooltip("0 = mira solo a Yari, 1 = mira solo al rival. Un valor intermedio encuadra a los dos.")]
        [Range(0f, 1f)] [SerializeField] private float lockFramingAmount = 0.3f;
        [SerializeField] private float lockTargetHeight = 1.3f;
        [Tooltip("Metros como mucho que se desplaza el punto de mira hacia el rival: con uno lejano (huyendo) Yari no se sale del cuadro.")]
        [SerializeField] private float lockMaxLookShift = 1.8f;
        [Tooltip("Con un rival fijado la cámara se aleja un poco para que quepan los dos.")]
        [SerializeField] private float lockPullBack = 0.12f;
        [Tooltip("Metros a los que el rival fijado empieza a contar para el encuadre; más lejos, la cámara mira solo a Yari.")]
        [SerializeField] private float lockFramingRange = 12f;

        [Header("Sensación de velocidad")]
        [Tooltip("Grados que se abre el campo de visión al correr a fondo. Yari no va más rápido: solo lo parece.")]
        [SerializeField] private float runFovKick = 4f;
        [Tooltip("Grados que se abre el campo de visión en sprint (en total, no sumados a los de correr).")]
        [SerializeField] private float sprintFovKick = 10f;
        [Tooltip("Cuánto se aleja la cámara al correr a fondo (0.06 = un 6 % más lejos).")]
        [SerializeField] private float runPullBack = 0.06f;
        [Tooltip("Cuánto se aleja la cámara en sprint.")]
        [SerializeField] private float sprintPullBack = 0.15f;
        [Tooltip("Segundos que tarda en abrirse o cerrarse. Lento a propósito: que no se note el cambio, solo la velocidad.")]
        [SerializeField] private float speedFeelTime = 0.45f;

        private Camera cam;
        private float baseFov = -1f;
        private float speedFeel;          // 0 = quieto o caminando, 1 = corriendo a fondo, 2 = sprint (suavizado)
        private float speedFeelVelocity;

        private float yaw;
        private float pitch;
        private bool initialized;
        private YariCombatController yari;
        private float lockBlend;          // 0 = cámara libre, 1 = encuadre de rival fijado (suavizado)
        private Vector3 lastEnemyPoint;
        private float indoorBlend;        // 0 = al aire libre, 1 = bajo techo (suavizado)
        private float indoorCheckAt;
        private bool indoor;
        private readonly RaycastHit[] castHits = new RaycastHit[16];
        private bool targetHidden;
        private readonly List<KeyValuePair<Renderer, UnityEngine.Rendering.ShadowCastingMode>> hiddenRenderers =
            new List<KeyValuePair<Renderer, UnityEngine.Rendering.ShadowCastingMode>>();

        // Sensación de impacto
        private Vector3 smoothedPosition;
        private float shakeAmplitude;
        private float shakeDuration;
        private float shakeStart = -10f;
        private float finisherUntil = -10f;
        private float finisherBlend;

        [Header("Impacto")]
        [Tooltip("Cuánto se acerca la cámara durante un remate (1 = nada, 0.6 = bastante).")]
        [Range(0.4f, 1f)] [SerializeField] private float finisherZoom = 0.62f;
        [Tooltip("Rigidez del muelle del empujón de los golpes. Más alto = vuelve antes a su sitio.")]
        [SerializeField] private float kickStiffness = 260f;
        [Tooltip("Amortiguación del muelle. Con 18 da un único rebote pequeño; con 32 vuelve sin rebotar.")]
        [SerializeField] private float kickDamping = 18f;
        [Tooltip("Grados que se inclina la cámara por cada metro de fuerza del golpe.")]
        [SerializeField] private float kickTiltPerMeter = 30f;

        // Empujón con muelle: posición (x derecha, y arriba, z adelante de la cámara) e inclinación (cabeceo, giro, alabeo)
        private Vector3 kickOffset, kickVelocity;
        private Vector3 kickAngles, kickAngularVelocity;
        private float shakeSeed;

        private void OnEnable()
        {
            Ayni.Combat.CombatFeedback.OnShake += HandleShake;
            Ayni.Combat.CombatFeedback.OnKick += HandleKick;
            Ayni.Combat.CombatFeedback.OnFinisherCamera += HandleFinisherCamera;
        }

        private void OnDisable()
        {
            Ayni.Combat.CombatFeedback.OnShake -= HandleShake;
            Ayni.Combat.CombatFeedback.OnKick -= HandleKick;
            Ayni.Combat.CombatFeedback.OnFinisherCamera -= HandleFinisherCamera;
            // Si Yari estaba oculto por tener la cámara encima, vuelve a verse (una escena toma la cámara)
            if (targetHidden && target != null) UpdateTargetFade(99f);
            kickOffset = kickVelocity = kickAngles = kickAngularVelocity = Vector3.zero;

            // Una escena toma la cámara: se le devuelve con su campo de visión normal
            speedFeel = 0f;
            speedFeelVelocity = 0f;
            if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov;
        }

        /// <summary>
        /// Abre el campo de visión según lo rápido que va Yari. Con un rival fijado, en los remates y en las
        /// caídas se queda en el encuadre normal: ahí manda el combate.
        /// </summary>
        private void UpdateSpeedFeel(bool allowed)
        {
            float target = 0f;
            if (allowed && yari != null && !yari.IsDead)
            {
                float pace = yari.PaceRatio;
                target = yari.IsSprinting ? 2f : Mathf.InverseLerp(0.6f, 1f, pace);
            }
            speedFeel = Mathf.SmoothDamp(speedFeel, target, ref speedFeelVelocity, speedFeelTime);

            if (cam != null && baseFov > 0f)
            {
                float kick = speedFeel <= 1f ? runFovKick * speedFeel : Mathf.Lerp(runFovKick, sprintFovKick, speedFeel - 1f);
                cam.fieldOfView = baseFov + kick;
            }
        }

        private float SpeedPullBack =>
            speedFeel <= 1f ? runPullBack * speedFeel : Mathf.Lerp(runPullBack, sprintPullBack, speedFeel - 1f);

        private void HandleShake(float amplitude, float duration)
        {
            // Si ya hay una sacudida más fuerte en curso, se conserva
            float remaining = 1f - Mathf.Clamp01((Time.unscaledTime - shakeStart) / Mathf.Max(0.01f, shakeDuration));
            if (shakeAmplitude * remaining > amplitude) return;
            shakeAmplitude = amplitude;
            shakeDuration = Mathf.Max(0.02f, duration);
            shakeStart = Time.unscaledTime;
            shakeSeed = Random.Range(0f, 100f);
        }

        /// <summary>
        /// Empuja la cámara hacia donde va el golpe (un puñetazo hacia delante la mete un poco hacia el rival) y la
        /// inclina hacia ese lado; el muelle la devuelve a su sitio con un rebote. Es lo que da peso al impacto:
        /// un golpe seco en una dirección, no un temblor.
        /// </summary>
        private void HandleKick(Vector3 worldDirection, float strength)
        {
            if (strength <= 0f) return;
            Vector3 dir = worldDirection.sqrMagnitude > 0.0001f
                ? worldDirection.normalized
                : transform.right * (Random.value < 0.5f ? -1f : 1f);

            // A la cámara: x derecha, y arriba, z adelante. Los golpes horizontales también la hunden un poco.
            Vector3 local = new Vector3(Vector3.Dot(dir, transform.right), Vector3.Dot(dir, transform.up) - 0.35f,
                                        Vector3.Dot(dir, transform.forward));
            // Velocidad inicial del muelle: el pico de desplazamiento queda cerca de la fuerza pedida
            float omega = Mathf.Sqrt(Mathf.Max(1f, kickStiffness));
            kickVelocity += local * (strength * omega * 1.6f);

            // Cabeceo hacia abajo (asiente con el golpe) y alabeo hacia el lado del impacto
            float tilt = strength * kickTiltPerMeter * omega * 1.6f;
            kickAngularVelocity += new Vector3(tilt * (0.6f + Mathf.Abs(local.z) * 0.4f), tilt * 0.25f * local.x, -tilt * local.x);
        }

        // Caída al vacío: la cámara no sigue a Yari hacia abajo, se queda arriba y lo mira caer
        private bool watchingFall;
        private float watchHeight;
        private float watchUntil;

        /// <summary>
        /// La cámara deja de seguir al personaje: sube a la altura indicada (la del borde), se coloca casi
        /// encima de él y lo mira caer. Dura hasta StopWatchingFall o hasta que pasen los segundos indicados.
        /// </summary>
        public void WatchFall(float height, float maxSeconds)
        {
            watchingFall = true;
            watchHeight = height;
            watchUntil = Time.unscaledTime + maxSeconds;
        }

        public void StopWatchingFall()
        {
            watchingFall = false;
        }

        private void UpdateFallWatch()
        {
            // Sobre el vacío, un poco por detrás de donde venía la cámara para no mirar en vertical pura
            Vector3 back = transform.position - target.position;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : -target.forward;
            Vector3 vantage = new Vector3(target.position.x, watchHeight, target.position.z) + back * 1.6f;
            vantage.y = Mathf.Max(vantage.y, target.position.y + 3.5f);

            smoothedPosition = Vector3.Lerp(smoothedPosition, vantage, 5f * Time.unscaledDeltaTime);
            transform.position = smoothedPosition;

            Vector3 toTarget = target.position + Vector3.up * 0.9f - smoothedPosition;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(toTarget, back * -1f + Vector3.up * 0.35f);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 7f * Time.unscaledDeltaTime);
            }
            ApplyShake();
        }

        private void HandleFinisherCamera(float duration)
        {
            finisherUntil = Time.unscaledTime + duration;
        }

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            if (target != null) yari = target.GetComponent<YariCombatController>();

            cam = GetComponent<Camera>();
            if (cam != null)
            {
                baseFov = cam.fieldOfView;
                cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, nearClip);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SnapBehindTarget();
        }

        /// <summary>Coloca la cámara directamente detrás del personaje (sin suavizado). La usan las escenas al terminar.</summary>
        public void SnapBehindTarget()
        {
            if (target == null) return;

            yaw = target.eulerAngles.y;
            pitch = defaultPitch;
            smoothedPosition = ComputeCameraPosition();
            transform.position = smoothedPosition;
            transform.LookAt(target.position + Vector3.up * lookHeight);
            initialized = true;
        }

        private Vector3 ComputeCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            // Bajo techo, más cerca del hombro; en los remates se acerca; al correr y esprintar y con un rival
            // fijado se aleja un poco
            Vector3 baseOffset = Vector3.Lerp(shoulderOffset, indoorOffset, indoorBlend);
            Vector3 framing = Vector3.Lerp(baseOffset, new Vector3(baseOffset.x, baseOffset.y, baseOffset.z * finisherZoom), finisherBlend);
            framing.z *= 1f + SpeedPullBack + lockPullBack * lockBlend * (1f - indoorBlend);
            Vector3 desired = target.position + rotation * framing;
            return Unblocked(desired);
        }

        private Vector3 Pivot => target.position + Vector3.up * Mathf.Lerp(lookHeight, indoorOffset.y, indoorBlend);

        /// <summary>
        /// El punto más lejano entre Yari y <paramref name="desired"/> al que puede ir la cámara sin atravesar el terreno,
        /// las rocas o los muros. Los personajes (Yari, rivales, aldeanos) y los disparadores no cuentan.
        /// </summary>
        private Vector3 Unblocked(Vector3 desired)
        {
            Vector3 pivot = Pivot;
            Vector3 dir = desired - pivot;
            float dist = dir.magnitude;
            if (dist < 0.001f) return desired;
            dir /= dist;
            int count = Physics.SphereCastNonAlloc(pivot, wallRadius, dir, castHits, dist,
                                                   Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = dist;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (hit.collider == null || IgnoredByCamera(hit.collider)) continue;
                // distance 0: la esfera ya empieza dentro de algo (muy pegado a un muro); se trata como muro a 0 m
                if (hit.distance < nearest) nearest = hit.distance;
            }
            if (nearest >= dist) return desired;
            // Contra el muro, pero sin bajar de la distancia mínima salvo que el muro esté aún más cerca
            float keep = nearest >= minWallDistance ? nearest : Mathf.Max(0.12f, nearest);
            Vector3 blocked = pivot + dir * keep;
            if (keep >= craneBelow) return blocked;

            // Muy pegada (Yari de espaldas a un muro): en vez de meterse en su nuca, la cámara sube y mira por encima
            // de su hombro, si por arriba hay sitio
            Vector3 back = -dir;
            back.y = 0f;
            back = back.sqrMagnitude > 0.001f ? back.normalized : -target.forward;
            Vector3 crane = pivot + Vector3.up * craneHeight - back * 0.35f;
            Vector3 craneDir = crane - pivot;
            float craneDist = craneDir.magnitude;
            craneDir /= craneDist;
            float craneFree = craneDist;
            count = Physics.SphereCastNonAlloc(pivot, wallRadius * 0.8f, craneDir, castHits, craneDist,
                                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (hit.collider == null || IgnoredByCamera(hit.collider)) continue;
                if (hit.distance < craneFree) craneFree = hit.distance;
            }
            if (craneFree <= keep) return blocked;
            // Mezcla: cuanto más pegada al muro, más arriba
            float t = Mathf.InverseLerp(craneBelow, 0.12f, keep);
            return Vector3.Lerp(blocked, pivot + craneDir * craneFree, t);
        }

        /// <summary>
        /// Con la cámara casi dentro de Yari (pegado a un muro) se dejan de dibujar sus mallas (siguen proyectando sombra)
        /// para no llenar la pantalla con su pelo.
        /// </summary>
        private void UpdateTargetFade(float distanceToPivot)
        {
            bool hide = distanceToPivot < hideBelow || (targetHidden && distanceToPivot < hideBelow + 0.15f);
            if (hide == targetHidden) return;
            targetHidden = hide;
            if (hide)
            {
                hiddenRenderers.Clear();
                foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
                {
                    if (r == null || !r.enabled || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                    hiddenRenderers.Add(new KeyValuePair<Renderer, UnityEngine.Rendering.ShadowCastingMode>(r, r.shadowCastingMode));
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
            else
            {
                foreach (var pair in hiddenRenderers) if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
                hiddenRenderers.Clear();
            }
        }

        private bool IgnoredByCamera(Collider c)
        {
            if (c.transform.IsChildOf(target)) return true;
            if (c is CharacterController) return true;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return true;
            return c.GetComponentInParent<Ayni.Enemy.EnemyController>() != null;
        }

        /// <summary>¿Hay techo sobre Yari (casa, kallanka)? Se comprueba unas veces por segundo.</summary>
        private void UpdateIndoor()
        {
            if (Time.unscaledTime >= indoorCheckAt)
            {
                indoorCheckAt = Time.unscaledTime + 0.2f;
                Vector3 head = target.position + Vector3.up * 1.2f;
                indoor = false;
                int count = Physics.RaycastNonAlloc(head, Vector3.up, castHits, roofCheckHeight - 1.2f,
                                                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (castHits[i].collider != null && !IgnoredByCamera(castHits[i].collider)) { indoor = true; break; }
                }
            }
            indoorBlend = Mathf.MoveTowards(indoorBlend, indoor ? 1f : 0f, 1.6f * Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Empujón con muelle y sacudida de impacto, en tiempo real para que se noten también durante la micro-pausa
        /// del golpe. La sacudida es ruido suave (Perlin), no una posición al azar en cada fotograma: así se siente
        /// como un temblor del cuerpo y no como un zumbido.
        /// </summary>
        private void ApplyShake()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            kickVelocity += (-kickStiffness * kickOffset - kickDamping * kickVelocity) * dt;
            kickOffset += kickVelocity * dt;
            kickAngularVelocity += (-kickStiffness * kickAngles - kickDamping * kickAngularVelocity) * dt;
            kickAngles += kickAngularVelocity * dt;

            Vector3 offset = transform.right * kickOffset.x + transform.up * kickOffset.y + transform.forward * kickOffset.z;
            Vector3 angles = kickAngles;

            float shakeT = (Time.unscaledTime - shakeStart) / Mathf.Max(0.01f, shakeDuration);
            if (shakeT < 1f)
            {
                float strength = shakeAmplitude * (1f - shakeT) * (1f - shakeT);
                float t = Time.unscaledTime * 28f;
                float nx = Mathf.PerlinNoise(shakeSeed, t) * 2f - 1f;
                float ny = Mathf.PerlinNoise(shakeSeed + 31.7f, t) * 2f - 1f;
                float nr = Mathf.PerlinNoise(shakeSeed + 63.1f, t) * 2f - 1f;
                offset += (transform.right * nx + transform.up * ny) * strength;
                angles.z += nr * strength * 40f;
            }

            if (offset.sqrMagnitude < 1e-8f && angles.sqrMagnitude < 1e-6f) return;
            transform.position = smoothedPosition + offset;
            transform.rotation *= Quaternion.Euler(angles);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (!initialized) SnapBehindTarget();

            if (watchingFall)
            {
                if (Time.unscaledTime > watchUntil) watchingFall = false;
                else
                {
                    UpdateSpeedFeel(false);
                    UpdateFallWatch();
                    return;
                }
            }

            Transform lockTarget = (yari != null && yari.LockTarget != null) ? yari.LockTarget.transform : null;

            if (lockTarget != null)
            {
                // Rival fijado: la cámara se alinea sola detrás de Yari, mirando hacia el rival
                Vector3 toEnemy = lockTarget.position - target.position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.01f)
                {
                    float desiredYaw = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, desiredYaw, lockYawSpeed * Time.deltaTime);
                }
                pitch = Mathf.Lerp(pitch, lockTiltDeg, lockYawSpeed * Time.deltaTime);

                lastEnemyPoint = lockTarget.position + Vector3.up * lockTargetHeight;
            }
            else if (!Ayni.Core.AyniGameState.InputLocked)
            {
                // Cámara libre con el ratón o el stick derecho (no gira mientras el tutorial está abierto)
                Vector2 mouse = Ayni.Core.AyniInput.MouseDelta;
                Vector2 stick = Ayni.Core.AyniInput.LookStick;
                yaw += mouse.x * mouseSensitivity + stick.x * stickYawSpeed * Time.unscaledDeltaTime;
                pitch -= mouse.y * mouseSensitivity + stick.y * (invertStickY ? -1f : 1f) * stickPitchSpeed * Time.unscaledDeltaTime;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            finisherBlend = Mathf.MoveTowards(finisherBlend, Time.unscaledTime < finisherUntil ? 1f : 0f, 4f * Time.unscaledDeltaTime);
            UpdateSpeedFeel(lockTarget == null && Time.unscaledTime >= finisherUntil && !Ayni.Core.AyniGameState.InputLocked);

            UpdateIndoor();
            lockBlend = Mathf.MoveTowards(lockBlend, lockTarget != null ? 1f : 0f, 3f * Time.deltaTime);

            Vector3 desiredPosition = ComputeCameraPosition();
            smoothedPosition = Vector3.Lerp(smoothedPosition, desiredPosition, smoothSpeed * Time.deltaTime);
            // El suavizado solo sirve para alejarse: si entre Yari y la cámara suavizada queda un muro o el terreno
            // (un muro que aparece de golpe, un salto, un teletransporte), la cámara se pone delante al instante
            smoothedPosition = Unblocked(smoothedPosition);
            transform.position = smoothedPosition;
            float toPivot = Vector3.Distance(smoothedPosition, Pivot);
            UpdateTargetFade(toPivot);

            // El punto de mira se desplaza suavemente de Yari hacia el rival al fijar, y vuelve al soltar; con un rival
            // lejano el desplazamiento se limita para que Yari no se salga del cuadro
            Vector3 lookPoint = target.position + Vector3.up * Mathf.Lerp(lookHeight, indoorOffset.y, indoorBlend);
            if (lockBlend > 0f)
            {
                // Solo cuenta un rival cercano: con uno lejano (el que huye) la cámara mira a Yari
                Vector3 toRival = lastEnemyPoint - lookPoint;
                float near = Mathf.InverseLerp(lockFramingRange, lockFramingRange * 0.45f, new Vector2(toRival.x, toRival.z).magnitude);
                Vector3 shift = Vector3.ClampMagnitude(toRival * lockFramingAmount, lockMaxLookShift);
                lookPoint += shift * (lockBlend * near);
            }

            transform.LookAt(lookPoint);

            ApplyShake();

            // El cursor lo libera el menú de pausa (Esc), que también sirve para salir del Play en el Editor
        }

        /// <summary>Estado de la cámara en la consola (para el puente de agentes).</summary>
        public static void Report()
        {
            var c = Camera.main != null ? Camera.main.GetComponent<ThirdPersonSifuCamera>() : null;
            if (c == null || c.target == null) { Debug.LogWarning("[Ayni Cámara] No hay cámara de juego activa"); return; }
            float d = Vector3.Distance(c.transform.position, c.Pivot);
            Debug.Log($"[Ayni Cámara] a {d:F2} m de Yari · bajo techo {c.indoor} ({c.indoorBlend:F2}) · fijado {c.lockBlend:F2} · " +
                      $"cabeceo {c.pitch:F0}° · plano cercano {(c.cam != null ? c.cam.nearClipPlane : -1f):F2}");
        }
    }
}
