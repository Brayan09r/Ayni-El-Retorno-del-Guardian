#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Ayni.Editor
{
    /// <summary>
    /// Guardar y subir el proyecto a GitHub desde Unity, sin abrir una terminal.
    ///  - "Ver cambios pendientes" solo mira: no modifica nada.
    ///  - "Guardar y subir a GitHub" hace git add, commit y push de la rama actual, previa confirmación.
    /// El mensaje del commit se lee de UserSettings/AyniGitMessage.txt (una carpeta que git ignora).
    /// Todo lo que responde git queda en UserSettings/AyniGit.txt.
    /// </summary>
    public static class AyniGitTools
    {
        private const string LogPath = "UserSettings/AyniGit.txt";
        private const string MessagePath = "UserSettings/AyniGitMessage.txt";

        [MenuItem("Ayni/Git/1. Ver cambios pendientes")]
        public static void ShowStatus()
        {
            var log = new StringBuilder();
            log.AppendLine("=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " · cambios pendientes ===");
            Run("rev-parse --abbrev-ref HEAD", log);
            Run("status --short --branch", log);
            Run("log -4 --oneline --decorate", log);
            WriteLog(log);
            Debug.Log("[Ayni Git] Estado guardado en " + LogPath + "\n" + log);
        }

        [MenuItem("Ayni/Git/2. Guardar y subir a GitHub")]
        public static void CommitAndPush()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Git] Sal del modo Play antes de subir.");
                return;
            }
            if (!File.Exists(MessagePath) || string.IsNullOrWhiteSpace(File.ReadAllText(MessagePath)))
            {
                EditorUtility.DisplayDialog("Subir a GitHub",
                    "Falta el mensaje del commit.\n\nEscribe una línea que describa los cambios en:\n" + MessagePath, "Entendido");
                return;
            }

            AssetDatabase.SaveAssets();
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            var log = new StringBuilder();
            log.AppendLine("=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " · guardar y subir ===");

            string branch = Run("rev-parse --abbrev-ref HEAD", log, out int branchCode).Trim();
            if (branchCode != 0 || string.IsNullOrEmpty(branch))
            {
                WriteLog(log);
                Debug.LogError("[Ayni Git] No se pudo ejecutar git en la carpeta del proyecto. Detalle en " + LogPath);
                return;
            }

            string status = Run("status --porcelain", log, out _);
            int changed = status.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            string firstLine = File.ReadAllLines(MessagePath)[0];

            if (!EditorUtility.DisplayDialog("Subir a GitHub",
                    $"Rama: {branch}\nArchivos con cambios: {changed}\n\nMensaje:\n{firstLine}\n\nSe guardará un commit y se subirá a GitHub.",
                    "Subir", "Cancelar"))
            {
                return;
            }

            try
            {
                if (changed > 0)
                {
                    EditorUtility.DisplayProgressBar("Ayni Git", "Guardando el commit...", 0.3f);
                    Run("add -A", log, out int addCode);
                    Run("commit -F \"" + MessagePath + "\"", log, out int commitCode);
                    if (addCode != 0 || commitCode != 0)
                    {
                        WriteLog(log);
                        Debug.LogError("[Ayni Git] No se pudo crear el commit. Detalle en " + LogPath);
                        return;
                    }
                }

                EditorUtility.DisplayProgressBar("Ayni Git", "Subiendo a GitHub...", 0.7f);
                Run("push origin " + branch, log, out int pushCode, 300000);
                Run("status --short --branch", log, out _);
                Run("log -3 --oneline --decorate", log, out _);
                WriteLog(log);

                if (pushCode == 0)
                {
                    File.Delete(MessagePath);
                    Debug.Log("<color=green>[Ayni Git]</color> Cambios subidos a GitHub (rama " + branch + "). Detalle en " + LogPath);
                }
                else
                {
                    Debug.LogError("[Ayni Git] El commit se guardó, pero GitHub rechazó la subida. Detalle en " + LogPath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void WriteLog(StringBuilder log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, log.ToString(), new UTF8Encoding(false));
        }

        private static string Run(string arguments, StringBuilder log)
        {
            return Run(arguments, log, out _);
        }

        private static string Run(string arguments, StringBuilder log, out int exitCode, int timeoutMs = 120000)
        {
            var output = new StringBuilder();
            var errors = new StringBuilder();
            exitCode = -1;
            log.AppendLine("$ git " + arguments);

            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                // Si git necesitara pedir usuario o contraseña, que falle en vez de quedarse esperando
                info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using (var process = new Process { StartInfo = info })
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) errors.AppendLine(e.Data); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        errors.AppendLine("(se agotó el tiempo de espera)");
                    }
                    else
                    {
                        process.WaitForExit();
                        exitCode = process.ExitCode;
                    }
                }
            }
            catch (Exception e)
            {
                errors.AppendLine("No se pudo ejecutar git: " + e.Message);
            }

            if (output.Length > 0) log.Append(output);
            if (errors.Length > 0) log.Append(errors);
            log.AppendLine("(código " + exitCode + ")");
            log.AppendLine();
            return output.ToString();
        }
    }
}
#endif
