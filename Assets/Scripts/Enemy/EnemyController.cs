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
        public StructureSystem Structure => structure != null ? structure : (structure = GetComponent<StructureSystem>());

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (structure != null)
            {
                structure.OnStructureBroken -= HandleStructureBroken;
                structure.OnStructureRecovered -= HandleStructureRecovered;
                structure.OnStructureBroken += HandleStructureBroken;
                structure.OnStructureRecovered += HandleStructureRecovered;
            }
        }

        private void OnDisable()
        {
            if (structure != null)
            {
                structure.OnStructureBroken -= HandleStructureBroken;
                structure.OnStructureRecovered -= HandleStructureRecovered;
            }
        }

        public void EnsureReferences()
        {
            if (structure == null) structure = GetComponent<StructureSystem>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (currentHealth <= 0f) currentHealth = maxHealth;
        }

        public void FindPlayerTarget()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("Yari_Hero");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }
        }

        private void Start()
        {
            EnsureReferences();
            FindPlayerTarget();
        }

        private void OnDestroy()
        {
            if (structure != null)
            {
                structure.OnStructureBroken -= HandleStructureBroken;
                structure.OnStructureRecovered -= HandleStructureRecovered;
            }
        }

        private void Update()
        {
            if (isDead || structure == null || structure.IsBroken) return;

            if (playerTarget == null)
            {
                FindPlayerTarget();
                if (playerTarget == null) return;
            }

            // No atacar a Yari si ha sido derrotado definitivamente
            if (playerTarget.TryGetComponent<Player.YariCombatController>(out var playerCtrl) && (playerCtrl.IsDead || !playerCtrl.enabled))
            {
                return;
            }

            float dist = Vector3.Distance(transform.position, playerTarget.position);
            if (dist <= attackRange && Time.time >= nextAttackTime)
            {
                AttackPlayer();
            }
        }

        private void AttackPlayer()
        {
            if (playerTarget == null) return;

            // Comprobar si el jugador bloquea o hace parry
            if (playerTarget.TryGetComponent<Player.YariCombatController>(out var player))
            {
                if (player.IsDead || !player.enabled) return;

                nextAttackTime = Time.time + attackCooldown;
                if (animator) animator.SetTrigger("Attack");
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
                player.TakeDamage(attackDamage, structureDamageOnPlayer * 0.5f);
            }
        }

        public void TakeHit(float healthDmg, float structDmg, Vector3 attackerPos)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - healthDmg);
            if (structure != null) structure.AddStructureDamage(structDmg);

            if (animator) animator.SetTrigger("Hit");

            if (currentHealth <= 0f)
            {
                Defeat(killed: true);
            }
        }

        private void HandleStructureBroken()
        {
            if (isDead) return;
            if (animator) animator.SetBool("IsStunned", true);
            Debug.Log($"[VULNERABLE] ¡La postura de {characterName} está ROTA! Presiona [F] para Golpe Letal o [X] para Desarme y Perdón (Ayni).");
        }

        private void HandleStructureRecovered()
        {
            if (isDead) return;
            if (animator) animator.SetBool("IsStunned", false);
        }

        public void Defeat(bool killed)
        {
            if (isDead) return;
            isDead = true;

            // Desactivar colisionador para permitir libre paso al jugador tras derrotarlo
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (structure != null)
            {
                structure.OnStructureBroken -= HandleStructureBroken;
                structure.OnStructureRecovered -= HandleStructureRecovered;
            }

            if (animator)
            {
                animator.SetBool("IsStunned", false);
                animator.SetTrigger(killed ? "Die" : "MercyKneel");
            }
            Debug.Log($"[Resultado] {characterName} ha sido {(killed ? "ejecutado (Venganza)" : "purificado y desarmado (Ayni)")}.");
        }

        public void ResetEnemy(Vector3? position = null)
        {
            isDead = false;
            currentHealth = maxHealth;
            if (position.HasValue) transform.position = position.Value;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
            if (structure != null)
            {
                structure.ResetStructure();
                structure.OnStructureBroken -= HandleStructureBroken;
                structure.OnStructureRecovered -= HandleStructureRecovered;
                structure.OnStructureBroken += HandleStructureBroken;
                structure.OnStructureRecovered += HandleStructureRecovered;
            }
            if (animator)
            {
                animator.SetBool("IsStunned", false);
            }
        }
    }
}
