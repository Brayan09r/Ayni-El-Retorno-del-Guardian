using System;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.UI;

namespace Ayni.World
{
    /// <summary>
    /// Un conjunto de casas del camino donde esperan rivales, al estilo de las salas de Sifu.
    /// La herramienta "Ayni > Entorno > Construir Aldea Inca" crea uno por conjunto, con sus puntos de aparición
    /// (delante de cada puerta y junto a la salida).
    ///
    /// Para poblarlo: coloca cada rival en uno de los "PuntoRival" y arrástralo a la lista Rivales. Como en Sifu,
    /// los rivales ya están en el patio haciendo lo suyo (<see cref="AyniRivalAmbient"/>: patrullar, hacer guardia o
    /// descansar) y pelean cuando descubren a Yari o él cruza la portada; entonces se lanza
    /// <see cref="OnPlayerEntered"/>. Cuando cae la mitad llega una segunda oleada corriendo desde la portada, y uno de
    /// los rivales tiene un segundo aliento: cae y se levanta furioso. Cuando todos han sido derrotados (muertos o
    /// perdonados) se lanza <see cref="OnCleared"/>.
    /// </summary>
    public class AyniEncounterSite : MonoBehaviour
    {
        /// <summary>Todos los conjuntos activos de la escena, en cualquier orden (cada uno sabe el suyo en <see cref="Order"/>).</summary>
        public static readonly List<AyniEncounterSite> All = new List<AyniEncounterSite>();

        /// <summary>Yari acaba de entrar en el patio de este conjunto.</summary>
        public static event Action<AyniEncounterSite> OnPlayerEntered;
        /// <summary>Todos los rivales del conjunto han sido derrotados.</summary>
        public static event Action<AyniEncounterSite> OnCleared;

        [SerializeField] private string siteName = "Conjunto";
        [Tooltip("Orden en el recorrido: 1 es el primero que encuentra Yari.")]
        [SerializeField] private int order = 1;
        [Tooltip("Tamaño del patio (ancho, alto, largo) en el que se considera que Yari ha entrado.")]
        [SerializeField] private Vector3 patioSize = new Vector3(20f, 5f, 20f);
        [SerializeField] private Transform entry;
        [SerializeField] private Transform exit;
        [Tooltip("Dónde pueden aparecer los rivales: delante de las puertas y junto a la salida. Miran hacia el patio.")]
        [SerializeField] private Transform[] spawnPoints = new Transform[0];
        [Tooltip("Puntos dentro de las casas, uno por puerta: para rivales que esperan dentro. Miran hacia la puerta.")]
        [SerializeField] private Transform[] interiorPoints = new Transform[0];

        [Header("Rivales")]
        [Tooltip("Rivales de este conjunto (objetos de la escena con EnemyController).")]
        [SerializeField] private List<GameObject> rivals = new List<GameObject>();
        [Tooltip("Los rivales permanecen ocultos hasta que Yari entra en el patio (si no están en su rutina).")]
        [SerializeField] private bool hideRivalsUntilEntered = true;
        [Tooltip("Los rivales esperan a la vista haciendo lo suyo (patrullar, guardia, descansar) en vez de ocultos.")]
        [SerializeField] private bool ambientBeforeFight = true;

        [Header("Oleadas")]
        [Tooltip("Rivales de refuerzo que llegan cuando cae la mitad (−1 = según el orden: 1, 2, 2, 3).")]
        [SerializeField] private int reinforcements = -1;
        [Tooltip("Cuántos rivales del patio tienen un segundo aliento (se levantan furiosos al caer).")]
        [SerializeField] private int secondWinds = 1;

        private readonly List<AyniRivalAmbient> ambient = new List<AyniRivalAmbient>();
        private GameObject template;
        private int startingRivals;
        private bool waveSent;

        private Transform player;
        private float nextPlayerSearch;

        public string SiteName => siteName;
        public int Order => order;
        public Transform Entry => entry;
        public Transform Exit => exit;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
        public IReadOnlyList<Transform> InteriorPoints => interiorPoints;
        public IReadOnlyList<GameObject> Rivals => rivals;
        public bool HideRivalsUntilEntered => hideRivalsUntilEntered;
        public bool PlayerEntered { get; private set; }
        public bool Cleared { get; private set; }

        /// <summary>Punto de aparición número <paramref name="index"/> (da la vuelta si se pide uno de más).</summary>
        public Transform GetSpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return transform;
            return spawnPoints[((index % spawnPoints.Length) + spawnPoints.Length) % spawnPoints.Length];
        }

        /// <summary>Punto número <paramref name="index"/> dentro de una casa (da la vuelta si se pide uno de más).</summary>
        public Transform GetInteriorPoint(int index)
        {
            if (interiorPoints == null || interiorPoints.Length == 0) return GetSpawnPoint(index);
            return interiorPoints[((index % interiorPoints.Length) + interiorPoints.Length) % interiorPoints.Length];
        }

        /// <summary>Lo usa la herramienta del editor al construir la aldea.</summary>
        public void SetInteriorPoints(Transform[] newInteriorPoints)
        {
            interiorPoints = newInteriorPoints ?? new Transform[0];
        }

        /// <summary>Añade un rival creado por código (por ejemplo, instanciado en un punto de aparición).</summary>
        public void AddRival(GameObject rival)
        {
            if (rival == null || rivals.Contains(rival)) return;
            rivals.Add(rival);
            if (hideRivalsUntilEntered && !PlayerEntered) rival.SetActive(false);
        }

        /// <summary>Sustituye la lista de rivales (la herramienta del editor la conserva así al reconstruir la aldea).</summary>
        public void SetRivals(IEnumerable<GameObject> newRivals, bool hideUntilEntered)
        {
            rivals.Clear();
            if (newRivals != null)
            {
                foreach (GameObject rival in newRivals)
                {
                    if (rival != null && !rivals.Contains(rival)) rivals.Add(rival);
                }
            }
            hideRivalsUntilEntered = hideUntilEntered;
        }

        /// <summary>Lo usa la herramienta del editor al construir la aldea.</summary>
        public void Setup(string newName, int newOrder, Vector3 newPatioSize, Transform newEntry, Transform newExit, Transform[] newSpawnPoints)
        {
            siteName = newName;
            order = newOrder;
            patioSize = newPatioSize;
            entry = newEntry;
            exit = newExit;
            spawnPoints = newSpawnPoints;
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Awake()
        {
            // Copia oculta de un rival, antes de que nadie lo toque, para crear refuerzos y patrullas del camino
            for (int i = rivals.Count - 1; i >= 0 && template == null; i--)
            {
                if (rivals[i] == null || rivals[i].GetComponent<EnemyController>() == null) continue;
                bool wasActive = rivals[i].activeSelf;
                rivals[i].SetActive(false);
                template = Instantiate(rivals[i], transform);
                template.name = rivals[i].name + "_Plantilla";
                rivals[i].SetActive(wasActive);
            }
        }

        /// <summary>
        /// Crea un rival nuevo como los de este patio (refuerzos, patrullas del camino, cazadores de Amaru). Llega activo
        /// y sin rutina: persigue a Yari en cuanto lo tiene a tiro.
        /// </summary>
        public EnemyController CreateRival(Vector3 position, Quaternion rotation, string name)
        {
            if (template == null) return null;
            GameObject rival = Instantiate(template, position, rotation);
            rival.name = name;
            rival.SetActive(true);
            return rival.GetComponent<EnemyController>();
        }

        private void Start()
        {
            startingRivals = 0;
            int winds = secondWinds;
            for (int i = rivals.Count - 1; i >= 0; i--)
            {
                var enemy = rivals[i] != null ? rivals[i].GetComponent<EnemyController>() : null;
                if (enemy == null) continue;
                startingRivals++;
                // El último de la lista (el más duro de cada patio) es el que se levanta
                if (winds-- > 0) enemy.SecondWinds = 1;
            }
            if (!hideRivalsUntilEntered) return;
            if (ambientBeforeFight)
            {
                StartAmbient();
                return;
            }
            for (int i = 0; i < rivals.Count; i++)
            {
                if (rivals[i] != null) rivals[i].SetActive(false);
            }
        }

        /// <summary>Reparte las rutinas: uno hace guardia, otro patrulla hacia el centro del patio y otro descansa.</summary>
        private void StartAmbient()
        {
            for (int i = 0; i < rivals.Count; i++)
            {
                GameObject rival = rivals[i];
                if (rival == null || rival.GetComponent<EnemyController>() == null) continue;
                rival.SetActive(true); // en la escena se guardan ocultos
                var routine = rival.GetComponent<AyniRivalAmbient>();
                if (routine == null) routine = rival.AddComponent<AyniRivalAmbient>();
                ambient.Add(routine);
            }
            for (int i = 0; i < ambient.Count; i++)
            {
                Vector3 from = ambient[i].transform.position;
                Vector3 toCentre = transform.position - from;
                toCentre.y = 0f;
                Vector3 patrolTo = from + Vector3.ClampMagnitude(toCentre * 0.6f, 6f);
                var activity = (AyniRivalAmbient.Activity)(i % 3);
                ambient[i].Begin(activity, patrolTo, ambient);
            }
        }

        private void Update()
        {
            if (Cleared) return;

            if (!PlayerEntered)
            {
                if (player == null)
                {
                    if (Time.unscaledTime < nextPlayerSearch) return;
                    nextPlayerSearch = Time.unscaledTime + 1f;
                    GameObject found = GameObject.FindGameObjectWithTag("Player");
                    if (found == null) return;
                    player = found.transform;
                }
                bool discovered = false;
                foreach (AyniRivalAmbient routine in ambient) if (routine != null && routine.IsAwake) discovered = true;
                if (!discovered && !Contains(player.position)) return;

                PlayerEntered = true;
                // Al cruzar la portada todos se enteran (si nadie lo había visto aún, el primero da la voz)
                if (!discovered && ambient.Count > 0 && ambient[0] != null) ambient[0].Notice(0f, true);
                for (int i = 0; i < rivals.Count; i++)
                {
                    if (rivals[i] != null) rivals[i].SetActive(true);
                }
                OnPlayerEntered?.Invoke(this);
                return;
            }

            if (rivals.Count == 0) return;
            int standing = 0;
            for (int i = 0; i < rivals.Count; i++)
            {
                GameObject rival = rivals[i];
                if (rival == null || !rival.activeInHierarchy) continue;
                var enemy = rival.GetComponent<EnemyController>();
                if (enemy == null || !enemy.IsDead) standing++;
            }
            if (!waveSent && standing <= startingRivals / 2)
            {
                SendReinforcements();
                return;
            }
            if (standing > 0) return; // aún queda alguien en pie
            Cleared = true;
            OnCleared?.Invoke(this);
        }

        /// <summary>Segunda oleada: llegan corriendo desde la portada más lejana de Yari.</summary>
        private void SendReinforcements()
        {
            waveSent = true;
            int count = reinforcements >= 0 ? reinforcements : order <= 1 ? 1 : order <= 3 ? 2 : 3;
            if (count <= 0 || template == null || player == null) return;

            Transform gate = entry;
            if (exit != null && (gate == null || (exit.position - player.position).sqrMagnitude > (gate.position - player.position).sqrMagnitude))
            {
                gate = exit;
            }
            Vector3 from = gate != null ? gate.position : transform.position;
            Vector3 toPatio = transform.position - from;
            toPatio.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, toPatio.sqrMagnitude > 0.01f ? toPatio.normalized : Vector3.forward);
            for (int i = 0; i < count; i++)
            {
                Vector3 spot = from - toPatio.normalized * 2f + side * ((i - (count - 1) * 0.5f) * 1.6f);
                if (Physics.Raycast(spot + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    spot = hit.point + Vector3.up * 0.05f;
                }
                EnemyController rival = CreateRival(spot, Quaternion.LookRotation(toPatio.sqrMagnitude > 0.01f ? toPatio : Vector3.forward), $"{template.name.Replace("_Plantilla", "")}_Refuerzo_{i + 1}");
                if (rival == null) continue;
                rival.HuntRange = 60f;
                rivals.Add(rival.gameObject);
            }
            AyniAudio.Play2D("titulo_golpe", 0.75f, 0.8f);
            AyniScreenFX.Caption("¡Llegan más guardias de Sayri!", 2.2f);
        }

        /// <summary>¿Está este punto del mundo dentro del patio?</summary>
        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= patioSize.x * 0.5f && Mathf.Abs(local.z) <= patioSize.z * 0.5f &&
                   local.y >= -1.5f && local.y <= patioSize.y;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, patioSize.y * 0.5f, 0f), patioSize);
            Gizmos.matrix = Matrix4x4.identity;
            DrawPoints(spawnPoints, new Color(0.9f, 0.2f, 0.15f, 0.9f));
            DrawPoints(interiorPoints, new Color(0.3f, 0.6f, 1f, 0.9f));
        }

        private static void DrawPoints(Transform[] points, Color color)
        {
            if (points == null) return;
            Gizmos.color = color;
            foreach (Transform point in points)
            {
                if (point == null) continue;
                Gizmos.DrawWireSphere(point.position + Vector3.up * 0.9f, 0.35f);
                Gizmos.DrawLine(point.position + Vector3.up * 0.9f, point.position + Vector3.up * 0.9f + point.forward * 0.9f);
            }
        }
    }
}
