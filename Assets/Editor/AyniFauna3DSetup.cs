#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Configura e integra los 4 modelos 3D animados de fauna andina (Llama, Vicuña, Cóndor y Vizcacha):
    ///  1. Importa cada FBX como Generic con sus clips de animación en bucle y nombres limpios.
    ///  2. Crea materiales URP Lit PBR con sus texturas 4K/2K (Diffuse + Normal).
    ///  3. Crea un AnimatorController por especie con todos sus estados (Idle, Walk, Run, Graze / Glide, Fly_Flap, Idle_Perch / Idle, Hop, Alert).
    ///  4. Genera los Prefabs en Assets/Resources/Fauna/ para que AyniCamelidModel, AyniCondorModel, AyniCritterModel,
    ///     AyniStaticLlamaSwap, AyniCaptiveCages y AyniFaunaSetup los usen automáticamente.
    /// </summary>
    public static class AyniFauna3DSetup
    {
        private const string FaunaArtDir = "Assets/Art/Fauna";
        private const string FaunaTexDir = "Assets/Art/Fauna/Textures";
        private const string FaunaResDir = "Assets/Resources/Fauna";

        private struct SpeciesConfig
        {
            public string key;          // Llama, Vicuna, Condor, Vizcacha
            public string fbxName;      // Llama_Andina_Rigged_Animated.fbx
            public string texPrefix;    // Llama_Andina
            public string defaultState; // Idle / Idle_Perch
            public string[] states;
            public float smoothness;
        }

        private static readonly SpeciesConfig[] Species =
        {
            new SpeciesConfig
            {
                key = "Llama",
                fbxName = "Llama_Andina_Rigged_Animated.fbx",
                texPrefix = "Llama_Andina",
                defaultState = "Idle",
                states = new[] { "Idle", "Walk", "Run", "Graze" },
                smoothness = 0.22f
            },
            new SpeciesConfig
            {
                key = "Vicuna",
                fbxName = "Vicuna_Andina_Rigged_Animated.fbx",
                texPrefix = "Vicuna_Andina",
                defaultState = "Idle",
                states = new[] { "Idle", "Walk", "Run", "Graze" },
                smoothness = 0.25f
            },
            new SpeciesConfig
            {
                key = "Condor",
                fbxName = "Condor_Andino_Rigged_Animated.fbx",
                texPrefix = "Condor_Andino",
                defaultState = "Idle_Perch",
                states = new[] { "Idle_Perch", "Glide", "Fly_Flap" },
                smoothness = 0.30f
            },
            new SpeciesConfig
            {
                key = "Vizcacha",
                fbxName = "Vizcacha_Andina_Rigged_Animated.fbx",
                texPrefix = "Vizcacha_Andina",
                defaultState = "Idle",
                states = new[] { "Idle", "Hop", "Alert" },
                smoothness = 0.20f
            }
        };

        [MenuItem("Ayni/Fauna Andina/Configurar Modelos 3D Animados (Llama, Vicuña, Cóndor, Vizcacha)")]
        public static void SetupAllFauna3D()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AyniFauna3D] Sal del modo Play antes de configurar la fauna 3D.");
                return;
            }

            Directory.CreateDirectory(FaunaResDir);

            foreach (var sp in Species)
            {
                SetupSpecies(sp);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Regenerar los animales colocados en la escena activa para que usen los nuevos prefabs 3D
            int condors = AyniFaunaSetup.SpawnCondors();
            int herds = AyniFaunaSetup.SpawnHerds();
            int small = AyniFaunaSetup.SpawnSmallFauna();
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"<color=green>[AyniFauna3D]</color> ¡Fauna 3D animada configurada e integrada en escena! ({condors} cóndores, {herds} camélidos, {small} vizcachas y zorros).");
        }

        private static void SetupSpecies(SpeciesConfig sp)
        {
            string fbxPath = $"{FaunaArtDir}/{sp.fbxName}";
            if (!File.Exists(fbxPath))
            {
                Debug.LogError($"[AyniFauna3D] No se encontró {fbxPath}");
                return;
            }

            // 1. Configurar FBX como Generic con clips limpios y en bucle
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            ModelImporter imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp != null)
            {
                imp.useFileUnits = true;
                imp.globalScale = 1.0f;
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                imp.SaveAndReimport();

                ModelImporterClipAnimation[] defaults = imp.defaultClipAnimations;
                if (defaults != null && defaults.Length > 0)
                {
                    var clips = new ModelImporterClipAnimation[defaults.Length];
                    for (int i = 0; i < defaults.Length; i++)
                    {
                        var c = defaults[i];
                        // Quitar prefijo "Armature|" para nombres limpios: Idle, Walk, Run, Graze, Fly_Flap, Glide, Hop...
                        int bar = c.name.LastIndexOf('|');
                        if (bar >= 0 && bar + 1 < c.name.Length)
                        {
                            c.name = c.name.Substring(bar + 1);
                        }
                        c.loopTime = true;
                        c.loopPose = true;
                        clips[i] = c;
                    }
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                }
            }

            // 2. Material URP Lit PBR
            Material mat = CreateOrUpdateMaterial(sp);

            // 3. AnimatorController con todos los clips del FBX
            AnimatorController controller = CreateOrUpdateController(sp, fbxPath);

            // 4. Prefab en Assets/Resources/Fauna/Fauna_<Key>.prefab
            CreatePrefab(sp, fbxPath, mat, controller);
        }

        private static Material CreateOrUpdateMaterial(SpeciesConfig sp)
        {
            string matPath = $"{FaunaArtDir}/M_Fauna_{sp.key}_PBR.mat";
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(litShader);
                mat.name = $"M_Fauna_{sp.key}_PBR";
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = litShader;
            }

            string diffPath = $"{FaunaTexDir}/{sp.texPrefix}_Diffuse.png";
            Texture2D diffTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
            if (diffTex != null)
            {
                mat.SetTexture("_BaseMap", diffTex);
                mat.SetTexture("_MainTex", diffTex);
                mat.SetColor("_BaseColor", Color.white);
            }

            string normPath = $"{FaunaTexDir}/{sp.texPrefix}_Normal.png";
            if (File.Exists(normPath))
            {
                TextureImporter nImp = AssetImporter.GetAtPath(normPath) as TextureImporter;
                if (nImp != null && nImp.textureType != TextureImporterType.NormalMap)
                {
                    nImp.textureType = TextureImporterType.NormalMap;
                    nImp.SaveAndReimport();
                }
                Texture2D normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
                if (normTex != null)
                {
                    mat.SetTexture("_BumpMap", normTex);
                    mat.SetFloat("_BumpScale", 0.85f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            mat.SetFloat("_WorkflowMode", 1f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", sp.smoothness);
            if (sp.key == "Condor")
            {
                // Plumas delgadas visibles desde ambos lados (arriba y abajo en vuelo)
                mat.SetFloat("_Cull", 0f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AnimatorController CreateOrUpdateController(SpeciesConfig sp, string fbxPath)
        {
            string ctrlPath = $"{FaunaArtDir}/AC_Fauna_{sp.key}.controller";
            if (File.Exists(ctrlPath)) AssetDatabase.DeleteAsset(ctrlPath);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            AnimationClip[] clips = Array.FindAll(
                Array.ConvertAll(AssetDatabase.LoadAllAssetsAtPath(fbxPath), a => a as AnimationClip),
                c => c != null && !c.name.StartsWith("__preview__"));

            AnimatorState defaultSt = null;
            foreach (string stateName in sp.states)
            {
                AnimationClip match = null;
                foreach (var c in clips)
                {
                    if (string.Equals(c.name, stateName, StringComparison.OrdinalIgnoreCase) ||
                        c.name.EndsWith("|" + stateName, StringComparison.OrdinalIgnoreCase))
                    {
                        match = c;
                        break;
                    }
                }

                AnimatorState st = sm.AddState(stateName);
                st.motion = match;
                if (stateName == sp.defaultState) defaultSt = st;
            }

            if (defaultSt != null) sm.defaultState = defaultSt;
            EditorUtility.SetDirty(ctrl);
            Debug.Log($"[AyniFauna3D] Controller {ctrlPath} creado con {clips.Length} clips.");
            return ctrl;
        }

        private static void CreatePrefab(SpeciesConfig sp, string fbxPath, Material mat, AnimatorController ctrl)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) return;

            GameObject go = UnityEngine.Object.Instantiate(fbx);
            go.name = $"Fauna_{sp.key}";

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = sp.key != "Condor";
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }
            }

            // Sin colisionadores decorativos
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            Animator anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is Avatar av && av.isValid)
                {
                    anim.avatar = av;
                    break;
                }
            }

            string prefabPath = $"{FaunaResDir}/Fauna_{sp.key}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"<color=green>[AyniFauna3D]</color> Prefab guardado en {prefabPath}.");
        }
    }
}
#endif
