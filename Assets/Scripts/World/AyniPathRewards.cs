using System;
using UnityEngine;

namespace Ayni.World
{
    /// <summary>
    /// Lo que Yari consiguió (o dejó escapar) en el camino hasta Amaru y que cambia el duelo:
    ///  - Quipu del chasqui (K1): si atrapa al Rastreador que huye con él, aprende a leer los dardos de Amaru.
    ///    Si el Rastreador escapa, Amaru queda avisado y siembra trampas en su plaza.
    ///  - Chumpi de las tejedoras (K2): si las saca de la casa en llamas a tiempo, el veneno le dura la mitad.
    /// Lo escriben AyniQuipuChase y AyniWeaversFire; lo leen el duelo, el HUD y la tarjeta final.
    /// Los animales liberados de la Portada del Cazador están en <see cref="AyniCaptiveCages"/>.
    /// </summary>
    public static class AyniPathRewards
    {
        public enum Reward { Quipu, AmaruWarned, Chumpi, WeaversLost }

        /// <summary>Cambió algo (para los avisos del HUD).</summary>
        public static event Action<Reward> OnChanged;

        public static bool QuipuRecovered { get; private set; }
        public static bool AmaruWarned { get; private set; }
        public static bool ChumpiReceived { get; private set; }
        public static bool WeaversLost { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnChanged = null;
            QuipuRecovered = AmaruWarned = ChumpiReceived = WeaversLost = false;
        }

        public static void Grant(Reward reward)
        {
            switch (reward)
            {
                case Reward.Quipu: if (QuipuRecovered) return; QuipuRecovered = true; break;
                case Reward.AmaruWarned: if (AmaruWarned) return; AmaruWarned = true; break;
                case Reward.Chumpi: if (ChumpiReceived) return; ChumpiReceived = true; break;
                case Reward.WeaversLost: if (WeaversLost) return; WeaversLost = true; break;
            }
            Debug.Log($"[Ayni Recompensas] {reward}");
            OnChanged?.Invoke(reward);
        }
    }
}
