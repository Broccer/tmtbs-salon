using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Cep menüsü (ve evdeki bilgisayar): TMTBS'nin bütün bölümleri — ana sayfa ve bekleyen emirler, piyasa
/// (bankoya git), portföy, haberler, sıralama, yol haritası, düello, sermaye artırımı hakları, profil, yolculuk.
/// </summary>
public partial class SalonDirector
{
    static readonly string[] PhoneTabs = { "Ana", "Piyasa", "Portföy", "Haberler", "Sıralama", "Görevler", "Düello", "Haklar", "Profil", "Git" };
    int phoneTab;
    bool atComputer;
    readonly Dictionary<string, Dictionary<string, object>> pdata = new Dictionary<string, Dictionary<string, object>>();
    readonly Dictionary<string, string> perr = new Dictionary<string, string>();
    readonly HashSet<string> ploading = new HashSet<string>();
    string phoneMsg;
    bool phoneOk;
    string rankPeriod = "hafta";
    bool favOnly;

    void OpenPhone(bool computer)
    {
        OpenPanelCommon(Panel.Phone);
        atComputer = computer;
        phoneMsg = null;
        pdata.Clear();
        perr.Clear();
        LoadTab(phoneTab);
    }

    void Fetch(string key, string path)
    {
        if (ploading.Contains(key)) return;
        ploading.Add(key);
        Market.I.Get(path, (r, err) =>
        {
            ploading.Remove(key);
            if (r != null) { pdata[key] = r; perr.Remove(key); } else perr[key] = err;
        });
    }

    void LoadTab(int t)
    {
        switch (PhoneTabs[t])
        {
            case "Ana": Fetch("ana", "/tmtbs/ana"); break;
            case "Haberler": Fetch("haber", "/tmtbs/haberler"); break;
            case "Sıralama": Fetch("sira", "/tmtbs/siralama?donem=" + rankPeriod); break;
            case "Görevler":
                Fetch("yol", "/tmtbs/yol");
                if (Market.I.Live) Market.I.Post("/tmtbs/yol/goruldu", null, (r, e) => { });
                break;
            case "Düello": Fetch("duello", "/tmtbs/duello"); break;
            case "Profil": Fetch("profil", "/tmtbs/profil/ben"); break;
            case "Portföy": case "Haklar": Market.I.RefreshStatus(); break;
        }
    }

    /// <summary>Yükleniyor / hata / veri. Veri yoksa false döner.</summary>
    bool Data(string key, out Dictionary<string, object> d)
    {
        if (pdata.TryGetValue(key, out d)) return true;
        if (perr.TryGetValue(key, out var e)) { Msg(e, false); return false; }
        GUILayout.Label("yükleniyor…", small);
        return false;
    }

    void DrawPhone()
    {
        var r = PanelRect(520);
        float iw = BeginPanel(r);
        GUILayout.BeginHorizontal();
        GUILayout.Label(atComputer ? "<b>Ev bilgisayarı</b>" : "<b>Cep · Borsa MTBS</b>", h2);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕", btn, GUILayout.Width(44), GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        GUILayout.EndHorizontal();

        int perRow = iw < 380 ? 4 : 5;
        for (int i = 0; i < PhoneTabs.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + perRow && j < PhoneTabs.Length; j++)
                if (GUILayout.Button(PhoneTabs[j], j == phoneTab ? tabOn : tab, GUILayout.Height(34)))
                {
                    phoneTab = j;
                    phoneMsg = null;
                    scroll = Vector2.zero;
                    LoadTab(j);
                }
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6);
        Msg(phoneMsg, phoneOk);

        switch (PhoneTabs[phoneTab])
        {
            case "Ana": TabHome(); break;
            case "Piyasa": TabMarket(); break;
            case "Portföy": TabPortfolio(); break;
            case "Haberler": TabNews(); break;
            case "Sıralama": TabRank(); break;
            case "Görevler": TabRoad(); break;
            case "Düello": TabDuel(); break;
            case "Haklar": TabRights(); break;
            case "Profil": TabProfile(); break;
            case "Git": TabTravel(); break;
        }
        GUILayout.Space(10);
        if (GUILayout.Button("Kapat (Esc)", GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        EndPanel();
    }

    bool NeedLive()
    {
        if (Market.I.Live) return true;
        GUILayout.Label("Bu bölüm hesabınla giriş yapınca açılır. Demo kipinde yalnız piyasa, portföy (sanal) ve yolculuk var.", label);
        return false;
    }

    // ---------------------------------------------------------------- Ana

    void TabHome()
    {
        var m = Market.I;
        Row("Nakit", Fmt.Mls(m.cash));
        Row("Hisseler", Fmt.Mls(m.stockValue));
        Row("Net değer", $"<b><color=#FFD54A>{Fmt.Mls(m.net)}</color></b>");
        if (!NeedLive()) return;
        if (!Data("ana", out var d)) return;
        var oz = J.O(d, "ozet");
        Title("Piyasa");
        Row("Piyasa değeri", Fmt.Mls(J.D(oz, "piyasa_degeri")));
        Row("Değişim", $"<color={Fmt.ChangeColor(J.D(oz, "degisim"))}>{Fmt.Pct(J.D(oz, "degisim"))}</color>");
        Row("Hacim", Fmt.N(J.D(oz, "hacim")));
        Row("Yükselen / düşen", $"<color=#5BE38A>{Fmt.N(J.D(oz, "yukselen"))}</color> / <color=#FF7A7A>{Fmt.N(J.D(oz, "dusen"))}</color>");
        if (J.D(oz, "seri") > 0) Row("Giriş serisi", Fmt.N(J.D(oz, "seri")) + " gün");

        var orders = J.A(d, "emirler");
        Title($"Bekleyen emirlerim ({orders.Count})");
        if (orders.Count == 0) GUILayout.Label("Bekleyen emrin yok.", small);
        foreach (var o in orders)
        {
            bool buy = J.S(o, "yon") == "al";
            long id = J.L(o, "id");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={(buy ? "#5BE38A" : "#FF7A7A")}>{(buy ? "AL" : "SAT")}</color> <b>{J.S(o, "isim")}</b> {Fmt.N(J.D(o, "kalan"))} × {Fmt.N2(J.D(o, "fiyat"))}  <size=12>(son {Fmt.N2(J.D(o, "son_fiyat"))})</size>", label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("İptal", GUILayout.Width(64), GUILayout.Height(30)))
                m.CancelOrder(id, 0, (msg, ok) => { phoneMsg = msg; phoneOk = ok; pdata.Remove("ana"); Fetch("ana", "/tmtbs/ana"); });
            GUILayout.EndHorizontal();
        }

        var news = J.A(d, "haberler");
        if (news.Count > 0)
        {
            Title("Son haberler");
            for (int i = 0; i < news.Count && i < 5; i++) GUILayout.Label("• " + NewsLine(news[i]), small);
        }
    }

    // ---------------------------------------------------------------- Piyasa

    void TabMarket()
    {
        var m = Market.I;
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(!favOnly, " Tümü", GUILayout.Height(28))) favOnly = false;
        if (GUILayout.Toggle(favOnly, " ★ Favoriler", GUILayout.Height(28))) favOnly = true;
        GUILayout.EndHorizontal();
        GUILayout.Label("Bir markaya dokun: bankosuna gidersin. ★ favoriye ekler.", small);
        foreach (var s in m.stocks)
        {
            if (favOnly && !s.favorite) continue;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(s.favorite ? "<color=#FFD54A>★</color>" : "☆", GUILayout.Width(36), GUILayout.Height(32)))
                m.ToggleFavorite(s, null);
            if (GUILayout.Button($"<b>{s.name}</b>   {Fmt.N2(s.price)}  <color={Fmt.ChangeColor(s.change)}>{Fmt.Pct(s.change)}</color>",
                new GUIStyle(btn) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal }, GUILayout.Height(32)))
                GoToCounter(s);
            GUILayout.EndHorizontal();
        }
    }

    // ---------------------------------------------------------------- Portföy

    void TabPortfolio()
    {
        var m = Market.I;
        Row("Hisse değeri", Fmt.Mls(m.stockValue));
        if (m.summary.Count > 0) Dump(m.summary);
        if (m.portfolio.Count == 0) { GUILayout.Label("Portföyün boş. Bir bankoya gidip hisse al.", label); return; }
        foreach (var p in m.portfolio)
        {
            double kz = J.D(p, "kz");
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{J.S(p, "isim")}</b>  {Fmt.N(J.D(p, "adet"))} adet", label);
            GUILayout.FlexibleSpace();
            var st = m.Find(J.L(p, "id"));
            if (st != null && GUILayout.Button("Bankoya git", GUILayout.Width(110), GUILayout.Height(30))) GoToCounter(st);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Fiyat {Fmt.N2(J.D(p, "son_fiyat"))} · maliyet {Fmt.N2(J.D(p, "ort_maliyet"))} · değer {Fmt.Mls(J.D(p, "deger"))} · " +
                $"<color={Fmt.ChangeColor(kz)}>K/Z {Fmt.N(kz)} ({Fmt.Pct(J.D(p, "kz_yuzde"))})</color>", small);
        }
    }

    // ---------------------------------------------------------------- Haberler

    static readonly Dictionary<string, string> NewsKinds = new Dictionary<string, string>
    {
        { "arz", "Halka arz" }, { "artirim", "Sermaye artırımı" }, { "temettu", "Temettü" },
        { "bolunme", "Bölünme" }, { "fabrika", "Fabrika" }, { "esik", "Eşik" },
    };

    string NewsLine(object n)
    {
        if (!(n is Dictionary<string, object> d)) return Val(n);
        string tur = J.S(d, "tur");
        string kind = NewsKinds.TryGetValue(tur, out var k) ? k : Pretty(tur);
        string text = J.S(d, "metin") != "" ? J.S(d, "metin") : J.S(d, "baslik");
        var extra = new List<string>();
        foreach (var kv in d)
        {
            if (kv.Key == "tur" || kv.Key == "isim" || kv.Key == "tarih" || kv.Key == "metin" || kv.Key == "baslik" || kv.Key == "id" || kv.Key == "magaza_id" || kv.Key == "logo_url") continue;
            if (kv.Value is double || kv.Value is string) extra.Add(Pretty(kv.Key) + " " + Val(kv.Value));
            if (extra.Count >= 3) break;
        }
        return $"<b>{kind}</b> · {J.S(d, "isim")} {(text != "" ? "· " + text : "")} {(extra.Count > 0 ? "· " + string.Join(", ", extra) : "")} <color=#7d879c>{ShortDate(J.S(d, "tarih"))}</color>";
    }

    void TabNews()
    {
        if (!NeedLive()) return;
        if (!Data("haber", out var d)) return;
        var list = J.A(d, "items");
        if (list.Count == 0) list = J.A(d, "haberler");
        if (list.Count == 0) list = J.A(d, "liste");
        if (list.Count == 0) { GUILayout.Label("Haber yok.", small); return; }
        for (int i = 0; i < list.Count && i < 40; i++) { GUILayout.Label(NewsLine(list[i]), label); GUILayout.Space(2); }
    }

    // ---------------------------------------------------------------- Sıralama

    void TabRank()
    {
        if (!NeedLive()) return;
        GUILayout.BeginHorizontal();
        foreach (var (k, n) in new[] { ("gun", "Gün"), ("hafta", "Hafta"), ("ay", "Ay"), ("yil", "Yıl") })
            if (GUILayout.Button(n, k == rankPeriod ? tabOn : tab, GUILayout.Height(32)))
            {
                rankPeriod = k;
                pdata.Remove("sira");
                Fetch("sira", "/tmtbs/siralama?donem=" + k);
            }
        GUILayout.EndHorizontal();
        if (!Data("sira", out var d)) return;
        var ben = J.Get(d, "ben");
        if (ben is Dictionary<string, object> bd && bd.Count > 0)
            Row("Senin sıran", $"<b>{Fmt.N(J.D(bd, "sira"))}.</b> · kazanç {Fmt.N(J.D(bd, "kazanc"))}");
        foreach (var o in J.A(d, "liste"))
        {
            bool me = J.B(o, "ben");
            string line = $"{Fmt.N(J.D(o, "sira"))}. {J.S(o, "ad")}";
            Row(me ? $"<color=#FFD54A><b>{line}</b></color>" : line,
                $"<color={Fmt.ChangeColor(J.D(o, "kazanc"))}>{Fmt.N(J.D(o, "kazanc"))}</color>  <size=12>net {Fmt.Short(J.D(o, "net"))}</size>");
        }
    }

    // ---------------------------------------------------------------- Görevler (yol haritası)

    void TabRoad()
    {
        if (!NeedLive()) return;
        if (!Data("yol", out var d)) return;
        var y = J.O(d, "yol");
        Row("Tamamlanan", $"{Fmt.N(J.D(y, "tamam_sayisi"))} / {Fmt.N(J.D(y, "toplam_gorev"))}");
        Row("Kazanılan ödül", $"{Fmt.Mls(J.D(y, "kazanilan"))} / {Fmt.Mls(J.D(y, "toplam_odul"))}");
        var next = J.Get(y, "sonraki");
        if (next is Dictionary<string, object> nd && nd.Count > 0) Row("Sıradaki", Pretty(J.S(nd, "key")));
        else if (next is string ns && ns != "") Row("Sıradaki", Pretty(ns));
        foreach (var g in J.A(y, "gorevler"))
        {
            bool done = J.B(g, "tamam");
            string mark = done ? "<color=#5BE38A>✓</color>" : "○";
            string yeni = J.B(g, "yeni") ? " <color=#FFD54A>yeni</color>" : "";
            Row($"{mark} {Pretty(J.S(g, "key"))}{yeni}", Fmt.Mls(J.D(g, "odul")));
        }
    }

    // ---------------------------------------------------------------- Düello

    void TabDuel()
    {
        if (!NeedLive()) return;
        if (!Data("duello", out var d)) return;
        double fee = J.D(d, "ucret");
        GUILayout.Label($"İki oyuncu aynı başlangıç parasıyla bir süre işlem yapar; daha çok kazanan ödülü alır. Katılım ücreti {Fmt.Mls(fee)}. Düello hesabıyla işlem için bankodaki panelde hesabı seç.", small);
        if (GUILayout.Button("Yeni düello aç (" + Fmt.Mls(fee) + ")", btn, GUILayout.Height(40)))
            DuelPost("/tmtbs/duello", new Dictionary<string, object>(), "Düello açıldı, rakip bekleniyor.");

        var mine = J.A(d, "benim");
        Title($"Düellolarım ({mine.Count})");
        if (mine.Count == 0) GUILayout.Label("Düellon yok.", small);
        foreach (var o in mine)
        {
            long id = J.L(o, "id");
            string durum = J.S(o, "durum");
            GUILayout.BeginHorizontal();
            string sonuc = J.S(o, "sonuc") != "" ? " · " + J.S(o, "sonuc") : "";
            GUILayout.Label($"#{id} · {Pretty(durum)} · rakip <b>{(J.S(o, "rakip") != "" ? J.S(o, "rakip") : "—")}</b> · ödül {Fmt.N(J.D(o, "odul"))}{sonuc}" +
                (J.S(o, "bitis") != "" ? $" <size=12>bitiş {ShortDate(J.S(o, "bitis"))}</size>" : ""), small);
            GUILayout.FlexibleSpace();
            if (J.B(o, "davet_bana") && GUILayout.Button("Kabul", GUILayout.Width(70), GUILayout.Height(30)))
                DuelPost("/tmtbs/duello/" + id + "/katil", null, "Düelloya katıldın.");
            if (durum == "bekliyor" && GUILayout.Button("Vazgeç", GUILayout.Width(76), GUILayout.Height(30)))
                DuelPost("/tmtbs/duello/" + id + "/vazgec", null, "Düellodan vazgeçildi.");
            GUILayout.EndHorizontal();
        }

        var salon = J.A(d, "salon");
        Title($"Açık düellolar ({salon.Count})");
        if (salon.Count == 0) GUILayout.Label("Şu an rakip arayan yok.", small);
        foreach (var o in salon)
        {
            long id = J.L(o, "id");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{J.S(o, "acan")}</b> · ücret {Fmt.N(J.D(o, "ucret"))} · ödül {Fmt.N(J.D(o, "odul"))} · {Fmt.N(J.D(o, "sure_gun"))} gün", small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Katıl", GUILayout.Width(70), GUILayout.Height(30)))
                DuelPost("/tmtbs/duello/" + id + "/katil", null, "Düelloya katıldın.");
            GUILayout.EndHorizontal();
        }
    }

    void DuelPost(string path, Dictionary<string, object> body, string okMsg)
    {
        Market.I.Post(path, body, (r, err) =>
        {
            phoneMsg = err ?? okMsg;
            phoneOk = err == null;
            pdata.Remove("duello");
            Fetch("duello", "/tmtbs/duello");
            Market.I.RefreshStatus();
        });
    }

    // ---------------------------------------------------------------- Haklar (sermaye artırımı)

    void TabRights()
    {
        if (!NeedLive()) return;
        var m = Market.I;
        GUILayout.Label("Sahip olduğun markalar sermaye artırımına gidince yeni hisse alma hakkın doğar. Katılırsan hakkın kadar hisseyi bu fiyattan alırsın.", small);
        if (m.rights.Count == 0) { GUILayout.Label("Bekleyen hakkın yok.", label); return; }
        foreach (var h in m.rights)
        {
            long id = J.L(h, "artirim_id");
            long hak = J.L(h, "hak");
            GUILayout.Space(4);
            GUILayout.Label($"<b>{J.S(h, "isim")}</b> · {Fmt.N(hak)} hak × {Fmt.N2(J.D(h, "fiyat"))} = {Fmt.Mls(J.D(h, "tutar"))} · {Fmt.N(J.D(h, "kalan_ay"))} ay kaldı", label);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
            if (GUILayout.Button("Katıl", btn, GUILayout.Height(36)))
                m.AnswerRight(id, true, hak, (msg, ok) => { phoneMsg = msg; phoneOk = ok; });
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Vazgeç", GUILayout.Height(36)))
                m.AnswerRight(id, false, 0, (msg, ok) => { phoneMsg = msg; phoneOk = ok; });
            GUILayout.EndHorizontal();
        }
    }

    // ---------------------------------------------------------------- Profil

    void TabProfile()
    {
        var m = Market.I;
        GUILayout.Label($"<b>{m.userName}</b>  {(m.Live ? "<color=#5BE38A>CANLI</color>" : "<color=#FFB74D>DEMO</color>")}", h2);
        if (m.Live && Data("profil", out var d))
        {
            var p = J.O(d, "profil");
            Dump(p.Count > 0 ? p : d, "adsoyad", "email");
            foreach (var kv in (p.Count > 0 ? p : d))
                if (kv.Value is Dictionary<string, object> sub && sub.Count > 0) { Title(Pretty(kv.Key)); Dump(sub); }
        }
        GUILayout.Space(10);
        if (GUILayout.Button(m.Live ? "Çıkış yap" : "Hesabınla giriş yap", btn, GUILayout.Height(42)))
        {
            ClosePanel();
            m.Logout();
        }
    }

    // ---------------------------------------------------------------- Yolculuk

    void TabTravel()
    {
        GUILayout.Label("Hızlı git (telefonda uzun yürümek istemezsen):", small);
        if (GUILayout.Button("Evim", btn, GUILayout.Height(42))) { GoTo(Town.HomeSpawn, Town.HomeFacing); Show("Evin", 1.5f); }
        if (GUILayout.Button("MTBS Bankası · vezne", btn, GUILayout.Height(42)))
        {
            GoTo(new Vector3(Town.BankX + 2.3f, 0, Town.Z - 2.4f), Quaternion.LookRotation(Vector3.right));
            Show("MTBS Bankası", 1.5f);
        }
        if (GUILayout.Button("Borsa MTBS · bina girişi", btn, GUILayout.Height(42)))
        {
            GoTo(new Vector3(0, 0, SeljukBuilding.ZF - 4f), Quaternion.identity);
            Show("Borsa MTBS", 1.5f);
        }
        if (GUILayout.Button("Seans salonu · panoların önü", btn, GUILayout.Height(42)))
        {
            GoTo(new Vector3(0, 0, 26f), Quaternion.identity);
            Show("Seans salonu", 1.5f);
        }
        GUILayout.Label("Bir markanın bankosuna gitmek için \"Piyasa\" bölümünden markaya dokun.", small);
    }
}
