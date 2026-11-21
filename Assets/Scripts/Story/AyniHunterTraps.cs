using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Combat;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;
using Ayni.UI;
using Ayni.World;
using Ayni.World.Fauna;

namespace Ayni.Story
{
    /// <summary>
    /// Trampas de Amaru (Biblia 4.3). Si el Rastreador escapó con el quipu (<see cref="AyniPathRewards.AmaruWarned"/>),
    /// Amaru sabe que Yari viene y siembra su plaza de lazos con estacas, tapados con hojas. Se ven si se mira el suelo:
    /// un círculo de hojas con estacas tumbadas alrededor. Al pisar uno, las estacas saltan y el lazo barre los pies:
    /// es un golpe bajo que se esquiva con el saltito (Guardia + arriba) y, si no, hace daño y rompe la guardia.
    /// Cada trampa salta una vez. Al empezar el duelo, Amaru se lo recuerda a Yari. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniHunterTraps : MonoBehaviour
    {
        [SerializeField] private int count = 6;
        [SerializeField] private float damage = 12f;
        [SerializeField] private float structureDamage = 30f;
        [SerializeField] private float triggerRadius = 0.7f;

        private class Trap
        {
            public GameObject root;
            public Transform[] stakes;
            public bool armed = true;
        }

        private readonly List<Trap> traps = new List<Trap>();
        private EnemyController boss;
        private YariCombatController yari;
        private bool placed, lineSaid, bossWasDormant = true;

        /// <summary>Trampas colocadas y armadas (para las sondas de prueba).</summary>
        public static int Armed { get; private set; }
        private static AyniHunterTraps instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Armed = 0;
            instance = null;
        }

        private void OnEnable()
        {
            instance = this;
            AyniPathRewards.OnChanged += HandleReward;
        }

        private void OnDisable()
        {
            AyniPathRewards.OnChanged -= HandleReward;
            if (instance == this) instance = null;
        }

        private void Start()
        {
            foreach (EnemyController e in EnemyController.All)
            {
                if (e != null && e.IsBoss && (boss == null || e.CharacterName.Contains("Amaru"))) boss = e;
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) yari = player.GetComponent<YariCombatController>();
            if (AyniPathRewards.AmaruWarned) Place();
        }

        private void HandleReward(AyniPathRewards.Reward reward)
        {
            if (reward == AyniPathRewards.Reward.AmaruWarned) Place();
        }

        // ───────────────────────── Colocar ─────────────────────────

        private void Place()
        {
            if (placed || boss == null) return;
            placed = true;

            // Centro de la plaza: unos metros por delante de donde espera Amaru, hacia donde llega Yari
            Vector3 b = boss.transform.position;
            Vector3 toArrival = boss.transform.forward;
            AyniEncounterSite last = null;
            foreach (AyniEncounterSite s in AyniEncounterSite.All) if (s != null && (last == null || s.Order > last.Order)) last = s;
            if (last != null && last.Exit != null)
            {
                toArrival = last.Exit.position - b;
                toArrival.y = 0f;
                toArrival = toArrival.sqrMagnitude > 0.01f ? toArrival.normalized : boss.transform.forward;
            }
            Vector3 center = b + toArrival * 7f;
            if (Physics.Raycast(center + Vector3.up * 10f, Vector3.down, out RaycastHit c, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                center = c.point;
            }

            var rng = new System.Random(4300);
            var spots = new List<Vector3>();
            for (int attempt = 0; attempt < 120 && spots.Count < count; attempt++)
            {
                float angle = (float)rng.NextDouble() * 360f;
                float radius = 2.5f + (float)rng.NextDouble() * 6.5f;
                Vector3 p = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 14f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.85f || Mathf.Abs(hit.point.y - center.y) > 1.2f) continue;
                if (hit.collider.GetComponentInParent<EnemyController>() != null) continue;
                if (!SolidAround(hit.point)) continue; // nunca al borde de la garganta: alrededor tiene que haber suelo
                bool crowded = false;
                foreach (Vector3 other in spots) if ((other - hit.point).sqrMagnitude < 2.4f * 2.4f) crowded = true;
                if (crowded || (hit.point - b).sqrMagnitude < 2.5f * 2.5f) continue;
                spots.Add(hit.point);
            }
            foreach (Vector3 spot in spots) traps.Add(Build(spot, rng));
            Armed = traps.Count;
            Debug.Log($"[Ayni Trampas] {traps.Count} trampas en la plaza de Amaru alrededor de {center}");
        }

        /// <summary>Hay suelo a la misma altura en ocho direcciones a 1,6 m (ni bordes de la garganta ni de muros).</summary>
        private static bool SolidAround(Vector3 p)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 q = p + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 1.6f;
                if (!Physics.Raycast(q + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(hit.point.y - p.y) > 0.5f) return false;
            }
            return true;
        }

        private static Trap Build(Vector3 position, System.Random rng)
        {
            var trap = new Trap { root = new GameObject("Trampa_Amaru") };
            trap.root.transform.SetPositionAndRotation(position + Vector3.up * 0.02f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            Material leaves = AyniFaunaPrimitives.Mat(new Color(0.28f, 0.3f, 0.12f), 0.05f);
            Material dry = AyniFaunaPrimitives.Mat(new Color(0.45f, 0.36f, 0.18f), 0.05f);
            Material wood = AyniFaunaPrimitives.Mat(new Color(0.3f, 0.21f, 0.12f), 0.05f);
            Material rope = AyniFaunaPrimitives.Mat(new Color(0.62f, 0.52f, 0.36f), 0.05f);

            // Hojas que tapan el lazo
            AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Hojas", trap.root.transform, leaves, Vector3.zero, Vector3.zero, new Vector3(1.15f, 0.015f, 1.15f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + (float)rng.NextDouble() * 30f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.32f;
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Hoja", trap.root.transform, dry, p + Vector3.up * 0.012f,
                    new Vector3(0f, a, 0f), new Vector3(0.28f, 0.006f, 0.16f));
            }
            // Lazo de soga en el borde
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.56f;
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Lazo", trap.root.transform, rope, p + Vector3.up * 0.02f,
                    new Vector3(90f, a + 90f, 0f), new Vector3(0.025f, 0.15f, 0.025f));
            }
            // Estacas tumbadas hacia fuera; al saltar la trampa se levantan hacia el centro
            trap.stakes = new Transform[7];
            for (int i = 0; i < trap.stakes.Length; i++)
            {
                float a = i * (360f / trap.stakes.Length);
                var pivot = AyniFaunaPrimitives.Pivot("Estaca", trap.root.transform, Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.62f);
                pivot.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(78f, 0f, 0f);
                AyniFaunaPrimitives.Primitive(PrimitiveType.Cylinder, "Palo", pivot, wood, new Vector3(0f, 0.26f, 0f), Vector3.zero, new Vector3(0.045f, 0.26f, 0.045f));
                AyniFaunaPrimitives.Primitive(PrimitiveType.Capsule, "Punta", pivot, wood, new Vector3(0f, 0.53f, 0f), Vector3.zero, new Vector3(0.035f, 0.05f, 0.035f));
                trap.stakes[i] = pivot;
            }
            return trap;
        }

        // ───────────────────────── Saltar ─────────────────────────

        private void Update()
        {
            if (boss != null)
            {
                // Al empezar el duelo, Amaru le recuerda a Yari que estaba avisado
                if (bossWasDormant && !boss.Dormant && placed && !lineSaid)
                {
                    lineSaid = true;
                    StartCoroutine(SayLater("Mi Rastreador me avisó de que venías, guardián. Mira bien dónde pisas.", 1.2f));
                }
                bossWasDormant = boss.Dormant;
            }

            if (traps.Count == 0 || yari == null || yari.IsDead) return;
            if (AyniGameState.CinematicPlaying || AyniBossIntro.Running || AyniJudgment.Active || yari.CinematicControl) return;
            Vector3 p = yari.transform.position;
            foreach (Trap trap in traps)
            {
                if (!trap.armed || trap.root == null) continue;
                Vector3 d = trap.root.transform.position - p;
                if (Mathf.Abs(d.y) > 0.8f) continue;
                d.y = 0f;
                if (d.magnitude <= triggerRadius) Spring(trap);
            }
        }

        private IEnumerator SayLater(string line, float delay)
        {
            yield return new WaitForSeconds(delay);
            AyniBossIntro.Say(line, 3.6f);
        }

        private void Spring(Trap trap)
        {
            trap.armed = false;
            Armed = Mathf.Max(0, Armed - 1);
            Vector3 at = trap.root.transform.position;
            AyniAudio.Play("jaula_rota", at + Vector3.up * 0.3f, 1f, 0.05f, 1.35f);
            AttackResult result = yari.ReceiveAttack(damage, structureDamage, AttackHeight.Low);
            if (result == AttackResult.Hit)
            {
                AyniAudio.Play("golpe_fuerte", at + Vector3.up * 0.4f, 0.8f);
                CombatFeedback.Flash(at + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.3f), 0.8f, 0.14f);
                CombatFeedback.Shake(0.08f, 0.2f);
            }
            Debug.Log($"[Ayni Trampas] Yari pisa una trampa: {result}");
            StartCoroutine(SnapAndFade(trap));
        }

        private IEnumerator SnapAndFade(Trap trap)
        {
            var from = new Quaternion[trap.stakes.Length];
            var to = new Quaternion[trap.stakes.Length];
            for (int i = 0; i < trap.stakes.Length; i++)
            {
                from[i] = trap.stakes[i].localRotation;
                float a = i * (360f / trap.stakes.Length);
                to[i] = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(-22f, 0f, 0f);
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.12f)
            {
                for (int i = 0; i < trap.stakes.Length; i++) trap.stakes[i].localRotation = Quaternion.Slerp(from[i], to[i], t);
                yield return null;
            }
            for (int i = 0; i < trap.stakes.Length; i++) trap.stakes[i].localRotation = to[i];
            yield return new WaitForSeconds(4f);
            if (trap.root != null) Destroy(trap.root);
        }

        // ───────────────────────── Para probar (puente de agentes) ─────────────────────────

        /// <summary>
        /// Pone a Yari a 1,5 m de la primera trampa armada, del lado de Amaru (lejos de la garganta) y mirándola: luego basta
        /// con andar hacia delante.
        /// </summary>
        public static void TestFaceTrap()
        {
            if (instance == null || instance.yari == null) return;
            foreach (Trap trap in instance.traps)
            {
                if (!trap.armed || trap.root == null) continue;
                Transform y = instance.yari.transform;
                Vector3 t = trap.root.transform.position;
                Vector3 toBoss = instance.boss != null ? instance.boss.transform.position - t : Vector3.forward;
                toBoss.y = 0f;
                toBoss = toBoss.sqrMagnitude > 0.01f ? toBoss.normalized : Vector3.forward;
                Vector3 from = t + toBoss * 1.5f;
                Vector3 dir = -toBoss;
                var mover = y.GetComponent<CharacterController>();
                if (mover != null) mover.enabled = false;
                y.SetPositionAndRotation(from + Vector3.up * 0.3f, Quaternion.LookRotation(dir));
                if (mover != null) mover.enabled = true;
                var cam = Camera.main != null ? Camera.main.GetComponent<Ayni.Player.ThirdPersonSifuCamera>() : null;
                if (cam != null) cam.SnapBehindTarget();
                Debug.Log($"[Ayni Trampas] Yari a 1,5 m de la trampa en {t} (desde {from})");
                return;
            }
        }

        /// <summary>Pone a Yari encima de la primera trampa armada.</summary>
        public static void TestStepOnTrap()
        {
            if (instance == null || instance.yari == null) return;
            foreach (Trap trap in instance.traps)
            {
                if (!trap.armed || trap.root == null) continue;
                Transform y = instance.yari.transform;
                var mover = y.GetComponent<CharacterController>();
                if (mover != null) mover.enabled = false;
                y.position = trap.root.transform.position + Vector3.up * 0.1f;
                if (mover != null) mover.enabled = true;
                Debug.Log($"[Ayni Trampas] Yari sobre la trampa en {trap.root.transform.position}");
                return;
            }
            Debug.LogWarning("[Ayni Trampas] No hay trampas armadas");
        }

        public static void Report()
        {
            Debug.Log($"[Ayni Trampas] Armadas: {Armed} de {(instance != null ? instance.traps.Count : 0)} · Amaru avisado {AyniPathRewards.AmaruWarned}");
            if (instance == null) return;
            foreach (Trap trap in instance.traps)
            {
                if (trap.root == null) continue;
                Vector3 p = trap.root.transform.position;
                string under = Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? $"{hit.collider.name} (capa {LayerMask.LayerToName(hit.collider.gameObject.layer)})" : "nada";
                Debug.Log($"[Ayni Trampas]   {p} · armada {trap.armed} · debajo: {under}");
            }
        }
    }
}
