using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canlı çok oyunculu görünüm: oyuncunun konumunu ~1 sn'de bir sunucuya yollar (/tmtbs/salon/konum),
/// aynı alandaki (TMTBS salonu ya da Müteşebbis şehri) diğer gerçek oyuncuları getirir ve onları
/// yumuşak geçişle yürütür. Gerçek oyuncular mavi halka ve mavi isimle botlardan ayrılır.
/// Sunucu adı jetondan çözer (gizlilik: "Ahmet Y."); girişsiz gezenler "Misafir".
/// </summary>
public class LivePlayers : MonoBehaviour
{
    public static LivePlayers I { get; private set; }
    public static bool Active;           // yönetici oyuna girince açar
    public static int OnlineCount => I != null ? I.remotes.Count : 0;

    public Walker player;

    class Remote
    {
        public GameObject go;
        public TextMesh tag;
        public Vector3 from, to;
        public float fromRy, toRy;
        public float t0, dur = 1f;
        public float lastSeen;
        public int poz;
        public CharAnim ca;
        public string name = "";
        public long uid;
    }

    public struct Info { public string name; public long uid; public Vector3 pos; }

    /// <summary>Salondaki diğer oyuncular (yakından uzağa).</summary>
    public static List<Info> Others(Vector3 from)
    {
        var l = new List<Info>();
        if (I == null) return l;
        foreach (var r in I.remotes.Values) l.Add(new Info { name = r.name, uid = r.uid, pos = r.go.transform.position });
        l.Sort((a, b) => (a.pos - from).sqrMagnitude.CompareTo((b.pos - from).sqrMagnitude));
        return l;
    }

    readonly Dictionary<string, Remote> remotes = new Dictionary<string, Remote>();
    readonly string sid = MakeSid();
    float nextSend;
    bool sending;
    int fails;
    static readonly Color Blue = new Color(0.45f, 0.78f, 1f);

    static string MakeSid()
    {
        const string a = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var c = new char[20];
        var r = new System.Random(System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode());
        for (int i = 0; i < c.Length; i++) c[i] = a[r.Next(a.Length)];
        return new string(c);
    }

    void Awake() { I = this; }

    void Update()
    {
        if (!Active || player == null)
        {
            if (remotes.Count > 0 && !Active) ClearAll();
            return;
        }
        if (!sending && Time.time >= nextSend) Send();
        float now = Time.time;
        foreach (var r in remotes.Values)
        {
            float k = Mathf.Clamp01((now - r.t0) / r.dur);
            r.go.transform.position = Vector3.Lerp(r.from, r.to, k);
            r.go.transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(r.fromRy, r.toRy, Mathf.Clamp01(k * 2f)), 0);
            if (r.ca == null) r.ca = r.go.GetComponent<CharAnim>();
            if (r.ca != null) r.ca.sitting = r.poz == 2;
        }
    }

    void Send()
    {
        sending = true;
        var p = player.transform;
        var rb = player.GetComponent<Rigidbody>();
        float speed = rb != null ? rb.GetVelocity().Flat().magnitude : 0f;
        bool mt = Market.I != null && Market.I.Live && !Market.I.Tm;
        var body = new Dictionary<string, object>
        {
            { "sid", sid }, { "alan", mt ? "mt" : "tm" },
            { "x", (double)p.position.x }, { "z", (double)p.position.z }, { "ry", (double)p.eulerAngles.y },
            { "hiz", (double)speed }, { "poz", player.SittingOn != null ? 2 : speed > 0.3f ? 1 : 0 },
            { "kiyafet", Characters.SavedModel },
        };
        Api.I.Post("/tmtbs/salon/konum", body, r =>
        {
            sending = false;
            fails = 0;
            nextSend = Time.time + (remotes.Count > 0 ? 1f : 2f);
            Apply(J.A(r, "oyuncular"));
        }, (m, c) =>
        {
            sending = false;
            fails++;
            // uç nokta yoksa (404) ya da ağ yoksa seyrek dene
            nextSend = Time.time + (c == 404 ? 60f : Mathf.Min(30f, 2f * fails));
        });
    }

    void Apply(List<object> list)
    {
        float now = Time.time;
        var seen = new HashSet<string>();
        foreach (var o in list)
        {
            string id = J.S(o, "sid");
            if (string.IsNullOrEmpty(id)) continue;
            seen.Add(id);
            var pos = new Vector3((float)J.D(o, "x"), 0f, (float)J.D(o, "z"));
            float ry = (float)J.D(o, "ry");
            if (!remotes.TryGetValue(id, out var r))
            {
                r = Create(id, J.S(o, "ad"), (int)J.D(o, "kiyafet"), pos, ry);
                remotes[id] = r;
            }
            else
            {
                r.from = r.go.transform.position;
                r.fromRy = r.go.transform.eulerAngles.y;
                // çok uzağa sıçradıysa (ışınlandı) doğrudan oraya koy
                if ((pos - r.from).sqrMagnitude > 15f * 15f) r.from = pos;
                r.to = pos;
                r.toRy = ry;
                r.t0 = now;
                r.dur = remotes.Count > 0 ? 1.05f : 2f;
                string ad = J.S(o, "ad");
                if (r.tag != null && r.tag.text != ad) r.tag.text = ad;
            }
            r.poz = (int)J.D(o, "poz");
            r.name = J.S(o, "ad");
            r.uid = J.L(o, "uid");
            r.lastSeen = now;
        }
        var gone = new List<string>();
        foreach (var kv in remotes) if (!seen.Contains(kv.Key) && now - kv.Value.lastSeen > 6f) gone.Add(kv.Key);
        foreach (var id in gone) { Destroy(remotes[id].go); remotes.Remove(id); }
    }

    Remote Create(string id, string name, int model, Vector3 pos, float ry)
    {
        var go = new GameObject("Oyuncu_" + id);
        go.layer = 2;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, ry, 0));
        var lc = go.AddComponent<LazyChar>();
        lc.model = model;
        lc.look = Look.Random(new System.Random(id.GetHashCode()));
        lc.near = 30f; lc.far = 40f;
        var tag = Gfx.Text("Isim", go.transform, new Vector3(0, 2.15f, 0), Quaternion.identity, name, 0.22f, Blue);
        tag.gameObject.AddComponent<Billboard>();
        Gfx.Prim(PrimitiveType.Cylinder, "Isaret", go.transform, new Vector3(0, 0.012f, 0), new Vector3(0.8f, 0.004f, 0.8f), Gfx.Glow(Blue), false);
        return new Remote { go = go, tag = tag, from = pos, to = pos, fromRy = ry, toRy = ry, t0 = Time.time, lastSeen = Time.time };
    }

    void ClearAll()
    {
        foreach (var r in remotes.Values) if (r.go) Destroy(r.go);
        remotes.Clear();
    }
}
