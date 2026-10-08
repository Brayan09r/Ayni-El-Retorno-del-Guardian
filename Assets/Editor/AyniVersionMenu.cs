#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Ayni.Core;

namespace Ayni.Editor
{
    /// <summary>Copia la versión del juego (AyniVersion.Number) a Project Settings > Player > Version.</summary>
    [InitializeOnLoad]
    public static class AyniVersionMenu
    {
        static AyniVersionMenu()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (PlayerSettings.bundleVersion != AyniVersion.Number) Sync();
            };
        }

        [MenuItem("Ayni/Versión/Sincronizar Versión del Proyecto")]
        public static void Sync()
        {
            PlayerSettings.bundleVersion = AyniVersion.Number;
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[Ayni Versión]</color> Versión del proyecto: {AyniVersion.Label} — {AyniVersion.Title}");
        }
    }
}
#endif
