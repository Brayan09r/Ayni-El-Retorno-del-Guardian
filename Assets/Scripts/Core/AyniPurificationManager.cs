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
        [SerializeField] private float executionRange = 4.5f;

        public int EnemiesKilled => enemiesKilled;
        public int EnemiesSpared => enemiesSpared;
        public float ExecutionRange
        {
            get => executionRange >= 4.5f ? executionRange : (executionRange = 4.5f);
            set => executionRange = value;
        }

        public event Action<EnemyController, bool> OnCombatResolved;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            if (executionRange < 4.5f) executionRange = 4.5f;
        }

        public bool TriggerExecutionAction(Vector3 playerPos, bool isAyniMercy)
        {
            return TriggerExecutionAction(playerPos, isAyniMercy, out _);
        }

        public bool TriggerExecutionAction(Vector3 playerPos, bool isAyniMercy, out EnemyController resolvedEnemy)
        {
            resolvedEnemy = null;
            EnemyController bestEnemy = null;
            float closestDist = float.MaxValue;
            float maxRange = ExecutionRange;

            // Buscar el enemigo con postura rota más cercano en el radio de ejecución
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead && enemy.Structure != null && enemy.Structure.IsBroken)
                {
                    float dist = Vector3.Distance(playerPos, enemy.transform.position);
                    if (dist <= maxRange && dist < closestDist)
                    {
                        bestEnemy = enemy;
                        closestDist = dist;
                    }
                }
            }

            if (bestEnemy != null)
            {
                resolvedEnemy = bestEnemy;
                ResolveDilemma(bestEnemy, isAyniMercy);
                return true;
            }

            return false;
        }

        private void ResolveDilemma(EnemyController enemy, bool isAyniMercy)
        {
            if (isAyniMercy)
            {
                enemiesSpared++;
                Debug.Log($"<color=cyan>[CAMINO DEL AYNI]</color> Has perdonado y desarmado a {enemy.CharacterName}. El equilibrio ecológico y social comienza a restaurarse.");
                enemy.Defeat(killed: false);

                // Reducir el contador de muerte del talismán como recompensa por restaurar el Ayni
                var talisman = FindFirstObjectByType<IllaTalismanSystem>();
                if (talisman != null)
                {
                    talisman.DecreaseDeathCounter();
                }
            }
            else
            {
                enemiesKilled++;
                Debug.Log($"<color=red>[CAMINO DE LA VENGANZA]</color> Has ejecutado a {enemy.CharacterName}. La violencia engendra más destrucción.");
                enemy.Defeat(killed: true);
            }

            OnCombatResolved?.Invoke(enemy, isAyniMercy);
        }

        public void ResetStats()
        {
            enemiesKilled = 0;
            enemiesSpared = 0;
        }
    }
}
