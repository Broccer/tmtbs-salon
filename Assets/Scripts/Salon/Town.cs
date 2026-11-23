using UnityEngine;

/// <summary>
/// Meydandaki diğer binalar: oyuncunun evi (solda) ve MTBS Bankası (sağda). Taş yollar turkuaz halıdan kapılara gider.
/// Oyuncu evde başlar; evdeki bilgisayardan hesabına bakar, bankada kredi çeker / borç öder.
/// </summary>
public static class Town
{
    public const float HouseX = -32f;
    public const float BankX = 34f;
    public static float Z => SeljukBuilding.CrossZ;

    /// <summary>Oyuncunun evde başladığı yer (kapıya bakar).</summary>
    public static Vector3 HomeSpawn => new Vector3(HouseX - 1.5f, 0, Z + 1.5f);
    public static Quaternion HomeFacing => Quaternion.LookRotation(Vector3.right);
    /// <summary>Bankanın kapısının önü (dışarıda).</summary>
    public static Vector3 BankDoorOutside => new Vector3(BankX - 12f, 0, Z);
    public static Vector3 HouseDoorOutside => new Vector3(HouseX + 8f, 0, Z);

    static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
    static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f);

    public static void Build(Transform root)
    {
        var t = new GameObject("Kasaba").transform;
        t.SetParent(root, false);
        BuildPaths(t);
        BuildHouse(t);
        BuildBank(t);
    }

    // ================================================================ yollar

    static void BuildPaths(Transform t)
    {
        var stone = Gfx.Lit(new Color(0.62f, 0.58f, 0.52f), 0.15f);
        var edge = Gfx.Lit(new Color(0.36f, 0.33f, 0.30f), 0.1f);
        float hx = HouseX + 6f, bx = BankX - 10f;
        // Halıdan eve
        float l1 = -2.6f - hx;
        Gfx.Box("Yol_Ev", t, new Vector3(hx + l1 / 2f, 0.004f, Z), new Vector3(l1, 0.01f, 3f), stone);
        Gfx.Box("Yol_Kenar", t, new Vector3(hx + l1 / 2f, 0.006f, Z + 1.55f), new Vector3(l1, 0.012f, 0.12f), edge);
        Gfx.Box("Yol_Kenar", t, new Vector3(hx + l1 / 2f, 0.006f, Z - 1.55f), new Vector3(l1, 0.012f, 0.12f), edge);
        // Halıdan bankaya
        float l2 = bx - 2.6f;
        Gfx.Box("Yol_Banka", t, new Vector3(2.6f + l2 / 2f, 0.004f, Z), new Vector3(l2, 0.01f, 3f), stone);
        Gfx.Box("Yol_Kenar", t, new Vector3(2.6f + l2 / 2f, 0.006f, Z + 1.55f), new Vector3(l2, 0.012f, 0.12f), edge);
        Gfx.Box("Yol_Kenar", t, new Vector3(2.6f + l2 / 2f, 0.006f, Z - 1.55f), new Vector3(l2, 0.012f, 0.12f), edge);

        // Kavşakta yön tabelası (iki yüzlü)
        var post = new GameObject("Yon_Tabelasi").transform;
        post.SetParent(t, false);
        post.position = new Vector3(3.6f, 0, Z + 2.6f);
        Gfx.Box("Direk", post, new Vector3(0, 1.3f, 0), new Vector3(0.12f, 2.6f, 0.12f), Gfx.Lit(new Color(0.2f, 0.18f, 0.16f), 0.3f));
        Gfx.Box("Levha", post, new Vector3(0, 2.25f, 0), new Vector3(2.6f, 0.9f, 0.06f), Gfx.Lit(Panel, 0.8f));
        string txt = "<color=#FFD54A>↑ BORSA MTBS</color>\n← EV      BANKA →";
        Gfx.Text("Yazi_On", post, new Vector3(0, 2.25f, -0.04f), Quaternion.identity, txt, 0.22f, Color.white);
        Gfx.Text("Yazi_Arka", post, new Vector3(0, 2.25f, 0.04f), Quaternion.Euler(0, 180f, 0), "<color=#FFD54A>↓ BORSA MTBS</color>\n← BANKA      EV →", 0.22f, Color.white);

        // Yol fenerleri
        foreach (float x in new[] { HouseX + 10f, HouseX + 18f, BankX - 14f, BankX - 22f })
            foreach (int s in new[] { -1, 1 })
            {
                Gfx.Prim(PrimitiveType.Cylinder, "Direk", t, new Vector3(x, 1.4f, Z + s * 2.4f), new Vector3(0.14f, 1.4f, 0.14f), Gfx.Lit(new Color(0.15f, 0.16f, 0.2f), 0.6f), false);
                Gfx.Prim(PrimitiveType.Sphere, "Fener", t, new Vector3(x, 3.0f, Z + s * 2.4f), Vector3.one * 0.4f, Gfx.Glow(new Color(1f, 0.9f, 0.6f)), false);
            }
    }

    /// <summary>Dört duvarlı oda; doorSide = +1 ise kapı +x duvarında, -1 ise -x duvarında.</summary>
    static void Room(Transform t, string name, Vector3 c, float sx, float sz, float h, int doorSide, float doorW, float doorH, Material wall, Material floor, Material ceiling)
    {
        float th = 0.4f;
        Gfx.Box(name + "_Zemin", t, new Vector3(c.x, 0.01f, c.z), new Vector3(sx, 0.02f, sz), floor);
        Gfx.Box(name + "_Tavan", t, new Vector3(c.x, h + th / 2f, c.z), new Vector3(sx + th * 2f, th, sz + th * 2f), ceiling, true);
        // Kapısız uzun duvarlar (±z)
        foreach (int s in new[] { -1, 1 })
            Gfx.Box(name + "_Duvar", t, new Vector3(c.x, h / 2f, c.z + s * (sz / 2f + th / 2f)), new Vector3(sx + th * 2f, h, th), wall, true);
        // Kapısız kısa duvar
        Gfx.Box(name + "_Duvar", t, new Vector3(c.x - doorSide * (sx / 2f + th / 2f), h / 2f, c.z), new Vector3(th, h, sz), wall, true);
        // Kapılı duvar: iki yan + üst
        float dx = c.x + doorSide * (sx / 2f + th / 2f);
        float side = (sz - doorW) / 2f;
        Gfx.Box(name + "_Duvar", t, new Vector3(dx, h / 2f, c.z - doorW / 2f - side / 2f), new Vector3(th, h, side), wall, true);
        Gfx.Box(name + "_Duvar", t, new Vector3(dx, h / 2f, c.z + doorW / 2f + side / 2f), new Vector3(th, h, side), wall, true);
        Gfx.Box(name + "_Lento", t, new Vector3(dx, doorH + (h - doorH) / 2f, c.z), new Vector3(th, h - doorH, doorW), wall, true);
    }

    static void PointLight(Transform t, Vector3 p, float range, float intensity, Color c)
    {
        var lg = new GameObject("Lamba");
        lg.transform.SetParent(t, false);
        lg.transform.position = p;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = c;
    }

    // ================================================================ ev

    static void BuildHouse(Transform root)
    {
        var t = new GameObject("Ev").transform;
        t.SetParent(root, false);
        var c = new Vector3(HouseX, 0, Z);
        float sx = 12f, sz = 10f, h = 4.2f;
        var wall = Gfx.Lit(new Color(0.86f, 0.80f, 0.70f), 0.1f);
        var wood = Gfx.Lit(new Color(0.45f, 0.30f, 0.18f), 0.4f);
        var roof = Gfx.Lit(new Color(0.55f, 0.22f, 0.16f), 0.2f);
        Room(t, "Ev", c, sx, sz, h, 1, 2.4f, 2.8f, wall, wood, Gfx.Lit(new Color(0.9f, 0.88f, 0.84f), 0.1f));

        // Kiremit çatı (iki eğimli yüzey)
        foreach (int s in new[] { -1, 1 })
        {
            var r = Gfx.Box("Cati", t, new Vector3(c.x, h + 1.55f, c.z + s * 2.75f), new Vector3(sx + 1.6f, 0.25f, 6.6f), roof);
            r.transform.localRotation = Quaternion.Euler(s * 28f, 0, 0);
        }
        // Kapı söveleri ve tabela
        float dx = c.x + sx / 2f + 0.22f;
        Gfx.Box("Sove", t, new Vector3(dx, 1.4f, c.z - 1.3f), new Vector3(0.1f, 2.8f, 0.15f), wood);
        Gfx.Box("Sove", t, new Vector3(dx, 1.4f, c.z + 1.3f), new Vector3(0.1f, 2.8f, 0.15f), wood);
        Gfx.Box("Sove", t, new Vector3(dx, 2.85f, c.z), new Vector3(0.1f, 0.15f, 2.75f), wood);
        Gfx.Box("Tabela", t, new Vector3(dx + 0.02f, 3.5f, c.z), new Vector3(0.06f, 0.6f, 2.6f), Gfx.Lit(Panel, 0.8f));
        Gfx.Text("Tabela_Yazi", t, new Vector3(dx + 0.07f, 3.5f, c.z), Quaternion.LookRotation(Vector3.left), "EVİM", 0.36f, Gold);
        // Pencereler (dışarıdan parlayan)
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Pencere", t, new Vector3(dx + 0.01f, 1.8f, c.z + s * 3.2f), new Vector3(0.04f, 1.2f, 1.6f), Gfx.Glow(new Color(1f, 0.85f, 0.55f)));

        // İç: halı, kanepe, yatak, sehpa, çalışma masası + bilgisayar
        Gfx.Box("Kilim", t, new Vector3(c.x + 1f, 0.025f, c.z), new Vector3(4f, 0.01f, 3f), Gfx.Lit(new Color(0.6f, 0.15f, 0.15f), 0.05f));
        var sofa = Gfx.Lit(new Color(0.18f, 0.32f, 0.5f), 0.2f);
        Gfx.Box("Kanepe", t, new Vector3(c.x + 1f, 0.3f, c.z + 4.2f), new Vector3(3.2f, 0.6f, 1f), sofa, true);
        Gfx.Box("Kanepe_Arka", t, new Vector3(c.x + 1f, 0.75f, c.z + 4.6f), new Vector3(3.2f, 0.9f, 0.25f), sofa);
        Gfx.Box("Sehpa", t, new Vector3(c.x + 1f, 0.25f, c.z + 2.6f), new Vector3(1.6f, 0.5f, 0.8f), wood, true);
        Gfx.Box("Yatak", t, new Vector3(c.x - 3.5f, 0.3f, c.z - 3.4f), new Vector3(2.2f, 0.6f, 2.8f), Gfx.Lit(new Color(0.92f, 0.92f, 0.95f), 0.2f), true);
        Gfx.Box("Yatak_Basi", t, new Vector3(c.x - 3.5f, 0.7f, c.z - 4.85f), new Vector3(2.3f, 1.4f, 0.15f), wood);
        Gfx.Box("Yorgan", t, new Vector3(c.x - 3.5f, 0.62f, c.z - 2.9f), new Vector3(2.24f, 0.06f, 1.9f), Gfx.Lit(new Color(0.85f, 0.55f, 0.2f), 0.1f));

        // Çalışma masası batı duvarında, ekran doğuya (odaya) bakar
        float mx = c.x - sx / 2f + 0.6f;
        Gfx.Box("Masa", t, new Vector3(mx, 0.75f, c.z + 1.5f), new Vector3(1f, 0.06f, 2f), wood, true);
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Masa_Ayak", t, new Vector3(mx, 0.37f, c.z + 1.5f + s * 0.9f), new Vector3(0.9f, 0.74f, 0.06f), wood);
        Gfx.Box("Monitor", t, new Vector3(mx - 0.2f, 1.2f, c.z + 1.5f), new Vector3(0.05f, 0.6f, 1f), Gfx.Lit(Panel, 0.9f));
        Gfx.Box("Monitor_Ekran", t, new Vector3(mx - 0.17f, 1.2f, c.z + 1.5f), new Vector3(0.01f, 0.52f, 0.92f), Gfx.Glow(new Color(0.2f, 0.6f, 0.9f)));
        Gfx.Text("Monitor_Yazi", t, new Vector3(mx - 0.16f, 1.2f, c.z + 1.5f), Quaternion.LookRotation(Vector3.left), "BORSA MTBS\n<size=40>hesabım</size>", 0.09f, Color.white);
        Gfx.Box("Klavye", t, new Vector3(mx + 0.15f, 0.79f, c.z + 1.5f), new Vector3(0.25f, 0.02f, 0.6f), Gfx.Lit(new Color(0.1f, 0.1f, 0.12f), 0.5f));
        var pc = new GameObject("Bilgisayar");
        pc.transform.SetParent(t, false);
        pc.transform.position = new Vector3(mx, 0, c.z + 1.5f);
        pc.transform.rotation = Quaternion.LookRotation(Vector3.right);
        Interactable.Make(pc, Interactable.Kind.Computer, new Vector3(0, 0, 1.1f), "Bilgisayarda hesabına bak", "BAK", 2f);

        PointLight(t, new Vector3(c.x, h - 0.6f, c.z), 11f, 1.3f, new Color(1f, 0.9f, 0.75f));
    }

    // ================================================================ banka

    static void BuildBank(Transform root)
    {
        var t = new GameObject("Banka").transform;
        t.SetParent(root, false);
        var c = new Vector3(BankX, 0, Z);
        float sx = 20f, sz = 16f, h = 7.5f;
        var marble = Gfx.Lit(new Color(0.88f, 0.87f, 0.84f), 0.6f);
        var stone = Gfx.Lit(new Color(0.78f, 0.76f, 0.72f), 0.2f);
        var floor = Gfx.Lit(new Color(0.20f, 0.22f, 0.26f), 0.75f);
        var navy = Gfx.Lit(new Color(0.06f, 0.12f, 0.28f), 0.4f);
        Room(t, "Banka", c, sx, sz, h, -1, 3.2f, 4.2f, stone, floor, Gfx.Lit(new Color(0.85f, 0.84f, 0.8f), 0.1f));

        // Cephe (batı, -x): basamaklar, sütunlar, alınlık, tabela
        float fx = c.x - sx / 2f - 0.4f;
        Gfx.Box("Basamak", t, new Vector3(fx - 1.1f, 0.06f, c.z), new Vector3(2.2f, 0.12f, sz + 1f), marble);
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            float z = c.z + i * 2.3f;
            if (Mathf.Abs(i) == 1) z = c.z + Mathf.Sign(i) * 2.2f;
            Gfx.Prim(PrimitiveType.Cylinder, "Sutun", t, new Vector3(fx - 1.2f, h / 2f, z), new Vector3(0.7f, h / 2f, 0.7f), marble, true);
            Gfx.Box("Sutun_Baslik", t, new Vector3(fx - 1.2f, h - 0.15f, z), new Vector3(0.95f, 0.3f, 0.95f), marble);
        }
        Gfx.Box("Saçak", t, new Vector3(fx - 1.0f, h + 0.3f, c.z), new Vector3(2.6f, 0.6f, sz + 1.4f), marble);
        // Alınlık (üçgen yaklaşık: iki eğik kiriş + dolgu)
        foreach (int s in new[] { -1, 1 })
        {
            var r = Gfx.Box("Alinlik", t, new Vector3(fx - 1.0f, h + 1.65f, c.z + s * 4.3f), new Vector3(0.6f, 0.4f, 9.6f), marble);
            r.transform.localRotation = Quaternion.Euler(s * -22f, 0, 0);
        }
        Gfx.Box("Alinlik_Dolgu", t, new Vector3(fx - 0.85f, h + 1.25f, c.z), new Vector3(0.3f, 1.3f, 9f), stone);
        Gfx.Box("Tabela", t, new Vector3(fx - 0.72f, 5.6f, c.z), new Vector3(0.08f, 1.0f, 6.8f), Gfx.Lit(Panel, 0.85f));
        Gfx.Box("Tabela_Cerceve", t, new Vector3(fx - 0.74f, 6.14f, c.z), new Vector3(0.1f, 0.06f, 6.9f), Gfx.Glow(Gold));
        Gfx.Box("Tabela_Cerceve", t, new Vector3(fx - 0.74f, 5.06f, c.z), new Vector3(0.1f, 0.06f, 6.9f), Gfx.Glow(Gold));
        Gfx.Text("Tabela_Yazi", t, new Vector3(fx - 0.78f, 5.6f, c.z), Quaternion.LookRotation(Vector3.right), "MTBS BANKASI", 0.62f, Gold);
        Gfx.Text("Alt_Yazi", t, new Vector3(fx - 0.5f, 4.6f, c.z), Quaternion.LookRotation(Vector3.right), "kredi · ödeme · hesap", 0.26f, Color.white);

        // İç: vezne tezgahı (doğu tarafında, kuzey-güney boyunca), arkasında görevliler
        float vx = c.x + 3.5f;
        Gfx.Box("Vezne", t, new Vector3(vx, 0.55f, c.z), new Vector3(0.8f, 1.1f, 9f), navy, true);
        Gfx.Box("Vezne_Ust", t, new Vector3(vx, 1.13f, c.z), new Vector3(0.95f, 0.06f, 9.1f), marble);
        Gfx.Box("Vezne_Serit", t, new Vector3(vx - 0.41f, 0.98f, c.z), new Vector3(0.02f, 0.05f, 9f), Gfx.Glow(Gold * 0.85f));
        Gfx.Box("Vezne_Cam", t, new Vector3(vx + 0.2f, 1.8f, c.z), new Vector3(0.03f, 1.2f, 9f), Gfx.Glow(new Color(0.55f, 0.75f, 0.85f) * 0.35f));
        // Veznenin üstünde duvar tabelası
        float wx = c.x + sx / 2f - 0.05f;
        Gfx.Box("Vezne_Pano", t, new Vector3(wx, 4.6f, c.z), new Vector3(0.1f, 2.2f, 9f), Gfx.Lit(Panel, 0.85f));
        Gfx.Text("Vezne_Baslik", t, new Vector3(wx - 0.08f, 5.2f, c.z), Quaternion.LookRotation(Vector3.right), "KREDİ  ·  ÖDEME", 0.5f, Gold);
        Gfx.Text("Vezne_Bilgi", t, new Vector3(wx - 0.08f, 4.2f, c.z), Quaternion.LookRotation(Vector3.right),
            "100.000 MLS kredi · 5 günde 20.000'lik dilimler\n30 günde bir 10.000 MLS taksit · gecikmede %5 ceza", 0.2f, Color.white);

        int n = 0;
        foreach (float dz in new[] { -2.4f, 2.4f })
        {
            var teller = new GameObject("Vezne_Gorevlisi");
            teller.transform.SetParent(t, false);
            teller.transform.position = new Vector3(vx + 0.9f, 0, c.z + dz);
            teller.transform.rotation = Quaternion.LookRotation(Vector3.left);
            var lc = teller.AddComponent<LazyChar>();
            lc.model = n == 0 ? 0 : 1;
            lc.look = new Look { female = n == 0, skin = 1 + n, hairStyle = 1, hairColor = 1, uniform = true };
            lc.idleState = n == 0 ? 3 : 0;
            var bubble = Gfx.Text("Soz", teller.transform, new Vector3(0, 2.2f, 0), Quaternion.identity, n == 0 ? "Kredi işlemleri buradan" : "Borç ödemesi buradan", 0.22f, Gfx.Hex("#FFD54A"));
            bubble.gameObject.AddComponent<Billboard>();
            bubble.gameObject.SetActive(false);
            teller.AddComponent<Greeter>().bubble = bubble;

            var desk = new GameObject("Vezne_Nokta");
            desk.transform.SetParent(t, false);
            desk.transform.position = new Vector3(vx, 0, c.z + dz);
            desk.transform.rotation = Quaternion.LookRotation(Vector3.left);
            Interactable.Make(desk, Interactable.Kind.Teller, new Vector3(0, 0, 1.2f), "Veznede kredi çek / borç öde", "VEZNE", 2.2f);
            n++;
        }

        // Bekleme koltukları
        for (int i = -2; i <= 2; i++)
            Gfx.Box("Bekleme_Koltugu", t, new Vector3(c.x - 6.5f, 0.25f, c.z + i * 1.4f + (i >= 0 ? 3.5f : -3.5f) * 0f + i * 0.2f), new Vector3(0.6f, 0.5f, 0.6f), navy, true);
        // Kasa dairesi kapısı (süs)
        Gfx.Prim(PrimitiveType.Cylinder, "Kasa_Kapi", t, new Vector3(c.x + 4f, 1.8f, c.z + sz / 2f - 0.05f), new Vector3(2.4f, 0.05f, 2.4f), Gfx.Lit(new Color(0.6f, 0.62f, 0.66f), 0.85f), false)
            .transform.localRotation = Quaternion.Euler(90f, 0, 0);
        Gfx.Text("Kasa_Yazi", t, new Vector3(c.x + 4f, 3.4f, c.z + sz / 2f - 0.12f), Quaternion.identity, "KASA", 0.3f, Gold);

        PointLight(t, new Vector3(c.x - 3f, h - 1f, c.z), 14f, 1.3f, new Color(1f, 0.95f, 0.85f));
        PointLight(t, new Vector3(c.x + 4f, h - 1f, c.z), 12f, 1.1f, new Color(1f, 0.95f, 0.85f));
        PointLight(t, new Vector3(fx - 4f, 5f, c.z), 14f, 1.2f, new Color(1f, 0.92f, 0.8f));
    }
}
