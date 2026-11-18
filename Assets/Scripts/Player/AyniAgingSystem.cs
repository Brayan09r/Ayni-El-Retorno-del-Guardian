using UnityEngine;
using Ayni.Core;

namespace Ayni.Player
{
    /// <summary>
    /// Controla la transformación visual del modelo 3D de Yari según su envejecimiento (IllaTalismanSystem):
    /// - Etapa Joven (20-35 años): Modelo original de Yari (Visual_Yari_3D).
    /// - Etapa Madura / Veterana (36+ años): Modelo experimentado con desgaste vital (Visual_Yari_Aged_3D).
    /// </summary>
    public class AyniAgingSystem : MonoBehaviour
    {
        [Header("Modelos Visuales")]
        [Tooltip("Contenedor visual de Yari joven (20-35 años).")]
        [SerializeField] private GameObject youngVisual;
        [Tooltip("Contenedor visual de Yari maduro (+10 años, 36+ años).")]
        [SerializeField] private GameObject agedVisual;

        private IllaTalismanSystem talisman;
        private YariCombatController combat;

        public GameObject YoungVisual { get => youngVisual; set => youngVisual = value; }
        public GameObject AgedVisual { get => agedVisual; set => agedVisual = value; }

        private void Awake()
        {
            talisman = GetComponent<IllaTalismanSystem>();
            combat = GetComponent<YariCombatController>();

            if (youngVisual == null)
            {
                var t = transform.Find("Visual_Yari_3D");
                if (t != null) youngVisual = t.gameObject;
            }

            if (agedVisual == null)
            {
                var t = transform.Find("Visual_Yari_Aged_3D");
                if (t != null) agedVisual = t.gameObject;
            }
        }

        private void OnEnable()
        {
            if (talisman != null)
            {
                talisman.OnAgeChanged += HandleAgeChanged;
            }
        }

        private void OnDisable()
        {
            if (talisman != null)
            {
                talisman.OnAgeChanged -= HandleAgeChanged;
            }
        }

        private void Start()
        {
            UpdateVisual(talisman != null ? talisman.CurrentAge : 20);
        }

        private void HandleAgeChanged(int newAge)
        {
            UpdateVisual(newAge);
        }

        /// <summary>
        /// Cambia la visibilidad de las mallas y sincroniza el Animator según la edad actual.
        /// </summary>
        public void UpdateVisual(int age)
        {
            bool isAged = age > 35;

            if (youngVisual != null)
            {
                youngVisual.SetActive(!isAged);
            }

            if (agedVisual != null)
            {
                agedVisual.SetActive(isAged);
            }

            if (combat != null)
            {
                combat.RefreshAnimator();
            }

            // Notificar o sincronizar componentes si es necesario
            Debug.Log($"[AyniAgingSystem] Aspecto visual de Yari actualizado: {(isAged ? "Maduro (+10 años)" : "Joven")}. Edad: {age} años.");
        }

        /// <summary>
        /// Permite alternar manualmente el aspecto en modo prueba o depuración.
        /// </summary>
        public void ForceSetAged(bool aged)
        {
            UpdateVisual(aged ? 40 : 20);
        }
    }
}
