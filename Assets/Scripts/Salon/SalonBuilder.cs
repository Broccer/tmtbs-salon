using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dünyayı koddan kurar:
///  - Seans salonu: duvarlara yaslı marka bankoları (her birinde alış/satış görevlisi, üstte tabela),
///    ön duvarda dev piyasa tablosu ve kayan bant, ortada sırt sırta banko peronları; arka sol köşede vezne, arka sağ köşede sıralama duvarları ve yuvarlak çalışma masaları.
///  - Selçuklu cepheli bina + koridor (SeljukBuilding), meydanda ev ve MTBS Bankası (Town).
///  - Oyuncu, bot borsacılar, kamera, ışıklar, arayüz.
/// Boş sahnede Play'e basınca da kendiliğinden çalışır.
/// </summary>
public class SalonBuilder : MonoBehaviour
{
    public const float HalfW = 20f;   // x
    public const float HalfD = 26f;   // z
    public const float Height = 10f;
    /// <summary>Seans salonunun (-1. kat) zemini. Giriş katı, koridor ve şehir y=0'dadır.</summary>
    public const float FloorY = -3.6f;
    public const float LandHalfW = 8f;            // giriş katı sahanlığının yarı genişliği
    public const float LandFrontZ = -HalfD + 5.5f; // sahanlığın ön kenarı (merdiven başı)
    public const float StairHalfW = 3f;
    public const float StairRun = 7.2f;           // 18 basamak × 0,4 m
    public const float StairEndZ = LandFrontZ + StairRun;

    /// <summary>Salondaki bir noktanın zemin yüksekliği (giriş katı, merdiven ya da alt kat). Salon dışı 0.</summary>
    public static float GroundY(float x, float z)
    {
        if (z < -HalfD || Mathf.Abs(x) > HalfW || z > HalfD) return 0f;
        if (Mathf.Abs(x) <= LandHalfW && z <= LandFrontZ) return 0f;
        if (Mathf.Abs(x) <= StairHalfW && z < StairEndZ) return Mathf.Lerp(0f, FloorY, (z - LandFrontZ) / StairRun);
        return FloorY;
    }

    const float CounterStep = 2.4f;
    const float CounterInset = 1.6f;  // banko merkezinin duvara uzaklığı

    static readonly Color Floor = new Color(0.10f, 0.12f, 0.17f);
    static readonly Color Carpet = new Color(0.05f, 0.66f, 0.68f); // turkuaz halı
    static readonly Color Wall = new Color(0.11f, 0.14f, 0.21f);
    static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.30f);

    Transform root;
    bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        if (FindAnyObjectByType<SalonBuilder>() != null) return;
        new GameObject("SalonKurucu").AddComponent<SalonBuilder>();
    }

    /// <summary>Dünyayı baştan kurar (dil değişince tabelalar ve metinler yeni dilde yeniden oluşsun).
    /// Oturum jetonu saklı olduğundan oyuncu kaldığı hesapla geri girer.</summary>
    public static void Restart()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += Reboot;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    static void Reboot(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Reboot;
        LivePlayers.Active = false;
        if (FindAnyObjectByType<SalonBuilder>() == null) new GameObject("SalonKurucu").AddComponent<SalonBuilder>();
    }

    void Awake()
    {
        if (built) return;
        built = true;
        Application.targetFrameRate = 60;
        Build();
    }

    void Build()
    {
        root = new GameObject("Salon").transform;

        // --- Servisler ---
        var services = new GameObject("Servisler");
        services.AddComponent<Api>();
        services.AddComponent<Market>();
        services.AddComponent<LogoCache>();
        services.AddComponent<Bell>();
        services.AddComponent<TouchControls>();
        HtmlInput.Ensure();
        SocialLogin.Ensure();

        // Seans salonu bir alt kattadır (-1): kapıdan giriş katına (sahanlık) girilir, merdivenle inilir.
        // Salonun içi "Alt_Kat" altında y=0'a göre kurulur, en sonda FloorY kadar indirilir.
        var outer = root;
        var lower = new GameObject("Alt_Kat").transform;
        lower.SetParent(outer, false);
        root = lower;
        float wallH = Height - FloorY; // duvar ve tavan dışarıdan bakınca eski yükseklikte kalsın

        // --- Zemin, halı, duvarlar, tavan ---
        Gfx.Box("Zemin", root, new Vector3(0, -0.5f, 0), new Vector3(HalfW * 2 + 2, 1, HalfD * 2 + 2), Gfx.Lit(Floor, 0.65f), true);
        Gfx.Box("Hali", root, new Vector3(0, 0.005f, 3.5f), new Vector3(5f, 0.01f, HalfD * 2 - 13f), Gfx.Lit(Carpet, 0.05f));
        Gfx.Box("Duvar_On", root, new Vector3(0, wallH / 2, HalfD + 0.25f), new Vector3(HalfW * 2 + 1, wallH, 0.5f), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sol", root, new Vector3(-HalfW - 0.25f, wallH / 2, 0), new Vector3(0.5f, wallH, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sag", root, new Vector3(HalfW + 0.25f, wallH / 2, 0), new Vector3(0.5f, wallH, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Arka_Alt", root, new Vector3(0, -FloorY / 2f, -HalfD - 0.25f), new Vector3(HalfW * 2 + 1, -FloorY, 0.5f), Gfx.Lit(Wall), true);
        Gfx.Box("Tavan", root, new Vector3(0, wallH + 0.25f, 0), new Vector3(HalfW * 2 + 1, 0.5f, HalfD * 2 + 1), Gfx.Lit(new Color(0.06f, 0.07f, 0.1f)));
        for (int i = -3; i <= 3; i++)
            Gfx.Box("Isik_Serit", root, new Vector3(i * 5f, wallH - 0.02f, 0), new Vector3(0.25f, 0.04f, HalfD * 2 - 4), Gfx.Glow(new Color(0.85f, 0.9f, 1f)));
        Gfx.Box("Supurgelik", root, new Vector3(0, 0.15f, HalfD - 0.02f), new Vector3(HalfW * 2, 0.3f, 0.05f), Gfx.Glow(Gold * 0.7f));

        var boards = BuildBoards();
        root = outer;
        SeljukBuilding.Build(root, boards); // dış cephe, koridor, arka duvar (kapı boşluklu), meydan
        Town.Build(root);                   // ev + banka
        City.Build(root);                   // cadde, otobüs, duraklar, iş merkezi, emlak, galeri, müzayede, dükkânlar
        BuildLanding();                     // giriş katı (sahanlık), merdiven, korkuluklar
        BuildTmGate();
        root = lower;
        BuildCounters();
        BuildBank();
        RankWall.Build(root, HalfW, HalfD);
        BuildDesks();
        BuildBots(12);
        root = outer;
        var player = BuildPlayer();

        // --- Kamera ---
        Camera cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.16f, 0.30f); // akşam göğü
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.15f;
        cam.farClipPlane = 220f;
        var chase = cam.GetComponent<ChaseCamera>();
        if (chase == null) chase = cam.gameObject.AddComponent<ChaseCamera>();
        chase.target = player.transform;
        chase.overview = true;

        // --- Işıklar ---
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) l.intensity = 0.65f;
        bool hasSun = false;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) hasSun = true;
        if (!hasSun)
        {
            var lg = new GameObject("Ana_Isik");
            var dl = lg.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 0.65f;
            dl.shadows = LightShadows.None;
            lg.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
        }
        RenderSettings.ambientLight = new Color(0.38f, 0.40f, 0.47f);
        foreach (float x in new[] { -10f, 10f })
            foreach (float z in new[] { -17f, 0f, 17f })
            {
                var lg = new GameObject("Tavan_Lamba");
                lg.transform.SetParent(lower, false);
                lg.transform.localPosition = new Vector3(x, 8.5f, z);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 22f;
                l.intensity = 1.25f;
                l.color = new Color(1f, 0.95f, 0.88f);
            }

        // --- Yönetici + arayüz ---
        var director = new GameObject("SalonYonetici").AddComponent<SalonDirector>();
        director.player = player;
        director.chase = chase;
        director.boards = boards;

        // --- Canlı oyuncular, borsa yönetim kürsüsü (gong + duyurular), ortam sesleri ---
        var live = new GameObject("CanliOyuncular").AddComponent<LivePlayers>();
        live.player = player;
        Podium.Build(lower, front: HalfD);
        lower.position = new Vector3(0, FloorY, 0); // salonun içi alt kata iner (çocuklar birlikte)
        var amb = new GameObject("OrtamSesi").AddComponent<Ambience>();
        amb.player = player.transform;
    }

    // ================================================================ ön duvar

    Boards BuildBoards()
    {
        var holder = new GameObject("Tablolar");
        holder.transform.SetParent(root, false);
        var b = holder.AddComponent<Boards>();
        Transform t = holder.transform;
        float z = HalfD - 0.05f;
        var front = Quaternion.identity; // yazı +z'ye bakar; salonun içinden okunur

        // Dev pano (bankoların tabelalarının üstünde başlar)
        Gfx.Box("Pano", t, new Vector3(0, 6.2f, z), new Vector3(28f, 5.6f, 0.1f), Gfx.Lit(Panel, 0.8f));
        Gfx.Box("Pano_Cerceve", t, new Vector3(0, 9.05f, z - 0.02f), new Vector3(28.2f, 0.08f, 0.1f), Gfx.Glow(Gold));
        Gfx.Box("Pano_Cerceve", t, new Vector3(0, 3.35f, z - 0.02f), new Vector3(28.2f, 0.08f, 0.1f), Gfx.Glow(Gold));

        var logo = Resources.Load<Texture2D>("tmtbs_logo");
        if (logo != null)
            Gfx.Prim(PrimitiveType.Quad, "Logo", t, new Vector3(-12.2f, 8.25f, z - 0.08f), new Vector3(1.3f, 1.3f, 1f), Gfx.Texture(logo), false);
        Gfx.Text("Baslik", t, new Vector3(-11.2f, 8.25f, z - 0.08f), front, Loc.T("TRADE OF MTBS  ·  SEANS SALONU"), 0.62f, Gold, TextAnchor.MiddleLeft, TextAlignment.Left);
        b.statusText = Gfx.Text("Durum", t, new Vector3(13.5f, 8.25f, z - 0.08f), front, "", 0.28f, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleRight, TextAlignment.Right);

        for (int col = 0; col < 2; col++)
        {
            float x0 = col == 0 ? -13.2f : 0.6f;
            float top = 7.45f;
            b.nameCols[col] = Gfx.Text("Ad", t, new Vector3(x0, top, z - 0.08f), front, "", 0.42f, Color.white, TextAnchor.UpperLeft, TextAlignment.Left);
            b.priceCols[col] = Gfx.Text("Fiyat", t, new Vector3(x0 + 9.2f, top, z - 0.08f), front, "", 0.42f, new Color(1f, 0.92f, 0.6f), TextAnchor.UpperRight, TextAlignment.Right);
            b.changeCols[col] = Gfx.Text("Degisim", t, new Vector3(x0 + 12.4f, top, z - 0.08f), front, "", 0.42f, Color.white, TextAnchor.UpperRight, TextAlignment.Right);
        }

        // Kayan bant
        Gfx.Box("Bant", t, new Vector3(0, 9.55f, z), new Vector3(HalfW * 2, 0.7f, 0.1f), Gfx.Lit(Color.black, 0.9f));
        b.ticker = Gfx.Text("Bant_Yazi", t, new Vector3(HalfW, 9.55f, z - 0.08f), front, "", 0.42f, Color.white, TextAnchor.MiddleLeft, TextAlignment.Left);
        b.tickerLeft = -HalfW;
        b.tickerRight = HalfW;

        // Yan duvarlar: banko tabelalarının üstünde saat ve endeks
        var left = Quaternion.LookRotation(Vector3.left);
        var right = Quaternion.LookRotation(Vector3.right);
        foreach (var side in new[] { -1, 1 })
        {
            float x = side * (HalfW - 0.05f);
            var rot = side < 0 ? left : right;
            Gfx.Box("Yan_Pano", t, new Vector3(x, 7.3f, 0f), new Vector3(0.1f, 3.6f, 14f), Gfx.Lit(Panel, 0.8f));
            Gfx.Box("Yan_Pano_Cerceve", t, new Vector3(x - side * 0.02f, 9.15f, 0f), new Vector3(0.1f, 0.08f, 14.1f), Gfx.Glow(Gold));
            var clock = Gfx.Text("Saat", t, new Vector3(x - side * 0.08f, 8.2f, 0f), rot, "", 0.55f, Gold);
            var idx = Gfx.Text("Endeks", t, new Vector3(x - side * 0.08f, 6.5f, 0f), rot, "", 0.5f, Color.white);
            if (side < 0) { b.clockL = clock; b.indexL = idx; } else { b.clockR = clock; b.indexR = idx; }
        }
        return b;
    }

    // ================================================================ giriş katı (sahanlık) + merdiven

    /// <summary>
    /// Kapıdan girilen giriş katı: arka duvar boyunca mermer sahanlık, cam korkuluklar ve ortadan seans salonuna
    /// (-1. kat) inen geniş merdiven. Salon marka sayısı arttıkça yeni katlarla büyüyecek; -2. kat kapısı şimdilik kapalı.
    /// </summary>
    void BuildLanding()
    {
        var t = new GameObject("Giris_Kati").transform;
        t.SetParent(root, false);
        var marble = Gfx.Lit(new Color(0.80f, 0.78f, 0.74f), 0.75f);
        var stone = Gfx.Lit(new Color(0.16f, 0.18f, 0.24f), 0.4f);
        var glass = Gfx.Lit(new Color(0.55f, 0.75f, 0.85f), 0.95f);
        var gold = Gfx.Glow(Gold * 0.85f);
        float depth = LandFrontZ + HalfD, zc = -HalfD + depth / 2f, h = -FloorY;

        // Sahanlık bloğu (üstü y=0) ve mermer üst yüzü
        Gfx.Box("Sahanlik", t, new Vector3(0, -h / 2f, zc), new Vector3(LandHalfW * 2f, h, depth), stone, true);
        Gfx.Box("Sahanlik_Mermer", t, new Vector3(0, 0.004f, zc), new Vector3(LandHalfW * 2f, 0.01f, depth), marble);
        Gfx.Box("Sahanlik_Hali", t, new Vector3(0, 0.01f, zc), new Vector3(5f, 0.01f, depth), Gfx.Lit(Carpet, 0.05f));
        Gfx.Box("Sahanlik_Altin", t, new Vector3(0, -0.06f, LandFrontZ + 0.01f), new Vector3(LandHalfW * 2f, 0.12f, 0.04f), gold);

        // Merdiven: 18 basamak; altında görünmez rampa (rijit gövdeli karakterler basamağa takılmasın)
        int steps = 18;
        float rise = h / steps, tread = StairRun / steps;
        for (int i = 0; i < steps; i++)
        {
            float top = -(i + 1) * rise;
            float z0 = LandFrontZ + i * tread;
            Gfx.Box("Basamak", t, new Vector3(0, (top + FloorY) / 2f, z0 + tread / 2f), new Vector3(StairHalfW * 2f, top - FloorY + 0.001f, tread), marble);
            Gfx.Box("Basamak_Hali", t, new Vector3(0, top + 0.006f, z0 + tread / 2f), new Vector3(3.6f, 0.012f, tread), Gfx.Lit(Carpet, 0.05f));
            Gfx.Box("Basamak_Burun", t, new Vector3(0, top - 0.02f, z0 + 0.02f), new Vector3(StairHalfW * 2f, 0.04f, 0.04f), gold);
        }
        float ang = Mathf.Atan2(h, StairRun) * Mathf.Rad2Deg;
        float len = Mathf.Sqrt(h * h + StairRun * StairRun);
        var slope = Quaternion.Euler(ang, 0, 0);
        Vector3 mid = new Vector3(0, -h / 2f, LandFrontZ + StairRun / 2f);
        var ramp = new GameObject("Rampa");
        ramp.transform.SetParent(t, false);
        ramp.transform.localPosition = mid - slope * Vector3.up * 0.1f;
        ramp.transform.localRotation = slope;
        ramp.AddComponent<BoxCollider>().size = new Vector3(StairHalfW * 2f, 0.2f, len + 0.02f);

        // Merdiven korkulukları (eğimli cam + altın küpeşte)
        foreach (int s in new[] { -1, 1 })
        {
            var rail = new GameObject("Merdiven_Korkuluk");
            rail.transform.SetParent(t, false);
            rail.transform.localPosition = mid + new Vector3(s * (StairHalfW + 0.05f), 0, 0);
            rail.transform.localRotation = slope;
            Gfx.Box("Cam", rail.transform, new Vector3(0, 0.55f, 0), new Vector3(0.04f, 1.0f, len), glass, true);
            Gfx.Box("Kupeste", rail.transform, new Vector3(0, 1.07f, 0), new Vector3(0.09f, 0.07f, len + 0.1f), gold);
        }

        // Sahanlık korkulukları: ön kenar (merdiven ağzı açık) ve iki yan
        float sideLen = LandHalfW - StairHalfW;
        foreach (int s in new[] { -1, 1 })
        {
            float cx = s * (StairHalfW + sideLen / 2f);
            Gfx.Box("Korkuluk_Cam", t, new Vector3(cx, 0.55f, LandFrontZ - 0.05f), new Vector3(sideLen, 1.0f, 0.04f), glass, true);
            Gfx.Box("Korkuluk_Ust", t, new Vector3(cx, 1.07f, LandFrontZ - 0.05f), new Vector3(sideLen + 0.05f, 0.07f, 0.09f), gold);
            Gfx.Box("Korkuluk_Cam", t, new Vector3(s * (LandHalfW - 0.05f), 0.55f, zc), new Vector3(0.04f, 1.0f, depth), glass, true);
            Gfx.Box("Korkuluk_Ust", t, new Vector3(s * (LandHalfW - 0.05f), 1.07f, zc), new Vector3(0.09f, 0.07f, depth), gold);
            Gfx.Box("Korkuluk_Dikme", t, new Vector3(s * StairHalfW, 0.55f, LandFrontZ - 0.05f), new Vector3(0.12f, 1.1f, 0.12f), gold);
        }

        // Merdiven başında kemer ve tabelalar (inerken ve çıkarken okunur)
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Kemer_Dikme", t, new Vector3(s * (StairHalfW + 0.25f), 1.6f, LandFrontZ - 0.3f), new Vector3(0.22f, 3.2f, 0.22f), gold, true);
        Gfx.Box("Kemer_Kiris", t, new Vector3(0, 3.3f, LandFrontZ - 0.3f), new Vector3(StairHalfW * 2f + 0.8f, 0.5f, 0.24f), Gfx.Lit(Panel, 0.5f));
        Gfx.Text("Kemer_Yazi", t, new Vector3(0, 3.3f, LandFrontZ - 0.43f), Quaternion.identity, Loc.T("▼  SEANS SALONU · -1. KAT"), 0.24f, Gold);
        Gfx.Text("Kemer_Yazi_Arka", t, new Vector3(0, 3.3f, LandFrontZ - 0.17f), Quaternion.LookRotation(Vector3.back), Loc.T("▲  GİRİŞ KATI · ÇIKIŞ"), 0.24f, Gold);

        // Sahanlığın ön yüzü (alt kattan görünür): -2. kat kapısı, salon büyüdükçe açılacak
        float fz = LandFrontZ + 0.03f, dx = -(StairHalfW + sideLen / 2f);
        Gfx.Box("Kat2_Kapi", t, new Vector3(dx, FloorY + 1.25f, fz), new Vector3(2.2f, 2.5f, 0.06f), Gfx.Lit(new Color(0.08f, 0.09f, 0.12f), 0.8f));
        Gfx.Box("Kat2_Kasa", t, new Vector3(dx, FloorY + 2.55f, fz + 0.01f), new Vector3(2.5f, 0.12f, 0.08f), gold);
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Kat2_Kasa", t, new Vector3(dx + s * 1.18f, FloorY + 1.25f, fz + 0.01f), new Vector3(0.12f, 2.5f, 0.08f), gold);
        Gfx.Text("Kat2_Yazi", t, new Vector3(dx, FloorY + 1.55f, fz + 0.06f), Quaternion.LookRotation(Vector3.back),
            Loc.T("-2. KAT\n<size=60>YAKINDA</size>"), 0.2f, Gold);
        Gfx.Text("Kat2_Not", t, new Vector3(dx, FloorY + 0.75f, fz + 0.06f), Quaternion.LookRotation(Vector3.back),
            Loc.T("Marka sayısı arttıkça\nsalon yeni katlarla büyüyecek"), 0.085f, Color.white);
        Gfx.Text("Kat1_Yazi", t, new Vector3(-dx, FloorY + 2.2f, fz + 0.06f), Quaternion.LookRotation(Vector3.back),
            Loc.T("BORSA MTBS\n<size=60>-1. KAT · SEANS SALONU</size>"), 0.3f, Gold);

        var lg = new GameObject("Sahanlik_Isik");
        lg.transform.SetParent(t, false);
        lg.transform.localPosition = new Vector3(0, 4.5f, LandFrontZ - 2f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 12f;
        l.intensity = 1.1f;
        l.color = new Color(1f, 0.9f, 0.75f);
    }

    // ================================================================ bankolar

    void BuildCounters()
    {
        var holder = new GameObject("Bankolar").transform;
        holder.SetParent(root, false);
        var spots = new List<(Vector3 p, Quaternion r)>();
        float wx = HalfW - CounterInset;
        // Yan duvarlar (önden arkaya; arka köşeler vezne ile sıralama/çalışma köşesine ayrıldı)
        for (float z = HalfD - 4.6f; z >= -HalfD + 11.3f; z -= CounterStep)
        {
            spots.Add((new Vector3(-wx, 0, z), Quaternion.LookRotation(Vector3.right)));
            spots.Add((new Vector3(wx, 0, z), Quaternion.LookRotation(Vector3.left)));
        }
        // Ön duvarın köşeleri (dev panonun iki yanı)
        foreach (float x in new[] { 16.2f, 18.6f })
        {
            spots.Add((new Vector3(-x, 0, HalfD - CounterInset), Quaternion.LookRotation(Vector3.back)));
            spots.Add((new Vector3(x, 0, HalfD - CounterInset), Quaternion.LookRotation(Vector3.back)));
        }
        // Orta peronlar: iki ada, her birinde sırt sırta iki banko sırası (biri orta koridora, biri yan koridora bakar)
        foreach (int s in new[] { -1, 1 })
            for (float z = PeronZ; z >= -PeronZ - 0.01f; z -= CounterStep)
            {
                spots.Add((new Vector3(s * (PeronX + 1.6f), 0, z), Quaternion.LookRotation(new Vector3(s, 0, 0))));
                spots.Add((new Vector3(s * (PeronX - 1.6f), 0, z), Quaternion.LookRotation(new Vector3(-s, 0, 0))));
            }
        // Önden arkaya sırala: en değerli markalar ön duvara (dev panoya) yakın
        spots.Sort((a, b) => b.p.z.CompareTo(a.p.z));
        for (int i = 0; i < spots.Count; i++)
            Kiosk.Create(holder, i, spots[i].p, spots[i].r, true, i);

        // Peron başlarında altın dikmeler ve halı şeridi
        var gold = Gfx.Glow(Gold * 0.8f);
        foreach (int s in new[] { -1, 1 })
        {
            Gfx.Box("Peron_Hali", holder, new Vector3(s * PeronX, 0.006f, 0), new Vector3(1.2f, 0.01f, PeronZ * 2f + 3f), Gfx.Lit(new Color(0.35f, 0.07f, 0.10f), 0.1f));
            foreach (int e in new[] { -1, 1 })
            {
                float z = e * (PeronZ + 1.6f);
                Gfx.Box("Peron_Dikme", holder, new Vector3(s * PeronX, 1.6f, z), new Vector3(0.35f, 3.2f, 0.35f), Gfx.Lit(Panel, 0.8f), true);
                Gfx.Box("Peron_Dikme_Altin", holder, new Vector3(s * PeronX, 3.25f, z), new Vector3(0.45f, 0.1f, 0.45f), gold);
                var rot = Quaternion.LookRotation(new Vector3(0, 0, -e)); // dışarıdan bakan okur
                Gfx.Text("Peron_No", holder, new Vector3(s * PeronX, 2.5f, z + e * 0.19f), rot, s < 0 ? Loc.T("PERON A") : Loc.T("PERON B"), 0.2f, Gold);
            }
        }
    }

    public const float PeronX = 9f;     // peron adasının merkezi (|x|)
    public const float PeronZ = 12f;    // peron bankolarının z aralığı (±)

    // ================================================================ MTBS Bankası (arka sol köşe): 3 müşteri temsilcisi

    public static readonly Vector3 VezneSpot = new Vector3(-14.6f, FloorY, -HalfD + 7.2f);

    /// <summary>Müşteri temsilcisinin söyleyeceği kredi bilgisi (oyuncu sandalyeye oturunca).</summary>
    public static string BankSpeech() => Loc.T("Hoş geldiniz! Portföyünüze göre kredi limitiniz var.\nFaiz ve vade ekranda; borcunuzu buradan ödeyebilirsiniz.");

    /// <summary>
    /// Banka köşesi: üç masa, her birinde bir müşteri temsilcisi ve karşısında müşteri sandalyesi. Oyuncu oturunca
    /// kamera temsilcinin karşısına geçer, temsilci kredi konusunda bilgi verir ve kredi/borç ekranı açılır.
    /// </summary>
    void BuildBank()
    {
        var t = new GameObject("Salon_Banka").transform;
        t.SetParent(root, false);
        var wood = Gfx.Lit(new Color(0.30f, 0.20f, 0.13f), 0.35f);
        var marble = Gfx.Lit(new Color(0.82f, 0.80f, 0.76f), 0.7f);
        var navy = Gfx.Lit(new Color(0.06f, 0.14f, 0.32f), 0.3f);
        var metal = Gfx.Lit(new Color(0.08f, 0.09f, 0.11f), 0.7f);
        var glass = Gfx.Lit(new Color(0.55f, 0.72f, 0.82f), 0.95f);
        var chairMat = Gfx.Lit(new Color(0.42f, 0.08f, 0.10f), 0.3f);
        var gold = Gfx.Glow(Gold * 0.85f);
        float zRep = -HalfD + 2.6f, zDesk = -HalfD + 3.4f, zChair = -HalfD + 4.6f;
        Gfx.Box("Vezne_Yan", t, new Vector3(-9.75f, 1.2f, -HalfD + 2.8f), new Vector3(0.3f, 2.4f, 3.4f), navy, true);
        Gfx.Box("Vezne_Arka", t, new Vector3(-14.6f, 2.6f, -HalfD + 0.1f), new Vector3(10f, 5.2f, 0.1f), navy);
        Gfx.Box("Vezne_Serit", t, new Vector3(-14.6f, 5.3f, -HalfD + 0.16f), new Vector3(10f, 0.1f, 0.06f), Gfx.Glow(Gold));
        Gfx.Text("Vezne_Yazi", t, new Vector3(-14.6f, 4.5f, -HalfD + 0.2f), Quaternion.LookRotation(Vector3.back), Loc.T("MTBS BANKASI"), 0.5f, Gold);
        Gfx.Text("Vezne_Alt", t, new Vector3(-14.6f, 3.7f, -HalfD + 0.2f), Quaternion.LookRotation(Vector3.back), Loc.T("Müşteri temsilcileri · kredi · borç ödeme"), 0.28f, Color.white);
        Gfx.Box("Banka_Hali", t, new Vector3(-14.6f, 0.006f, -HalfD + 3.6f), new Vector3(9.6f, 0.01f, 4.4f), Gfx.Lit(new Color(0.05f, 0.10f, 0.24f), 0.1f));

        float[] xs = { -17.9f, -14.6f, -11.3f };
        string[] names = { "Selin", "Murat", "Elif" };
        for (int n = 0; n < xs.Length; n++)
        {
            float x = xs[n];
            // Masa, temsilcinin ekranı, isim plakası
            Gfx.Box("Banka_Masa", t, new Vector3(x, 0.38f, zDesk), new Vector3(1.7f, 0.76f, 0.8f), wood, true);
            Gfx.Box("Banka_Masa_Ust", t, new Vector3(x, 0.78f, zDesk), new Vector3(1.8f, 0.04f, 0.9f), marble);
            var mon = Gfx.Box("Banka_Ekran", t, new Vector3(x + 0.45f, 1.05f, zDesk - 0.15f), new Vector3(0.55f, 0.34f, 0.03f), metal);
            mon.transform.localRotation = Quaternion.Euler(0, 20f, 0);
            Gfx.Box("Plaka", t, new Vector3(x - 0.3f, 0.88f, zDesk + 0.3f), new Vector3(0.6f, 0.16f, 0.03f), gold);
            Gfx.Text("Plaka_Yazi", t, new Vector3(x - 0.3f, 0.88f, zDesk + 0.32f), Quaternion.LookRotation(Vector3.back),
                Loc.T("{0} · Müşteri Temsilcisi", names[n]), 0.035f, new Color(0.15f, 0.1f, 0.03f));
            Gfx.Text("Masa_No", t, new Vector3(x, 2.3f, zRep - 0.6f), Quaternion.LookRotation(Vector3.back), (n + 1).ToString(), 0.4f, Gold);
            if (n > 0) Gfx.Box("Banka_Bolme", t, new Vector3(x - 1.65f, 0.8f, zDesk), new Vector3(0.04f, 1.6f, 2.6f), glass);

            // Temsilcinin koltuğu ve kendisi (müşteriye bakar)
            Gfx.Box("Temsilci_Koltuk", t, new Vector3(x, 0.75f, zRep - 0.3f), new Vector3(0.55f, 0.9f, 0.08f), navy);
            var rep = new GameObject("Musteri_Temsilcisi");
            rep.transform.SetParent(t, false);
            rep.transform.position = new Vector3(x, 0, zRep);
            rep.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var lc = rep.AddComponent<LazyChar>();
            lc.model = n % 2;
            lc.look = new Look { female = n != 1, skin = 1 + n, hairStyle = n, hairColor = n, uniform = true };
            lc.idleState = 3;
            var bubble = Gfx.Text("Soz", rep.transform, new Vector3(0, 2.15f, 0), Quaternion.identity, Loc.T("Buyurun, oturun"), 0.16f, Gfx.Hex("#FFD54A"));
            bubble.gameObject.AddComponent<Billboard>();
            bubble.gameObject.SetActive(false);
            rep.AddComponent<Greeter>().bubble = bubble;

            // Müşteri sandalyesi (temsilciye bakar)
            Gfx.Box("Sandalye_Oturak", t, new Vector3(x, 0.46f, zChair), new Vector3(0.52f, 0.08f, 0.5f), chairMat);
            Gfx.Box("Sandalye_Sirt", t, new Vector3(x, 0.8f, zChair + 0.25f), new Vector3(0.52f, 0.6f, 0.07f), chairMat);
            Gfx.Prim(PrimitiveType.Cylinder, "Sandalye_Ayak", t, new Vector3(x, 0.22f, zChair), new Vector3(0.07f, 0.22f, 0.07f), metal, false);
            var sg = new GameObject("Banka_Sandalye");
            sg.transform.SetParent(t, false);
            sg.transform.position = new Vector3(x, 0, zChair - 0.05f);
            sg.transform.rotation = Quaternion.LookRotation(Vector3.back);
            var seat = sg.AddComponent<Seat>();
            var focus = new GameObject("Temsilci_Odak").transform;
            focus.SetParent(t, false);
            focus.position = new Vector3(x, 1.45f, zRep);
            focus.rotation = Quaternion.LookRotation(Vector3.forward);
            seat.focus = focus;
            seat.camLocal = new Vector3(0.6f, 0.35f, 2.6f); // oyuncunun omzunun üstünden temsilciye
            seat.bank = true;
            seat.speech = bubble;
            seat.idleSpeech = bubble.text;
            var it = Interactable.Make(sg, Interactable.Kind.Seat, new Vector3(0, 0, -0.75f), Loc.T("Müşteri temsilcisi: kredi ve borç"), Loc.T("OTUR"), 1.9f);
            it.seat = seat;
        }

        var lg = new GameObject("Banka_Lamba");
        lg.transform.SetParent(t, false);
        lg.transform.position = new Vector3(-14.6f, 4.2f, -HalfD + 5f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 12f;
        l.intensity = 1.1f;
        l.color = new Color(1f, 0.9f, 0.78f);
    }

    // ================================================================ çalışma masaları (arka sağ köşe, sıralama duvarlarının önü)

    /// <summary>Masaların önü (hızlı git noktası).</summary>
    public static readonly Vector3 DeskSpot = new Vector3(14.5f, FloorY, -HalfD + 9.6f);

    /// <summary>
    /// Üç yuvarlak çalışma masası; her masa buzlu cam bölmelerle üçe ayrılmış, her bölmede bir bilgisayar ekranı ve koltuk.
    /// Oyuncu koltuğa oturunca kamera ekrana yaklaşır ve masa paneli (portföy, düello, sıralama) açılır.
    /// </summary>
    void BuildDesks()
    {
        var t = new GameObject("Calisma_Masalari").transform;
        t.SetParent(root, false);
        var top = Gfx.Lit(new Color(0.20f, 0.13f, 0.09f), 0.65f);
        var metal = Gfx.Lit(new Color(0.08f, 0.09f, 0.11f), 0.7f);
        var glass = Gfx.Lit(new Color(0.55f, 0.72f, 0.82f), 0.95f);
        var chair = Gfx.Lit(new Color(0.06f, 0.14f, 0.32f), 0.3f);
        var rug = Gfx.Lit(new Color(0.14f, 0.05f, 0.08f), 0.1f);
        var gold = Gfx.Glow(Gold * 0.8f);
        var screen = Gfx.Glow(new Color(0.07f, 0.22f, 0.38f));
        Vector3[] centers = { new Vector3(12f, 0, -19.6f), new Vector3(17f, 0, -19.6f), new Vector3(14.5f, 0, -23.3f) };
        for (int n = 0; n < centers.Length; n++)
        {
            var m = new GameObject("Masa_" + (n + 1)).transform;
            m.SetParent(t, false);
            m.position = centers[n];
            m.rotation = Quaternion.Euler(0, n * 40f + 20f, 0);
            var block = m.gameObject.AddComponent<BoxCollider>(); // içinden geçilmesin
            block.center = new Vector3(0, 0.45f, 0);
            block.size = new Vector3(1.7f, 0.9f, 1.7f);

            Gfx.Prim(PrimitiveType.Cylinder, "Hali", m, new Vector3(0, 0.006f, 0), new Vector3(3.9f, 0.003f, 3.9f), rug, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Tabla", m, new Vector3(0, 0.76f, 0), new Vector3(2.1f, 0.03f, 2.1f), top, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Tabla_Serit", m, new Vector3(0, 0.745f, 0), new Vector3(2.15f, 0.012f, 2.15f), gold, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Ayak", m, new Vector3(0, 0.38f, 0), new Vector3(0.26f, 0.36f, 0.26f), metal, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Taban", m, new Vector3(0, 0.02f, 0), new Vector3(0.9f, 0.02f, 0.9f), metal, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Orta_Gobek", m, new Vector3(0, 0.98f, 0), new Vector3(0.2f, 0.2f, 0.2f), gold, false);

            for (int k = 0; k < 3; k++)
            {
                // Bölme: iki çalışma alanı arasında buzlu cam
                var dDir = Quaternion.Euler(0, 60f + k * 120f, 0) * Vector3.forward;
                var div = Gfx.Box("Bolme", m, dDir * 0.52f + Vector3.up * 0.99f, new Vector3(0.025f, 0.44f, 0.9f), glass);
                div.transform.localRotation = Quaternion.LookRotation(dDir);

                // Ekran: bölümün ortasında, dışa (koltuğa) bakar
                var dir = Quaternion.Euler(0, k * 120f, 0) * Vector3.forward;
                var face = Quaternion.LookRotation(dir);
                var bezel = Gfx.Box("Ekran_Cerceve", m, dir * 0.38f + Vector3.up * 1.08f, new Vector3(0.62f, 0.38f, 0.03f), metal);
                bezel.transform.localRotation = face;
                var stand = Gfx.Box("Ekran_Ayak", m, dir * 0.35f + Vector3.up * 0.88f, new Vector3(0.05f, 0.24f, 0.05f), metal);
                stand.transform.localRotation = face;
                var q = Gfx.Prim(PrimitiveType.Quad, "Ekran", m, dir * 0.397f + Vector3.up * 1.08f, new Vector3(0.56f, 0.32f, 1f), screen, false);
                q.transform.localRotation = Quaternion.LookRotation(-dir); // görünen yüzü dışa
                Gfx.Text("Ekran_Yazi", m, dir * 0.40f + Vector3.up * 1.1f, Quaternion.LookRotation(-dir),
                    Loc.T("<b>MTBS</b>\n<size=40>Portföy · Düello · Sıralama</size>"), 0.05f, new Color(0.75f, 0.9f, 1f));

                // Koltuk
                var cs = Gfx.Box("Koltuk_Oturak", m, dir * 1.3f + Vector3.up * 0.46f, new Vector3(0.5f, 0.08f, 0.5f), chair);
                cs.transform.localRotation = face;
                var cb = Gfx.Box("Koltuk_Sirt", m, dir * 1.55f + Vector3.up * 0.78f, new Vector3(0.5f, 0.56f, 0.07f), chair);
                cb.transform.localRotation = face;
                Gfx.Prim(PrimitiveType.Cylinder, "Koltuk_Ayak", m, dir * 1.3f + Vector3.up * 0.22f, new Vector3(0.07f, 0.22f, 0.07f), metal, false);

                // Oturma noktası (masaya bakar) ve ekran odağı (ileri yönü ekrandan koltuğa)
                var sg = new GameObject("Masa_Koltuk");
                sg.transform.SetParent(m, false);
                sg.transform.localPosition = dir * 1.25f;
                sg.transform.localRotation = Quaternion.LookRotation(-dir);
                var seat = sg.AddComponent<Seat>();
                var focus = new GameObject("Ekran_Odak").transform;
                focus.SetParent(m, false);
                focus.localPosition = dir * 0.40f + Vector3.up * 1.08f;
                focus.localRotation = face;
                seat.focus = focus;
                var it = Interactable.Make(sg, Interactable.Kind.Seat, new Vector3(0, 0, -0.75f), Loc.T("Çalışma masası: portföy, düello ve sıralama"), Loc.T("OTUR"), 1.9f);
                it.seat = seat;
            }
        }

        var lg = new GameObject("Masa_Lamba");
        lg.transform.SetParent(t, false);
        lg.transform.position = new Vector3(14.5f, 4.2f, -21f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 13f;
        l.intensity = 1.2f;
        l.color = new Color(1f, 0.88f, 0.72f);
    }


    // ================================================================ Trade of MTBS kapı perdesi: Müteşebbis tanıtımı

    /// <summary>Trade of MTBS hesabıyla girildiğinde açılır: salon kapısını kadife perdeyle kapatır, perdede Müteşebbis tanıtılır.</summary>
    public static GameObject TmGate;

    public const string MtPlayUrl = "https://play.google.com/store/apps/details?id=com.bavsla.mutesebbis_oyunu";
    public const string MtAppStoreUrl = "https://apps.apple.com/app/id6811590488";
    public const string MtWebUrl = "https://mutesebbis.tr";

    void BuildTmGate()
    {
        TmGate = new GameObject("TMTBS_Kapi");
        TmGate.transform.SetParent(root, false);
        var t = TmGate.transform;
        float w = SeljukBuilding.DoorW, dh = SeljukBuilding.DoorH, zc = -HalfD - 0.2f;
        var back = Quaternion.LookRotation(Vector3.back); // salonun içinden okunur
        var bar = new GameObject("Bariyer");
        bar.transform.SetParent(t, false);
        bar.transform.position = new Vector3(0, 1.5f, zc + 0.35f);
        bar.AddComponent<BoxCollider>().size = new Vector3(w + 0.6f, 3f, 0.4f);

        // Kadife perde: kıvrımlı dikey şeritler, üstte altın saçak
        var velvet = Gfx.Lit(new Color(0.42f, 0.04f, 0.08f), 0.35f);
        var velvetDark = Gfx.Lit(new Color(0.30f, 0.02f, 0.05f), 0.35f);
        int folds = 12;
        float fw = (w + 0.4f) / folds;
        for (int i = 0; i < folds; i++)
            Gfx.Box("Perde", t, new Vector3(-w / 2f - 0.2f + fw * (i + 0.5f), dh / 2f, zc + (i % 2 == 0 ? 0.05f : -0.03f)),
                new Vector3(fw + 0.02f, dh, 0.06f), i % 2 == 0 ? velvet : velvetDark);
        Gfx.Box("Perde_Sacak", t, new Vector3(0, dh - 0.25f, zc + 0.12f), new Vector3(w + 0.5f, 0.5f, 0.08f), velvetDark);
        Gfx.Box("Perde_Sacak_Altin", t, new Vector3(0, dh - 0.52f, zc + 0.15f), new Vector3(w + 0.5f, 0.06f, 0.06f), Gfx.Glow(Gold));
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Perde_Bag", t, new Vector3(s * (w / 2f - 0.05f), 2.2f, zc + 0.12f), new Vector3(0.12f, 0.08f, 0.1f), Gfx.Glow(Gold));

        // Tanıtım panosu
        float pz = zc + 0.16f;
        Gfx.Box("Tanitim_Pano", t, new Vector3(0, 2.75f, pz), new Vector3(4.8f, 3.5f, 0.04f), Gfx.Lit(new Color(0.012f, 0.012f, 0.016f), 0.6f));
        Gfx.Box("Tanitim_Cerceve", t, new Vector3(0, 4.52f, pz + 0.01f), new Vector3(4.9f, 0.05f, 0.05f), Gfx.Glow(Gold));
        Gfx.Box("Tanitim_Cerceve", t, new Vector3(0, 0.98f, pz + 0.01f), new Vector3(4.9f, 0.05f, 0.05f), Gfx.Glow(Gold));
        var logo = Resources.Load<Texture2D>("mutesebbis_logo");
        if (logo != null)
        {
            var lq = Gfx.Prim(PrimitiveType.Quad, "Mutesebbis_Logo", t, new Vector3(0, 3.75f, pz + 0.03f), new Vector3(1.45f, 1.45f, 1f), Gfx.Texture(logo), false);
            lq.transform.localRotation = back;
        }
        else Gfx.Text("Mutesebbis_Ad", t, new Vector3(0, 3.8f, pz + 0.03f), back, "MÜTEŞEBBİS", 0.5f, Gold);
        Gfx.Text("Tanitim_Resmi", t, new Vector3(0, 4.38f, pz + 0.03f), back, Loc.T("RESMİ OYUNUMUZ"), 0.12f, new Color(0.8f, 0.85f, 0.95f));
        Gfx.Text("Tanitim_Slogan", t, new Vector3(0, 2.86f, pz + 0.03f), back, Loc.T("İş kur, marka yönet, kariyer yap."), 0.2f, Color.white);
        Gfx.Text("Tanitim_Alt", t, new Vector3(0, 2.5f, pz + 0.03f), back,
            Loc.T("Kapının dışındaki şehir Müteşebbis'in: ev, araba, iş ve kendi markan.\nÜcretsiz indir, aynı borsada oyna."), 0.085f, new Color(0.85f, 0.88f, 0.95f));

        // Uygulama düğmeleri (tıklanınca resmî mağaza / site açılır)
        StoreButton(t, new Vector3(-1.55f, 1.75f, pz + 0.04f), back, Loc.T("Google Play"), new Color(0.07f, 0.45f, 0.22f), MtPlayUrl);
        StoreButton(t, new Vector3(0f, 1.75f, pz + 0.04f), back, Loc.T("App Store"), new Color(0.06f, 0.30f, 0.62f), MtAppStoreUrl);
        StoreButton(t, new Vector3(1.55f, 1.75f, pz + 0.04f), back, "mutesebbis.tr", new Color(0.55f, 0.38f, 0.08f), MtWebUrl);
        Gfx.Text("Tanitim_Not", t, new Vector3(0, 1.25f, pz + 0.03f), back,
            Loc.T("Şehre çıkmak için perdeye yaklaşıp KAPI'ya bas: Müteşebbis hesabınla gir."), 0.075f, Gold);

        var it = new GameObject("Kapi_Nokta");
        it.transform.SetParent(t, false);
        it.transform.position = new Vector3(0, 0, -HalfD + 0.9f);
        Interactable.Make(it, Interactable.Kind.Door, new Vector3(0, 0, 0.6f), Loc.T("Müteşebbis'i tanı · şehre çıkış"), Loc.T("KAPI"), 3.2f);

        var lg = new GameObject("Perde_Isik");
        lg.transform.SetParent(t, false);
        lg.transform.position = new Vector3(0, 4.6f, -HalfD + 2.6f);
        lg.transform.rotation = Quaternion.Euler(35f, 180f, 0);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Spot;
        l.range = 9f;
        l.spotAngle = 70f;
        l.intensity = 1.6f;
        l.color = new Color(1f, 0.9f, 0.78f);
        TmGate.SetActive(false);
    }

    static void StoreButton(Transform t, Vector3 pos, Quaternion rot, string label, Color c, string url)
    {
        var b = Gfx.Box("Magaza_Dugme", t, pos, new Vector3(1.38f, 0.42f, 0.06f), Gfx.Lit(c, 0.6f), true);
        b.transform.localRotation = rot;
        Gfx.Box("Magaza_Dugme_Kenar", t, pos + new Vector3(0, -0.23f, 0), new Vector3(1.38f, 0.03f, 0.07f), Gfx.Glow(Gold * 0.9f));
        Gfx.Text("Magaza_Yazi", t, pos + new Vector3(0, 0, 0.04f), rot, label, 0.13f, Color.white);
        var ck = b.AddComponent<Clickable3D>();
        ck.maxDistance = 9f;
        ck.onClick = () =>
        {
            if (Bell.I) Bell.I.Tick();
            Application.OpenURL(url);
        };
    }

    // ================================================================ insanlar

    GameObject Person(string name, Vector3 pos, out TextMesh label, bool bubble)
    {
        var p = new GameObject(name);
        p.transform.SetParent(root, false);
        p.transform.position = pos;
        var t = p.transform;
        p.layer = 2; // kamera/tıklama ışınları karakterlere takılmasın

        var col = p.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.height = 1.8f;
        col.radius = 0.32f;
        RigidbodyCompat.SetSurface(col, 0f, 0f);
        var rb = p.AddComponent<Rigidbody>();
        rb.mass = 70f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        label = Gfx.Text(bubble ? "Balon" : "Isim", t, new Vector3(0, bubble ? 2.25f : 2.15f, 0), Quaternion.identity, "", bubble ? 0.28f : 0.22f, Color.white);
        label.gameObject.AddComponent<Billboard>();
        if (bubble) label.gameObject.SetActive(false);
        return p;
    }

    Walker BuildPlayer()
    {
        var go = Person("Borsaci", new Vector3(0f, FloorY, -6f), out TextMesh tag, false);
        Characters.Spawn(go.transform, Characters.SavedModel, false, Look.Load());
        go.transform.rotation = Quaternion.Euler(0, 180f, 0);
        go.transform.localScale = Vector3.one * 1.6f; // menüde önizleme; oyuna girince normal boy
        Gfx.Prim(PrimitiveType.Cylinder, "Isaret", go.transform, new Vector3(0, 0.01f, 0), new Vector3(0.9f, 0.004f, 0.9f), Gfx.Glow(Gold), false);
        var w = go.AddComponent<Walker>();
        w.nameTag = tag;
        tag.color = Gold;
        return w;
    }

    void BuildBots(int n)
    {
        var rnd = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            Vector3 pos = i % 2 == 0
                ? new Vector3((float)(rnd.NextDouble() * 3 - 1.5), 0, (float)(rnd.NextDouble() * 31 - 11.5))
                : new Vector3((rnd.NextDouble() < 0.5 ? -1 : 1) * BotTrader.Lane, 0, (float)(rnd.NextDouble() * 34 - 15));
            var go = Person("Bot_Borsaci", pos, out TextMesh bubble, true);
            var lc = go.AddComponent<LazyChar>();
            lc.model = rnd.Next(Characters.Count);
            lc.look = Look.Random(rnd);
            var bot = go.AddComponent<BotTrader>();
            bot.bubble = bubble;
            bot.walkSpeed = 1.2f + (float)rnd.NextDouble() * 0.6f;
        }
    }
}
