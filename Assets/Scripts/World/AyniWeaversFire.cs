using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;
using Ayni.World.Fauna;

namespace Ayni.World
{
    /// <summary>
    /// Las tejedoras atrapadas (Kancha de los Tejedores, K2). Una de las casas arde desde el principio del nivel: su
    /// columna de humo se ve desde el camino. Los Saqueadores atrancaron la puerta con las tejedoras dentro. Cuando Yari
    /// entra en la kancha empieza la cuenta atrás: hay que romper la tranca manteniendo [V] / [LT] junto a la puerta
    /// (si golpean a Yari, pierde la mitad de lo avanzado) antes de que el fuego llegue a los telares.
    ///  - A tiempo: las tejedoras le dan un chumpi (faja tejida) y el veneno de Amaru le dura la mitad.
    ///  - Tarde: las tejedoras escapan por detrás, pero el fuego se lleva los telares (sin recompensa).
    /// Todo se construye por código sobre la aldea ya hecha. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniWeaversFire : MonoBehaviour
    {
        [Tooltip("Segundos desde que Yari entra en la kancha hasta que el fuego alcanza los telares.")]
        [SerializeField] private float timeLimit = 55f;
        [SerializeField] private float holdSeconds = 1.6f;
        [SerializeField] private float interactRange = 2.0f;
        [Tooltip("Duración del veneno de Amaru con el chumpi (1 = sin cambio).")]
        [SerializeField] private float chumpiPoison = 0.5f;

        /// <summary>Corre la cuenta atrás y las tejedoras siguen dentro.</summary>
        public static bool Urgent { get; private set; }
        public static float TimeLeft { get; private set; }

        private static AyniWeaversFire instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Urgent = false;
            TimeLeft = 0f;
            instance = null;
        }

        private AyniEncounterSite site;
        private YariCombatController yari;
        private Vector3 door, inward, roof;
        private Bounds house;
        private GameObject root, barricade;
        private readonly List<AyniFireLight> flames = new List<AyniFireLight>();
        private ParticleSystem smoke;
        private bool started, resolved, toldHowTo;
        private float deadline, progress, lastHealth;

        private void OnEnable() => instance = this;

        private void OnDisable()
        {
            if (instance == this) instance = null;
            Urgent = false;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && s.Order == 2) site = s;
            if (site == null || !ChooseHouse()) return;
            Build();
            Debug.Log($"[Ayni Tejedoras] Casa en llamas en {site.SiteName}: puerta en {door} · techo en {roof}");
        }

        // ───────────────────────── La casa ─────────────────────────

        /// <summary>
        /// La casa que arde: la de un punto interior sin rivales cerca. El punto interior mira hacia su puerta; la puerta
        /// está en el borde de la colisión de la casa, medido en los ejes de la propia casa (no en la caja del mundo, que
        /// en una casa girada es mucho más grande).
        /// </summary>
        private bool ChooseHouse()
        {
            float best = float.MinValue;
            bool found = false;
            foreach (Transform point in site.InteriorPoints)
            {
                if (point == null) continue;
                Vector3 p = point.position + Vector3.up * 1f;
                MeshCollider houseCollider = null;
                float smallest = float.MaxValue;
                foreach (Collider c in Physics.OverlapSphere(p, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    var mc = c as MeshCollider;
                    if (mc == null || mc.sharedMesh == null || c.name != "Colision") continue;
                    Bounds lb = mc.sharedMesh.bounds;
                    Vector3 local = mc.transform.InverseTransformPoint(p);
                    if (!lb.Contains(local)) continue;
                    float volume = lb.size.x * lb.size.y * lb.size.z;
                    if (volume < smallest)
                    {
                        smallest = volume;
                        houseCollider = mc;
                    }
                }

                float score = 100f;
                foreach (GameObject rival in site.Rivals)
                {
                    if (rival != null) score = Mathf.Min(score, Vector3.Distance(rival.transform.position, point.position));
                }
                if (houseCollider == null) score -= 50f;
                Debug.Log($"[Ayni Tejedoras] Punto {point.name} en {point.position}: casa {(houseCollider != null ? (houseCollider.transform.parent != null ? houseCollider.transform.parent.name : houseCollider.name) : "ninguna")} · rival más cercano a {score:F1} m");
                if (score <= best) continue;

                best = score;
                found = true;
                Vector3 forward = point.forward;
                forward.y = 0f;
                forward.Normalize();
                inward = -forward;
                if (houseCollider != null)
                {
                    Transform h = houseCollider.transform;
                    Bounds lb = houseCollider.sharedMesh.bounds;
                    Vector3 local = h.InverseTransformPoint(point.position);
                    Vector3 dir = h.InverseTransformDirection(forward);
                    float exit = ExitDistance(local, dir, lb);
                    door = h.TransformPoint(local + dir * (exit + 0.12f / Mathf.Max(0.01f, h.lossyScale.x)));
                    roof = h.TransformPoint(new Vector3(lb.center.x, lb.max.y, lb.center.z));
                    house = new Bounds(h.TransformPoint(lb.center), Vector3.Scale(lb.size, h.lossyScale));
                    houseAlong = h.TransformDirection(lb.size.x >= lb.size.z ? Vector3.right : Vector3.forward).normalized;
                    houseLength = Mathf.Max(lb.size.x * h.lossyScale.x, lb.size.z * h.lossyScale.z);
                }
                else
                {
                    house = new Bounds(point.position + Vector3.up * 1.2f, new Vector3(4f, 2.6f, 4f));
                    door = point.position + forward * 2.2f;
                    roof = point.position + Vector3.up * 2.5f;
                    houseAlong = Vector3.Cross(Vector3.up, forward);
                    houseLength = 4f;
                }
            }
            if (found && Physics.Raycast(door + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                door.y = hit.point.y;
            }
            return found;
        }

        private Vector3 houseAlong;
        private float houseLength;

        /// <summary>Distancia desde <paramref name="p"/> (dentro de la caja) hasta su borde, siguiendo <paramref name="dir"/>.</summary>
        private static float ExitDistance(Vector3 p, Vector3 dir, Bounds b)
        {
            float t = float.MaxValue;
            if (Mathf.Abs(dir.x) > 0.001f) t = Mathf.Min(t, ((dir.x > 0f ? b.max.x : b.min.x) - p.x) / dir.x);
            if (Mathf.Abs(dir.z) > 0.001f) t = Mathf.Min(t, ((dir.z > 0f ? b.max.z : b.min.z) - p.z) / dir.z);
            return t == float.MaxValue ? 2f : Mathf.Max(0.3f, t);
        }

        private void Build()
        {
            root = new GameObject("Casa_En_Llamas");
            root.transform.position = roof;

            // Llamas sobre el techo (tres focos a lo largo de la casa) y una columna de humo que se ve desde el camino
            for (int i = -1; i <= 1; i += 2)
            {
                var go = new GameObject("Luz_Fuego_" + (i + 2));
                go.transform.SetParent(root.transform, false);
                go.transform.position = roof + houseAlong * (i * houseLength * 0.25f) + Vector3.up * 0.4f;
                var fire = go.AddComponent<AyniFireLight>();
                fire.Configure(4f, 13f, true, false);
                flames.Add(fire);
            }
            fireParticles = BuildFlames(root.transform, roof, houseAlong, houseLength);
            smoke = BuildSmoke(root.transform);

            // La tranca: dos troncos cruzados y uno atravesado delante de la puerta
            barricade = new GameObject("Tranca");
            barricade.transform.SetParent(root.transform, false);
            barricade.transform.SetPositionAndRotation(door, Quaternion.LookRotation(-inward));
            Material wood = AyniFaunaPrimitives.Mat(new Color(0.3f, 0.2f, 0.12f), 0.05f);
            Material rope = AyniFaunaPrimitives.Mat(new Color(0.62f, 0.52f, 0.36f), 0.05f);
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Tronco_1", barricade.transform, wood, new Vector3(0f, 0.95f, 0f), new Vector3(0f, 0f, 38f), new Vector3(0.16f, 1.1f, 0.16f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Tronco_2", barricade.transform, wood, new Vector3(0f, 0.95f, 0.06f), new Vector3(0f, 0f, -38f), new Vector3(0.16f, 1.1f, 0.16f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Travesano", barricade.transform, wood, new Vector3(0f, 1.25f, -0.06f), new Vector3(0f, 0f, 90f), new Vector3(0.12f, 0.9f, 0.12f));
            AyniFaunaPrimitives.Primitive(PrimitiveType.Sphere, "Nudo", barricade.transform, rope, new Vector3(0f, 0.95f, -0.1f), Vector3.zero, new Vector3(0.2f, 0.2f, 0.16f));
            var block = barricade.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, 1f, 0f);
            block.size = new Vector3(1.6f, 2f, 0.3f);
        }

        private ParticleSystem fireParticles;

        /// <summary>Llamas a lo largo del techo de paja: lenguas de fuego amarillas que suben, se vuelven rojas y se apagan.</summary>
        private static ParticleSystem BuildFlames(Transform parent, Vector3 roofTop, Vector3 along, float length)
        {
            var go = new GameObject("Llamas_Techo");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(roofTop - Vector3.up * 0.35f, Quaternion.LookRotation(along));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f, 0.95f), new Color(1f, 0.55f, 0.15f, 0.95f));
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.2f, 0.4f, Mathf.Max(2f, length * 0.8f));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.1f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SmokeMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private static ParticleSystem BuildSmoke(Transform parent)
        {
            var go = new GameObject("Humo");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.4f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.11f, 0.1f, 0.8f), new Color(0.25f, 0.23f, 0.21f, 0.7f));
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 18f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.3f, 0.8f); // el viento de la cordillera lo inclina
            velocity.y = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SmokeMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private static Material smokeMaterial;

        private static Material SmokeMaterial()
        {
            if (smokeMaterial != null) return smokeMaterial;
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
            smokeMaterial = new Material(Ayni.Combat.CombatFeedback.SpriteMaterial) { mainTexture = tex };
            return smokeMaterial;
        }

        // ───────────────────────── La cuenta atrás ─────────────────────────

        private bool Blocked()
        {
            return yari == null || yari.IsDead || yari.IsGameOver || yari.IsBeingRescued || AyniGameState.InputLocked ||
                   AyniGameState.CinematicPlaying || Ayni.Story.AyniJudgment.Active || AyniDeathAging.Active;
        }

        private void Update()
        {
            if (root == null || resolved || yari == null) return;
            float dist = Flat(yari.transform.position, door);

            if (!started && (site.PlayerEntered || dist < 22f) && !Blocked())
            {
                started = true;
                Urgent = true;
                deadline = Time.time + timeLimit;
                AyniAudio.Play2D("titulo_golpe", 0.5f, 1.1f);
                AyniScreenFX.Caption($"¡Las tejedoras están atrapadas en la casa en llamas! Rompe la tranca de la puerta: mantén [{AyniInput.Label(AyniInput.Action.Interact)}].", 4.5f);
            }
            if (!started) return;

            TimeLeft = Mathf.Max(0f, deadline - Time.time);
            if (TimeLeft <= 0f)
            {
                Fail();
                return;
            }

            float health = yari.CurrentHealth;
            bool hurt = health < lastHealth - 0.01f;
            lastHealth = health;
            if (dist > interactRange || Blocked())
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
                return;
            }
            if (!toldHowTo)
            {
                toldHowTo = true;
                AyniAudio.Play("ui_mover", door + Vector3.up, 0.6f);
            }
            if (hurt && progress > 0f) progress *= 0.5f;
            if (AyniInput.Held(AyniInput.Action.Interact))
            {
                progress += Time.deltaTime / Mathf.Max(0.1f, holdSeconds);
                if (progress >= 1f) Rescue();
            }
            else
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
            }
        }

        private void Rescue()
        {
            resolved = true;
            Urgent = false;
            progress = 0f;
            AyniAudio.Play("jaula_rota", door + Vector3.up, 1f, 0.05f, 0.85f);
            StartCoroutine(BreakBarricade());
            StartCoroutine(PutOut());
            AyniPathRewards.Grant(AyniPathRewards.Reward.Chumpi);
            Ayni.Enemy.AmaruHunter.PoisonSecondsMultiplier = chumpiPoison;
            StartCoroutine(Thanks());
            Debug.Log($"[Ayni Tejedoras] Rescatadas con {TimeLeft:F0} s de sobra");
        }

        private IEnumerator Thanks()
        {
            AyniScreenFX.Caption("TEJEDORA: «¡Gracias, guardián! Toma este chumpi: lo tejimos con lana de vicuña para el Inca.»", 4.2f);
            yield return new WaitForSeconds(4.6f);
            AyniAudio.Play2D("illa_brillo", 0.5f, 1.25f);
            AyniScreenFX.Caption("El chumpi te protege: el veneno de los dardos de Amaru te durará la mitad.", 3.8f);
        }

        private void Fail()
        {
            resolved = true;
            Urgent = false;
            AyniPathRewards.Grant(AyniPathRewards.Reward.WeaversLost);
            foreach (AyniFireLight fire in flames) if (fire != null) fire.Configure(6f, 16f, true, false);
            if (fireParticles != null)
            {
                var emission = fireParticles.emission;
                emission.rateOverTime = 130f;
            }
            AyniAudio.Play2D("titulo_golpe", 0.55f, 0.7f);
            AyniScreenFX.Caption("El fuego devoró los telares. Las tejedoras escaparon por detrás, pero lo perdieron todo.", 4.5f);
            Debug.Log("[Ayni Tejedoras] Se acabó el tiempo");
        }

        private IEnumerator BreakBarricade()
        {
            if (barricade == null) yield break;
            var block = barricade.GetComponent<Collider>();
            if (block != null) block.enabled = false;
            var parts = new List<Transform>();
            foreach (Transform child in barricade.transform) parts.Add(child);
            var start = new List<Vector3>();
            var startRot = new List<Quaternion>();
            foreach (Transform p in parts)
            {
                start.Add(p.localPosition);
                startRot.Add(p.localRotation);
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    float side = i % 2 == 0 ? 1f : -1f;
                    parts[i].localPosition = start[i] + new Vector3(side * 0.7f * t, -0.8f * t * t, -0.6f * t);
                    parts[i].localRotation = startRot[i] * Quaternion.Euler(0f, 0f, side * 70f * t);
                }
                yield return null;
            }
        }

        /// <summary>Con la puerta abierta, el fuego se apaga poco a poco (queda el humo un rato).</summary>
        private IEnumerator PutOut()
        {
            yield return new WaitForSeconds(1.5f);
            if (fireParticles != null) fireParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 3f)
            {
                foreach (AyniFireLight fire in flames)
                {
                    if (fire == null) continue;
                    var light = fire.GetComponent<Light>();
                    if (light != null) light.intensity *= 0.97f;
                }
                yield return null;
            }
            foreach (AyniFireLight fire in flames) if (fire != null) fire.gameObject.SetActive(false);
            yield return new WaitForSeconds(4f);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnGUI()
        {
            if (!started || resolved || yari == null || Blocked() || Ayni.Story.AyniBossIntro.Running) return;
            float dist = Flat(yari.transform.position, door);
            if (dist <= interactRange) AyniHoldPrompt.Draw(door + Vector3.up * 2.3f, "romper la tranca", progress);
            else AyniHoldPrompt.Marker(door + Vector3.up * 2.4f, new Color(1f, 0.55f, 0.2f), $"Tejedoras · {Mathf.CeilToInt(TimeLeft)} s");
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Pone a Yari delante de la tranca, mirándola.</summary>
        public static void TestGoToDoor()
        {
            if (instance == null || instance.yari == null || instance.root == null) return;
            Transform y = instance.yari.transform;
            var mover = y.GetComponent<CharacterController>();
            if (mover != null) mover.enabled = false;
            Vector3 stand = instance.door - instance.inward * 1.1f;
            y.SetPositionAndRotation(stand + Vector3.up * 0.1f, Quaternion.LookRotation(instance.inward));
            if (mover != null) mover.enabled = true;
            var cam = Camera.main != null ? Camera.main.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
            if (cam != null) cam.SnapBehindTarget();
            Debug.Log($"[Ayni Tejedoras] Yari ante la tranca en {instance.door}");
        }

        public static void TestRescue()
        {
            if (instance != null && !instance.resolved && instance.root != null) instance.Rescue();
        }

        /// <summary>Deja la cuenta atrás en unos segundos.</summary>
        public static void TestTimeLeft(float seconds)
        {
            if (instance == null || instance.resolved) return;
            instance.started = true;
            Urgent = true;
            instance.deadline = Time.time + seconds;
        }

        public static void Report()
        {
            Debug.Log($"[Ayni Tejedoras] Cuenta atrás: {Urgent} · quedan {TimeLeft:F0} s · chumpi {AyniPathRewards.ChumpiReceived} · perdidas {AyniPathRewards.WeaversLost}" +
                      (instance != null ? $" · puerta {instance.door}" : ""));
        }
    }
}
