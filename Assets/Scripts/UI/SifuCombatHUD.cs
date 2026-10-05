using UnityEngine;
using Ayni.Core;
using Ayni.Combat;
using Ayni.Enemy;
using Ayni.Player;

namespace Ayni.UI
{
    /// <summary>
    /// HUD de combate con dibujo instantáneo en pantalla (OnGUI) para probar de inmediato:
    /// vida, edad y postura de Yari; vida y postura del rival más cercano; avisos de ataque y Juicio Ayni.
    /// </summary>
    public class SifuCombatHUD : MonoBehaviour
    {
        [SerializeField] private float enemyBarRange = 25f;

        private YariCombatController yari;
        private IllaTalismanSystem talisman;
        private StructureSystem playerStructure;

        private static readonly Color HealthColor = new Color(0.85f, 0.2f, 0.2f);
        private static readonly Color StructureColor = new Color(0.2f, 0.85f, 0.95f);
        private static readonly Color StructureDangerColor = new Color(1f, 0.6f, 0.1f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.6f);

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.GetComponent<YariCombatController>();
                talisman = player.GetComponent<IllaTalismanSystem>();
                playerStructure = player.GetComponent<StructureSystem>();
            }

            // Tutorial de inicio (se añade solo; no hace falta configurarlo en la escena)
            if (GetComponent<AyniTutorial>() == null) gameObject.AddComponent<AyniTutorial>();
        }

        private bool showControls = true;

        private void Update()
        {
            // H muestra u oculta el panel de controles (en ventanas pequeñas tapa a Yari)
            if (Input.GetKeyDown(KeyCode.H)) showControls = !showControls;
        }

        private void OnGUI()
        {
            GUI.skin.box.fontSize = 14;
            GUI.skin.label.fontSize = 14;
            GUI.skin.label.richText = true;
            GUI.skin.box.richText = true;

            DrawPlayerPanel();
            DrawControlsPanel();

            EnemyController enemy = FindNearestEnemy();
            if (enemy != null) DrawEnemyPanel(enemy);
            DrawLockOnMarker();
            DrawBrokenPostureMarkers();

            DrawCenterMessages(enemy);
        }

        private void DrawPlayerPanel()
        {
            GUILayout.BeginArea(new Rect(20, 20, 340, 190), GUI.skin.box);
            GUILayout.Label("<b>AYNI: EL RETORNO DEL GUARDIÁN</b>");

            if (talisman != null)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"<b>EDAD DE YARI:</b> {talisman.CurrentAge} años  |  Etapa: {talisman.GetCurrentStage()}");
                GUILayout.Label($"<b>ILLA SAGRADA:</b> próxima caída +{talisman.DeathCounter + 1} años");
                GUI.color = Color.white;
            }

            if (yari != null)
            {
                GUILayout.Label($"Vida: {yari.CurrentHealth:F0} / {yari.MaxHealth:F0}");
                DrawBar(GUILayoutUtility.GetRect(300, 14), yari.MaxHealth > 0f ? yari.CurrentHealth / yari.MaxHealth : 0f, HealthColor);
            }

            if (playerStructure != null)
            {
                GUILayout.Label($"Estructura / Postura: {playerStructure.CurrentStructure:F0} / {playerStructure.MaxStructure:F0}");
                float fill = playerStructure.StructureRatio;
                DrawBar(GUILayoutUtility.GetRect(300, 14), fill, fill > 0.8f ? StructureDangerColor : StructureColor);
            }
            GUILayout.EndArea();
        }

        private void DrawControlsPanel()
        {
            if (!showControls)
            {
                GUILayout.BeginArea(new Rect(20, Screen.height - 50, 300, 30), GUI.skin.box);
                GUILayout.Label("<b>[H]</b> Mostrar controles   <b>[F1]</b> Tutorial");
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(20, Screen.height - 195, Mathf.Min(620f, Screen.width - 40f), 175), GUI.skin.box);
            GUILayout.Label("<b>CONTROLES DE COMBATE RUMI MAKI</b>  ([H] ocultar · [F1] tutorial)");
            GUILayout.Label("• <b>[Clic Izq]:</b> Golpe Ligero  |  <b>[Q / E]:</b> Golpe Pesado  |  <b>[LShift]:</b> Sprint");
            GUILayout.Label("• <b>[Tab / Clic central]:</b> Fijar o soltar al rival (Lock-On)");
            GUILayout.Label("• <b>[Clic Der / G]:</b> Guardia — púlsala justo antes del impacto para el Parry");
            GUILayout.Label("• <b>[Guardia + S / Espacio]:</b> Agacharse (evita ataques altos)  |  <b>[Guardia + W]:</b> Saltar (evita barridos)");
            GUILayout.Label("• <b>Postura del rival rota:</b> [F] Venganza (Matar)  |  [X] Restaurar Ayni (Perdonar)");
            GUILayout.EndArea();
        }

        private void DrawEnemyPanel(EnemyController enemy)
        {
            // Centrado arriba; en ventanas estrechas se coloca a la derecha del panel de Yari para no solaparse
            float width = Mathf.Clamp(Screen.width - 390f, 220f, 420f);
            float x = Mathf.Max(Screen.width / 2f - width / 2f, 370f);
            GUILayout.BeginArea(new Rect(x, 20, width, 90), GUI.skin.box);
            GUILayout.Label($"<b>{enemy.CharacterName.ToUpper()}</b>   Vida: {enemy.CurrentHealth:F0} / {enemy.MaxHealth:F0}");
            DrawBar(GUILayoutUtility.GetRect(width - 20, 14), enemy.MaxHealth > 0f ? enemy.CurrentHealth / enemy.MaxHealth : 0f, HealthColor);
            GUILayout.Space(4);
            float fill = enemy.Structure.StructureRatio;
            DrawBar(GUILayoutUtility.GetRect(width - 20, 10), fill, fill > 0.8f ? StructureDangerColor : StructureColor);
            GUILayout.EndArea();
        }

        private void DrawCenterMessages(EnemyController enemy)
        {
            Rect center = new Rect(Screen.width / 2f - 260, Screen.height / 2f - 110, 520, 70);
            GUI.skin.box.fontSize = 20;

            if (yari != null && yari.IsGameOver)
            {
                GUI.color = Color.red;
                GUI.Box(center, "EL TALISMÁN ILLA SE HA ROTO\n[R] Reintentar");
            }
            else if (yari != null && yari.IsDead)
            {
                GUI.color = Color.yellow;
                GUI.Box(center, "YARI HA CAÍDO\nEl Talismán Illa reclama sus años...");
            }
            else if (enemy != null && enemy.Structure.IsBroken)
            {
                GUI.color = Color.red;
                GUI.Box(center, $"¡POSTURA DE {enemy.CharacterName.ToUpper()} ROTA! ({enemy.Structure.BrokenTimeRemaining:F1} s)\n" +
                                "[F] Golpe Letal (Venganza)  |  [X] Desarme y Perdón (Ayni)");
            }
            else if (enemy != null && enemy.IsWindingUp)
            {
                bool high = enemy.PendingAttackHeight == AttackHeight.High;
                GUI.color = high ? new Color(1f, 0.4f, 0.3f) : Color.yellow;
                GUI.Box(new Rect(center.x + 110, center.y, 300, 36), high ? "¡ATAQUE ALTO!" : "¡BARRIDO BAJO!");
            }

            GUI.color = Color.white;
            GUI.skin.box.fontSize = 14;
        }

        /// <summary>Marca roja que late sobre cada rival con la postura rota: momento de rematar (F) o perdonar (X).</summary>
        private void DrawBrokenPostureMarkers()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController e = enemies[i];
                if (e == null || e.IsDead || !e.Structure.IsBroken) continue;

                Vector3 screen = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 2.05f);
                if (screen.z <= 0f) continue;

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                float size = Mathf.Lerp(16f, 24f, pulse);
                var rect = new Rect(screen.x - size / 2f, Screen.height - screen.y - size / 2f, size, size);

                Matrix4x4 prev = GUI.matrix;
                Color prevColor = GUI.color;
                GUIUtility.RotateAroundPivot(45f, rect.center);
                GUI.color = new Color(1f, 0.15f, 0.1f, Mathf.Lerp(0.7f, 1f, pulse));
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.matrix = prev;

                GUI.color = Color.white;
                var label = new Rect(screen.x - 40f, Screen.height - screen.y - size - 22f, 80f, 20f);
                var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
                GUI.Label(label, "F  /  X", style);
                GUI.color = prevColor;
            }
        }

        /// <summary>Marca en pantalla sobre el rival fijado con Lock-On.</summary>
        private void DrawLockOnMarker()
        {
            if (yari == null || yari.LockTarget == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(yari.LockTarget.transform.position + Vector3.up * 1.35f);
            if (screen.z <= 0f) return;

            const float size = 14f;
            var rect = new Rect(screen.x - size / 2f, Screen.height - screen.y - size / 2f, size, size);
            Matrix4x4 prev = GUI.matrix;
            Color prevColor = GUI.color;
            GUIUtility.RotateAroundPivot(45f, rect.center);
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, size - 8f, size - 8f), Texture2D.whiteTexture);
            GUI.matrix = prev;
            GUI.color = prevColor;
        }

        /// <summary>Rival fijado, o si no hay, el rival vivo más cercano a Yari dentro del rango de las barras.</summary>
        private EnemyController FindNearestEnemy()
        {
            if (yari == null) return null;
            if (yari.LockTarget != null && !yari.LockTarget.IsDead) return yari.LockTarget;

            EnemyController nearest = null;
            float best = enemyBarRange;
            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController e = enemies[i];
                if (e == null || e.IsDead) continue;
                float dist = Vector3.Distance(yari.transform.position, e.transform.position);
                if (dist < best)
                {
                    best = dist;
                    nearest = e;
                }
            }
            return nearest;
        }

        private static void DrawBar(Rect rect, float ratio, Color fillColor)
        {
            Color prev = GUI.color;
            GUI.color = BarBackColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
