using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir markanın duvara yaslı işlem bankosu: arkasında alış ve satış görevlisi, üstünde duvar tabelası
/// (logo, ad, hisse değeri, değişim, en iyi alış/satış emri), bankonun önünde de aynı bilgiler.
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
        // Tezgah üstü ALIŞ / SATIŞ levhaları
        foreach (int s in new[] { -1, 1 })
        {
            bool buy = s > 0;
            var lv = Gfx.Box("Levha", t, new Vector3(s * 0.62f, 1.2f, 0.18f), new Vector3(0.5f, 0.2f, 0.03f), Gfx.Lit(Panel, 0.8f));
            lv.transform.localRotation = Quaternion.Euler(-12f, 0, 0);
            Gfx.Text("Levha_Yazi", t, new Vector3(s * 0.62f, 1.2f, 0.20f), back * Quaternion.Euler(12f, 0, 0), buy ? Loc.T("ALIŞ") : Loc.T("SATIŞ"), 0.1f, buy ? Green : Red);
        }
        // Arka duvara yaslı pano ve tabela
        float wz = -1.52f;
        Gfx.Box("Arka_Pano", t, new Vector3(0, 1.4f, wz), new Vector3(2.4f, 2.8f, 0.08f), Gfx.Lit(new Color(0.12f, 0.15f, 0.22f), 0.3f));
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

        // Görevliler: biri alış, biri satış
        if (clerks)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var p = new GameObject(s > 0 ? "Gorevli_Alis" : "Gorevli_Satis");
                p.transform.SetParent(t, false);
                p.transform.localPosition = new Vector3(s * 0.55f, 0, -0.85f);
                p.transform.localRotation = Quaternion.identity;
                int model = (seed + (s > 0 ? 0 : 1)) % Characters.Count;
                var rnd = new System.Random(seed * 7 + s);
                var lc = p.AddComponent<LazyChar>();
                lc.model = model;
                lc.look = Look.Random(rnd);
                lc.idleState = Mathf.Abs(seed + s) % 3 == 0 ? 3 : 0;
            }
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
        if (edgeMat == null) return;
        Color c = target;
        if (Time.time < flashUntil) c = Color.Lerp(target, flashColor, Mathf.PingPong(Time.time * 6f, 1f));
        if (edgeMat.color != c) edgeMat.color = c;
    }
}
