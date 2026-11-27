using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Trade of MTBS uygulamasında olup salonda eksik kalan bölümler: zengin ana sayfa (endeks, hareketliler,
/// canlı işlem akışı), oyuncu profilleri (performans, başarı kartları, portföy dağılımı, düello daveti),
/// düello ayrıntısı, görev açıklamaları, dil seçimi ve hesap silme.
/// </summary>
public partial class SalonDirector
{
    static string T(string s, params object[] a) => Loc.T(s, a);

    void OnCreditArrived(double amount)
    {
        Show(T("+{0} MLS hesabına yattı!", Fmt.N(amount)), 3.5f);
        if (Bell.I) Bell.I.Ring();
    }

    // ================================================================ logolar (listelerde)

    readonly Dictionary<long, Texture2D> logoTex = new Dictionary<long, Texture2D>();
    readonly HashSet<long> logoAsked = new HashSet<long>();
    GUIStyle initialsSt;

    /// <summary>Satır içi küçük marka logosu; logo yoksa baş harfli renkli rozet.</summary>
    void LogoIcon(long id, string name, float size)
    {
        var r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        if (id > 0 && !logoAsked.Contains(id) && LogoCache.I != null)
        {
            logoAsked.Add(id);
            LogoCache.I.Get(id, t => { if (t != null) logoTex[id] = t; });
        }
        if (Event.current.type != EventType.Repaint) return;
        if (logoTex.TryGetValue(id, out var tex) && tex != null)
        {
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            return;
        }
        if (initialsSt == null) initialsSt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 0, 0) };
        initialsSt.fontSize = Mathf.RoundToInt(size * 0.42f);
        initialsSt.normal.textColor = Color.white;
        Color o = GUI.color;
        GUI.color = LogoCache.BadgeColor(name);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, GUI.color, 0, size * 0.3f);
        GUI.color = o;
        GUI.Label(r, LogoCache.Initials(name), initialsSt);
    }

    // ================================================================ tıklanabilir satır

    float panelDrag;

    /// <summary>Satırın tamamı dokunulabilir (kaydırma sürüklemesi tıklama sayılmaz).</summary>
    bool Clicked()
    {
        var e = Event.current;
        var r = GUILayoutUtility.GetLastRect();
        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) panelDrag = 0;
        if (e.type == EventType.MouseUp && r.Contains(e.mousePosition) && panelDrag < 10f) { e.Use(); return true; }
        return false;
    }

    void HoverTint()
    {
        if (Event.current.type != EventType.Repaint) return;
        var r = GUILayoutUtility.GetLastRect();
        if (!r.Contains(Event.current.mousePosition)) return;
        Color o = GUI.color;
        GUI.color = new Color(1f, 0.85f, 0.4f, 0.07f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = o;
    }

    // ================================================================ kademeler

    static string TierName(int k) => T(Tiers[Mathf.Clamp(k, 1, Tiers.Length) - 1].ad);

    void ProgressBar(float p, Color c, float h = 10)
    {
        var bar = GUILayoutUtility.GetRect(10, h, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint) return;
        GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(1, 1, 1, 0.12f), 0, h / 2);
        if (p > 0.001f) GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(h, bar.width * Mathf.Clamp01(p)), bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, c, 0, h / 2);
    }

    void TierCard(double net)
    {
        int k = TierOf(net);
        GUILayout.Label($"<color=#FFD54A><b>Sv. {k} · {TierName(k)}</b></color>", label);
        if (k < Tiers.Length)
        {
            double lo = Tiers[k - 1].esik, hi = Tiers[k].esik;
            ProgressBar((float)((net - lo) / (hi - lo)), new Color(1f, 0.82f, 0.3f));
            GUILayout.Label(T("{0} kademesine {1} kaldı", TierName(k + 1), Fmt.Mls(System.Math.Max(0, hi - net))), small);
        }
        else GUILayout.Label("<color=#FFD54A>" + T("En üst kademedesin!") + "</color>", small);
    }

    // ================================================================ Ana sayfa (Trade of MTBS)

    int moversTab;
    float liveNext;
    Texture2D indexTex;
    string indexKey;

    void TmHome()
    {
        var m = Market.I;
        GUILayout.Label("<b>" + T("Merhaba, {0}", m.userName != "" ? m.userName : T("Borsacı")) + "</b>", h2);
        TierCard(m.net);
        GUILayout.Space(4);
        Row(T("Nakit"), Fmt.Mls(m.cash));
        Row(T("Hisseler"), Fmt.Mls(m.stockValue));
        double borc = J.D(m.credit, "kalan");
        if (borc > 0) Row(T("Borç"), $"<color=#FF9A9A>{Fmt.Mls(borc)}</color>");
        Row(T("Net değer"), $"<b><color=#FFD54A>{Fmt.Mls(m.net)}</color></b>");

        Dictionary<string, object> d = null;
        if (m.Live)
        {
            if (Time.time > liveNext)
            {
                liveNext = Time.time + 10f;
                Fetch("canli", "/tmtbs/canli?son=0&n=8");
                if (pdata.ContainsKey("ana")) Fetch("ana", "/tmtbs/ana");
            }
            pdata.TryGetValue("ana", out d);
        }
        var oz = d != null ? J.O(d, "ozet") : new Dictionary<string, object>();

        // ---- Endeks
        Title(T("BorsaMTBS Endeksi"));
        double pd = oz.Count > 0 ? J.D(oz, "piyasa_degeri") : m.stocks.Sum(s => s.marketCap);
        double chg = oz.Count > 0 ? J.D(oz, "degisim") : 0;
        GUILayout.Label($"<size=24><b>{Fmt.N2(pd / 1000.0)}</b></size>   <color={Fmt.ChangeColor(chg)}><b>{Fmt.Pct(chg)}</b></color>  <size=12>{T("Bugün")}</size>", label);
        GUILayout.Label(T("Toplam piyasa değerinin binde biri"), small);
        var seri = J.A(oz, "seri");
        if (seri.Count > 1)
        {
            string key = J.S(oz, "zaman");
            if (indexTex == null || indexKey != key)
            {
                if (indexTex != null) Destroy(indexTex);
                var pts = new List<object>();
                foreach (var p in seri) pts.Add(new Dictionary<string, object> { { "f", J.D(p, "d") } });
                indexTex = BuildChartTex(pts, 480, 120, out _, out _);
                indexKey = key;
            }
            var rect = GUILayoutUtility.GetRect(200, 70, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, indexTex, ScaleMode.StretchToFill, true);
            GUILayout.Label(T("Son 7 gün"), small);
        }
        if (oz.Count > 0)
        {
            Row(T("24s hacim"), Fmt.Mls(J.D(oz, "hacim")));
            Row(T("Yükselen / düşen"), $"<color=#5BE38A>{Fmt.N(J.D(oz, "yukselen"))}</color> / <color=#FF7A7A>{Fmt.N(J.D(oz, "dusen"))}</color> / {Fmt.N(J.D(oz, "sabit"))}");
        }

        // ---- Günün hareketlileri
        Title(T("Günün hareketlileri"));
        GUILayout.BeginHorizontal();
        string[] mt = { T("Yükselenler"), T("Düşenler"), T("En çok işlem") };
        for (int i = 0; i < mt.Length; i++) if (TabBtn(mt[i], moversTab == i, 30)) moversTab = i;
        GUILayout.EndHorizontal();
        var rows = new List<(long id, string isim, double fiyat, double deg, double hacim, double islem)>();
        var hs = J.A(oz, "hisseler");
        if (hs.Count > 0)
            foreach (var h in hs) rows.Add((J.L(h, "id"), J.S(h, "isim"), J.D(h, "son_fiyat"), J.D(h, "degisim"), J.D(h, "hacim"), J.D(h, "islem")));
        else
            foreach (var s in m.stocks) rows.Add((s.id, s.name, s.price, s.change, s.volume, s.volume));
        IEnumerable<(long id, string isim, double fiyat, double deg, double hacim, double islem)> sel =
            moversTab == 0 ? rows.Where(x => x.deg > 0).OrderByDescending(x => x.deg)
            : moversTab == 1 ? rows.Where(x => x.deg < 0).OrderBy(x => x.deg)
            : rows.Where(x => x.islem > 0).OrderByDescending(x => x.islem);
        var top = sel.Take(6).ToList();
        if (top.Count == 0) GUILayout.Label(T("Son 24 saatte bu listeye giren hisse yok."), small);
        foreach (var x in top)
        {
            GUILayout.BeginHorizontal();
            LogoIcon(x.id, x.isim, 28);
            GUILayout.Label($"<b>{x.isim}</b>", label);
            GUILayout.FlexibleSpace();
            string extra = moversTab == 2 ? $"<size=12>{T("Hacim {0} · {1} işlem", Fmt.Short(x.hacim), Fmt.N(x.islem))}</size>  " : "";
            GUILayout.Label($"{extra}{Fmt.N2(x.fiyat)}  <color={Fmt.ChangeColor(x.deg)}>{Fmt.Pct(x.deg)}</color>", right);
            GUILayout.EndHorizontal();
            HoverTint();
            if (Clicked()) { var st = m.Find(x.id); if (st != null) GoToCounter(st); }
        }

        if (!m.Live) { GUILayout.Space(6); NeedLive(); return; }

        // ---- Canlı işlem akışı
        Title(T("Canlı işlemler"));
        if (pdata.TryGetValue("canli", out var cd))
        {
            var list = J.A(cd, "islemler");
            if (list.Count == 0) GUILayout.Label(T("Henüz işlem yok."), small);
            foreach (var t in list)
            {
                int yon = (int)J.D(t, "yon");
                string col = yon > 0 ? "#5BE38A" : yon < 0 ? "#FF7A7A" : "#C9D1E0";
                string ok = yon > 0 ? "▲" : yon < 0 ? "▼" : "•";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color=#7d879c>{J.S(t, "zaman")}</color>", small, GUILayout.Width(64));
                LogoIcon(J.L(t, "magaza_id"), J.S(t, "isim"), 22);
                GUILayout.Label($"<b>{J.S(t, "isim")}</b>", small);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{Fmt.N(J.D(t, "adet"))} × <color={col}><b>{Fmt.N2(J.D(t, "fiyat"))}</b> {ok}</color>", right);
                GUILayout.EndHorizontal();
            }
        }
        else GUILayout.Label(T("yükleniyor…"), small);

        if (d == null) { if (!Data("ana", out d)) return; }

        // ---- Açık emirler
        var orders = J.A(d, "emirler");
        Title(T("Açık emirlerim") + $" ({orders.Count})");
        if (orders.Count == 0) GUILayout.Label(T("Bekleyen emrin yok."), small);
        foreach (var o in orders)
        {
            bool buy = J.S(o, "yon") == "al";
            long id = J.L(o, "id");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={(buy ? "#5BE38A" : "#FF7A7A")}>{(buy ? T("AL") : T("SAT"))}</color> <b>{J.S(o, "isim")}</b> " +
                            T("{0}/{1} adet @ {2} · piyasa {3}", Fmt.N(J.D(o, "kalan")), Fmt.N(J.D(o, "adet")), Fmt.N2(J.D(o, "fiyat")), Fmt.N2(J.D(o, "son_fiyat"))), small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("İptal"), GUILayout.Width(70), GUILayout.Height(30)))
                m.CancelOrder(id, 0, (msg, okk) => { phoneMsg = msg; phoneOk = okk; pdata.Remove("ana"); Fetch("ana", "/tmtbs/ana"); });
            GUILayout.EndHorizontal();
        }

        // ---- Haberler
        var news = J.A(d, "haberler");
        Title(T("Piyasa haberleri"));
        if (news.Count == 0) GUILayout.Label(T("Henüz haber yok."), small);
        for (int i = 0; i < news.Count && i < 6; i++) NewsCard(news[i]);
    }

    // ================================================================ Haber biçimi (uygulamayla aynı)

    static string Ago(string tarih)
    {
        if (!System.DateTime.TryParse(tarih, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t)) return ShortDate(tarih);
        var f = System.DateTime.Now - t;
        if (f.TotalMinutes < 1) return T("az önce");
        if (f.TotalMinutes < 60) return T("{0} dk önce", (int)f.TotalMinutes);
        if (f.TotalHours < 24) return T("{0} sa önce", (int)f.TotalHours);
        if (f.TotalDays < 7) return T("{0} gün önce", (int)f.TotalDays);
        return ShortDate(tarih);
    }

    /// <summary>Haber: (etiket, başlık, ayrıntı, renk). Bilinmeyen tür için null.</summary>
    static (string tag, string title, string detail, string color)? NewsParts(Dictionary<string, object> h)
    {
        string ad = J.S(h, "isim");
        switch (J.S(h, "tur"))
        {
            case "arz":
                return (T("Halka arz"), T("{0} borsada işlem görmeye başladı", ad),
                    T("{0} hisse (%{1}) arz edildi · arz fiyatı {2}", Fmt.N(J.D(h, "adet")), Fmt.N(J.D(h, "yuzde")), Fmt.N2(J.D(h, "fiyat"))), "#7CC8FF");
            case "artirim":
                return (T("Sermaye artırımı"), T("{0} bedelli sermaye artırımı yaptı", ad),
                    T("{0} yeni hisse · hisse başı {1} · toplam {2}", Fmt.N(J.D(h, "adet")), Fmt.N2(J.D(h, "fiyat")), Fmt.Mls(J.D(h, "tutar"))), "#B39DFF");
            case "temettu":
                return (T("Kâr payı"), T("{0} kâr payı dağıttı", ad),
                    T("Hisse başı {0} · toplam {1}", Fmt.Mls(J.D(h, "hisse_basi")), Fmt.Mls(J.D(h, "tutar"))), "#5BE38A");
            case "bolunme":
                int k = (int)J.D(h, "kat");
                return (T("Hisse bölünmesi"), T("{0} hissesini 1 → {1} böldü", ad, k),
                    T("Bedelsiz: hisse adedin {0} katına çıkar, hisse başı fiyat {0}'da birine iner; toplam değerin değişmez.", k), "#FFB74D");
            case "fabrika":
                int sv = (int)J.D(h, "seviye");
                return (T("Fabrika"), sv <= 1 ? T("{0} fabrika yapımına başladı", ad) : T("{0} fabrikasını {1}. seviyeye geliştiriyor", ad, sv),
                    sv <= 1 ? T("Yatırım {0} · fabrika birim maliyetini düşürür", Fmt.Mls(J.D(h, "tutar"))) : T("Yatırım {0} · seviye {1}", Fmt.Mls(J.D(h, "tutar")), sv), "#4FC3F7");
            case "esik":
                return (T("Rekor"), T("{0} piyasa değeri {1} sınırını aştı", ad, Fmt.Short(J.D(h, "esik"))),
                    T("Hisse fiyatı {0} seviyesine ulaştı", Fmt.N2(J.D(h, "fiyat"))), "#FFD54A");
        }
        return null;
    }

    void NewsCard(object n)
    {
        if (!(n is Dictionary<string, object> h)) return;
        var p = NewsParts(h);
        GUILayout.BeginHorizontal();
        LogoIcon(J.L(h, "magaza_id"), J.S(h, "isim"), 34);
        GUILayout.Space(4);
        GUILayout.BeginVertical();
        if (p.HasValue)
        {
            GUILayout.Label($"<color={p.Value.color}><b>{p.Value.tag}</b></color>  <color=#7d879c><size=11>{Ago(J.S(h, "tarih"))}</size></color>", small);
            GUILayout.Label("<b>" + p.Value.title + "</b>", label);
            GUILayout.Label(p.Value.detail, small);
        }
        else GUILayout.Label(NewsLine(h), small);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        HoverTint();
        if (Clicked()) { var st = Market.I.Find(J.L(h, "magaza_id")); if (st != null) GoToCounter(st); }
        GUILayout.Space(4);
    }

    void TmNews()
    {
        if (!NeedLive()) return;
        if (!Data("haber", out var d)) return;
        var list = J.A(d, "haberler");
        if (list.Count == 0) { GUILayout.Label(T("Henüz haber yok."), small); return; }
        GUILayout.Label(T("Halka arz, kâr payı, sermaye artırımı ve hisse bölünmeleri fiyatı etkiler. Habere dokun: o markanın bankosuna gidersin."), small);
        for (int i = 0; i < list.Count && i < 40; i++) NewsCard(list[i]);
    }

    // ================================================================ Piyasa: arama + sıralama

    string marketSearch = "";
    int marketSort; // 0 piyasa değeri, 1 yükselen, 2 düşen, 3 hacim, 4 favoriler

    void TmMarket()
    {
        var m = Market.I;
        marketSearch = HtmlInput.Field("ara", marketSearch, T("Hisse ara"), field, false, false, 40, GUILayout.Height(38));
        GUILayout.BeginHorizontal();
        string[] tabs = { T("Tümü"), T("Yükselenler"), T("Düşenler"), T("Hacim"), T("Favoriler") };
        for (int i = 0; i < tabs.Length; i++) if (TabBtn(tabs[i], marketSort == i, 30)) marketSort = i;
        GUILayout.EndHorizontal();
        IEnumerable<Stock> q = m.stocks;
        string sq = (marketSearch ?? "").Trim();
        if (sq.Length > 0) q = q.Where(s => s.name.IndexOf(sq, System.StringComparison.OrdinalIgnoreCase) >= 0);
        switch (marketSort)
        {
            case 1: q = q.Where(s => s.change > 0).OrderByDescending(s => s.change); break;
            case 2: q = q.Where(s => s.change < 0).OrderBy(s => s.change); break;
            case 3: q = q.OrderByDescending(s => s.volume); break;
            case 4: q = q.Where(s => s.favorite); break;
            default: q = q.OrderByDescending(s => s.marketCap); break;
        }
        var list = q.ToList();
        GUILayout.Label(T("Bir markaya dokun: bankosuna gidersin. Yıldız favoriye ekler."), small);
        if (list.Count == 0)
            GUILayout.Label(marketSort == 4 ? T("Henüz favorin yok. Bir hissenin yıldızına dokun.") : T("Sonuç yok."), small);
        foreach (var s in list)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(s.favorite ? "<color=#FFD54A>★</color>" : "☆", GUILayout.Width(36), GUILayout.Height(34)))
                m.ToggleFavorite(s, null);
            LogoIcon(s.id, s.name, 30);
            GUILayout.Label($"<b>{s.name}</b>\n<size=11><color=#9AA6BC>{T("PD {0}", Fmt.Short(s.marketCap))}</color></size>", small);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<b>{Fmt.N2(s.price)}</b>\n<color={Fmt.ChangeColor(s.change)}>{Fmt.Pct(s.change)}</color>", right);
            GUILayout.EndHorizontal();
            HoverTint();
            if (Clicked()) GoToCounter(s);
        }
    }

    // ================================================================ Portföy

    bool portfolioMarked;

    void TmPortfolio()
    {
        var m = Market.I;
        if (m.Live && !portfolioMarked) { portfolioMarked = true; m.Post("/tmtbs/yol/portfoy", null, (r, e) => { }); }
        double borc = J.D(m.credit, "kalan");
        Row(T("Nakit"), Fmt.Mls(m.cash));
        Row(T("Hisseler"), Fmt.Mls(m.stockValue));
        if (borc > 0) Row(T("Borç"), Fmt.Mls(borc));
        Row(T("Net değer"), $"<b><color=#FFD54A>{Fmt.Mls(m.net)}</color></b>");
        GUILayout.Label(T("Nakit {0} + hisseler {1} − borç {2}", Fmt.Short(m.cash), Fmt.Short(m.stockValue), Fmt.Short(borc)), small);
        if (m.portfolio.Count == 0) { GUILayout.Space(6); GUILayout.Label(T("Henüz hissen yok. Piyasa sayfasından ilk alımını yap."), label); return; }
        double kz = 0, cost = 0;
        foreach (var p in m.portfolio) { kz += J.D(p, "kz"); cost += J.D(p, "adet") * J.D(p, "ort_maliyet"); }
        Row(T("Hisse kâr/zarar"), $"<color={Fmt.ChangeColor(kz)}>{Fmt.N(kz)} ({Fmt.Pct(cost > 0 ? kz / cost * 100 : 0)})</color>");
        Title(T("Hisselerim"));
        foreach (var p in m.portfolio)
        {
            double k = J.D(p, "kz");
            long id = J.L(p, "id");
            GUILayout.BeginHorizontal();
            LogoIcon(id, J.S(p, "isim"), 32);
            GUILayout.Label($"<b>{J.S(p, "isim")}</b>\n<size=12>{T("{0} adet · ort. {1}", Fmt.N(J.D(p, "adet")), Fmt.N2(J.D(p, "ort_maliyet")))}</size>", small);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<b>{Fmt.Mls(J.D(p, "deger"))}</b>\n<color={Fmt.ChangeColor(k)}>{Fmt.N(k)} ({Fmt.Pct(J.D(p, "kz_yuzde"))})</color>", right);
            GUILayout.EndHorizontal();
            HoverTint();
            if (Clicked()) { var st = m.Find(id); if (st != null) GoToCounter(st); }
        }
    }

    // ================================================================ Görevler (ödül yolu)

    static readonly Dictionary<string, (string title, string desc)> RoadTasks = new Dictionary<string, (string, string)>
    {
        { "hisse_incele", ("Bir hisseyi incele", "Piyasadan bir hisse seç, sayfasını aç; grafiği, fiyatı ve şirket künyesini gör.") },
        { "favori", ("Favoriye ekle", "Beğendiğin bir hissenin yıldızına dokun; favorilerini Piyasa'da süzebilirsin.") },
        { "haber", ("Haberleri oku", "Piyasa haberlerini aç: halka arz, kâr payı, sermaye artırımı ve hisse bölünmeleri fiyatı etkiler.") },
        { "siralama", ("Sıralamaya bak", "Sıralamada kendi kademendeki oyuncuları gör; günlük, haftalık, aylık ve yıllık yarışırsın.") },
        { "profil", ("Profilini aç", "Profilinde net değerini, kademeni, performansını ve portföy dağılımını gör.") },
        { "portfoy", ("Portföyünü incele", "Portföyünde hisselerinin değerini, maliyetini ve kâr/zararını gör.") },
        { "al", ("İlk hissesini al", "Bir hisse için alış emri ver; fiyat karşı emirle eşleşince hisse portföyüne girer.") },
        { "sat", ("Bir hisse sat", "Portföyündeki bir hisse için satış emri ver; eşleşince nakdin artar.") },
        { "kademe", ("Yatırımcı kademesine ulaş", "Net değerini 10.000 MLS'ye çıkar: alım-satımla, kâr payıyla ve doğru hisse seçimiyle.") },
        { "duello", ("Trade Duello'yu keşfet", "Düello salonunu aç, kuralları oku; rakibinle aynı bütçeyle yarışabilirsin.") },
    };

    static string TaskTitle(string key) => RoadTasks.TryGetValue(key, out var t) ? T(t.title) : Pretty(key);

    static readonly Dictionary<string, string> TaskTab = new Dictionary<string, string>
    {
        { "hisse_incele", "Piyasa" }, { "favori", "Piyasa" }, { "haber", "Haberler" }, { "siralama", "Sıralama" },
        { "profil", "Profil" }, { "portfoy", "Portföy" }, { "al", "Piyasa" }, { "sat", "Portföy" }, { "kademe", "Ana" }, { "duello", "Düello" },
    };

    void TmRoadPlus()
    {
        if (!NeedLive()) return;
        if (!Data("yol", out var d)) return;
        var y = J.O(d, "yol");
        double total = J.D(y, "toplam_odul");
        GUILayout.Label(T("Görevleri tamamla, oyunu öğren ve toplam {0} MLS kazan.", Fmt.N(total)), small);
        double got = J.D(y, "kazanilan");
        GUILayout.Label($"<b>{T("{0}/{1} görev · {2} / {3} MLS", Fmt.N(J.D(y, "tamam_sayisi")), Fmt.N(J.D(y, "toplam_gorev")), Fmt.N(got), Fmt.N(total))}</b>", label);
        ProgressBar(total > 0 ? (float)(got / total) : 0, new Color(0.35f, 0.9f, 0.55f));
        string next = J.S(y, "sonraki");
        if (next != "") GUILayout.Label(T("Sıradaki: {0}", TaskTitle(next)), small);
        GUILayout.Space(6);
        foreach (var g in J.A(y, "gorevler"))
        {
            string key = J.S(g, "key");
            bool done = J.B(g, "tamam");
            GUILayout.BeginHorizontal();
            GUILayout.Label(done ? "<color=#5BE38A><size=20>●</size></color>" : "<color=#7d879c><size=20>○</size></color>", label, GUILayout.Width(28));
            GUILayout.BeginVertical();
            string yeni = J.B(g, "yeni") ? $"  <color=#FFD54A>{T("YENİ")}</color>" : "";
            GUILayout.Label($"<b>{TaskTitle(key)}</b>{yeni}", label);
            if (RoadTasks.TryGetValue(key, out var t)) GUILayout.Label(T(t.desc), small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(done ? $"<color=#5BE38A>{T("Tamamlandı")}</color>" : $"<color=#FFD54A>{T("+{0} MLS", Fmt.N(J.D(g, "odul")))}</color>", right, GUILayout.Width(110));
            GUILayout.EndHorizontal();
            HoverTint();
            if (!done && Clicked() && TaskTab.TryGetValue(key, out var tab)) SwitchTab(tab);
            GUILayout.Space(4);
        }
    }

    void SwitchTab(string name)
    {
        int i = System.Array.IndexOf(PhoneTabs, name);
        if (i < 0) return;
        phoneTab = i;
        placeTab = null;
        phoneMsg = null;
        scroll = Vector2.zero;
        LoadTab(i);
    }

    // ================================================================ Profil (kendi + başka oyuncular)

    long profileId;            // 0 = kendi profilin
    string profileBack;        // geri dönülecek sekme
    bool confirmDelete;

    void OpenProfile(long id, string backTab)
    {
        profileId = Market.I.userId > 0 && id == Market.I.userId ? 0 : id;
        profileBack = backTab;
        confirmDelete = false;
        SwitchTab("Profil");
    }

    string ProfilePath() => "/tmtbs/profil/" + (profileId > 0 ? profileId.ToString() : "ben");
    string ProfileKey() => "profil" + profileId;

    void TmProfile()
    {
        var m = Market.I;
        if (profileId > 0 && GUILayout.Button("‹ " + T("Geri"), GUILayout.Width(110), GUILayout.Height(32)))
        {
            profileId = 0;
            string back = profileBack;
            profileBack = null;
            if (!string.IsNullOrEmpty(back)) { SwitchTab(back); return; }
            Fetch(ProfileKey(), ProfilePath());
            return;
        }
        if (!m.Live)
        {
            GUILayout.Label($"<b>{m.userName}</b>  <color=#FFB74D>DEMO</color>", h2);
            GUILayout.Label(T("Profil, sıralama ve düello hesabınla giriş yapınca açılır."), small);
            LanguagePicker();
            GUILayout.Space(10);
            if (GUILayout.Button(T("Hesabınla giriş yap"), btn, GUILayout.Height(42))) { ClosePanel(); m.Logout(); }
            return;
        }
        if (!pdata.ContainsKey(ProfileKey())) Fetch(ProfileKey(), ProfilePath());
        if (!Data(ProfileKey(), out var d)) return;
        var p = J.O(d, "profil");
        bool me = J.B(p, "ben");
        int k = (int)J.D(p, "kademe");
        if (k < 1) k = TierOf(J.D(p, "net"));
        string ad = J.S(p, "ad");

        // Başlık: baş harfli avatar + ad + kademe
        GUILayout.BeginHorizontal();
        LogoIcon(-1, ad, 52);
        GUILayout.Space(6);
        GUILayout.BeginVertical();
        GUILayout.Label("<b>" + (me ? T("{0} (sen)", ad) : ad) + "</b>", h2);
        GUILayout.Label($"<color=#FFD54A>Sv. {k} · {TierName(k)}</color>", small);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"<size=11>{T("Net değer")}</size>\n<b><color=#FFD54A>{Fmt.Mls(J.D(p, "net"))}</color></b>", right);
        GUILayout.EndHorizontal();
        if (me) TierCard(J.D(p, "net"));

        // Performans
        Title(T("Performans"));
        var donem = J.O(p, "donemler");
        GUILayout.BeginHorizontal();
        foreach (var (key, name) in new[] { ("gun", "Günlük"), ("hafta", "Haftalık"), ("ay", "Aylık"), ("yil", "Yıllık") })
        {
            var x = J.O(donem, key);
            double kaz = J.D(x, "kazanc");
            int sira = (int)J.D(x, "sira");
            GUILayout.BeginVertical(box, GUILayout.ExpandWidth(true));
            GUILayout.Label($"<size=11><color=#9AA6BC>{T(name)}</color></size>", small);
            GUILayout.Label($"<b><color={Fmt.ChangeColor(kaz)}>{(kaz > 0 ? "+" : "")}{Fmt.Short(kaz)}</color></b>", label);
            GUILayout.Label($"<size=11>{(sira > 0 ? "#" + sira + " / " + Fmt.N(J.D(x, "n")) : "—")}</size>", small);
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(T("Sıra, oyuncunun kendi kademesindeki sıradır."), small);

        // Başarı kartları
        Title(T("Başarı kartları"));
        var cards = J.A(p, "kartlar");
        if (cards.Count == 0) GUILayout.Label(T("Henüz kart kazanılmadı."), small);
        else
        {
            var order = new Dictionary<string, int> { { "yil", 0 }, { "ay", 1 }, { "hafta", 2 }, { "gun", 3 } };
            var sorted = cards.OrderBy(c => order.TryGetValue(J.S(c, "donem"), out var o) ? o : 9).ThenBy(c => J.D(c, "sira")).ThenByDescending(c => J.D(c, "kademe")).ToList();
            foreach (var c in sorted)
            {
                int sira = (int)J.D(c, "sira");
                string medal = sira == 1 ? "#FFD54A" : sira == 2 ? "#D9DEE7" : "#E0A06A";
                string dn = PeriodName(J.S(c, "donem"));
                int adet = (int)J.D(c, "adet");
                Row($"<color={medal}><b>{T("{0}. sıra", sira)}</b></color> · {dn} · Sv. {Fmt.N(J.D(c, "kademe"))} {TierName((int)J.D(c, "kademe"))}", adet > 1 ? "×" + adet : "");
            }
        }
        GUILayout.Label(T("Gün, hafta, ay ve yıl kapanınca kademesinde ilk 3'e girenler kart kazanır."), small);

        // Portföy dağılımı
        Title(T("Portföy dağılımı"));
        var pf = J.A(p, "portfoy");
        if (pf.Count == 0) GUILayout.Label(T("Henüz hissesi yok."), small);
        foreach (var h in pf)
        {
            double y = J.D(h, "yuzde");
            GUILayout.BeginHorizontal();
            LogoIcon(J.L(h, "id"), J.S(h, "isim"), 28);
            GUILayout.BeginVertical();
            GUILayout.Label($"<b>{J.S(h, "isim")}</b>", small);
            ProgressBar((float)(y / 100.0), new Color(0.3f, 0.85f, 0.5f), 6);
            GUILayout.EndVertical();
            GUILayout.Label($"<b>%{y:0.0}</b>", right, GUILayout.Width(64));
            GUILayout.EndHorizontal();
            HoverTint();
            if (Clicked()) { var st = m.Find(J.L(h, "id")); if (st != null) GoToCounter(st); }
        }

        GUILayout.Space(6);
        if (J.S(p, "katilim") != "" && !J.B(p, "sinirli")) Row(T("Katılım"), J.S(p, "katilim"));
        Row(T("İşlem sayısı"), Fmt.N(J.D(p, "islem_sayisi")));
        Row(T("Hisse çeşidi"), Fmt.N(J.D(p, "hisse_cesidi")));

        if (!me)
        {
            GUILayout.Space(10);
            GUI.backgroundColor = new Color(0.85f, 0.45f, 0.25f);
            if (ActButton(T("Düelloya davet et"), 42))
            {
                worldBusy = true;
                m.Post("/tmtbs/duello", new Dictionary<string, object> { { "davet_id", profileId } }, (r, err) =>
                {
                    worldBusy = false;
                    phoneMsg = err ?? T("Düello açıldı. Rakip bekleniyor.");
                    phoneOk = err == null;
                    m.RefreshStatus();
                });
            }
            GUI.backgroundColor = BtnBase;
            return;
        }

        // Hesap
        Title(T("Hesap"));
        LanguagePicker();
        GUILayout.Space(8);
        if (GUILayout.Button(T("Çıkış yap"), btn, GUILayout.Height(40))) { ClosePanel(); m.Logout(); return; }
        GUILayout.Space(6);
        if (!confirmDelete)
        {
            if (GUILayout.Button($"<color=#FF8A8A>{T("Hesabımı sil")}</color>", GUILayout.Height(36))) confirmDelete = true;
        }
        else
        {
            GUILayout.Label($"<color=#FF8A8A>{T("Hesabın, paran, hisselerin ve sıralaman kalıcı olarak silinir. Bu işlem geri alınamaz.")}</color>", small);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
            if (ActButton(T("Evet, kalıcı olarak sil"), 40))
            {
                worldBusy = true;
                m.Post("/tmtbs/hesap/sil", null, (r, err) =>
                {
                    worldBusy = false;
                    if (err != null) { phoneMsg = err; phoneOk = false; return; }
                    confirmDelete = false;
                    ClosePanel();
                    m.Logout();
                    Show(T("Hesabın silindi."), 3f);
                });
            }
            GUI.backgroundColor = BtnBase;
            if (GUILayout.Button(T("Vazgeç"), GUILayout.Height(40))) confirmDelete = false;
            GUILayout.EndHorizontal();
        }
    }

    static string PeriodName(string k)
    {
        switch (k)
        {
            case "gun": return T("Günlük");
            case "hafta": return T("Haftalık");
            case "ay": return T("Aylık");
            case "yil": return T("Yıllık");
        }
        return k;
    }

    void LanguagePicker()
    {
        GUILayout.Label(T("Dil"), small);
        int per = 3;
        for (int i = 0; i < Loc.Langs.Length; i += per)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + per && j < Loc.Langs.Length; j++)
                if (TabBtn(Loc.Langs[j].name, Loc.Lang == Loc.Langs[j].code, 32)) Loc.Set(Loc.Langs[j].code);
            GUILayout.EndHorizontal();
        }
    }

    // ================================================================ Sıralama (oyuncuya dokun → profil)

    void TmRankPlus()
    {
        if (!NeedLive()) return;
        GUILayout.BeginHorizontal();
        foreach (var (k, n) in new[] { ("gun", "Günlük"), ("hafta", "Haftalık"), ("ay", "Aylık"), ("yil", "Yıllık") })
            if (TabBtn(T(n), k == rankPeriod, 32))
            {
                rankPeriod = k;
                pdata.Remove("sira");
                Fetch("sira", RankPath());
            }
        GUILayout.EndHorizontal();
        RankTierPicker();
        if (!Data("sira", out var d)) return;
        GUILayout.Label(T("{0} itibarıyla net değer artışı · {1} oyuncu", J.S(d, "baslangic"), Fmt.N(J.D(d, "oyuncu_sayisi"))), small);
        var ben = J.Get(d, "ben");
        if (ben is Dictionary<string, object> bd && bd.Count > 0)
            Row(T("Senin sıran"), $"<b>{Fmt.N(J.D(bd, "sira"))}.</b> · {Fmt.N(J.D(bd, "kazanc"))}");
        var list = J.A(d, "liste");
        if (list.Count == 0) GUILayout.Label(T("Henüz oyuncu yok."), small);
        foreach (var o in list)
        {
            bool me = J.B(o, "ben");
            int s = (int)J.D(o, "sira");
            string medal = s == 1 ? "#FFD54A" : s == 2 ? "#D9DEE7" : s == 3 ? "#E0A06A" : "#C9D1E0";
            string nm = me ? T("{0} (sen)", J.S(o, "ad")) : J.S(o, "ad");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={medal}><b>{s}.</b></color>", label, GUILayout.Width(40));
            GUILayout.Label(me ? $"<color=#FFD54A><b>{nm}</b></color>" : $"<b>{nm}</b>", label);
            GUILayout.FlexibleSpace();
            double kz = J.D(o, "kazanc");
            GUILayout.Label($"<color={Fmt.ChangeColor(kz)}><b>{(kz > 0 ? "+" : "")}{Fmt.N(kz)}</b></color>\n<size=11>{T("Net {0}", Fmt.Short(J.D(o, "net")))}</size>", right);
            GUILayout.EndHorizontal();
            HoverTint();
            if (Clicked()) OpenProfile(J.L(o, "id"), "Sıralama");
        }
    }

    // ================================================================ Düello ayrıntısı

    long duelSel;

    void DuelDetail()
    {
        if (duelSel <= 0) return;
        string key = "duello" + duelSel;
        if (!pdata.ContainsKey(key)) Fetch(key, "/tmtbs/duello/" + duelSel);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>{T("Düello")} #{duelSel}</b>", h2);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("×", GUILayout.Width(40), GUILayout.Height(30))) { duelSel = 0; return; }
        GUILayout.EndHorizontal();
        if (!Data(key, out var d)) return;
        var x = J.O(d, "duello");
        Row(T("Durum"), DuelState(J.S(x, "durum")));
        if (J.S(x, "bitis") != "") Row(T("Bitiş"), ShortDate(J.S(x, "bitis")));
        Row(T("Ödül"), Fmt.Mls(J.D(x, "odul")));
        var players = J.A(x, "oyuncular");
        if (players.Count > 0)
        {
            Title(T("Oyuncular"));
            long win = J.L(x, "kazanan_id");
            foreach (var p in players.OrderByDescending(p => J.D(p, "net")))
            {
                bool me = J.B(p, "ben");
                string nm = me ? T("{0} (sen)", J.S(p, "ad")) : J.S(p, "ad");
                string crown = win > 0 && J.L(p, "id") == win ? "  <color=#FFD54A>" + T("Kazanan") + "</color>" : "";
                Row($"<b>{nm}</b>{crown}", $"{Fmt.Mls(J.D(p, "net"))}");
            }
        }
        if (J.Get(x, "kazandim") != null && J.S(x, "durum") == "bitti")
            GUILayout.Label(J.B(x, "kazandim") ? $"<color=#5BE38A><b>{T("Düelloyu kazandın!")}</b></color>" : T("Bu düelloyu rakibin kazandı."), label);
        var bp = J.A(x, "benim_portfoy");
        if (J.Get(x, "benim_nakit") != null || bp.Count > 0)
        {
            Title(T("Düello hesabın"));
            Row(T("Nakit"), Fmt.Mls(J.D(x, "benim_nakit")));
            foreach (var h in bp) Row(J.S(h, "isim") + "  " + T("{0} adet", Fmt.N(J.D(h, "adet"))), Fmt.Mls(J.D(h, "deger")));
            GUILayout.Label(T("Düello hesabıyla işlem için bankodaki panelde hesabı seç."), small);
        }
        GUILayout.Space(8);
    }

    static string DuelState(string s)
    {
        switch (s)
        {
            case "bekliyor": return "<color=#FFB74D>" + T("Rakip bekleniyor") + "</color>";
            case "aktif": case "devam": case "basladi": return "<color=#5BE38A>" + T("Sürüyor") + "</color>";
            case "bitti": return T("Bitti");
            case "iptal": return T("İptal edildi");
        }
        return Pretty(s);
    }

    // ================================================================ Şirket künyesi (uygulamadaki gibi etiketli)

    void DrawInfoPlus(Stock s)
    {
        if (!s.detailLoaded) { GUILayout.Label(T("yükleniyor…"), small); return; }
        var d = s.detail;
        var mk = J.O(d, "marka");
        if (!Market.I.Live || mk.Count == 0)
        {
            Row(T("Sermaye"), Fmt.Mls(J.D(d, "sermaye")));
            Row(T("Defter değeri"), Fmt.N2(J.D(d, "defter_degeri")));
            if (!Market.I.Live) GUILayout.Label(T("Şirket künyesi hesabınla giriş yapınca görünür."), small);
            HisseMantigi();
            return;
        }
        Title(T("Şirket künyesi"));
        if (J.S(mk, "kurucu") != "") Row(T("Kurucu"), J.S(mk, "kurucu"));
        if (J.S(mk, "kurulus") != "") Row(T("Kuruluş"), J.S(mk, "kurulus").Split(' ')[0]);
        if (J.S(mk, "halka_arz") != "") Row(T("Halka arz"), J.S(mk, "halka_arz").Split(' ')[0]);
        Row(T("Sermaye"), Fmt.Mls(J.D(mk, "sermaye")));
        Row(T("Kasa"), Fmt.Mls(J.D(mk, "kasa")));
        if (J.D(mk, "stok_degeri") > 0) Row(T("Stok değeri"), Fmt.Mls(J.D(mk, "stok_degeri")));
        var fab = J.O(mk, "fabrika");
        int sv = (int)J.D(fab, "seviye");
        if (sv > 0)
        {
            Row(T("Fabrika seviye {0}", sv), T("Fabrika {0}", Fmt.Mls(J.D(mk, "fabrika_degeri"))));
            string fx = J.B(fab, "yapimda")
                ? T("Birim maliyet %{0} · seviye {1} yapımda ({2} ay)", Fmt.N(J.D(fab, "oran")), Fmt.N(J.D(fab, "hedef")), Fmt.N(J.D(fab, "kalan_ay")))
                : T("Birim maliyet satış fiyatının %{0}", Fmt.N(J.D(fab, "oran")));
            GUILayout.Label(fx, small);
        }
        else Row(T("Fabrika"), T("Fabrika yok"));
        Row(T("Ürün çeşitliliği"), T("{0} ürün · {1} kategori", Fmt.N(J.D(mk, "urun_sayisi")), J.A(mk, "kategoriler").Count));
        Row(T("Sipariş"), Fmt.N(J.D(mk, "siparis")));
        Row(T("Satılan ürün"), Fmt.N(J.D(mk, "satilan")));
        Title(T("Hisse"));
        Row(T("Toplam hisse"), Fmt.N(J.D(mk, "hisse_toplam")));
        Row(T("Halka açıklık"), "%" + Fmt.N(J.D(mk, "halka_aciklik")));
        Row(T("Hissedar"), Fmt.N(J.D(mk, "hissedar")));
        Row(T("Arz fiyatı"), Fmt.N2(J.D(mk, "arz_fiyat")));
        Row(T("Defter değeri"), Fmt.N2(J.D(mk, "defter_degeri")));
        Row(T("PD/DD"), Fmt.N2(J.D(mk, "pd_dd")));
        Row(T("Kâr payı"), T("{0} kez", Fmt.N(J.D(mk, "temettu_sayisi"))) + " · " + Fmt.Mls(J.D(mk, "temettu_toplam")));
        Row(T("Sermaye artırımı"), T("{0} kez", Fmt.N(J.D(mk, "artirim_sayisi"))));

        var urun = J.A(mk, "urunler");
        if (urun.Count > 0)
        {
            Title(T("Ürünler"));
            for (int i = 0; i < urun.Count && i < 12; i++)
                Row("• " + J.S(urun[i], "isim") + $" <size=11><color=#9AA6BC>{J.S(urun[i], "kategori")}</color></size>", Fmt.N2(J.D(urun[i], "fiyat")) + " MLS");
        }
        var tem = J.A(d, "temettuler");
        if (tem.Count > 0)
        {
            Title(T("Kâr payı"));
            foreach (var t in tem) Row(ShortDate(J.S(t, "tarih")), Fmt.Mls(J.D(t, "tutar") > 0 ? J.D(t, "tutar") : J.D(t, "hisse_basi")));
        }
        var hak = J.Get(d, "bekleyen_hak");
        if (hak is Dictionary<string, object> hd && hd.Count > 0)
        {
            Title(T("Sermaye artırımı"));
            Dump(hd);
            GUILayout.Label(T("Hakkını cep menüsündeki \"Haklar\" bölümünden kullanabilirsin."), small);
        }
        var hab = J.A(mk, "haberler");
        if (hab.Count > 0)
        {
            Title(T("Şirket haberleri"));
            for (int i = 0; i < hab.Count && i < 8; i++) NewsCard(hab[i]);
        }
        HisseMantigi();
    }

    void HisseMantigi()
    {
        Title(T("Hisse mantığı: kısaca"));
        foreach (var t in new[]
        {
            "Her hisse, Müteşebbis oyunundaki bir oyuncunun şirketidir.",
            "Şirket ürün sattıkça kâr eder; kâr, zarar ve büyüme şirketin defter değerini oluşturur.",
            "Defter değeri, şirketin toplam değerinin hisse sayısına bölünmüş hâlidir; fiyatın referansıdır.",
            "Fiyatı oyuncular belirler: alanlar çoğalırsa fiyat yükselir, satanlar çoğalırsa düşer.",
            "Fiyat defter değerinin altındaysa ucuz, üstündeyse pahalı olabilir; haberleri takip et.",
        })
            GUILayout.Label("• " + T(t), small);
    }
}
