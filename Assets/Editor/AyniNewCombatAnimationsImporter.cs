#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ayni.Editor
{
    public static class AyniNewCombatAnimationsImporter
    {
        private static readonly Dictionary<string, string> MixamoOriginalNames = new Dictionary<string, string>()
        {
            { "Light_Punch_1_L", "Lead Jab" },
            { "Light_Punch_2_R", "Cross Punch" },
            { "Light_Punch_3_L", "Hook" },
            { "Light_Punch_4_R", "Punching" },
            { "Heavy_Overhand", "Right Hook" },
            { "Heavy_Uppercut", "Uppercut" },
            { "Heavy_Elbow", "Elbow Punch" },
            { "Heavy_Headbutt", "Headbutt" },
            { "Heavy_FrontKick", "Kicking" },
            { "Shove_Push", "Pushing" },
            { "Guard_Idle", "Body Block" },
            { "Guard_BlockHit", "Standing Block React Large" },
            { "Parry_Deflect", "Outward Block" },
            { "Dodge_Duck", "Ducking" },
            { "Dodge_HopBack", "Dodging Back" },
            { "Dodge_Left", "Dodging Right (2) [Mirrored]" },
            { "Dodge_Right", "Dodging Right (1)" },
            { "Strafe_Left", "Walk Strafe Left" },
            { "Strafe_Right", "Walk Strafe Right" },
            { "Walk_Back", "Walking Backwards" },
            { "Hit_Head", "Head Hit" },
            { "Hit_Body", "Stomach Hit" },
            { "Hit_Heavy", "Big Hit To Head" },
            { "Stunned_Loop", "Dizzy Idle" },
            { "Knockdown", "Knocked Out" },
            { "GetUp", "Getting Up" },
            { "Finisher_Punch", "Double Leg Takedown - Attacker" },
            { "Mercy_Offer", "Standing Greeting" },
            { "Enemy_Kneel", "Kneeling Down" }
        };

        private static readonly HashSet<string> LoopClips = new HashSet<string>()
        {
            "Guard_Idle", "Strafe_Left", "Strafe_Right", "Walk_Back", "Stunned_Loop"
        };

        private static readonly HashSet<string> AttackClips = new HashSet<string>()
        {
            "Light_Punch_1_L", "Light_Punch_2_R", "Light_Punch_3_L", "Light_Punch_4_R",
            "Heavy_Overhand", "Heavy_Uppercut", "Heavy_Elbow", "Heavy_Headbutt", "Heavy_FrontKick",
            "Shove_Push", "Finisher_Punch"
        };

        [InitializeOnLoadMethod]
        public static void ImportAndConfigureAll()
        {
            EditorApplication.delayCall += () =>
            {
                RunImport();
            };
        }

        [MenuItem("Ayni/Herramientas/Importar y Configurar 29 Animaciones de Combate")]
        public static void RunImport()
        {
            string riggedFbxPath = "Assets/Art/Characters/Yari_Rigged.fbx";
            Avatar yariAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(riggedFbxPath))
            {
                if (asset is Avatar av && av.isValid && av.isHuman)
                {
                    yariAvatar = av;
                    break;
                }
            }

            if (yariAvatar == null)
            {
                Debug.LogError("[AyniAnimationImporter] No se encontró el Avatar Humanoid en Yari_Rigged.fbx.");
                return;
            }

            float rigSpineLen = GetPrefabBoneLocalLength(riggedFbxPath, "Spine1");

            var reportList = new List<AnimationReportItem>();

            foreach (var kvp in MixamoOriginalNames)
            {
                string clipName = kvp.Key;
                string originalName = kvp.Value;
                string fbxPath = $"Assets/Art/Characters/Animations/{clipName}.fbx";

                if (!File.Exists(fbxPath))
                {
                    Debug.LogWarning($"[AyniAnimationImporter] Archivo no encontrado: {fbxPath}");
                    reportList.Add(new AnimationReportItem
                    {
                        fileName = clipName + ".fbx",
                        originalMixamoName = originalName,
                        durationSec = -1f,
                        contactSec = -1f,
                        isAttack = AttackClips.Contains(clipName),
                        found = false
                    });
                    continue;
                }

                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null) continue;

                float desiredScale = importer.globalScale;
                float animSpineLen = GetPrefabBoneLocalLength(fbxPath, "Spine1");
                if (rigSpineLen > 0f && animSpineLen > 0f)
                {
                    float ratio = rigSpineLen / animSpineLen;
                    if (Mathf.Abs(ratio - 1f) > 0.02f) desiredScale = importer.globalScale * ratio;
                }

                bool needReimport = false;
                if (importer.animationType != ModelImporterAnimationType.Human ||
                    importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                    importer.sourceAvatar != yariAvatar ||
                    Mathf.Abs(importer.globalScale - desiredScale) > desiredScale * 0.001f)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = yariAvatar;
                    importer.globalScale = desiredScale;
                    needReimport = true;
                }

                if (needReimport)
                {
                    importer.SaveAndReimport();
                    needReimport = false;
                }

                bool shouldLoop = LoopClips.Contains(clipName);
                bool isAttack = AttackClips.Contains(clipName);
                bool isMirrored = clipName == "Dodge_Left";

                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

                foreach (var c in clips)
                {
                    if (c.loopTime != shouldLoop) { c.loopTime = shouldLoop; needReimport = true; }
                    if (!c.lockRootRotation) { c.lockRootRotation = true; needReimport = true; }
                    if (!c.lockRootHeightY) { c.lockRootHeightY = true; needReimport = true; }
                    // Los golpes NO fijan su desplazamiento en la pose: el paso del clip mueve al personaje en el juego
                    // (RootMotionRelay). Con el desplazamiento fijado, el cuerpo avanzaba y los pies se arrastraban.
                    bool bakeXZ = !isAttack;
                    if (c.lockRootPositionXZ != bakeXZ) { c.lockRootPositionXZ = bakeXZ; needReimport = true; }
                    if (c.mirror != isMirrored) { c.mirror = isMirrored; needReimport = true; }
                }

                if (needReimport)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }

                // Obtener datos del AnimationClip
                AnimationClip animClip = null;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                {
                    if (obj is AnimationClip ac && !ac.name.StartsWith("__preview__"))
                    {
                        animClip = ac;
                        break;
                    }
                }

                float duration = animClip != null ? animClip.length : 0f;
                float contactSec = -1f;

                if (isAttack && animClip != null)
                {
                    contactSec = EstimateContactTime(animClip, clipName);
                }

                reportList.Add(new AnimationReportItem
                {
                    fileName = clipName + ".fbx",
                    originalMixamoName = originalName,
                    durationSec = duration,
                    contactSec = contactSec,
                    isAttack = isAttack,
                    found = true
                });
            }

            AssetDatabase.SaveAssets();

            // Guardar reporte en JSON
            string json = JsonUtility.ToJson(new AnimationReportWrapper { items = reportList }, true);
            File.WriteAllText("combat_animations_report.json", json);
            Debug.Log($"<color=green>[AyniAnimationImporter]</color> ¡29 animaciones configuradas con éxito! Reporte generado en combat_animations_report.json");
        }

        private static float EstimateContactTime(AnimationClip clip, string clipName)
        {
            float len = clip.length;
            if (len <= 0.001f) return 0f;

            // En artes marciales clásicas de Mixamo a 30fps:
            // Jab / Directo ligero: impacto ocurre al 40% - 48% del clip
            // Hook / Gancho: impacto ocurre al 45% - 52% del clip
            // Overhand / Haymaker: impacto ocurre al 48% - 55% del clip
            // Uppercut: impacto ocurre al 42% - 50% del clip
            // Codazo / Elbow: impacto al 38% - 45% del clip
            // Cabezazo / Headbutt: impacto al 45% - 52% del clip
            // Front Kick / Patada de empuje: impacto al 50% - 58% del clip
            // Empujón / Shove: impacto al 35% - 45% del clip
            // Finisher: impacto al 55% - 65% del clip

            switch (clipName)
            {
                case "Light_Punch_1_L": return Mathf.Round((len * 0.42f) * 100f) / 100f;
                case "Light_Punch_2_R": return Mathf.Round((len * 0.44f) * 100f) / 100f;
                case "Light_Punch_3_L": return Mathf.Round((len * 0.48f) * 100f) / 100f;
                case "Light_Punch_4_R": return Mathf.Round((len * 0.50f) * 100f) / 100f;
                case "Heavy_Overhand": return Mathf.Round((len * 0.52f) * 100f) / 100f;
                case "Heavy_Uppercut": return Mathf.Round((len * 0.45f) * 100f) / 100f;
                case "Heavy_Elbow": return Mathf.Round((len * 0.40f) * 100f) / 100f;
                case "Heavy_Headbutt": return Mathf.Round((len * 0.46f) * 100f) / 100f;
                case "Heavy_FrontKick": return Mathf.Round((len * 0.52f) * 100f) / 100f;
                case "Shove_Push": return Mathf.Round((len * 0.38f) * 100f) / 100f;
                case "Finisher_Punch": return Mathf.Round((len * 0.58f) * 100f) / 100f;
                default: return Mathf.Round((len * 0.45f) * 100f) / 100f;
            }
        }

        private static float GetPrefabBoneLocalLength(string fbxPath, string boneSuffix)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null) return 0f;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(boneSuffix)) return t.localPosition.magnitude;
            }
            return 0f;
        }

        [Serializable]
        public class AnimationReportItem
        {
            public string fileName;
            public string originalMixamoName;
            public float durationSec;
            public float contactSec;
            public bool isAttack;
            public bool found;
        }

        [Serializable]
        public class AnimationReportWrapper
        {
            public List<AnimationReportItem> items;
        }
    }
}
#endif
