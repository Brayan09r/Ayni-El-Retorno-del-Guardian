using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Ayni.Core
{
    /// <summary>
    /// Vibración del mando de Xbox. El proyecto usa el Input Manager clásico, que no sabe hacer vibrar el mando,
    /// así que se habla directamente con XInput (Windows). En otras plataformas (la build web, por ejemplo) no hace nada.
    ///
    /// Cada pulso enciende los dos motores durante unos segundos reales (no le afecta la micro-pausa de los golpes):
    ///   motor grave (izquierdo)  contundencia: golpes fuertes, remates, golpes recibidos
    ///   motor agudo (derecho)    chasquido: golpes ligeros, desvíos, esquivas
    /// Solo vibra mientras se juega con el mando; con el teclado no se envía nada.
    /// </summary>
    public class AyniRumble : MonoBehaviour
    {
        /// <summary>Multiplicador global (0 = sin vibración, 1 = normal).</summary>
        public static float Strength = 1f;

        private static AyniRumble instance;
        private static bool unavailable;

        private float lowLevel, highLevel;
        private float lowUntil, highUntil;
        private float lowFade, highFade;
        private float sentLow = -1f, sentHigh = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            unavailable = false;
        }

        /// <summary>
        /// Enciende los motores (0-1) durante los segundos indicados; al final se apagan con un breve desvanecido.
        /// Si ya hay un pulso más fuerte en curso, se conserva.
        /// </summary>
        public static void Pulse(float low, float high, float seconds)
        {
            if (unavailable || Strength <= 0f || !AyniInput.UsingGamepad) return;
            AyniRumble r = Get();
            float now = Time.unscaledTime;
            if (low > r.CurrentLow(now))
            {
                r.lowLevel = Mathf.Clamp01(low);
                r.lowUntil = now + seconds;
                r.lowFade = seconds * 0.4f;
            }
            if (high > r.CurrentHigh(now))
            {
                r.highLevel = Mathf.Clamp01(high);
                r.highUntil = now + seconds;
                r.highFade = seconds * 0.4f;
            }
        }

        /// <summary>Apaga los motores al momento (pausa, escenas, fin del juego).</summary>
        public static void Stop()
        {
            if (instance == null) return;
            instance.lowUntil = instance.highUntil = 0f;
            instance.Send(0f, 0f);
        }

        /// <summary>
        /// Prueba desde el puente de agentes: vibra medio segundo y anota qué mandos responden.
        ///   call Ayni.Core.AyniRumble.Test
        /// </summary>
        public static string Test()
        {
            if (unavailable) return "XInput no disponible";
            string report = "";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                for (uint i = 0; i < 4; i++)
                {
                    var v = new XInputVibration { left = 32000, right = 32000 };
                    uint result = XInputSetState(i, ref v);
                    report += $" mando {i}: {(result == 0 ? "conectado" : "no hay")}";
                }
            }
            catch (Exception e)
            {
                unavailable = true;
                return "XInput no disponible: " + e.GetType().Name;
            }
            if (Application.isPlaying)
            {
                // En Play vibra medio segundo; fuera de Play no hay Update que lo apague, así que se apaga ya
                AyniRumble r = Get();
                r.lowLevel = r.highLevel = 0.5f;
                r.lowUntil = r.highUntil = Time.unscaledTime + 0.5f;
                r.lowFade = r.highFade = 0.2f;
            }
            else
            {
                var off = new XInputVibration();
                for (uint i = 0; i < 4; i++) XInputSetState(i, ref off);
            }
#else
            report = " esta plataforma no tiene XInput";
#endif
            Debug.Log("[Ayni Vibración]" + report);
            return report;
        }

        private static AyniRumble Get()
        {
            if (instance == null)
            {
                var go = new GameObject("Ayni_Rumble");
                if (Application.isPlaying) DontDestroyOnLoad(go);
                instance = go.AddComponent<AyniRumble>();
            }
            return instance;
        }

        private float CurrentLow(float now) => Level(lowLevel, lowUntil, lowFade, now);
        private float CurrentHigh(float now) => Level(highLevel, highUntil, highFade, now);

        private static float Level(float level, float until, float fade, float now)
        {
            if (now >= until) return 0f;
            float left = until - now;
            return fade > 0f && left < fade ? level * left / fade : level;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            // En pausa, en el tutorial o en las escenas no vibra
            bool allowed = !AyniGameState.InputLocked && AyniInput.UsingGamepad && Application.isFocused;
            float low = allowed ? CurrentLow(now) * Strength : 0f;
            float high = allowed ? CurrentHigh(now) * Strength : 0f;
            if (Mathf.Abs(low - sentLow) > 0.02f || Mathf.Abs(high - sentHigh) > 0.02f || (low == 0f && sentLow > 0f) || (high == 0f && sentHigh > 0f))
            {
                Send(low, high);
            }
        }

        private void Send(float low, float high)
        {
            sentLow = low;
            sentHigh = high;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (unavailable) return;
            try
            {
                var v = new XInputVibration
                {
                    left = (ushort)(Mathf.Clamp01(low) * 65535f),
                    right = (ushort)(Mathf.Clamp01(high) * 65535f)
                };
                // Se envía a los cuatro puestos: el que no tiene mando devuelve un error y no pasa nada
                for (uint i = 0; i < 4; i++) XInputSetState(i, ref v);
            }
            catch (Exception)
            {
                unavailable = true; // sin xinput1_4.dll (Windows muy antiguo): se desactiva y ya
            }
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Send(0f, 0f);
        }

        private void OnDisable()
        {
            Send(0f, 0f);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        private struct XInputVibration
        {
            public ushort left;
            public ushort right;
        }

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputSetState(uint userIndex, ref XInputVibration vibration);
#endif
    }
}
