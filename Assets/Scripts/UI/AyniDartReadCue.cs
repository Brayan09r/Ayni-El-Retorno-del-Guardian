using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;
using Ayni.World;

namespace Ayni.UI
{
    /// <summary>
    /// Lo que enseña el quipu de los chasquis: con él, mientras Amaru apunta un dardo, un anillo verde se cierra sobre
    /// su pecho y se junta con el círculo del centro justo cuando sopla. Al cerrarse sale "¡Esquiva!": es el momento de
    /// agacharse o apartarse (Guardia + abajo o a un lado). Encima, qué dardo de la ráfaga es (1/3, 2/3, 3/3).
    /// Sin el quipu solo queda el aviso general del HUD. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniDartReadCue : MonoBehaviour
    {
        private YariCombatController yari;
        private bool logged;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
        }

        private void OnGUI()
        {
            if (!AyniPathRewards.QuipuRecovered || !AmaruHunter.DartWarning || AmaruHunter.Shooter == null) return;
            if (AyniGameState.CinematicPlaying || AyniJudgment.Active || AyniDeathAging.Active || (yari != null && yari.IsDead)) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(AmaruHunter.Shooter.position + Vector3.up * 1.35f);
            if (screen.z <= 0f) return;
            var c = new Vector2(screen.x, Screen.height - screen.y);
            if (!logged && Event.current.type == EventType.Repaint)
            {
                logged = true;
                Debug.Log("[Ayni Quipu] Anillo de lectura del dardo en pantalla");
            }

            float k = Mathf.Clamp01(AmaruHunter.WarningProgress);
            Color green = AmaruHunter.PoisonGreen;
            const float target = 30f;
            float size = Mathf.Lerp(130f, target, k * k * (3f - 2f * k));
            // Círculo fijo del centro (adonde llega el anillo) y el anillo que se cierra
            AyniInkaUI.Draw(new Rect(c.x - target / 2f, c.y - target / 2f, target, target), AyniInkaUI.Ring, new Color(1f, 1f, 1f, 0.55f));
            AyniInkaUI.Draw(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), AyniInkaUI.Ring, new Color(green.r, green.g, green.b, 0.35f + 0.6f * k));
            if (k > 0.9f)
            {
                AyniInkaUI.Draw(new Rect(c.x - 22f, c.y - 22f, 44f, 44f), AyniInkaUI.Disc, new Color(green.r, green.g, green.b, 0.35f));
                AyniInkaUI.Label(new Rect(c.x - 80f, c.y + 26f, 160f, 20f), AyniInkaUI.Spaced("¡Esquiva!"),
                                 AyniInkaUI.Style(14, FontStyle.Bold, TextAnchor.MiddleCenter), green);
            }
            if (AmaruHunter.DartsInVolley > 1)
            {
                AyniInkaUI.Label(new Rect(c.x - 40f, c.y - size / 2f - 20f, 80f, 16f), $"{AmaruHunter.DartNumber} / {AmaruHunter.DartsInVolley}",
                                 AyniInkaUI.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter), AyniInkaUI.Bone, 0.9f);
            }
        }
    }
}
