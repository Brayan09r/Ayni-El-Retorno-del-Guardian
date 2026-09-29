#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Ayni.Core;
using Ayni.Player;
using Ayni.Enemy;
using Ayni.Combat;
using Ayni.UI;
using System.IO;

namespace Ayni.Editor
{
    public static class AyniSceneSetup
    {
        // Clave por proyecto: la configuración automática corre UNA sola vez (no en cada arranque de Unity),
        // así no se sobrescribe el Animator Controller ni la escena cada vez que abres el editor.
        // Para volver a ejecutarla usa el menú Ayni/2.
        private static string AutoSetupKey => "AyniAutoSetupDone_v11_" + Application.dataPath.GetHashCode();

        // Altura real deseada para Yari en metros (1 unidad de Unity = 1 metro)
        private const float YariTargetHeight = 1.75f;

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (EditorPrefs.GetBool(AutoSetupKey, false)) return;
                EditorPrefs.SetBool(AutoSetupKey, true);
                SetupCombatInPathMap();
            };
        }

        // IMPORTANTE: en Unity, GetComponent<T>() ?? AddComponent<T>() NO funciona.
        // En el Editor, GetComponent devuelve un objeto "falso null" que el operador ?? no detecta,
        // por eso salía "MissingComponentException: There is no 'CharacterController' attached to Yari_Hero".
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        [MenuItem("Ayni/0. Arreglar Color Magenta del Terreno (Activar URP)")]
        public static void FixMagentaTerrain()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/network of paths/Shaders/UniversalRenderPipelineAsset.asset");
            if (urpAsset == null)
            {
                string[] guids = AssetDatabase.FindAssets("UniversalRenderPipelineAsset t:RenderPipelineAsset");
                if (guids.Length > 0)
                {
                    urpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (urpAsset != null)
            {
                GraphicsSettings.defaultRenderPipeline = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
                Debug.Log("<color=green>[Ayni]</color> ¡Universal Render Pipeline (URP) asignado con éxito! Las texturas del terreno se actualizarán.");
            }
            else
            {
                Debug.LogWarning("[Ayni] No se encontró el asset de URP. Asignando shader de terreno estándar...");
            }

            // Forzar actualización del material del terreno
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/network of paths/Materials/terrain material.mat");
            if (terrainMat != null)
            {
                Shader urpTerrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? Shader.Find("Nature/Terrain/Standard");
                if (urpTerrainShader != null && terrainMat.shader != null && terrainMat.shader.name.Contains("Error"))
                {
                    terrainMat.shader = urpTerrainShader;
                }
                EditorUtility.SetDirty(terrainMat);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static Avatar ConfigureHumanoidRigs()
        {
            string riggedFbxPath = "Assets/Art/Characters/Yari_Rigged.fbx";
            if (!File.Exists(riggedFbxPath))
            {
                Debug.LogWarning("[Ayni] Yari_Rigged.fbx no encontrado aún. Omitiendo configuración de Avatar Humanoid.");
                return null;
            }

            // 1. Configurar Yari_Rigged como Humanoid (Create From This Model)
            var importer = AssetImporter.GetAtPath(riggedFbxPath) as ModelImporter;
            if (importer != null &&
                (importer.animationType != ModelImporterAnimationType.Human ||
                 importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            // 1b. Corregir escala: el FBX de Mixamo llegó con Yari midiendo ~1 cm (el OBJ original era muy pequeño).
            //     Medimos su altura real y ajustamos el "Scale Factor" del importador para que mida ~1.75 m.
            float scaleFactor = FixRiggedModelScale(importer, riggedFbxPath);
            float rigSpineLen = GetPrefabBoneLocalLength(riggedFbxPath, "Spine1");

            // 1c. Regenerar el Avatar si su esqueleto de referencia no coincide con el modelo.
            //     Al cambiar el Scale Factor, el Avatar guardado conservaba un esqueleto 100 veces más grande que
            //     los huesos reales; en Play, Unity colocaba los huesos según ese esqueleto y estiraba a Yari
            //     (la cabeza quedaba a 50 m de altura y la malla fuera de la cámara).
            bool rigRebuilt = RebuildAvatarIfInconsistent(importer, riggedFbxPath, rigSpineLen);
            Avatar yariAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(riggedFbxPath))
            {
                if (asset is Avatar av) { yariAvatar = av; break; }
            }

            if (yariAvatar == null || !yariAvatar.isValid || !yariAvatar.isHuman)
            {
                Debug.LogError("[Ayni] El Avatar de Yari_Rigged.fbx no es un Humanoid válido. " +
                               "Selecciona Yari_Rigged.fbx > Rig > Configure... y revisa que los huesos estén en verde.");
                return yariAvatar;
            }

            // 2. Configurar todas las animaciones como Humanoid (Copy From Other Avatar -> Yari)
            //    y activar Loop Time en Idle y Caminar/Trotar.
            string animsFolder = "Assets/Art/Characters/Animations";
            if (Directory.Exists(animsFolder))
            {
                foreach (var animFile in Directory.GetFiles(animsFolder, "*.fbx"))
                {
                    string unityPath = animFile.Replace("\\", "/");
                    var animImporter = AssetImporter.GetAtPath(unityPath) as ModelImporter;
                    if (animImporter == null) continue;

                    // Escala de la animación: sus FBX vienen en otra unidad que Yari_Rigged.
                    // Se compara la longitud de un mismo hueso (Spine1) en el esqueleto de Yari y en el de la
                    // animación, y se ajusta el Scale Factor hasta que sean iguales; si no, el esqueleto se estira.
                    float desiredScale = animImporter.globalScale;
                    float animSpineLen = GetPrefabBoneLocalLength(unityPath, "Spine1");
                    if (rigSpineLen > 0f && animSpineLen > 0f)
                    {
                        float ratio = rigSpineLen / animSpineLen;
                        if (Mathf.Abs(ratio - 1f) > 0.02f) desiredScale = animImporter.globalScale * ratio;
                    }

                    bool needReimport = false;
                    if (animImporter.animationType != ModelImporterAnimationType.Human ||
                        animImporter.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                        animImporter.sourceAvatar != yariAvatar ||
                        Mathf.Abs(animImporter.globalScale - desiredScale) > desiredScale * 0.001f ||
                        rigRebuilt)
                    {
                        animImporter.animationType = ModelImporterAnimationType.Human;
                        animImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        animImporter.sourceAvatar = yariAvatar;
                        animImporter.globalScale = desiredScale;
                        needReimport = true;
                        Debug.Log($"[Ayni] {Path.GetFileName(unityPath)}: Scale Factor {desiredScale:0.######} (hueso Spine1 anim {animSpineLen:0.#####} → Yari {rigSpineLen:0.#####}).");
                    }

                    if (needReimport)
                    {
                        animImporter.SaveAndReimport(); // primero reimportar para obtener los clips en Humanoid
                        needReimport = false;
                    }

                    // Loop Time solo en las animaciones cíclicas (Idle y Jog/Walk)
                    string fileName = Path.GetFileNameWithoutExtension(unityPath).ToLowerInvariant();
                    bool shouldLoop = fileName.Contains("idle") || fileName.Contains("jog") ||
                                      fileName.Contains("walk") || fileName.Contains("run");

                    var clips = animImporter.clipAnimations;
                    if (clips == null || clips.Length == 0) clips = animImporter.defaultClipAnimations;

                    foreach (var c in clips)
                    {
                        if (c.loopTime != shouldLoop) { c.loopTime = shouldLoop; needReimport = true; }
                        // Mantener a Yari en su sitio: la raíz no rota ni sube/baja por la animación
                        if (!c.lockRootRotation) { c.lockRootRotation = true; needReimport = true; }
                        if (!c.lockRootHeightY) { c.lockRootHeightY = true; needReimport = true; }
                        if (shouldLoop && !c.lockRootPositionXZ) { c.lockRootPositionXZ = true; needReimport = true; }
                    }

                    if (needReimport)
                    {
                        animImporter.clipAnimations = clips;
                        animImporter.SaveAndReimport();
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[Ayni]</color> Rig Humanoid configurado en Yari_Rigged y en las 7 animaciones (Loop en Idle/Jog).");
            return yariAvatar;
        }

        /// <summary>
        /// Mide la altura de Yari (con el Scale Factor actual) y, si no está entre 1.2 m y 2.5 m,
        /// ajusta ModelImporter.globalScale para que mida YariTargetHeight. Devuelve el Scale Factor final.
        /// </summary>
        private static float FixRiggedModelScale(ModelImporter importer, string fbxPath)
        {
            if (importer == null) return 1f;

            float height = MeasureModelHeight(fbxPath);
            if (height <= 0.00001f)
            {
                Debug.LogWarning("[Ayni] No se pudo medir la altura de Yari_Rigged.fbx.");
                return importer.globalScale;
            }

            if (height < 1.2f || height > 2.5f)
            {
                float oldScale = importer.globalScale;
                importer.globalScale = oldScale * (YariTargetHeight / height);
                importer.SaveAndReimport(); // regenera también el Avatar Humanoid a la nueva escala

                Debug.Log($"<color=green>[Ayni]</color> Escala de Yari corregida: medía {height * 100f:0.##} cm → " +
                          $"ahora mide {MeasureModelHeight(fbxPath):0.00} m (Scale Factor {oldScale:0.###} → {importer.globalScale:0.###}).");
            }

            return importer.globalScale;
        }

        /// <summary>
        /// Longitud (en las unidades del importador, ya con su Scale Factor) de un hueso del esqueleto guardado
        /// en la HumanDescription del FBX. Sirve para comparar el tamaño del esqueleto de Yari con el de cada animación.
        /// </summary>
        private static float GetSkeletonBoneLength(ModelImporter imp, string boneSuffix)
        {
            if (imp == null) return 0f;
            var skeleton = imp.humanDescription.skeleton;
            if (skeleton == null) return 0f;
            foreach (var bone in skeleton)
            {
                if (bone.name != null && bone.name.EndsWith(boneSuffix))
                    return bone.position.magnitude;
            }
            return 0f;
        }

        /// <summary>Carga una textura de datos (metal, suavidad, oclusión) asegurando que NO sea sRGB.</summary>
        private static Texture2D LoadLinearTexture(string path)
        {
            if (!File.Exists(path)) return null;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.sRGBTexture)
            {
                ti.sRGBTexture = false;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static bool RebuildAvatarIfInconsistent(ModelImporter importer, string fbxPath, float realSpineLen)
        {
            if (importer == null || realSpineLen <= 0f) return false;

            float avatarSpineLen = GetSkeletonBoneLength(importer, "Spine1");
            if (avatarSpineLen > 0f && Mathf.Abs(avatarSpineLen / realSpineLen - 1f) < 0.02f) return false; // ya coincide

            // Borrar el mapeo/esqueleto viejo y dejar que Unity lo genere otra vez (Mixamo se mapea solo)
            var hd = importer.humanDescription;
            hd.human = new HumanBone[0];
            hd.skeleton = new SkeletonBone[0];
            importer.humanDescription = hd;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            float newLen = GetSkeletonBoneLength(importer, "Spine1");
            Debug.Log($"<color=green>[Ayni]</color> Avatar Humanoid de Yari regenerado (esqueleto de referencia " +
                      $"Spine1 {avatarSpineLen:0.#####} → {newLen:0.#####}; hueso real {realSpineLen:0.#####}).");
            return true;
        }

        /// <summary>
        /// Longitud real en Unity (localPosition, ya con Scale Factor y unidades del archivo) de un hueso del FBX importado.
        /// Es exactamente el valor que el Animator escribe en los huesos, por eso sirve para igualar animaciones y modelo.
        /// </summary>
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

        /// <summary>Altura (Y) de la cadera "Hips" del FBX en su pose por defecto, con el Scale Factor actual.</summary>
        private static float MeasureHipsHeight(string fbxPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null) return 0f;

            var temp = Object.Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;
            temp.transform.localScale = Vector3.one;

            float h = 0f;
            foreach (var t in temp.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("Hips")) { h = Mathf.Abs(t.position.y); break; }
            }
            Object.DestroyImmediate(temp);
            return h;
        }

        private static float MeasureModelHeight(string fbxPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null) return 0f;

            var temp = Object.Instantiate(prefab);
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
            Object.DestroyImmediate(temp);

            return hasBounds ? bounds.size.y : 0f;
        }

        private static AnimationClip LoadClipFromFBX(string fbxPath)
        {
            if (!File.Exists(fbxPath)) return null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var obj in assets)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        [MenuItem("Ayni/1. Generar Animator Controller y Rig Humanoid de Yari")]
        public static void CreateYariAnimatorController()
        {
            string folderPath = "Assets/Art/Characters";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Characters");
            }

            // Configurar ruts humanoid primero
            Avatar yariAvatar = ConfigureHumanoidRigs();

            string controllerPath = $"{folderPath}/Yari_AnimatorController.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Parámetros de animación estilo Sifu
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGuarding", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsStunned", AnimatorControllerParameterType.Bool);
            controller.AddParameter("LightAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("DuckAvoid", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpAvoid", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // Cargar Clips de Animación descargados de Mixamo
            string animFolder = "Assets/Art/Characters/Animations";
            AnimationClip idleClip = LoadClipFromFBX($"{animFolder}/Combat_Idle.fbx");
            AnimationClip jogClip = LoadClipFromFBX($"{animFolder}/Jog_Forward_InPlace.fbx");
            AnimationClip punchClip = LoadClipFromFBX($"{animFolder}/RumiMaki_LightPunch.fbx");
            AnimationClip kickClip = LoadClipFromFBX($"{animFolder}/RumiMaki_HeavyKick.fbx");
            AnimationClip dodgeClip = LoadClipFromFBX($"{animFolder}/Sifu_DuckAvoid.fbx");
            AnimationClip hitClip = LoadClipFromFBX($"{animFolder}/Impact_Hit.fbx");
            AnimationClip deathClip = LoadClipFromFBX($"{animFolder}/Defeat_Death.fbx");

            if (idleClip == null || jogClip == null || punchClip == null || kickClip == null ||
                dodgeClip == null || hitClip == null || deathClip == null)
            {
                Debug.LogWarning("[Ayni] Falta algún clip en Assets/Art/Characters/Animations. Nombres esperados: " +
                                 "Combat_Idle, Jog_Forward_InPlace, RumiMaki_LightPunch, RumiMaki_HeavyKick, " +
                                 "Sifu_DuckAvoid, Impact_Hit, Defeat_Death (.fbx)");
            }

            var rootStateMachine = controller.layers[0].stateMachine;

            // 1. Estado Locomoción: Blend Tree (Combat Idle <-> Jog Forward)
            //    CreateBlendTreeInController crea el estado + el árbol y lo guarda dentro del .controller
            BlendTree blendTree;
            var idleState = controller.CreateBlendTreeInController("Idle_Walk_Run", out blendTree, 0);
            blendTree.name = "Locomotion_BlendTree";
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";
            blendTree.useAutomaticThresholds = false;
            if (idleClip != null) blendTree.AddChild(idleClip, 0f);
            if (jogClip != null) blendTree.AddChild(jogClip, 1f);

            // 2. Guardia y Combate
            var guardState = rootStateMachine.AddState("Guard_Stance");
            if (idleClip != null) guardState.motion = idleClip;

            var lightAttackState = rootStateMachine.AddState("RumiMaki_LightStrike");
            if (punchClip != null) lightAttackState.motion = punchClip;

            var heavyAttackState = rootStateMachine.AddState("RumiMaki_HeavyImpact");
            if (kickClip != null) heavyAttackState.motion = kickClip;

            var duckAvoidState = rootStateMachine.AddState("Sifu_DuckAvoid");
            if (dodgeClip != null) duckAvoidState.motion = dodgeClip;

            var jumpAvoidState = rootStateMachine.AddState("Sifu_JumpAvoid");
            if (dodgeClip != null) jumpAvoidState.motion = dodgeClip;

            var hitState = rootStateMachine.AddState("Impact_Hit");
            if (hitClip != null) hitState.motion = hitClip;

            var stunnedState = rootStateMachine.AddState("BrokenStructure_Stunned");
            if (hitClip != null) stunnedState.motion = hitClip;

            var dieState = rootStateMachine.AddState("Defeat_Death");
            if (deathClip != null) dieState.motion = deathClip;

            rootStateMachine.defaultState = idleState;

            // Transiciones desde AnyState
            AddTriggerTransition(rootStateMachine, lightAttackState, "LightAttack");
            AddTriggerTransition(rootStateMachine, heavyAttackState, "HeavyAttack");
            AddTriggerTransition(rootStateMachine, duckAvoidState, "DuckAvoid");
            AddTriggerTransition(rootStateMachine, jumpAvoidState, "JumpAvoid");
            AddTriggerTransition(rootStateMachine, hitState, "Hit");
            AddTriggerTransition(rootStateMachine, dieState, "Die");

            // Transición a guardia
            var toGuard = idleState.AddTransition(guardState);
            toGuard.AddCondition(AnimatorConditionMode.If, 0, "IsGuarding");
            toGuard.hasExitTime = false;

            var fromGuard = guardState.AddTransition(idleState);
            fromGuard.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGuarding");
            fromGuard.hasExitTime = false;

            // Transición de ataques de regreso a Idle
            lightAttackState.AddTransition(idleState).hasExitTime = true;
            heavyAttackState.AddTransition(idleState).hasExitTime = true;
            duckAvoidState.AddTransition(guardState).hasExitTime = true;
            jumpAvoidState.AddTransition(guardState).hasExitTime = true;
            hitState.AddTransition(idleState).hasExitTime = true;

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[Ayni]</color> Animator Controller generado y clips de combate asignados en: {controllerPath}");
        }

        private static void AddTriggerTransition(AnimatorStateMachine sm, AnimatorState targetState, string triggerName)
        {
            var trans = sm.AddAnyStateTransition(targetState);
            trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            trans.hasExitTime = false;
            trans.duration = 0.1f;
        }

        [MenuItem("Ayni/2. Integrar Combate en el Mapa de Caminos")]
        public static void SetupCombatInPathMap()
        {
            // 0. Activar URP primero
            FixMagentaTerrain();

            // 1. Generar Animator Controller con clips descargados
            CreateYariAnimatorController();

            // 2. Buscar si existe la escena del mapa importada
            string[] sceneGuids = AssetDatabase.FindAssets("SampleScene t:Scene");
            string mapScenePath = "";
            foreach (var guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("network of paths") || path.Contains("Network"))
                {
                    mapScenePath = path;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(mapScenePath) && EditorSceneManager.GetActiveScene().path != mapScenePath)
            {
                EditorSceneManager.OpenScene(mapScenePath);
                Debug.Log($"[Ayni] Escena del mapa montañoso abierta: {mapScenePath}");
            }

            // Desactivar el FirstPersonAIO para usar la cámara en 3ª persona de Sifu
            var fpsControllers = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var comp in fpsControllers)
            {
                if (comp.GetType().Name.Contains("FirstPerson"))
                {
                    comp.gameObject.SetActive(false);
                    Debug.Log("[Ayni] FirstPersonAIO desactivado para sustituirlo por el combate en 3ª persona.");
                }
            }

            // Detectar posición del sendero donde estaba la cámara original del mapa (609.8f, 464.8f)
            Vector3 spawnPos = new Vector3(612f, 0f, 468f);
            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                float terrainY = activeTerrain.SampleHeight(spawnPos) + activeTerrain.transform.position.y;
                spawnPos.y = terrainY + 0.1f;
            }
            else
            {
                spawnPos.y = 8.5f;
            }

            // 3. Spawning Protagonista Yari con Modelo 3D Riggeado
            GameObject player = GameObject.Find("Yari_Hero");
            if (player == null)
            {
                player = new GameObject("Yari_Hero");
            }
            player.tag = "Player";
            player.transform.position = spawnPos;

            // Quitar primitivas si existían
            var oldFilter = player.GetComponent<MeshFilter>();
            if (oldFilter != null) Object.DestroyImmediate(oldFilter);
            var oldRenderer = player.GetComponent<MeshRenderer>();
            if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);
            var oldCapCollider = player.GetComponent<CapsuleCollider>();
            if (oldCapCollider != null) Object.DestroyImmediate(oldCapCollider);

            var cc = GetOrAdd<CharacterController>(player);
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1f, 0f);

            // Cargar y adjuntar modelo 3D con Rig y Texturas PBR
            AttachYari3DModel(player);

            if (!player.GetComponent<StructureSystem>()) player.AddComponent<StructureSystem>();
            if (!player.GetComponent<IllaTalismanSystem>()) player.AddComponent<IllaTalismanSystem>();
            var combatCtrl = GetOrAdd<YariCombatController>(player);

            // 4. Spawning Jefe Apo Rumi en el sendero
            Vector3 bossPos = spawnPos + new Vector3(2f, 0f, 9f);
            if (activeTerrain != null)
            {
                bossPos.y = activeTerrain.SampleHeight(bossPos) + activeTerrain.transform.position.y + 1.25f;
            }

            GameObject boss = GameObject.Find("Jefe_ApoRumi_Test");
            if (boss == null)
            {
                boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                boss.name = "Jefe_ApoRumi_Test";
            }
            boss.transform.position = bossPos;
            boss.transform.localScale = new Vector3(1.35f, 1.25f, 1.35f);

            var bossRenderer = boss.GetComponent<MeshRenderer>();
            if (bossRenderer) bossRenderer.material.color = new Color(0.55f, 0.35f, 0.15f);

            if (!boss.GetComponent<StructureSystem>()) boss.AddComponent<StructureSystem>();
            var enemyCtrl = GetOrAdd<EnemyController>(boss);

            SerializedObject soBoss = new SerializedObject(enemyCtrl);
            soBoss.FindProperty("characterName").stringValue = "Apo Rumi el Acaparador";
            soBoss.FindProperty("isBoss").boolValue = true;
            soBoss.FindProperty("maxHealth").floatValue = 250f;
            soBoss.ApplyModifiedProperties();

            // 5. Cámara en tercera persona sobre el hombro
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camObj = GameObject.Find("Main Camera");
                if (camObj == null) camObj = new GameObject("Main Camera");
                cam = GetOrAdd<Camera>(camObj);
                camObj.tag = "MainCamera";
            }

            // Asegurar un solo AudioListener
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 1; i < listeners.Length; i++)
            {
                Object.DestroyImmediate(listeners[i]);
            }

            // Posicionar la cámara detrás de Yari inicialmente
            cam.transform.position = spawnPos + new Vector3(0.5f, 2.0f, -3.5f);
            cam.transform.LookAt(spawnPos + Vector3.up * 1.4f);

            var sifuCam = GetOrAdd<ThirdPersonSifuCamera>(cam.gameObject);
            SerializedObject soCam = new SerializedObject(sifuCam);
            soCam.FindProperty("target").objectReferenceValue = player.transform;
            soCam.ApplyModifiedProperties();

            // Asignar referencia de cámara al jugador
            SerializedObject soCombat = new SerializedObject(combatCtrl);
            soCombat.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
            soCombat.ApplyModifiedProperties();

            // 6. Game Manager & HUD
            GameObject gm = GameObject.Find("GameManager_Ayni");
            if (gm == null) gm = new GameObject("GameManager_Ayni");
            if (!gm.GetComponent<AyniPurificationManager>()) gm.AddComponent<AyniPurificationManager>();
            if (!gm.GetComponent<SifuCombatHUD>()) gm.AddComponent<SifuCombatHUD>();

            // 7. GUARDAR LA ESCENA EN DISCO
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            bool saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[AYNI COMPLETO]</color> ¡Yari con su modelo 3D riggeado y animaciones de combate, Apo Rumi, la cámara sobre el hombro y el HUD han sido guardados permanentemente en la escena! (Guardado: {saved})");
        }

        [MenuItem("Ayni/3. Cargar Modelo 3D de Yari Riggeado con Texturas PBR")]
        public static void LoadYari3DModelMenu()
        {
            GameObject player = GameObject.Find("Yari_Hero");
            if (player == null)
            {
                Debug.LogWarning("[Ayni] No se encontró Yari_Hero en la escena. Ejecuta primero '2. Integrar Combate en el Mapa de Caminos'.");
                return;
            }

            AttachYari3DModel(player);
            Debug.Log("<color=green>[Ayni]</color> ¡Modelo 3D riggeado de Yari y texturas PBR aplicados con éxito!");
        }

        public static void AttachYari3DModel(GameObject player)
        {
            Transform existingVisual = player.transform.Find("Visual_Yari_3D");
            if (existingVisual != null)
            {
                Object.DestroyImmediate(existingVisual.gameObject);
            }

            // Priorizar modelo riggeado con esqueleto
            string riggedFbxPath = "Assets/Art/Characters/Yari_Rigged.fbx";
            string fbxPath = File.Exists(riggedFbxPath) ? riggedFbxPath : "Assets/Art/Characters/yari_3d_model_game_ready.fbx";
            if (!File.Exists(fbxPath))
            {
                fbxPath = "Assets/Art/Characters/yari_3d_model.fbx";
            }

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[Ayni] No se encontró el modelo FBX en {fbxPath}. Verificando assets...");
                return;
            }

            GameObject visualObj = Object.Instantiate(modelPrefab, player.transform);
            visualObj.name = "Visual_Yari_3D";
            visualObj.transform.localPosition = Vector3.zero;
            visualObj.transform.localRotation = Quaternion.identity;

            // Escala humana: Si es el modelo riggeado de Mixamo viene en escala 1.0 (metros). Si es el modelo crudo se escala a 1.85.
            if (fbxPath == riggedFbxPath)
            {
                visualObj.transform.localScale = Vector3.one;
            }
            else
            {
                visualObj.transform.localScale = new Vector3(1.85f, 1.85f, 1.85f);
            }

            // Crear y configurar Material PBR
            string matPath = "Assets/Art/Characters/M_Yari_PBR.mat";
            Material yariMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (yariMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                yariMat = new Material(litShader);
                AssetDatabase.CreateAsset(yariMat, matPath);
            }

            // Asignar Albedo (Base Map)
            string[] albedoGuids = AssetDatabase.FindAssets("Color_ t:Texture2D", new[] { "Assets/Art/Characters/Textures" });
            if (albedoGuids.Length > 0)
            {
                Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(albedoGuids[0]));
                yariMat.SetTexture("_BaseMap", albedoTex);
                yariMat.SetTexture("_MainTex", albedoTex);
            }

            // Asignar Normal Map
            string[] normGuids = AssetDatabase.FindAssets("NormalGL_ t:Texture2D", new[] { "Assets/Art/Characters/Textures" });
            if (normGuids.Length > 0)
            {
                string normPath = AssetDatabase.GUIDToAssetPath(normGuids[0]);
                TextureImporter texImporter = AssetImporter.GetAtPath(normPath) as TextureImporter;
                if (texImporter != null && texImporter.textureType != TextureImporterType.NormalMap)
                {
                    texImporter.textureType = TextureImporterType.NormalMap;
                    texImporter.SaveAndReimport();
                }
                Texture2D normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
                yariMat.SetTexture("_BumpMap", normTex);
                yariMat.EnableKeyword("_NORMALMAP");
            }

            // Metal / Suavidad / Oclusión
            // La textura ORM de Tripo trae: R = oclusión, G = rugosidad, B = metal.
            // URP NO la entiende así (lee el metal en R): usar la ORM directa volvía a Yari 100% metálico (bronce).
            // Por eso se usan dos texturas separadas y convertidas:
            //   Yari_MetallicSmoothness.png -> R = metal, A = suavidad (1 - rugosidad)
            //   Yari_Occlusion.png          -> oclusión en escala de grises
            const string texFolder = "Assets/Art/Characters/Textures";
            Texture2D msTex = LoadLinearTexture($"{texFolder}/Yari_MetallicSmoothness.png");
            Texture2D occTex = LoadLinearTexture($"{texFolder}/Yari_Occlusion.png");

            yariMat.SetFloat("_WorkflowMode", 1f);              // Metallic
            yariMat.SetFloat("_SmoothnessTextureChannel", 0f);  // suavidad en el alfa del mapa metálico
            yariMat.SetFloat("_Metallic", 0f);
            if (msTex != null)
            {
                yariMat.SetTexture("_MetallicGlossMap", msTex);
                yariMat.SetFloat("_Smoothness", 1f);            // multiplicador; el valor real viene de la textura
                yariMat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                // Sin textura convertida: material no metálico, algo de brillo suave
                yariMat.SetTexture("_MetallicGlossMap", null);
                yariMat.SetFloat("_Smoothness", 0.3f);
                yariMat.DisableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (occTex != null)
            {
                yariMat.SetTexture("_OcclusionMap", occTex);
                yariMat.SetFloat("_OcclusionStrength", 1f);
                yariMat.EnableKeyword("_OCCLUSIONMAP");
            }
            yariMat.SetColor("_BaseColor", Color.white);

            // Aplicar el material a todos los renderers (SkinnedMeshRenderer o MeshRenderer)
            var renderers = visualObj.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                rend.sharedMaterial = yariMat;
            }

            // Asignar Animator y Avatar
            // El Animator va en el modelo (Visual_Yari_3D), que es donde están los huesos de Mixamo.
            // Si se pone en Yari_Hero (el padre), el Avatar no encuentra los huesos y Yari se queda en T-Pose.
            var rootAnim = player.GetComponent<Animator>();
            if (rootAnim != null) Object.DestroyImmediate(rootAnim);

            var anim = GetOrAdd<Animator>(visualObj);
            var runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Characters/Yari_AnimatorController.controller");
            if (runtimeController != null) anim.runtimeAnimatorController = runtimeController;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Extraer y asignar Avatar Humanoid
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is Avatar av)
                {
                    anim.avatar = av;
                    break;
                }
            }

            if (anim.avatar == null || !anim.avatar.isHuman)
            {
                Debug.LogWarning("[Ayni] Yari no tiene un Avatar Humanoid válido: las animaciones no se verán. Ejecuta 'Ayni/1. Generar Animator Controller y Rig Humanoid de Yari'.");
            }

            EditorUtility.SetDirty(yariMat);
            EditorUtility.SetDirty(player);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
