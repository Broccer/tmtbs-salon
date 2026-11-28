using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerçek ses kayıtları (Resources/Sounds, BigSoundBank — CC0 kamu malı, kaynak gösterme gerekmez).
/// salon_ugultu: kalabalık (Crowd of 50-60 People #1) · gong: Gong, strong #1 · zil: Small Bell #1 ·
/// duyuru: Counter Bell #3 · telefon: Fixed Telephone Ring #4 · para: Coins #1 · bip: Beep of a Cash Register #1 ·
/// tik: Computer mouse. Dosya yoksa null döner; çağıran sessiz kalır.
/// </summary>
public static class Sfx
{
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    public static AudioClip Get(string name)
    {
        if (cache.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("Sounds/" + name);
        cache[name] = c;
        return c;
    }
}
