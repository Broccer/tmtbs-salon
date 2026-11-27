using UnityEngine;

/// <summary>
/// Binanın karşılama görevlileri: hepsi kadın, aynı üniforma (turkuaz ceket, lacivert etek, altın şerit),
/// ama farklı tip: saç biçimi/rengi, ten rengi ve boy. Oyuncu yaklaşınca kısa bir söz söylerler.
/// </summary>
public static class Attendants
{
    static readonly Color[] Skins =
    {
        new Color(0.93f, 0.76f, 0.64f), new Color(0.80f, 0.60f, 0.45f),
        new Color(0.58f, 0.40f, 0.30f), new Color(0.88f, 0.70f, 0.58f),
    };
    static readonly Color[] Hairs =
    {
        new Color(0.08f, 0.06f, 0.05f), new Color(0.30f, 0.17f, 0.09f),
        new Color(0.55f, 0.22f, 0.10f), new Color(0.86f, 0.70f, 0.38f),
    };

    public static void Build(Transform parent, float zOutside, float zInside)
    {
        var holder = new GameObject("Gorevliler").transform;
        holder.SetParent(parent, false);
        // 0 uzun saç, 1 topuz, 2 kısa küt, 3 yandan örgü/at kuyruğu
        Make(holder, 0, new Vector3(-3.7f, 0, zOutside), 1.00f, Loc.T("Hoş geldiniz!"));
        Make(holder, 1, new Vector3(3.7f, 0, zOutside), 0.96f, Loc.T("Borsa MTBS'ye hoş geldiniz"));
        Make(holder, 2, new Vector3(-2.2f, 0, zInside), 1.03f, Loc.T("İyi seanslar!"));
        Make(holder, 3, new Vector3(2.2f, 0, zInside), 0.98f, Loc.T("Salon ileride →"));
    }

    static void Make(Transform parent, int type, Vector3 pos, float scale, string greeting)
    {
        var g = new GameObject("Gorevli_" + (type + 1));
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, 180f, 0); // gelenlere (-z) döner
        g.transform.localScale = Vector3.one * scale;
        var t = g.transform;

        var col = g.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.height = 1.8f;
        col.radius = 0.3f;

        int[] skinOf = { 0, 2, 3, 1 };
        var lc = g.AddComponent<LazyChar>();
        lc.model = 0; // kadın model
        lc.look = new Look { female = true, skin = skinOf[type % 4], hairStyle = type % 4, hairColor = (type + 1) % Avatar.Hairs.Length, uniform = true };
        lc.idleState = type % 2 == 0 ? 3 : 0;

        // Konuşma balonu
        var bubble = Gfx.Text("Soz", t, new Vector3(0, 2.3f, 0), Quaternion.identity, greeting, 0.26f, Gfx.Hex("#FFD54A"));
        bubble.gameObject.AddComponent<Billboard>();
        bubble.gameObject.SetActive(false);
        var gr = g.AddComponent<Greeter>();
        gr.bubble = bubble;
    }
}

/// <summary>Oyuncu yaklaşınca selamlama sözünü gösterir.</summary>
public class Greeter : MonoBehaviour
{
    public TextMesh bubble;
    public float range = 6f;
    Walker player;

    void Update()
    {
        if (bubble == null) return;
        if (player == null) player = FindAnyObjectByType<Walker>();
        if (player == null) return;
        bool near = (player.transform.position - transform.position).Flat().magnitude < range;
        if (bubble.gameObject.activeSelf != near) bubble.gameObject.SetActive(near);
    }
}
