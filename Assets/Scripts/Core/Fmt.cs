using System.Globalization;

/// <summary>Türkçe sayı biçimleri: 12.345 MLS, %+2,1</summary>
public static class Fmt
{
    static readonly CultureInfo Tr = MakeTr();

    static CultureInfo MakeTr()
    {
        // WebGL'de kültür verisi olmayabilir; ayraçları elle veriyoruz.
        var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        c.NumberFormat.NumberGroupSeparator = ".";
        c.NumberFormat.NumberDecimalSeparator = ",";
        return c;
    }

    public static string N(double v) => v.ToString("#,0", Tr);
    public static string N2(double v) => v.ToString("#,0.##", Tr);
    public static string Mls(double v) => N(v) + " MLS";
    public static string Pct(double v) => (v > 0 ? "+" : "") + v.ToString("0.0", Tr) + "%";

    /// <summary>12,3 B / 4,5 M kısaltması.</summary>
    public static string Short(double v)
    {
        double a = System.Math.Abs(v);
        if (a >= 1e9) return (v / 1e9).ToString("0.#", Tr) + " Mr";
        if (a >= 1e6) return (v / 1e6).ToString("0.#", Tr) + " Mn";
        if (a >= 1e3) return (v / 1e3).ToString("0.#", Tr) + " B";
        return N(v);
    }

    public static string ChangeColor(double v) => v > 0.05 ? "#3BD16F" : v < -0.05 ? "#FF5A5A" : "#C8CCD4";
}
