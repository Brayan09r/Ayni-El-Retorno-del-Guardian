#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Compila el juego para navegador (WebGL), listo para subirlo a Unity Play.
    ///   Ayni > Publicar > 1. Preparar y compilar para Web      (la primera vez tarda: cambia de plataforma y reimporta)
    ///   Ayni > Publicar > 2. Volver a la plataforma de Windows  (para seguir trabajando como siempre)
    /// La compilación queda en Builds/Web/AyniNivel1 (fuera de git). Para publicarla:
    /// File > Build Profiles > Web > Publish to Play.
    /// Desde el puente de agentes: SwitchToWeb, luego Build; el resultado se escribe en UserSettings/AyniAgent/webbuild.txt.
    /// </summary>
    public static class AyniWebBuild
    {
        private const string Scene = "Assets/network of paths/Scenes/SampleScene.unity";
        public const string Output = "Builds/Web/AyniNivel1";
        private const string ReportPath = "UserSettings/AyniAgent/webbuild.txt";

        [MenuItem("Ayni/Publicar/1. Preparar y compilar para Web")]
        public static void BuildFromMenu()
        {
            SwitchToWeb();
            EditorApplication.delayCall += Build;
        }

        [MenuItem("Ayni/Publicar/2. Volver a la plataforma de Windows")]
        public static void BackToWindows()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64) { Note("Ya está en Windows."); return; }
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Note(ok ? "Plataforma activa: Windows." : "No se pudo volver a Windows.");
        }

        /// <summary>Texturas más ligeras solo para Web, ajustes del reproductor y cambio de plataforma.</summary>
        public static void SwitchToWeb()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Note("Sal del modo Play antes de compilar."); return; }
            Note("PREPARANDO: texturas y ajustes para Web...");
            int changed = LightenTextures();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                Note(ok ? $"Plataforma activa: Web ({changed} texturas aligeradas)." : "ERROR: no se pudo cambiar a la plataforma Web (¿está instalado el módulo WebGL?).");
            }
            else Note($"La plataforma activa ya era Web ({changed} texturas aligeradas).");
        }

        /// <summary>
        /// En el navegador todo se descarga antes de jugar: las texturas de los personajes (4K) pasan a 1024 y el resto
        /// queda como mucho a 2048. Es un ajuste propio de la plataforma Web: en Windows siguen a su tamaño.
        /// </summary>
        private static int LightenTextures()
        {
            int changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                    int cap = path.StartsWith("Assets/Art/Characters", StringComparison.OrdinalIgnoreCase) ? 1024 : 2048;
                    TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
                    int current = web.overridden ? web.maxTextureSize : importer.maxTextureSize;
                    if (current <= cap) continue;
                    web.overridden = true;
                    web.maxTextureSize = cap;
                    web.format = TextureImporterFormat.Automatic;
                    importer.SetPlatformTextureSettings(web);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return changed;
        }

        private static void ConfigurePlayer()
        {
            // Gzip con descompresión de reserva: carga en cualquier servidor, también si no envía las cabeceras de compresión
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Note("Sal del modo Play antes de compilar."); return; }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) { Note("ERROR: primero hay que cambiar a la plataforma Web (SwitchToWeb)."); return; }

            Note("COMPILANDO para Web... (puede tardar bastante la primera vez)");
            EditorSceneManager.SaveOpenScenes();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var text = new StringBuilder();
            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary s = report.summary;
                text.AppendLine($"RESULTADO: {s.result}");
                text.AppendLine($"Tamaño: {s.totalSize / (1024f * 1024f):F1} MB · Tiempo: {s.totalTime.TotalMinutes:F1} min · Errores: {s.totalErrors} · Avisos: {s.totalWarnings}");
                text.AppendLine($"Carpeta: {Path.GetFullPath(Output)}");
                int shown = 0;
                foreach (BuildStep step in report.steps)
                {
                    foreach (BuildStepMessage message in step.messages)
                    {
                        if (message.type != LogType.Error && message.type != LogType.Exception) continue;
                        if (shown++ < 25) text.AppendLine("  ERROR [" + step.name + "] " + message.content);
                    }
                }
            }
            catch (Exception e)
            {
                text.AppendLine("RESULTADO: Excepción");
                text.AppendLine(e.ToString());
            }
            Note(text.ToString().TrimEnd());
        }

        /// <summary>Busca en el registro del editor los enlaces de Unity Play (la ventana de publicar no deja copiarlos).</summary>
        public static void FindPlayLinks()
        {
            try
            {
                string path = Application.consoleLogPath;
                string text;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }
                var found = new System.Collections.Generic.List<string>();
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(text, @"https?://[^\s""'<>]*(play\.unity|connect\.unity|unity\.com/[^\s""'<>]*games)[^\s""'<>]*"))
                {
                    if (!found.Contains(m.Value)) found.Add(m.Value);
                }
                var sb = new StringBuilder("Enlaces de Unity Play en el registro del editor (" + found.Count + "):");
                for (int i = Mathf.Max(0, found.Count - 12); i < found.Count; i++) sb.Append("\n   ").Append(found[i]);
                Note(sb.ToString());
            }
            catch (Exception e) { Note("No se pudo leer el registro del editor: " + e.Message); }
        }

        /// <summary>Vuelca lo que sabe la ventana "Publish to Unity Play" (tipo, campos y cualquier texto con un enlace).</summary>
        public static void InspectPublisher()
        {
            var sb = new StringBuilder("Ventanas de publicación:");
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                string title = window.titleContent != null ? window.titleContent.text : "";
                Type type = window.GetType();
                if (title.IndexOf("Play", StringComparison.OrdinalIgnoreCase) < 0 && type.FullName.IndexOf("Publish", StringComparison.OrdinalIgnoreCase) < 0) continue;
                sb.Append("\n  ").Append(title).Append(" -> ").Append(type.FullName).Append(" [").Append(type.Assembly.GetName().Name).Append("]");
                var seen = new System.Collections.Generic.HashSet<object>();
                Walk(window, type.Name, 0, sb, seen);
            }
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name.IndexOf("Publisher", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Connect.Share", StringComparison.OrdinalIgnoreCase) < 0) continue;
                sb.Append("\n  Ensamblado: ").Append(name);
                foreach (Type t in assembly.GetTypes()) sb.Append("\n     ").Append(t.FullName);
            }
            Note(sb.ToString());
        }

        private static void Walk(object target, string path, int depth, StringBuilder sb, System.Collections.Generic.HashSet<object> seen)
        {
            if (target == null || depth > 5 || !seen.Add(target)) return;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                                         System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            for (Type type = target.GetType(); type != null && type != typeof(EditorWindow) && type != typeof(ScriptableObject) && type != typeof(object); type = type.BaseType)
            {
                foreach (System.Reflection.FieldInfo field in type.GetFields(flags))
                {
                    object value;
                    try { value = field.GetValue(target); } catch (Exception) { continue; }
                    if (value == null) continue;
                    if (value is string text)
                    {
                        if (text.Length > 0 && text.Length < 400) sb.Append("\n     ").Append(path).Append('.').Append(field.Name).Append(" = ").Append(text);
                        continue;
                    }
                    Type ft = value.GetType();
                    if (ft.IsPrimitive || ft.IsEnum) { if (depth <= 2) sb.Append("\n     ").Append(path).Append('.').Append(field.Name).Append(" = ").Append(value); continue; }
                    if (value is UnityEngine.Object || ft.Namespace == null) continue;
                    if (ft.Namespace.StartsWith("UnityEngine.UIElements") || ft.Namespace.StartsWith("System")) continue;
                    Walk(value, path + "." + field.Name, depth + 1, sb, seen);
                }
            }
        }

        private static void Note(string message)
        {
            Debug.Log("[Ayni Web] " + message);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.AppendAllText(ReportPath, DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
            }
            catch (Exception) { }
        }
    }
}
#endif
