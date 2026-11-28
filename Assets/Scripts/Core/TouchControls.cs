using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Telefon kontrolleri: sol altta serbest joystick (kameraya göre yürür; sonuna kadar itince koşar),
/// sağ altta İŞLEM (E) ve KOŞ düğmeleri. Ekranın geri kalanında bir parmakla sürüklemek kamerayı döndürür,
/// iki parmak yakınlaştırır, kısa dokunuş tıklamadır. Dokunuşlar ham okunur; IMGUI düğmelerinden bağımsızdır.
/// Yalnız dokunmatik arayüzde ve hareket serbestken görünür.
/// </summary>
public class TouchControls : MonoBehaviour
{
    public static Vector2 Move;
    public static bool Run;
    public static bool InteractDown;
    public static bool Visible;
    public static string InteractLabel = "İŞLEM";
    /// <summary>Bu karede kamera sürüklemesi (piksel) ve iki parmak aralığının değişimi (piksel, + = açıldı).</summary>
    public static Vector2 LookDelta;
    public static float PinchDelta;
    static bool tap;
    static Vector2 tapPos;

    /// <summary>Kısa dokunuş oldu mu (bir kez döner).</summary>
    public static bool TakeTap(out Vector2 pos)
    {
        pos = tapPos;
        if (!tap) return false;
        tap = false;
        return true;
    }

    int camId = -1, cam2Id = -1;
    Vector2 camPos, cam2Pos, camStart;
    float camTime, camMoved;
    bool camMulti;

    struct T { public int id; public Vector2 pos; public bool began, ended; }

    int stickId = -1;
    Vector2 stickOrigin, stickPos;
    int btnId = -1, runId = -1;
    readonly List<T> touches = new List<T>();

    float Unit => Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 720f);
    Vector2 InteractCenter => new Vector2(Screen.width - 120 * Unit, 150 * Unit);
    Vector2 RunCenter => new Vector2(Screen.width - 260 * Unit, 90 * Unit);
    float BtnR => 62 * Unit;

    /// <summary>Bu ekran noktası (y yukarı) bir kontrolün üstünde mi? (dünyaya tıklamayı engellemek için)</summary>
    public static bool Owns(Vector2 p)
    {
        if (!Visible) return false;
        var tc = inst;
        if (tc == null) return false;
        if ((p - tc.InteractCenter).magnitude < tc.BtnR * 1.2f) return true;
        if ((p - tc.RunCenter).magnitude < tc.BtnR) return true;
        return p.x < Screen.width * 0.42f && p.y < Screen.height * 0.6f;
    }

    static TouchControls inst;
    void Awake() { inst = this; }

    void ReadTouches()
    {
        touches.Clear();
#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts == null) return;
        foreach (TouchControl t in ts.touches)
        {
            var ph = t.phase.ReadValue();
            if (ph == UnityEngine.InputSystem.TouchPhase.None) continue;
            bool ended = ph == UnityEngine.InputSystem.TouchPhase.Ended || ph == UnityEngine.InputSystem.TouchPhase.Canceled;
            touches.Add(new T { id = t.touchId.ReadValue(), pos = t.position.ReadValue(), began = t.press.wasPressedThisFrame, ended = ended || t.press.wasReleasedThisFrame });
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            touches.Add(new T { id = t.fingerId, pos = t.position, began = t.phase == UnityEngine.TouchPhase.Began,
                ended = t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled });
        }
#endif
        if (touches.Count > 0) HtmlInput.NoteTouch();
    }

    void Update()
    {
        InteractDown = false;
        LookDelta = Vector2.zero;
        PinchDelta = 0f;
        tap = false;
        ReadTouches();
        Visible = HtmlInput.Touch && !InputReader.Blocked && !HtmlInput.Open_;
        if (!Visible)
        {
            stickId = btnId = runId = camId = cam2Id = -1;
            Move = Vector2.zero;
            return;
        }

        bool stickAlive = false, btnAlive = false, runAlive = false, camAlive = false, cam2Alive = false;
        Vector2 newCam = camPos, newCam2 = cam2Pos;
        foreach (var t in touches)
        {
            if (t.began)
            {
                if ((t.pos - InteractCenter).magnitude < BtnR * 1.2f) { btnId = t.id; InteractDown = true; }
                else if ((t.pos - RunCenter).magnitude < BtnR) { runId = t.id; Run = !Run; }
                else if (stickId < 0 && t.pos.x < Screen.width * 0.5f && t.pos.y < Screen.height * 0.7f)
                {
                    stickId = t.id;
                    stickOrigin = t.pos;
                    stickPos = t.pos;
                }
                else if (camId < 0)
                {
                    camId = t.id; camPos = newCam = camStart = t.pos;
                    camTime = Time.unscaledTime; camMoved = 0f; camMulti = false;
                }
                else if (cam2Id < 0 && t.id != camId)
                {
                    cam2Id = t.id; cam2Pos = newCam2 = t.pos; camMulti = true;
                }
            }
            if (t.id == camId)
            {
                newCam = t.pos;
                if (t.ended)
                {
                    float u = Unit;
                    if (!camMulti && camMoved < 12f * u && Time.unscaledTime - camTime < 0.35f && GUIUtility.hotControl == 0)
                    { tap = true; tapPos = camStart; }
                    camId = -1;
                }
                else camAlive = true;
            }
            if (t.id == cam2Id) { newCam2 = t.pos; if (t.ended) cam2Id = -1; else cam2Alive = true; }
            if (t.id == stickId)
            {
                if (t.ended) stickId = -1; else { stickAlive = true; stickPos = t.pos; }
            }
            if (t.id == btnId) { if (t.ended) btnId = -1; else btnAlive = true; }
            if (t.id == runId) { if (t.ended) runId = -1; else runAlive = true; }
        }
        if (!stickAlive) stickId = -1;
        if (!btnAlive) btnId = -1;
        if (!runAlive) runId = -1;
        if (!camAlive) camId = -1;
        if (!cam2Alive) cam2Id = -1;

        // Kamera: tek parmak döndürür, iki parmak yakınlaştırır (arayüz öğesi sürüklenmiyorsa)
        if (camId >= 0 || cam2Id >= 0)
        {
            bool ui = GUIUtility.hotControl != 0;
            if (camId >= 0 && cam2Id >= 0)
            {
                PinchDelta = ui ? 0f : (newCam - newCam2).magnitude - (camPos - cam2Pos).magnitude;
            }
            else if (camId >= 0)
            {
                Vector2 d = newCam - camPos;
                camMoved += d.magnitude;
                if (!ui && !camMulti && camMoved > 6f * Unit) LookDelta = d;
            }
            camPos = newCam;
            cam2Pos = newCam2;
        }

        if (stickId >= 0)
        {
            float r = 80f * Unit;
            Vector2 d = Vector2.ClampMagnitude((stickPos - stickOrigin) / r, 1f);
            // Ölü bölge, hafif eğri: küçük hareketlerde ince kontrol
            if (d.magnitude < 0.15f) d = Vector2.zero;
            Move = new Vector2(Mathf.Sign(d.x) * d.x * d.x, d.y);
        }
        else Move = Vector2.zero;
    }

    void OnGUI()
    {
        if (!Visible) return;
        GUI.depth = 10;
        Color old = GUI.color;
        // Ekran koordinatı (y yukarı) → GUI (y aşağı), ölçeksiz piksel
        GUI.matrix = Matrix4x4.identity;
        float u = Unit;

        // Joystick
        Vector2 o = stickId >= 0 ? stickOrigin : new Vector2(150 * u, 150 * u);
        Vector2 k = stickId >= 0 ? stickPos : o;
        Vector2 kd = Vector2.ClampMagnitude(k - o, 80 * u);
        Circle(o, 80 * u, new Color(1, 1, 1, stickId >= 0 ? 0.18f : 0.10f));
        Circle(o + kd, 36 * u, new Color(1f, 0.84f, 0.3f, stickId >= 0 ? 0.75f : 0.4f));
        if (stickId < 0) Label(o + new Vector2(0, -105 * u), Loc.T("yürü"), 14 * u);

        // İşlem
        Circle(InteractCenter, BtnR, btnId >= 0 ? new Color(0.06f, 0.7f, 0.72f, 0.95f) : new Color(0.06f, 0.6f, 0.62f, 0.75f));
        Label(InteractCenter, InteractLabel, 17 * u);
        // Koş
        Circle(RunCenter, BtnR * 0.72f, Run ? new Color(1f, 0.6f, 0.2f, 0.85f) : new Color(1, 1, 1, 0.18f));
        Label(RunCenter, Run ? Loc.T("KOŞ ✓") : Loc.T("KOŞ"), 14 * u);
        GUI.color = old;
    }

    static Texture2D disc;
    static void Circle(Vector2 screenCenter, float r, Color c)
    {
        if (disc == null)
        {
            const int n = 64;
            disc = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f);
                disc.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 20f)));
            }
            disc.Apply();
        }
        GUI.color = c;
        GUI.DrawTexture(new Rect(screenCenter.x - r, Screen.height - screenCenter.y - r, r * 2, r * 2), disc);
    }

    static GUIStyle lab;
    static void Label(Vector2 screenCenter, string text, float size)
    {
        if (lab == null) lab = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        lab.fontSize = Mathf.RoundToInt(size);
        lab.normal.textColor = Color.white;
        GUI.color = Color.white;
        GUI.Label(new Rect(screenCenter.x - 100, Screen.height - screenCenter.y - 20, 200, 40), text, lab);
    }
}
