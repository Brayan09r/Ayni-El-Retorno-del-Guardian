#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Ayni.Core;
using Ayni.Combat;
using Ayni.Player;

namespace Ayni.Editor
{
    /// <summary>
    /// Configuración e integración del modelo 3D de Yari Maduro (+10 años):
    ///  1. Configura el importador de Yari_Aged_Rigged.fbx como Humanoid con Avatar.
    ///  2. Ajusta la escala para igualar la talla del Yari original.
    ///  3. Configura el Material URP M_Yari_Aged_PBR.mat.
    ///  4. Crea el Prefab Assets/Prefabs/Yari_Aged.prefab con Animator y AndeanCombatStanceModifier.
    ///  5. Integra Visual_Yari_Aged_3D y AyniAgingSystem en Yari_Hero dentro de la escena activa.
    /// </summary>
    public static class AyniYariAgedSetup
    {
        public const string YoungFbxPath = "Assets/Art/Characters/Yari_Rigged.fbx";
        public const string AgedFbxPath = "Assets/Art/Characters/Yari_Aged_Rigged.fbx";
        public const string AgedMatPath = "Assets/Art/Characters/M_Yari_Aged_PBR.mat";
        public const string YoungMatPath = "Assets/Art/Characters/M_Yari_PBR.mat";
        public const string ControllerPath = "Assets/Art/Characters/Yari_AnimatorController.controller";
        public const string PrefabPath = "Assets/Prefabs/Yari_Aged.prefab";

        [MenuItem("Ayni/Personajes/Configurar Yari Maduro (Humanoid + Material)")]
        public static void SetupYariAged()
        {
            if (!File.Exists(AgedFbxPath))
            {
                Debug.LogError($"[AyniYariAged] No se encontró {AgedFbxPath}.");
                return;
            }

            // 1. Configurar Humanoid
            AssetDatabase.ImportAsset(AgedFbxPath, ImportAssetOptions.ForceUpdate);
            ModelImporter imp = AssetImporter.GetAtPath(AgedFbxPath) as ModelImporter;
            if (imp != null)
            {
                // Resetear animación para limpiar cualquier mapeo previo
                imp.animationType = ModelImporterAnimationType.None;
                imp.SaveAndReimport();

                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();
                Debug.Log("<color=green>[AyniYariAged]</color> Yari_Aged_Rigged.fbx configurado como Humanoid con UVs.");
                FixAgedScale(imp);
            }

            // 2. Crear material PBR
            CreateOrUpdateAgedMaterial();

            // 3. Crear o actualizar Prefab de Yari Maduro con Animator y Avatar
            CreateAgedPrefab();

            // 4. Integrar en todas las escenas principales (Yari_Hero)
            IntegrateAllScenes();

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[AyniYariAged]</color> ¡Yari Maduro configurado e integrado con éxito en todas las escenas!");
        }

        public static void IntegrateAllScenes()
        {
            string currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            IntegrateIntoScene();

            string demoScene = "Assets/Scenes/Ayni_Sifu_DemoScene.unity";
            if (File.Exists(demoScene) && currentScene != demoScene)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(demoScene);
                IntegrateIntoScene();
                if (!string.IsNullOrEmpty(currentScene))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(currentScene);
                }
            }
        }

        private static void FixAgedScale(ModelImporter imp)
        {
            float youngHeight = MeasureHeight(YoungFbxPath);
            float agedHeight = MeasureHeight(AgedFbxPath);

            if (youngHeight > 0.01f && agedHeight > 0.001f)
            {
                float ratio = youngHeight / agedHeight;
                if (Mathf.Abs(ratio - 1f) > 0.02f)
                {
                    imp.globalScale *= ratio;
                    imp.SaveAndReimport();
                    Debug.Log($"[AyniYariAged] Escala corregida: {imp.globalScale:0.###} (Altura {youngHeight:0.00}m)");
                }
            }
        }

        private static float MeasureHeight(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return 0f;
            var temp = UnityEngine.Object.Instantiate(go);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.position = Vector3.zero;

            Bounds b = default;
            bool init = false;
            foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
            {
                if (!init) { b = r.bounds; init = true; }
                else b.Encapsulate(r.bounds);
            }
            UnityEngine.Object.DestroyImmediate(temp);
            return init ? b.size.y : 0f;
        }

        private static void CreateOrUpdateAgedMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AgedMatPath);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(litShader);
                mat.name = "M_Yari_Aged_PBR";
                AssetDatabase.CreateAsset(mat, AgedMatPath);
            }
            else
            {
                mat.shader = litShader;
            }

            // 1. Textura Base (Diffuse / Albedo)
            string diffusePath = "Assets/Art/Characters/Textures/Yari_Aged_Diffuse.png";
            Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuseTex != null)
            {
                mat.SetTexture("_BaseMap", diffuseTex);
                mat.SetTexture("_MainTex", diffuseTex);
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                mat.SetColor("_BaseColor", new Color(0.85f, 0.70f, 0.55f, 1f));
            }

            // 2. Mapa de Normales
            string normalPath = "Assets/Art/Characters/Textures/Yari_Aged_Normal.png";
            if (File.Exists(normalPath))
            {
                TextureImporter normImp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (normImp != null && normImp.textureType != TextureImporterType.NormalMap)
                {
                    normImp.textureType = TextureImporterType.NormalMap;
                    normImp.SaveAndReimport();
                }
                Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normalTex != null)
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            mat.SetFloat("_WorkflowMode", 1f); // Metallic
            mat.SetFloat("_Metallic", 0.05f);
            mat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(mat);
        }

        private static void CreateAgedPrefab()
        {
            string dir = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(AgedFbxPath);
            if (fbx == null) return;

            var go = UnityEngine.Object.Instantiate(fbx);
            go.name = "Visual_Yari_Aged_3D";

            Material agedMat = AssetDatabase.LoadAssetAtPath<Material>(AgedMatPath);
            if (agedMat != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = agedMat;
                }
            }

            // Configurar Animator
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller != null) anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Extraer y asignar Avatar Humanoid
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AgedFbxPath))
            {
                if (asset is Avatar av && av.isHuman)
                {
                    anim.avatar = av;
                    break;
                }
            }

            if (go.GetComponent<AndeanCombatStanceModifier>() == null)
            {
                go.AddComponent<AndeanCombatStanceModifier>();
            }

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[AyniYariAged] Prefab creado en {PrefabPath}.");
        }

        /// <summary>
        /// Inserta Visual_Yari_Aged_3D y configura AyniAgingSystem en Yari_Hero de la escena activa.
        /// </summary>
        public static void IntegrateIntoScene()
        {
            GameObject player = GameObject.Find("Yari_Hero");
            if (player == null)
            {
                Debug.LogWarning("[AyniYariAged] No se encontró Yari_Hero en la escena activa.");
                return;
            }

            Transform youngVisual = player.transform.Find("Visual_Yari_3D");
            if (youngVisual == null)
            {
                Debug.LogWarning("[AyniYariAged] No se encontró Visual_Yari_3D bajo Yari_Hero.");
            }

            Transform existingAged = player.transform.Find("Visual_Yari_Aged_3D");
            if (existingAged != null)
            {
                UnityEngine.Object.DestroyImmediate(existingAged.gameObject);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject agedObj;
            if (prefab != null)
            {
                agedObj = UnityEngine.Object.Instantiate(prefab, player.transform);
                agedObj.name = "Visual_Yari_Aged_3D";
            }
            else
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(AgedFbxPath);
                agedObj = UnityEngine.Object.Instantiate(fbx, player.transform);
                agedObj.name = "Visual_Yari_Aged_3D";
            }

            agedObj.transform.localPosition = Vector3.zero;
            agedObj.transform.localRotation = Quaternion.identity;
            agedObj.transform.localScale = youngVisual != null ? youngVisual.localScale : Vector3.one;

            // Asegurar Animator y Avatar
            var anim = agedObj.GetComponent<Animator>();
            if (anim == null) anim = agedObj.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller != null) anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AgedFbxPath))
            {
                if (asset is Avatar av && av.isHuman)
                {
                    anim.avatar = av;
                    break;
                }
            }

            if (agedObj.GetComponent<AndeanCombatStanceModifier>() == null)
            {
                agedObj.AddComponent<AndeanCombatStanceModifier>();
            }

            // Inicia desactivado porque Yari comienza con su aspecto joven (20 años)
            agedObj.SetActive(false);

            // Configurar AyniAgingSystem en Yari_Hero
            var agingSystem = player.GetComponent<AyniAgingSystem>();
            if (agingSystem == null)
            {
                agingSystem = player.AddComponent<AyniAgingSystem>();
            }

            if (youngVisual != null) agingSystem.YoungVisual = youngVisual.gameObject;
            agingSystem.AgedVisual = agedObj;

            EditorUtility.SetDirty(player);
            if (player.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(player.scene);
            }

            Debug.Log("<color=green>[AyniYariAged]</color> Visual_Yari_Aged_3D y AyniAgingSystem integrados en Yari_Hero.");
        }
    }
}
#endif
