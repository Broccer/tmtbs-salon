using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Küçük JSON okuyucu/yazıcı. Sunucunun dinamik yanıtları (sayılar bazen metin) için
/// JsonUtility yetmiyor. Yazarken ASCII dışı her karakter \uXXXX olur: mutesebbis.tr
/// barındırması POST gövdesindeki çok baytlı UTF-8'i bozuyor.
/// </summary>
public static class Json
{
    public static object Parse(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        int i = 0;
        try { return Value(s, ref i); }
        catch { return null; }
    }

    static void Ws(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{') return Obj(s, ref i);
        if (c == '[') return Arr(s, ref i);
        if (c == '"') return Str(s, ref i);
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    static Dictionary<string, object> Obj(string s, ref int i)
    {
        var d = new Dictionary<string, object>();
        i++;
        Ws(s, ref i);
        if (s[i] == '}') { i++; return d; }
        while (true)
        {
            Ws(s, ref i);
            string k = Str(s, ref i);
            Ws(s, ref i);
            i++; // :
            d[k] = Value(s, ref i);
            Ws(s, ref i);
            if (s[i] == ',') { i++; continue; }
            i++; // }
            return d;
        }
    }

    static List<object> Arr(string s, ref int i)
    {
        var l = new List<object>();
        i++;
        Ws(s, ref i);
        if (s[i] == ']') { i++; return l; }
        while (true)
        {
            l.Add(Value(s, ref i));
            Ws(s, ref i);
            if (s[i] == ',') { i++; continue; }
            i++; // ]
            return l;
        }
    }

    static string Str(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++; // "
        while (s[i] != '"')
        {
            char c = s[i++];
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                    i += 4;
                    break;
                default: sb.Append(e); break;
            }
        }
        i++;
        return sb.ToString();
    }

    public static string Write(object o)
    {
        var sb = new StringBuilder();
        WriteValue(sb, o);
        return sb.ToString();
    }

    static void WriteValue(StringBuilder sb, object o)
    {
        switch (o)
        {
            case null: sb.Append("null"); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case string str: WriteString(sb, str); break;
            case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case double n: sb.Append(n.ToString("R", CultureInfo.InvariantCulture)); break;
            case float n: sb.Append(n.ToString("R", CultureInfo.InvariantCulture)); break;
            case IDictionary<string, object> d:
                sb.Append('{');
                bool first = true;
                foreach (var kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    WriteValue(sb, kv.Value);
                }
                sb.Append('}');
                break;
            case System.Collections.IEnumerable list:
                sb.Append('[');
                bool f = true;
                foreach (var x in list)
                {
                    if (!f) sb.Append(',');
                    f = false;
                    WriteValue(sb, x);
                }
                sb.Append(']');
                break;
            default: WriteString(sb, o.ToString()); break;
        }
    }

    static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c < 0x20 || c > 0x7E) sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        sb.Append('"');
    }
}

/// <summary>Gevşek erişim: sayı metin gelse de, alan eksik olsa da çalışır.</summary>
public static class J
{
    public static Dictionary<string, object> O(object o, string key = null)
    {
        if (key != null) o = Get(o, key);
        return o as Dictionary<string, object> ?? new Dictionary<string, object>();
    }

    public static List<object> A(object o, string key = null)
    {
        if (key != null) o = Get(o, key);
        return o as List<object> ?? new List<object>();
    }

    public static object Get(object o, string key)
    {
        return o is Dictionary<string, object> d && d.TryGetValue(key, out var v) ? v : null;
    }

    public static double D(object o, string key = null)
    {
        if (key != null) o = Get(o, key);
        switch (o)
        {
            case double d: return d;
            case bool b: return b ? 1 : 0;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r): return r;
            default: return 0;
        }
    }

    public static long L(object o, string key = null) => (long)System.Math.Round(D(o, key));

    public static bool B(object o, string key = null)
    {
        if (key != null) o = Get(o, key);
        switch (o)
        {
            case bool b: return b;
            case double d: return d != 0;
            case string s: return s == "1" || s.ToLowerInvariant() == "true";
            default: return false;
        }
    }

    public static string S(object o, string key = null)
    {
        if (key != null) o = Get(o, key);
        // Sunucudan gelen yazılar (marka/oyuncu adı, haber): Arapça varsa harfleri birleşik biçime çevir, sağdan sola diz.
        return o == null ? "" : o is double d ? d.ToString(CultureInfo.InvariantCulture) : TextFix.Display(o.ToString());
    }
}
