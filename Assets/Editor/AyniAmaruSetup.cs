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

namespace Ayni.Editor
{
    // Se ejecuta SOLO desde el menú. Antes corría en cada compilación y en cada Play: recreaba el modelo,
    // pisaba los valores del Inspector, guardaba la escena y lanzaba un error al entrar en Play.
    public static class AyniAmaruSetup
    {
        // Altura deseada de Amaru en metros
        private const float AmaruTargetHeight = 1.8f;

        [MenuItem("Ayni/Jefes/Integrar Jefe Amaru (Reemplazar Cápsula)")]
        public static void SetupAmaruBoss()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AyniAmaru] Sal del modo Play antes de integrar al jefe.");
                return;
            }

            string amaruFbxPath = "Assets/Art/Characters/Amaru_Rigged.fbx";
            if (!File.Exists(amaruFbxPath))
            {
                Debug.LogWarning("[AyniAmaru] No se encontró Amaru_Rigged.fbx");
                return;
            }

            // 1. Asegurar Avatar Humanoid en Amaru_Rigged.fbx
            ModelImporter importer = AssetImporter.GetAtPath(amaruFbxPath) as ModelImporter;
            if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            // 1b. Corregir la escala del modelo (llegó diminuto, igual que le pasó a Yari)
            FixAmaruScale(importer, amaruFbxPath);

            Avatar amaruAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(amaruFbxPath))
            {
                if (asset is Avatar av && av.isValid)
                {
                    amaruAvatar = av;
                    break;
                }
            }

            // 2. Crear o actualizar AnimatorController para Amaru
            AnimatorController animController = CreateOrGetAmaruAnimatorController();

            // 3. Crear Prefab de Amaru y/o reemplazar la cápsula en la escena activa
            ReplaceCapsuleInScene(amaruFbxPath, amaruAvatar, animController);
        }

        [MenuItem("Ayni/Jefes/Actualizar Animaciones de Amaru (sin tocar la escena)")]
        public static void UpdateAmaruAnimations()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AyniAmaru] Sal del modo Play antes de actualizar las animaciones.");
                return;
            }
            CreateOrGetAmaruAnimatorController();
            Debug.Log("<color=green>[AyniAmaru]</color> Animator Controller de Amaru actualizado (ataques variados, reacciones y cadencia del trote).");
        }

        public static AnimatorController CreateOrGetAmaruAnimatorController()
        {
            string controllerPath = "Assets/Art/Characters/Amaru_AnimatorController.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            // Parámetros requeridos por EnemyController
            EnsureParam(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParam(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParam(controller, "IsStunned", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "Die", AnimatorControllerParameterType.Trigger);
            EnsureParam(controller, "MercyKneel", AnimatorControllerParameterType.Trigger);
            EnsureParam(controller, "AttackSpeed", AnimatorControllerParameterType.Float);
            SetFloatDefault(controller, "AttackSpeed", 1f);

            // IK Pass en la capa base: necesario para que FootIK apoye los pies en el terreno
            var baseLayers = controller.layers;
            if (!baseLayers[0].iKPass)
            {
                baseLayers[0].iKPass = true;
                controller.layers = baseLayers;
            }

            var rootSm = controller.layers[0].stateMachine;

            // Cargar clips
            AnimationClip idleClip = LoadClip("Assets/Art/Characters/Animations/Combat_Idle.fbx");
            AnimationClip moveClip = LoadClip("Assets/Art/Characters/Animations/Jog_Forward_InPlace.fbx");
            AnimationClip attackClip = LoadClip("Assets/Art/Characters/Animations/Heavy_Elbow.fbx");
            AnimationClip hitClip = LoadClip("Assets/Art/Characters/Animations/Hit_Body.fbx");
            AnimationClip stunnedClip = LoadClip("Assets/Art/Characters/Animations/Stunned_Loop.fbx");
            AnimationClip dieClip = LoadClip("Assets/Art/Characters/Animations/Knockdown.fbx");
            AnimationClip kneelClip = LoadClip("Assets/Art/Characters/Animations/Enemy_Kneel.fbx");

            // Estados
            var idleState = GetOrCreateState(rootSm, "Idle", idleClip);
            var moveState = GetOrCreateState(rootSm, "Move", moveClip);
            var attackState = GetOrCreateState(rootSm, "Attack", attackClip);
            var hitState = GetOrCreateState(rootSm, "Hit", hitClip);
            var stunnedState = GetOrCreateState(rootSm, "Stunned", stunnedClip);
            var dieState = GetOrCreateState(rootSm, "Die", dieClip);
            var kneelState = GetOrCreateState(rootSm, "MercyKneel", kneelClip);

            rootSm.defaultState = idleState;

            // Ataques variados que EnemyController lanza por código, sincronizados con el instante del golpe:
            // altos = puñetazo descendente, codazo y gancho; bajo = patada frontal.
            string[,] attackStates =
            {
                { "Atk_Overhand", "Heavy_Overhand" },
                { "Atk_Elbow", "Heavy_Elbow" },
                { "Atk_Uppercut", "Heavy_Uppercut" },
                { "Atk_FrontKick", "Heavy_FrontKick" },
            };
            for (int i = 0; i < attackStates.GetLength(0); i++)
            {
                AnimationClip atkClip = LoadClip($"Assets/Art/Characters/Animations/{attackStates[i, 1]}.fbx");
                if (atkClip == null) atkClip = attackClip;
                var atkState = GetOrCreateState(rootSm, attackStates[i, 0], atkClip);
                atkState.speed = 1f;
                atkState.speedParameterActive = true;
                atkState.speedParameter = "AttackSpeed";
                EnsureExitToIdle(atkState, idleState, 0.85f);
            }

            // Reacciones a los golpes: cabeza, cuerpo (estado "Hit") y golpe fuerte
            var hitHeadState = GetOrCreateState(rootSm, "Hit_Head", LoadClip("Assets/Art/Characters/Animations/Hit_Head.fbx") ?? hitClip);
            var hitHeavyState = GetOrCreateState(rootSm, "Hit_Heavy", LoadClip("Assets/Art/Characters/Animations/Hit_Heavy.fbx") ?? hitClip);
            hitState.speed = 1.3f;
            hitHeadState.speed = 1.3f;
            hitHeavyState.speed = 1.1f;
            EnsureExitToIdle(hitHeadState, idleState, 0.5f);
            EnsureExitToIdle(hitHeavyState, idleState, 0.7f);

            // Derribo: cae y se levanta (EnemyController controla el paso de uno a otro)
            AnimationClip getUpClip = LoadClip("Assets/Art/Characters/Animations/GetUp.fbx");
            var knockdownState = GetOrCreateState(rootSm, "Knockdown", dieClip);
            knockdownState.speed = 1.3f;
            if (getUpClip != null)
            {
                var getUpState = GetOrCreateState(rootSm, "GetUp", getUpClip);
                // El tramo útil del clip (del 25 % al 88 %) dura 1.6 s
                getUpState.speed = Mathf.Max(0.5f, getUpClip.length * 0.63f / 1.6f);
                EnsureExitToIdle(getUpState, idleState, 0.92f);
            }

            // Cadencia del trote ajustada a la velocidad real del jefe para que los pies patinen menos
            float bossMoveSpeed = 3.4f;
            GameObject sceneBoss = GameObject.Find("Jefe_Amaru_ElCazador");
            var bossCtrl = sceneBoss != null ? sceneBoss.GetComponent<EnemyController>() : null;
            if (bossCtrl != null) bossMoveSpeed = new SerializedObject(bossCtrl).FindProperty("moveSpeed").floatValue;
            float naturalJog = 0f;
            try { naturalJog = AyniAttackTimingBaker.MeasureGroundSpeed(moveClip); }
            catch (Exception e) { Debug.LogWarning("[AyniAmaru] No se pudo medir el trote: " + e.Message); }
            if (naturalJog > 0f)
            {
                moveState.speed = Mathf.Clamp(bossMoveSpeed / naturalJog, 0.7f, 1.15f);
                Debug.Log($"[AyniAmaru] Trote: velocidad natural {naturalJog:0.00} m/s, Amaru se mueve a {bossMoveSpeed:0.00} m/s → reproducción a {moveState.speed:0.00}x.");
            }

            // Transiciones Locomoción
            if (!HasTransition(idleState, moveState))
            {
                var t = idleState.AddTransition(moveState);
                t.hasExitTime = false;
                t.duration = 0.2f;
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }

            if (!HasTransition(moveState, idleState))
            {
                var t = moveState.AddTransition(idleState);
                t.hasExitTime = false;
                t.duration = 0.2f;
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }

            // Transiciones AnyState -> Acciones
            EnsureAnyTransition(rootSm, attackState, "Attack", AnimatorConditionMode.If);
            EnsureAnyTransition(rootSm, hitState, "Hit", AnimatorConditionMode.If);
            // El aturdido no debe reiniciarse a sí mismo mientras IsStunned siga activo
            EnsureAnyTransition(rootSm, stunnedState, "IsStunned", AnimatorConditionMode.If, false);
            EnsureAnyTransition(rootSm, dieState, "Die", AnimatorConditionMode.If);
            EnsureAnyTransition(rootSm, kneelState, "MercyKneel", AnimatorConditionMode.If);

            // Regreso al Idle tras animación
            EnsureExitToIdle(attackState, idleState, 0.85f);
            EnsureExitToIdle(hitState, idleState, 0.5f);

            // Recuperación de Stun
            if (!HasTransition(stunnedState, idleState))
            {
                var t = stunnedState.AddTransition(idleState);
                t.hasExitTime = false;
                t.duration = 0.25f;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsStunned");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void ReplaceCapsuleInScene(string amaruFbxPath, Avatar amaruAvatar, RuntimeAnimatorController animController)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return;

            // Buscar el objeto existente del jefe (la cápsula o placeholder previo)
            GameObject bossObj = GameObject.Find("Jefe_Amaru_ElCazador");
            if (bossObj == null) bossObj = GameObject.Find("Jefe_ApoRumi_Test");
            if (bossObj == null) bossObj = GameObject.Find("Enemy_Boss");

            Vector3 spawnPos = new Vector3(612f, 8.5f, 478f);
            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                spawnPos.y = activeTerrain.SampleHeight(spawnPos) + activeTerrain.transform.position.y + 0.1f;
            }

            if (bossObj == null)
            {
                bossObj = new GameObject("Jefe_Amaru_ElCazador");
                bossObj.transform.position = spawnPos;
            }
            else
            {
                bossObj.name = "Jefe_Amaru_ElCazador";
            }

            // La cápsula original estaba deformada (1.35 x 1.25 x 1.35) y con el pivote en su centro:
            // el jefe usa escala 1 y el pivote en los pies, apoyado sobre el terreno.
            bossObj.transform.localScale = Vector3.one;
            if (activeTerrain != null)
            {
                Vector3 p = bossObj.transform.position;
                p.y = activeTerrain.SampleHeight(p) + activeTerrain.transform.position.y + 0.05f;
                bossObj.transform.position = p;
            }

            // 1. Quitar primitivas gráficas de cápsula
            var mf = bossObj.GetComponent<MeshFilter>();
            if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
            var mr = bossObj.GetComponent<MeshRenderer>();
            if (mr != null) UnityEngine.Object.DestroyImmediate(mr);
            var capCol = bossObj.GetComponent<CapsuleCollider>();
            if (capCol != null) UnityEngine.Object.DestroyImmediate(capCol);

            // 2. Configurar CharacterController para movimiento y colisión física
            var cc = bossObj.GetComponent<CharacterController>();
            if (cc == null) cc = bossObj.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1.0f, 0f);

            // 3. Destruir modelo visual anterior si existe y reinstanciar Amaru_Rigged
            Transform oldVisual = bossObj.transform.Find("Visual_Amaru_3D");
            if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(amaruFbxPath);
            if (modelPrefab != null)
            {
                GameObject visual = UnityEngine.Object.Instantiate(modelPrefab, bossObj.transform);
                visual.name = "Visual_Amaru_3D";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                // Aplicar material M_Amaru_PBR si existe
                Material amaruMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/M_Amaru_PBR.mat");
                if (amaruMat != null)
                {
                    FixAmaruMaterial(amaruMat);
                    foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.sharedMaterial = amaruMat;
                    }
                }

                // Configurar Animator en el objeto visual (o en el root)
                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = animController;
                if (amaruAvatar != null) animator.avatar = amaruAvatar;
                animator.applyRootMotion = false;

                if (visual.GetComponent<AndeanCombatStanceModifier>() == null)
                {
                    visual.AddComponent<AndeanCombatStanceModifier>();
                }
            }

            // 4. Configurar StructureSystem
            var structure = bossObj.GetComponent<StructureSystem>();
            if (structure == null) structure = bossObj.AddComponent<StructureSystem>();
            SerializedObject soStruct = new SerializedObject(structure);
            soStruct.FindProperty("maxStructure").floatValue = 100f;
            soStruct.ApplyModifiedProperties();

            // 5. Configurar EnemyController
            var enemyCtrl = bossObj.GetComponent<EnemyController>();
            if (enemyCtrl == null) enemyCtrl = bossObj.AddComponent<EnemyController>();

            SerializedObject soBoss = new SerializedObject(enemyCtrl);
            soBoss.FindProperty("characterName").stringValue = "Amaru el Cazador";
            soBoss.FindProperty("isBoss").boolValue = true;
            soBoss.FindProperty("maxHealth").floatValue = 220f;
            soBoss.FindProperty("attackDamage").floatValue = 18f;
            soBoss.FindProperty("structureDamageOnPlayer").floatValue = 22f;
            soBoss.FindProperty("attackRange").floatValue = 1.7f;
            soBoss.FindProperty("windupTime").floatValue = 0.65f;
            soBoss.FindProperty("recoverTime").floatValue = 0.75f;
            soBoss.FindProperty("moveSpeed").floatValue = 3.4f;
            soBoss.ApplyModifiedProperties();

            // 6. Guardar cambios en la escena
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[AyniAmaruSetup]</color> ¡Cápsula reemplazada exitosamente por Amaru el Cazador con modelo 3D riggeado, texturas PBR y Animator Controller! (Escena guardada: {saved})");
        }

        /// <summary>Ajusta el Scale Factor del FBX para que Amaru mida una altura humana.</summary>
        private static void FixAmaruScale(ModelImporter importer, string fbxPath)
        {
            if (importer == null) return;

            float height = MeasureModelHeight(fbxPath);
            if (height <= 0.00001f)
            {
                Debug.LogWarning("[AyniAmaru] No se pudo medir la altura de Amaru_Rigged.fbx.");
                return;
            }

            if (height < 1.4f || height > 2.3f)
            {
                float oldScale = importer.globalScale;
                importer.globalScale = oldScale * (AmaruTargetHeight / height);
                importer.SaveAndReimport(); // regenera también el Avatar Humanoid a la nueva escala
                Debug.Log($"<color=green>[AyniAmaru]</color> Escala corregida: medía {height:0.####} m → ahora mide " +
                          $"{MeasureModelHeight(fbxPath):0.00} m (Scale Factor {oldScale:0.###} → {importer.globalScale:0.###}).");
            }
            else
            {
                Debug.Log($"[AyniAmaru] Altura de Amaru correcta: {height:0.00} m.");
            }

            RebuildAvatarIfInconsistent(importer, fbxPath);
        }

        /// <summary>
        /// Tras cambiar el Scale Factor, el Avatar conserva el esqueleto de referencia a la escala antigua y en Play
        /// el Animator encoge los huesos (el modelo desaparece). Si no coincide con los huesos reales, se regenera.
        /// </summary>
        private static void RebuildAvatarIfInconsistent(ModelImporter importer, string fbxPath)
        {
            float realLen = GetPrefabBoneLocalLength(fbxPath, "Spine1");
            if (realLen <= 0f) return;

            float avatarLen = GetSkeletonBoneLength(importer, "Spine1");
            if (avatarLen > 0f && Mathf.Abs(avatarLen / realLen - 1f) < 0.02f) return; // ya coincide

            var hd = importer.humanDescription;
            hd.human = new HumanBone[0];
            hd.skeleton = new SkeletonBone[0];
            importer.humanDescription = hd;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            Debug.Log($"<color=green>[AyniAmaru]</color> Avatar Humanoid regenerado (esqueleto de referencia Spine1 " +
                      $"{avatarLen:0.#####} → {GetSkeletonBoneLength(importer, "Spine1"):0.#####}; hueso real {realLen:0.#####}).");
        }

        private static float GetSkeletonBoneLength(ModelImporter imp, string boneSuffix)
        {
            var skeleton = imp.humanDescription.skeleton;
            if (skeleton == null) return 0f;
            foreach (var bone in skeleton)
            {
                if (bone.name != null && bone.name.EndsWith(boneSuffix)) return bone.position.magnitude;
            }
            return 0f;
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

        /// <summary>
        /// El material tenía asignados los mapas de metal y oclusión de YARI, que no corresponden a la malla de Amaru.
        /// Se quitan y se deja un material no metálico. (Amaru_MetallicRoughness.png está en formato glTF y habría que
        /// convertirla a metal/suavidad de URP antes de poder usarla.)
        /// </summary>
        private static void FixAmaruMaterial(Material mat)
        {
            Texture metal = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            Texture occlusion = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;

            bool changed = false;
            if (metal != null && metal.name.StartsWith("Yari"))
            {
                mat.SetTexture("_MetallicGlossMap", null);
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.25f);
                changed = true;
            }
            if (occlusion != null && occlusion.name.StartsWith("Yari"))
            {
                mat.SetTexture("_OcclusionMap", null);
                mat.DisableKeyword("_OCCLUSIONMAP");
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(mat);
                Debug.Log("[AyniAmaru] Material de Amaru corregido: se quitaron los mapas de metal y oclusión de Yari.");
            }
        }

        private static void SetFloatDefault(AnimatorController c, string name, float value)
        {
            var parameters = c.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name && parameters[i].defaultFloat != value)
                {
                    parameters[i].defaultFloat = value;
                    c.parameters = parameters;
                    return;
                }
            }
        }

        private static void EnsureParam(AnimatorController c, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in c.parameters)
            {
                if (p.name == name) return;
            }
            c.AddParameter(name, type);
        }

        private static AnimatorState GetOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            foreach (var s in sm.states)
            {
                if (s.state.name == name)
                {
                    if (clip != null) s.state.motion = clip;
                    return s.state;
                }
            }
            var newState = sm.AddState(name);
            if (clip != null) newState.motion = clip;
            return newState;
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static bool HasTransition(AnimatorState from, AnimatorState to)
        {
            foreach (var t in from.transitions)
            {
                if (t.destinationState == to) return true;
            }
            return false;
        }

        private static void EnsureAnyTransition(AnimatorStateMachine sm, AnimatorState dest, string paramName,
            AnimatorConditionMode mode, bool canTransitionToSelf = true)
        {
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState == dest)
                {
                    if (t.canTransitionToSelf != canTransitionToSelf)
                    {
                        t.canTransitionToSelf = canTransitionToSelf;
                        EditorUtility.SetDirty(t);
                    }
                    return;
                }
            }
            var tr = sm.AddAnyStateTransition(dest);
            tr.hasExitTime = false;
            tr.canTransitionToSelf = canTransitionToSelf;
            tr.duration = 0.1f;
            tr.AddCondition(mode, 0f, paramName);
        }

        private static void EnsureExitToIdle(AnimatorState from, AnimatorState idle, float exitTime)
        {
            foreach (var existing in from.transitions)
            {
                if (existing.destinationState == idle)
                {
                    existing.hasExitTime = true;
                    existing.exitTime = exitTime;
                    return;
                }
            }
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = 0.2f;
        }
    }
}
#endif
