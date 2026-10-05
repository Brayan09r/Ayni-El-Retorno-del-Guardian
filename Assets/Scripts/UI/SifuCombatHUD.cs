using System;
using UnityEngine;
using UnityEngine.UI;
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
            EnsureCanvasAndBindings();
        }

        private void Update()
        {
            if (playerCombat == null || playerStructure == null || talisman == null)
            {
                FindPlayerReferences();
            }

            if (hudCanvas != null && hudCanvas.renderMode == RenderMode.ScreenSpaceCamera && hudCanvas.worldCamera == null)
            {
                if (Camera.main != null) hudCanvas.worldCamera = Camera.main;
            }

            UpdateHealthDisplay();
            UpdateStructureDisplay();
            UpdateTalismanDisplay();
            UpdateFinisherPrompt();
        }

        /// <summary>
        /// Localiza a Yari en la escena y enlaza sus componentes de combate y talismán.
        /// </summary>
        public void FindPlayerReferences()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = GameObject.Find("Yari_Hero");
            }

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

        private void UpdateHealthDisplay()
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

        private void UpdateTalismanDisplay()
        {
            if (talisman == null) return;

            if (ageText != null)
            {
                string stageName = talisman.GetCurrentStage() switch
                {
                    IllaTalismanSystem.AgeStage.Youth => "JUVENTUD",
                    IllaTalismanSystem.AgeStage.Prime => "MADUREZ",
                    IllaTalismanSystem.AgeStage.Elder => "ANCESTRO",
                    _ => "GUARDIÁN"
                };

                ageText.text = $"<b>EDAD:</b> <color=#F1C40F>{talisman.CurrentAge} AÑOS</color>  |  <color=#BDC3C7>{stageName}</color>";
            }

            if (talismanText != null)
            {
                talismanText.text = $"<b>ILLA SAGRADA:</b> +{talisman.DeathCounter} AÑOS / CAÍDAS";
            }
        }

        private void UpdateFinisherPrompt()
        {
            if (finisherPromptPanel == null) return;
            if (playerCombat == null)
            {
                if (finisherPromptPanel.activeSelf) finisherPromptPanel.SetActive(false);
                return;
            }

            EnemyController brokenEnemy = null;
            float closestDist = float.MaxValue;
            Vector3 playerPos = playerCombat.transform.position;
            float maxDist = AyniPurificationManager.Instance != null ? AyniPurificationManager.Instance.ExecutionRange : finisherPromptMaxDistance;

            var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead && enemy.Structure != null && enemy.Structure.IsBroken)
                {
                    float dist = Vector3.Distance(playerPos, enemy.transform.position);
                    // Solo activar el aviso si el rival roto está en el rango de proximidad para ejecutar
                    if (dist <= maxDist && dist < closestDist)
                    {
                        brokenEnemy = enemy;
                        closestDist = dist;
                    }
                }
            }

            if (brokenEnemy != null)
            {
                if (!finisherPromptPanel.activeSelf) finisherPromptPanel.SetActive(true);
                if (finisherPromptTitle != null)
                {
                    finisherPromptTitle.text = $"¡POSTURA DE {brokenEnemy.CharacterName.ToUpper()} ROTA!";
                }
            }
            else
            {
                if (finisherPromptPanel.activeSelf) finisherPromptPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Comprueba si el Canvas uGUI existe y está enlazado; de lo contrario lo construye programáticamente.
        /// </summary>
        public void EnsureCanvasAndBindings()
        {
            FindPlayerReferences();

            if (hudCanvas == null)
            {
                hudCanvas = GetComponentInChildren<Canvas>();
                if (hudCanvas == null)
                {
                    var canvasObj = GameObject.Find("Canvas_SifuHUD");
                    if (canvasObj != null) hudCanvas = canvasObj.GetComponent<Canvas>();
                }
            }

            if (hudCanvas != null)
            {
                if (hudCanvas.renderMode == RenderMode.ScreenSpaceCamera && hudCanvas.worldCamera == null)
                {
                    if (Camera.main != null) hudCanvas.worldCamera = Camera.main;
                }

                // Reconectar componentes uGUI desde la jerarquía si estuvieran desvinculados
                if (healthBarFill == null)
                    healthBarFill = hudCanvas.transform.Find("Panel_TopLeft_Sifu/HealthBar_BG/HealthBar_Fill")?.GetComponent<Image>();
                if (healthText == null)
                    healthText = hudCanvas.transform.Find("Panel_TopLeft_Sifu/HealthLabel")?.GetComponent<Text>();
                if (structureBarFill == null)
                    structureBarFill = hudCanvas.transform.Find("Panel_TopLeft_Sifu/StructureBar_BG/StructureBar_Fill")?.GetComponent<Image>();
                if (structureText == null)
                    structureText = hudCanvas.transform.Find("Panel_TopLeft_Sifu/StructureLabel")?.GetComponent<Text>();
                if (ageText == null)
                    ageText = hudCanvas.transform.Find("Panel_TopLeft_Sifu/AgeText")?.GetComponent<Text>();
                if (talismanText == null)
                    talismanText = hudCanvas.transform.Find("Panel_TopLeft_Sifu/TalismanText")?.GetComponent<Text>();
                if (finisherPromptPanel == null)
                    finisherPromptPanel = hudCanvas.transform.Find("Panel_FinisherPrompt")?.gameObject;
                if (finisherPromptTitle == null && finisherPromptPanel != null)
                    finisherPromptTitle = finisherPromptPanel.transform.Find("FinisherTitle")?.GetComponent<Text>();
            }

            if (hudCanvas == null || healthBarFill == null || structureBarFill == null)
            {
                BuildCanvasHUD(this);
            }
        }

        /// <summary>
        /// Genera de raíz la jerarquía Canvas uGUI con estética Sifu Andina y enlaza sus componentes.
        /// </summary>
        public static Canvas BuildCanvasHUD(SifuCombatHUD hudInstance = null)
        {
            GameObject canvasObj = GameObject.Find("Canvas_SifuHUD");
            if (canvasObj != null)
            {
                if (Application.isPlaying) Destroy(canvasObj);
                else DestroyImmediate(canvasObj);
            }

            canvasObj = new GameObject("Canvas_SifuHUD");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
            }
            canvas.planeDistance = 0.5f;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            Font defaultFont = GetDefaultFont();
            Sprite whiteSpr = GetDefaultWhiteSprite();

            // 1. PANEL SUPERIOR IZQUIERDO: HUD YARI & TALISMÁN
            GameObject topLeftPanel = CreateUIObject("Panel_TopLeft_Sifu", canvasObj.transform);
            var rectTopLeft = topLeftPanel.GetComponent<RectTransform>();
            SetAnchors(rectTopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rectTopLeft.anchoredPosition = new Vector2(30f, -30f);
            rectTopLeft.sizeDelta = new Vector2(400f, 175f);

            var imgTopLeft = topLeftPanel.AddComponent<Image>();
            imgTopLeft.sprite = whiteSpr;
            imgTopLeft.color = new Color(0.08f, 0.09f, 0.12f, 0.90f); // Pizarra oscura andina

            // Marco decorativo dorado superior
            GameObject topAccent = CreateUIObject("TopAccentBar", topLeftPanel.transform);
            var rectTopAccent = topAccent.GetComponent<RectTransform>();
            SetAnchors(rectTopAccent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rectTopAccent.anchoredPosition = new Vector2(0f, 0f);
            rectTopAccent.sizeDelta = new Vector2(0f, 4f);
            var imgTopAccent = topAccent.AddComponent<Image>();
            imgTopAccent.sprite = whiteSpr;
            imgTopAccent.color = new Color(0.92f, 0.75f, 0.22f, 1f); // Oro Inca

            // Título
            GameObject titleObj = CreateUIObject("TitleText", topLeftPanel.transform);
            var rectTitle = titleObj.GetComponent<RectTransform>();
            rectTitle.anchoredPosition = new Vector2(20f, -14f);
            rectTitle.sizeDelta = new Vector2(360f, 22f);
            var txtTitle = titleObj.AddComponent<Text>();
            txtTitle.font = defaultFont;
            txtTitle.fontSize = 13;
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.color = new Color(0.92f, 0.75f, 0.22f);
            txtTitle.text = "AYNI : EL RETORNO DEL GUARDIÁN";

            // Fila de Talismán & Edad
            GameObject ageObj = CreateUIObject("AgeText", topLeftPanel.transform);
            var rectAge = ageObj.GetComponent<RectTransform>();
            rectAge.anchoredPosition = new Vector2(20f, -38f);
            rectAge.sizeDelta = new Vector2(360f, 22f);
            var txtAge = ageObj.AddComponent<Text>();
            txtAge.font = defaultFont;
            txtAge.fontSize = 13;
            txtAge.color = Color.white;
            txtAge.text = "<b>EDAD:</b> <color=#F1C40F>20 AÑOS</color>  |  <color=#BDC3C7>JUVENTUD</color>";

            GameObject talismanObj = CreateUIObject("TalismanText", topLeftPanel.transform);
            var rectTalisman = talismanObj.GetComponent<RectTransform>();
            rectTalisman.anchoredPosition = new Vector2(20f, -58f);
            rectTalisman.sizeDelta = new Vector2(360f, 20f);
            var txtTalisman = talismanObj.AddComponent<Text>();
            txtTalisman.font = defaultFont;
            txtTalisman.fontSize = 12;
            txtTalisman.color = new Color(0.88f, 0.78f, 0.45f);
            txtTalisman.text = "<b>ILLA SAGRADA:</b> +0 AÑOS / CAÍDAS";

            // Barra de Salud
            GameObject healthLabelObj = CreateUIObject("HealthLabel", topLeftPanel.transform);
            var rectHL = healthLabelObj.GetComponent<RectTransform>();
            rectHL.anchoredPosition = new Vector2(20f, -80f);
            rectHL.sizeDelta = new Vector2(360f, 18f);
            var txtHealth = healthLabelObj.AddComponent<Text>();
            txtHealth.font = defaultFont;
            txtHealth.fontSize = 11;
            txtHealth.fontStyle = FontStyle.Bold;
            txtHealth.color = new Color(0.9f, 0.9f, 0.9f);
            txtHealth.text = "SALUD: 100 / 100";

            GameObject healthBg = CreateUIObject("HealthBar_BG", topLeftPanel.transform);
            var rectHBg = healthBg.GetComponent<RectTransform>();
            rectHBg.anchoredPosition = new Vector2(20f, -100f);
            rectHBg.sizeDelta = new Vector2(360f, 14f);
            var imgHBg = healthBg.AddComponent<Image>();
            imgHBg.sprite = whiteSpr;
            imgHBg.color = new Color(0.18f, 0.08f, 0.08f, 0.95f);

            GameObject healthFill = CreateUIObject("HealthBar_Fill", healthBg.transform);
            var rectHFill = healthFill.GetComponent<RectTransform>();
            SetAnchors(rectHFill, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
            rectHFill.offsetMin = Vector2.zero;
            rectHFill.offsetMax = Vector2.zero;
            var imgHFill = healthFill.AddComponent<Image>();
            imgHFill.sprite = whiteSpr;
            imgHFill.type = Image.Type.Filled;
            imgHFill.fillMethod = Image.FillMethod.Horizontal;
            imgHFill.fillAmount = 1.0f;
            imgHFill.color = new Color(0.18f, 0.80f, 0.44f); // Jade Andino

            // Barra de Estructura / Postura
            GameObject structLabelObj = CreateUIObject("StructureLabel", topLeftPanel.transform);
            var rectSL = structLabelObj.GetComponent<RectTransform>();
            rectSL.anchoredPosition = new Vector2(20f, -122f);
            rectSL.sizeDelta = new Vector2(360f, 18f);
            var txtStruct = structLabelObj.AddComponent<Text>();
            txtStruct.font = defaultFont;
            txtStruct.fontSize = 11;
            txtStruct.fontStyle = FontStyle.Bold;
            txtStruct.color = new Color(0.9f, 0.9f, 0.9f);
            txtStruct.text = "ESTRUCTURA: 0%";

            GameObject structBg = CreateUIObject("StructureBar_BG", topLeftPanel.transform);
            var rectSBg = structBg.GetComponent<RectTransform>();
            rectSBg.anchoredPosition = new Vector2(20f, -142f);
            rectSBg.sizeDelta = new Vector2(360f, 14f);
            var imgSBg = structBg.AddComponent<Image>();
            imgSBg.sprite = whiteSpr;
            imgSBg.color = new Color(0.10f, 0.14f, 0.18f, 0.95f);

            GameObject structFill = CreateUIObject("StructureBar_Fill", structBg.transform);
            var rectSFill = structFill.GetComponent<RectTransform>();
            SetAnchors(rectSFill, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
            rectSFill.offsetMin = Vector2.zero;
            rectSFill.offsetMax = Vector2.zero;
            var imgSFill = structFill.AddComponent<Image>();
            imgSFill.sprite = whiteSpr;
            imgSFill.type = Image.Type.Filled;
            imgSFill.fillMethod = Image.FillMethod.Horizontal;
            imgSFill.fillAmount = 0.0f;
            imgSFill.color = new Color(0.11f, 0.75f, 0.70f); // Turquesa

            // 2. PANEL CENTRAL: AVISO DE REMATE (VENGANZA / AYNI)
            GameObject finisherPanel = CreateUIObject("Panel_FinisherPrompt", canvasObj.transform);
            var rectFinisher = finisherPanel.GetComponent<RectTransform>();
            SetAnchors(rectFinisher, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rectFinisher.anchoredPosition = new Vector2(0f, 50f);
            rectFinisher.sizeDelta = new Vector2(560f, 100f);

            var imgFinisher = finisherPanel.AddComponent<Image>();
            imgFinisher.sprite = whiteSpr;
            imgFinisher.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);

            // Borde dorado del banner central
            GameObject finisherBorder = CreateUIObject("FinisherBorder", finisherPanel.transform);
            var rectFBorder = finisherBorder.GetComponent<RectTransform>();
            SetAnchors(rectFBorder, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rectFBorder.anchoredPosition = new Vector2(0f, 0f);
            rectFBorder.sizeDelta = new Vector2(0f, 3f);
            var imgFBorder = finisherBorder.AddComponent<Image>();
            imgFBorder.sprite = whiteSpr;
            imgFBorder.color = new Color(0.92f, 0.75f, 0.22f);

            GameObject finisherTitle = CreateUIObject("FinisherTitle", finisherPanel.transform);
            var rectFTitle = finisherTitle.GetComponent<RectTransform>();
            rectFTitle.anchoredPosition = new Vector2(0f, 22f);
            rectFTitle.sizeDelta = new Vector2(520f, 30f);
            var txtFTitle = finisherTitle.AddComponent<Text>();
            txtFTitle.font = defaultFont;
            txtFTitle.fontSize = 17;
            txtFTitle.fontStyle = FontStyle.Bold;
            txtFTitle.alignment = TextAnchor.MiddleCenter;
            txtFTitle.color = new Color(0.95f, 0.35f, 0.35f);
            txtFTitle.text = "¡POSTURA ROTA!";

            GameObject finisherActions = CreateUIObject("FinisherActions", finisherPanel.transform);
            var rectFActions = finisherActions.GetComponent<RectTransform>();
            rectFActions.anchoredPosition = new Vector2(0f, -18f);
            rectFActions.sizeDelta = new Vector2(520f, 30f);
            var txtFActions = finisherActions.AddComponent<Text>();
            txtFActions.font = defaultFont;
            txtFActions.fontSize = 13;
            txtFActions.alignment = TextAnchor.MiddleCenter;
            txtFActions.text = "<color=#E74C3C><b>[F] VENGANZA</b> (Golpe Letal)</color>    |    <color=#1ABC9C><b>[X] AYNI</b> (Desarme y Perdón)</color>";

            finisherPanel.SetActive(false);

            // 3. BARRA INFERIOR DE CONTROLES
            GameObject controlsPanel = CreateUIObject("Panel_Controls_Sifu", canvasObj.transform);
            var rectControls = controlsPanel.GetComponent<RectTransform>();
            SetAnchors(rectControls, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            rectControls.anchoredPosition = new Vector2(30f, 20f);
            rectControls.sizeDelta = new Vector2(620f, 36f);

            var imgControls = controlsPanel.AddComponent<Image>();
            imgControls.sprite = whiteSpr;
            imgControls.color = new Color(0.06f, 0.07f, 0.09f, 0.80f);

            GameObject controlsText = CreateUIObject("ControlsText", controlsPanel.transform);
            var rectCText = controlsText.GetComponent<RectTransform>();
            SetAnchors(rectCText, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rectCText.offsetMin = new Vector2(12f, 0f);
            rectCText.offsetMax = new Vector2(-12f, 0f);
            var txtControls = controlsText.AddComponent<Text>();
            txtControls.font = defaultFont;
            txtControls.fontSize = 11;
            txtControls.alignment = TextAnchor.MiddleLeft;
            txtControls.color = new Color(0.85f, 0.85f, 0.85f);
            txtControls.text = "<b>[Click Izq]:</b> Ligero | <b>[Q/E]:</b> Fuerte | <b>[Click Der/G]:</b> Guardia/Parry | <b>[Shift]:</b> Sprint | <b>[Espacio]:</b> Salto | <b>[C/Ctrl]:</b> Agachado";

            // Enlazar campos en la instancia si se proporciona
            if (hudInstance != null)
            {
                hudInstance.hudCanvas = canvas;
                hudInstance.healthBarFill = imgHFill;
                hudInstance.healthText = txtHealth;
                hudInstance.structureBarFill = imgSFill;
                hudInstance.structureText = txtStruct;
                hudInstance.ageText = txtAge;
                hudInstance.talismanText = txtTalisman;
                hudInstance.finisherPromptPanel = finisherPanel;
                hudInstance.finisherPromptTitle = txtFTitle;
            }

            return canvas;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                string[] osFonts = Font.GetOSInstalledFontNames();
                if (osFonts != null && osFonts.Length > 0)
                {
                    font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                }
            }
            return font;
        }

        private static Sprite GetDefaultWhiteSprite()
        {
            if (defaultWhiteSprite == null)
            {
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.DontSave;
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                defaultWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                defaultWhiteSprite.hideFlags = HideFlags.DontSave;
            }
            return defaultWhiteSprite;
        }
    }
}
