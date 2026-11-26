using System.Collections.Generic;
using UnityEngine;

/// <summary>Trade of MTBS hesabına özel bölümler: kademeli sıralama, yol haritası, düello, MTBS Bankası kredisi.</summary>
public partial class SalonDirector
{
    // ---------------------------------------------------------------- Sıralama

    void TmRank()
    {
        if (!NeedLive()) return;
        GUILayout.BeginHorizontal();
        foreach (var (k, n) in new[] { ("gun", "Gün"), ("hafta", "Hafta"), ("ay", "Ay"), ("yil", "Yıl") })
            if (TabBtn(n, k == rankPeriod, 32))
            {
                rankPeriod = k;
                pdata.Remove("sira");
                Fetch("sira", RankPath());
            }
        GUILayout.EndHorizontal();
        RankTierPicker();
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

    void TmRoad()
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

    void TmDuel()
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

    string payText = "";
    string bankMsg;
    bool bankOk, bankBusy;

    void OpenCredit()
    {
        OpenPanelCommon(Panel.Credit);
        bankMsg = null;
        payText = "";
        Market.I.RefreshStatus();
    }

    static string CreditState(string d)
    {
        switch (d)
        {
            case "aktif": return "<color=#5BE38A>Aktif</color>";
            case "bitti": return "Kapandı";
            case "gecikmede": return "<color=#FF7A7A>Gecikmede</color>";
            case "": return "Kredi yok";
            default: return d;
        }
    }

    void DrawCredit()
    {
        var m = Market.I;
        var r = PanelRect(480);
        BeginPanel(r);
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>MTBS Bankası · Vezne</b>", h2);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("×", btn, GUILayout.Width(44), GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        GUILayout.EndHorizontal();

        if (!m.Live)
        {
            GUILayout.Label("Kredi çekmek ve borç ödemek için hesabınla giriş yapmalısın. Demo kipinde banka işlemi yapılamaz.", label);
            GUILayout.Space(6);
            GUILayout.Label("Kredi: 100.000 MLS, beş günde 20.000'lik dilimler halinde hesabına yatar. 30 günde bir 10.000 MLS taksit otomatik tahsil edilir; geciken taksitte %5 ceza işler ve borç ödenene kadar alış yapılamaz.", small);
            GUILayout.Space(10);
            if (GUILayout.Button("Kapat", GUILayout.Height(38))) { EndPanel(); ClosePanel(); return; }
            EndPanel();
            return;
        }

        var k = m.credit;
        Row("Nakit", Fmt.Mls(m.cash));
        string durum = J.S(k, "durum");
        Row("Kredi", CreditState(durum));
        if (k.Count > 0 && durum != "")
        {
            Row("Kredi tutarı", Fmt.Mls(J.D(k, "anapara")));
            Row("Hesabına yatan", Fmt.Mls(J.D(k, "verilen")));
            if (J.D(k, "dilim_sayisi") > 0)
                Row("Dilimler", $"{Fmt.N(J.D(k, "dilim_verilen"))} / {Fmt.N(J.D(k, "dilim_sayisi"))} · {Fmt.Mls(J.D(k, "dilim"))}");
            if (J.S(k, "sonraki_dilim") != "") Row("Sonraki dilim", ShortDate(J.S(k, "sonraki_dilim")));
            Row("Kalan borç", $"<b>{Fmt.Mls(J.D(k, "kalan"))}</b>");
            Row("Ödenen", Fmt.Mls(J.D(k, "odenen")));
            Row("Taksit", Fmt.Mls(J.D(k, "taksit")));
            if (J.S(k, "sonraki_vade") != "") Row("Sonraki vade", $"{ShortDate(J.S(k, "sonraki_vade"))} · {Fmt.N(J.D(k, "kalan_gun"))} gün");
            if (J.D(k, "gecikmis") > 0) Row("<color=#FF7A7A>Gecikmiş</color>", $"<color=#FF7A7A>{Fmt.Mls(J.D(k, "gecikmis"))}</color>");
            if (J.D(k, "ceza_toplam") > 0) Row("Ceza toplamı", Fmt.Mls(J.D(k, "ceza_toplam")));
            if (J.B(k, "alis_yasak")) GUILayout.Label("<color=#FF7A7A>Gecikmiş taksidin var: ödeyene kadar borsada alış yapamazsın.</color>", small);
        }
        else GUILayout.Label("Henüz kredi kullanmadın. 100.000 MLS'lik kredi beş günde 20.000'lik dilimlerle hesabına yatar.", small);

        GUILayout.Space(8);
        GUI.enabled = !bankBusy;
        GUI.backgroundColor = new Color(0.35f, 0.7f, 1f);
        if (GUILayout.Button(durum == "aktif" ? "Sıradaki dilimi şimdi çek" : "Kredi çek (100.000 MLS)", btn, GUILayout.Height(46)))
        {
            bankBusy = true;
            bankMsg = null;
            m.CreditDraw((msg, ok) => { bankBusy = false; bankMsg = msg; bankOk = ok; if (ok && Bell.I) Bell.I.Ring(); });
        }
        GUI.backgroundColor = SalonDirector.BtnBase;

        double kalan = J.D(k, "kalan");
        if (kalan > 0)
        {
            GUILayout.Label("Borç ödemesi (nakitten)", small);
            GUILayout.BeginHorizontal();
            payText = HtmlInput.Field("odeme", payText, "Ödenecek tutar (MLS)", field, false, true, 12, GUILayout.Height(40), GUILayout.MinWidth(120));
            if (GUILayout.Button("Öde", btn, GUILayout.Width(80), GUILayout.Height(40)))
            {
                long amt = (long)ParsePrice(payText);
                if (amt <= 0) { bankMsg = "Bir tutar yaz."; bankOk = false; }
                else Pay(amt);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            double tk = J.D(k, "taksit");
            if (tk > 0 && GUILayout.Button("Bir taksit (" + Fmt.N(System.Math.Min(tk, kalan)) + ")", GUILayout.Height(34))) Pay((long)System.Math.Min(tk, kalan));
            if (GUILayout.Button("Tamamını öde (" + Fmt.N(kalan) + ")", GUILayout.Height(34))) Pay((long)System.Math.Ceiling(kalan));
            GUILayout.EndHorizontal();
        }
        GUI.enabled = true;
        if (bankBusy) GUILayout.Label("İşleniyor…", small);
        Msg(bankMsg, bankOk);

        var plan = J.A(k, "plan");
        if (plan.Count > 0)
        {
            Title("Ödeme planı");
            for (int i = 0; i < plan.Count && i < 12; i++) Row(ShortDate(J.S(plan[i], "vade")), Fmt.Mls(J.D(plan[i], "tutar")));
        }
        var moves = J.A(k, "hareketler");
        if (moves.Count > 0)
        {
            Title("Hareketler");
            for (int i = 0; i < moves.Count && i < 15; i++)
            {
                var h = moves[i];
                Row($"{ShortDate(J.S(h, "tarih"))} · {Pretty(J.S(h, "tur"))}", $"{Fmt.Mls(J.D(h, "tutar"))} <size=12>(kalan {Fmt.N(J.D(h, "kalan"))})</size>");
            }
        }
        GUILayout.Space(10);
        if (GUILayout.Button("Kapat (Esc)", GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        EndPanel();
    }

    void Pay(long amt)
    {
        bankBusy = true;
        bankMsg = null;
        Market.I.CreditPay(amt, (msg, ok) => { bankBusy = false; bankMsg = msg; bankOk = ok; if (ok) payText = ""; });
    }

    // ---------------------------------------------------------------- Sekme yönlendirme (Trade of MTBS)

    static readonly string[] TmTabs = { "Ana", "Piyasa", "Portföy", "Haberler", "Sıralama", "Kademe", "Görevler", "Düello", "Haklar", "Profil", "Git" };

    bool LoadTm(string tab)
    {
        var m = Market.I;
        switch (tab)
        {
            case "Ana": Fetch("ana", "/tmtbs/ana"); return true;
            case "Haberler": Fetch("haber", "/tmtbs/haberler"); return true;
            case "Sıralama": Fetch("sira", RankPath()); return true;
            case "Görevler":
                Fetch("yol", "/tmtbs/yol");
                if (m.Live) m.Post("/tmtbs/yol/goruldu", null, (r, e) => { });
                return true;
            case "Düello": Fetch("duello", "/tmtbs/duello"); return true;
            case "Profil": Fetch("profil", "/tmtbs/profil/ben"); return true;
            case "Kademe": case "Portföy": case "Haklar": m.RefreshStatus(); return true;
            case "Git": case "Piyasa": return true;
        }
        return false;
    }

    bool DrawTm(string tab)
    {
        switch (tab)
        {
            case "Sıralama": TmRank(); return true;
            case "Görevler": TmRoad(); return true;
            case "Düello": TmDuel(); return true;
            case "Kademe": TmTiers(); return true;
            case "Git": TmTravel(); return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- Kademeler (oyuncu seviyesi)

    static readonly (double esik, string ad)[] Tiers =
    {
        (0, "Çaylak"), (10000, "Yatırımcı"), (50000, "Tüccar"), (150000, "Borsacı"),
        (500000, "Fon Yöneticisi"), (1500000, "Borsa Kurdu"), (5000000, "Balina"), (20000000, "Efsane"),
    };

    /// <summary>Net değere göre kademe numarası (1..8).</summary>
    static int TierOf(double net)
    {
        int k = 1;
        for (int i = 0; i < Tiers.Length; i++) if (net >= Tiers[i].esik) k = i + 1;
        return k;
    }

    int rankTier; // 0: kendi kademem

    string RankPath() => "/tmtbs/siralama?donem=" + rankPeriod + (rankTier > 0 ? "&kademe=" + rankTier : "");

    void RankTierPicker()
    {
        int mine = TierOf(Market.I.net);
        if (pdata.TryGetValue("sira", out var d))
        {
            int bk = (int)J.D(d, "benim_kademem");
            if (bk > 0) mine = bk;
        }
        var counts = new Dictionary<int, int>();
        if (pdata.TryGetValue("sira", out var dd))
            foreach (var k in J.A(dd, "kademeler")) counts[(int)J.D(k, "no")] = (int)J.D(k, "n");
        GUILayout.Label("Her oyuncu kendi kademesindekilerle yarışır. Başka kademeye de bakabilirsin:", small);
        int sel = rankTier > 0 ? rankTier : mine;
        for (int i = 0; i < Tiers.Length; i += 4)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + 4 && j < Tiers.Length; j++)
            {
                int no = j + 1;
                string n = Tiers[j].ad + (counts.TryGetValue(no, out var c) ? $" ({c})" : "") + (no == mine ? " *" : "");
                if (TabBtn(n, no == sel, 30))
                {
                    rankTier = no == mine ? 0 : no;
                    pdata.Remove("sira");
                    Fetch("sira", RankPath());
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    void TmTiers()
    {
        var m = Market.I;
        double net = m.net;
        int k = TierOf(net);
        GUILayout.Label($"Kademen: <b><color=#FFD54A>Sv. {k} · {Tiers[k - 1].ad}</color></b>", h2);
        Row("Net değerin", Fmt.Mls(net));
        if (k < Tiers.Length)
        {
            double lo = Tiers[k - 1].esik, hi = Tiers[k].esik;
            float p = Mathf.Clamp01((float)((net - lo) / (hi - lo)));
            Row("Sonraki", $"{Tiers[k].ad} · {Fmt.Mls(System.Math.Max(0, hi - net))} kaldı");
            var bar = GUILayoutUtility.GetRect(10, 14, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(1, 1, 1, 0.12f), 0, 0);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * p, bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(1f, 0.82f, 0.3f), 0, 0);
        }
        else GUILayout.Label("<color=#FFD54A>En üst kademedesin!</color>", label);

        Title("Kademe tablosu (net değere göre)");
        for (int i = 0; i < Tiers.Length; i++)
        {
            string ad = $"Sv. {i + 1} · {Tiers[i].ad}";
            Row(i + 1 == k ? $"<color=#FFD54A><b>{ad} Â· sen</b></color>" : ad, Fmt.Mls(Tiers[i].esik) + "+");
        }
        GUILayout.Label("Sıralamada her oyuncu kendi kademesindeki oyuncularla yarışır; net değerin eşiği geçince bir üst kademeye çıkarsın.", small);

        Title("Alım-satım kuralları");
        GUILayout.Label("<b>Fiyat kademesi</b> (fiyatın en küçük adımı):", small);
        Row("10 MLS altı", "0,1");
        Row("10 – 50", "0,5");
        Row("50 – 100", "1");
        Row("100 – 500", "5");
        Row("500 ve üstü", "10");
        GUILayout.Label("• Limit emir fiyatı kademeye uymalı ve günün fiyat bandı içinde olmalı.\n" +
                        "• Piyasa emri karşı taraftaki en iyi fiyattan hemen gerçekleşir; limit emir eşleşene kadar defterde bekler, istediğinde iptal edersin.\n" +
                        "• Bekleyen satış emrindeki hisseler satılabilir adetten düşer; bekleyen alış emrinin tutarı nakitten ayrılır.\n" +
                        "• Banka kredisinin taksidi gecikirse borç ödenene kadar alış yapılamaz.\n" +
                        "• Düello hesabıyla yapılan işlemler ana portföyünü etkilemez.", small);
    }

    /// <summary>Banko panelinin altına kısa kural notu (fiyat kademesi + kredi yasağı).</summary>
    void TradeRulesNote(Stock s)
    {
        GUILayout.Space(6);
        if (J.B(Market.I.credit, "alis_yasak"))
            GUILayout.Label("<color=#FF7A7A>Gecikmiş kredi taksidin var: ödeyene kadar alış yapamazsın (salondaki MTBS Bankası veznesi).</color>", small);
        GUILayout.Label($"<color=#7d879c>Fiyat kademesi bu fiyatta {Fmt.N2(Tick.Step(s.price))} MLS · kurallar: Menü → Kademe</color>", small);
    }

    void LoadDuelAccounts()
    {
        Market.I.Get("/tmtbs/duello", (r, err) =>
        {
            activeDuels.Clear();
            if (r == null) return;
            foreach (var d in J.A(r, "benim"))
            {
                string st = J.S(d, "durum");
                if (st == "aktif" || st == "devam" || st == "basladi") activeDuels.Add(J.L(d, "id"));
            }
        });
    }

    // ---------------------------------------------------------------- Git (yalnız salon içi)

    void TmTravel()
    {
        GUILayout.Label("Salonun içinde hızlı git:", small);
        if (GUILayout.Button("Panoların önü", btn, GUILayout.Height(42))) { GoTo(new Vector3(0, 0, SalonBuilder.HalfD - 7f), Quaternion.identity); Show("Seans salonu", 1.5f); }
        if (GUILayout.Button("Peron A", btn, GUILayout.Height(42))) { GoTo(new Vector3(-BotTrader.Lane, 0, 0), Quaternion.identity); Show("Peron A", 1.5f); }
        if (GUILayout.Button("Peron B", btn, GUILayout.Height(42))) { GoTo(new Vector3(BotTrader.Lane, 0, 0), Quaternion.identity); Show("Peron B", 1.5f); }
        if (GUILayout.Button("MTBS Bankası veznesi", btn, GUILayout.Height(42))) { GoTo(SalonBuilder.VezneSpot, Quaternion.LookRotation(Vector3.back)); Show("Vezne", 1.5f); }
        if (GUILayout.Button("Düello salonu", btn, GUILayout.Height(42))) { GoTo(SalonBuilder.DuelSpot, Quaternion.LookRotation(Vector3.back)); Show("Düello salonu", 1.5f); }
        if (GUILayout.Button("Salon kapısı · şehre çıkış", btn, GUILayout.Height(42))) { GoTo(new Vector3(0, 0, -SalonBuilder.HalfD + 4.5f), Quaternion.LookRotation(Vector3.back)); gateAgain = Time.time + 1f; }
        GUILayout.Label("Trade of MTBS hesabıyla yalnız borsa salonundasın. Şehre çıkmak, ev, iş ve dükkânlar için Müteşebbis hesabıyla gir.", small);
    }

    // ---------------------------------------------------------------- Kapı: Müteşebbis'e yönlendirme

    float gateAgain;
    bool switchToMt;

    void OpenGate()
    {
        OpenPanelCommon(Panel.Gate);
        gateAgain = Time.time + 6f;
    }

    void DrawGate()
    {
        float w = Mathf.Min(460, W - 16), h = 400;
        var r = new Rect((W - w) / 2, Mathf.Max(8, (H - h) / 2), w, h);
        BeginPanel(r);
        GUILayout.Label("Şehir Müteşebbis'in dünyası", h2);
        GUILayout.Space(4);
        GUILayout.Label("Trade of MTBS hesabıyla yalnız <b>borsa salonunda</b> oynarsın: al-sat, portföy, sıralama, düello ve banka veznesi burada.", label);
        GUILayout.Space(4);
        GUILayout.Label("Kapının dışındaki şehir <b>Müteşebbis</b> oyununun: evinde uyan, babandan harçlık al, iş bul, otobüse bin, ev ve araba al, dükkânlardan alışveriş yap, kendi markanı kur. Bunun için Müteşebbis'e ayrıca üye olmalısın (ücretsiz). Telefonda Müteşebbis uygulaması da var.", small);
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.35f, 0.8f, 0.5f);
        if (GUILayout.Button("Müteşebbis hesabımla gir", btn, GUILayout.Height(44)))
        {
            EndPanel();
            ClosePanel();
            loginAcc = Market.Account.Mutesebbis;
            switchToMt = true;
            Market.I.Logout();
            return;
        }
        GUI.backgroundColor = SalonDirector.BtnBase;
        if (GUILayout.Button("mutesebbis.tr'de üye ol", btn, GUILayout.Height(40))) Application.OpenURL("https://mutesebbis.tr/register");
        if (GUILayout.Button("Salona dön", GUILayout.Height(38)))
        {
            EndPanel();
            ClosePanel();
            player.Teleport(player.transform.position + Vector3.forward * 2.5f, Quaternion.identity);
            return;
        }
        EndPanel();
    }

    // ---------------------------------------------------------------- Google / Apple

    void OnSocial(string provider, string token, string name, string error)
    {
        if (view == View.Game) return;
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(token))
        {
            busy = false;
            formMsg = error == "iptal" || string.IsNullOrEmpty(error) ? "Giriş yarıda bırakıldı." : "Giriş olmadı: " + error;
            formErr = true;
            return;
        }
        busy = true;
        formMsg = (provider == "apple" ? "Apple" : "Google") + " ile giriliyor…";
        formErr = false;
        Market.I.SocialLoginTm(provider, token, name, err =>
        {
            busy = false;
            if (err != null) { formMsg = err; formErr = true; return; }
            formMsg = null;
            EnterGame();
        });
    }

    void SocialButtons()
    {
        GUI.backgroundColor = SalonDirector.BtnBase;
        if (GUILayout.Button("<b>G</b>  Google ile devam et", btn, GUILayout.Height(40)))
        {
            formMsg = "Google penceresi açılıyor…";
            formErr = false;
            SocialLogin.Begin("google");
        }
        GUI.backgroundColor = new Color(0.25f, 0.25f, 0.28f);
        if (GUILayout.Button("<color=white><b>Apple</b> ile devam et</color>", btn, GUILayout.Height(40)))
        {
            formMsg = "Apple penceresi açılıyor…";
            formErr = false;
            SocialLogin.Begin("apple");
        }
        GUI.backgroundColor = SalonDirector.BtnBase;
    }
}
