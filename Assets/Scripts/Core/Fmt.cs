using System.Globalization;

/// <summary>Dile göre sayı biçimleri: tr/es/pt/fr 12.345 MLS · %+2,1 — diğerleri 12,345 MLS · +2.1%</summary>
public static class Fmt
{
    static readonly CultureInfo Dot = Make(".", ",");   // Türkçe, İspanyolca, Portekizce, Fransızca (Fransızca boşluk yerine nokta)
    static readonly CultureInfo Comma = Make(",", "."); // İngilizce, Çince, Hintçe, Arapça, Bengalce

    static CultureInfo Make(string group, string dec)
    {
        // WebGL'de kültür verisi olmayabilir; ayraçları elle veriyoruz.
        var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        c.NumberFormat.NumberGroupSeparator = group;
        c.NumberFormat.NumberDecimalSeparator = dec;
        return c;
    }

    static CultureInfo Tr
    {
        get
        {
            switch (Loc.Lang) { case "tr": case "es": case "pt": case "fr": return Dot; }
            return Comma;
        }
    }

    public static string N(double v) => v.ToString("#,0", Tr);
    public static string N2(double v) => v.ToString("#,0.##", Tr);
    public static string Mls(double v) => N(v) + " MLS";
    public static string Pct(double v) => (v > 0 ? "+" : "") + v.ToString("0.0", Tr) + "%";

    /// <summary>12,3 B / 4,5 Mn kısaltması (dile göre: 12.3K / 4.5M).</summary>
    public static string Short(double v)
    {
        double a = System.Math.Abs(v);
        if (a >= 1e9) return (v / 1e9).ToString("0.#", Tr) + Loc.T(" Mr");
        if (a >= 1e6) return (v / 1e6).ToString("0.#", Tr) + Loc.T(" Mn");
        if (a >= 1e3) return (v / 1e3).ToString("0.#", Tr) + Loc.T(" B");
        return N(v);
    }

    public static string ChangeColor(double v) => v > 0.05 ? "#3BD16F" : v < -0.05 ? "#FF5A5A" : "#C8CCD4";
}
