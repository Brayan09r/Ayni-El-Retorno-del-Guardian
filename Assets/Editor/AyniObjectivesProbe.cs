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
        }
    }
}
