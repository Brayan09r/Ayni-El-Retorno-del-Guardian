using System.Collections.Generic;
using UnityEngine;

namespace Ayni.World
{
    /// <summary>
    /// Marca de un árbol quemado del camino, en pie o caído (los pone "Ayni > Entorno > Construir Aldea Inca" junto a cada
    /// obstáculo, o "Marcar Árboles Quemados del Camino" sobre una aldea ya construida).
    /// No dibuja nada: guarda por dónde rebrota el árbol. Si Yari perdona a Amaru, AyniReforestation recorre todas las
    /// marcas y hace renacer cada árbol: la madera quemada recupera su color, sale una copa nueva en la punta y varas de
    /// queñua en las ramas rotas y a lo largo del tronco.
    /// </summary>
    public class AyniBurntTree : MonoBehaviour
    {
        /// <summary>Tipo de rebrote: copa nueva en la punta del tronco.</summary>
        public const int Crown = 0;
        /// <summary>Tipo de rebrote: vara que sale de una rama rota o de la corteza.</summary>
        public const int Shoot = 1;

        /// <summary>Todos los árboles quemados activos de la escena.</summary>
        public static readonly List<AyniBurntTree> All = new List<AyniBurntTree>();

        [Tooltip("Caído a través del camino (si no, sigue en pie).")]
        [SerializeField] private bool fallen;
        [Tooltip("Mallas de madera quemada a las que pertenece: recuperan el color de la corteza viva.")]
        [SerializeField] private Renderer[] wood = new Renderer[0];
        [Tooltip("Puntos de rebrote, respecto a esta marca.")]
        [SerializeField] private Vector3[] shootPoints = new Vector3[0];
        [SerializeField] private Vector3[] shootDirections = new Vector3[0];
        [SerializeField] private float[] shootSizes = new float[0];
        [Tooltip("0 = copa nueva, 1 = vara.")]
        [SerializeField] private int[] shootKinds = new int[0];

        public bool Fallen => fallen;
        public IReadOnlyList<Renderer> Wood => wood;
        public int ShootCount => shootPoints != null ? shootPoints.Length : 0;

        /// <summary>Rebrote número <paramref name="index"/>, en coordenadas del mundo.</summary>
        public void GetShoot(int index, out Vector3 point, out Vector3 direction, out float size, out bool crown)
        {
            point = transform.TransformPoint(shootPoints[index]);
            direction = index < shootDirections.Length ? transform.TransformDirection(shootDirections[index]) : Vector3.up;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.up;
            direction.Normalize();
            size = index < shootSizes.Length ? shootSizes[index] : 1f;
            crown = index < shootKinds.Length && shootKinds[index] == Crown;
        }

        /// <summary>Lo llama la herramienta que construye la aldea.</summary>
        public void Setup(bool isFallen, Renderer[] woodRenderers, Vector3[] points, Vector3[] directions, float[] sizes, int[] kinds)
        {
            fallen = isFallen;
            wood = woodRenderers ?? new Renderer[0];
            shootPoints = points ?? new Vector3[0];
            shootDirections = directions ?? new Vector3[0];
            shootSizes = sizes ?? new float[0];
            shootKinds = kinds ?? new int[0];
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < ShootCount; i++)
            {
                GetShoot(i, out Vector3 point, out Vector3 direction, out float size, out bool crown);
                Gizmos.color = crown ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.75f, 0.9f, 0.3f);
                Gizmos.DrawLine(point, point + direction * size * (crown ? 1.8f : 0.9f));
                Gizmos.DrawWireSphere(point, 0.06f);
            }
        }
    }
}
