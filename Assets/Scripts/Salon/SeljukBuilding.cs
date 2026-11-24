using UnityEngine;

/// <summary>
/// Salonun dışındaki Selçuklu mimarili bina: taş cephe, sivri kemerli taç kapı (eyvan), mukarnas, turkuaz çini bantları,
/// iki minare, kubbe. Cephede "BORSA MTBS" tabelası, logo, endeks ve en çok yükselen/düşen 5 marka görünür.
/// Kapıdan turkuaz halı serili koridora, oradan salona girilir. Her şey koddan kurulur.
/// </summary>
public static class SeljukBuilding
{
    public const float DoorW = 6f;
    public const float DoorH = 5.5f;
    public const float Z0 = -SalonBuilder.HalfD - 0.5f;   // salon arka duvarının dış yüzü
    const float CorridorLen = 11f;
    public const float ZF = Z0 - CorridorLen;              // cephenin dış yüzü
    public const float SquareDepth = 120f;            // meydanın derinliği (cepheden geriye)
    public const float SquareHalfW = 86f;
    public const float CrossZ = ZF - 30f;             // ev ve bankaya giden yolların kesiştiği yer
    const float FH = 12f;                           // cephe yüksekliği
    const float PortalZ = 1.6f;                     // taç kapının öne çıkıntısı
    const float PortalH = 15f;

    public static readonly Color Turquoise = new Color(0.05f, 0.66f, 0.68f);
    static readonly Color TurquoiseDark = new Color(0.03f, 0.40f, 0.50f);
    static readonly Color Stone = new Color(0.74f, 0.62f, 0.44f);
    static readonly Color StoneDark = new Color(0.52f, 0.41f, 0.28f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
    static readonly Color Navy = new Color(0.04f, 0.12f, 0.26f);
    static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f);

    public static void Build(Transform root, Boards b)
    {
        var t = new GameObject("Bina").transform;
        t.SetParent(root, false);
        var stone = Gfx.Lit(Stone, 0.1f);
        var stoneDark = Gfx.Lit(StoneDark, 0.1f);
        var turq = Gfx.Lit(Turquoise, 0.7f);
        var turqDark = Gfx.Lit(TurquoiseDark, 0.7f);
        var gold = Gfx.Glow(Gold);

        BuildGroundAndLimits(t);
        BuildShellAndCorridor(t, stone, stoneDark, turq, gold);
        BuildFacade(t, b, stone, stoneDark, turq, turqDark, gold);
        BuildMinarets(t, stone, stoneDark, turq, gold);
        BuildDome(t, stoneDark, turq, gold);
        BuildLamps(t, gold);
        Attendants.Build(t, ZF - PortalZ - 1.3f, Z0 - 3.5f);
    }

    // ================================================================ zemin, sınırlar

    static void BuildGroundAndLimits(Transform t)
    {
        Gfx.Box("Meydan", t, new Vector3(0, -0.5f, ZF - SquareDepth / 2f + 0.5f), new Vector3(SquareHalfW * 2f + 10f, 1f, SquareDepth + 1f), Gfx.Lit(new Color(0.42f, 0.37f, 0.30f), 0.2f), true);
        // Meydandaki turkuaz halı kapıdan koridora, oradan salona uzanır
        float len = -(CrossZ - 3f) - SalonBuilder.HalfD;
        Gfx.Box("Hali_Dis", t, new Vector3(0, 0.006f, -SalonBuilder.HalfD - len / 2f), new Vector3(5f, 0.01f, len), Gfx.Lit(Turquoise, 0.05f));
        Gfx.Box("Hali_Kenar", t, new Vector3(-2.65f, 0.007f, -SalonBuilder.HalfD - len / 2f), new Vector3(0.18f, 0.01f, len), Gfx.Glow(Gold * 0.8f));
        Gfx.Box("Hali_Kenar", t, new Vector3(2.65f, 0.007f, -SalonBuilder.HalfD - len / 2f), new Vector3(0.18f, 0.01f, len), Gfx.Glow(Gold * 0.8f));
        // Görünmez sınırlar: oyuncu meydandan taşmasın
        Limit(t, new Vector3(-SquareHalfW, 5f, ZF - SquareDepth / 2f), new Vector3(1f, 10f, SquareDepth + 2f));
        Limit(t, new Vector3(SquareHalfW, 5f, ZF - SquareDepth / 2f), new Vector3(1f, 10f, SquareDepth + 2f));
        Limit(t, new Vector3(0f, 5f, ZF - SquareDepth), new Vector3(SquareHalfW * 2f, 10f, 1f));
        // Cephenin iki yanı: binanın arkasına (salonun yanlarına) geçilmesin
        float hw = SalonBuilder.HalfW + 0.5f;
        Limit(t, new Vector3(-(hw + SquareHalfW) / 2f, 5f, ZF + 0.5f), new Vector3(SquareHalfW - hw, 10f, 1f));
        Limit(t, new Vector3((hw + SquareHalfW) / 2f, 5f, ZF + 0.5f), new Vector3(SquareHalfW - hw, 10f, 1f));
    }

    static void Limit(Transform t, Vector3 center, Vector3 size)
    {
        var g = new GameObject("Sinir");
        g.transform.SetParent(t, false);
        g.transform.localPosition = center;
        g.AddComponent<BoxCollider>().size = size;
    }

    // ================================================================ salon arka duvarı + koridor

    static void BuildShellAndCorridor(Transform t, Material stone, Material stoneDark, Material turq, Material gold)
    {
        var wall = Gfx.Lit(new Color(0.11f, 0.14f, 0.21f));
        float hw = SalonBuilder.HalfW + 0.5f;                 // 17,5
        float zb = -SalonBuilder.HalfD - 0.25f;               // arka duvar ekseni
        float side = (hw + DoorW / 2f) / 2f;                  // yan parça merkezi
        float sideW = hw - DoorW / 2f;
        // Salonun arka duvarı, ortasında kapı boşluğuyla
        Gfx.Box("Duvar_Arka_Sol", t, new Vector3(-side, SalonBuilder.Height / 2f, zb), new Vector3(sideW, SalonBuilder.Height, 0.5f), wall, true);
        Gfx.Box("Duvar_Arka_Sag", t, new Vector3(side, SalonBuilder.Height / 2f, zb), new Vector3(sideW, SalonBuilder.Height, 0.5f), wall, true);
        float lh = SalonBuilder.Height - DoorH;
        Gfx.Box("Duvar_Arka_Ust", t, new Vector3(0, DoorH + lh / 2f, zb), new Vector3(DoorW, lh, 0.5f), wall, true);
        // Salon kapısına altın söve
        Gfx.Box("Sove", t, new Vector3(-DoorW / 2f - 0.1f, DoorH / 2f, zb + 0.28f), new Vector3(0.2f, DoorH, 0.08f), gold);
        Gfx.Box("Sove", t, new Vector3(DoorW / 2f + 0.1f, DoorH / 2f, zb + 0.28f), new Vector3(0.2f, DoorH, 0.08f), gold);
        Gfx.Box("Sove", t, new Vector3(0, DoorH + 0.1f, zb + 0.28f), new Vector3(DoorW + 0.4f, 0.2f, 0.08f), gold);

        // Giriş bloğunun dış yan duvarları ve çatısı (içi kapalı; yalnız koridor kullanılır)
        float zc = (Z0 + ZF) / 2f;
        // Koridorun zemini: salon zemini ile meydan arasındaki boşluğu kapatır (yoksa oyuncu düşer)
        Gfx.Box("Koridor_Zemin", t, new Vector3(0, -0.503f, zc), new Vector3(hw * 2f, 1f, CorridorLen + 3f), Gfx.Lit(new Color(0.42f, 0.37f, 0.30f), 0.2f), true);
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Blok_Yan", t, new Vector3(s * (hw - 0.25f), FH / 2f, zc), new Vector3(0.5f, FH, CorridorLen), stone, true);
        Gfx.Box("Blok_Cati", t, new Vector3(0, FH + 0.25f, zc), new Vector3(hw * 2f, 0.5f, CorridorLen + 1f), stoneDark);

        // Koridor: iki yan duvar, tavan; duvarlarda turkuaz çini bantları ve altın şerit
        float cw = DoorW / 2f + 0.25f;
        foreach (int s in new[] { -1, 1 })
        {
            Gfx.Box("Koridor_Duvar", t, new Vector3(s * cw, 3.5f, zc), new Vector3(0.5f, 7f, CorridorLen), stone, true);
            float xi = s * (cw - 0.27f);
            Gfx.Box("Koridor_Cini", t, new Vector3(xi, 1.2f, zc), new Vector3(0.05f, 2.0f, CorridorLen - 0.2f), turq);
            Gfx.Box("Koridor_Altin", t, new Vector3(xi, 2.28f, zc), new Vector3(0.06f, 0.1f, CorridorLen - 0.2f), gold);
            Gfx.Box("Koridor_Altin", t, new Vector3(xi, 5.6f, zc), new Vector3(0.06f, 0.1f, CorridorLen - 0.2f), gold);
            // Eşkenar dörtgen çini motifleri (geçme desen)
            for (int i = 0; i < 6; i++)
            {
                float z = Z0 - 0.9f - i * 1.5f;
                var d = Gfx.Box("Koridor_Motif", t, new Vector3(xi, 4.0f, z), new Vector3(0.9f, 0.9f, 0.05f), i % 2 == 0 ? turq : gold);
                d.transform.localRotation = Quaternion.Euler(0, 90f, 45f);
            }
        }
        Gfx.Box("Koridor_Tavan", t, new Vector3(0, 7.25f, zc), new Vector3(DoorW + 1f, 0.5f, CorridorLen), Gfx.Lit(new Color(0.20f, 0.30f, 0.34f), 0.2f));
        // Tavan ışık şeridi
        Gfx.Box("Koridor_Isik", t, new Vector3(0, 6.97f, zc), new Vector3(0.3f, 0.04f, CorridorLen - 1f), Gfx.Glow(new Color(0.9f, 0.95f, 1f)));
        foreach (float z in new[] { Z0 - 3f, Z0 - 8f })
        {
            var lg = new GameObject("Koridor_Lamba");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = new Vector3(0, 5.5f, z);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 9f;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.93f, 0.8f);
        }
    }

    // ================================================================ cephe

    static void BuildFacade(Transform t, Boards b, Material stone, Material stoneDark, Material turq, Material turqDark, Material gold)
    {
        float hw = SalonBuilder.HalfW + 0.5f;
        float zw = ZF + 0.5f; // cephe duvarının ekseni (kalınlık 1)
        float side = (hw + DoorW / 2f) / 2f;
        float sideW = hw - DoorW / 2f;
        // Ana cephe duvarı, ortada kapı boşluğu
        Gfx.Box("Cephe_Sol", t, new Vector3(-side, FH / 2f, zw), new Vector3(sideW, FH, 1f), stone, true);
        Gfx.Box("Cephe_Sag", t, new Vector3(side, FH / 2f, zw), new Vector3(sideW, FH, 1f), stone, true);
        Gfx.Box("Cephe_Ust", t, new Vector3(0, DoorH + (FH - DoorH) / 2f, zw), new Vector3(DoorW, FH - DoorH, 1f), stone, true);

        float zf = ZF - 0.06f; // cephenin önüne konan süslerin ekseni
        // Alt kuşak, çini bantları, silmeler
        Gfx.Box("Cephe_Kaide", t, new Vector3(0, 0.45f, zf), new Vector3(hw * 2f + 0.2f, 0.9f, 0.14f), stoneDark);
        Gfx.Box("Cephe_Cini", t, new Vector3(0, 11.6f, zf), new Vector3(hw * 2f + 0.2f, 0.55f, 0.14f), turq);
        Gfx.Box("Cephe_Silme", t, new Vector3(0, 12.1f, zf), new Vector3(hw * 2f + 0.3f, 0.2f, 0.4f), gold);
        for (float x = -hw + 0.5f; x <= hw - 0.4f; x += 1.4f) // Selçuklu mazgal dişleri
            Gfx.Box("Mazgal", t, new Vector3(x, 12.75f, ZF + 0.5f), new Vector3(0.75f, 0.9f, 0.7f), stoneDark);
        // Geçme motif sırası (dörtgenler) iki yan kanatta
        foreach (int s in new[] { -1, 1 })
            for (float x = 7.8f; x <= 16.8f; x += 1.2f)
                foreach (float y in new[] { 1.5f, 10.6f })
                {
                    bool alt = Mathf.RoundToInt((x - 7.8f) / 1.2f) % 2 == 0;
                    var d = Gfx.Box("Motif", t, new Vector3(s * x, y, zf - 0.02f), new Vector3(0.62f, 0.62f, 0.06f), alt ? turq : gold);
                    d.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                }

        // ---- Taç kapı (eyvan)
        float pz = ZF - PortalZ / 2f;                   // taç kapı gövdesinin ekseni
        float front = ZF - PortalZ;                     // taç kapının ön yüzü
        float ph = PortalH;
        Gfx.Box("Tac_Sol", t, new Vector3(-5f, ph / 2f, pz), new Vector3(4f, ph, PortalZ), stone, true);
        Gfx.Box("Tac_Sag", t, new Vector3(5f, ph / 2f, pz), new Vector3(4f, ph, PortalZ), stone, true);
        Gfx.Box("Tac_Ust", t, new Vector3(0, DoorH + (ph - DoorH) / 2f, pz), new Vector3(DoorW, ph - DoorH, PortalZ), stone, true);
        Gfx.Box("Tac_Silme", t, new Vector3(0, ph + 0.15f, pz), new Vector3(14.6f, 0.3f, PortalZ + 0.3f), gold);
        Gfx.Box("Tac_Cini", t, new Vector3(0, ph - 0.45f, front - 0.05f), new Vector3(14f, 0.5f, 0.1f), turq);
        for (float x = -6.6f; x <= 6.7f; x += 1.1f) // taç kapının üst mazgalı
            Gfx.Box("Tac_Mazgal", t, new Vector3(x, ph + 0.8f, pz), new Vector3(0.6f, 0.9f, 0.8f), stoneDark);

        // Sivri kemer: turkuaz çerçeve → altın çizgi → lacivert girinti, üst üste (kapı boşluğu açık kalır)
        Arch(t, "Kemer_Cerceve", front - 0.03f, 0.06f, 5.6f, 12.0f, turq);
        Arch(t, "Kemer_Altin", front - 0.06f, 0.06f, 5.2f, 11.6f, gold);
        Arch(t, "Kemer_Girinti", front - 0.09f, 0.06f, 4.9f, 11.3f, Gfx.Lit(Navy, 0.4f));
        Mukarnas(t, front - 0.15f, turq, gold, 4.9f, 11.3f);
        // Kapı söveleri
        foreach (int s in new[] { -1, 1 })
            Gfx.Box("Sove", t, new Vector3(s * (DoorW / 2f + 0.15f), DoorH / 2f, front - 0.2f), new Vector3(0.3f, DoorH, 0.12f), turqDark);
        Gfx.Box("Sove", t, new Vector3(0, DoorH + 0.15f, front - 0.2f), new Vector3(DoorW + 0.6f, 0.3f, 0.12f), turqDark);

        // ---- Tabela: BORSA MTBS + logo + endeks
        Gfx.Box("Tabela", t, new Vector3(0, 13.45f, front - 0.08f), new Vector3(12.6f, 2.5f, 0.12f), Gfx.Lit(Panel, 0.8f));
        Gfx.Box("Tabela_Cerceve", t, new Vector3(0, 14.74f, front - 0.1f), new Vector3(12.8f, 0.1f, 0.14f), gold);
        Gfx.Box("Tabela_Cerceve", t, new Vector3(0, 12.16f, front - 0.1f), new Vector3(12.8f, 0.1f, 0.14f), gold);
        var logo = Resources.Load<Texture2D>("tmtbs_logo");
        if (logo != null)
            Gfx.Prim(PrimitiveType.Quad, "Logo", t, new Vector3(-5.1f, 13.55f, front - 0.16f), new Vector3(2.0f, 2.0f, 1f), Gfx.Texture(logo), false);
        Gfx.Text("Tabela_Yazi", t, new Vector3(0.9f, 13.95f, front - 0.17f), Quaternion.identity, "BORSA MTBS", 1.05f, Gold, TextAnchor.MiddleCenter, TextAlignment.Center);
        b.facadeIndex = Gfx.Text("Tabela_Endeks", t, new Vector3(0.9f, 12.75f, front - 0.17f), Quaternion.identity, "", 0.5f, Color.white, TextAnchor.MiddleCenter, TextAlignment.Center);

        // ---- Yan kanatlarda: en çok yükselen / düşen 5 marka
        for (int p = 0; p < 2; p++)
        {
            float cx = p == 0 ? -11.8f : 11.8f;
            bool up = p == 0;
            float pzf = ZF - 0.06f;
            Gfx.Box("Cephe_Pano", t, new Vector3(cx, 6f, pzf), new Vector3(8.8f, 7f, 0.12f), Gfx.Lit(Panel, 0.8f));
            var edge = up ? Gfx.Glow(new Color(0.23f, 0.82f, 0.43f)) : Gfx.Glow(new Color(1f, 0.35f, 0.35f));
            Gfx.Box("Cephe_Pano_Cerceve", t, new Vector3(cx, 9.54f, pzf - 0.02f), new Vector3(8.9f, 0.08f, 0.14f), edge);
            Gfx.Box("Cephe_Pano_Cerceve", t, new Vector3(cx, 2.46f, pzf - 0.02f), new Vector3(8.9f, 0.08f, 0.14f), edge);
            float zt = pzf - 0.1f;
            Gfx.Text("Pano_Baslik", t, new Vector3(cx, 8.85f, zt), Quaternion.identity,
                up ? "EN ÇOK YÜKSELEN 5" : "EN ÇOK DÜŞEN 5", 0.6f, up ? new Color(0.23f, 0.82f, 0.43f) : new Color(1f, 0.4f, 0.4f));
            float top = 8.0f;
            b.fNames[p] = Gfx.Text("Pano_Ad", t, new Vector3(cx - 4.1f, top, zt), Quaternion.identity, "", 0.42f, Color.white, TextAnchor.UpperLeft, TextAlignment.Left);
            b.fPrices[p] = Gfx.Text("Pano_Fiyat", t, new Vector3(cx + 1.3f, top, zt), Quaternion.identity, "", 0.42f, new Color(1f, 0.92f, 0.6f), TextAnchor.UpperRight, TextAlignment.Right);
            b.fChanges[p] = Gfx.Text("Pano_Degisim", t, new Vector3(cx + 4.1f, top, zt), Quaternion.identity, "", 0.42f, Color.white, TextAnchor.UpperRight, TextAlignment.Right);
            Gfx.Text("Pano_Alt", t, new Vector3(cx, 3.0f, zt), Quaternion.identity, "canlı borsa · Trade of MTBS", 0.26f, new Color(0.6f, 0.65f, 0.75f));
        }
    }

    /// <summary>Sivri (ogival) kemer: üst üste daralan dilimlerle çizilir. Kapı boşluğunun içi boş kalır.</summary>
    static void Arch(Transform t, string name, float zc, float depth, float hw, float h, Material m)
    {
        const int n = 18;
        float sh = h / n;
        for (int i = 0; i < n; i++)
        {
            float frac = (i + 0.5f) / n;
            float half = hw * (1f - Mathf.Pow(frac, 2.3f));
            if (half < 0.05f) continue;
            float y0 = i * sh, yc = y0 + sh / 2f;
            if (y0 < DoorH - 0.01f)
            {
                float w = half - DoorW / 2f;
                if (w <= 0.02f) continue;
                foreach (int s in new[] { -1, 1 })
                    Gfx.Box(name, t, new Vector3(s * (DoorW / 2f + w / 2f), yc, zc), new Vector3(w, sh * 1.02f, depth), m);
            }
            else Gfx.Box(name, t, new Vector3(0, yc, zc), new Vector3(half * 2f, sh * 1.02f, depth), m);
        }
    }

    /// <summary>Kemerin üst kısmında mukarnas (petek) sıraları: yarım küp dizileri.</summary>
    static void Mukarnas(Transform t, float zc, Material a, Material b, float hw, float h)
    {
        for (int r = 0; r < 5; r++)
        {
            float y = 6.4f + r * 0.78f;
            float frac = y / h;
            float half = hw * (1f - Mathf.Pow(frac, 2.3f)) - 0.35f;
            if (half < 0.4f) break;
            float off = (r % 2) * 0.33f;
            int k = 0;
            for (float x = -half + off; x <= half; x += 0.66f, k++)
                Gfx.Box("Mukarnas", t, new Vector3(x, y, zc), new Vector3(0.5f, 0.6f, 0.12f), (k + r) % 2 == 0 ? a : b);
        }
    }

    // ================================================================ minareler, kubbe, lambalar

    static void BuildMinarets(Transform t, Material stone, Material stoneDark, Material turq, Material gold)
    {
        foreach (int s in new[] { -1, 1 })
        {
            float x = s * (SalonBuilder.HalfW + 0.3f);
            float z = ZF + 0.9f;
            var m = new GameObject("Minare").transform;
            m.SetParent(t, false);
            m.localPosition = new Vector3(x, 0, z);
            Gfx.Box("Kaide", m, new Vector3(0, 1.6f, 0), new Vector3(2.8f, 3.2f, 2.8f), stoneDark, true);
            Gfx.Prim(PrimitiveType.Cylinder, "Govde", m, new Vector3(0, 10.2f, 0), new Vector3(2.0f, 7.0f, 2.0f), stone, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Cini_Bant", m, new Vector3(0, 7.0f, 0), new Vector3(2.12f, 0.45f, 2.12f), turq, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Cini_Bant", m, new Vector3(0, 11.5f, 0), new Vector3(2.12f, 0.45f, 2.12f), turq, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Sah_Nisin", m, new Vector3(0, 15.4f, 0), new Vector3(3.2f, 0.2f, 3.2f), stoneDark, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Sah_Nisin_Altin", m, new Vector3(0, 15.65f, 0), new Vector3(3.3f, 0.06f, 3.3f), gold, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Ust_Govde", m, new Vector3(0, 17.2f, 0), new Vector3(1.5f, 1.6f, 1.5f), stone, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Kep", m, new Vector3(0, 18.9f, 0), new Vector3(1.7f, 0.12f, 1.7f), turq, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Kep", m, new Vector3(0, 19.4f, 0), new Vector3(1.2f, 0.4f, 1.2f), turq, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Kep", m, new Vector3(0, 20.1f, 0), new Vector3(0.7f, 0.35f, 0.7f), turq, false);
            Gfx.Prim(PrimitiveType.Sphere, "Alem", m, new Vector3(0, 20.7f, 0), Vector3.one * 0.35f, gold, false);
        }
    }

    static void BuildDome(Transform t, Material stoneDark, Material turq, Material gold)
    {
        float cz = (Z0 + ZF) / 2f;
        Gfx.Prim(PrimitiveType.Cylinder, "Kubbe_Kasnak", t, new Vector3(0, FH + 1.5f, cz), new Vector3(10.6f, 1.0f, 10.6f), stoneDark, false);
        Gfx.Prim(PrimitiveType.Sphere, "Kubbe", t, new Vector3(0, FH + 2.5f, cz), new Vector3(10f, 10f, 10f), turq, false);
        Gfx.Prim(PrimitiveType.Cylinder, "Kubbe_Altin", t, new Vector3(0, FH + 2.5f, cz), new Vector3(10.15f, 0.05f, 10.15f), gold, false);
        Gfx.Prim(PrimitiveType.Sphere, "Kubbe_Alem", t, new Vector3(0, FH + 7.7f, cz), Vector3.one * 0.5f, gold, false);
    }

    static void BuildLamps(Transform t, Material gold)
    {
        // Meydanda halının iki yanında lambalıklar
        for (int i = 0; i < 6; i++)
            foreach (int s in new[] { -1, 1 })
            {
                float z = ZF - 6f - i * 4.5f;
                Gfx.Prim(PrimitiveType.Cylinder, "Direk", t, new Vector3(s * 4.2f, 1.6f, z), new Vector3(0.18f, 1.6f, 0.18f), Gfx.Lit(new Color(0.15f, 0.16f, 0.2f), 0.6f), false);
                Gfx.Prim(PrimitiveType.Sphere, "Fener", t, new Vector3(s * 4.2f, 3.4f, z), Vector3.one * 0.5f, Gfx.Glow(new Color(1f, 0.9f, 0.6f)), false);
            }
        foreach (var p in new[] { new Vector3(-10f, 8f, ZF - 7.5f), new Vector3(10f, 8f, ZF - 7.5f), new Vector3(0f, 12f, ZF - 9.5f), new Vector3(0f, 4f, ZF - 5.5f) })
        {
            var lg = new GameObject("Dis_Lamba");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = p;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 24f;
            l.intensity = 1.5f;
            l.color = new Color(1f, 0.93f, 0.8f);
        }
    }
}
