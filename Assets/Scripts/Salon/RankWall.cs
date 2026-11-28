using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Salonun arka sağ köşesindeki sıralama duvarları (eskiden düello salonu vardı).
/// Arka duvar: seçili kademenin sıralaması (ilk 10, "sen" altın renkte) — açılışta oyuncunun kendi kademesi;
/// üstte tıklanabilir 8 kademe ve dönem (gün/hafta/ay/yıl) düğmeleri.
/// Yan duvar: kademeler tablosu (eşik, oyuncu sayısı; senin kademen vurgulu), satıra tıklayınca arka duvar o kademeye geçer.
/// Veri Trade of MTBS sunucusundan (/tmtbs/siralama), yalnız oyuncu köşeye yakınken çekilir.
/// </summary>
public class RankWall : MonoBehaviour
{
    public static RankWall I { get; private set; }

    const int Rows = 10;
    static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
    static readonly Color Dim = new Color(0.78f, 0.82f, 0.9f);
    static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f);
    static readonly Color BtnOff = new Color(0.14f, 0.17f, 0.24f);

    TextMesh title, mine, note;
    readonly TextMesh[] rowL = new TextMesh[Rows], rowR = new TextMesh[Rows];
    readonly List<(int no, Renderer bg, TextMesh tx)> tierBtns = new List<(int, Renderer, TextMesh)>();
    readonly List<(string key, Renderer bg, TextMesh tx)> periodBtns = new List<(string, Renderer, TextMesh)>();
    readonly List<(int no, Renderer bg, TextMesh tx)> tierRows = new List<(int, Renderer, TextMesh)>();

    int tier;            // gösterilen kademe (0: henüz bilinmiyor → kendi kademem)
    int myTier;
    string period = "hafta";
    bool busy, dirty = true;
    float fetchedAt = -999f;
    Dictionary<int, int> counts = new Dictionary<int, int>();
    public Vector3 center;

    static readonly (string key, string name)[] Periods = { ("gun", "Günlük"), ("hafta", "Haftalık"), ("ay", "Aylık"), ("yil", "Yıllık") };

    // ---------------------------------------------------------------- kurulum

    public static RankWall Build(Transform parent, float halfW, float halfD)
    {
        var g = new GameObject("Siralama_Duvari");
        g.transform.SetParent(parent, false);
        var w = g.AddComponent<RankWall>();
        I = w;
        w.center = new Vector3(14.5f, 0, -halfD + 5f);

        // ---- Arka duvar: sıralama listesi
        float bz = -halfD + 0.12f, cx = 14.6f, bw = 9.6f;
        var back = Quaternion.LookRotation(Vector3.back); // salondan bakan okur
        var t = g.transform;
        Gfx.Box("Sira_Pano", t, new Vector3(cx, 4.7f, bz), new Vector3(bw, 5.4f, 0.1f), Gfx.Lit(Panel, 0.85f));
        Gfx.Box("Sira_Ust_Serit", t, new Vector3(cx, 7.45f, bz + 0.02f), new Vector3(bw + 0.1f, 0.08f, 0.12f), Gfx.Glow(Gold));
        Gfx.Box("Sira_Alt_Serit", t, new Vector3(cx, 1.98f, bz + 0.02f), new Vector3(bw + 0.1f, 0.06f, 0.12f), Gfx.Glow(Gold * 0.7f));
        float fz = bz + 0.07f;
        w.title = Gfx.Text("Sira_Baslik", t, new Vector3(cx, 7.05f, fz), back, "", 0.36f, Gold);
        // Kademe düğmeleri (8)
        var tiers = SalonDirector.Tiers;
        float tw = (bw - 0.4f) / tiers.Length;
        for (int i = 0; i < tiers.Length; i++)
        {
            int no = i + 1;
            var pos = new Vector3(cx + bw / 2f - 0.2f - tw * (i + 0.5f), 6.45f, fz); // salondan bakınca soldan sağa
            var (bg, tx) = Button(t, "Kademe_" + no, pos, new Vector3(tw - 0.08f, 0.42f, 0.04f), Loc.T(tiers[i].ad), 0.13f, back,
                () => w.Select(no));
            w.tierBtns.Add((no, bg, tx));
        }
        // Dönem düğmeleri (sağda) ve "senin sıran" (solda)
        for (int i = 0; i < Periods.Length; i++)
        {
            string key = Periods[i].key;
            var pos = new Vector3(cx - bw / 2f + 0.2f + 0.62f + (Periods.Length - 1 - i) * 1.26f, 5.85f, fz);
            var (bg, tx) = Button(t, "Donem_" + key, pos, new Vector3(1.18f, 0.38f, 0.04f), Loc.T(Periods[i].name), 0.13f, back,
                () => w.SetPeriod(key));
            w.periodBtns.Add((key, bg, tx));
        }
        w.mine = Gfx.Text("Sira_Ben", t, new Vector3(cx + bw / 2f - 0.3f, 5.85f, fz), back, "", 0.16f, Gold, TextAnchor.MiddleLeft, TextAlignment.Left);
        // Satırlar
        for (int r = 0; r < Rows; r++)
        {
            float y = 5.3f - r * 0.32f;
            w.rowL[r] = Gfx.Text("Sira_Satir_" + r, t, new Vector3(cx + bw / 2f - 0.35f, y, fz), back, "", 0.17f, Color.white, TextAnchor.MiddleLeft, TextAlignment.Left);
            w.rowR[r] = Gfx.Text("Sira_Kazanc_" + r, t, new Vector3(cx - bw / 2f + 0.35f, y, fz), back, "", 0.17f, Color.white, TextAnchor.MiddleRight, TextAlignment.Right);
        }
        w.note = Gfx.Text("Sira_Not", t, new Vector3(cx, 2.2f, fz), back, "", 0.12f, Dim);

        // ---- Yan (sağ) duvar: kademeler tablosu
        float sx = halfW - 0.12f, sz = -halfD + 5.2f, sw = 7.4f;
        var side = Quaternion.LookRotation(Vector3.right);
        Gfx.Box("Kademe_Pano", t, new Vector3(sx, 4.5f, sz), new Vector3(0.1f, 4.8f, sw), Gfx.Lit(Panel, 0.85f));
        Gfx.Box("Kademe_Serit", t, new Vector3(sx - 0.02f, 6.95f, sz), new Vector3(0.12f, 0.08f, sw + 0.1f), Gfx.Glow(Gold));
        float px = sx - 0.07f;
        Gfx.Text("Kademe_Baslik", t, new Vector3(px, 6.55f, sz), side, Loc.T("KADEMELER"), 0.32f, Gold);
        Gfx.Text("Kademe_Alt", t, new Vector3(px, 6.12f, sz), side, Loc.T("Her oyuncu kendi kademesindekilerle yarışır · satıra dokun, sıralamasını gör"), 0.1f, Dim);
        for (int i = 0; i < tiers.Length; i++)
        {
            int no = i + 1;
            float y = 5.6f - i * 0.47f;
            var (bg, tx) = Button(t, "Kademe_Satir_" + no, new Vector3(px, y, sz), new Vector3(0.04f, 0.4f, sw - 0.4f), "", 0.15f, side,
                () => w.Select(no), true);
            w.tierRows.Add((no, bg, tx));
        }
        w.Paint();
        return w;
    }

    /// <summary>Duvara yaslı, tıklanabilir düz düğme (arka plan kutusu + yazı).</summary>
    static (Renderer, TextMesh) Button(Transform t, string name, Vector3 pos, Vector3 size, string text, float h, Quaternion rot,
        System.Action click, bool leftAlign = false)
    {
        var b = Gfx.Box(name, t, pos, size, Gfx.Lit(BtnOff, 0.3f));
        var col = b.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1f, 1f) + new Vector3(size.x < 0.1f ? 6f : 0f, 0f, size.z < 0.1f ? 6f : 0f); // ince kenarda da yakalansın
        var c = b.AddComponent<Clickable3D>();
        c.onClick = click;
        c.maxDistance = 16f;
        Vector3 off = rot * Vector3.back * 0.035f; // yazı kutunun önünde
        var tx = leftAlign
            ? Gfx.Text(name + "_Yazi", t, pos + off + rot * Vector3.left * (Mathf.Max(size.x, size.z) / 2f - 0.15f), rot, text, h, Color.white, TextAnchor.MiddleLeft, TextAlignment.Left)
            : Gfx.Text(name + "_Yazi", t, pos + off, rot, text, h, Color.white);
        return (b.GetComponent<Renderer>(), tx);
    }

    // ---------------------------------------------------------------- etkileşim

    public void Select(int no)
    {
        if (tier == no) return;
        tier = no;
        dirty = true;
        Paint();
        if (Bell.I) Bell.I.Tick();
    }

    void SetPeriod(string key)
    {
        if (period == key) return;
        period = key;
        dirty = true;
        Paint();
        if (Bell.I) Bell.I.Tick();
    }

    // ---------------------------------------------------------------- veri

    void Update()
    {
        var m = Market.I;
        var cam = Camera.main;
        if (m == null || cam == null) return;
        if ((cam.transform.position - center).sqrMagnitude > 26f * 26f) return;
        if (!m.Tm || !m.Live)
        {
            if (note.text == "" || rowL[0].text != "") ShowMessage(m.Tm ? Loc.T("Sıralamayı görmek için Trade of MTBS hesabınla giriş yap.") : Loc.T("Sıralama Trade of MTBS hesabıyla görünür."));
            return;
        }
        if (busy) return;
        if (!dirty && Time.time - fetchedAt < 90f) return;
        Load();
    }

    void ShowMessage(string msg)
    {
        for (int r = 0; r < Rows; r++) { rowL[r].text = ""; rowR[r].text = ""; }
        mine.text = "";
        note.text = msg;
        title.text = Loc.T("SIRALAMA");
        dirty = true;
    }

    void Load()
    {
        busy = true;
        dirty = false;
        fetchedAt = Time.time;
        int askTier = tier;
        string askPeriod = period;
        string path = "/tmtbs/siralama?donem=" + askPeriod + (askTier > 0 ? "&kademe=" + askTier : "");
        Market.I.Get(path, (d, err) =>
        {
            busy = false;
            if (this == null) return;
            if (askTier != tier || askPeriod != period) { dirty = true; return; } // bu arada seçim değişti
            if (d == null) { ShowMessage(err ?? Loc.T("Sıralama alınamadı.")); fetchedAt = Time.time - 60f; dirty = false; return; }
            int bk = (int)J.D(d, "benim_kademem");
            if (bk <= 0) bk = SalonDirector.TierOf(Market.I.net);
            myTier = bk;
            if (tier == 0) tier = bk; // ilk açılış: kendi kademem
            counts.Clear();
            foreach (var k in J.A(d, "kademeler")) counts[(int)J.D(k, "no")] = (int)J.D(k, "n");
            Fill(d);
            Paint();
        });
    }

    void Fill(Dictionary<string, object> d)
    {
        var list = J.A(d, "liste");
        for (int r = 0; r < Rows; r++)
        {
            if (r >= list.Count) { rowL[r].text = ""; rowR[r].text = ""; continue; }
            var o = list[r];
            bool me = J.B(o, "ben");
            int s = (int)J.D(o, "sira");
            string medal = s == 1 ? "#FFD54A" : s == 2 ? "#D9DEE7" : s == 3 ? "#E0A06A" : "#C9D1E0";
            string nm = J.S(o, "ad");
            if (nm.Length > 22) nm = nm.Substring(0, 21) + "…";
            rowL[r].text = $"<color={medal}><b>{s}.</b></color>  " + (me ? $"<color=#FFD54A><b>{Loc.T("{0} (sen)", nm)}</b></color>" : nm);
            double kz = J.D(o, "kazanc");
            rowR[r].text = $"<color={Fmt.ChangeColor(kz)}><b>{(kz > 0 ? "+" : "")}{Fmt.N(kz)}</b></color>  <color=#8a93a8>{Loc.T("Net {0}", Fmt.Short(J.D(o, "net")))}</color>";
        }
        var ben = J.Get(d, "ben") as Dictionary<string, object>;
        mine.text = tier == myTier && ben != null && ben.Count > 0
            ? Loc.T("Senin sıran: <b>{0}.</b>", Fmt.N(J.D(ben, "sira")))
            : "";
        note.text = list.Count == 0
            ? Loc.T("Bu kademede henüz oyuncu yok.")
            : Loc.T("{0} itibarıyla net değer artışı · {1} oyuncu", J.S(d, "baslangic"), Fmt.N(J.D(d, "oyuncu_sayisi")));
    }

    void Paint()
    {
        var tiers = SalonDirector.Tiers;
        int show = tier > 0 ? tier : myTier;
        string pname = "";
        foreach (var p in Periods) if (p.key == period) pname = Loc.T(p.name);
        if (show > 0)
            title.text = Loc.T("SIRALAMA · {0}", Loc.T(tiers[show - 1].ad).ToUpperInvariant()) + "  <size=40>" + pname + (show == myTier ? Loc.T(" · senin kademen") : "") + "</size>";
        else title.text = Loc.T("SIRALAMA");
        foreach (var (no, bg, tx) in tierBtns) PaintBtn(bg, tx, no == show, no == myTier);
        foreach (var (key, bg, tx) in periodBtns) PaintBtn(bg, tx, key == period, false);
        for (int i = 0; i < tierRows.Count; i++)
        {
            var (no, bg, tx) = tierRows[i];
            string cnt = counts.TryGetValue(no, out var c) ? Loc.T(" · {0} oyuncu", Fmt.N(c)) : "";
            string mark = no == myTier ? Loc.T("  ← senin kademen") : "";
            tx.text = $"<b>{no}. {Loc.T(tiers[i].ad)}</b>  <color=#8a93a8>{Fmt.Short(tiers[i].esik)}+ MLS{cnt}</color><color=#FFD54A>{mark}</color>";
            PaintBtn(bg, tx, no == show, no == myTier);
        }
    }

    static void PaintBtn(Renderer bg, TextMesh tx, bool on, bool mineMark)
    {
        if (bg) bg.sharedMaterial = on ? Gfx.Glow(Gold * 0.8f) : mineMark ? Gfx.Lit(new Color(0.32f, 0.26f, 0.08f), 0.3f) : Gfx.Lit(BtnOff, 0.3f);
        if (tx) tx.color = on ? new Color(0.06f, 0.06f, 0.09f) : Color.white;
    }
}

/// <summary>Dünyada tıklanabilir / dokunulabilir nesne (oyuncu maxDistance içindeyse onClick çalışır).</summary>
public class Clickable3D : MonoBehaviour
{
    public System.Action onClick;
    public float maxDistance = 8f;
}
