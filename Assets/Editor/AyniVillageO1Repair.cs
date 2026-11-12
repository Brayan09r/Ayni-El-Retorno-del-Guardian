#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ayni.Editor
{
    public static partial class AyniVillageBuilder
    {
        [MenuItem("Ayni/Entorno/Reparar tronco inicial O1")]
        public static void RepairFirstTrunk()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/network of paths/Scenes/SampleScene.unity") { Debug.LogError("Abre SampleScene antes de reparar O1."); return; }
            var obstacle = GameObject.Find("O1_Tronco");
            if (obstacle == null) { Debug.LogError("No se encontró O1_Tronco."); return; }
            if (obstacle.transform.Find("Troncos") != null) { Debug.Log("O1 ya tiene tronco."); return; }
            var mesh = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().FirstOrDefault(m => m.name == "O1_Troncos");
            var other = GameObject.Find("O4_TroncoDoble");
            var material = other != null ? other.transform.Find("Troncos")?.GetComponent<MeshRenderer>()?.sharedMaterial : null;
            if (mesh == null || material == null) { Debug.LogError("Faltan la malla O1_Troncos o el material de tronco."); return; }
            var wood = new GameObject("Troncos");
            wood.transform.SetParent(obstacle.transform, false);
            wood.AddComponent<MeshFilter>().sharedMesh = mesh;
            wood.AddComponent<MeshRenderer>().sharedMaterial = material;
            wood.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(wood, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("O1 reparado: tronco quemado y colisión restaurados para el salto.");
        }
    }
}
#endif
