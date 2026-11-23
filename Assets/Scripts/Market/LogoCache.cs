using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Marka logoları: sunucunun PNG uç noktasından (/tmtbs/logo/{id}) indirilir. Uç nokta yoksa
/// (404) bir daha denenmez; bankolar baş harfli renkli rozet gösterir.
/// </summary>
public class LogoCache : MonoBehaviour
{
    public static LogoCache I { get; private set; }

    readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();
    readonly Dictionary<long, List<Action<Texture2D>>> waiting = new Dictionary<long, List<Action<Texture2D>>>();
    readonly Queue<long> queue = new Queue<long>();
    bool endpointMissing;
    int running;

    void Awake() { I = this; }

    public void Get(long id, Action<Texture2D> done)
    {
        if (id <= 0 || endpointMissing) return;
        if (cache.TryGetValue(id, out var t)) { if (t != null) done(t); return; }
        if (waiting.TryGetValue(id, out var list)) { list.Add(done); return; }
        waiting[id] = new List<Action<Texture2D>> { done };
        queue.Enqueue(id);
    }

    void Update()
    {
        while (running < 3 && queue.Count > 0 && !endpointMissing)
        {
            running++;
            StartCoroutine(Fetch(queue.Dequeue()));
        }
    }

    IEnumerator Fetch(long id)
    {
        using (var req = UnityWebRequest.Get(Api.BaseUrl + "/tmtbs/logo/" + id))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            running--;
            Texture2D tex = null;
            if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.data != null && req.downloadHandler.data.Length > 8)
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(req.downloadHandler.data)) { Destroy(tex); tex = null; }
                else { tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear; }
            }
            else if (req.responseCode == 404 || req.responseCode == 0)
            {
                endpointMissing = true; // sunucuda henüz yok: rozetle devam
                queue.Clear();
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
