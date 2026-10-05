using UnityEngine;
using UnityEngine.SceneManagement;

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

        /// <summary>
        /// True mientras se reproduce una escena cinemática (el prólogo): los rivales se quedan quietos
        /// y la red de seguridad de las quebradas no rescata a nadie.
        /// </summary>
        public static bool CinematicPlaying { get; set; }

        public static void LockInput()
        {
            locked = true;
        }

        public static void UnlockInput()
        {
            locked = false;
            releaseFrame = Time.frameCount + 1;
        }

        /// <summary>Vuelve a cargar el nivel actual (reintentar tras un Game Over o volver a jugar el nivel).</summary>
        public static void ReloadLevel()
        {
            // El estado estático sobrevive al cambio de escena: se deja limpio para el nivel nuevo
            Time.timeScale = 1f;
            locked = false;
            releaseFrame = -1;
            CinematicPlaying = false;
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // En el Editor la escena puede no estar en Build Settings
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            locked = false;
            releaseFrame = -1;
            CinematicPlaying = false;
        }
    }
}
