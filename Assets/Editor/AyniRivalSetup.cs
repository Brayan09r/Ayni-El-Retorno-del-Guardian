#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ayni.Combat;
using Ayni.Enemy;
using Ayni.World;

namespace Ayni.Editor
{
    public static class AyniRivalSetup
    {
        // Talla respecto a Yari (1 = igual de alto). La lámina de rivales dibuja al Rastreador de la talla del héroe;
        // se compara la altura del hueso de la cabeza, que no depende de plumas ni peinados.
        private const float RastreadorHeightVsYari = 1.0f;
        private const float FallbackTargetHeight = 1.5f; // solo si no se puede medir a Yari
        private const string YariFbxPath = "Assets/Art/Characters/Yari_Rigged.fbx";
        private const string RastreadorFbxPath = "Assets/Art/Characters/Rastreador_Rigged.fbx";
        private const string DiffusePath = "Assets/Art/Characters/Textures/Rastreador_Diffuse.png";
        private const string NormalPath = "Assets/Art/Characters/Textures/Rastreador_Normal.png";
        private const string MatPath = "Assets/Art/Characters/M_Rastreador_PBR.mat";
        private const string ControllerPath = "Assets/Art/Characters/Rastreador_AnimatorController.controller";
        private const string PrefabPath = "Assets/Prefabs/Rival_1_Rastreador.prefab";

        // Rival 2: Saqueador (K2 · Kancha de los Tejedores)
        private const float SaqueadorHeightVsYari = 1.05f;
        private const string SaqueadorFbxPath = "Assets/Art/Characters/Saqueador_Rigged.fbx";
        private const string SaqueadorDiffusePath = "Assets/Art/Characters/Textures/Saqueador_Diffuse.png";
        private const string SaqueadorNormalPath = "Assets/Art/Characters/Textures/Saqueador_Normal.png";
        private const string SaqueadorMatPath = "Assets/Art/Characters/M_Saqueador_PBR.mat";
        private const string SaqueadorControllerPath = "Assets/Art/Characters/Saqueador_AnimatorController.controller";
        private const string SaqueadorPrefabPath = "Assets/Prefabs/Rival_2_Saqueador.prefab";

        // Rival 3: Guardia del Tambo (K3 · Tambo del Camino)
        private const float GuardiaHeightVsYari = 1.15f;
        private const string GuardiaFbxPath = "Assets/Art/Characters/Guardia_Rigged.fbx";
        private const string GuardiaDiffusePath = "Assets/Art/Characters/Textures/Guardia_Diffuse.png";
        private const string GuardiaNormalPath = "Assets/Art/Characters/Textures/Guardia_Normal.png";
        private const string GuardiaMatPath = "Assets/Art/Characters/M_Guardia_PBR.mat";
        private const string GuardiaControllerPath = "Assets/Art/Characters/Guardia_AnimatorController.controller";
        private const string GuardiaPrefabPath = "Assets/Prefabs/Rival_3_Guardia.prefab";

        // Rival 4: Cazador de Élite (K4 · Portada del Santuario)
        private const float CazadorEliteHeightVsYari = 1.24f;
        private const string CazadorEliteFbxPath = "Assets/Art/Characters/CazadorElite_Rigged.fbx";
        private const string CazadorEliteDiffusePath = "Assets/Art/Characters/Textures/CazadorElite_Diffuse.png";
        private const string CazadorEliteNormalPath = "Assets/Art/Characters/Textures/CazadorElite_Normal.png";
        private const string CazadorEliteMatPath = "Assets/Art/Characters/M_CazadorElite_PBR.mat";
        private const string CazadorEliteControllerPath = "Assets/Art/Characters/CazadorElite_AnimatorController.controller";
        private const string CazadorElitePrefabPath = "Assets/Prefabs/Rival_4_CazadorElite.prefab";

        [MenuItem("Ayni/Rivales/Configurar e Integrar Rival 1: Rastreador")]
        public static void SetupRastreador()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AyniRival] Sal del modo Play antes de configurar rivales.");
                return;
            }

            if (!File.Exists(RastreadorFbxPath))
            {
                Debug.LogError($"[AyniRival] No se encontró {RastreadorFbxPath}. Verifica la descarga de Mixamo.");
                return;
            }

            // 1. Configurar Normal Map
            ConfigureNormalMap(NormalPath);

            // 2. Configurar Importador Humanoid en el FBX
            ModelImporter importer = AssetImporter.GetAtPath(RastreadorFbxPath) as ModelImporter;
            if (importer != null)
            {
                bool reimportNeeded = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    reimportNeeded = true;
                }

                if (reimportNeeded)
                {
                    importer.SaveAndReimport();
                }

                FixModelScale(importer, RastreadorFbxPath, RastreadorHeightVsYari);
            }

            // 3. Obtener Avatar
            Avatar rastreadorAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RastreadorFbxPath))
            {
                if (asset is Avatar av && av.isValid)
                {
                    rastreadorAvatar = av;
                    break;
                }
            }

            // 4. Crear / Configurar Material PBR
            Material pbrMat = CreateOrUpdateMaterial(MatPath, DiffusePath, NormalPath);

            // 5. Crear / Configurar Animator Controller
            AnimatorController animController = CreateOrGetRivalAnimatorController(ControllerPath);

            // 6. Crear o Actualizar Prefab
            GameObject prefab = CreateOrUpdatePrefab(rastreadorAvatar, pbrMat, animController);

            // 7. Poblar K1 · Puesto de Chasquis en la escena activa
            PopulateChasquiSite(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[AyniRival]</color> ¡Rival 1: Rastreador (K1 · Puesto de Chasquis) configurado e integrado con éxito!");
        }

        [MenuItem("Ayni/Rivales/Configurar e Integrar Rival 2: Saqueador")]
        public static void SetupSaqueador()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AyniRival] Sal del modo Play antes de configurar rivales.");
                return;
            }

            if (!File.Exists(SaqueadorFbxPath))
            {
                Debug.LogError($"[AyniRival] No se encontró {SaqueadorFbxPath}. Verifica la descarga de Mixamo.");
                return;
            }

            // 1. Configurar Normal Map
            ConfigureNormalMap(SaqueadorNormalPath);

            // 2. Configurar Importador Humanoid en el FBX
            ModelImporter importer = AssetImporter.GetAtPath(SaqueadorFbxPath) as ModelImporter;
            if (importer != null)
            {
                bool reimportNeeded = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    reimportNeeded = true;
                }

                if (reimportNeeded)
                {
                    importer.SaveAndReimport();
                }

                FixModelScale(importer, SaqueadorFbxPath, SaqueadorHeightVsYari);
            }

            // 3. Obtener Avatar
            Avatar saqueadorAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SaqueadorFbxPath))
            {
                if (asset is Avatar av && av.isValid)
                {
                    saqueadorAvatar = av;
                    break;
                }
            }

            // 4. Crear / Configurar Material PBR
            Material pbrMat = CreateOrUpdateMaterial(SaqueadorMatPath, SaqueadorDiffusePath, SaqueadorNormalPath);

            // 5. Crear / Configurar Animator Controller
            AnimatorController animController = CreateOrGetRivalAnimatorController(SaqueadorControllerPath);

            // 6. Crear o Actualizar Prefab
            GameObject prefab = CreateOrUpdateSaqueadorPrefab(saqueadorAvatar, pbrMat, animController);

            // 7. Poblar K2 · Kancha de los Tejedores en la escena activa
            PopulateTejedoresSite(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[AyniRival]</color> ¡Rival 2: Saqueador (K2 · Kancha de los Tejedores) configurado e integrado con éxito!");
        }

        [MenuItem("Ayni/Rivales/Configurar e Integrar Rival 3: Guardia del Tambo")]
        public static void SetupGuardiaTambo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AyniRival] Sal del modo Play antes de configurar rivales.");
                return;
            }

            if (!File.Exists(GuardiaFbxPath))
            {
                Debug.LogError($"[AyniRival] No se encontró {GuardiaFbxPath}. Verifica la descarga de Mixamo.");
                return;
            }

            // 1. Configurar Normal Map
            ConfigureNormalMap(GuardiaNormalPath);

            // 2. Configurar Importador Humanoid en el FBX
            ModelImporter importer = AssetImporter.GetAtPath(GuardiaFbxPath) as ModelImporter;
            if (importer != null)
            {
                bool reimportNeeded = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    reimportNeeded = true;
                }

                if (reimportNeeded)
                {
                    importer.SaveAndReimport();
                }

                FixModelScale(importer, GuardiaFbxPath, GuardiaHeightVsYari);
            }

            // 3. Obtener Avatar
            Avatar guardiaAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(GuardiaFbxPath))
            {
                if (asset is Avatar av && av.isValid)
                {
                    guardiaAvatar = av;
                    break;
                }
            }

            // 4. Crear / Configurar Material PBR
            Material pbrMat = CreateOrUpdateMaterial(GuardiaMatPath, GuardiaDiffusePath, GuardiaNormalPath);

            // 5. Crear / Configurar Animator Controller
            AnimatorController animController = CreateOrGetRivalAnimatorController(GuardiaControllerPath);

            // 6. Crear o Actualizar Prefab
            GameObject prefab = CreateOrUpdateGuardiaPrefab(guardiaAvatar, pbrMat, animController);

            // 7. Poblar K3 · Tambo del Camino en la escena activa
            PopulateTamboSite(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[AyniRival]</color> ¡Rival 3: Guardia del Tambo (K3 · Tambo del Camino) configurado e integrado con éxito!");
        }

        [MenuItem("Ayni/Rivales/Configurar e Integrar Rival 4: Cazador de Élite")]
        public static void SetupCazadorElite()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AyniRival] Sal del modo Play antes de configurar rivales.");
                return;
            }

            if (!File.Exists(CazadorEliteFbxPath))
            {
                Debug.LogError($"[AyniRival] No se encontró {CazadorEliteFbxPath}. Verifica la descarga de Mixamo.");
                return;
            }

            // 1. Configurar Normal Map
            ConfigureNormalMap(CazadorEliteNormalPath);

            // 2. Configurar Importador Humanoid en el FBX
            ModelImporter importer = AssetImporter.GetAtPath(CazadorEliteFbxPath) as ModelImporter;
            if (importer != null)
            {
                bool reimportNeeded = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    reimportNeeded = true;
                }

                if (reimportNeeded)
                {
                    importer.SaveAndReimport();
                }

                FixModelScale(importer, CazadorEliteFbxPath, CazadorEliteHeightVsYari);
            }

            // 3. Obtener Avatar
            Avatar cazadorAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(CazadorEliteFbxPath))
            {
                if (asset is Avatar av && av.isValid)
                {
                    cazadorAvatar = av;
                    break;
                }
            }

            // 4. Crear / Configurar Material PBR
            Material pbrMat = CreateOrUpdateMaterial(CazadorEliteMatPath, CazadorEliteDiffusePath, CazadorEliteNormalPath);

            // 5. Crear / Configurar Animator Controller
            AnimatorController animController = CreateOrGetRivalAnimatorController(CazadorEliteControllerPath);

            // 6. Crear o Actualizar Prefab
            GameObject prefab = CreateOrUpdateCazadorElitePrefab(cazadorAvatar, pbrMat, animController);

            // 7. Poblar K4 · Portada del Santuario en la escena activa
            PopulatePortadaSite(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[AyniRival]</color> ¡Rival 4: Cazador de Élite (K4 · Portada del Santuario) configurado e integrado con éxito!");
        }

        [MenuItem("Ayni/Rivales/Verificar Rival 1 en Escena")]
        public static void VerifyRival()
        {
            var rivals = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[AyniRival Verifica] Encontrados {rivals.Length} enemigos en la escena (activos e inactivos):");
            foreach (var e in rivals)
            {
                var anim = e.GetComponentInChildren<Animator>(true);
                var smr = e.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var cc = e.GetComponent<CharacterController>();
                float h = cc != null ? cc.height : 0f;
                Debug.Log($"   -> {e.name} (activo={e.gameObject.activeSelf}): pos={e.transform.position}, CC altura={h}m, SMR={smr?.name}, mat={smr?.sharedMaterial?.name}, anim={anim?.runtimeAnimatorController?.name}, avatarValid={anim?.avatar?.isValid}");
            }
        }

        [MenuItem("Ayni/Rivales/Foto Primer Plano Rival 1")]
        public static void ShotRastreador()
        {
            GameObject rival = GameObject.Find("Rival_Rastreador_1");
            if (rival == null)
            {
                var rivals = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var r in rivals)
                {
                    if (r.name.Contains("Rastreador")) { rival = r.gameObject; break; }
                }
            }

            if (rival == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el objeto de Rival 1 en la escena.");
                return;
            }

            bool wasActive = rival.activeSelf;
            rival.SetActive(true);

            Vector3 targetPos = rival.transform.position + Vector3.up * 1.0f;
            Vector3 camPos = targetPos + rival.transform.forward * 2.8f + Vector3.up * 0.35f;

            string dir = "DebugCaptures";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "rastreador_close_up.png");

            var go = new GameObject("Ayni_CapturaRival") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = camPos;
                cam.transform.LookAt(targetPos);
                cam.fieldOfView = 36f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"<color=green>[AyniRival]</color> Captura de primer plano guardada en {path}.");
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(go);
                rival.SetActive(wasActive);
            }
        }

        [MenuItem("Ayni/Rivales/Foto Combate Playmode")]
        public static void ShotRivalPlayMode()
        {
            GameObject rival = GameObject.Find("Rival_Rastreador_1");
            if (rival == null) return;

            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;

            Vector3 target = rival.transform.position + Vector3.up * 1.0f;
            Vector3 camPos = target + rival.transform.forward * 2.5f + Vector3.up * 0.2f;
            cam.transform.position = camPos;
            cam.transform.LookAt(target);

            string dir = "DebugCaptures";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "rastreador_combat_anim.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"<color=green>[AyniRival]</color> Captura de combate pedida: {path}");
        }

        private static void ConfigureNormalMap(string normalPath)
        {
            if (!File.Exists(normalPath)) return;
            TextureImporter imp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                Debug.Log($"[AyniRival] {Path.GetFileName(normalPath)} configurado como NormalMap.");
            }
        }

        private static Material CreateOrUpdateMaterial(string matPath, string diffusePath, string normalPath)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = litShader;
            }

            Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            if (diffuseTex != null)
            {
                mat.SetTexture("_BaseMap", diffuseTex);
                mat.SetColor("_BaseColor", Color.white);
            }

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.22f);
            mat.SetFloat("_Metallic", 0.0f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AnimatorController CreateOrGetRivalAnimatorController(string controllerPath)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                string amaruControllerPath = "Assets/Art/Characters/Amaru_AnimatorController.controller";
                if (File.Exists(amaruControllerPath))
                {
                    AssetDatabase.CopyAsset(amaruControllerPath, controllerPath);
                    controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                }
                else
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                }
            }

            return controller;
        }

        private static GameObject CreateOrUpdatePrefab(Avatar avatar, Material mat, RuntimeAnimatorController animController)
        {
            string prefabDir = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            GameObject root = new GameObject("Rival_1_Rastreador");
            root.transform.position = Vector3.zero;

            // CharacterController
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.85f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 0.925f, 0f);

            // StructureSystem
            var structure = root.AddComponent<StructureSystem>();
            SerializedObject soStruct = new SerializedObject(structure);
            soStruct.FindProperty("maxStructure").floatValue = 65f;
            soStruct.ApplyModifiedProperties();

            // EnemyController (Stats de chasqui rastreador: ágil, rápido, daño moderado)
            var enemy = root.AddComponent<EnemyController>();
            SerializedObject soEnemy = new SerializedObject(enemy);
            soEnemy.FindProperty("characterName").stringValue = "Rastreador";
            soEnemy.FindProperty("isBoss").boolValue = false;
            soEnemy.FindProperty("maxHealth").floatValue = 75f;
            soEnemy.FindProperty("attackDamage").floatValue = 9f;
            soEnemy.FindProperty("structureDamageOnPlayer").floatValue = 12f;
            soEnemy.FindProperty("attackRange").floatValue = 1.6f;
            soEnemy.FindProperty("windupTime").floatValue = 0.45f;
            soEnemy.FindProperty("recoverTime").floatValue = 0.6f;
            soEnemy.FindProperty("moveSpeed").floatValue = 3.8f;
            soEnemy.FindProperty("turnSpeed").floatValue = 9f;
            soEnemy.FindProperty("aggroRange").floatValue = 16f;
            soEnemy.ApplyModifiedProperties();

            // Visual 3D
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(RastreadorFbxPath);
            if (fbxModel != null)
            {
                GameObject visual = UnityEngine.Object.Instantiate(fbxModel, root.transform);
                visual.name = "Visual_Rastreador_3D";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                if (mat != null)
                {
                    foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.sharedMaterial = mat;
                    }
                }

                var anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.AddComponent<Animator>();
                anim.runtimeAnimatorController = animController;
                if (avatar != null) anim.avatar = avatar;
                anim.applyRootMotion = false;

                if (visual.GetComponent<AndeanCombatStanceModifier>() == null)
                {
                    visual.AddComponent<AndeanCombatStanceModifier>();
                }
            }

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[AyniRival] Prefab guardado en {PrefabPath}.");
            return prefabAsset;
        }

        private static void PopulateChasquiSite(GameObject prefab)
        {
            if (prefab == null) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return;

            AyniEncounterSite chasquiSite = null;
            foreach (var site in UnityEngine.Object.FindObjectsByType<AyniEncounterSite>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (site.Order == 1 || site.SiteName.Contains("Chasquis"))
                {
                    chasquiSite = site;
                    break;
                }
            }

            if (chasquiSite == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el conjunto K1 · Puesto de Chasquis en la escena.");
                return;
            }

            // Si ya tiene rivales configurados, no duplicar
            if (chasquiSite.Rivals != null && chasquiSite.Rivals.Count > 0)
            {
                Debug.Log($"[AyniRival] K1 · Puesto de Chasquis ya cuenta con {chasquiSite.Rivals.Count} rivales asignados.");
                return;
            }

            // Ubicar rivales en los spawn points del patio
            int countToSpawn = Mathf.Min(2, chasquiSite.SpawnPoints.Count);
            if (countToSpawn == 0) countToSpawn = 1;

            for (int i = 0; i < countToSpawn; i++)
            {
                Transform sp = chasquiSite.GetSpawnPoint(i);
                Vector3 pos = sp != null ? sp.position : chasquiSite.transform.position;
                Quaternion rot = sp != null ? sp.rotation : Quaternion.identity;

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null)
                {
                    instance.name = $"Rival_Rastreador_{i + 1}";
                    instance.transform.position = pos;
                    instance.transform.rotation = rot;
                    chasquiSite.AddRival(instance);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AyniRival] Poblado K1 · Puesto de Chasquis con {countToSpawn} Rastreadores.");
        }

        private static GameObject CreateOrUpdateSaqueadorPrefab(Avatar avatar, Material mat, RuntimeAnimatorController animController)
        {
            string prefabDir = Path.GetDirectoryName(SaqueadorPrefabPath);
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            GameObject root = new GameObject("Rival_2_Saqueador");
            root.transform.position = Vector3.zero;

            // CharacterController (más fornido y pesado que el chasqui)
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.90f;
            cc.radius = 0.46f;
            cc.center = new Vector3(0f, 0.95f, 0f);

            // StructureSystem
            var structure = root.AddComponent<StructureSystem>();
            SerializedObject soStruct = new SerializedObject(structure);
            soStruct.FindProperty("maxStructure").floatValue = 75f;
            soStruct.ApplyModifiedProperties();

            // EnemyController (Stats de Saqueador: mayor pegada y resistencia)
            var enemy = root.AddComponent<EnemyController>();
            SerializedObject soEnemy = new SerializedObject(enemy);
            soEnemy.FindProperty("characterName").stringValue = "Saqueador";
            soEnemy.FindProperty("isBoss").boolValue = false;
            soEnemy.FindProperty("maxHealth").floatValue = 90f;
            soEnemy.FindProperty("attackDamage").floatValue = 12f;
            soEnemy.FindProperty("structureDamageOnPlayer").floatValue = 15f;
            soEnemy.FindProperty("attackRange").floatValue = 1.7f;
            soEnemy.FindProperty("windupTime").floatValue = 0.52f;
            soEnemy.FindProperty("recoverTime").floatValue = 0.68f;
            soEnemy.FindProperty("moveSpeed").floatValue = 3.3f;
            soEnemy.FindProperty("turnSpeed").floatValue = 7.5f;
            soEnemy.FindProperty("aggroRange").floatValue = 17f;
            soEnemy.ApplyModifiedProperties();

            // Visual 3D
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(SaqueadorFbxPath);
            if (fbxModel != null)
            {
                GameObject visual = UnityEngine.Object.Instantiate(fbxModel, root.transform);
                visual.name = "Visual_Saqueador_3D";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                if (mat != null)
                {
                    foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.sharedMaterial = mat;
                    }
                }

                var anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.AddComponent<Animator>();
                anim.runtimeAnimatorController = animController;
                if (avatar != null) anim.avatar = avatar;
                anim.applyRootMotion = false;

                if (visual.GetComponent<AndeanCombatStanceModifier>() == null)
                {
                    visual.AddComponent<AndeanCombatStanceModifier>();
                }
            }

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, SaqueadorPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[AyniRival] Prefab guardado en {SaqueadorPrefabPath}.");
            return prefabAsset;
        }

        private static void PopulateTejedoresSite(GameObject prefab)
        {
            if (prefab == null) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return;

            AyniEncounterSite tejedoresSite = null;
            foreach (var site in UnityEngine.Object.FindObjectsByType<AyniEncounterSite>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (site.Order == 2 || site.SiteName.Contains("Tejedores"))
                {
                    tejedoresSite = site;
                    break;
                }
            }

            if (tejedoresSite == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el conjunto K2 · Kancha de los Tejedores en la escena.");
                return;
            }

            if (tejedoresSite.Rivals != null && tejedoresSite.Rivals.Count > 0)
            {
                Debug.Log($"[AyniRival] K2 · Kancha de los Tejedores ya cuenta con {tejedoresSite.Rivals.Count} rivales asignados.");
                return;
            }

            int countToSpawn = Mathf.Min(3, tejedoresSite.SpawnPoints.Count);
            if (countToSpawn == 0) countToSpawn = 2;

            for (int i = 0; i < countToSpawn; i++)
            {
                Transform sp = tejedoresSite.GetSpawnPoint(i);
                Vector3 pos = sp != null ? sp.position : tejedoresSite.transform.position;
                Quaternion rot = sp != null ? sp.rotation : Quaternion.identity;

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null)
                {
                    instance.name = $"Rival_Saqueador_{i + 1}";
                    instance.transform.position = pos;
                    instance.transform.rotation = rot;
                    tejedoresSite.AddRival(instance);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AyniRival] Poblado K2 · Kancha de los Tejedores con {countToSpawn} Saqueadores.");
        }

        [MenuItem("Ayni/Rivales/Foto Primer Plano Rival 2")]
        public static void ShotSaqueador()
        {
            GameObject rival = GameObject.Find("Rival_Saqueador_1");
            if (rival == null)
            {
                var rivals = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var r in rivals)
                {
                    if (r.name.Contains("Saqueador")) { rival = r.gameObject; break; }
                }
            }

            if (rival == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el objeto de Rival 2 en la escena.");
                return;
            }

            bool wasActive = rival.activeSelf;
            rival.SetActive(true);

            Vector3 targetPos = rival.transform.position + Vector3.up * 1.0f;
            Vector3 camPos = targetPos + rival.transform.forward * 2.8f + Vector3.up * 0.35f;

            string dir = "DebugCaptures";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "saqueador_close_up.png");

            var go = new GameObject("Ayni_CapturaRival2") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = camPos;
                cam.transform.LookAt(targetPos);
                cam.fieldOfView = 36f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"<color=green>[AyniRival]</color> Captura de primer plano guardada en {path}.");
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(go);
                rival.SetActive(wasActive);
            }
        }

        private static GameObject CreateOrUpdateGuardiaPrefab(Avatar avatar, Material mat, RuntimeAnimatorController animController)
        {
            string prefabDir = Path.GetDirectoryName(GuardiaPrefabPath);
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            GameObject root = new GameObject("Rival_3_Guardia");
            root.transform.position = Vector3.zero;

            // CharacterController (pesado, centinela del tambo)
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.95f;
            cc.radius = 0.48f;
            cc.center = new Vector3(0f, 0.975f, 0f);

            // StructureSystem (alta postura para defensa)
            var structure = root.AddComponent<StructureSystem>();
            SerializedObject soStruct = new SerializedObject(structure);
            soStruct.FindProperty("maxStructure").floatValue = 85f;
            soStruct.ApplyModifiedProperties();

            // EnemyController (Stats de Guardia del Tambo: tanque defensor)
            var enemy = root.AddComponent<EnemyController>();
            SerializedObject soEnemy = new SerializedObject(enemy);
            soEnemy.FindProperty("characterName").stringValue = "Guardia del Tambo";
            soEnemy.FindProperty("isBoss").boolValue = false;
            soEnemy.FindProperty("maxHealth").floatValue = 110f;
            soEnemy.FindProperty("attackDamage").floatValue = 14f;
            soEnemy.FindProperty("structureDamageOnPlayer").floatValue = 18f;
            soEnemy.FindProperty("attackRange").floatValue = 1.8f;
            soEnemy.FindProperty("windupTime").floatValue = 0.55f;
            soEnemy.FindProperty("recoverTime").floatValue = 0.72f;
            soEnemy.FindProperty("moveSpeed").floatValue = 3.1f;
            soEnemy.FindProperty("turnSpeed").floatValue = 7.0f;
            soEnemy.FindProperty("aggroRange").floatValue = 18f;
            soEnemy.ApplyModifiedProperties();

            // Visual 3D
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(GuardiaFbxPath);
            if (fbxModel != null)
            {
                GameObject visual = UnityEngine.Object.Instantiate(fbxModel, root.transform);
                visual.name = "Visual_Guardia_3D";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                if (mat != null)
                {
                    foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.sharedMaterial = mat;
                    }
                }

                var anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.AddComponent<Animator>();
                anim.runtimeAnimatorController = animController;
                if (avatar != null) anim.avatar = avatar;
                anim.applyRootMotion = false;

                if (visual.GetComponent<AndeanCombatStanceModifier>() == null)
                {
                    visual.AddComponent<AndeanCombatStanceModifier>();
                }
            }

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, GuardiaPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[AyniRival] Prefab guardado en {GuardiaPrefabPath}.");
            return prefabAsset;
        }

        private static void PopulateTamboSite(GameObject prefab)
        {
            if (prefab == null) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return;

            AyniEncounterSite tamboSite = null;
            foreach (var site in UnityEngine.Object.FindObjectsByType<AyniEncounterSite>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (site.Order == 3 || site.SiteName.Contains("Tambo"))
                {
                    tamboSite = site;
                    break;
                }
            }

            if (tamboSite == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el conjunto K3 · Tambo del Camino en la escena.");
                return;
            }

            if (tamboSite.Rivals != null && tamboSite.Rivals.Count > 0)
            {
                Debug.Log($"[AyniRival] K3 · Tambo del Camino ya cuenta con {tamboSite.Rivals.Count} rivales asignados.");
                return;
            }

            int countToSpawn = Mathf.Min(3, tamboSite.SpawnPoints.Count);
            if (countToSpawn == 0) countToSpawn = 2;

            for (int i = 0; i < countToSpawn; i++)
            {
                Transform sp = tamboSite.GetSpawnPoint(i);
                Vector3 pos = sp != null ? sp.position : tamboSite.transform.position;
                Quaternion rot = sp != null ? sp.rotation : Quaternion.identity;

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null)
                {
                    instance.name = $"Rival_Guardia_{i + 1}";
                    instance.transform.position = pos;
                    instance.transform.rotation = rot;
                    tamboSite.AddRival(instance);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AyniRival] Poblado K3 · Tambo del Camino con {countToSpawn} Guardias del Tambo.");
        }

        [MenuItem("Ayni/Rivales/Foto Primer Plano Rival 3")]
        public static void ShotGuardia()
        {
            GameObject rival = GameObject.Find("Rival_Guardia_1");
            if (rival == null)
            {
                var rivals = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var r in rivals)
                {
                    if (r.name.Contains("Guardia")) { rival = r.gameObject; break; }
                }
            }

            if (rival == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el objeto de Rival 3 en la escena.");
                return;
            }

            bool wasActive = rival.activeSelf;
            rival.SetActive(true);

            Vector3 targetPos = rival.transform.position + Vector3.up * 1.0f;
            Vector3 camPos = targetPos + rival.transform.forward * 2.8f + Vector3.up * 0.35f;

            string dir = "DebugCaptures";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "guardia_close_up.png");

            var go = new GameObject("Ayni_CapturaRival3") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = camPos;
                cam.transform.LookAt(targetPos);
                cam.fieldOfView = 36f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"<color=green>[AyniRival]</color> Captura de primer plano guardada en {path}.");
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(go);
                rival.SetActive(wasActive);
            }
        }

        private static GameObject CreateOrUpdateCazadorElitePrefab(Avatar avatar, Material mat, RuntimeAnimatorController animController)
        {
            string prefabDir = Path.GetDirectoryName(CazadorElitePrefabPath);
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            GameObject root = new GameObject("Rival_4_CazadorElite");
            root.transform.position = Vector3.zero;

            // CharacterController (alto, imponente, teniente de Amaru)
            var cc = root.AddComponent<CharacterController>();
            cc.height = 2.05f;
            cc.radius = 0.50f;
            cc.center = new Vector3(0f, 1.025f, 0f);

            // StructureSystem (alta postura)
            var structure = root.AddComponent<StructureSystem>();
            SerializedObject soStruct = new SerializedObject(structure);
            soStruct.FindProperty("maxStructure").floatValue = 95f;
            soStruct.ApplyModifiedProperties();

            // EnemyController (Stats de Cazador de Élite: letal y agresivo)
            var enemy = root.AddComponent<EnemyController>();
            SerializedObject soEnemy = new SerializedObject(enemy);
            soEnemy.FindProperty("characterName").stringValue = "Cazador de Élite";
            soEnemy.FindProperty("isBoss").boolValue = false;
            soEnemy.FindProperty("maxHealth").floatValue = 130f;
            soEnemy.FindProperty("attackDamage").floatValue = 16f;
            soEnemy.FindProperty("structureDamageOnPlayer").floatValue = 20f;
            soEnemy.FindProperty("attackRange").floatValue = 1.85f;
            soEnemy.FindProperty("windupTime").floatValue = 0.48f;
            soEnemy.FindProperty("recoverTime").floatValue = 0.62f;
            soEnemy.FindProperty("moveSpeed").floatValue = 3.7f;
            soEnemy.FindProperty("turnSpeed").floatValue = 8.5f;
            soEnemy.FindProperty("aggroRange").floatValue = 19f;
            soEnemy.ApplyModifiedProperties();

            // Visual 3D
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(CazadorEliteFbxPath);
            if (fbxModel != null)
            {
                GameObject visual = UnityEngine.Object.Instantiate(fbxModel, root.transform);
                visual.name = "Visual_CazadorElite_3D";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                if (mat != null)
                {
                    foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.sharedMaterial = mat;
                    }
                }

                var anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.AddComponent<Animator>();
                anim.runtimeAnimatorController = animController;
                if (avatar != null) anim.avatar = avatar;
                anim.applyRootMotion = false;

                if (visual.GetComponent<AndeanCombatStanceModifier>() == null)
                {
                    visual.AddComponent<AndeanCombatStanceModifier>();
                }
            }

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, CazadorElitePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[AyniRival] Prefab guardado en {CazadorElitePrefabPath}.");
            return prefabAsset;
        }

        private static void PopulatePortadaSite(GameObject prefab)
        {
            if (prefab == null) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return;

            AyniEncounterSite portadaSite = null;
            foreach (var site in UnityEngine.Object.FindObjectsByType<AyniEncounterSite>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (site.Order == 4 || site.SiteName.Contains("Portada"))
                {
                    portadaSite = site;
                    break;
                }
            }

            if (portadaSite == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el conjunto K4 · Portada del Santuario en la escena.");
                return;
            }

            if (portadaSite.Rivals != null && portadaSite.Rivals.Count > 0)
            {
                Debug.Log($"[AyniRival] K4 · Portada del Santuario ya cuenta con {portadaSite.Rivals.Count} rivales asignados.");
                return;
            }

            int countToSpawn = Mathf.Min(3, portadaSite.SpawnPoints.Count);
            if (countToSpawn == 0) countToSpawn = 2;

            for (int i = 0; i < countToSpawn; i++)
            {
                Transform sp = portadaSite.GetSpawnPoint(i);
                Vector3 pos = sp != null ? sp.position : portadaSite.transform.position;
                Quaternion rot = sp != null ? sp.rotation : Quaternion.identity;

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null)
                {
                    instance.name = $"Rival_CazadorElite_{i + 1}";
                    instance.transform.position = pos;
                    instance.transform.rotation = rot;
                    portadaSite.AddRival(instance);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AyniRival] Poblado K4 · Portada del Santuario con {countToSpawn} Cazadores de Élite.");
        }

        [MenuItem("Ayni/Rivales/Foto Primer Plano Rival 4")]
        public static void ShotCazadorElite()
        {
            GameObject rival = GameObject.Find("Rival_CazadorElite_1");
            if (rival == null)
            {
                var rivals = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var r in rivals)
                {
                    if (r.name.Contains("CazadorElite")) { rival = r.gameObject; break; }
                }
            }

            if (rival == null)
            {
                Debug.LogWarning("[AyniRival] No se encontró el objeto de Rival 4 en la escena.");
                return;
            }

            bool wasActive = rival.activeSelf;
            rival.SetActive(true);

            Vector3 targetPos = rival.transform.position + Vector3.up * 1.0f;
            Vector3 camPos = targetPos + rival.transform.forward * 2.8f + Vector3.up * 0.35f;

            string dir = "DebugCaptures";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "cazador_elite_close_up.png");

            var go = new GameObject("Ayni_CapturaRival4") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = camPos;
                cam.transform.LookAt(targetPos);
                cam.fieldOfView = 36f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"<color=green>[AyniRival]</color> Captura de primer plano guardada en {path}.");
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(go);
                rival.SetActive(wasActive);
            }
        }

        /// <summary>
        /// Deja el modelo a la talla pedida respecto a Yari y con un Avatar Humanoid coherente con esa escala.
        /// Se puede repetir: si ya está bien, no reimporta nada.
        /// </summary>
        private static void FixModelScale(ModelImporter importer, string fbxPath, float heightVsYari)
        {
            if (importer == null) return;

            // 1. Talla
            float yariHead = MeasureBoneHeight(YariFbxPath, "Head");
            float head = MeasureBoneHeight(fbxPath, "Head");
            float correction = 1f;
            if (yariHead > 0.01f && head > 0.00001f)
            {
                correction = yariHead * heightVsYari / head;
            }
            else
            {
                float height = MeasureModelHeight(fbxPath);
                if (height > 0.00001f) correction = FallbackTargetHeight / height;
            }
            if (Mathf.Abs(correction - 1f) > 0.02f)
            {
                float oldScale = importer.globalScale;
                importer.globalScale = oldScale * correction;
                importer.SaveAndReimport(); // con el Avatar automático, se regenera también a la nueva escala
                Debug.Log($"<color=green>[AyniRival]</color> Escala de {fbxPath}: {oldScale:0.###} -> {importer.globalScale:0.###}. " +
                          $"Cabeza a {MeasureBoneHeight(fbxPath, "Head"):0.00} m (Yari: {yariHead:0.00} m).");
            }
            else
            {
                Debug.Log($"[AyniRival] Talla de {fbxPath} correcta: cabeza a {head:0.00} m (Yari: {yariHead:0.00} m).");
            }

            // 2. Avatar
            NormalizeAvatar(importer, fbxPath);
        }

        /// <summary>
        /// El Avatar Humanoid debe generarse solo, a partir del modelo y a su misma escala (como los de Yari y Amaru).
        /// Si el .meta guarda un esqueleto de referencia propio, o su escala no coincide con la del modelo, en Play el
        /// Animator estira los huesos cien veces: el rival no se ve y cada golpe lo lanza decenas de metros, porque el
        /// desplazamiento del clip también sale multiplicado.
        /// </summary>
        private static void NormalizeAvatar(ModelImporter importer, string fbxPath)
        {
            var so = new SerializedObject(importer);
            SerializedProperty human = so.FindProperty("m_HumanDescription.m_Human");
            SerializedProperty skeleton = so.FindProperty("m_HumanDescription.m_Skeleton");
            SerializedProperty avatarScale = so.FindProperty("m_HumanDescription.m_GlobalScale");
            SerializedProperty modelScale = so.FindProperty("m_GlobalScale");
            if (human == null || skeleton == null || avatarScale == null || modelScale == null)
            {
                Debug.LogWarning("[AyniRival] No se pudo comprobar el Avatar de " + fbxPath + " (cambió el formato del importador). " +
                                 "Compara su .meta con el de Amaru_Rigged.fbx: 'human' y 'skeleton' vacíos y las dos 'globalScale' iguales.");
                return;
            }

            bool dirty = false;
            if (human.arraySize > 0) { human.ClearArray(); dirty = true; }
            if (skeleton.arraySize > 0) { skeleton.ClearArray(); dirty = true; }
            if (Mathf.Abs(avatarScale.floatValue - modelScale.floatValue) > 0.0005f * Mathf.Max(1f, Mathf.Abs(modelScale.floatValue)))
            {
                avatarScale.floatValue = modelScale.floatValue;
                dirty = true;
            }
            if (!dirty) return;

            so.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            Debug.Log($"<color=green>[AyniRival]</color> Avatar Humanoid de {fbxPath} regenerado a la escala del modelo.");
        }

        /// <summary>Altura sobre el suelo de un hueso del modelo en su pose de reposo (por ejemplo "Head").</summary>
        private static float MeasureBoneHeight(string fbxPath, string boneSuffix)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null) return 0f;
            var temp = UnityEngine.Object.Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float height = 0f;
            foreach (var t in temp.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(":" + boneSuffix) || t.name == boneSuffix) { height = t.position.y; break; }
            }
            UnityEngine.Object.DestroyImmediate(temp);
            return height;
        }

        private static float MeasureModelHeight(string fbxPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null) return 0f;

            var temp = UnityEngine.Object.Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }
            UnityEngine.Object.DestroyImmediate(temp);
            return hasBounds ? bounds.size.y : 0f;
        }
    }
}
#endif
