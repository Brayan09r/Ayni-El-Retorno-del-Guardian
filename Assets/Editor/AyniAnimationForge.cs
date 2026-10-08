#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ayni.Combat;

namespace Ayni.Editor
{
    /// <summary>
    /// Fragua de animaciones: crea clips Humanoid nuevos para Yari a partir de las poses de sus clips de Mixamo
    /// (guardia, agachado, salto, golpe recibido...) y de movimientos procedurales sobre los músculos del Avatar.
    ///
    ///   Yari_Avoid_Duck / Jump / SwayL / SwayR  Esquivas en el sitio estilo Sifu (los pies no se despegan del lugar)
    ///   Yari_Fall_Loop                          Caída en el aire (bucle): brazos y piernas buscando equilibrio
    ///   Yari_Fall_Back                          Caída de espaldas al vacío tras un golpe (prólogo del puente)
    ///   Yari_Land_Hard                          Aterrizaje pesado tras una caída alta
    ///
    /// Todos llevan el desplazamiento "horneado en la pose": no mueven al personaje, solo su cuerpo.
    /// Se guardan en Assets/Art/Characters/Animations/Generadas y se pueden regenerar cuando se quiera.
    /// Las hojas de fotogramas (para revisar cómo quedan) se guardan en DebugCaptures/forja_*.png.
    /// </summary>
    public static class AyniAnimationForge
    {
        public const string OutFolder = "Assets/Art/Characters/Animations/Generadas";
        private const string RigPath = "Assets/Art/Characters/Yari_Rigged.fbx";
        private const string AnimFolder = "Assets/Art/Characters/Animations";
        private const string MaterialPath = "Assets/Art/Characters/M_Yari_PBR.mat";
        private const float Fps = 30f;

        public const string AvoidDuck = "Yari_Avoid_Duck";
        public const string AvoidJump = "Yari_Avoid_Jump";
        public const string AvoidSwayL = "Yari_Avoid_SwayL";
        public const string AvoidSwayR = "Yari_Avoid_SwayR";
        public const string FallLoop = "Yari_Fall_Loop";
        public const string FallBack = "Yari_Fall_Back";
        public const string LandHard = "Yari_Land_Hard";
        public const string BackKick = "Yari_Back_Kick";
        /// <summary>Grito de guerra de un jefe al empezar su segunda fase (sirve para cualquier rig Humanoid).</summary>
        public const string Roar = "Gen_Roar";
        /// <summary>Segundo del clip de patada hacia atrás en el que la pierna llega a su extensión (impacto).</summary>
        public const float BackKickContact = 0.22f;

        // ───────────────────────── Pose ─────────────────────────

        private struct Pose
        {
            public Vector3 pos;
            public Quaternion rot;
            public float[] m;

            public Pose Clone()
            {
                return new Pose { pos = pos, rot = rot, m = (float[])m.Clone() };
            }

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                var r = new Pose
                {
                    pos = Vector3.LerpUnclamped(a.pos, b.pos, t),
                    rot = Quaternion.SlerpUnclamped(a.rot, b.rot, t),
                    m = new float[a.m.Length]
                };
                for (int i = 0; i < r.m.Length; i++) r.m[i] = Mathf.LerpUnclamped(a.m[i], b.m[i], t);
                return r;
            }
        }

        private static Dictionary<string, int> muscleIndex;
        private static string[] muscleProperty;
        private static readonly Dictionary<string, float> intentSign = new Dictionary<string, float>();

        private static int M(string name)
        {
            if (muscleIndex == null)
            {
                muscleIndex = new Dictionary<string, int>();
                for (int i = 0; i < HumanTrait.MuscleCount; i++) muscleIndex[HumanTrait.MuscleName[i]] = i;
            }
            if (!muscleIndex.TryGetValue(name, out int index)) throw new ArgumentException("Músculo desconocido: " + name);
            return index;
        }

        /// <summary>Suma <paramref name="amount"/> al músculo en el sentido calibrado (ver Calibrate): + = la intención descrita.</summary>
        private static void Nudge(ref Pose p, string muscle, float amount)
        {
            float sign = intentSign.TryGetValue(muscle, out float s) ? s : 1f;
            int i = M(muscle);
            p.m[i] = Mathf.Clamp(p.m[i] + amount * sign, -1f, 1f);
        }

        /// <summary>Lo mismo para los dos lados: "Arm Down-Up" afecta a "Left Arm Down-Up" y "Right Arm Down-Up".</summary>
        private static void NudgeBoth(ref Pose p, string muscle, float amount)
        {
            Nudge(ref p, "Left " + muscle, amount);
            Nudge(ref p, "Right " + muscle, amount);
        }

        private static void CopyRange(ref Pose to, Pose from, string firstMuscle, string lastMuscle)
        {
            int a = M(firstMuscle), b = M(lastMuscle);
            for (int i = a; i <= b; i++) to.m[i] = from.m[i];
        }

        private static void CopyLegs(ref Pose to, Pose from)
        {
            CopyRange(ref to, from, "Left Upper Leg Front-Back", "Right Toes Up-Down");
        }

        private static void CopyArms(ref Pose to, Pose from)
        {
            CopyRange(ref to, from, "Left Shoulder Down-Up", "Right Hand In-Out");
        }

        // ───────────────────────── Esqueleto de muestreo ─────────────────────────

        private class Rig : IDisposable
        {
            public readonly GameObject go;
            public readonly Animator anim;
            private readonly HumanPoseHandler handler;
            private HumanPose scratch;

            public Rig(Scene? scene = null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
                if (prefab == null) throw new Exception("No se encontró " + RigPath);

                go = UnityEngine.Object.Instantiate(prefab);
                go.hideFlags = HideFlags.HideAndDontSave;
                if (scene.HasValue) SceneManager.MoveGameObjectToScene(go, scene.Value);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                if (anim.avatar == null)
                {
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RigPath))
                    {
                        if (asset is Avatar av) { anim.avatar = av; break; }
                    }
                }
                if (anim.avatar == null || !anim.avatar.isHuman) throw new Exception("Yari_Rigged no tiene Avatar Humanoid.");
                anim.applyRootMotion = false;
                handler = new HumanPoseHandler(anim.avatar, go.transform);
            }

            public Pose Sample(AnimationClip clip, float time, bool pinHorizontal = true)
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, clip, Mathf.Clamp(time, 0f, clip.length));
                AnimationMode.EndSampling();
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return Read(pinHorizontal);
            }

            public Pose Read(bool pinHorizontal = true)
            {
                handler.GetHumanPose(ref scratch);
                var p = new Pose { pos = scratch.bodyPosition, rot = scratch.bodyRotation, m = (float[])scratch.muscles.Clone() };
                if (pinHorizontal)
                {
                    p.pos.x = 0f;
                    p.pos.z = 0f;
                    // Quita el giro sobre el eje vertical (algunos clips de Mixamo terminan girados)
                    Vector3 fwd = p.rot * Vector3.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 0.0001f)
                    {
                        float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                        p.rot = Quaternion.Euler(0f, -yaw, 0f) * p.rot;
                    }
                }
                return p;
            }

            private AnimationClip probe;

            /// <summary>
            /// Pone el esqueleto en la pose indicada. Se hace a través de un clip temporal de un fotograma, igual que lo
            /// reproducirá el Animator en el juego (SetHumanPose no se lleva bien con el modo de muestreo del editor).
            /// </summary>
            public void Apply(Pose p)
            {
                if (probe == null) probe = new AnimationClip { frameRate = Fps, hideFlags = HideFlags.HideAndDontSave };
                FillClip(probe, _ => p, 1f / Fps, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, probe, 0f);
                AnimationMode.EndSampling();
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            public Vector3 Bone(HumanBodyBones bone)
            {
                Transform t = anim.GetBoneTransform(bone);
                return t != null ? go.transform.InverseTransformPoint(t.position) : Vector3.zero;
            }

            public Vector3 HeadTop()
            {
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) return Vector3.zero;
                foreach (Transform child in head.GetComponentsInChildren<Transform>())
                {
                    if (child.name.Contains("HeadTop")) return go.transform.InverseTransformPoint(child.position);
                }
                return go.transform.InverseTransformPoint(head.position + head.up * 0.15f);
            }

            public void Dispose()
            {
                handler?.Dispose();
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ───────────────────────── Menú ─────────────────────────

        /// <summary>
        /// Modo de muestreo limpio: si quedó uno abierto (por un error anterior), las poses se acumulan mal
        /// y las hojas de fotogramas salen torcidas. Por eso siempre se cierra y se abre de nuevo.
        /// </summary>
        private static void BeginAnimationMode()
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            AnimationMode.StartAnimationMode();
        }

        private static void EndAnimationMode()
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }

        [MenuItem("Ayni/Animaciones/1. Generar Caídas y Esquivas (Sifu)")]
        public static void GenerateAll()
        {
            Generate(true);
        }

        public static bool Generate(bool upgradeAnimator)
        {
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(AnimFolder, "Generadas");

            BeginAnimationMode();
            var report = new System.Text.StringBuilder();
            try
            {
                using (var rig = new Rig())
                {
                    AnimationClip guardClip = LoadClip("Guard_Idle");
                    AnimationClip combatClip = LoadClip("Combat_Idle");
                    AnimationClip crouchClip = LoadClip("Crouch_Idle");
                    AnimationClip hitHeavyClip = LoadClip("Hit_Heavy");
                    if (guardClip == null || combatClip == null || crouchClip == null)
                    {
                        Debug.LogError("[Ayni Forja] Faltan clips base (Guard_Idle, Combat_Idle o Crouch_Idle).");
                        return false;
                    }

                    BuildPropertyNames(guardClip);

                    Pose guard = rig.Sample(guardClip, Mathf.Min(0.4f, guardClip.length));
                    Pose combat = rig.Sample(combatClip, Mathf.Min(0.3f, combatClip.length));
                    Pose crouch = rig.Sample(crouchClip, Mathf.Min(0.5f, crouchClip.length));

                    // Relación entre las unidades del Avatar y los metros (para alturas de salto y caída)
                    rig.Apply(guard);
                    float hipsMeters = rig.Bone(HumanBodyBones.Hips).y;
                    float metersPerUnit = guard.pos.y > 0.01f ? hipsMeters / guard.pos.y : 1f;
                    report.AppendLine($"   Guardia: cuerpo y = {guard.pos.y:F3} u, cadera a {hipsMeters:F2} m → {metersPerUnit:F3} m por unidad");

                    Calibrate(rig, guard, report);

                    BuildAvoidDuck(guard, crouch, report);
                    BuildAvoidJump(guard, crouch, report);
                    BuildAvoidSway(rig, guard, crouch, true, report);
                    BuildAvoidSway(rig, guard, crouch, false, report);
                    Pose fallStart = BuildFallLoop(combat, report);
                    BuildFallBack(rig, combat, hitHeavyClip, report);
                    BuildLandHard(fallStart, crouch, combat, metersPerUnit, report);
                    BuildBackKick(rig, guard, crouch, metersPerUnit, report);
                    BuildRoar(rig, combat, crouch, report);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Ayni Forja] No se pudieron generar las animaciones: " + e);
                return false;
            }
            finally
            {
                EndAnimationMode();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[Ayni Forja]</color> Animaciones generadas en " + OutFolder + ":\n" + report);

            if (upgradeAnimator) AyniAnimatorUpgrade.Apply();
            return true;
        }

        [MenuItem("Ayni/Animaciones/2. Hojas de Fotogramas de las Animaciones Generadas")]
        public static void RenderSheets()
        {
            RenderSheet(AvoidDuck, 6);
            RenderSheet(AvoidJump, 6);
            RenderSheet(AvoidSwayL, 5);
            RenderSheet(FallLoop, 6);
            RenderSheet(FallBack, 7);
            RenderSheet(LandHard, 6);
            RenderSheet(BackKick, 7);
            RenderSheet(Roar, 7);
        }

        // ───────────────────────── Recetas ─────────────────────────
        // Valores en el "sentido calibrado" (ver Calibrate), medidos con el laboratorio de poses sobre el rig de Yari:
        //   Brazo  Down-Up 0.6 + Front-Back -0.6 + Forearm 0.6 = brazos abiertos en cruz · Down-Up 0.9 = brazos arriba en V
        //          Front-Back +0.6..0.8 = brazos hacia delante
        //   Pierna Upper Leg Front-Back -0.5 + Lower Leg Stretch 0.5 = de pie · 1 / -1 = recogida bajo el cuerpo
        //   Al inclinar la columna hacia un lado la cadera gira al contrario: se compensa con Roll (≈ -12° por cada lado +).

        private static void BuildAvoidDuck(Pose guard, Pose crouch, System.Text.StringBuilder report)
        {
            // Amago rápido al estilo Sifu: media flexión (no sentadilla), el tronco baja hacia delante y un poco
            // de lado, la guardia sigue arriba y la mirada no se separa del rival
            Pose legs = Pose.Lerp(guard, crouch, 0.6f);
            Pose low = guard.Clone();
            CopyLegs(ref low, legs);
            low.pos.y = legs.pos.y;
            Nudge(ref low, "Spine Front-Back", 0.45f);
            Nudge(ref low, "Chest Front-Back", 0.3f);
            Nudge(ref low, "UpperChest Front-Back", 0.2f);
            Nudge(ref low, "Spine Left-Right", -0.18f);
            Nudge(ref low, "Head Nod Down-Up", -0.4f);
            low.rot = Quaternion.Euler(16f, 0f, 0f) * guard.rot;

            Pose lowHold = Pose.Lerp(low, guard, 0.12f);

            var keys = new List<(float, Pose)>
            {
                (0f, guard), (0.07f, low), (0.22f, lowHold), (0.44f, guard)
            };
            Save(AvoidDuck, Keyed(keys), 0.44f, false);
            report.AppendLine($"   {AvoidDuck}: 0.44 s (amago rápido bajo un golpe alto)");
        }

        private static void BuildAvoidJump(Pose guard, Pose crouch, System.Text.StringBuilder report)
        {
            Pose prep = guard.Clone();
            CopyLegs(ref prep, Pose.Lerp(guard, crouch, 0.4f));
            prep.pos.y = Mathf.Lerp(guard.pos.y, crouch.pos.y, 0.4f);

            // Piernas recogidas bajo el cuerpo para pasar por encima del barrido
            Pose air = guard.Clone();
            SetBoth(ref air, "Upper Leg Front-Back", 0.95f);
            SetBoth(ref air, "Lower Leg Stretch", -0.95f);
            Nudge(ref air, "Spine Front-Back", 0.15f);
            air.pos.y = guard.pos.y + 0.33f;

            Pose airLate = air.Clone();
            airLate.pos.y = guard.pos.y + 0.24f;
            SetBoth(ref airLate, "Upper Leg Front-Back", 0.6f);
            SetBoth(ref airLate, "Lower Leg Stretch", -0.6f);

            var keys = new List<(float, Pose)>
            {
                (0f, guard), (0.07f, prep), (0.19f, air), (0.33f, airLate), (0.44f, prep), (0.62f, guard)
            };
            Save(AvoidJump, Keyed(keys), 0.62f, false);
            report.AppendLine($"   {AvoidJump}: 0.62 s (salto corto con las piernas recogidas sobre el barrido)");
        }

        /// <summary>
        /// Balanceo lateral a partir de la esquiva real de Mixamo (Dodge_Left / Dodge_Right): se toma solo el tramo
        /// central de la esquiva (rodillas que se flexionan, tronco que se inclina y gira, la cabeza sale de la línea
        /// del golpe) acelerado a 0.46 s, con los brazos en guardia y fundido con la guardia al entrar y al salir.
        /// Si falta el clip de Mixamo, se usa la versión procedural.
        /// </summary>
        private static void BuildAvoidSway(Rig rig, Pose guard, Pose crouch, bool left, System.Text.StringBuilder report)
        {
            string name = left ? AvoidSwayL : AvoidSwayR;
            AnimationClip mocap = LoadClip(left ? "Dodge_Left" : "Dodge_Right");
            if (mocap == null)
            {
                BuildAvoidSwayProcedural(rig, guard, crouch, left, report);
                return;
            }

            // Instante de máxima inclinación: donde la cabeza más se separa de su posición inicial
            const float step = 1f / Fps;
            rig.Sample(mocap, 0f, false);
            float headStart = rig.Bone(HumanBodyBones.Head).x;
            float peakTime = mocap.length * 0.4f, peak = 0f;
            for (float t = 0f; t <= mocap.length; t += step)
            {
                rig.Sample(mocap, t, false);
                float d = Mathf.Abs(rig.Bone(HumanBodyBones.Head).x - headStart);
                if (d > peak)
                {
                    peak = d;
                    peakTime = t;
                }
            }

            float from = Mathf.Max(0f, peakTime - 0.3f);
            float to = Mathf.Min(mocap.length, peakTime + 0.3f);
            const float length = 0.46f;

            // Referencia: la pose al empezar el tramo (se le quita el giro y la posición horizontal)
            Pose start = rig.Sample(mocap, from, false);
            Vector3 fwd = start.rot * Vector3.forward;
            fwd.y = 0f;
            float yaw = fwd.sqrMagnitude > 0.0001f ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg : 0f;
            Quaternion unYaw = Quaternion.Euler(0f, -yaw, 0f);
            Vector3 origin = start.pos;

            Pose Evaluate(float t)
            {
                float u = Mathf.Clamp01(t / length);
                Pose m = rig.Sample(mocap, Mathf.Lerp(from, to, u), false);
                Vector3 offset = unYaw * (m.pos - origin);
                m.pos = new Vector3(offset.x, m.pos.y, offset.z);
                m.rot = unYaw * m.rot;
                // La guardia no baja durante la esquiva: brazos de la guardia con un poco del movimiento real,
                // que equilibra el cuerpo (sin él, al echarse atrás los brazos quedaban demasiado altos)
                Pose arms = Pose.Lerp(guard, m, 0.35f);
                CopyArms(ref m, arms);

                // Entra rápido desde la guardia y vuelve a ella al final; 85 % de la amplitud de la captura
                float w = 0.85f * Smooth(Mathf.InverseLerp(0f, 0.16f, u)) * (1f - Smooth(Mathf.InverseLerp(0.72f, 1f, u)));
                return Pose.Lerp(guard, m, w);
            }

            Save(name, Evaluate, length, false);
            rig.Sample(mocap, peakTime, false);
            report.AppendLine($"   {name}: {length:F2} s (balanceo desde la esquiva de Mixamo, tramo {from:F2}–{to:F2} s; la cabeza se aparta {peak:F2} m)");
        }

        private static void BuildAvoidSwayProcedural(Rig rig, Pose guard, Pose crouch, bool left, System.Text.StringBuilder report)
        {
            // Balanceo marcado del tronco hacia un lado, con las rodillas flexionadas y los pies plantados:
            // la cabeza sale de la línea del golpe
            float side = left ? -1f : 1f; // + = hacia la derecha del personaje
            Pose legs = Pose.Lerp(guard, crouch, 0.35f);
            Pose lean = guard.Clone();
            CopyLegs(ref lean, legs);
            lean.pos.y = legs.pos.y;
            Nudge(ref lean, "Spine Left-Right", 0.8f * side);
            Nudge(ref lean, "Chest Left-Right", 0.8f * side);
            Nudge(ref lean, "UpperChest Left-Right", 0.75f * side);
            Nudge(ref lean, "Head Tilt Left-Right", -0.25f * side);
            Nudge(ref lean, "Spine Front-Back", 0.2f);
            Nudge(ref lean, "Chest Front-Back", 0.1f);
            lean.rot = Quaternion.Euler(6f, 0f, -18f * side) * guard.rot;

            rig.Apply(guard);
            float headBase = rig.Bone(HumanBodyBones.Head).x;
            rig.Apply(lean);
            float headShift = rig.Bone(HumanBodyBones.Head).x - headBase;

            var keys = new List<(float, Pose)>
            {
                (0f, guard), (0.08f, lean), (0.22f, Pose.Lerp(lean, guard, 0.08f)), (0.42f, guard)
            };
            string name = left ? AvoidSwayL : AvoidSwayR;
            Save(name, Keyed(keys), 0.42f, false);
            report.AppendLine($"   {name}: 0.42 s (balanceo lateral; la cabeza se desplaza {headShift:+0.00;-0.00} m, + = derecha)");
        }

        /// <summary>Caída en el aire: brazos abiertos que buscan el equilibrio y piernas que pedalean. Devuelve la pose del primer fotograma.</summary>
        private static Pose BuildFallLoop(Pose combat, System.Text.StringBuilder report)
        {
            const float length = 1.0f;
            Pose Evaluate(float t)
            {
                float w1 = 2f * Mathf.PI * t / length;       // un ciclo por clip
                float w2 = 2f * w1;                          // dos ciclos por clip
                Pose p = combat.Clone();

                foreach (string side in new[] { "Left", "Right" })
                {
                    float ph = side == "Left" ? 0f : 2.2f;
                    SetIntent(ref p, side + " Arm Down-Up", 0.72f + 0.16f * Mathf.Sin(w2 + ph));
                    SetIntent(ref p, side + " Arm Front-Back", -0.3f + 0.35f * Mathf.Sin(w1 + ph));
                    SetIntent(ref p, side + " Forearm Stretch", 0.62f + 0.18f * Mathf.Sin(w2 + ph + 1f));
                    SetIntent(ref p, side + " Shoulder Down-Up", 0.3f);

                    float lp = side == "Left" ? 0f : Mathf.PI;
                    SetIntent(ref p, side + " Upper Leg Front-Back", -0.15f + 0.45f * Mathf.Sin(w2 + lp));
                    SetIntent(ref p, side + " Lower Leg Stretch", -0.1f - 0.45f * (0.5f + 0.5f * Mathf.Sin(w2 + lp + Mathf.PI * 0.5f)));
                    SetIntent(ref p, side + " Upper Leg In-Out", 0.2f);
                }

                Nudge(ref p, "Spine Front-Back", 0.1f);
                Nudge(ref p, "Head Nod Down-Up", 0.2f);
                p.rot = Quaternion.Euler(10f + 3f * Mathf.Sin(w1), 0f, 4f * Mathf.Sin(w1 + 0.7f)) * combat.rot;
                return p;
            }

            Save(FallLoop, Evaluate, length, true);
            report.AppendLine($"   {FallLoop}: {length:F2} s en bucle (caída en el aire)");
            return Evaluate(0f);
        }

        /// <summary>Golpe que arroja a Yari de espaldas al vacío: retroceso del golpe, giro hacia atrás y brazos que buscan el puente.</summary>
        private static void BuildFallBack(Rig rig, Pose combat, AnimationClip hitHeavy, System.Text.StringBuilder report)
        {
            const float length = 2.6f;
            const float recoilEnd = 0.35f;

            // Retroceso tomado del clip real de golpe fuerte
            var recoil = new List<Pose>();
            int recoilFrames = Mathf.CeilToInt(recoilEnd * Fps) + 1;
            for (int i = 0; i < recoilFrames; i++)
            {
                float u = i / (float)(recoilFrames - 1);
                recoil.Add(hitHeavy != null ? rig.Sample(hitHeavy, Mathf.Lerp(0.05f, Mathf.Min(0.55f, hitHeavy.length), u)) : combat);
            }
            Pose recoilLast = recoil[recoil.Count - 1];

            // Brazos estirados hacia delante y arriba (hacia el puente que se aleja), piernas por delante, cuerpo encogido
            Pose reach = combat.Clone();
            SetBoth(ref reach, "Arm Down-Up", 0.75f);
            SetBoth(ref reach, "Arm Front-Back", 0.5f);
            SetBoth(ref reach, "Forearm Stretch", 0.85f);
            SetBoth(ref reach, "Upper Leg Front-Back", 0.35f);
            SetBoth(ref reach, "Lower Leg Stretch", -0.2f);
            SetBoth(ref reach, "Upper Leg In-Out", 0.15f);
            Nudge(ref reach, "Spine Front-Back", 0.35f);
            Nudge(ref reach, "Chest Front-Back", 0.2f);
            Nudge(ref reach, "Head Nod Down-Up", 0.3f);

            Pose Evaluate(float t)
            {
                if (t <= recoilEnd)
                {
                    float f = t / recoilEnd * (recoil.Count - 1);
                    int i = Mathf.Min(recoil.Count - 2, Mathf.FloorToInt(f));
                    return Pose.Lerp(recoil[i], recoil[i + 1], f - i);
                }

                float u = Mathf.InverseLerp(recoilEnd, 1.2f, t);
                Pose p = Pose.Lerp(recoilLast, reach, Smooth(u));

                // Después, los brazos y las piernas se agitan buscando de dónde agarrarse
                float flail = Mathf.InverseLerp(0.9f, 1.4f, t);
                float w = 2f * Mathf.PI * 1.6f * t;
                Nudge(ref p, "Left Arm Down-Up", 0.15f * flail * Mathf.Sin(w));
                Nudge(ref p, "Right Arm Down-Up", 0.15f * flail * Mathf.Sin(w + 2f));
                Nudge(ref p, "Left Arm Front-Back", -0.25f * flail * (0.5f + 0.5f * Mathf.Sin(w + 0.5f)));
                Nudge(ref p, "Right Arm Front-Back", -0.25f * flail * (0.5f + 0.5f * Mathf.Sin(w + 2.5f)));
                Nudge(ref p, "Left Upper Leg Front-Back", 0.2f * flail * Mathf.Sin(w * 0.8f));
                Nudge(ref p, "Right Upper Leg Front-Back", 0.2f * flail * Mathf.Sin(w * 0.8f + Mathf.PI));

                // El cuerpo gira de espaldas: primero rápido por el impacto y luego se frena
                float tilt = -80f * Smooth(Mathf.InverseLerp(recoilEnd, 1.3f, t)) - 15f * Mathf.InverseLerp(1.3f, length, t);
                p.rot = Quaternion.Euler(tilt, 0f, 6f * Mathf.Sin(t * 2.3f)) * recoilLast.rot;
                return p;
            }

            Save(FallBack, Evaluate, length, false);
            report.AppendLine($"   {FallBack}: {length:F2} s (golpe y caída de espaldas al vacío)");
        }

        private static void BuildLandHard(Pose fallStart, Pose crouch, Pose combat, float metersPerUnit, System.Text.StringBuilder report)
        {
            // Impacto: rodillas muy flexionadas, tronco hacia delante, mano derecha al suelo y brazo izquierdo abierto
            Pose impact = combat.Clone();
            CopyLegs(ref impact, crouch);
            impact.pos.y = crouch.pos.y - 0.06f / Mathf.Max(0.01f, metersPerUnit);
            Nudge(ref impact, "Spine Front-Back", 0.45f);
            Nudge(ref impact, "Chest Front-Back", 0.2f);
            Nudge(ref impact, "Head Nod Down-Up", 0.15f);
            impact.rot = Quaternion.Euler(14f, 0f, 0f) * combat.rot;
            SetIntent(ref impact, "Right Arm Down-Up", -0.65f);
            SetIntent(ref impact, "Right Arm Front-Back", 0.7f);
            SetIntent(ref impact, "Right Forearm Stretch", 1f);
            SetIntent(ref impact, "Left Arm Down-Up", 0.35f);
            SetIntent(ref impact, "Left Arm Front-Back", -0.6f);
            SetIntent(ref impact, "Left Forearm Stretch", 0.7f);

            Pose rise = Pose.Lerp(impact, combat, 0.2f);

            var keys = new List<(float, Pose)>
            {
                (0f, fallStart), (0.07f, impact), (0.42f, rise), (1.0f, combat)
            };
            Save(LandHard, Keyed(keys), 1.0f, false);
            report.AppendLine($"   {LandHard}: 1.00 s (aterrizaje pesado)");
        }

        /// <summary>
        /// Patada hacia atrás (estilo Sifu): sin girarse, Yari recoge la rodilla, inclina el tronco y lanza la pierna
        /// derecha recta hacia atrás mirando por encima del hombro; luego la recoge y vuelve a la guardia.
        /// </summary>
        private static void BuildBackKick(Rig rig, Pose guard, Pose crouch, float metersPerUnit, System.Text.StringBuilder report)
        {
            const float length = 0.72f;

            rig.Apply(guard);
            float groundY = Mathf.Min(rig.Bone(HumanBodyBones.LeftFoot).y, rig.Bone(HumanBodyBones.RightFoot).y);

            // Recoger: rodilla derecha arriba, tronco algo inclinado y la mirada buscando atrás
            Pose chamber = guard.Clone();
            chamber.rot = Quaternion.Euler(22f, 0f, 0f) * guard.rot;
            SetIntent(ref chamber, "Left Upper Leg Front-Back", -0.15f);   // compensa la inclinación: la pierna de apoyo sigue vertical
            SetIntent(ref chamber, "Left Lower Leg Stretch", 0.45f);
            SetIntent(ref chamber, "Right Upper Leg Front-Back", 0.45f);
            SetIntent(ref chamber, "Right Lower Leg Stretch", -0.95f);
            Nudge(ref chamber, "Spine Front-Back", 0.15f);
            Nudge(ref chamber, "Head Turn Left-Right", 0.45f);
            Nudge(ref chamber, "Neck Turn Left-Right", 0.3f);
            GroundFoot(rig, ref chamber, HumanBodyBones.LeftFoot, groundY, metersPerUnit);

            // Impacto: tronco casi horizontal y la pierna derecha recta hacia atrás, a la altura del pecho del rival.
            // La rotación del cuerpo se reparte entre cadera y columna: con la espalda arqueada, la cadera se inclina más
            // y la pierna sube más
            Pose extend = guard.Clone();
            extend.rot = Quaternion.Euler(66f, 0f, 0f) * guard.rot;
            SetIntent(ref extend, "Left Upper Leg Front-Back", 0.5f);
            SetIntent(ref extend, "Left Lower Leg Stretch", 0.6f);
            SetIntent(ref extend, "Right Upper Leg Front-Back", -1f);
            SetIntent(ref extend, "Right Lower Leg Stretch", 1f);
            SetIntent(ref extend, "Right Upper Leg In-Out", 0.08f);
            Nudge(ref extend, "Spine Front-Back", -0.25f);
            Nudge(ref extend, "Chest Front-Back", -0.1f);
            Nudge(ref extend, "Head Turn Left-Right", 0.8f);
            Nudge(ref extend, "Neck Turn Left-Right", 0.55f);
            Nudge(ref extend, "Head Nod Down-Up", -0.45f);
            Nudge(ref extend, "Chest Twist Left-Right", 0.2f);
            GroundFoot(rig, ref extend, HumanBodyBones.LeftFoot, groundY, metersPerUnit);

            Pose hold = Pose.Lerp(extend, chamber, 0.15f);

            var keys = new List<(float, Pose)>
            {
                (0f, guard), (0.10f, chamber), (BackKickContact, extend), (0.34f, hold), (0.47f, chamber), (length, guard)
            };
            Save(BackKick, Keyed(keys), length, false);

            rig.Apply(extend);
            Vector3 foot = rig.Bone(HumanBodyBones.RightFoot);
            report.AppendLine($"   {BackKick}: {length:F2} s (patada hacia atrás; en el impacto el pie llega a {-foot.z:F2} m por detrás y {foot.y - groundY:F2} m de altura)");
        }

        /// <summary>
        /// Grito de guerra (segunda fase de un jefe): se encoge reuniendo fuerza, abre el pecho y los brazos hacia abajo
        /// y atrás con la cabeza alta, tiembla de rabia y vuelve a la guardia.
        /// </summary>
        private static void BuildRoar(Rig rig, Pose combat, Pose crouch, System.Text.StringBuilder report)
        {
            const float length = 1.5f;

            Pose gather = combat.Clone();
            CopyLegs(ref gather, Pose.Lerp(combat, crouch, 0.45f));
            gather.pos.y = Mathf.Lerp(combat.pos.y, crouch.pos.y, 0.45f);
            Nudge(ref gather, "Spine Front-Back", 0.35f);
            Nudge(ref gather, "Chest Front-Back", 0.25f);
            Nudge(ref gather, "Head Nod Down-Up", 0.35f);
            SetBoth(ref gather, "Arm Down-Up", -0.35f);
            SetBoth(ref gather, "Arm Front-Back", 0.35f);
            SetBoth(ref gather, "Forearm Stretch", -0.4f);

            Pose roar = combat.Clone();
            CopyLegs(ref roar, Pose.Lerp(combat, crouch, 0.35f));
            roar.pos.y = Mathf.Lerp(combat.pos.y, crouch.pos.y, 0.35f);
            SetBoth(ref roar, "Upper Leg In-Out", 0.25f);
            Nudge(ref roar, "Spine Front-Back", -0.3f);
            Nudge(ref roar, "Chest Front-Back", -0.3f);
            Nudge(ref roar, "UpperChest Front-Back", -0.2f);
            Nudge(ref roar, "Head Nod Down-Up", -0.55f);
            // Brazos tensos hacia abajo y atrás, codos flexionados y puños a la altura de la cadera
            SetBoth(ref roar, "Arm Down-Up", -0.3f);
            SetBoth(ref roar, "Arm Front-Back", -0.4f);
            SetBoth(ref roar, "Forearm Stretch", -0.15f);
            SetBoth(ref roar, "Shoulder Down-Up", 0.3f);
            roar.rot = Quaternion.Euler(-8f, 0f, 0f) * combat.rot;
            rig.Apply(combat);
            float groundY = Mathf.Min(rig.Bone(HumanBodyBones.LeftFoot).y, rig.Bone(HumanBodyBones.RightFoot).y);
            float metersPerUnit = 0.715f;
            GroundFoot(rig, ref roar, HumanBodyBones.LeftFoot, groundY, metersPerUnit);
            GroundFoot(rig, ref gather, HumanBodyBones.LeftFoot, groundY, metersPerUnit);

            Pose Evaluate(float t)
            {
                Pose p;
                if (t < 0.3f) p = Pose.Lerp(combat, gather, Smooth(t / 0.3f));
                else if (t < 0.5f) p = Pose.Lerp(gather, roar, Smooth((t - 0.3f) / 0.2f));
                else if (t < 1.15f)
                {
                    p = roar.Clone();
                    // Temblor de rabia en brazos y pecho
                    float shake = Mathf.Sin(t * 2f * Mathf.PI * 13f) * Mathf.InverseLerp(1.15f, 0.55f, t);
                    NudgeBoth(ref p, "Arm Down-Up", 0.05f * shake);
                    Nudge(ref p, "Chest Left-Right", 0.04f * shake);
                }
                else p = Pose.Lerp(roar, combat, Smooth((t - 1.15f) / (length - 1.15f)));
                return p;
            }

            Save(Roar, Evaluate, length, false);
            report.AppendLine($"   {Roar}: {length:F2} s (grito de guerra de un jefe al empezar su segunda fase)");
        }

        /// <summary>Sube o baja el cuerpo de la pose para que el pie indicado quede apoyado a la altura del suelo.</summary>
        private static void GroundFoot(Rig rig, ref Pose pose, HumanBodyBones foot, float groundY, float metersPerUnit)
        {
            for (int i = 0; i < 2; i++)
            {
                rig.Apply(pose);
                float error = rig.Bone(foot).y - groundY;
                pose.pos.y -= error / Mathf.Max(0.01f, metersPerUnit);
            }
        }

        private static void SetBoth(ref Pose p, string muscle, float value)
        {
            SetIntent(ref p, "Left " + muscle, value);
            SetIntent(ref p, "Right " + muscle, value);
        }

        // ───────────────────────── Utilidades de las recetas ─────────────────────────

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Interpola poses clave con aceleración y frenado suaves.</summary>
        private static Func<float, Pose> Keyed(List<(float time, Pose pose)> keys)
        {
            return t =>
            {
                if (t <= keys[0].time) return keys[0].pose;
                for (int i = 0; i < keys.Count - 1; i++)
                {
                    if (t <= keys[i + 1].time)
                    {
                        float u = Mathf.InverseLerp(keys[i].time, keys[i + 1].time, t);
                        return Pose.Lerp(keys[i].pose, keys[i + 1].pose, Smooth(u));
                    }
                }
                return keys[keys.Count - 1].pose;
            };
        }

        /// <summary>
        /// Averigua el signo de cada músculo que usan las recetas, moviéndolo sobre el esqueleto y mirando qué hace:
        /// así "Nudge(Arm Down-Up, +0.5)" siempre sube el brazo, sin depender de convenciones.
        /// </summary>
        private static void Calibrate(Rig rig, Pose basePose, System.Text.StringBuilder report)
        {
            intentSign.Clear();
            Vector3 headTop() => rig.HeadTop();

            foreach (string side in new[] { "Left", "Right" })
            {
                HumanBodyBones hand = side == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                HumanBodyBones elbow = side == "Left" ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                HumanBodyBones upperArm = side == "Left" ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
                HumanBodyBones foot = side == "Left" ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                HumanBodyBones knee = side == "Left" ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                HumanBodyBones upperLeg = side == "Left" ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
                float outward = side == "Left" ? -1f : 1f;

                // El brazo y el muslo se miden por el codo y la rodilla: con la articulación doblada,
                // la mano o el pie pueden moverse al revés que el hueso que se gira
                Learn(rig, basePose, side + " Arm Down-Up", () => rig.Bone(elbow).y);
                Learn(rig, basePose, side + " Arm Front-Back", () => rig.Bone(elbow).z);
                Learn(rig, basePose, side + " Forearm Stretch", () => Vector3.Distance(rig.Bone(hand), rig.Bone(upperArm)));
                Learn(rig, basePose, side + " Shoulder Down-Up", () => rig.Bone(upperArm).y);
                Learn(rig, basePose, side + " Upper Leg Front-Back", () => rig.Bone(knee).z);
                Learn(rig, basePose, side + " Upper Leg In-Out", () => rig.Bone(knee).x * outward);
                Learn(rig, basePose, side + " Lower Leg Stretch", () => Vector3.Distance(rig.Bone(foot), rig.Bone(upperLeg)));
            }

            // Tronco: + = inclinarse hacia delante / hacia la derecha del personaje
            Learn(rig, basePose, "Spine Front-Back", () => headTop().z);
            Learn(rig, basePose, "Chest Front-Back", () => headTop().z);
            Learn(rig, basePose, "UpperChest Front-Back", () => headTop().z);
            Learn(rig, basePose, "Spine Left-Right", () => headTop().x);
            Learn(rig, basePose, "Chest Left-Right", () => headTop().x);
            Learn(rig, basePose, "UpperChest Left-Right", () => headTop().x);
            // Cabeza: + = mirar hacia abajo / inclinarla hacia la derecha
            Learn(rig, basePose, "Head Nod Down-Up", () => headTop().z - rig.Bone(HumanBodyBones.Head).z);
            Learn(rig, basePose, "Head Tilt Left-Right", () => headTop().x - rig.Bone(HumanBodyBones.Head).x);
            // Giro de cabeza y cuello: + = mirar hacia la derecha del personaje (eje Z del hueso = hacia donde mira la cara)
            Learn(rig, basePose, "Head Turn Left-Right", () => FaceYaw(rig));
            Learn(rig, basePose, "Neck Turn Left-Right", () => FaceYaw(rig));
            Learn(rig, basePose, "Spine Twist Left-Right", () => FaceYaw(rig));
            Learn(rig, basePose, "Chest Twist Left-Right", () => FaceYaw(rig));

            var signs = new System.Text.StringBuilder();
            foreach (var kv in intentSign) signs.Append($"{kv.Key}={(kv.Value > 0 ? "+" : "-")} ");
            report.AppendLine("   Calibración de músculos: " + signs);
        }

        private static float FaceYaw(Rig rig)
        {
            Transform head = rig.anim.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return 0f;
            Vector3 f = rig.go.transform.InverseTransformDirection(head.forward);
            f.y = 0f;
            return f.sqrMagnitude < 0.0001f ? 0f : Vector3.SignedAngle(Vector3.forward, f, Vector3.up);
        }

        private static void Learn(Rig rig, Pose basePose, string muscle, Func<float> measure)
        {
            int i = M(muscle);
            Pose a = basePose.Clone();
            Pose b = basePose.Clone();
            float center = Mathf.Clamp(basePose.m[i], -0.6f, 0.6f);
            a.m[i] = center - 0.3f;
            b.m[i] = center + 0.3f;
            rig.Apply(a);
            float va = measure();
            rig.Apply(b);
            float vb = measure();
            intentSign[muscle] = vb >= va ? 1f : -1f;
        }

        // ───────────────────────── Escritura del clip ─────────────────────────

        private static void BuildPropertyNames(AnimationClip humanoidClip)
        {
            var props = new HashSet<string>();
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(humanoidClip))
            {
                if (b.type == typeof(Animator)) props.Add(b.propertyName);
            }

            muscleProperty = new string[HumanTrait.MuscleCount];
            var missing = new List<string>();
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string name = HumanTrait.MuscleName[i];
                string finger = FingerPropertyName(name);
                if (props.Contains(name)) muscleProperty[i] = name;
                else if (props.Contains(finger)) muscleProperty[i] = finger;
                else
                {
                    muscleProperty[i] = finger;
                    missing.Add(name);
                }
            }
            if (missing.Count > 0 && missing.Count < HumanTrait.MuscleCount)
            {
                Debug.Log("[Ayni Forja] Músculos sin curva en el clip de referencia (normal en ojos y mandíbula): " + string.Join(", ", missing));
            }
        }

        /// <summary>"Left Index 1 Stretched" → "LeftHand.Index.1 Stretched" (así se llaman las curvas de los dedos en los clips).</summary>
        private static string FingerPropertyName(string muscle)
        {
            string[] parts = muscle.Split(' ');
            if (parts.Length < 3 || (parts[0] != "Left" && parts[0] != "Right")) return muscle;
            string finger = parts[1];
            if (finger != "Thumb" && finger != "Index" && finger != "Middle" && finger != "Ring" && finger != "Little") return muscle;
            if (parts[2] == "Spread") return $"{parts[0]}Hand.{finger}.Spread";
            return $"{parts[0]}Hand.{finger}.{parts[2]} {string.Join(" ", parts, 3, parts.Length - 3)}";
        }

        /// <summary>Escribe en el clip las curvas de músculos y de cuerpo de la función de poses, muestreada a 30 fps.</summary>
        private static void FillClip(AnimationClip clip, Func<float, Pose> evaluate, float length, bool loop)
        {
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * Fps) + 1);
            int muscleCount = HumanTrait.MuscleCount;
            var curves = new AnimationCurve[muscleCount + 7];
            for (int c = 0; c < curves.Length; c++) curves[c] = new AnimationCurve();

            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(length, f / Fps);
                // En los bucles el último fotograma es exactamente el primero
                Pose p = loop && f == frames - 1 ? evaluate(0f) : evaluate(t);
                for (int i = 0; i < muscleCount; i++) curves[i].AddKey(t, p.m[i]);
                curves[muscleCount + 0].AddKey(t, p.pos.x);
                curves[muscleCount + 1].AddKey(t, p.pos.y);
                curves[muscleCount + 2].AddKey(t, p.pos.z);
                Quaternion q = p.rot;
                curves[muscleCount + 3].AddKey(t, q.x);
                curves[muscleCount + 4].AddKey(t, q.y);
                curves[muscleCount + 5].AddKey(t, q.z);
                curves[muscleCount + 6].AddKey(t, q.w);
            }

            var bindings = new EditorCurveBinding[curves.Length];
            for (int i = 0; i < muscleCount; i++) bindings[i] = EditorCurveBinding.FloatCurve("", typeof(Animator), muscleProperty[i]);
            string[] root = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < root.Length; i++) bindings[muscleCount + i] = EditorCurveBinding.FloatCurve("", typeof(Animator), root[i]);

            AnimationUtility.SetEditorCurves(clip, bindings, curves);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlendOrientation = true;     // todo el movimiento del cuerpo se queda en la pose:
            settings.loopBlendPositionY = true;       // el personaje no se desplaza por la animación
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void Save(string name, Func<float, Pose> evaluate, float length, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = Fps };
            FillClip(clip, evaluate, length, loop);

            string path = $"{OutFolder}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                // Conserva el GUID: el Animator y quien lo use no pierden la referencia
                EditorUtility.CopySerialized(clip, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(clip);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, path);
            }
        }

        private static AnimationClip LoadClip(string fbxName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath($"{AnimFolder}/{fbxName}.fbx"))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }

        public static AnimationClip LoadGenerated(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutFolder}/{name}.anim");
        }

        // ───────────────────────── Hojas de fotogramas ─────────────────────────

        /// <summary>Escena de vista previa aislada con Yari, suelo, luz y cámara para dibujar poses.</summary>
        private class Stage : IDisposable
        {
            public const int CellW = 220, CellH = 300;
            private readonly Scene scene;
            public readonly Rig rig;
            private readonly Camera cam;
            private readonly RenderTexture rt;
            private readonly Material groundMat;
            private readonly List<GameObject> extras = new List<GameObject>();

            public Stage()
            {
                scene = EditorSceneManager.NewPreviewScene();
                rig = new Rig(scene);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (mat != null)
                {
                    foreach (var r in rig.go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
                }

                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.localScale = Vector3.one * 0.6f;
                groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.42f, 0.38f, 0.33f) };
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
                extras.Add(ground);

                var lightGo = new GameObject("ForjaLuz") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
                extras.Add(lightGo);

                var camGo = new GameObject("ForjaCam") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(camGo, scene);
                cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.68f, 0.74f);
                cam.fieldOfView = 34f;
                cam.nearClipPlane = 0.05f;
                rt = new RenderTexture(CellW, CellH, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                extras.Add(camGo);

                // El primer render de una escena de vista previa a veces sale vacío
                cam.Render();
            }

            private readonly List<(SkinnedMeshRenderer smr, Mesh mesh, Transform view)> baked = new List<(SkinnedMeshRenderer, Mesh, Transform)>();

            /// <summary>
            /// En el editor la malla con esqueleto se dibuja con la pose del muestreo anterior; por eso se "hornea"
            /// la malla con los huesos actuales y se dibuja esa copia.
            /// </summary>
            private void BakeSkins()
            {
                if (baked.Count == 0)
                {
                    foreach (var smr in rig.go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var view = new GameObject("Horneado_" + smr.name) { hideFlags = HideFlags.HideAndDontSave };
                        SceneManager.MoveGameObjectToScene(view, scene);
                        var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                        view.AddComponent<MeshFilter>().sharedMesh = mesh;
                        view.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                        smr.enabled = false;
                        baked.Add((smr, mesh, view.transform));
                        extras.Add(view);
                    }
                }
                foreach (var b in baked)
                {
                    b.smr.BakeMesh(b.mesh, true);
                    b.view.SetPositionAndRotation(b.smr.transform.position, b.smr.transform.rotation);
                }
            }

            /// <summary>Dibuja la pose actual del esqueleto de frente (fila 0) y de perfil (fila 1) en la columna indicada.</summary>
            public void Shoot(Texture2D sheet, int column)
            {
                BakeSkins();
                for (int row = 0; row < 2; row++)
                {
                    Vector3 eye = row == 0 ? new Vector3(0.4f, 1.15f, 4.2f) : new Vector3(4.2f, 1.15f, 0.3f);
                    cam.transform.position = eye;
                    cam.transform.LookAt(new Vector3(0f, 0.85f, 0f));
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, CellW, CellH), column * CellW, (1 - row) * CellH);
                    RenderTexture.active = null;
                }
            }

            public void Dispose()
            {
                if (cam != null) cam.targetTexture = null;
                RenderTexture.active = null;
                rig.Dispose();
                foreach (var b in baked) if (b.mesh != null) UnityEngine.Object.DestroyImmediate(b.mesh);
                foreach (var go in extras) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(groundMat);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void WriteSheet(Texture2D sheet, string fileName)
        {
            sheet.Apply();
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugCaptures");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, fileName);
            File.WriteAllBytes(file, sheet.EncodeToPNG());
            Debug.Log($"[Ayni Forja] Hoja de fotogramas: {file}");
        }

        /// <summary>
        /// Dibuja el clip en varios instantes, de frente (fila de arriba) y de perfil (fila de abajo),
        /// en una escena de vista previa aislada. Resultado: DebugCaptures/forja_NOMBRE.png
        /// </summary>
        public static void RenderSheet(string clipName, int columns)
        {
            AnimationClip clip = LoadGenerated(clipName) ?? LoadClip(clipName);
            if (clip == null)
            {
                Debug.LogWarning("[Ayni Forja] No existe el clip " + clipName);
                return;
            }

            BeginAnimationMode();
            var sheet = new Texture2D(Stage.CellW * columns, Stage.CellH * 2, TextureFormat.RGB24, false);
            try
            {
                using (var stage = new Stage())
                {
                    for (int c = 0; c < columns; c++)
                    {
                        float t = columns == 1 ? 0f : clip.length * c / (columns - 1);
                        stage.rig.go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(stage.rig.go, clip, t);
                        AnimationMode.EndSampling();
                        stage.rig.go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        stage.Shoot(sheet, c);
                    }
                }
                WriteSheet(sheet, "forja_" + clipName + ".png");
            }
            finally
            {
                EndAnimationMode();
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        /// <summary>
        /// Laboratorio de poses: parte de la pose de un clip y fija valores absolutos (en el sentido calibrado)
        /// para ver qué significa cada número. spec = "Arm Down-Up=0.5;Forearm Stretch=1|Arm Down-Up=-0.5" (| separa columnas).
        /// Los músculos sin "Left"/"Right" se aplican a los dos lados. Resultado: DebugCaptures/laboratorio_NOMBRE.png
        /// </summary>
        public static void PoseLab(string fileName, string baseClip, string spec)
        {
            BeginAnimationMode();
            string[] columns = spec.Split('|');
            var sheet = new Texture2D(Stage.CellW * columns.Length, Stage.CellH * 2, TextureFormat.RGB24, false);
            try
            {
                // Se mide con un esqueleto aparte: leer la pose (GetHumanPose) del esqueleto que se dibuja congela su malla
                using (var probe = new Rig())
                using (var stage = new Stage())
                {
                    AnimationClip clip = LoadClip(baseClip);
                    BuildPropertyNames(clip);
                    Pose basePose = probe.Sample(clip, Mathf.Min(0.4f, clip.length));
                    Calibrate(probe, basePose, new System.Text.StringBuilder());

                    var values = new System.Text.StringBuilder("[Ayni Forja] Laboratorio " + fileName + " (valores base:");
                    foreach (string muscle in new[] { "Left Arm Down-Up", "Left Arm Front-Back", "Left Forearm Stretch",
                                                      "Left Upper Leg Front-Back", "Left Lower Leg Stretch", "Spine Front-Back" })
                    {
                        values.Append($" {muscle}={GetIntent(basePose, muscle):F2}");
                    }
                    values.Append(")");

                    for (int c = 0; c < columns.Length; c++)
                    {
                        Pose p = basePose.Clone();
                        foreach (string assignment in columns[c].Split(';'))
                        {
                            if (string.IsNullOrWhiteSpace(assignment)) continue;
                            string[] kv = assignment.Split('=');
                            string muscle = kv[0].Trim();
                            float value = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                            // Cuerpo entero: Roll / Pitch en grados, Lift en unidades del Avatar
                            if (muscle == "Roll") { p.rot = Quaternion.Euler(0f, 0f, value) * p.rot; continue; }
                            if (muscle == "Pitch") { p.rot = Quaternion.Euler(value, 0f, 0f) * p.rot; continue; }
                            if (muscle == "Lift") { p.pos.y += value; continue; }
                            if (HasSides(muscle))
                            {
                                SetIntent(ref p, "Left " + muscle, value);
                                SetIntent(ref p, "Right " + muscle, value);
                            }
                            else SetIntent(ref p, muscle, value);
                        }
                        stage.rig.Apply(p);
                        stage.Shoot(sheet, c);
                        values.Append($"\n   columna {c}: {columns[c]}  → mano izq {stage.rig.Bone(HumanBodyBones.LeftHand):F2} pie izq {stage.rig.Bone(HumanBodyBones.LeftFoot):F2}");
                    }
                    Debug.Log(values.ToString());
                }
                WriteSheet(sheet, "laboratorio_" + fileName + ".png");
            }
            finally
            {
                EndAnimationMode();
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        private static bool HasSides(string muscle)
        {
            M("Spine Front-Back");
            return !muscle.StartsWith("Left ") && !muscle.StartsWith("Right ") && muscleIndex.ContainsKey("Left " + muscle);
        }

        /// <summary>Fija el valor de un músculo en el sentido calibrado (+ = la intención descrita en Calibrate).</summary>
        private static void SetIntent(ref Pose p, string muscle, float value)
        {
            float sign = intentSign.TryGetValue(muscle, out float s) ? s : 1f;
            p.m[M(muscle)] = Mathf.Clamp(value * sign, -1f, 1f);
        }

        /// <summary>Valor actual de un músculo en el sentido calibrado.</summary>
        private static float GetIntent(Pose p, string muscle)
        {
            float sign = intentSign.TryGetValue(muscle, out float s) ? s : 1f;
            return p.m[M(muscle)] * sign;
        }
    }
}
#endif
