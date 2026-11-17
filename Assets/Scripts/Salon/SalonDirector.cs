using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Salonun akışı ve arayüzü: giriş (e-posta/şifre, şifre belirleme), demo gezinti,
/// üst bilgi çubuğu, kürsüde al-sat paneli; piyasa yenilenince kürsüleri günceller
/// ve sert hareketlerde bot borsacıları koşturur.
/// </summary>
public class SalonDirector : MonoBehaviour
{
    public Walker player;
    public ChaseCamera chase;
    public Boards boards;

    enum View { Login, Forgot, Salon }
    View view = View.Login;

    const string EmailKey = "tmtbs_salon_email";
    static readonly Vector3 Spawn = new Vector3(0, 0, -19f);

    string email = "", pass = "", code = "", newPass = "";
    string formMsg;
    bool formErr;
    bool busy;
    bool codeSent;

    Stock panel;
    string qtyText = "1";
    string tradeMsg;
    bool tradeOk;
    bool tradeBusy;

    string banner;
    float bannerUntil;
    float scale = 1f;
    GUIStyle h1, h2, label, small, box, btn, field, big;

    void Start()
    {
        InputReader.Blocked = true;
        try { email = PlayerPrefs.GetString(EmailKey, ""); } catch { }
        Market.I.Changed += OnMarket;
        Market.I.LoggedOut += OnLoggedOut;
        busy = true;
        formMsg = "Oturum kontrol ediliyor…";
        Market.I.TryResume(ok =>
        {
            busy = false;
            formMsg = null;
            if (ok) EnterSalon();
        });
    }

    void OnDestroy()
    {
        if (Market.I == null) return;
        Market.I.Changed -= OnMarket;
        Market.I.LoggedOut -= OnLoggedOut;
    }

    // ================================================================ akış

    void EnterSalon()
    {
        view = View.Salon;
        chase.overview = false;
        InputReader.Blocked = false;
        player.SetName(Market.I.userName);
        player.Teleport(Spawn, Quaternion.identity);
        if (Bell.I) Bell.I.Ring();
        Show(Market.I.mode == Market.Mode.Demo ? "Seans açıldı · Demo" : "Seans açıldı", 2.5f);
    }

    void OnLoggedOut()
    {
        view = View.Login;
        chase.overview = true;
        panel = null;
        InputReader.Blocked = true;
        formMsg = "Oturum kapandı. Tekrar giriş yapabilirsin.";
        formErr = false;
        foreach (var k in Kiosk.All) k.Bind(null);
    }

    void OnMarket()
    {
        var list = Market.I.stocks;
        foreach (var k in Kiosk.All) k.Bind(k.slot < list.Count ? list[k.slot] : null);

        // Sert hareket: botlar o kürsüye koşar (en fazla 2 hisse)
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
        if (view != View.Salon) return;

        if (panel != null)
        {
            if (InputReader.BackPressed()) ClosePanel();
            return;
        }

        if (InputReader.InteractPressed() && player.Nearest != null && player.Nearest.stock != null)
            OpenPanel(player.Nearest.stock);

        if (InputReader.ClickPressed(out Vector2 sp) && GUIUtility.hotControl == 0 && Camera.main != null)
        {
            var ray = Camera.main.ScreenPointToRay(sp);
            if (Physics.Raycast(ray, out RaycastHit hit, 60f))
            {
                var k = hit.collider.GetComponentInParent<Kiosk>();
                if (k != null && k.stock != null)
                {
                    float d = (k.transform.position - player.transform.position).Flat().magnitude;
                    if (d < 9f) OpenPanel(k.stock);
                    else Show("Önce kürsüye yaklaş", 1.2f);
                }
            }
        }
    }

    void OpenPanel(Stock s)
    {
        panel = s;
        qtyText = "1";
        tradeMsg = null;
        InputReader.Blocked = true;
        Market.I.LoadDetail(s, null);
        if (Bell.I) Bell.I.Tick();
    }

    void ClosePanel()
    {
        panel = null;
        InputReader.Blocked = false;
    }

    void Show(string text, float seconds)
    {
        banner = text;
        bannerUntil = Time.time + seconds;
    }

    // ================================================================ arayüz

    void Styles()
    {
        if (h1 != null) return;
        h1 = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        h1.normal.textColor = new Color(1f, 0.82f, 0.35f);
        h2 = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, richText = true };
        h2.normal.textColor = Color.white;
        label = new GUIStyle(GUI.skin.label) { fontSize = 17, richText = true, wordWrap = true };
        label.normal.textColor = new Color(0.9f, 0.92f, 0.96f);
        small = new GUIStyle(label) { fontSize = 14 };
        small.normal.textColor = new Color(0.7f, 0.74f, 0.82f);
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 12, 12) };
        btn = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, richText = true };
        field = new GUIStyle(GUI.skin.textField) { fontSize = 18, padding = new RectOffset(8, 8, 6, 6) };
        big = new GUIStyle(h1) { fontSize = 46 };
        big.normal.textColor = Color.white;
    }

    void OnGUI()
    {
        Styles();
        scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.6f, 2f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float W = Screen.width / scale, H = Screen.height / scale;

        if (view == View.Login) DrawLogin(W, H);
        else if (view == View.Forgot) DrawForgot(W, H);
        else DrawSalon(W, H);

        if (!string.IsNullOrEmpty(banner) && Time.time < bannerUntil)
            GUI.Label(new Rect(0, H * 0.18f, W, 70), banner, big);
    }

    void Dim(float W, float H, float a)
    {
        Color old = GUI.color;
        GUI.color = new Color(0, 0, 0, a);
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
        GUI.color = old;
    }

    void FormMessage(Rect r)
    {
        if (string.IsNullOrEmpty(formMsg)) return;
        string c = formErr ? "#FF7A7A" : "#9FE0B0";
        GUI.Label(r, $"<color={c}>{formMsg}</color>", small);
    }

    void DrawLogin(float W, float H)
    {
        Dim(W, H, 0.45f);
        float w = 460, h = 470;
        var r = new Rect((W - w) / 2, (H - h) / 2, w, h);
        GUI.Box(r, GUIContent.none, box);
        GUILayout.BeginArea(new Rect(r.x + 24, r.y + 18, w - 48, h - 30));
        GUILayout.Label("TRADE OF MTBS", h1);
        GUILayout.Label("<b>Seans Salonu</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(10);
        GUILayout.Label("E-posta", small);
        email = GUILayout.TextField(email, 120, field, GUILayout.Height(36));
        GUILayout.Label("Şifre", small);
        GUI.SetNextControlName("sifre");
        pass = GUILayout.PasswordField(pass, '•', 64, field, GUILayout.Height(36));
        GUILayout.Space(10);

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        GUI.enabled = !busy;
        GUI.backgroundColor = new Color(0.35f, 0.6f, 1f);
        if (GUILayout.Button(busy ? "Bekle…" : "Giriş yap", btn, GUILayout.Height(44)) || (enter && !busy && email.Length > 0 && pass.Length > 0))
            DoLogin();
        GUI.backgroundColor = Color.white;
        GUILayout.Space(4);
        if (GUILayout.Button("Şifremi unuttum / şifre belirle", GUILayout.Height(30)))
        {
            view = View.Forgot;
            codeSent = false;
            formMsg = null;
        }
        GUILayout.Space(6);
        if (GUILayout.Button("Misafir olarak gez (demo, sanal veriler)", GUILayout.Height(34)))
        {
            Market.I.StartDemo();
            EnterSalon();
        }
        GUI.enabled = true;
        GUILayout.Space(6);
        FormMessage(GUILayoutUtility.GetRect(w - 48, 40));
        GUILayout.Label("Uygulamaya Google ya da Apple ile girdiysen şifren yoktur: \"Şifre belirle\" ile e-postana gelen kodla bir şifre koy.", small);
        GUILayout.EndArea();
    }

    void DoLogin()
    {
        busy = true;
        formMsg = null;
        try { PlayerPrefs.SetString(EmailKey, email.Trim()); } catch { }
        Market.I.Login(email, pass, err =>
        {
            busy = false;
            pass = "";
            if (err != null) { formMsg = err; formErr = true; return; }
            EnterSalon();
        });
    }

    void DrawForgot(float W, float H)
    {
        Dim(W, H, 0.45f);
        float w = 460, h = codeSent ? 470 : 330;
        var r = new Rect((W - w) / 2, (H - h) / 2, w, h);
        GUI.Box(r, GUIContent.none, box);
        GUILayout.BeginArea(new Rect(r.x + 24, r.y + 18, w - 48, h - 30));
        GUILayout.Label("Şifre belirle", h1);
        GUILayout.Label("E-posta", small);
        email = GUILayout.TextField(email, 120, field, GUILayout.Height(36));
        GUI.enabled = !busy;
        if (!codeSent)
        {
            GUILayout.Space(8);
            if (GUILayout.Button(busy ? "Gönderiliyor…" : "Kod gönder", btn, GUILayout.Height(42)))
            {
                busy = true;
                Market.I.SendResetCode(email, err =>
                {
                    busy = false;
                    if (err != null) { formMsg = err; formErr = true; return; }
                    codeSent = true;
                    formMsg = "Kayıtlı bir hesapsa e-postana 6 haneli kod gönderildi.";
                    formErr = false;
                });
            }
        }
        else
        {
            GUILayout.Label("E-postadaki 6 haneli kod", small);
            code = GUILayout.TextField(code, 6, field, GUILayout.Height(36));
            GUILayout.Label("Yeni şifre", small);
            newPass = GUILayout.PasswordField(newPass, '•', 64, field, GUILayout.Height(36));
            GUILayout.Space(8);
            if (GUILayout.Button(busy ? "Bekle…" : "Kaydet ve gir", btn, GUILayout.Height(42)))
            {
                busy = true;
                Market.I.ResetPassword(email, code, newPass, err =>
                {
                    busy = false;
                    newPass = "";
                    if (err != null) { formMsg = err; formErr = true; return; }
                    EnterSalon();
                });
            }
        }
        GUI.enabled = true;
        GUILayout.Space(6);
        if (GUILayout.Button("Geri", GUILayout.Height(30))) { view = View.Login; formMsg = null; }
        FormMessage(GUILayoutUtility.GetRect(w - 48, 44));
        GUILayout.EndArea();
    }

    void DrawSalon(float W, float H)
    {
        var m = Market.I;

        // Üst sol: hesap özeti
        GUI.Box(new Rect(12, 12, 360, 112), GUIContent.none, box);
        GUI.Label(new Rect(28, 18, 330, 28), $"<b>{m.userName}</b>", h2);
        GUI.Label(new Rect(28, 50, 330, 24), $"Nakit  <b>{Fmt.Mls(m.cash)}</b>", label);
        GUI.Label(new Rect(28, 72, 330, 24), $"Hisseler  <b>{Fmt.Mls(m.stockValue)}</b>", label);
        GUI.Label(new Rect(28, 94, 330, 24), $"Net değer  <b><color=#FFD54A>{Fmt.Mls(m.net)}</color></b>", label);

        // Üst sağ: kip + çıkış
        string badge = m.mode == Market.Mode.Demo ? "<color=#FFB74D>DEMO</color>" : "<color=#5BE38A>CANLI</color>";
        GUI.Box(new Rect(W - 262, 12, 250, 74), GUIContent.none, box);
        GUI.Label(new Rect(W - 248, 18, 230, 24), $"<b>{badge}</b>  {m.status}", small);
        if (GUI.Button(new Rect(W - 248, 46, 222, 30), m.mode == Market.Mode.Demo ? "Hesabınla giriş yap" : "Çıkış yap"))
        {
            panel = null;
            m.Logout();
        }

        // Kürsü yakınında ipucu
        if (panel == null && player.Nearest != null && player.Nearest.stock != null)
        {
            var r = new Rect(W / 2 - 230, H - 120, 460, 46);
            GUI.Box(r, GUIContent.none, box);
            GUI.Label(r, $"<b>E</b> · <color=#FFD54A>{player.Nearest.stock.name}</color> kürsüsünde işlem yap",
                new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        }

        GUI.Label(new Rect(14, H - 34, W - 28, 28),
            "W/S yürü · A/D dön · Shift koş · E kürsüde işlem · Kürsüye tıklayarak da açabilirsin · Esc paneli kapat", small);

        if (panel != null) DrawTradePanel(W, H);
    }

    void DrawTradePanel(float W, float H)
    {
        var m = Market.I;
        var s = panel;
        float w = 430, h = 560;
        var r = new Rect(W - w - 16, Mathf.Max(96, (H - h) / 2), w, h);
        GUI.Box(r, GUIContent.none, box);
        GUILayout.BeginArea(new Rect(r.x + 18, r.y + 14, w - 36, h - 24));

        GUILayout.Label($"<b>{s.name}</b>", h2);
        GUILayout.Label($"<size=30><b>{Fmt.N2(s.price)}</b></size> MLS   <color={Fmt.ChangeColor(s.change)}><b>{Fmt.Pct(s.change)}</b></color>", label);
        long sellable = m.mode == Market.Mode.Demo ? s.myQty : s.myFree;
        string mine = s.myQty > 0
            ? $"Portföyünde <b>{Fmt.N(s.myQty)}</b> adet · satılabilir <b>{Fmt.N(sellable)}</b> · ort. maliyet {Fmt.N2(s.myCost)}"
            : "Portföyünde bu hisseden yok.";
        GUILayout.Label(mine, small);
        GUILayout.Space(6);

        // Emir defteri
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((w - 46) / 2));
        GUILayout.Label("<color=#5BE38A><b>Alış</b></color>  fiyat × adet", small);
        BookRows(s.bids, s.detailLoaded);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width((w - 46) / 2));
        GUILayout.Label("<color=#FF7A7A><b>Satış</b></color>  fiyat × adet", small);
        BookRows(s.asks, s.detailLoaded);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        // Adet
        long qty = ParseQty();
        double buyPx = s.bestAsk > 0 ? s.bestAsk : s.price;
        double sellPx = s.bestBid > 0 ? s.bestBid : s.price;
        GUILayout.Label("Adet", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10", GUILayout.Width(48), GUILayout.Height(34))) qtyText = System.Math.Max(1, qty - 10).ToString();
        if (GUILayout.Button("-1", GUILayout.Width(40), GUILayout.Height(34))) qtyText = System.Math.Max(1, qty - 1).ToString();
        qtyText = GUILayout.TextField(qtyText, 9, field, GUILayout.Width(110), GUILayout.Height(34));
        if (GUILayout.Button("+1", GUILayout.Width(40), GUILayout.Height(34))) qtyText = (qty + 1).ToString();
        if (GUILayout.Button("+10", GUILayout.Width(48), GUILayout.Height(34))) qtyText = (qty + 10).ToString();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Alınabilecek en çok", GUILayout.Height(28)) && buyPx > 0)
            qtyText = System.Math.Max(1, (long)(m.cash / buyPx)).ToString();
        if (GUILayout.Button("Elimdekinin tamamı", GUILayout.Height(28)) && sellable > 0)
            qtyText = sellable.ToString();
        GUILayout.EndHorizontal();
        qty = ParseQty();
        GUILayout.Label($"Alış ≈ <b>{Fmt.Mls(qty * buyPx)}</b>   ·   Satış ≈ <b>{Fmt.Mls(qty * sellPx)}</b>", small);
        GUILayout.Space(6);

        GUI.enabled = !tradeBusy && s.detailLoaded;
        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
        if (GUILayout.Button($"AL · {Fmt.N2(buyPx)}", btn, GUILayout.Height(48))) Trade(true, qty);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button($"SAT · {Fmt.N2(sellPx)}", btn, GUILayout.Height(48))) Trade(false, qty);
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        if (tradeBusy) GUILayout.Label("Emir gönderiliyor…", small);
        else if (!string.IsNullOrEmpty(tradeMsg))
            GUILayout.Label($"<color={(tradeOk ? "#5BE38A" : "#FF7A7A")}>{tradeMsg}</color>", label);
        else
            GUILayout.Label("Emir, karşı tarafın en iyi fiyatından verilir.", small);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Kapat (Esc)", GUILayout.Height(32))) ClosePanel();
        GUILayout.EndArea();
    }

    void BookRows(List<KeyValuePair<double, long>> rows, bool loaded)
    {
        if (!loaded) { GUILayout.Label("yükleniyor…", small); return; }
        if (rows.Count == 0) { GUILayout.Label("—", small); return; }
        for (int i = 0; i < rows.Count && i < 5; i++)
            GUILayout.Label($"{Fmt.N2(rows[i].Key)} × {Fmt.N(rows[i].Value)}", label);
    }

    long ParseQty()
    {
        return long.TryParse(qtyText, out long q) && q > 0 ? q : 1;
    }

    void Trade(bool buy, long qty)
    {
        tradeBusy = true;
        tradeMsg = null;
        Market.I.PlaceOrder(panel, buy, qty, (msg, ok) =>
        {
            tradeBusy = false;
            tradeMsg = msg;
            tradeOk = ok;
            if (ok && Bell.I) Bell.I.Tick();
            if (ok && panel != null) Market.I.LoadDetail(panel, null);
        });
    }
}
