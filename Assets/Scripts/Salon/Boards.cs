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
    // Dış cephe: endeks ve en çok yükselen/düşen 5 marka (0 = yükselen, 1 = düşen)
    public TextMesh facadeIndex;
    public TextMesh[] fNames = new TextMesh[2];
    public TextMesh[] fPrices = new TextMesh[2];
    public TextMesh[] fChanges = new TextMesh[2];

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
        else if (ticker) ticker.text = Loc.T("TRADE OF MTBS · SEANS SALONU");

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
        RefreshFacade(list);
    }

    void RefreshFacade(System.Collections.Generic.List<Stock> list)
    {
        if (facadeIndex)
        {
            if (list != null && list.Count > 0)
            {
                double cap = list.Sum(s => Math.Max(1, s.marketCap));
                double ch = list.Sum(s => s.change * Math.Max(1, s.marketCap)) / cap;
                facadeIndex.text = Loc.T("TMTBS ENDEKS  <color={0}>{1} {2}</color>", Fmt.ChangeColor(ch), (ch > 0.05 ? "▲" : ch < -0.05 ? "▼" : "■"), Fmt.Pct(ch));
            }
            else facadeIndex.text = Loc.T("TMTBS ENDEKS  —");
        }
        var up = list == null ? new System.Collections.Generic.List<Stock>() : list.OrderByDescending(s => s.change).Take(5).ToList();
        var down = list == null ? new System.Collections.Generic.List<Stock>() : list.Where(s => !up.Contains(s)).OrderBy(s => s.change).Take(5).ToList();
        FillFacade(0, up);
        FillFacade(1, down);
    }

    void FillFacade(int p, System.Collections.Generic.List<Stock> rows)
    {
        var sbN = new StringBuilder();
        var sbP = new StringBuilder();
        var sbC = new StringBuilder();
        for (int r = 0; r < 5; r++)
        {
            if (r >= rows.Count) { sbN.AppendLine(" "); sbP.AppendLine(" "); sbC.AppendLine(" "); continue; }
            var s = rows[r];
            sbN.AppendLine(s.name.Length > 10 ? s.name.Substring(0, 9) + "…" : s.name);
            sbP.AppendLine(Fmt.N2(s.price));
            sbC.AppendLine($"<color={Fmt.ChangeColor(s.change)}>{Fmt.Pct(s.change)}</color>");
        }
        if (fNames[p]) fNames[p].text = sbN.ToString();
        if (fPrices[p]) fPrices[p].text = sbP.ToString();
        if (fChanges[p]) fChanges[p].text = sbC.ToString();
    }

    void Update()
    {
        string now = DateTime.Now.ToString("HH:mm:ss");
        if (clockL) clockL.text = Loc.T("SEANS\n") + now;
        if (clockR) clockR.text = Loc.T("SEANS\n") + now;

        if (ticker)
        {
            // Gerçek genişlik: yazının sınırlarından
            // Kayan bant yalnız salonun içindeyken görünür (dışarıdan duvarların yanından taşardı)
            var cam = Camera.main;
            bool inside = cam == null || (cam.transform.position.z > -SalonBuilder.HalfD && Mathf.Abs(cam.transform.position.x) < SalonBuilder.HalfW);
            var tr = ticker.GetComponent<Renderer>();
            if (tr && tr.enabled != inside) tr.enabled = inside;
            var r = ticker.GetComponent<Renderer>();
            if (r) tickerWidth = Mathf.Max(5f, r.bounds.size.x);
            tickerX -= Time.deltaTime * 2.2f;
            if (tickerX < tickerLeft - tickerWidth) tickerX = tickerRight;
            var p = ticker.transform.localPosition;
            ticker.transform.localPosition = new Vector3(tickerX, p.y, p.z);
        }
    }
}
