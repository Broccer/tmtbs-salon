using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Stock
{
    public long id;
    public string name = "";
    public double price;
    public double change;      // yüzde (aylık)
    public double volume;
    public double marketCap;
    public double bestBid, bestAsk;
    public readonly List<KeyValuePair<double, long>> bids = new List<KeyValuePair<double, long>>();
    public readonly List<KeyValuePair<double, long>> asks = new List<KeyValuePair<double, long>>();
    public long myQty;         // portföydeki adet (emirdekiler dahil)
    public long myFree;        // satışa konabilir adet
    public double myCost;      // ortalama maliyet
    public bool detailLoaded;
    public double prevPrice;   // son yenilemedeki fiyat (salonda hareket efekti için)
    public string logoUrl = "";
    public bool favorite;
    public double minPrice, maxPrice; // günlük fiyat bandı (0 = sınırsız)
    /// <summary>Ham detay (/tmtbs/hisse/{id}.borsa): benim.emirler, islemler, marka künyesi, bekleyen_hak…</summary>
    public Dictionary<string, object> detail = new Dictionary<string, object>();
    public int order = -1;     // salondaki banko sırası (ilk yüklemede sabitlenir)
}

/// <summary>
/// Piyasa verisi ve TMTBS sunucusunun bütün işlevleri. İki kip:
///  - Canlı: Trade of MTBS hesabıyla giriş, gerçek fiyat/portföy, gerçek emir, kredi, sıralama…
///  - Demo: girişsiz gezinti; gerçek fiyatlar (/tmtbs/vitrin), sanal para.
/// </summary>
public class Market : MonoBehaviour
{
    public static Market I { get; private set; }

    public enum Mode { None, Live, Demo }
    public Mode mode = Mode.None;

    /// <summary>Hangi hesapla girildi: Trade of MTBS hesabı yalnız borsa binasında oynar; Müteşebbis hesabı bütün şehirde.</summary>
    public enum Account { Mutesebbis, Tmtbs }
    public Account account = Account.Mutesebbis;
    public bool Tm => account == Account.Tmtbs;
    const string AccountKey = "mutesebbis_dunya_hesap";
    /// <summary>TMTBS kredi durumu (yoksa boş).</summary>
    public Dictionary<string, object> credit = new Dictionary<string, object>();

    public readonly List<Stock> stocks = new List<Stock>();
    public string userName = "";
    public long userId;
    public double cash, stockValue, net;
    /// <summary>Son /dashboard yanıtı: user, time, is, ev, ulasim, yemek, egitim, magaza…</summary>
    public Dictionary<string, object> dash = new Dictionary<string, object>();
    public int dashVersion;
    public List<object> rights = new List<object>();                               // bekleyen sermaye artırımı hakları
    public List<object> portfolio = new List<object>();
    public Dictionary<string, object> summary = new Dictionary<string, object>();  // portföy özeti
    public float lastRefresh = -999f;
    public string status = "";
    public event Action Changed;
    public event Action LoggedOut;

    public float refreshSeconds = 12f;
    bool refreshing;
    int nextOrder;

    public bool Live => mode == Mode.Live;

    void Awake() { I = this; }

    void Update()
    {
        if (mode == Mode.None) return;
        if (!refreshing && Time.time - lastRefresh > refreshSeconds)
        {
            if (mode == Mode.Demo) DemoRefresh(); else Refresh();
        }
    }

    // ================================================================ giriş

    static Dictionary<string, object> Body(params object[] kv)
    {
        var d = new Dictionary<string, object>();
        for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
        return d;
    }

    void SetAccount(Account a)
    {
        account = a;
        try { PlayerPrefs.SetString(AccountKey, a == Account.Tmtbs ? "tm" : "mt"); PlayerPrefs.Save(); } catch { }
    }

    public void Login(Account a, string email, string pass, Action<string> done)
    {
        string path = a == Account.Tmtbs ? "/tmtbs/auth/giris" : "/auth/login";
        Api.I.Post(path, Body("email", email.Trim(), "sifre", pass, "cihaz", "dunya-webgl"),
            r => { SetAccount(a); AcceptLogin(r); done(null); },
            (m, c) => done(m));
    }

    /// <summary>Müteşebbis kaydı hemen giriş yapar (done(null, false)); TMTBS kaydı e-posta kodu bekler (done(null, true)).</summary>
    public void Register(Account a, string name, string email, string pass, Action<string, bool> done)
    {
        if (a == Account.Tmtbs)
        {
            Api.I.Post("/tmtbs/auth/kayit", Body("adsoyad", name.Trim(), "email", email.Trim(), "sifre", pass),
                r => done(null, true), (m, c) => done(m, false));
            return;
        }
        Api.I.Post("/auth/register", Body("adsoyad", name.Trim(), "email", email.Trim(), "sifre", pass, "sifre_tekrar", pass, "ulke", "Türkiye", "cihaz", "dunya-webgl"),
            r => { SetAccount(a); AcceptLogin(r); done(null, false); }, (m, c) => done(m, false));
    }

    /// <summary>TMTBS: e-postadaki 6 haneli onay koduyla hesabı açar ve girer.</summary>
    public void VerifyTm(string email, string code, Action<string> done)
    {
        Api.I.Post("/tmtbs/auth/dogrula", Body("email", email.Trim(), "kod", code.Trim(), "cihaz", "dunya-webgl"),
            r => { SetAccount(Account.Tmtbs); AcceptLogin(r); done(null); }, (m, c) => done(m));
    }

    /// <summary>Müteşebbis: sıfırlama bağlantısı; TMTBS: 6 haneli kod.</summary>
    public void SendResetCode(Account a, string email, Action<string> done)
    {
        if (a == Account.Tmtbs)
            Api.I.Post("/tmtbs/auth/kod-gonder", Body("email", email.Trim(), "tip", "sifre"), r => done(null), (m, c) => done(m));
        else
            Api.I.Post("/auth/sifremi-unuttum", Body("email", email.Trim()), r => done(null), (m, c) => done(m));
    }

    public void ResetPasswordTm(string email, string code, string newPass, Action<string> done)
    {
        Api.I.Post("/tmtbs/auth/sifre-yenile", Body("email", email.Trim(), "kod", code.Trim(), "sifre", newPass, "cihaz", "dunya-webgl"),
            r => { SetAccount(Account.Tmtbs); AcceptLogin(r); done(null); }, (m, c) => done(m));
    }

    /// <summary>TMTBS: Google / Apple kimlik jetonuyla giriş (hesap yoksa açılır).</summary>
    public void SocialLoginTm(string provider, string token, string name, Action<string> done)
    {
        var body = provider == "apple" ? Body("identity_token", token, "adsoyad", name ?? "", "cihaz", "dunya-webgl")
                                       : Body("id_token", token, "adsoyad", name ?? "", "cihaz", "dunya-webgl");
        Api.I.Post("/tmtbs/auth/" + (provider == "apple" ? "apple" : "google"), body,
            r => { SetAccount(Account.Tmtbs); AcceptLogin(r); done(null); }, (m, c) => done(m));
    }

    void AcceptLogin(Dictionary<string, object> r)
    {
        Api.I.SetToken(J.S(r, "token"));
        userName = J.S(J.O(r, "user"), "adsoyad");
        userId = J.L(J.O(r, "user"), "id");
        StartLive();
    }

    /// <summary>Kayıtlı jeton varsa doğrudan canlı kipe geçer (hangi hesapla girildiği de saklıdır).</summary>
    public void TryResume(Action<bool> done)
    {
        if (!Api.I.HasToken) { done(false); return; }
        string acc = "mt";
        try { acc = PlayerPrefs.GetString(AccountKey, "mt"); } catch { }
        account = acc == "tm" ? Account.Tmtbs : Account.Mutesebbis;
        Api.I.Get(Tm ? "/tmtbs/ben" : "/me", r =>
        {
            userName = J.S(J.O(r, "user"), "adsoyad");
            userId = J.L(J.O(r, "user"), "id");
            StartLive();
            done(true);
        }, (m, c) =>
        {
            if (c == 401) Api.I.SetToken("");
            done(false);
        });
    }


    public void Logout()
    {
        if (mode == Mode.Live && Api.I.HasToken) Api.I.Post(Tm ? "/tmtbs/auth/cikis" : "/auth/logout", null, null, null);
        Api.I.SetToken("");
        mode = Mode.None;
        stocks.Clear();
        dash.Clear();
        credit.Clear();
        rights.Clear();
        portfolio.Clear();
        nextOrder = 0;
        Changed?.Invoke();
        LoggedOut?.Invoke();
    }

    void StartLive()
    {
        mode = Mode.Live;
        stocks.Clear();
        nextOrder = 0;
        status = "Canlı veriler yükleniyor…";
        Refresh();
    }

    // ================================================================ canlı veri

    public void Refresh()
    {
        if (mode != Mode.Live || refreshing) return;
        refreshing = true;
        lastRefresh = Time.time;
        Api.I.Get(Tm ? "/tmtbs/piyasa" : "/borsa", r =>
        {
            ApplyList(J.A(r, "liste"));
            if (!Tm) ApplyBorsa(r);
            else ApplyFavorites(J.A(r, "favoriler"));
            Api.I.Get(Tm ? "/tmtbs/durum" : "/dashboard", d =>
            {
                if (Tm) ApplyTmStatus(d); else ApplyDash(d);
                refreshing = false;
                status = "Canlı · " + DateTime.Now.ToString("HH:mm:ss");
                Changed?.Invoke();
            }, OnFail);
        }, OnFail);
    }

    /// <summary>Yalnız hesap durumunu yeniler (nakit, kredi, portföy).</summary>
    public void RefreshStatus(Action done = null)
    {
        if (mode != Mode.Live) { done?.Invoke(); return; }
        if (Tm)
        {
            Api.I.Get("/tmtbs/durum", d => { ApplyTmStatus(d); Changed?.Invoke(); done?.Invoke(); }, (m, c) => done?.Invoke());
            return;
        }
        Api.I.Get("/dashboard", d =>
        {
            ApplyDash(d);
            Api.I.Get("/borsa", r => { ApplyBorsa(r); Changed?.Invoke(); done?.Invoke(); }, (m, c) => { Changed?.Invoke(); done?.Invoke(); });
        }, (m, c) => done?.Invoke());
    }

    void OnFail(string msg, long code)
    {
        refreshing = false;
        status = "Bağlantı sorunu: " + msg;
        if (code == 401)
        {
            Api.I.SetToken("");
            mode = Mode.None;
            LoggedOut?.Invoke();
        }
        Changed?.Invoke();
    }

    void ApplyList(List<object> list)
    {
        var seen = new HashSet<long>();
        foreach (var o in list)
        {
            long id = J.L(o, "id");
            if (id == 0) continue;
            seen.Add(id);
            var s = Find(id);
            if (s == null) { s = new Stock { id = id }; stocks.Add(s); }
            s.prevPrice = s.price;
            s.name = J.S(o, "isim");
            s.price = J.D(o, "son_fiyat");
            s.change = J.D(o, "degisim");
            s.volume = J.D(o, "hacim");
            s.marketCap = J.D(o, "piyasa_degeri");
            s.logoUrl = J.S(o, "logo_url");
            s.bestBid = J.D(o, "en_iyi_alis");
            s.bestAsk = J.D(o, "en_iyi_satis");
            if (s.prevPrice <= 0) s.prevPrice = s.price;
        }
        stocks.RemoveAll(s => !seen.Contains(s.id));
        stocks.Sort((a, b) => b.marketCap.CompareTo(a.marketCap));
        // Banko sırası ilk gelişte piyasa değerine göre verilir, sonra sabit kalır (yeni marka sona eklenir)
        foreach (var s in stocks) if (s.order < 0) s.order = nextOrder++;
    }

    void ApplyFavorites(List<object> favs)
    {
        if (favs == null) return;
        var set = new HashSet<long>(favs.Select(f => J.L(f)));
        foreach (var s in stocks) s.favorite = set.Contains(s.id);
    }

    void ApplyDash(Dictionary<string, object> d)
    {
        dash = d;
        dashVersion++;
        var u = J.O(d, "user");
        cash = J.D(u, "para");
        string n = J.S(u, "adsoyad");
        if (!string.IsNullOrEmpty(n)) userName = n;
        if (J.L(u, "id") > 0) userId = J.L(u, "id");
        net = cash + stockValue;
    }

    void ApplyTmStatus(Dictionary<string, object> d)
    {
        cash = J.D(d, "para");
        var u = J.O(d, "user");
        string n = J.S(u, "adsoyad");
        if (!string.IsNullOrEmpty(n)) userName = n;
        if (J.L(u, "id") > 0) userId = J.L(u, "id");
        credit = J.O(d, "kredi");
        ApplyBorsa(d);
        stockValue = J.D(d, "hisse_degeri");
        net = J.D(d, "net_deger");
    }

    void ApplyBorsa(Dictionary<string, object> d)
    {
        rights = J.A(d, "haklar");
        portfolio = J.A(d, "portfoy");
        summary = J.O(d, "ozet");
        if (J.Get(d, "favoriler") != null) ApplyFavorites(J.A(d, "favoriler"));
        stockValue = 0;
        foreach (var s in stocks) { s.myQty = 0; s.myCost = 0; }
        foreach (var p in portfolio)
        {
            stockValue += J.D(p, "deger");
            var s = Find(J.L(p, "id"));
            if (s == null) continue;
            s.myQty = J.L(p, "adet");
            s.myCost = J.D(p, "ort_maliyet");
        }
        net = cash + stockValue;
    }

    /// <summary>Nakit değişen bir işlemden sonra (satın alma, harçlık, fatura) hemen yeniden okur.</summary>
    public void Touch() { lastRefresh = -999f; }

    public void LoadDetail(Stock s, Action done, long duel = 0)
    {
        if (mode == Mode.Demo) { DemoBook(s); done?.Invoke(); return; }
        Api.I.Get(Tm ? "/tmtbs/hisse/" + s.id + (duel > 0 ? "?duello=" + duel : "") : "/borsa/" + s.id, r => { ApplyDetail(s, J.O(r, "borsa")); done?.Invoke(); },
            (m, c) => { status = m; done?.Invoke(); });
    }

    void ApplyDetail(Stock s, Dictionary<string, object> b)
    {
        if (b.Count == 0) return;
        s.detail = b;
        if (J.Get(b, "son_fiyat") != null) s.price = J.D(b, "son_fiyat");
        if (J.Get(b, "degisim") != null) s.change = J.D(b, "degisim");
        s.bestBid = J.D(b, "en_iyi_alis");
        s.bestAsk = J.D(b, "en_iyi_satis");
        s.minPrice = J.D(b, "fiyat_min");
        s.maxPrice = J.D(b, "fiyat_max");
        Fill(s.bids, J.A(b, "alislar"));
        Fill(s.asks, J.A(b, "satislar"));
        var benim = J.O(b, "benim");
        s.myFree = J.L(benim, "bos");
        if (J.Get(benim, "adet") != null) s.myQty = J.L(benim, "adet");
        s.detailLoaded = true;
    }

    static void Fill(List<KeyValuePair<double, long>> dst, List<object> src)
    {
        dst.Clear();
        foreach (var o in src)
            dst.Add(new KeyValuePair<double, long>(J.D(o, "fiyat"), J.L(o, "adet") > 0 ? J.L(o, "adet") : J.L(o, "kalan")));
    }

    /// <summary>Piyasa emri: alışta en iyi satış, satışta en iyi alış fiyatından.</summary>
    public void PlaceOrder(Stock s, bool buy, long qty, Action<string, bool> done)
    {
        double px = buy ? (s.bestAsk > 0 ? s.bestAsk : s.price) : (s.bestBid > 0 ? s.bestBid : s.price);
        PlaceLimit(s, buy, px, qty, 0, done);
    }

    /// <summary>Limit emir (fiyat kademeye oturtulur). duel &gt; 0 ise düello hesabıyla.</summary>
    public void PlaceLimit(Stock s, bool buy, double px, long qty, long duel, Action<string, bool> done)
    {
        px = Tick.Snap(px);
        if (qty <= 0 || px <= 0) { done("Geçersiz adet ya da fiyat.", false); return; }

        if (mode == Mode.Demo) { DemoOrder(s, buy, qty, px, done); return; }

        var body = new Dictionary<string, object> { { "yon", buy ? "al" : "sat" }, { "fiyat", px }, { "adet", qty } };
        if (duel > 0 && Tm) body["duello"] = duel;
        Api.I.Post(Tm ? "/tmtbs/hisse/" + s.id + "/emir" : "/borsa/" + s.id + "/emir", body, r =>
        {
            long done_ = J.L(r, "islem_adet");
            ApplyDetail(s, J.O(r, "borsa"));
            string verb = buy ? "alındı" : "satıldı";
            string msg = done_ >= qty ? $"{Fmt.N(done_)} adet {verb}."
                : done_ > 0 ? $"{Fmt.N(done_)} adet {verb}, kalan {Fmt.N(qty - done_)} adet emir defterinde bekliyor."
                : "Emir deftere yazıldı, karşı taraf bekleniyor.";
            done(msg, true);
            lastRefresh = -999f; // hemen yenile
        }, (m, c) => done(m, false));
    }

    public void CancelOrder(long orderId, long duel, Action<string, bool> done)
    {
        if (mode != Mode.Live) { done("Demo kipinde emir defteri yok.", false); return; }
        var body = new Dictionary<string, object>();
        if (duel > 0) body["duello"] = duel;
        if (duel > 0 && Tm) body["duello"] = duel;
        Api.I.Post(Tm ? "/tmtbs/emir/" + orderId + "/iptal" : "/borsa/emir/" + orderId + "/iptal", body, r => { done("Emir iptal edildi.", true); lastRefresh = -999f; }, (m, c) => done(m, false));
    }

    public void ToggleFavorite(Stock s, Action<string> done)
    {
        if (mode != Mode.Live) { s.favorite = !s.favorite; done?.Invoke(null); return; }
        Api.I.Post(Tm ? "/tmtbs/hisse/" + s.id + "/favori" : "/borsa/" + s.id + "/favori", null, r => { s.favorite = J.B(r, "favori"); Changed?.Invoke(); done?.Invoke(null); },
            (m, c) => done?.Invoke(m));
    }

    // ================================================================ diğer ekranlar (canlı)

    /// <summary>Genel GET: sonuç ya da hata metni.</summary>
    public void Get(string path, Action<Dictionary<string, object>, string> done)
    {
        if (mode != Mode.Live) { done(null, "Bu bölüm hesabınla giriş yapınca görünür."); return; }
        Api.I.Get(path, r => done(r, null), (m, c) => done(null, m));
    }

    public void Post(string path, Dictionary<string, object> body, Action<Dictionary<string, object>, string> done)
    {
        if (mode != Mode.Live) { done(null, "Bu işlem hesabınla giriş yapınca yapılabilir."); return; }
        Api.I.Post(path, body, r => done(r, null), (m, c) => done(null, c == 404 ? "Bu işlem sunucuda henüz açık değil." : m));
    }

    public void CreditDraw(Action<string, bool> done)
    {
        Post("/tmtbs/kredi/cek", null, (r, err) =>
        {
            if (err != null) { done(err, false); return; }
            credit = J.O(r, "kredi");
            RefreshStatus();
            done($"{Fmt.Mls(J.D(r, "yatan"))} hesabına yatırıldı.", true);
        });
    }

    public void CreditPay(long amount, Action<string, bool> done)
    {
        Post("/tmtbs/kredi/ode", new Dictionary<string, object> { { "tutar", amount } }, (r, err) =>
        {
            if (err != null) { done(err, false); return; }
            credit = J.O(r, "kredi");
            RefreshStatus();
            done($"{Fmt.Mls(J.D(r, "odenen"))} borcuna ödendi.", true);
        });
    }

    public void AnswerRight(long rightId, bool join, long qty, Action<string, bool> done)
    {
        var body = new Dictionary<string, object> { { "katil", join } };
        if (join && qty > 0) body["adet"] = qty;
        Post((Tm ? "/tmtbs/artirim/" : "/borsa/artirim/") + rightId, body, (r, err) =>
        {
            if (err != null) { done(err, false); return; }
            rights = J.A(r, "haklar");
            RefreshStatus();
            done(join ? "Sermaye artırımına katıldın." : "Haktan vazgeçtin.", true);
        });
    }

    /// <summary>Demo kipinde sanal para ekler (babanın harçlığı).</summary>
    public void DemoGift(double amount)
    {
        if (mode != Mode.Demo) return;
        cash += amount;
        RecalcDemo();
        Changed?.Invoke();
    }

    public Stock Find(long id)
    {
        foreach (var s in stocks) if (s.id == id) return s;
        return null;
    }

    // ================================================================ demo

    public void StartDemo()
    {
        mode = Mode.Demo;
        stocks.Clear();
        nextOrder = 0;
        userName = "Misafir";
        cash = 100000;
        dash = new Dictionary<string, object>();
        credit = new Dictionary<string, object>();
        RecalcDemo();
        status = "Demo · gerçek fiyatlar yükleniyor…";
        Changed?.Invoke();
        DemoRefresh();
    }

    /// <summary>Demo kipi de markaları ve fiyatları sunucudaki gerçek borsadan alır (girişsiz /tmtbs/vitrin); yalnız para ve emirler sanaldır.</summary>
    void DemoRefresh()
    {
        if (refreshing) return;
        refreshing = true;
        lastRefresh = Time.time;
        Api.I.Get("/tmtbs/vitrin", r =>
        {
            var favs = stocks.Where(x => x.favorite).Select(x => x.id).ToList();
            ApplyList(J.A(r, "liste"));
            foreach (var s in stocks) s.favorite = favs.Contains(s.id);
            refreshing = false;
            RecalcDemo();
            status = "Demo · gerçek fiyatlar · sanal para · " + DateTime.Now.ToString("HH:mm:ss");
            Changed?.Invoke();
        }, (m, c) =>
        {
            refreshing = false;
            status = "Demo · bağlantı sorunu: " + m;
            Changed?.Invoke();
        });
    }

    void DemoBook(Stock s)
    {
        s.bids.Clear();
        s.asks.Clear();
        double step = Tick.Step(s.price);
        for (int i = 1; i <= 5; i++)
        {
            s.bids.Add(new KeyValuePair<double, long>(Math.Max(step, Tick.Snap(s.price - i * step)), UnityEngine.Random.Range(10, 900)));
            s.asks.Add(new KeyValuePair<double, long>(Tick.Snap(s.price + (i - 1) * step), UnityEngine.Random.Range(10, 900)));
        }
        s.bestBid = s.bids[0].Key;
        s.bestAsk = s.asks[0].Key;
        s.myFree = s.myQty;
        s.detailLoaded = true;
    }

    void DemoOrder(Stock s, bool buy, long qty, double px, Action<string, bool> done)
    {
        if (buy)
        {
            if (qty * px > cash) { done("Nakit yetmiyor.", false); return; }
            s.myCost = (s.myCost * s.myQty + px * qty) / (s.myQty + qty);
            s.myQty += qty;
            cash -= qty * px;
        }
        else
        {
            if (qty > s.myQty) { done("Elinde o kadar hisse yok.", false); return; }
            s.myQty -= qty;
            cash += qty * px;
            if (s.myQty == 0) s.myCost = 0;
        }
        s.myFree = s.myQty;
        RecalcDemo();
        Changed?.Invoke();
        done($"{Fmt.N(qty)} adet {(buy ? "alındı" : "satıldı")} (demo).", true);
    }

    void RecalcDemo()
    {
        stockValue = stocks.Sum(x => x.myQty * x.price);
        net = cash + stockValue;
        portfolio = stocks.Where(x => x.myQty > 0).Select(x => (object)new Dictionary<string, object>
        {
            { "id", (double)x.id }, { "isim", x.name }, { "adet", (double)x.myQty }, { "son_fiyat", x.price },
            { "deger", x.myQty * x.price }, { "ort_maliyet", x.myCost }, { "kz", x.myQty * (x.price - x.myCost) },
            { "kz_yuzde", x.myCost > 0 ? (x.price - x.myCost) * 100 / x.myCost : 0 },
        }).ToList();
    }
}

/// <summary>Borsa fiyat kademesi: &lt;10 → 0,1 · &lt;50 → 0,5 · &lt;100 → 1 · &lt;500 → 5 · ≥500 → 10.</summary>
public static class Tick
{
    public static double Step(double p) => p < 10 ? 0.1 : p < 50 ? 0.5 : p < 100 ? 1 : p < 500 ? 5 : 10;

    public static double Snap(double p)
    {
        if (p <= 0) return 0;
        double s = Step(p);
        double v = Math.Round(Math.Round(p / s) * s, 1);
        return Math.Max(0.1, v);
    }

    public static double Up(double p) => Snap(Snap(p) + Step(Snap(p)));

    public static double Down(double p)
    {
        double q = Snap(p);
        double s = Step(q - 1e-9);
        return Math.Max(0.1, Snap(q - s));
    }
}
