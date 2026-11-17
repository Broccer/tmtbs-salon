using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seans salonunu koddan kurar: salon, ön duvarda dev piyasa tablosu ve kayan bant,
/// yan duvarlarda saat/endeks, 12 işlem kürsüsü, oyuncunun borsacısı, bot borsacılar,
/// kamera, ışıklar ve arayüz. Boş sahnede Play'e basınca da kendiliğinden çalışır.
/// </summary>
public class SalonBuilder : MonoBehaviour
{
    public const float HalfW = 17f;   // x
    public const float HalfD = 23f;   // z
    public const float Height = 9f;
    public const int KioskCount = 12;

    static readonly Color Floor = new Color(0.10f, 0.12f, 0.17f);
    static readonly Color Carpet = new Color(0.36f, 0.08f, 0.12f);
    static readonly Color Wall = new Color(0.11f, 0.14f, 0.21f);
    static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
    static readonly Color Navy = new Color(0.08f, 0.12f, 0.30f);
    static readonly Color Skin = new Color(0.86f, 0.68f, 0.54f);
    static readonly Color Steel = new Color(0.55f, 0.58f, 0.64f);

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
        Build();
    }

    void Build()
    {
        root = new GameObject("Salon").transform;

        // --- Servisler ---
        var services = new GameObject("Servisler");
        services.AddComponent<Api>();
        services.AddComponent<Market>();
        services.AddComponent<Bell>();

        // --- Zemin, halı, duvarlar, tavan ---
        Gfx.Box("Zemin", root, new Vector3(0, -0.5f, 0), new Vector3(HalfW * 2 + 2, 1, HalfD * 2 + 2), Gfx.Lit(Floor, 0.65f), true);
        Gfx.Box("Hali", root, new Vector3(0, 0.005f, -2f), new Vector3(5f, 0.01f, HalfD * 2 - 4), Gfx.Lit(Carpet, 0.05f));
        Gfx.Box("Duvar_On", root, new Vector3(0, Height / 2, HalfD + 0.25f), new Vector3(HalfW * 2 + 1, Height, 0.5f), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Arka", root, new Vector3(0, Height / 2, -HalfD - 0.25f), new Vector3(HalfW * 2 + 1, Height, 0.5f), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sol", root, new Vector3(-HalfW - 0.25f, Height / 2, 0), new Vector3(0.5f, Height, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Duvar_Sag", root, new Vector3(HalfW + 0.25f, Height / 2, 0), new Vector3(0.5f, Height, HalfD * 2 + 1), Gfx.Lit(Wall), true);
        Gfx.Box("Tavan", root, new Vector3(0, Height + 0.25f, 0), new Vector3(HalfW * 2 + 1, 0.5f, HalfD * 2 + 1), Gfx.Lit(new Color(0.06f, 0.07f, 0.1f)));
        // Tavanda ışık şeritleri
        for (int i = -3; i <= 3; i++)
            Gfx.Box("Isik_Serit", root, new Vector3(i * 4.5f, Height - 0.02f, 0), new Vector3(0.25f, 0.04f, HalfD * 2 - 4), Gfx.Glow(new Color(0.85f, 0.9f, 1f)));
        // Duvar dibi altın şerit
        Gfx.Box("Supurgelik", root, new Vector3(0, 0.15f, HalfD - 0.02f), new Vector3(HalfW * 2, 0.3f, 0.05f), Gfx.Glow(Gold * 0.7f));

        var boards = BuildBoards();
        BuildKiosks();
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
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
        cam.fieldOfView = 60f;
        cam.transform.position = new Vector3(0, 7.5f, -14f);
        cam.transform.LookAt(new Vector3(0, 3f, 8f));
        var chase = cam.GetComponent<ChaseCamera>();
        if (chase == null) chase = cam.gameObject.AddComponent<ChaseCamera>();
        chase.target = player.transform;
        chase.overview = true;

        // --- Işıklar ---
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.intensity = 0.6f;
        if (FindAnyObjectByType<Light>() == null)
        {
            var lg = new GameObject("Ana_Isik");
            var dl = lg.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 0.6f;
            lg.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
        }
        RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.45f);
        foreach (var p in new[] { new Vector3(-8, 7, -8), new Vector3(8, 7, -8), new Vector3(-8, 7, 10), new Vector3(8, 7, 10), new Vector3(0, 7, 20) })
        {
            var lg = new GameObject("Tavan_Lamba");
            lg.transform.SetParent(root, false);
            lg.transform.position = p;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 16f;
            l.intensity = 1.2f;
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

        // Dev pano
        Gfx.Box("Pano", t, new Vector3(0, 5.2f, z), new Vector3(28f, 5.6f, 0.1f), Gfx.Lit(Panel, 0.8f));
        Gfx.Box("Pano_Cerceve", t, new Vector3(0, 8.05f, z - 0.02f), new Vector3(28.2f, 0.08f, 0.1f), Gfx.Glow(Gold));
        Gfx.Box("Pano_Cerceve", t, new Vector3(0, 2.35f, z - 0.02f), new Vector3(28.2f, 0.08f, 0.1f), Gfx.Glow(Gold));

        // Logo + başlık
        var logo = Resources.Load<Texture2D>("tmtbs_logo");
        if (logo != null)
        {
            var q = Gfx.Prim(PrimitiveType.Quad, "Logo", t, new Vector3(-12.2f, 7.25f, z - 0.08f), new Vector3(1.3f, 1.3f, 1f), Gfx.Texture(logo), false);
            q.transform.localRotation = Quaternion.identity;
        }
        Gfx.Text("Baslik", t, new Vector3(-11.2f, 7.25f, z - 0.08f), front, "TRADE OF MTBS  ·  SEANS SALONU", 0.62f, Gold, TextAnchor.MiddleLeft, TextAlignment.Left);
        b.statusText = Gfx.Text("Durum", t, new Vector3(13.5f, 7.25f, z - 0.08f), front, "", 0.28f, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleRight, TextAlignment.Right);

        // İki sütun: ad | fiyat | değişim
        for (int col = 0; col < 2; col++)
        {
            float x0 = col == 0 ? -13.2f : 0.6f;
            float top = 6.45f;
            b.nameCols[col] = Gfx.Text("Ad", t, new Vector3(x0, top, z - 0.08f), front, "", 0.42f, Color.white, TextAnchor.UpperLeft, TextAlignment.Left);
            b.priceCols[col] = Gfx.Text("Fiyat", t, new Vector3(x0 + 9.2f, top, z - 0.08f), front, "", 0.42f, new Color(1f, 0.92f, 0.6f), TextAnchor.UpperRight, TextAlignment.Right);
            b.changeCols[col] = Gfx.Text("Degisim", t, new Vector3(x0 + 12.4f, top, z - 0.08f), front, "", 0.42f, Color.white, TextAnchor.UpperRight, TextAlignment.Right);
        }

        // Kayan bant (duvardan duvara, yan duvarların arkasına girince görünmez olur)
        Gfx.Box("Bant", t, new Vector3(0, 8.55f, z), new Vector3(HalfW * 2, 0.7f, 0.1f), Gfx.Lit(Color.black, 0.9f));
        b.ticker = Gfx.Text("Bant_Yazi", t, new Vector3(HalfW, 8.55f, z - 0.08f), front, "", 0.42f, Color.white, TextAnchor.MiddleLeft, TextAlignment.Left);
        b.tickerLeft = -HalfW;
        b.tickerRight = HalfW;

        // Yan duvarlar: saat ve endeks (içeriden okunur)
        var left = Quaternion.LookRotation(Vector3.left);
        var right = Quaternion.LookRotation(Vector3.right);
        foreach (var side in new[] { -1, 1 })
        {
            float x = side * (HalfW - 0.05f);
            var rot = side < 0 ? left : right;
            Gfx.Box("Yan_Pano", t, new Vector3(x, 5.6f, 6f), new Vector3(0.1f, 3.6f, 9f), Gfx.Lit(Panel, 0.8f));
            var clock = Gfx.Text("Saat", t, new Vector3(x - side * 0.08f, 6.6f, 6f), rot, "", 0.6f, Gold);
            var idx = Gfx.Text("Endeks", t, new Vector3(x - side * 0.08f, 4.7f, 6f), rot, "", 0.55f, Color.white);
            if (side < 0) { b.clockL = clock; b.indexL = idx; } else { b.clockR = clock; b.indexR = idx; }
        }

        // Arka duvar: giriş tabelası (içeriden)
        Gfx.Text("Giris_Yazi", t, new Vector3(0, 6.5f, -HalfD + 0.1f), Quaternion.LookRotation(Vector3.back),
            "Bu bir oyundur · Sanal para", 0.35f, new Color(0.65f, 0.7f, 0.8f));
        return b;
    }

    // ================================================================ kürsüler

    void BuildKiosks()
    {
        float[] xs = { -10.5f, -3.5f, 3.5f, 10.5f };
        float[] zs = { 12f, 3f, -6f };
        int slot = 0;
        foreach (float z in zs)
        foreach (float x in xs)
            BuildKiosk(slot++, new Vector3(x, 0, z));
    }

    void BuildKiosk(int slot, Vector3 pos)
    {
        var go = new GameObject("Kursu_" + (slot + 1));
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        var t = go.transform;
        var k = go.AddComponent<Kiosk>();
        k.slot = slot;

        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 1.2f, 0);
        col.height = 2.4f;
        col.radius = 1.15f;

        Gfx.Prim(PrimitiveType.Cylinder, "Taban", t, new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.45f, 2.2f), Gfx.Lit(new Color(0.16f, 0.18f, 0.24f), 0.7f), false);
        Gfx.Prim(PrimitiveType.Cylinder, "Tezgah", t, new Vector3(0, 0.95f, 0), new Vector3(2.4f, 0.05f, 2.4f), Gfx.Lit(Steel, 0.8f), false);
        var ring = Gfx.Prim(PrimitiveType.Cylinder, "Halka", t, new Vector3(0, 1.02f, 0), new Vector3(2.45f, 0.02f, 2.45f), Gfx.Glow(Color.grey), false);
        k.ring = ring.GetComponent<Renderer>();
        Gfx.Box("Direk", t, new Vector3(0, 1.8f, 0), new Vector3(0.18f, 1.6f, 0.18f), Gfx.Lit(Steel, 0.8f));
        Gfx.Box("Ekran", t, new Vector3(0, 2.75f, 0), new Vector3(2.1f, 1.15f, 0.14f), Gfx.Lit(Panel, 0.9f));
        Gfx.Box("Ekran_Cerceve", t, new Vector3(0, 3.36f, 0), new Vector3(2.14f, 0.05f, 0.16f), Gfx.Glow(Gold * 0.8f));

        // Ekranın iki yüzü: ön (-z tarafından okunur) ve arka (+z tarafından)
        for (int side = 0; side < 2; side++)
        {
            float zz = side == 0 ? -0.08f : 0.08f;
            var rot = side == 0 ? Quaternion.identity : Quaternion.Euler(0, 180f, 0);
            k.nameText[side] = Gfx.Text("Ad", t, new Vector3(0, 3.08f, zz), rot, "", 0.2f, Gold);
            k.priceText[side] = Gfx.Text("Fiyat", t, new Vector3(0, 2.72f, zz), rot, "", 0.34f, Color.white);
            k.changeText[side] = Gfx.Text("Degisim", t, new Vector3(0, 2.38f, zz), rot, "", 0.22f, Color.white);
        }
    }

    // ================================================================ insanlar

    GameObject Person(string name, Vector3 pos, Color suit, Color tie, out TextMesh label, bool bubble)
    {
        var p = new GameObject(name);
        p.transform.SetParent(root, false);
        p.transform.position = pos;
        var t = p.transform;

        var col = p.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.height = 1.8f;
        col.radius = 0.32f;
        RigidbodyCompat.SetSurface(col, 0f, 0f);
        var rb = p.AddComponent<Rigidbody>();
        rb.mass = 70f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Gfx.Prim(PrimitiveType.Capsule, "Ceket", t, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 0.4f, 0.4f), Gfx.Lit(suit, 0.3f), false);
        Gfx.Box("Pantolon", t, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.9f, 0.3f), Gfx.Lit(suit * 0.6f, 0.2f));
        Gfx.Box("Gomlek", t, new Vector3(0, 1.38f, 0.19f), new Vector3(0.2f, 0.3f, 0.04f), Gfx.Lit(Color.white));
        Gfx.Box("Kravat", t, new Vector3(0, 1.32f, 0.215f), new Vector3(0.07f, 0.32f, 0.02f), Gfx.Lit(tie));
        Gfx.Prim(PrimitiveType.Sphere, "Kafa", t, new Vector3(0, 1.72f, 0), Vector3.one * 0.3f, Gfx.Lit(Skin), false);
        // Kulaklık/telefon değil; elde bir dosya (borsacı havası)
        Gfx.Box("Dosya", t, new Vector3(0.33f, 1.05f, 0.05f), new Vector3(0.05f, 0.3f, 0.24f), Gfx.Lit(new Color(0.9f, 0.85f, 0.7f)));

        label = Gfx.Text(bubble ? "Balon" : "Isim", t, new Vector3(0, bubble ? 2.25f : 2.15f, 0), Quaternion.identity, "", bubble ? 0.28f : 0.22f, Color.white);
        label.gameObject.AddComponent<Billboard>();
        if (bubble) label.gameObject.SetActive(false);
        return p;
    }

    Walker BuildPlayer()
    {
        var go = Person("Borsaci", new Vector3(0, 0, -19f), Navy, new Color(0.85f, 0.15f, 0.2f), out TextMesh tag, false);
        // Ayak altında altın halka: oyuncunun kendisi
        Gfx.Prim(PrimitiveType.Cylinder, "Isaret", go.transform, new Vector3(0, 0.01f, 0), new Vector3(0.9f, 0.004f, 0.9f), Gfx.Glow(Gold), false);
        var w = go.AddComponent<Walker>();
        w.nameTag = tag;
        tag.color = Gold;
        return w;
    }

    void BuildBots(int n)
    {
        var suits = new[] { new Color(0.2f, 0.2f, 0.22f), new Color(0.3f, 0.25f, 0.2f), new Color(0.15f, 0.2f, 0.28f), new Color(0.35f, 0.35f, 0.38f), new Color(0.12f, 0.16f, 0.14f) };
        var ties = new[] { new Color(0.2f, 0.4f, 0.9f), new Color(0.9f, 0.7f, 0.1f), new Color(0.1f, 0.6f, 0.35f), new Color(0.6f, 0.1f, 0.5f) };
        var rnd = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            var pos = new Vector3((float)(rnd.NextDouble() * 26 - 13), 0, (float)(rnd.NextDouble() * 30 - 14));
            var go = Person("Bot_Borsaci", pos, suits[i % suits.Length], ties[i % ties.Length], out TextMesh bubble, true);
            var bot = go.AddComponent<BotTrader>();
            bot.bubble = bubble;
            bot.walkSpeed = 1.3f + (float)rnd.NextDouble() * 0.8f;
        }
    }
}
