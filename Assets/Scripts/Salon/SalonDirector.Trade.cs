using System.Collections.Generic;
using UnityEngine;

/// <summary>Bankoda işlem paneli: al-sat (piyasa/limit), emirlerim + iptal, defter, grafik, künye, favori, düello hesabı.</summary>
public partial class SalonDirector
{
    Stock trade;
    int tradeTab;
    string qtyText = "1", priceText = "";
    bool limitMode;
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
        limitMode = false;
        priceText = PxText(s.price);
        tradeMsg = null;
        chart = null;
        duel = 0;
        Market.I.LoadDetail(s, () => { if (trade == s && string.IsNullOrEmpty(priceText)) priceText = PxText(s.price); });
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
            if (GUILayout.Button(TradeTabs[i], i == tradeTab ? tabOn : tab, GUILayout.Height(34)))
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

    void DrawOrderForm(Stock s, long sellable, float iw)
    {
        var m = Market.I;
        long qty = ParseQty();
        double buyPx = s.bestAsk > 0 ? s.bestAsk : s.price;
        double sellPx = s.bestBid > 0 ? s.bestBid : s.price;

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(!limitMode, " Piyasa emri", GUILayout.Height(30))) limitMode = false;
        if (GUILayout.Toggle(limitMode, " Limit emir", GUILayout.Height(30)) && !limitMode) { limitMode = true; priceText = PxText(Tick.Snap(s.price)); }
        GUILayout.EndHorizontal();

        GUILayout.Label("Adet", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10", GUILayout.Width(48), GUILayout.Height(38))) qtyText = System.Math.Max(1, qty - 10).ToString();
        if (GUILayout.Button("-1", GUILayout.Width(42), GUILayout.Height(38))) qtyText = System.Math.Max(1, qty - 1).ToString();
        qtyText = HtmlInput.Field("adet", qtyText, "Adet", field, false, true, 9, GUILayout.Height(38), GUILayout.MinWidth(70));
        if (GUILayout.Button("+1", GUILayout.Width(42), GUILayout.Height(38))) qtyText = (qty + 1).ToString();
        if (GUILayout.Button("+10", GUILayout.Width(48), GUILayout.Height(38))) qtyText = (qty + 10).ToString();
        GUILayout.EndHorizontal();

        double limitPx = Tick.Snap(ParsePrice(priceText));
        if (limitMode)
        {
            GUILayout.Label($"Fiyat (kademe {Fmt.N2(Tick.Step(limitPx > 0 ? limitPx : s.price))})" +
                (s.minPrice > 0 && s.maxPrice > 0 ? $" · bugünkü bant {Fmt.N2(s.minPrice)} – {Fmt.N2(s.maxPrice)}" : ""), small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹", GUILayout.Width(48), GUILayout.Height(38))) priceText = PxText(Tick.Down(limitPx > 0 ? limitPx : s.price));
            priceText = HtmlInput.Field("fiyat", priceText, "Limit fiyat (MLS)", field, false, true, 12, GUILayout.Height(38), GUILayout.MinWidth(90));
            if (GUILayout.Button("›", GUILayout.Width(48), GUILayout.Height(38))) priceText = PxText(Tick.Up(limitPx > 0 ? limitPx : s.price));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (s.bestBid > 0 && GUILayout.Button("En iyi alış " + Fmt.N2(s.bestBid), GUILayout.Height(30))) priceText = PxText(s.bestBid);
            if (s.bestAsk > 0 && GUILayout.Button("En iyi satış " + Fmt.N2(s.bestAsk), GUILayout.Height(30))) priceText = PxText(s.bestAsk);
            GUILayout.EndHorizontal();
            limitPx = Tick.Snap(ParsePrice(priceText));
            buyPx = sellPx = limitPx;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Alınabilecek en çok", GUILayout.Height(32)) && buyPx > 0)
            qtyText = System.Math.Max(1, (long)(m.cash / buyPx)).ToString();
        if (GUILayout.Button("Elimdekinin tamamı", GUILayout.Height(32)) && sellable > 0)
            qtyText = sellable.ToString();
        GUILayout.EndHorizontal();
        qty = ParseQty();
        GUILayout.Label($"Alış ≈ <b>{Fmt.Mls(qty * buyPx)}</b>   ·   Satış ≈ <b>{Fmt.Mls(qty * sellPx)}</b>", small);


        GUI.enabled = !tradeBusy && s.detailLoaded && (!limitMode || limitPx > 0);
        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
        if (GUILayout.Button($"AL · {Fmt.N2(buyPx)}", btn, GUILayout.Height(52))) SendOrder(true, qty, limitMode ? limitPx : 0);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button($"SAT · {Fmt.N2(sellPx)}", btn, GUILayout.Height(52))) SendOrder(false, qty, limitMode ? limitPx : 0);
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        if (tradeBusy) GUILayout.Label("Emir gönderiliyor…", small);
        else if (!string.IsNullOrEmpty(tradeMsg)) Msg(tradeMsg, tradeOk);
        else GUILayout.Label(limitMode ? "Limit emir: fiyatın gelene kadar defterde bekler." : "Piyasa emri karşı tarafın en iyi fiyatından verilir.", small);

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
                limitMode = true;
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
        if (!Market.I.Live) { chart = new Dictionary<string, object> { { "_hata", "Grafik hesabınla giriş yapınca görünür." } }; return; }
        chartBusy = true;
        var s = trade;
        Market.I.Get((Market.I.Tm ? "/tmtbs/hisse/" + s.id + "/grafik?aralik=" : "/dunya/grafik/" + s.id + "?aralik=") + chartRange, (r, err) =>
        {
            chartBusy = false;
            if (trade != s) return;
            chart = r ?? new Dictionary<string, object> { { "_hata", err } };
        });
    }

    void DrawChart(float iw)
    {
        GUILayout.BeginHorizontal();
        foreach (var (k, n) in new[] { ("1g", "1 gün"), ("7g", "7 gün"), ("1a", "1 ay"), ("1y", "1 yıl") })
            if (GUILayout.Button(n, k == chartRange ? tabOn : tab, GUILayout.Height(32))) { chartRange = k; chart = null; LoadChart(); }
        GUILayout.EndHorizontal();
        if (chart == null || chartBusy) { GUILayout.Label("yükleniyor…", small); return; }
        if (chart.ContainsKey("_hata")) { Msg(J.S(chart, "_hata"), false); return; }
        var pts = J.A(chart, "noktalar");
        var rect = GUILayoutUtility.GetRect(iw, 170);
        if (Event.current.type == EventType.Repaint && pts.Count > 1)
        {
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var p in pts) { double f = J.D(p, "f"); if (f < lo) lo = f; if (f > hi) hi = f; }
            if (hi - lo < 1e-6) { hi += 1; lo -= 1; }
            bool up = J.D(pts[pts.Count - 1], "f") >= J.D(pts[0], "f");
            Color c = up ? new Color(0.3f, 0.9f, 0.45f, 0.9f) : new Color(1f, 0.4f, 0.4f, 0.9f);
            Color old = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.05f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            float bw = rect.width / pts.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                float v = (float)((J.D(pts[i], "f") - lo) / (hi - lo));
                float bh = Mathf.Max(2f, v * (rect.height - 6));
                GUI.color = new Color(c.r, c.g, c.b, 0.35f);
                GUI.DrawTexture(new Rect(rect.x + i * bw, rect.yMax - bh, Mathf.Max(1f, bw - 0.5f), bh), Texture2D.whiteTexture);
                GUI.color = c;
                GUI.DrawTexture(new Rect(rect.x + i * bw, rect.yMax - bh - 1.5f, Mathf.Max(1.5f, bw), 3f), Texture2D.whiteTexture);
            }
            GUI.color = old;
            GUI.Label(new Rect(rect.x + 4, rect.y + 2, 200, 20), Fmt.N2(hi), small);
            GUI.Label(new Rect(rect.x + 4, rect.yMax - 20, 200, 20), Fmt.N2(lo), small);
        }
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
