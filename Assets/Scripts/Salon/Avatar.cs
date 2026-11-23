using UnityEngine;

/// <summary>Bir borsacının görünümü: cinsiyet, ten, saç biçimi/rengi, takım rengi, sakal.</summary>
public struct Look
{
    public bool female;
    public int skin, hairStyle, hairColor, suit;
    public bool beard;
    /// <summary>Görevli üniforması (turkuaz ceket, lacivert etek, altın kemer).</summary>
    public bool uniform;

    const string Key = "tmtbs_salon_look";

    public static Look Random(System.Random r)
    {
        var l = new Look
        {
            female = r.NextDouble() < 0.5,
            skin = r.Next(Avatar.Skins.Length),
            hairStyle = r.Next(4),
            hairColor = r.Next(Avatar.Hairs.Length),
            suit = r.Next(Avatar.Suits.Length),
        };
        l.beard = !l.female && r.NextDouble() < 0.3;
        return l;
    }

    /// <summary>Oyuncunun kayıtlı seçimi (yoksa varsayılan erkek, lacivert takım).</summary>
    public static Look Load()
    {
        var l = new Look { skin = 0, hairStyle = 0, hairColor = 1, suit = 0 };
        try
        {
            string s = PlayerPrefs.GetString(Key, "");
            var p = s.Split(',');
            if (p.Length == 6)
            {
                l.female = p[0] == "1";
                l.skin = Mathf.Clamp(int.Parse(p[1]), 0, Avatar.Skins.Length - 1);
                l.hairStyle = Mathf.Clamp(int.Parse(p[2]), 0, 3);
                l.hairColor = Mathf.Clamp(int.Parse(p[3]), 0, Avatar.Hairs.Length - 1);
                l.suit = Mathf.Clamp(int.Parse(p[4]), 0, Avatar.Suits.Length - 1);
                l.beard = p[5] == "1";
            }
        }
        catch { }
        return l;
    }

    public void Save()
    {
        try
        {
            PlayerPrefs.SetString(Key, $"{(female ? 1 : 0)},{skin},{hairStyle},{hairColor},{suit},{(beard ? 1 : 0)}");
            PlayerPrefs.Save();
        }
        catch { }
    }
}

/// <summary>Borsacı gövdesini (kadın/erkek, farklı tipler) koddan kurar. Görseller "Gorunum" altında durur; yeniden kurulabilir.</summary>
public static class Avatar
{
    public static readonly Color[] Skins =
    {
        new Color(0.93f, 0.76f, 0.64f), new Color(0.86f, 0.68f, 0.54f), new Color(0.76f, 0.56f, 0.42f),
        new Color(0.58f, 0.40f, 0.30f), new Color(0.38f, 0.26f, 0.20f),
    };
    public static readonly Color[] Hairs =
    {
        new Color(0.08f, 0.06f, 0.05f), new Color(0.30f, 0.17f, 0.09f), new Color(0.55f, 0.22f, 0.10f),
        new Color(0.86f, 0.70f, 0.38f), new Color(0.65f, 0.65f, 0.68f),
    };
    public static readonly Color[] Suits =
    {
        new Color(0.08f, 0.12f, 0.30f), new Color(0.20f, 0.20f, 0.22f), new Color(0.40f, 0.10f, 0.16f),
        new Color(0.10f, 0.28f, 0.20f), new Color(0.05f, 0.45f, 0.48f),
    };
    static readonly Color[] Accents =
    {
        new Color(0.85f, 0.15f, 0.20f), new Color(0.9f, 0.7f, 0.1f), new Color(0.2f, 0.4f, 0.9f),
        new Color(0.9f, 0.9f, 0.9f), new Color(1f, 0.8f, 0.3f),
    };

    public static readonly string[] SkinNames = { "Açık", "Buğday", "Bronz", "Esmer", "Koyu" };
    public static readonly string[] HairColorNames = { "Siyah", "Kahve", "Kızıl", "Sarı", "Gri" };
    public static readonly string[] SuitNames = { "Lacivert", "Füme", "Bordo", "Yeşil", "Turkuaz" };
    public static readonly string[] FemaleHair = { "Uzun", "Topuz", "Küt", "At kuyruğu" };
    public static readonly string[] MaleHair = { "Kısa", "Yana taralı", "Dazlak", "Kıvırcık" };

    static GameObject P(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
        => Gfx.Prim(type, name, parent, pos, scale, m, false);

    static Transform Pivot(string name, Transform parent, Vector3 pos)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = pos;
        return g;
    }

    /// <summary>
    /// İnsan oranlı (yaklaşık 1,8 m) gövde: boyun, gövde, eklemli kol/bacak, eller, ayakkabılar, kulak/burun/göz/kaş/dudak, saç.
    /// Kol ve bacaklar pivotlara bağlıdır; AvatarAnimator yürürken sallar.
    /// </summary>
    public static void Rebuild(Transform owner, Look l)
    {
        var old = owner.Find("Gorunum");
        if (old != null) { old.name = "Gorunum_eski"; Object.Destroy(old.gameObject); }
        var t = new GameObject("Gorunum").transform;
        t.SetParent(owner, false);

        bool f = l.female;
        Color skin = Skins[l.skin % Skins.Length];
        Color hair = Hairs[l.hairColor % Hairs.Length];
        Color suit = l.uniform ? SeljukBuilding.Turquoise : Suits[l.suit % Suits.Length];
        Color accent = l.uniform ? new Color(1f, 0.80f, 0.30f) : Accents[l.suit % Accents.Length];
        Color lower = l.uniform ? new Color(0.06f, 0.12f, 0.30f) : suit * 0.62f;
        lower.a = 1f; suit.a = 1f;
        bool skirt = f && (l.uniform || (l.hairStyle + l.suit) % 2 == 0);

        var skinM = Gfx.Lit(skin, 0.35f);
        var skinDark = Gfx.Lit(skin * 0.82f, 0.3f);
        var hairM = Gfx.Lit(hair, 0.45f);
        var suitM = Gfx.Lit(suit, 0.3f);
        var lowerM = Gfx.Lit(lower, 0.2f);
        var white = Gfx.Lit(new Color(0.96f, 0.96f, 0.96f), 0.2f);
        var dark = Gfx.Lit(new Color(0.07f, 0.06f, 0.06f), 0.5f);
        var iris = Gfx.Lit(new Color(0.20f, 0.12f, 0.06f), 0.6f);
        var gold = Gfx.Glow(new Color(1f, 0.80f, 0.30f));
        var accentM = Gfx.Lit(accent, 0.3f);

        float shW = f ? 0.20f : 0.245f;      // omuz yarı genişliği
        float chestW = f ? 0.35f : 0.43f;
        float hipW = f ? 0.36f : 0.34f;

        // ---- Gövde
        var body = Pivot("Govde", t, Vector3.zero);
        P(PrimitiveType.Sphere, "Gogus", body, new Vector3(0, 1.28f, 0), new Vector3(chestW, 0.40f, f ? 0.22f : 0.25f), suitM);
        P(PrimitiveType.Sphere, "Karin", body, new Vector3(0, 1.04f, 0), new Vector3(f ? 0.29f : 0.35f, 0.34f, f ? 0.20f : 0.23f), suitM);
        if (skirt)
        {
            P(PrimitiveType.Cylinder, "Etek", body, new Vector3(0, 0.80f, 0), new Vector3(0.37f, 0.17f, 0.27f), lowerM);
            P(PrimitiveType.Sphere, "Kalca", body, new Vector3(0, 0.90f, 0), new Vector3(hipW, 0.24f, 0.25f), lowerM);
        }
        else
        {
            P(PrimitiveType.Sphere, "Kalca", body, new Vector3(0, 0.90f, 0), new Vector3(hipW, 0.26f, 0.24f), lowerM);
            if (!f) P(PrimitiveType.Sphere, "Ceket_Alt", body, new Vector3(0, 0.96f, 0), new Vector3(0.37f, 0.22f, 0.25f), suitM);
        }
        // Kemer / boyun çizgisi
        Gfx.Box("Kemer", body, new Vector3(0, 0.90f, f ? 0.11f : 0.12f), new Vector3(f ? 0.26f : 0.30f, 0.035f, 0.02f), l.uniform ? gold : dark);
        // Yaka, gömlek/bluz, kravat/atkı
        if (!f)
        {
            Gfx.Box("Gomlek", body, new Vector3(0, 1.32f, 0.118f), new Vector3(0.11f, 0.30f, 0.02f), white);
            Gfx.Box("Kravat", body, new Vector3(0, 1.27f, 0.131f), new Vector3(0.045f, 0.28f, 0.015f), accentM);
            Gfx.Box("Kravat_Dugum", body, new Vector3(0, 1.425f, 0.131f), new Vector3(0.05f, 0.04f, 0.02f), accentM);
            foreach (int s in new[] { -1, 1 })
            {
                var rev = Gfx.Box("Yaka_Rever", body, new Vector3(s * 0.075f, 1.31f, 0.125f), new Vector3(0.05f, 0.30f, 0.012f), suitM);
                rev.transform.localRotation = Quaternion.Euler(0, 0, s * -9f);
            }
        }
        else
        {
            Gfx.Box("Bluz", body, new Vector3(0, 1.34f, 0.108f), new Vector3(0.12f, 0.20f, 0.02f), l.uniform ? white : Gfx.Lit(accent * 0.9f + new Color(0.05f, 0.05f, 0.05f), 0.3f));
            if (l.uniform)
            {
                Gfx.Box("Serit", body, new Vector3(0.10f, 1.22f, 0.112f), new Vector3(0.025f, 0.34f, 0.012f), gold);
                Gfx.Box("Yaka", body, new Vector3(0, 1.43f, 0.11f), new Vector3(0.14f, 0.03f, 0.02f), white);
            }
            else
            {
                // İnce zincir kolye
                P(PrimitiveType.Sphere, "Kolye", body, new Vector3(0, 1.38f, 0.118f), Vector3.one * 0.025f, gold);
            }
        }

        // ---- Boyun ve baş
        P(PrimitiveType.Cylinder, "Boyun", body, new Vector3(0, 1.50f, 0), new Vector3(f ? 0.075f : 0.10f, 0.07f, f ? 0.075f : 0.10f), skinDark);
        var head = Pivot("Bas", body, new Vector3(0, 0, 0));
        float hw = f ? 0.165f : 0.18f;
        P(PrimitiveType.Sphere, "Kafa", head, new Vector3(0, 1.675f, 0), new Vector3(hw, 0.225f, 0.19f), skinM);
        P(PrimitiveType.Sphere, "Cene", head, new Vector3(0, 1.605f, 0.02f), new Vector3(hw * 0.82f, 0.15f, 0.15f), skinM);
        foreach (int s in new[] { -1, 1 })
        {
            P(PrimitiveType.Sphere, "Kulak", head, new Vector3(s * hw * 0.5f, 1.67f, -0.005f), new Vector3(0.025f, 0.05f, 0.032f), skinM);
            P(PrimitiveType.Sphere, "Goz_Beyaz", head, new Vector3(s * 0.04f, 1.69f, 0.083f), new Vector3(0.042f, 0.026f, 0.02f), white);
            P(PrimitiveType.Sphere, "Goz_Iris", head, new Vector3(s * 0.04f, 1.69f, 0.093f), new Vector3(0.02f, 0.02f, 0.012f), iris);
            var brow = Gfx.Box("Kas", head, new Vector3(s * 0.042f, f ? 1.727f : 1.722f, 0.088f), new Vector3(0.052f, f ? 0.008f : 0.013f, 0.014f), hairM);
            brow.transform.localRotation = Quaternion.Euler(0, 0, s * (f ? -6f : -3f));
        }
        P(PrimitiveType.Sphere, "Burun", head, new Vector3(0, 1.652f, 0.097f), new Vector3(0.032f, 0.046f, 0.04f), skinM);
        Gfx.Box("Dudak", head, new Vector3(0, 1.595f, 0.098f), new Vector3(0.048f, f ? 0.014f : 0.010f, 0.012f),
            Gfx.Lit(f ? new Color(0.72f, 0.22f, 0.28f) : skin * 0.72f, 0.4f));

        // ---- Saç
        switch (f ? l.hairStyle % 4 : 4 + l.hairStyle % 4)
        {
            case 0: // uzun
                P(PrimitiveType.Sphere, "Sac", head, new Vector3(0, 1.725f, -0.012f), new Vector3(hw * 1.14f, 0.215f, 0.225f), hairM);
                Gfx.Box("Sac_Arka", head, new Vector3(0, 1.50f, -0.095f), new Vector3(0.20f, 0.42f, 0.06f), hairM);
                foreach (int s in new[] { -1, 1 })
                    P(PrimitiveType.Capsule, "Sac_Yan", head, new Vector3(s * 0.088f, 1.58f, 0.005f), new Vector3(0.05f, 0.15f, 0.07f), hairM);
                Gfx.Box("Kakul", head, new Vector3(0, 1.775f, 0.075f), new Vector3(0.17f, 0.04f, 0.05f), hairM);
                break;
            case 1: // topuz
                P(PrimitiveType.Sphere, "Sac", head, new Vector3(0, 1.725f, -0.01f), new Vector3(hw * 1.12f, 0.21f, 0.22f), hairM);
                P(PrimitiveType.Sphere, "Topuz", head, new Vector3(0, 1.855f, -0.07f), Vector3.one * 0.105f, hairM);
                Gfx.Box("Kakul", head, new Vector3(0, 1.775f, 0.075f), new Vector3(0.16f, 0.035f, 0.045f), hairM);
                break;
            case 2: // küt
                P(PrimitiveType.Sphere, "Sac", head, new Vector3(0, 1.725f, -0.012f), new Vector3(hw * 1.16f, 0.22f, 0.23f), hairM);
                foreach (int s in new[] { -1, 1 })
                    Gfx.Box("Sac_Yan", head, new Vector3(s * 0.092f, 1.60f, -0.005f), new Vector3(0.04f, 0.17f, 0.17f), hairM);
                Gfx.Box("Sac_Arka", head, new Vector3(0, 1.595f, -0.095f), new Vector3(0.19f, 0.15f, 0.05f), hairM);
                Gfx.Box("Kakul", head, new Vector3(0, 1.775f, 0.075f), new Vector3(0.17f, 0.04f, 0.05f), hairM);
                break;
            case 3: // at kuyruğu
                P(PrimitiveType.Sphere, "Sac", head, new Vector3(0, 1.725f, -0.01f), new Vector3(hw * 1.12f, 0.21f, 0.22f), hairM);
                var tail = P(PrimitiveType.Capsule, "Kuyruk", head, new Vector3(0, 1.60f, -0.145f), new Vector3(0.065f, 0.17f, 0.065f), hairM);
                tail.transform.localRotation = Quaternion.Euler(18f, 0, 0);
                P(PrimitiveType.Sphere, "Toka", head, new Vector3(0, 1.735f, -0.115f), Vector3.one * 0.04f, accentM);
                Gfx.Box("Kakul", head, new Vector3(0, 1.775f, 0.075f), new Vector3(0.16f, 0.035f, 0.045f), hairM);
                break;
            case 4: // kısa
                P(PrimitiveType.Sphere, "Sac", head, new Vector3(0, 1.755f, -0.01f), new Vector3(hw * 1.1f, 0.15f, 0.215f), hairM);
                break;
            case 5: // yana taralı
                P(PrimitiveType.Sphere, "Sac", head, new Vector3(0, 1.755f, -0.01f), new Vector3(hw * 1.1f, 0.155f, 0.215f), hairM);
                var sw = Gfx.Box("Tarama", head, new Vector3(0.045f, 1.815f, 0.045f), new Vector3(0.13f, 0.03f, 0.12f), hairM);
                sw.transform.localRotation = Quaternion.Euler(0, 0, -8f);
                break;
            case 6: // dazlak: hafif gölge
                P(PrimitiveType.Sphere, "Sac_Golge", head, new Vector3(0, 1.755f, -0.01f), new Vector3(hw * 1.03f, 0.15f, 0.2f), Gfx.Lit(skin * 0.9f, 0.5f));
                break;
            default: // kıvırcık
                P(PrimitiveType.Sphere, "Sac", head, new Vector3(0, 1.775f, -0.01f), new Vector3(hw * 1.28f, 0.20f, 0.25f), hairM);
                break;
        }
        if (!f && l.beard)
        {
            P(PrimitiveType.Sphere, "Sakal", head, new Vector3(0, 1.585f, 0.055f), new Vector3(0.135f, 0.10f, 0.105f), hairM);
            Gfx.Box("Biyik", head, new Vector3(0, 1.616f, 0.105f), new Vector3(0.075f, 0.014f, 0.014f), hairM);
        }

        // ---- Bacaklar (kalça pivotundan sallanır)
        float hipX = f ? 0.085f : 0.10f;
        var legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            int s = i == 0 ? -1 : 1;
            var leg = Pivot(i == 0 ? "Bacak_Sol" : "Bacak_Sag", t, new Vector3(s * hipX, 0.86f, 0));
            legs[i] = leg;
            var legM = skirt ? skinM : lowerM;
            P(PrimitiveType.Capsule, "Uyluk", leg, new Vector3(0, -0.20f, 0), new Vector3(0.155f, 0.21f, 0.155f), legM);
            P(PrimitiveType.Capsule, "Baldir", leg, new Vector3(0, -0.60f, 0), new Vector3(0.115f, 0.20f, 0.115f), legM);
            Gfx.Box("Ayakkabi", leg, new Vector3(0, -0.815f, 0.05f), new Vector3(0.10f, 0.07f, 0.26f), dark);
            if (f && !l.uniform) Gfx.Box("Topuk", leg, new Vector3(0, -0.80f, -0.06f), new Vector3(0.06f, 0.10f, 0.04f), dark);
        }

        // ---- Kollar (omuz pivotundan sallanır): üst kol, dirsek, ön kol, kol düğmesi, el
        var arms = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            int s = i == 0 ? -1 : 1;
            var arm = Pivot(i == 0 ? "Kol_Sol" : "Kol_Sag", t, new Vector3(s * shW, 1.41f, 0));
            arms[i] = arm;
            P(PrimitiveType.Sphere, "Omuz", arm, Vector3.zero, Vector3.one * (f ? 0.105f : 0.13f), suitM);
            P(PrimitiveType.Capsule, "Ustkol", arm, new Vector3(0, -0.15f, 0), new Vector3(f ? 0.085f : 0.10f, 0.165f, f ? 0.085f : 0.10f), suitM);
            var fore = Pivot("Onkol", arm, new Vector3(0, -0.31f, 0));
            fore.localRotation = Quaternion.Euler(-14f, 0, 0);
            P(PrimitiveType.Capsule, "Onkol_Govde", fore, new Vector3(0, -0.14f, 0), new Vector3(f ? 0.07f : 0.085f, 0.15f, f ? 0.07f : 0.085f), suitM);
            P(PrimitiveType.Cylinder, "Manset", fore, new Vector3(0, -0.275f, 0), new Vector3(f ? 0.066f : 0.08f, 0.012f, f ? 0.066f : 0.08f), white);
            P(PrimitiveType.Sphere, "El", fore, new Vector3(0, -0.325f, 0.005f), new Vector3(0.068f, 0.10f, 0.045f), skinM);
            if (i == 1 && !l.uniform)
                Gfx.Box("Dosya", fore, new Vector3(0.02f, -0.35f, 0.05f), new Vector3(0.025f, 0.26f, 0.20f), Gfx.Lit(new Color(0.9f, 0.85f, 0.7f)));
        }

        var anim = t.gameObject.AddComponent<AvatarAnimator>();
        anim.legs = legs;
        anim.arms = arms;
        anim.body = body;
        anim.head = head;
    }
}

/// <summary>Yürürken kol ve bacakları sallar, dururken hafif nefes/duruş hareketi yapar.</summary>
public class AvatarAnimator : MonoBehaviour
{
    public Transform[] legs, arms;
    public Transform body, head;
    Rigidbody rb;
    float phase, seed;
    Vector3 lastPos;

    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
        seed = Random.value * 10f;
        lastPos = transform.position;
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = 0f;
        if (rb != null)
        {
            speed = rb.GetVelocity().Flat().magnitude;
        }
        else
        {
            speed = (transform.position - lastPos).Flat().magnitude / dt;
            lastPos = transform.position;
        }
        float walk = Mathf.Clamp01(speed / 2.2f);
        phase += dt * (2.5f + speed * 1.9f);
        float amp = 34f * walk;
        float sw = Mathf.Sin(phase);

        if (legs != null && legs.Length == 2)
        {
            legs[0].localRotation = Quaternion.Euler(sw * amp, 0, 0);
            legs[1].localRotation = Quaternion.Euler(-sw * amp, 0, 0);
        }
        if (arms != null && arms.Length == 2)
        {
            float rest = 3f;
            arms[0].localRotation = Quaternion.Euler(-sw * amp * 0.8f, 0, rest);
            arms[1].localRotation = Quaternion.Euler(sw * amp * 0.8f, 0, -rest);
        }
        float t = Time.time + seed;
        if (body != null)
            body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(phase)) * 0.025f * walk + Mathf.Sin(t * 1.6f) * 0.003f * (1f - walk), 0);
        if (head != null)
            head.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.5f) * 5f * (1f - walk), 0);
    }
}
