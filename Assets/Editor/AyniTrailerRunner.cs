#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;
using Ayni.UI;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// Graba las tomas del tráiler directamente desde la vista de juego (con HUD y efectos de pantalla), a 30 fps
    /// exactos, en la carpeta Trailer/tomas del proyecto (fuera de Assets). Cada toma se dirige sola: coloca a Yari,
    /// mueve la cámara y simula las teclas. Desde el puente de agentes:
    ///   call Ayni.Editor.AyniTrailer.GameViewHD            pone la vista de juego a 1920 x 1080 (antes de dar Play)
    ///   call Ayni.Editor.AyniTrailer.Arm prologo           la toma empieza sola al entrar en Play (para el prólogo)
    ///   call Ayni.Editor.AyniTrailer.Shot nombre           en Play: graba la toma
    ///   call Ayni.Editor.AyniTrailer.GameViewRestore       devuelve la vista de juego a como estaba
    /// Cuando una toma termina escribe en la consola "[Ayni Trailer] TOMA LISTA: nombre".
    /// </summary>
    [InitializeOnLoad]
    public static class AyniTrailer
    {
        public const int Fps = 30;
        private const string ArmKey = "AyniTrailerArmedShot";
        private const string SizeKey = "AyniTrailerPreviousSize";

        public static string Folder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Trailer", "tomas");

        static AyniTrailer()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) AyniTrailerRunner.Shutdown();
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                string armed = EditorPrefs.GetString(ArmKey, "");
                if (string.IsNullOrEmpty(armed)) return;
                EditorPrefs.DeleteKey(ArmKey);
                Shot(armed);
            };
        }

        public static void Arm(string shot)
        {
            EditorPrefs.SetString(ArmKey, shot);
            Debug.Log("[Ayni Trailer] Toma armada para el próximo Play: " + shot);
        }

        public static void Shot(string name)
        {
            if (!Application.isPlaying) { Debug.Log("[Ayni Trailer] Las tomas se graban en Play."); return; }
            AyniTrailerRunner.Get().Run(name);
        }

        public static void Abort()
        {
            if (!Application.isPlaying) return;
            AyniTrailerRunner.Get().Abort();
        }

        // ───────────────────────── Tamaño de la vista de juego ─────────────────────────

        public static void GameViewHD() => SetGameViewSize(1920, 1080);
        public static void GameView720() => SetGameViewSize(1280, 720);

        /// <summary>Elige un tamaño de la lista de la vista de juego por su posición (0 = Free Aspect, 1 = 16:9 Aspect...).</summary>
        public static void GameViewIndex(int index)
        {
            try
            {
                Type viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(viewType)) SelectSize(viewType, view, index);
                EditorPrefs.DeleteKey(SizeKey);
                Debug.Log("[Ayni Trailer] Vista de juego en el tamaño " + index + ".");
            }
            catch (Exception e) { Debug.LogWarning("[Ayni Trailer] No se pudo cambiar la vista de juego: " + e.Message); }
        }

        public static void GameViewRestore()
        {
            // Sin tamaño guardado se vuelve al primero de la lista (Free Aspect)
            int previous = Mathf.Max(0, EditorPrefs.GetInt(SizeKey, 0));
            try
            {
                Type viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(viewType)) SelectSize(viewType, view, previous);
                EditorPrefs.DeleteKey(SizeKey);
                Debug.Log("[Ayni Trailer] Vista de juego restaurada (tamaño " + previous + ").");
            }
            catch (Exception e) { Debug.LogWarning("[Ayni Trailer] No se pudo restaurar la vista de juego: " + e.Message); }
        }

        private static void SelectSize(Type viewType, UnityEngine.Object view, int index)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo selected = viewType.GetProperty("selectedSizeIndex", flags);
            selected.SetValue(view, index);
            (view as EditorWindow)?.Repaint();
        }

        private static void SetGameViewSize(int width, int height)
        {
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Assembly editor = typeof(UnityEditor.Editor).Assembly;
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
                Type viewType = editor.GetType("UnityEditor.GameView");
                object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                object groupType = sizesType.GetProperty("currentGroupType", flags).GetValue(sizes);
                object group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { groupType });
                Type groupClass = group.GetType();
                int total = (int)groupClass.GetMethod("GetTotalCount", flags).Invoke(group, null);
                MethodInfo getSize = groupClass.GetMethod("GetGameViewSize", flags);

                int index = -1;
                for (int i = 0; i < total; i++)
                {
                    object size = getSize.Invoke(group, new object[] { i });
                    Type sizeClass = size.GetType();
                    int w = (int)sizeClass.GetProperty("width", flags).GetValue(size);
                    int h = (int)sizeClass.GetProperty("height", flags).GetValue(size);
                    string kind = sizeClass.GetProperty("sizeType", flags).GetValue(size).ToString();
                    if (w == width && h == height && kind == "FixedResolution") { index = i; break; }
                }
                if (index < 0)
                {
                    Type sizeClass = editor.GetType("UnityEditor.GameViewSize");
                    Type kindType = editor.GetType("UnityEditor.GameViewSizeType");
                    ConstructorInfo ctor = sizeClass.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                    object custom = ctor.Invoke(new[] { Enum.Parse(kindType, "FixedResolution"), width, height, "Trailer " + width + "x" + height });
                    groupClass.GetMethod("AddCustomSize", flags).Invoke(group, new[] { custom });
                    index = total;
                }

                PropertyInfo selected = viewType.GetProperty("selectedSizeIndex", flags);
                int count = 0;
                foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(viewType))
                {
                    int current = (int)selected.GetValue(view);
                    if (!EditorPrefs.HasKey(SizeKey) && current != index) EditorPrefs.SetInt(SizeKey, current);
                    SelectSize(viewType, view, index);
                    count++;
                }
                Debug.Log($"[Ayni Trailer] Vista de juego a {width} x {height} (índice {index}, {count} ventana(s)).");
            }
            catch (Exception e)
            {
                Debug.LogError("[Ayni Trailer] No se pudo cambiar el tamaño de la vista de juego: " + e);
            }
        }
    }

    /// <summary>
    /// El que graba y dirige: vive solo mientras dura el Play. No es un componente (los scripts de la carpeta Editor no
    /// se pueden añadir a objetos): sus corrutinas las ejecuta la cámara del juego.
    /// </summary>
    public class AyniTrailerRunner
    {
        private struct Anchor { public Vector2 center; public float yaw, length; }

        private static readonly Dictionary<string, Anchor> Anchors = new Dictionary<string, Anchor>
        {
            { "K1", new Anchor { center = new Vector2(330.9f, 468.3f), yaw = 144.2f, length = 24f } },
            { "K2", new Anchor { center = new Vector2(392.1f, 387.7f), yaw = 116.1f, length = 28f } },
            { "K3", new Anchor { center = new Vector2(477.9f, 433.9f), yaw = 18.3f, length = 32f } },
            { "K4", new Anchor { center = new Vector2(556.1f, 519.1f), yaw = 141.1f, length = 26f } },
            { "O1", new Anchor { center = new Vector2(283.0f, 539.7f), yaw = 105.1f } },
            { "O2", new Anchor { center = new Vector2(294.5f, 506.0f), yaw = 145.0f } },
            { "O3", new Anchor { center = new Vector2(360.3f, 441.7f), yaw = 155.5f } },
            { "O4", new Anchor { center = new Vector2(368.7f, 414.5f), yaw = 155.3f } },
            { "O5", new Anchor { center = new Vector2(429.7f, 384.5f), yaw = 76.4f } },
            { "O6", new Anchor { center = new Vector2(450.4f, 386.0f), yaw = 65.4f } },
            { "O7", new Anchor { center = new Vector2(484.6f, 475.6f), yaw = 9.9f } },
            { "O8", new Anchor { center = new Vector2(497.9f, 507.0f), yaw = 37.2f } },
            { "PLAZA", new Anchor { center = new Vector2(614f, 477f), yaw = 0f } },
        };

        private MediaEncoder encoder;
        private bool recording;
        private int generation, frames, recWidth, recHeight;
        private double realStart;
        private string clipName;
        private int previousTargetFrameRate, previousVSync;

        private Camera cam;
        private ThirdPersonSifuCamera follow;
        private Transform player;
        private YariCombatController yari;
        private Terrain terrain;
        private float baseFov;
        private bool busy, prepared;
        private Coroutine current;
        private MonoBehaviour host;
        private static AyniTrailerRunner instance;

        public static AyniTrailerRunner Get()
        {
            if (instance != null && instance.host != null) return instance;
            instance = new AyniTrailerRunner();
            instance.Setup();
            return instance;
        }

        public static void Shutdown()
        {
            if (instance != null && instance.recording) instance.End();
            instance = null;
        }

        private Coroutine StartCoroutine(IEnumerator routine) => host.StartCoroutine(routine);
        private void StopCoroutine(Coroutine routine) { if (host != null && routine != null) host.StopCoroutine(routine); }
        private static void Destroy(UnityEngine.Object target) => UnityEngine.Object.Destroy(target);

        private void Setup()
        {
            cam = Camera.main;
            if (cam != null)
            {
                follow = cam.GetComponent<ThirdPersonSifuCamera>();
                baseFov = cam.fieldOfView;
            }
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null)
            {
                player = found.transform;
                yari = found.GetComponent<YariCombatController>();
            }
            terrain = Terrain.activeTerrain;
            host = follow != null ? (MonoBehaviour)follow : yari;
        }

        public void Run(string shot)
        {
            if (busy) { Debug.LogWarning("[Ayni Trailer] Ya hay una toma en marcha."); return; }
            current = StartCoroutine(RunShot(shot));
        }

        public void Abort()
        {
            if (current != null) StopCoroutine(current);
            if (recording) End();
            ReleaseCamera();
            busy = false;
            Debug.Log("[Ayni Trailer] Toma interrumpida.");
        }

        private IEnumerator RunShot(string shot)
        {
            busy = true;
            IEnumerator body = null;
            switch (shot)
            {
                case "prueba": body = Prueba(); break;
                case "prologo": body = Prologo(); break;
                case "salto": body = Salto(false, "salto"); break;
                case "salto_lado": body = Salto(true, "salto_lado2"); break;
                case "aldea": body = Aldea(); break;
                case "casa": body = Casa(false); break;
                case "casa_dentro": body = Casa(true); break;
                case "combate1": body = Combate("K1", 1, "combate_k1", 26f, true); break;
                case "combate2": body = Combate("K2", 2, "combate_k2", 26f, false); break;
                case "combate3": body = Combate("K3", 3, "combate_k3", 26f, true); break;
                case "combate4": body = Combate("K4", 4, "combate_k4", 26f, false); break;
                case "jefe": body = Jefe(); break;
                case "renacer": body = Renacer(); break;
                case "renacer_pie": body = RenacerEnPie(); break;
            }
            if (body == null)
            {
                Debug.LogWarning("[Ayni Trailer] Toma desconocida: " + shot);
                busy = false;
                yield break;
            }
            yield return body;
            if (recording) End();
            busy = false;
            Debug.Log("[Ayni Trailer] TOMA LISTA: " + shot);
        }

        // ───────────────────────── Grabación ─────────────────────────

        private void Begin(string clip)
        {
            if (recording) End();
            Directory.CreateDirectory(AyniTrailer.Folder);
            string path = Path.Combine(AyniTrailer.Folder, clip + ".mp4");
            if (File.Exists(path)) File.Delete(path);

            recWidth = Screen.width & ~1;
            recHeight = Screen.height & ~1;
            var attributes = new VideoTrackAttributes
            {
                frameRate = new MediaRational(AyniTrailer.Fps),
                width = (uint)recWidth,
                height = (uint)recHeight,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High
            };
            encoder = new MediaEncoder(path, attributes);
            clipName = clip;
            frames = 0;
            realStart = EditorApplication.timeSinceStartup;

            previousTargetFrameRate = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = AyniTrailer.Fps;
            Time.captureFramerate = AyniTrailer.Fps; // el tiempo del juego avanza 1/30 s por fotograma, tarde lo que tarde en grabarse

            recording = true;
            generation++;
            StartCoroutine(CaptureLoop(generation));
        }

        private IEnumerator CaptureLoop(int mine)
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (recording && mine == generation)
            {
                yield return endOfFrame;
                if (!recording || mine != generation) yield break;

                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (shot == null) continue;
                Texture2D frame = shot;
                if (shot.width != recWidth || shot.height != recHeight)
                {
                    Debug.LogError($"[Ayni Trailer] La vista de juego cambió de tamaño ({shot.width} x {shot.height}): se corta la toma.");
                    Destroy(shot);
                    End();
                    yield break;
                }
                if (shot.format != TextureFormat.RGBA32)
                {
                    frame = new Texture2D(recWidth, recHeight, TextureFormat.RGBA32, false);
                    frame.SetPixels32(shot.GetPixels32());
                    frame.Apply(false);
                }
                encoder.AddFrame(frame);
                frames++;
                if (frame != shot) Destroy(frame);
                Destroy(shot);
            }
        }

        private void End()
        {
            if (!recording) return;
            recording = false;
            generation++;
            try { encoder?.Dispose(); } catch (Exception e) { Debug.LogWarning("[Ayni Trailer] Al cerrar el video: " + e.Message); }
            encoder = null;
            Time.captureFramerate = 0;
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSync;
            double real = EditorApplication.timeSinceStartup - realStart;
            Debug.Log($"[Ayni Trailer] Clip {clipName}.mp4: {frames} fotogramas ({frames / (float)AyniTrailer.Fps:F1} s de video) a {recWidth} x {recHeight}, " +
                      $"grabados en {real:F1} s reales ({frames / Math.Max(0.01, real):F0} fps).");
        }

        // ───────────────────────── Utilidades ─────────────────────────

        private static int Frames(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * AyniTrailer.Fps));

        private IEnumerator Wait(float seconds)
        {
            int n = Frames(seconds);
            for (int i = 0; i < n; i++) yield return null;
        }

        private float Ground(float x, float z)
        {
            return terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : 0f;
        }

        private static Vector2 Forward(float yaw) => new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
        private static Vector2 Right(float yaw) => new Vector2(Mathf.Cos(yaw * Mathf.Deg2Rad), -Mathf.Sin(yaw * Mathf.Deg2Rad));

        /// <summary>Punto del mundo a partir de coordenadas locales de un conjunto u obstáculo (x derecha, z marcha), sobre el suelo.</summary>
        private Vector3 OnGround(string id, float lx, float lz, float above = 0f)
        {
            Anchor a = Anchors[id];
            Vector2 f = Forward(a.yaw), r = Right(a.yaw);
            float x = a.center.x + r.x * lx + f.x * lz, z = a.center.y + r.y * lx + f.y * lz;
            return new Vector3(x, Ground(x, z) + above, z);
        }

        /// <summary>Como <see cref="OnGround"/>, pero la altura se mide desde el suelo del centro (para trayectorias de cámara lisas).</summary>
        private Vector3 Air(string id, float lx, float height, float lz)
        {
            Anchor a = Anchors[id];
            Vector2 f = Forward(a.yaw), r = Right(a.yaw);
            float x = a.center.x + r.x * lx + f.x * lz, z = a.center.y + r.y * lx + f.y * lz;
            float y = Ground(a.center.x, a.center.y) + height;
            return new Vector3(x, Mathf.Max(y, Ground(x, z) + 0.7f), z);
        }

        private Vector2 Local(string id, Vector3 world)
        {
            Anchor a = Anchors[id];
            Vector2 off = new Vector2(world.x, world.z) - a.center;
            return new Vector2(Vector2.Dot(off, Right(a.yaw)), Vector2.Dot(off, Forward(a.yaw)));
        }

        private void Teleport(Vector3 position, float yaw)
        {
            if (player == null) return;
            var mover = player.GetComponent<CharacterController>();
            if (mover != null) mover.enabled = false;
            player.SetPositionAndRotation(position + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
            if (mover != null) mover.enabled = true;
            if (follow != null && follow.enabled) follow.SnapBehindTarget();
        }

        private void TakeCamera()
        {
            if (follow != null) follow.enabled = false;
        }

        private void ReleaseCamera()
        {
            if (cam != null) cam.fieldOfView = baseFov;
            if (follow != null)
            {
                follow.enabled = true;
                follow.SnapBehindTarget();
            }
        }

        private void SetCamera(Vector3 position, Vector3 lookAt, float fov)
        {
            cam.transform.position = position;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - position);
            cam.fieldOfView = fov;
        }

        /// <summary>Vuelo de cámara entre dos encuadres (posición, punto al que mira y campo de visión).</summary>
        private IEnumerator Fly(float seconds, Vector3 fromPos, Vector3 fromLook, Vector3 toPos, Vector3 toLook, float fromFov = 50f, float toFov = 50f)
        {
            TakeCamera();
            int n = Frames(seconds);
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 1f;
                float u = Mathf.Lerp(t, t * t * (3f - 2f * t), 0.45f); // casi lineal: los cortes a mitad de vuelo no se notan
                SetCamera(Vector3.Lerp(fromPos, toPos, u), Vector3.Lerp(fromLook, toLook, u), Mathf.Lerp(fromFov, toFov, u));
                yield return null;
            }
        }

        private void SetHud(bool visible)
        {
            foreach (SifuCombatHUD hud in UnityEngine.Object.FindObjectsByType<SifuCombatHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)) hud.enabled = visible;
        }

        /// <summary>Quita el tutorial del principio y recoge el panel de controles (una vez por sesión de Play).</summary>
        private IEnumerator Prepare()
        {
            if (prepared) yield break;
            prepared = true;
            yield return Wait(0.5f);
            AyniInput.Simulate("Skip", 0.15f);
            yield return Wait(0.6f);
            AyniInput.Simulate("ToggleHud", 0.1f);
            yield return Wait(0.4f);
        }

        /// <summary>Mueve a Yari hacia una dirección del mundo (la entrada es relativa a la cámara, como con el teclado).</summary>
        private void MoveTowards(Vector3 worldDirection, bool sprint, float hold = 0.12f)
        {
            Vector3 f = cam.transform.forward, r = cam.transform.right;
            f.y = 0f; r.y = 0f;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f || f.sqrMagnitude < 0.0001f) return;
            f.Normalize(); r.Normalize(); worldDirection.Normalize();
            AyniInput.SimulateKeys(Vector3.Dot(worldDirection, r), Vector3.Dot(worldDirection, f), hold);
            if (sprint) AyniInput.Simulate("Sprint", hold);
        }

        private AyniEncounterSite Site(int order)
        {
            foreach (AyniEncounterSite site in UnityEngine.Object.FindObjectsByType<AyniEncounterSite>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (site.Order == order) return site;
            }
            return null;
        }

        private EnemyController Nearest(float maxDistance, bool bossOnly = false)
        {
            EnemyController best = null;
            float bestDistance = maxDistance;
            foreach (EnemyController enemy in EnemyController.All)
            {
                if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
                if (bossOnly && !enemy.IsBoss) continue;
                Vector3 to = enemy.transform.position - player.position;
                to.y = 0f;
                if (to.magnitude < bestDistance) { bestDistance = to.magnitude; best = enemy; }
            }
            return best;
        }

        // ───────────────────────── Yari pelea solo ─────────────────────────

        private float nextAttack, nextLock, nextGuard, nextJudge;
        private int strikes;

        /// <summary>
        /// Un fotograma de pelea automática: fija al rival más cercano, se acerca, encadena golpes, se cubre cuando el rival
        /// arma el golpe y resuelve el Juicio (perdón o venganza) cuando le rompe la postura. Devuelve el rival, o null.
        /// </summary>
        private EnemyController Fight(bool mercy, bool allowJudge = true, bool bossOnly = false)
        {
            EnemyController target = Nearest(40f, bossOnly);
            if (target == null) return null;
            float now = Time.unscaledTime;
            Vector3 to = target.transform.position - player.position;
            to.y = 0f;
            float distance = to.magnitude;

            if (yari.LockTarget == null && distance < 12f && now >= nextLock)
            {
                AyniInput.Simulate("LockOn", 0.08f);
                nextLock = now + 0.7f;
            }

            bool threatened = false;
            foreach (EnemyController enemy in EnemyController.All)
            {
                if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || !enemy.IsWindingUp) continue;
                Vector3 d = enemy.transform.position - player.position;
                d.y = 0f;
                if (d.magnitude < 3.2f) threatened = true;
            }

            if (allowJudge && target.CanBeJudged && distance < (target.IsBoss ? 8f : 3f))
            {
                if (now >= nextJudge)
                {
                    AyniInput.Simulate(mercy ? "Mercy" : "Execute", 0.12f);
                    nextJudge = now + 1.2f;
                }
            }
            else if (threatened && now >= nextGuard)
            {
                AyniInput.Simulate("Guard", 0.5f);
                nextGuard = now + 1.1f;
                nextAttack = now + 0.55f;
            }
            else if (distance > 1.75f)
            {
                MoveTowards(to, distance > 5f);
            }
            else if (now >= nextAttack && now >= nextGuard - 0.6f)
            {
                strikes++;
                AyniInput.Simulate(strikes % 5 == 0 ? "HeavyAttack" : "LightAttack", 0.08f);
                nextAttack = now + 0.36f;
            }
            return target;
        }

        // ───────────────────────── Tomas ─────────────────────────

        private IEnumerator Prueba()
        {
            yield return Prepare();
            SetHud(true);
            Begin("prueba");
            Vector3 p = player.position;
            yield return Fly(2f, p + new Vector3(4f, 2.2f, -4f), p + Vector3.up, p + new Vector3(-4f, 2.2f, -4f), p + Vector3.up);
            ReleaseCamera();
            yield return Wait(1f);
            End();
        }

        /// <summary>El prólogo entero, desde el primer fotograma (se arma antes de dar Play).</summary>
        private IEnumerator Prologo()
        {
            prepared = true; // tras el prólogo no sale el tutorial del principio de la misma forma
            Begin("prologo");
            yield return Wait(3f);
            int limit = Frames(80f);
            while (AyniPrologue.IsRunning && limit-- > 0) yield return null;
            yield return Wait(4f);
            End();
        }

        /// <summary>Yari corre por el Qhapaq Ñan y salta el árbol quemado caído.</summary>
        private IEnumerator Salto(bool side, string clip)
        {
            yield return Prepare();
            SetHud(false);
            ReleaseCamera();
            Teleport(OnGround("O1", 0f, -26f), Anchors["O1"].yaw + 10f);
            yield return Wait(0.8f);
            Begin(clip);
            bool jumped = false;
            int limit = Frames(11f);
            Vector3 forward = OnGround("O1", 0f, 30f) - OnGround("O1", 0f, -30f);
            while (limit-- > 0)
            {
                Vector2 local = Local("O1", player.position);
                // Corrige la deriva: siempre hacia el eje del camino
                Vector3 aim = OnGround("O1", 0f, local.y + 6f) - player.position;
                MoveTowards(side ? aim : forward + aim * 0.5f, true, 0.15f);
                if (!jumped && local.y > -2.3f)
                {
                    AyniInput.Simulate("Jump", 0.15f);
                    jumped = true;
                }
                if (side)
                {
                    TakeCamera();
                    Vector3 chest = player.position + Vector3.up * 0.95f;
                    // Travelling lateral: la cámara va un poco por delante de Yari, a su altura
                    Vector3 from = Air("O1", -4.6f, 1.05f, local.y + 2.6f);
                    SetCamera(from, chest, 40f);
                }
                if (local.y > 15f) break;
                yield return null;
            }
            End();
            ReleaseCamera();
        }

        /// <summary>Vuelos de cámara sobre el valle y dos conjuntos de casas.</summary>
        private IEnumerator Aldea()
        {
            yield return Prepare();
            SetHud(false);
            Teleport(OnGround("O1", 0f, -30f), Anchors["O1"].yaw); // Yari fuera de plano

            Begin("aldea_k3");
            yield return Fly(8f, Air("K3", -9f, 15f, -44f), Air("K3", 0f, 2f, 0f), Air("K3", 5f, 6f, -19f), Air("K3", -2f, 2f, 6f), 48f, 52f);
            End();

            Begin("aldea_k2");
            yield return Fly(7f, Air("K2", 26f, 9f, -20f), Air("K2", 0f, 2f, 0f), Air("K2", 24f, 7f, 18f), Air("K2", 0f, 2f, 0f), 46f, 46f);
            End();

            Begin("aldea_k4");
            yield return Fly(7f, Air("K4", -3f, 2.2f, -30f), Air("K4", 0f, 2.6f, 0f), Air("K4", 1f, 2.0f, -10f), Air("K4", 0f, 2.4f, 12f), 50f, 54f);
            End();

            Begin("valle");
            yield return Fly(9f, Air("K2", -40f, 60f, -60f), Air("K3", 0f, 0f, 0f), Air("K2", 10f, 46f, -30f), Air("K3", 0f, 0f, 20f), 55f, 55f);
            End();
            ReleaseCamera();
        }

        /// <summary>Yari entra andando en una de las casas del primer conjunto (sin que salgan los rivales).</summary>
        private IEnumerator Casa(bool fromInside)
        {
            yield return Prepare();
            SetHud(false);
            AyniEncounterSite site = Site(1);
            if (site == null) { Debug.LogWarning("[Ayni Trailer] No está el conjunto K1."); yield break; }
            site.enabled = false;
            ReleaseCamera();

            Transform door = site.GetSpawnPoint(0), inside = site.GetInteriorPoint(0);
            Vector3 into = inside.position - door.position;
            into.y = 0f;
            into.Normalize();
            Vector3 start = door.position - into * 3.2f;
            start.y = Ground(start.x, start.z);
            Teleport(start, Quaternion.LookRotation(into).eulerAngles.y);
            yield return Wait(0.8f);

            if (fromInside)
            {
                // Cámara fija al fondo del cuarto: se ve la puerta, el interior y a Yari entrando a contraluz
                TakeCamera();
                Vector3 side = Vector3.Cross(Vector3.up, into);
                Vector3 eye = inside.position + into * 1.15f + side * 1.2f;
                eye.y = Ground(inside.position.x, inside.position.z) + 1.45f;
                SetCamera(eye, door.position + Vector3.up * 0.95f, 62f);
            }

            Begin(fromInside ? "casa_dentro" : "casa");
            int limit = Frames(8f);
            while (limit-- > 0)
            {
                Vector3 to = inside.position - player.position;
                to.y = 0f;
                if (to.magnitude < 0.5f) break;
                if (fromInside)
                {
                    // Sin la cámara del juego detrás, la dirección se da respecto a la cámara fija
                    MoveTowards(to, false, 0.15f);
                    Vector3 look = Vector3.Lerp(door.position, player.position, 0.6f) + Vector3.up * 0.9f;
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), 0.08f);
                }
                else MoveTowards(to, false, 0.15f);
                yield return null;
            }
            yield return Wait(1.5f);
            End();
            ReleaseCamera();

            Teleport(OnGround("O1", 0f, -30f), Anchors["O1"].yaw);
            yield return Wait(0.3f);
            site.enabled = true;
        }

        /// <summary>Yari entra corriendo en un conjunto, aparecen los rivales y pelea.</summary>
        private IEnumerator Combate(string id, int order, string clip, float seconds, bool mercy)
        {
            yield return Prepare();
            SetHud(true);
            ReleaseCamera();
            Anchor a = Anchors[id];
            Teleport(OnGround(id, 0f, -(a.length * 0.5f + 7f)), a.yaw + 10f);
            yield return Wait(0.8f);

            Begin(clip);
            int limit = Frames(6f);
            while (limit-- > 0)
            {
                Vector2 local = Local(id, player.position);
                if (local.y > -a.length * 0.5f + 5f) break;
                MoveTowards(OnGround(id, 0f, local.y + 6f) - player.position, true, 0.15f);
                yield return null;
            }

            limit = Frames(seconds);
            int idle = 0;
            while (limit-- > 0)
            {
                if (yari != null && (yari.IsDead || yari.IsGameOver)) { yield return null; continue; }
                EnemyController target = Fight(mercy);
                if (target == null && ++idle > Frames(2.5f)) break;
                if (target != null) idle = 0;
                yield return null;
            }
            yield return Wait(1f);
            End();
        }

        /// <summary>Combate con Amaru, Juicio Ayni, perdón y desenlace completo.</summary>
        private IEnumerator Jefe()
        {
            yield return Prepare();
            SetHud(true);
            ReleaseCamera();
            EnemyController boss = null;
            foreach (EnemyController enemy in EnemyController.All)
            {
                if (enemy != null && enemy.IsBoss) boss = enemy;
            }
            if (boss == null) { Debug.LogWarning("[Ayni Trailer] No está el jefe."); yield break; }

            Vector3 from = boss.transform.position + new Vector3(-6f, 0f, -9f);
            from.y = Ground(from.x, from.z);
            Vector3 toBoss = boss.transform.position - from;
            toBoss.y = 0f;
            Teleport(from, Quaternion.LookRotation(toBoss).eulerAngles.y);
            yield return Wait(0.8f);

            Begin("jefe");
            int limit = Frames(16f);
            while (limit-- > 0)
            {
                if (yari != null && (yari.IsDead || yari.IsGameOver)) { yield return null; continue; }
                if (boss.CanBeJudged) break;
                Fight(true, false, true);
                yield return null;
            }

            // Golpe final: lo deja en su última fase y con la postura rota -> Juicio Ayni
            if (!boss.CanBeJudged)
            {
                int approach = Frames(3f);
                while (approach-- > 0)
                {
                    Vector3 d = boss.transform.position - player.position;
                    d.y = 0f;
                    if (d.magnitude < 3f) break;
                    MoveTowards(d, true);
                    yield return null;
                }
                AyniPlaytestProbe.HitBoss(200f, 999f);
            }
            yield return Wait(3.2f);          // la pantalla del Juicio, a cámara lenta
            AyniInput.Simulate("Mercy", 0.15f);
            yield return Wait(25f);           // gesto de perdón, lluvia, queñual, título y tarjeta
            End();
        }

        /// <summary>Encuadra el grupo de árboles quemados en pie de un obstáculo y los graba renaciendo.</summary>
        private IEnumerator RenacerEnPie()
        {
            yield return Prepare();
            SetHud(false);
            Teleport(OnGround("K4", 0f, -40f), Anchors["K4"].yaw); // Yari fuera de plano
            foreach (string id in new[] { "O5", "O1", "O3" })
            {
                AyniOutcomePreview.ClearReforest();
                yield return Wait(0.5f);

                var trees = new List<Vector2>();
                foreach (AyniBurntTree tree in AyniBurntTree.All)
                {
                    if (tree == null || tree.Fallen) continue;
                    Vector2 l = Local(id, tree.transform.position);
                    if (Mathf.Abs(l.x) < 30f && Mathf.Abs(l.y) < 16f) trees.Add(l);
                }
                int right = 0;
                foreach (Vector2 l in trees) if (l.x > 0f) right++;
                float s = right * 2 >= trees.Count ? 1f : -1f;
                Vector2 c = Vector2.zero;
                int n = 0;
                foreach (Vector2 l in trees)
                {
                    if (l.x * s <= 0f) continue;
                    c += l;
                    n++;
                }
                if (n == 0) continue;
                c /= n;

                Vector3 look = OnGround(id, c.x, c.y, 2.4f);
                Vector3 from0 = OnGround(id, s * 1.5f, c.y - 17f, 1.9f), from1 = OnGround(id, s * 4f, c.y - 12f, 2.2f);
                Begin("renacer_pie_" + id);
                TakeCamera();
                StartCoroutine(Fly(19f, from0, look, from1, look + Vector3.up * 0.5f, 54f, 54f));
                yield return Wait(1.6f);
                AyniOutcomePreview.ReforestAt(Anchors["PLAZA"].center.x, Anchors["PLAZA"].center.y);
                yield return Wait(17.4f);
                End();
            }
            AyniOutcomePreview.ClearReforest();
            ReleaseCamera();
        }

        /// <summary>Los árboles quemados del camino renacen: dos encuadres, antes y después en la misma toma.</summary>
        private IEnumerator Renacer()
        {
            yield return Prepare();
            SetHud(false);
            Teleport(OnGround("K1", 0f, -40f), Anchors["K1"].yaw); // Yari fuera de plano

            // Troncos caídos del cuarto obstáculo
            Begin("renacer_o4");
            TakeCamera();
            StartCoroutine(Fly(19f, Air("O4", 6.5f, 3.4f, -13f), Air("O4", -3f, 0.8f, 1f), Air("O4", 2.5f, 2.6f, -9.5f), Air("O4", -3f, 1.0f, 2f), 50f, 50f));
            yield return Wait(1.6f);
            AyniOutcomePreview.ReforestAt(Anchors["PLAZA"].center.x, Anchors["PLAZA"].center.y);
            yield return Wait(17.4f);
            End();

            // Árboles en pie junto al muro caído: se vuelve a empezar para tener el antes
            AyniOutcomePreview.ClearReforest();
            yield return Wait(0.5f);
            Begin("renacer_o5");
            StartCoroutine(Fly(19f, Air("O5", 3f, 2.2f, -17f), Air("O5", 15f, 2.6f, 2f), Air("O5", 8f, 2.4f, -12f), Air("O5", 16f, 3.0f, 2f), 52f, 52f));
            yield return Wait(1.6f);
            AyniOutcomePreview.ReforestAt(Anchors["PLAZA"].center.x, Anchors["PLAZA"].center.y);
            yield return Wait(17.4f);
            End();
            ReleaseCamera();
        }
    }
}
#endif
