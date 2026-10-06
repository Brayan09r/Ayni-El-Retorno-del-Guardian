using System;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Enemy;

namespace Ayni.World
{
    /// <summary>
    /// Un conjunto de casas del camino donde esperan rivales, al estilo de las salas de Sifu.
    /// La herramienta "Ayni > Entorno > Construir Aldea Inca" crea uno por conjunto, con sus puntos de aparición
    /// (delante de cada puerta y junto a la salida).
    ///
    /// Para poblarlo: coloca cada rival en uno de los "PuntoRival" y arrástralo a la lista Rivales. Quedan ocultos
    /// hasta que Yari cruza la portada; entonces aparecen y se lanza <see cref="OnPlayerEntered"/>. Cuando todos
    /// han sido derrotados (muertos o perdonados) se lanza <see cref="OnCleared"/>.
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
        [Tooltip("Los rivales permanecen ocultos hasta que Yari entra en el patio.")]
        [SerializeField] private bool hideRivalsUntilEntered = true;

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

        private void Start()
        {
            if (!hideRivalsUntilEntered) return;
            for (int i = 0; i < rivals.Count; i++)
            {
                if (rivals[i] != null) rivals[i].SetActive(false);
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
                if (!Contains(player.position)) return;

                PlayerEntered = true;
                for (int i = 0; i < rivals.Count; i++)
                {
                    if (rivals[i] != null) rivals[i].SetActive(true);
                }
                OnPlayerEntered?.Invoke(this);
                return;
            }

            if (rivals.Count == 0) return;
            for (int i = 0; i < rivals.Count; i++)
            {
                GameObject rival = rivals[i];
                if (rival == null || !rival.activeInHierarchy) continue;
                var enemy = rival.GetComponent<EnemyController>();
                if (enemy == null || !enemy.IsDead) return; // aún queda alguien en pie
            }
            Cleared = true;
            OnCleared?.Invoke(this);
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
