using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Trade of MTBS hesabı için Google / Apple ile giriş köprüsü (Social.jslib). Sonuç <see cref="Received"/> ile gelir:
/// (sağlayıcı, kimlik jetonu, ad, hata). Açılır pencere engellenip sayfa yönlendirildiyse dönüşte bekleyen sonuç okunur.
/// </summary>
public class SocialLogin : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SocialStart(string provider);
    [DllImport("__Internal")] static extern string SocialPending();
#else
    static void SocialStart(string provider) { Debug.Log("Sosyal giriş yalnız WebGL'de: " + provider); }
    static string SocialPending() => null;
#endif

#if UNITY_WEBGL
    public const bool Available = true;
#else
    public const bool Available = false; // mağaza sürümünde yerel Google/Apple girişi henüz yok: e-posta ve misafir
#endif
    public static SocialLogin I { get; private set; }
    public static event Action<string, string, string, string> Received;
    static string pending;

    public static void Ensure()
    {
        if (I != null) return;
        var g = new GameObject("SocialLogin"); // jslib bu adla SendMessage yapar
        DontDestroyOnLoad(g);
        I = g.AddComponent<SocialLogin>();
        try { pending = SocialPending(); } catch { pending = null; }
    }

    /// <summary>Sayfa yönlendirmeyle döndüyse bekleyen sonucu bir kez verir.</summary>
    public static bool TakePending(out string provider, out string token, out string name, out string error)
    {
        provider = token = name = error = "";
        if (string.IsNullOrEmpty(pending)) return false;
        Split(pending, out provider, out token, out name, out error);
        pending = null;
        return true;
    }

    public static void Begin(string provider)
    {
        Ensure();
        SocialStart(provider);
    }

    static void Split(string msg, out string provider, out string token, out string name, out string error)
    {
        var p = msg.Split('\u0001');
        provider = p.Length > 0 ? p[0] : "";
        token = p.Length > 1 ? p[1] : "";
        name = p.Length > 2 ? p[2] : "";
        error = p.Length > 3 ? p[3] : "";
    }

    public void OnSocial(string msg)
    {
        Split(msg ?? "", out var provider, out var token, out var name, out var error);
        Received?.Invoke(provider, token, name, error);
    }
}
