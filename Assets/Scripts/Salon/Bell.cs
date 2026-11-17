using UnityEngine;

/// <summary>Seans zili ve küçük efektler; ses dosyası gerekmez, kodla üretilir.</summary>
public class Bell : MonoBehaviour
{
    public static Bell I { get; private set; }
    AudioSource src;
    AudioClip bell, tick;

    void Awake()
    {
        I = this;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        bell = Make("zil", 2.2f, new[] { 523.25f, 1046.5f, 1567.98f, 2093f }, new[] { 1f, 0.6f, 0.35f, 0.2f }, 1.6f);
        tick = Make("tik", 0.12f, new[] { 1760f }, new[] { 1f }, 30f);
    }

    static AudioClip Make(string name, float seconds, float[] freqs, float[] amps, float decay)
    {
        const int rate = 22050;
        int n = (int)(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float v = 0f;
            for (int f = 0; f < freqs.Length; f++) v += amps[f] * Mathf.Sin(2f * Mathf.PI * freqs[f] * t);
            data[i] = v * Mathf.Exp(-decay * t) * 0.25f;
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public void Ring() { if (src) src.PlayOneShot(bell, 0.9f); }
    public void Tick() { if (src) src.PlayOneShot(tick, 0.4f); }
}
