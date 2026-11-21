using UnityEngine;
using Ayni.Enemy;
using Ayni.Story;
using Ayni.UI;
using Ayni.World;

namespace Ayni.Editor
{
    /// <summary>
    /// Sondas para probar los objetivos del nivel y el acecho de Amaru por el puente de agentes, sin jugar las peleas.
    /// En Play:
    /// <code>
    /// call Ayni.Editor.AyniVillageBuilder.TeleportLocal K1 0 0 0      Yari en el patio del Puesto de Chasquis
    /// call Ayni.Editor.AyniObjectivesProbe.ClearSite 1                 derrota a todos sus rivales (repetir tras los refuerzos)
    /// call Ayni.Editor.AyniObjectivesProbe.Report                      objetivo actual, kanchas y acecho
    /// call Ayni.Story.AyniHunterStalker.Test 1                         fuerza una aparición de Amaru (0 a 3)
    /// call Ayni.Editor.AyniObjectivesProbe.GoToCage 2                  Yari ante la puerta de una jaula (0 puma, 1 cóndor, 2 vicuñas)
    /// call Ayni.World.AyniCaptiveCages.TestFree 2                      la abre como si la abriera Yari
    /// call Ayni.Story.AyniFreedAllies.TestPuma                         la puma madre acude ya contra Amaru
    /// </code>
    /// </summary>
    public static class AyniObjectivesProbe
    {
        /// <summary>Derrota (Venganza) a todos los rivales en pie de la kancha con ese orden.</summary>
        public static void ClearSite(int order)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Ayni Objetivos] ClearSite solo funciona en Play");
                return;
            }
            AyniEncounterSite site = null;
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == order) site = s;
            if (site == null)
            {
                Debug.LogWarning($"[Ayni Objetivos] No hay kancha con orden {order}");
                return;
            }
            int defeated = 0;
            // Copia: derrotar puede hacer que la kancha añada refuerzos a su lista
            var rivals = new System.Collections.Generic.List<GameObject>(site.Rivals);
            foreach (GameObject rival in rivals)
            {
                if (rival == null || !rival.activeInHierarchy) continue;
                var enemy = rival.GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead) continue;
                enemy.Defeat(true);
                defeated++;
            }
            Debug.Log($"[Ayni Objetivos] {AyniLevelObjectives.NameOf(site)}: {defeated} rivales derrotados · " +
                      $"entró: {site.PlayerEntered} · liberado: {site.Cleared}");
        }

        /// <summary>Pone a Yari delante de la puerta de la jaula de ese animal, mirándola (0 puma, 1 cóndor, 2 vicuñas).</summary>
        public static void GoToCage(int kind)
        {
            foreach (AyniCage cage in AyniCaptiveCages.Cages)
            {
                if (cage == null || (int)cage.Kind != kind) continue;
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                Vector3 door = cage.DoorPoint;
                Vector3 look = cage.transform.position - door;
                look.y = 0f;
                var mover = player.GetComponent<CharacterController>();
                if (mover != null) mover.enabled = false;
                player.transform.SetPositionAndRotation(door + Vector3.up * 0.1f, Quaternion.LookRotation(look));
                if (mover != null) mover.enabled = true;
                var cam = Camera.main != null ? Camera.main.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
                if (cam != null) cam.SnapBehindTarget();
                Debug.Log($"[Ayni Jaulas] Yari ante la jaula {cage.Kind}");
                return;
            }
            Debug.LogWarning($"[Ayni Jaulas] No hay jaula del tipo {kind}");
        }

        /// <summary>
        /// Suelta la cámara y la deja mirando a Amaru desde <paramref name="distance"/> m, a <paramref name="height"/> m de
        /// altura y <paramref name="angle"/>° respecto a la línea Amaru-Yari (para ver llegar a la puma madre).
        /// <c>AyniPlaytestProbe.FollowCamera 1</c> la devuelve detrás de Yari.
        /// </summary>
        public static void WatchBoss(float distance, float height, float angle)
        {
            EnemyController boss = null;
            foreach (EnemyController e in EnemyController.All) if (e != null && e.IsBoss) boss = e;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (boss == null || player == null) return;
            Vector3 b = boss.transform.position;
            Vector3 dir = player.transform.position - b;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            Vector3 cam = b + Quaternion.Euler(0f, angle, 0f) * dir * distance + Vector3.up * height;
            Vector3 look = b + Vector3.up * 1f;
            AyniPlaytestProbe.PlaceCamera(cam.x, cam.y, cam.z, look.x, look.y, look.z);
        }

        /// <summary>Objetivo en pantalla, estado de cada kancha y del acecho.</summary>
        public static void Report()
        {
            Debug.Log($"[Ayni Objetivos] Objetivo: \"{AyniLevelObjectives.CurrentObjective}\" · {AyniLevelObjectives.CurrentDetail} · " +
                      $"liberadas: {AyniLevelObjectives.SitesLiberated}/{AyniEncounterSite.All.Count}");
            foreach (AyniEncounterSite s in AyniEncounterSite.All)
            {
                if (s == null) continue;
                Debug.Log($"[Ayni Objetivos]   K{s.Order} {AyniLevelObjectives.NameOf(s)}: entró {s.PlayerEntered} · liberado {s.Cleared} · " +
                          $"en pie {AyniLevelObjectives.Standing(s)}");
            }
            AyniHunterStalker.Report();
            AyniCaptiveCages.Report();
            AyniFreedAllies.Report();
        }
    }
}
