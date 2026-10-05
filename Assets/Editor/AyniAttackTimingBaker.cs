#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Ayni.Combat;

namespace Ayni.Editor
{
    /// <summary>
    /// Mide en cada clip de ataque el instante real de impacto: reproduce el clip sobre el esqueleto de Yari
    /// y busca el momento en que el hueso que golpea (mano, codo, cabeza o pie) alcanza su máxima extensión hacia delante.
    /// Guarda el resultado en Assets/Resources/YariAttackTimings.asset, que YariCombatController lee en Play.
    /// </summary>
    public static class AyniAttackTimingBaker
    {
        private const string AnimFolder = "Assets/Art/Characters/Animations";
        private const string RigPath = "Assets/Art/Characters/Yari_Rigged.fbx";
        private const string TablePath = "Assets/Resources/" + AttackTimingTable.ResourceName + ".asset";

        private static readonly string[] AttackClips =
        {
            "Light_Punch_1_L", "Light_Punch_2_R", "Light_Punch_3_L", "Light_Punch_4_R",
            "Heavy_Overhand", "Heavy_Uppercut", "Heavy_Elbow", "Heavy_Headbutt", "Heavy_FrontKick"
        };

        private static readonly string[] LocomotionClips =
        {
            "Walk_Forward_InPlace", "Jog_Forward_InPlace", "Run_Forward_InPlace", "Sprint_Run_InPlace", "Crouch_Walk_InPlace"
        };

        [MenuItem("Ayni/Herramientas/Medir Tiempos de Impacto de los Ataques")]
        public static void Bake()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Ayni Timing] No se encontró {RigPath}.");
                return;
            }

            GameObject go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Animator anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RigPath))
                {
                    if (asset is Avatar av) { anim.avatar = av; break; }
                }
            }

            if (!anim.isHuman)
            {
                Debug.LogWarning("[Ayni Timing] Yari_Rigged no tiene un Avatar Humanoid válido; no se midieron los tiempos.");
                Object.DestroyImmediate(go);
                return;
            }

            AttackTimingTable table = AssetDatabase.LoadAssetAtPath<AttackTimingTable>(TablePath);
            if (table == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                table = ScriptableObject.CreateInstance<AttackTimingTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }
            table.entries.Clear();
            table.locomotion.Clear();

            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            string report = "";

            bool startedMode = !AnimationMode.InAnimationMode();
            if (startedMode) AnimationMode.StartAnimationMode();
            try
            {
                foreach (string clipName in AttackClips)
                {
                    AnimationClip clip = LoadClip($"{AnimFolder}/{clipName}.fbx");
                    var entry = new AttackTimingTable.Entry { clipName = clipName };
                    if (clip == null)
                    {
                        report += $"\n   {clipName}: CLIP NO ENCONTRADO";
                        table.entries.Add(entry);
                        continue;
                    }
                    entry.length = clip.length;

                    // 1er intento: modo de animación del Editor. 2º intento: muestreo directo del clip.
                    if (!Measure(go, anim, head, clip, clipName, true, entry))
                    {
                        Measure(go, anim, head, clip, clipName, false, entry);
                    }

                    report += entry.contactTime > 0f
                        ? $"\n   {clipName}: duración {entry.length:F2} s, impacto {entry.contactTime:F2} s ({entry.strikingBone})"
                        : $"\n   {clipName}: duración {entry.length:F2} s, impacto NO MEDIDO";
                    table.entries.Add(entry);
                }
            }
            finally
            {
                if (startedMode) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }

            foreach (string clipName in LocomotionClips)
            {
                float speed = MeasureGroundSpeed(LoadClip($"{AnimFolder}/{clipName}.fbx"));
                table.locomotion.Add(new AttackTimingTable.LocomotionEntry { clipName = clipName, groundSpeed = speed });
                report += speed > 0f
                    ? $"\n   {clipName}: velocidad natural {speed:F2} m/s"
                    : $"\n   {clipName}: velocidad natural NO MEDIDA";
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "attack_timings.json"), JsonUtility.ToJson(table, true));

            Debug.Log($"<color=green>[Ayni Timing]</color> Tiempos de impacto guardados en {TablePath}:{report}");
        }

        // ───────────────────────── Salto ─────────────────────────

        /// <summary>
        /// Prepara el clip de salto: mide cuándo despegan y aterrizan los pies y deja la altura del salto fuera de la pose,
        /// para que la física del juego sea la única que eleva a Yari (si no, subía dos veces: por el clip y por la física).
        /// </summary>
        public static void SetupJumpClip()
        {
            string path = $"{AnimFolder}/Jump.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            // 1. Con la altura dentro de la pose se puede medir el despegue y el aterrizaje
            SetBakeHeightIntoPose(importer, true);
            AnimationClip clip = LoadClip(path);
            float takeoff = 0f, land = 0f, length = clip != null ? clip.length : 0f;
            bool measured = clip != null && MeasureJump(clip, out takeoff, out land);

            // 2. La altura sale de la pose: en el juego la pone la física
            SetBakeHeightIntoPose(importer, false);

            AttackTimingTable table = LoadOrCreateTable();
            if (measured)
            {
                table.jumpTakeoff = takeoff;
                table.jumpLand = land;
                table.jumpLength = length;
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[Ayni Timing]</color> Salto: clip de {length:F2} s, despega en {takeoff:F2} s y aterriza en {land:F2} s.");
            }
            else
            {
                Debug.LogWarning("[Ayni Timing] No se pudo medir el clip de salto; se conservan los valores anteriores.");
            }
        }

        private static void SetBakeHeightIntoPose(ModelImporter importer, bool bake)
        {
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            bool changed = false;
            foreach (var c in clips)
            {
                if (c.lockRootHeightY != bake) { c.lockRootHeightY = bake; changed = true; }
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static AttackTimingTable LoadOrCreateTable()
        {
            AttackTimingTable table = AssetDatabase.LoadAssetAtPath<AttackTimingTable>(TablePath);
            if (table == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                table = ScriptableObject.CreateInstance<AttackTimingTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }
            return table;
        }

        /// <summary>Busca en el clip el tramo en el que los dos pies están en el aire.</summary>
        private static bool MeasureJump(AnimationClip clip, out float takeoff, out float land)
        {
            takeoff = 0f;
            land = 0f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (prefab == null) return false;

            GameObject go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Animator anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RigPath))
                {
                    if (asset is Avatar av) { anim.avatar = av; break; }
                }
            }

            bool ok = false;
            bool startedMode = !AnimationMode.InAnimationMode();
            if (startedMode) AnimationMode.StartAnimationMode();
            try
            {
                if (anim.isHuman)
                {
                    ok = JumpPass(go, anim, clip, true, out takeoff, out land);
                    if (!ok) ok = JumpPass(go, anim, clip, false, out takeoff, out land);
                }
            }
            finally
            {
                if (startedMode) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
            return ok;
        }

        private static bool JumpPass(GameObject go, Animator anim, AnimationClip clip, bool useAnimationMode,
            out float takeoff, out float land)
        {
            takeoff = 0f;
            land = 0f;

            Transform leftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null) return false;

            const float step = 1f / 60f;
            int samples = Mathf.Max(8, Mathf.CeilToInt(clip.length / step));
            var lowestFoot = new float[samples];
            float floor = float.MaxValue, peak = float.MinValue;
            for (int i = 0; i < samples; i++)
            {
                Sample(go, clip, Mathf.Min(i * step, clip.length), useAnimationMode);
                float y = Mathf.Min(go.transform.InverseTransformPoint(leftFoot.position).y,
                                    go.transform.InverseTransformPoint(rightFoot.position).y);
                lowestFoot[i] = y;
                floor = Mathf.Min(floor, y);
                peak = Mathf.Max(peak, y);
            }

            float rise = peak - floor;
            if (rise < 0.08f) return false; // los pies no llegan a separarse del suelo: no hay nada que medir

            float airborneAbove = floor + Mathf.Max(rise * 0.25f, 0.05f);
            int first = -1, last = -1;
            for (int i = 0; i < samples; i++)
            {
                if (lowestFoot[i] > airborneAbove)
                {
                    if (first < 0) first = i;
                    last = i;
                }
            }
            if (first < 0 || last <= first) return false;

            takeoff = Mathf.Round(first * step * 100f) / 100f;
            land = Mathf.Round(Mathf.Min((last + 1) * step, clip.length) * 100f) / 100f;
            return land - takeoff > 0.1f;
        }

        /// <summary>
        /// Velocidad sobre el suelo (m/s) a la que un clip "en el sitio" no patina: mientras un pie está apoyado
        /// se desliza hacia atrás justo a la velocidad a la que el personaje debería avanzar. Devuelve 0 si no se pudo medir.
        /// </summary>
        public static float MeasureGroundSpeed(AnimationClip clip)
        {
            if (clip == null) return 0f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (prefab == null) return 0f;

            GameObject go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Animator anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RigPath))
                {
                    if (asset is Avatar av) { anim.avatar = av; break; }
                }
            }

            float result = 0f;
            bool startedMode = !AnimationMode.InAnimationMode();
            if (startedMode) AnimationMode.StartAnimationMode();
            try
            {
                if (anim.isHuman)
                {
                    result = GroundSpeedPass(go, anim, clip, true);
                    if (result <= 0f) result = GroundSpeedPass(go, anim, clip, false);
                }
            }
            finally
            {
                if (startedMode) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
            return result;
        }

        private static float GroundSpeedPass(GameObject go, Animator anim, AnimationClip clip, bool useAnimationMode)
        {
            const float step = 1f / 120f;
            int samples = Mathf.Max(8, Mathf.CeilToInt(clip.length / step));
            var speeds = new System.Collections.Generic.List<float>();

            foreach (HumanBodyBones boneId in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Transform foot = anim.GetBoneTransform(boneId);
                if (foot == null) continue;

                var y = new float[samples];
                var z = new float[samples];
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < samples; i++)
                {
                    Sample(go, clip, Mathf.Min(i * step, clip.length), useAnimationMode);
                    Vector3 p = go.transform.InverseTransformPoint(foot.position);
                    y[i] = p.y;
                    z[i] = p.z;
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                }

                // Pie apoyado = en el 10 % más bajo de su recorrido vertical. Con el 20 % entraban, al correr, los
                // instantes en que el pie aún está posándose o ya despega, y la velocidad salía hasta un 20 % baja.
                float plantedBelow = minY + (maxY - minY) * 0.1f;
                for (int i = 0; i < samples - 1; i++)
                {
                    if (y[i] <= plantedBelow && y[i + 1] <= plantedBelow)
                    {
                        float backwardSpeed = -(z[i + 1] - z[i]) / step;
                        if (backwardSpeed > 0.05f) speeds.Add(backwardSpeed);
                    }
                }
            }

            if (speeds.Count < 4) return 0f;
            speeds.Sort();
            float median = speeds[speeds.Count / 2];
            return (median > 0.3f && median < 12f) ? Mathf.Round(median * 100f) / 100f : 0f;
        }

        private static bool Measure(GameObject go, Animator anim, Transform head, AnimationClip clip, string clipName,
            bool useAnimationMode, AttackTimingTable.Entry entry)
        {
            HumanBodyBones[] candidates;
            if (clipName.Contains("Headbutt")) candidates = new[] { HumanBodyBones.Head };
            else if (clipName.Contains("Kick")) candidates = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            else if (clipName.Contains("Elbow")) candidates = new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm };
            else candidates = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand };

            float upWeight = clipName.Contains("Uppercut") ? 1f : 0f; // el gancho ascendente golpea hacia arriba

            Sample(go, clip, 0f, useAnimationMode);
            float modelHeight = head != null ? Mathf.Max(0.01f, go.transform.InverseTransformPoint(head.position).y) : 1.6f;

            float bestExcursion = 0f;
            float bestTime = -1f;
            string bestBone = "";

            const float step = 1f / 60f;
            float searchEnd = clip.length * 0.8f;

            foreach (HumanBodyBones boneId in candidates)
            {
                Transform bone = anim.GetBoneTransform(boneId);
                if (bone == null) continue;

                Sample(go, clip, 0f, useAnimationMode);
                float baseValue = Reach(go, bone, upWeight);

                for (float t = step; t <= searchEnd; t += step)
                {
                    Sample(go, clip, t, useAnimationMode);
                    float excursion = Reach(go, bone, upWeight) - baseValue;
                    if (excursion > bestExcursion)
                    {
                        bestExcursion = excursion;
                        bestTime = t;
                        bestBone = boneId.ToString();
                    }
                }
            }

            // Si el hueso apenas se movió, el muestreo no funcionó: no dar por bueno el resultado
            if (bestTime <= 0f || bestExcursion < modelHeight * 0.06f) return false;

            entry.contactTime = Mathf.Round(bestTime * 100f) / 100f;
            entry.strikingBone = bestBone;
            return true;
        }

        private static float Reach(GameObject go, Transform bone, float upWeight)
        {
            Vector3 local = go.transform.InverseTransformPoint(bone.position);
            return local.z + local.y * upWeight;
        }

        private static void Sample(GameObject go, AnimationClip clip, float time, bool useAnimationMode)
        {
            if (useAnimationMode)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, clip, time);
                AnimationMode.EndSampling();
            }
            else
            {
                clip.SampleAnimation(go, time);
            }
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }
    }
}
#endif
