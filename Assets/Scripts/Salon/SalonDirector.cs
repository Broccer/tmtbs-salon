using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyunun akışı ve arayüzü. Giriş → oyuncu evinde başlar → meydana çıkar → borsa binasına (seans salonu)
/// ya da MTBS Bankası'na girer. Bankoda al-sat (limit/piyasa emri, defter, grafik, künye, emir iptali),
/// Müteşebbis hesabıyla: evde uyan, babandan harçlık al, şehirde iş bul, otobüse bin, büfe, banka, emlak, galeri, müzayede, dükkânlar.
/// Telefonda dokunmatik kontroller, büyük yazılar, dokununca açılan yazı kutuları ve sürükleyerek kaydırma.
/// </summary>
public partial class SalonDirector : MonoBehaviour
{
    public Walker player;
    public ChaseCamera chase;
    public Boards boards;

    enum View { Login, Forgot, Register, Game }
    View view = View.Login;
    enum Panel { None, Trade, Phone, Dialog, Credit, Gate }
    Panel panel = Panel.None;

    const string EmailKey = "mutesebbis_dunya_email";

    string email = "", pass = "", code = "", newPass = "", regName = "";
    string formMsg;
    bool formErr, busy, codeSent;

    string banner;
    float bannerUntil;
    float scale = 1f;
    float W, H;
    GUIStyle h1, h2, label, small, box, btn, field, big, tab, tabOn, right;

    // ================================================================ yaşam döngüsü

    void PreviewPose()
    {
        player.StandUp();
        player.SetFrozen(true);
        player.transform.localScale = Vector3.one * 1.6f;
        player.Teleport(new Vector3(0f, 0, -6f), Quaternion.Euler(0, 180f, 0));
    }

    void Start()
    {
        InputReader.Blocked = true;
        PreviewPose();
        try { email = PlayerPrefs.GetString(EmailKey, ""); } catch { }
        try { loginAcc = PlayerPrefs.GetString("mutesebbis_dunya_hesap", "tm") == "mt" ? Market.Account.Mutesebbis : Market.Account.Tmtbs; } catch { }
        SocialLogin.Received += OnSocial;
        Market.I.Changed += OnMarket;
        Market.I.LoggedOut += OnLoggedOut;
        busy = true;
        formMsg = "Oturum kontrol ediliyor…";
        Market.I.TryResume(ok =>
        {
            busy = false;
            formMsg = null;
            if (ok) EnterGame();
            else if (SocialLogin.TakePending(out var sp, out var st, out var sn, out var se)) { loginAcc = Market.Account.Tmtbs; OnSocial(sp, st, sn, se); }
        });
    }

    void OnDestroy()
    {
        SocialLogin.Received -= OnSocial;
        if (Market.I == null) return;
        Market.I.Changed -= OnMarket;
        Market.I.LoggedOut -= OnLoggedOut;
    }

    void EnterGame()
    {
        view = View.Game;
        panel = Panel.None;
        chase.overview = false;
        InputReader.Blocked = false;
        player.SetName(Market.I.userName);
        player.transform.localScale = Vector3.one;
        player.SetFrozen(false);
        phoneTab = 0;
        bool tm = Market.I.Tm;
        loginAcc = Market.I.account;
        if (SalonBuilder.TmGate) SalonBuilder.TmGate.SetActive(tm);
        if (Bell.I) Bell.I.Ring();
        if (tm)
        {
            // Trade of MTBS hesabı: yalnız borsa salonu
            player.Teleport(new Vector3(0, 0, -SalonBuilder.HalfD + 6f), Quaternion.identity);
            chase.Snap();
            fadeUntil = Time.time + 1.5f;
            gateAgain = Time.time + 3f;
            Show("Trade of MTBS · Seans salonu", 3f);
            Hint("Bankolara yaklaş ve al-sat. Arkada MTBS Bankası veznesi ve düello salonu var.", 8f);
            return;
        }
        player.Teleport(Town.HomeSpawn, Town.HomeFacing);
        chase.Snap();
        WakeUp();
    }

    void OnLoggedOut()
    {
        view = View.Login;
        panel = Panel.None;
        chase.overview = true;
        chase.seated = false;
        InputReader.Blocked = true;
        PreviewPose();
        formMsg = switchToMt ? "Müteşebbis hesabınla giriş yap; şehir seni bekliyor. Hesabın yoksa \"Yeni hesap aç\"." : "Oturum kapandı. Tekrar giriş yapabilirsin.";
        if (switchToMt) loginAcc = Market.Account.Mutesebbis;
        switchToMt = false;
        formErr = false;
        foreach (var k in Kiosk.All) k.Bind(null);
        if (SalonBuilder.TmGate) SalonBuilder.TmGate.SetActive(false);
    }

    void OnMarket()
    {
        var bySlot = new Dictionary<int, Stock>();
        foreach (var s in Market.I.stocks) if (s.order >= 0) bySlot[s.order] = s;
        foreach (var k in Kiosk.All) k.Bind(bySlot.TryGetValue(k.slot, out var st) ? st : null);

        // Sert hareket: botlar o bankoya koşar (en fazla 2 hisse)
        var movers = new List<(Kiosk k, double move)>();
        foreach (var k in Kiosk.All)
        {
            var s = k.stock;
            if (s == null || s.prevPrice <= 0 || s.price == s.prevPrice) continue;
            double mv = s.price / s.prevPrice - 1.0;
            if (System.Math.Abs(mv) >= 0.015) movers.Add((k, mv));
        }
        movers.Sort((a, b) => System.Math.Abs(b.move).CompareTo(System.Math.Abs(a.move)));
        var bots = FindObjectsByType<BotTrader>(FindObjectsSortMode.None);
        for (int i = 0; i < movers.Count && i < 2; i++)
        {
            int n = Random.Range(2, 5);
            for (int j = 0; j < n && bots.Length > 0; j++)
                bots[Random.Range(0, bots.Length)].Rush(movers[i].k, movers[i].move > 0);
        }
    }

    void Update()
    {
        if (view != View.Game) return;
        WorldUpdate();
        chase.seated = player.SittingOn != null;
        var near = player.Nearest;
        TouchControls.InteractLabel = near != null ? near.buttonLabel : "İŞLEM";

        if (panel != Panel.None)
        {
            if (InputReader.BackPressed() && !HtmlInput.Open_) ClosePanel();
            return;
        }
        if (InputReader.MenuPressed()) { OpenPhone(false); return; }
        if (InputReader.InteractPressed() && near != null) Use(near);

        if (InputReader.ClickPressed(out Vector2 sp) && GUIUtility.hotControl == 0 && Camera.main != null)
        {
            var ray = Camera.main.ScreenPointToRay(sp);
            if (Physics.Raycast(ray, out RaycastHit hit, 60f))
            {
                var k = hit.collider.GetComponentInParent<Kiosk>();
                if (k != null && k.stock != null)
                {
                    float d = (k.StandPoint() - player.transform.position).Flat().magnitude;
                    if (d < 7f) OpenTrade(k.stock);
                    else Show("Önce bankoya yaklaş", 1.2f);
                }
            }
        }
    }

    void Use(Interactable it)
    {
        if (UseWorld(it)) return;
        switch (it.kind)
        {
            case Interactable.Kind.Counter: if (it.counter != null && it.counter.stock != null) OpenTrade(it.counter.stock); break;
            case Interactable.Kind.Computer: OpenPhone(true); break;
            case Interactable.Kind.Seat:
                player.Sit(it.seat);
                Hint("Panoları izliyorsun. Kalkmak için yürü.", 4f);
                break;
        }
    }

    void OpenPanelCommon(Panel p)
    {
        panel = p;
        InputReader.Blocked = true;
        scroll = Vector2.zero;
        if (Bell.I) Bell.I.Tick();
    }

    void ClosePanel()
    {
        panel = Panel.None;
        placeTab = null;
        trade = null;
        InputReader.Blocked = false;
        GUIUtility.keyboardControl = 0;
    }

    /// <summary>Oyuncuyu bir yere ışınla (oturuyorsa kaldır).</summary>
    void GoTo(Vector3 p, Quaternion r)
    {
        player.StandUp();
        player.Teleport(p, r);
        chase.Snap();
        ClosePanel();
    }

    void GoToCounter(Stock s)
    {
        foreach (var k in Kiosk.All)
            if (k.stock == s) { GoTo(k.StandPoint(0), k.FacingCounter); Show(s.name, 1.5f); return; }
        Show("Bu markanın bankosu yok", 1.5f);
    }

    void Show(string text, float seconds) { banner = text; bannerUntil = Time.time + seconds; }

    string hint;
    float hintUntil;
    void Hint(string text, float seconds) { hint = text; hintUntil = Time.time + seconds; }

    string Place()
    {
        Vector3 p = player.transform.position;
        if (p.z > -SalonBuilder.HalfD) return "Seans salonu";
        if (p.z > SeljukBuilding.ZF) return "Borsa MTBS · giriş";
        if (Mathf.Abs(p.x - Town.HouseX) < 11.2f && Mathf.Abs(p.z - Town.Z) < 6.2f) return p.x < Town.PartitionX ? "Odan" : "Evin · salon";
        if (p.z < City.AvenueZ + 9f) return "Şehir · cadde";
        if (Mathf.Abs(p.x - Town.BankX) < 10.2f && Mathf.Abs(p.z - Town.Z) < 8.2f) return "MTBS Bankası";
        return "Meydan";
    }

    // ================================================================ arayüz temeli

    void Styles()
    {
        if (h1 != null) return;
        h1 = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        h1.normal.textColor = new Color(1f, 0.82f, 0.35f);
        h2 = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true, wordWrap = true };
        h2.normal.textColor = Color.white;
        label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = true };
        label.normal.textColor = new Color(0.9f, 0.92f, 0.96f);
        right = new GUIStyle(label) { alignment = TextAnchor.UpperRight, wordWrap = false };
        small = new GUIStyle(label) { fontSize = 13 };
        small.normal.textColor = new Color(0.7f, 0.74f, 0.82f);
        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.13f, 0.93f));
        bg.Apply();
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 10, 10) };
        box.normal.background = bg;
        btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true };
        field = new GUIStyle(GUI.skin.textField) { fontSize = 17, padding = new RectOffset(8, 8, 6, 6) };
        big = new GUIStyle(h1) { fontSize = 40 };
        big.normal.textColor = Color.white;
        tab = new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true };
        tabOn = new GUIStyle(tab) { fontStyle = FontStyle.Bold };
        tabOn.normal.textColor = tabOn.hover.textColor = new Color(1f, 0.84f, 0.3f);
    }

    void OnGUI()
    {
        Styles();
        float baseSize = HtmlInput.Touch ? 430f : 720f;
        scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / baseSize, 0.7f, 4f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        W = Screen.width / scale;
        H = Screen.height / scale;

        if (view == View.Login) DrawLogin();
        else if (view == View.Forgot) DrawForgot();
        else if (view == View.Register) DrawRegister();
        else DrawGame();

        if (!string.IsNullOrEmpty(banner) && Time.time < bannerUntil)
            GUI.Label(new Rect(0, H * 0.2f, W, 60), banner, big);
    }

    void Dim(float a)
    {
        Color old = GUI.color;
        GUI.color = new Color(0, 0, 0, a);
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
        GUI.color = old;
    }

    Vector2 scroll;

    /// <summary>Panel çerçevesi + kaydırma alanı (dokunmatikte sürükleyerek kaydırılır). Dönüş: iç genişlik.</summary>
    float BeginPanel(Rect r)
    {
        GUI.Box(r, GUIContent.none, box);
        var e = Event.current;
        if (e.type == EventType.MouseDrag && r.Contains(e.mousePosition) && HtmlInput.Touch)
        {
            scroll.y -= e.delta.y;
            if (scroll.y < 0) scroll.y = 0;
            e.Use();
        }
        GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 18));
        scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
        return r.width - 24 - 18;
    }

    void EndPanel()
    {
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    Rect PanelRect(float maxW)
    {
        float w = Mathf.Min(maxW, W - 16);
        float top = W < 700 ? 8 : 70;
        float h = H - top - (W < 700 ? 8 : 16);
        float x = W < 700 ? (W - w) / 2 : W - w - 12;
        return new Rect(x, top, w, h);
    }

    GUIStyle smallNw;

    void Row(string k, string v)
    {
        if (smallNw == null) smallNw = new GUIStyle(small) { wordWrap = false };
        GUILayout.BeginHorizontal();
        GUILayout.Label(k, smallNw);
        GUILayout.FlexibleSpace();
        GUILayout.Label(v, right);
        GUILayout.EndHorizontal();
    }

    void Title(string t)
    {
        GUILayout.Space(8);
        GUILayout.Label("<color=#FFD54A><b>" + t + "</b></color>", label);
    }

    void Msg(string m, bool ok)
    {
        if (string.IsNullOrEmpty(m)) return;
        GUILayout.Label($"<color={(ok ? "#5BE38A" : "#FF7A7A")}>{m}</color>", label);
    }

    static readonly Dictionary<string, string> KeyNames = new Dictionary<string, string>
    {
        { "pd_dd", "PD/DD" }, { "kurulus", "Kuruluş" }, { "halka_arz", "Halka arz" }, { "kurucu", "Kurucu" },
        { "sermaye", "Sermaye" }, { "kasa", "Kasa" }, { "hissedar", "Hissedar" }, { "defter_degeri", "Defter değeri" },
        { "piyasa_degeri", "Piyasa değeri" }, { "degisim", "Değişim" }, { "hacim", "Hacim" }, { "son_fiyat", "Son fiyat" },
        { "adet", "Adet" }, { "fiyat", "Fiyat" }, { "tutar", "Tutar" }, { "tarih", "Tarih" }, { "oran", "Oran" },
        { "seviye", "Seviye" }, { "uretim", "Üretim" }, { "kapasite", "Kapasite" }, { "isim", "Ad" }, { "ad", "Ad" },
        { "net", "Net değer" }, { "net_deger", "Net değer" }, { "kazanc", "Kazanç" }, { "sira", "Sıra" },
        { "para", "Nakit" }, { "hisse_degeri", "Hisse değeri" }, { "kademe", "Kademe" }, { "uyelik", "Üyelik" },
        { "katilim", "Katılım" }, { "kayit", "Kayıt" }, { "toplam_adet", "Toplam adet" }, { "kalan_ay", "Kalan ay" },
    };

    static string Pretty(string key)
    {
        if (KeyNames.TryGetValue(key, out var n)) return n;
        string s = key.Replace('_', ' ');
        return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;
    }

    static string Val(object v)
    {
        switch (v)
        {
            case null: return "—";
            case bool b: return b ? "evet" : "hayır";
            case double d: return System.Math.Abs(d - System.Math.Round(d)) < 1e-9 ? Fmt.N(d) : Fmt.N2(d);
            case string s: return s.Length > 70 ? s.Substring(0, 69) + "…" : s;
            case List<object> l: return l.Count + " kayıt";
            default: return "…";
        }
    }

    /// <summary>Sözlüğün düz alanlarını satır satır yazar (atlananlar hariç).</summary>
    void Dump(Dictionary<string, object> d, params string[] skip)
    {
        var sk = new HashSet<string>(skip) { "id", "magaza_id", "logo_url", "ok", "user_id" };
        foreach (var kv in d)
        {
            if (sk.Contains(kv.Key) || kv.Value is Dictionary<string, object>) continue;
            if (kv.Value is List<object> l && l.Count > 0 && l[0] is Dictionary<string, object>) continue;
            Row(Pretty(kv.Key), Val(kv.Value));
        }
    }

    // ================================================================ giriş

    void FormMessage()
    {
        if (string.IsNullOrEmpty(formMsg)) return;
        GUILayout.Label($"<color={(formErr ? "#FF7A7A" : "#9FE0B0")}>{formMsg}</color>", small);
    }

    Look look;
    bool lookLoaded;

    void Pick(string title, string value, ref int idx, int count, ref bool changed)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(title, small, GUILayout.Width(70));
        if (GUILayout.Button("‹", GUILayout.Width(34), GUILayout.Height(30))) { idx = (idx + count - 1) % count; changed = true; }
        GUILayout.Label("<b>" + value + "</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = false }, GUILayout.Width(110));
        if (GUILayout.Button("›", GUILayout.Width(34), GUILayout.Height(30))) { idx = (idx + 1) % count; changed = true; }
        GUILayout.EndHorizontal();
    }

    /// <summary>Borsacını seç. Mixamo modelleri varsa model seçimi, yoksa ilkel gövde seçenekleri.</summary>
    void DrawLookPicker(Rect r)
    {
        GUI.Box(r, GUIContent.none, box);
        GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 16));
        GUILayout.Label("<b>Borsacını seç</b>", h2);
        if (Characters.Available)
        {
            int m = Characters.SavedModel;
            bool ch = false;
            Pick("Karakter", Characters.Labels[m], ref m, Characters.Count, ref ch);
            if (ch)
            {
                Characters.SavedModel = m;
                Characters.Spawn(player.transform, m, false, Look.Load());
            }
            GUILayout.Label("Karakterin salonda, bankada ve evinde seni temsil eder.", small);
        }
        else
        {
            if (!lookLoaded) { look = Look.Load(); lookLoaded = true; }
            bool ch = false;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!look.female, " Erkek", GUILayout.Height(28)) && look.female) { look.female = false; ch = true; }
            if (GUILayout.Toggle(look.female, " Kadın", GUILayout.Height(28)) && !look.female) { look.female = true; look.beard = false; ch = true; }
            GUILayout.EndHorizontal();
            Pick("Ten", Avatar.SkinNames[look.skin], ref look.skin, Avatar.Skins.Length, ref ch);
            Pick("Saç", (look.female ? Avatar.FemaleHair : Avatar.MaleHair)[look.hairStyle], ref look.hairStyle, 4, ref ch);
            Pick("Takım", Avatar.SuitNames[look.suit], ref look.suit, Avatar.Suits.Length, ref ch);
            if (ch) { look.Save(); Avatar.Rebuild(player.transform, look); }
        }
        GUILayout.EndArea();
    }

    Market.Account loginAcc = Market.Account.Tmtbs;
    bool Tml => loginAcc == Market.Account.Tmtbs;

    void AccountToggle()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Trade of MTBS", Tml ? tabOn : tab, GUILayout.Height(36)) && !Tml) { loginAcc = Market.Account.Tmtbs; formMsg = null; }
        if (GUILayout.Button("Müteşebbis", !Tml ? tabOn : tab, GUILayout.Height(36)) && Tml) { loginAcc = Market.Account.Mutesebbis; formMsg = null; }
        GUILayout.EndHorizontal();
    }

    void DrawLogin()
    {
        Dim(0.35f);
        bool narrow = W < 820;
        float w = Mathf.Min(440, W - 16), h = Tml ? 620 : 520;
        h = Mathf.Min(h, H - 16);
        var r = narrow ? new Rect((W - w) / 2, Mathf.Max(8, H - h - 150), w, h) : new Rect(W - w - 30, Mathf.Max(8, (H - h) / 2), w, h);
        var pr = narrow ? new Rect((W - w) / 2, Mathf.Max(8, r.y - 140), w, 132) : new Rect(24, (H - 160) / 2, 300, 160);
        if (narrow && r.y < 150) { pr = new Rect((W - w) / 2, r.yMax + 6, w, 132); }
        if (!narrow || pr.yMax <= H) DrawLookPicker(pr);

        BeginPanel(r);
        GUILayout.Label(Tml ? "TRADE OF MTBS" : "MÜTEŞEBBİS", h1);
        GUILayout.Label(Tml ? "<b>3B Borsa salonu · al-sat, sıralama, düello</b>" : "<b>3B Dünya · şehir, ev, iş ve borsa</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(4);
        AccountToggle();
        GUILayout.Space(6);
        GUILayout.Label("E-posta", small);
        email = HtmlInput.Field("eposta", email, "E-posta adresin", field, false, false, 120, GUILayout.Height(38));
        GUILayout.Label("Şifre", small);
        pass = HtmlInput.Field("sifre", pass, "Şifren", field, true, false, 64, GUILayout.Height(38));
        GUILayout.Space(8);

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        GUI.enabled = !busy;
        GUI.backgroundColor = new Color(0.35f, 0.6f, 1f);
        if (GUILayout.Button(busy ? "Bekle…" : "Giriş yap", btn, GUILayout.Height(46)) || (enter && !busy && email.Length > 0 && pass.Length > 0))
            DoLogin();
        GUI.backgroundColor = Color.white;
        if (Tml)
        {
            GUILayout.Space(2);
            SocialButtons();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Yeni hesap aç", GUILayout.Height(34))) { view = View.Register; formMsg = null; codeSent = false; }
        if (GUILayout.Button("Şifremi unuttum", GUILayout.Height(34)))
        {
            view = View.Forgot;
            codeSent = false;
            formMsg = null;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        if (GUILayout.Button("Misafir olarak gez (gerçek fiyatlar, sanal para)", GUILayout.Height(38)))
        {
            Market.I.account = loginAcc;
            Market.I.StartDemo();
            EnterGame();
        }
        GUI.enabled = true;
        FormMessage();
        GUILayout.Label(Tml
            ? "Trade of MTBS uygulamasındaki hesabınla aynı (Google / Apple dahil). Bu hesapla yalnız borsa salonunda oynarsın; şehir için Müteşebbis hesabı gerekir."
            : "Müteşebbis uygulamasındaki ya da mutesebbis.tr'deki hesabınla aynı. Google/Apple ile girdiysen önce \"Şifremi unuttum\" ile şifre koy.", small);
        EndPanel();
    }

    void DoLogin()
    {
        busy = true;
        formMsg = null;
        try { PlayerPrefs.SetString(EmailKey, email.Trim()); } catch { }
        Market.I.Login(loginAcc, email, pass, err =>
        {
            busy = false;
            pass = "";
            if (err != null) { formMsg = err; formErr = true; return; }
            EnterGame();
        });
    }

    void DrawForgot()
    {
        Dim(0.45f);
        float w = Mathf.Min(440, W - 16), h = Tml && codeSent ? 470 : 340;
        var r = new Rect((W - w) / 2, Mathf.Max(8, (H - h) / 2), w, Mathf.Min(h, H - 16));
        BeginPanel(r);
        GUILayout.Label("Şifremi unuttum", h1);
        GUILayout.Label(Tml ? "<b>Trade of MTBS hesabı</b>" : "<b>Müteşebbis hesabı</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Label("E-posta", small);
        email = HtmlInput.Field("eposta2", email, "E-posta adresin", field, false, false, 120, GUILayout.Height(38));
        GUILayout.Space(8);
        GUI.enabled = !busy && (!codeSent || Tml);
        if (GUILayout.Button(busy ? "Gönderiliyor…" : Tml ? (codeSent ? "Kodu yeniden gönder" : "Sıfırlama kodu gönder") : "Sıfırlama bağlantısı gönder", btn, GUILayout.Height(44)))
        {
            busy = true;
            Market.I.SendResetCode(loginAcc, email, err =>
            {
                busy = false;
                if (err != null) { formMsg = err; formErr = true; return; }
                codeSent = true;
                formMsg = Tml ? "Kayıtlı bir hesapsa e-postana 6 haneli kod gönderildi." : "Kayıtlı bir hesapsa e-postana şifre sıfırlama bağlantısı gönderildi. Yeni şifreni koyup buradan gir.";
                formErr = false;
            });
        }
        GUI.enabled = true;
        if (Tml && codeSent)
        {
            GUILayout.Label("E-postadaki kod", small);
            code = HtmlInput.Field("kod", code, "6 haneli kod", field, false, true, 6, GUILayout.Height(38));
            GUILayout.Label("Yeni şifre", small);
            newPass = HtmlInput.Field("yenisifre", newPass, "En az 8 karakter", field, true, false, 64, GUILayout.Height(38));
            GUI.enabled = !busy;
            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.5f);
            if (GUILayout.Button(busy ? "Bekle…" : "Şifreyi değiştir ve gir", btn, GUILayout.Height(44)))
            {
                busy = true;
                Market.I.ResetPasswordTm(email, code, newPass, err =>
                {
                    busy = false;
                    if (err != null) { formMsg = err; formErr = true; return; }
                    code = newPass = "";
                    formMsg = null;
                    EnterGame();
                });
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }
        GUILayout.Space(6);
        if (GUILayout.Button("Geri", GUILayout.Height(34))) { view = View.Login; formMsg = null; }
        FormMessage();
        EndPanel();
    }

    void DrawRegister()
    {
        Dim(0.45f);
        float w = Mathf.Min(440, W - 16), h = Tml ? 560 : 440;
        var r = new Rect((W - w) / 2, Mathf.Max(8, (H - h) / 2), w, Mathf.Min(h, H - 16));
        BeginPanel(r);
        GUILayout.Label(Tml ? "Yeni Trade of MTBS hesabı" : "Yeni Müteşebbis hesabı", h1);
        if (Tml && codeSent)
        {
            GUILayout.Label($"<b>{email.Trim()}</b> adresine 6 haneli onay kodu gönderdik. Kodu yaz, hesabın açılsın.", label);
            code = HtmlInput.Field("kod2", code, "6 haneli kod", field, false, true, 6, GUILayout.Height(38));
            GUI.enabled = !busy;
            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.5f);
            if (GUILayout.Button(busy ? "Bekle…" : "Onayla ve başla", btn, GUILayout.Height(44)))
            {
                busy = true;
                Market.I.VerifyTm(email, code, err =>
                {
                    busy = false;
                    if (err != null) { formMsg = err; formErr = true; return; }
                    code = "";
                    formMsg = null;
                    EnterGame();
                });
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Kodu yeniden gönder", GUILayout.Height(34)))
            {
                busy = true;
                Market.I.Post("/tmtbs/auth/kod-gonder", new Dictionary<string, object> { { "email", email.Trim() }, { "tip", "onay" } }, (res, err) =>
                {
                    busy = false;
                    formMsg = err ?? "Kod yeniden gönderildi.";
                    formErr = err != null;
                });
            }
            GUI.enabled = true;
        }
        else
        {
            if (Tml) { SocialButtons(); GUILayout.Label("ya da e-postayla:", small); }
            GUILayout.Label("Ad soyad", small);
            regName = HtmlInput.Field("adsoyad", regName, "Adın ve soyadın", field, false, false, 60, GUILayout.Height(38));
            GUILayout.Label("E-posta", small);
            email = HtmlInput.Field("eposta3", email, "E-posta adresin", field, false, false, 120, GUILayout.Height(38));
            GUILayout.Label("Şifre", small);
            pass = HtmlInput.Field("sifre3", pass, "En az 8 karakter", field, true, false, 64, GUILayout.Height(38));
            GUILayout.Space(8);
            GUI.enabled = !busy;
            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.5f);
            if (GUILayout.Button(busy ? "Bekle…" : "Hesap aç", btn, GUILayout.Height(44)))
            {
                busy = true;
                formMsg = null;
                try { PlayerPrefs.SetString(EmailKey, email.Trim()); } catch { }
                Market.I.Register(loginAcc, regName, email, pass, (err, needCode) =>
                {
                    busy = false;
                    pass = "";
                    if (err != null) { formMsg = err; formErr = true; return; }
                    if (needCode) { codeSent = true; code = ""; formMsg = "Onay kodu e-postana gönderildi."; formErr = false; return; }
                    EnterGame();
                });
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }
        GUILayout.Space(6);
        if (GUILayout.Button("Geri", GUILayout.Height(34))) { view = View.Login; formMsg = null; codeSent = false; }
        FormMessage();
        EndPanel();
    }

    // ================================================================ oyun içi üst bilgi

    void DrawGame()
    {
        var m = Market.I;
        bool narrow = W < 700;
        bool hudHidden = narrow && panel != Panel.None;

        if (!hudHidden)
        {
            float bw = Mathf.Min(300, W * 0.58f);
            GUI.Box(new Rect(8, 8, bw, 92), GUIContent.none, box);
            GUI.Label(new Rect(20, 12, bw - 20, 26), $"<b>{Cut(m.userName, 22)}</b>  <size=12>{(m.mode == Market.Mode.Demo ? "<color=#FFB74D>DEMO</color>" : "<color=#5BE38A>CANLI</color>")}</size>", h2);
            GUI.Label(new Rect(20, 40, bw - 20, 22), $"Nakit <b>{Fmt.Mls(m.cash)}</b>", label);
            GUI.Label(new Rect(20, 62, bw - 20, 22), $"Net <b><color=#FFD54A>{Fmt.Mls(m.net)}</color></b> · <size=12>{Place()}</size>", label);

            float mw = narrow ? 96 : 150;
            if (GUI.Button(new Rect(W - mw - 8, 8, mw, 44), narrow ? "☰ Menü" : "☰ Menü  (M)", btn)) OpenPhone(false);
            if (!narrow) GUI.Label(new Rect(W - 330, 56, 322, 22), m.status, new GUIStyle(small) { alignment = TextAnchor.UpperRight });
        }

        if (panel == Panel.None)
        {
            var near = player.Nearest;
            if (near != null && !string.IsNullOrEmpty(near.prompt))
            {
                float pw = Mathf.Min(460, W - 16);
                var r = HtmlInput.Touch ? new Rect((W - pw) / 2, H * 0.56f, pw, 48) : new Rect((W - pw) / 2, H - 112, pw, 48);
                GUI.Box(r, GUIContent.none, box);
                string key = HtmlInput.Touch ? near.buttonLabel : "E";
                if (GUI.Button(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), $"<b>{key}</b> · <color=#FFD54A>{near.prompt}</color>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter }))
                    Use(near);
            }
            if (!string.IsNullOrEmpty(hint) && Time.time < hintUntil)
            {
                float hw = Mathf.Min(520, W - 16);
                GUI.Label(new Rect((W - hw) / 2, 108, hw, 50), $"<b>{hint}</b>", new GUIStyle(label) { alignment = TextAnchor.UpperCenter });
            }
            if (!HtmlInput.Touch)
                GUI.Label(new Rect(14, H - 30, W - 28, 26),
                    Market.I.Tm ? "W/S yürü · A/D dön · Shift koş · E kullan (banko, vezne, düello) · M telefon · Esc kapat" : "W/S yürü · A/D dön · Shift koş · E kullan / konuş (banko, vezne, durak, büfe, dükkân…) · M telefon · Esc kapat", small);
        }

        if (panel == Panel.Trade) DrawTrade();
        else if (panel == Panel.Phone) DrawPhone();
        else if (panel == Panel.Dialog) DrawDialog();
        else if (panel == Panel.Credit) DrawCredit();
        else if (panel == Panel.Gate) DrawGate();
        DrawFade();
    }
}
