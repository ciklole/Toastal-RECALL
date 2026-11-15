using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ToastalRecall
{
    /// <summary>
    /// Small input abstraction so gameplay works with the new Input System
    /// (the project's active input handler) and still compiles without it.
    /// </summary>
    public static class ToastalInput
    {
        public static Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                    return Mouse.current.position.ReadValue();
                if (Touchscreen.current != null)
                    return Touchscreen.current.primaryTouch.position.ReadValue();
#endif
                return Vector2.zero;
            }
        }

        public static bool PointerPressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                    return Mouse.current.leftButton.wasPressedThisFrame;
                if (Touchscreen.current != null)
                    return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
#endif
                return false;
            }
        }

        public static bool PointerHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                    return Mouse.current.leftButton.isPressed;
                if (Touchscreen.current != null)
                    return Touchscreen.current.primaryTouch.press.isPressed;
#endif
                return false;
            }
        }

        public static bool PointerReleasedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                    return Mouse.current.leftButton.wasReleasedThisFrame;
                if (Touchscreen.current != null)
                    return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
#endif
                return false;
            }
        }

        public static bool MiddleButtonPressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
#else
                return false;
#endif
            }
        }

        public static bool MiddleButtonHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.middleButton.isPressed;
#else
                return false;
#endif
            }
        }

        public static Vector2 ScrollDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                    return Mouse.current.scroll.ReadValue();
#endif
                return Vector2.zero;
            }
        }

        public static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
