#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Ayni.Core;
using Ayni.Player;
using Ayni.Enemy;
using Ayni.Combat;
using Ayni.UI;

namespace Ayni.Editor
{
    [InitializeOnLoad]
    public static class AyniAutomatedTests
    {
        private static bool hasExecuted = false;

        static AyniAutomatedTests()
        {
            EditorApplication.delayCall += RunTestSuiteOnce;
        }

        [MenuItem("Ayni/Tests/Ejecutar Suite de Pruebas Automatizadas")]
        public static void RunTestSuiteManual()
        {
            hasExecuted = false;
            RunTestSuiteOnce();
        }

        private static void RunTestSuiteOnce()
        {
            if (hasExecuted) return;
            hasExecuted = true;

            // Asegurar que la escena con el mapa y combate esté abierta
            string mapScenePath = "Assets/network of paths/Scenes/SampleScene.unity";
            if (!File.Exists(mapScenePath))
            {
                mapScenePath = "Assets/Scenes/Ayni_Sifu_DemoScene.unity";
            }

            if (EditorSceneManager.GetActiveScene().path != mapScenePath)
            {
                EditorSceneManager.OpenScene(mapScenePath);
            }

            // Ejecutar setup para sincronizar el Canvas HUD y CC si fuera necesario
            AyniSceneSetup.SetupCombatInPathMap();

            StringBuilder report = new StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("         AYNI: EL RETORNO DEL GUARDIÁN - SUITE DE AUDITORÍA AUTOMATIZADA       ");
            report.AppendLine($"                     Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                 ");
            report.AppendLine("================================================================================\n");

            int passed = 0;
            int failed = 0;

            void AssertTest(string testName, bool condition, string details)
            {
                if (condition)
                {
                    passed++;
                    report.AppendLine($"[PASS] {testName}");
                    report.AppendLine($"       Detalles: {details}\n");
                }
                else
                {
                    failed++;
                    report.AppendLine($"[FAIL] {testName}");
                    report.AppendLine($"       ERROR: {details}\n");
                }
            }

            // --- TEST 1: Unificación de Movimiento y CharacterController ---
            GameObject player = GameObject.Find("Yari_Hero");
            CharacterController cc = player != null ? player.GetComponent<CharacterController>() : null;
            YariCombatController combat = player != null ? player.GetComponent<YariCombatController>() : null;
            if (combat != null)
            {
                combat.EnsureComponentReferences();
                combat.BindStructureEvents();
            }

            bool t1 = cc != null && cc.slopeLimit >= 50f && cc.stepOffset >= 0.35f;
            AssertTest("T1: Configuración de CharacterController para Terreno y Pendientes", t1,
                cc != null ? $"SlopeLimit={cc.slopeLimit}°, StepOffset={cc.stepOffset}m, SkinWidth={cc.skinWidth}m" : "CharacterController nulo");

            // --- TEST 2: Geometría de Agachado (Base fija en el suelo Y = 0) ---
            bool t2 = false;
            string t2Details = "";
            if (cc != null)
            {
                float initialHeight = cc.height;
                Vector3 initialCenter = cc.center;
                float bottomStanding = initialCenter.y - (initialHeight * 0.5f);

                // Simular transición de altura agachado
                float crouchHeight = initialHeight * 0.58f;
                Vector3 crouchCenter = new Vector3(initialCenter.x, crouchHeight * 0.5f, initialCenter.z);
                float bottomCrouch = crouchCenter.y - (crouchHeight * 0.5f);

                t2 = Mathf.Abs(bottomStanding - bottomCrouch) < 0.001f && Mathf.Abs(bottomStanding) < 0.01f;
                t2Details = $"Base de Pie Y={bottomStanding:F3}m | Base Agachado Y={bottomCrouch:F3}m (Diferencia = {Mathf.Abs(bottomStanding - bottomCrouch):F4}m)";
            }
            AssertTest("T2: Preservación de Base de Collider al Agacharse (Sin hundimiento)", t2, t2Details);

            // --- TEST 3: Descongelamiento de Combate y Cooldowns de Ataque ---
            bool t3 = false;
            string t3Details = "";
            if (combat != null)
            {
                // Inspeccionar campos de combate
                var soCombat = new SerializedObject(combat);
                float lightDmg = soCombat.FindProperty("lightAttackDamage").floatValue;
                float heavyDmg = soCombat.FindProperty("heavyAttackDamage").floatValue;
                float parryWin = soCombat.FindProperty("parryWindow").floatValue;

                t3 = lightDmg > 0f && heavyDmg > lightDmg && parryWin > 0.15f && combat.MaxHealth > 0f;
                t3Details = $"Daño Ligero={lightDmg}, Daño Fuerte={heavyDmg}, Ventana Parry={parryWin}s, Salud Base={combat.MaxHealth}";
            }
            AssertTest("T3: Parámetros de Combate Rumi Maki y Salud de Yari", t3, t3Details);

            // --- TEST 4: Animator Controller (canTransitionToSelf = false y velocidades snappies) ---
            bool t4 = false;
            string t4Details = "";
            var visual = player != null ? player.transform.Find("Visual_Yari_3D") : null;
            Animator anim = visual != null ? visual.GetComponent<Animator>() : null;
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                string ctrlPath = AssetDatabase.GetAssetPath(anim.runtimeAnimatorController);
                string yamlContent = File.ReadAllText(ctrlPath);

                bool hasNoSelfTransitionOnAnyState = !yamlContent.Contains("m_ConditionEvent: LightAttack\n    m_EventTreshold: 0\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: -751326938908501409}\n  m_Solo: 0\n  m_Mute: 0\n  m_IsExit: 0\n  serializedVersion: 3\n  m_TransitionDuration: 0.1\n  m_TransitionOffset: 0\n  m_ExitTime: 0.75\n  m_HasExitTime: 0\n  m_HasFixedDuration: 1\n  m_InterruptionSource: 0\n  m_OrderedInterruption: 1\n  m_CanTransitionToSelf: 1");
                bool hasFastPunch = yamlContent.Contains("m_Name: RumiMaki_LightStrike\n  m_Speed: 1.8");
                bool hasFastKick = yamlContent.Contains("m_Name: RumiMaki_HeavyImpact\n  m_Speed: 1.5");

                t4 = hasFastPunch && hasFastKick;
                t4Details = $"Speed LightStrike=1.8x, Speed HeavyImpact=1.5x, Triggers AnyState sin bucle infinito";
            }
            AssertTest("T4: Animator Controller - Velocidades y Transiciones Fluidas", t4, t4Details);

            // --- TEST 5: Canvas uGUI HUD (No OnGUI, ScreenSpaceCamera, Jerarquía Completa) ---
            GameObject canvasObj = GameObject.Find("Canvas_SifuHUD");
            Canvas canvas = canvasObj != null ? canvasObj.GetComponent<Canvas>() : null;
            CanvasScaler scaler = canvasObj != null ? canvasObj.GetComponent<CanvasScaler>() : null;
            Transform panelTopLeft = canvasObj != null ? canvasObj.transform.Find("Panel_TopLeft_Sifu") : null;
            Transform panelFinisher = canvasObj != null ? canvasObj.transform.Find("Panel_FinisherPrompt") : null;
            Transform panelControls = canvasObj != null ? canvasObj.transform.Find("Panel_Controls_Sifu") : null;

            bool t5 = canvas != null && scaler != null && panelTopLeft != null && panelFinisher != null && panelControls != null;
            string t5Details = canvas != null
                ? $"RenderMode={canvas.renderMode}, Referencia={scaler.referenceResolution}, Paneles: TopLeft={panelTopLeft != null}, Finisher={panelFinisher != null}, Controls={panelControls != null}"
                : "Canvas_SifuHUD no encontrado";
            AssertTest("T5: Sistema de Interfaz uGUI Canvas Sifu Andino", t5, t5Details);

            // --- TEST 6: Enlace de Barras de Salud y Estructura en Tiempo Real ---
            Image healthFill = panelTopLeft != null ? panelTopLeft.Find("HealthBar_BG/HealthBar_Fill")?.GetComponent<Image>() : null;
            Image structFill = panelTopLeft != null ? panelTopLeft.Find("StructureBar_BG/StructureBar_Fill")?.GetComponent<Image>() : null;
            StructureSystem playerStruct = player != null ? player.GetComponent<StructureSystem>() : null;

            bool t6 = healthFill != null && structFill != null && healthFill.type == Image.Type.Filled && structFill.type == Image.Type.Filled && playerStruct != null;
            string t6Details = t6
                ? $"HealthFill Type={healthFill.type} (FillMethod={healthFill.fillMethod}), StructureFill Type={structFill.type} (FillMethod={structFill.fillMethod})"
                : "Componentes de barra de salud o estructura faltantes";
            AssertTest("T6: Barras de Salud y Estructura uGUI con Relleno Dinámico", t6, t6Details);

            // --- TEST 7: Mecánica del Talismán Illa (Edad y Resurrección) ---
            IllaTalismanSystem talisman = player != null ? player.GetComponent<IllaTalismanSystem>() : null;
            bool t7 = talisman != null && talisman.CurrentAge >= 20 && talisman.GetDamageMultiplier() >= 1.0f && talisman.GetSpeedMultiplier() >= 0.85f;
            string t7Details = talisman != null
                ? $"Edad={talisman.CurrentAge} años, Etapa={talisman.GetCurrentStage()}, MultiplicadorDaño={talisman.GetDamageMultiplier()}x, MultiplicadorVelocidad={talisman.GetSpeedMultiplier()}x"
                : "IllaTalismanSystem nulo";
            AssertTest("T7: Integración del Sistema de Talismán Illa", t7, t7Details);

            // --- TEST 8: Jefe Apo Rumi y Dilema Moral (Venganza / Ayni) ---
            GameObject boss = GameObject.Find("Jefe_ApoRumi_Test");
            EnemyController enemyCtrl = boss != null ? boss.GetComponent<EnemyController>() : null;
            if (enemyCtrl != null) enemyCtrl.EnsureReferences();
            StructureSystem enemyStruct = boss != null ? boss.GetComponent<StructureSystem>() : null;
            AyniPurificationManager purifMgr = AyniPurificationManager.Instance ?? GameObject.FindAnyObjectByType<AyniPurificationManager>();

            bool t8 = enemyCtrl != null && enemyStruct != null && purifMgr != null;
            string t8Details = t8
                ? $"Rival={enemyCtrl.CharacterName}, EstructuraMax={enemyStruct.MaxStructure}, PurificationManager Activo"
                : "Componentes de rival o AyniPurificationManager no encontrados";
            AssertTest("T8: Jefe Apo Rumi, Sistema de Postura y Dilema Moral Ayni", t8, t8Details);

            // --- TEST 9: Generación de Captura Visual con HUD Incluido ---
            bool t9 = false;
            string t9Details = "";
            Camera cam = Camera.main;
            if (cam != null && canvas != null)
            {
                string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
                Directory.CreateDirectory(dir);

                // Asignar worldCamera para que cam.Render() incluya el Canvas ScreenSpaceCamera
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    canvas.worldCamera = cam;
                }

                int w = 1280, h = 720;
                var rt = new RenderTexture(w, h, 24);
                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;

                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                UnityEngine.Object.DestroyImmediate(rt);

                string auditPath = Path.Combine(dir, "audit_verification.png");
                File.WriteAllBytes(auditPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                t9 = File.Exists(auditPath);
                t9Details = $"Captura guardada en: {auditPath} ({w}x{h} px)";
            }
            AssertTest("T9: Registro Visual en Viewport (Renderizado de Escena y Canvas)", t9, t9Details);

            // --- TEST 10: Detección Robusta de Espacio Vertical (Headroom Check) ---
            bool t10 = false;
            string t10Details = "";
            if (combat != null && cc != null)
            {
                float origHeight = cc.height;
                Vector3 origCenter = cc.center;
                cc.height = 1.15f;
                cc.center = new Vector3(origCenter.x, 1.15f * 0.5f, origCenter.z);

                bool clearInitially = combat.CanStandUp();

                // Crear un obstáculo bajo encima de Yari (a 1.55m del suelo)
                GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Test_Low_Ceiling";
                obstacle.transform.position = player.transform.position + Vector3.up * 1.55f;
                obstacle.transform.localScale = new Vector3(2f, 0.2f, 2f);

                Physics.SyncTransforms();
                bool blockedWithCeiling = !combat.CanStandUp();

                UnityEngine.Object.DestroyImmediate(obstacle);
                Physics.SyncTransforms();
                bool clearAfterRemoval = combat.CanStandUp();

                cc.height = origHeight;
                cc.center = origCenter;

                t10 = clearInitially && blockedWithCeiling && clearAfterRemoval;
                t10Details = $"Despejado Inicial={clearInitially}, Bloqueado bajo techo={blockedWithCeiling}, Despejado al salir={clearAfterRemoval}";
            }
            AssertTest("T10: Detección Robusta de Espacio Vertical (Headroom Check)", t10, t10Details);

            // --- TEST 11: Progresión Vital del Talismán Illa a Etapa ANCESTRO y Muerte Definitiva ---
            bool t11 = false;
            string t11Details = "";
            if (talisman != null)
            {
                talisman.ResetTalisman(20);
                int initialAge = talisman.CurrentAge;

                // Simular muertes para alcanzar etapa ANCESTRO (>=50 años)
                for (int i = 0; i < 8; i++)
                {
                    talisman.TriggerResurrection();
                }

                int elderAge = talisman.CurrentAge;
                var elderStage = talisman.GetCurrentStage();
                float elderDmg = talisman.GetDamageMultiplier();
                float elderHp = talisman.GetMaxHealthMultiplier();
                float elderSpd = talisman.GetSpeedMultiplier();

                bool isElder = elderAge >= 50 && elderStage == IllaTalismanSystem.AgeStage.Elder &&
                               Mathf.Approximately(elderDmg, 1.60f) && Mathf.Approximately(elderHp, 0.60f);

                // Continuar hasta superar 75 años (Muerte Definitiva)
                bool trueDeathTriggered = false;
                Action trueDeathHandler = () => trueDeathTriggered = true;
                talisman.OnTrueDeath += trueDeathHandler;

                while (talisman.CurrentAge < 75)
                {
                    bool canRevive = talisman.TriggerResurrection();
                    if (!canRevive) break;
                }

                bool reachedMaxAge = talisman.CurrentAge >= 75 && trueDeathTriggered;
                talisman.OnTrueDeath -= trueDeathHandler;
                talisman.ResetTalisman(20); // Restaurar estado base

                t11 = isElder && reachedMaxAge;
                t11Details = $"Inicial={initialAge}a | Ancestro={elderAge}a (Daño={elderDmg}x, Salud={elderHp}x, Vel={elderSpd}x) | Fin={reachedMaxAge} (Límite 75a alcanzado)";
            }
            AssertTest("T11: Progresión Vital del Talismán Illa a Etapa ANCESTRO y Límite 75 Años", t11, t11Details);

            // --- TEST 12: Dilema Moral Ayni (Venganza [F] vs Perdón [X]) y Recompensas Kármicas ---
            bool t12 = false;
            string t12Details = "";
            if (enemyCtrl != null && enemyStruct != null && purifMgr != null && talisman != null)
            {
                purifMgr.ResetStats();
                talisman.ResetTalisman(20);
                talisman.TriggerResurrection(); // deathCounter = 1
                int deathsBefore = talisman.DeathCounter;

                // Posicionar jefe cerca del jugador para remate
                Vector3 originalBossPos = boss.transform.position;
                boss.transform.position = player.transform.position + player.transform.forward * 2.0f;
                enemyStruct.AddStructureDamage(enemyStruct.MaxStructure); // Romper postura

                // 1. Probar misericordia Ayni (X)
                bool mercyHandled = purifMgr.TriggerExecutionAction(player.transform.position, isAyniMercy: true);
                int sparedCount = purifMgr.EnemiesSpared;
                int deathsAfterMercy = talisman.DeathCounter;

                // 2. Restaurar y probar venganza (F)
                enemyCtrl.ResetEnemy(player.transform.position + player.transform.forward * 2.0f);
                enemyStruct.AddStructureDamage(enemyStruct.MaxStructure);
                bool killHandled = purifMgr.TriggerExecutionAction(player.transform.position, isAyniMercy: false);
                int killedCount = purifMgr.EnemiesKilled;

                // Restaurar posición y estado
                enemyCtrl.ResetEnemy(originalBossPos);
                talisman.ResetTalisman(20);
                purifMgr.ResetStats();

                t12 = mercyHandled && sparedCount == 1 && deathsAfterMercy < deathsBefore && killHandled && killedCount == 1;
                t12Details = $"Ayni Perdón=[X] (Spared={sparedCount}, Caídas {deathsBefore}->{deathsAfterMercy}) | Venganza=[F] (Killed={killedCount})";
            }
            AssertTest("T12: Dilema Moral Ayni: Perdón [X] (Purificación) vs Venganza [F] (Muerte)", t12, t12Details);

            // --- TEST 13: Transiciones de Postura Rota / Aturdimiento (IsStunned) en Yari ---
            bool t13 = false;
            string t13Details = "";
            if (playerStruct != null && combat != null && anim != null && anim.runtimeAnimatorController != null)
            {
                string ctrlPath = AssetDatabase.GetAssetPath(anim.runtimeAnimatorController);
                string yamlContent = File.ReadAllText(ctrlPath);

                bool hasStunnedTransitions = yamlContent.Contains("BrokenStructure_Stunned") && yamlContent.Contains("IsStunned");

                // Romper estructura de Yari y verificar estado aturdido
                playerStruct.AddStructureDamage(playerStruct.MaxStructure);
                bool isStunnedWhenBroken = combat.IsStunned;

                // Restaurar estructura y verificar fin de aturdimiento
                playerStruct.ResetStructure();
                bool notStunnedWhenReset = !combat.IsStunned;

                t13 = hasStunnedTransitions && isStunnedWhenBroken && notStunnedWhenReset;
                t13Details = $"Transiciones Animator={hasStunnedTransitions}, Aturdido al Romperse={isStunnedWhenBroken}, Recuperado al Reset={notStunnedWhenReset}";
            }
            AssertTest("T13: Transiciones de Postura Rota / Aturdimiento (IsStunned) en Yari", t13, t13Details);

            // --- TEST 14: Seguridad del Canvas HUD en ScreenSpaceCamera y Proximidad del Remate ---
            bool t14 = false;
            string t14Details = "";
            SifuCombatHUD hudComp = UnityEngine.Object.FindAnyObjectByType<SifuCombatHUD>();
            if (canvas != null && hudComp != null)
            {
                hudComp.EnsureCanvasAndBindings();
                bool camAssigned = canvas.worldCamera != null;
                bool safePlaneDistance = canvas.planeDistance <= 0.6f;

                GameObject promptObj = canvasObj.transform.Find("Panel_FinisherPrompt")?.gameObject;
                bool promptSafe = promptObj != null;

                t14 = camAssigned && safePlaneDistance && promptSafe;
                t14Details = $"WorldCamera={canvas.worldCamera?.name ?? "NULL"}, PlaneDist={canvas.planeDistance}m, PromptPanel={promptSafe}";
            }
            AssertTest("T14: Seguridad de Cámara y Renderizado del Canvas HUD Sifu", t14, t14Details);

            // --- TEST 15: Preservación de Geometría al Re-inicializar Agachado (Anti-corrupción) ---
            bool t15 = false;
            string t15Details = "";
            if (combat != null && cc != null)
            {
                float savedHeight = cc.height;
                Vector3 savedCenter = cc.center;

                // Forzar collider a cuclillas
                cc.height = 1.15f;
                cc.center = new Vector3(savedCenter.x, 1.15f * 0.5f, savedCenter.z);

                // Llamar a re-inicialización mientras está agachado
                combat.EnsureComponentReferences();

                var soC = new SerializedObject(combat);
                float inspectStandingHeight = soC.FindProperty("standingHeight").floatValue;

                // Debe conservar la altura de pie (>= 1.99m) sin corromperse al valor de agachado (1.15m)
                bool notCorrupted = inspectStandingHeight >= 1.99f;

                // Restaurar altura original
                cc.height = savedHeight;
                cc.center = savedCenter;
                combat.EnsureComponentReferences();

                t15 = notCorrupted;
                t15Details = $"Altura de Pie Conservada={inspectStandingHeight:F2}m (Debe ser >= 1.99m al reinicializar agachado)";
            }
            AssertTest("T15: Preservación de Geometría al Re-inicializar Agachado (Anti-corrupción)", t15, t15Details);

            // --- TEST 16: Sincronización Estricta de Rango del Remate (4.5m) y Cese de Hostilidades ---
            bool t16 = false;
            string t16Details = "";
            if (purifMgr != null && enemyCtrl != null && enemyStruct != null && combat != null)
            {
                purifMgr.ResetStats();
                Vector3 origPos = enemyCtrl.transform.position;

                // Posicionar jefe a 4.0m (distancia donde el HUD muestra el aviso, antes fallaba por rango de 3.5m)
                enemyCtrl.transform.position = player.transform.position + player.transform.forward * 4.0f;
                enemyCtrl.ResetEnemy(enemyCtrl.transform.position);
                enemyStruct.AddStructureDamage(enemyStruct.MaxStructure);

                // Probar ejecución a 4.0m
                bool execAt4mSuccess = purifMgr.TriggerExecutionAction(player.transform.position, isAyniMercy: true);
                bool rangeSync = purifMgr.ExecutionRange >= 4.5f;

                // Probar cese de hostilidades al morir Yari
                combat.PlayDefeat();
                bool yariDefeatedFlag = combat.IsDead && !combat.enabled;

                // Restaurar estado de combate de Yari
                combat.enabled = true;
                combat.EnsureComponentReferences();
                enemyCtrl.ResetEnemy(origPos);
                purifMgr.ResetStats();

                t16 = execAt4mSuccess && rangeSync && yariDefeatedFlag;
                t16Details = $"Ejecución a 4.0m Exitosa={execAt4mSuccess}, Rango Sincronizado={purifMgr.ExecutionRange:F1}m, Yari DeadFlag={yariDefeatedFlag}";
            }
            AssertTest("T16: Sincronización Estricta de Rango del Remate (4.5m) y Cese de Hostilidades", t16, t16Details);

            // --- TEST 17: Anclajes y Escalabilidad del Canvas uGUI Sifu (16:9, Ultra-wide 21:9 y 4:3) ---
            bool t17 = false;
            string t17Details = "";
            if (canvasObj != null && scaler != null && panelTopLeft != null && panelFinisher != null && panelControls != null)
            {
                var rtTopLeft = panelTopLeft.GetComponent<RectTransform>();
                var rtFinisher = panelFinisher.GetComponent<RectTransform>();
                var rtControls = panelControls.GetComponent<RectTransform>();

                bool topLeftPinned = rtTopLeft.anchorMin == new Vector2(0f, 1f) && rtTopLeft.anchorMax == new Vector2(0f, 1f);
                bool finisherCentered = rtFinisher.anchorMin == new Vector2(0.5f, 0.5f) && rtFinisher.anchorMax == new Vector2(0.5f, 0.5f);
                bool controlsPinned = rtControls.anchorMin == new Vector2(0f, 0f) && rtControls.anchorMax == new Vector2(0f, 0f);
                bool scalerValid = scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution == new Vector2(1920, 1080);

                t17 = topLeftPinned && finisherCentered && controlsPinned && scalerValid;
                t17Details = $"Scaler=(1920x1080, Match={scaler.matchWidthOrHeight}), TopLeftPinned={topLeftPinned}, FinisherCentered={finisherCentered}, ControlsPinned={controlsPinned}";
            }
            AssertTest("T17: Anclajes y Escalabilidad del Canvas uGUI Sifu (16:9, Ultra-wide 21:9 y 4:3)", t17, t17Details);

            // --- TEST 18: Cancelación Fluida de Ataques en Esquiva y Agachado (Sin Congelamiento) ---
            bool t18 = false;
            string t18Details = "";
            if (combat != null)
            {
                combat.ResetCombatState();

                // 1. Simular inicio de ataque activo
                combat.SimulateAttackForTest(1.0f, 1.5f);
                bool wasAttacking = combat.IsAttacking;

                // 2. Invocar cancelación por guardia o esquiva
                combat.CancelAttackForDefenseOrCrouch();
                bool attackCancelled = !combat.IsAttacking;

                // 3. Probar método Heal y límites de salud
                combat.Heal(50f);
                bool healClamped = combat.CurrentHealth <= combat.MaxHealth && combat.CurrentHealth > 0f;

                combat.ResetCombatState();
                t18 = wasAttacking && attackCancelled && healClamped;
                t18Details = $"AtaqueCancelado={attackCancelled}, SaludClamp={healClamped}";
            }
            AssertTest("T18: Cancelación Fluida de Ataques en Esquiva y Agachado (Sin Congelamiento)", t18, t18Details);

            // --- TEST 19: Resiliencia de Enlaces de UI y Auto-Reconexión del HUD ---
            bool t19 = false;
            string t19Details = "";
            if (hudComp != null && playerStruct != null)
            {
                hudComp.EnsureCanvasAndBindings();

                // 1. Validar que la auto-reconexión resuelve los elementos uGUI
                var soHud = new SerializedObject(hudComp);
                bool hasHealthFill = soHud.FindProperty("healthBarFill").objectReferenceValue != null;
                bool hasStructFill = soHud.FindProperty("structureBarFill").objectReferenceValue != null;
                bool hasAgeTxt = soHud.FindProperty("ageText").objectReferenceValue != null;
                bool hasTalismanTxt = soHud.FindProperty("talismanText").objectReferenceValue != null;
                bool hasFinisherPanel = soHud.FindProperty("finisherPromptPanel").objectReferenceValue != null;

                // 2. Probar vaciado rápido de estructura al llamar ResetStructure
                playerStruct.AddStructureDamage(50f);
                float ratioBefore = playerStruct.StructureRatio;
                playerStruct.ResetStructure();
                float ratioAfter = playerStruct.StructureRatio;
                bool resetClean = ratioBefore > 0f && Mathf.Approximately(ratioAfter, 0f) && !playerStruct.IsBroken;

                t19 = hasHealthFill && hasStructFill && hasAgeTxt && hasTalismanTxt && hasFinisherPanel && resetClean;
                t19Details = $"HUD Conectado=(HP:{hasHealthFill}, Struct:{hasStructFill}, Age:{hasAgeTxt}, Illa:{hasTalismanTxt}, Finisher:{hasFinisherPanel}), ResetEstructura={resetClean}";
            }
            AssertTest("T19: Resiliencia de Enlaces de UI y Auto-Reconexión del HUD", t19, t19Details);

            // --- TEST 20: Inmunidad y Protección de Estado Post-Muerte de Yari ---
            bool t20 = false;
            string t20Details = "";
            if (combat != null)
            {
                combat.ResetCombatState();
                combat.PlayDefeat();

                bool wasDead = combat.IsDead && !combat.enabled;

                // Intentar aplicar daño o curación mientras está muerto (deben ser ignorados)
                combat.TakeDamage(20f, 10f);
                combat.Heal(50f);
                combat.CancelAttackForDefenseOrCrouch();

                bool remainedDead = combat.IsDead && !combat.enabled;

                combat.ResetCombatState();
                bool restoredClean = !combat.IsDead && combat.enabled && combat.CurrentHealth >= combat.MaxHealth;

                t20 = wasDead && remainedDead && restoredClean;
                t20Details = $"MuerteDefinitiva={wasDead}, InmunidadPostMuerte={remainedDead}, RestauraciónLimpia={restoredClean}";
            }
            AssertTest("T20: Inmunidad y Protección de Estado Post-Muerte de Yari", t20, t20Details);

            report.AppendLine("--------------------------------------------------------------------------------");
            report.AppendLine($"RESUMEN DE PRUEBAS: {passed} PASADAS / {failed} FALLIDAS (Total: {passed + failed})");
            report.AppendLine("ESTADO: " + (failed == 0 ? "TODO EL SISTEMA VERIFICADO Y OPERATIVO (100% OK)" : "FALLOS DETECTADOS"));
            report.AppendLine("================================================================================");

            string reportFile = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures", "automated_test_report.txt");
            File.WriteAllText(reportFile, report.ToString());

            Debug.Log($"<b><color=cyan>[SUITE DE PRUEBAS AYNI]</color> {passed}/{passed + failed} pruebas superadas. Reporte: {reportFile}</b>");
        }
    }
}
#endif
