#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Ayni.Enemy;

namespace Ayni.Editor
{
    /// <summary>
    /// Prueba automática del puente colgante: en Play, aparta a los rivales, coloca a Yari en la entrada oeste
    /// y lo hace caminar hasta el otro lado con su propio CharacterController. Escribe el resultado en la consola.
    /// Solo sirve para comprobar la colisión; no cambia nada de la escena guardada.
    /// </summary>
    public static class AyniEnvironmentDebug
    {
        private static readonly Vector3 Start = new Vector3(634.5f, 0f, 466.5f);
        private static readonly Vector3 End = new Vector3(668f, 0f, 466.5f);
        private const float WalkSpeed = 3.5f;

        private static CharacterController walker;
        private static double startTime;
        private static double nextLog;
        private static float lowestY;

        [MenuItem("Ayni/Debug/Probar Cruce del Puente (en Play)")]
        public static void TestBridge()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Ayni Puente] Entra en Play antes de lanzar la prueba.");
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            walker = player != null ? player.GetComponent<CharacterController>() : null;
            Terrain terrain = Terrain.activeTerrain;
            if (walker == null || terrain == null)
            {
                Debug.LogWarning("[Ayni Puente] No se encontró a Yari o el terreno.");
                return;
            }

            // Si el tutorial sigue abierto, el juego está en pausa: se cierra para que la prueba avance
            var tutorial = Object.FindFirstObjectByType<Ayni.UI.AyniTutorial>();
            if (tutorial != null) Object.Destroy(tutorial);

            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                enemy.gameObject.SetActive(false);
            }

            Vector3 p = Start;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.2f;
            walker.enabled = false;
            walker.transform.position = p;
            walker.enabled = true;

            startTime = EditorApplication.timeSinceStartup;
            nextLog = startTime;
            lowestY = p.y;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
            Debug.Log($"[Ayni Puente] Prueba iniciada en {p}.");
        }

        private static void Step()
        {
            if (!EditorApplication.isPlaying || walker == null)
            {
                EditorApplication.update -= Step;
                return;
            }

            Vector3 pos = walker.transform.position;
            lowestY = Mathf.Min(lowestY, pos.y);
            double now = EditorApplication.timeSinceStartup;

            if (now >= nextLog)
            {
                nextLog = now + 1.0;
                Debug.Log($"[Ayni Puente] t={now - startTime:F1}s  posición {pos}");
            }

            bool arrived = pos.x >= End.x;
            bool fell = pos.y < -5f;
            bool timeout = now - startTime > 20.0;
            if (arrived || fell || timeout)
            {
                EditorApplication.update -= Step;
                string result = arrived ? "<color=green>CRUZÓ</color>" : fell ? "<color=red>CAYÓ</color>" : "<color=orange>SE ATASCÓ</color>";
                Debug.Log($"[Ayni Puente] Resultado: {result}. Posición final {pos}, altura mínima {lowestY:F2}.");
                return;
            }

            // Empuja a Yari hacia el este manteniéndolo en el eje del puente; la gravedad la pone su propio script
            Vector3 step = Vector3.right * (WalkSpeed * Time.deltaTime);
            step.z = Mathf.Clamp(End.z - pos.z, -0.05f, 0.05f);
            step.y = -0.05f;
            walker.Move(step);
        }
    }
}
#endif
