using UnityEngine;

namespace Ayni.Core
{
    /// <summary>
    /// Pasos sobre la piedra: suena un paso cada vez que el personaje recorre una zancada por el suelo.
    /// La zancada se alarga al correr y el paso suena más fuerte. Lo añaden solos YariCombatController y EnemyController.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniFootsteps : MonoBehaviour
    {
        [SerializeField] private float volume = 0.5f;
        [Tooltip("Metros entre paso y paso caminando y corriendo.")]
        [SerializeField] private float walkStride = 0.62f;
        [SerializeField] private float runStride = 1.15f;

        private CharacterController controller;
        private Vector3 lastPosition;
        private float travelled;
        private float lastGroundedAt;

        public static AyniFootsteps Attach(GameObject owner, float volume)
        {
            var steps = owner.GetComponent<AyniFootsteps>();
            if (steps == null) steps = owner.AddComponent<AyniFootsteps>();
            steps.volume = volume;
            return steps;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            delta.y = 0f;
            float distance = delta.magnitude;

            // Las cuestas hacen que el CharacterController pierda el suelo algún fotograma: margen de 0.15 s
            if (controller != null && controller.enabled && controller.isGrounded) lastGroundedAt = Time.time;
            bool grounded = controller == null || Time.time - lastGroundedAt < 0.15f;

            // Teletransportes (rescates, escenas) y el aire no cuentan como pasos
            if (!grounded || Time.deltaTime <= 0f || distance > 1.5f || AyniGameState.CinematicPlaying)
            {
                return;
            }

            float speed = distance / Time.deltaTime;
            if (speed < 0.35f)
            {
                // Al pararse, el siguiente paso suena enseguida al volver a andar
                travelled = Mathf.Max(travelled, walkStride * 0.6f);
                return;
            }

            float stride = Mathf.Lerp(walkStride, runStride, Mathf.InverseLerp(1.2f, 5f, speed));
            travelled += distance;
            if (travelled >= stride)
            {
                travelled -= stride;
                float loud = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0.8f, 5f, speed));
                AyniAudio.Play("paso_piedra", position, volume * loud, 0.1f);
            }
        }
    }
}
