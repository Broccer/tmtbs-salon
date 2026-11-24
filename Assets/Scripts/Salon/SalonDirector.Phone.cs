using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Cep menüsü (ve evdeki bilgisayar): TMTBS'nin bütün bölümleri — ana sayfa ve bekleyen emirler, piyasa
/// (bankoya git), portföy, haberler, sıralama, yol haritası, düello, sermaye artırımı hakları, profil, yolculuk.
/// </summary>
public partial class SalonDirector
{
    static readonly string[] PhoneTabs = { "Hayat", "İş", "Alışveriş", "Emlak", "Galeri", "Müzayede", "Ulaşım", "Ana", "Piyasa", "Portföy", "Haberler", "Sıralama", "Görevler", "Haklar", "Profil" };
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
        placeTab = null;
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

    void LoadTab(int t) { LoadTabName(PhoneTabs[t]); }

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
        GUILayout.Label(placeTab != null ? "<b>" + placeTitle + "</b>" : atComputer ? "<b>Ev bilgisayarı</b>" : "<b>Telefon · Müteşebbis</b>", h2);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕", btn, GUILayout.Width(44), GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        GUILayout.EndHorizontal();

        int perRow = iw < 380 ? 4 : 5;
        if (placeTab == null)
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

        DrawTabName(CurrentTab);
        GUILayout.Space(10);
        if (GUILayout.Button("Kapat (Esc)", GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        EndPanel();
    }

    bool NeedLive()
    {
        if (Market.I.Live) return true;
        GUILayout.Label("Bu bölüm hesabınla giriş yapınca açılır. Demo kipinde yalnız piyasa, portföy (sanal), otobüs ve evdekiler var.", label);
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
                m.CancelOrder(id, 0, (msg, ok) => { phoneMsg = msg; phoneOk = ok; pdata.Remove("ana"); Fetch("ana", "/dunya/piyasa"); });
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

}
