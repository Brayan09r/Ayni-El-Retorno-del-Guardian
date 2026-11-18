using UnityEngine;
using Ayni.Core;
using Ayni.Fauna;
using Ayni.Player;
using Ayni.UI;

namespace Ayni.World
{
    /// <summary>
    /// Límite del nivel 1: un pasillo alrededor del Qhapaq Ñan, del inicio del camino al puente. Dentro hay sitio de
    /// sobra para pelear, explorar las kanchas y ver la fauna; fuera, el terreno sigue cientos de metros hasta el borde
    /// del mundo, donde Yari caía al vacío. Detrás del punto de partida solo se puede retroceder unos metros.
    /// Al acercarse al borde sale un aviso y, si Yari insiste, el puma más cercano baja a cortarle el paso.
    /// Las quebradas junto al camino quedan dentro: caer en ellas sigue siendo mortal a propósito (AyniAbyssRescue).
    /// SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniLevelBounds : MonoBehaviour
    {
        // Eje del camino (x, z) y medio ancho del pasillo en cada punto. Sale de las kanchas (K1-K4), los obstáculos
        // (O1-O8) y la plaza de Amaru de AyniVillageBuilder; el primer punto queda unos metros detrás del inicio.
        private static readonly Vector3[] Route =
        {
            new Vector3(240.0f, 539.5f, 8f),   // detrás del punto de partida (247.9, 539.5)
            new Vector3(250.0f, 539.5f, 16f),
            new Vector3(283.0f, 539.7f, 40f),  // O1
            new Vector3(294.5f, 506.0f, 44f),  // O2
            new Vector3(330.9f, 468.3f, 44f),  // K1 Puesto de Chasquis
            new Vector3(360.3f, 441.7f, 40f),  // O3
            new Vector3(368.7f, 414.5f, 40f),  // O4
            new Vector3(392.1f, 387.7f, 44f),  // K2 Kancha de los Tejedores
            new Vector3(429.7f, 384.5f, 40f),  // O5
            new Vector3(450.4f, 386.0f, 40f),  // O6
            new Vector3(477.9f, 433.9f, 44f),  // K3 Tambo del Camino
            new Vector3(484.6f, 475.6f, 40f),  // O7
            new Vector3(497.9f, 507.0f, 40f),  // O8
            new Vector3(556.1f, 519.1f, 44f),  // K4 Portada del Cazador
            new Vector3(614.0f, 477.0f, 40f),  // plaza de Amaru
            new Vector3(640.0f, 467.0f, 30f),  // puente colgante
            new Vector3(675.0f, 466.0f, 16f),  // otra orilla
        };

        [Tooltip("A esta distancia del borde aparece el aviso.")]
        [SerializeField] private float warnMargin = 5f;
        [Tooltip("Segundos empujando el borde antes de que salga un puma.")]
        [SerializeField] private float pumaAfter = 1.5f;

        private YariCombatController yari;
        private CharacterController mover;
        private float pressingSince = -1f;
        private bool hintShown;

        private void Update()
        {
            if (yari == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                yari = player.GetComponent<YariCombatController>();
                mover = player.GetComponent<CharacterController>();
                if (yari == null || mover == null) return;
            }
            if (AyniGameState.CinematicPlaying || yari.CinematicControl || yari.IsDead) return;

            Vector3 p = yari.transform.position;
            Nearest(new Vector2(p.x, p.z), out Vector2 axis, out float halfWidth);
            Vector2 off = new Vector2(p.x, p.z) - axis;
            float distance = off.magnitude;

            if (distance > halfWidth && distance > 0.001f)
            {
                // Fuera del pasillo: vuelve al borde (como una pared invisible que no frena el resto del movimiento)
                Vector2 back = off / distance * (halfWidth - distance);
                if (mover.enabled) mover.Move(new Vector3(back.x, 0f, back.y));
                if (pressingSince < 0f) pressingSince = Time.time;
                if (Time.time - pressingSince > pumaAfter) AyniPumaPack.WarnAtBorder(p);
            }
            else
            {
                pressingSince = -1f;
            }

            bool near = distance > halfWidth - warnMargin;
            if (near != hintShown)
            {
                hintShown = near;
                AyniScreenFX.Hint(near ? "Más allá del Qhapaq Ñan rondan los pumas del cerro. Vuelve al camino." : null);
            }
        }

        /// <summary>Punto del eje del camino más cercano a <paramref name="p"/> y el medio ancho del pasillo ahí.</summary>
        public static void Nearest(Vector2 p, out Vector2 point, out float halfWidth)
        {
            float best = float.MaxValue;
            point = new Vector2(Route[0].x, Route[0].y);
            halfWidth = Route[0].z;
            for (int i = 0; i < Route.Length - 1; i++)
            {
                Vector2 a = new Vector2(Route[i].x, Route[i].y), b = new Vector2(Route[i + 1].x, Route[i + 1].y);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                Vector2 q = a + ab * t;
                float d = (p - q).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                point = q;
                halfWidth = Mathf.Lerp(Route[i].z, Route[i + 1].z, t);
            }
        }
    }
}
