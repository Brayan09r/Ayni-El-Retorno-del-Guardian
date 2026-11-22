using System;
using UnityEngine;

namespace Ayni.World
{
    /// <summary>
    /// Lo que Yari consiguió (o dejó escapar) en el camino hasta Amaru y que cambia el duelo:
    ///  - Quipu del chasqui (K1): si atrapa al Rastreador que huye con él, aprende a leer los dardos de Amaru.
    ///    Si el Rastreador escapa, Amaru queda avisado y siembra trampas en su plaza.
    ///  - Chumpi de las tejedoras (K2): si las saca de la casa en llamas a tiempo, el veneno le dura la mitad.
    ///  - Capitán del Tambo (K3), mini Juicio: perdonado, se lleva a un Cazador de la Portada (uno menos); ejecutado,
    ///    Yari se queda su champi de bronce (más daño contra Amaru), pero la apacheta no acepta su ofrenda.
    ///  - Apacheta del camino: dejar una piedra trae la bendición del Apu (la próxima caída no cobra años).
    /// Lo escriben AyniQuipuChase y AyniWeaversFire; lo leen el duelo, el HUD y la tarjeta final.
    /// Los animales liberados de la Portada del Cazador están en <see cref="AyniCaptiveCages"/>.
    /// </summary>
    public static class AyniPathRewards
    {
        public enum Reward { Quipu, AmaruWarned, Chumpi, WeaversLost, CaptainSpared, CaptainSlain, ApuBlessing, ApuRejected }

        /// <summary>Cambió algo (para los avisos del HUD).</summary>
        public static event Action<Reward> OnChanged;

        public static bool QuipuRecovered { get; private set; }
        public static bool AmaruWarned { get; private set; }
        public static bool ChumpiReceived { get; private set; }
        public static bool WeaversLost { get; private set; }
        public static bool CaptainSpared { get; private set; }
        public static bool CaptainSlain { get; private set; }
        public static bool ApuBlessing { get; private set; }
        public static bool ApuRejected { get; private set; }
        /// <summary>Daño extra contra Amaru con la champi del Capitán del Tambo.</summary>
        public const float ChampiDamageBonus = 1.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnChanged = null;
            QuipuRecovered = AmaruWarned = ChumpiReceived = WeaversLost = false;
            CaptainSpared = CaptainSlain = ApuBlessing = ApuRejected = false;
        }

        public static void Grant(Reward reward)
        {
            switch (reward)
            {
                case Reward.Quipu: if (QuipuRecovered) return; QuipuRecovered = true; break;
                case Reward.AmaruWarned: if (AmaruWarned) return; AmaruWarned = true; break;
                case Reward.Chumpi: if (ChumpiReceived) return; ChumpiReceived = true; break;
                case Reward.WeaversLost: if (WeaversLost) return; WeaversLost = true; break;
                case Reward.CaptainSpared: if (CaptainSpared || CaptainSlain) return; CaptainSpared = true; break;
                case Reward.CaptainSlain: if (CaptainSpared || CaptainSlain) return; CaptainSlain = true; break;
                case Reward.ApuBlessing: if (ApuBlessing || ApuRejected) return; ApuBlessing = true; break;
                case Reward.ApuRejected: if (ApuBlessing || ApuRejected) return; ApuRejected = true; break;
            }
            Debug.Log($"[Ayni Recompensas] {reward}");
            OnChanged?.Invoke(reward);
        }
    }
}
