#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Ayni.Enemy;

namespace Ayni.Editor
{
    /// <summary>
    /// Sondas de movimiento y combate para probar en Play desde el puente de agentes (AyniAgentBridge):
    ///   call Ayni.Editor.AyniLocomotionProbe.MeasureSlide 3     mide cuánto patinan los pies durante 3 s
    ///   call Ayni.Editor.AyniLocomotionProbe.TraceStates 4      registra los cambios de estado del Animator durante 4 s
    ///   call Ayni.Editor.AyniLocomotionProbe.RemoveEnemies      aparta a los rivales para probar sin que ataquen
    ///   call Ayni.Editor.AyniLocomotionProbe.SetYariField campo valor   cambia un ajuste de Yari durante el Play
    ///   call Ayni.Editor.AyniLocomotionProbe.ReloadSceneFromDisk        reabre la escena guardada (fuera de Play)
    /// </summary>
    public static class AyniLocomotionProbe
    {
        private static readonly string[] KnownStates =
        {
            "Relaxed_Locomotion", "Combat_Locomotion", "Crouch_Locomotion", "LockOn_Locomotion", "Guard_Stance",
            "Fall_Loop", "Fall_Back", "Land_Hard", "Jump", "GetUp", "Defeat_Death",
            "Atk_Light1", "Atk_Light2", "Atk_Light3", "Atk_Light4", "Atk_Overhand", "Atk_Uppercut", "Atk_Elbow",
            "Atk_Headbutt", "Atk_FrontKick"
        };

        // ───────────────────────── Patinaje de los pies ─────────────────────────

        private static Animator slideAnimator;
        private static Transform slideRoot, leftFoot, rightFoot;
        private static float slideEnd, slideStart;
        private static Vector3 lastLeftLocal, lastRightLocal, startPosition;
        private static int lastFrame;
        private static readonly System.Collections.Generic.List<float> backSpeeds = new System.Collections.Generic.List<float>();

        /// <summary>
        /// Compara la velocidad a la que avanza Yari con la que "pisan" sus pies. Mientras un pie apoya, se mueve
        /// hacia atrás respecto al cuerpo justo a la velocidad que la animación cubre sobre el suelo; si esa velocidad
        /// no coincide con la del cuerpo, el pie patina. No depende de la pendiente del terreno.
        /// </summary>
        public static void MeasureSlide(float seconds)
        {
            if (!Application.isPlaying) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            slideAnimator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (slideAnimator == null || !slideAnimator.isHuman) return;

            slideRoot = player.transform;
            leftFoot = slideAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = slideAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            lastLeftLocal = slideRoot.InverseTransformPoint(leftFoot.position);
            lastRightLocal = slideRoot.InverseTransformPoint(rightFoot.position);
            startPosition = slideRoot.position;
            slideStart = Time.time;
            slideEnd = Time.time + seconds;
            backSpeeds.Clear();
            lastFrame = Time.frameCount;
            EditorApplication.update -= SlideTick;
            EditorApplication.update += SlideTick;
        }

        private static void SlideTick()
        {
            if (!Application.isPlaying || slideRoot == null)
            {
                EditorApplication.update -= SlideTick;
                return;
            }
            if (Time.frameCount == lastFrame || Time.deltaTime <= 0f) return;
            lastFrame = Time.frameCount;

            // Velocidad de cada pie hacia atrás, vista desde el cuerpo
            Vector3 l = slideRoot.InverseTransformPoint(leftFoot.position);
            Vector3 r = slideRoot.InverseTransformPoint(rightFoot.position);
            float leftBack = -(l.z - lastLeftLocal.z) / Time.deltaTime;
            float rightBack = -(r.z - lastRightLocal.z) / Time.deltaTime;
            if (leftBack > 0.15f) backSpeeds.Add(leftBack);
            if (rightBack > 0.15f) backSpeeds.Add(rightBack);
            lastLeftLocal = l;
            lastRightLocal = r;

            if (Time.time < slideEnd) return;
            EditorApplication.update -= SlideTick;

            float elapsed = Mathf.Max(0.01f, Time.time - slideStart);
            Vector3 travelled = slideRoot.position - startPosition;
            travelled.y = 0f;
            float bodySpeed = travelled.magnitude / elapsed;

            float covered = 0f;
            if (backSpeeds.Count > 4)
            {
                backSpeeds.Sort();
                covered = backSpeeds[backSpeeds.Count / 2]; // mediana: la velocidad del pie mientras apoya
            }
            float slide = Mathf.Abs(bodySpeed - covered);
            Debug.Log($"[Ayni Paso] Yari avanza a {bodySpeed:F2} m/s · los pies pisan a {covered:F2} m/s · patinan {slide:F2} m/s " +
                      $"({(bodySpeed > 0.05f ? slide / bodySpeed * 100f : 0f):F0} %) · velocidad del Animator {slideAnimator.speed:F2}");
        }

        // ───────────────────────── Traza de estados ─────────────────────────

        private static Animator traceAnimator;
        private static float traceEnd, traceStart;
        private static string traceLast;
        private static StringBuilder trace;

        public static void TraceStates(float seconds)
        {
            if (!Application.isPlaying) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            traceAnimator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (traceAnimator == null) return;

            traceStart = Time.time;
            traceEnd = Time.time + seconds;
            traceLast = null;
            trace = new StringBuilder();
            EditorApplication.update -= TraceTick;
            EditorApplication.update += TraceTick;
        }

        private static void TraceTick()
        {
            if (!Application.isPlaying || traceAnimator == null)
            {
                EditorApplication.update -= TraceTick;
                return;
            }

            AnimatorStateInfo info = traceAnimator.GetCurrentAnimatorStateInfo(0);
            string name = "?";
            foreach (string s in KnownStates)
            {
                if (info.IsName(s)) { name = s; break; }
            }
            if (name != traceLast)
            {
                trace.Append($"\n   {Time.time - traceStart:F2} s  {name}");
                traceLast = name;
            }

            if (Time.time < traceEnd) return;
            EditorApplication.update -= TraceTick;
            Debug.Log("[Ayni Traza] estados del Animator:" + trace);
        }

        // ───────────────────────── Utilidades ─────────────────────────

        /// <summary>Cambia un número privado de YariCombatController durante el Play (para afinar sin recompilar).</summary>
        public static void SetYariField(string field, float value)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var yari = player != null ? player.GetComponent<Ayni.Player.YariCombatController>() : null;
            if (yari == null) return;
            var info = typeof(Ayni.Player.YariCombatController).GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (info == null || info.FieldType != typeof(float))
            {
                Debug.LogWarning("[Ayni Prueba] No existe el campo numérico " + field);
                return;
            }
            info.SetValue(yari, value);
        }

        /// <summary>
        /// Vuelve a abrir la escena tal como está guardada, descartando lo que el editor tenga en memoria.
        /// Hace falta tras cambiar en el código el valor inicial de un ajuste nuevo: la escena abierta se queda con el anterior.
        /// </summary>
        public static void ReloadSceneFromDisk()
        {
            if (Application.isPlaying) return;
            string path = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(path))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
        }

        public static void RemoveEnemies()
        {
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                enemy.gameObject.SetActive(false);
            }
        }
    }
}
#endif
