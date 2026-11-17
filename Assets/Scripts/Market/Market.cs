using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Stock
{
    public long id;
    public string name = "";
    public double price;
    public double change;      // yüzde
    public double volume;
    public double marketCap;
    public double bestBid, bestAsk;
    public readonly List<KeyValuePair<double, long>> bids = new List<KeyValuePair<double, long>>();
    public readonly List<KeyValuePair<double, long>> asks = new List<KeyValuePair<double, long>>();
    public long myQty;         // portföydeki adet
    public long myFree;        // satışa konabilir adet
    public double myCost;      // ortalama maliyet
    public bool detailLoaded;
    public double prevPrice;   // son yenilemedeki fiyat (salonda hareket efekti için)
}

/// <summary>
/// Piyasa verisi. İki kip:
///  - Canlı: Trade of MTBS hesabıyla giriş, gerçek fiyat/portföy, gerçek emir.
///  - Demo: girişsiz gezinti; uydurma şirketler, fiyatlar rastgele yürür, para sanal.
/// </summary>
public class Market : MonoBehaviour
{
    public static Market I { get; private set; }

    public enum Mode { None, Live, Demo }
    public Mode mode = Mode.None;

    public readonly List<Stock> stocks = new List<Stock>();
    public string userName = "";
    public double cash, stockValue, net;
    public float lastRefresh = -999f;
    public string status = "";
    public event Action Changed;
    public event Action LoggedOut;

    public float refreshSeconds = 12f;
    bool refreshing;

    void Awake() { I = this; }

    void Update()
    {
        if (mode == Mode.None) return;
        if (mode == Mode.Demo) { DemoTick(); return; }
        if (!refreshing && Time.time - lastRefresh > refreshSeconds) Refresh();
    }

    // ================================================================ giriş

    public void Login(string email, string pass, Action<string> done)
    {
        Api.I.Post("/tmtbs/auth/giris", new Dictionary<string, object> { { "email", email.Trim() }, { "sifre", pass }, { "cihaz", "salon-webgl" } },
            r => { AcceptLogin(r); done(null); },
            (m, c) => done(m));
    }

    public void SendResetCode(string email, Action<string> done)
    {
        Api.I.Post("/tmtbs/auth/kod-gonder", new Dictionary<string, object> { { "email", email.Trim() }, { "tip", "sifre" } },
            r => done(null), (m, c) => done(m));
    }

    public void ResetPassword(string email, string code, string newPass, Action<string> done)
    {
        Api.I.Post("/tmtbs/auth/sifre-yenile",
            new Dictionary<string, object> { { "email", email.Trim() }, { "kod", code.Trim() }, { "sifre", newPass }, { "cihaz", "salon-webgl" } },
            r => { AcceptLogin(r); done(null); },
            (m, c) => done(m));
    }

    void AcceptLogin(Dictionary<string, object> r)
    {
        Api.I.SetToken(J.S(r, "token"));
        userName = J.S(J.O(r, "user"), "adsoyad");
        StartLive();
    }

    /// <summary>Kayıtlı jeton varsa doğrudan canlı kipe geçer.</summary>
    public void TryResume(Action<bool> done)
    {
        if (!Api.I.HasToken) { done(false); return; }
        Api.I.Get("/tmtbs/ben", r =>
        {
            userName = J.S(J.O(r, "user"), "adsoyad");
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
        if (mode == Mode.Live && Api.I.HasToken) Api.I.Post("/tmtbs/auth/cikis", null, null, null);
        Api.I.SetToken("");
        mode = Mode.None;
        stocks.Clear();
        Changed?.Invoke();
        LoggedOut?.Invoke();
    }

    void StartLive()
    {
        mode = Mode.Live;
        stocks.Clear();
        status = "Canlı veriler yükleniyor…";
        Refresh();
    }

    // ================================================================ canlı veri

    public void Refresh()
    {
        if (mode != Mode.Live || refreshing) return;
        refreshing = true;
        lastRefresh = Time.time;
        Api.I.Get("/tmtbs/piyasa", r =>
        {
            ApplyList(J.A(r, "liste"));
            Api.I.Get("/tmtbs/durum", d =>
            {
                ApplyStatus(d);
                refreshing = false;
                status = "Canlı · " + DateTime.Now.ToString("HH:mm:ss");
                Changed?.Invoke();
            }, OnFail);
        }, OnFail);
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
            if (s.prevPrice <= 0) s.prevPrice = s.price;
        }
        stocks.RemoveAll(s => !seen.Contains(s.id));
        stocks.Sort((a, b) => b.marketCap.CompareTo(a.marketCap));
    }

    void ApplyStatus(Dictionary<string, object> d)
    {
        cash = J.D(d, "para");
        stockValue = J.D(d, "hisse_degeri");
        net = J.D(d, "net_deger");
        string n = J.S(J.O(d, "user"), "adsoyad");
        if (!string.IsNullOrEmpty(n)) userName = n;
        foreach (var s in stocks) { s.myQty = 0; s.myCost = 0; }
        foreach (var p in J.A(d, "portfoy"))
        {
            var s = Find(J.L(p, "id"));
            if (s == null) continue;
            s.myQty = J.L(p, "adet");
            s.myCost = J.D(p, "ort_maliyet");
        }
    }

    public void LoadDetail(Stock s, Action done)
    {
        if (mode == Mode.Demo) { DemoBook(s); done?.Invoke(); return; }
        Api.I.Get("/tmtbs/hisse/" + s.id, r => { ApplyDetail(s, J.O(r, "borsa")); done?.Invoke(); },
            (m, c) => { status = m; done?.Invoke(); });
    }

    void ApplyDetail(Stock s, Dictionary<string, object> b)
    {
        if (b.Count == 0) return;
        if (J.Get(b, "son_fiyat") != null) s.price = J.D(b, "son_fiyat");
        if (J.Get(b, "degisim") != null) s.change = J.D(b, "degisim");
        s.bestBid = J.D(b, "en_iyi_alis");
        s.bestAsk = J.D(b, "en_iyi_satis");
        Fill(s.bids, J.A(b, "alislar"));
        Fill(s.asks, J.A(b, "satislar"));
        var benim = J.O(b, "benim");
        s.myFree = J.L(benim, "bos");
        s.detailLoaded = true;
    }

    static void Fill(List<KeyValuePair<double, long>> dst, List<object> src)
    {
        dst.Clear();
        foreach (var o in src)
            dst.Add(new KeyValuePair<double, long>(J.D(o, "fiyat"), J.L(o, "adet") > 0 ? J.L(o, "adet") : J.L(o, "kalan")));
    }

    /// <summary>
    /// Alışta en iyi satış fiyatından, satışta en iyi alış fiyatından emir verir (karşı taraf yoksa son fiyattan).
    /// Sonuç metni done'a gelir.
    /// </summary>
    public void PlaceOrder(Stock s, bool buy, long qty, Action<string, bool> done)
    {
        double px = buy ? (s.bestAsk > 0 ? s.bestAsk : s.price) : (s.bestBid > 0 ? s.bestBid : s.price);
        if (qty <= 0 || px <= 0) { done("Geçersiz adet ya da fiyat.", false); return; }

        if (mode == Mode.Demo) { DemoOrder(s, buy, qty, px, done); return; }

        var body = new Dictionary<string, object> { { "yon", buy ? "al" : "sat" }, { "fiyat", (long)Math.Round(px) }, { "adet", qty } };
        Api.I.Post("/tmtbs/hisse/" + s.id + "/emir", body, r =>
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

    public Stock Find(long id)
    {
        foreach (var s in stocks) if (s.id == id) return s;
        return null;
    }

    // ================================================================ demo

    static readonly string[] DemoNames =
    {
        "Anadolu Çay", "Ege Tekstil", "Karadeniz Fındık", "Boğaz Lojistik", "Kapadokya Turizm", "Marmara Yazılım",
        "Toros Çimento", "Fırat Enerji", "Trakya Süt", "Akdeniz Narenciye", "Van Halı", "Uludağ İçecek"
    };
    float nextDemoTick;
    readonly Dictionary<long, double> demoOpen = new Dictionary<long, double>();

    public void StartDemo()
    {
        mode = Mode.Demo;
        stocks.Clear();
        demoOpen.Clear();
        userName = "Misafir";
        cash = 100000;
        var rnd = new System.Random(42);
        for (int i = 0; i < DemoNames.Length; i++)
        {
            double p = Math.Round(5 + rnd.NextDouble() * 120);
            var s = new Stock { id = i + 1, name = DemoNames[i], price = p, prevPrice = p, marketCap = p * (20000 + rnd.Next(80000)) };
            demoOpen[s.id] = p;
            stocks.Add(s);
        }
        stocks.Sort((a, b) => b.marketCap.CompareTo(a.marketCap));
        RecalcDemo();
        status = "Demo · sanal para";
        Changed?.Invoke();
    }

    void DemoTick()
    {
        if (Time.time < nextDemoTick) return;
        nextDemoTick = Time.time + 2.5f;
        foreach (var s in stocks)
        {
            s.prevPrice = s.price;
            double step = UnityEngine.Random.Range(-0.025f, 0.026f);
            if (UnityEngine.Random.value < 0.04f) step *= 4f; // ara sıra sert hareket
            s.price = Math.Max(1, Math.Round(s.price * (1 + step)));
            s.change = (s.price / demoOpen[s.id] - 1) * 100.0;
            s.volume += UnityEngine.Random.Range(0, 400) * s.price;
            s.marketCap = s.marketCap * (s.price / Math.Max(1, s.prevPrice));
        }
        RecalcDemo();
        Changed?.Invoke();
    }

    void DemoBook(Stock s)
    {
        s.bids.Clear();
        s.asks.Clear();
        for (int i = 1; i <= 5; i++)
        {
            s.bids.Add(new KeyValuePair<double, long>(Math.Max(1, s.price - i), UnityEngine.Random.Range(10, 900)));
            s.asks.Add(new KeyValuePair<double, long>(s.price + i - 1, UnityEngine.Random.Range(10, 900)));
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
    }
}
