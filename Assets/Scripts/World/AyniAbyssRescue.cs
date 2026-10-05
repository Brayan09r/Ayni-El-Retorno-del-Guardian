using System;
using System.Collections.Generic;
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;

namespace Ayni.World
{
    /// <summary>
    /// Las quebradas matan: si Yari cae al vacío muere en la caída y el Illa lo resucita en el último suelo
    /// firme que pisó, a cambio de años (YariCombatController.FallToDeath). Los rivales que caen vuelven sin más
    /// a su último suelo firme, para que nadie quede atrapado bajo el agua.
    /// La herramienta de entorno (Ayni > Entorno) lo añade al objeto "Ayni_Entorno".
    /// </summary>
    public class AyniAbyssRescue : MonoBehaviour
    {
        [Tooltip("Por debajo de esta altura (mundo) un rival se considera caído a la quebrada y vuelve arriba.")]
        [SerializeField] private float abyssY = -8f;
        [Tooltip("Por debajo de esta altura Yari ya no tiene salvación: empieza la caída mortal. Solo el interior de las quebradas baja de aquí.")]
        [SerializeField] private float playerDeathY = -3f;
        [Tooltip("Altura del agua del fondo de la garganta.")]
        [SerializeField] private float waterY = -18f;
        [Tooltip("Solo se recuerdan como suelo firme las posiciones por encima de esta altura.")]
        [SerializeField] private float safeMinY = 0.5f;
        [Tooltip("Pendiente máxima (grados) del suelo para recordarlo como firme: las paredes de la quebrada no cuentan.")]
        [SerializeField] private float maxSafeSlope = 30f;
        [SerializeField] private float sampleInterval = 0.3f;

        /// <summary>Se lanza cuando alguien es devuelto al camino (para penalizaciones o efectos futuros).</summary>
        public static event Action<Transform> OnRescued;

        private const int HistorySize = 6;

        private class Tracked
        {
            public Transform transform;
            public CharacterController controller;
            public YariCombatController yari;
            public readonly Vector3[] history = new Vector3[HistorySize];
            public int count;
            public float timer;
        }

        private readonly List<Tracked> tracked = new List<Tracked>();
        private float refreshTimer;

        public void Configure(float newAbyssY, float newSafeMinY)
        {
            abyssY = newAbyssY;
            safeMinY = newSafeMinY;
        }

        private void Start()
        {
            RefreshTracked();
        }

        private void RefreshTracked()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) Track(player.transform);

            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null) Track(enemies[i].transform);
            }

            tracked.RemoveAll(t => t.transform == null);
        }

        private void Track(Transform target)
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                if (tracked[i].transform == target) return;
            }

            var entry = new Tracked
            {
                transform = target,
                controller = target.GetComponent<CharacterController>(),
                yari = target.GetComponent<YariCombatController>()
            };
            Push(entry, target.position);
            tracked.Add(entry);
        }

        private static void Push(Tracked entry, Vector3 position)
        {
            // history[0] es la más reciente
            for (int i = HistorySize - 1; i > 0; i--) entry.history[i] = entry.history[i - 1];
            entry.history[0] = position;
            entry.count = Mathf.Min(entry.count + 1, HistorySize);
        }

        private void Update()
        {
            // Durante el prólogo Yari cae a la garganta a propósito
            if (AyniGameState.CinematicPlaying) return;

            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 1f;
                RefreshTracked();
            }

            for (int i = 0; i < tracked.Count; i++)
            {
                Tracked entry = tracked[i];
                if (entry.transform == null) continue;
                if (entry.controller == null) entry.controller = entry.transform.GetComponent<CharacterController>();

                Vector3 position = entry.transform.position;

                if (position.y < (entry.yari != null ? playerDeathY : abyssY))
                {
                    Rescue(entry);
                    continue;
                }

                entry.timer -= Time.deltaTime;
                bool grounded = entry.controller == null || entry.controller.isGrounded;
                if (entry.timer <= 0f && grounded && position.y > safeMinY)
                {
                    entry.timer = sampleInterval;
                    if (IsFirmGround(position)) Push(entry, position);
                }
            }
        }

        /// <summary>
        /// Suelo casi llano bajo los pies y también a metro y medio alrededor: ni una pared, ni una cornisa,
        /// ni el borde mismo del precipicio (ahí se volvería a caer nada más regresar).
        /// </summary>
        private bool IsFirmGround(Vector3 position)
        {
            if (!GroundBelow(position, out RaycastHit center)) return false;
            if (Vector3.Angle(center.normal, Vector3.up) > maxSafeSlope) return false;

            const float margin = 1.6f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = i == 0 ? Vector3.forward : i == 1 ? Vector3.back : i == 2 ? Vector3.right : Vector3.left;
                if (!GroundBelow(position + offset * margin, out RaycastHit around)) return false;
                if (Mathf.Abs(around.point.y - center.point.y) > 0.9f) return false;
            }
            return true;
        }

        private static bool GroundBelow(Vector3 position, out RaycastHit hit)
        {
            return Physics.Raycast(position + Vector3.up * 0.6f, Vector3.down, out hit, 2.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private void Rescue(Tracked entry)
        {
            // La posición más antigua guardada: aproximadamente un segundo y medio antes de caer
            Vector3 target = entry.history[Mathf.Max(0, entry.count - 1)] + Vector3.up * 0.15f;

            // Yari: la caída es mortal. Su propia escena lo ve caer y el Illa lo resucita en el suelo firme
            YariCombatController yari = entry.yari;
            if (yari != null)
            {
                if (yari.IsBeingRescued) return;
                float rimY = Mathf.Max(entry.history[0].y, target.y);
                yari.FallToDeath(target, rimY, waterY);
                for (int i = 0; i < HistorySize; i++) entry.history[i] = target;
                entry.count = HistorySize;
                entry.timer = sampleInterval;
                OnRescued?.Invoke(entry.transform);
                return;
            }

            bool wasEnabled = entry.controller != null && entry.controller.enabled;
            if (entry.controller != null) entry.controller.enabled = false;
            entry.transform.position = target;
            if (entry.controller != null) entry.controller.enabled = wasEnabled;

            // Que no vuelva a caer por el mismo punto: todo el historial apunta al lugar seguro
            for (int i = 0; i < HistorySize; i++) entry.history[i] = target;
            entry.count = HistorySize;
            entry.timer = sampleInterval;
            OnRescued?.Invoke(entry.transform);
        }
    }
}
