using UnityEngine;

/// <summary>
/// Oyuncunun borsacısı: hareket kameraya göredir (W/ileri = kameranın baktığı yön, A/D yana), karakter gittiği yöne
/// yumuşakça döner. Shift koşar; telefonda joystick sonuna kadar itilince de koşar.
/// Önündeki en yakın kullanılabilir şeyi (banko, vezne, sandalye, bilgisayar) bulur; sandalyeye oturabilir.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Walker : MonoBehaviour
{
    public float walkSpeed = 3.4f;
    public float runSpeed = 7f;
    public float turnSpeed = 720f; // derece/sn: gidilen yöne dönüş
    public TextMesh nameTag;

    Rigidbody rb;
    Transform cam;
    public Interactable Nearest { get; private set; }
    public Seat SittingOn { get; private set; }

    void Awake() { rb = GetComponent<Rigidbody>(); }

    /// <summary>Girdiyi (x yan, y ileri) kameranın yatay yönüne göre dünyaya çevirir.</summary>
    Vector3 WorldMove(Vector2 m)
    {
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        Vector3 f = cam != null ? cam.forward.Flat() : transform.forward.Flat();
        if (f.sqrMagnitude < 0.0001f) f = cam != null ? cam.up.Flat() : Vector3.forward;
        f.Normalize();
        Vector3 r = new Vector3(f.z, 0f, -f.x);
        return Vector3.ClampMagnitude(r * m.x + f * m.y, 1f);
    }

    void FixedUpdate()
    {
        if (rb.isKinematic) return;
        Vector2 m = InputReader.Move();
        float dt = Time.fixedDeltaTime;
        Vector3 dir = WorldMove(m);
        float amount = dir.magnitude;
        if (amount > 0.05f)
        {
            Quaternion face = Quaternion.LookRotation(dir / amount, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, face, turnSpeed * dt));
        }
        bool run = InputReader.Run() || (TouchControls.Move.magnitude > 0.95f);
        float speed = run ? runSpeed : walkSpeed;
        Vector3 want = dir * speed;
        Vector3 v = rb.GetVelocity();
        Vector3 h = Vector3.MoveTowards(v.Flat(), want, 24f * dt);
        rb.SetVelocity(new Vector3(h.x, v.y, h.z));
    }

    void Update()
    {
        if (SittingOn != null)
        {
            Nearest = null;
            // Hareket girişi gelince kalk
            if (InputReader.Move().sqrMagnitude > 0.2f) StandUp();
            return;
        }
        Interactable best = null;
        float bestScore = float.MaxValue;
        Vector3 fwd = transform.forward;
        Vector3 me = transform.position;
        foreach (var it in Interactable.All)
        {
            if (it.kind == Interactable.Kind.Counter && (it.counter == null || it.counter.stock == null)) continue;
            if (it.kind == Interactable.Kind.Seat && it.seat != null && it.seat.taken) continue;
            Vector3 to = (it.Point - me).Flat();
            float d = to.magnitude;
            if (d > it.range) continue;
            Vector3 toObj = (it.transform.position - me).Flat();
            if (toObj.magnitude > 0.5f && Vector3.Dot(fwd, toObj.normalized) < -0.2f) continue; // arkadakileri sayma
            float score = d - Vector3.Dot(fwd, toObj.normalized) * 0.4f;
            if (score < bestScore) { bestScore = score; best = it; }
        }
        Nearest = best;
    }

    public void SetName(string n) { if (nameTag) nameTag.text = n; }

    public void SetFrozen(bool frozen)
    {
        if (frozen) rb.SetVelocity(Vector3.zero);
        rb.isKinematic = frozen;
    }

    public void Teleport(Vector3 p, Quaternion r)
    {
        if (!rb.isKinematic) rb.SetVelocity(Vector3.zero);
        rb.position = p;
        rb.rotation = r;
        transform.SetPositionAndRotation(p, r);
    }

    public void Sit(Seat s)
    {
        if (s == null) return;
        SittingOn = s;
        s.taken = true;
        SetFrozen(true);
        Teleport(s.SitPosition + new Vector3(0, 0.02f, 0), s.SitRotation);
        var ca = GetComponent<CharAnim>();
        if (ca != null) ca.sitting = true;
    }

    public void StandUp()
    {
        var s = SittingOn;
        if (s == null) return;
        SittingOn = null;
        s.taken = false;
        var ca = GetComponent<CharAnim>();
        if (ca != null) ca.sitting = false;
        Teleport(s.transform.TransformPoint(new Vector3(0, 0, -0.9f)), s.SitRotation); // sandalyenin arkasına (masa önde)
        SetFrozen(false);
    }
}
