using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Marka logoları: sunucunun PNG uç noktasından (/tmtbs/logo/{id}) indirilir. 404 yalnız o markanın logosu
/// olmadığı anlamına gelir (bankosu baş harfli renkli rozet gösterir); diğer markalar yüklenmeye devam eder.
/// Geçici ağ hatasında logo iki kez daha denenir.
/// </summary>
public class LogoCache : MonoBehaviour
{
    public static LogoCache I { get; private set; }

    readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();
    readonly Dictionary<long, List<Action<Texture2D>>> waiting = new Dictionary<long, List<Action<Texture2D>>>();
    readonly Dictionary<long, int> tries = new Dictionary<long, int>();
    readonly Queue<long> queue = new Queue<long>();
    int running;
    float nextRetry;
    readonly List<long> retry = new List<long>();

    void Awake() { I = this; }

    public void Get(long id, Action<Texture2D> done)
    {
        if (id <= 0) return;
        if (cache.TryGetValue(id, out var t)) { if (t != null) done(t); return; }
        if (waiting.TryGetValue(id, out var list)) { list.Add(done); return; }
        waiting[id] = new List<Action<Texture2D>> { done };
        queue.Enqueue(id);
    }

    void Update()
    {
        if (retry.Count > 0 && Time.time > nextRetry)
        {
            foreach (var id in retry) queue.Enqueue(id);
            retry.Clear();
        }
        while (running < 4 && queue.Count > 0)
        {
            running++;
            StartCoroutine(Fetch(queue.Dequeue()));
        }
    }

    IEnumerator Fetch(long id)
    {
        using (var req = UnityWebRequest.Get(Api.BaseUrl + "/tmtbs/logo/" + id))
        {
            req.timeout = 20;
            yield return req.SendWebRequest();
            running--;
            Texture2D tex = null;
            if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.data != null && req.downloadHandler.data.Length > 8)
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!tex.LoadImage(req.downloadHandler.data)) { Destroy(tex); tex = null; }
                else { tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 4; }
            }
            else if (req.responseCode != 404)
            {
                // ağ/sunucu hatası: en çok 2 kez daha dene (logosu olan marka rozetle kalmasın)
                tries.TryGetValue(id, out int n);
                if (n < 2)
                {
                    tries[id] = n + 1;
                    retry.Add(id);
                    nextRetry = Time.time + 4f + 4f * n;
                    yield break;
                }
            }
            cache[id] = tex;
            if (waiting.TryGetValue(id, out var list))
            {
                waiting.Remove(id);
                if (tex != null) foreach (var a in list) a(tex);
            }
        }
    }

    /// <summary>Marka adından kararlı bir rozet rengi.</summary>
    public static Color BadgeColor(string name)
    {
        int h = 17;
        foreach (char c in name ?? "") h = h * 31 + c;
        float hue = (Mathf.Abs(h) % 360) / 360f;
        return Color.HSVToRGB(hue, 0.55f, 0.75f);
    }

    public static string Initials(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        var parts = name.Split(new[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return (char.ToUpper(parts[0][0]).ToString() + char.ToUpper(parts[1][0])).Trim();
        return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
    }
}
