using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Ayni.Enemy;

namespace Ayni.Editor
{
    /// <summary>
    /// Sondas para revisar cómo se mueven los animales en Play (por el puente de agentes):
    /// <code>
    /// call Ayni.Editor.AyniFaunaProbe.List                                   todos los animales con su índice
    /// call Ayni.Editor.AyniFaunaProbe.Watch llama 0 caminar 12 0.25 4 1.2    graba 12 fotogramas siguiendo a la llama 0
    /// </code>
    /// <c>Watch</c>: filtro (parte del tipo o del nombre), índice, qué hacer (caminar, huir, cazar, nada), fotogramas,
    /// segundos entre fotogramas, distancia y altura de la cámara. Guarda DebugCaptures/rec_fauna_FILTRO_INDICE.png y anota
    /// en la consola, en cada fotograma, la velocidad real del animal y la animación que reproduce.
    /// </summary>
    public static class AyniFaunaProbe
    {
        private static readonly string[] Types = { "AyniGrazingAnimal", "AyniVizcacha", "AyniCondorFlight", "PumaTerritory", "AyniFreedRunner" };

        private static List<MonoBehaviour> Find(string filter)
        {
            var list = new List<MonoBehaviour>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID))
            {
                string type = mb.GetType().Name;
                if (System.Array.IndexOf(Types, type) < 0) continue;
                if (!string.IsNullOrEmpty(filter) && filter != "*" &&
                    type.ToLowerInvariant().IndexOf(filter.ToLowerInvariant()) < 0 &&
                    mb.name.ToLowerInvariant().IndexOf(filter.ToLowerInvariant()) < 0) continue;
                list.Add(mb);
            }
            return list;
        }

        public static void List()
        {
            var sb = new StringBuilder();
            var all = Find("*");
            for (int i = 0; i < all.Count; i++)
            {
                MonoBehaviour mb = all[i];
                Animator anim = mb.GetComponentInChildren<Animator>();
                sb.Append($"\n   [{mb.GetType().Name}] {mb.name} en {mb.transform.position:F1} activo={mb.isActiveAndEnabled}" +
                          (anim != null ? $" animator={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "sin controlador")} humano={anim.isHuman}" : ""));
            }
            Debug.Log($"[Ayni Fauna Prueba] {all.Count} animales:" + sb);
        }

        // ───────────────────────── Grabar ─────────────────────────

        private static Component target;
        private static Texture2D sheet;
        private static int frames, index;
        private static float interval, next, distance, height;
        private static string fileName;
        private static Vector3 lastPos;
        private static float lastTime;
        private static Ayni.Player.ThirdPersonSifuCamera camScript;
        private const int Cols = 6, W = 320, H = 240;

        public static void Watch(string filter, int which, string action, int frameCount, float seconds, float camDistance, float camHeight)
        {
            if (!Application.isPlaying) return;
            var found = Find(filter);
            if (found.Count > 0)
            {
                target = found[Mathf.Clamp(which, 0, found.Count - 1)];
                Act((MonoBehaviour)target, action);
            }
            else
            {
                // Animales sin script de fauna (p. ej. los que salen de las jaulas): por nombre exacto
                GameObject go = GameObject.Find(filter);
                if (go == null) { Debug.LogWarning($"[Ayni Fauna Prueba] Nada con '{filter}'"); return; }
                target = go.transform;
            }

            frames = Mathf.Max(1, frameCount);
            interval = Mathf.Max(0f, seconds);
            distance = camDistance;
            height = camHeight;
            index = 0;
            next = 0f;
            fileName = $"rec_fauna_{filter}_{which}";
            int rows = Mathf.CeilToInt(frames / (float)Cols);
            sheet = new Texture2D(W * Mathf.Min(Cols, frames), H * rows, TextureFormat.RGB24, false);
            lastPos = target.transform.position;
            lastTime = Time.time;
            Camera cam = Camera.main;
            camScript = cam != null ? cam.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
            if (camScript != null) camScript.enabled = false;
            Debug.Log($"[Ayni Fauna Prueba] Grabando {target.name} ({target.GetType().Name}) · {action}");
            UnityEditor.EditorApplication.update -= Tick;
            UnityEditor.EditorApplication.update += Tick;
        }

        /// <summary>Pone al animal a hacer algo visible: caminar o huir (camélidos, zorros), cazar (puma).</summary>
        private static void Act(MonoBehaviour mb, string action)
        {
            if (action == "cazar" && mb is Ayni.Fauna.PumaTerritory puma)
            {
                puma.Hunt(8f);
                return;
            }
            if (action != "caminar" && action != "huir" && action != "pastar") return;
            // AyniGrazingAnimal / AyniVizcacha: su máquina de estados es privada; se fuerza por reflexión
            System.Type t = mb.GetType();
            MethodInfo enter = null;
            for (System.Type k = t; k != null && enter == null; k = k.BaseType)
            {
                enter = k.GetMethod("EnterState", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (enter == null) return;
            System.Type stateType = enter.GetParameters()[0].ParameterType;
            string wanted = action == "huir" ? "Fleeing" : action == "pastar" ? "Grazing" : "Walking";
            if (!System.Enum.IsDefined(stateType, wanted)) return;
            if (wanted == "Fleeing")
            {
                // Para huir necesita un destino: lejos de donde está ahora
                FieldInfo targetField = null;
                for (System.Type k = t; k != null && targetField == null; k = k.BaseType)
                {
                    targetField = k.GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (targetField != null) targetField.SetValue(mb, mb.transform.position + mb.transform.forward * 10f);
            }
            enter.Invoke(mb, new[] { System.Enum.Parse(stateType, wanted) });
        }

        private static void Tick()
        {
            if (!Application.isPlaying || sheet == null || target == null)
            {
                Stop();
                return;
            }
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            Camera cam = Camera.main;
            if (cam == null) return;

            Transform t = target.transform;
            Vector3 side = Vector3.Cross(Vector3.up, Flat(t.forward)).normalized;
            cam.transform.position = t.position + side * distance + Vector3.up * height - Flat(t.forward) * (distance * 0.25f);
            cam.transform.LookAt(t.position + Vector3.up * (height * 0.45f));

            var rt = RenderTexture.GetTemporary(W, H, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            int col = index % Cols, row = index / Cols, rows = sheet.height / H;
            sheet.ReadPixels(new Rect(0, 0, W, H), col * W, (rows - 1 - row) * H);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // Velocidad real y animación (para ver pies que patinan o estados que no cambian)
            float dt = Mathf.Max(0.0001f, Time.time - lastTime);
            Vector3 moved = t.position - lastPos;
            moved.y = 0f;
            float speed = moved.magnitude / dt;
            lastPos = t.position;
            lastTime = Time.time;
            string clip = "";
            Animator anim = target.GetComponentInChildren<Animator>();
            if (anim != null && anim.isActiveAndEnabled)
            {
                var info = anim.GetCurrentAnimatorClipInfo(0);
                var st = anim.GetCurrentAnimatorStateInfo(0);
                clip = info.Length > 0 ? $" clip={info[0].clip.name} t={st.normalizedTime:F2} vel_anim={st.speed * st.speedMultiplier * anim.speed:F2}" : " sin clip";
                foreach (var p in anim.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Float) clip += $" {p.name}={anim.GetFloat(p.name):F2}";
                }
            }
            // Altura de la cabeza sobre las patas (para ver el cuello al pastar)
            foreach (Transform b in t.GetComponentsInChildren<Transform>())
            {
                if (b.name != "Head") continue;
                clip += $" cabeza a {b.position.y - t.position.y:F2} m";
                break;
            }
            Debug.Log($"[Ayni Fauna Prueba] #{index} {target.name} pos {t.position:F2} giro {t.eulerAngles:F0} velocidad {speed:F2} m/s{clip}");

            index++;
            if (index >= frames)
            {
                sheet.Apply();
                string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, fileName + ".png"), sheet.EncodeToPNG());
                Debug.Log($"[Ayni Fauna Prueba] Grabación guardada: {fileName}.png");
                Stop();
            }
        }

        private static void Stop()
        {
            UnityEditor.EditorApplication.update -= Tick;
            if (sheet != null) Object.DestroyImmediate(sheet);
            sheet = null;
            if (camScript != null)
            {
                camScript.enabled = true;
                camScript.SnapBehindTarget();
            }
            camScript = null;
        }

        // ───────────────────────── Clips aislados ─────────────────────────

        /// <summary>
        /// Muestrea cada estado del prefab Resources/Fauna/Fauna_KEY de perfil (fila por estado, columna por instante)
        /// y guarda DebugCaptures/clips_KEY.png. Anota por clip los huesos que más giran y cuánto se desplaza la raíz.
        /// Funciona fuera de Play.
        /// </summary>
        public static void Clips(string key, int columns, float camDistance, float camHeight, float yaw)
        {
            var prefab = Resources.Load<GameObject>("Fauna/Fauna_" + key);
            if (prefab == null) { Debug.LogWarning($"[Ayni Fauna Prueba] No hay Fauna_{key}"); return; }
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.DontSave;
            Vector3 origin = new Vector3(0f, 900f, 0f);
            go.transform.SetPositionAndRotation(origin, Quaternion.identity);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false;

            var renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(origin, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            var camGo = new GameObject("FaunaClipCam") { hideFlags = HideFlags.DontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            cam.fieldOfView = 40f;
            var lightGo = new GameObject("FaunaClipLight") { hideFlags = HideFlags.DontSave };
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightGo.transform.rotation = Quaternion.Euler(40f, 30f, 0f);

            var clips = new List<AnimationClip>();
            if (anim != null && anim.runtimeAnimatorController != null) clips.AddRange(anim.runtimeAnimatorController.animationClips);
            int cols = Mathf.Max(1, columns);
            var tex = new Texture2D(W * cols, H * Mathf.Max(1, clips.Count), TextureFormat.RGB24, false);
            var log = new StringBuilder($"[Ayni Fauna Prueba] {key}: modelo {b.size:F2} m, centro {b.center - origin:F2}, {clips.Count} clips");

            // Pose de reposo para comparar
            var bones = go.GetComponentsInChildren<Transform>();
            var rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localRotation;

            for (int c = 0; c < clips.Count; c++)
            {
                AnimationClip clip = clips[c];
                var maxAngle = new float[bones.Length];
                Vector3 rootStart = Vector3.zero, rootEnd = Vector3.zero;
                for (int k = 0; k < cols; k++)
                {
                    float t = clip.length * k / cols;
                    clip.SampleAnimation(go, t);
                    for (int i = 0; i < bones.Length; i++)
                        maxAngle[i] = Mathf.Max(maxAngle[i], Quaternion.Angle(rest[i], bones[i].localRotation));
                    if (k == 0) rootStart = bones.Length > 1 ? bones[1].position : origin;
                    rootEnd = bones.Length > 1 ? bones[1].position : origin;

                    Bounds cb = b;
                    foreach (var r in renderers) cb.Encapsulate(r.bounds);
                    Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    camGo.transform.position = b.center + side * camDistance + Vector3.up * camHeight;
                    camGo.transform.LookAt(b.center);
                    var rt = RenderTexture.GetTemporary(W, H, 24);
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), k * W, (clips.Count - 1 - c) * H);
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);
                }
                // Los cinco huesos que más se mueven en este clip
                var order = new List<int>();
                for (int i = 0; i < bones.Length; i++) order.Add(i);
                order.Sort((x, y) => maxAngle[y].CompareTo(maxAngle[x]));
                log.Append($"\n   fila {c}: {clip.name} {clip.length:F2} s loop={clip.isLooping} raíz {(rootEnd - rootStart):F2} ·");
                for (int n = 0; n < Mathf.Min(6, order.Count); n++)
                    log.Append($" {bones[order[n]].name} {maxAngle[order[n]]:F0}°");
            }
            log.Append("\n   huesos:");
            for (int i = 0; i < bones.Length; i++)
                log.Append($" {bones[i].name}{(bones[i].parent != null && bones[i].parent != go.transform ? "<" + bones[i].parent.name : "")} {rest[i].eulerAngles:F0};");
            Debug.Log(log.ToString());

            tex.Apply();
            string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"clips_{key}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Debug.Log($"[Ayni Fauna Prueba] Guardado clips_{key}.png");
        }

        /// <summary>
        /// Posición de algunos huesos (en el espacio del modelo: +Z delante, +Y arriba) a lo largo de cada clip.
        /// Sirve para ver hacia dónde va de verdad el cuello al pastar o cómo queda el cuerpo al volar.
        /// </summary>
        public static void ClipBones(string key, int samples, string boneList)
        {
            var prefab = Resources.Load<GameObject>("Fauna/Fauna_" + key);
            if (prefab == null) { Debug.LogWarning($"[Ayni Fauna Prueba] No hay Fauna_{key}"); return; }
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false;
            var wanted = boneList.Split(',');
            var all = go.GetComponentsInChildren<Transform>();
            var log = new StringBuilder($"[Ayni Fauna Prueba] Huesos de {key}:");
            var clips = anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.animationClips : new AnimationClip[0];
            for (int c = -1; c < clips.Length; c++)
            {
                log.Append(c < 0 ? "\n   reposo:" : $"\n   {clips[c].name}:");
                int n = c < 0 ? 1 : Mathf.Max(1, samples);
                for (int k = 0; k < n; k++)
                {
                    if (c >= 0) clips[c].SampleAnimation(anim.gameObject, clips[c].length * k / n);
                    log.Append(" |");
                    foreach (string w in wanted)
                    {
                        foreach (var bt in all)
                        {
                            if (bt.name != w) continue;
                            Vector3 p = go.transform.InverseTransformPoint(bt.position);
                            log.Append($" {w}({p.x:F2},{p.y:F2},{p.z:F2})");
                            break;
                        }
                    }
                }
            }
            Debug.Log(log.ToString());
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Velocidad "natural" de cada clip: a qué velocidad avanza el suelo bajo las patas apoyadas (las 4 más bajas).
        /// Con ella se ajusta la velocidad de la animación para que las patas no patinen.
        /// <paramref name="key"/>: Llama, Vicuna... (Resources/Fauna) o una ruta de prefab/FBX en Assets/.
        /// </summary>
        public static void GaitSpeed(string key)
        {
            GameObject prefab = key.Contains("/")
                ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(key)
                : Resources.Load<GameObject>("Fauna/Fauna_" + key);
            if (prefab == null) { Debug.LogWarning($"[Ayni Fauna Prueba] No se encontró {key}"); return; }
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false;
            var clips = anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.animationClips : new AnimationClip[0];

            // Las cuatro patas: huesos "Foot" si los hay; si no, los huesos hoja más bajos en reposo
            var bones = new List<Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>())
            {
                if (t.name.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponent<Renderer>() == null) bones.Add(t);
            }
            if (bones.Count < 4)
            {
                bones.Clear();
                foreach (var t in go.GetComponentsInChildren<Transform>())
                {
                    if (t == go.transform || t.childCount > 0 || t.GetComponent<Renderer>() != null) continue;
                    bones.Add(t);
                }
                bones.Sort((a, b) => a.position.y.CompareTo(b.position.y));
            }
            if (bones.Count > 4) bones.RemoveRange(4, bones.Count - 4);
            GameObject sampleRoot = anim != null ? anim.gameObject : go;
            var log = new StringBuilder($"[Ayni Fauna Prueba] Marcha de {key} (escala 1) · patas:");
            foreach (var b in bones) log.Append($" {b.name}");

            const int N = 120;
            foreach (var clip in clips)
            {
                var ys = new float[bones.Count, N];
                var zs = new float[bones.Count, N];
                for (int k = 0; k < N; k++)
                {
                    clip.SampleAnimation(sampleRoot, clip.length * k / N);
                    for (int i = 0; i < bones.Count; i++)
                    {
                        Vector3 p = go.transform.InverseTransformPoint(bones[i].position);
                        ys[i, k] = p.y;
                        zs[i, k] = p.z;
                    }
                }
                float sum = 0f; int count = 0; float lift = 0f;
                for (int i = 0; i < bones.Count; i++)
                {
                    float min = float.MaxValue, max = float.MinValue;
                    for (int k = 0; k < N; k++) { min = Mathf.Min(min, ys[i, k]); max = Mathf.Max(max, ys[i, k]); }
                    lift = Mathf.Max(lift, max - min);
                    // Apoyo: la parte baja del ciclo de cada pata
                    float limit = min + Mathf.Max(0.01f, (max - min) * 0.35f);
                    for (int k = 0; k < N; k++)
                    {
                        int k2 = (k + 1) % N;
                        if (ys[i, k] > limit || ys[i, k2] > limit) continue;
                        float dz = zs[i, k] - zs[i, k2];
                        sum += dz / (clip.length / N);
                        count++;
                    }
                }
                float natural = count > 0 ? sum / count : 0f;
                log.Append($"\n   {clip.name}: {clip.length:F2} s · suelo a {natural:F2} m/s (negativo = marcha al revés) · pata sube {lift:F2} m");
            }
            Debug.Log(log.ToString());
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Jerarquía de huesos de un modelo (ruta, posición en reposo en el espacio del modelo) y las rutas que anima
        /// cada clip, para saber qué huesos mueve cada animación.
        /// </summary>
        public static void Bones(string key, string clipName)
        {
            var prefab = Resources.Load<GameObject>("Fauna/Fauna_" + key);
            if (prefab == null) { Debug.LogWarning($"[Ayni Fauna Prueba] No hay Fauna_{key}"); return; }
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anim = go.GetComponentInChildren<Animator>();
            var log = new StringBuilder($"[Ayni Fauna Prueba] Huesos de {key}:");
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
            {
                if (t.GetComponent<Renderer>() != null) continue;
                Vector3 p = go.transform.InverseTransformPoint(t.position);
                log.Append($"\n   {UnityEditor.AnimationUtility.CalculateTransformPath(t, go.transform)} ({p.x:F2},{p.y:F2},{p.z:F2})");
            }
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
                {
                    if (!string.IsNullOrEmpty(clipName) && clip.name != clipName) continue;
                    var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
                    log.Append($"\n   clip {clip.name}: {bindings.Length} curvas, {clip.length:F2} s, {clip.frameRate} fps");
                    if (string.IsNullOrEmpty(clipName)) continue;
                    foreach (var b in bindings)
                    {
                        var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, b);
                        log.Append($"\n      {b.path} · {b.propertyName} · {curve.keys.Length} claves");
                    }
                }
            }
            Debug.Log(log.ToString());
            Object.DestroyImmediate(go);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v : Vector3.forward;
        }
    }
}
