#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ayni.World.Fauna;

namespace Ayni.Editor
{
    /// <summary>
    /// Herramienta de fauna andina ambiental del Nivel 1 (menú Ayni > Fauna Andina).
    /// Solo agrega objetos decorativos bajo Ayni_Entorno/Fauna_Andina: no modifica el terreno,
    /// la aldea, la iluminación ni el combate, y puede quitarse por completo con un clic.
    /// </summary>
    public static class AyniFaunaSetup
    {
        private const string FaunaRootName = "Fauna_Andina";
        private const string CondorGroupName = "Condores";

        private struct CondorSpot
        {
            public string name;
            public Vector2 center;     // (x, z) en el mundo
            public float radius;
            public float altitude;     // metros sobre el terreno
            public float speed;        // m/s de planeo
            public bool clockwise;
            public float scale;

            public CondorSpot(string name, float x, float z, float radius, float altitude, float speed, bool clockwise, float scale)
            {
                this.name = name;
                center = new Vector2(x, z);
                this.radius = radius;
                this.altitude = altitude;
                this.speed = speed;
                this.clockwise = clockwise;
                this.scale = scale;
            }
        }

        // Térmicas sobre los lugares donde el cielo se ve abierto: la garganta del puente colgante,
        // el cañón norte, muy alto sobre la plaza de Amaru (para no distraer en el jefe) y la aldea.
        private static readonly CondorSpot[] CondorSpots =
        {
            new CondorSpot("Kuntur_Garganta_1", 651f, 466f, 38f, 34f, 11f, false, 1.00f),
            new CondorSpot("Kuntur_Garganta_2", 651f, 466f, 52f, 46f, 12f, true,  1.05f),
            new CondorSpot("Kuntur_Canon_Norte", 638f, 495f, 45f, 52f, 10f, false, 0.95f),
            new CondorSpot("Kuntur_Plaza_Amaru", 612f, 468f, 60f, 70f, 12f, true,  1.00f),
            new CondorSpot("Kuntur_Aldea",      520f, 480f, 70f, 60f, 11f, false, 1.10f),
        };

        [MenuItem("Ayni/Fauna Andina/1. Soltar Cóndores sobre el Cañón y la Aldea")]
        public static void SpawnCondorsFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Fauna] Sal del modo Play antes de agregar la fauna.");
                return;
            }

            int count = SpawnCondors();
            MarkDirty();
            Debug.Log($"[Ayni Fauna] {count} cóndores planeando sobre el cañón y la aldea. Guarda la escena (Ctrl+S) para conservarlos.");
        }

        [MenuItem("Ayni/Fauna Andina/Quitar Fauna Andina")]
        public static void RemoveAllFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Fauna] Sal del modo Play antes de quitar la fauna.");
                return;
            }

            GameObject envRoot = GameObject.Find("Ayni_Entorno");
            Transform fauna = envRoot != null ? envRoot.transform.Find(FaunaRootName) : null;
            if (fauna == null)
            {
                Debug.Log("[Ayni Fauna] No hay fauna andina en la escena.");
                return;
            }

            Undo.DestroyObjectImmediate(fauna.gameObject);
            MarkDirty();
            Debug.Log("[Ayni Fauna] Fauna andina retirada de la escena.");
        }

        public static int SpawnCondors()
        {
            Transform group = ResetGroup(CondorGroupName);
            Terrain terrain = Terrain.activeTerrain;

            foreach (CondorSpot spot in CondorSpots)
            {
                AyniCondorModel.Parts parts = AyniCondorModel.Build(group, spot.name, spot.scale);

                var center = new Vector3(spot.center.x, 0f, spot.center.y);
                float ground = terrain != null ? terrain.SampleHeight(center) + terrain.GetPosition().y : 0f;
                center.y = ground;
                parts.root.transform.position = new Vector3(center.x + spot.radius, ground + spot.altitude, center.z);

                var flight = parts.root.AddComponent<AyniCondorFlight>();
                flight.Configure(center, spot.radius, spot.altitude, spot.speed, spot.clockwise,
                    parts.leftWing, parts.rightWing);
            }

            Undo.RegisterCreatedObjectUndo(group.gameObject, "Soltar Cóndores");
            return CondorSpots.Length;
        }

        // ------------------------------------------------------------------------------------------------
        private static Transform GetOrCreateFaunaRoot()
        {
            GameObject envRoot = AyniEnvironmentBuilder.GetOrCreateRoot();
            Transform fauna = envRoot.transform.Find(FaunaRootName);
            if (fauna == null)
            {
                var go = new GameObject(FaunaRootName);
                go.transform.SetParent(envRoot.transform, false);
                fauna = go.transform;
            }
            return fauna;
        }

        private static Transform ResetGroup(string groupName)
        {
            Transform fauna = GetOrCreateFaunaRoot();
            Transform old = fauna.Find(groupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(groupName);
            go.transform.SetParent(fauna, false);
            return go.transform;
        }

        private static void MarkDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
#endif
