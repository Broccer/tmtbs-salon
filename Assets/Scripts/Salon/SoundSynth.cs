using UnityEngine;

/// <summary>
/// Ses dosyası olmadan kodla üretilen sesler: gong, duyuru zili, kalabalık uğultusu (konuşma benzeri mırıltı),
/// borsacı çağrıları, anons sesi, telefon zili ve tabela bip'i. Konuşma sesleri basit "formant" sentezidir:
/// ses telleri titreşimi (testere dişi) iki rezonans süzgecinden geçirilir, hece hece sesli harf değişir.
/// </summary>
public static class SoundSynth
{
    public const int Rate = 22050;

    // Sesli harf formantları (F1, F2) — a, e, ı, i, o, ö, u, ü
    static readonly float[,] Vowels =
    {
        { 750, 1250 }, { 520, 1850 }, { 400, 1450 }, { 300, 2250 }, { 480, 900 }, { 420, 1550 }, { 330, 800 }, { 300, 1700 },
    };

    static AudioClip Clip(string name, float[] d)
    {
        float peak = 0.0001f;
        foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        float k = 0.9f / peak;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
        var c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    /// <summary>İki kutuplu rezonans (bant geçiren) süzgeç.</summary>
    struct Reso
    {
        float y1, y2;
        public float Run(float x, float freq, float bw)
        {
            float r = Mathf.Exp(-Mathf.PI * bw / Rate);
            float c = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / Rate);
            float y = (1 - r) * x + c * y1 - r * r * y2;
            y2 = y1; y1 = y;
            return y;
        }
    }

    // ------------------------------------------------------------------ gong ve zil

    public static AudioClip Gong()
    {
        float sec = 7f;
        int n = (int)(sec * Rate);
        var d = new float[n];
        float f0 = 98f;
        float[] ratio = { 1f, 1.49f, 2.03f, 2.53f, 2.98f, 3.65f, 4.41f, 5.12f, 6.3f };
        float[] amp = { 1f, 0.75f, 0.55f, 0.5f, 0.42f, 0.33f, 0.25f, 0.18f, 0.12f };
        float[] dec = { 0.35f, 0.45f, 0.55f, 0.6f, 0.75f, 0.9f, 1.1f, 1.4f, 1.8f };
        var rnd = new System.Random(3);
        float lp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = 0;
            for (int p = 0; p < ratio.Length; p++)
            {
                // yüksek kısmilerde "açılma" (gong'un kabarması) ve hafif vuru
                float bloom = 1f - Mathf.Exp(-t * (3f + p));
                float beat = 1f + 0.25f * Mathf.Sin(2 * Mathf.PI * (0.7f + p * 0.37f) * t);
                v += amp[p] * bloom * beat * Mathf.Exp(-dec[p] * t) * Mathf.Sin(2 * Mathf.PI * f0 * ratio[p] * t * (1f + 0.004f * Mathf.Exp(-t * 2f)));
            }
            // tokmak darbesi: kısa, yumuşatılmış gürültü
            float noise = (float)(rnd.NextDouble() * 2 - 1);
            lp += (noise - lp) * 0.08f;
            v += lp * 2.5f * Mathf.Exp(-t * 40f);
            d[i] = v;
        }
        return Clip("gong", d);
    }

    public static AudioClip Chime()
    {
        float sec = 2.2f;
        int n = (int)(sec * Rate);
        var d = new float[n];
        float[] notes = { 659.25f, 523.25f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = 0;
            for (int k = 0; k < 2; k++)
            {
                float tt = t - k * 0.55f;
                if (tt < 0) continue;
                float f = notes[k];
                v += Mathf.Exp(-tt * 2.2f) * (Mathf.Sin(2 * Mathf.PI * f * tt) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * tt) + 0.15f * Mathf.Sin(2 * Mathf.PI * f * 3f * tt));
            }
            d[i] = v;
        }
        return Clip("zil2", d);
    }

    public static AudioClip Beep()
    {
        int n = (int)(0.09f * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            d[i] = Mathf.Sin(2 * Mathf.PI * 1320f * t) * Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 30f);
        }
        return Clip("bip", d);
    }

    public static AudioClip Phone()
    {
        float sec = 2.4f;
        int n = (int)(sec * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float on = (t % 1.2f) < 0.42f ? 1f : 0f;
            float trem = 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 20f * t));
            d[i] = on * trem * (Mathf.Sin(2 * Mathf.PI * 880f * t) + 0.8f * Mathf.Sin(2 * Mathf.PI * 1100f * t)) * 0.5f;
        }
        return Clip("telefon", d);
    }

    // ------------------------------------------------------------------ insan sesleri

    /// <summary>
    /// Tek konuşmacının mırıltısı out dizisine eklenir: heceler (sesli harf + kısa sessiz), cümle arası duraklar.
    /// </summary>
    static void Voice(float[] outBuf, int start, int len, float pitch, float gain, System.Random r, float rate = 5f, bool loopSafe = false)
    {
        var f1 = new Reso();
        var f2 = new Reso();
        var f3 = new Reso();
        float phase = 0;
        int sylLen = (int)(Rate / rate);
        int pos = 0;
        int vowel = r.Next(8), next = r.Next(8);
        float env = 0, target = 0;
        float pitchNow = pitch;
        int pause = 0;
        int sylCount = 0, sentence = 3 + r.Next(8);
        while (pos < len)
        {
            int sl = (int)(sylLen * (0.6f + (float)r.NextDouble() * 0.8f));
            vowel = next;
            next = r.Next(8);
            bool silent = pause > 0;
            if (pause > 0) pause--;
            if (++sylCount >= sentence) { sylCount = 0; sentence = 3 + r.Next(9); pause = 1 + r.Next(3); }
            float pTarget = pitch * (0.85f + (float)r.NextDouble() * 0.35f) * (sylCount == 0 ? 0.85f : 1f);
            for (int i = 0; i < sl && pos < len; i++, pos++)
            {
                float u = i / (float)sl;
                target = silent ? 0f : (u < 0.12f ? u / 0.12f : u > 0.75f ? (1f - u) / 0.25f : 1f);
                env += (target - env) * 0.004f;
                pitchNow += (pTarget - pitchNow) * 0.0006f;
                phase += pitchNow / Rate;
                if (phase >= 1f) phase -= 1f;
                float src = (phase * 2f - 1f) + ((float)r.NextDouble() * 2 - 1) * 0.15f; // testere dişi + nefes
                // formantlar hecenin içinde bir sonraki sesli harfe kayar
                float k = Mathf.SmoothStep(0, 1, u);
                float F1 = Mathf.Lerp(Vowels[vowel, 0], Vowels[next, 0], k * 0.6f);
                float F2 = Mathf.Lerp(Vowels[vowel, 1], Vowels[next, 1], k * 0.6f);
                float s = f1.Run(src, F1, 90f) * 1.0f + f2.Run(src, F2, 120f) * 0.6f + f3.Run(src, 2600f, 200f) * 0.2f;
                // ünsüz izlenimi: hece başında kısa gürültü
                if (u < 0.06f) s += ((float)r.NextDouble() * 2 - 1) * 0.15f;
                int idx = start + pos;
                if (loopSafe) idx %= outBuf.Length;
                if (idx < outBuf.Length) outBuf[idx] += s * env * gain;
            }
        }
    }

    /// <summary>Kalabalık uğultusu (döngü): çok sayıda uzak konuşmacı + oda yansıması; uçları birbirine geçişli.</summary>
    public static AudioClip Crowd(int seed, float seconds = 10f, int voices = 16)
    {
        int n = (int)(seconds * Rate);
        var d = new float[n];
        var r = new System.Random(seed);
        for (int v = 0; v < voices; v++)
        {
            bool female = r.NextDouble() < 0.4;
            float pitch = female ? 180f + (float)r.NextDouble() * 60f : 100f + (float)r.NextDouble() * 40f;
            float gain = 0.25f + (float)r.NextDouble() * 0.75f;
            int off = r.Next(n);
            Voice(d, off, n, pitch, gain, r, 4.5f + (float)r.NextDouble() * 2f, true);
        }
        // uzak salon yansıması: birkaç gecikmeli kopya + alçak geçiren
        var o = new float[n];
        int[] taps = { 0, 1543, 2711, 4012, 5530 };
        float[] g = { 1f, 0.45f, 0.32f, 0.22f, 0.15f };
        for (int i = 0; i < n; i++)
        {
            float s = 0;
            for (int t = 0; t < taps.Length; t++) s += d[(i - taps[t] + n) % n] * g[t];
            o[i] = s;
        }
        float lp = 0;
        var rn = new System.Random(seed + 1);
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                lp += (o[i] - lp) * 0.35f;
                o[i] = lp + ((float)rn.NextDouble() * 2 - 1) * 0.004f;
            }
        return Clip("kalabalik" + seed, o);
    }

    /// <summary>Kısa borsacı çağrısı ("Alııın!", "Satıyorum!" havasında yükselen/alçalan tek ses).</summary>
    public static AudioClip Shout(int seed)
    {
        var r = new System.Random(seed);
        float sec = 0.55f + (float)r.NextDouble() * 0.4f;
        int n = (int)(sec * Rate);
        var d = new float[n];
        bool female = r.NextDouble() < 0.35;
        float p0 = female ? 260f : 150f;
        float rise = 1.15f + (float)r.NextDouble() * 0.35f;
        int va = r.Next(8), vb = r.Next(8);
        var f1 = new Reso(); var f2 = new Reso(); var f3 = new Reso();
        float phase = 0;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float pitch = p0 * Mathf.Lerp(1f, rise, Mathf.Sin(u * Mathf.PI * 0.8f));
            phase += pitch / Rate;
            if (phase >= 1) phase -= 1;
            float src = (phase * 2 - 1) + ((float)r.NextDouble() * 2 - 1) * 0.25f;
            float F1 = Mathf.Lerp(Vowels[va, 0], Vowels[vb, 0], Mathf.SmoothStep(0.3f, 0.7f, u));
            float F2 = Mathf.Lerp(Vowels[va, 1], Vowels[vb, 1], Mathf.SmoothStep(0.3f, 0.7f, u));
            float s = f1.Run(src, F1 * 1.1f, 110f) + f2.Run(src, F2, 140f) * 0.7f + f3.Run(src, 2800f, 250f) * 0.3f;
            float env = Mathf.Min(1f, u * 18f) * Mathf.Min(1f, (1f - u) * 5f);
            if (u < 0.05f) s += ((float)r.NextDouble() * 2 - 1) * 0.3f;
            d[i] = s * env;
        }
        return Clip("cagri" + seed, d);
    }

    /// <summary>Hoparlörden anons: tek, tok bir ses; dar bant (hoparlör) ve yankı.</summary>
    public static AudioClip Announcer(int seed, float seconds)
    {
        int n = (int)(seconds * Rate);
        var d = new float[n];
        var r = new System.Random(seed);
        Voice(d, 0, n, 118f, 1f, r, 5.2f);
        // hoparlör: bant geçiren + yankı
        var o = new float[n];
        var hp = 0f; var lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float x = d[i] + (i > 3300 ? o[i - 3300] * 0.32f : 0) + (i > 7100 ? o[i - 7100] * 0.18f : 0);
            lp += (x - lp) * 0.45f;
            hp += (lp - hp) * 0.03f;
            o[i] = lp - hp;
        }
        // baş ve son yumuşak
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            o[i] *= Mathf.Min(1f, u * 20f) * Mathf.Min(1f, (1f - u) * 8f);
        }
        return Clip("anons" + seed, o);
    }
}
