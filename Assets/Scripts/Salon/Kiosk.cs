using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir markanın duvara yaslı işlem bankosu: arkasında tek görevli, onun arkasında markanın fiyat grafiği,
/// üstünde duvar tabelası (logo, ad, hisse değeri, değişim, en iyi alış/satış emri), bankonun önünde de aynı bilgiler.
/// Tezgahın üstünde basılabilir AL ve SAT düğmeleri al-sat ekranını açar.
/// Grafik yalnız oyuncu yakındayken sunucudan çekilir ve birkaç dakikada bir yenilenir.
/// Yerel +z salonun içine bakar; banko duvara yaslıdır (yerel z = -1,6 duvar).
/// Fiyat yükselince tabela yeşil, düşünce kırmızı yanıp söner.
/// </summary>
public class Kiosk : MonoBehaviour
{
    public static readonly List<Kiosk> All = new List<Kiosk>();

    public int slot;
    public Stock stock;
    public TextMesh signName, signPrice, signChange, signBook, signInitials;
    public TextMesh deskName, deskPrice, deskBid, deskAsk;
    public Renderer signEdge, logoQuad;
    public Interactable interact;

    Color target = Color.grey;
    float flashUntil;
    Color flashColor;
    long logoFor;
    Material edgeMat;
    public Renderer chartQuad;
    public TextMesh chartHi, chartLo;
    Texture2D chartTex;
    long chartFor = -1;
    float chartAt = -999f;
    bool chartBusy;
    const float ChartRefresh = 180f, ChartRange = 14f;
    static readonly Color ChartBg = new Color(0.035f, 0.05f, 0.08f, 1f);

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Bankonun önünde (salon tarafında) durma noktası. side: -1 sol, 0 orta, 1 sağ.</summary>
    public Vector3 StandPoint(int side = 0) => transform.TransformPoint(new Vector3(side * 0.7f, 0, 1.35f));
    public Quaternion FacingCounter => Quaternion.LookRotation(-transform.forward);

    static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
    static readonly Color Wood = new Color(0.30f, 0.20f, 0.13f);
    static readonly Color Marble = new Color(0.82f, 0.80f, 0.76f);
    static readonly Color Green = new Color(0.30f, 0.90f, 0.45f);
    static readonly Color Red = new Color(1f, 0.42f, 0.42f);

    public static Kiosk Create(Transform parent, int slot, Vector3 pos, Quaternion rot, bool clerks, int seed)
    {
        var go = new GameObject("Banko_" + (slot + 1));
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var t = go.transform;
        var k = go.AddComponent<Kiosk>();
        k.slot = slot;

        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.75f, -0.55f);
        col.size = new Vector3(2.3f, 1.5f, 2.1f);

        var back = Quaternion.Euler(0, 180f, 0); // yazılar salondan (+z tarafından) okunur
        // Tezgah: ahşap gövde, mermer üst, altın şerit
        Gfx.Box("Tezgah", t, new Vector3(0, 0.52f, 0), new Vector3(2.2f, 1.04f, 0.7f), Gfx.Lit(Wood, 0.35f));
        Gfx.Box("Tezgah_Ust", t, new Vector3(0, 1.07f, -0.02f), new Vector3(2.32f, 0.06f, 0.84f), Gfx.Lit(Marble, 0.7f));
        Gfx.Box("Tezgah_Serit", t, new Vector3(0, 0.95f, 0.36f), new Vector3(2.2f, 0.05f, 0.02f), Gfx.Glow(Gold * 0.85f));
        // Ön ekran (tezgahın önü)
        Gfx.Box("On_Ekran", t, new Vector3(0, 0.55f, 0.355f), new Vector3(1.9f, 0.62f, 0.02f), Gfx.Lit(Panel, 0.9f));
        k.deskName = Gfx.Text("On_Ad", t, new Vector3(0, 0.74f, 0.37f), back, "", 0.16f, Gold);
        k.deskPrice = Gfx.Text("On_Fiyat", t, new Vector3(0, 0.52f, 0.37f), back, "", 0.2f, Color.white);
        k.deskBid = Gfx.Text("On_Alis", t, new Vector3(0.88f, 0.32f, 0.37f), back, "", 0.11f, Green, TextAnchor.MiddleLeft, TextAlignment.Left);
        k.deskAsk = Gfx.Text("On_Satis", t, new Vector3(-0.88f, 0.32f, 0.37f), back, "", 0.11f, Red, TextAnchor.MiddleRight, TextAlignment.Right);
        // Tezgah üstü basılabilir AL / SAT düğmeleri
        foreach (int s in new[] { -1, 1 })
        {
            bool buy = s > 0;
            KioskButton.Create(k, t, buy, new Vector3(s * 0.6f, 1.12f, 0.12f), buy ? Green : Red, buy ? Loc.T("AL") : Loc.T("SAT"), back);
        }
        // Arka duvara yaslı pano ve tabela
        float wz = -1.52f;
        Gfx.Box("Arka_Pano", t, new Vector3(0, 1.4f, wz), new Vector3(2.4f, 2.8f, 0.08f), Gfx.Lit(new Color(0.12f, 0.15f, 0.22f), 0.3f));
        // Markanın fiyat grafiği: görevlinin başının üstünden görünür
        Gfx.Box("Grafik_Cerceve", t, new Vector3(0, 2.2f, wz + 0.05f), new Vector3(2.28f, 1.18f, 0.04f), Gfx.Glow(Gold * 0.45f));
        var cq = Gfx.Prim(PrimitiveType.Quad, "Grafik", t, new Vector3(0, 2.2f, wz + 0.075f), new Vector3(2.2f, 1.1f, 1f), Gfx.Lit(ChartBg, 0.2f), false);
        cq.transform.localRotation = back;
        k.chartQuad = cq.GetComponent<Renderer>();
        k.chartHi = Gfx.Text("Grafik_Ust", t, new Vector3(1.04f, 2.66f, wz + 0.085f), back, "", 0.07f, new Color(0.8f, 0.84f, 0.9f), TextAnchor.MiddleLeft, TextAlignment.Left);
        k.chartLo = Gfx.Text("Grafik_Alt", t, new Vector3(1.04f, 1.74f, wz + 0.085f), back, "", 0.07f, new Color(0.8f, 0.84f, 0.9f), TextAnchor.MiddleLeft, TextAlignment.Left);
        Gfx.Text("Grafik_Not", t, new Vector3(-1.04f, 2.66f, wz + 0.085f), back, Loc.T("7 gün"), 0.07f, Gold, TextAnchor.MiddleRight, TextAlignment.Right);
        Gfx.Box("Tabela", t, new Vector3(0, 3.75f, wz + 0.06f), new Vector3(2.36f, 1.7f, 0.08f), Gfx.Lit(Panel, 0.85f));
        var edge = Gfx.Box("Tabela_Cerceve", t, new Vector3(0, 4.63f, wz + 0.08f), new Vector3(2.4f, 0.07f, 0.08f), Gfx.Glow(Color.grey));
        k.signEdge = edge.GetComponent<Renderer>();
        k.edgeMat = new Material(k.signEdge.sharedMaterial);
        k.signEdge.sharedMaterial = k.edgeMat;
        Gfx.Box("Tabela_Alt", t, new Vector3(0, 2.88f, wz + 0.08f), new Vector3(2.4f, 0.05f, 0.08f), Gfx.Glow(Gold * 0.7f));
        float fz = wz + 0.11f;
        // Logo yeri: renkli rozet + baş harfler; sunucudan logo gelirse dokuyla değişir
        var lq = Gfx.Prim(PrimitiveType.Quad, "Logo", t, new Vector3(0.78f, 4.08f, fz), new Vector3(0.62f, 0.62f, 1f), Gfx.Lit(Color.grey, 0.2f), false);
        lq.transform.localRotation = back;
        k.logoQuad = lq.GetComponent<Renderer>();
        k.signInitials = Gfx.Text("Logo_Harf", t, new Vector3(0.78f, 4.08f, fz + 0.01f), back, "", 0.22f, Color.white);
        k.signName = Gfx.Text("Ad", t, new Vector3(0.38f, 4.3f, fz), back, "", 0.17f, Gold, TextAnchor.MiddleLeft, TextAlignment.Left);
        k.signPrice = Gfx.Text("Fiyat", t, new Vector3(0.38f, 3.98f, fz), back, "", 0.22f, Color.white, TextAnchor.MiddleLeft, TextAlignment.Left);
        k.signChange = Gfx.Text("Degisim", t, new Vector3(0.38f, 3.68f, fz), back, "", 0.15f, Color.white, TextAnchor.MiddleLeft, TextAlignment.Left);
        k.signBook = Gfx.Text("Defter", t, new Vector3(0, 3.22f, fz), back, "", 0.12f, new Color(0.8f, 0.84f, 0.9f));

        // Görevli: tek kişi, tezgahın ortasında
        if (clerks)
        {
            var p = new GameObject("Gorevli");
            p.transform.SetParent(t, false);
            p.transform.localPosition = new Vector3(0, 0, -0.85f);
            p.transform.localRotation = Quaternion.identity;
            var rnd = new System.Random(seed * 7 + 1);
            var lc = p.AddComponent<LazyChar>();
            lc.model = seed % Characters.Count;
            lc.look = Look.Random(rnd);
            lc.idleState = seed % 3 == 0 ? 3 : 0;
        }

        k.interact = Interactable.Make(go, Interactable.Kind.Counter, new Vector3(0, 0, 1.35f), "", Loc.T("İŞLEM"), 2.2f);
        k.interact.counter = k;
        return k;
    }

    public void Bind(Stock s)
    {
        stock = s;
        if (s == null)
        {
            Set(signName, "—"); Set(signPrice, ""); Set(signChange, ""); Set(signBook, "");
            Set(deskName, ""); Set(deskPrice, ""); Set(deskBid, ""); Set(deskAsk, ""); Set(signInitials, "");
            target = new Color(0.3f, 0.32f, 0.36f);
            if (interact) interact.prompt = "";
            return;
        }
        string n = s.name.Length > 15 ? s.name.Substring(0, 14) + "…" : s.name;
        string chg = $"<color={Fmt.ChangeColor(s.change)}>{(s.change > 0.05 ? "▲" : s.change < -0.05 ? "▼" : "■")} {Fmt.Pct(s.change)}</color>";
        Set(signName, n);
        Set(signPrice, Fmt.N2(s.price) + Loc.T(" <size=40>MLS</size>"));
        Set(signChange, chg);
        Set(signBook, Loc.T("<color=#5BE38A>Alış {0}</color>    <color=#FF7A7A>Satış {1}</color>", (s.bestBid > 0 ? Fmt.N2(s.bestBid) : "—"), (s.bestAsk > 0 ? Fmt.N2(s.bestAsk) : "—")));
        Set(deskName, n);
        Set(deskPrice, Fmt.N2(s.price) + "  " + chg);
        Set(deskBid, s.bestBid > 0 ? Loc.T("Alış ") + Fmt.N2(s.bestBid) : "");
        Set(deskAsk, s.bestAsk > 0 ? Loc.T("Satış ") + Fmt.N2(s.bestAsk) : "");
        if (interact) interact.prompt = Loc.T("{0} bankosunda işlem yap", s.name);
        target = s.change > 0.05 ? new Color(0.2f, 0.85f, 0.4f) : s.change < -0.05 ? new Color(1f, 0.3f, 0.3f) : new Color(0.75f, 0.78f, 0.85f);
        if (s.prevPrice > 0 && s.price != s.prevPrice)
        {
            flashColor = s.price > s.prevPrice ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f);
            flashUntil = Time.time + 1.6f;
        }

        if (logoFor != s.id)
        {
            logoFor = s.id;
            Set(signInitials, LogoCache.Initials(s.name));
            if (logoQuad) logoQuad.sharedMaterial = Gfx.Lit(LogoCache.BadgeColor(s.name), 0.2f);
            long id = s.id;
            if (LogoCache.I) LogoCache.I.Get(id, tex =>
            {
                if (this == null || stock == null || stock.id != id || logoQuad == null) return;
                logoQuad.sharedMaterial = Gfx.Texture(tex);
                Set(signInitials, "");
            });
        }
    }

    static void Set(TextMesh tm, string v) { if (tm && tm.text != v) tm.text = v; }

    void Update()
    {
        UpdateChart();
        if (edgeMat == null) return;
        Color c = target;
        if (Time.time < flashUntil) c = Color.Lerp(target, flashColor, Mathf.PingPong(Time.time * 6f, 1f));
        if (edgeMat.color != c) edgeMat.color = c;
    }

    // ---------------------------------------------------------------- Grafik
    void UpdateChart()
    {
        if (chartQuad == null) return;
        if (stock == null)
        {
            if (chartFor != -1) { chartFor = -1; ClearChart(); }
            return;
        }
        if (stock.id != chartFor) { chartFor = stock.id; chartAt = -999f; ClearChart(); }
        if (chartBusy || Time.time - chartAt < ChartRefresh) return;
        var cam = Camera.main;
        if (cam == null || (cam.transform.position - transform.position).sqrMagnitude > ChartRange * ChartRange) return;
        LoadChart();
    }

    void ClearChart()
    {
        if (chartTex != null) { Destroy(chartTex); chartTex = null; }
        chartQuad.sharedMaterial = Gfx.Lit(ChartBg, 0.2f);
        Set(chartHi, "");
        Set(chartLo, "");
    }

    void LoadChart()
    {
        if (Market.I == null) return;
        chartBusy = true;
        chartAt = Time.time;
        long id = stock.id;
        System.Action<Dictionary<string, object>, string> done = (r, err) =>
        {
            chartBusy = false;
            if (this == null || stock == null || stock.id != id) return;
            var g = r != null ? J.O(r, "grafik") : null;
            var pts = g != null ? J.A(g, "noktalar") : null;
            if (pts == null || pts.Count < 2)
            {
                if (err != null) chartAt = Time.time - ChartRefresh + 30f; // hata: 30 sn sonra yeniden dene
                return;
            }
            var tex = SalonDirector.BuildChartTex(pts, 320, 160, out float lo, out float hi, ChartBg);
            if (chartTex != null) Destroy(chartTex);
            chartTex = tex;
            chartQuad.sharedMaterial = Gfx.Texture(tex);
            Set(chartHi, Fmt.N2(hi));
            Set(chartLo, Fmt.N2(lo));
        };
        const string range = "7g";
        if (Market.I.Live)
            Market.I.Get((Market.I.Tm ? "/tmtbs/hisse/" + id + "/grafik?aralik=" : "/dunya/grafik/" + id + "?aralik=") + range, done);
        else if (Api.I != null) // demo/misafir: girişsiz herkese açık grafik
            Api.I.Get("/tmtbs/vitrin/grafik/" + id + "?aralik=" + range, r => done(r, null), (m, c) => done(null, m));
        else chartBusy = false;
    }

    void OnDestroy()
    {
        if (chartTex != null) Destroy(chartTex);
    }
}

/// <summary>Bankonun tezgahındaki basılabilir AL / SAT düğmesi: tıklanınca / dokununca al-sat ekranı açılır.</summary>
public class KioskButton : MonoBehaviour
{
    public Kiosk kiosk;
    public bool buy;
    Transform cap;
    Vector3 capPos;
    float pressT = -9f;

    public static KioskButton Create(Kiosk k, Transform parent, bool buy, Vector3 pos, Color color, string text, Quaternion textRot)
    {
        var root = new GameObject(buy ? "Dugme_AL" : "Dugme_SAT");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;
        root.transform.localRotation = Quaternion.Euler(14f, 0, 0); // ön kenarı alçak: salondan bakana eğik
        var rt = root.transform;
        Gfx.Box("Kaide", rt, Vector3.zero, new Vector3(0.62f, 0.08f, 0.34f), Gfx.Lit(new Color(0.08f, 0.09f, 0.11f), 0.6f));
        var c = Gfx.Box("Kapak", rt, new Vector3(0, 0.06f, 0), new Vector3(0.54f, 0.05f, 0.26f), Gfx.Glow(color * 0.9f));
        // Yazı kapağın üst yüzünde, yukarı bakar (kapağın ölçeğinden etkilenmesin diye köke bağlı)
        var txt = Gfx.Text("Yazi", rt, new Vector3(0, 0.09f, 0), Quaternion.Euler(90f, 180f, 0), "<b>" + text + "</b>", text.Length > 4 ? 0.085f : 0.14f, Color.white);
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.05f, 0);
        col.size = new Vector3(0.7f, 0.3f, 0.45f);
        col.isTrigger = true; // oyuncuyu itmesin; yalnız tıklama ışını içindir
        var b = root.AddComponent<KioskButton>();
        b.kiosk = k;
        b.buy = buy;
        b.cap = c.transform;
        b.capPos = c.transform.localPosition;
        b.label = txt.transform;
        b.labelPos = txt.transform.localPosition;
        return b;
    }

    Transform label;
    Vector3 labelPos;

    public void Press()
    {
        pressT = Time.time;
        if (Bell.I) Bell.I.Tick();
    }

    void Update()
    {
        if (cap == null) return;
        float a = Time.time - pressT;
        float dip = a < 0.25f ? Mathf.Sin(a / 0.25f * Mathf.PI) * 0.035f : 0f;
        cap.localPosition = capPos + Vector3.down * dip;
        if (label) label.localPosition = labelPos + Vector3.down * dip;
    }
}
