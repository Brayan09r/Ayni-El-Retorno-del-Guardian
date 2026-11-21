using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;
using Ayni.World;

namespace Ayni.Story
{
    /// <summary>
    /// Amaru caza a Yari por el camino. Antes de llegar al Puesto de Chasquis y tras liberar cada una de las tres
    /// primeras kanchas, el Cazador aparece en lo alto de un cerro por delante, mira a Yari, le habla y, al irse de un
    /// salto, le clava un dardo de aviso a los pies (no hace daño). Así el jefe no es solo una barra al final del
    /// camino: el jugador sabe desde el principio que alguien lo acecha.
    ///
    /// La figura es una copia del modelo de Amaru sin su IA ni sus colisiones (el jefe de verdad sigue esperando en el
    /// puente). Un rombo rojo con su nombre lo señala en pantalla, o en el borde si queda fuera de la vista.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniHunterStalker : MonoBehaviour
    {
        /// <summary>Lo que dice Amaru en cada aparición (0 = antes del Puesto de Chasquis; 1-3 = tras liberar K1-K3).</summary>
        private static readonly string[] Lines =
        {
            "Corre, guardián. A mis presas siempre les dejo correr un poco.",
            "Mis Rastreadores eran lentos. Los Saqueadores de los telares no lo serán.",
            "¿Oyes el silencio del bosque? Así suena el Antisuyo cuando yo cazo.",
            "Ya casi llegas al puente, Yari. Allí cayó tu orden. Allí caerás tú.",
        };

        [Tooltip("Segundos que Amaru se queda mirando antes de irse.")]
        [SerializeField] private float watchSeconds = 5.5f;
        [Tooltip("Si Yari se acerca a menos de esto, Amaru se va antes.")]
        [SerializeField] private float leaveDistance = 11f;
        [Tooltip("Metros que Yari camina desde el inicio antes de la primera aparición.")]
        [SerializeField] private float firstAppearanceWalk = 35f;

        /// <summary>Amaru está ahora mismo a la vista en el camino.</summary>
        public static bool Showing { get; private set; }
        /// <summary>Apariciones ya hechas (para las sondas de prueba).</summary>
        public static int Appearances { get; private set; }
        /// <summary>Dónde se ha asomado la última vez.</summary>
        public static Vector3 LastPerch { get; private set; }

        private static AyniHunterStalker instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Showing = false;
            Appearances = 0;
            LastPerch = Vector3.zero;
            instance = null;
        }

        private EnemyController boss;
        private YariCombatController yari;
        private GameObject figure;   // copia visual de Amaru (inactiva mientras no se asoma)
        private Animator figureAnim;
        private readonly bool[] done = new bool[Lines.Length];
        private Vector3? startPosition;
        private GUIStyle markerStyle;

        private void OnEnable()
        {
            instance = this;
            AyniEncounterSite.OnCleared += HandleCleared;
        }

        private void OnDisable()
        {
            AyniEncounterSite.OnCleared -= HandleCleared;
            if (instance == this) instance = null;
            Showing = false;
        }

        private void Start()
        {
            foreach (EnemyController e in EnemyController.All)
            {
                if (e != null && e.IsBoss && (boss == null || e.CharacterName.Contains("Amaru"))) boss = e;
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
        }

        private void OnDestroy()
        {
            if (figure != null) Destroy(figure);
        }

        private void HandleCleared(AyniEncounterSite site)
        {
            if (site == null || site.Order < 1 || site.Order >= Lines.Length) return;
            StartCoroutine(AppearAfter(site.Order, 3.4f)); // después del aviso de "liberado"
        }

        private IEnumerator AppearAfter(int index, float delay)
        {
            yield return new WaitForSeconds(delay);
            // Si sigue la pelea (patrullas, refuerzos rezagados), espera un poco a que se calme
            for (float waited = 0f; waited < 12f && FightNearby(); waited += 0.5f) yield return new WaitForSeconds(0.5f);
            TryAppear(index);
        }

        private void Update()
        {
            if (yari == null || done[0] || Showing) return;
            if (AyniGameState.InputLocked || AyniGameState.CinematicPlaying) return;
            // La primera vez: cuando Yari ya echó a andar desde el inicio y aún no llegó al Puesto de Chasquis
            if (startPosition == null)
            {
                startPosition = yari.transform.position;
                return;
            }
            AyniEncounterSite first = SiteByOrder(1);
            if (first == null || first.PlayerEntered)
            {
                done[0] = true;
                return;
            }
            if (Flat(yari.transform.position, startPosition.Value) >= firstAppearanceWalk && !FightNearby()) TryAppear(0);
        }

        // ───────────────────────── La aparición ─────────────────────────

        /// <summary>Lanza la aparición número <paramref name="index"/> si no se hizo ya y tiene sentido ahora.</summary>
        private void TryAppear(int index)
        {
            if (index < 0 || index >= done.Length || done[index] || Showing) return;
            if (boss == null || yari == null || boss.IsDead || yari.IsDead) return;
            // Con la presentación del jefe o el duelo en marcha ya no hace falta
            if (!boss.Dormant || AyniBossIntro.Running) return;
            done[index] = true;

            Vector3 goal = GoalAfter(index);
            if (!FindPerch(goal, out Vector3 perch))
            {
                // Sin un cerro a la vista: solo su voz, desde lejos
                AyniBossIntro.Say(Lines[index], 4f);
                Debug.Log($"[Ayni Acecho] Aparición {index}: sin sitio a la vista, solo la voz");
                return;
            }
            StartCoroutine(Appearance(index, perch));
        }

        /// <summary>Hacia dónde va Yari después de esta aparición: la siguiente kancha o el puente.</summary>
        private Vector3 GoalAfter(int index)
        {
            AyniEncounterSite next = SiteByOrder(index + 1);
            if (next != null) return next.Entry != null ? next.Entry.position : next.transform.position;
            return boss.transform.position;
        }

        private IEnumerator Appearance(int index, Vector3 perch)
        {
            if (!EnsureFigure())
            {
                AyniBossIntro.Say(Lines[index], 4f);
                yield break;
            }
            Showing = true;
            Appearances++;
            LastPerch = perch;
            Debug.Log($"[Ayni Acecho] Aparición {index}: Amaru en ({perch.x:F0}, {perch.y:F1}, {perch.z:F0}), a {Flat(perch, yari.transform.position):F0} m de Yari");

            Transform t = figure.transform;
            t.position = perch;
            t.rotation = Quaternion.LookRotation(FlatDir(perch, yari.transform.position));
            figure.SetActive(true);
            Play("Idle", 0f);

            AyniAudio.Play2D("postura_rota", 0.4f, 0.7f);
            yield return new WaitForSeconds(0.35f);
            Play("Phase_Roar", 0.15f);
            AyniBossIntro.Say(Lines[index], 4.2f);

            // Mira a Yari mientras habla; si Yari se le echa encima, se va antes
            float until = Time.time + watchSeconds;
            bool roarEnded = false;
            while (Time.time < until && yari != null && !yari.IsDead)
            {
                if (!roarEnded && Time.time > until - watchSeconds + 2.6f)
                {
                    roarEnded = true;
                    Play("Idle", 0.4f);
                }
                Vector3 look = FlatDir(t.position, yari.transform.position);
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(look), 140f * Time.deltaTime);
                if (Flat(t.position, yari.transform.position) < leaveDistance) break;
                if (!boss.Dormant) break; // empezó el duelo de verdad
                yield return null;
            }

            // Se va: dardo de aviso a los pies de Yari y salto hacia atrás, entre una nube de polvo
            if (yari != null && !yari.IsDead) AyniWarningDart.Shoot(t.position + Vector3.up * 1.45f, yari.transform);
            Play(HasState("Hunter_Leap") ? "Hunter_Leap" : "Avoid_Jump", 0.1f);
            Vector3 from = t.position;
            Vector3 away = -FlatDir(t.position, yari != null ? yari.transform.position : t.position + t.forward);
            Vector3 to = from + away * 3.5f + Vector3.up * 1.6f;
            for (float u = 0f; u < 1f; u += Time.deltaTime / 0.55f)
            {
                float k = Mathf.Clamp01(u);
                t.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                yield return null;
            }
            AyniDustPuff.Burst(t.position + Vector3.up * 0.9f);
            AyniAudio.Play("aterrizaje", t.position, 0.6f);
            figure.SetActive(false);
            Showing = false;

            if (index == 0) AyniScreenFX.Caption("Amaru te está cazando. Sigue el Qhapaq Ñan.", 3.2f);
        }

        /// <summary>
        /// Busca un sitio alto en el cerro, por delante de Yari (hacia <paramref name="goal"/>), que se vea desde donde
        /// está y desde la cámara: entre 16 y 34 m, sobre terreno (no tejados ni patios) y no muy empinado.
        /// </summary>
        private bool FindPerch(Vector3 goal, out Vector3 perch)
        {
            perch = Vector3.zero;
            Vector3 origin = yari.transform.position;
            Vector3 toGoal = FlatDir(origin, goal);
            Camera cam = Camera.main;
            Vector3 eye = origin + Vector3.up * 1.6f;
            float best = float.MinValue;

            // Se busca alrededor de hacia dónde mira la cámara (para que el jugador lo vea sin buscarlo) y alrededor
            // del camino que le queda (para que esté "por delante")
            var directions = new List<Vector3>();
            if (cam != null)
            {
                Vector3 view = cam.transform.forward;
                view.y = 0f;
                if (view.sqrMagnitude > 0.01f)
                {
                    view.Normalize();
                    for (float a = -28f; a <= 28f; a += 7f) directions.Add(Quaternion.Euler(0f, a, 0f) * view);
                }
            }
            for (float a = -55f; a <= 55f; a += 11f) directions.Add(Quaternion.Euler(0f, a, 0f) * toGoal);

            foreach (Vector3 dir in directions)
            {
                for (float d = 16f; d <= 34f; d += 3f)
                {
                    Vector3 xz = origin + dir * d;
                    if (!Physics.Raycast(new Vector3(xz.x, origin.y + 80f, xz.z), Vector3.down, out RaycastHit hit, 200f,
                                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (!(hit.collider is TerrainCollider) || hit.normal.y < 0.62f) continue;
                    float rise = hit.point.y - origin.y;
                    if (rise < 0.8f || rise > 16f) continue; // en alto, pero no en la cima de un Apu
                    if (InsideAnySite(hit.point)) continue;

                    Vector3 head = hit.point + Vector3.up * 1.7f;
                    if (Physics.Linecast(eye + dir * 0.6f, head, out RaycastHit block, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        (block.point - head).sqrMagnitude > 0.8f) continue;

                    float ahead = Vector3.Dot(dir, toGoal); // 1 = justo en el camino que queda
                    float score = Mathf.Min(rise, 7f) + ahead * 2f - Mathf.Abs(d - 22f) / 6f;
                    if (cam != null)
                    {
                        Vector3 v = cam.WorldToViewportPoint(head);
                        if (v.z > 0f && v.x > 0.12f && v.x < 0.88f && v.y > 0.15f && v.y < 0.9f) score += 8f;
                    }
                    if (score > best)
                    {
                        best = score;
                        perch = hit.point + Vector3.up * 0.02f;
                    }
                }
            }
            return best > float.MinValue;
        }

        private static bool InsideAnySite(Vector3 point)
        {
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Contains(point)) return true;
            return false;
        }

        private bool FightNearby()
        {
            if (yari == null) return false;
            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null || e.IsDead || e.Dormant || e.IsBoss || !e.isActiveAndEnabled) continue;
                if (e.State == EnemyState.Idle) continue;
                if ((e.transform.position - yari.transform.position).sqrMagnitude < 22f * 22f) return true;
            }
            return false;
        }

        // ───────────────────────── La figura ─────────────────────────

        /// <summary>Copia del modelo de Amaru sin scripts ni colisiones (ver <see cref="AyniVisualClone"/>).</summary>
        private bool EnsureFigure()
        {
            if (figure != null) return true;
            if (boss == null) return false;
            figure = AyniVisualClone.Create(boss.gameObject, "Amaru_Silueta");
            figureAnim = AyniVisualClone.AnimatorOf(figure);
            return figure != null;
        }

        private bool HasState(string state) => figureAnim != null && figureAnim.HasState(0, Animator.StringToHash(state));

        private void Play(string state, float fade) => AyniVisualClone.Play(figureAnim, state, fade);

        // ───────────────────────── Marca en pantalla ─────────────────────────

        private void OnGUI()
        {
            if (!Showing || figure == null || !figure.activeSelf) return;
            GUI.depth = -20; // por encima del HUD y del panel de objetivos
            if (AyniGameState.CinematicPlaying || AyniJudgment.Active || AyniBossIntro.Running) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 world = figure.transform.position + Vector3.up * 2.3f;
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            if (behind) screen = -screen;
            var p = new Vector2(screen.x, Screen.height - screen.y);
            // Zona libre para la marca: ni el panel de objetivos (arriba) ni la vida de Yari (abajo)
            const float side = 40f, top = 110f, bottom = 130f;
            bool offscreen = behind || p.x < side || p.x > Screen.width - side || p.y < top || p.y > Screen.height - bottom;
            if (offscreen)
            {
                Vector2 c = new Vector2(Screen.width / 2f, (top + Screen.height - bottom) / 2f);
                Vector2 d = p - c;
                if (behind) d.y = Mathf.Abs(d.y);
                if (d.sqrMagnitude < 1f) d = Vector2.up;
                float sx = (Screen.width / 2f - side) / Mathf.Max(Mathf.Abs(d.x), 0.001f);
                float sy = ((Screen.height - bottom - top) / 2f) / Mathf.Max(Mathf.Abs(d.y), 0.001f);
                p = c + d * Mathf.Min(sx, sy);
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float size = Mathf.Lerp(12f, 16f, pulse);
            var rect = new Rect(p.x - size / 2f, p.y - size / 2f, size, size);
            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, rect.center);
            AyniInkaUI.Fill(new Rect(rect.x - 2f, rect.y - 2f, size + 4f, size + 4f), new Color(0f, 0f, 0f, 0.6f));
            AyniInkaUI.Fill(rect, new Color(AyniInkaUI.Grana.r + 0.2f, AyniInkaUI.Grana.g, AyniInkaUI.Grana.b, 0.95f));
            GUI.matrix = prev;

            if (markerStyle == null) markerStyle = AyniInkaUI.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter);
            AyniInkaUI.Label(new Rect(p.x - 60f, p.y - size - 14f, 120f, 16f), AyniInkaUI.Spaced("Amaru"), markerStyle,
                             new Color(1f, 0.55f, 0.45f), 0.9f);
        }

        // ───────────────────────── Ayudas ─────────────────────────

        private static AyniEncounterSite SiteByOrder(int order)
        {
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == order) return s;
            return null;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 FlatDir(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Fuerza una aparición (0 a 3) aunque ya se hiciera. En Play: <c>call Ayni.Story.AyniHunterStalker.Test 1</c>.</summary>
        public static void Test(int index)
        {
            if (instance == null)
            {
                Debug.LogWarning("[Ayni Acecho] No hay AyniHunterStalker en la escena (¿estás en Play?)");
                return;
            }
            index = Mathf.Clamp(index, 0, Lines.Length - 1);
            instance.done[index] = false;
            instance.TryAppear(index);
        }

        /// <summary>Estado del acecho en la consola.</summary>
        public static void Report()
        {
            string doneList = instance != null ? string.Join(",", instance.done) : "-";
            Debug.Log($"[Ayni Acecho] Visible: {Showing} · apariciones: {Appearances} · hechas: [{doneList}] · último sitio: {LastPerch}");
        }
    }

    /// <summary>Dardo que no hace daño: vuela hasta el suelo, a un paso de Yari, y se queda clavado unos segundos.</summary>
    internal class AyniWarningDart : MonoBehaviour
    {
        private static Material shaftMaterial;
        private Vector3 velocity, target;
        private bool stuck;
        private float stuckAt;

        public static void Shoot(Vector3 origin, Transform yari)
        {
            Vector3 toYari = yari.position - origin;
            toYari.y = 0f;
            toYari = toYari.sqrMagnitude > 0.0001f ? toYari.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toYari);
            Vector3 spot = yari.position + side * (UnityEngine.Random.value < 0.5f ? 1.1f : -1.1f) - toYari * 0.6f;
            if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                spot = hit.point;
            }
            var go = new GameObject("Dardo_Aviso");
            go.transform.position = origin;
            var dart = go.AddComponent<AyniWarningDart>();
            dart.target = spot;
            dart.velocity = (spot - origin).normalized * 38f;
            go.transform.rotation = Quaternion.LookRotation(dart.velocity);
            dart.BuildVisual();
            AyniAudio.Play("dardo_lanzado", origin, 0.9f);
        }

        private void BuildVisual()
        {
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(transform, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            shaft.transform.localScale = new Vector3(0.025f, 0.22f, 0.025f);
            if (shaftMaterial == null) shaftMaterial = new Material(CombatFeedback.SpriteMaterial) { color = new Color(0.2f, 0.45f, 0.12f) };
            shaft.GetComponent<Renderer>().sharedMaterial = shaftMaterial;

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = CombatFeedback.SpriteMaterial;
            trail.time = 0.2f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.06f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(AmaruHunter.PoisonGreen, 0f), new GradientColorKey(new Color(0.1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (stuck)
            {
                if (Time.time - stuckAt > 8f) Destroy(gameObject);
                return;
            }
            Vector3 step = velocity * Time.deltaTime;
            Vector3 left = target - transform.position;
            if (left.sqrMagnitude <= step.sqrMagnitude || Vector3.Dot(left, velocity) <= 0f)
            {
                transform.position = target;
                stuck = true;
                stuckAt = Time.time;
                AyniAudio.Play("dardo_impacto", target, 0.9f);
                CombatFeedback.Flash(target + Vector3.up * 0.1f, AmaruHunter.PoisonGreen, 0.6f, 0.16f);
                AyniDustPuff.Burst(target, 0.5f);
                return;
            }
            transform.position += step;
        }
    }

    /// <summary>Nube de polvo breve (partículas con el material de destellos del combate).</summary>
    internal static class AyniDustPuff
    {
        private static Material material;

        public static void Burst(Vector3 position, float scale = 1f)
        {
            var go = new GameObject("Polvo_Acecho");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 2.2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.2f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.47f, 0.38f, 0.55f), new Color(0.38f, 0.33f, 0.28f, 0.45f));
            main.gravityModifier = -0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(26 * scale + 6)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f * scale;
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        private static Material GetMaterial()
        {
            if (material != null) return material;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            material = new Material(CombatFeedback.SpriteMaterial) { mainTexture = tex };
            return material;
        }
    }
}
