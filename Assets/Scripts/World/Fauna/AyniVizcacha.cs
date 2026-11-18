using UnityEngine;
using Ayni.Player;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Comportamiento ambiental de la vizcacha de la sierra.
    ///  - Toma sol: sentada quieta sobre una roca o un muro de piedra, moviendo las orejas de vez en cuando.
    ///  - Salta: a ratos se cambia a otra roca cercana con saltos cortos.
    ///  - Alerta: si Yari se acerca, se yergue y lo mira fijamente.
    ///  - Huye: si Yari llega muy cerca, se aleja a saltos rápidos y se queda agazapada un rato.
    /// Es decorativa: no tiene colisionadores.
    /// </summary>
    [DisallowMultipleComponent]
    public class AyniVizcacha : MonoBehaviour
    {
        private enum State { Basking, Hopping, Alert, Hiding }

        [Header("Colonia")]
        [SerializeField] private Vector3 home;
        [SerializeField] private float colonyRadius = 7f;

        [Header("Reacción ante Yari")]
        [SerializeField] private float watchRadius = 14f;
        [SerializeField] private float fleeRadius = 7f;

        [Header("Partes (las asigna AyniCritterModel)")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftEar;
        [SerializeField] private Transform rightEar;
        [SerializeField] private Transform tail;

        private State state;
        private float stateTimer;
        private int hopsLeft;
        private bool fleeing;

        private Vector3 hopFrom, hopTo;
        private float hopTime, hopDuration, hopHeight;

        private float nextEarTwitch;
        private float earTwitchStart = -10f;
        private bool twitchLeft;
        private Quaternion leftEarRest, rightEarRest, tailRest;
        private float bodyStretch = 1f;
        private float nextAlertCheck;

        private static Transform cachedPlayer;
        private static float nextPlayerSearch;

        public void Configure(Vector3 colonyCenter, float radius, AyniCritterModel.VizcachaParts parts)
        {
            home = colonyCenter;
            colonyRadius = radius;
            body = parts.body;
            head = parts.head;
            leftEar = parts.leftEar;
            rightEar = parts.rightEar;
            tail = parts.tail;
        }

        private void Start()
        {
            if (home == Vector3.zero) home = transform.position;
            if (leftEar != null) leftEarRest = leftEar.localRotation;
            if (rightEar != null) rightEarRest = rightEar.localRotation;
            if (tail != null) tailRest = tail.localRotation;
            EnterBasking();
            stateTimer *= Random.value;
            nextEarTwitch = Time.time + Random.Range(0.5f, 3f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (state != State.Hopping && Time.time >= nextAlertCheck)
            {
                nextAlertCheck = Time.time + 0.2f;
                CheckForYari();
            }

            stateTimer -= dt;
            float stretchTarget = 1f;
            switch (state)
            {
                case State.Basking:
                    if (stateTimer <= 0f) StartHops(Random.Range(1, 4), false);
                    break;

                case State.Alert:
                    stretchTarget = 1.15f; // erguida, vigilando
                    LookAtPlayer(dt);
                    if (stateTimer <= 0f) EnterBasking();
                    break;

                case State.Hiding:
                    stretchTarget = 0.78f; // agazapada contra la roca
                    if (stateTimer <= 0f) EnterBasking();
                    break;

                case State.Hopping:
                    UpdateHop(dt);
                    break;
            }

            if (state != State.Hopping)
            {
                bodyStretch = Mathf.MoveTowards(bodyStretch, stretchTarget, dt * 1.5f);
            }
            if (body != null) body.localScale = new Vector3(1f, bodyStretch, 1f / Mathf.Sqrt(bodyStretch));

            AnimateEarsAndTail();
        }

        // ------------------------------------------------------------------------------------------------
        private void EnterBasking()
        {
            state = State.Basking;
            stateTimer = Random.Range(6f, 18f);
            fleeing = false;
        }

        private void CheckForYari()
        {
            Transform player = FindPlayer();
            if (player == null) return;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist < fleeRadius && state != State.Hiding)
            {
                StartHops(Random.Range(3, 5), true);
            }
            else if (dist < watchRadius && state == State.Basking)
            {
                state = State.Alert;
                stateTimer = Random.Range(3f, 6f);
            }
            else if (state == State.Alert && dist < watchRadius)
            {
                stateTimer = Mathf.Max(stateTimer, 1.5f); // sigue vigilando mientras Yari esté cerca
            }
        }

        private void StartHops(int count, bool flee)
        {
            hopsLeft = count;
            fleeing = flee;
            if (!NextHop())
            {
                if (flee) { state = State.Hiding; stateTimer = Random.Range(5f, 9f); }
                else EnterBasking();
            }
        }

        /// <summary>Elige el siguiente salto; devuelve false si no hay dónde aterrizar.</summary>
        private bool NextHop()
        {
            Transform player = FindPlayer();
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 dir;
                float distance;
                if (fleeing && player != null)
                {
                    dir = transform.position - player.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
                    dir = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * dir.normalized;
                    distance = Random.Range(1.4f, 2.4f);
                }
                else
                {
                    Vector2 r = Random.insideUnitCircle.normalized;
                    dir = new Vector3(r.x, 0f, r.y);
                    distance = Random.Range(0.6f, 1.6f);
                }

                Vector3 candidate = transform.position + dir * distance;
                Vector3 fromHome = candidate - home;
                fromHome.y = 0f;
                float limit = colonyRadius * (fleeing ? 1.8f : 1f);
                if (fromHome.magnitude > limit) continue;

                if (TryFindPerch(candidate, out Vector3 landing))
                {
                    hopFrom = transform.position;
                    hopTo = landing;
                    hopTime = 0f;
                    float horizontal = Vector3.Distance(new Vector3(hopFrom.x, 0f, hopFrom.z), new Vector3(hopTo.x, 0f, hopTo.z));
                    hopDuration = (fleeing ? 0.22f : 0.3f) + horizontal * 0.06f;
                    hopHeight = 0.2f + horizontal * 0.12f + Mathf.Max(0f, hopTo.y - hopFrom.y);
                    state = State.Hopping;

                    Vector3 flat = hopTo - hopFrom;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    return true;
                }
            }
            return false;
        }

        private void UpdateHop(float dt)
        {
            hopTime += dt;
            float t = Mathf.Clamp01(hopTime / hopDuration);
            Vector3 pos = Vector3.Lerp(hopFrom, hopTo, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * hopHeight;
            transform.position = pos;

            // Se estira en el aire y se recoge al aterrizar
            bodyStretch = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;

            if (t >= 1f)
            {
                hopsLeft--;
                if (hopsLeft > 0 && NextHop()) return;

                if (fleeing)
                {
                    state = State.Hiding;
                    stateTimer = Random.Range(5f, 9f);
                }
                else
                {
                    EnterBasking();
                }
            }
        }

        private void LookAtPlayer(float dt)
        {
            Transform player = FindPlayer();
            if (player == null) return;
            Vector3 flat = player.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 120f * dt);
        }

        private void AnimateEarsAndTail()
        {
            // Movimiento rápido de una oreja cada pocos segundos
            if (Time.time >= nextEarTwitch)
            {
                earTwitchStart = Time.time;
                twitchLeft = Random.value < 0.5f;
                nextEarTwitch = Time.time + Random.Range(1.2f, 4.5f);
            }
            float k = Time.time - earTwitchStart;
            float twitch = k < 0.25f ? Mathf.Sin(k / 0.25f * Mathf.PI) * 25f : 0f;
            // En alerta las orejas se paran rectas
            float erect = state == State.Alert ? -10f : 0f;

            if (leftEar != null)
                leftEar.localRotation = leftEarRest * Quaternion.Euler(erect, 0f, twitchLeft ? twitch : 0f);
            if (rightEar != null)
                rightEar.localRotation = rightEarRest * Quaternion.Euler(erect, 0f, twitchLeft ? 0f : -twitch);

            if (tail != null)
            {
                float sway = Mathf.Sin(Time.time * 1.3f + GetInstanceID() * 0.1f) * (state == State.Hopping ? 14f : 4f);
                tail.localRotation = tailRest * Quaternion.Euler(state == State.Hopping ? -15f : 0f, sway, 0f);
            }
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>
        /// Busca dónde apoyarse cerca de <paramref name="point"/>: sobre una roca o muro de piedra, o sobre el terreno
        /// con una superficie razonablemente plana. Nunca sobre techos de casas ni sobre Yari.
        /// </summary>
        public static bool TryFindPerch(Vector3 point, out Vector3 landing)
        {
            landing = point;
            Terrain terrain = Terrain.activeTerrain;
            float baseY = terrain != null ? terrain.SampleHeight(point) + terrain.GetPosition().y : point.y;

            Vector3 from = new Vector3(point.x, baseY + 12f, point.z);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (hit.normal.y < 0.7f) return false;
            if (!(hit.collider is TerrainCollider) && !IsStoneSurface(hit.collider.transform)) return false;

            landing = hit.point;
            return true;
        }

        /// <summary>True si el objeto (o alguno de sus padres) es una roca o un muro de piedra.</summary>
        public static bool IsStoneSurface(Transform t)
        {
            if (t.GetComponentInParent<YariCombatController>() != null) return false;
            for (Transform cur = t; cur != null; cur = cur.parent)
            {
                string n = cur.name.ToLowerInvariant();
                if (n.Contains("roca") || n.Contains("rock") || n.Contains("boulder") || n.Contains("piedra") ||
                    n.Contains("muro") || n.Contains("anden") || n.Contains("andén"))
                {
                    return true;
                }
            }
            return false;
        }

        private static Transform FindPlayer()
        {
            if (cachedPlayer == null && Time.time >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.time + 2f;
                var yari = FindFirstObjectByType<YariCombatController>();
                cachedPlayer = yari != null ? yari.transform : null;
            }
            return cachedPlayer;
        }
    }
}
