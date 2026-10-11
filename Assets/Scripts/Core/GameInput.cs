using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Neanderthal
{
    /// <summary>
    /// Thin input layer so gameplay code never talks to Unity input directly.
    /// Works with the legacy Input Manager (default for this project) and falls back to the
    /// Input System package when the project is switched to "Input System only".
    /// </summary>
    public static class GameInput
    {
        public static float MouseSensitivity = 2f;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static Keyboard Kb => Keyboard.current;
        static Mouse Ms => Mouse.current;
        static bool Held(Key k) => Kb != null && Kb[k].isPressed;
        static bool Down(Key k) => Kb != null && Kb[k].wasPressedThisFrame;

        public static Vector2 Move => new Vector2(
            (Held(Key.D) ? 1f : 0f) - (Held(Key.A) ? 1f : 0f),
            (Held(Key.W) ? 1f : 0f) - (Held(Key.S) ? 1f : 0f));
        public static Vector2 Look => Ms == null ? Vector2.zero : Ms.delta.ReadValue() * 0.05f * MouseSensitivity;
        public static float Scroll => Ms == null ? 0f : Mathf.Sign(Ms.scroll.ReadValue().y) * (Mathf.Abs(Ms.scroll.ReadValue().y) > 0.01f ? 1f : 0f);
        public static bool JumpDown => Down(Key.Space);
        public static bool JumpHeld => Held(Key.Space);
        public static bool SprintHeld => Held(Key.LeftShift);
        public static bool CrouchHeld => Held(Key.LeftCtrl) || Held(Key.C);
        public static bool CrouchDown => Down(Key.LeftCtrl) || Down(Key.C);
        public static bool RollDown => Down(Key.V) || Down(Key.LeftAlt);
        public static bool FireHeld => Ms != null && Ms.leftButton.isPressed;
        public static bool FireDown => Ms != null && Ms.leftButton.wasPressedThisFrame;
        public static bool FireUp => Ms != null && Ms.leftButton.wasReleasedThisFrame;
        public static bool AltHeld => Ms != null && Ms.rightButton.isPressed;
        public static bool AltDown => Ms != null && Ms.rightButton.wasPressedThisFrame;
        public static bool AltUp => Ms != null && Ms.rightButton.wasReleasedThisFrame;
        public static bool ReloadDown => Down(Key.R);
        public static bool InteractDown => Down(Key.E);
        public static bool CraftingDown => Down(Key.Tab);
        public static bool InventoryDown => Down(Key.I);
        public static bool DebugDown => Down(Key.F3);
        public static bool HealDown => Down(Key.H);
        public static bool PauseDown => Down(Key.Escape);
        public static bool SlotDown(int slot)
        {
            switch (slot)
            {
                case 1: return Down(Key.Digit1);
                case 2: return Down(Key.Digit2);
                case 3: return Down(Key.Digit3);
                case 4: return Down(Key.Digit4);
                case 5: return Down(Key.Digit5);
                case 6: return Down(Key.Digit6);
                case 7: return Down(Key.Digit7);
                case 8: return Down(Key.Digit8);
                case 9: return Down(Key.Digit9);
                default: return false;
            }
        }
#else
        public static Vector2 Move => new Vector2(
            (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
            (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
        public static Vector2 Look => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * MouseSensitivity;
        public static float Scroll
        {
            get
            {
                float y = Input.mouseScrollDelta.y;
                return Mathf.Abs(y) > 0.01f ? Mathf.Sign(y) : 0f;
            }
        }
        public static bool JumpDown => Input.GetKeyDown(KeyCode.Space);
        public static bool JumpHeld => Input.GetKey(KeyCode.Space);
        public static bool SprintHeld => Input.GetKey(KeyCode.LeftShift);
        public static bool CrouchHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        public static bool CrouchDown => Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
        public static bool RollDown => Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.LeftAlt);
        public static bool FireHeld => Input.GetMouseButton(0);
        public static bool FireDown => Input.GetMouseButtonDown(0);
        public static bool FireUp => Input.GetMouseButtonUp(0);
        public static bool AltHeld => Input.GetMouseButton(1);
        public static bool AltDown => Input.GetMouseButtonDown(1);
        public static bool AltUp => Input.GetMouseButtonUp(1);
        public static bool ReloadDown => Input.GetKeyDown(KeyCode.R);
        public static bool InteractDown => Input.GetKeyDown(KeyCode.E);
        public static bool CraftingDown => Input.GetKeyDown(KeyCode.Tab);
        public static bool InventoryDown => Input.GetKeyDown(KeyCode.I);
        public static bool DebugDown => Input.GetKeyDown(KeyCode.F3);
        public static bool HealDown => Input.GetKeyDown(KeyCode.H);
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape);
        public static bool SlotDown(int slot) => slot >= 1 && slot <= 9 && Input.GetKeyDown(KeyCode.Alpha0 + slot);
#endif
    }
}
