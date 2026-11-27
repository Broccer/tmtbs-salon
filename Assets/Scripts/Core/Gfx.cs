using System.Collections.Generic;
using UnityEngine;

/// <summary>Koddan nesne üretme yardımcıları (malzeme önbelleği, kutu, yazı).</summary>
public static class Gfx
{
    static Material baseMat, glowMat, unlitMat;
    static Font font;
    static readonly Dictionary<Color, Material> lit = new Dictionary<Color, Material>();
    static readonly Dictionary<Color, Material> glow = new Dictionary<Color, Material>();

    public static Font Font
    {
        get
        {
            if (font == null)
            {
                // Çok dilli Noto yazı tipi (CI'da tools/fonts/build_font.py üretir); yoksa Unity'nin yerleşiği.
                font = Resources.Load<Font>("Fonts/TmtbsSans");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // Yerleşik yazı shader'ı her şeyin önünde çizer; derinlik denemeli olanı kullan.
                var sh = Resources.Load<Shader>("Tmtbs_Text3D");
                if (sh != null && font != null) font.material.shader = sh;
            }
            return font;
        }
    }

    static Material Base()
    {
        if (baseMat == null) baseMat = Resources.Load<Material>("SalonBase");
        if (baseMat == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(tmp);
        }
        return baseMat;
    }

    /// <summary>Işık alan malzeme.</summary>
    public static Material Lit(Color c, float smooth = 0.2f)
    {
        if (lit.TryGetValue(c, out var m)) return m;
        m = new Material(Base()) { color = c };
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        lit[c] = m;
        return m;
    }

    /// <summary>Kendi ışığıyla parlayan (gölgesiz, düz renk) malzeme.</summary>
    public static Material Glow(Color c)
    {
        if (glow.TryGetValue(c, out var m)) return m;
        if (glowMat == null) glowMat = Resources.Load<Material>("SalonGlow");
        m = glowMat != null ? new Material(glowMat) { color = c } : Lit(c, 0.9f);
        glow[c] = m;
        return m;
    }

    public static Material Texture(Texture2D tex)
    {
        if (unlitMat == null) unlitMat = Resources.Load<Material>("SalonUnlit");
        var m = unlitMat != null ? new Material(unlitMat) : new Material(Base());
        m.mainTexture = tex;
        return m;
    }

    public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false)
        => Prim(PrimitiveType.Cube, name, parent, pos, scale, mat, collider);

    /// <summary>
    /// Dünyada duran yazı. height = bir satırın yaklaşık yüksekliği (metre).
    /// Yazı, transformun +z yönüne bakan kameradan okunur.
    /// </summary>
    public static TextMesh Text(string name, Transform parent, Vector3 pos, Quaternion rot, string text, float height,
        Color color, TextAnchor anchor = TextAnchor.MiddleCenter, TextAlignment align = TextAlignment.Center)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        var tm = g.AddComponent<TextMesh>();
        tm.font = Font;
        tm.fontSize = 64;
        tm.characterSize = height * 10f / 64f * 1.4f;
        tm.richText = true;
        tm.anchor = anchor;
        tm.alignment = align;
        tm.color = color;
        tm.text = text;
        g.GetComponent<MeshRenderer>().sharedMaterial = Font.material;
        return tm;
    }

    public static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
