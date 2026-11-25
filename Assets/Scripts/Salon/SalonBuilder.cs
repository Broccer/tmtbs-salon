using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dünyayı koddan kurar:
///  - Seans salonu: duvarlara yaslı marka bankoları (her birinde alış/satış görevlisi, üstte tabela),
///    ön duvarda dev piyasa tablosu ve kayan bant, ortada sırt sırta banko peronları; arka köşelerde vezne ve düello salonu.
///  - Selçuklu cepheli bina + koridor (SeljukBuilding), meydanda ev ve MTBS Bankası (Town).
///  - Oyuncu, bot borsacılar, kamera, ışıklar, arayüz.
/// Boş sahnede Play'e basınca da kendiliğinden çalışır.
/// </summary>
public class SalonBuilder : MonoBehaviour
{
    public const float HalfW = 20f;   // x
    public const float HalfD = 26f;   // z
    public const float Height = 10f;
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

        // --- Zemin, halı, duvarlar, tavan ---
        Gfx.Box("Zemin", root, new Vector3(0, -0.5f, 0), new Vector3(HalfW * 2 + 2, 1, HalfD * 2 + 2), Gfx.Lit(Floor, 0.65f), true);
        Gfx.Box("Hali", root, new Vector3(0, 0.005f, -1.5f), new Vector3(5f, 0.01f, HalfD * 2 - 3), Gfx.Lit(Carpet, 0.05f));
        Gfx.Box("Duvar_On", root, new Vector3(0, Height / 2, HalfD + 0.25f), new Vector3(HalfW * 2 + 1, Height, 0.5f), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sol", root, new Vector3(-HalfW - 0.25f, Height / 2, 0), new Vector3(0.5f, Height, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sag", root, new Vector3(HalfW + 0.25f, Height / 2, 0), new Vector3(0.5f, Height, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Tavan", root, new Vector3(0, Height + 0.25f, 0), new Vector3(HalfW * 2 + 1, 0.5f, HalfD * 2 + 1), Gfx.Lit(new Color(0.06f, 0.07f, 0.1f)));
        for (int i = -3; i <= 3; i++)
            Gfx.Box("Isik_Serit", root, new Vector3(i * 5f, Height - 0.02f, 0), new Vector3(0.25f, 0.04f, HalfD * 2 - 4), Gfx.Glow(new Color(0.85f, 0.9f, 1f)));
        Gfx.Box("Supurgelik", root, new Vector3(0, 0.15f, HalfD - 0.02f), new Vector3(HalfW * 2, 0.3f, 0.05f), Gfx.Glow(Gold * 0.7f));

        var boards = BuildBoards();
        SeljukBuilding.Build(root, boards); // dış cephe, koridor, arka duvar (kapı boşluklu), meydan
        Town.Build(root);                   // ev + banka
        City.Build(root);                   // cadde, otobüs, duraklar, iş merkezi, emlak, galeri, müzayede, dükkânlar
        BuildCounters();
        BuildBank();
        BuildDuelHall();
        BuildTmGate();
        var player = BuildPlayer();
        BuildBots(12);

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
                lg.transform.SetParent(root, false);
                lg.transform.position = new Vector3(x, 8f, z);
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
        Gfx.Text("Baslik", t, new Vector3(-11.2f, 8.25f, z - 0.08f), front, "TRADE OF MTBS  ·  SEANS SALONU", 0.62f, Gold, TextAnchor.MiddleLeft, TextAlignment.Left);
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

    // ================================================================ bankolar

    void BuildCounters()
    {
        var holder = new GameObject("Bankolar").transform;
        holder.SetParent(root, false);
        var spots = new List<(Vector3 p, Quaternion r)>();
        float wx = HalfW - CounterInset;
        // Yan duvarlar (önden arkaya; arka köşeler vezne ve düello salonuna ayrıldı)
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
                Gfx.Text("Peron_No", holder, new Vector3(s * PeronX, 2.5f, z + e * 0.19f), rot, s < 0 ? "PERON A" : "PERON B", 0.2f, Gold);
            }
        }
    }

    public const float PeronX = 9f;     // peron adasının merkezi (|x|)
    public const float PeronZ = 12f;    // peron bankolarının z aralığı (±)

    // ================================================================ MTBS Bankası veznesi (arka sol köşe)

    public static readonly Vector3 VezneSpot = new Vector3(-15f, 0, -HalfD + 6.6f);

    void BuildBank()
    {
        var t = new GameObject("Salon_Vezne").transform;
        t.SetParent(root, false);
        float z = -HalfD + 4.4f;                 // vezne tezgâhı
        var wood = Gfx.Lit(new Color(0.30f, 0.20f, 0.13f), 0.35f);
        var marble = Gfx.Lit(new Color(0.82f, 0.80f, 0.76f), 0.7f);
        var navy = Gfx.Lit(new Color(0.06f, 0.14f, 0.32f), 0.3f);
        Gfx.Box("Vezne_Tezgah", t, new Vector3(-14.6f, 0.55f, z), new Vector3(9.6f, 1.1f, 0.8f), wood, true);
        Gfx.Box("Vezne_Ust", t, new Vector3(-14.6f, 1.13f, z), new Vector3(9.8f, 0.07f, 0.95f), marble);
        Gfx.Box("Vezne_Cam", t, new Vector3(-14.6f, 1.75f, z - 0.2f), new Vector3(9.6f, 1.15f, 0.04f), Gfx.Lit(new Color(0.5f, 0.7f, 0.8f), 0.9f));
        Gfx.Box("Vezne_Yan", t, new Vector3(-9.75f, 1.2f, z - 1.6f), new Vector3(0.3f, 2.4f, 3.4f), navy, true);
        Gfx.Box("Vezne_Arka", t, new Vector3(-14.6f, 2.6f, -HalfD + 0.1f), new Vector3(10f, 5.2f, 0.1f), navy);
        Gfx.Box("Vezne_Serit", t, new Vector3(-14.6f, 5.3f, -HalfD + 0.16f), new Vector3(10f, 0.1f, 0.06f), Gfx.Glow(Gold));
        Gfx.Text("Vezne_Yazi", t, new Vector3(-14.6f, 4.5f, -HalfD + 0.2f), Quaternion.LookRotation(Vector3.back), "MTBS BANKASI · VEZNE", 0.5f, Gold);
        Gfx.Text("Vezne_Alt", t, new Vector3(-14.6f, 3.7f, -HalfD + 0.2f), Quaternion.LookRotation(Vector3.back), "Kredi · borç ödeme · hesap", 0.28f, Color.white);
        int n = 0;
        foreach (float x in new[] { -17.4f, -12.2f })
        {
            var teller = new GameObject("Vezne_Gorevlisi");
            teller.transform.SetParent(t, false);
            teller.transform.position = new Vector3(x, 0, z - 1.0f);
            var lc = teller.AddComponent<LazyChar>();
            lc.model = n;
            lc.look = new Look { female = n == 0, skin = 1 + n, hairStyle = 1, hairColor = 1, uniform = true };
            lc.idleState = n == 0 ? 3 : 0;
            var bubble = Gfx.Text("Soz", teller.transform, new Vector3(0, 2.2f, 0), Quaternion.identity, n == 0 ? "Kredi buradan" : "Borcunu burada öde", 0.22f, Gfx.Hex("#FFD54A"));
            bubble.gameObject.AddComponent<Billboard>();
            bubble.gameObject.SetActive(false);
            teller.AddComponent<Greeter>().bubble = bubble;

            var desk = new GameObject("Vezne_Nokta");
            desk.transform.SetParent(t, false);
            desk.transform.position = new Vector3(x, 0, z);
            Interactable.Make(desk, Interactable.Kind.Teller, new Vector3(0, 0, 1.2f), "MTBS Bankası veznesi: kredi ve borç", "VEZNE", 2.4f).arg = 1;
            n++;
        }
    }

    // ================================================================ düello salonu (arka sağ köşe)

    public static readonly Vector3 DuelSpot = new Vector3(14.6f, 0, -HalfD + 10f);

    void BuildDuelHall()
    {
        var t = new GameObject("Duello_Salonu").transform;
        t.SetParent(root, false);
        float x0 = 9f, x1 = HalfW, zf = -HalfD + 8f;   // ön duvar çizgisi
        float doorL = 13.1f, doorR = 16.1f;
        var wall = Gfx.Lit(new Color(0.30f, 0.06f, 0.09f), 0.3f);
        var gold = Gfx.Glow(Gold);
        float h = 3.4f;
        // Ön duvar (kapı boşluklu) ve sol duvar
        Gfx.Box("Duello_On_Sol", t, new Vector3((x0 + doorL) / 2f, h / 2f, zf), new Vector3(doorL - x0, h, 0.25f), wall, true);
        Gfx.Box("Duello_On_Sag", t, new Vector3((doorR + x1) / 2f, h / 2f, zf), new Vector3(x1 - doorR, h, 0.25f), wall, true);
        Gfx.Box("Duello_Yan", t, new Vector3(x0, h / 2f, (zf - HalfD) / 2f), new Vector3(0.25f, h, zf + HalfD), wall, true);
        Gfx.Box("Duello_Ust_Serit", t, new Vector3((x0 + x1) / 2f, h + 0.05f, zf), new Vector3(x1 - x0, 0.1f, 0.3f), gold);
        Gfx.Box("Duello_Tabela", t, new Vector3((doorL + doorR) / 2f, h + 0.75f, zf), new Vector3(6.4f, 1.2f, 0.15f), Gfx.Lit(Panel, 0.85f));
        Gfx.Text("Duello_Yazi", t, new Vector3((doorL + doorR) / 2f, h + 0.82f, zf + 0.1f), Quaternion.LookRotation(Vector3.back), "DÜELLO SALONU", 0.5f, Gold);
        Gfx.Text("Duello_Alt", t, new Vector3((doorL + doorR) / 2f, h + 0.38f, zf + 0.1f), Quaternion.LookRotation(Vector3.back), "Trade of MTBS · bire bir borsa yarışı", 0.2f, Color.white);
        Gfx.Box("Duello_Zemin", t, new Vector3((x0 + x1) / 2f, 0.008f, (zf - HalfD) / 2f), new Vector3(x1 - x0 - 0.3f, 0.01f, zf + HalfD - 0.3f), Gfx.Lit(new Color(0.12f, 0.05f, 0.07f), 0.4f));

        // Karşılıklı iki düello masası ve aradaki skor ekranı
        float cx = (x0 + x1) / 2f, cz = -HalfD + 3.6f;
        var desk = Gfx.Lit(new Color(0.08f, 0.09f, 0.12f), 0.7f);
        foreach (int s in new[] { -1, 1 })
        {
            Gfx.Box("Duello_Masa", t, new Vector3(cx + s * 2.6f, 0.5f, cz), new Vector3(1.4f, 1f, 2.4f), desk, true);
            var scr = Gfx.Box("Duello_Ekran", t, new Vector3(cx + s * 2.2f, 1.45f, cz), new Vector3(0.06f, 0.8f, 1.6f), Gfx.Glow(s < 0 ? new Color(0.3f, 0.9f, 0.45f) : new Color(1f, 0.42f, 0.42f)));
            scr.name = "Duello_Ekran";
        }
        Gfx.Box("Skor_Ekran", t, new Vector3(cx, 2.6f, -HalfD + 0.15f), new Vector3(6f, 2.2f, 0.1f), Gfx.Lit(Panel, 0.9f));
        Gfx.Text("Skor_Yazi", t, new Vector3(cx, 2.6f, -HalfD + 0.22f), Quaternion.LookRotation(Vector3.back), "SEN  vs  RAKİP\n<size=60>Aynı başlangıç parası · süre sonunda çok kazanan alır</size>", 0.32f, Gold);

        var it = new GameObject("Duello_Nokta");
        it.transform.SetParent(t, false);
        it.transform.position = new Vector3(cx, 0, cz);
        Interactable.Make(it, Interactable.Kind.Duel, new Vector3(0, 0, 2.2f), "Düello aç ya da bir düelloya katıl", "DÜELLO", 2.6f);

        var lg = new GameObject("Duello_Lamba");
        lg.transform.SetParent(t, false);
        lg.transform.position = new Vector3(cx, 3f, -HalfD + 4f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 10f;
        l.intensity = 1.4f;
        l.color = new Color(1f, 0.8f, 0.7f);
    }

    // ================================================================ Trade of MTBS kapı bariyeri

    /// <summary>Trade of MTBS hesabıyla girildiğinde açılır: salon kapısını kapatır ve Müteşebbis'e yönlendirir.</summary>
    public static GameObject TmGate;

    void BuildTmGate()
    {
        TmGate = new GameObject("TMTBS_Kapi");
        TmGate.transform.SetParent(root, false);
        var t = TmGate.transform;
        float z = -HalfD + 0.9f, w = SeljukBuilding.DoorW;
        var bar = new GameObject("Bariyer");
        bar.transform.SetParent(t, false);
        bar.transform.position = new Vector3(0, 1.5f, z - 0.4f);
        bar.AddComponent<BoxCollider>().size = new Vector3(w + 0.6f, 3f, 0.4f);
        var red = Gfx.Lit(new Color(0.55f, 0.08f, 0.12f), 0.2f);
        foreach (int s in new[] { -1, 1 })
        {
            Gfx.Box("Dikme", t, new Vector3(s * (w / 2f - 0.2f), 0.5f, z - 0.4f), new Vector3(0.14f, 1f, 0.14f), Gfx.Glow(Gold * 0.85f));
            Gfx.Prim(PrimitiveType.Sphere, "Dikme_Top", t, new Vector3(s * (w / 2f - 0.2f), 1.05f, z - 0.4f), Vector3.one * 0.2f, Gfx.Glow(Gold), false);
        }
        Gfx.Box("Kadife_Ip", t, new Vector3(0, 0.85f, z - 0.4f), new Vector3(w - 0.4f, 0.08f, 0.08f), red);
        Gfx.Text("Kapi_Yazi", t, new Vector3(0, 2.2f, z - 0.35f), Quaternion.LookRotation(Vector3.back), "ŞEHRE ÇIKIŞ · MÜTEŞEBBİS HESABIYLA", 0.24f, Gold);
        var it = new GameObject("Kapi_Nokta");
        it.transform.SetParent(t, false);
        it.transform.position = new Vector3(0, 0, z);
        Interactable.Make(it, Interactable.Kind.Door, new Vector3(0, 0, 0.6f), "Şehre çıkmak için Müteşebbis'e geç", "KAPI", 3.2f);
        TmGate.SetActive(false);
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
        var go = Person("Borsaci", new Vector3(0f, 0, -6f), out TextMesh tag, false);
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
                ? new Vector3((float)(rnd.NextDouble() * 3 - 1.5), 0, (float)(rnd.NextDouble() * 40 - 21))
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
