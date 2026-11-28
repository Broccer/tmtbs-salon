using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Girdi: WASD/Oklar kameraya göre yürü, Shift koş, E/Enter işlem, M/Tab cep menüsü, Esc kapat.
/// Kamera: fareyle sürükle (sol ya da sağ tuş) döndürür, tekerlek yakınlaştırır; gamepad sağ çubuk.
/// Telefonda TouchControls: sol joystick yürür, ekranın geri kalanında parmakla sürüklemek kamerayı döndürür,
/// iki parmak yakınlaştırır, kısa dokunuş tıklamadır. UI açıkken (Blocked) hareket ve kamera okunmaz.
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
        if (TouchControls.Move.sqrMagnitude > v.sqrMagnitude) v = TouchControls.Move;
        if (v.sqrMagnitude < 0.04f) return Vector2.zero;
        return Vector2.ClampMagnitude(v, 1f);
    }

    public static bool Run()
    {
        if (Blocked) return false;
        if (TouchControls.Run) return true;
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
        if (TouchControls.InteractDown) return true;
        if (GUIUtility.keyboardControl != 0) return false; // bir yazı alanına yazılıyor
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) || (g != null && g.buttonSouth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return);
#endif
    }

    /// <summary>M ya da Tab: cep telefonu menüsü.</summary>
    public static bool MenuPressed()
    {
        if (GUIUtility.keyboardControl != 0) return false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.mKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Tab);
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

    /// <summary>Dünyaya kısa tıklama / dokunuş (sürükleme değil). Bırakıldığı karede bir kez döner.</summary>
    public static bool ClickPressed(out Vector2 screenPos)
    {
        screenPos = Vector2.zero;
        Sample();
        if (Blocked) return false;
        if (TouchControls.TakeTap(out screenPos)) return true;
        if (clickReady && !TouchControls.Owns(clickPos)) { screenPos = clickPos; clickReady = false; return true; }
        return false;
    }

    // ---------------------------------------------------------------- Kamera (kare başına bir kez örneklenir)
    public static float MouseSensitivity = 0.2f;  // derece / piksel
    static int sampled = -1;
    static Vector2 look;
    static float zoom;
    static bool leftArmed, leftDragging, clickReady;
    static Vector2 leftDownPos, clickPos;
    static float dragPx;

    /// <summary>Bu karede kameranın dönüşü (derece; x yatay, y dikey).</summary>
    public static Vector2 Look() { Sample(); return look; }

    /// <summary>Bu karede yakınlaştırma (pozitif = yaklaş, birim: metre).</summary>
    public static float Zoom() { Sample(); return zoom; }

    static void Sample()
    {
        if (sampled == Time.frameCount) return;
        sampled = Time.frameCount;
        look = Vector2.zero;
        zoom = 0f;
        clickReady = false;
        float dt = Time.unscaledDeltaTime;

        if (Blocked || HtmlInput.Open_)
        {
            leftArmed = leftDragging = false;
            return;
        }

        // Telefon: TouchControls sürükleme ve iki parmak
        float sw = Mathf.Max(1f, Screen.width), sh = Mathf.Max(1f, Screen.height);
        look += new Vector2(TouchControls.LookDelta.x / sw * 220f, TouchControls.LookDelta.y / sh * 120f);
        zoom += TouchControls.PinchDelta / sh * 9f;

        if (HtmlInput.Touch) return; // dokunmatikte tarayıcının taklit ettiği fare olaylarını sayma
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m != null)
        {
            Vector2 d = m.delta.ReadValue();
            Vector2 pos = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame) { leftArmed = true; leftDragging = false; leftDownPos = pos; dragPx = 0f; }
            if (leftArmed && m.leftButton.isPressed)
            {
                dragPx = Mathf.Max(dragPx, (pos - leftDownPos).magnitude); // basılan noktadan uzaklık (oraya gelirken yapılan hareket sayılmaz)
                if (!leftDragging && dragPx > 8f && GUIUtility.hotControl == 0 && !TouchControls.Owns(leftDownPos)) leftDragging = true;
            }
            if (m.leftButton.wasReleasedThisFrame)
            {
                if (leftArmed && !leftDragging) { clickReady = true; clickPos = leftDownPos; }
                leftArmed = leftDragging = false;
            }
            if (GUIUtility.hotControl != 0) leftDragging = false; // bir arayüz öğesi sürükleniyor
            if (leftDragging || m.rightButton.isPressed) look += d * MouseSensitivity;
            float sc = m.scroll.ReadValue().y;
            // Tekerlek ölçeği platforma göre değişir (Windows 120/çentik, WebGL farklı): yalnız yönü kullan.
            if (Mathf.Abs(sc) > 0.01f) zoom += Mathf.Sign(sc) * 0.9f;
        }
        var g = Gamepad.current;
        if (g != null)
        {
            Vector2 rs = g.rightStick.ReadValue();
            if (rs.sqrMagnitude > 0.02f) look += new Vector2(rs.x * 160f, rs.y * 100f) * dt;
            if (g.leftShoulder.isPressed) zoom -= 4f * dt;
            if (g.rightShoulder.isPressed) zoom += 4f * dt;
        }
#else
        Vector2 pos = Input.mousePosition;
        Vector2 d = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        if (Input.GetMouseButtonDown(0)) { leftArmed = true; leftDragging = false; leftDownPos = pos; dragPx = 0f; }
        if (leftArmed && Input.GetMouseButton(0))
        {
            dragPx = Mathf.Max(dragPx, (pos - leftDownPos).magnitude);
            if (!leftDragging && dragPx > 8f && GUIUtility.hotControl == 0 && !TouchControls.Owns(leftDownPos)) leftDragging = true;
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (leftArmed && !leftDragging) { clickReady = true; clickPos = leftDownPos; }
            leftArmed = leftDragging = false;
        }
        if (leftDragging || Input.GetMouseButton(1)) look += d * MouseSensitivity;
        zoom += Input.mouseScrollDelta.y * 0.9f;
#endif
    }
}