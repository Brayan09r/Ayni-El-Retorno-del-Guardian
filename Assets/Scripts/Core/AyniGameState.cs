using UnityEngine;

namespace Ayni.Core
{
    /// <summary>
    /// Estado global mínimo del juego. Sirve para que el tutorial (u otra pausa) bloquee la entrada
    /// del jugador y de la cámara sin que esos scripts tengan que conocer la interfaz.
    /// </summary>
    public static class AyniGameState
    {
        private static bool locked;
        private static int releaseFrame = -1;

        /// <summary>
        /// True mientras la entrada está bloqueada. Sigue siendo true durante el fotograma en que se libera,
        /// para que la tecla o el clic que cierra el tutorial no dispare también un golpe.
        /// </summary>
        public static bool InputLocked => locked || Time.frameCount <= releaseFrame;

        public static void LockInput()
        {
            locked = true;
        }

        public static void UnlockInput()
        {
            locked = false;
            releaseFrame = Time.frameCount + 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            locked = false;
            releaseFrame = -1;
        }
    }
}
