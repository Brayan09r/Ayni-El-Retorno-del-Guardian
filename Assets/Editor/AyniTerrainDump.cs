#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace Ayni.Editor
{
    /// <summary>
    /// Vuelca el relieve a DebugCaptures/terreno_2m.bin (alturas del mundo, una muestra cada 2 m, float32 por filas
    /// de sur a norte) para planificar fuera de Unity dónde caben construcciones.
    ///   call Ayni.Editor.AyniTerrainDump.Dump
    /// </summary>
    public static class AyniTerrainDump
    {
        /// <summary>Qué hay en el terreno y en la escena alrededor de un punto: árboles, hierba y objetos con malla.</summary>
        public static void Survey(float x, float z, float radius)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            int trees = 0;
            foreach (TreeInstance t in data.treeInstances)
            {
                float tx = origin.x + t.position.x * data.size.x, tz = origin.z + t.position.z * data.size.z;
                if ((tx - x) * (tx - x) + (tz - z) * (tz - z) < radius * radius) trees++;
            }
            var names = new System.Text.StringBuilder();
            int count = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer) continue;
                Vector3 c = r.bounds.center;
                if (r.bounds.size.x > 300f) continue;
                if ((c.x - x) * (c.x - x) + (c.z - z) * (c.z - z) > radius * radius) continue;
                count++;
                if (count <= 12) names.Append(r.transform.root.name).Append('/').Append(r.name).Append(' ').Append(c.ToString("F0")).Append("; ");
            }
            Debug.Log($"[Ayni Terreno] ({x:F0}, {z:F0}) r={radius:F0}: árboles del terreno {trees} de {data.treeInstanceCount}, " +
                      $"capas de detalle {data.detailPrototypes.Length}, capas de textura {data.terrainLayers.Length}, objetos con malla {count}: {names}");
        }

        /// <summary>Datos de la escena que hacen falta para colocar construcciones: Yari, Amaru, luz y cámara.</summary>
        public static void SceneInfo()
        {
            var sb = new System.Text.StringBuilder("[Ayni Terreno] Escena: ");
            foreach (string n in new[] { "Yari_Hero", "Amaru_Hunter", "Ayni_Entorno", "Main Camera" })
            {
                GameObject go = GameObject.Find(n);
                sb.Append(n).Append(go != null ? " pos " + go.transform.position.ToString("F2") + " rot " + go.transform.eulerAngles.ToString("F1") : " NO ESTÁ").Append(" | ");
            }
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                sb.Append(root.name).Append(root.activeSelf ? "" : "(off)").Append(", ");
            }
            GameObject env = GameObject.Find("Ayni_Entorno");
            if (env != null)
            {
                sb.Append(" | hijos de Ayni_Entorno: ");
                foreach (Transform t in env.transform) sb.Append(t.name).Append(", ");
            }
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) sb.Append(" | sol ").Append(l.transform.eulerAngles.ToString("F0")).Append(" int ").Append(l.intensity.ToString("F2"));
            }
            Debug.Log(sb.ToString());
        }

        public static void Dump()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[Ayni Terreno] No hay terreno activo."); return; }

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            const float step = 2f;
            int nx = Mathf.FloorToInt(size.x / step) + 1, nz = Mathf.FloorToInt(size.z / step) + 1;

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            Directory.CreateDirectory(dir);
            using (var writer = new BinaryWriter(File.Create(Path.Combine(dir, "terreno_2m.bin"))))
            {
                for (int z = 0; z < nz; z++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        var p = new Vector3(origin.x + x * step, 0f, origin.z + z * step);
                        writer.Write(terrain.SampleHeight(p) + origin.y);
                    }
                }
            }
            Debug.Log($"[Ayni Terreno] Relieve volcado: {nx} x {nz} muestras cada {step} m desde ({origin.x}, {origin.z}), tamaño {size}.");
        }
    }
}
#endif
