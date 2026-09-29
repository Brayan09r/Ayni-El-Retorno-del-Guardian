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
                if (!enemy.IsDead && enemy.Structure.IsBroken)
                {
                    float dist = Vector3.Distance(playerPos, enemy.transform.position);
                    if (dist <= executionRange)
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
                enemy.Defeat(killed: false);

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
                enemy.Defeat(killed: true);
            }

            OnCombatResolved?.Invoke(enemy, isAyniMercy);
        }
    }
}
