#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Ayni.Player;
using Ayni.Story;

namespace Ayni.Editor
{
    /// <summary>
    /// Para ver en Play el desenlace del perdón sin tener que ganar el combate (puente de agentes):
    ///   call Ayni.Editor.AyniOutcomePreview.Reforest          hace brotar el queñual alrededor de Yari
    ///   call Ayni.Editor.AyniOutcomePreview.Shot 40 9 3.2     coloca la cámara: ángulo (grados), distancia y altura
    ///   call Ayni.Editor.AyniOutcomePreview.Look 40 9 3.2 1.6 lo mismo, mirando a la altura indicada
    ///   call Ayni.Editor.AyniOutcomePreview.ReleaseCamera     devuelve la cámara al juego
    /// </summary>
    public static class AyniOutcomePreview
    {
        private static Vector3 center;
        private static readonly List<GameObject> spawned = new List<GameObject>();

        public static void Reforest()
        {
            if (!Application.isPlaying) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            foreach (GameObject go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();

            center = player.transform.position + player.transform.forward * 1.2f;
            AyniReforestation.Begin(center, new[] { player.transform.position, center + player.transform.forward * 1.2f }, spawned);
            Debug.Log($"[Ayni Desenlace] Reforestación de prueba alrededor de {center}.");
        }

        public static void Shot(float angle, float distance, float height)
        {
            Look(angle, distance, height, 1.1f);
        }

        public static void Look(float angle, float distance, float height, float lookHeight)
        {
            if (!Application.isPlaying) return; // fuera de Play movería la cámara de la escena abierta
            Camera cam = Camera.main;
            if (cam == null) return;
            var gameplay = cam.GetComponent<ThirdPersonSifuCamera>();
            if (gameplay != null) gameplay.enabled = false;

            float a = angle * Mathf.Deg2Rad;
            cam.transform.position = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * distance + Vector3.up * height;
            cam.transform.rotation = Quaternion.LookRotation(center + Vector3.up * lookHeight - cam.transform.position);
        }

        public static void ReleaseCamera()
        {
            if (!Application.isPlaying) return;
            Camera cam = Camera.main;
            var gameplay = cam != null ? cam.GetComponent<ThirdPersonSifuCamera>() : null;
            if (gameplay != null)
            {
                gameplay.enabled = true;
                gameplay.SnapBehindTarget();
            }
        }
    }
}
#endif
