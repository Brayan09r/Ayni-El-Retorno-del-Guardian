#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ayni.Editor
{
    /// <summary>
    /// Ajusta la iluminación de la escena para que los personajes no se vean como siluetas negras:
    ///  - Luz ambiente más clara (cielo / horizonte / suelo), que rellena las zonas en sombra.
    ///  - Luz de relleno suave que acompaña a la cámara e ilumina el lado que ve el jugador.
    ///  - Posprocesado activado en la cámara con un perfil propio (tonemapping, contraste, saturación, viñeta).
    /// Solo se ejecuta desde el menú; se puede repetir sin duplicar nada.
    /// </summary>
    public static class AyniVisualPolish
    {
        private const string ProfilePath = "Assets/Art/Ayni_PostProcess_Profile.asset";
        private const string FillLightName = "Ayni_LuzDeRelleno";

        [MenuItem("Ayni/4. Mejorar Iluminación y Contraste")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Ayni Visual] Sal del modo Play antes de ajustar la iluminación.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();

            // 1. Luz ambiente por gradiente: no depende de hornear la iluminación y aclara las sombras
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.45f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.29f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.14f, 0.12f);

            // 2. Luz de relleno ligada a la cámara: ilumina siempre la cara de los personajes que mira al jugador
            Camera cam = Camera.main;
            if (cam != null)
            {
                Transform fillT = cam.transform.Find(FillLightName);
                if (fillT == null)
                {
                    var fillGo = new GameObject(FillLightName);
                    fillT = fillGo.transform;
                    fillT.SetParent(cam.transform, false);
                }
                fillT.localPosition = Vector3.zero;
                fillT.localRotation = Quaternion.Euler(18f, -22f, 0f);

                Light fill = fillT.GetComponent<Light>();
                if (fill == null) fill = fillT.gameObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.86f, 0.91f, 1f);
                fill.intensity = 0.3f;
                fill.shadows = LightShadows.None;

                // 3. Activar el posprocesado en la cámara (estaba desactivado: el volumen global no hacía nada)
                var camData = cam.GetUniversalAdditionalCameraData();
                camData.renderPostProcessing = true;
                EditorUtility.SetDirty(camData);
            }
            else
            {
                Debug.LogWarning("[Ayni Visual] No hay Main Camera en la escena.");
            }

            // 4. Perfil de posprocesado propio (no se toca el del paquete del mapa)
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.saturation.Override(5f);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.12f);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.4f);

            EditorUtility.SetDirty(profile);

            Volume volume = null;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.isGlobal) { volume = v; break; }
            }
            if (volume == null)
            {
                var volGo = new GameObject("Global Volume");
                volume = volGo.AddComponent<Volume>();
                volume.isGlobal = true;
            }
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[Ayni Visual]</color> Iluminación ajustada: luz ambiente, luz de relleno de cámara y " +
                      $"posprocesado ({ProfilePath}). Escena guardada: {saved}");
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;

            component = profile.Add<T>(false);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
#endif
