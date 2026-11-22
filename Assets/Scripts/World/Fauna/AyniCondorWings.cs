using UnityEngine;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Pliega las alas del cóndor 3D cuando está posado. El FBX no trae una pose de alas recogidas: su "Idle_Perch" las
    /// tiene abiertas, como si fuera a despegar. Encima de la animación, cada ala se gira para quedar pegada al flanco,
    /// hacia atrás y abajo, a lo largo del lomo. En vuelo se despliega (<see cref="SetFolded"/>).
    /// AyniCondorModel lo añade al modelo 3D; empieza plegado.
    /// </summary>
    public class AyniCondorWings : MonoBehaviour
    {
        [Tooltip("Plegado al que va (1 = alas recogidas, 0 = lo que diga la animación).")]
        [Range(0f, 1f)] [SerializeField] private float targetFold = 1f;
        [Tooltip("Cuánto plegado cambia por segundo al recoger o abrir las alas.")]
        [SerializeField] private float foldSpeed = 1.8f;

        private float fold = 1f;
        private Transform leftInner, leftMid, leftTip, rightInner, rightMid, rightTip;

        // Direcciones de las alas recogidas en el espacio del modelo posado (cuerpo inclinado unos 45°): hacia atrás y abajo,
        // un poco hacia fuera para no meterse en el cuerpo. x se multiplica por el lado (+1 izquierda, que está en +X).
        private static readonly Vector3 UpperWing = new Vector3(0.32f, -0.55f, -0.78f);
        private static readonly Vector3 Hand = new Vector3(0.12f, -0.62f, -0.78f);

        /// <summary>¿Está (o va a estar) con las alas recogidas?</summary>
        public bool Folded => targetFold > 0.5f;

        private void Awake()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Wing_L_Inner": leftInner = t; break;
                    case "Wing_L_Mid": leftMid = t; break;
                    case "Wing_L_Tip": leftTip = t; break;
                    case "Wing_R_Inner": rightInner = t; break;
                    case "Wing_R_Mid": rightMid = t; break;
                    case "Wing_R_Tip": rightTip = t; break;
                }
            }
            fold = targetFold;
        }

        /// <summary>Recoge (true) o abre (false) las alas; con <paramref name="instant"/> sin transición.</summary>
        public void SetFolded(bool folded, bool instant = false)
        {
            targetFold = folded ? 1f : 0f;
            if (instant) fold = targetFold;
        }

        private void LateUpdate()
        {
            fold = Mathf.MoveTowards(fold, targetFold, foldSpeed * Time.deltaTime);
            if (fold <= 0.001f) return;
            float w = Mathf.SmoothStep(0f, 1f, fold);
            // Girar hacia una dirección fija no se acumula aunque el Animator no reescriba los huesos un fotograma
            Fold(leftInner, leftMid, leftTip, 1f, w);
            Fold(rightInner, rightMid, rightTip, -1f, w);
        }

        private void Fold(Transform inner, Transform mid, Transform tip, float side, float w)
        {
            if (inner == null || mid == null) return;
            Aim(inner, mid.position, transform.TransformDirection(new Vector3(UpperWing.x * side, UpperWing.y, UpperWing.z)), w);
            if (tip != null) Aim(mid, tip.position, transform.TransformDirection(new Vector3(Hand.x * side, Hand.y, Hand.z)), w);
        }

        private static void Aim(Transform bone, Vector3 childPos, Vector3 dir, float w)
        {
            Vector3 now = childPos - bone.position;
            if (now.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, dir), w) * bone.rotation;
        }
    }
}
