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

    string CurrentTab => placeTab ?? PhoneTabs[Mathf.Clamp(phoneTab, 0, PhoneTabs.Length - 1)];

    // ================================================================ giriş / dünya verisi

    void WakeUp()
    {
        fadeUntil = Time.time + 2.5f;
        fatherAutoShown = false;
        Show(T("Günaydın! Yatağında uyandın."), 3.5f);
        Hint(T("Odandan çık, salona geç: annen ve baban seni bekliyor."), 9f);
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
                if (i >= list.Count) { lbl.text = T("— boş —"); continue; }
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
                Town.LevelSign.text = T("EV SEVİYESİ {0}\n<size=60>{1}</size>", Fmt.N(J.D(ev, "seviye")), Cut(J.S(ev, "ev_adi"), 24));
        }
        if (m.Tm)
        {
            // Trade of MTBS: salon kapısına yaklaşınca Müteşebbis yönlendirmesi
            if (panel == Panel.None && Time.time > gateAgain && player.transform.position.z < -SalonBuilder.HalfD + 2.6f && Mathf.Abs(player.transform.position.x) < SeljukBuilding.DoorW / 2f + 0.5f)
                OpenGate();
            return;
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
            case Interactable.Kind.Teller:
                if (Market.I.Tm) OpenCredit();
                else OpenPlace("Fatura", it.arg == 1 ? T("MTBS Bankası · Salon veznesi") : T("MTBS Bankası · Vezne"), 0);
                return true;
            case Interactable.Kind.Door: OpenGate(); return true;
            case Interactable.Kind.Podium: OpenPlace("Kürsü", T("Borsa Yönetimi · Duyurular"), 0); return true;
            case Interactable.Kind.Duel:
                if (Market.I.Tm) OpenPlace("Düello", T("Düello Salonu"), 0);
                else Hint(T("Düello Trade of MTBS hesabıyla oynanır. Menü → Profil → çıkış yapıp Trade of MTBS ile gir."), 5f);
                return true;
            case Interactable.Kind.BusStop: OpenPlace("Otobüs", T("Durak · ") + City.Stops[it.arg].name, it.arg); return true;
            case Interactable.Kind.Buffet: OpenPlace("Büfe", T("Büfe · ") + City.Stops[it.arg].name, it.arg); return true;
            case Interactable.Kind.JobCenter: OpenPlace("İş", T("İş Bulma Merkezi"), 0); return true;
            case Interactable.Kind.Estate: OpenPlace("Emlak", T("Emlak Ofisi"), 0); return true;
            case Interactable.Kind.CarDealer: OpenPlace("Galeri", T("Araba Galerisi"), 0); return true;
            case Interactable.Kind.Auction: OpenPlace("Müzayede", T("Müzayede Salonu & Galeri"), 0); return true;
            case Interactable.Kind.Shop:
                shopId = City.ShopIds[it.arg];
                OpenPlace("Dükkân", string.IsNullOrEmpty(City.ShopNames[it.arg]) ? T("Mağaza") : City.ShopNames[it.arg], it.arg);
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
        if (tab == "Kürsü") return; // kürsü kendi verisini (duyurular) tutar
        if (tab == "Masa") { LoadTabName(DeskTabs[deskTab]); return; }
        var m = Market.I;
        if (m.Tm && LoadTm(tab)) return;
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

    /// <summary>Kürsü: borsa yönetiminin son duyuruları (halka arz, artırım, bölünme, temettü, fabrika).</summary>
    void TabPodium()
    {
        GUILayout.Label(T("Borsa yönetimi önemli gelişmeleri gong çalarak duyurur. Yeni halka arzlar, sermaye artırımları, bölünmeler ve temettüler burada."), small);
        Title(T("Son duyurular"));
        var p = Podium.I;
        if (p == null || p.History.Count == 0) { GUILayout.Label(T("Henüz duyuru yok; birazdan gelir."), small); return; }
        for (int i = 0; i < p.History.Count && i < 30; i++)
        {
            GUILayout.Label((i == 0 ? "<color=#FFD54A>" : "<color=#C9D1E0>") + p.History[i] + "</color>", label);
            GUILayout.Space(2);
        }
    }

    void DrawTabName(string tab)
    {
        if (tab == "Kürsü") { TabPodium(); return; }
        if (tab == "Masa") { DrawDesk(); return; }
        if (Market.I.Tm && DrawTm(tab)) return;
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
        if (d.Count == 0) { GUILayout.Label(T("yükleniyor…"), small); return; }
        var tm = J.O(d, "time");
        Row(T("Oyun zamanı"), T("{0}. ay · {1}. sezon", Fmt.N(J.D(tm, "ay")), Fmt.N(J.D(tm, "sezon"))));
        Row(T("Nakit"), Fmt.Mls(Market.I.cash));
        var job = J.O(d, "is");
        string mes = J.S(job, "aktif_meslek");
        Row(T("İş"), mes != "" ? $"<b>{mes}</b> · {Fmt.Mls(J.D(job, "maas"))}" : T("<color=#FF7A7A>İşsiz</color> → İş Bulma Merkezi"));
        var ev = J.O(d, "ev");
        Row(T("Konut"), J.S(ev, "ev_adi"));
        Row(T("Ev faturası"), Ok(J.B(ev, "gider_odendi"), T("bu ay ödendi"), T("ödenmedi ({0}) → MTBS Bankası", Fmt.Mls(J.D(ev, "gider_tutari")))));
        var ul = J.O(d, "ulasim");
        if (J.S(ul, "arac_adi") != "") Row(T("Ulaşım"), J.S(ul, "arac_adi"));
        Row(T("Otobüs bileti"), Ok(J.B(ul, "bilet_odendi"), T("bu ay alındı"), T("alınmadı ({0}) → durak büfesi", Fmt.Mls(J.D(ul, "bilet_ucreti")))));
        var y = J.O(d, "yemek");
        Row(T("Bu ayın yemeği"), Ok(J.B(y, "bu_ay_alindi"), T("alındı"), T("alınmadı → durak büfesi")));
        var st = J.O(d, "status");
        if (st.Count > 0) { Title(T("Sıradaki iş")); Dump(st); }
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
            default: return T("Uygun değil");
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
                Row(T("Mesleğin"), $"<b>{mes}</b>");
                Row(T("Maaş"), Fmt.Mls(J.D(job, "maas")));
                if (J.D(job, "bekleyen_maas") > 0) Row(T("Bekleyen maaş"), Fmt.Mls(J.D(job, "bekleyen_maas")));
                GUILayout.Label(T("Maaşını Müteşebbis uygulamasından (kısa bir reklam izleyerek) çekersin."), small);
                if (J.B(job, "ayrilabilir") && ActButton(T("İşten ayrıl"), 34))
                    Act("/kariyer/ayril", null, T("İşten ayrıldın."), "kariyer", "ilanlar");
            }
            else GUILayout.Label(T("Şu an bir işin yok. Aşağıdaki ilanlardan sana uygun olana başvur."), label);
        }
        Title(T("İş ilanları"));
        if (!Data("ilanlar", out var il)) return;
        var list = J.A(il, "ilanlar");
        if (list.Count == 0) { GUILayout.Label(T("Şu an ilan yok."), small); return; }
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
                if (ActButton(T("Başvur"), 34, 90))
                    Act("/kariyer/basvur", new Dictionary<string, object> { { "ilan_id", id } }, T("Tebrikler, işe girdin: {0}!", title), "kariyer", "ilanlar");
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
            if (GUILayout.Button(T("← Mağazalar"), GUILayout.Height(32))) { shopId = 0; return; }
            DrawStore(shopId, true);
            return;
        }
        GUILayout.Label(T("Markaların mağazaları (çarşıdaki dükkânlar da bunlardan):"), small);
        if (!Data("pazar", out var d)) return;
        foreach (var o in J.A(d, "magazalar"))
        {
            long id = J.L(o, "id");
            if (GUILayout.Button(T("<b>{0}</b>  <size=12>{1} ürün</size>", J.S(o, "isim"), Fmt.N(J.D(o, "urun_sayisi"))),
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
        if (id <= 0) { GUILayout.Label(T("Bu dükkân henüz boş (mağaza bilgisi yükleniyor ya da yok)."), label); return; }
        if (!Data("magaza" + id, out var d)) return;
        var mg = J.O(d, "magaza");
        if (fromPhone) GUILayout.Label($"<b>{J.S(mg, "isim")}</b>", h2);
        var items = J.A(d, "urunler");
        if (items.Count == 0) { GUILayout.Label(T("Bu mağazada satışta ürün yok."), label); return; }
        GUILayout.Label(T("Ayda alabileceğin ürün sayısı ve kategori sırasını oyun kuralları belirler; sunucu uygun değilse söyler."), small);
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
            GUILayout.Label(T("<b>{0}</b>\n<size=12>{1} · stok {2}</size>", name, Pretty(J.S(o, "kategori")), Fmt.N(J.D(o, "stok"))), label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(disc ? $"<color=#5BE38A><b>{Fmt.Mls(px)}</b></color>\n<size=11><color=#7d879c>{Fmt.Mls(J.D(o, "fiyat"))}</color></size>" : $"<b>{Fmt.Mls(px)}</b>", label, GUILayout.Width(110));
            GUI.enabled = J.D(o, "stok") > 0;
            if (ActButton(T("Satın al"), 34, 90))
                Act("/urun/" + uid + "/satin-al", null, T("Satın aldın: {0}", name), "magaza" + id);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    // ================================================================ Emlak ofisi / araba galerisi

    void TabEstate()
    {
        if (!NeedLive()) return;
        Title(T("Kiralık konut (ev seviyen)"));
        if (Data("ev", out var e))
        {
            var s = J.O(e, "status");
            Row(T("Oturduğun yer"), T("<b>{0}</b> · seviye {1}/{2}", J.S(s, "ev_adi"), Fmt.N(J.D(s, "seviye")), Fmt.N(J.D(s, "max_seviye"))));
            Row(T("Aylık kira/gider"), Fmt.Mls(J.D(s, "gider_tutari")));
            if (J.B(s, "yukseltme_devam_ediyor")) Row(T("Taşınma"), T("sürüyor · {0} ay kaldı", Fmt.N(J.D(s, "kalan_ay"))));
            else if (J.S(s, "sonraki_adi") != "")
            {
                if (J.B(s, "yukseltilebilir"))
                {
                    if (ActButton(T("\"{0}\" konutuna taşın", J.S(s, "sonraki_adi")), 38))
                        Act("/hayat/ev/yukselt", null, T("Taşınma başladı."), "ev");
                }
                else GUILayout.Label(T("Sıradaki konut: {0} (şartları henüz sağlamıyorsun).", J.S(s, "sonraki_adi")), small);
            }
        }
        Title(T("Satılık evler"));
        Listings("evp", "/hayat/ev-pazari", T("Ev"));
    }

    void TabCars()
    {
        if (!NeedLive()) return;
        Title(T("Ulaşım seviyen"));
        if (Data("ulasim", out var u))
        {
            var s = J.O(u, "status");
            Row(T("Aracın"), T("<b>{0}</b> · seviye {1}", (J.S(s, "arac_adi") != "" ? J.S(s, "arac_adi") : T("Toplu taşıma")), Fmt.N(J.D(s, "seviye"))));
            if (J.B(s, "yukseltme_devam_ediyor")) Row(T("Yükseltme"), T("sürüyor · {0} ay kaldı", Fmt.N(J.D(s, "kalan_ay"))));
            else if (J.S(s, "sonraki_adi") != "" && J.B(s, "yukseltilebilir") && ActButton(T("\"{0}\" seviyesine geç", J.S(s, "sonraki_adi")), 38))
                Act("/hayat/ulasim/yukselt", null, T("Ulaşım yükseltmesi başladı."), "ulasim");
        }
        Title(T("Satılık arabalar"));
        Listings("arp", "/hayat/arac-pazari", T("Araba"));
    }

    /// <summary>Ev / araba pazarı: ilanlar (al), sahip oldukların (sat / satıştan çek).</summary>
    void Listings(string key, string path, string what)
    {
        if (!Data(key, out var d)) return;
        var owned = J.A(d, "sahip");
        if (owned.Count > 0)
        {
            GUILayout.Label(T("<b>Senin {0}ların</b>", what.ToLower()), label);
            foreach (var x in owned)
            {
                var o = x as Dictionary<string, object>;
                if (o == null) continue;
                OwnedRow(o, key, path);
            }
            GUILayout.Space(6);
        }
        var list = J.A(d, "ilanlar");
        if (list.Count == 0) { GUILayout.Label(T("Şu an ilan yok."), small); return; }
        foreach (var x in list)
        {
            var o = x as Dictionary<string, object>;
            if (o == null) continue;
            if (J.B(o, "kendi_evi")) continue;
            long id = J.L(o, "id");
            string name = J.S(o, "isim");
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            string seller = J.S(o, "satici_adsoyad") != "" ? T(" · satıcı ") + J.S(o, "satici_adsoyad") : "";
            GUILayout.Label(T("<b>{0}</b> · {1}\n<size=12>min. seviye {2}{3}</size>", name, Fmt.Mls(J.D(o, "fiyat")), Fmt.N(J.D(o, "min_seviye")), seller), label);
            GUILayout.FlexibleSpace();
            if (J.B(o, "satin_alinabilir"))
            {
                if (ActButton(T("Satın al"), 34, 90))
                    Act(path + "/al", new Dictionary<string, object> { { "id", id } }, T("{0} artık senin!", name), key);
            }
            else GUILayout.Label(T("<color=#FFB74D><size=12>şartlar uymuyor</size></color>"), small, GUILayout.Width(110));
            GUILayout.EndHorizontal();
        }
    }

    void OwnedRow(Dictionary<string, object> o, string key, string path)
    {
        long id = J.L(o, "id");
        string name = J.S(o, "isim");
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("<b>{0}</b> · değer {1}{2}", name, Fmt.Mls(J.D(o, "fiyat")), (J.B(o, "satista_mi") ? T(" · <color=#FFD54A>satışta</color>") : "")), label);
        GUILayout.FlexibleSpace();
        if (J.B(o, "satista_mi"))
        {
            if (ActButton(T("Satıştan çek"), 32, 110)) Act(path + "/cek", new Dictionary<string, object> { { "id", id } }, T("İlan kaldırıldı."), key);
        }
        else
        {
            sellText = HtmlInput.Field("satis" + id, sellText, T("Fiyat"), field, false, true, 12, GUILayout.Width(110), GUILayout.Height(32));
            if (ActButton(T("Sat"), 32, 60))
            {
                long px = (long)ParsePrice(sellText);
                if (px <= 0) { phoneMsg = T("Satış fiyatı yaz."); phoneOk = false; }
                else Act(path + "/sat", new Dictionary<string, object> { { "id", id }, { "fiyat", px } }, T("Satışa çıkarıldı."), key);
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
            if (GUILayout.Button(T("← Bütün eserler"), GUILayout.Height(32))) { auctionSel = 0; return; }
            DrawLot(auctionSel);
            return;
        }
        if (!Data("muz", out var d)) return;
        var list = J.A(d, "aktif");
        GUILayout.Label(T("Açık artırmadaki eserler. Bir esere dokun, teklif ver."), small);
        if (list.Count == 0) GUILayout.Label(T("Şu an açık artırma yok."), label);
        foreach (var x in list)
        {
            var o = x as Dictionary<string, object>;
            if (o == null) continue;
            long id = J.L(o, "id");
            if (GUILayout.Button(T("<b>{0}</b> · {1}\n<size=12>güncel {2} · bitiş {3}</size>", J.S(o, "urun_adi"), J.S(o, "sanatci"), Fmt.Mls(J.D(o, "guncel_fiyat")), ShortDate(J.S(o, "bitis_tarihi"))),
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
            Title(T("Biten müzayedeler"));
            for (int i = 0; i < ended.Count && i < 10; i++)
                Row(J.S(ended[i], "urun_adi"), Fmt.Mls(J.D(ended[i], "guncel_fiyat")));
        }
    }

    void DrawLot(long id)
    {
        if (!Data("muz" + id, out var d)) return;
        var u = J.O(d, "urun");
        GUILayout.Label($"<b>{J.S(u, "urun_adi")}</b>", h2);
        if (J.S(u, "sanatci") != "") Row(T("Sanatçı"), J.S(u, "sanatci"));
        if (J.S(u, "aciklama") != "") GUILayout.Label(J.S(u, "aciklama"), small);
        Row(T("Güncel fiyat"), $"<b>{Fmt.Mls(J.D(u, "guncel_fiyat"))}</b>");
        Row(T("Bitiş"), ShortDate(J.S(u, "bitis_tarihi")));
        GUILayout.BeginHorizontal();
        bidText = HtmlInput.Field("teklif", bidText, T("Teklifin (MLS)"), field, false, true, 12, GUILayout.Height(40), GUILayout.MinWidth(120));
        if (ActButton(T("Teklif ver"), 40, 110))
        {
            long b = (long)ParsePrice(bidText);
            if (b <= 0) { phoneMsg = T("Bir teklif yaz."); phoneOk = false; }
            else Act("/muzayede/" + id + "/teklif", new Dictionary<string, object> { { "teklif", b } }, T("Teklifin alındı. Biri geçerse haber verilir."), "muz", "muz" + id);
        }
        GUILayout.EndHorizontal();
        var bids = J.A(d, "teklifler");
        if (bids.Count > 0)
        {
            Title(T("Teklifler"));
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
        Title(T("Aylık otobüs bileti"));
        if (Data("ulasim", out var u))
        {
            var s = J.O(u, "status");
            if (J.B(s, "bilet_odendi")) GUILayout.Label(T("<color=#5BE38A>Bu ayın bileti alındı.</color> Otobüse binebilirsin."), label);
            else
            {
                GUILayout.Label(T("Bu ay biletini almadın. Almazsan ay sonunda {0} ceza kesilir.", Fmt.Mls(J.D(s, "bilet_ceza"))), small);
                if (ActButton(T("Bilet al ({0})", Fmt.Mls(J.D(s, "bilet_ucreti"))), 42))
                    Act("/hayat/ulasim/bilet", null, T("Biletin alındı, bu ay otobüse binebilirsin."), "ulasim");
            }
        }
        Title(T("Bu ayın yemeği"));
        if (!Data("yemek", out var y)) return;
        var st = J.O(y, "status");
        if (J.B(st, "bu_ay_alindi"))
        {
            GUILayout.Label(T("<color=#5BE38A>Bu ayın yemeği alındı</color>: {0}. Afiyet olsun!", J.S(J.O(st, "satin"), "isim")), label);
            return;
        }
        GUILayout.Label(T("Her ay bir yemek alman gerekir; almazsan {0} ceza.", Fmt.Mls(J.D(st, "ceza"))), small);
        long rec = J.L(st, "onerilen_id");
        foreach (var x in J.A(st, "tum_yemekler"))
        {
            var o = x as Dictionary<string, object>;
            if (o == null) continue;
            long id = J.L(o, "id");
            string name = J.S(o, "isim");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{name}</b>{(id == rec ? " <color=#FFD54A>* bu ayın önerisi</color>" : "")} · {Fmt.Mls(J.D(o, "fiyat"))}", label);
            GUILayout.FlexibleSpace();
            if (ActButton(T("Al"), 32, 70))
                Act("/hayat/yemek/satin-al", new Dictionary<string, object> { { "yemek_id", id } }, T("{0} aldın. Afiyet olsun!", name), "yemek");
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
            Row(T("Aylık bilet"), Ok(ticket, T("alındı"), T("alınmadı ({0})", Fmt.Mls(J.D(s, "bilet_ucreti")))));
        }
        if (!atStop)
        {
            GUILayout.Label(T("Otobüse binmek için caddedeki bir durağa yürü (meydandan güneye). Aylık bileti durağın yanındaki büfeden alırsın."), small);
            foreach (var st in City.Stops) GUILayout.Label("• " + st.name, small);
            GUILayout.Space(6);
            if (GUILayout.Button(T("Eve dön"), btn, GUILayout.Height(40))) { GoTo(Town.HomeSpawn, Town.HomeFacing); Show(T("Evindesin"), 1.5f); }
            return;
        }
        if (!ticket)
        {
            GUILayout.Label(T("<color=#FF7A7A>Bu ay otobüs biletin yok.</color> Hemen yandaki büfeden alabilirsin."), label);
            return;
        }
        GUILayout.Label(T("Nereye gidiyorsun?"), label);
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
        Show(T("Otobüsle geldin: ") + City.Stops[i].name, 2.5f);
        if (Bell.I) Bell.I.Tick();
    }

    void TabBills()
    {
        if (!NeedLive()) return;
        if (Data("ev", out var e))
        {
            var s = J.O(e, "status");
            Row(T("Konut"), $"<b>{J.S(s, "ev_adi")}</b>");
            Row(T("Aylık ev gideri"), Fmt.Mls(J.D(s, "gider_tutari")));
            if (J.D(s, "gider_ceza") > 0) Row(T("Gecikme cezası"), Fmt.Mls(J.D(s, "gider_ceza")));
            if (J.B(s, "gider_odendi")) GUILayout.Label(T("<color=#5BE38A>Bu ayın ev faturası ödendi.</color>"), label);
            else if (ActButton(T("Ev faturasını öde ({0})", Fmt.Mls(J.D(s, "gider_tutari"))), 46))
                Act("/hayat/ev/gider", null, T("Ev faturan ödendi. Teşekkür ederiz!"), "ev");
        }
        Title(T("Varlıkların"));
        Row(T("Nakit"), Fmt.Mls(Market.I.cash));
        Row(T("Hisseler"), Fmt.Mls(Market.I.stockValue));
        if (Data("varlik", out var v))
        {
            Row(T("Sahip olduğun evler"), Fmt.N(J.A(v, "evler").Count));
            Row(T("Sahip olduğun arabalar"), Fmt.N(J.A(v, "araclar").Count));
        }
    }

    // ================================================================ genel ağaç görünümü (sıralama, görevler)

    void TabTree(string key, string payPath)
    {
        if (!NeedLive()) return;
        if (!Data(key, out var d)) return;
        if (payPath != null && ActButton(T("Tamamlanan görevlerin ödülünü al"), 38))
            Act(payPath, null, T("Ödüller hesabına geçti."), key);
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
        PanelBox(r);
        GUILayout.BeginArea(new Rect(r.x + 18, r.y + 12, w - 36, h - 22));
        bool father = dialogWho == 0;
        GUILayout.Label(father ? T("<b>Baban</b>") : T("<b>Annen</b>"), h2);
        string mes = J.S(J.O(m.dash, "is"), "aktif_meslek");
        bool give = father && (!m.Live || (allowanceKnown && !allowanceTaken));
        string text;
        if (father)
        {
            if (give)
                text = T("Günaydın evladım! Artık büyüdün, kendi ayaklarının üzerinde durma zamanı. Al şu 10.000 MLS'yi; bu benden son harçlık!\n\n") +
                       "Şehre in, caddenin batısındaki İş Bulma Merkezi'ne git ve kendine bir iş bul. Otobüs durakları caddede; aylık bileti duraktaki büfeden alırsın.";
            else if (mes == "")
                text = T("Hâlâ iş bulamadın mı? Oyalanma! İş Bulma Merkezi caddenin batısında, \"Evler\" durağının karşısında. Önce bir işe gir, sonra borsaya bakarsın.");
            else
                text = T("Aferin evladım! {0} olarak çalıştığını duydum, gurur duyuyorum. Paranı iyi değerlendir: borsa binası meydanın kuzeyinde.", mes);
        }
        else
        {
            var y = J.O(m.dash, "yemek");
            var ev = J.O(m.dash, "ev");
            text = T("Günaydın canım, kahvaltın masada! Bu ayın yemeğini durak büfesinden almayı unutma; almazsan ay sonunda ceza yersin. ") +
                   T("Ev faturasını da MTBS Bankası'nın veznesinde ödüyoruz.");
            if (m.Live && ev.Count > 0)
                text += T("\n\nYemek: {0} · Ev faturası: {1}", (J.B(y, "bu_ay_alindi") ? T("alındı ✓") : T("alınmadı")), (J.B(ev, "gider_odendi") ? T("ödendi ✓") : T("ödenmedi")));
        }
        GUILayout.Label(text, label);
        GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(dialogMsg)) GUILayout.Label($"<color={(dialogOk ? "#9FE0B0" : "#FF7A7A")}>{dialogMsg}</color>", small);
        GUILayout.BeginHorizontal();
        if (give)
        {
            GUI.enabled = !worldBusy;
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.5f);
            if (GUILayout.Button(T("Sağ ol baba! (10.000 MLS)"), btn, GUILayout.Height(44))) TakeAllowance();
            GUI.backgroundColor = SalonDirector.BtnBase;
            GUI.enabled = true;
        }
        if (GUILayout.Button(give ? T("Sonra") : T("Tamam"), GUILayout.Height(44), GUILayout.Width(110))) ClosePanel();
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
            dialogMsg = T("Demo: 10.000 MLS sanal harçlık eklendi.");
            dialogOk = true;
            Show(T("+10.000 MLS"), 2f);
            if (Bell.I) Bell.I.Ring();
            ClosePanel();
            Hint(T("Babanın dediği gibi: şehre in, İş Bulma Merkezi'ne git."), 8f);
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
                Show(T("+10.000 MLS · babandan harçlık"), 2.5f);
                if (Bell.I) Bell.I.Ring();
            }
            else Show(T("Harçlığını daha önce almıştın"), 2f);
            ClosePanel();
            Hint(T("Babanın dediği gibi: şehre in, İş Bulma Merkezi'ne git (meydandan güneye, cadde)."), 9f);
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

    // ---------------------------------------------------------------- Çalışma masası (salonun arka sağ köşesi)

    static readonly string[] DeskTabs = { "Portföy", "Düello", "Sıralama" };
    int deskTab;

    void OpenDesk()
    {
        OpenPlace("Masa", T("Çalışma masası"), 0);
        Show(T("Kalkmak için paneli kapat"), 2f);
    }

    void DrawDesk()
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < DeskTabs.Length; i++)
            if (TabBtn(T(DeskTabs[i]), i == deskTab, 36))
            {
                deskTab = i;
                phoneMsg = null;
                scroll = Vector2.zero;
                LoadTabName(DeskTabs[i]);
            }
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        string tab = DeskTabs[deskTab];
        if (tab == "Düello" && !Market.I.Tm)
        {
            GUILayout.Label(T("Düello Trade of MTBS hesabıyla oynanır. Menü → Profil → çıkış yapıp Trade of MTBS ile gir."), label);
            return;
        }
        DrawTabName(tab);
    }
}
