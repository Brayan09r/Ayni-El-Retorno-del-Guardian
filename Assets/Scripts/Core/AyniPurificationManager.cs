using System;
using UnityEngine;
using Ayni.Enemy;

namespace Ayni.Core
{
    /// <summary>
    /// Gestiona el Dilema Moral de Sifu adaptado al Ayni:
    /// Opción A: Golpe Letal (Venganza ciega)
    /// Opción B: Desarme y Perdón (Restauración del Ayni y los ODS)
    /// </summary>
    public class AyniPurificationManager : MonoBehaviour
    {
        public static AyniPurificationManager Instance { get; private set; }

        [Header("Estadísticas del Viaje")]
        [SerializeField] private int enemiesKilled = 0;
        [SerializeField] private int enemiesSpared = 0;
        [SerializeField] private float executionRange = 3.0f;
        [Tooltip("Distancia desde la que se puede resolver el Juicio Ayni de un jefe (Yari se acerca solo).")]
        [SerializeField] private float bossJudgmentRange = 9f;

        public int EnemiesKilled => enemiesKilled;
        public int EnemiesSpared => enemiesSpared;

        public event Action<EnemyController, bool> OnCombatResolved;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void TriggerExecutionAction(Vector3 playerPos, bool isAyniMercy)
        {
            // Buscar si hay algún enemigo cercano con la postura rota
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.CanBeJudged)
                {
                    float dist = Vector3.Distance(playerPos, enemy.transform.position);
                    if (dist <= (enemy.IsBoss ? bossJudgmentRange : executionRange))
                    {
                        ResolveDilemma(enemy, isAyniMercy);
                        return;
                    }
                }
            }
        }

        private void ResolveDilemma(EnemyController enemy, bool isAyniMercy)
        {
            if (isAyniMercy)
            {
                enemiesSpared++;
                Debug.Log($"<color=cyan>[CAMINO DEL AYNI]</color> Has perdonado y desarmado a {enemy.CharacterName}. El equilibrio ecológico y social comienza a restaurarse.");
                enemy.Defeat(killed: false, reactionDelay: 0.45f);

                // Reducir el contador de muerte del talismán como recompensa por restaurar el Ayni
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null && player.TryGetComponent<IllaTalismanSystem>(out var talisman))
                {
                    talisman.DecreaseDeathCounter();
                }
            }
            else
            {
                enemiesKilled++;
                Debug.Log($"<color=red>[CAMINO DE LA VENGANZA]</color> Has ejecutado a {enemy.CharacterName}. La violencia engendra más destrucción.");
                enemy.Defeat(killed: true, reactionDelay: 0.25f);
            }

            OnCombatResolved?.Invoke(enemy, isAyniMercy);
        }
    }
}
