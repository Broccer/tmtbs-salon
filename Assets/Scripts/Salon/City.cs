using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Meydanın güneyindeki şehir: doğu-batı caddesi, dolaşan otobüs, duraklar ve yanlarındaki büfeler (aylık bilet + yemek),
/// karşı kaldırımda İş Bulma Merkezi, Emlak Ofisi, Araba Galerisi, Müzayede Salonu &amp; Galeri ve markaların dükkânları.
/// Bütün işlemler Müteşebbis sunucusundaki gerçek uçlarla yapılır (SalonDirector.World).
/// </summary>
public static class City
{
    public const float AvenueZ = -100f;          // cadde ortası
    const float RoadHalf = 5f;                   // cadde yarı genişliği
    const float WalkW = 3.6f;                    // kaldırım genişliği
    public static float NorthWalkZ => AvenueZ + RoadHalf + WalkW / 2f;
    static float SouthFrontZ => AvenueZ - RoadHalf - WalkW;   // binaların ön yüzü
    public const float HalfLen = 82f;

    public struct Stop { public string name; public float x; }
    public static readonly Stop[] Stops =
    {
        new Stop { name = "Evler · İş Bulma Merkezi", x = -46f },
        new Stop { name = "Borsa Meydanı · Galeri · Müzayede", x = -2f },
        new Stop { name = "MTBS Bankası · Çarşı", x = 30f },
        new Stop { name = "Çarşı sonu · Dükkânlar", x = 64f },
    };
    public static Vector3 StopPoint(int i) => new Vector3(Stops[i].x - 1.8f, 0, NorthWalkZ);

    public const int ShopCount = 5;
    public static readonly TextMesh[] ShopSigns = new TextMesh[ShopCount];
    public static readonly long[] ShopIds = new long[ShopCount];
    public static readonly string[] ShopNames = new string[ShopCount];
    public static readonly List<TextMesh> ArtLabels = new List<TextMesh>();

    static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
    static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f);

    public static void Build(Transform root)
    {
        var t = new GameObject("Sehir").transform;
        t.SetParent(root, false);
        BuildStreet(t);
        for (int i = 0; i < Stops.Length; i++) BuildStop(t, i);

        var bus = new GameObject("Otobus").AddComponent<Bus>();
        bus.transform.SetParent(t, false);

        BuildJobCenter(t, -62f);
        BuildEstate(t, -38f);
        BuildCarDealer(t, -14f);
        BuildAuction(t, 14f);
        for (int i = 0; i < ShopCount; i++) BuildShop(t, i, 34f + i * 10f);
    }

    // ================================================================ cadde

    static void BuildStreet(Transform t)
    {
        var asphalt = Gfx.Lit(new Color(0.16f, 0.17f, 0.19f), 0.25f);
        var walk = Gfx.Lit(new Color(0.66f, 0.63f, 0.58f), 0.12f);
        var curb = Gfx.Lit(new Color(0.42f, 0.41f, 0.40f), 0.1f);
        var line = Gfx.Lit(new Color(0.95f, 0.85f, 0.35f), 0.1f);
        float L = HalfLen * 2f;
        Gfx.Box("Cadde", t, new Vector3(0, 0.005f, AvenueZ), new Vector3(L, 0.01f, RoadHalf * 2f), asphalt);
        for (float x = -HalfLen + 2f; x < HalfLen; x += 6f)
            Gfx.Box("Serit", t, new Vector3(x, 0.012f, AvenueZ), new Vector3(3f, 0.005f, 0.18f), line);
        foreach (int s in new[] { -1, 1 })
        {
            float z = AvenueZ + s * (RoadHalf + WalkW / 2f);
            Gfx.Box("Kaldirim", t, new Vector3(0, 0.07f, z), new Vector3(L, 0.14f, WalkW), walk);
            Gfx.Box("Bordur", t, new Vector3(0, 0.08f, AvenueZ + s * (RoadHalf + 0.1f)), new Vector3(L, 0.16f, 0.22f), curb);
        }
        // Meydandan caddeye taş yol
        float z0 = SeljukBuilding.CrossZ - 3f, z1 = AvenueZ + RoadHalf + WalkW;
        Gfx.Box("Yol_Cadde", t, new Vector3(0, 0.004f, (z0 + z1) / 2f), new Vector3(3f, 0.01f, z0 - z1), Gfx.Lit(new Color(0.62f, 0.58f, 0.52f), 0.15f));
        // Yaya geçidi
        for (int i = -4; i <= 4; i++)
            Gfx.Box("Gecit", t, new Vector3(i * 0.7f, 0.013f, AvenueZ), new Vector3(0.4f, 0.005f, RoadHalf * 2f - 0.6f), Gfx.Lit(Color.white, 0.1f));
        // Sokak lambaları ve ağaçlar
        var pole = Gfx.Lit(new Color(0.15f, 0.16f, 0.2f), 0.6f);
        var leaf = Gfx.Lit(new Color(0.20f, 0.45f, 0.22f), 0.1f);
        var bark = Gfx.Lit(new Color(0.35f, 0.24f, 0.15f), 0.1f);
        for (float x = -HalfLen + 6f; x < HalfLen; x += 16f)
            foreach (int s in new[] { -1, 1 })
            {
                float z = AvenueZ + s * (RoadHalf + 0.6f);
                if (s > 0 && NearStop(x, 6f)) continue;
                Gfx.Prim(PrimitiveType.Cylinder, "Lamba_Direk", t, new Vector3(x, 2.2f, z), new Vector3(0.16f, 2.2f, 0.16f), pole, true);
                Gfx.Prim(PrimitiveType.Sphere, "Lamba", t, new Vector3(x, 4.5f, z), Vector3.one * 0.5f, Gfx.Glow(new Color(1f, 0.92f, 0.7f)), false);
                float tx = x + 8f;
                if (s > 0 && NearStop(tx, 7f)) continue;
                Gfx.Prim(PrimitiveType.Cylinder, "Agac_Govde", t, new Vector3(tx, 1.2f, z), new Vector3(0.3f, 1.2f, 0.3f), bark, true);
                Gfx.Prim(PrimitiveType.Sphere, "Agac_Yaprak", t, new Vector3(tx, 3.2f, z), new Vector3(2.4f, 2.2f, 2.4f), leaf, false);
            }
    }

    static bool NearStop(float x, float d)
    {
        foreach (var s in Stops) if (Mathf.Abs(s.x - x) < d) return true;
        return false;
    }

    // ================================================================ durak + büfe

    static void BuildStop(Transform root, int i)
    {
        var st = Stops[i];
        var t = new GameObject("Durak_" + i).transform;
        t.SetParent(root, false);
        t.position = new Vector3(st.x, 0.14f, NorthWalkZ);
        var frame = Gfx.Lit(new Color(0.12f, 0.35f, 0.55f), 0.5f);
        var glass = Gfx.Glow(new Color(0.55f, 0.75f, 0.85f) * 0.25f);
        // Sundurma: arka cam, çatı, iki direk, bank
        Gfx.Box("Durak_Cati", t, new Vector3(0, 2.7f, 0.4f), new Vector3(4.6f, 0.12f, 1.8f), frame);
        Gfx.Box("Durak_Arka", t, new Vector3(0, 1.4f, 1.2f), new Vector3(4.4f, 2.4f, 0.05f), glass, true);
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Durak_Direk", t, new Vector3(s * 2.2f, 1.35f, 1.2f), new Vector3(0.1f, 2.7f, 0.1f), frame);
        Gfx.Box("Durak_Bank", t, new Vector3(0, 0.45f, 0.85f), new Vector3(3f, 0.08f, 0.45f), Gfx.Lit(new Color(0.45f, 0.3f, 0.18f), 0.3f), true);
        // Durak tabelası (caddeden okunur: cadde güneyde, okuyan kuzeye bakar)
        Gfx.Prim(PrimitiveType.Cylinder, "Tabela_Direk", t, new Vector3(-2.8f, 1.5f, -0.6f), new Vector3(0.08f, 1.5f, 0.08f), frame, false);
        Gfx.Box("Tabela", t, new Vector3(-2.8f, 3.1f, -0.6f), new Vector3(0.9f, 0.9f, 0.06f), Gfx.Lit(new Color(0.95f, 0.75f, 0.1f), 0.3f));
        Gfx.Text("Tabela_H", t, new Vector3(-2.8f, 3.1f, -0.64f), Quaternion.identity, "<b>H</b>", 0.6f, new Color(0.1f, 0.25f, 0.1f));
        Gfx.Text("Tabela_H2", t, new Vector3(-2.8f, 3.1f, -0.56f), Quaternion.Euler(0, 180f, 0), "<b>H</b>", 0.6f, new Color(0.1f, 0.25f, 0.1f));
        Gfx.Box("Isim_Pano", t, new Vector3(0, 3.1f, 0.4f), new Vector3(4.4f, 0.5f, 0.05f), Gfx.Lit(Panel, 0.8f));
        Gfx.Text("Isim", t, new Vector3(0, 3.1f, 0.36f), Quaternion.identity, Loc.T("DURAK · ") + st.name, 0.18f, Gold);
        Gfx.Text("Isim2", t, new Vector3(0, 3.1f, 0.44f), Quaternion.Euler(0, 180f, 0), Loc.T("DURAK · ") + st.name, 0.18f, Gold);

        var stopGo = new GameObject("Durak_Nokta");
        stopGo.transform.SetParent(t, false);
        stopGo.transform.localPosition = new Vector3(0, 0, 0.3f);
        stopGo.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        Interactable.Make(stopGo, Interactable.Kind.BusStop, new Vector3(0, 0, 0.5f), Loc.T("Otobüse bin · ") + st.name, Loc.T("BİN"), 2.6f).arg = i;

        // Büfe (durağın doğusunda): küçük kulübe, tezgâh, görevli
        var b = new GameObject("Bufe").transform;
        b.SetParent(t, false);
        b.localPosition = new Vector3(5.2f, 0, 0.6f);
        var red = Gfx.Lit(new Color(0.75f, 0.18f, 0.15f), 0.3f);
        var white = Gfx.Lit(new Color(0.95f, 0.94f, 0.9f), 0.2f);
        Gfx.Box("Bufe_Arka", b, new Vector3(0, 1.3f, 1.4f), new Vector3(2.6f, 2.6f, 0.12f), white, true);
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Bufe_Yan", b, new Vector3(s * 1.3f, 1.3f, 0.6f), new Vector3(0.12f, 2.6f, 1.7f), white, true);
        Gfx.Box("Bufe_Alt", b, new Vector3(0, 0.45f, -0.2f), new Vector3(2.6f, 0.9f, 0.12f), white, true);
        Gfx.Box("Bufe_Tezgah", b, new Vector3(0, 1.0f, -0.3f), new Vector3(2.6f, 0.1f, 0.6f), red);
        Gfx.Box("Bufe_Cati", b, new Vector3(0, 2.75f, 0.2f), new Vector3(3.0f, 0.3f, 2.6f), red);
        Gfx.Text("Bufe_Yazi", b, new Vector3(0, 2.75f, -1.12f), Quaternion.identity, Loc.T("BÜFE · bilet · yemek"), 0.2f, Color.white);
        var clerk = Person(b, new Vector3(0, 0, 0.55f), Quaternion.LookRotation(Vector3.back), i % 2 == 0, 3, Loc.T("Bilet, yemek? Buyur!"));
        var desk = new GameObject("Bufe_Nokta");
        desk.transform.SetParent(b, false);
        desk.transform.localPosition = new Vector3(0, 0, -0.3f);
        desk.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        Interactable.Make(desk, Interactable.Kind.Buffet, new Vector3(0, 0, 0.9f), Loc.T("Büfe: aylık otobüs bileti ve yemek"), Loc.T("BÜFE"), 2.2f).arg = i;
        clerk.name = "Bufeci";
    }

    // ================================================================ yardımcılar

    /// <summary>Kuzeye (caddeye) bakan dikdörtgen bina: yerel +z ön yüz, ortada kapı. Kökü döndürür.</summary>
    static Transform Building(Transform root, string name, float x, float w, float d, float h, Color wallColor, Color floorColor, string title, string sub)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        t.position = new Vector3(x, 0, SouthFrontZ - d / 2f - 0.3f);
        t.rotation = Quaternion.Euler(0, 180f, 0); // yerel -z kuzeye (caddeye) bakar: kapı ve tabela orada
        var wall = Gfx.Lit(wallColor, 0.15f);
        float th = 0.35f, doorW = 3.2f, doorH = 3.4f;
        Gfx.Box("Zemin", t, new Vector3(0, 0.02f, 0), new Vector3(w, 0.04f, d), Gfx.Lit(floorColor, 0.5f));
        Gfx.Box("Tavan", t, new Vector3(0, h + th / 2f, 0), new Vector3(w + th * 2f, th, d + th * 2f), Gfx.Lit(wallColor * 0.85f, 0.1f), true);
        Gfx.Box("Arka", t, new Vector3(0, h / 2f, d / 2f + th / 2f), new Vector3(w + th * 2f, h, th), wall, true);
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Yan", t, new Vector3(s * (w / 2f + th / 2f), h / 2f, 0), new Vector3(th, h, d), wall, true);
        float side = (w - doorW) / 2f, fz = -d / 2f - th / 2f;
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("On", t, new Vector3(s * (doorW / 2f + side / 2f), h / 2f, fz), new Vector3(side, h, th), wall, true);
        Gfx.Box("Lento", t, new Vector3(0, doorH + (h - doorH) / 2f, fz), new Vector3(doorW, h - doorH, th), wall, true);
        // Vitrin camları
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Vitrin", t, new Vector3(s * (doorW / 2f + side / 2f), 1.9f, fz - th / 2f - 0.01f), new Vector3(side * 0.7f, 2f, 0.03f), Gfx.Glow(new Color(1f, 0.9f, 0.7f) * 0.45f));
        // Tabela (caddeden okunur: okuyan güneye, yani yerel +z'ye bakar → metin dönüşü yerel identity)
        float sy = Mathf.Min(h - 0.6f, doorH + 1.1f);
        Gfx.Box("Tabela", t, new Vector3(0, sy, fz - th / 2f - 0.05f), new Vector3(Mathf.Min(w - 1f, 12f), 1.3f, 0.08f), Gfx.Lit(Panel, 0.85f));
        Gfx.Text("Tabela_Yazi", t, new Vector3(0, sy + 0.15f, fz - th / 2f - 0.1f), Quaternion.identity, title, 0.5f, Gold);
        if (!string.IsNullOrEmpty(sub))
            Gfx.Text("Tabela_Alt", t, new Vector3(0, sy - 0.38f, fz - th / 2f - 0.1f), Quaternion.identity, sub, 0.2f, Color.white);
        Light(t, new Vector3(0, h - 0.8f, 0), Mathf.Max(w, d) * 0.9f, 1.25f);
        return t;
    }

    static void Light(Transform t, Vector3 local, float range, float intensity)
    {
        var lg = new GameObject("Lamba");
        lg.transform.SetParent(t, false);
        lg.transform.localPosition = local;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = new Color(1f, 0.94f, 0.84f);
    }

    /// <summary>Görevli/yaşayan karakter (yakına gelince konuşma balonu).</summary>
    public static GameObject Person(Transform parent, Vector3 local, Quaternion rot, bool female, int idleState, string says, int model = -1)
    {
        var g = new GameObject("Kisi");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = local;
        g.transform.localRotation = rot;
        var lc = g.AddComponent<LazyChar>();
        lc.model = model >= 0 ? model : (female ? 0 : 1);
        lc.look = new Look { female = female, skin = 1 + Mathf.Abs(says.Length) % 3, hairStyle = 1, hairColor = 1 + says.Length % 2, uniform = model < 0 };
        lc.idleState = idleState;
        if (!string.IsNullOrEmpty(says))
        {
            var bubble = Gfx.Text("Soz", g.transform, new Vector3(0, 2.2f, 0), Quaternion.identity, says, 0.2f, Gfx.Hex("#FFD54A"));
            bubble.gameObject.AddComponent<Billboard>();
            bubble.gameObject.SetActive(false);
            g.AddComponent<Greeter>().bubble = bubble;
        }
        return g;
    }

    /// <summary>Tezgâh + arkasında görevli + önünde kullanılabilir nokta (yerel koordinatta, kapıya bakan).</summary>
    static void Desk(Transform t, Vector3 local, Interactable.Kind kind, string prompt, string button, bool female, string says, int arg = 0)
    {
        var navy = Gfx.Lit(new Color(0.08f, 0.14f, 0.3f), 0.4f);
        Gfx.Box("Tezgah", t, local + new Vector3(0, 0.55f, 0), new Vector3(3f, 1.1f, 0.8f), navy, true);
        Gfx.Box("Tezgah_Ust", t, local + new Vector3(0, 1.13f, 0), new Vector3(3.1f, 0.06f, 0.9f), Gfx.Lit(new Color(0.88f, 0.87f, 0.84f), 0.6f));
        // Kapı yerel -z'de: görevli tezgâhın arkasında (+z), kapıya (-z) bakar
        Person(t, local + new Vector3(0, 0, 0.9f), Quaternion.LookRotation(Vector3.back), female, 3, says);
        var p = new GameObject("Tezgah_Nokta");
        p.transform.SetParent(t, false);
        p.transform.localPosition = local;
        p.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        Interactable.Make(p, kind, new Vector3(0, 0, 1.2f), prompt, button, 2.4f).arg = arg;
    }

    // ================================================================ binalar
    // Building() kökü 180° döndürür: yerel -z kapı (kuzey, cadde), yerel +z arka duvar.

    static void BuildJobCenter(Transform root, float x)
    {
        var t = Building(root, "Is_Bulma_Merkezi", x, 16f, 16f, 5f, new Color(0.80f, 0.84f, 0.88f), new Color(0.35f, 0.38f, 0.42f),
            Loc.T("İŞ BULMA MERKEZİ"), Loc.T("ilanlar · başvuru · kariyer"));
        Desk(t, new Vector3(-3.5f, 0, 3.5f), Interactable.Kind.JobCenter, Loc.T("İş ilanlarına bak, başvur"), Loc.T("İLAN"), true, Loc.T("Hoş geldin! İş mi arıyorsun?"));
        Desk(t, new Vector3(3.5f, 0, 3.5f), Interactable.Kind.JobCenter, Loc.T("Kariyer danışmanı: ilanlar, işten ayrılma"), Loc.T("İLAN"), false, Loc.T("Sana uygun ilanlara bakalım."));
        // İlan panosu (arka duvar)
        Gfx.Box("Ilan_Panosu", t, new Vector3(0, 2.6f, 7.8f), new Vector3(8f, 2.2f, 0.08f), Gfx.Lit(new Color(0.55f, 0.4f, 0.25f), 0.2f));
        var rnd = new System.Random(7);
        for (int i = 0; i < 14; i++)
            Gfx.Box("Ilan", t, new Vector3(-3.4f + (i % 7) * 1.13f, 2.1f + (i / 7) * 1.0f, 7.74f), new Vector3(0.8f, 0.7f, 0.02f),
                Gfx.Lit(new Color(0.9f + (float)rnd.NextDouble() * 0.1f, 0.9f, 0.75f + (float)rnd.NextDouble() * 0.2f), 0.1f));
        Gfx.Text("Pano_Yazi", t, new Vector3(0, 3.95f, 7.72f), Quaternion.identity, Loc.T("GÜNCEL İLANLAR"), 0.3f, Gold);
        // Bekleme sandalyeleri
        for (int i = -2; i <= 2; i++)
            Gfx.Box("Bekleme", t, new Vector3(i * 1.2f, 0.25f, -4f), new Vector3(0.6f, 0.5f, 0.6f), Gfx.Lit(new Color(0.2f, 0.4f, 0.6f), 0.3f), true);
    }

    static void BuildEstate(Transform root, float x)
    {
        var t = Building(root, "Emlak_Ofisi", x, 14f, 14f, 4.5f, new Color(0.93f, 0.88f, 0.78f), new Color(0.48f, 0.34f, 0.22f),
            Loc.T("EMLAK OFİSİ"), Loc.T("satılık · kiralık konut"));
        Desk(t, new Vector3(0, 0, 3.2f), Interactable.Kind.Estate, Loc.T("Emlakçı: ev al, sat ya da kiralık konuta taşın"), Loc.T("EMLAK"), true, Loc.T("Hayalindeki ev burada!"));
        // Duvarda ev maketleri / ilan resimleri
        var roofs = new[] { new Color(0.7f, 0.2f, 0.15f), new Color(0.2f, 0.35f, 0.6f), new Color(0.3f, 0.5f, 0.25f) };
        for (int i = 0; i < 3; i++)
        {
            float lx = -4f + i * 4f;
            Gfx.Box("Maket_Kaide", t, new Vector3(lx, 0.45f, -3.2f), new Vector3(1.6f, 0.9f, 1.2f), Gfx.Lit(new Color(0.25f, 0.25f, 0.28f), 0.4f), true);
            Gfx.Box("Maket_Ev", t, new Vector3(lx, 1.2f, -3.2f), new Vector3(1.0f, 0.6f, 0.8f), Gfx.Lit(new Color(0.95f, 0.92f, 0.85f), 0.2f));
            var r = Gfx.Box("Maket_Cati", t, new Vector3(lx, 1.62f, -3.2f), new Vector3(1.15f, 0.12f, 0.95f), Gfx.Lit(roofs[i], 0.2f));
            r.transform.localRotation = Quaternion.Euler(0, 0, 0);
            Gfx.Box("Ilan_Resim", t, new Vector3(lx, 2.6f, 6.8f), new Vector3(2.4f, 1.6f, 0.05f), Gfx.Lit(roofs[i] * 0.8f + Color.white * 0.2f, 0.1f));
        }
    }

    static void BuildCarDealer(Transform root, float x)
    {
        var t = Building(root, "Araba_Galerisi", x, 20f, 18f, 5.5f, new Color(0.25f, 0.27f, 0.32f), new Color(0.75f, 0.76f, 0.78f),
            Loc.T("ARABA GALERİSİ"), Loc.T("sıfır · ikinci el · ulaşım"));
        Desk(t, new Vector3(6f, 0, 5.5f), Interactable.Kind.CarDealer, Loc.T("Satış danışmanı: araba al ya da sat"), Loc.T("GALERİ"), false, Loc.T("Test sürüşü ister misin?"));
        var colors = new[] { new Color(0.75f, 0.1f, 0.1f), new Color(0.1f, 0.25f, 0.6f), new Color(0.9f, 0.9f, 0.92f), new Color(0.1f, 0.1f, 0.12f) };
        for (int i = 0; i < 4; i++)
        {
            var c = new GameObject("Araba").transform;
            c.SetParent(t, false);
            c.localPosition = new Vector3(-6f + (i % 2) * 7f, 0, -3.5f + (i / 2) * 6f);
            c.localRotation = Quaternion.Euler(0, 30f + i * 25f, 0);
            Car(c, colors[i]);
            Gfx.Box("Podyum", c, new Vector3(0, 0.05f, 0), new Vector3(3f, 0.1f, 5f), Gfx.Lit(new Color(0.15f, 0.15f, 0.17f), 0.8f));
        }
        var p = new GameObject("Araba_Nokta");
        p.transform.SetParent(t, false);
        p.transform.localPosition = new Vector3(-2.5f, 0, -1f);
        p.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        Interactable.Make(p, Interactable.Kind.CarDealer, Vector3.zero, Loc.T("Arabalara bak (galeri)"), Loc.T("GALERİ"), 3f);
    }

    public static void Car(Transform c, Color paint)
    {
        var body = Gfx.Lit(paint, 0.85f);
        var dark = Gfx.Lit(new Color(0.08f, 0.08f, 0.1f), 0.3f);
        Gfx.Box("Govde", c, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.6f, 4.2f), body, true);
        Gfx.Box("Kabin", c, new Vector3(0, 1.1f, -0.2f), new Vector3(1.6f, 0.5f, 2.2f), body);
        Gfx.Box("Cam", c, new Vector3(0, 1.1f, -0.2f), new Vector3(1.62f, 0.4f, 2.0f), Gfx.Glow(new Color(0.4f, 0.55f, 0.65f) * 0.4f));
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Gfx.Prim(PrimitiveType.Cylinder, "Teker", c, new Vector3(sx * 0.85f, 0.35f, sz * 1.35f), new Vector3(0.7f, 0.12f, 0.7f), dark, false)
                    .transform.localRotation = Quaternion.Euler(0, 0, 90f);
        foreach (int sx in new[] { -1, 1 })
            Gfx.Box("Far", c, new Vector3(sx * 0.6f, 0.65f, 2.11f), new Vector3(0.35f, 0.15f, 0.02f), Gfx.Glow(new Color(1f, 0.95f, 0.8f)));
    }

    static void BuildAuction(Transform root, float x)
    {
        var t = Building(root, "Muzayede_Salonu", x, 20f, 18f, 6f, new Color(0.45f, 0.12f, 0.14f), new Color(0.22f, 0.12f, 0.08f),
            Loc.T("MÜZAYEDE SALONU & GALERİ"), Loc.T("eserler · açık artırma"));
        Desk(t, new Vector3(0, 0, 6.2f), Interactable.Kind.Auction, Loc.T("Müzayede: eserleri gör, teklif ver"), Loc.T("TEKLİF"), true, Loc.T("Açık artırma sürüyor!"));
        // Kürsü
        Gfx.Box("Kursu", t, new Vector3(-4f, 0.6f, 5.6f), new Vector3(1f, 1.2f, 0.7f), Gfx.Lit(new Color(0.35f, 0.22f, 0.12f), 0.5f), true);
        // Duvarlarda eser çerçeveleri (adı ve güncel fiyatı sunucudan gelir)
        ArtLabels.Clear();
        var gold = Gfx.Lit(Gold, 0.7f);
        var canvases = new[] { new Color(0.2f, 0.35f, 0.55f), new Color(0.65f, 0.45f, 0.2f), new Color(0.3f, 0.5f, 0.35f), new Color(0.55f, 0.25f, 0.4f), new Color(0.8f, 0.7f, 0.45f), new Color(0.25f, 0.25f, 0.3f) };
        for (int i = 0; i < 6; i++)
        {
            int side = i < 3 ? -1 : 1;
            float z = -4.5f + (i % 3) * 4.5f;
            float wx = side * 9.85f;
            var face = Quaternion.Euler(0, side < 0 ? -90f : 90f, 0); // metnin +z'si duvara doğru
            Gfx.Box("Cerceve", t, new Vector3(wx, 2.5f, z), new Vector3(0.08f, 2.0f, 2.8f), gold);
            Gfx.Box("Tuval", t, new Vector3(wx - side * 0.05f, 2.5f, z), new Vector3(0.02f, 1.7f, 2.5f), Gfx.Lit(canvases[i], 0.05f));
            Gfx.Box("Tuval_Desen", t, new Vector3(wx - side * 0.06f, 2.4f + (i % 2) * 0.3f, z + 0.3f), new Vector3(0.01f, 0.7f, 0.9f), Gfx.Lit(canvases[(i + 2) % 6] * 1.2f, 0.05f));
            var lbl = Gfx.Text("Eser_Etiket", t, new Vector3(wx - side * 0.07f, 1.15f, z), face, Loc.T("Eser"), 0.13f, Color.white);
            ArtLabels.Add(lbl);
            Light(t, new Vector3(wx - side * 1.5f, 4.6f, z), 4f, 0.6f);
        }
        // Seyirci sandalyeleri
        for (int r = 0; r < 3; r++)
            for (int c = -3; c <= 3; c++)
                Gfx.Box("Sandalye", t, new Vector3(c * 1.1f, 0.25f, 1.5f - r * 1.6f), new Vector3(0.55f, 0.5f, 0.55f), Gfx.Lit(new Color(0.55f, 0.1f, 0.12f), 0.3f), true);
    }

    static void BuildShop(Transform root, int i, float x)
    {
        var tints = new[] { new Color(0.95f, 0.85f, 0.6f), new Color(0.7f, 0.85f, 0.95f), new Color(0.85f, 0.95f, 0.75f), new Color(0.95f, 0.75f, 0.8f), new Color(0.85f, 0.8f, 0.95f) };
        var t = Building(root, "Dukkan_" + i, x, 9f, 12f, 4.2f, tints[i % tints.Length], new Color(0.6f, 0.55f, 0.5f), Loc.T("MAĞAZA"), Loc.T("markamızın ürünleri"));
        ShopSigns[i] = t.Find("Tabela_Yazi").GetComponent<TextMesh>();
        ShopSigns[i].characterSize *= 0.6f;
        // Raflar
        var shelf = Gfx.Lit(new Color(0.4f, 0.3f, 0.2f), 0.3f);
        foreach (int s in new[] { -1, 1 })
        {
            Gfx.Box("Raf", t, new Vector3(s * 3.9f, 1.2f, 1f), new Vector3(0.6f, 2.4f, 7f), shelf, true);
            for (int k = 0; k < 8; k++)
                Gfx.Box("Urun", t, new Vector3(s * 3.75f, 0.6f + (k % 4) * 0.55f, -1.5f + (k / 4) * 3.5f), new Vector3(0.3f, 0.35f, 0.9f), Gfx.Lit(Color.HSVToRGB((i * 0.17f + k * 0.11f) % 1f, 0.6f, 0.85f), 0.3f));
        }
        Desk(t, new Vector3(0, 0, 3.8f), Interactable.Kind.Shop, Loc.T("Dükkân: ürünlere bak, satın al"), Loc.T("AL"), i % 2 == 1, Loc.T("Hoş geldiniz!"), i);
    }
}

/// <summary>Caddede dolaşan otobüs: kuzey şeritte doğuya giderken her durakta bekler, güney şeritten döner.</summary>
public class Bus : MonoBehaviour
{
    readonly List<Vector3> path = new List<Vector3>();
    readonly List<bool> stopAt = new List<bool>();
    int target;
    float waitUntil;
    const float Speed = 9f;

    void Start()
    {
        BuildModel();
        float zN = City.AvenueZ + 2.3f, zS = City.AvenueZ - 2.3f;
        float L = City.HalfLen - 4f;
        path.Add(new Vector3(-L, 0, zN)); stopAt.Add(false);
        foreach (var s in City.Stops) { path.Add(new Vector3(s.x + 1.2f, 0, zN)); stopAt.Add(true); }
        path.Add(new Vector3(L, 0, zN)); stopAt.Add(false);
        path.Add(new Vector3(L, 0, zS)); stopAt.Add(false);
        path.Add(new Vector3(-L, 0, zS)); stopAt.Add(false);
        transform.position = path[0];
        target = 1;
    }

    void BuildModel()
    {
        var paint = Gfx.Lit(new Color(0.1f, 0.55f, 0.6f), 0.6f);
        var dark = Gfx.Lit(new Color(0.06f, 0.06f, 0.08f), 0.3f);
        var t = transform;
        Gfx.Box("Govde", t, new Vector3(0, 1.75f, 0), new Vector3(2.5f, 2.7f, 11f), paint);
        Gfx.Box("Serit", t, new Vector3(0, 1.0f, 0), new Vector3(2.52f, 0.25f, 11.02f), Gfx.Lit(new Color(1f, 0.8f, 0.3f), 0.4f));
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Camlar", t, new Vector3(s * 1.26f, 2.2f, 0), new Vector3(0.02f, 1.1f, 10f), Gfx.Glow(new Color(0.6f, 0.8f, 0.9f) * 0.4f));
        Gfx.Box("On_Cam", t, new Vector3(0, 2.1f, 5.51f), new Vector3(2.2f, 1.5f, 0.02f), Gfx.Glow(new Color(0.6f, 0.8f, 0.9f) * 0.4f));
        Gfx.Box("Hat_Pano", t, new Vector3(0, 2.95f, 5.52f), new Vector3(1.8f, 0.35f, 0.02f), dark);
        Gfx.Text("Hat_Yazi", t, new Vector3(0, 2.95f, 5.54f), Quaternion.Euler(0, 180f, 0), Loc.T("1 · ŞEHİR HATTI"), 0.16f, new Color(1f, 0.6f, 0.1f));
        foreach (int sx in new[] { -1, 1 })
            foreach (float sz in new[] { -3.6f, 3.6f })
                Gfx.Prim(PrimitiveType.Cylinder, "Teker", t, new Vector3(sx * 1.15f, 0.5f, sz), new Vector3(1f, 0.18f, 1f), dark, false)
                    .transform.localRotation = Quaternion.Euler(0, 0, 90f);
        foreach (int sx in new[] { -1, 1 })
            Gfx.Box("Far", t, new Vector3(sx * 0.9f, 0.8f, 5.51f), new Vector3(0.4f, 0.2f, 0.02f), Gfx.Glow(new Color(1f, 0.95f, 0.8f)));
    }

    void Update()
    {
        if (path.Count == 0 || Time.time < waitUntil) return;
        Vector3 to = path[target];
        Vector3 d = to - transform.position;
        float step = Speed * Time.deltaTime;
        if (d.magnitude <= step)
        {
            transform.position = to;
            if (stopAt[target]) waitUntil = Time.time + 4f;
            target = (target + 1) % path.Count;
            return;
        }
        Vector3 dir = d.normalized;
        transform.position += dir * step;
        var want = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 140f * Time.deltaTime);
    }
}
