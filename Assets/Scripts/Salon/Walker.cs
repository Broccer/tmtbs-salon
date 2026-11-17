using UnityEngine;

/// <summary>
/// Oyuncunun borsacısı: W/S ileri-geri, A/D döner, Shift koşar. En yakın kürsüyü bulur.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Walker : MonoBehaviour
{
    public float walkSpeed = 3.2f;
    public float runSpeed = 6f;
    public float turnSpeed = 150f;
    public float interactRange = 3.4f;
    public TextMesh nameTag;

    Rigidbody rb;
    public Kiosk Nearest { get; private set; }

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void FixedUpdate()
    {
        Vector2 m = InputReader.Move();
        float dt = Time.fixedDeltaTime;
        float turn = m.x * turnSpeed * dt;
        // Geri giderken dönüş yönü ters hissettirmesin
        if (m.y < -0.1f) turn = -turn * 0.6f;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));

        float speed = InputReader.Run() ? runSpeed : walkSpeed;
        if (m.y < 0f) speed *= 0.6f;
        Vector3 fwd = rb.rotation * Vector3.forward;
        Vector3 want = fwd * (m.y * speed);
        Vector3 v = rb.GetVelocity();
        Vector3 h = Vector3.MoveTowards(v.Flat(), want, 20f * dt);
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));
    }

    void Update()
    {
        // Önündeki en yakın kürsü
        Kiosk best = null;
        float bestD = interactRange;
        Vector3 fwd = transform.forward;
        foreach (var k in Kiosk.All)
        {
            Vector3 to = (k.transform.position - transform.position).Flat();
            float d = to.magnitude;
            if (d > bestD) continue;
            if (d > 1.6f && Vector3.Dot(fwd, to / d) < 0.2f) continue; // arkadakileri sayma
            best = k;
            bestD = d;
        }
        Nearest = best;
    }

    public void SetName(string n)
    {
        if (nameTag) nameTag.text = n;
    }

    public void Teleport(Vector3 p, Quaternion r)
    {
        rb.SetVelocity(Vector3.zero);
        rb.position = p;
        rb.rotation = r;
        transform.SetPositionAndRotation(p, r);
    }
}
