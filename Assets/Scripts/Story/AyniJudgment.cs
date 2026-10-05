using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.UI;

namespace Ayni.Story
{
    /// <summary>
    /// El Gran Dilema Moral (Biblia, 6.3): al romperle la postura a un jefe en su fase final, la acción se congela
    /// a cámara lenta y aparecen los dos caminos: botón rojo (Venganza) o botón verde (Ayni).
    /// La decisión la resuelve YariCombatController → AyniPurificationManager; esto solo pone la escena.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniJudgment : MonoBehaviour
    {
        [Tooltip("Velocidad del tiempo mientras se espera la decisión.")]
        [SerializeField] private float judgmentTimeScale = 0.15f;

        /// <summary>True mientras el juego espera la decisión del jugador.</summary>
        public static bool Active { get; private set; }

        private EnemyController target;
        private float startedAt;
        private float nextZoom;
        private GUIStyle titleStyle, buttonStyle, labelStyle, textStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = false;
        }

        private void OnEnable()
        {
            EnemyController.OnJudgmentReady += Begin;
        }

        private void OnDisable()
        {
            EnemyController.OnJudgmentReady -= Begin;
            if (Active) End();
        }

        private void Begin(EnemyController enemy)
        {
            if (Active) return;
            target = enemy;
            Active = true;
            startedAt = Time.unscaledTime;
            nextZoom = 0f;
            AyniScreenFX.Letterbox(true);
            CombatFeedback.Shake(0.08f, 0.25f);
            CombatFeedback.Flash(enemy.transform.position + Vector3.up * 1.5f, new Color(1f, 0.85f, 0.5f), 2.4f, 0.3f);
        }

        private void End()
        {
            Active = false;
            target = null;
            AyniScreenFX.Letterbox(false);
            if (!AyniGameState.InputLocked) Time.timeScale = 1f;
        }

        private void Update()
        {
            if (!Active) return;
            if (target == null || target.IsDead || !target.Structure.IsBroken)
            {
                End();
                return;
            }

            if (!AyniGameState.InputLocked) Time.timeScale = judgmentTimeScale;
            if (Time.unscaledTime >= nextZoom)
            {
                nextZoom = Time.unscaledTime + 1.5f;
                CombatFeedback.FinisherCamera(2f);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter, wordWrap = true, richText = true };
        }

        private void OnGUI()
        {
            if (!Active || target == null || AyniGameState.CinematicPlaying) return;
            EnsureStyles();
            GUI.depth = -60;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2f);
            float w = Screen.width / scale, h = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float appear = Mathf.Clamp01((Time.unscaledTime - startedAt) / 0.4f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

            GUI.color = new Color(1f, 0.85f, 0.45f, appear);
            GUI.Label(new Rect(0, h * 0.2f, w, 44f), "JUICIO AYNI", titleStyle);
            GUI.color = new Color(1f, 1f, 1f, 0.9f * appear);
            GUI.Label(new Rect(w * 0.2f, h * 0.2f + 44f, w * 0.6f, 30f), $"{target.CharacterName} está a tu merced.", textStyle);

            float cx = w * 0.5f, cy = h * 0.62f;
            DrawChoice(new Vector2(cx - 170f, cy), new Color(0.85f, 0.15f, 0.12f), AyniInput.Label(AyniInput.Action.Execute),
                       "VENGANZA", "Lo rematas. La violencia deja\nla tierra marchita (Hucha).", appear, pulse);
            DrawChoice(new Vector2(cx + 170f, cy), new Color(0.2f, 0.7f, 0.3f), AyniInput.Label(AyniInput.Action.Mercy),
                       "AYNI", "Lo desarmas y perdonas.\nEl Antisuyo puede sanar.", appear, 1f - pulse);

            GUI.color = Color.white;
            GUI.matrix = previous;
        }

        private void DrawChoice(Vector2 center, Color color, string key, string title, string text, float alpha, float pulse)
        {
            float r = 46f + 4f * pulse;
            var circle = new Rect(center.x - r, center.y - r, r * 2f, r * 2f);
            GUI.color = new Color(color.r, color.g, color.b, 0.9f * alpha);
            GUI.DrawTexture(circle, CircleTexture());
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(circle, key, buttonStyle);
            GUI.Label(new Rect(center.x - 120f, center.y + r + 6f, 240f, 30f), title, labelStyle);
            GUI.color = new Color(1f, 1f, 1f, 0.85f * alpha);
            GUI.Label(new Rect(center.x - 130f, center.y + r + 38f, 260f, 50f), text, textStyle);
        }

        private static Texture2D circle;

        private static Texture2D CircleTexture()
        {
            if (circle != null) return circle;
            const int size = 64;
            circle = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * size * 0.5f);
                    float ring = d > 0.86f ? 1f : 0.8f;
                    circle.SetPixel(x, y, new Color(ring, ring, ring, a));
                }
            }
            circle.Apply();
            return circle;
        }
    }
}
