using UnityEngine;

namespace Ayni.Player
{
    /// <summary>
    /// Cámara en tercera persona con encuadre cinematográfico cercano estilo Sifu.
    /// - Empieza detrás de Yari (no de frente ni de lado).
    /// - Sigue al personaje sobre el hombro y gira con el ratón.
    /// - Si hay una montaña/roca entre la cámara y Yari, la cámara se acerca para no perderlo de vista.
    /// - Con un rival fijado (Lock-On) se coloca sola detrás de Yari mirando hacia el rival y encuadra a los dos.
    /// </summary>
    public class ThirdPersonSifuCamera : MonoBehaviour
    {
        [Header("Objetivo")]
        [SerializeField] private Transform target;

        [Header("Distancia y Ángulos")]
        [SerializeField] private Vector3 offset = new Vector3(0.5f, 1.6f, -3.2f); // Desplazamiento sobre el hombro
        [SerializeField] private float lookAtHeight = 1.4f;                         // Altura del pecho/cabeza de Yari
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
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private float minDistance = 0.8f;

        [Header("Fijación de Blanco (Lock-On)")]
        [Tooltip("Rapidez con la que la cámara se coloca detrás de Yari mirando al rival fijado.")]
        [SerializeField] private float lockYawSpeed = 6f;
        [Tooltip("Inclinación de la cámara con un rival fijado.")]
        [SerializeField] private float lockPitch = 14f;
        [Tooltip("0 = mira solo a Yari, 1 = mira solo al rival. Un valor intermedio encuadra a los dos.")]
        [Range(0f, 1f)] [SerializeField] private float lockFraming = 0.35f;
        [SerializeField] private float lockTargetHeight = 1.3f;

        private float yaw;
        private float pitch;
        private bool initialized;
        private YariCombatController yari;
        private float lockBlend;          // 0 = cámara libre, 1 = encuadre de rival fijado (suavizado)
        private Vector3 lastEnemyPoint;

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

        private void OnEnable()
        {
            Ayni.Combat.CombatFeedback.OnShake += HandleShake;
            Ayni.Combat.CombatFeedback.OnFinisherCamera += HandleFinisherCamera;
        }

        private void OnDisable()
        {
            Ayni.Combat.CombatFeedback.OnShake -= HandleShake;
            Ayni.Combat.CombatFeedback.OnFinisherCamera -= HandleFinisherCamera;
        }

        private void HandleShake(float amplitude, float duration)
        {
            // Si ya hay una sacudida más fuerte en curso, se conserva
            float remaining = 1f - Mathf.Clamp01((Time.unscaledTime - shakeStart) / Mathf.Max(0.01f, shakeDuration));
            if (shakeAmplitude * remaining > amplitude) return;
            shakeAmplitude = amplitude;
            shakeDuration = Mathf.Max(0.02f, duration);
            shakeStart = Time.unscaledTime;
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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SnapBehindTarget();
        }

        /// <summary>Coloca la cámara directamente detrás del personaje (sin suavizado). La usan las escenas al terminar.</summary>
        public void SnapBehindTarget()
        {
            if (target == null) return;

            yaw = target.eulerAngles.y;
            pitch = 10f;
            smoothedPosition = ComputeCameraPosition();
            transform.position = smoothedPosition;
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
            initialized = true;
        }

        private Vector3 ComputeCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * lookAtHeight;
            // En los remates la cámara se acerca
            Vector3 desired = target.position + rotation * Vector3.Lerp(offset, new Vector3(offset.x, offset.y, offset.z * finisherZoom), finisherBlend);

            // Evitar que la cámara atraviese el terreno o las rocas
            Vector3 dir = desired - pivot;
            float dist = dir.magnitude;
            if (dist > 0.001f &&
                Physics.SphereCast(pivot, collisionRadius, dir / dist, out RaycastHit hit, dist,
                                   Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(target))
            {
                desired = pivot + dir / dist * Mathf.Max(hit.distance, minDistance);
            }

            return desired;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (!initialized) SnapBehindTarget();

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
                pitch = Mathf.Lerp(pitch, lockPitch, lockYawSpeed * Time.deltaTime);

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

            Vector3 desiredPosition = ComputeCameraPosition();
            smoothedPosition = Vector3.Lerp(smoothedPosition, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;

            // El punto de mira se desplaza suavemente de Yari hacia el rival al fijar, y vuelve al soltar
            lockBlend = Mathf.MoveTowards(lockBlend, lockTarget != null ? 1f : 0f, 3f * Time.deltaTime);
            Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
            if (lockBlend > 0f) lookPoint = Vector3.Lerp(lookPoint, lastEnemyPoint, lockFraming * lockBlend);
            transform.LookAt(lookPoint);

            // Sacudida de impacto (en tiempo real, para que se note también durante la micro-pausa del golpe)
            float shakeT = (Time.unscaledTime - shakeStart) / Mathf.Max(0.01f, shakeDuration);
            if (shakeT < 1f)
            {
                float strength = shakeAmplitude * (1f - shakeT);
                Vector2 jitter = Random.insideUnitCircle * strength;
                transform.position = smoothedPosition + transform.right * jitter.x + transform.up * jitter.y;
            }

            // Escape libera el cursor (útil para salir del Play en el Editor)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
