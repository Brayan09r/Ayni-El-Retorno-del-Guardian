using System.Collections.Generic;
using UnityEngine;
using Ayni.Enemy;

namespace Ayni.World
{
    /// <summary>
    /// Patrullas del Qhapaq Ñan: entre kancha y kancha el camino ya no está vacío. En cada tramo una pareja de guardias
    /// de Sayri va y viene por el camino (con la rutina de AyniRivalAmbient) y pelea cuando ve u oye a Yari, como los
    /// grupos sueltos de Sifu entre sala y sala. Cada pareja es del mismo tipo que los rivales de la kancha siguiente.
    /// No cuentan para despejar las kanchas. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniRoadPatrols : MonoBehaviour
    {
        private struct Patrol
        {
            public int nextSite;   // orden de la kancha cuyos rivales forman la pareja
            public Vector2 from, to;
            public Patrol(int site, float x1, float z1, float x2, float z2)
            {
                nextSite = site;
                from = new Vector2(x1, z1);
                to = new Vector2(x2, z2);
            }
        }

        // Tramos entre los obstáculos del camino (O3-O8 de AyniVillageBuilder), para que no choquen con los troncos
        private static readonly Patrol[] Patrols =
        {
            new Patrol(2, 362f, 436f, 366f, 421f),   // K1 → K2, entre O3 y O4
            new Patrol(3, 433f, 385f, 447f, 386f),   // K2 → K3, entre O5 y O6
            new Patrol(4, 487f, 481f, 495f, 500f),   // K3 → K4, entre O7 y O8
            new Patrol(4, 572f, 508f, 596f, 492f),   // K4 → plaza de Amaru
        };

        private void Start()
        {
            int made = 0;
            for (int p = 0; p < Patrols.Length; p++)
            {
                AyniEncounterSite site = null;
                foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == Patrols[p].nextSite) site = s;
                if (site == null) continue;

                Vector3 a = Ground(Patrols[p].from), b = Ground(Patrols[p].to);
                Vector3 along = b - a;
                along.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
                var pair = new List<AyniRivalAmbient>();
                for (int i = 0; i < 2; i++)
                {
                    Vector3 start = Ground(new Vector2(a.x, a.z) + new Vector2(side.x, side.z) * (i == 0 ? 0.9f : -0.9f) - new Vector2(along.normalized.x, along.normalized.z) * (i * 1.2f));
                    EnemyController rival = site.CreateRival(start, Quaternion.LookRotation(along), $"Patrulla_{p + 1}_{i + 1}");
                    if (rival == null) continue;
                    var routine = rival.gameObject.AddComponent<AyniRivalAmbient>();
                    pair.Add(routine);
                    made++;
                }
                foreach (AyniRivalAmbient routine in pair)
                {
                    Vector3 offset = routine.transform.position - a;
                    routine.Begin(AyniRivalAmbient.Activity.Patrol, b + offset, pair);
                }
            }
            if (made > 0) Debug.Log($"[Ayni Camino] {made} guardias patrullan el camino entre las kanchas");
        }

        private static Vector3 Ground(Vector2 xz)
        {
            var p = new Vector3(xz.x, 0f, xz.y);
            return Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.05f : p;
        }
    }
}
