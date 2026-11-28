using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Borsa yönetim birimi (kürsü): ön duvarın önünde sahne, konuşmacı, bronz gong ve duyuru şeridi.
/// Sunucudaki herkese açık borsa haberlerini (/tmtbs/salon/duyuru: halka arz, sermaye artırımı, bölünme,
/// temettü, fabrika…) 30 sn'de bir okur; yeni haber gelince gong çalınır, konuşmacı duyurur, şerit ve ekranın
/// üstünde duyuru görünür. Her saat başı da seans gongu çalar. Yaklaşınca E ile duyuru geçmişi açılır.
/// </summary>
public class Podium : MonoBehaviour
{
    public static Podium I { get; private set; }
    public static event System.Action<string> Announced;

    public readonly List<string> History = new List<string>();
    public TextMesh strip, bubble;
    public Transform gong;
    public AudioSource gongSrc, voiceSrc;

    AudioClip gongClip, chimeClip;
    long lastTs = -1;
    float nextPoll = 2f, nextMovers = 60f;
    int lastHour = -1;
    readonly Queue<string> pending = new Queue<string>();
    float nextAnnounce, swingT = -10f, bubbleUntil;
    string stripText = "";

    public static void Build(Transform root, float front)
    {
        var holder = new GameObject("Kursu");
        holder.transform.SetParent(root, false);
        var p = holder.AddComponent<Podium>();
        Transform t = holder.transform;
        float zc = front - 3.2f;         // sahne merkezi
        float zf = zc - 1.8f;            // sahne ön yüzü
        var marble = Gfx.Lit(new Color(0.07f, 0.08f, 0.11f), 0.85f);
        var gold = Gfx.Glow(new Color(1f, 0.78f, 0.3f));
        var bronze = Gfx.Lit(new Color(0.72f, 0.48f, 0.18f), 0.9f);
        var wood = Gfx.Lit(new Color(0.24f, 0.13f, 0.07f), 0.55f);

        // Sahne ve kenar süsleri
        Gfx.Box("Sahne", t, new Vector3(0, 0.45f, zc), new Vector3(11f, 0.9f, 3.6f), marble, true);
        Gfx.Box("Sahne_Altin", t, new Vector3(0, 0.91f, zf + 0.04f), new Vector3(11.05f, 0.05f, 0.08f), gold);
        Gfx.Box("Sahne_Hali", t, new Vector3(0, 0.905f, zc), new Vector3(9.5f, 0.01f, 2.8f), Gfx.Lit(new Color(0.42f, 0.06f, 0.09f), 0.1f));
        // Duyuru şeridi (sahnenin ön yüzünde LED bant)
        Gfx.Box("Serit", t, new Vector3(0, 0.45f, zf - 0.02f), new Vector3(10.6f, 0.5f, 0.04f), Gfx.Lit(Color.black, 0.95f));
        p.strip = Gfx.Text("Serit_Yazi", t, new Vector3(0, 0.45f, zf - 0.06f), Quaternion.identity,
            "", 0.24f, new Color(1f, 0.82f, 0.35f));

        // Kürsü (konuşma masası) ve tabela
        Gfx.Box("Kursu_Masa", t, new Vector3(-1.6f, 1.45f, zc - 0.5f), new Vector3(1.1f, 1.1f, 0.6f), wood, true);
        Gfx.Box("Kursu_Ust", t, new Vector3(-1.6f, 2.03f, zc - 0.55f), new Vector3(1.25f, 0.06f, 0.75f), wood);
        Gfx.Box("Kursu_Plaka", t, new Vector3(-1.6f, 1.55f, zc - 0.81f), new Vector3(0.9f, 0.32f, 0.02f), gold);
        Gfx.Text("Kursu_Yazi", t, new Vector3(-1.6f, 1.55f, zc - 0.83f), Quaternion.identity, Loc.T("BORSA YÖNETİMİ"), 0.13f, new Color(0.15f, 0.1f, 0.03f));
        Gfx.Text("Kursu_Baslik", t, new Vector3(0, 2.95f, front - 0.12f), Quaternion.identity,
            Loc.T("BORSA YÖNETİM KURULU  ·  DUYURU KÜRSÜSÜ"), 0.3f, new Color(1f, 0.8f, 0.3f));

        // Gong: iki dikme + üst kiriş + asılı bronz disk + tokmak
        float gx = 2.6f, gy = 0.9f;
        Gfx.Box("Gong_Dikme", t, new Vector3(gx - 1.0f, gy + 1.15f, zc), new Vector3(0.14f, 2.3f, 0.14f), wood, true);
        Gfx.Box("Gong_Dikme", t, new Vector3(gx + 1.0f, gy + 1.15f, zc), new Vector3(0.14f, 2.3f, 0.14f), wood, true);
        Gfx.Box("Gong_Kiris", t, new Vector3(gx, gy + 2.3f, zc), new Vector3(2.3f, 0.16f, 0.18f), wood);
        Gfx.Box("Gong_Kiris_Altin", t, new Vector3(gx, gy + 2.4f, zc), new Vector3(2.35f, 0.04f, 0.2f), gold);
        var pivot = new GameObject("Gong_Pivot").transform;
        pivot.SetParent(t, false);
        pivot.localPosition = new Vector3(gx, gy + 2.2f, zc);
        Gfx.Box("Gong_Ip", pivot, new Vector3(-0.35f, -0.2f, 0), new Vector3(0.03f, 0.4f, 0.03f), Gfx.Lit(Color.black, 0.2f));
        Gfx.Box("Gong_Ip", pivot, new Vector3(0.35f, -0.2f, 0), new Vector3(0.03f, 0.4f, 0.03f), Gfx.Lit(Color.black, 0.2f));
        var disc = Gfx.Prim(PrimitiveType.Cylinder, "Gong_Disk", pivot, new Vector3(0, -0.95f, 0), new Vector3(1.35f, 0.035f, 1.35f), bronze, false);
        disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var boss = Gfx.Prim(PrimitiveType.Sphere, "Gong_Gobek", pivot, new Vector3(0, -0.95f, -0.04f), new Vector3(0.28f, 0.28f, 0.08f), gold, false);
        boss.layer = disc.layer = 2;
        p.gong = pivot;
        Gfx.Box("Tokmak", t, new Vector3(gx + 0.85f, gy + 0.65f, zc - 0.35f), new Vector3(0.06f, 0.9f, 0.06f), wood);
        Gfx.Prim(PrimitiveType.Sphere, "Tokmak_Bas", t, new Vector3(gx + 0.85f, gy + 1.12f, zc - 0.35f), Vector3.one * 0.2f, Gfx.Lit(new Color(0.9f, 0.85f, 0.75f), 0.2f), false);

        // Konuşmacı (borsa yönetimi temsilcisi)
        var host = new GameObject("Konusmaci");
        host.transform.SetParent(t, false);
        host.transform.localPosition = new Vector3(-1.6f, 0.9f, zc + 0.15f);
        host.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        host.layer = 2;
        var lc = host.AddComponent<LazyChar>();
        lc.model = 1;
        var look = Look.Random(new System.Random(42));
        look.uniform = true;
        lc.look = look;
        lc.idleState = 3;
        p.bubble = Gfx.Text("Konusmaci_Soz", host.transform, new Vector3(0, 2.4f, 0), Quaternion.identity, "", 0.2f, new Color(1f, 0.85f, 0.4f));
        p.bubble.gameObject.AddComponent<Billboard>();
        p.bubble.gameObject.SetActive(false);

        // Spot ışığı sahneye
        var lg = new GameObject("Kursu_Isik");
        lg.transform.SetParent(t, false);
        lg.transform.localPosition = new Vector3(0, 6f, zc - 4f);
        lg.transform.localRotation = Quaternion.Euler(52, 0, 0);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Spot;
        l.range = 14f;
        l.spotAngle = 60f;
        l.intensity = 2.2f;
        l.color = new Color(1f, 0.9f, 0.75f);

        // Yaklaşınca duyuru geçmişi
        var it = new GameObject("Kursu_Nokta");
        it.transform.SetParent(t, false);
        it.transform.localPosition = new Vector3(0, 0, zf - 1.2f);
        Interactable.Make(it, Interactable.Kind.Podium, Vector3.zero, Loc.T("Borsa yönetimi · duyurular"), Loc.T("DUYURU"), 3.2f);

        // Ses kaynakları (3B: yaklaştıkça güçlenir)
        p.gongSrc = pivot.gameObject.AddComponent<AudioSource>();
        Ambience.Spatial(p.gongSrc, 4f, 70f);
        p.voiceSrc = host.AddComponent<AudioSource>();
        Ambience.Spatial(p.voiceSrc, 3f, 40f);
    }

    void Awake() { I = this; }

    void Start()
    {
        gongClip = Sfx.Get("gong");
        chimeClip = Sfx.Get("duyuru");
        SetStrip(Loc.T("Trade of MTBS borsasına hoş geldiniz · Seans açık · İyi işlemler!"));
    }

    void Update()
    {
        if (Time.time >= nextPoll && Api.I != null) Poll();

        // Saat başı seans gongu
        int h = System.DateTime.Now.Hour;
        if (lastHour < 0) lastHour = h;
        else if (h != lastHour)
        {
            lastHour = h;
            pending.Enqueue(Loc.T("Saat {0}.00 · seans devam ediyor. Bol kazançlar!", (h).ToString("00")));
        }

        // Ara sıra günün öne çıkanları (gongsuz, kısa zil)
        if (Time.time >= nextMovers)
        {
            nextMovers = Time.time + 150f + Random.value * 90f;
            var m = MoversLine();
            if (m != null) Say(m, false);
        }

        if (pending.Count > 0 && Time.time >= nextAnnounce)
        {
            nextAnnounce = Time.time + 9f;
            Say(pending.Dequeue(), true);
        }

        // Gong sallanması
        float st = Time.time - swingT;
        if (gong != null)
            gong.localRotation = st < 6f ? Quaternion.Euler(Mathf.Sin(st * 7f) * 9f * Mathf.Exp(-st * 0.9f), 0, 0) : Quaternion.identity;

        if (bubble != null && bubble.gameObject.activeSelf && Time.time > bubbleUntil) bubble.gameObject.SetActive(false);
    }

    void Poll()
    {
        nextPoll = Time.time + 30f;
        Api.I.Get("/tmtbs/salon/duyuru", r =>
        {
            var list = J.A(r, "haberler");
            long max = lastTs;
            var fresh = new List<(long ts, string line)>();
            foreach (var n in list)
            {
                long ts = J.L(n, "ts");
                if (ts > max) max = ts;
                string line = Line(n);
                if (lastTs >= 0 && ts > lastTs) fresh.Add((ts, line));
                if (lastTs < 0 && History.Count < 8) History.Add(line);
            }
            fresh.Sort((a, b) => a.ts.CompareTo(b.ts));
            foreach (var f in fresh) { pending.Enqueue(f.line); History.Insert(0, f.line); }
            while (History.Count > 30) History.RemoveAt(History.Count - 1);
            if (lastTs < 0 && History.Count > 0) SetStrip(History[0]);
            lastTs = max < 0 ? 0 : max;
        }, (m, c) => { nextPoll = Time.time + (c == 404 ? 300f : 60f); });
    }

    static string Line(object n)
    {
        string isim = J.S(n, "isim");
        switch (J.S(n, "tur"))
        {
            case "arz": return $"{isim} halka arz oldu: {Fmt.N(J.D(n, "adet"))} hisse, {Fmt.N2(J.D(n, "fiyat"))} MLS'den.";
            case "artirim": return $"{isim} sermaye artırımı yaptı: {Fmt.N(J.D(n, "adet"))} yeni hisse.";
            case "temettu": return $"{isim} temettü dağıtıyor: hisse başı {Fmt.N(J.D(n, "hisse_basi"))} MLS.";
            case "bolunme": return $"{isim} hisseleri bölündü: 1'e {Fmt.N(J.D(n, "kat"))}.";
            case "fabrika": return $"{isim} fabrika yatırımı: {Fmt.N(J.D(n, "seviye"))}. seviye.";
            default: return isim + " · " + Pretty(J.S(n, "tur"));
        }
    }

    static string Pretty(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpper(s[0]) + s.Substring(1).Replace('_', ' ');

    static string MoversLine()
    {
        if (Market.I == null || Market.I.stocks == null || Market.I.stocks.Count == 0) return null;
        Stock up = null, down = null;
        foreach (var s in Market.I.stocks)
        {
            if (s.change == 0) continue;
            if (up == null || s.change > up.change) up = s;
            if (down == null || s.change < down.change) down = s;
        }
        if (up == null) return null;
        string t = Loc.T("Günün yükseleni {0} {1}", up.name, Fmt.Pct(up.change));
        if (down != null && down != up && down.change < 0) t += Loc.T(", düşeni {0} {1}", down.name, Fmt.Pct(down.change));
        return t + ".";
    }

    void Say(string text, bool withGong)
    {
        if (withGong)
        {
            swingT = Time.time;
            if (gongSrc && gongClip) gongSrc.PlayOneShot(gongClip, 1f);
        }
        else if (gongSrc && chimeClip) gongSrc.PlayOneShot(chimeClip, 0.7f);
        if (voiceSrc) Ambience.I?.Announce(voiceSrc, text.Length);
        if (bubble != null)
        {
            bubble.text = Wrap(text, 34);
            bubble.gameObject.SetActive(true);
            bubbleUntil = Time.time + 8f;
        }
        SetStrip(text);
        if (withGong && !History.Contains(text)) { History.Insert(0, text); if (History.Count > 30) History.RemoveAt(30); }
        Announced?.Invoke(text);
    }

    void SetStrip(string t)
    {
        stripText = t.Length > 70 ? t.Substring(0, 68) + "..." : t;
        if (strip != null) strip.text = stripText;
    }

    static string Wrap(string s, int n)
    {
        var sb = new System.Text.StringBuilder();
        int col = 0;
        foreach (var w in s.Split(' '))
        {
            if (col > 0 && col + w.Length > n) { sb.Append('\n'); col = 0; }
            else if (col > 0) { sb.Append(' '); col++; }
            sb.Append(w);
            col += w.Length;
        }
        return sb.ToString();
    }
}
