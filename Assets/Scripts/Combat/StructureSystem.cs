using System;
using UnityEngine;

namespace Ayni.Combat
{
    /// <summary>
    /// Sistema de Estructura / Postura (Mecánica idéntica a Sifu y Sekiro).
    /// Si la barra de estructura se llena al 100%, el personaje queda aturdido y vulnerable a ejecuciones o al perdón del Ayni.
    /// Pasado <see cref="brokenDuration"/> la postura se recupera sola.
    /// </summary>
    public class StructureSystem : MonoBehaviour
    {
        [Header("Parámetros de Estructura")]
        [SerializeField] private float maxStructure = 100f;
        [SerializeField] private float currentStructure = 0f;
        [SerializeField] private float recoveryRate = 18f; // Tasa de recuperación de estructura por segundo
        [SerializeField] private float recoveryDelay = 1.2f;
        [Tooltip("Segundos que dura la postura rota antes de recuperarse sola. 0 = no se recupera hasta llamar a ResetStructure().")]
        [SerializeField] private float brokenDuration = 4f;

        private float lastDamageTime;
        private float brokenTime;
        public bool IsBroken { get; private set; }

        public float CurrentStructure => currentStructure;
        public float MaxStructure => maxStructure;
        public float StructureRatio => maxStructure > 0f ? currentStructure / maxStructure : 0f;

        /// <summary>Segundos que quedan de aturdimiento (0 si no está rota o si no se recupera sola).</summary>
        public float BrokenTimeRemaining =>
            IsBroken && brokenDuration > 0f ? Mathf.Max(0f, brokenDuration - (Time.time - brokenTime)) : 0f;

        public event Action OnStructureBroken;
        public event Action OnStructureRecovered;
        public event Action<float, float> OnStructureChanged;

        private void Update()
        {
            if (IsBroken)
            {
                if (brokenDuration > 0f && Time.time - brokenTime >= brokenDuration)
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
            brokenTime = Time.time;
            Debug.Log($"[StructureSystem] ¡Estructura Rota en {gameObject.name}! Estado de aturdimiento.");
            OnStructureBroken?.Invoke();
            OnStructureChanged?.Invoke(currentStructure, maxStructure);
        }

        public void ResetStructure()
        {
            bool wasBroken = IsBroken;
            currentStructure = 0f;
            IsBroken = false;
            lastDamageTime = Time.time;
            if (wasBroken) OnStructureRecovered?.Invoke();
            OnStructureChanged?.Invoke(currentStructure, maxStructure);
        }
    }
}
