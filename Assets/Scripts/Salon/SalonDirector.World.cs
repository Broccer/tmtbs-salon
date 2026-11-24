using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Müteşebbis dünyası: yatakta uyanma, baba (bir kerelik 10.000 MLS harçlık) ve anne, binalardaki işlemler
/// (iş bulma, emlak, araba galerisi, müzayede, dükkânlar, büfe, otobüs, banka faturası). Hepsi Müteşebbis sunucusunun
/// gerçek uçlarıyla çalışır; aynı bölümler cep menüsünden de açılır.
/// </summary>
public partial class SalonDirector
{
    string placeTab;              // null değilse cep paneli tek bir binanın paneli olarak açılır
    string placeTitle;
    int placeArg;
    long shopId;
    long auctionSel;
    string bidText = "", sellText = "";
    bool worldBusy;

    bool allowanceKnown, allowanceTaken, fatherAutoShown;
    int dialogWho;
    string dialogMsg;
    bool dialogOk;
    float fadeUntil;
    int seenDash = -1;

    string CurrentTab => placeTab ?? PhoneTabs[phoneTab];

    // ================================================================ giriş / dünya verisi

    void WakeUp()
    {
        fadeUntil = Time.time + 2.5f;
        fatherAutoShown = false;
        Show("Günaydın! Yatağında uyandın.", 3.5f);
        Hint("Odandan çık, salona geç: annen ve baban seni bekliyor.", 9f);
        LoadWorldData();
    }

    void LoadWorldData()
    {
        var m = Market.I;
        if (!m.Live)
        {
            allowanceKnown = true;
            return;
        }
        allowanceKnown = false;
        m.Get("/dunya/durum", (r, err) =>
        {
            allowanceKnown = true;
            allowanceTaken = r != null && J.B(r, "harclik_alindi");
        });
        m.Get("/pazar", (r, err) =>
        {
            if (r == null) return;
            var stores = J.A(r, "magazalar");
            for (int i = 0; i < City.ShopCount; i++)
            {
                if (i >= stores.Count) { City.ShopIds[i] = 0; continue; }
                City.ShopIds[i] = J.L(stores[i], "id");
                City.ShopNames[i] = J.S(stores[i], "isim");
                if (City.ShopSigns[i] != null)
                {
                    string n = City.ShopNames[i];
                    City.ShopSigns[i].text = (n.Length > 18 ? n.Substring(0, 17) + "…" : n).ToUpper(new System.Globalization.CultureInfo("tr-TR"));
                }
            }
        });
        m.Get("/muzayede", (r, err) =>
        {
            if (r == null) return;
            var list = J.A(r, "aktif");
            for (int i = 0; i < City.ArtLabels.Count; i++)
            {
                var lbl = City.ArtLabels[i];
                if (lbl == null) continue;
                if (i >= list.Count) { lbl.text = "— boş —"; continue; }
                var o = list[i];
                lbl.text = $"{Cut(J.S(o, "urun_adi"), 22)}\n<size=70>{Cut(J.S(o, "sanatci"), 22)}</size>\n{Fmt.Mls(J.D(o, "guncel_fiyat"))}";
            }
        });
    }

    static string Cut(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length > n ? s.Substring(0, n - 1) + "…" : s;

    /// <summary>Panel/dünya etkileşimlerine ek Update işleri: baba ilk karşılaşma, oda levhası.</summary>
    void WorldUpdate()
    {
        var m = Market.I;
        if (m.dashVersion != seenDash && Town.LevelSign != null)
        {
            seenDash = m.dashVersion;
            var ev = J.O(m.dash, "ev");
            if (ev.Count > 0)
                Town.LevelSign.text = $"EV SEVİYESİ {Fmt.N(J.D(ev, "seviye"))}\n<size=60>{Cut(J.S(ev, "ev_adi"), 24)}</size>";
        }
        if (panel != Panel.None || fatherAutoShown || Town.Father == null || !allowanceKnown) return;
        if (m.Live && allowanceTaken) { fatherAutoShown = true; return; }
        if ((player.transform.position - Town.Father.position).Flat().magnitude < 3.6f)
        {
            fatherAutoShown = true;
            OpenDialog(0);
        }
    }

    bool UseWorld(Interactable it)
    {
        switch (it.kind)
        {
            case Interactable.Kind.Teller: OpenPlace("Fatura", "MTBS Bankası · Vezne", 0); return true;
            case Interactable.Kind.BusStop: OpenPlace("Otobüs", "Durak · " + City.Stops[it.arg].name, it.arg); return true;
            case Interactable.Kind.Buffet: OpenPlace("Büfe", "Büfe · " + City.Stops[it.arg].name, it.arg); return true;
            case Interactable.Kind.JobCenter: OpenPlace("İş", "İş Bulma Merkezi", 0); return true;
            case Interactable.Kind.Estate: OpenPlace("Emlak", "Emlak Ofisi", 0); return true;
            case Interactable.Kind.CarDealer: OpenPlace("Galeri", "Araba Galerisi", 0); return true;
            case Interactable.Kind.Auction: OpenPlace("Müzayede", "Müzayede Salonu & Galeri", 0); return true;
            case Interactable.Kind.Shop:
                shopId = City.ShopIds[it.arg];
                OpenPlace("Dükkân", string.IsNullOrEmpty(City.ShopNames[it.arg]) ? "Mağaza" : City.ShopNames[it.arg], it.arg);
                return true;
            case Interactable.Kind.Parent: OpenDialog(it.arg); return true;
        }
        return false;
    }

    void OpenPlace(string tab, string title, int arg)
    {
        OpenPanelCommon(Panel.Phone);
        atComputer = false;
        placeTab = tab;
        placeTitle = title;
        placeArg = arg;
        phoneMsg = null;
        auctionSel = 0;
        pdata.Clear();
        perr.Clear();
        LoadTabName(tab);
    }

    void LoadTabName(string tab)
    {
        var m = Market.I;
        switch (tab)
        {
            case "Ana": Fetch("ana", "/dunya/piyasa"); break;
            case "Hayat": m.RefreshStatus(); break;
            case "İş": Fetch("kariyer", "/kariyer"); Fetch("ilanlar", "/kariyer/ilanlar"); break;
            case "Alışveriş": Fetch("pazar", "/pazar"); if (shopId > 0) Fetch("magaza" + shopId, "/pazar/" + shopId); break;
            case "Dükkân": if (shopId > 0) Fetch("magaza" + shopId, "/pazar/" + shopId); break;
            case "Emlak": Fetch("ev", "/hayat/ev"); Fetch("evp", "/hayat/ev-pazari"); break;
            case "Galeri": Fetch("ulasim", "/hayat/ulasim"); Fetch("arp", "/hayat/arac-pazari"); break;
            case "Müzayede": Fetch("muz", "/muzayede"); if (auctionSel > 0) Fetch("muz" + auctionSel, "/muzayede/" + auctionSel); break;
            case "Haberler": Fetch("haber", "/haber?limit=30"); break;
            case "Sıralama": Fetch("sira", "/siralama-ozet"); break;
            case "Görevler": Fetch("yol", "/yol-haritasi"); break;
            case "Profil": Fetch("profil", "/profil"); break;
            case "Büfe": Fetch("ulasim", "/hayat/ulasim"); Fetch("yemek", "/hayat/yemek"); break;
            case "Otobüs": case "Ulaşım": Fetch("ulasim", "/hayat/ulasim"); break;
            case "Fatura": Fetch("ev", "/hayat/ev"); Fetch("varlik", "/varlik"); break;
            case "Portföy": case "Haklar": m.RefreshStatus(); break;
        }
    }

    void DrawTabName(string tab)
    {
        switch (tab)
        {
            case "Ana": TabHome(); break;
            case "Hayat": TabLife(); break;
            case "İş": TabJob(); break;
            case "Alışveriş": TabShopping(); break;
            case "Dükkân": if (NeedLive()) DrawStore(shopId, false); break;
            case "Emlak": TabEstate(); break;
            case "Galeri": TabCars(); break;
            case "Müzayede": TabAuction(); break;
            case "Piyasa": TabMarket(); break;
            case "Portföy": TabPortfolio(); break;
            case "Haberler": TabNews(); break;
            case "Sıralama": TabTree("sira", null); break;
            case "Görevler": TabTree("yol", "/yol-haritasi"); break;
            case "Haklar": TabRights(); break;
            case "Profil": TabProfile(); break;
            case "Büfe": TabBuffet(); break;
            case "Otobüs": TabBus(true); break;
            case "Ulaşım": TabBus(false); break;
            case "Fatura": TabBills(); break;
        }
    }

    /// <summary>Sunucuya işlem gönderir; başarılıysa verilen önbellek anahtarlarını silip sekmeyi yeniden yükler.</summary>
    void Act(string path, Dictionary<string, object> body, string okMsg, params string[] reload)
    {
        if (worldBusy) return;
        worldBusy = true;
        phoneMsg = null;
        Market.I.Post(path, body, (r, err) =>
        {
            worldBusy = false;
            phoneMsg = err ?? okMsg;
            phoneOk = err == null;
            if (err != null) return;
            foreach (var k in reload) pdata.Remove(k);
            LoadTabName(CurrentTab);
            Market.I.RefreshStatus();
            if (Bell.I) Bell.I.Ring();
        });
    }

    bool ActButton(string text, float h = 36, float w = 0)
    {
        GUI.enabled = !worldBusy;
        bool b = w > 0 ? GUILayout.Button(text, btn, GUILayout.Width(w), GUILayout.Height(h)) : GUILayout.Button(text, btn, GUILayout.Height(h));
        GUI.enabled = true;
        return b;
    }

    static string Ok(bool v, string yes, string no) => v ? $"<color=#5BE38A>{yes}</color>" : $"<color=#FF7A7A>{no}</color>";

    // ================================================================ Hayat (özet)

    void TabLife()
    {
        if (!NeedLive()) return;
        var d = Market.I.dash;
        if (d.Count == 0) { GUILayout.Label("yükleniyor…", small); return; }
        var tm = J.O(d, "time");
        Row("Oyun zamanı", $"{Fmt.N(J.D(tm, "ay"))}. ay · {Fmt.N(J.D(tm, "sezon"))}. sezon");
        Row("Nakit", Fmt.Mls(Market.I.cash));
        var job = J.O(d, "is");
        string mes = J.S(job, "aktif_meslek");
        Row("İş", mes != "" ? $"<b>{mes}</b> · {Fmt.Mls(J.D(job, "maas"))}" : "<color=#FF7A7A>İşsiz</color> → İş Bulma Merkezi");
        var ev = J.O(d, "ev");
        Row("Konut", J.S(ev, "ev_adi"));
        Row("Ev faturası", Ok(J.B(ev, "gider_odendi"), "bu ay ödendi", $"ödenmedi ({Fmt.Mls(J.D(ev, "gider_tutari"))}) → MTBS Bankası"));
        var ul = J.O(d, "ulasim");
        if (J.S(ul, "arac_adi") != "") Row("Ulaşım", J.S(ul, "arac_adi"));
        Row("Otobüs bileti", Ok(J.B(ul, "bilet_odendi"), "bu ay alındı", $"alınmadı ({Fmt.Mls(J.D(ul, "bilet_ucreti"))}) → durak büfesi"));
        var y = J.O(d, "yemek");
        Row("Bu ayın yemeği", Ok(J.B(y, "bu_ay_alindi"), "alındı", "alınmadı → durak büfesi"));
        var st = J.O(d, "status");
        if (st.Count > 0) { Title("Sıradaki iş"); Dump(st); }
    }

    // ================================================================ İş Bulma Merkezi

    static string JobBlock(Dictionary<string, object> o)
    {
        switch (J.S(o, "engel"))
        {
            case "ceo": return "Marka sahibisin";
            case "rich": return "Servetin bu iş için çok yüksek";
            case "employed": return "Zaten çalışıyorsun";
            case "first_job": return "Önce ilk işini bul";
            case "edu": return "Eğitim gerekli: " + J.S(o, "egitim_adi");
            case "cert": return "Sertifika gerekli: " + J.S(o, "sertifika_adi");
            default: return "Uygun değil";
        }
    }

    void TabJob()
    {
        if (!NeedLive()) return;
        if (Data("kariyer", out var k))
        {
            var job = J.O(k, "is");
            string mes = J.S(job, "aktif_meslek");
            if (mes != "")
            {
                Row("Mesleğin", $"<b>{mes}</b>");
                Row("Maaş", Fmt.Mls(J.D(job, "maas")));
                if (J.D(job, "bekleyen_maas") > 0) Row("Bekleyen maaş", Fmt.Mls(J.D(job, "bekleyen_maas")));
                GUILayout.Label("Maaşını Müteşebbis uygulamasından (kısa bir reklam izleyerek) çekersin.", small);
                if (J.B(job, "ayrilabilir") && ActButton("İşten ayrıl", 34))
                    Act("/kariyer/ayril", null, "İşten ayrıldın.", "kariyer", "ilanlar");
            }
            else GUILayout.Label("Şu an bir işin yok. Aşağıdaki ilanlardan sana uygun olana başvur.", label);
        }
        Title("İş ilanları");
        if (!Data("ilanlar", out var il)) return;
        var list = J.A(il, "ilanlar");
        if (list.Count == 0) { GUILayout.Label("Şu an ilan yok.", small); return; }
        for (int i = 0; i < list.Count && i < 60; i++)
        {
            var o = list[i] as Dictionary<string, object>;
            if (o == null) continue;
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            string sub = J.S(o, "kategori") + (J.S(o, "egitim_adi") != "" ? " · " + J.S(o, "egitim_adi") : "");
            GUILayout.Label($"<b>{J.S(o, "baslik")}</b> · {Fmt.Mls(J.D(o, "maas"))}\n<size=12>{sub}</size>", label);
            GUILayout.FlexibleSpace();
            if (J.B(o, "uygun"))
            {
                long id = J.L(o, "id");
                string title = J.S(o, "baslik");
                if (ActButton("Başvur", 34, 90))
                    Act("/kariyer/basvur", new Dictionary<string, object> { { "ilan_id", id } }, $"Tebrikler, işe girdin: {title}!", "kariyer", "ilanlar");
            }
            else GUILayout.Label($"<color=#FFB74D><size=12>{JobBlock(o)}</size></color>", small, GUILayout.Width(130));
            GUILayout.EndHorizontal();
        }
    }

    // ================================================================ Alışveriş / dükkân

    void TabShopping()
    {
        if (!NeedLive()) return;
        if (shopId > 0)
        {
            if (GUILayout.Button("← Mağazalar", GUILayout.Height(32))) { shopId = 0; return; }
            DrawStore(shopId, true);
            return;
        }
        GUILayout.Label("Markaların mağazaları (çarşıdaki dükkânlar da bunlardan):", small);
        if (!Data("pazar", out var d)) return;
        foreach (var o in J.A(d, "magazalar"))
        {
            long id = J.L(o, "id");
            if (GUILayout.Button($"<b>{J.S(o, "isim")}</b>  <size=12>{Fmt.N(J.D(o, "urun_sayisi"))} ürün</size>",
                new GUIStyle(btn) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal }, GUILayout.Height(36)))
            {
                shopId = id;
                Fetch("magaza" + id, "/pazar/" + id);
            }
        }
    }

    static double ProductPrice(Dictionary<string, object> o)
    {
        if (J.D(o, "displayed_price") > 0) return J.D(o, "displayed_price");
        if (J.B(o, "indirimli_mi") && J.D(o, "indirim_fiyat") > 0) return J.D(o, "indirim_fiyat");
        return J.D(o, "fiyat");
    }

    void DrawStore(long id, bool fromPhone)
    {
        if (id <= 0) { GUILayout.Label("Bu dükkân henüz boş (mağaza bilgisi yükleniyor ya da yok).", label); return; }
        if (!Data("magaza" + id, out var d)) return;
        var mg = J.O(d, "magaza");
        if (fromPhone) GUILayout.Label($"<b>{J.S(mg, "isim")}</b>", h2);
        var items = J.A(d, "urunler");
        if (items.Count == 0) { GUILayout.Label("Bu mağazada satışta ürün yok.", label); return; }
        GUILayout.Label("Ayda alabileceğin ürün sayısı ve kategori sırasını oyun kuralları belirler; sunucu uygun değilse söyler.", small);
        foreach (var x in items)
        {
            var o = x as Dictionary<string, object>;
            if (o == null) continue;
            long uid = J.L(o, "id");
            string name = J.S(o, "isim");
            double px = ProductPrice(o);
            bool disc = px < J.D(o, "fiyat");
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{name}</b>\n<size=12>{Pretty(J.S(o, "kategori"))} · stok {Fmt.N(J.D(o, "stok"))}</size>", label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(disc ? $"<color=#5BE38A><b>{Fmt.Mls(px)}</b></color>\n<size=11><color=#7d879c>{Fmt.Mls(J.D(o, "fiyat"))}</color></size>" : $"<b>{Fmt.Mls(px)}</b>", label, GUILayout.Width(110));
            GUI.enabled = J.D(o, "stok") > 0;
            if (ActButton("Satın al", 34, 90))
                Act("/urun/" + uid + "/satin-al", null, $"Satın aldın: {name}", "magaza" + id);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    // ================================================================ Emlak ofisi / araba galerisi

    void TabEstate()
    {
        if (!NeedLive()) return;
        Title("Kiralık konut (ev seviyen)");
        if (Data("ev", out var e))
        {
            var s = J.O(e, "status");
            Row("Oturduğun yer", $"<b>{J.S(s, "ev_adi")}</b> · seviye {Fmt.N(J.D(s, "seviye"))}/{Fmt.N(J.D(s, "max_seviye"))}");
            Row("Aylık kira/gider", Fmt.Mls(J.D(s, "gider_tutari")));
            if (J.B(s, "yukseltme_devam_ediyor")) Row("Taşınma", $"sürüyor · {Fmt.N(J.D(s, "kalan_ay"))} ay kaldı");
            else if (J.S(s, "sonraki_adi") != "")
            {
                if (J.B(s, "yukseltilebilir"))
                {
                    if (ActButton($"\"{J.S(s, "sonraki_adi")}\" konutuna taşın", 38))
                        Act("/hayat/ev/yukselt", null, "Taşınma başladı.", "ev");
                }
                else GUILayout.Label($"Sıradaki konut: {J.S(s, "sonraki_adi")} (şartları henüz sağlamıyorsun).", small);
            }
        }
        Title("Satılık evler");
        Listings("evp", "/hayat/ev-pazari", "Ev");
    }

    void TabCars()
    {
        if (!NeedLive()) return;
        Title("Ulaşım seviyen");
        if (Data("ulasim", out var u))
        {
            var s = J.O(u, "status");
            Row("Aracın", $"<b>{(J.S(s, "arac_adi") != "" ? J.S(s, "arac_adi") : "Toplu taşıma")}</b> · seviye {Fmt.N(J.D(s, "seviye"))}");
            if (J.B(s, "yukseltme_devam_ediyor")) Row("Yükseltme", $"sürüyor · {Fmt.N(J.D(s, "kalan_ay"))} ay kaldı");
            else if (J.S(s, "sonraki_adi") != "" && J.B(s, "yukseltilebilir") && ActButton($"\"{J.S(s, "sonraki_adi")}\" seviyesine geç", 38))
                Act("/hayat/ulasim/yukselt", null, "Ulaşım yükseltmesi başladı.", "ulasim");
        }
        Title("Satılık arabalar");
        Listings("arp", "/hayat/arac-pazari", "Araba");
    }

    /// <summary>Ev / araba pazarı: ilanlar (al), sahip oldukların (sat / satıştan çek).</summary>
    void Listings(string key, string path, string what)
    {
        if (!Data(key, out var d)) return;
        var owned = J.A(d, "sahip");
        if (owned.Count > 0)
        {
            GUILayout.Label($"<b>Senin {what.ToLower()}ların</b>", label);
            foreach (var x in owned)
            {
                var o = x as Dictionary<string, object>;
                if (o == null) continue;
                OwnedRow(o, key, path);
            }
            GUILayout.Space(6);
        }
        var list = J.A(d, "ilanlar");
        if (list.Count == 0) { GUILayout.Label("Şu an ilan yok.", small); return; }
        foreach (var x in list)
        {
            var o = x as Dictionary<string, object>;
            if (o == null) continue;
            if (J.B(o, "kendi_evi")) continue;
            long id = J.L(o, "id");
            string name = J.S(o, "isim");
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            string seller = J.S(o, "satici_adsoyad") != "" ? " · satıcı " + J.S(o, "satici_adsoyad") : "";
            GUILayout.Label($"<b>{name}</b> · {Fmt.Mls(J.D(o, "fiyat"))}\n<size=12>min. seviye {Fmt.N(J.D(o, "min_seviye"))}{seller}</size>", label);
            GUILayout.FlexibleSpace();
            if (J.B(o, "satin_alinabilir"))
            {
                if (ActButton("Satın al", 34, 90))
                    Act(path + "/al", new Dictionary<string, object> { { "id", id } }, $"{name} artık senin!", key);
            }
            else GUILayout.Label("<color=#FFB74D><size=12>şartlar uymuyor</size></color>", small, GUILayout.Width(110));
            GUILayout.EndHorizontal();
        }
    }

    void OwnedRow(Dictionary<string, object> o, string key, string path)
    {
        long id = J.L(o, "id");
        string name = J.S(o, "isim");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>{name}</b> · değer {Fmt.Mls(J.D(o, "fiyat"))}{(J.B(o, "satista_mi") ? " · <color=#FFD54A>satışta</color>" : "")}", label);
        GUILayout.FlexibleSpace();
        if (J.B(o, "satista_mi"))
        {
            if (ActButton("Satıştan çek", 32, 110)) Act(path + "/cek", new Dictionary<string, object> { { "id", id } }, "İlan kaldırıldı.", key);
        }
        else
        {
            sellText = HtmlInput.Field("satis" + id, sellText, "Fiyat", field, false, true, 12, GUILayout.Width(110), GUILayout.Height(32));
            if (ActButton("Sat", 32, 60))
            {
                long px = (long)ParsePrice(sellText);
                if (px <= 0) { phoneMsg = "Satış fiyatı yaz."; phoneOk = false; }
                else Act(path + "/sat", new Dictionary<string, object> { { "id", id }, { "fiyat", px } }, "Satışa çıkarıldı.", key);
            }
        }
        GUILayout.EndHorizontal();
    }

    // ================================================================ Müzayede

    void TabAuction()
    {
        if (!NeedLive()) return;
        if (auctionSel > 0)
        {
            if (GUILayout.Button("← Bütün eserler", GUILayout.Height(32))) { auctionSel = 0; return; }
            DrawLot(auctionSel);
            return;
        }
        if (!Data("muz", out var d)) return;
        var list = J.A(d, "aktif");
        GUILayout.Label("Açık artırmadaki eserler. Bir esere dokun, teklif ver.", small);
        if (list.Count == 0) GUILayout.Label("Şu an açık artırma yok.", label);
        foreach (var x in list)
        {
            var o = x as Dictionary<string, object>;
            if (o == null) continue;
            long id = J.L(o, "id");
            if (GUILayout.Button($"<b>{J.S(o, "urun_adi")}</b> · {J.S(o, "sanatci")}\n<size=12>güncel {Fmt.Mls(J.D(o, "guncel_fiyat"))} · bitiş {ShortDate(J.S(o, "bitis_tarihi"))}</size>",
                new GUIStyle(btn) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal, wordWrap = true }, GUILayout.Height(52)))
            {
                auctionSel = id;
                bidText = System.Math.Ceiling(J.D(o, "guncel_fiyat") * 1.05 + 1).ToString("0");
                Fetch("muz" + id, "/muzayede/" + id);
            }
        }
        var ended = J.A(d, "bitmis");
        if (ended.Count > 0)
        {
            Title("Biten müzayedeler");
            for (int i = 0; i < ended.Count && i < 10; i++)
                Row(J.S(ended[i], "urun_adi"), Fmt.Mls(J.D(ended[i], "guncel_fiyat")));
        }
    }

    void DrawLot(long id)
    {
        if (!Data("muz" + id, out var d)) return;
        var u = J.O(d, "urun");
        GUILayout.Label($"<b>{J.S(u, "urun_adi")}</b>", h2);
        if (J.S(u, "sanatci") != "") Row("Sanatçı", J.S(u, "sanatci"));
        if (J.S(u, "aciklama") != "") GUILayout.Label(J.S(u, "aciklama"), small);
        Row("Güncel fiyat", $"<b>{Fmt.Mls(J.D(u, "guncel_fiyat"))}</b>");
        Row("Bitiş", ShortDate(J.S(u, "bitis_tarihi")));
        GUILayout.BeginHorizontal();
        bidText = HtmlInput.Field("teklif", bidText, "Teklifin (MLS)", field, false, true, 12, GUILayout.Height(40), GUILayout.MinWidth(120));
        if (ActButton("Teklif ver", 40, 110))
        {
            long b = (long)ParsePrice(bidText);
            if (b <= 0) { phoneMsg = "Bir teklif yaz."; phoneOk = false; }
            else Act("/muzayede/" + id + "/teklif", new Dictionary<string, object> { { "teklif", b } }, "Teklifin alındı. Biri geçerse haber verilir.", "muz", "muz" + id);
        }
        GUILayout.EndHorizontal();
        var bids = J.A(d, "teklifler");
        if (bids.Count > 0)
        {
            Title("Teklifler");
            for (int i = 0; i < bids.Count && i < 12; i++)
            {
                var o = bids[i] as Dictionary<string, object>;
                if (o == null) continue;
                string who = J.S(o, "adsoyad") != "" ? J.S(o, "adsoyad") : J.S(o, "kullanici_adi");
                Row($"{(who != "" ? who : "Teklif")} <size=12>{ShortDate(J.S(o, "teklif_tarihi"))}</size>", Fmt.Mls(J.D(o, "teklif_miktari")));
            }
        }
    }

    // ================================================================ Büfe / otobüs / fatura

    void TabBuffet()
    {
        if (!NeedLive()) return;
        Title("Aylık otobüs bileti");
        if (Data("ulasim", out var u))
        {
            var s = J.O(u, "status");
            if (J.B(s, "bilet_odendi")) GUILayout.Label("<color=#5BE38A>Bu ayın bileti alındı.</color> Otobüse binebilirsin.", label);
            else
            {
                GUILayout.Label($"Bu ay biletini almadın. Almazsan ay sonunda {Fmt.Mls(J.D(s, "bilet_ceza"))} ceza kesilir.", small);
                if (ActButton($"Bilet al ({Fmt.Mls(J.D(s, "bilet_ucreti"))})", 42))
                    Act("/hayat/ulasim/bilet", null, "Biletin alındı, bu ay otobüse binebilirsin.", "ulasim");
            }
        }
        Title("Bu ayın yemeği");
        if (!Data("yemek", out var y)) return;
        var st = J.O(y, "status");
        if (J.B(st, "bu_ay_alindi"))
        {
            GUILayout.Label($"<color=#5BE38A>Bu ayın yemeği alındı</color>: {J.S(J.O(st, "satin"), "isim")}. Afiyet olsun!", label);
            return;
        }
        GUILayout.Label($"Her ay bir yemek alman gerekir; almazsan {Fmt.Mls(J.D(st, "ceza"))} ceza.", small);
        long rec = J.L(st, "onerilen_id");
        foreach (var x in J.A(st, "tum_yemekler"))
        {
            var o = x as Dictionary<string, object>;
            if (o == null) continue;
            long id = J.L(o, "id");
            string name = J.S(o, "isim");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{name}</b>{(id == rec ? " <color=#FFD54A>★ bu ayın önerisi</color>" : "")} · {Fmt.Mls(J.D(o, "fiyat"))}", label);
            GUILayout.FlexibleSpace();
            if (ActButton("Al", 32, 70))
                Act("/hayat/yemek/satin-al", new Dictionary<string, object> { { "yemek_id", id } }, $"{name} aldın. Afiyet olsun!", "yemek");
            GUILayout.EndHorizontal();
        }
    }

    void TabBus(bool atStop)
    {
        var m = Market.I;
        bool ticket = !m.Live;
        if (m.Live && Data("ulasim", out var u))
        {
            var s = J.O(u, "status");
            ticket = J.B(s, "bilet_odendi");
            Row("Aylık bilet", Ok(ticket, "alındı", $"alınmadı ({Fmt.Mls(J.D(s, "bilet_ucreti"))})"));
        }
        if (!atStop)
        {
            GUILayout.Label("Otobüse binmek için caddedeki bir durağa yürü (meydandan güneye). Aylık bileti durağın yanındaki büfeden alırsın.", small);
            foreach (var st in City.Stops) GUILayout.Label("• " + st.name, small);
            GUILayout.Space(6);
            if (GUILayout.Button("Eve dön", btn, GUILayout.Height(40))) { GoTo(Town.HomeSpawn, Town.HomeFacing); Show("Evindesin", 1.5f); }
            return;
        }
        if (!ticket)
        {
            GUILayout.Label("<color=#FF7A7A>Bu ay otobüs biletin yok.</color> Hemen yandaki büfeden alabilirsin.", label);
            return;
        }
        GUILayout.Label("Nereye gidiyorsun?", label);
        for (int i = 0; i < City.Stops.Length; i++)
        {
            if (i == placeArg) continue;
            int k = i;
            if (GUILayout.Button("🚌 " + City.Stops[i].name, btn, GUILayout.Height(44))) Ride(k);
        }
    }

    void Ride(int i)
    {
        GoTo(City.StopPoint(i), Quaternion.LookRotation(Vector3.back));
        fadeUntil = Time.time + 1.6f;
        Show("Otobüsle geldin: " + City.Stops[i].name, 2.5f);
        if (Bell.I) Bell.I.Tick();
    }

    void TabBills()
    {
        if (!NeedLive()) return;
        if (Data("ev", out var e))
        {
            var s = J.O(e, "status");
            Row("Konut", $"<b>{J.S(s, "ev_adi")}</b>");
            Row("Aylık ev gideri", Fmt.Mls(J.D(s, "gider_tutari")));
            if (J.D(s, "gider_ceza") > 0) Row("Gecikme cezası", Fmt.Mls(J.D(s, "gider_ceza")));
            if (J.B(s, "gider_odendi")) GUILayout.Label("<color=#5BE38A>Bu ayın ev faturası ödendi.</color>", label);
            else if (ActButton($"Ev faturasını öde ({Fmt.Mls(J.D(s, "gider_tutari"))})", 46))
                Act("/hayat/ev/gider", null, "Ev faturan ödendi. Teşekkür ederiz!", "ev");
        }
        Title("Varlıkların");
        Row("Nakit", Fmt.Mls(Market.I.cash));
        Row("Hisseler", Fmt.Mls(Market.I.stockValue));
        if (Data("varlik", out var v))
        {
            Row("Sahip olduğun evler", Fmt.N(J.A(v, "evler").Count));
            Row("Sahip olduğun arabalar", Fmt.N(J.A(v, "araclar").Count));
        }
    }

    // ================================================================ genel ağaç görünümü (sıralama, görevler)

    void TabTree(string key, string payPath)
    {
        if (!NeedLive()) return;
        if (!Data(key, out var d)) return;
        if (payPath != null && ActButton("Tamamlanan görevlerin ödülünü al", 38))
            Act(payPath, null, "Ödüller hesabına geçti.", key);
        Tree(d, 0);
    }

    static readonly HashSet<string> TreeSkip = new HashSet<string> { "ok", "id", "logo_url", "resim_url", "foto_url", "user_id", "magaza_id" };

    void Tree(object o, int depth)
    {
        if (depth > 3) return;
        if (o is Dictionary<string, object> d)
        {
            foreach (var kv in d)
            {
                if (TreeSkip.Contains(kv.Key) || kv.Value == null) continue;
                if (kv.Value is Dictionary<string, object> sub) { if (sub.Count > 0) { Title(Pretty(kv.Key)); Tree(sub, depth + 1); } }
                else if (kv.Value is List<object> l) { if (l.Count > 0) { GUILayout.Label($"<b>{Pretty(kv.Key)}</b>", label); Tree(l, depth + 1); } }
                else Row(Pretty(kv.Key), Val(kv.Value));
            }
        }
        else if (o is List<object> list)
        {
            for (int i = 0; i < list.Count && i < 30; i++)
            {
                if (list[i] is Dictionary<string, object> di) GUILayout.Label("• " + Line(di), small);
                else GUILayout.Label("• " + Val(list[i]), small);
            }
        }
    }

    static string Line(Dictionary<string, object> d)
    {
        var parts = d.Where(kv => !TreeSkip.Contains(kv.Key) && (kv.Value is string || kv.Value is double || kv.Value is bool))
            .Take(6)
            .Select(kv => kv.Value is string s ? s : Pretty(kv.Key) + " " + Val(kv.Value));
        return string.Join(" · ", parts);
    }

    // ================================================================ anne & baba

    void OpenDialog(int who)
    {
        OpenPanelCommon(Panel.Dialog);
        dialogWho = who;
        dialogMsg = null;
    }

    void DrawDialog()
    {
        var m = Market.I;
        float w = Mathf.Min(560, W - 16), h = Mathf.Min(300, H - 16);
        var r = new Rect((W - w) / 2, H - h - (HtmlInput.Touch ? 8 : 40), w, h);
        GUI.Box(r, GUIContent.none, box);
        GUILayout.BeginArea(new Rect(r.x + 18, r.y + 12, w - 36, h - 22));
        bool father = dialogWho == 0;
        GUILayout.Label(father ? "<b>Baban</b>" : "<b>Annen</b>", h2);
        string mes = J.S(J.O(m.dash, "is"), "aktif_meslek");
        bool give = father && (!m.Live || (allowanceKnown && !allowanceTaken));
        string text;
        if (father)
        {
            if (give)
                text = "Günaydın evladım! Artık büyüdün, kendi ayaklarının üzerinde durma zamanı. Al şu 10.000 MLS'yi; bu benden son harçlık!\n\n" +
                       "Şehre in, caddenin batısındaki İş Bulma Merkezi'ne git ve kendine bir iş bul. Otobüs durakları caddede; aylık bileti duraktaki büfeden alırsın.";
            else if (mes == "")
                text = "Hâlâ iş bulamadın mı? Oyalanma! İş Bulma Merkezi caddenin batısında, \"Evler\" durağının karşısında. Önce bir işe gir, sonra borsaya bakarsın.";
            else
                text = $"Aferin evladım! {mes} olarak çalıştığını duydum, gurur duyuyorum. Paranı iyi değerlendir: borsa binası meydanın kuzeyinde.";
        }
        else
        {
            var y = J.O(m.dash, "yemek");
            var ev = J.O(m.dash, "ev");
            text = "Günaydın canım, kahvaltın masada! Bu ayın yemeğini durak büfesinden almayı unutma; almazsan ay sonunda ceza yersin. " +
                   "Ev faturasını da MTBS Bankası'nın veznesinde ödüyoruz.";
            if (m.Live && ev.Count > 0)
                text += $"\n\nYemek: {(J.B(y, "bu_ay_alindi") ? "alındı ✓" : "alınmadı")} · Ev faturası: {(J.B(ev, "gider_odendi") ? "ödendi ✓" : "ödenmedi")}";
        }
        GUILayout.Label(text, label);
        GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(dialogMsg)) GUILayout.Label($"<color={(dialogOk ? "#9FE0B0" : "#FF7A7A")}>{dialogMsg}</color>", small);
        GUILayout.BeginHorizontal();
        if (give)
        {
            GUI.enabled = !worldBusy;
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.5f);
            if (GUILayout.Button("Sağ ol baba! (10.000 MLS)", btn, GUILayout.Height(44))) TakeAllowance();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }
        if (GUILayout.Button(give ? "Sonra" : "Tamam", GUILayout.Height(44), GUILayout.Width(110))) ClosePanel();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void TakeAllowance()
    {
        var m = Market.I;
        if (!m.Live)
        {
            m.DemoGift(10000);
            allowanceTaken = true;
            dialogMsg = "Demo: 10.000 MLS sanal harçlık eklendi.";
            dialogOk = true;
            Show("+10.000 MLS", 2f);
            if (Bell.I) Bell.I.Ring();
            ClosePanel();
            Hint("Babanın dediği gibi: şehre in, İş Bulma Merkezi'ne git.", 8f);
            return;
        }
        worldBusy = true;
        m.Post("/dunya/baba-harclik", null, (r, err) =>
        {
            worldBusy = false;
            if (err != null) { dialogMsg = err; dialogOk = false; return; }
            allowanceTaken = true;
            bool given = J.B(r, "verildi");
            m.RefreshStatus();
            if (given)
            {
                Show("+10.000 MLS · babandan harçlık", 2.5f);
                if (Bell.I) Bell.I.Ring();
            }
            else Show("Harçlığını daha önce almıştın", 2f);
            ClosePanel();
            Hint("Babanın dediği gibi: şehre in, İş Bulma Merkezi'ne git (meydandan güneye, cadde).", 9f);
        });
    }

    void DrawFade()
    {
        if (Time.time >= fadeUntil) return;
        float a = Mathf.Clamp01((fadeUntil - Time.time) / 1.2f);
        var c = GUI.color;
        GUI.color = new Color(0, 0, 0, a);
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
        GUI.color = c;
    }
}
