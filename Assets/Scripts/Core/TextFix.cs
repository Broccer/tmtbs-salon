using System.Collections.Generic;
using System.Text;

/// <summary>
/// Unity'nin IMGUI/TextMesh yazısı harf birleştirme (shaping) ve sağdan sola dizme yapmaz.
/// Arapça/Farsça harfleri Unicode "sunum biçimlerine" (başta/ortada/sonda/tek) çevirir, lam-elif bağını kurar
/// ve sağdan sola yazıyı görsel sıraya dizer. Yazı tipi (TmtbsSans) bu kod noktalarını içerir.
/// Arapça içermeyen yazı olduğu gibi döner (hızlı yol).
/// </summary>
public static class TextFix
{
    // harf → (tek, son, baş, orta); baş/orta 0 ise yalnız sağdan bağlanır (elif, dal, ra, vav…)
    static readonly Dictionary<char, int[]> Forms = new Dictionary<char, int[]>
    {
        { 'ء', new[] { 0xFE80, 0, 0, 0 } },
        { 'آ', new[] { 0xFE81, 0xFE82, 0, 0 } }, { 'أ', new[] { 0xFE83, 0xFE84, 0, 0 } },
        { 'ؤ', new[] { 0xFE85, 0xFE86, 0, 0 } }, { 'إ', new[] { 0xFE87, 0xFE88, 0, 0 } },
        { 'ئ', new[] { 0xFE89, 0xFE8A, 0xFE8B, 0xFE8C } }, { 'ا', new[] { 0xFE8D, 0xFE8E, 0, 0 } },
        { 'ب', new[] { 0xFE8F, 0xFE90, 0xFE91, 0xFE92 } }, { 'ة', new[] { 0xFE93, 0xFE94, 0, 0 } },
        { 'ت', new[] { 0xFE95, 0xFE96, 0xFE97, 0xFE98 } }, { 'ث', new[] { 0xFE99, 0xFE9A, 0xFE9B, 0xFE9C } },
        { 'ج', new[] { 0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0 } }, { 'ح', new[] { 0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4 } },
        { 'خ', new[] { 0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8 } }, { 'د', new[] { 0xFEA9, 0xFEAA, 0, 0 } },
        { 'ذ', new[] { 0xFEAB, 0xFEAC, 0, 0 } }, { 'ر', new[] { 0xFEAD, 0xFEAE, 0, 0 } },
        { 'ز', new[] { 0xFEAF, 0xFEB0, 0, 0 } }, { 'س', new[] { 0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4 } },
        { 'ش', new[] { 0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8 } }, { 'ص', new[] { 0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC } },
        { 'ض', new[] { 0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0 } }, { 'ط', new[] { 0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4 } },
        { 'ظ', new[] { 0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8 } }, { 'ع', new[] { 0xFEC9, 0xFECA, 0xFECB, 0xFECC } },
        { 'غ', new[] { 0xFECD, 0xFECE, 0xFECF, 0xFED0 } }, { 'ف', new[] { 0xFED1, 0xFED2, 0xFED3, 0xFED4 } },
        { 'ق', new[] { 0xFED5, 0xFED6, 0xFED7, 0xFED8 } }, { 'ك', new[] { 0xFED9, 0xFEDA, 0xFEDB, 0xFEDC } },
        { 'ل', new[] { 0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0 } }, { 'م', new[] { 0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4 } },
        { 'ن', new[] { 0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8 } }, { 'ه', new[] { 0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC } },
        { 'و', new[] { 0xFEED, 0xFEEE, 0, 0 } }, { 'ى', new[] { 0xFEEF, 0xFEF0, 0, 0 } },
        { 'ي', new[] { 0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4 } },
        { 'پ', new[] { 0xFB56, 0xFB57, 0xFB58, 0xFB59 } }, { 'چ', new[] { 0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D } },
        { 'ژ', new[] { 0xFB8A, 0xFB8B, 0, 0 } }, { 'ک', new[] { 0xFB8E, 0xFB8F, 0xFB90, 0xFB91 } },
        { 'گ', new[] { 0xFB92, 0xFB93, 0xFB94, 0xFB95 } }, { 'ی', new[] { 0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF } },
    };

    // lam + elif türü → (tek, son)
    static readonly Dictionary<char, int[]> LamAlef = new Dictionary<char, int[]>
    {
        { 'آ', new[] { 0xFEF5, 0xFEF6 } }, { 'أ', new[] { 0xFEF7, 0xFEF8 } },
        { 'إ', new[] { 0xFEF9, 0xFEFA } }, { 'ا', new[] { 0xFEFB, 0xFEFC } },
    };

    static bool IsArabic(char c) => (c >= '؀' && c <= 'ۿ') || (c >= 'ݐ' && c <= 'ݿ') || (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿');
    static bool IsRtl(char c) => IsArabic(c) || (c >= '֐' && c <= '׿');
    static bool IsMark(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';
    static bool JoinsLeft(char c) => c == 'ـ' || (Forms.TryGetValue(c, out var f) && f[2] != 0); // sonraki harfe bağlanır
    static bool JoinsRight(char c) => c == 'ـ' || (Forms.TryGetValue(c, out var f) && f[1] != 0); // önceki harfe bağlanır

    public static string Display(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        bool any = false;
        foreach (char c in s) if (IsRtl(c)) { any = true; break; }
        if (!any) return s;
        return Reorder(Shape(s));
    }

    static string Shape(string s)
    {
        var sb = new StringBuilder(s.Length);
        int n = s.Length;
        for (int i = 0; i < n; i++)
        {
            char c = s[i];
            if (!Forms.ContainsKey(c)) { sb.Append(c); continue; }
            int p = i - 1;
            while (p >= 0 && IsMark(s[p])) p--;
            int q = i + 1;
            while (q < n && IsMark(s[q])) q++;
            bool prevJoins = p >= 0 && JoinsLeft(s[p]) && JoinsRight(c);
            // lam + elif bağı
            if (c == 'ل' && q < n && LamAlef.TryGetValue(s[q], out var la))
            {
                sb.Append((char)(prevJoins ? la[1] : la[0]));
                for (int k = i + 1; k < q; k++) sb.Append(s[k]); // aradaki harekeler
                i = q;
                continue;
            }
            bool nextJoins = q < n && JoinsLeft(c) && JoinsRight(s[q]);
            var f = Forms[c];
            int form = prevJoins && nextJoins ? f[3] : prevJoins ? f[1] : nextJoins ? f[2] : f[0];
            sb.Append((char)(form != 0 ? form : f[0]));
        }
        return sb.ToString();
    }

    static bool IsLtrStrong(char c) => char.IsLetterOrDigit(c) && !IsRtl(c);

    /// <summary>Basit çift yönlü dizme: paragraf yönü ilk güçlü harfe göre; sağdan sola parçalar ters çevrilir.</summary>
    static string Reorder(string s)
    {
        bool rtlPara = false;
        foreach (char c in s)
        {
            if (IsRtl(c)) { rtlPara = true; break; }
            if (IsLtrStrong(c)) break;
        }
        var chars = s.ToCharArray();
        if (rtlPara)
        {
            System.Array.Reverse(chars);
            // soldan sağa parçaları (Latin kelimeler, sayılar) tekrar düzelt
            int i = 0;
            while (i < chars.Length)
            {
                if (!IsLtrStrong(chars[i])) { i++; continue; }
                int j = i;
                while (j + 1 < chars.Length && (IsLtrStrong(chars[j + 1]) ||
                       ((chars[j + 1] == ' ' || chars[j + 1] == '.' || chars[j + 1] == ',' || chars[j + 1] == ':' || chars[j + 1] == '-') && j + 2 < chars.Length && IsLtrStrong(chars[j + 2]))))
                    j++;
                System.Array.Reverse(chars, i, j - i + 1);
                i = j + 1;
            }
            return new string(chars);
        }
        // soldan sağa paragraf: yalnız sağdan sola parçaları ters çevir
        int a = 0;
        while (a < chars.Length)
        {
            if (!IsRtl(chars[a])) { a++; continue; }
            int b = a;
            while (b + 1 < chars.Length && (IsRtl(chars[b + 1]) || IsMark(chars[b + 1]) ||
                   (chars[b + 1] == ' ' && b + 2 < chars.Length && IsRtl(chars[b + 2]))))
                b++;
            System.Array.Reverse(chars, a, b - a + 1);
            a = b + 1;
        }
        return new string(chars);
    }
}
