using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Borsa salonunun sesleri: sürekli kalabalık uğultusu (iki katman), bankolardan ve botlardan gelen
/// borsacı çağrıları, çalan telefonlar, tabela bip'leri; piyasa sert hareket edince salon coşar.
/// Salonun dışında (koridor, şehir) uğultu kısılır. Sesler kodla üretilir (SoundSynth).
/// </summary>
public class Ambience : MonoBehaviour
{
    public static Ambience I { get; private set; }
    public Transform player;

    AudioSource bedA, bedB;
    readonly List<AudioSource> pool = new List<AudioSource>();
    AudioClip[] shouts, announcers;
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
        // Üretim birkaç kareye yayılır (açılışta takılma olmasın)
        yield return null;
        var a = SoundSynth.Crowd(11, 10f, 16);
        yield return null;
        var b = SoundSynth.Crowd(29, 13f, 10);
        yield return null;
        shouts = new AudioClip[6];
        for (int i = 0; i < shouts.Length; i++) { shouts[i] = SoundSynth.Shout(100 + i); if (i % 2 == 1) yield return null; }
        announcers = new[] { SoundSynth.Announcer(7, 3.2f), SoundSynth.Announcer(8, 4.5f), SoundSynth.Announcer(9, 2.4f) };
        yield return null;
        phone = SoundSynth.Phone();
        beep = SoundSynth.Beep();

        bedA = gameObject.AddComponent<AudioSource>();
        bedA.clip = a; bedA.loop = true; bedA.volume = 0f; bedA.spatialBlend = 0f; bedA.Play();
        bedB = gameObject.AddComponent<AudioSource>();
        bedB.clip = b; bedB.loop = true; bedB.volume = 0f; bedB.spatialBlend = 0f; bedB.pitch = 0.93f; bedB.Play();
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
        float busy = 0.32f + 0.25f * excite;
        float tA = inside ? busy : 0.05f;
        float tB = inside ? busy * 0.7f : 0.03f;
        bedA.volume = Mathf.MoveTowards(bedA.volume, tA, Time.deltaTime * 0.4f);
        bedB.volume = Mathf.MoveTowards(bedB.volume, tB, Time.deltaTime * 0.4f);
        excite = Mathf.MoveTowards(excite, 0f, Time.deltaTime * 0.05f);

        if (!inside) return;
        if (Time.time >= nextEvent)
        {
            nextEvent = Time.time + Mathf.Lerp(2.6f, 0.9f, excite) * (0.6f + Random.value * 0.8f);
            var src = Next();
            src.transform.position = RandomVoicePos();
            src.pitch = 0.9f + Random.value * 0.25f;
            float roll = Random.value;
            if (roll < 0.72f) src.PlayOneShot(shouts[Random.Range(0, shouts.Length)], 0.55f + 0.35f * excite);
            else src.PlayOneShot(beep, 0.35f);
        }
        if (Time.time >= nextPhone)
        {
            nextPhone = Time.time + 12f + Random.value * 22f;
            var src = Next();
            var ks = Kiosk.All;
            src.transform.position = ks.Count > 0 ? ks[Random.Range(0, ks.Count)].transform.position + Vector3.up * 1.1f : player.position;
            src.pitch = 0.95f + Random.value * 0.1f;
            src.PlayOneShot(phone, 0.5f);
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
        if (!ready || !InSalon()) return;
        var src = Next();
        src.transform.position = p;
        src.pitch = 0.95f + Random.value * 0.2f;
        src.PlayOneShot(shouts[Random.Range(0, shouts.Length)], 0.9f);
    }

    /// <summary>Kürsüden anons (hoparlör sesi); uzunluk yazının boyuna göre.</summary>
    public void Announce(AudioSource src, int textLen)
    {
        if (!ready || announcers == null) return;
        int i = textLen > 70 ? 1 : textLen > 40 ? 0 : 2;
        src.spatialBlend = 0.55f; // hoparlör: salonun her yerinde duyulur, kürsüye yakınken daha net
        src.PlayOneShot(announcers[i], 0.9f);
    }
}
