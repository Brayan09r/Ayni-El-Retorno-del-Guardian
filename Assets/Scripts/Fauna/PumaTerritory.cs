using Ayni.Core;
using Ayni.Enemy;
using UnityEngine;

namespace Ayni.Fauna
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(EnemyController), typeof(CharacterController))]
    public sealed class PumaTerritory : MonoBehaviour
    {
        [SerializeField] private float disengageDistance = 12f;
        [SerializeField] private float territoryRadius = 18f;
        [SerializeField] private float returnSpeed = 3f;
        private EnemyController enemy;
        private CharacterController mover;
        private Transform player;
        private Vector3 origin;
        private Quaternion originalRotation;
        private bool returning;
        private float gravity;
        private bool despawning;

        private void Start()
        {
            enemy = GetComponent<EnemyController>();
            mover = GetComponent<CharacterController>();
            origin = transform.position;
            originalRotation = transform.rotation;
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target != null) player = target.transform;
        }

        private void Update()
        {
            if (enemy == null) return;
            if (enemy.IsDead)
            {
                if (!despawning)
                {
                    despawning = true;
                    foreach (Renderer body in GetComponentsInChildren<Renderer>()) body.enabled = false;
                    Destroy(gameObject, 0.2f);
                }
                return;
            }
            if (AyniGameState.CinematicPlaying) return;
            Vector3 home = origin - transform.position; home.y = 0f;
            Vector3 toPlayer = player == null ? Vector3.one * 10000f : player.position - transform.position;
            toPlayer.y = 0f;
            if (!returning && home.sqrMagnitude > .25f &&
                (toPlayer.sqrMagnitude > disengageDistance * disengageDistance || home.sqrMagnitude > territoryRadius * territoryRadius))
            {
                returning = true; enemy.ExternalControl = true;
                if (enemy.Animator != null) enemy.Animator.CrossFadeInFixedTime("Run", .15f);
            }
            if (!returning) return;
            // Respeta aturdimientos y derribos mientras regresa.
            if (enemy.State == EnemyState.Stunned || enemy.State == EnemyState.Downed || enemy.State == EnemyState.Stagger)
            {
                returning = false; enemy.ExternalControl = false; return;
            }
            if (home.sqrMagnitude <= .12f)
            {
                returning = false; enemy.ExternalControl = false;
                transform.rotation = originalRotation;
                if (enemy.Animator != null) { enemy.Animator.SetFloat("Speed", 0f); enemy.Animator.CrossFadeInFixedTime("Idle", .15f); }
                return;
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(home), Time.deltaTime * 8f);
            gravity = mover.isGrounded ? -2f : gravity - 18f * Time.deltaTime;
            mover.Move((home.normalized * returnSpeed + Vector3.up * gravity) * Time.deltaTime);
            if (enemy.Animator != null) enemy.Animator.SetFloat("Speed", 1f);
        }

        private void OnDisable() { if (enemy != null) enemy.ExternalControl = false; }
    }
}
