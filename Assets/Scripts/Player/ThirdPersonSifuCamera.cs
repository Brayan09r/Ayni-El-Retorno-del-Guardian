using UnityEngine;

namespace Ayni.Player
{
    /// <summary>
    /// Cámara en tercera persona con encuadre cinematográfico cercano estilo Sifu.
    /// - Empieza detrás de Yari (no de frente ni de lado).
    /// - Sigue al personaje sobre el hombro y gira con el ratón.
    /// - Si hay una montaña/roca entre la cámara y Yari, la cámara se acerca para no perderlo de vista.
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

        [Header("Colisión con el escenario")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private float minDistance = 0.8f;

        private float yaw;
        private float pitch;
        private bool initialized;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SnapBehindTarget();
        }

        /// <summary>Coloca la cámara directamente detrás del personaje (sin suavizado).</summary>
        private void SnapBehindTarget()
        {
            if (target == null) return;

            yaw = target.eulerAngles.y;
            pitch = 10f;
            transform.position = ComputeCameraPosition();
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
            initialized = true;
        }

        private Vector3 ComputeCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * lookAtHeight;
            Vector3 desired = target.position + rotation * offset;

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

            // Entrada de ratón
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Vector3 desiredPosition = ComputeCameraPosition();
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * lookAtHeight);

            // Escape libera el cursor (útil para salir del Play en el Editor)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
