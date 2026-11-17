using UnityEngine;

/// <summary>
/// Unity 6 bazı Rigidbody/fizik API adlarını değiştirdi (velocity → linearVelocity,
/// PhysicMaterial → PhysicsMaterial). Kod hem Unity 6 hem 2022 LTS'de derlensin diye
/// bu küçük yardımcıdan geçiyoruz.
/// </summary>
public static class RigidbodyCompat
{
    public static Vector3 GetVelocity(this Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    public static void SetVelocity(this Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    public static void SetDamping(this Rigidbody rb, float linear, float angular)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = linear;
        rb.angularDamping = angular;
#else
        rb.drag = linear;
        rb.angularDrag = angular;
#endif
    }

    public static void SetSurface(Collider c, float bounciness, float friction)
    {
#if UNITY_6000_0_OR_NEWER
        var m = new PhysicsMaterial(c.name + "_Yuzey")
        {
            bounciness = bounciness,
            dynamicFriction = friction,
            staticFriction = friction,
            bounceCombine = PhysicsMaterialCombine.Maximum,
            frictionCombine = friction <= 0f ? PhysicsMaterialCombine.Minimum : PhysicsMaterialCombine.Average
        };
#else
        var m = new PhysicMaterial(c.name + "_Yuzey")
        {
            bounciness = bounciness,
            dynamicFriction = friction,
            staticFriction = friction,
            bounceCombine = PhysicMaterialCombine.Maximum,
            frictionCombine = friction <= 0f ? PhysicMaterialCombine.Minimum : PhysicMaterialCombine.Average
        };
#endif
        c.sharedMaterial = m;
    }

    public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);
}
