#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Ajustes de importación de los sonidos de Assets/Resources/AyniAudio:
    /// los efectos cortos se descomprimen al cargar (suenan sin retraso) y el ambiente y la música
    /// se guardan comprimidos en memoria (son largos y se reproducen en bucle).
    /// </summary>
    public class AyniAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/AyniAudio/")) return;

            var importer = (AudioImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool longClip = file.StartsWith("amb_") || file.StartsWith("prologo_");

            importer.forceToMono = true;
            importer.loadInBackground = longClip;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = longClip ? 0.6f : 1f;
            settings.preloadAudioData = !longClip;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("Ayni/Audio/Reimportar Sonidos")]
        public static void ReimportAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/AyniAudio" });
            foreach (string guid in guids)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
            Debug.Log($"<color=green>[Ayni Audio]</color> {guids.Length} sonidos reimportados.");
        }
    }
}
#endif
