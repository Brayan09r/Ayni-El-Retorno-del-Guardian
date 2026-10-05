using UnityEngine;
using Ayni.Core;
using Ayni.Combat;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;

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

        private float avoidMessageUntil;

        private static string Key(AyniInput.Action action) => AyniInput.Label(action);

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.GetComponent<YariCombatController>();
                talisman = player.GetComponent<IllaTalismanSystem>();
                playerStructure = player.GetComponent<StructureSystem>();
                if (yari != null) yari.OnAvoided += HandleAvoided;
            }

            // Prólogo, tutorial y prueba del mando (se añaden solos; no hace falta configurarlos en la escena).
            // El prólogo va primero: el tutorial espera a que termine.
            if (GetComponent<AyniPrologue>() == null) gameObject.AddComponent<AyniPrologue>();
            if (GetComponent<AyniTutorial>() == null) gameObject.AddComponent<AyniTutorial>();
            if (GetComponent<AyniGamepadTester>() == null) gameObject.AddComponent<AyniGamepadTester>();
            // Juicio Ayni del jefe y desenlace del nivel
            if (GetComponent<AyniJudgment>() == null) gameObject.AddComponent<AyniJudgment>();
            if (GetComponent<AyniLevelOutcome>() == null) gameObject.AddComponent<AyniLevelOutcome>();

            // Amaru el Cazador: segunda fase con salto atrás y dardos envenenados
            foreach (EnemyController boss in EnemyController.All)
            {
                if (boss != null && boss.IsBoss && boss.CharacterName.Contains("Amaru") && boss.GetComponent<AmaruHunter>() == null)
                {
                    boss.gameObject.AddComponent<AmaruHunter>();
                }
            }
        }

        private void OnDestroy()
        {
            if (yari != null) yari.OnAvoided -= HandleAvoided;
        }

        private void HandleAvoided()
        {
            avoidMessageUntil = Time.unscaledTime + 0.7f;
        }

        private bool showControls = true;

        private void Update()
        {
            // H (cruceta arriba en el mando) muestra u oculta el panel de controles (en ventanas pequeñas tapa a Yari)
            if (AyniInput.Down(AyniInput.Action.ToggleHud) && !AyniGameState.InputLocked) showControls = !showControls;
        }

        private void OnGUI()
        {
            // Durante el prólogo (y el desenlace) la pantalla es de la escena; en el Juicio Ayni, de la decisión
            if (AyniGameState.CinematicPlaying || AyniJudgment.Active) return;

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
                GUILayout.Label($"<b>EDAD DE YARI:</b> {talisman.CurrentAge} años  |  Etapa: {StageName(talisman.GetCurrentStage())}");
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

        private static string StageName(IllaTalismanSystem.AgeStage stage)
        {
            switch (stage)
            {
                case IllaTalismanSystem.AgeStage.Youth: return "Joven";
                case IllaTalismanSystem.AgeStage.Prime: return "Maduro";
                default: return "Anciano";
            }
        }

        private void DrawControlsPanel()
        {
            bool pad = AyniInput.UsingGamepad;
            if (!showControls)
            {
                GUILayout.BeginArea(new Rect(20, Screen.height - 50, 360, 30), GUI.skin.box);
                GUILayout.Label($"<b>[{Key(AyniInput.Action.ToggleHud)}]</b> Mostrar controles   <b>[{Key(AyniInput.Action.Tutorial)}]</b> Tutorial");
                GUILayout.EndArea();
                return;
            }

            string down = pad ? "Stick ↓" : "S";
            string up = pad ? "Stick ↑ / A" : "W / Espacio";
            string sides = pad ? "Stick ← →" : "A / D";

            GUILayout.BeginArea(new Rect(20, Screen.height - 195, Mathf.Min(680f, Screen.width - 40f), 175), GUI.skin.box);
            GUILayout.Label($"<b>CONTROLES RUMI MAKI — {(pad ? "MANDO" : "TECLADO Y RATÓN")}</b>  ([{Key(AyniInput.Action.ToggleHud)}] ocultar · [{Key(AyniInput.Action.Tutorial)}] tutorial)");
            GUILayout.Label($"• <b>[{Key(AyniInput.Action.LightAttack)}]:</b> Golpe Ligero  |  <b>[{Key(AyniInput.Action.HeavyAttack)}]:</b> Golpe Pesado  |  <b>[{Key(AyniInput.Action.Sprint)}]:</b> Correr");
            GUILayout.Label($"• <b>[{Key(AyniInput.Action.LockOn)}]:</b> Fijar o soltar al rival (Lock-On)");
            GUILayout.Label($"• <b>[{Key(AyniInput.Action.Guard)}] mantener:</b> Guardia (Yari se planta) — púlsala justo antes del impacto para el Parry");
            GUILayout.Label($"• <b>Guardia + {down}:</b> Agacharse (evita altos)  |  <b>+ {up}:</b> Saltito (evita barridos)  |  <b>+ {sides}:</b> Balanceo (evita todo)");
            GUILayout.Label($"• <b>Postura del rival rota:</b> [{Key(AyniInput.Action.Execute)}] Venganza (Matar)  |  [{Key(AyniInput.Action.Mercy)}] Restaurar Ayni (Perdonar)");
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
                GUI.Box(center, $"EL TALISMÁN ILLA SE HA ROTO\n[{Key(AyniInput.Action.Restart)}] Reintentar");
            }
            else if (yari != null && yari.IsDead)
            {
                GUI.color = Color.yellow;
                GUI.Box(center, "YARI HA CAÍDO\nEl Talismán Illa reclama sus años...");
            }
            else if (AyniJudgment.Active)
            {
                // El Juicio Ayni dibuja su propia pantalla
            }
            else if (enemy != null && enemy.Structure.IsBroken && !enemy.CanBeJudged)
            {
                GUI.color = new Color(0.4f, 0.9f, 1f);
                GUI.Box(center, $"¡POSTURA DE {enemy.CharacterName.ToUpper()} ROTA! ({enemy.Structure.BrokenTimeRemaining:F1} s)\n" +
                                "¡Castígalo ahora! Sus golpes recibidos duelen más");
            }
            else if (enemy != null && enemy.Structure.IsBroken)
            {
                GUI.color = Color.red;
                GUI.Box(center, $"¡POSTURA DE {enemy.CharacterName.ToUpper()} ROTA! ({enemy.Structure.BrokenTimeRemaining:F1} s)\n" +
                                $"[{Key(AyniInput.Action.Execute)}] Golpe Letal (Venganza)  |  [{Key(AyniInput.Action.Mercy)}] Desarme y Perdón (Ayni)");
            }
            else if (AmaruHunter.DartWarning)
            {
                bool pad = AyniInput.UsingGamepad;
                GUI.color = new Color(0.45f, 1f, 0.35f);
                GUI.Box(new Rect(center.x + 60, center.y, 400, 36), "¡DARDO ENVENENADO!  (" + (pad ? "LB + ↓ o ← →" : "Guardia + S o A/D") + ")");
            }
            else if (Time.unscaledTime < avoidMessageUntil)
            {
                GUI.color = new Color(0.6f, 0.92f, 1f);
                GUI.Box(new Rect(center.x + 110, center.y, 300, 36), "¡ESQUIVA!  Contraataca ya");
            }
            else if (enemy != null && enemy.IsWindingUp)
            {
                bool high = enemy.PendingAttackHeight == AttackHeight.High;
                bool pad = AyniInput.UsingGamepad;
                GUI.color = high ? new Color(1f, 0.4f, 0.3f) : Color.yellow;
                string tip = high ? (pad ? "LB + ↓" : "Guardia + S") : (pad ? "LB + ↑" : "Guardia + W");
                GUI.Box(new Rect(center.x + 80, center.y, 360, 36), (high ? "¡ATAQUE ALTO!" : "¡BARRIDO BAJO!") + $"  ({tip})");
            }

            GUI.color = Color.white;
            GUI.skin.box.fontSize = 14;
        }

        /// <summary>Marca roja que late sobre cada rival con la postura rota: momento de rematar (F / B) o perdonar (X / A).</summary>
        private void DrawBrokenPostureMarkers()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController e = enemies[i];
                if (e == null || !e.CanBeJudged) continue;

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
                GUI.Label(label, $"{Key(AyniInput.Action.Execute)}  /  {Key(AyniInput.Action.Mercy)}", style);
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
