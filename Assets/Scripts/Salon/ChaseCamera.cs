using UnityEngine;

/// <summary>Borsacının arkasından izleyen kamera; salon duvarlarının dışına çıkmaz. Yazılar kameraya döner.</summary>
public class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5.5f;
    public float height = 3.0f;
    public float smoothing = 5f;
    public Vector2 bounds = new Vector2(16.3f, 22.3f);

    /// <summary>Menüde salonu yukarıdan gösteren yavaş tur.</summary>
    public bool overview = true;

    void LateUpdate()
    {
        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        Vector3 pos, look;
        if (overview || target == null)
        {
            float a = Time.time * 0.06f;
            pos = new Vector3(Mathf.Sin(a) * 11f, 7.5f, -14f + Mathf.Cos(a) * 3f);
            look = new Vector3(0, 3f, 8f);
            k = 1f - Mathf.Exp(-1.5f * Time.deltaTime);
        }
        else
        {
            Vector3 f = target.forward.Flat().normalized;
            pos = target.position - f * distance + Vector3.up * height;
            look = target.position + Vector3.up * 1.5f + f * 2.5f;
        }
        pos.x = Mathf.Clamp(pos.x, -bounds.x, bounds.x);
        pos.z = Mathf.Clamp(pos.z, -bounds.y, bounds.y);
        transform.position = Vector3.Lerp(transform.position, pos, k);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - transform.position), k);

        foreach (var b in Billboard.All) b.Face(transform);
    }
}
