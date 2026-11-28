using UnityEngine;

/// <summary>
/// Üçüncü şahıs yörünge kamerası: oyuncu kamerayı fareyle / sağ çubukla / parmakla döndürür, tekerlek ya da
/// iki parmakla yakınlaştırır. Yürürken bir süre dokunulmazsa kamera yavaşça karakterin arkasına geçer.
/// Duvara/tavana çarpınca yaklaşır (küre ışını), oturunca panoya bakar, menüde salonu yavaşça dolaşır.
/// Yazılar kameraya döner.
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5.2f;
    public float minDistance = 2f;
    public float maxDistance = 9f;
    public float smoothing = 10f;
    public bool overview = true;
    public bool seated;
    /// <summary>Oturulan çalışma masası ekranı: kamera yavaşça ekranın önüne gelir.</summary>
    public Transform focus;

    /// <summary>Kameranın yatay açısı (derece). Hareket bu yöne göre okunur.</summary>
    public float Yaw { get; private set; }
    public float Pitch { get; private set; } = 16f;
    const float MinPitch = -8f, MaxPitch = 60f;
    const float PivotHeight = 1.55f;

    float idle = 99f;          // son kamera girdisinden bu yana geçen süre
    Vector3 pivot;
    bool pivotInit;

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (overview || target == null)
        {
            float a = Time.time * 0.07f;
            Vector3 pos = new Vector3(Mathf.Sin(a) * 6f, 4.2f, -14.5f + Mathf.Cos(a) * 1.5f);
            Vector3 look = new Vector3(0, 1.6f, -6f);
            float k = 1f - Mathf.Exp(-1.5f * dt);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - transform.position), k);
            pivotInit = false;
        }
        else if (seated && focus != null)
        {
            Vector3 pos = focus.position + focus.forward * 0.85f + Vector3.up * 0.06f; // ekran çerçevesiyle birlikte görünsün
            float k = 1f - Mathf.Exp(-3.2f * dt);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(focus.position - pos), k);
            Yaw = transform.eulerAngles.y;
            pivotInit = false;
        }
        else if (seated)
        {
            Vector3 f = target.forward.Flat().normalized;
            Vector3 pos = target.position - f * 2.2f + Vector3.up * 2.1f;
            Vector3 look = target.position + f * 20f + Vector3.up * 4.5f;
            float k = 1f - Mathf.Exp(-6f * dt);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - transform.position), k);
            Yaw = target.eulerAngles.y;
            pivotInit = false;
        }
        else Orbit(dt);
        foreach (var b in Billboard.All) b.Face(transform);
    }

    void Orbit(float dt)
    {
        // Girdi: döndürme (derece) ve yakınlaştırma
        Vector2 look = InputReader.Look();
        float zoom = InputReader.Zoom();
        if (look.sqrMagnitude > 0.0001f || Mathf.Abs(zoom) > 0.0001f) idle = 0f; else idle += dt;
        Yaw += look.x;
        Pitch = Mathf.Clamp(Pitch - look.y, MinPitch, MaxPitch);
        distance = Mathf.Clamp(distance - zoom, minDistance, maxDistance);

        // Yürürken kameraya bir süre dokunulmazsa yavaşça karakterin arkasına geç (karaktere doğru yürürken değil)
        Vector2 mv = InputReader.Move();
        // Yalnız çoğunlukla ileri giderken: yana yürürken kovalarsa kamera karakterin etrafında döner durur.
        if (idle > 1.6f && mv.y > 0.3f && Mathf.Abs(mv.x) < mv.y)
        {
            float targetYaw = target.eulerAngles.y;
            float rate = 70f * Mathf.Clamp01(mv.magnitude);
            Yaw = Mathf.MoveTowardsAngle(Yaw, targetYaw, rate * dt);
            Pitch = Mathf.MoveTowards(Pitch, 16f, 12f * dt);
        }

        // Pivot (baş hizası) yumuşak izlenir; açı girdisi gecikmesiz uygulanır
        Vector3 want = target.position + Vector3.up * PivotHeight;
        if (!pivotInit) { pivot = want; pivotInit = true; }
        pivot = Vector3.Lerp(pivot, want, 1f - Mathf.Exp(-smoothing * dt));

        Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
        Vector3 dir = rot * Vector3.back;
        float len = distance;
        if (Physics.SphereCast(pivot, 0.3f, dir, out RaycastHit hit, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            len = Mathf.Max(0.5f, hit.distance - 0.05f);
        Vector3 camPos = pivot + dir * len;
        if (camPos.y < 0.3f) camPos.y = 0.3f; // yerin altına inmesin
        transform.position = camPos;
        transform.rotation = Quaternion.LookRotation(pivot + Vector3.up * 0.15f - camPos);
    }

    /// <summary>Işınlanmada kamerayı hemen karakterin arkasına al.</summary>
    public void Snap()
    {
        if (target == null) return;
        Yaw = target.eulerAngles.y;
        Pitch = 16f;
        pivot = target.position + Vector3.up * PivotHeight;
        pivotInit = true;
        Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
        transform.position = pivot + rot * Vector3.back * Mathf.Min(distance, 4f);
        transform.rotation = Quaternion.LookRotation(pivot - transform.position);
    }
}
