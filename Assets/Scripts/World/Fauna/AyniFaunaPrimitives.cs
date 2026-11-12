using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Utilidades compartidas por los modelos low-poly de la fauna andina (cóndor, camélidos, vizcacha, zorro):
    /// materiales URP mate cacheados por color y primitivas sin colisionador.
    /// </summary>
    public static class AyniFaunaPrimitives
    {
        private static readonly Dictionary<(Color, float), Material> MaterialCache = new Dictionary<(Color, float), Material>();

        /// <summary>Material URP Lit de color liso; se reutiliza para todos los animales con el mismo color.</summary>
        public static Material Mat(Color color, float smoothness = 0.08f)
        {
            var key = (color, smoothness);
            if (MaterialCache.TryGetValue(key, out Material cached) && cached != null) return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = "Ayni_Fauna_" + ColorUtility.ToHtmlStringRGB(color), color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            MaterialCache[key] = mat;
            return mat;
        }

        /// <summary>Crea una primitiva de Unity sin colisionador, con sombras y sondas de luz.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat,
            Vector3 pos, Vector3 euler, Vector3 scale, bool receiveShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            ConfigureRenderer(go.GetComponent<MeshRenderer>(), mat, receiveShadows);
            return go;
        }

        public static void ConfigureRenderer(MeshRenderer mr, Material mat, bool receiveShadows = true)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = receiveShadows;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
        }

        /// <summary>Crea un pivote vacío (articulación) como hijo de <paramref name="parent"/>.</summary>
        public static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }
    }
}
