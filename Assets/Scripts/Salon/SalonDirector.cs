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
        player.Teleport(new Vector3(0f, SalonBuilder.FloorY, -6f), Quaternion.Euler(0, 180f, 0));
    }

    void Start()
    {
        InputReader.Blocked = true;
        PreviewPose();
        try { email = PlayerPrefs.GetString(EmailKey, ""); } catch { }
        try { loginAcc = PlayerPrefs.GetString("mutesebbis_dunya_hesap", "tm") == "mt" ? Market.Account.Mutesebbis : Market.Account.Tmtbs; } catch { }
        SocialLogin.Received += OnSocial;
        Podium.Announced += OnAnnounce;
        Market.I.Changed += OnMarket;
        Market.I.LoggedOut += OnLoggedOut;
        Market.I.CreditArrived += OnCreditArrived;
        busy = true;
        formMsg = T("Oturum kontrol ediliyor…");
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
        Podium.Announced -= OnAnnounce;
        if (Market.I == null) return;
        Market.I.Changed -= OnMarket;
        Market.I.LoggedOut -= OnLoggedOut;
        Market.I.CreditArrived -= OnCreditArrived;
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
        LivePlayers.Active = true;
        bool tm = Market.I.Tm;
        loginAcc = Market.I.account;
        if (SalonBuilder.TmGate) SalonBuilder.TmGate.SetActive(tm);
        if (Bell.I) Bell.I.Ring();
        if (tm)
        {
            // Trade of MTBS hesabı: yalnız borsa salonu
            player.Teleport(new Vector3(0, 0, -SalonBuilder.HalfD + 3.2f), Quaternion.identity); // giriş katı, merdiven başı
            chase.Snap();
            fadeUntil = Time.time + 1.5f;
            gateAgain = Time.time + 3f;
            Show(T("Trade of MTBS · Seans salonu"), 3f);
            Hint(T("Merdivenden seans salonuna in. Bankolarda al-sat; arkada solda MTBS Bankası müşteri temsilcileri, sağda sıralama duvarları ve çalışma masaları var."), 8f);
            return;
        }
        player.Teleport(Town.HomeSpawn, Town.HomeFacing);
        chase.Snap();
        WakeUp();
    }

    /// <summary>Kürsüden duyuru: salondaysa ekranın üstünde kısa bilgi.</summary>
    void OnAnnounce(string text)
    {
        if (view != View.Game || panel != Panel.None) return;
        if (player.transform.position.z <= -SalonBuilder.HalfD) return;
        Hint(T("<color=#FFD54A>BORSA YÖNETİMİ</color> · ") + text, 7f);
    }

    void OnLoggedOut()
    {
        view = View.Login;
        panel = Panel.None;
        LivePlayers.Active = false;
        chase.overview = true;
        chase.seated = false;
        InputReader.Blocked = true;
        PreviewPose();
        formMsg = switchToMt ? T("Müteşebbis hesabınla giriş yap; şehir seni bekliyor. Hesabın yoksa \"Yeni hesap aç\".") : T("Oturum kapandı. Tekrar giriş yapabilirsin.");
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
        chase.focus = player.SittingOn != null ? player.SittingOn.focus : null;
        if (chase.focus != null) chase.focusCam = player.SittingOn.camLocal;
        // Masada: kamera ekrana yaklaşınca panel açılır; panel kapanınca kalkılır
        if (player.SittingOn != null && player.SittingOn.focus != null)
        {
            if (deskOpenAt > 0f && Time.time >= deskOpenAt) { deskOpenAt = 0f; if (player.SittingOn.bank) OpenBankDesk(); else OpenDesk(); }
            else if (deskOpenAt <= 0f && panel == Panel.None) player.StandUp();
        }
        else deskOpenAt = 0f;
        var near = player.Nearest;
        TouchControls.InteractLabel = near != null ? near.buttonLabel : T("İŞLEM");

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
            // Bankonun AL / SAT düğmesi bankonun çarpışma kutusunun içinde kalır: ışının değdiği her şeye bak
            KioskButton kb = null;
            ChartRangeButton rb = null;
            float rbDist = 0f;
            foreach (var h in Physics.RaycastAll(ray, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var b = h.collider.GetComponent<KioskButton>();
                if (b != null && (kb == null || h.distance < kbDist)) { kb = b; kbDist = h.distance; }
                var cr = h.collider.GetComponent<ChartRangeButton>();
                if (cr != null && (rb == null || h.distance < rbDist)) { rb = cr; rbDist = h.distance; }
            }
            Clickable3D wc = null;
            float wcDist = 0f;
            foreach (var h in Physics.RaycastAll(ray, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var c = h.collider.GetComponent<Clickable3D>();
                if (c != null && (wc == null || h.distance < wcDist)) { wc = c; wcDist = h.distance; }
            }
            if (wc != null && (kb == null || wcDist < kbDist) && (rb == null || wcDist < rbDist))
            {
                float d = (wc.transform.position - player.transform.position).Flat().magnitude;
                if (d <= wc.maxDistance) wc.onClick?.Invoke();
                else Show(T("Biraz daha yaklaş"), 1.2f);
            }
            else if (rb != null && rb.kiosk != null && (kb == null || rbDist < kbDist))
            {
                float d = (rb.kiosk.StandPoint() - player.transform.position).Flat().magnitude;
                if (d < 9f) { rb.kiosk.SetRange(rb.key); if (Bell.I) Bell.I.Tick(); }
                else Show(T("Önce bankoya yaklaş"), 1.2f);
            }
            else if (kb != null && kb.kiosk != null && kb.kiosk.stock != null)
            {
                float d = (kb.kiosk.StandPoint() - player.transform.position).Flat().magnitude;
                if (d < 7f) { kb.Press(); OpenTrade(kb.kiosk.stock, kb.buy); }
                else Show(T("Önce bankoya yaklaş"), 1.2f);
            }
            else if (Physics.Raycast(ray, out RaycastHit hit, 60f))
            {
                var k = hit.collider.GetComponentInParent<Kiosk>();
                if (k != null && k.stock != null)
                {
                    float d = (k.StandPoint() - player.transform.position).Flat().magnitude;
                    if (d < 7f) OpenTrade(k.stock);
                    else Show(T("Önce bankoya yaklaş"), 1.2f);
                }
            }
        }
    }

    float kbDist;
    float deskOpenAt;

    void Use(Interactable it)
    {
        if (UseWorld(it)) return;
        switch (it.kind)
        {
            case Interactable.Kind.Counter: if (it.counter != null && it.counter.stock != null) OpenTrade(it.counter.stock); break;
            case Interactable.Kind.Computer: OpenPhone(true); break;
            case Interactable.Kind.Seat when it.seat != null && it.seat.focus != null:
                player.Sit(it.seat);
                deskOpenAt = Time.time + (it.seat.bank ? 2.2f : 0.9f);
                if (it.seat.bank && it.seat.speech != null)
                {
                    it.seat.speech.text = SalonBuilder.BankSpeech();
                    it.seat.speech.gameObject.SetActive(true);
                }
                break;
            case Interactable.Kind.Seat:
                player.Sit(it.seat);
                Hint(T("Panoları izliyorsun. Kalkmak için yürü."), 4f);
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
        Show(T("Bu markanın bankosu yok"), 1.5f);
    }

    void Show(string text, float seconds) { banner = text; bannerUntil = Time.time + seconds; }

    string hint;
    float hintUntil;
    void Hint(string text, float seconds) { hint = text; hintUntil = Time.time + seconds; }

    string Place()
    {
        Vector3 p = player.transform.position;
        if (p.z > -SalonBuilder.HalfD) return T("Seans salonu");
        if (p.z > SeljukBuilding.ZF) return T("Borsa MTBS · giriş");
        if (Mathf.Abs(p.x - Town.HouseX) < 11.2f && Mathf.Abs(p.z - Town.Z) < 6.2f) return p.x < Town.PartitionX ? T("Odan") : T("Evin · salon");
        if (p.z < City.AvenueZ + 9f) return T("Şehir · cadde");
        if (Mathf.Abs(p.x - Town.BankX) < 10.2f && Mathf.Abs(p.z - Town.Z) < 8.2f) return T("MTBS Bankası");
        return T("Meydan");
    }

    // ================================================================ arayüz temeli

    void Styles()
    {
        if (h1 != null) return;
        GUI.skin.font = Gfx.Font; // çok dilli Noto (Çince, Arapça, Hintçe… harfleri de çizer)
        h1 =new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        h1.normal.textColor = new Color(1f, 0.82f, 0.35f);
        h2 = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true, wordWrap = true };
        h2.normal.textColor = Color.white;
        label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = true };
        label.normal.textColor = new Color(0.9f, 0.92f, 0.96f);
        right = new GUIStyle(label) { alignment = TextAnchor.UpperRight, wordWrap = false };
        small = new GUIStyle(label) { fontSize = 13 };
        small.normal.textColor = new Color(0.7f, 0.74f, 0.82f);
        Theme();
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 10, 10), border = new RectOffset(22, 22, 22, 22) };
        box.normal.background = RTex(64, 64, 18, new Color(0.11f, 0.13f, 0.20f, 0.985f), new Color(0.04f, 0.05f, 0.08f, 0.99f), new Color(1f, 0.82f, 0.35f, 0.38f), 1.2f, 0f, 0.05f);
        glassTex = RTex(64, 64, 18, new Color(0.10f, 0.12f, 0.18f, 0.78f), new Color(0.05f, 0.06f, 0.09f, 0.82f), new Color(1, 1, 1, 0.14f), 1f, 0f, 0.06f);
        pillTex = RTex(32, 32, 15, new Color(0.08f, 0.09f, 0.13f, 0.86f), new Color(0.05f, 0.06f, 0.09f, 0.86f), new Color(1f, 0.82f, 0.35f, 0.5f), 1f);
        btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true };
        field = new GUIStyle(GUI.skin.textField) { fontSize = 17, padding = new RectOffset(10, 10, 7, 7) };
        big = new GUIStyle(h1) { fontSize = 40 };
        big.normal.textColor = Color.white;
        tab = new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true, margin = new RectOffset(2, 2, 2, 2) };
        tab.normal.background = RTex(32, 32, 15, new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.03f), new Color(1, 1, 1, 0.10f), 1f);
        tab.hover.background = RTex(32, 32, 15, new Color(1, 1, 1, 0.13f), new Color(1, 1, 1, 0.08f), new Color(1f, 0.85f, 0.45f, 0.35f), 1f);
        tab.active.background = tab.hover.background;
        tab.normal.textColor = tab.hover.textColor = tab.active.textColor = new Color(0.76f, 0.8f, 0.88f);
        tab.hover.textColor = Color.white;
        tabOn = new GUIStyle(tab) { fontStyle = FontStyle.Bold };
        tabOn.normal.background = tabOn.hover.background = tabOn.active.background =
            RTex(32, 32, 15, new Color(1f, 0.86f, 0.42f, 0.98f), new Color(0.92f, 0.66f, 0.18f, 0.98f), new Color(1f, 0.95f, 0.75f, 0.9f), 1f, 0f, 0.18f);
        tabOn.normal.textColor = tabOn.hover.textColor = tabOn.active.textColor = new Color(0.12f, 0.09f, 0.03f);
        shadowTex = RTex(64, 64, 22, new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0), 0f, 10f);
    }

    /// <summary>Tuşların varsayılan rengi (renkli tuşlardan sonra buna dönülür).</summary>
    public static readonly Color BtnBase = new Color(0.26f, 0.33f, 0.50f);
    Texture2D shadowTex, glassTex, pillTex;

    /// <summary>Yuvarlak köşeli, dikey geçişli, kenarlıklı doku (9 dilimli kullanılır). soft &gt; 0: yumuşak kenar (gölge).</summary>
    static Texture2D RTex(int w, int h, float rad, Color top, Color bottom, Color border, float bw, float soft = 0f, float gloss = 0f)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, rad, w - rad), cy = Mathf.Clamp(y + 0.5f, rad, h - rad);
                float d = rad - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)); // kenara uzaklık (içeride +)
                float a = soft > 0 ? Mathf.Clamp01(d / soft) : Mathf.Clamp01(d + 0.5f);
                float v = y / (float)(h - 1);
                Color c = Color.Lerp(bottom, top, v);
                // cam parıltısı: üst bölümde yumuşak beyaz bant + en üstte ince ışık çizgisi
                if (gloss > 0)
                {
                    float g = Mathf.SmoothStep(0.52f, 1f, v) * gloss;
                    c = new Color(Mathf.Lerp(c.r, 1, g), Mathf.Lerp(c.g, 1, g), Mathf.Lerp(c.b, 1, g), Mathf.Max(c.a, g * 0.6f));
                    if (d > 0.5f && d < bw + 2.2f && y > h * 0.5f) c = Color.Lerp(c, new Color(1, 1, 1, c.a), 0.18f * gloss * 4f);
                }
                if (bw > 0) c = Color.Lerp(c, new Color(border.r, border.g, border.b, Mathf.Max(c.a, border.a)), Mathf.Clamp01(bw + 0.5f - d) * border.a);
                c.a *= a;
                px[y * w + x] = c;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    /// <summary>Varsayılan Unity görünümü yerine koyu, yuvarlak köşeli tema (tüm tuş/alan/kaydırma çubukları).</summary>
    static void Theme()
    {
        var s = GUI.skin;
        var b = s.button;
        // Beyaz tabanlı (GUI.backgroundColor ile renklenir) cam parıltılı düğmeler
        b.normal.background = RTex(32, 40, 11, new Color(0.96f, 0.97f, 1f), new Color(0.70f, 0.73f, 0.80f), new Color(1, 1, 1, 0.45f), 1f, 0f, 0.16f);
        b.hover.background = RTex(32, 40, 11, new Color(1f, 1f, 1f), new Color(0.84f, 0.86f, 0.92f), new Color(1f, 0.93f, 0.7f, 0.95f), 1.3f, 0f, 0.22f);
        b.active.background = RTex(32, 40, 11, new Color(0.66f, 0.69f, 0.76f), new Color(0.86f, 0.88f, 0.93f), new Color(1, 1, 1, 0.5f), 1f);
        b.focused.background = b.normal.background;
        b.onNormal.background = b.onHover.background = b.onActive.background = b.active.background;
        b.border = new RectOffset(12, 12, 13, 13);
        b.normal.textColor = b.hover.textColor = b.active.textColor = b.focused.textColor = Color.white;
        b.fontSize = Mathf.Max(b.fontSize, 14);
        b.richText = true;
        b.margin = new RectOffset(3, 3, 3, 3);

        var f = s.textField;
        f.normal.background = RTex(32, 32, 8, new Color(0.03f, 0.04f, 0.07f, 0.95f), new Color(0.05f, 0.06f, 0.1f, 0.95f), new Color(1, 1, 1, 0.18f), 1f);
        f.hover.background = RTex(32, 32, 8, new Color(0.04f, 0.05f, 0.09f, 0.95f), new Color(0.06f, 0.07f, 0.11f, 0.95f), new Color(1, 1, 1, 0.32f), 1f);
        f.focused.background = f.onFocused.background = RTex(32, 32, 8, new Color(0.04f, 0.05f, 0.09f, 0.97f), new Color(0.06f, 0.07f, 0.11f, 0.97f), new Color(1f, 0.82f, 0.35f, 0.9f), 1.5f);
        f.border = new RectOffset(9, 9, 9, 9);
        f.normal.textColor = f.hover.textColor = f.focused.textColor = Color.white;

        var sb = s.verticalScrollbar;
        sb.normal.background = RTex(12, 32, 5, new Color(1, 1, 1, 0.05f), new Color(1, 1, 1, 0.05f), new Color(0, 0, 0, 0), 0f);
        sb.border = new RectOffset(5, 5, 6, 6);
        sb.fixedWidth = 10;
        var th = s.verticalScrollbarThumb;
        th.normal.background = RTex(12, 32, 5, new Color(1f, 0.85f, 0.45f, 0.55f), new Color(1f, 0.75f, 0.3f, 0.55f), new Color(0, 0, 0, 0), 0f);
        th.border = new RectOffset(5, 5, 6, 6);
        th.fixedWidth = 10;

        var tg = s.toggle;
        tg.normal.textColor = tg.hover.textColor = tg.active.textColor = new Color(0.85f, 0.88f, 0.94f);
        tg.onNormal.textColor = tg.onHover.textColor = tg.onActive.textColor = new Color(1f, 0.88f, 0.45f);
        tg.fontSize = Mathf.Max(tg.fontSize, 14);
    }

    void OnGUI()
    {
        Styles();
        GUI.backgroundColor = BtnBase;
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
        {
            // gölgeli büyük başlık, son yarım saniyede söner
            float a = Mathf.Clamp01((bannerUntil - Time.time) / 0.5f);
            Color o = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.55f * a);
            GUI.Label(new Rect(2, H * 0.2f + 3, W, 72), banner, big);
            GUI.color = new Color(1, 1, 1, a);
            GUI.Label(new Rect(0, H * 0.2f, W, 72), banner, big);
            GUI.color = o;
        }
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
        DrawShadow(r);
        PanelBox(r);
        // Üst kenarda ortadan yanlara sönen altın vurgu + başlık bölgesinde sıcak parıltı
        if (Event.current.type == EventType.Repaint)
        {
            Color oc = GUI.color;
            int seg = 24;
            float lw = r.width - 36;
            for (int i = 0; i < seg; i++)
            {
                float u = Mathf.Abs((i + 0.5f) / seg * 2f - 1f);
                GUI.color = new Color(1f, 0.84f, 0.4f, 0.9f * (1f - u * u));
                GUI.DrawTexture(new Rect(r.x + 18 + i * lw / seg, r.y + 1, lw / seg + 0.5f, 2f), Texture2D.whiteTexture);
            }
            for (int i = 0; i < 10; i++)
            {
                GUI.color = new Color(1f, 0.75f, 0.3f, 0.035f * (1f - i / 10f));
                GUI.DrawTexture(new Rect(r.x + 6, r.y + 3 + i * 5, r.width - 12, 5), Texture2D.whiteTexture);
            }
            GUI.color = oc;
        }
        rowStripe = false;
        var e = Event.current;
        if (e.type == EventType.MouseDown) panelDrag = 0;
        if (e.type == EventType.MouseDrag) panelDrag += e.delta.magnitude;
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

    GUIStyle demoStyle;
    Texture2D demoTex;

    /// <summary>Sol altta her zaman görünen "3B DEMO" rozeti: bu sürümün deneme olduğunu belirtir.</summary>
    void DemoBadge()
    {
        if (demoStyle == null)
        {
            demoTex = RTex(32, 32, 10, new Color(1f, 0.55f, 0.15f, 0.92f), new Color(0.9f, 0.4f, 0.1f, 0.92f), new Color(1, 1, 1, 0.5f), 1f);
            demoStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, border = new RectOffset(10, 10, 10, 10) };
            demoStyle.normal.background = demoTex;
            demoStyle.normal.textColor = Color.white;
        }
        const string t = "3B DEMO · geliştirme sürümü";
        float w = 210;
        Color ob = GUI.backgroundColor;
        GUI.backgroundColor = Color.white;
        GUI.Label(new Rect(8, H - 56, w, 22), t, demoStyle);
        GUI.backgroundColor = ob;
    }

    /// <summary>Koyu panel kutusu (tema rengiyle boyanmaz).</summary>
    void PanelBox(Rect r)
    {
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = Color.white;
        GUI.Box(r, GUIContent.none, box);
        GUI.backgroundColor = old;
    }

    /// <summary>Sekme tuşu: seçili olan altın vurgulu (tema rengiyle boyanmaz).</summary>
    bool TabBtn(string text, bool on, float h, float w = 0)
    {
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = Color.white;
        bool r = w > 0 ? GUILayout.Button(text, on ? tabOn : tab, GUILayout.Height(h), GUILayout.Width(w)) : GUILayout.Button(text, on ? tabOn : tab, GUILayout.Height(h));
        GUI.backgroundColor = old;
        return r;
    }

    void DrawShadow(Rect r)
    {
        if (shadowTex == null || Event.current.type != EventType.Repaint) return;
        var st = new GUIStyle { border = new RectOffset(24, 24, 24, 24) };
        st.normal.background = shadowTex;
        st.Draw(new Rect(r.x - 8, r.y - 4, r.width + 16, r.height + 16), false, false, false, false);
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

    bool rowStripe;

    void Row(string k, string v)
    {
        if (smallNw == null) smallNw = new GUIStyle(small) { wordWrap = false, padding = new RectOffset(8, 4, 3, 3) };
        GUILayout.BeginHorizontal();
        GUILayout.Label(k, smallNw);
        GUILayout.FlexibleSpace();
        GUILayout.Label(v, right);
        GUILayout.Space(6);
        GUILayout.EndHorizontal();
        // okunurluk için çizgili satırlar
        rowStripe = !rowStripe;
        if (rowStripe && Event.current.type == EventType.Repaint)
        {
            var r = GUILayoutUtility.GetLastRect();
            Color o = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.035f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = o;
        }
    }

    GUIStyle titleSt;

    void Title(string t)
    {
        if (titleSt == null) titleSt = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
        GUILayout.Space(10);
        GUILayout.Label("<color=#FFD54A>" + t.ToUpper(new System.Globalization.CultureInfo("tr-TR")) + "</color>", titleSt);
        // başlığın altında soldan sağa sönen altın çizgi
        var r = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.Repaint)
        {
            Color o = GUI.color;
            for (int i = 0; i < 12; i++)
            {
                GUI.color = new Color(1f, 0.82f, 0.35f, 0.55f * (1f - i / 12f));
                GUI.DrawTexture(new Rect(r.x + i * r.width / 12f, r.yMax, r.width / 12f + 1, 1.2f), Texture2D.whiteTexture);
            }
            GUI.color = o;
        }
        GUILayout.Space(4);
        rowStripe = false;
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
        if (KeyNames.TryGetValue(key, out var n)) return T(n);
        string s = key.Replace('_', ' ');
        return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;
    }

    static string Val(object v)
    {
        switch (v)
        {
            case null: return "—";
            case bool b: return b ? T("evet") : T("hayır");
            case double d: return System.Math.Abs(d - System.Math.Round(d)) < 1e-9 ? Fmt.N(d) : Fmt.N2(d);
            case string s: return s.Length > 70 ? s.Substring(0, 69) + "…" : s;
            case List<object> l: return T("{0} kayıt", l.Count);
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
        if (GUILayout.Button("<", GUILayout.Width(34), GUILayout.Height(30))) { idx = (idx + count - 1) % count; changed = true; }
        GUILayout.Label("<b>" + value + "</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = false }, GUILayout.Width(110));
        if (GUILayout.Button(">", GUILayout.Width(34), GUILayout.Height(30))) { idx = (idx + 1) % count; changed = true; }
        GUILayout.EndHorizontal();
    }

    /// <summary>Borsacını seç. Mixamo modelleri varsa model seçimi, yoksa ilkel gövde seçenekleri.</summary>
    void DrawLookPicker(Rect r)
    {
        PanelBox(r);
        GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 16));
        GUILayout.Label(T("<b>Borsacını seç</b>"), h2);
        if (Characters.Available)
        {
            int m = Characters.SavedModel;
            bool ch = false;
            Pick(T("Karakter"), T(Characters.Labels[m]), ref m, Characters.Count, ref ch);
            if (ch)
            {
                Characters.SavedModel = m;
                Characters.Spawn(player.transform, m, false, Look.Load());
            }
            GUILayout.Label(T("Karakterin salonda, bankada ve evinde seni temsil eder."), small);
        }
        else
        {
            if (!lookLoaded) { look = Look.Load(); lookLoaded = true; }
            bool ch = false;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!look.female, T(" Erkek"), GUILayout.Height(28)) && look.female) { look.female = false; ch = true; }
            if (GUILayout.Toggle(look.female, T(" Kadın"), GUILayout.Height(28)) && !look.female) { look.female = true; look.beard = false; ch = true; }
            GUILayout.EndHorizontal();
            Pick(T("Ten"), Avatar.SkinNames[look.skin], ref look.skin, Avatar.Skins.Length, ref ch);
            Pick(T("Saç"), (look.female ? Avatar.FemaleHair : Avatar.MaleHair)[look.hairStyle], ref look.hairStyle, 4, ref ch);
            Pick(T("Takım"), Avatar.SuitNames[look.suit], ref look.suit, Avatar.Suits.Length, ref ch);
            if (ch) { look.Save(); Avatar.Rebuild(player.transform, look); }
        }
        GUILayout.EndArea();
    }

    Market.Account loginAcc = Market.Account.Tmtbs;
    bool Tml => loginAcc == Market.Account.Tmtbs;

    void AccountToggle()
    {
        GUILayout.BeginHorizontal();
        if (TabBtn(T("Trade of MTBS"), Tml, 36) && !Tml) { loginAcc = Market.Account.Tmtbs; formMsg = null; }
        if (TabBtn(T("Müteşebbis"), !Tml, 36) && Tml) { loginAcc = Market.Account.Mutesebbis; formMsg = null; }
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
        GUILayout.Label(Tml ? T("TRADE OF MTBS") : T("MÜTEŞEBBİS"), h1);
        LoginLanguage();
        GUILayout.Label(Tml ? T("<b>3B Borsa salonu · al-sat, sıralama, düello</b>") : T("<b>3B Dünya · şehir, ev, iş ve borsa</b>"), new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(4);
        AccountToggle();
        GUILayout.Space(6);
        GUILayout.Label(T("E-posta"), small);
        email = HtmlInput.Field("eposta", email, T("E-posta adresin"), field, false, false, 120, GUILayout.Height(38));
        GUILayout.Label(T("Şifre"), small);
        pass = HtmlInput.Field("sifre", pass, T("Şifren"), field, true, false, 64, GUILayout.Height(38));
        GUILayout.Space(8);

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        GUI.enabled = !busy;
        GUI.backgroundColor = new Color(0.35f, 0.6f, 1f);
        if (GUILayout.Button(busy ? T("Bekle…") : T("Giriş yap"), btn, GUILayout.Height(46)) || (enter && !busy && email.Length > 0 && pass.Length > 0))
            DoLogin();
        GUI.backgroundColor = SalonDirector.BtnBase;
        if (Tml && SocialLogin.Available)
        {
            GUILayout.Space(2);
            SocialButtons();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(T("Yeni hesap aç"), GUILayout.Height(34))) { view = View.Register; formMsg = null; codeSent = false; }
        if (GUILayout.Button(T("Şifremi unuttum"), GUILayout.Height(34)))
        {
            view = View.Forgot;
            codeSent = false;
            formMsg = null;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        if (GUILayout.Button(T("Misafir olarak gez (gerçek fiyatlar, sanal para)"), GUILayout.Height(38)))
        {
            Market.I.account = loginAcc;
            Market.I.StartDemo();
            EnterGame();
        }
        GUI.enabled = true;
        FormMessage();
        GUILayout.Label(Tml
            ? T("Trade of MTBS uygulamasındaki hesabınla aynı (Google / Apple dahil). Bu hesapla yalnız borsa salonunda oynarsın; şehir için Müteşebbis hesabı gerekir.")
            : T("Müteşebbis uygulamasındaki ya da mutesebbis.tr'deki hesabınla aynı. Google/Apple ile girdiysen önce \"Şifremi unuttum\" ile şifre koy."), small);
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
        GUILayout.Label(T("Şifremi unuttum"), h1);
        GUILayout.Label(Tml ? T("<b>Trade of MTBS hesabı</b>") : T("<b>Müteşebbis hesabı</b>"), new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Label(T("E-posta"), small);
        email = HtmlInput.Field("eposta2", email, T("E-posta adresin"), field, false, false, 120, GUILayout.Height(38));
        GUILayout.Space(8);
        GUI.enabled = !busy && (!codeSent || Tml);
        if (GUILayout.Button(busy ? T("Gönderiliyor…") : Tml ? (codeSent ? T("Kodu yeniden gönder") : T("Sıfırlama kodu gönder")) : T("Sıfırlama bağlantısı gönder"), btn, GUILayout.Height(44)))
        {
            busy = true;
            Market.I.SendResetCode(loginAcc, email, err =>
            {
                busy = false;
                if (err != null) { formMsg = err; formErr = true; return; }
                codeSent = true;
                formMsg = Tml ? T("Kayıtlı bir hesapsa e-postana 6 haneli kod gönderildi.") : T("Kayıtlı bir hesapsa e-postana şifre sıfırlama bağlantısı gönderildi. Yeni şifreni koyup buradan gir.");
                formErr = false;
            });
        }
        GUI.enabled = true;
        if (Tml && codeSent)
        {
            GUILayout.Label(T("E-postadaki kod"), small);
            code = HtmlInput.Field("kod", code, T("6 haneli kod"), field, false, true, 6, GUILayout.Height(38));
            GUILayout.Label(T("Yeni şifre"), small);
            newPass = HtmlInput.Field("yenisifre", newPass, T("En az 8 karakter"), field, true, false, 64, GUILayout.Height(38));
            GUI.enabled = !busy;
            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.5f);
            if (GUILayout.Button(busy ? T("Bekle…") : T("Şifreyi değiştir ve gir"), btn, GUILayout.Height(44)))
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
            GUI.backgroundColor = SalonDirector.BtnBase;
            GUI.enabled = true;
        }
        GUILayout.Space(6);
        if (GUILayout.Button(T("Geri"), GUILayout.Height(34))) { view = View.Login; formMsg = null; }
        FormMessage();
        EndPanel();
    }

    void DrawRegister()
    {
        Dim(0.45f);
        float w = Mathf.Min(440, W - 16), h = Tml ? 560 : 440;
        var r = new Rect((W - w) / 2, Mathf.Max(8, (H - h) / 2), w, Mathf.Min(h, H - 16));
        BeginPanel(r);
        GUILayout.Label(Tml ? T("Yeni Trade of MTBS hesabı") : T("Yeni Müteşebbis hesabı"), h1);
        if (Tml && codeSent)
        {
            GUILayout.Label(T("<b>{0}</b> adresine 6 haneli onay kodu gönderdik. Kodu yaz, hesabın açılsın.", email.Trim()), label);
            code = HtmlInput.Field("kod2", code, T("6 haneli kod"), field, false, true, 6, GUILayout.Height(38));
            GUI.enabled = !busy;
            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.5f);
            if (GUILayout.Button(busy ? T("Bekle…") : T("Onayla ve başla"), btn, GUILayout.Height(44)))
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
            GUI.backgroundColor = SalonDirector.BtnBase;
            if (GUILayout.Button(T("Kodu yeniden gönder"), GUILayout.Height(34)))
            {
                busy = true;
                Market.I.Post("/tmtbs/auth/kod-gonder", new Dictionary<string, object> { { "email", email.Trim() }, { "tip", "onay" } }, (res, err) =>
                {
                    busy = false;
                    formMsg = err ?? T("Kod yeniden gönderildi.");
                    formErr = err != null;
                });
            }
            GUI.enabled = true;
        }
        else
        {
            if (Tml && SocialLogin.Available) { SocialButtons(); GUILayout.Label(T("ya da e-postayla:"), small); }
            GUILayout.Label(T("Ad soyad"), small);
            regName = HtmlInput.Field("adsoyad", regName, T("Adın ve soyadın"), field, false, false, 60, GUILayout.Height(38));
            GUILayout.Label(T("E-posta"), small);
            email = HtmlInput.Field("eposta3", email, T("E-posta adresin"), field, false, false, 120, GUILayout.Height(38));
            GUILayout.Label(T("Şifre"), small);
            pass = HtmlInput.Field("sifre3", pass, T("En az 8 karakter"), field, true, false, 64, GUILayout.Height(38));
            GUILayout.Space(8);
            GUI.enabled = !busy;
            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.5f);
            if (GUILayout.Button(busy ? T("Bekle…") : T("Hesap aç"), btn, GUILayout.Height(44)))
            {
                busy = true;
                formMsg = null;
                try { PlayerPrefs.SetString(EmailKey, email.Trim()); } catch { }
                Market.I.Register(loginAcc, regName, email, pass, (err, needCode) =>
                {
                    busy = false;
                    pass = "";
                    if (err != null) { formMsg = err; formErr = true; return; }
                    if (needCode) { codeSent = true; code = ""; formMsg = T("Onay kodu e-postana gönderildi."); formErr = false; return; }
                    EnterGame();
                });
            }
            GUI.backgroundColor = SalonDirector.BtnBase;
            GUI.enabled = true;
        }
        GUILayout.Space(6);
        if (GUILayout.Button(T("Geri"), GUILayout.Height(34))) { view = View.Login; formMsg = null; codeSent = false; }
        FormMessage();
        EndPanel();
    }

    // ================================================================ oyun içi üst bilgi

    GUIStyle hintSt, hudName, hudKey, hudVal, avatarSt, pillSt;
    Texture2D avatarTex;

    /// <summary>Sol üstte oyuncu kartı (baş harf rozeti, CANLI/DEMO, nakit, net, yer, çevrimiçi sayısı); sağ üstte menü.</summary>
    void DrawHud(Market m, bool narrow)
    {
        if (hudName == null)
        {
            hudName = new GUIStyle(label) { fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            hudKey = new GUIStyle(small) { fontSize = 11, wordWrap = false, padding = new RectOffset(0, 0, 0, 0) };
            hudKey.normal.textColor = new Color(0.62f, 0.67f, 0.76f);
            hudVal = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false, padding = new RectOffset(0, 0, 0, 0) };
            avatarTex = RTex(48, 48, 24, new Color(1f, 0.86f, 0.42f), new Color(0.85f, 0.58f, 0.15f), new Color(1, 1, 1, 0.7f), 1.5f, 0f, 0.2f);
            avatarSt = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            avatarSt.normal.textColor = new Color(0.15f, 0.1f, 0.03f);
            pillSt = new GUIStyle(small) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(9, 9, 9, 9) };
        }
        float bw = Mathf.Min(320, W * 0.62f);
        var card = new Rect(8, 8, bw, 112);
        DrawShadow(card);
        Color ob = GUI.backgroundColor;
        GUI.backgroundColor = Color.white;
        var gs = new GUIStyle { border = new RectOffset(22, 22, 22, 22) };
        gs.normal.background = glassTex;
        GUI.Box(card, GUIContent.none, gs);
        // baş harf rozeti
        GUI.DrawTexture(new Rect(18, 17, 40, 40), avatarTex);
        string ini = string.IsNullOrEmpty(m.userName) ? "?" : m.userName.Substring(0, 1).ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        GUI.Label(new Rect(18, 17, 40, 40), ini, avatarSt);
        GUI.Label(new Rect(66, 16, bw - 130, 26), Cut(m.userName, 20), hudName);
        bool demo = m.mode == Market.Mode.Demo;
        var pr = new Rect(bw - 62, 17, 56, 18);
        pillSt.normal.background = demo ? demoTex ?? pillTex : liveTex ?? (liveTex = RTex(32, 32, 9, new Color(0.2f, 0.75f, 0.42f, 0.95f), new Color(0.1f, 0.55f, 0.3f, 0.95f), new Color(1, 1, 1, 0.45f), 1f));
        pillSt.normal.textColor = Color.white;
        GUI.Label(pr, demo ? T("DEMO") : T("CANLI"), pillSt);
        GUI.Label(new Rect(66, 42, bw - 80, 18), Place() + (LivePlayers.OnlineCount > 0 ? $"  ·  <color=#7CC8FF>{T("{0} oyuncu burada", LivePlayers.OnlineCount)}</color>" : ""), hudKey);
        float cw = (bw - 28) / 2f;
        GUI.Label(new Rect(18, 68, cw, 16), T("NAKİT"), hudKey);
        GUI.Label(new Rect(18, 84, cw, 26), Fmt.Mls(m.cash), hudVal);
        GUI.Label(new Rect(18 + cw, 68, cw, 16), T("NET DEĞER"), hudKey);
        GUI.Label(new Rect(18 + cw, 84, cw, 26), $"<color=#FFD54A>{Fmt.Mls(m.net)}</color>", hudVal);
        GUI.backgroundColor = ob;
        // karta dokun: salondaki oyuncular (Trade of MTBS) / cep menüsü
        if (panel == Panel.None && Event.current.type == EventType.MouseUp && card.Contains(Event.current.mousePosition))
        {
            Event.current.Use();
            OpenPhone(false);
            if (m.Tm) SwitchTab("Git");
        }

        float mw = narrow ? 96 : 150;
        if (GUI.Button(new Rect(W - mw - 8, 8, mw, 44), narrow ? T("Menü") : T("Menü") + "  (M)", btn)) OpenPhone(false);
        if (!narrow) GUI.Label(new Rect(W - 330, 56, 322, 22), m.status, new GUIStyle(small) { alignment = TextAnchor.UpperRight });
    }

    Texture2D liveTex;

    void DrawGame()
    {
        var m = Market.I;
        bool narrow = W < 700;
        bool hudHidden = narrow && panel != Panel.None;

        if (!hudHidden)
        {
            DrawHud(m, narrow);
        }

        if (panel == Panel.None)
        {
            var near = player.Nearest;
            if (near != null && !string.IsNullOrEmpty(near.prompt))
            {
                float pw = Mathf.Min(460, W - 16);
                var r = HtmlInput.Touch ? new Rect((W - pw) / 2, H * 0.56f, pw, 48) : new Rect((W - pw) / 2, H - 112, pw, 48);
                PanelBox(r);
                string key = HtmlInput.Touch ? near.buttonLabel : "E";
                if (GUI.Button(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), $"<b>{key}</b> · <color=#FFD54A>{near.prompt}</color>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter }))
                    Use(near);
            }
            if (!string.IsNullOrEmpty(hint) && Time.time < hintUntil)
            {
                if (hintSt == null) hintSt = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(18, 18, 8, 8), border = new RectOffset(15, 15, 15, 15) };
                hintSt.normal.background = pillTex;
                float hw = Mathf.Min(560, W - 16);
                var gc = new GUIContent($"<b>{hint}</b>");
                float hh = Mathf.Max(40, hintSt.CalcHeight(gc, hw));
                Color ob = GUI.backgroundColor;
                GUI.backgroundColor = Color.white;
                GUI.Label(new Rect((W - hw) / 2, 128, hw, hh), gc, hintSt);
                GUI.backgroundColor = ob;
            }
            if (!HtmlInput.Touch)
                GUI.Label(new Rect(14, H - 30, W - 28, 26),
                    Market.I.Tm ? T("WASD yürü · Fareyle sürükle: kamera · Tekerlek: yakınlaş · Shift koş · E kullan (banko, vezne, masa) · M telefon · Esc kapat") : T("WASD yürü · Fareyle sürükle: kamera · Tekerlek: yakınlaş · Shift koş · E kullan / konuş (banko, vezne, durak, büfe, dükkân…) · M telefon · Esc kapat"), small);
        }

        if (panel == Panel.Trade) DrawTrade();
        else if (panel == Panel.Phone) DrawPhone();
        else if (panel == Panel.Dialog) DrawDialog();
        else if (panel == Panel.Credit) DrawCredit();
        else if (panel == Panel.Gate) DrawGate();
        DrawFade();
    }
}
