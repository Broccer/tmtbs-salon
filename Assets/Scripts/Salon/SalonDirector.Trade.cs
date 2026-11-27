using System.Collections.Generic;
using UnityEngine;

/// <summary>Bankoda işlem paneli: al-sat (piyasa/limit), emirlerim + iptal, defter, grafik, künye, favori, düello hesabı.</summary>
public partial class SalonDirector
{
    Stock trade;
    int tradeTab;
    string qtyText = "1", priceText = "";
    string tradeMsg;
    bool tradeOk, tradeBusy;
    long duel;                                   // 0 = ana hesap
    readonly List<long> activeDuels = new List<long>();
    Dictionary<string, object> chart;
    string chartRange = "1a";
    bool chartBusy;

    static readonly string[] TradeTabs = { "Al-Sat", "Defter", "Grafik", "Künye" };

    void OpenTrade(Stock s)
    {
        OpenPanelCommon(Panel.Trade);
        trade = s;
        tradeTab = 0;
        qtyText = "1";
        priceText = "";
        tradeMsg = null;
        chart = null;
        duel = 0;
        Market.I.LoadDetail(s, () => { if (trade == s) priceText = PxText(DefaultPx(s)); });
        activeDuels.Clear();
        if (Market.I.Tm && Market.I.Live) LoadDuelAccounts();
    }

    static double ParsePrice(string t)
    {
        if (string.IsNullOrEmpty(t)) return 0;
        t = t.Trim().Replace(" ", "");
        // Ondalık için virgül de nokta da kabul edilir (binlik ayraç yok)
        t = t.Replace(",", ".");
        return double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;
    }

    static string PxText(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    long ParseQty() => long.TryParse((qtyText ?? "").Trim().Replace(".", ""), out long q) && q > 0 ? q : 1;

    void DrawTrade()
    {
        var m = Market.I;
        var s = trade;
        if (s == null) { ClosePanel(); return; }
        var r = PanelRect(480);
        float iw = BeginPanel(r);

        // Başlık
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>{s.name}</b>", h2);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(s.favorite ? "<color=#FFD54A>*</color>" : "+", btn, GUILayout.Width(44), GUILayout.Height(36)))
            m.ToggleFavorite(s, err => { if (err != null) tradeMsg = err; });
        if (GUILayout.Button("×", btn, GUILayout.Width(44), GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        GUILayout.EndHorizontal();
        GUILayout.Label($"<size=28><b>{Fmt.N2(s.price)}</b></size> MLS   <color={Fmt.ChangeColor(s.change)}><b>{Fmt.Pct(s.change)}</b></color>", label);
        long sellable = m.mode == Market.Mode.Demo ? s.myQty : s.myFree;
        GUILayout.Label(s.myQty > 0
            ? $"Portföyünde <b>{Fmt.N(s.myQty)}</b> adet · satılabilir <b>{Fmt.N(sellable)}</b> · ort. maliyet {Fmt.N2(s.myCost)}"
            : "Portföyünde bu hisseden yok.", small);

        if (activeDuels.Count > 0)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hesap:", small, GUILayout.Width(52));
            if (GUILayout.Toggle(duel == 0, " Ana", GUILayout.Height(28)) && duel != 0) { duel = 0; m.LoadDetail(s, null); }
            foreach (var d in activeDuels)
                if (GUILayout.Toggle(duel == d, " Düello #" + d, GUILayout.Height(28)) && duel != d) { duel = d; m.LoadDetail(s, null, d); }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < TradeTabs.Length; i++)
            if (TabBtn(TradeTabs[i], i == tradeTab, 34))
            {
                tradeTab = i;
                if (i == 2 && chart == null) LoadChart();
            }
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        if (tradeTab == 0) DrawOrderForm(s, sellable, iw);
        else if (tradeTab == 1) DrawBook(s, iw);
        else if (tradeTab == 2) DrawChart(iw);
        else DrawInfo(s);
        if (Market.I.Tm) TradeRulesNote(s);

        GUILayout.Space(10);
        if (GUILayout.Button("Kapat (Esc)", GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        EndPanel();
    }

    /// <summary>Fiyatı kademeye oturtur ve bugünkü bandın içine çeker (Trade of MTBS kuralları).</summary>
    static double ClampPx(Stock s, double p)
    {
        p = Tick.Snap(p);
        if (s.minPrice > 0 && p < s.minPrice) p = s.minPrice;
        if (s.maxPrice > 0 && p > s.maxPrice) p = s.maxPrice;
        return p;
    }

    /// <summary>Emri hemen eşleşecek en uygun başlangıç fiyatı: en iyi satış, yoksa son fiyat.</summary>
    static double DefaultPx(Stock s) => ClampPx(s, s.bestAsk > 0 ? s.bestAsk : s.price);

    void DrawOrderForm(Stock s, long sellable, float iw)
    {
        var m = Market.I;
        long qty = ParseQty();
        if (ParsePrice(priceText) <= 0 && s.detailLoaded) priceText = PxText(DefaultPx(s));
        double px = ClampPx(s, ParsePrice(priceText));

        // ---- Fiyat: kademe kademe ya da elle yazılır; bandın dışına çıkamaz
        GUILayout.Label($"<b>Fiyat</b>  <size=12>(kademe {Fmt.N2(Tick.Step(px))} MLS" +
            (s.minPrice > 0 && s.maxPrice > 0 ? $" · bugün izin verilen {Fmt.N2(s.minPrice)} – {Fmt.N2(s.maxPrice)}" : "") + ")</size>", label);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<<", GUILayout.Width(46), GUILayout.Height(40))) { double p = px; for (int i = 0; i < 5; i++) p = Tick.Down(p); priceText = PxText(ClampPx(s, p)); }
        if (GUILayout.Button("<", GUILayout.Width(40), GUILayout.Height(40))) priceText = PxText(ClampPx(s, Tick.Down(px)));
        priceText = HtmlInput.Field("fiyat", priceText, "Fiyat (MLS)", field, false, true, 12, GUILayout.Height(40), GUILayout.MinWidth(80));
        if (GUILayout.Button(">", GUILayout.Width(40), GUILayout.Height(40))) priceText = PxText(ClampPx(s, Tick.Up(px)));
        if (GUILayout.Button(">>", GUILayout.Width(46), GUILayout.Height(40))) { double p = px; for (int i = 0; i < 5; i++) p = Tick.Up(p); priceText = PxText(ClampPx(s, p)); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (s.bestAsk > 0 && GUILayout.Button($"<size=12>En iyi satış</size>\n<b>{Fmt.N2(s.bestAsk)}</b>", GUILayout.Height(40))) priceText = PxText(ClampPx(s, s.bestAsk));
        if (GUILayout.Button($"<size=12>Son fiyat</size>\n<b>{Fmt.N2(s.price)}</b>", GUILayout.Height(40))) priceText = PxText(ClampPx(s, s.price));
        if (s.bestBid > 0 && GUILayout.Button($"<size=12>En iyi alış</size>\n<b>{Fmt.N2(s.bestBid)}</b>", GUILayout.Height(40))) priceText = PxText(ClampPx(s, s.bestBid));
        GUILayout.EndHorizontal();
        double typed = ParsePrice(priceText);
        px = ClampPx(s, typed);
        bool pxFixed = typed > 0 && System.Math.Abs(typed - px) > 0.001;
        if (pxFixed)
            GUILayout.Label($"<color=#FFB74A>Yazdığın fiyat geçerli kademeye / bugünkü banda göre <b>{Fmt.N2(px)}</b> olarak verilecek.</color>", small);

        // ---- Adet
        GUILayout.Space(4);
        GUILayout.Label("<b>Adet</b>", label);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10", GUILayout.Width(48), GUILayout.Height(38))) qtyText = System.Math.Max(1, qty - 10).ToString();
        if (GUILayout.Button("-1", GUILayout.Width(42), GUILayout.Height(38))) qtyText = System.Math.Max(1, qty - 1).ToString();
        qtyText = HtmlInput.Field("adet", qtyText, "Adet", field, false, true, 9, GUILayout.Height(38), GUILayout.MinWidth(70));
        if (GUILayout.Button("+1", GUILayout.Width(42), GUILayout.Height(38))) qtyText = (qty + 1).ToString();
        if (GUILayout.Button("+10", GUILayout.Width(48), GUILayout.Height(38))) qtyText = (qty + 10).ToString();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Alınabilecek en çok", GUILayout.Height(32)) && px > 0)
            qtyText = System.Math.Max(1, (long)(m.cash / px)).ToString();
        if (GUILayout.Button("Elimdekinin tamamı", GUILayout.Height(32)) && sellable > 0)
            qtyText = sellable.ToString();
        GUILayout.EndHorizontal();
        qty = ParseQty();
        GUILayout.Label($"Tutar ≈ <b>{Fmt.Mls(qty * px)}</b>   ·   Nakit {Fmt.Mls(m.cash)}", small);

        GUI.enabled = !tradeBusy && s.detailLoaded && px > 0;
        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
        if (GUILayout.Button($"AL · {Fmt.N2(px)}", btn, GUILayout.Height(52))) SendOrder(true, qty, px);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button($"SAT · {Fmt.N2(px)}", btn, GUILayout.Height(52))) SendOrder(false, qty, px);
        GUI.backgroundColor = SalonDirector.BtnBase;
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        if (tradeBusy) GUILayout.Label("Emir gönderiliyor…", small);
        else if (!string.IsNullOrEmpty(tradeMsg)) Msg(tradeMsg, tradeOk);
        else GUILayout.Label("Alışta fiyatın en iyi satışa eşit ya da üstündeyse, satışta en iyi alışa eşit ya da altındaysa emir hemen eşleşir; değilse fiyatın gelene kadar defterde bekler.", small);

        // Bu hissedeki bekleyen emirlerim
        var mine = J.A(J.O(s.detail, "benim"), "emirler");
        if (mine.Count > 0)
        {
            Title("Bekleyen emirlerim");
            foreach (var o in mine)
            {
                long id = J.L(o, "id");
                bool buy = J.S(o, "yon") == "al";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color={(buy ? "#5BE38A" : "#FF7A7A")}>{(buy ? "AL" : "SAT")}</color> {Fmt.N(J.D(o, "kalan") > 0 ? J.D(o, "kalan") : J.D(o, "adet"))} × {Fmt.N2(J.D(o, "fiyat"))}", label);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("İptal", GUILayout.Width(70), GUILayout.Height(30)))
                    m.CancelOrder(id, duel, (msg, ok) => { tradeMsg = msg; tradeOk = ok; m.LoadDetail(s, null, duel); });
                GUILayout.EndHorizontal();
            }
        }
    }

    void SendOrder(bool buy, long qty, double limitPx)
    {
        tradeBusy = true;
        tradeMsg = null;
        var s = trade;
        System.Action<string, bool> done = (msg, ok) =>
        {
            tradeBusy = false;
            tradeMsg = msg;
            tradeOk = ok;
            if (ok && Bell.I) Bell.I.Tick();
            if (ok && s != null) Market.I.LoadDetail(s, null, duel);
            if (ok) Market.I.RefreshStatus();
        };
        if (limitPx > 0 || duel > 0)
        {
            double px = limitPx > 0 ? limitPx : (buy ? (s.bestAsk > 0 ? s.bestAsk : s.price) : (s.bestBid > 0 ? s.bestBid : s.price));
            Market.I.PlaceLimit(s, buy, px, qty, duel, done);
        }
        else Market.I.PlaceOrder(s, buy, qty, done);
    }

    void DrawBook(Stock s, float iw)
    {
        if (!s.detailLoaded) { GUILayout.Label("yükleniyor…", small); return; }
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(iw / 2 - 4));
        GUILayout.Label("<color=#5BE38A><b>Alış</b></color>  fiyat × adet", small);
        BookRows(s.bids);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(iw / 2 - 4));
        GUILayout.Label("<color=#FF7A7A><b>Satış</b></color>  fiyat × adet", small);
        BookRows(s.asks);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        var trades = J.A(s.detail, "islemler");
        if (trades.Count > 0)
        {
            Title("Son işlemler");
            for (int i = 0; i < trades.Count && i < 12; i++)
                Row(ShortDate(J.S(trades[i], "tarih")), $"{Fmt.N2(J.D(trades[i], "fiyat"))} × {Fmt.N(J.D(trades[i], "adet"))}");
        }
    }

    void BookRows(List<KeyValuePair<double, long>> rows)
    {
        if (rows.Count == 0) { GUILayout.Label("—", small); return; }
        for (int i = 0; i < rows.Count && i < 8; i++)
        {
            var kv = rows[i];
            if (GUILayout.Button($"{Fmt.N2(kv.Key)} × {Fmt.N(kv.Value)}", label))
            {
                priceText = PxText(kv.Key);
                tradeTab = 0;
            }
        }
    }

    static string ShortDate(string t)
    {
        if (string.IsNullOrEmpty(t)) return "";
        // 2026-11-23 14:05:00 → 23.11 14:05
        if (t.Length >= 16 && t[4] == '-') return t.Substring(8, 2) + "." + t.Substring(5, 2) + " " + t.Substring(11, 5);
        return t;
    }

    void LoadChart()
    {
        if (trade == null || chartBusy) return;
        chartBusy = true;
        var s = trade;
        // Sunucu { ok, grafik: { noktalar, acilis, … } } döner; eskiden üst düzey okunuyordu ve grafik hep boş kalıyordu.
        System.Action<Dictionary<string, object>, string> done = (r, err) =>
        {
            chartBusy = false;
            if (trade != s) return;
            var g = r != null ? J.O(r, "grafik") : null;
            chart = g != null && g.Count > 0 ? g : new Dictionary<string, object> { { "_hata", err ?? "Grafik alınamadı." } };
            if (chartTex != null) { Destroy(chartTex); chartTex = null; }
        };
        if (Market.I.Live)
            Market.I.Get((Market.I.Tm ? "/tmtbs/hisse/" + s.id + "/grafik?aralik=" : "/dunya/grafik/" + s.id + "?aralik=") + chartRange, done);
        else // demo/misafir: girişsiz herkese açık grafik
            Api.I.Get("/tmtbs/vitrin/grafik/" + s.id + "?aralik=" + chartRange, r => done(r, null), (m, c) => done(null, m));
    }

    Texture2D chartTex;
    float chartHi, chartLo;

    /// <summary>Fiyat çizgisi + altında geçişli dolgu + yatay kılavuz çizgileri; bir kez doku olarak çizilir.</summary>
    Texture2D BuildChartTex(List<object> pts, int w, int h, out float lo, out float hi)
    {
        double l = double.MaxValue, u = double.MinValue;
        foreach (var p in pts) { double f = J.D(p, "f"); if (f < l) l = f; if (f > u) u = f; }
        if (u - l < 1e-6) { u += 1; l = System.Math.Max(0, l - 1); }
        double pad = (u - l) * 0.08;
        u += pad; l = System.Math.Max(0, l - pad);
        lo = (float)l; hi = (float)u;
        bool up = J.D(pts[pts.Count - 1], "f") >= J.D(pts[0], "f");
        Color line = up ? new Color(0.25f, 0.92f, 0.5f) : new Color(1f, 0.38f, 0.38f);
        var px = new Color[w * h];
        var bg = new Color(0, 0, 0, 0);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        // kılavuz çizgileri
        for (int gl = 1; gl < 4; gl++)
        {
            int y = h * gl / 4;
            for (int x = 0; x < w; x += 2) px[y * w + x] = new Color(1, 1, 1, 0.08f);
        }
        // her sütun için çizginin yüksekliği (doğrusal ara değer)
        var ys = new float[w];
        int n = pts.Count;
        for (int x = 0; x < w; x++)
        {
            float t = x / (float)(w - 1) * (n - 1);
            int i0 = Mathf.Clamp(Mathf.FloorToInt(t), 0, n - 1), i1 = Mathf.Min(n - 1, i0 + 1);
            float v = Mathf.Lerp((float)J.D(pts[i0], "f"), (float)J.D(pts[i1], "f"), t - i0);
            ys[x] = (v - lo) / (hi - lo) * (h - 4) + 2;
        }
        for (int x = 0; x < w; x++)
        {
            float yv = ys[x];
            float yPrev = x > 0 ? ys[x - 1] : yv;
            float a = Mathf.Min(yv, yPrev) - 1.2f, b = Mathf.Max(yv, yPrev) + 1.2f;
            for (int y = 0; y < h; y++)
            {
                Color c = px[y * w + x];
                if (y < yv) // dolgu: çizgiye yaklaştıkça koyulaşan geçiş
                {
                    float k = y / Mathf.Max(1f, yv);
                    c = Color.Lerp(c, new Color(line.r, line.g, line.b, 1f), 0.04f + 0.30f * k * k);
                    c.a = Mathf.Max(c.a, 0.04f + 0.30f * k * k);
                }
                if (y >= a && y <= b) // kenarı yumuşak çizgi
                {
                    float d = Mathf.Min(y - a, b - y);
                    float al = Mathf.Clamp01(d / 1.2f);
                    c = Color.Lerp(c, line, al);
                    c.a = Mathf.Max(c.a, al);
                }
                px[y * w + x] = c;
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    void DrawChart(float iw)
    {
        GUILayout.BeginHorizontal();
        foreach (var (k, n) in new[] { ("1g", "1 gün"), ("7g", "7 gün"), ("1a", "1 ay"), ("1y", "1 yıl") })
            if (TabBtn(n, k == chartRange, 32)) { chartRange = k; chart = null; LoadChart(); }
        GUILayout.EndHorizontal();
        if (chart == null || chartBusy) { GUILayout.Label("yükleniyor…", small); return; }
        if (chart.ContainsKey("_hata")) { Msg(J.S(chart, "_hata"), false); return; }
        var pts = J.A(chart, "noktalar");
        var rect = GUILayoutUtility.GetRect(iw, 190);
        if (Event.current.type == EventType.Repaint && pts.Count > 1)
        {
            if (chartTex == null) chartTex = BuildChartTex(pts, 640, 200, out chartLo, out chartHi);
            Color old = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.04f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.DrawTexture(rect, chartTex, ScaleMode.StretchToFill, true);
            GUI.Label(new Rect(rect.x + 6, rect.y + 2, 200, 20), Fmt.N2(chartHi), small);
            GUI.Label(new Rect(rect.x + 6, rect.yMax - 20, 200, 20), Fmt.N2(chartLo), small);
            double last = J.D(pts[pts.Count - 1], "f");
            GUI.Label(new Rect(rect.xMax - 130, rect.y + 2, 124, 20), "<b>" + Fmt.N2(last) + "</b>", right);
            if (pts.Count > 2)
            {
                GUI.Label(new Rect(rect.x + 6, rect.yMax + 1, 200, 18), ShortDate(J.S(pts[0], "t")), small);
                GUI.Label(new Rect(rect.xMax - 206, rect.yMax + 1, 200, 18), ShortDate(J.S(pts[pts.Count - 1], "t")), right);
            }
        }
        GUILayout.Space(18);
        if (pts.Count <= 1) GUILayout.Label("Bu aralıkta işlem yok.", small);
        Row("Açılış", Fmt.N2(J.D(chart, "acilis")));
        Row("Kapanış", Fmt.N2(J.D(chart, "kapanis")));
        Row("En yüksek", Fmt.N2(J.D(chart, "en_yuksek")));
        Row("En düşük", Fmt.N2(J.D(chart, "en_dusuk")));
        Row("Değişim", $"<color={Fmt.ChangeColor(J.D(chart, "degisim"))}>{Fmt.Pct(J.D(chart, "degisim"))}</color>");
        Row("Hacim", Fmt.N(J.D(chart, "hacim")));
    }

    void DrawInfo(Stock s)
    {
        if (!s.detailLoaded) { GUILayout.Label("yükleniyor…", small); return; }
        if (!Market.I.Live) { GUILayout.Label("Şirket künyesi hesabınla giriş yapınca görünür.", small); return; }
        var d = s.detail;
        Row("Sermaye", Fmt.Mls(J.D(d, "sermaye")));
        Row("Defter değeri", Fmt.N2(J.D(d, "defter_degeri")));
        var marka = J.O(d, "marka");
        if (marka.Count > 0)
        {
            Title("Künye");
            Dump(marka, "isim", "haberler", "urunler", "fabrika");
            var fab = J.O(marka, "fabrika");
            if (fab.Count > 0) { Title("Fabrika"); Dump(fab); }
            var urun = J.A(marka, "urunler");
            if (urun.Count > 0)
            {
                Title("Ürünler");
                foreach (var u in urun)
                    GUILayout.Label("• " + (u is Dictionary<string, object> ud ? (J.S(ud, "isim") != "" ? J.S(ud, "isim") : J.S(ud, "ad")) : Val(u)), small);
            }
            var hab = J.A(marka, "haberler");
            if (hab.Count > 0)
            {
                Title("Haberler");
                for (int i = 0; i < hab.Count && i < 8; i++)
                    GUILayout.Label("• " + NewsLine(hab[i]), small);
            }
        }
        var tem = J.A(d, "temettuler");
        if (tem.Count > 0)
        {
            Title("Temettüler");
            foreach (var t in tem) Row(ShortDate(J.S(t, "tarih")), Fmt.Mls(J.D(t, "tutar") > 0 ? J.D(t, "tutar") : J.D(t, "hisse_basi")));
        }
        var hak = J.Get(d, "bekleyen_hak");
        if (hak is Dictionary<string, object> hd && hd.Count > 0)
        {
            Title("Bekleyen sermaye artırımı hakkın");
            Dump(hd);
            GUILayout.Label("Hakkını cep menüsündeki \"Haklar\" bölümünden kullanabilirsin.", small);
        }
    }
}
