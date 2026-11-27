using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dil desteği (Trade of MTBS uygulamasıyla aynı 9 dil). Kaynak dil Türkçe: metinler kodda Türkçe yazılır,
/// <see cref="T(string, object[])"/> seçili dile çevirir; çeviri yoksa Türkçe kalır.
/// Çeviriler Resources/Lang/&lt;kod&gt;.json (düz {"Türkçe": "çeviri"} nesnesi). {0}, {1}… yer tutucuları.
/// </summary>
public static class Loc
{
    public static readonly (string code, string name)[] Langs =
    {
        ("tr", "Türkçe"), ("en", "English"), ("zh", "中文"), ("hi", "हिन्दी"), ("es", "Español"),
        ("fr", "Français"), ("ar", "العربية"), ("bn", "বাংলা"), ("pt", "Português"),
    };

    const string Key = "tmtbs3d_dil";
    static Dictionary<string, string> map;
    static readonly Dictionary<string, string> shaped = new Dictionary<string, string>();
    static bool ready;
    static string lang = "tr";

    public static event Action Changed;

    public static string Lang { get { Init(); return lang; } }
    public static bool Rtl => Lang == "ar";

    static void Init()
    {
        if (ready) return;
        ready = true;
        string saved = PlayerPrefs.GetString(Key, "");
        lang = IsKnown(saved) ? saved : Detect();
        Load();
    }

    static bool IsKnown(string c) { foreach (var l in Langs) if (l.code == c) return true; return false; }

    static string Detect()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: case SystemLanguage.ChineseTraditional: return "zh";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.Arabic: return "ar";
            case SystemLanguage.Portuguese: return "pt";
            case SystemLanguage.English: return "en";
        }
        // Unity'nin SystemLanguage'ında Hintçe/Bengalce yok: tarayıcı/cihaz dili Unknown gelir → İngilizce
        return "en";
    }

    public static void Set(string code)
    {
        Init();
        if (!IsKnown(code) || code == lang) return;
        lang = code;
        PlayerPrefs.SetString(Key, code);
        PlayerPrefs.Save();
        Load();
        Changed?.Invoke();
    }

    static void Load()
    {
        map = null;
        shaped.Clear();
        if (lang == "tr") return;
        var ta = Resources.Load<TextAsset>("Lang/" + lang);
        if (ta == null) return;
        var o = Json.Parse(ta.text) as Dictionary<string, object>;
        if (o == null) return;
        map = new Dictionary<string, string>(o.Count);
        foreach (var kv in o) if (kv.Value is string s && s.Length > 0) map[kv.Key] = s;
    }

    /// <summary>Türkçe kaynak metni seçili dile çevirir; {0}, {1}… değerlerle doldurulur.</summary>
    public static string T(string s, params object[] a)
    {
        Init();
        if (string.IsNullOrEmpty(s)) return s;
        string t = s;
        if (map != null && map.TryGetValue(s, out var tr)) t = tr;
        bool args = a != null && a.Length > 0;
        if (lang == "ar" && !ReferenceEquals(t, s))
        {
            // Arapça: sunum biçimleri + sağdan sola. Değerler (sayı, renk etiketi, ad) bozulmasın diye
            // yalnız sabit parçalar şekillendirilir, parçaların sırası ters çevrilir.
            if (args) return ArFormat(t, a);
            if (!shaped.TryGetValue(t, out var sh)) { sh = TextFix.Display(t); shaped[t] = sh; }
            return sh;
        }
        if (args)
            for (int i = 0; i < a.Length; i++) t = t.Replace("{" + i + "}", a[i] == null ? "" : a[i].ToString());
        return t;
    }

    static string ArFormat(string t, object[] a)
    {
        var parts = new List<string>();
        int i = 0, lit = 0;
        while (i < t.Length)
        {
            if (t[i] == '{')
            {
                int j = t.IndexOf('}', i);
                if (j > i + 1 && int.TryParse(t.Substring(i + 1, j - i - 1), out int n) && n >= 0 && n < a.Length)
                {
                    if (i > lit) parts.Add(TextFix.Display(t.Substring(lit, i - lit)));
                    parts.Add(a[n] == null ? "" : a[n].ToString());
                    i = j + 1;
                    lit = i;
                    continue;
                }
            }
            i++;
        }
        if (lit < t.Length) parts.Add(TextFix.Display(t.Substring(lit)));
        parts.Reverse();
        return string.Concat(parts);
    }
}
