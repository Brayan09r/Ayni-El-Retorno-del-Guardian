#if UNITY_EDITOR
using UnityEngine;
using Ayni.Core;
using Ayni.Enemy;
using Ayni.Player;

namespace Ayni.Editor
{
    /// <summary>
    /// Sondas para pruebas automáticas en Play desde el puente de agentes (AyniAgentBridge):
    ///   call Ayni.Editor.AyniPlaytestProbe.Report etiqueta
    ///   call Ayni.Editor.AyniPlaytestProbe.TeleportYari x y z
    /// Junto con AyniInput.Simulate / SimulateMove permiten probar la guardia, las esquivas y las caídas sin mando.
    /// </summary>
    public static class AyniPlaytestProbe
    {
        private static readonly string[] KnownStates =
        {
            "Relaxed_Locomotion", "Combat_Locomotion", "Crouch_Locomotion", "LockOn_Locomotion", "Guard_Stance",
            "Avoid_Duck", "Avoid_Jump", "Avoid_SwayL", "Avoid_SwayR", "Sifu_DuckAvoid", "Sifu_JumpAvoid",
            "Fall_Loop", "Fall_Back", "Land_Hard", "Jump", "GetUp", "Defeat_Death", "Impact_Hit", "Impact_HitBody",
            "Impact_HitHeavy", "Guard_BlockHit", "Parry_Deflect", "Mercy_Offer",
            "Atk_Light1", "Atk_Light2", "Atk_Light3", "Atk_Light4", "Atk_Overhand", "Atk_Uppercut", "Atk_Elbow",
            "Atk_Headbutt", "Atk_FrontKick", "BrokenStructure_Stunned"
        };

        public static string Report(string label)
        {
            if (!Application.isPlaying) return "no está en Play";
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return "sin Yari";

            var yari = player.GetComponent<YariCombatController>();
            var animator = player.GetComponentInChildren<Animator>();
            string state = StateName(animator);
            Vector3 p = player.transform.position;

            string text = $"[{label}] Yari pos=({p.x:F2}, {p.y:F2}, {p.z:F2}) rotY={player.transform.eulerAngles.y:F0} estado={state} " +
                          $"guardia={yari.IsGuarding} cayendo={yari.IsFalling} vida={yari.CurrentHealth:F0}/{yari.MaxHealth:F0} " +
                          $"timeScale={Time.timeScale:F2} entradaBloqueada={AyniGameState.InputLocked} mando={AyniInput.UsingGamepad}";

            foreach (var enemy in EnemyController.All)
            {
                if (enemy == null) continue;
                float dist = Vector3.Distance(enemy.transform.position, p);
                text += $"\n      {enemy.CharacterName}: estado={enemy.State} dist={dist:F1} vida={enemy.CurrentHealth:F0} postura={enemy.Structure.CurrentStructure:F0}";
            }
            Debug.Log("[Ayni Prueba] " + text);
            return state;
        }

        public static void TeleportYari(float x, float y, float z)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var mover = player.GetComponent<CharacterController>();
            if (mover != null) mover.enabled = false;
            player.transform.position = new Vector3(x, y, z);
            if (mover != null) mover.enabled = true;
            Debug.Log($"[Ayni Prueba] Yari teletransportado a ({x}, {y}, {z})");
        }

        /// <summary>Golpea al jefe con el daño indicado (vida y postura) como si fuera un golpe pesado de Yari.</summary>
        public static void HitBoss(float health, float structure)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            foreach (var enemy in EnemyController.All)
            {
                if (enemy == null || !enemy.IsBoss) continue;
                Vector3 from = player != null ? player.transform.position : enemy.transform.position + Vector3.forward;
                enemy.TakeHit(health, structure, from, true, Ayni.Combat.HitReaction.Heavy, 0f);
                Debug.Log($"[Ayni Prueba] Golpe de prueba a {enemy.CharacterName}: vida {enemy.CurrentHealth:F0} ({enemy.HealthRatio:P0}), postura rota={enemy.Structure.IsBroken}, juicio={enemy.CanBeJudged}");
            }
        }

        private static string StateName(Animator animator)
        {
            if (animator == null) return "-";
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            foreach (string s in KnownStates)
            {
                if (info.IsName(s)) return s + $"({info.normalizedTime:F2})";
            }
            return "?";
        }
    }
}
#endif
