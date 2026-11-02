using UnityEngine;
using Ayni.Core;
using Ayni.Combat;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;

namespace Ayni.UI
{
    /// <summary>
    /// HUD de combate (OnGUI), minimalista y con motivos incas (ver AyniInkaUI): el medallón de la Illa con la edad,
    /// la estructura y la vida de Yari abajo a la izquierda; el rival arriba, centrado; y los avisos en bandas de piedra.
    /// </summary>
    public class SifuCombatHUD : MonoBehaviour
    {
        [SerializeField] private float enemyBarRange = 25f;

        private YariCombatController yari;
        private IllaTalismanSystem talisman;
        private StructureSystem playerStructure;

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
            // Viento de la cordillera y tambores al entrar en los patios con rivales
            if (GetComponent<AyniLevelAmbience>() == null) gameObject.AddComponent<AyniLevelAmbience>();
            // Cambio de fase de los jefes (caída, grito y segunda fase)
            if (GetComponent<AyniBossPhaseDirector>() == null) gameObject.AddComponent<AyniBossPhaseDirector>();
            // Pausa con las opciones de sonido (Esc / Menu)
            if (GetComponent<AyniPauseMenu>() == null) gameObject.AddComponent<AyniPauseMenu>();

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

        // Oculto al empezar (como en Sifu, la pantalla limpia); [H] lo muestra y el tutorial está en F1
        private bool showControls = false;

        private void Update()
        {
            // H (cruceta arriba en el mando) muestra u oculta el panel de controles (en ventanas pequeñas tapa a Yari)
            if (AyniInput.Down(AyniInput.Action.ToggleHud) && !AyniGameState.InputLocked) showControls = !showControls;
        }

        private void OnGUI()
        {
            // Durante el prólogo (y el desenlace) la pantalla es de la escena; en el Juicio Ayni, de la decisión
            if (AyniGameState.CinematicPlaying || AyniJudgment.Active) return;
            // Caída mortal a la quebrada: la pantalla es de la escena (franjas de cine, fundido y título)
            if (yari != null && yari.IsBeingRescued) return;

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

        // ───────────────────────── Yari ─────────────────────────

        // Valores de hace un instante: el tramo perdido se ve claro y se consume poco a poco
        private float healthGhost = 1f, structureGhost;
        private float healthGhostHold, structureGhostHold;
        private EnemyController ghostEnemy;
        private float enemyHealthGhost = 1f, enemyGhostHold;
        private int lastAge = -1;
        private float ageGlowUntil;

        /// <summary>
        /// Abajo a la izquierda, como en Sifu: el medallón de la Illa con la edad de Yari, la estructura (fina, de oro)
        /// y la vida (roja de grana), con remates escalonados y una franja de tocapu. Sin recuadros ni textos largos.
        /// </summary>
        private void DrawPlayerPanel()
        {
            if (yari == null) return;
            float h = Screen.height;
            var medal = new Rect(26f, h - 26f - 80f, 80f, 80f);

            int age = talisman != null ? talisman.CurrentAge : 0;
            if (lastAge >= 0 && age != lastAge) ageGlowUntil = Time.unscaledTime + 2f;
            lastAge = age;
            float glow = Mathf.Clamp01((ageGlowUntil - Time.unscaledTime) / 2f);
            AyniInkaUI.Medallion(medal, talisman != null ? age.ToString() : "—", "AÑOS", glow);

            // Lo que cobrará la Illa en la próxima caída: una cuenta roja en el borde del medallón
            if (talisman != null)
            {
                var badge = new Rect(medal.xMax - 22f, medal.y - 4f, 26f, 26f);
                AyniInkaUI.Draw(badge, AyniInkaUI.Disc, new Color(0f, 0f, 0f, 0.7f));
                AyniInkaUI.Draw(new Rect(badge.x + 2f, badge.y + 2f, 22f, 22f), AyniInkaUI.Disc, AyniInkaUI.Grana);
                AyniInkaUI.Draw(badge, AyniInkaUI.Ring, AyniInkaUI.Gold);
                AyniInkaUI.Label(badge, $"+{talisman.DeathCounter + 1}", AyniInkaUI.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter), AyniInkaUI.Bone);
            }

            float x = medal.xMax + 22f;
            const float width = 250f;
            var nameRect = new Rect(x, medal.y + 6f, width, 16f);
            string stage = talisman != null ? StageName(talisman.GetCurrentStage()) : "";
            AyniInkaUI.Label(nameRect, AyniInkaUI.Spaced("Yari") + $"   <color=#bfae8e><size=11>{AyniInkaUI.Spaced(stage)}</size></color>",
                             AyniInkaUI.Style(13, FontStyle.Bold, TextAnchor.MiddleLeft), AyniInkaUI.Gold);

            // Estructura: fina, de oro; se enciende en ocre cuando está a punto de romperse
            if (playerStructure != null)
            {
                float s = playerStructure.StructureRatio;
                Ghost(ref structureGhost, ref structureGhostHold, s);
                Color c = s > 0.8f ? Color.Lerp(AyniInkaUI.Ochre, AyniInkaUI.BrightGold, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f)) : AyniInkaUI.Gold;
                AyniInkaUI.Bar(new Rect(x, medal.y + 30f, width * 0.8f, 4f), s, structureGhost, c);
            }

            // Vida
            float hr = yari.MaxHealth > 0f ? yari.CurrentHealth / yari.MaxHealth : 0f;
            Ghost(ref healthGhost, ref healthGhostHold, hr);
            AyniInkaUI.Bar(new Rect(x, medal.y + 44f, width, 8f), hr, healthGhost, AyniInkaUI.Grana);

            AyniInkaUI.TocapuBand(new Rect(x - 4f, medal.y + 62f, width + 8f, 6f), new Color(AyniInkaUI.Gold.r, AyniInkaUI.Gold.g, AyniInkaUI.Gold.b, 0.35f));
        }

        /// <summary>El valor fantasma espera un instante y luego baja hasta el real (o sube con él al curarse).</summary>
        private static void Ghost(ref float ghost, ref float hold, float value)
        {
            if (value >= ghost)
            {
                ghost = value;
                hold = Time.unscaledTime + 0.45f;
            }
            else if (Time.unscaledTime > hold)
            {
                ghost = Mathf.MoveTowards(ghost, value, 0.6f * Time.unscaledDeltaTime);
            }
        }

        private static string StageName(IllaTalismanSystem.AgeStage stage)
        {
            switch (stage)
            {
                case IllaTalismanSystem.AgeStage.Youth: return "joven";
                case IllaTalismanSystem.AgeStage.Prime: return "maduro";
                default: return "anciano";
            }
        }

        // ───────────────────────── Controles ─────────────────────────

        private void DrawControlsPanel()
        {
            bool pad = AyniInput.UsingGamepad;
            var small = AyniInkaUI.Style(12, FontStyle.Normal, TextAnchor.MiddleLeft);
            if (!showControls)
            {
                // Arriba a la izquierda, discreto: cómo ver los controles, el tutorial y la versión que se prueba
                AyniInkaUI.Label(new Rect(22f, 16f, 360f, 18f),
                    $"<b>[{Key(AyniInput.Action.ToggleHud)}]</b> Controles   <b>[{Key(AyniInput.Action.Tutorial)}]</b> Tutorial   <color=#8f8572>{AyniVersion.Label}</color>",
                    small, AyniInkaUI.Bone, 0.75f);
                return;
            }

            string down = pad ? "Stick ↓" : "S";
            string up = pad ? "Stick ↑ / A" : "W / Espacio";
            string sides = pad ? "Stick ← →" : "A / D";

            var area = new Rect(16f, 12f, Mathf.Min(700f, Screen.width - 32f), 150f);
            AyniInkaUI.Band(area, 0.95f);
            var line = AyniInkaUI.Style(13, FontStyle.Normal, TextAnchor.MiddleLeft);
            float y = area.y + 8f;
            void Row(string text)
            {
                AyniInkaUI.Label(new Rect(area.x + 14f, y, area.width - 28f, 20f), text, line, AyniInkaUI.Bone);
                y += 22f;
            }
            AyniInkaUI.Label(new Rect(area.x + 14f, y, area.width - 28f, 20f),
                $"<b>{AyniInkaUI.Spaced("Rumi Maki")}</b>   <color=#bfae8e>{(pad ? "mando" : "teclado y ratón")} · [{Key(AyniInput.Action.ToggleHud)}] ocultar · [{Key(AyniInput.Action.Pause)}] pausa</color>",
                line, AyniInkaUI.Gold);
            y += 24f;
            Row($"<b>[{Key(AyniInput.Action.LightAttack)}]</b> golpe ligero   <b>[{Key(AyniInput.Action.HeavyAttack)}]</b> golpe pesado   <b>[{Key(AyniInput.Action.Sprint)}]</b> correr   <b>[{Key(AyniInput.Action.Jump)}]</b> saltar / trepar");
            Row($"<b>[{Key(AyniInput.Action.Guard)}]</b> guardia (justo antes del golpe: desvío)   <b>[{Key(AyniInput.Action.LockOn)}]</b> fijar rival");
            Row($"<b>Guardia + {down}</b> agacharse   <b>+ {up}</b> saltito   <b>+ {sides}</b> balanceo");
            Row($"<b>Postura rota:</b>  [{Key(AyniInput.Action.Execute)}] Venganza   [{Key(AyniInput.Action.Mercy)}] Ayni (perdón)");
        }

        // ───────────────────────── Rival ─────────────────────────

        /// <summary>Arriba, centrado: el nombre en mayúsculas espaciadas, las fases como rombos y dos barras finas.</summary>
        private void DrawEnemyPanel(EnemyController enemy)
        {
            float width = Mathf.Clamp(Screen.width * 0.36f, 240f, 440f);
            float x = Screen.width / 2f - width / 2f;
            const float top = 22f;

            string name = AyniInkaUI.Spaced(enemy.CharacterName);
            AyniInkaUI.Label(new Rect(x, top, width, 18f), name, AyniInkaUI.Style(14, FontStyle.Bold, TextAnchor.MiddleCenter), AyniInkaUI.Bone);

            // Fases del jefe: un rombo por fase, lleno el de la fase en curso y las ya pasadas
            if (enemy.PhaseCount > 1)
            {
                float pipX = x + width / 2f - (enemy.PhaseCount * 14f) / 2f;
                for (int i = 0; i < enemy.PhaseCount; i++)
                {
                    var pip = new Rect(pipX + i * 14f + 3f, top + 22f, 8f, 8f);
                    Matrix4x4 m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(45f, pip.center);
                    AyniInkaUI.Fill(pip, i < enemy.Phase ? AyniInkaUI.Gold : new Color(0f, 0f, 0f, 0.6f));
                    GUI.matrix = m;
                }
            }

            if (ghostEnemy != enemy)
            {
                ghostEnemy = enemy;
                enemyHealthGhost = 1f;
            }
            float hr = enemy.MaxHealth > 0f ? enemy.CurrentHealth / enemy.MaxHealth : 0f;
            Ghost(ref enemyHealthGhost, ref enemyGhostHold, hr);
            float barsY = top + (enemy.PhaseCount > 1 ? 38f : 26f);
            AyniInkaUI.Bar(new Rect(x, barsY, width, 6f), hr, enemyHealthGhost, AyniInkaUI.Grana);

            float s = enemy.Structure.StructureRatio;
            Color sc = enemy.Structure.IsBroken ? Color.Lerp(AyniInkaUI.Grana, AyniInkaUI.BrightGold, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f))
                     : s > 0.8f ? AyniInkaUI.Ochre : AyniInkaUI.Gold;
            AyniInkaUI.Bar(new Rect(x + width * 0.15f, barsY + 12f, width * 0.7f, 3f), enemy.Structure.IsBroken ? 1f : s, s, sc);
        }

        // ───────────────────────── Avisos ─────────────────────────

        private void DrawCenterMessages(EnemyController enemy)
        {
            // Al caer, la cuenta de años (AyniDeathAging) dibuja su propia pantalla
            if (AyniDeathAging.Active) return;

            Rect center = new Rect(Screen.width / 2f - 280, Screen.height / 2f - 120, 560, 64);
            Rect slim = new Rect(Screen.width / 2f - 230, Screen.height / 2f - 110, 460, 40);
            var big = AyniInkaUI.Style(18, FontStyle.Bold, TextAnchor.MiddleCenter);
            var mid = AyniInkaUI.Style(16, FontStyle.Bold, TextAnchor.MiddleCenter);

            if (yari != null && yari.IsGameOver)
            {
                AyniInkaUI.Band(center);
                AyniInkaUI.Label(center, $"{AyniInkaUI.Spaced("La Illa se ha roto")}\n<size=13>[{Key(AyniInput.Action.Restart)}] Reintentar</size>", big, new Color(1f, 0.45f, 0.35f));
            }
            else if (AyniJudgment.Active)
            {
                // El Juicio Ayni dibuja su propia pantalla
            }
            else if (enemy != null && enemy.Structure.IsBroken && !enemy.CanBeJudged)
            {
                AyniInkaUI.Band(center);
                AyniInkaUI.Label(center, $"{AyniInkaUI.Spaced("Postura rota")}  <size=13>({enemy.Structure.BrokenTimeRemaining:F1} s)</size>\n<size=13>¡Castígalo ahora! Sus golpes recibidos duelen más</size>",
                                 big, new Color(0.55f, 0.92f, 1f));
            }
            else if (enemy != null && enemy.Structure.IsBroken)
            {
                AyniInkaUI.Band(center);
                AyniInkaUI.Label(center, $"{AyniInkaUI.Spaced("Postura rota")}  <size=13>({enemy.Structure.BrokenTimeRemaining:F1} s)</size>\n" +
                                 $"<size=13>[{Key(AyniInput.Action.Execute)}] Venganza   ·   [{Key(AyniInput.Action.Mercy)}] Ayni (desarme y perdón)</size>",
                                 big, new Color(1f, 0.5f, 0.4f));
            }
            else if (AmaruHunter.DartWarning)
            {
                bool pad = AyniInput.UsingGamepad;
                AyniInkaUI.Band(slim);
                AyniInkaUI.Label(slim, $"¡Dardo envenenado!  <size=13>({(pad ? "LB + ↓ o ← →" : "Guardia + S o A/D")})</size>", mid, new Color(0.5f, 1f, 0.4f));
            }
            else if (Time.unscaledTime < avoidMessageUntil)
            {
                AyniInkaUI.Band(slim);
                AyniInkaUI.Label(slim, "¡Esquiva!  <size=13>contraataca ya</size>", mid, new Color(0.62f, 0.92f, 1f));
            }
            else if (enemy != null && enemy.IsWindingUp)
            {
                bool high = enemy.PendingAttackHeight == AttackHeight.High;
                bool pad = AyniInput.UsingGamepad;
                string tip = high ? (pad ? "LB + ↓" : "Guardia + S") : (pad ? "LB + ↑" : "Guardia + W");
                AyniInkaUI.Band(slim);
                AyniInkaUI.Label(slim, (high ? "¡Ataque alto!" : "¡Barrido bajo!") + $"  <size=13>({tip})</size>", mid,
                                 high ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.85f, 0.3f));
            }
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
    }
}
