using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Daboiz.Core
{
    /// <summary>
    /// Thin input shim so the game runs with zero setup whether the project's
    /// "Active Input Handling" is Old, New (Input System package) or Both.
    /// Gameplay code only talks to this class, never to UnityEngine.Input directly.
    /// </summary>
    public static class GameInput
    {
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        // ---------------- New Input System path ----------------
        private static Key Map(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.A: return Key.A; case KeyCode.B: return Key.B; case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D; case KeyCode.E: return Key.E; case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G; case KeyCode.H: return Key.H; case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J; case KeyCode.K: return Key.K; case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M; case KeyCode.N: return Key.N; case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P; case KeyCode.Q: return Key.Q; case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S; case KeyCode.T: return Key.T; case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V; case KeyCode.W: return Key.W; case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y; case KeyCode.Z: return Key.Z;
                case KeyCode.Alpha0: return Key.Digit0; case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2; case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4; case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6; case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8; case KeyCode.Alpha9: return Key.Digit9;
                case KeyCode.Space: return Key.Space;
                case KeyCode.LeftControl: return Key.LeftCtrl; case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftShift: return Key.LeftShift; case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.Escape: return Key.Escape; case KeyCode.Tab: return Key.Tab;
                case KeyCode.Return: return Key.Enter; case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.LeftBracket: return Key.LeftBracket; case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.F1: return Key.F1;
                default: return Key.None;
            }
        }

        private static UnityEngine.InputSystem.Controls.KeyControl Ctrl(KeyCode k)
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            Key key = Map(k);
            if (key == Key.None) return null;
            return kb[key];
        }

        private static UnityEngine.InputSystem.Controls.ButtonControl MouseBtn(int b)
        {
            var m = Mouse.current;
            if (m == null) return null;
            switch (b)
            {
                case 0: return m.leftButton;
                case 1: return m.rightButton;
                case 2: return m.middleButton;
                default: return null;
            }
        }

        public static bool GetKey(KeyCode k) { var c = Ctrl(k); return c != null && c.isPressed; }
        public static bool GetKeyDown(KeyCode k) { var c = Ctrl(k); return c != null && c.wasPressedThisFrame; }
        public static bool GetKeyUp(KeyCode k) { var c = Ctrl(k); return c != null && c.wasReleasedThisFrame; }
        public static bool GetMouseButton(int b) { var c = MouseBtn(b); return c != null && c.isPressed; }
        public static bool GetMouseButtonDown(int b) { var c = MouseBtn(b); return c != null && c.wasPressedThisFrame; }
        public static bool GetMouseButtonUp(int b) { var c = MouseBtn(b); return c != null && c.wasReleasedThisFrame; }

        /// <summary>Mouse delta in roughly the same units as legacy "Mouse X"/"Mouse Y".</summary>
        public static Vector2 MouseDelta
        {
            get { var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f; }
        }

        /// <summary>Scroll in notches (positive = up).</summary>
        public static float Scroll
        {
            get { var m = Mouse.current; return m == null ? 0f : m.scroll.ReadValue().y / 120f; }
        }
#else
        // ---------------- Legacy Input Manager path ----------------
        public static bool GetKey(KeyCode k) { return Input.GetKey(k); }
        public static bool GetKeyDown(KeyCode k) { return Input.GetKeyDown(k); }
        public static bool GetKeyUp(KeyCode k) { return Input.GetKeyUp(k); }
        public static bool GetMouseButton(int b) { return Input.GetMouseButton(b); }
        public static bool GetMouseButtonDown(int b) { return Input.GetMouseButtonDown(b); }
        public static bool GetMouseButtonUp(int b) { return Input.GetMouseButtonUp(b); }

        public static Vector2 MouseDelta
        {
            get
            {
                // Raw pointer delta scaled to match the default "Mouse X" axis (sensitivity 0.1)
                // without depending on InputManager axis names existing.
                Vector2 d;
                try { d = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")); }
                catch (System.ArgumentException) { d = Vector2.zero; }
                return d;
            }
        }

        public static float Scroll { get { return Input.mouseScrollDelta.y; } }
#endif
    }
}
