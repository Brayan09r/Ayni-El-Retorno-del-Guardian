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
    ///  1. Cóndores planeando en térmicas sobre el cañón, el puente colgante y la aldea.
    ///  2. Rebaños de llamas, alpacas y vicuñas pastando junto a los conjuntos incas.
    /// </summary>
    public static class AyniFaunaSetup
    {
        private const string FaunaRootName = "Fauna_Andina";
        private const string CondorGroupName = "Condores";
        private const string HerdGroupName = "Rebanos";

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

        private struct HerdSpot
        {
            public string name;
            public Vector2 center;
            public float radius;
            public AyniCamelidSpecies[] members;
            public float walkSpeed;
            public float alertRadius;
            public float runSpeed;

            public HerdSpot(string name, float x, float z, float radius, float walkSpeed, float alertRadius, float runSpeed,
                params AyniCamelidSpecies[] members)
            {
                this.name = name;
                center = new Vector2(x, z);
                this.radius = radius;
                this.walkSpeed = walkSpeed;
                this.alertRadius = alertRadius;
                this.runSpeed = runSpeed;
                this.members = members;
            }
        }

        private const AyniCamelidSpecies L = AyniCamelidSpecies.Llama;
        private const AyniCamelidSpecies A = AyniCamelidSpecies.Alpaca;
        private const AyniCamelidSpecies V = AyniCamelidSpecies.Vicuna;

        // Rebaños en los pastizales a un lado del camino, cada uno ligado a la historia del lugar:
        //  - Junto al Puesto de Chasquis, un rebaño mixto recibe a Yari al iniciar el recorrido.
        //  - Las alpacas pastan junto a la Kancha de los Tejedores (su fibra es la del tejido), con una llama guardiana.
        //  - Una caravana de llamas de carga descansa junto al Tambo del Camino.
        //  - Las vicuñas silvestres se mantienen lejos, cerca de la Portada del Cazador (memoria del chaccu).
        private static readonly HerdSpot[] HerdSpots =
        {
            new HerdSpot("Rebano_Chasquis",   312f, 530f, 12f, 0.85f,  5.5f, 3.0f, L, L, L, A, A),
            new HerdSpot("Alpacas_Tejedores", 360f, 364f, 12f, 0.75f,  7f,   3.2f, A, A, A, A, A, A, L),
            new HerdSpot("Caravana_Tambo",    460f, 467f, 14f, 0.9f,   5f,   3.0f, L, L, L, L, L),
            new HerdSpot("Vicunas_Cazador",   523f, 550f, 18f, 1.1f,  16f,   5.5f, V, V, V, V, V),
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

        [MenuItem("Ayni/Fauna Andina/2. Soltar Rebaños (Llamas, Alpacas y Vicuñas)")]
        public static void SpawnHerdsFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Fauna] Sal del modo Play antes de agregar la fauna.");
                return;
            }

            int count = SpawnHerds();
            MarkDirty();
            Debug.Log($"[Ayni Fauna] {count} camélidos pastando cerca de la aldea. Guarda la escena (Ctrl+S) para conservarlos.");
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

        public static int SpawnHerds()
        {
            Transform group = ResetGroup(HerdGroupName);
            Physics.SyncTransforms();
            int total = 0;

            for (int h = 0; h < HerdSpots.Length; h++)
            {
                HerdSpot spot = HerdSpots[h];
                var herd = new GameObject(spot.name).transform;
                herd.SetParent(group, false);

                var rng = new System.Random(1000 + h * 97);
                var home = new Vector3(spot.center.x, 0f, spot.center.y);
                int placed = 0;

                for (int i = 0; i < spot.members.Length; i++)
                {
                    // Busca un lugar de pasto despejado cerca del centro del rebaño
                    bool found = false;
                    Vector3 pos = home;
                    for (int attempt = 0; attempt < 20 && !found; attempt++)
                    {
                        float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                        float r = (float)Mathf.Sqrt((float)rng.NextDouble()) * spot.radius * 0.6f;
                        pos = new Vector3(home.x + Mathf.Cos(a) * r, 0f, home.z + Mathf.Sin(a) * r);
                        if (AyniGrazingAnimal.IsOpenGround(pos, 26f, out float groundY))
                        {
                            pos.y = groundY;
                            found = true;
                        }
                    }
                    if (!found) continue;

                    AyniCamelidSpecies species = spot.members[i];
                    string animalName = $"{SpeciesName(species)}_{i + 1}";
                    AyniCamelidModel.Parts parts = AyniCamelidModel.Build(herd, animalName, species, 7000 + h * 31 + i);
                    parts.root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f));

                    var grazing = parts.root.AddComponent<AyniGrazingAnimal>();
                    grazing.Configure(home, spot.radius, spot.walkSpeed, parts);
                    grazing.ConfigureAlert(spot.alertRadius, spot.runSpeed);
                    placed++;
                }

                if (placed == 0)
                {
                    Debug.LogWarning($"[Ayni Fauna] No se encontró pasto despejado para {spot.name} cerca de ({spot.center.x}, {spot.center.y}). Ajusta su centro en AyniFaunaSetup.HerdSpots.");
                }
                total += placed;
            }

            Undo.RegisterCreatedObjectUndo(group.gameObject, "Soltar Rebaños");
            return total;
        }

        private static string SpeciesName(AyniCamelidSpecies species)
        {
            switch (species)
            {
                case AyniCamelidSpecies.Alpaca: return "Alpaca";
                case AyniCamelidSpecies.Vicuna: return "Vicuna";
                default: return "Llama";
            }
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
