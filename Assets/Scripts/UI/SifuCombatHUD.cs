using UnityEngine;
using Ayni.Core;
using Ayni.Combat;
using Ayni.Enemy;

namespace Ayni.UI
{
    /// <summary>
    /// HUD de combate con dibujo instantáneo en pantalla (OnGUI) para probar de inmediato:
    /// Muestra edad, contador de muerte de la Illa, barra de postura y comandos de remate/Ayni.
    /// </summary>
    public class SifuCombatHUD : MonoBehaviour
    {
        private IllaTalismanSystem talisman;
        private StructureSystem playerStructure;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                talisman = player.GetComponent<IllaTalismanSystem>();
                playerStructure = player.GetComponent<StructureSystem>();
            }
        }

        private void OnGUI()
        {
            GUI.skin.box.fontSize = 14;
            GUI.skin.label.fontSize = 14;

            // Panel Superior Izquierdo: Talismán / Edad / Estructura de Yari
            GUILayout.BeginArea(new Rect(20, 20, 320, 160), GUI.skin.box);
            GUILayout.Label("<b>AYNI: EL RETORNO DEL GUARDIÁN</b>");
            
            if (talisman != null)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"<b>EDAD DE YARI:</b> {talisman.CurrentAge} años  |  Etapa: {talisman.GetCurrentStage()}");
                GUILayout.Label($"<b>ILLA SAGRADA:</b> +{talisman.DeathCounter} años por caída");
                GUI.color = Color.white;
            }

            if (playerStructure != null)
            {
                GUILayout.Space(5);
                GUILayout.Label($"Estructura / Postura de Yari: {playerStructure.CurrentStructure:F0} / {playerStructure.MaxStructure:F0}");
                Rect barRect = GUILayoutUtility.GetRect(280, 16);
                GUI.Box(barRect, "");
                float fill = Mathf.Clamp01(playerStructure.StructureRatio);
                GUI.color = fill > 0.8f ? Color.red : Color.cyan;
                GUI.Box(new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height), "");
                GUI.color = Color.white;
            }
            GUILayout.EndArea();

            // Panel Inferior: Controles de Combate Rumi Maki
            GUILayout.BeginArea(new Rect(20, Screen.height - 130, 480, 110), GUI.skin.box);
            GUILayout.Label("<b>CONTROLES DE COMBATE RUMI MAKI (ESTILO SIFU):</b>");
            GUILayout.Label("• <b>[Click Izq]:</b> Golpe Ligero  |  <b>[Q]:</b> Golpe Fuerte");
            GUILayout.Label("• <b>[Click Der / LShift]:</b> Bloquear / Preparar Parry");
            GUILayout.Label("• <b>[LShift + S]:</b> Esquiva Abajo (Duck)  |  <b>[LShift + W]:</b> Esquiva Arriba (Jump)");
            GUILayout.Label("• <b>Estructura Rota del Rival:</b> [F] Venganza (Matar)  |  [X] Restaurar Ayni (Perdonar)");
            GUILayout.EndArea();

            // Mostrar aviso central si hay un rival con postura rota
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (!enemy.IsDead && enemy.Structure.IsBroken)
                {
                    GUI.color = Color.red;
                    GUI.skin.box.fontSize = 20;
                    GUI.Box(new Rect(Screen.width / 2 - 250, Screen.height / 2 - 80, 500, 70), 
                        $"¡POSTURA DE {enemy.CharacterName.ToUpper()} ROTA!\n[F] Golpe Letal (Venganza)  |  [X] Desarme y Perdón (Ayni)");
                    GUI.color = Color.white;
                    GUI.skin.box.fontSize = 14;
                    break;
                }
            }
        }
    }
}
