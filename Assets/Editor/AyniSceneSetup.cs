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
        private static string AutoSetupKey => "AyniAutoSetupDone_v12_" + Application.dataPath.GetHashCode();

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
            float yariSkeletonSpine = GetSkeletonBoneLength(importer, "Spine1");
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

                    // Esqueleto de referencia copiado del Avatar: si quedó a otra escala (pasó con Neutral_Idle, Sprint,
                    // Crouch y Jump, guardados 14 000 veces más grandes), el clip coloca la cadera a la altura del
                    // suelo y Yari se hunde hasta la cintura. Se vuelve a copiar del Avatar de Yari.
                    float animSkeletonSpine = GetSkeletonBoneLength(animImporter, "Spine1");
                    bool staleSkeleton = yariSkeletonSpine > 0f &&
                                         (animSkeletonSpine <= 0f || Mathf.Abs(animSkeletonSpine / yariSkeletonSpine - 1f) > 0.02f);
                    if (staleSkeleton)
                    {
                        Debug.Log($"[Ayni] {Path.GetFileName(unityPath)}: esqueleto de referencia desfasado " +
                                  $"(Spine1 {animSkeletonSpine:0.#####} en vez de {yariSkeletonSpine:0.#####}); se vuelve a copiar del Avatar de Yari.");
                    }

                    bool needReimport = false;
                    if (animImporter.animationType != ModelImporterAnimationType.Human ||
                        animImporter.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                        animImporter.sourceAvatar != yariAvatar ||
                        Mathf.Abs(animImporter.globalScale - desiredScale) > desiredScale * 0.001f ||
                        rigRebuilt || staleSkeleton)
                    {
                        animImporter.animationType = ModelImporterAnimationType.Human;
                        animImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        animImporter.sourceAvatar = yariAvatar;
                        if (staleSkeleton) animImporter.humanDescription = importer.humanDescription;
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
                    // (Run_Jump lleva "run" en el nombre pero es un salto: no es cíclico)
                    bool shouldLoop = (fileName.Contains("idle") || fileName.Contains("jog") ||
                                       fileName.Contains("walk") || fileName.Contains("run") ||
                                       fileName.Contains("strafe") || fileName.Contains("loop")) &&
                                      !fileName.Contains("jump");

                    var clips = animImporter.clipAnimations;
                    if (clips == null || clips.Length == 0) clips = animImporter.defaultClipAnimations;

                    // Pasos laterales, hacia atrás y agachado: se conserva la orientación con la que se animó el clip.
                    // Con la opción por defecto ("Body Orientation") Unity gira el clip hacia donde mira el torso, y como
                    // en esos clips el torso va girado, Yari acababa dando los pasos en diagonal.
                    bool keepAuthoredFacing = fileName.StartsWith("strafe") || fileName == "walk_back" ||
                                              fileName.StartsWith("crouch_walk");
                    var takes = animImporter.defaultClipAnimations;

                    foreach (var c in clips)
                    {
                        // Clips cíclicos: el rango es la toma completa. Al sustituir un .fbx conservando su .meta, el rango
                        // guardado era el del clip anterior y el nuevo quedaba cortado a media zancada (salto en cada vuelta).
                        if (shouldLoop && takes != null)
                        {
                            foreach (var take in takes)
                            {
                                if (take.takeName != c.takeName) continue;
                                if (Mathf.Abs(c.firstFrame - take.firstFrame) > 0.01f || Mathf.Abs(c.lastFrame - take.lastFrame) > 0.01f)
                                {
                                    Debug.Log($"[Ayni] {Path.GetFileName(unityPath)}: rango del clip {c.firstFrame:0}-{c.lastFrame:0} → " +
                                              $"{take.firstFrame:0}-{take.lastFrame:0} (la toma completa).");
                                    c.firstFrame = take.firstFrame;
                                    c.lastFrame = take.lastFrame;
                                    needReimport = true;
                                }
                                break;
                            }
                        }
                        if (keepAuthoredFacing && !c.keepOriginalOrientation) { c.keepOriginalOrientation = true; needReimport = true; }

                        if (c.loopTime != shouldLoop) { c.loopTime = shouldLoop; needReimport = true; }
                        // Mantener a Yari en su sitio: la raíz no rota ni sube/baja por la animación
                        if (!c.lockRootRotation) { c.lockRootRotation = true; needReimport = true; }
                        // (el salto es la excepción: su altura la pone la física; lo configura AyniAttackTimingBaker.SetupJumpClip)
                        if (!fileName.Contains("jump") && !c.lockRootHeightY) { c.lockRootHeightY = true; needReimport = true; }
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

            // Preparar el clip de salto (medir despegue/aterrizaje y sacar la altura de la pose)
            try { AyniAttackTimingBaker.SetupJumpClip(); }
            catch (System.Exception e) { Debug.LogWarning("[Ayni] No se pudo preparar el clip de salto: " + e.Message); }

            string controllerPath = $"{folderPath}/Yari_AnimatorController.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Parámetros de animación estilo Sifu / Ayni
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("InCombatStance", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsGuarding", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsStunned", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("LightAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("DuckAvoid", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpAvoid", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "JumpSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });
            // Fijación de blanco: dirección del movimiento respecto al rival
            controller.AddParameter("IsLockedOn", AnimatorControllerParameterType.Bool);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            // Multiplicador de velocidad de los estados de ataque (lo fija YariCombatController en cada golpe)
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "AttackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });

            // Cargar Clips de Animación descargados de Mixamo
            string animFolder = "Assets/Art/Characters/Animations";
            AnimationClip neutralIdleClip = LoadClipFromFBX($"{animFolder}/Neutral_Idle.fbx");
            AnimationClip combatIdleClip = LoadClipFromFBX($"{animFolder}/Combat_Idle.fbx");
            AnimationClip jogClip = LoadClipFromFBX($"{animFolder}/Jog_Forward_InPlace.fbx");
            AnimationClip walkClip = LoadClipFromFBX($"{animFolder}/Walk_Forward_InPlace.fbx");
            AnimationClip runClip = LoadClipFromFBX($"{animFolder}/Run_Forward_InPlace.fbx");
            AnimationClip baseRunClip = runClip != null ? runClip : jogClip;
            AnimationClip sprintClip = LoadClipFromFBX($"{animFolder}/Sprint_Run_InPlace.fbx");
            AnimationClip crouchIdleClip = LoadClipFromFBX($"{animFolder}/Crouch_Idle.fbx");
            AnimationClip crouchWalkClip = LoadClipFromFBX($"{animFolder}/Crouch_Walk_InPlace.fbx");
            AnimationClip jumpClip = LoadClipFromFBX($"{animFolder}/Jump.fbx");
            AnimationClip runJumpClip = LoadClipFromFBX($"{animFolder}/Run_Jump.fbx");
            AnimationClip punchClip = LoadClipFromFBX($"{animFolder}/RumiMaki_LightPunch.fbx");
            AnimationClip kickClip = LoadClipFromFBX($"{animFolder}/RumiMaki_HeavyKick.fbx");
            AnimationClip dodgeClip = LoadClipFromFBX($"{animFolder}/Sifu_DuckAvoid.fbx");
            AnimationClip hitClip = LoadClipFromFBX($"{animFolder}/Impact_Hit.fbx");
            AnimationClip deathClip = LoadClipFromFBX($"{animFolder}/Defeat_Death.fbx");

            // Animaciones de combate nuevas (pelea andina de puños: Takanakuy / Tinku)
            AnimationClip guardIdleClip = LoadClipFromFBX($"{animFolder}/Guard_Idle.fbx");
            AnimationClip blockHitClip = LoadClipFromFBX($"{animFolder}/Guard_BlockHit.fbx");
            AnimationClip parryClip = LoadClipFromFBX($"{animFolder}/Parry_Deflect.fbx");
            AnimationClip duckClip = LoadClipFromFBX($"{animFolder}/Dodge_Duck.fbx");
            AnimationClip hopBackClip = LoadClipFromFBX($"{animFolder}/Dodge_HopBack.fbx");
            AnimationClip hitHeadClip = LoadClipFromFBX($"{animFolder}/Hit_Head.fbx");
            AnimationClip hitBodyClip = LoadClipFromFBX($"{animFolder}/Hit_Body.fbx");
            AnimationClip hitHeavyClip = LoadClipFromFBX($"{animFolder}/Hit_Heavy.fbx");
            AnimationClip stunnedLoopClip = LoadClipFromFBX($"{animFolder}/Stunned_Loop.fbx");

            if (guardIdleClip == null) guardIdleClip = combatIdleClip;
            if (duckClip == null) duckClip = dodgeClip;
            if (hopBackClip == null) hopBackClip = dodgeClip;
            if (hitHeadClip == null) hitHeadClip = hitClip;
            if (hitBodyClip == null) hitBodyClip = hitHeadClip;
            if (hitHeavyClip == null) hitHeavyClip = hitHeadClip;
            if (stunnedLoopClip == null) stunnedLoopClip = hitClip;
            if (blockHitClip == null) blockHitClip = guardIdleClip;
            if (parryClip == null) parryClip = guardIdleClip;

            // Respaldo por si falta alguno
            if (neutralIdleClip == null) neutralIdleClip = combatIdleClip;
            if (sprintClip == null) sprintClip = jogClip;
            if (crouchIdleClip == null) crouchIdleClip = neutralIdleClip;
            if (crouchWalkClip == null) crouchWalkClip = jogClip;

            var rootStateMachine = controller.layers[0].stateMachine;

            // 1. Locomoción Relajada (Neutral Idle <-> Walk <-> Run <-> Sprint)
            BlendTree relaxedBlendTree;
            var relaxedState = controller.CreateBlendTreeInController("Relaxed_Locomotion", out relaxedBlendTree, 0);
            relaxedBlendTree.name = "Relaxed_BlendTree";
            relaxedBlendTree.blendType = BlendTreeType.Simple1D;
            relaxedBlendTree.blendParameter = "Speed";
            relaxedBlendTree.useAutomaticThresholds = false;
            if (neutralIdleClip != null) relaxedBlendTree.AddChild(neutralIdleClip, 0f);
            if (walkClip != null) relaxedBlendTree.AddChild(walkClip, 0.5f);
            if (baseRunClip != null) relaxedBlendTree.AddChild(baseRunClip, 1f);
            if (sprintClip != null) relaxedBlendTree.AddChild(sprintClip, 2f);

            // 2. Locomoción de Combate (Combat Idle <-> Walk <-> Run <-> Sprint)
            BlendTree combatBlendTree;
            var combatState = controller.CreateBlendTreeInController("Combat_Locomotion", out combatBlendTree, 0);
            combatBlendTree.name = "Combat_BlendTree";
            combatBlendTree.blendType = BlendTreeType.Simple1D;
            combatBlendTree.blendParameter = "Speed";
            combatBlendTree.useAutomaticThresholds = false;
            if (combatIdleClip != null) combatBlendTree.AddChild(combatIdleClip, 0f);
            if (walkClip != null) combatBlendTree.AddChild(walkClip, 0.5f);
            if (baseRunClip != null) combatBlendTree.AddChild(baseRunClip, 1f);
            if (sprintClip != null) combatBlendTree.AddChild(sprintClip, 2f);

            // 3. Locomoción Agachado / Cuclillas (Crouch Idle <-> Crouch Walk)
            BlendTree crouchBlendTree;
            var crouchState = controller.CreateBlendTreeInController("Crouch_Locomotion", out crouchBlendTree, 0);
            crouchBlendTree.name = "Crouch_BlendTree";
            crouchBlendTree.blendType = BlendTreeType.Simple1D;
            crouchBlendTree.blendParameter = "Speed";
            crouchBlendTree.useAutomaticThresholds = false;
            if (crouchIdleClip != null) crouchBlendTree.AddChild(crouchIdleClip, 0f);
            if (crouchWalkClip != null) crouchBlendTree.AddChild(crouchWalkClip, 1f);
            if (crouchWalkClip != null) crouchBlendTree.AddChild(crouchWalkClip, 2f);

            // 3b. Locomoción con el rival fijado: avanzar, retroceder y pasos laterales sin dejar de encararlo
            AnimationClip strafeLeftClip = LoadClipFromFBX($"{animFolder}/Strafe_Left.fbx");
            AnimationClip strafeRightClip = LoadClipFromFBX($"{animFolder}/Strafe_Right.fbx");
            AnimationClip walkBackClip = LoadClipFromFBX($"{animFolder}/Walk_Back.fbx");

            BlendTree lockOnBlendTree;
            var lockOnState = controller.CreateBlendTreeInController("LockOn_Locomotion", out lockOnBlendTree, 0);
            lockOnBlendTree.name = "LockOn_BlendTree";
            lockOnBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            lockOnBlendTree.blendParameter = "MoveX";
            lockOnBlendTree.blendParameterY = "MoveY";
            if (combatIdleClip != null) lockOnBlendTree.AddChild(combatIdleClip, new Vector2(0f, 0f));
            // Hacia el rival se va al mismo paso que al correr sin fijarlo (y con su misma cadencia)
            if (baseRunClip != null) lockOnBlendTree.AddChild(baseRunClip, new Vector2(0f, 1f));
            if (walkBackClip != null) lockOnBlendTree.AddChild(walkBackClip, new Vector2(0f, -1f));
            if (strafeLeftClip != null) lockOnBlendTree.AddChild(strafeLeftClip, new Vector2(-1f, 0f));
            if (strafeRightClip != null) lockOnBlendTree.AddChild(strafeRightClip, new Vector2(1f, 0f));

            // Los clips se reproducen a velocidad normal dentro de los BlendTrees. La cadencia de los pasos la pone
            // YariCombatController en cada fotograma (velocidad real / lo que cubre el clip, medido por
            // AyniAttackTimingBaker), así que sigue a la velocidad de Yari aunque cambie con la edad de la Illa.

            // El estado por defecto es la postura natural relajada
            rootStateMachine.defaultState = relaxedState;

            // 4. Estados de Acciones de Combate, Guardia y Salto
            var guardState = rootStateMachine.AddState("Guard_Stance");
            if (guardIdleClip != null) guardState.motion = guardIdleClip;

            var jumpState = rootStateMachine.AddState("Jump");
            if (jumpClip != null) jumpState.motion = jumpClip;
            // La velocidad la fija el código para que el clip dure lo mismo que el salto real
            jumpState.speedParameterActive = true;
            jumpState.speedParameter = "JumpSpeed";

            // Salto en carrera: lo lanza el código (CrossFade) cuando Yari salta corriendo
            AnimatorState runJumpState = null;
            if (runJumpClip != null)
            {
                runJumpState = rootStateMachine.AddState("Run_Jump");
                runJumpState.motion = runJumpClip;
                runJumpState.speedParameterActive = true;
                runJumpState.speedParameter = "JumpSpeed";
            }

            var lightAttackState = rootStateMachine.AddState("RumiMaki_LightStrike");
            if (punchClip != null) lightAttackState.motion = punchClip;

            var heavyAttackState = rootStateMachine.AddState("RumiMaki_HeavyImpact");
            if (kickClip != null) heavyAttackState.motion = kickClip;

            var duckAvoidState = rootStateMachine.AddState("Sifu_DuckAvoid");
            if (duckClip != null) duckAvoidState.motion = duckClip;
            duckAvoidState.speed = 1.5f;

            var jumpAvoidState = rootStateMachine.AddState("Sifu_JumpAvoid");
            if (hopBackClip != null) jumpAvoidState.motion = hopBackClip;
            jumpAvoidState.speed = 1.8f;

            var hitState = rootStateMachine.AddState("Impact_Hit");
            if (hitHeadClip != null) hitState.motion = hitHeadClip;
            hitState.speed = 1.3f;

            var stunnedState = rootStateMachine.AddState("BrokenStructure_Stunned");
            if (stunnedLoopClip != null) stunnedState.motion = stunnedLoopClip;

            var dieState = rootStateMachine.AddState("Defeat_Death");
            if (deathClip != null) dieState.motion = deathClip;

            // 5. Combo Rumi Maki: estados reproducidos por código (YariCombatController los lanza con CrossFade,
            //    saltándose la preparación larga del clip y con la velocidad del parámetro AttackSpeed).
            string[,] attackStates =
            {
                { "Atk_Light1", "Light_Punch_1_L" },
                { "Atk_Light2", "Light_Punch_2_R" },
                { "Atk_Light3", "Light_Punch_3_L" },
                { "Atk_Light4", "Light_Punch_4_R" },
                { "Atk_Overhand", "Heavy_Overhand" },
                { "Atk_Uppercut", "Heavy_Uppercut" },
                { "Atk_Elbow", "Heavy_Elbow" },
                { "Atk_Headbutt", "Heavy_Headbutt" },
                { "Atk_FrontKick", "Heavy_FrontKick" },
            };
            for (int i = 0; i < attackStates.GetLength(0); i++)
            {
                AnimationClip atkClip = LoadClipFromFBX($"{animFolder}/{attackStates[i, 1]}.fbx");
                if (atkClip == null) atkClip = punchClip;
                AddCodeDrivenState(rootStateMachine, attackStates[i, 0], atkClip, combatState, "AttackSpeed", 1f, 0.9f);
            }

            // Reacciones defensivas y de impacto lanzadas por código
            AddCodeDrivenState(rootStateMachine, "Guard_BlockHit", blockHitClip, guardState, null, 1.5f, 0.6f);
            AddCodeDrivenState(rootStateMachine, "Parry_Deflect", parryClip, guardState, null, 2.0f, 0.5f);
            AddCodeDrivenState(rootStateMachine, "Impact_HitBody", hitBodyClip, combatState, null, 1.3f, 0.45f);
            AddCodeDrivenState(rootStateMachine, "Impact_HitHeavy", hitHeavyClip, combatState, null, 1.0f, 0.7f);

            // Resurrección: Yari se levanta del suelo. La velocidad se calcula para que el tramo útil del clip
            // dure GetUpSeconds, igual que el valor "Get Up Duration" de YariCombatController.
            AnimationClip getUpClip = LoadClipFromFBX($"{animFolder}/GetUp.fbx");
            if (getUpClip != null)
            {
                const float GetUpSeconds = 2.0f;
                float usefulSpan = YariCombatController.GetUpEndNormalized - YariCombatController.GetUpStartNormalized;
                float getUpSpeed = Mathf.Max(0.5f, getUpClip.length * usefulSpan / GetUpSeconds);
                AddCodeDrivenState(rootStateMachine, "GetUp", getUpClip, combatState, null, getUpSpeed,
                                   YariCombatController.GetUpEndNormalized);
            }

            // Juicio Ayni: gesto de perdón de Yari
            AnimationClip mercyClip = LoadClipFromFBX($"{animFolder}/Mercy_Offer.fbx");
            if (mercyClip != null)
            {
                AddCodeDrivenState(rootStateMachine, "Mercy_Offer", mercyClip, relaxedState, null, 1.5f, 0.55f);
            }

            // Postura rota (lo usará el rival cuando tenga modelo): entra y sale con el bool IsStunned
            var anyToStunned = rootStateMachine.AddAnyStateTransition(stunnedState);
            anyToStunned.AddCondition(AnimatorConditionMode.If, 0, "IsStunned");
            anyToStunned.hasExitTime = false;
            anyToStunned.duration = 0.15f;
            anyToStunned.canTransitionToSelf = false;
            var stunnedToCombat = stunnedState.AddTransition(combatState);
            stunnedToCombat.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStunned");
            stunnedToCombat.hasExitTime = false;
            stunnedToCombat.duration = 0.2f;

            // Transiciones entre Locomoción Relajada y de Combate
            var relaxedToCombat = relaxedState.AddTransition(combatState);
            relaxedToCombat.AddCondition(AnimatorConditionMode.If, 0, "InCombatStance");
            relaxedToCombat.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            relaxedToCombat.hasExitTime = false;
            relaxedToCombat.duration = 0.2f;

            var combatToRelaxed = combatState.AddTransition(relaxedState);
            combatToRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombatStance");
            combatToRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            combatToRelaxed.hasExitTime = false;
            combatToRelaxed.duration = 0.25f;

            // Transiciones a Agachado
            var relaxedToCrouch = relaxedState.AddTransition(crouchState);
            relaxedToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            relaxedToCrouch.hasExitTime = false;
            relaxedToCrouch.duration = 0.15f;

            var combatToCrouch = combatState.AddTransition(crouchState);
            combatToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            combatToCrouch.hasExitTime = false;
            combatToCrouch.duration = 0.15f;

            var crouchToRelaxed = crouchState.AddTransition(relaxedState);
            crouchToRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            crouchToRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombatStance");
            crouchToRelaxed.hasExitTime = false;
            crouchToRelaxed.duration = 0.2f;

            var crouchToCombat = crouchState.AddTransition(combatState);
            crouchToCombat.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            crouchToCombat.AddCondition(AnimatorConditionMode.If, 0, "InCombatStance");
            crouchToCombat.hasExitTime = false;
            crouchToCombat.duration = 0.2f;

            // Transiciones a Guardia
            var relaxedToGuard = relaxedState.AddTransition(guardState);
            relaxedToGuard.AddCondition(AnimatorConditionMode.If, 0, "IsGuarding");
            relaxedToGuard.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); // guardia a cuerpo completo solo si está quieto
            relaxedToGuard.hasExitTime = false;
            relaxedToGuard.duration = 0.1f;

            var combatToGuard = combatState.AddTransition(guardState);
            combatToGuard.AddCondition(AnimatorConditionMode.If, 0, "IsGuarding");
            combatToGuard.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); // guardia a cuerpo completo solo si está quieto
            combatToGuard.hasExitTime = false;
            combatToGuard.duration = 0.1f;

            var crouchToGuard = crouchState.AddTransition(guardState);
            crouchToGuard.AddCondition(AnimatorConditionMode.If, 0, "IsGuarding");
            crouchToGuard.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); // guardia a cuerpo completo solo si está quieto
            crouchToGuard.hasExitTime = false;
            crouchToGuard.duration = 0.1f;

            var fromGuard = guardState.AddTransition(combatState);
            fromGuard.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGuarding");
            fromGuard.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLockedOn");
            fromGuard.hasExitTime = false;
            fromGuard.duration = 0.15f;

            // Al caminar en guardia las piernas vuelven a la locomoción; los brazos los mantiene la capa UpperBody
            AddConditionalTransition(guardState, combatState, 0.15f,
                (AnimatorConditionMode.IfNot, 0f, "IsLockedOn"), (AnimatorConditionMode.Greater, 0.1f, "Speed"));
            AddConditionalTransition(guardState, lockOnState, 0.15f,
                (AnimatorConditionMode.If, 0f, "IsLockedOn"), (AnimatorConditionMode.IfNot, 0f, "IsGuarding"));
            AddConditionalTransition(guardState, lockOnState, 0.15f,
                (AnimatorConditionMode.If, 0f, "IsLockedOn"), (AnimatorConditionMode.Greater, 0.1f, "Speed"));

            // Fijación de blanco: entrar y salir de la locomoción encarada al rival
            AddConditionalTransition(relaxedState, lockOnState, 0.2f, (AnimatorConditionMode.If, 0f, "IsLockedOn"));
            AddConditionalTransition(combatState, lockOnState, 0.2f, (AnimatorConditionMode.If, 0f, "IsLockedOn"));
            AddConditionalTransition(lockOnState, combatState, 0.2f, (AnimatorConditionMode.IfNot, 0f, "IsLockedOn"));
            AddConditionalTransition(lockOnState, crouchState, 0.15f, (AnimatorConditionMode.If, 0f, "IsCrouching"));
            AddConditionalTransition(lockOnState, guardState, 0.1f,
                (AnimatorConditionMode.If, 0f, "IsGuarding"), (AnimatorConditionMode.Less, 0.1f, "Speed"));

            // Salto y Regreso
            AddTriggerTransition(rootStateMachine, jumpState, "Jump");
            var jumpToRelaxed = jumpState.AddTransition(relaxedState);
            jumpToRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombatStance");
            jumpToRelaxed.hasExitTime = true;
            jumpToRelaxed.exitTime = 0.85f;
            jumpToRelaxed.duration = 0.15f;

            var jumpToCombat = jumpState.AddTransition(combatState);
            jumpToCombat.AddCondition(AnimatorConditionMode.If, 0, "InCombatStance");
            jumpToCombat.hasExitTime = true;
            jumpToCombat.exitTime = 0.85f;
            jumpToCombat.duration = 0.15f;

            if (runJumpState != null)
            {
                var runJumpToRelaxed = runJumpState.AddTransition(relaxedState);
                runJumpToRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombatStance");
                runJumpToRelaxed.hasExitTime = true;
                runJumpToRelaxed.exitTime = 0.9f;
                runJumpToRelaxed.duration = 0.12f;

                var runJumpToCombat = runJumpState.AddTransition(combatState);
                runJumpToCombat.AddCondition(AnimatorConditionMode.If, 0, "InCombatStance");
                runJumpToCombat.hasExitTime = true;
                runJumpToCombat.exitTime = 0.9f;
                runJumpToCombat.duration = 0.12f;
            }

            // Transiciones desde AnyState para Combate
            AddTriggerTransition(rootStateMachine, lightAttackState, "LightAttack");
            AddTriggerTransition(rootStateMachine, heavyAttackState, "HeavyAttack");
            AddTriggerTransition(rootStateMachine, duckAvoidState, "DuckAvoid");
            AddTriggerTransition(rootStateMachine, jumpAvoidState, "JumpAvoid");
            AddTriggerTransition(rootStateMachine, hitState, "Hit");
            AddTriggerTransition(rootStateMachine, dieState, "Die");

            // Retorno de ataques y reacciones a combate
            var lightToCombat = lightAttackState.AddTransition(combatState);
            lightToCombat.hasExitTime = true;
            lightToCombat.exitTime = 0.88f;
            lightToCombat.duration = 0.1f;

            var heavyToCombat = heavyAttackState.AddTransition(combatState);
            heavyToCombat.hasExitTime = true;
            heavyToCombat.exitTime = 0.88f;
            heavyToCombat.duration = 0.1f;

            var duckToGuard = duckAvoidState.AddTransition(guardState);
            duckToGuard.hasExitTime = true;
            duckToGuard.exitTime = 0.85f;
            duckToGuard.duration = 0.1f;

            var jumpAvoidToGuard = jumpAvoidState.AddTransition(guardState);
            jumpAvoidToGuard.hasExitTime = true;
            jumpAvoidToGuard.exitTime = 0.85f;
            jumpAvoidToGuard.duration = 0.1f;

            var hitToCombat = hitState.AddTransition(combatState);
            hitToCombat.hasExitTime = true;
            hitToCombat.exitTime = 0.45f;
            hitToCombat.duration = 0.2f;

            // Capa de brazos: mantiene la guardia alta mientras las piernas caminan (el código controla su peso)
            AvatarMask upperMask = GetOrCreateUpperBodyMask();
            controller.AddLayer("UpperBody");
            var layers = controller.layers;
            layers[0].iKPass = true; // necesario para que FootIK apoye los pies en el terreno
            layers[1].avatarMask = upperMask;
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            var upperGuardState = controller.layers[1].stateMachine.AddState("Guard_Upper");
            if (guardIdleClip != null) upperGuardState.motion = guardIdleClip;

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[Ayni]</color> Animator Controller generado (locomoción, combo Rumi Maki, guardia, esquivas y reacciones) en: {controllerPath}");

            // Al regenerar el controller, el Animator de Yari en la escena pierde la referencia: volver a asignarla
            GameObject sceneYari = GameObject.Find("Yari_Hero");
            if (sceneYari != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var sceneAnimator = sceneYari.GetComponentInChildren<Animator>(true);
                if (sceneAnimator != null && sceneAnimator.runtimeAnimatorController != controller)
                {
                    sceneAnimator.runtimeAnimatorController = controller;
                    EditorUtility.SetDirty(sceneAnimator);
                    EditorSceneManager.MarkSceneDirty(sceneYari.scene);
                    EditorSceneManager.SaveScene(sceneYari.scene);
                    Debug.Log("[Ayni] Animator Controller reasignado a Yari_Hero y escena guardada.");
                }
            }

            // Medir en los clips el instante real de impacto de cada golpe
            try { AyniAttackTimingBaker.Bake(); }
            catch (System.Exception e) { Debug.LogWarning("[Ayni] No se pudieron medir los tiempos de impacto: " + e.Message); }

            // Volver a poner las esquivas en el sitio, las caídas y el aterrizaje (Ayni > Animaciones)
            try { AyniAnimatorUpgrade.Apply(); }
            catch (System.Exception e) { Debug.LogWarning("[Ayni] No se pudieron añadir los estados generados: " + e.Message); }
        }

        private static void AddConditionalTransition(AnimatorState from, AnimatorState to, float duration,
            params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            foreach (var c in conditions) t.AddCondition(c.mode, c.threshold, c.parameter);
        }

        /// <summary>Máscara de tronco, cabeza y brazos para la capa de guardia.</summary>
        private static AvatarMask GetOrCreateUpperBodyMask()
        {
            const string maskPath = "Assets/Art/Characters/Yari_UpperBodyMask.mask";
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, maskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>Estado que el código reproduce con CrossFade y que vuelve solo a <paramref name="returnState"/>.</summary>
        private static AnimatorState AddCodeDrivenState(AnimatorStateMachine sm, string stateName, AnimationClip clip,
            AnimatorState returnState, string speedParameter, float speed, float exitTime)
        {
            var state = sm.AddState(stateName);
            if (clip != null) state.motion = clip;
            state.speed = speed;
            if (!string.IsNullOrEmpty(speedParameter))
            {
                state.speedParameterActive = true;
                state.speedParameter = speedParameter;
            }

            var back = state.AddTransition(returnState);
            back.hasExitTime = true;
            back.exitTime = exitTime;
            back.duration = 0.15f;
            return state;
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

            // 4. Jefe de prueba. Si Amaru ya está integrado (Ayni > Jefes), no se crea la cápsula de Apo Rumi.
            if (GameObject.Find("Jefe_Amaru_ElCazador") == null)
            {
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
            }

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

            if (visualObj.GetComponent<AndeanCombatStanceModifier>() == null)
            {
                visualObj.AddComponent<AndeanCombatStanceModifier>();
            }

            EditorUtility.SetDirty(yariMat);
            EditorUtility.SetDirty(player);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
