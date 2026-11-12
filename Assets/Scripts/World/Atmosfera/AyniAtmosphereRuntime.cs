using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ayni.World
{
    /// <summary>
    /// Instala automáticamente los efectos atmosféricos de clima (gradación de color, motas de luz, sombras de nubes)
    /// al cargar cualquier escena que tenga el objeto <c>Ayni_Entorno</c>. No hace falta tocar la escena:
    /// el menú principal y otras escenas sin entorno andino quedan intactas.
    /// Para desactivar todos los efectos sin borrar código, poner <see cref="Enabled"/> en false antes de cargar la escena.
    /// </summary>
    public static class AyniAtmosphereRuntime
    {
        public const string HostName = "Ayni_Atmosfera_Clima_Runtime";

        /// <summary>Interruptor global de los efectos atmosféricos de clima.</summary>
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoads()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Enabled) return;
            GameObject env = GameObject.Find("Ayni_Entorno");
            if (env == null) return;
            if (env.transform.Find(HostName) != null) return;

            var host = new GameObject(HostName);
            host.transform.SetParent(env.transform, false);
            host.AddComponent<AyniClimateGrade>();
            host.AddComponent<AyniLightMotes>();
        }
    }
}
