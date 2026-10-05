#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ayni.Editor
{
    /// <summary>
    /// Atmósfera del GDD ("rayos de sol atravesando cañones, niebla de montaña"): hora dorada.
    ///  - Sol bajo y cálido desde el oeste, cielo de atardecer y luz ambiente a juego.
    ///  - Bruma de distancia cálida, niebla en capas dentro de la garganta y cordillera nevada al fondo.
    ///  - Posprocesado con la paleta del GDD (oro solar en las luces, azul Hanan Pacha en las sombras).
    /// Se puede repetir sin duplicar nada. "Crear Acantilados y Quebradas" también lo aplica.
    /// </summary>
    public static class AyniAtmosphere
    {
        private const string EnvFolder = "Assets/Art/Environment";
        private const string SkyMatPath = EnvFolder + "/Ayni_Cielo.mat";
        private const string MistMatPath = EnvFolder + "/Ayni_Niebla.mat";
        private const string MountainMatPath = EnvFolder + "/Ayni_Montanas.mat";
        private const string MountainMeshPath = EnvFolder + "/Ayni_MontanasLejanas.asset";
        private const string ProfilePath = "Assets/Art/Ayni_PostProcess_Profile.asset";

        private static readonly Color FogColor = new Color(0.80f, 0.71f, 0.63f);
        private static readonly Rect MistArea = new Rect(560f, 270f, 150f, 365f);
        private static readonly float[] MistHeights = { -12.5f, -7f };

        [MenuItem("Ayni/Entorno/Aplicar Atmósfera de Hora Dorada")]
        public static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Atmósfera] Sal del modo Play antes de cambiar la atmósfera.");
                return;
            }

            Apply(AyniEnvironmentBuilder.GetOrCreateRoot());
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            AyniEnvironmentBuilder.CaptureViews();
        }

        /// <summary>Aplica todo sin guardar la escena (lo hace quien llama).</summary>
        public static void Apply(GameObject root)
        {
            Directory.CreateDirectory(EnvFolder);
            SetupSunAndSky();
            SetupPipeline();
            SetupPostProcessing();
            BuildMist(root);
            BuildMountains(root);
            Debug.Log("<color=green>[Ayni Atmósfera]</color> Hora dorada aplicada: sol, cielo, bruma, niebla de la garganta, cordillera y posprocesado.");
        }

        // ------------------------------------------------------------------------------------------------
        private static void SetupSunAndSky()
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional && l.shadows != LightShadows.None) { sun = l; break; }
                }
            }

            if (sun != null)
            {
                // Sol a 17° sobre el horizonte, viniendo del oeste-noroeste: ilumina de lado los acantilados
                // que miran a la arena y deja la garganta en sombra
                sun.transform.rotation = Quaternion.Euler(17f, 106f, 0f);
                sun.color = new Color(1f, 0.80f, 0.58f);
                sun.intensity = 1.7f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.78f;
                RenderSettings.sun = sun;
                EditorUtility.SetDirty(sun);
                EditorUtility.SetDirty(sun.transform);
            }
            else
            {
                Debug.LogWarning("[Ayni Atmósfera] No se encontró la luz direccional principal.");
            }

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
                if (sky == null)
                {
                    sky = new Material(skyShader) { name = "Ayni_Cielo" };
                    AssetDatabase.CreateAsset(sky, SkyMatPath);
                }
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_SunSizeConvergence", 4f);
                sky.SetFloat("_AtmosphereThickness", 1.25f);
                sky.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.66f));
                sky.SetColor("_GroundColor", new Color(0.55f, 0.50f, 0.46f));
                sky.SetFloat("_Exposure", 1.25f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Luz ambiente generosa: con el sol tan bajo, media escena queda en sombra y no debe verse negra
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.57f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.49f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.22f, 0.19f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.0019f;

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 6000f);
                cam.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(cam);
            }

            DynamicGI.UpdateEnvironment();
        }

        private static void SetupPipeline()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;

            // La niebla y el agua se funden con el terreno leyendo la profundidad de la escena
            urp.supportsCameraDepthTexture = true;
            // Sombras hasta los acantilados del otro lado de la garganta
            urp.shadowDistance = Mathf.Max(urp.shadowDistance, 140f);
            urp.shadowCascadeCount = Mathf.Max(urp.shadowCascadeCount, 4);
            EditorUtility.SetDirty(urp);
        }

        private static void SetupPostProcessing()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogWarning("[Ayni Atmósfera] No existe " + ProfilePath + ". Ejecuta antes Ayni > 4. Mejorar Iluminación y Contraste.");
                return;
            }

            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(8f);
            color.colorFilter.Override(new Color(1f, 0.985f, 0.96f));

            var balance = GetOrAdd<WhiteBalance>(profile);
            balance.temperature.Override(4f);
            balance.tint.Override(0f);

            // Paleta del GDD: oro solar en las luces altas, azul Hanan Pacha en las sombras
            var smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.94f, 0.98f, 1.08f, 0f));
            smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.07f, 1.0f, 0.90f, 0f));

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.88f, 0.70f));

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.42f);

            EditorUtility.SetDirty(profile);
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                component.active = true;
                return component;
            }

            component = profile.Add<T>(false);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------------------------------------
        private static void BuildMist(GameObject root)
        {
            Transform old = root.transform.Find("Niebla_Garganta");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Shader shader = Shader.Find("Ayni/Niebla");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[Ayni Atmósfera] El shader 'Ayni/Niebla' no compiló; no se crea la niebla.");
                return;
            }

            var parent = new GameObject("Niebla_Garganta");
            parent.transform.SetParent(root.transform, false);

            for (int i = 0; i < MistHeights.Length; i++)
            {
                string path = MistMatPath.Replace(".mat", "_" + (i + 1) + ".mat");
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != shader)
                {
                    mat.shader = shader;
                }
                mat.SetColor("_Color", new Color(0.92f, 0.90f, 0.88f));
                mat.SetFloat("_Density", 0.5f);
                mat.SetFloat("_Scale", 30f + 9f * i);
                mat.SetFloat("_Speed", 0.7f + 0.25f * i);
                mat.SetFloat("_SoftDistance", 4.5f);
                mat.SetFloat("_Seed", i * 1.7f);
                EditorUtility.SetDirty(mat);

                GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Plane);
                sheet.name = "Capa_" + (i + 1);
                Object.DestroyImmediate(sheet.GetComponent<Collider>());
                sheet.transform.SetParent(parent.transform, false);
                sheet.transform.position = new Vector3(MistArea.center.x, MistHeights[i], MistArea.center.y);
                sheet.transform.localScale = new Vector3(MistArea.width / 10f, 1f, MistArea.height / 10f);
                var renderer = sheet.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Anillo de montañas alrededor del mapa: tapa el horizonte vacío y da la escala de la cordillera.</summary>
        private static void BuildMountains(GameObject root)
        {
            Transform old = root.transform.Find("Montanas_Lejanas");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Shader shader = Shader.Find("Ayni/Montanas Lejanas");
            Terrain terrain = Terrain.activeTerrain;
            if (shader == null || !shader.isSupported || terrain == null)
            {
                Debug.LogError("[Ayni Atmósfera] El shader 'Ayni/Montanas Lejanas' no compiló o no hay terreno; no se crea la cordillera.");
                return;
            }

            Vector3 size = terrain.terrainData.size;
            Vector3 center = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            center.y = 0f;

            const int angular = 288;
            const int radial = 26;
            const float inner = 430f;
            const float outer = 4200f;
            const float baseY = -45f;

            var vertices = new Vector3[angular * radial];
            var colors = new Color[angular * radial];
            for (int a = 0; a < angular; a++)
            {
                float angle = a * Mathf.PI * 2f / angular;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                for (int r = 0; r < radial; r++)
                {
                    float t = r / (float)(radial - 1);
                    float radius = Mathf.Lerp(inner, outer, Mathf.Pow(t, 1.25f));
                    float x = cos * radius, z = sin * radius;

                    // Nada hasta pasar el borde del mapa, luego estribaciones y al fondo la cordillera
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(800f, 2700f, radius));
                    float fall = 1f - 0.45f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3000f, outer, radius));
                    float massif = 0.45f + 0.75f * Mathf.PerlinNoise(x / 2100f + 31.7f, z / 2100f + 8.3f);
                    float ridge = Ridged(x, z);
                    float height = rise * fall * massif * (90f + 560f * ridge);

                    vertices[a * radial + r] = new Vector3(center.x + x, baseY + height, center.z + z);
                    float snowNoise = Mathf.PerlinNoise(x / 260f + 53.1f, z / 260f + 77.7f) * 110f;
                    float snow = Mathf.InverseLerp(300f, 400f, baseY + height + snowNoise);
                    float rockTone = Mathf.PerlinNoise(x / 420f + 51f, z / 420f + 19f);
                    colors[a * radial + r] = new Color(snow, rockTone, 0f, 1f);
                }
            }

            var triangles = new int[angular * (radial - 1) * 6];
            int ti = 0;
            for (int a = 0; a < angular; a++)
            {
                int an = (a + 1) % angular;
                for (int r = 0; r < radial - 1; r++)
                {
                    int i00 = a * radial + r, i01 = a * radial + r + 1;
                    int i10 = an * radial + r, i11 = an * radial + r + 1;
                    triangles[ti++] = i00; triangles[ti++] = i10; triangles[ti++] = i01;
                    triangles[ti++] = i10; triangles[ti++] = i11; triangles[ti++] = i01;
                }
            }

            AssetDatabase.DeleteAsset(MountainMeshPath);
            var mesh = new Mesh { name = "Ayni_MontanasLejanas" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MountainMeshPath);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MountainMatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Ayni_Montanas" };
                AssetDatabase.CreateAsset(mat, MountainMatPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetFloat("_HazeLow", 0.86f);
            mat.SetFloat("_HazeHigh", 0.30f);
            mat.SetFloat("_HazeHeight", 380f);
            EditorUtility.SetDirty(mat);

            var go = new GameObject("Montanas_Lejanas");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Ruido de crestas (0..1): varias octavas de Perlin plegado, para que salgan aristas y picos.</summary>
        private static float Ridged(float x, float z)
        {
            float sum = 0f, amplitude = 0.55f, frequency = 1f / 1300f, total = 0f;
            for (int o = 0; o < 5; o++)
            {
                float n = Mathf.PerlinNoise(x * frequency + 100f + o * 17.3f, z * frequency + 100f + o * 9.1f);
                float ridge = 1f - Mathf.Abs(2f * n - 1f);
                sum += ridge * ridge * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.1f;
            }
            return Mathf.Clamp01(sum / total);
        }
    }
}
#endif
