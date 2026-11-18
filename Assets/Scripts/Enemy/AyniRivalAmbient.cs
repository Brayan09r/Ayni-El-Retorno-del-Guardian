using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Player;

namespace Ayni.Enemy
{
    /// <summary>
    /// Lo que hace un rival antes de la pelea, como en Sifu: nadie aparece de la nada; los rivales ya están en la
    /// kancha haciendo lo suyo (patrullar caminando, hacer guardia mirando alrededor o descansar arrodillados) y
    /// solo pelean cuando ven u oyen a Yari, cuando él entra en el patio o cuando lo golpean. El que lo descubre se
    /// gira hacia él y avisa al resto del grupo, que se van sumando con unas décimas de diferencia.
    /// AyniEncounterSite se lo pone a sus rivales al empezar.
    /// </summary>
    public class AyniRivalAmbient : MonoBehaviour
    {
        public enum Activity { Guard, Patrol, Rest }

        /// <summary>Un rival acaba de descubrir a Yari y da la voz (la música entra en combate en ese instante).</summary>
        public static event System.Action<AyniRivalAmbient> OnAlert;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnAlert = null;

        [Tooltip("Distancia a la que ve a Yari si lo tiene delante (y sin muros en medio).")]
        [SerializeField] private float sightRange = 11f;
        [Tooltip("Distancia a la que lo oye aunque esté de espaldas (más si Yari corre).")]
        [SerializeField] private float hearRange = 4.5f;
        [SerializeField] private float patrolSpeed = 1.15f;

        private static readonly int WalkState = Animator.StringToHash("Ambient_Walk");

        private EnemyController enemy;
        private CharacterController mover;
        private Animator animator;
        private Transform yari;
        private YariCombatController yariController;
        private List<AyniRivalAmbient> group;
        private Activity activity;
        private Vector3 home, patrolEnd, target;
        private float baseYaw, lookYaw, timer, wakeAt, verticalSpeed, startHealth;
        private bool waking, awake, walking;

        public bool IsAwake => awake || waking;

        /// <summary>Pone al rival en su rutina. <paramref name="group"/> son los del mismo patio, a los que avisa.</summary>
        public void Begin(Activity what, Vector3 patrolTo, List<AyniRivalAmbient> mates)
        {
            enemy = GetComponent<EnemyController>();
            mover = GetComponent<CharacterController>();
            animator = enemy != null ? enemy.Animator : GetComponentInChildren<Animator>();
            group = mates;
            activity = what;
            home = transform.position;
            baseYaw = lookYaw = transform.eulerAngles.y;
            patrolEnd = patrolTo;
            target = patrolEnd;
            startHealth = enemy != null ? enemy.CurrentHealth : 0f;
            timer = Random.Range(1f, 3f);
            if (enemy != null)
            {
                enemy.ExternalControl = true;
                enemy.Dormant = true;
            }

            if (animator != null && activity == Activity.Patrol && !animator.HasState(0, WalkState)) activity = Activity.Guard;
            if (animator != null && activity == Activity.Rest && animator.HasState(0, Animator.StringToHash("MercyKneel")))
            {
                animator.CrossFadeInFixedTime("MercyKneel", 0.1f, 0, 1f); // ya arrodillado, sin el gesto de rendirse
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.transform;
                yariController = player.GetComponent<YariCombatController>();
            }
        }

        private void Update()
        {
            if (enemy == null || awake) return;
            if (enemy.IsDead)
            {
                enabled = false;
                return;
            }
            if (waking)
            {
                UpdateWaking();
                return;
            }
            if (AyniGameState.CinematicPlaying) return;

            // Golpeado mientras estaba en lo suyo: despierta al instante y avisa a todos
            if (enemy.CurrentHealth < startHealth - 0.01f)
            {
                Notice(0f, true);
                return;
            }
            if (yari != null && Perceives()) Notice(0f, true);
            if (waking) return;

            switch (activity)
            {
                case Activity.Patrol: UpdatePatrol(); break;
                case Activity.Guard: UpdateGuard(); break;
            }
            StickToGround();
        }

        private bool Perceives()
        {
            if (yariController != null && yariController.IsDead) return false;
            Vector3 to = yari.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            float hearing = hearRange * (yariController != null && yariController.IsSprinting ? 2f : 1f);
            if (distance <= hearing) return true;
            if (distance > sightRange) return false;
            // Arrodillado mira al suelo: solo oye
            if (activity == Activity.Rest) return false;
            if (Vector3.Angle(transform.forward, to) > 65f) return false;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 head = yari.position + Vector3.up * 1.4f;
            return !Physics.Linecast(eye, head, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                   hit.transform == yari || hit.transform.IsChildOf(yari);
        }

        /// <summary>Descubre a Yari (o le avisan): se gira hacia él y, tras <paramref name="delay"/> s, pelea.</summary>
        public void Notice(float delay, bool alertGroup)
        {
            if (awake || waking) return;
            waking = true;
            wakeAt = Time.time + delay + (activity == Activity.Rest ? 0.9f : 0.35f);
            walking = false;
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                // El que descansaba se pone de pie; los demás se plantan
                animator.CrossFadeInFixedTime(activity == Activity.Rest && animator.HasState(0, Animator.StringToHash("GetUp")) ? "GetUp" : "Idle", 0.2f);
            }
            if (alertGroup)
            {
                OnAlert?.Invoke(this);
                if (group != null)
                {
                    float stagger = 0.15f;
                    foreach (AyniRivalAmbient mate in group)
                    {
                        if (mate == null || mate == this) continue;
                        mate.Notice(stagger, false);
                        stagger += Random.Range(0.15f, 0.35f);
                    }
                }
            }
        }

        private void UpdateWaking()
        {
            if (yari != null)
            {
                Vector3 to = yari.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
                }
            }
            StickToGround();
            if (Time.time < wakeAt) return;
            waking = false;
            awake = true;
            if (enemy != null)
            {
                enemy.ExternalControl = false;
                enemy.Dormant = false;
            }
            enabled = false;
        }

        private void UpdatePatrol()
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (!walking)
            {
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                walking = true;
                if (animator != null) animator.CrossFadeInFixedTime(WalkState, 0.3f);
            }
            if (to.magnitude < 0.4f)
            {
                walking = false;
                timer = Random.Range(2f, 4f);
                target = (target - home).sqrMagnitude < 0.01f ? patrolEnd : home;
                if (animator != null) animator.CrossFadeInFixedTime("Idle", 0.35f);
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 140f * Time.deltaTime);
            if (mover != null && mover.enabled) mover.Move(transform.forward * (patrolSpeed * Time.deltaTime));
        }

        private void UpdateGuard()
        {
            // Mira a un lado y a otro de su puesto, despacio
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = Random.Range(2.5f, 5f);
                lookYaw = baseYaw + Random.Range(-40f, 40f);
            }
            Quaternion look = Quaternion.Euler(0f, lookYaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 45f * Time.deltaTime);
        }

        private void StickToGround()
        {
            if (mover == null || !mover.enabled) return;
            verticalSpeed = mover.isGrounded ? -2f : verticalSpeed - 18f * Time.deltaTime;
            mover.Move(Vector3.up * (verticalSpeed * Time.deltaTime));
        }
    }
}
