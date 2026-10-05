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
    ///   call Ayni.Editor.AyniLocomotionProbe.MeasureClips       (fuera de Play) lo que cubre cada clip de locomoción
    ///   call Ayni.Editor.AyniLocomotionProbe.MeasureJump 3      mide el próximo salto (largo, alto y tiempo en el aire)
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
            "Fall_Loop", "Fall_Back", "Land_Hard", "Jump", "Run_Jump", "GetUp", "Defeat_Death",
            "Atk_Light1", "Atk_Light2", "Atk_Light3", "Atk_Light4", "Atk_Overhand", "Atk_Uppercut", "Atk_Elbow",
            "Atk_Headbutt", "Atk_FrontKick"
        };

        // ───────────────────────── Patinaje de los pies ─────────────────────────

        private static Animator slideAnimator;
        private static Transform slideRoot, leftFoot, rightFoot;
        private static float slideEnd, slideStart;
        private static Vector3 lastLeftLocal, lastRightLocal, lastBodyPosition;
        private static float bodyPath;
        private static int lastFrame;
        // Por cada pie: altura respecto al cuerpo y velocidad hacia atrás en cada fotograma
        private static readonly System.Collections.Generic.List<Vector2> leftSamples = new System.Collections.Generic.List<Vector2>();
        private static readonly System.Collections.Generic.List<Vector2> rightSamples = new System.Collections.Generic.List<Vector2>();

        /// <summary>
        /// Compara la velocidad a la que avanza Yari con la que "pisan" sus pies. Mientras un pie apoya (está en la
        /// parte más baja de su recorrido) se mueve hacia atrás respecto al cuerpo justo a la velocidad que la
        /// animación cubre sobre el suelo; si no coincide con la del cuerpo, el pie patina. Es el mismo criterio que
        /// usa AyniAttackTimingBaker.MeasureGroundSpeed sobre el clip, pero medido en el juego, con mezclas y cadencia.
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
            lastBodyPosition = slideRoot.position;
            bodyPath = 0f;
            slideStart = Time.time;
            slideEnd = Time.time + seconds;
            leftSamples.Clear();
            rightSamples.Clear();
            lastFrame = Time.frameCount;
            // Una muestra por fotograma del juego, con la pose ya animada (EditorApplication.update no va al mismo ritmo)
            Application.onBeforeRender -= SlideTick;
            Application.onBeforeRender += SlideTick;
        }

        private static void SlideTick()
        {
            if (!Application.isPlaying || slideRoot == null)
            {
                Application.onBeforeRender -= SlideTick;
                return;
            }
            if (Time.frameCount == lastFrame || Time.deltaTime <= 0f) return;
            lastFrame = Time.frameCount;

            Vector3 l = slideRoot.InverseTransformPoint(leftFoot.position);
            Vector3 r = slideRoot.InverseTransformPoint(rightFoot.position);
            // Velocidad del pie respecto al cuerpo en el plano del suelo (vale para avanzar, retroceder y los laterales)
            leftSamples.Add(new Vector2(l.y, new Vector2(l.x - lastLeftLocal.x, l.z - lastLeftLocal.z).magnitude / Time.deltaTime));
            rightSamples.Add(new Vector2(r.y, new Vector2(r.x - lastRightLocal.x, r.z - lastRightLocal.z).magnitude / Time.deltaTime));
            lastLeftLocal = l;
            lastRightLocal = r;

            // Camino recorrido (no la distancia en línea recta: con un rival fijado Yari se mueve en arco)
            Vector3 step = slideRoot.position - lastBodyPosition;
            step.y = 0f;
            bodyPath += step.magnitude;
            lastBodyPosition = slideRoot.position;

            if (Time.time < slideEnd) return;
            Application.onBeforeRender -= SlideTick;

            float elapsed = Mathf.Max(0.01f, Time.time - slideStart);
            float bodySpeed = bodyPath / elapsed;

            var planted = new System.Collections.Generic.List<float>();
            CollectPlanted(leftSamples, planted);
            CollectPlanted(rightSamples, planted);
            float covered = 0f, low = 0f, high = 0f;
            if (planted.Count > 4)
            {
                planted.Sort();
                covered = planted[planted.Count / 2];
                low = planted[planted.Count / 4];
                high = planted[planted.Count * 3 / 4];
            }

            float slide = Mathf.Abs(bodySpeed - covered);
            Debug.Log($"[Ayni Paso] Yari avanza a {bodySpeed:F2} m/s · los pies pisan a {covered:F2} m/s · patinan {slide:F2} m/s " +
                      $"({(bodySpeed > 0.05f ? slide / bodySpeed * 100f : 0f):F0} %) · velocidad del Animator {slideAnimator.speed:F2} " +
                      $"· {planted.Count} muestras de apoyo de {leftSamples.Count * 2} (entre {low:F2} y {high:F2} m/s)");
        }

        /// <summary>Velocidades hacia atrás de un pie mientras está apoyado (en el 10 % más bajo de su recorrido vertical).</summary>
        private static void CollectPlanted(System.Collections.Generic.List<Vector2> samples, System.Collections.Generic.List<float> planted)
        {
            if (samples.Count < 8) return;
            // Percentiles 3 y 97 en vez del mínimo y el máximo: un fotograma raro no mueve el umbral
            var heights = new System.Collections.Generic.List<float>(samples.Count);
            foreach (Vector2 s in samples) heights.Add(s.x);
            heights.Sort();
            float minY = heights[Mathf.FloorToInt((heights.Count - 1) * 0.03f)];
            float maxY = heights[Mathf.FloorToInt((heights.Count - 1) * 0.97f)];
            float below = minY + (maxY - minY) * 0.1f;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].x <= below && samples[i - 1].x <= below && samples[i].y > 0.05f) planted.Add(samples[i].y);
            }
        }

        /// <summary>
        /// Fuera de Play: escribe cuántos metros por segundo cubre cada clip de locomoción a velocidad normal
        /// (lo que mide AyniAttackTimingBaker.MeasureGroundSpeed) y cuánto dura su ciclo.
        /// </summary>
        public static void MeasureClips()
        {
            var report = new StringBuilder("[Ayni Paso] Lo que cubre cada clip a velocidad normal:");
            foreach (string name in new[] { "Walk_Forward_InPlace", "Jog_Forward_InPlace", "Run_Forward_InPlace", "Sprint_Run_InPlace",
                                            "Crouch_Walk_InPlace", "Strafe_Left", "Strafe_Right", "Walk_Back" })
            {
                AnimationClip clip = null;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath($"Assets/Art/Characters/Animations/{name}.fbx"))
                {
                    if (asset is AnimationClip c && !c.name.StartsWith("__preview__")) { clip = c; break; }
                }
                if (clip == null) { report.Append($"\n   {name}: no está"); continue; }
                float speed = AyniAttackTimingBaker.MeasureGroundSpeed(clip);
                report.Append($"\n   {name}: {speed:F2} m/s · ciclo de {clip.length:F2} s");
            }
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Fuera de Play: guarda en DebugCaptures/foot_paths.csv la trayectoria de cada pie (respecto al cuerpo)
        /// a lo largo de cada clip de locomoción, para analizar la zancada con calma.
        /// </summary>
        public static void DumpFootPaths()
        {
            const string rigPath = "Assets/Art/Characters/Yari_Rigged.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
            if (prefab == null) return;

            GameObject go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(rigPath))
                {
                    if (asset is Avatar av) { anim.avatar = av; break; }
                }
            }

            var csv = new StringBuilder("clip,t,ly,lz,ry,rz,hy,hz,lx,rx\n");
            bool started = !AnimationMode.InAnimationMode();
            if (started) AnimationMode.StartAnimationMode();
            try
            {
                Transform lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                foreach (string name in new[] { "Walk_Forward_InPlace", "Jog_Forward_InPlace", "Run_Forward_InPlace", "Sprint_Run_InPlace",
                                                "Crouch_Walk_InPlace", "Strafe_Left", "Strafe_Right", "Walk_Back" })
                {
                    AnimationClip clip = null;
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath($"Assets/Art/Characters/Animations/{name}.fbx"))
                    {
                        if (asset is AnimationClip c && !c.name.StartsWith("__preview__")) { clip = c; break; }
                    }
                    if (clip == null || lf == null || rf == null) continue;
                    const float step = 1f / 240f;
                    for (float t = 0f; t <= clip.length; t += step)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(go, clip, t);
                        AnimationMode.EndSampling();
                        Vector3 l = go.transform.InverseTransformPoint(lf.position);
                        Vector3 r = go.transform.InverseTransformPoint(rf.position);
                        Vector3 h = go.transform.InverseTransformPoint(hips.position);
                        csv.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4}\n", name, t, l.y, l.z, r.y, r.z, h.y, h.z, l.x, r.x));
                    }
                }
            }
            finally
            {
                if (started) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }

            string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "foot_paths.csv"), csv.ToString());
            Debug.Log("[Ayni Paso] Trayectorias de los pies guardadas en DebugCaptures/foot_paths.csv");
        }

        /// <summary>
        /// Fuera de Play: cambia en un clip de Assets/Art/Characters/Animations si la rotación de la raíz se calcula
        /// desde la orientación original (1) o desde la del torso (0), para comparar con DumpFootPaths.
        /// </summary>
        public static void SetClipFacing(string fbxName, float keepOriginal)
        {
            var importer = AssetImporter.GetAtPath($"Assets/Art/Characters/Animations/{fbxName}.fbx") as ModelImporter;
            if (importer == null) { Debug.LogWarning("[Ayni Paso] No existe " + fbxName); return; }
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (var c in clips) c.keepOriginalOrientation = keepOriginal > 0.5f;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log($"[Ayni Paso] {fbxName}: orientación {(keepOriginal > 0.5f ? "original" : "del torso")}.");
        }

        // ───────────────────────── Salto ─────────────────────────

        private static CharacterController leapBody;
        private static float leapEnd, leapAirStart;
        private static Vector3 leapFrom;
        private static float leapPeak;
        private static bool leapInAir;

        /// <summary>Mide el próximo salto de Yari (distancia, altura y tiempo en el aire) dentro de los segundos indicados.</summary>
        public static void MeasureJump(float seconds)
        {
            if (!Application.isPlaying) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            leapBody = player != null ? player.GetComponent<CharacterController>() : null;
            if (leapBody == null) return;
            leapEnd = Time.time + seconds;
            leapInAir = false;
            leapFrom = player.transform.position;
            Application.onBeforeRender -= JumpTick;
            Application.onBeforeRender += JumpTick;
        }

        private static void JumpTick()
        {
            if (!Application.isPlaying || leapBody == null || Time.time > leapEnd)
            {
                Application.onBeforeRender -= JumpTick;
                if (Application.isPlaying && !leapInAir) Debug.Log("[Ayni Salto] No hubo ningún salto en ese tiempo.");
                return;
            }

            Vector3 p = leapBody.transform.position;
            if (!leapInAir)
            {
                if (leapBody.isGrounded) { leapFrom = p; return; }
                leapInAir = true;
                leapAirStart = Time.time;
                leapPeak = p.y;
                return;
            }

            leapPeak = Mathf.Max(leapPeak, p.y);
            if (!leapBody.isGrounded || Time.time - leapAirStart < 0.1f) return;

            Application.onBeforeRender -= JumpTick;
            Vector3 flat = p - leapFrom;
            flat.y = 0f;
            float air = Time.time - leapAirStart;
            Debug.Log($"[Ayni Salto] {flat.magnitude:F2} m de largo · {leapPeak - leapFrom.y:F2} m de alto · {air:F2} s en el aire · " +
                      $"{flat.magnitude / Mathf.Max(0.01f, air):F2} m/s");
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
            if (!Application.isPlaying) return;
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

        /// <summary>Deja a los rivales quietos y sin atacar, pero presentes (para probar el movimiento con uno fijado).</summary>
        public static void PacifyEnemies()
        {
            if (!Application.isPlaying) return; // fuera de Play tocaría la escena abierta
            // Siguen activos (para poder fijarlos), pero no ven a Yari, no se mueven y no alcanzan a golpear
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                foreach (string field in new[] { "aggroRange", "moveSpeed", "attackRange" })
                {
                    var info = typeof(EnemyController).GetField(field, flags);
                    if (info != null && info.FieldType == typeof(float)) info.SetValue(enemy, 0f);
                }
            }
        }

        /// <summary>Cámara lenta para capturar un movimiento fotograma a fotograma (1 = normal).</summary>
        public static void SetTimeScale(float scale)
        {
            if (Application.isPlaying) Time.timeScale = Mathf.Clamp(scale, 0.05f, 2f);
        }

        public static void RemoveEnemies()
        {
            if (!Application.isPlaying) return; // fuera de Play dejaría al jefe desactivado en la escena abierta
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                enemy.gameObject.SetActive(false);
            }
        }
    }
}
#endif
