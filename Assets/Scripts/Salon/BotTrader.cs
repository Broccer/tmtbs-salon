using UnityEngine;

/// <summary>
/// Salondaki bot borsacılar: kürsüler arasında dolaşır. Bir hisse sert hareket edince
/// o kürsüye koşar ve "AL! AL!" / "SAT!" diye bağırır.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BotTrader : MonoBehaviour
{
    public TextMesh bubble;
    public float walkSpeed = 1.6f;
    public float rushSpeed = 4.5f;

    Rigidbody rb;
    Vector3 target;
    float waitUntil;
    float shoutUntil;
    bool rushing;

    static readonly string[] BuyShouts = { "AL! AL!", "Alıcıyım!", "Yukarı!", "Kaçırma!" };
    static readonly string[] SellShouts = { "SAT!", "Satıcıyım!", "Düşüyor!", "Elden çıkar!" };

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        PickWander();
    }

    void PickWander()
    {
        rushing = false;
        if (Kiosk.All.Count > 0 && Random.value < 0.7f)
        {
            var k = Kiosk.All[Random.Range(0, Kiosk.All.Count)];
            target = k.StandPoint(Random.value < 0.5f ? -1 : 1) + new Vector3(Random.Range(-1.2f, 1.2f), 0, Random.Range(-0.4f, 0.4f));
        }
        else
        {
            target = new Vector3(Random.Range(-14f, 14f), 0, Random.Range(-18f, 18f));
        }
        target.y = 0;
    }

    /// <summary>Sert hareket eden hissenin kürsüsüne koş ve bağır.</summary>
    public void Rush(Kiosk k, bool up)
    {
        rushing = true;
        target = k.StandPoint(-1) + new Vector3(Random.Range(-1.4f, 1.4f), 0, Random.Range(-0.8f, 0.3f));
        target.y = 0;
        var shouts = up ? BuyShouts : SellShouts;
        Shout(shouts[Random.Range(0, shouts.Length)], up ? new Color(0.35f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f), 4f);
    }

    void Shout(string text, Color c, float seconds)
    {
        if (!bubble) return;
        bubble.text = text;
        bubble.color = c;
        bubble.gameObject.SetActive(true);
        shoutUntil = Time.time + seconds;
    }

    void Update()
    {
        if (bubble && bubble.gameObject.activeSelf && Time.time > shoutUntil) bubble.gameObject.SetActive(false);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 to = (target - rb.position).Flat();
        Vector3 want = Vector3.zero;
        if (Time.time >= waitUntil)
        {
            if (to.magnitude < 0.5f)
            {
                waitUntil = Time.time + (rushing ? Random.Range(3f, 5f) : Random.Range(2f, 7f));
                PickWander();
            }
            else want = to.normalized * (rushing ? rushSpeed : walkSpeed);
        }
        Vector3 v = rb.GetVelocity();
        Vector3 h = Vector3.MoveTowards(v.Flat(), want, 10f * dt);
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));
        Vector3 face = h.sqrMagnitude > 0.05f ? h : (Kiosk.All.Count > 0 ? NearestKiosk() - rb.position : Vector3.zero).Flat();
        if (face.sqrMagnitude > 0.01f)
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(face.normalized), 360f * dt));
    }

    Vector3 NearestKiosk()
    {
        Vector3 best = rb.position + transform.forward;
        float bd = float.MaxValue;
        foreach (var k in Kiosk.All)
        {
            float d = (k.transform.position - rb.position).sqrMagnitude;
            if (d < bd) { bd = d; best = k.transform.position; }
        }
        return best;
    }
}
