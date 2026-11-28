using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Trade of MTBS hesabı için Google / Apple ile giriş (Trade of MTBS uygulamasıyla aynı sunucu uçları).
/// Web: Social.jslib (mutesebbis.tr köprü sayfası). Android: Credential Manager ile Google (Plugins/Android/SocialAuth.androidlib).
/// iOS: Apple (ASAuthorization) + Google (ASWebAuthenticationSession, PKCE) — Plugins/iOS/SocialAuth.mm.
/// Sonuç <see cref="Received"/> ile gelir: (sağlayıcı, kimlik jetonu, ad, hata). Hata "iptal" ise kullanıcı vazgeçmiştir.
/// </summary>
public class SocialLogin : MonoBehaviour
{
    /// <summary>Google web istemcisi (GCP projesi trade-of-mtbs). Android'de ID token'ın aud'u budur; sunucu kabul eder.</summary>
    public const string GoogleServerClientId = "935868935496-9jca4ostje38297p4sc7k6tvj52c577n.apps.googleusercontent.com";

    /// <summary>iOS OAuth istemcisi (Bundle ID com.bavsla.tmtbs3d). Boşsa iOS'ta Google düğmesi gizlenir.</summary>
    public const string GoogleIosClientId = "935868935496-2gnpu0ub1g8b6sce7i19flfbsjm9npuc.apps.googleusercontent.com";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SocialStart(string provider);
    [DllImport("__Internal")] static extern string SocialPending();
#elif UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void TmSocialApple();
    [DllImport("__Internal")] static extern void TmSocialGoogle(string iosClientId);
    static void SocialStart(string provider)
    {
        if (provider == "apple") TmSocialApple();
        else TmSocialGoogle(GoogleIosClientId);
    }
    static string SocialPending() => null;
#elif UNITY_ANDROID && !UNITY_EDITOR
    static void SocialStart(string provider)
    {
        if (provider != "google") { Deliver(provider, "", "", "Bu cihazda desteklenmiyor"); return; }
        try
        {
            using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var act = up.GetStatic<AndroidJavaObject>("currentActivity");
            using var cls = new AndroidJavaClass("com.bavsla.social.SocialAuth");
            cls.CallStatic("google", act, GoogleServerClientId, new AndroidCallback());
        }
        catch (Exception e) { Deliver("google", "", "", e.Message); }
    }
    static string SocialPending() => null;

    class AndroidCallback : AndroidJavaProxy
    {
        public AndroidCallback() : base("com.bavsla.social.SocialAuth$Callback") { }
        // Java tarafında arka iş parçacığından çağrılır: ana iş parçacığına Update'te aktarılır.
        public void onResult(string provider, string token, string name, string error) => Deliver(provider, token, name, error);
    }
#else
    static void SocialStart(string provider) { Deliver(provider, "", "", "Sosyal giriş bu platformda yok"); }
    static string SocialPending() => null;
#endif

#if UNITY_WEBGL || UNITY_ANDROID
    public const bool Available = true;
#elif UNITY_IOS
    public const bool Available = true;
#else
    public const bool Available = false;
#endif

    /// <summary>Google düğmesi gösterilsin mi (iOS'ta istemci kimliği tanımlıysa).</summary>
    public static bool GoogleAvailable
    {
        get
        {
#if UNITY_IOS
            return GoogleIosClientId.Length > 0;
#else
            return Available;
#endif
        }
    }

    /// <summary>Apple ile giriş yalnız web ve iOS'ta (Trade of MTBS uygulamasında olduğu gibi Android'de yok).</summary>
    public static bool AppleAvailable
    {
        get
        {
#if UNITY_WEBGL || UNITY_IOS
            return true;
#else
            return false;
#endif
        }
    }

    public static SocialLogin I { get; private set; }
    public static event Action<string, string, string, string> Received;
    static string pending;
    static readonly object gate = new object();
    static string queued;

    public static void Ensure()
    {
        if (I != null) return;
        var g = new GameObject("SocialLogin"); // jslib / iOS eklentisi bu adla SendMessage yapar
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

    /// <summary>Herhangi bir iş parçacığından sonucu sıraya koyar; Update'te ana iş parçacığında dağıtılır.</summary>
    static void Deliver(string provider, string token, string name, string error)
    {
        lock (gate) queued = string.Join("\u0001", provider ?? "", token ?? "", name ?? "", error ?? "");
    }

    void Update()
    {
        string m;
        lock (gate) { m = queued; queued = null; }
        if (m != null) OnSocial(m);
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
