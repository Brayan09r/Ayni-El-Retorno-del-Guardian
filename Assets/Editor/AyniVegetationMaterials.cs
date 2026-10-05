#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Ayni.Story;

namespace Ayni.Editor
{
    /// <summary>
    /// Crea (una sola vez) las plantillas de material de la vegetación del desenlace en Assets/Resources/AyniVegetacion.
    /// El queñual se genera por código, pero sus materiales necesitan variantes del shader URP/Lit (recorte por
    /// transparencia, sin brillos) que Unity solo incluye en la compilación del juego si algún asset las usa.
    /// Sin estas plantillas, en el ejecutable las hojas saldrían como cuadrados opacos.
    /// </summary>
    [InitializeOnLoad]
    public static class AyniVegetationMaterials
    {
        static AyniVegetationMaterials()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("Ayni/Entorno/Recrear Materiales de la Vegetación")]
        public static void Recreate()
        {
            Build(true);
        }

        private static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Build(false);
        }

        private static void Build(bool overwrite)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            const string resources = "Assets/Resources";
            string folder = resources + "/" + AyniReforestation.TemplateFolder;
            if (!AssetDatabase.IsValidFolder(resources)) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(resources, AyniReforestation.TemplateFolder);

            bool created = false;
            foreach (var (doubleSided, cutout) in new[] { (false, false), (true, false), (true, true) })
            {
                string path = folder + "/" + AyniReforestation.TemplateName(doubleSided, cutout) + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null && !overwrite) continue;

                if (existing == null)
                {
                    var material = new Material(shader);
                    AyniReforestation.ConfigureMaterial(material, doubleSided, cutout);
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    existing.shader = shader;
                    AyniReforestation.ConfigureMaterial(existing, doubleSided, cutout);
                    EditorUtility.SetDirty(existing);
                }
                created = true;
            }

            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("<color=green>[Ayni]</color> Plantillas de material de la vegetación listas en " + folder + ".");
            }
        }
    }
}
#endif
