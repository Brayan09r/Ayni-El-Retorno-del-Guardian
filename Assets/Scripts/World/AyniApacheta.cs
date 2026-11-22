using System.Collections;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.Story;
using Ayni.UI;
using Ayni.World.Fauna;

namespace Ayni.World
{
    /// <summary>
    /// Apacheta del camino, poco después de la salida del Tambo: el montículo de piedras donde los caminantes andinos
    /// dejan una piedra al Apu, el espíritu de la montaña, para que los cuide en el viaje.
    ///  - Yari deja su piedra manteniendo <c>V</c> / <c>LT</c>.
    ///  - <b>Bendición del Apu:</b> vida llena y su próxima caída no le cobra años (la Illa lo levanta igual).
    ///  - Si ejecutó al Capitán del Tambo, la apacheta no acepta la ofrenda: la piedra rueda cuesta abajo.
    ///  - Si lo perdonó, el capitán está allí, que ya dejó la suya.
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniApacheta : MonoBehaviour
    {
        /// <summary>Hay apacheta en el nivel.</summary>
        public static bool Exists { get; private set; }
        /// <summary>Yari ya dejó su piedra (aceptada o no).</summary>
        public static bool Offered { get; private set; }
        /// <summary>Dónde está (base del montículo).</summary>
        public static Vector3 Position { get; private set; }

        [SerializeField] private float interactRange = 2.6f;
        [SerializeField] private float holdSeconds = 1f;

        private static AyniApacheta instance;
        private YariCombatController yari;
        private IllaTalismanSystem talisman;
        private Transform cairn;
        private float topY;
        private float progress;
        private bool figurePlaced, figureSpoke;
        private Vector3 side;
        private static readonly Color MarkerColor = new Color(0.85f, 0.75f, 0.55f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Exists = false;
            Offered = false;
            Position = Vector3.zero;
            instance = null;
        }

        private void OnEnable() => instance = this;

        private void OnDestroy()
        {
            if (talisman != null) talisman.OnProtectedFall -= HandleProtectedFall;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                yari = player.GetComponent<YariCombatController>();
                talisman = player.GetComponent<IllaTalismanSystem>();
                if (talisman != null) talisman.OnProtectedFall += HandleProtectedFall;
            }
            if (!FindSpot(out Vector3 spot, out Vector3 along)) return;
            Position = spot;
            side = Vector3.Cross(Vector3.up, along).normalized;
            Build(spot);
            Exists = true;
            Debug.Log($"[Ayni Apacheta] En {spot}");
        }

        // ───────────────────────── Dónde ─────────────────────────

        /// <summary>
        /// Al borde del camino, entre 9 y 24 m después de la salida del Tambo hacia la Portada: suelo casi llano, sin
        /// muros, casas ni rocas encima y a una altura parecida a la del camino (para no ponerla en un barranco).
        /// </summary>
        private static bool FindSpot(out Vector3 spot, out Vector3 along)
        {
            spot = Vector3.zero;
            along = Vector3.forward;
            AyniEncounterSite tambo = null, gate = null;
            foreach (AyniEncounterSite s in AyniEncounterSite.All)
            {
                if (s == null) continue;
                if (s.Order == 3) tambo = s;
                if (s.Order == 4) gate = s;
            }
            if (tambo == null) return false;

            Vector3 from = tambo.Exit != null ? tambo.Exit.position : tambo.transform.position + tambo.transform.forward * 12f;
            Vector3 to = gate != null ? (gate.Entry != null ? gate.Entry.position : gate.transform.position) : from + (from - tambo.transform.position);
            along = to - from;
            along.y = 0f;
            if (along.sqrMagnitude < 0.01f) along = tambo.transform.forward;
            along.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, along);

            float[] distances = { 12f, 9f, 15f, 18f, 21f, 24f };
            float[] offsets = { 3.6f, -3.6f, 4.6f, -4.6f, 2.8f, -2.8f };
            foreach (float d in distances)
            {
                Vector3 road = from + along * d;
                if (!Ground(road, out Vector3 roadGround, out _)) continue;
                foreach (float o in offsets)
                {
                    Vector3 p = roadGround + right * o;
                    if (!Ground(p, out Vector3 g, out Vector3 normal)) continue;
                    if (normal.y < 0.88f || Mathf.Abs(g.y - roadGround.y) > 1.2f) continue;
                    if (tambo.Contains(g) || (gate != null && gate.Contains(g))) continue;
                    if (Blocked(g)) continue;
                    spot = g;
                    return true;
                }
            }
            Debug.LogWarning("[Ayni Apacheta] No encontré un sitio libre junto al camino");
            return false;
        }

        private static bool Ground(Vector3 p, out Vector3 point, out Vector3 normal)
        {
            point = p;
            normal = Vector3.up;
            if (!Physics.Raycast(p + Vector3.up * 12f, Vector3.down, out RaycastHit hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        /// <summary>¿Hay algo (muro, casa, roca, tronco) donde iría el montículo?</summary>
        private static bool Blocked(Vector3 g)
        {
            // Desde 20 cm sobre el suelo: el propio suelo (terreno o losas del camino) no cuenta
            foreach (Collider c in Physics.OverlapCapsule(g + Vector3.up * 1.2f, g + Vector3.up * 2.2f, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (c is TerrainCollider) continue;
                if (c.GetComponentInParent<EnemyController>() != null || c.GetComponentInParent<YariCombatController>() != null) continue;
                return true;
            }
            return false;
        }

        // ───────────────────────── El montículo ─────────────────────────

        private void Build(Vector3 spot)
        {
            var root = new GameObject("Apacheta").transform;
            root.position = spot;
            cairn = root;
            var rng = new System.Random(3303);

            Color[] tones =
            {
                new Color(0.4f, 0.38f, 0.35f), new Color(0.5f, 0.47f, 0.42f), new Color(0.31f, 0.29f, 0.27f),
                new Color(0.47f, 0.4f, 0.32f), new Color(0.57f, 0.55f, 0.5f), new Color(0.36f, 0.33f, 0.28f),
            };
            var mats = new Material[tones.Length];
            for (int i = 0; i < tones.Length; i++) mats[i] = AyniFaunaPrimitives.Mat(tones[i], 0.02f);

            // Anillos de piedras, cada vez más pequeños, hasta la punta
            const int layers = 6;
            for (int layer = 0; layer < layers; layer++)
            {
                float k = layer / (float)layers;
                float radius = 0.85f * (1f - k) + 0.05f;
                float y = layer * 0.19f + 0.08f;
                int count = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * radius / 0.34f));
                for (int i = 0; i < count; i++)
                {
                    float a = (i + (float)rng.NextDouble() * 0.5f) / count * Mathf.PI * 2f;
                    float size = Mathf.Lerp(0.36f, 0.24f, k) * (0.8f + 0.4f * (float)rng.NextDouble());
                    Vector3 pos = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                    Stone(root, mats[rng.Next(mats.Length)], pos, size, (float)rng.NextDouble() * 360f, rng);
                }
            }
            topY = layers * 0.19f + 0.08f;
            Stone(root, mats[1], new Vector3(0f, topY, 0f), 0.24f, 30f, rng);

            // Vara con lanas de colores, como las que se atan en las apachetas
            Material wood = AyniFaunaPrimitives.Mat(new Color(0.5f, 0.37f, 0.22f), 0.05f);
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Vara", root, wood, new Vector3(0.04f, topY + 0.32f, 0f), new Vector3(0f, 0f, 5f), new Vector3(0.035f, 0.36f, 0.035f));
            Color[] wool = { new Color(0.85f, 0.12f, 0.1f), new Color(0.98f, 0.78f, 0.12f), new Color(0.12f, 0.6f, 0.35f), new Color(0.6f, 0.2f, 0.65f) };
            for (int i = 0; i < wool.Length; i++)
            {
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cube, "Lana_" + i, root, AyniFaunaPrimitives.Mat(wool[i], 0.05f),
                    new Vector3(0.07f + i * 0.012f, topY + 0.55f - i * 0.04f, 0.015f * i), new Vector3(0f, i * 40f, 14f + i * 7f),
                    new Vector3(0.025f, 0.3f, 0.014f));
            }

            // Que no se pueda atravesar
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.65f, 0f);
            col.radius = 0.85f;
            col.height = 1.4f;
        }

        /// <summary>
        /// Una piedra: casi siempre una laja angulosa (cubo aplastado y torcido), a veces un canto rodado. Así el montículo
        /// se ve de piedras de cerro amontonadas, no de huevos.
        /// </summary>
        private static Transform Stone(Transform parent, Material mat, Vector3 pos, float size, float yaw, System.Random rng = null)
        {
            float r() => rng != null ? (float)rng.NextDouble() : 0.5f;
            bool slab = rng == null || r() < 0.72f;
            Vector3 euler = new Vector3((r() - 0.5f) * 24f, yaw, (r() - 0.5f) * 24f);
            Vector3 scale = slab
                ? new Vector3(size * (1f + 0.3f * r()), size * (0.38f + 0.2f * r()), size * (0.7f + 0.3f * r()))
                : new Vector3(size, size * 0.6f, size * 0.82f);
            return AyniFaunaPrimitives.Primitive(slab ? PrimitiveType.Cube : PrimitiveType.Sphere, "Piedra", parent, mat, pos, euler, scale).transform;
        }

        // ───────────────────────── Ofrenda ─────────────────────────

        private void Update()
        {
            if (!Exists || yari == null) return;
            float dist = Flat(yari.transform.position, Position);
            PlaceFigure(dist);

            if (Offered || !CanInteract(dist))
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
                return;
            }
            if (AyniInput.Held(AyniInput.Action.Interact))
            {
                progress += Time.deltaTime / Mathf.Max(0.1f, holdSeconds);
                if (progress >= 1f) StartCoroutine(Offer());
            }
            else
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f);
            }
        }

        private bool CanInteract(float dist)
        {
            if (dist > interactRange) return false;
            if (AyniGameState.InputLocked || AyniGameState.CinematicPlaying || AyniJudgment.Active) return false;
            if (yari.IsBeingRescued || yari.IsGameOver) return false;
            // No en plena pelea
            foreach (EnemyController e in EnemyController.All)
            {
                if (e == null || e.IsDead || e.Dormant || e.ExternalControl) continue;
                if (Flat(e.transform.position, yari.transform.position) < 12f) return false;
            }
            return true;
        }

        private IEnumerator Offer()
        {
            Offered = true;
            progress = 0f;
            bool accepted = !AyniPathRewards.CaptainSlain;
            AyniScreenFX.Caption("YARI: «Apu, cuida mi camino.»", 2f);

            // La piedra va de la mano de Yari a la punta del montículo
            Material mat = AyniFaunaPrimitives.Mat(new Color(0.6f, 0.58f, 0.53f), 0.05f);
            Transform stone = Stone(cairn, mat, Vector3.zero, 0.22f, 15f);
            Vector3 start = yari.transform.position + Vector3.up * 1.2f + yari.transform.forward * 0.3f;
            Vector3 top = Position + Vector3.up * (topY + 0.14f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.55f)
            {
                stone.position = Vector3.Lerp(start, top, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f;
                yield return null;
            }
            stone.position = top;
            AyniAudio.Play("aterrizaje", top, 0.35f, 0.05f, 1.6f);
            yield return new WaitForSeconds(0.35f);
            // Que Yari termine su oración antes de la respuesta del Apu
            for (float waited = 0f; AyniVoice.Speaking && waited < 4f; waited += Time.unscaledDeltaTime) yield return null;

            if (accepted)
            {
                AyniPathRewards.Grant(AyniPathRewards.Reward.ApuBlessing);
                if (talisman != null) talisman.GrantFallProtection();
                yari.Heal(9999f);
                AyniAudio.Play2D("illa_brillo", 0.8f);
                CombatFeedback.Flash(top + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.6f), 2.2f, 0.5f);
                AyniScreenFX.Title("BENDICIÓN DEL APU", "Tu próxima caída no te costará años.", 3.4f);
                Debug.Log("[Ayni Apacheta] Ofrenda aceptada: bendición del Apu");
            }
            else
            {
                // Rueda cuesta abajo y cae al pie del montículo
                AyniPathRewards.Grant(AyniPathRewards.Reward.ApuRejected);
                Vector3 away = yari.transform.position - Position;
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : side;
                Vector3 rest = Position + away * 1.25f + Vector3.up * 0.1f;
                Vector3 from = stone.position;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
                {
                    stone.position = Vector3.Lerp(from, rest, t) + Vector3.up * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * 0.12f * (1f - t);
                    stone.Rotate(Vector3.Cross(Vector3.up, away), 540f * Time.deltaTime, Space.World);
                    yield return null;
                }
                AyniAudio.Play("aterrizaje", rest, 0.3f, 0.05f, 0.8f);
                AyniScreenFX.Caption("La piedra rueda cuesta abajo. La apacheta no acepta la ofrenda de unas manos manchadas de Hucha.", 4.8f);
                Debug.Log("[Ayni Apacheta] Ofrenda rechazada (capitán ejecutado)");
            }
        }

        private void HandleProtectedFall()
        {
            AyniScreenFX.Caption("El Apu te levanta: esta vez la Illa no te cobra años.", 3.6f);
        }

        // ───────────────────────── El capitán, si lo perdonaste ─────────────────────────

        private void PlaceFigure(float dist)
        {
            GameObject figure = AyniTamboCaptain.Figure;
            if (!AyniPathRewards.CaptainSpared || figure == null) return;
            if (!figurePlaced && dist < 70f && !AyniTamboCaptain.Talking &&
                (AyniTamboCaptain.Captain == null || !AyniTamboCaptain.Captain.gameObject.activeInHierarchy))
            {
                figurePlaced = true;
                // Arrodillado ante el montículo, del lado por el que llega Yari (ofrece su piedra al Apu)
                Vector3 toYari = yari.transform.position - Position;
                toYari.y = 0f;
                Vector3 dir = toYari.sqrMagnitude > 0.01f ? toYari.normalized : side;
                Vector3 spot = Position + (dir + Vector3.Cross(Vector3.up, dir) * 0.8f).normalized * 1.55f;
                if (Physics.Raycast(spot + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    spot = hit.point;
                }
                Vector3 toCairn = Position - spot;
                toCairn.y = 0f;
                figure.transform.position = spot;
                figure.transform.rotation = Quaternion.LookRotation(toCairn.sqrMagnitude > 0.01f ? toCairn : Vector3.forward);
                figure.SetActive(true);
                Animator anim = AyniVisualClone.AnimatorOf(figure);
                if (anim != null && anim.HasState(0, Animator.StringToHash("MercyKneel"))) anim.Play("MercyKneel", 0, 1f); // ya arrodillado
                else AyniVisualClone.Play(anim, "Idle", 0f);
            }
            if (figurePlaced && !figureSpoke && dist < 9f)
            {
                figureSpoke = true;
                AyniScreenFX.Caption("CAPITÁN DEL TAMBO: «Mi piedra ya está puesta. Pon la tuya, guardián: el Apu cuida a quien camina con Ayni.»", 5f);
                AyniVoice.Reply(this, "YARI: «Gracias, capitán. Que el Apu te acompañe.»", 5f, 3f);
            }
        }

        // ───────────────────────── Dibujo ─────────────────────────

        private void OnGUI()
        {
            if (!Exists || Offered || yari == null) return;
            if (AyniGameState.CinematicPlaying || AyniGameState.InputLocked || AyniJudgment.Active || AyniBossIntro.Running || AyniDeathAging.Active) return;
            if (yari.IsBeingRescued || yari.IsGameOver) return;
            float dist = Flat(yari.transform.position, Position);
            Vector3 above = Position + Vector3.up * (topY + 0.9f);
            if (CanInteract(dist)) AyniHoldPrompt.Draw(above, "dejar una piedra", progress);
            else if (dist < 45f && dist > interactRange) AyniHoldPrompt.Marker(above, MarkerColor, "Apacheta");
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>Pone a Yari junto a la apacheta, mirándola.</summary>
        public static void TestGoTo()
        {
            if (instance == null || !Exists || instance.yari == null) { Debug.LogWarning("[Ayni Apacheta] No hay apacheta (¿estás en Play?)"); return; }
            Vector3 toward = Position - instance.yari.transform.position;
            toward.y = 0f;
            Vector3 dir = toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward;
            Vector3 p = Position - dir * 2.1f;
            var cc = instance.yari.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // que el rayo no dé en el propio Yari
            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = hit.point;
            instance.yari.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.LookRotation(dir));
            if (cc != null) cc.enabled = true;
            Debug.Log($"[Ayni Apacheta] Yari junto a la apacheta ({p})");
        }

        /// <summary>Deja la piedra como si Yari la dejara.</summary>
        public static void TestOffer()
        {
            if (instance == null || !Exists || Offered) { Debug.LogWarning("[Ayni Apacheta] No hay apacheta o ya se dejó la piedra"); return; }
            instance.StartCoroutine(instance.Offer());
        }

        /// <summary>Estado en la consola.</summary>
        public static void Report()
        {
            int protectedFalls = instance != null && instance.talisman != null ? instance.talisman.ProtectedFalls : 0;
            Debug.Log($"[Ayni Apacheta] Existe {Exists} en {Position} · ofrenda {Offered} · bendición {AyniPathRewards.ApuBlessing} · " +
                      $"rechazo {AyniPathRewards.ApuRejected} · caídas protegidas {protectedFalls} · capitán junto a ella {instance != null && instance.figurePlaced}");
        }
    }
}
