using UnityEngine;

/// <summary>
/// Borsacının arkasından izleyen kamera. Duvara/tavana çarpınca yaklaşır (küre ışını),
/// oturunca panoya doğru bakar. Menüde salonu yavaşça dolaşır. Yazılar kameraya döner.
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5.2f;
    public float height = 2.6f;
    public float smoothing = 6f;
    public bool overview = true;
    public bool seated;

    void LateUpdate()
    {
        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        Vector3 pos, look;
        if (overview || target == null)
        {
            float a = Time.time * 0.07f;
            pos = new Vector3(Mathf.Sin(a) * 6f, 4.2f, -14.5f + Mathf.Cos(a) * 1.5f);
            look = new Vector3(0, 1.6f, -6f);
            k = 1f - Mathf.Exp(-1.5f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - transform.position), k);
        }
        else
        {
            Vector3 f = target.forward.Flat().normalized;
            Vector3 head = target.position + Vector3.up * 1.6f;
            if (seated)
            {
                pos = target.position - f * 2.2f + Vector3.up * 2.1f;
                look = target.position + f * 20f + Vector3.up * 4.5f;
            }
            else
            {
                pos = target.position - f * distance + Vector3.up * height;
                look = head + f * 2.5f;
            }
            // Duvar/tavan arasına girmesin: baştan istenen konuma küre ışını
            Vector3 dir = pos - head;
            float len = dir.magnitude;
            if (len > 0.01f && Physics.SphereCast(head, 0.3f, dir / len, out RaycastHit hit, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                pos = head + dir / len * Mathf.Max(0.4f, hit.distance - 0.05f);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - transform.position), k);
        }
        foreach (var b in Billboard.All) b.Face(transform);
    }

    /// <summary>Işınlanmada kamerayı hemen yerine al.</summary>
    public void Snap()
    {
        if (target == null) return;
        Vector3 f = target.forward.Flat().normalized;
        transform.position = target.position - f * 3f + Vector3.up * 2.2f;
        transform.rotation = Quaternion.LookRotation(target.position + Vector3.up * 1.5f + f * 2f - transform.position);
    }
}
