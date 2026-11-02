using System.Collections;
using UnityEngine;
using Ayni.Core;

namespace Ayni.UI
{
    /// <summary>
    /// La Illa cobra sus años (estilo Sifu): cuando Yari cae, la acción se ralentiza, la pantalla se apaga, la cámara
    /// rodea despacio su cuerpo y el medallón de la Illa aparece grande en el centro y cuenta, año a año, la edad que
    /// tendrá al levantarse. Si cambia de etapa (joven → maduro → anciano) lo dice; si la Illa se rompe, también.
    /// Al terminar, el medallón baja a su sitio en el HUD y la cámara de juego vuelve detrás de Yari.
    /// La lanza YariCombatController al morir; dura <see cref="Duration"/> segundos reales.
    /// </summary>
    public class AyniDeathAging : MonoBehaviour
    {
        public const float Duration = 2.9f;

        /// <summary>La cuenta está en pantalla (el HUD no dibuja sus avisos mientras tanto).</summary>
        public static bool Active { get; private set; }

        private int fromAge, toAge;
        private bool breaks;
        private Transform yari;
        private float start;
        private int shownAge;
        private float tickFlash;
        private string stageNote;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = false;
        }

        public static void Play(Transform yari, int fromAge, int toAge, bool breaks)
        {
            var go = new GameObject("Ayni_CuentaDeLaIlla");
            var fx = go.AddComponent<AyniDeathAging>();
            fx.yari = yari;
            fx.fromAge = fromAge;
            fx.toAge = toAge;
            fx.breaks = breaks;
            fx.shownAge = fromAge;
            fx.stageNote = StageNote(fromAge, toAge, breaks);
            fx.StartCoroutine(fx.Run());
        }

        private static string StageNote(int from, int to, bool breaks)
        {
            if (breaks) return "La Illa no puede sostener más años";
            if (from <= 35 && to > 35) return "Yari madura: golpea más fuerte, pero resiste menos";
            if (from <= 55 && to > 55) return "Yari envejece: su puño es de piedra, su cuerpo frágil";
            return null;
        }

        private IEnumerator Run()
        {
            Active = true;
            start = Time.unscaledTime;

            // Cámara lenta mientras cae y la pantalla se apaga en tonos de piedra
            Time.timeScale = 0.35f;
            AyniScreenFX.Tint(new Color(0.05f, 0.035f, 0.03f), 0.55f);
            AyniScreenFX.Letterbox(true);
            AyniAudio.Play2D("titulo_golpe", 0.55f, 0.7f);

            // La cámara deja de seguir a Yari y lo rodea despacio desde un lado, bajando hacia el suelo
            Camera cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
            if (follow != null) follow.enabled = false;
            Vector3 camStart = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 body = yari != null ? yari.position + Vector3.up * 0.4f : camStart;
            Vector3 side = cam != null ? Vector3.Cross(Vector3.up, (body - camStart).normalized) : Vector3.right;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;

            // Los años se cuentan a partir de 0.9 s; cada uno, un golpe de tambor (como mucho 1.4 s en total)
            int years = Mathf.Max(1, toAge - fromAge);
            float tickEvery = Mathf.Min(0.32f, 1.4f / years);
            float nextTick = 0.9f;

            while (Time.unscaledTime - start < Duration)
            {
                float t = Time.unscaledTime - start;
                if (t > 0.8f && Time.timeScale < 0.99f) Time.timeScale = 1f;

                if (cam != null && yari != null)
                {
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Duration));
                    // Plano bajo y lateral, casi a ras de suelo: Yari tendido, la cámara a la altura de su rostro
                    Vector3 orbit = body + (Quaternion.AngleAxis(Mathf.Lerp(-20f, 25f, u), Vector3.up) * side) * Mathf.Lerp(3.2f, 2.3f, u) +
                                    Vector3.up * Mathf.Lerp(1.5f, 0.75f, u);
                    cam.transform.position = Vector3.Lerp(camStart, orbit, Mathf.Clamp01(t / 0.6f));
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(body - cam.transform.position), 6f * Time.unscaledDeltaTime);
                }

                if (t >= nextTick && shownAge < toAge)
                {
                    shownAge++;
                    nextTick += tickEvery;
                    tickFlash = 1f;
                    AyniAudio.Play2D("titulo_golpe", 0.45f, 1.15f + 0.04f * (shownAge - fromAge));
                    if (shownAge == toAge) AyniAudio.Play2D(breaks ? "postura_rota" : "illa_brillo", 0.85f);
                }
                tickFlash = Mathf.MoveTowards(tickFlash, 0f, 3.5f * Time.unscaledDeltaTime);
                yield return null;
            }

            Time.timeScale = 1f;
            AyniScreenFX.Tint(Color.clear, 0f);
            if (!breaks) AyniScreenFX.Letterbox(false);
            if (follow != null)
            {
                follow.enabled = true;
                follow.SnapBehindTarget();
            }
            Active = false;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Active)
            {
                Active = false;
                if (Time.timeScale < 0.99f) Time.timeScale = 1f;
            }
        }

        private void OnGUI()
        {
            if (!Active) return;
            GUI.depth = -20; // por encima del HUD
            float t = Time.unscaledTime - start;
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.45f));
            // Al final el medallón baja hasta su sitio en el HUD (abajo a la izquierda)
            float leave = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (Duration - 0.55f)) / 0.5f));
            if (breaks) leave = 0f;

            float size = Mathf.Lerp(Mathf.Lerp(120f, 168f, appear), 80f, leave);
            Vector2 centre = Vector2.Lerp(new Vector2(Screen.width / 2f, Screen.height * 0.42f),
                                          new Vector2(26f + 40f, Screen.height - 26f - 40f), leave);
            float pulse = 1f + 0.08f * tickFlash;
            var medal = new Rect(centre.x - size * pulse / 2f, centre.y - size * pulse / 2f, size * pulse, size * pulse);
            float alpha = appear;

            AyniInkaUI.Medallion(medal, shownAge.ToString(), "AÑOS", 0.4f + 0.6f * tickFlash, alpha);

            float textAlpha = alpha * (1f - leave);
            if (textAlpha <= 0.01f) return;
            int lost = toAge - fromAge;
            var title = AyniInkaUI.Style(20, FontStyle.Bold, TextAnchor.MiddleCenter);
            var sub = AyniInkaUI.Style(14, FontStyle.Normal, TextAnchor.MiddleCenter);
            float y = centre.y + size / 2f + 18f;
            if (breaks)
            {
                AyniInkaUI.Label(new Rect(0f, y, Screen.width, 26f), AyniInkaUI.Spaced("La Illa se ha roto"), title, new Color(1f, 0.42f, 0.32f), textAlpha);
            }
            else
            {
                AyniInkaUI.Label(new Rect(0f, y, Screen.width, 26f),
                    AyniInkaUI.Spaced(lost == 1 ? "La Illa cobra un año" : $"La Illa cobra {lost} años"), title, AyniInkaUI.Gold, textAlpha);
                AyniInkaUI.Label(new Rect(0f, y + 28f, Screen.width, 20f), $"Próxima caída: +{lost + 1}", sub, AyniInkaUI.Bone, textAlpha * 0.85f);
            }
            if (!string.IsNullOrEmpty(stageNote))
            {
                AyniInkaUI.Label(new Rect(0f, y + 52f, Screen.width, 20f), stageNote, sub, AyniInkaUI.Jade, textAlpha);
            }
            AyniInkaUI.TocapuBand(new Rect(Screen.width / 2f - 120f, y - 8f, 240f, 6f),
                                  new Color(AyniInkaUI.Gold.r, AyniInkaUI.Gold.g, AyniInkaUI.Gold.b, 0.5f * textAlpha));
        }
    }
}
