using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir hissenin işlem kürsüsü: iki yüzlü ekran (ad, fiyat, değişim), üstte renkli ışık halkası.
/// Fiyat yükselince yeşil, düşünce kırmızı yanıp söner.
/// </summary>
public class Kiosk : MonoBehaviour
{
    public static readonly List<Kiosk> All = new List<Kiosk>();

    public int slot;              // piyasa değerine göre sıra (0 = en büyük)
    public Stock stock;
    public TextMesh[] nameText = new TextMesh[2];
    public TextMesh[] priceText = new TextMesh[2];
    public TextMesh[] changeText = new TextMesh[2];
    public Renderer ring;
    public Renderer screenGlow;

    Color target = Color.grey;
    float flashUntil;
    Color flashColor;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public Vector3 StandPoint(int side = -1) => transform.position + new Vector3(0, 0, side * 1.9f);

    public void Bind(Stock s)
    {
        stock = s;
        if (s == null)
        {
            SetTexts("—", "", "");
            target = new Color(0.3f, 0.32f, 0.36f);
            return;
        }
        string chg = $"<color={Fmt.ChangeColor(s.change)}>{Fmt.Pct(s.change)}</color>";
        SetTexts(s.name, Fmt.N2(s.price), chg);
        target = s.change > 0.05 ? new Color(0.2f, 0.85f, 0.4f) : s.change < -0.05 ? new Color(1f, 0.3f, 0.3f) : new Color(0.75f, 0.78f, 0.85f);
        if (s.prevPrice > 0 && s.price != s.prevPrice)
        {
            flashColor = s.price > s.prevPrice ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f);
            flashUntil = Time.time + 1.2f;
        }
    }

    void SetTexts(string n, string p, string c)
    {
        string shortName = n.Length > 16 ? n.Substring(0, 15) + "…" : n;
        for (int i = 0; i < 2; i++)
        {
            if (nameText[i]) nameText[i].text = shortName;
            if (priceText[i]) priceText[i].text = p;
            if (changeText[i]) changeText[i].text = c;
        }
    }

    void Update()
    {
        if (!ring) return;
        Color c = target;
        if (Time.time < flashUntil)
            c = Color.Lerp(target, flashColor, Mathf.PingPong(Time.time * 6f, 1f));
        // Malzemeyi paylaşmamak için örnek malzeme (12 kürsü, sorun değil)
        ring.material.color = c;
    }
}
