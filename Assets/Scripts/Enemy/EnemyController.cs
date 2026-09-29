using System;
using UnityEngine;
using Ayni.Combat;

namespace Ayni.Enemy
{
    [RequireComponent(typeof(StructureSystem))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Datos del Rival")]
        [SerializeField] private string characterName = "Rival";
        [SerializeField] private bool isBoss = false;
        [SerializeField] private float maxHealth = 150f;
        [SerializeField] private float currentHealth;

        [Header("Combate")]
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float structureDamageOnPlayer = 20f;
        [SerializeField] private float attackCooldown = 2.0f;
        [SerializeField] private float attackRange = 2.2f;

        private StructureSystem structure;
        private Animator animator;
        private Transform playerTarget;
        private float nextAttackTime;
        private bool isDead;

        public string CharacterName => characterName;
        public bool IsBoss => isBoss;
        public bool IsDead => isDead;
        public StructureSystem Structure => structure;

        private void Awake()
        {
            structure = GetComponent<StructureSystem>();
            animator = GetComponentInChildren<Animator>();
            currentHealth = maxHealth;
        }

        private void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }

            structure.OnStructureBroken += HandleStructureBroken;
            structure.OnStructureRecovered += HandleStructureRecovered;
        }

        private void Update()
        {
            if (isDead || structure.IsBroken || playerTarget == null) return;

            float dist = Vector3.Distance(transform.position, playerTarget.position);
            if (dist <= attackRange && Time.time >= nextAttackTime)
            {
                AttackPlayer();
            }
        }

        private void AttackPlayer()
        {
            nextAttackTime = Time.time + attackCooldown;
            if (animator) animator.SetTrigger("Attack");

            // Comprobar si el jugador bloquea o hace parry
            if (playerTarget.TryGetComponent<Player.YariCombatController>(out var player))
            {
                if (player.TryParry())
                {
                    Debug.Log($"[Parry Exitoso] ¡Yari desvió el golpe de {characterName}! Estructura de {characterName} dañada.");
                    structure.AddStructureDamage(35f);
                    return;
                }

                if (player.IsGuarding)
                {
                    Debug.Log($"[Bloqueo] Yari bloqueó el golpe de {characterName}.");
                    if (player.TryGetComponent<StructureSystem>(out var pStruct))
                    {
                        pStruct.AddStructureDamage(structureDamageOnPlayer);
                    }
                    return;
                }

                // Golpe directo recibido por Yari
                Debug.Log($"[Impacto] {characterName} conectó un golpe directo a Yari.");
                player.PlayHitReaction(); // animación Impact_Hit en Yari
                // Si Yari cae a 0 de vida, el talismán resucita y envejece
                if (player.TryGetComponent<Core.IllaTalismanSystem>(out var talisman))
                {
                    // Lógica de daño
                }
            }
        }

        public void TakeHit(float healthDmg, float structDmg, Vector3 attackerPos)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - healthDmg);
            structure.AddStructureDamage(structDmg);

            if (animator) animator.SetTrigger("Hit");

            if (currentHealth <= 0f)
            {
                Defeat(killed: true);
            }
        }

        private void HandleStructureBroken()
        {
            if (animator) animator.SetBool("IsStunned", true);
            Debug.Log($"[VULNERABLE] ¡La postura de {characterName} está ROTA! Presiona [F] para Golpe Letal o [X] para Desarme y Perdón (Ayni).");
        }

        private void HandleStructureRecovered()
        {
            if (animator) animator.SetBool("IsStunned", false);
        }

        public void Defeat(bool killed)
        {
            isDead = true;
            if (animator) animator.SetTrigger(killed ? "Die" : "MercyKneel");
            Debug.Log($"[Resultado] {characterName} ha sido {(killed ? "ejecutado (Venganza)" : "purificado y desarmado (Ayni)")}.");
        }
    }
}
