using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Salon girdisi: WASD/Oklar yürü (A/D döner), Shift koş, E/Enter kürsüde işlem, Esc kapat.
/// Gamepad: sol çubuk, A işlem, B kapat. UI açıkken (Blocked) hareket okunmaz.
/// </summary>
public static class InputReader
{
    public static bool Blocked;

    public static Vector2 Move()
    {
        if (Blocked) return Vector2.zero;
        Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
        }
        var g = Gamepad.current;
        if (g != null)
        {
            Vector2 s = g.leftStick.ReadValue();
            if (s.sqrMagnitude > v.sqrMagnitude) v = s;
        }
#else
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1f;
#endif
        if (v.sqrMagnitude < 0.04f) return Vector2.zero;
        return Vector2.ClampMagnitude(v, 1f);
    }

    public static bool Run()
    {
        if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed)) || (g != null && g.rightTrigger.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    public static bool InteractPressed()
    {
        if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) || (g != null && g.buttonSouth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return);
#endif
    }

    public static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public static bool ClickPressed(out Vector2 screenPos)
    {
        screenPos = Vector2.zero;
        if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) { screenPos = m.position.ReadValue(); return true; }
        var t = Touchscreen.current;
        if (t != null && t.primaryTouch.press.wasPressedThisFrame) { screenPos = t.primaryTouch.position.ReadValue(); return true; }
        return false;
#else
        if (Input.GetMouseButtonDown(0)) { screenPos = Input.mousePosition; return true; }
        return false;
#endif
    }
}
