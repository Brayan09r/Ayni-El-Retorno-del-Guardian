#if UNITY_EDITOR
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;

namespace Ayni.Editor
{
    /// <summary>
    /// Sondas para pruebas automáticas en Play desde el puente de agentes (AyniAgentBridge):
    ///   call Ayni.Editor.AyniPlaytestProbe.Report etiqueta
    ///   call Ayni.Editor.AyniPlaytestProbe.TeleportYari x y z
    /// Junto con AyniInput.Simulate / SimulateMove permiten probar la guardia, las esquivas y las caídas sin mando.
    /// </summary>
    public static class AyniPlaytestProbe
    {
        private static readonly string[] KnownStates =
        {
            "Relaxed_Locomotion", "Combat_Locomotion", "Crouch_Locomotion", "LockOn_Locomotion", "Guard_Stance",
            "Avoid_Duck", "Avoid_Jump", "Avoid_SwayL", "Avoid_SwayR", "Sifu_DuckAvoid", "Sifu_JumpAvoid",
            "Fall_Loop", "Fall_Back", "Land_Hard", "Jump", "GetUp", "Defeat_Death", "Impact_Hit", "Impact_HitBody",
            "Impact_HitHeavy", "Guard_BlockHit", "Parry_Deflect", "Mercy_Offer",
            "Atk_Light1", "Atk_Light2", "Atk_Light3", "Atk_Light4", "Atk_Overhand", "Atk_Uppercut", "Atk_Elbow",
            "Atk_Headbutt", "Atk_FrontKick", "Atk_BackKick", "Run_Jump", "BrokenStructure_Stunned"
        };

        public static string Report(string label)
        {
            if (!Application.isPlaying) return "no está en Play";
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return "sin Yari";

            var yari = player.GetComponent<YariCombatController>();
            var animator = player.GetComponentInChildren<Animator>();
            string state = StateName(animator);
            Vector3 p = player.transform.position;

            string text = $"[{label}] Yari pos=({p.x:F2}, {p.y:F2}, {p.z:F2}) rotY={player.transform.eulerAngles.y:F0} estado={state} " +
                          $"guardia={yari.IsGuarding} cayendo={yari.IsFalling} vida={yari.CurrentHealth:F0}/{yari.MaxHealth:F0} " +
                          $"timeScale={Time.timeScale:F2} entradaBloqueada={AyniGameState.InputLocked} mando={AyniInput.UsingGamepad}";

            foreach (var enemy in EnemyController.All)
            {
                if (enemy == null) continue;
                float dist = Vector3.Distance(enemy.transform.position, p);
                text += $"\n      {enemy.CharacterName}: estado={enemy.State} dist={dist:F1} vida={enemy.CurrentHealth:F0} postura={enemy.Structure.CurrentStructure:F0}";
            }
            text += $"\n      sonidos ({AyniAudio.LoadedCount} cargados): {AyniAudio.Recent}";
            Debug.Log("[Ayni Prueba] " + text);
            return state;
        }

        public static void TeleportYari(float x, float y, float z)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var mover = player.GetComponent<CharacterController>();
            if (mover != null) mover.enabled = false;
            player.transform.position = new Vector3(x, y, z);
            if (mover != null) mover.enabled = true;
            Debug.Log($"[Ayni Prueba] Yari teletransportado a ({x}, {y}, {z})");
        }

        /// <summary>Golpea al jefe con el daño indicado (vida y postura) como si fuera un golpe pesado de Yari.</summary>
        public static void HitBoss(float health, float structure)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            foreach (var enemy in EnemyController.All)
            {
                if (enemy == null || !enemy.IsBoss) continue;
                Vector3 from = player != null ? player.transform.position : enemy.transform.position + Vector3.forward;
                enemy.TakeHit(health, structure, from, true, Ayni.Combat.HitReaction.Heavy, 0f);
                Debug.Log($"[Ayni Prueba] Golpe de prueba a {enemy.CharacterName}: vida {enemy.CurrentHealth:F0} ({enemy.HealthRatio:P0}), postura rota={enemy.Structure.IsBroken}, juicio={enemy.CanBeJudged}");
            }
        }

        /// <summary>
        /// Coloca a Yari a <paramref name="distance"/> m del jefe, mirando de modo que el jefe quede a <paramref name="angle"/>
        /// grados de su frente (180 = a la espalda), y pone la cámara detrás de Yari.
        /// </summary>
        public static void PlaceYariNearBoss(float distance, float angle)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            EnemyController boss = null;
            foreach (var e in EnemyController.All) if (e != null && e.IsBoss) boss = e;
            if (player == null || boss == null) return;

            Vector3 bossPos = boss.transform.position;
            Vector3 dirFromBoss = Quaternion.Euler(0f, 37f, 0f) * Vector3.forward;
            Vector3 pos = bossPos + dirFromBoss * distance;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f)) pos.y = hit.point.y + 0.05f;
            Vector3 toBoss = bossPos - pos;
            toBoss.y = 0f;
            Quaternion facing = Quaternion.LookRotation(Quaternion.Euler(0f, -angle, 0f) * toBoss.normalized);

            var mover = player.GetComponent<CharacterController>();
            if (mover != null) mover.enabled = false;
            player.transform.SetPositionAndRotation(pos, facing);
            if (mover != null) mover.enabled = true;

            var cam = Camera.main != null ? Camera.main.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
            if (cam != null) cam.SnapBehindTarget();
            Debug.Log($"[Ayni Prueba] Yari a {distance} m del jefe, con el jefe a {angle}° de su frente");
        }

        /// <summary>Congela (1) o libera (0) la IA de todos los rivales; siguen recibiendo daño.</summary>
        public static void FreezeEnemies(int on)
        {
            foreach (var e in EnemyController.All) if (e != null) e.ExternalControl = on != 0;
            Debug.Log($"[Ayni Prueba] Rivales {(on != 0 ? "congelados" : "liberados")}");
        }

        /// <summary>Pone a Yari en un estado del Animator en el punto indicado (speed 0 = congelado) y mide sus huesos.</summary>
        public static void PlayYariState(string state, float normalizedTime, float speed)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (animator == null) return;
            animator.Play(state, 0, normalizedTime);
            animator.speed = speed;
            animator.Update(0f);
            Bones(state);
        }

        /// <summary>Activa o desactiva el apoyo de pies (FootIK) y la postura andina de Yari para comparar.</summary>
        public static void SetYariHelpers(int footIK, int stance)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var ik = player.GetComponentInChildren<Ayni.Combat.FootIK>();
            if (ik != null) ik.enabled = footIK != 0;
            var st = player.GetComponentInChildren<Ayni.Combat.AndeanCombatStanceModifier>();
            if (st != null) st.enabled = stance != 0;
            Debug.Log($"[Ayni Prueba] FootIK={(ik != null ? ik.enabled.ToString() : "-")} postura andina={(st != null ? st.enabled.ToString() : "-")}");
        }

        public static void Bones(string label)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (animator == null || !animator.isHuman) return;
            Vector3 root = player.transform.position;
            string Y(HumanBodyBones b)
            {
                Transform t = animator.GetBoneTransform(b);
                return t != null ? (t.position.y - root.y).ToString("F2") : "-";
            }
            Debug.Log($"[Ayni Prueba] huesos [{label}] (altura sobre la base) cadera={Y(HumanBodyBones.Hips)} cabeza={Y(HumanBodyBones.Head)} " +
                      $"rodilla izq={Y(HumanBodyBones.LeftLowerLeg)} pie izq={Y(HumanBodyBones.LeftFoot)} pie der={Y(HumanBodyBones.RightFoot)} " +
                      $"animator.speed={animator.speed:F2} estado={StateName(animator)}");
        }

        // ───────────────────────── Grabadora de fotogramas ─────────────────────────

        private static string recName;
        private static int recFrames, recIndex;
        private static float recInterval, recNext;
        private static string recView;
        private static Texture2D recSheet;
        private static Ayni.Player.ThirdPersonSifuCamera recCamScript;
        private static bool recCamWasEnabled;
        private const int RecCols = 6, RecW = 320, RecH = 240;

        /// <summary>
        /// Graba <paramref name="frames"/> imágenes cada <paramref name="interval"/> segundos reales y las junta en
        /// DebugCaptures/rec_NOMBRE.png. view: "juego" (cámara del juego), "lado" o "frente" (cámara fija junto a Yari).
        /// </summary>
        public static void Record(string name, int frames, float interval, string view)
        {
            if (!Application.isPlaying) return;
            StopRecording();
            recName = name;
            recFrames = Mathf.Max(1, frames);
            recInterval = Mathf.Max(0f, interval);
            recIndex = 0;
            recNext = 0f;
            recView = view;
            int rows = Mathf.CeilToInt(recFrames / (float)RecCols);
            recSheet = new Texture2D(RecW * Mathf.Min(RecCols, recFrames), RecH * rows, TextureFormat.RGB24, false);

            Camera cam = Camera.main;
            if (cam != null && view != "juego")
            {
                recCamScript = cam.GetComponent<Ayni.Player.ThirdPersonSifuCamera>();
                recCamWasEnabled = recCamScript != null && recCamScript.enabled;
                if (recCamScript != null) recCamScript.enabled = false;
            }
            UnityEditor.EditorApplication.update += RecordTick;
        }

        private static void RecordTick()
        {
            if (!Application.isPlaying || recSheet == null)
            {
                StopRecording();
                return;
            }
            if (Time.unscaledTime < recNext) return;
            recNext = Time.unscaledTime + recInterval;

            Camera cam = Camera.main;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (cam == null || player == null) return;

            if (recView != "juego")
            {
                Transform t = player.transform;
                Vector3 offset = recView == "frente" ? t.forward * 3.2f + t.right * 0.4f : t.right * 3.4f + t.forward * 0.6f;
                cam.transform.position = t.position + offset + Vector3.up * 1.2f;
                cam.transform.LookAt(t.position + Vector3.up * 0.9f);
            }

            var rt = RenderTexture.GetTemporary(RecW, RecH, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            int col = recIndex % RecCols, row = recIndex / RecCols;
            int rows = recSheet.height / RecH;
            recSheet.ReadPixels(new Rect(0, 0, RecW, RecH), col * RecW, (rows - 1 - row) * RecH);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            recIndex++;
            if (recIndex >= recFrames)
            {
                recSheet.Apply();
                string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "rec_" + recName + ".png"), recSheet.EncodeToPNG());
                Debug.Log($"[Ayni Prueba] Grabación guardada: rec_{recName}.png ({recFrames} fotogramas)");
                StopRecording();
            }
        }

        private static void StopRecording()
        {
            UnityEditor.EditorApplication.update -= RecordTick;
            if (recSheet != null) Object.DestroyImmediate(recSheet);
            recSheet = null;
            if (recCamScript != null)
            {
                recCamScript.enabled = recCamWasEnabled;
                if (recCamWasEnabled) recCamScript.SnapBehindTarget();
            }
            recCamScript = null;
        }

        private static string StateName(Animator animator)
        {
            if (animator == null) return "-";
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            foreach (string s in KnownStates)
            {
                if (info.IsName(s)) return s + $"({info.normalizedTime:F2})";
            }
            return "?";
        }
    }
}
#endif
