using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun yaklaşıp E (telefonda İŞLEM) ile kullanabildiği şeyler: banko, banka veznesi, sandalye, ev bilgisayarı.
/// </summary>
public class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    public enum Kind { Counter, Teller, Seat, Computer, BusStop, Buffet, JobCenter, Estate, CarDealer, Auction, Shop, Parent, Duel, Door }
    public Kind kind;
    public string prompt = "";
    public string buttonLabel = "İŞLEM";
    public float range = 2.4f;
    public Vector3 localPoint;     // oyuncunun durması gereken nokta (yerel)
    public Kiosk counter;
    public Seat seat;
    public int arg;                // durak / dükkân / ebeveyn sırası

    public Vector3 Point => transform.TransformPoint(localPoint);

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static Interactable Make(GameObject g, Kind kind, Vector3 localPoint, string prompt, string button, float range = 2.4f)
    {
        var i = g.AddComponent<Interactable>();
        i.kind = kind;
        i.localPoint = localPoint;
        i.prompt = prompt;
        i.buttonLabel = button;
        i.range = range;
        return i;
    }
}

/// <summary>Oturulabilir sandalye: oturma noktası ve baktığı yön.</summary>
public class Seat : MonoBehaviour
{
    public bool taken;
    public Vector3 SitPosition => transform.position;
    public Quaternion SitRotation => transform.rotation;
}
