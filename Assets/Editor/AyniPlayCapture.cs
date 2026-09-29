#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Herramienta de depuración: al entrar en Play guarda 3 capturas de lo que ve la Main Camera
    /// (a los 1.5 s, 3 s y 5 s) en la carpeta del proyecto: DebugCaptures/play_1.png, play_2.png, play_3.png.
    /// Se puede desactivar desde el menú Ayni > Debug > Capturas al dar Play.
    /// </summary>
    [InitializeOnLoad]
    public static class AyniPlayCapture
    {
        private const string PrefKey = "AyniPlayCaptureEnabled";
        private static readonly float[] CaptureTimes = { 1.5f, 3f, 5f };
        private static double playStart;
        private static int nextIndex;

        static AyniPlayCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Ayni/Debug/Capturas al dar Play")]
        private static void Toggle()
        {
            bool enabled = !EditorPrefs.GetBool(PrefKey, true);
            EditorPrefs.SetBool(PrefKey, enabled);
            Debug.Log($"[Ayni Debug] Capturas al dar Play: {(enabled ? "ACTIVADAS" : "desactivadas")}");
        }

        [MenuItem("Ayni/Debug/Capturas al dar Play", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("Ayni/Debug/Capturas al dar Play", EditorPrefs.GetBool(PrefKey, true));
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PrefKey, true))
            {
                playStart = EditorApplication.timeSinceStartup;
                nextIndex = 0;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (nextIndex >= CaptureTimes.Length) { EditorApplication.update -= Tick; return; }

            if (EditorApplication.timeSinceStartup - playStart >= CaptureTimes[nextIndex])
            {
                Capture($"play_{nextIndex + 1}.png");
                nextIndex++;
            }
        }

        private static void Capture(string fileName)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Ayni Debug] No hay Main Camera para capturar.");
                return;
            }

            // Asegurar que el Canvas tenga asignada la cámara si está en ScreenSpaceCamera
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                canvas.worldCamera = cam;
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
            Object.DestroyImmediate(rt);

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Yari_Hero");
            string info = player != null
                ? $"Yari pos {player.transform.position}, cam pos {cam.transform.position}, cam rot {cam.transform.eulerAngles}, fov {cam.fieldOfView}"
                : "Yari no encontrado";

            if (canvas != null)
            {
                info += $"\n   [Canvas HUD] '{canvas.name}' Modo={canvas.renderMode} Activo={canvas.gameObject.activeInHierarchy}";
            }

            if (player != null)
            {
                if (player.TryGetComponent<Player.YariCombatController>(out var yariCombat))
                {
                    info += $"\n   [Combate Yari] Salud={yariCombat.CurrentHealth:F0}/{yariCombat.MaxHealth:F0}, Postura={(yariCombat.Structure != null ? yariCombat.Structure.CurrentStructure : 0):F0}/{(yariCombat.Structure != null ? yariCombat.Structure.MaxStructure : 100):F0}, Edad={(yariCombat.Talisman != null ? yariCombat.Talisman.CurrentAge : 20)} años, Muertes/Caídas={(yariCombat.Talisman != null ? yariCombat.Talisman.DeathCounter : 0)}";
                }

                foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    info += $"\n   SMR '{smr.name}' activo={smr.gameObject.activeInHierarchy} enabled={smr.enabled} visible={smr.isVisible} " +
                            $"bounds c={smr.bounds.center} size={smr.bounds.size} mesh={(smr.sharedMesh ? smr.sharedMesh.name : "NULL")} " +
                            $"rootBone={(smr.rootBone ? smr.rootBone.name : "NULL")} lossyScale={smr.transform.lossyScale} " +
                            $"mat={(smr.sharedMaterial ? smr.sharedMaterial.name + "/" + smr.sharedMaterial.shader.name : "NULL")}";
                }
                foreach (var anim in player.GetComponentsInChildren<Animator>(true))
                {
                    var hips = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
                    var head = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                    info += $"\n   Animator en '{anim.name}' human={anim.isHuman} avatarValid={(anim.avatar ? anim.avatar.isValid : false)} " +
                            $"controller={(anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "NULL")} " +
                            $"hips={(hips ? hips.position.ToString() : "-")} head={(head ? head.position.ToString() : "-")} " +
                            $"state={(anim.runtimeAnimatorController ? anim.GetCurrentAnimatorClipInfo(0).Length.ToString() + " clips" : "-")}";
                    info += $"\n   VisualRoot localPos={anim.transform.localPosition} localRot={anim.transform.localEulerAngles} localScale={anim.transform.localScale}";
                    foreach (var t in anim.GetComponentsInChildren<Transform>(true))
                    {
                        string n = t.name;
                        if (n.EndsWith("Hips") || n.EndsWith("Spine") || n.EndsWith("Spine1") || n.EndsWith("Spine2") ||
                            n.EndsWith("Neck") || n.EndsWith("Head") || n.EndsWith("LeftUpLeg") || n.EndsWith("LeftFoot") ||
                            n.EndsWith("LeftArm") || n.StartsWith("tripo"))
                        {
                            info += $"\n      {n}: lp={t.localPosition.ToString("F3")} ls={t.localScale.ToString("F3")} wp={t.position.ToString("F2")} lr={t.localEulerAngles.ToString("F0")}";
                        }
                    }
                }
            }

            var enemies = Object.FindObjectsByType<Enemy.EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                info += $"\n   [Rival/Jefe] '{enemy.CharacterName}' Estructura={(enemy.Structure != null ? enemy.Structure.CurrentStructure : 0):F0} PosturaRota={(enemy.Structure != null && enemy.Structure.IsBroken)} Muerto={enemy.IsDead}";
            }

            File.AppendAllText(Path.Combine(dir, "capture_log.txt"), $"{System.DateTime.Now:HH:mm:ss} {fileName}: {info}\n");
            Debug.Log($"[Ayni Debug] Captura guardada: {path} | {info}");
        }
    }
}
#endif
