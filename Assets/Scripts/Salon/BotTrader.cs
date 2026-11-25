using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Salondaki bot borsacılar: peronlar ve duvar bankoları arasındaki koridorlardan dolaşır.
/// Bir hisse sert hareket edince o bankoya koşar ve "AL! AL!" / "SAT!" diye bağırır.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BotTrader : MonoBehaviour
{
    public TextMesh bubble;
    public float walkSpeed = 1.5f;
    public float rushSpeed = 4.2f;

    Rigidbody rb;
    readonly Queue<Vector3> path = new Queue<Vector3>();
    Kiosk facing;
    float waitUntil, shoutUntil, stuckSince;
    bool rushing;
    Vector3 lastPos;

    static readonly string[] BuyShouts = { "AL! AL!", "Alıcıyım!", "Yukarı!", "Kaçırma!" };
    static readonly string[] SellShouts = { "SAT!", "Satıcıyım!", "Düşüyor!", "Elden çıkar!" };

    public const float Lane = 14.5f;   // peron ile yan duvar bankoları arasındaki koridor (|x|)
    const float CrossZ = 16f;          // peronların önünde / arkasında açık geçiş (±)

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void Start() { PickWander(); }

    static int SideOf(Vector3 p) => Mathf.Abs(p.x) < SalonBuilder.PeronX - 1.8f ? 0 : (int)Mathf.Sign(p.x);
    static float LaneX(int side) => side == 0 ? 0f : side * Lane;

    void Route(Vector3 end)
    {
        path.Clear();
        Vector3 start = rb != null ? rb.position : transform.position;
        int a = SideOf(start), b = SideOf(end);
        float band = SalonBuilder.PeronZ + 2f;   // peron adalarının kapladığı z bandı
        bool blocked = a != b && Mathf.Abs(start.z) < band + 1f || a != b && Mathf.Abs(end.z) < band + 1f;
        if (Mathf.Abs(start.x - LaneX(a)) > 0.5f) path.Enqueue(new Vector3(LaneX(a), 0, start.z));
        if (blocked)
        {
            float cz = (start.z + end.z) >= 0 ? CrossZ : -CrossZ;
            path.Enqueue(new Vector3(LaneX(a), 0, cz));
            path.Enqueue(new Vector3(LaneX(b), 0, cz));
        }
        path.Enqueue(new Vector3(LaneX(b), 0, end.z));
        path.Enqueue(end);
    }

    void PickWander()
    {
        rushing = false;
        facing = null;
        if (Kiosk.All.Count > 0 && Random.value < 0.75f)
        {
            var k = Kiosk.All[Random.Range(0, Kiosk.All.Count)];
            facing = k;
            Route(k.StandPoint(Random.Range(-1, 2)));
        }
        else Route(new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-20f, 20f)));
    }

    /// <summary>Sert hareket eden hissenin bankosuna koş ve bağır.</summary>
    public void Rush(Kiosk k, bool up)
    {
        rushing = true;
        facing = k;
        waitUntil = 0;
        Route(k.StandPoint(Random.Range(-1, 2)) + k.transform.forward * Random.Range(0.2f, 1.2f));
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
        Vector3 want = Vector3.zero;
        if (Time.time >= waitUntil)
        {
            if (path.Count == 0)
            {
                waitUntil = Time.time + (rushing ? Random.Range(3f, 5f) : Random.Range(3f, 9f));
                PickWander();
            }
            else
            {
                Vector3 to = (path.Peek() - rb.position).Flat();
                if (to.magnitude < 0.45f) { path.Dequeue(); stuckSince = Time.time; }
                else want = to.normalized * (rushing ? rushSpeed : walkSpeed);
                // Takıldıysa (biri önünü kesti) bu noktayı atla
                if ((rb.position - lastPos).Flat().magnitude > 0.02f) stuckSince = Time.time;
                else if (Time.time - stuckSince > 2.5f && path.Count > 0) { path.Dequeue(); stuckSince = Time.time; }
            }
        }
        lastPos = rb.position;
        Vector3 v = rb.GetVelocity();
        Vector3 h = Vector3.MoveTowards(v.Flat(), want, 10f * dt);
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));
        Vector3 face = h.sqrMagnitude > 0.05f ? h : (facing != null ? -facing.transform.forward : Vector3.zero);
        if (face.sqrMagnitude > 0.01f)
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(face.normalized), 360f * dt));
    }
}
