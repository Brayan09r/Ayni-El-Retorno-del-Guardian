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
    ///   call Ayni.Editor.AyniOutcomePreview.ReforestAt 571 571 hace brotar el queñual con centro en ese punto del mapa (x, z):
    ///                                                         sirve para ver renacer los árboles quemados del camino desde lejos
    ///   call Ayni.Editor.AyniOutcomePreview.BurntTrees        cuántos árboles quemados hay marcados y a qué distancia de Yari
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

        /// <summary>Como <see cref="Reforest"/>, pero con el centro en un punto del mapa (como si el perdón ocurriera allí).</summary>
        public static void ReforestAt(float x, float z)
        {
            if (!Application.isPlaying) return;
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();

            Terrain terrain = Terrain.activeTerrain;
            float y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : 0f;
            center = new Vector3(x, y, z);
            AyniReforestation.Begin(center, new[] { center }, spawned);
            Debug.Log($"[Ayni Desenlace] Reforestación de prueba con centro en {center}.");
        }

        /// <summary>Quita la reforestación de prueba: los árboles del camino vuelven a estar quemados.</summary>
        public static void ClearReforest()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();
        }

        public static void BurntTrees()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 from = player != null ? player.transform.position : Vector3.zero;
            int standing = 0, fallen = 0, shoots = 0;
            float nearest = float.MaxValue, farthest = 0f;
            foreach (Ayni.World.AyniBurntTree tree in Ayni.World.AyniBurntTree.All)
            {
                if (tree == null) continue;
                if (tree.Fallen) fallen++; else standing++;
                shoots += tree.ShootCount;
                float d = Vector3.Distance(from, tree.transform.position);
                nearest = Mathf.Min(nearest, d);
                farthest = Mathf.Max(farthest, d);
            }
            Debug.Log($"[Ayni Desenlace] Árboles quemados: {standing} en pie, {fallen} caídos, {shoots} rebrotes. " +
                      $"De Yari: el más cercano a {nearest:F0} m y el más lejano a {farthest:F0} m.");
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
