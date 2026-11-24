using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dünyayı koddan kurar:
///  - Seans salonu: duvarlara yaslı marka bankoları (her birinde alış/satış görevlisi, üstte tabela),
///    ön duvarda dev piyasa tablosu ve kayan bant, ortada ekranları izlemek için sandalye sıraları.
///  - Selçuklu cepheli bina + koridor (SeljukBuilding), meydanda ev ve MTBS Bankası (Town).
///  - Oyuncu, bot borsacılar, kamera, ışıklar, arayüz.
/// Boş sahnede Play'e basınca da kendiliğinden çalışır.
/// </summary>
public class SalonBuilder : MonoBehaviour
{
    public const float HalfW = 24f;   // x
    public const float HalfD = 38f;   // z
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

        // --- Zemin, halı, duvarlar, tavan ---
        Gfx.Box("Zemin", root, new Vector3(0, -0.5f, 0), new Vector3(HalfW * 2 + 2, 1, HalfD * 2 + 2), Gfx.Lit(Floor, 0.65f), true);
        Gfx.Box("Hali", root, new Vector3(0, 0.005f, -1.5f), new Vector3(5f, 0.01f, HalfD * 2 - 3), Gfx.Lit(Carpet, 0.05f));
        Gfx.Box("Duvar_On", root, new Vector3(0, Height / 2, HalfD + 0.25f), new Vector3(HalfW * 2 + 1, Height, 0.5f), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sol", root, new Vector3(-HalfW - 0.25f, Height / 2, 0), new Vector3(0.5f, Height, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sag", root, new Vector3(HalfW + 0.25f, Height / 2, 0), new Vector3(0.5f, Height, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Tavan", root, new Vector3(0, Height + 0.25f, 0), new Vector3(HalfW * 2 + 1, 0.5f, HalfD * 2 + 1), Gfx.Lit(new Color(0.06f, 0.07f, 0.1f)));
        for (int i = -4; i <= 4; i++)
            Gfx.Box("Isik_Serit", root, new Vector3(i * 5f, Height - 0.02f, 0), new Vector3(0.25f, 0.04f, HalfD * 2 - 4), Gfx.Glow(new Color(0.85f, 0.9f, 1f)));
        Gfx.Box("Supurgelik", root, new Vector3(0, 0.15f, HalfD - 0.02f), new Vector3(HalfW * 2, 0.3f, 0.05f), Gfx.Glow(Gold * 0.7f));

        var boards = BuildBoards();
        SeljukBuilding.Build(root, boards); // dış cephe, koridor, arka duvar (kapı boşluklu), meydan
        Town.Build(root);                   // ev + banka
        City.Build(root);                   // cadde, otobüs, duraklar, iş merkezi, emlak, galeri, müzayede, dükkânlar
        BuildCounters();
        BuildSeats();
        var player = BuildPlayer();
        BuildBots(14);

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
        foreach (float x in new[] { -12f, 12f })
            foreach (float z in new[] { -26f, -8f, 10f, 28f })
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
        // Yan duvarlar (önden arkaya)
        for (float z = HalfD - 4.6f; z >= -HalfD + 2.2f; z -= CounterStep)
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
        // Arka duvar (kapının iki yanı)
        for (float x = SeljukBuilding.DoorW / 2f + 1.6f; x <= HalfW - 5f + 0.01f; x += CounterStep)
        {
            spots.Add((new Vector3(-x, 0, -HalfD + CounterInset), Quaternion.identity));
            spots.Add((new Vector3(x, 0, -HalfD + CounterInset), Quaternion.identity));
        }
        // Önden arkaya sırala: en değerli markalar ön duvara (dev panoya) yakın
        spots.Sort((a, b) => b.p.z.CompareTo(a.p.z));
        for (int i = 0; i < spots.Count; i++)
            Kiosk.Create(holder, i, spots[i].p, spots[i].r, true, i);
    }

    // ================================================================ sandalyeler

    void BuildSeats()
    {
        var holder = new GameObject("Sandalyeler").transform;
        holder.SetParent(root, false);
        var frame = Gfx.Lit(new Color(0.12f, 0.13f, 0.16f), 0.6f);
        var cushion = Gfx.Lit(new Color(0.55f, 0.10f, 0.14f), 0.15f);
        var statics = new List<GameObject>();
        int rowIdx = 0;
        for (float z = -14f; z <= 22f; z += 3f, rowIdx++)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float x0 = side * 4.4f, x1 = side * 15.2f;
                int n = 0;
                for (float x = 4.4f; x <= 15.21f; x += 1.2f, n++)
                {
                    float xx = side * x;
                    var c = new GameObject("Sandalye");
                    c.transform.SetParent(holder, false);
                    c.transform.position = new Vector3(xx, 0, z);
                    var ct = c.transform;
                    statics.Add(Gfx.Box("Ayak", ct, new Vector3(0, 0.21f, 0), new Vector3(0.44f, 0.42f, 0.44f), frame));
                    statics.Add(Gfx.Box("Oturak", ct, new Vector3(0, 0.46f, 0), new Vector3(0.52f, 0.08f, 0.5f), cushion));
                    statics.Add(Gfx.Box("Arkalik", ct, new Vector3(0, 0.78f, -0.24f), new Vector3(0.52f, 0.6f, 0.06f), cushion));
                    var seat = new GameObject("Oturma");
                    seat.transform.SetParent(ct, false);
                    seat.transform.localPosition = new Vector3(0, 0, -0.02f);
                    var s = seat.AddComponent<Seat>();
                    var it = Interactable.Make(seat, Interactable.Kind.Seat, new Vector3(0, 0, 0.75f), "Otur ve panoları izle", "OTUR", 1.25f);
                    it.seat = s;
                }
                // Sıranın tek çarpışma kutusu
                var col = new GameObject("Sira_Carpisma");
                col.transform.SetParent(holder, false);
                col.transform.position = new Vector3((x0 + x1) / 2f, 0.5f, z - 0.03f);
                col.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(x1 - x0) + 0.6f, 1f, 0.56f);
            }
        }
        StaticBatchingUtility.Combine(holder.gameObject);
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
                ? new Vector3((float)(rnd.NextDouble() * 3 - 1.5), 0, (float)(rnd.NextDouble() * 50 - 34))
                : new Vector3((rnd.NextDouble() < 0.5 ? -1 : 1) * 18.5f, 0, (float)(rnd.NextDouble() * 60 - 32));
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
