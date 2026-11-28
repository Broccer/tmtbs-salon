using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Borsa salonunun sesleri: gerçek kalabalık kaydı (iki katman, farklı yerden başlar), çalan telefonlar,
/// bankolardan tabela bip'leri; piyasa sert hareket edince uğultu yükselir. Salonun dışında uğultu kısılır.
/// Kayıtlar Resources/Sounds'ta (Sfx). Eskiden kodla üretilen bağırış ve anons sesleri yapay durduğu için kaldırıldı.
/// </summary>
public class Ambience : MonoBehaviour
{
    public static Ambience I { get; private set; }
    public Transform player;

    AudioSource bedA, bedB;
    readonly List<AudioSource> pool = new List<AudioSource>();
    AudioClip phone, beep;
    bool ready;
    float nextEvent, excite, nextPhone = 8f;
    int poolIdx;

    public static void Spatial(AudioSource s, float min, float max)
    {
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = min;
        s.maxDistance = max;
        s.dopplerLevel = 0f;
    }

    void Awake() { I = this; }

    IEnumerator Start()
    {
        yield return null;
        var crowd = Sfx.Get("salon_ugultu");
        phone = Sfx.Get("telefon");
        beep = Sfx.Get("bip");

        // Aynı kaydın iki katmanı: biri yarısından başlar, hafif farklı hızda — tekrar fark edilmesin
        bedA = gameObject.AddComponent<AudioSource>();
        bedA.clip = crowd; bedA.loop = true; bedA.volume = 0f; bedA.spatialBlend = 0f;
        bedB = gameObject.AddComponent<AudioSource>();
        bedB.clip = crowd; bedB.loop = true; bedB.volume = 0f; bedB.spatialBlend = 0f; bedB.pitch = 0.96f;
        if (crowd != null)
        {
            bedA.Play();
            bedB.Play();
            bedB.time = crowd.length * 0.5f;
        }
        for (int i = 0; i < 6; i++)
        {
            var g = new GameObject("Ses_" + i);
            g.transform.SetParent(transform, false);
            var s = g.AddComponent<AudioSource>();
            Spatial(s, 2f, 32f);
            pool.Add(s);
        }
        if (Market.I != null) Market.I.Changed += OnMarket;
        ready = true;
    }

    void OnDestroy() { if (Market.I != null) Market.I.Changed -= OnMarket; }

    bool InSalon()
    {
        if (player == null) return false;
        var p = player.position;
        return p.z > -SalonBuilder.HalfD && p.z < SalonBuilder.HalfD && Mathf.Abs(p.x) < SalonBuilder.HalfW;
    }

    /// <summary>Sert fiyat hareketinde salon coşar: uğultu yükselir, çağrılar sıklaşır.</summary>
    void OnMarket()
    {
        if (Market.I == null) return;
        foreach (var s in Market.I.stocks)
        {
            if (s.prevPrice <= 0 || s.price == s.prevPrice) continue;
            if (System.Math.Abs(s.price / s.prevPrice - 1.0) >= 0.015) { excite = Mathf.Min(1f, excite + 0.5f); break; }
        }
    }

    void Update()
    {
        if (!ready) return;
        bool inside = InSalon();
        float busy = 0.45f + 0.3f * excite;
        float tA = inside ? busy : 0.05f;
        float tB = inside ? busy * 0.45f : 0.02f;
        bedA.volume = Mathf.MoveTowards(bedA.volume, tA, Time.deltaTime * 0.4f);
        bedB.volume = Mathf.MoveTowards(bedB.volume, tB, Time.deltaTime * 0.4f);
        excite = Mathf.MoveTowards(excite, 0f, Time.deltaTime * 0.05f);

        if (!inside) return;
        if (Time.time >= nextEvent)
        {
            // Bankolarda ara ara tabela / kasa bip'i (piyasa hareketliyken sıklaşır)
            nextEvent = Time.time + Mathf.Lerp(9f, 3f, excite) * (0.6f + Random.value * 0.8f);
            var src = Next();
            src.transform.position = RandomVoicePos();
            src.pitch = 0.97f + Random.value * 0.06f;
            if (beep) src.PlayOneShot(beep, 0.25f);
        }
        if (Time.time >= nextPhone)
        {
            nextPhone = Time.time + 12f + Random.value * 22f;
            var src = Next();
            var ks = Kiosk.All;
            src.transform.position = ks.Count > 0 ? ks[Random.Range(0, ks.Count)].transform.position + Vector3.up * 1.1f : player.position;
            src.pitch = 0.95f + Random.value * 0.1f;
            if (phone) src.PlayOneShot(phone, 0.45f);
        }
    }

    AudioSource Next()
    {
        var s = pool[poolIdx];
        poolIdx = (poolIdx + 1) % pool.Count;
        return s;
    }

    Vector3 RandomVoicePos()
    {
        var bots = BotTrader.All;
        if (bots.Count > 0 && Random.value < 0.7f) return bots[Random.Range(0, bots.Count)].transform.position + Vector3.up * 1.6f;
        var ks = Kiosk.All;
        if (ks.Count > 0) return ks[Random.Range(0, ks.Count)].transform.position + Vector3.up * 1.6f;
        return player.position + Random.insideUnitSphere * 8f;
    }

    /// <summary>Bir botun bağırışı (koşarak bankoya giderken).</summary>
    public void ShoutAt(Vector3 p)
    {
        // Yapay bağırış kaldırıldı: koşuşturma anında salon uğultusu kısa süre yükselir.
        if (!ready || !InSalon()) return;
        excite = Mathf.Min(1f, excite + 0.15f);
    }

    /// <summary>Kürsüden anons (hoparlör sesi); uzunluk yazının boyuna göre.</summary>
    public void Announce(AudioSource src, int textLen)
    {
        // Sahte konuşma sesi kaldırıldı; anons yazıyla (şerit + balon) ve kürsünün zil sesiyle verilir.
    }
}
