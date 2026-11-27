using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Telefonda yazı alanları: IMGUI TextField sanal klavye açmaz. Dokunmatik cihazda alan bir düğme gibi çizilir,
/// dokununca sayfanın üstünde gerçek bir HTML kutusu açılır (HtmlInput.jslib), sonuç buraya döner.
/// Masaüstünde normal TextField kullanılır.
/// </summary>
public class HtmlInput : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void HtmlInputOpen(string id, string value, string title, int password, int numeric);
#endif

    static HtmlInput inst;
    static readonly Dictionary<string, string> results = new Dictionary<string, string>();
    static bool touchSeen;
    public static bool Open_ { get; private set; }

    /// <summary>Dokunmatik arayüz mü (telefon/tablet ya da ekrana dokunulmuş).</summary>
    public static bool Touch
    {
        get { return Application.isMobilePlatform || touchSeen; }
    }

    public static void NoteTouch() { touchSeen = true; }

    public static void Ensure()
    {
        if (inst != null) return;
        var g = new GameObject("HtmlInput"); // jslib bu adla SendMessage yapar
        DontDestroyOnLoad(g);
        inst = g.AddComponent<HtmlInput>();
    }

    /// <summary>
    /// Yazı alanı. Masaüstünde TextField; dokunmatikte düğme → HTML giriş kutusu.
    /// </summary>
    public static string Field(string id, string value, string title, GUIStyle style, bool password = false, bool numeric = false, int max = 120, params GUILayoutOption[] opts)
    {
        if (results.TryGetValue(id, out var r))
        {
            results.Remove(id);
            value = r.Length > max ? r.Substring(0, max) : r;
            GUI.changed = true;
        }
        if (!Touch)
        {
            return password ? GUILayout.PasswordField(value ?? "", '•', max, style, opts) : GUILayout.TextField(value ?? "", max, style, opts);
        }
        string shown = string.IsNullOrEmpty(value) ? "<color=#7d879c>dokun ve yaz…</color>" : (password ? new string('•', value.Length) : value);
        var st = new GUIStyle(style) { richText = true };
        if (GUILayout.Button(shown, st, opts)) Open(id, value ?? "", title, password, numeric);
        return value;
    }

    public static void Open(string id, string value, string title, bool password, bool numeric)
    {
        Ensure();
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            WebGLInput.captureAllKeyboardInput = false;
            Open_ = true;
            HtmlInputOpen(id, value, title, password ? 1 : 0, numeric ? 1 : 0);
        }
        catch (Exception e) { Debug.LogWarning(e); Open_ = false; WebGLInput.captureAllKeyboardInput = true; }
#elif UNITY_ANDROID || UNITY_IOS
        // Mağaza sürümü: telefonun kendi klavyesi
        try
        {
            kbId = id;
            kb = TouchScreenKeyboard.Open(value, numeric ? TouchScreenKeyboardType.NumberPad : TouchScreenKeyboardType.Default, false, false, password, false, title);
            Open_ = kb != null;
        }
        catch (Exception e) { Debug.LogWarning(e); Open_ = false; kb = null; }
#endif
    }

#if UNITY_ANDROID || UNITY_IOS
    static TouchScreenKeyboard kb;
    static string kbId;

    void Update()
    {
        if (kb == null) return;
        var st = kb.status;
        if (st == TouchScreenKeyboard.Status.Visible) return;
        if (st == TouchScreenKeyboard.Status.Done) results[kbId] = kb.text ?? "";
        kb = null;
        Open_ = false;
    }
#endif

    // jslib: "1" + id + "\u0001" + değer (1 = Tamam, 0 = Vazgeç)
    public void OnHtmlInput(string msg)
    {
        Open_ = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = true;
#endif
        if (string.IsNullOrEmpty(msg)) return;
        int sep = msg.IndexOf('\u0001');
        if (sep < 1) return;
        bool ok = msg[0] == '1';
        string id = msg.Substring(1, sep - 1);
        string v = msg.Substring(sep + 1);
        if (ok) results[id] = v;
    }
}
