using System;
using UnityEngine;

namespace Ayni.Combat
{
    /// <summary>
    /// Sistema de Estructura / Postura (Mecánica idéntica a Sifu y Sekiro).
    /// Si la barra de estructura se llena al 100%, el personaje queda aturdido y vulnerable a ejecuciones o al perdón del Ayni.
    /// </summary>
    public class StructureSystem : MonoBehaviour
    {
        [Header("Parámetros de Estructura")]
        [SerializeField] private float maxStructure = 100f;
        [SerializeField] private float currentStructure = 0f;
        [SerializeField] private float recoveryRate = 18f; // Tasa de recuperación de estructura por segundo
        [SerializeField] private float recoveryDelay = 1.2f;
        [SerializeField] private float brokenStunDuration = 4.0f; // Tiempo de aturdimiento antes de recuperar postura

        private float lastDamageTime;
        private float breakTime;
        public bool IsBroken { get; private set; }

        public float CurrentStructure => currentStructure;
        public float MaxStructure => maxStructure;
        public float StructureRatio => maxStructure > 0f ? currentStructure / maxStructure : 0f;

        public event Action OnStructureBroken;
        public event Action OnStructureRecovered;
        public event Action<float, float> OnStructureChanged;

        private void Update()
        {
            if (IsBroken)
            {
                // Auto-recuperación si expira el tiempo de aturdimiento sin que haya ejecución
                if (Time.time - breakTime >= brokenStunDuration)
                {
                    ResetStructure();
                }
                return;
            }

            if (Time.time - lastDamageTime >= recoveryDelay && currentStructure > 0f)
            {
                currentStructure = Mathf.Max(0f, currentStructure - recoveryRate * Time.deltaTime);
                OnStructureChanged?.Invoke(currentStructure, maxStructure);
            }
        }

        /// <summary>
        /// Aplica daño a la barra de estructura (al bloquear golpes o recibir impactos fuertes).
        /// </summary>
        public void AddStructureDamage(float amount)
        {
            if (IsBroken) return;

            currentStructure += amount;
            lastDamageTime = Time.time;
            OnStructureChanged?.Invoke(currentStructure, maxStructure);

            if (currentStructure >= maxStructure)
            {
                BreakStructure();
            }
        }

        private void BreakStructure()
        {
            currentStructure = maxStructure;
            IsBroken = true;
            breakTime = Time.time;
            Debug.Log($"[StructureSystem] ¡Estructura Rota en {gameObject.name}! Estado de aturdimiento.");
            OnStructureBroken?.Invoke();
            OnStructureChanged?.Invoke(currentStructure, maxStructure);
        }

        public void ResetStructure()
        {
            currentStructure = 0f;
            IsBroken = false;
            OnStructureRecovered?.Invoke();
            OnStructureChanged?.Invoke(currentStructure, maxStructure);
        }
    }
}
