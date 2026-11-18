#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Las pistas de música (Resources/AyniAudio/musica_*) duran 2-3 minutos: descomprimidas al cargar ocuparían
    /// ~60 MB cada una en memoria. Se importan en streaming (Vorbis), que es lo normal para música de fondo.
    /// </summary>
    public class AyniMusicImport : AssetPostprocessor
    {
        /// <summary>Vuelve a importar las pistas con estos ajustes (p. ej. las que se copiaron antes de existir esta clase).</summary>
        [MenuItem("Ayni/Audio/Reimportar música")]
        public static void ReimportMusic()
        {
            foreach (string guid in AssetDatabase.FindAssets("musica_ t:AudioClip", new[] { "Assets/Resources/AyniAudio" }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/AyniAudio/musica_")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.75f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
        }
    }
}
#endif
