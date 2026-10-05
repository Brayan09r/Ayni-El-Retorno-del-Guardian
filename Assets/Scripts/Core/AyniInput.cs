using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ayni.Core
{
    public enum AyniDevice
    {
        KeyboardMouse,
        Gamepad
    }

    /// <summary>
    /// Entrada única del juego: teclado y ratón o mando de Xbox (XInput), con el Input Manager clásico de Unity.
    /// Los scripts preguntan por acciones ("golpe ligero", "guardia"...) y no por teclas, así funcionan igual con los dos.
    ///
    /// Mando de Xbox:
    ///   Stick izq. moverse · Stick der. cámara · X golpe ligero · Y golpe pesado · A saltar / perdonar (Ayni)
    ///   B agacharse / rematar (Venganza) · LB guardia (+ stick: esquivas) · RB o R3 fijar rival · RT o L3 esprintar
    ///   (el stick a medias camina y a fondo corre)
    /// Teclado: WASD camina y con Shift corre; no hay sprint.
    ///   View tutorial · Menu saltar escena / reintentar · Cruceta arriba mostrar u ocultar controles
    ///
    /// Los ejes del mando (stick derecho, gatillos, cruceta) los crea el menú Ayni > Mando > Configurar Ejes del Mando.
    /// Si faltan, el juego sigue funcionando con teclado y con el stick izquierdo.
    /// </summary>
    public static class AyniInput
    {
        public enum Action
        {
            LightAttack,
            HeavyAttack,
            Guard,
            Jump,
            Sprint,
            CrouchToggle,
            CrouchHold,
            LockOn,
            Execute,   // Juicio Ayni: venganza
            Mercy,     // Juicio Ayni: perdón
            Confirm,   // Menús y tutorial: siguiente
            Back,      // Menús y tutorial: anterior
            Skip,      // Saltar escena o tutorial
            Tutorial,
            Restart,
            ToggleHud
        }

        // Botones del mando de Xbox en Windows (Input Manager clásico)
        public const KeyCode PadA = KeyCode.JoystickButton0;
        public const KeyCode PadB = KeyCode.JoystickButton1;
        public const KeyCode PadX = KeyCode.JoystickButton2;
        public const KeyCode PadY = KeyCode.JoystickButton3;
        public const KeyCode PadLB = KeyCode.JoystickButton4;
        public const KeyCode PadRB = KeyCode.JoystickButton5;
        public const KeyCode PadView = KeyCode.JoystickButton6;
        public const KeyCode PadMenu = KeyCode.JoystickButton7;
        public const KeyCode PadL3 = KeyCode.JoystickButton8;
        public const KeyCode PadR3 = KeyCode.JoystickButton9;

        // Ejes que crea la herramienta de configuración
        public const string AxisLeftX = "Ayni_LeftX";
        public const string AxisLeftY = "Ayni_LeftY";
        public const string AxisRightX = "Ayni_RightX";
        public const string AxisRightY = "Ayni_RightY";
        public const string AxisTriggers = "Ayni_Triggers";
        public const string AxisLT = "Ayni_LT";
        public const string AxisRT = "Ayni_RT";
        public const string AxisDPadX = "Ayni_DPadX";
        public const string AxisDPadY = "Ayni_DPadY";

        private const float StickDeadZone = 0.2f;
        private const float TriggerThreshold = 0.4f;

        private static readonly int ActionCount = Enum.GetValues(typeof(Action)).Length;
        private static bool[] held = new bool[ActionCount];
        private static bool[] prevHeld = new bool[ActionCount];
        private static int updatedFrame = -1;

        private static Vector2 move;
        private static Vector2 lookStick;
        private static Vector2 mouseDelta;

        private static readonly Dictionary<string, bool> axisExists = new Dictionary<string, bool>();

        // Simulación (pruebas automáticas desde el editor)
        private static readonly float[] simulatedUntil = new float[ActionCount];
        private static Vector2 simulatedMove;
        private static float simulatedMoveUntil = -1f;
        private static bool simulatedFromStick = true;
        private static bool moveFromStick;

        /// <summary>Último dispositivo usado: decide qué botones muestran el HUD y el tutorial.</summary>
        public static AyniDevice LastDevice { get; private set; } = AyniDevice.KeyboardMouse;
        public static bool UsingGamepad => LastDevice == AyniDevice.Gamepad;

        /// <summary>Se dispara cuando el jugador cambia de teclado a mando o al revés.</summary>
        public static event Action<AyniDevice> OnDeviceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            held = new bool[ActionCount];
            prevHeld = new bool[ActionCount];
            updatedFrame = -1;
            axisExists.Clear();
            Array.Clear(simulatedUntil, 0, simulatedUntil.Length);
            simulatedMoveUntil = -1f;
            OnDeviceChanged = null;
            LastDevice = AyniDevice.KeyboardMouse;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateUpdater()
        {
            var go = new GameObject("Ayni_Input");
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<AyniInputUpdater>();
        }

        // ───────────────────────── Consultas ─────────────────────────

        /// <summary>Dirección de movimiento (stick izquierdo, cruceta o WASD / flechas). Magnitud entre 0 y 1.</summary>
        public static Vector2 Move { get { EnsureUpdated(); return move; } }

        /// <summary>
        /// El movimiento de este fotograma viene de un stick analógico y no de las teclas. Con el stick la inclinación
        /// decide entre caminar y correr; con las teclas se camina y Shift hace correr.
        /// </summary>
        public static bool MoveFromStick { get { EnsureUpdated(); return moveFromStick; } }

        /// <summary>Stick derecho, entre -1 y 1 (arriba = +Y). La cámara lo multiplica por su velocidad de giro.</summary>
        public static Vector2 LookStick { get { EnsureUpdated(); return lookStick; } }

        /// <summary>Movimiento del ratón en este fotograma (ejes Mouse X / Mouse Y).</summary>
        public static Vector2 MouseDelta { get { EnsureUpdated(); return mouseDelta; } }

        public static bool Held(Action action) { EnsureUpdated(); return held[(int)action]; }
        public static bool Down(Action action) { EnsureUpdated(); return held[(int)action] && !prevHeld[(int)action]; }
        public static bool Up(Action action) { EnsureUpdated(); return !held[(int)action] && prevHeld[(int)action]; }

        /// <summary>Texto del botón de una acción según el dispositivo en uso (para el HUD y el tutorial).</summary>
        public static string Label(Action action)
        {
            bool pad = UsingGamepad;
            switch (action)
            {
                case Action.LightAttack: return pad ? "X" : "Clic Izq.";
                case Action.HeavyAttack: return pad ? "Y" : "Q / E";
                case Action.Guard: return pad ? "LB" : "Clic Der. / G";
                case Action.Jump: return pad ? "A" : "Espacio";
                case Action.Sprint: return pad ? "RT / L3" : "Shift Izq.";
                case Action.CrouchToggle: return pad ? "B" : "C";
                case Action.CrouchHold: return pad ? "B" : "Ctrl Izq.";
                case Action.LockOn: return pad ? "RB / R3" : "Tab / Clic central";
                case Action.Execute: return pad ? "B" : "F";
                case Action.Mercy: return pad ? "A" : "X";
                case Action.Confirm: return pad ? "A" : "Enter";
                case Action.Back: return pad ? "B" : "Retroceso";
                case Action.Skip: return pad ? "Menu" : "Tab";
                case Action.Tutorial: return pad ? "View" : "F1";
                case Action.Restart: return pad ? "Menu" : "R";
                case Action.ToggleHud: return pad ? "Cruceta ↑" : "H";
            }
            return action.ToString();
        }

        /// <summary>Etiqueta de las direcciones de esquiva (Guardia + dirección).</summary>
        public static string DirLabel(string padDir, string keyDir) => UsingGamepad ? padDir : keyDir;

        // ───────────────────────── Simulación para pruebas ─────────────────────────

        /// <summary>Mantiene pulsada una acción durante unos segundos reales (pruebas automáticas).</summary>
        public static void Simulate(string actionName, float seconds)
        {
            if (Enum.TryParse(actionName, true, out Action action))
            {
                simulatedUntil[(int)action] = Time.unscaledTime + seconds;
            }
            else
            {
                Debug.LogWarning("[AyniInput] Acción desconocida para simular: " + actionName);
            }
        }

        /// <summary>Simula el stick izquierdo durante unos segundos reales.</summary>
        public static void SimulateMove(float x, float y, float seconds)
        {
            simulatedMove = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            simulatedMoveUntil = Time.unscaledTime + seconds;
            simulatedFromStick = true;
        }

        /// <summary>Simula las teclas de movimiento (WASD) durante unos segundos reales.</summary>
        public static void SimulateKeys(float x, float y, float seconds)
        {
            simulatedMove = new Vector2(x, y).normalized;
            simulatedMoveUntil = Time.unscaledTime + seconds;
            simulatedFromStick = false;
        }

        // ───────────────────────── Lectura ─────────────────────────

        private static void EnsureUpdated()
        {
            if (updatedFrame != Time.frameCount) Refresh();
        }

        /// <summary>Lee todos los dispositivos una vez por fotograma (lo llama AyniInputUpdater antes que el resto).</summary>
        internal static void Refresh()
        {
            if (updatedFrame == Time.frameCount) return;
            updatedFrame = Time.frameCount;

            var swap = prevHeld;
            prevHeld = held;
            held = swap;

            bool keyboardActivity = Input.anyKey && !AnyJoystickButton();
            Vector2 mouse = new Vector2(SafeAxis("Mouse X"), SafeAxis("Mouse Y"));
            if (mouse.sqrMagnitude > 0.01f) keyboardActivity = true;
            mouseDelta = mouse;

            // --- Movimiento ---
            Vector2 keys = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keys.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keys.x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keys.y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keys.y += 1f;

            Vector2 leftStick = ReadStick(AxisLeftX, AxisLeftY, "Horizontal", "Vertical");
            Vector2 dpad = new Vector2(SafeAxis(AxisDPadX), SafeAxis(AxisDPadY));
            if (dpad.sqrMagnitude < 0.25f) dpad = Vector2.zero;

            Vector2 pad = leftStick.sqrMagnitude >= dpad.sqrMagnitude ? leftStick : dpad;
            bool padActivity = pad != Vector2.zero || AnyJoystickButton();

            if (keys != Vector2.zero) move = keys.normalized;
            else move = Vector2.ClampMagnitude(pad, 1f);
            moveFromStick = keys == Vector2.zero && pad != Vector2.zero;

            if (Time.unscaledTime < simulatedMoveUntil)
            {
                move = simulatedMove;
                moveFromStick = simulatedFromStick;
            }

            // --- Cámara ---
            Vector2 rightStick = new Vector2(SafeAxis(AxisRightX), SafeAxis(AxisRightY));
            float mag = rightStick.magnitude;
            if (mag < StickDeadZone) rightStick = Vector2.zero;
            else
            {
                // Curva de respuesta: precisión cerca del centro, giro rápido al fondo
                float t = Mathf.InverseLerp(StickDeadZone, 1f, Mathf.Min(1f, mag));
                rightStick = rightStick / mag * (t * t);
                padActivity = true;
            }
            lookStick = rightStick;

            // --- Gatillos ---
            float lt = SafeAxis(AxisLT);
            float rt = SafeAxis(AxisRT);
            float combined = SafeAxis(AxisTriggers); // algunos controladores usan un único eje: LT positivo, RT negativo
            lt = Mathf.Max(lt, combined);
            rt = Mathf.Max(rt, -combined);
            if (lt > TriggerThreshold || rt > TriggerThreshold) padActivity = true;

            float dpadY = dpad.y;

            // --- Botones ---
            Set(Action.LightAttack, Input.GetMouseButton(0) || Input.GetKey(PadX));
            Set(Action.HeavyAttack, Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E) || Input.GetKey(PadY));
            Set(Action.Guard, Input.GetMouseButton(1) || Input.GetKey(KeyCode.G) || Input.GetKey(PadLB));
            Set(Action.Jump, Input.GetKey(KeyCode.Space) || Input.GetKey(PadA));
            Set(Action.Sprint, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(PadL3) || rt > TriggerThreshold);
            Set(Action.CrouchToggle, Input.GetKey(KeyCode.C) || Input.GetKey(PadB));
            Set(Action.CrouchHold, Input.GetKey(KeyCode.LeftControl));
            Set(Action.LockOn, Input.GetKey(KeyCode.Tab) || Input.GetMouseButton(2) || Input.GetKey(PadRB) || Input.GetKey(PadR3));
            Set(Action.Execute, Input.GetKey(KeyCode.F) || Input.GetKey(PadB));
            Set(Action.Mercy, Input.GetKey(KeyCode.X) || Input.GetKey(PadA));
            Set(Action.Confirm, Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space) ||
                                Input.GetMouseButton(0) || Input.GetKey(PadA));
            Set(Action.Back, Input.GetKey(KeyCode.Backspace) || Input.GetKey(PadB));
            Set(Action.Skip, Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.Escape) || Input.GetKey(PadMenu));
            Set(Action.Tutorial, Input.GetKey(KeyCode.F1) || Input.GetKey(PadView));
            Set(Action.Restart, Input.GetKey(KeyCode.R) || Input.GetKey(PadMenu));
            Set(Action.ToggleHud, Input.GetKey(KeyCode.H) || dpadY > 0.5f);

            for (int i = 0; i < ActionCount; i++)
            {
                if (Time.unscaledTime < simulatedUntil[i]) held[i] = true;
            }

            // --- Dispositivo en uso ---
            AyniDevice device = LastDevice;
            if (padActivity) device = AyniDevice.Gamepad;
            else if (keyboardActivity) device = AyniDevice.KeyboardMouse;
            if (device != LastDevice)
            {
                LastDevice = device;
                OnDeviceChanged?.Invoke(device);
            }
        }

        private static void Set(Action action, bool value) => held[(int)action] = value;

        private static Vector2 ReadStick(string axisX, string axisY, string fallbackX, string fallbackY)
        {
            Vector2 v;
            if (HasAxis(axisX) && HasAxis(axisY)) v = new Vector2(SafeAxis(axisX), SafeAxis(axisY));
            else
            {
                // Sin los ejes propios: los ejes por defecto de Unity también leen el stick izquierdo
                // (y el teclado, pero si hay teclas pulsadas ya se usan ellas antes que el stick)
                v = new Vector2(SafeAxis(fallbackX), SafeAxis(fallbackY));
            }

            float mag = v.magnitude;
            if (mag < StickDeadZone) return Vector2.zero;
            float t = Mathf.InverseLerp(StickDeadZone, 1f, Mathf.Min(1f, mag));
            return v / mag * t;
        }

        private static bool AnyJoystickButton()
        {
            for (KeyCode k = KeyCode.JoystickButton0; k <= KeyCode.JoystickButton9; k++)
            {
                if (Input.GetKey(k)) return true;
            }
            return false;
        }

        private static bool HasAxis(string axis)
        {
            if (axisExists.TryGetValue(axis, out bool exists)) return exists;
            try
            {
                Input.GetAxisRaw(axis);
                exists = true;
            }
            catch (ArgumentException)
            {
                exists = false;
            }
            axisExists[axis] = exists;
            return exists;
        }

        private static float SafeAxis(string axis)
        {
            return HasAxis(axis) ? Input.GetAxisRaw(axis) : 0f;
        }
    }

    /// <summary>Lee la entrada al principio de cada fotograma, antes que el resto de scripts.</summary>
    [DefaultExecutionOrder(-1000)]
    internal class AyniInputUpdater : MonoBehaviour
    {
        private void Update()
        {
            AyniInput.Refresh();
        }
    }
}
