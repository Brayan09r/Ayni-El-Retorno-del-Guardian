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
    [InitializeOnLoad]
    public static class AyniAmaruSetup
    {
        static AyniAmaruSetup()
        {
            EditorApplication.delayCall += () =>
            {
                SetupAmaruBoss();
            };
        }

        [MenuItem("Ayni/Jefes/Integrar Jefe Amaru (Reemplazar Cápsula)")]
        public static void SetupAmaruBoss()
        {
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

            var rootSm = controller.layers[0].stateMachine;

            // Cargar clips
            AnimationClip idleClip = LoadClip("Assets/Art/Characters/Animations/Combat_Idle.fbx");
            AnimationClip moveClip = LoadClip("Assets/Art/Characters/Animations/Strafe_Right.fbx");
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
            EnsureAnyTransition(rootSm, stunnedState, "IsStunned", AnimatorConditionMode.If);
            EnsureAnyTransition(rootSm, dieState, "Die", AnimatorConditionMode.If);
            EnsureAnyTransition(rootSm, kneelState, "MercyKneel", AnimatorConditionMode.If);

            // Regreso al Idle tras animación
            EnsureExitToIdle(attackState, idleState, 0.85f);
            EnsureExitToIdle(hitState, idleState, 0.85f);

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
            soBoss.FindProperty("attackRange").floatValue = 2.2f;
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

        private static void EnsureAnyTransition(AnimatorStateMachine sm, AnimatorState dest, string paramName, AnimatorConditionMode mode)
        {
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState == dest) return;
            }
            var tr = sm.AddAnyStateTransition(dest);
            tr.hasExitTime = false;
            tr.duration = 0.1f;
            tr.AddCondition(mode, 0f, paramName);
        }

        private static void EnsureExitToIdle(AnimatorState from, AnimatorState idle, float exitTime)
        {
            if (HasTransition(from, idle)) return;
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = 0.2f;
        }
    }
}
#endif
