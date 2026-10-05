using System;
using UnityEngine;

namespace Ayni.Core
{
    /// <summary>
    /// Gestiona la mecánica central del Talismán (Illa Sagrada de Piedra):
    /// Envejecimiento al morir, aumento de daño por edad, y reducción de vitalidad y velocidad (Estilo Sifu).
    /// </summary>
    public class IllaTalismanSystem : MonoBehaviour
    {
        [Header("Configuración de Edad")]
        [SerializeField] private int startingAge = 20;
        [SerializeField] private int maxAge = 75;
        [SerializeField] private int deathCounter = 0; // Incrementa los años sumados por cada muerte consecutiva

        public int CurrentAge { get; private set; }
        public int DeathCounter => deathCounter;

        public event Action<int> OnAgeChanged;
        public event Action OnTrueDeath;

        public enum AgeStage
        {
            Youth,    // 20 a 35 años: Alta velocidad, esquivas ágiles, gran vida base
            Prime,    // 36 a 55 años: Fuerza equilibrada, técnicas intermedias de Rumi Maki
            Elder     // 56 a 75 años: Daño masivo de impacto y contraataques letales, vida muy reducida
        }

        private void Awake()
        {
            CurrentAge = startingAge;
        }

        public AgeStage GetCurrentStage()
        {
            if (CurrentAge <= 35) return AgeStage.Youth;
            if (CurrentAge <= 55) return AgeStage.Prime;
            return AgeStage.Elder;
        }

        /// <summary>
        /// Resucita al jugador consumiendo años de vida según la mecánica de la Illa Sagrada.
        /// Retorna true si resucitó con éxito o false si superó la edad máxima.
        /// </summary>
        public bool TriggerResurrection()
        {
            deathCounter++;
            int yearsLost = deathCounter;
            CurrentAge += yearsLost;

            Debug.Log($"[Illa Sagrada] Yari resucitó. Nueva edad: {CurrentAge} años (+{yearsLost}). Contador de muertes: {deathCounter}");
            OnAgeChanged?.Invoke(CurrentAge);

            if (CurrentAge >= maxAge)
            {
                Debug.Log("[Illa Sagrada] La energía vital de Yari se ha agotado. Muerte definitiva.");
                OnTrueDeath?.Invoke();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Derrotar a enemigos de élite o purificar a un jefe con Ayni reduce el contador de muertes.
        /// </summary>
        public void DecreaseDeathCounter()
        {
            if (deathCounter > 0)
            {
                deathCounter--;
                Debug.Log($"[Illa Sagrada] El Ayni purifica tu karma. Contador de muertes reducido a: {deathCounter}");
            }
        }

        public float GetDamageMultiplier()
        {
            return GetCurrentStage() switch
            {
                AgeStage.Youth => 1.0f,
                AgeStage.Prime => 1.25f,
                AgeStage.Elder => 1.60f, // Gran pegada ancestral
                _ => 1.0f
            };
        }

        public float GetSpeedMultiplier()
        {
            return GetCurrentStage() switch
            {
                AgeStage.Youth => 1.15f,
                AgeStage.Prime => 1.0f,
                AgeStage.Elder => 0.85f,
                _ => 1.0f
            };
        }

        public float GetMaxHealthMultiplier()
        {
            return GetCurrentStage() switch
            {
                AgeStage.Youth => 1.0f,
                AgeStage.Prime => 0.85f,
                AgeStage.Elder => 0.60f, // Barra de vida reducida
                _ => 1.0f
            };
        }
    }
}
