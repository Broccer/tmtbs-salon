using System;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Ön duvardaki dev piyasa tablosu, kayan fiyat bandı, yan duvarlarda saat ve endeks.
/// </summary>
public class Boards : MonoBehaviour
{
    public TextMesh[] nameCols = new TextMesh[2];
    public TextMesh[] priceCols = new TextMesh[2];
    public TextMesh[] changeCols = new TextMesh[2];
    public TextMesh ticker;
    public float tickerLeft = -17f, tickerRight = 17f;
    public TextMesh clockL, clockR;
    public TextMesh indexL, indexR;
    public TextMesh statusText;

    float tickerX;
    float tickerWidth = 60f;

    void Start()
    {
        if (Market.I) Market.I.Changed += Refresh;
        Refresh();
        tickerX = tickerRight;
    }

    void OnDestroy()
    {
        if (Market.I) Market.I.Changed -= Refresh;
    }

    void Refresh()
    {
        var m = Market.I;
        var list = m != null ? m.stocks : null;
        int rows = 7;
        for (int col = 0; col < 2; col++)
        {
            var sbN = new StringBuilder();
            var sbP = new StringBuilder();
            var sbC = new StringBuilder();
            for (int r = 0; r < rows; r++)
            {
                int i = col * rows + r;
                if (list == null || i >= list.Count) { sbN.AppendLine(" "); sbP.AppendLine(" "); sbC.AppendLine(" "); continue; }
                var s = list[i];
                string n = s.name.Length > 18 ? s.name.Substring(0, 17) + "…" : s.name;
                sbN.AppendLine(n);
                sbP.AppendLine(Fmt.N2(s.price));
                sbC.AppendLine($"<color={Fmt.ChangeColor(s.change)}>{Fmt.Pct(s.change)}</color>");
            }
            if (nameCols[col]) nameCols[col].text = sbN.ToString();
            if (priceCols[col]) priceCols[col].text = sbP.ToString();
            if (changeCols[col]) changeCols[col].text = sbC.ToString();
        }

        // Kayan bant
        if (ticker && list != null && list.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var s in list)
                sb.Append($"<color=#FFD54A>{s.name}</color> {Fmt.N2(s.price)} <color={Fmt.ChangeColor(s.change)}>{Fmt.Pct(s.change)}</color>     ");
            ticker.text = sb.ToString();
            tickerWidth = Mathf.Max(20f, ticker.text.Length * 0.09f); // yaklaşık genişlik; tam ölçüm Update'te
        }
        else if (ticker) ticker.text = "TRADE OF MTBS · SEANS SALONU";

        // Endeks: piyasa değeriyle ağırlıklı ortalama değişim
        string idx = "—";
        if (list != null && list.Count > 0)
        {
            double cap = list.Sum(s => Math.Max(1, s.marketCap));
            double ch = list.Sum(s => s.change * Math.Max(1, s.marketCap)) / cap;
            idx = $"<color={Fmt.ChangeColor(ch)}>{Fmt.Pct(ch)}</color>";
        }
        string t = "TMTBS ENDEKS\n" + idx;
        if (indexL) indexL.text = t;
        if (indexR) indexR.text = t;
        if (statusText && m != null) statusText.text = m.status;
    }

    void Update()
    {
        string now = DateTime.Now.ToString("HH:mm:ss");
        if (clockL) clockL.text = "SEANS\n" + now;
        if (clockR) clockR.text = "SEANS\n" + now;

        if (ticker)
        {
            // Gerçek genişlik: yazının sınırlarından
            var r = ticker.GetComponent<Renderer>();
            if (r) tickerWidth = Mathf.Max(5f, r.bounds.size.x);
            tickerX -= Time.deltaTime * 2.2f;
            if (tickerX < tickerLeft - tickerWidth) tickerX = tickerRight;
            var p = ticker.transform.localPosition;
            ticker.transform.localPosition = new Vector3(tickerX, p.y, p.z);
        }
    }
}
