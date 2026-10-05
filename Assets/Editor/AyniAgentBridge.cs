#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Puente para agentes (Claude, Antigravity...) que trabajan desde la terminal con Unity abierto.
    /// Cada medio segundo mira si existe UserSettings/AyniAgent/cmd.txt; si existe, ejecuta sus líneas y lo borra.
    /// UserSettings está ignorado por git, así que nada de esto se sube al repositorio.
    ///
    /// Comandos (uno por línea):
    ///   refresh                         Reimporta y recompila los cambios del disco
    ///   menu Ayni/Herramientas/...      Ejecuta un menú del editor
    ///   call Ayni.Editor.Clase.Metodo [args...]   Llama a un método estático (solo del espacio Ayni)
    ///   play / stop                     Entra o sale del modo Play
    ///   capture nombre                  Guarda lo que ve la Main Camera en DebugCaptures/nombre.png
    ///   screenshot nombre               Captura la Game View completa (con HUD) en DebugCaptures/nombre.png (solo en Play)
    ///   wait segundos                   Espera antes de seguir con las siguientes líneas
    ///   clearlog                        Vacía el registro
    /// Todo lo que pasa (y todos los mensajes de la consola) se añade a UserSettings/AyniAgent/log.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class AyniAgentBridge
    {
        private const string Folder = "UserSettings/AyniAgent";
        private static readonly string CommandPath = Folder + "/cmd.txt";
        private static readonly string LogPath = Folder + "/log.txt";
        private static readonly string PendingPath = Folder + "/pending.txt";

        private static double nextPoll;
        private static double resumeAt;
        private static readonly Queue<string> queue = new Queue<string>();

        static AyniAgentBridge()
        {
            // Unity abre procesos auxiliares de importación que también cargan los scripts del editor:
            // el puente solo debe vivir en el editor principal (si no, varios procesos se roban los comandos)
            if (AssetDatabase.IsAssetImportWorkerProcess() || Application.isBatchMode) return;

            Directory.CreateDirectory(Folder);
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Las líneas que quedaron pendientes antes de una recompilación siguen ejecutándose después
            if (File.Exists(PendingPath))
            {
                foreach (string line in File.ReadAllLines(PendingPath)) queue.Enqueue(line);
                Append($"[bridge] reanudado tras recarga con {queue.Count} líneas pendientes");
            }
            else
            {
                Append("[bridge] listo");
            }
        }

        /// <summary>
        /// Cuando el Play lo pide el agente, el juego sigue corriendo aunque Unity no tenga el foco
        /// (si no, se congela y los tiempos de las pruebas no cuadran). No cambia los ajustes del proyecto.
        /// </summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("AyniAgentPlay", false))
            {
                Application.runInBackground = true;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool("AyniAgentPlay", false);
            }
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            string text = $"[{type}] {message}";
            if (type == LogType.Exception || type == LogType.Error)
            {
                text += "\n" + stackTrace;
            }
            Append(text);
        }

        private static readonly object fileLock = new object();

        private static void Append(string text)
        {
            try
            {
                lock (fileLock)
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {text}\n", new UTF8Encoding(false));
                }
            }
            catch (Exception) { }
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll) return;
            nextPoll = now + 0.25;

            if (queue.Count == 0 && File.Exists(CommandPath))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(CommandPath);
                    File.Delete(CommandPath);
                }
                catch (IOException) { return; } // el archivo aún se está escribiendo
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line)) queue.Enqueue(line.Trim());
                }
                SavePending();
            }

            while (queue.Count > 0)
            {
                if (now < resumeAt) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

                string line = queue.Dequeue();
                // La cola queda siempre guardada en disco: si un comando recarga el dominio, se retoma donde iba
                SavePending();
                Append("> " + line);
                try
                {
                    if (!Execute(line)) return; // el comando pidió esperar (o provoca una recarga)
                }
                catch (Exception e)
                {
                    Append("[bridge] ERROR: " + e);
                }
            }
        }

        /// <summary>Devuelve false si hay que dejar de procesar líneas en este tick.</summary>
        private static bool Execute(string line)
        {
            int space = line.IndexOf(' ');
            string verb = (space < 0 ? line : line.Substring(0, space)).ToLowerInvariant();
            string rest = space < 0 ? "" : line.Substring(space + 1).Trim();

            switch (verb)
            {
                case "refresh":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    resumeAt = EditorApplication.timeSinceStartup + 1.0;
                    return false;

                case "menu":
                    bool ok = EditorApplication.ExecuteMenuItem(rest);
                    Append(ok ? "[bridge] menú ejecutado" : "[bridge] MENÚ NO ENCONTRADO: " + rest);
                    return true;

                case "call":
                    Call(rest);
                    return true;

                case "play":
                    SessionState.SetBool("AyniAgentPlay", true);
                    EditorApplication.isPlaying = true;
                    return false;

                case "stop":
                    // Salir de Play no recarga el dominio: la cola sigue en memoria
                    EditorApplication.isPlaying = false;
                    resumeAt = EditorApplication.timeSinceStartup + 1.0;
                    return false;

                case "wait":
                    if (float.TryParse(rest, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds))
                    {
                        resumeAt = EditorApplication.timeSinceStartup + seconds;
                    }
                    return false;

                case "capture":
                    CaptureCamera(string.IsNullOrEmpty(rest) ? "agent_capture" : rest);
                    return true;

                case "screenshot":
                    string file = Path.Combine(CapturesFolder(), (string.IsNullOrEmpty(rest) ? "agent_screen" : rest) + ".png");
                    ScreenCapture.CaptureScreenshot(file);
                    Append("[bridge] captura de Game View pedida: " + file);
                    return true;

                case "status":
                    Append($"[bridge] estado: play={EditorApplication.isPlaying} pausa={EditorApplication.isPaused} compilando={EditorApplication.isCompiling} escena={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} tiempo={Time.unscaledTime:F1}");
                    return true;

                case "clearlog":
                    lock (fileLock) { File.WriteAllText(LogPath, ""); }
                    return true;

                default:
                    Append("[bridge] comando desconocido: " + verb);
                    return true;
            }
        }

        /// <summary>Guarda en disco las líneas que quedan, por si el comando recarga el dominio (recompilar, Play).</summary>
        private static void SavePending()
        {
            if (queue.Count > 0) File.WriteAllLines(PendingPath, queue.ToArray());
            else if (File.Exists(PendingPath)) File.Delete(PendingPath);
        }

        private static void Call(string spec)
        {
            string[] parts = spec.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string full = parts[0];
            int dot = full.LastIndexOf('.');
            string typeName = full.Substring(0, dot);
            string methodName = full.Substring(dot + 1);
            if (!typeName.StartsWith("Ayni."))
            {
                Append("[bridge] solo se permiten métodos del espacio de nombres Ayni.");
                return;
            }

            Type type = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) break;
            }
            if (type == null)
            {
                Append("[bridge] tipo no encontrado: " + typeName);
                return;
            }

            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);

            foreach (MethodInfo m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != methodName) continue;
                ParameterInfo[] ps = m.GetParameters();
                // Si sobran palabras, el último parámetro de texto recibe el resto de la línea (con espacios)
                if (ps.Length > 0 && args.Length > ps.Length && ps[ps.Length - 1].ParameterType == typeof(string))
                {
                    var joined = new string[ps.Length];
                    Array.Copy(args, joined, ps.Length - 1);
                    joined[ps.Length - 1] = string.Join(" ", args, ps.Length - 1, args.Length - ps.Length + 1);
                    args = joined;
                }
                if (ps.Length != args.Length) continue;

                var values = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    values[i] = Convert.ChangeType(args[i], ps[i].ParameterType, System.Globalization.CultureInfo.InvariantCulture);
                }
                object result = m.Invoke(null, values);
                Append($"[bridge] {methodName} -> {(result != null ? result.ToString() : "ok")}");
                return;
            }
            Append($"[bridge] método no encontrado: {methodName} con {args.Length} argumentos");
        }

        private static string CapturesFolder()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CaptureCamera(string name)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Append("[bridge] no hay Main Camera");
                return;
            }

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            UnityEngine.Object.DestroyImmediate(rt);

            string path = Path.Combine(CapturesFolder(), name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Append("[bridge] captura guardada: " + path);
        }
    }
}
#endif
