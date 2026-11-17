using System.Collections.Generic;
using UnityEngine;

/// <summary>Kameraya dönen yazı (isim etiketleri, baloncuklar).</summary>
public class Billboard : MonoBehaviour
{
    public static readonly List<Billboard> All = new List<Billboard>();
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Face(Transform cam)
    {
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }
}
