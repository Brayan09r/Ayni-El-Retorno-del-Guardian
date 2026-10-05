using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ayni.Combat
{
    /// <summary>
    /// Tiempos de impacto medidos en los clips de ataque (los genera el menú
    /// Ayni > Herramientas > Medir Tiempos de Impacto de los Ataques y se guardan en
    /// Assets/Resources/YariAttackTimings.asset).
    /// </summary>
    public class AttackTimingTable : ScriptableObject
    {
        public const string ResourceName = "YariAttackTimings";

        [Serializable]
        public class Entry
        {
            public string clipName;
            [Tooltip("Duración del clip en segundos.")]
            public float length;
            [Tooltip("Segundo del clip en el que el golpe alcanza su máxima extensión. -1 = no se pudo medir.")]
            public float contactTime = -1f;
            [Tooltip("Hueso que golpea, detectado al medir.")]
            public string strikingBone;
        }

        [Serializable]
        public class LocomotionEntry
        {
            public string clipName;
            [Tooltip("Velocidad sobre el suelo (m/s) a la que el clip no patina, medida en los pies.")]
            public float groundSpeed;
        }

        [Header("Salto (medido en el clip Jump)")]
        [Tooltip("Segundo del clip en el que los pies despegan del suelo.")]
        public float jumpTakeoff;
        [Tooltip("Segundo del clip en el que los pies vuelven a tocar el suelo.")]
        public float jumpLand;
        public float jumpLength;

        [Header("Salto en carrera (medido en el clip Run_Jump)")]
        public float runJumpTakeoff;
        public float runJumpLand;
        public float runJumpLength;

        public List<Entry> entries = new List<Entry>();
        public List<LocomotionEntry> locomotion = new List<LocomotionEntry>();

        /// <summary>Nombre del hueso (HumanBodyBones) que golpea en ese clip, o null si no se midió.</summary>
        public string GetStrikingBone(string clipName)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].clipName == clipName) return entries[i].strikingBone;
            }
            return null;
        }

        /// <summary>
        /// Metros por segundo que cubren los pasos de un clip de locomoción a velocidad normal.
        /// Devuelve <paramref name="fallback"/> si el clip no está medido.
        /// </summary>
        public float GetGroundSpeed(string clipName, float fallback)
        {
            for (int i = 0; i < locomotion.Count; i++)
            {
                if (locomotion[i].clipName == clipName && locomotion[i].groundSpeed > 0.05f) return locomotion[i].groundSpeed;
            }
            return fallback;
        }

        public bool TryGetContact(string clipName, out float contactTime)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].clipName == clipName && entries[i].contactTime > 0f)
                {
                    contactTime = entries[i].contactTime;
                    return true;
                }
            }
            contactTime = -1f;
            return false;
        }
    }
}
