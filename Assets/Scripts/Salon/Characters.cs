using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mixamo karakterleri (Resources/Characters): Louise (kadın), Josh, Joe (erkek).
/// Animatör: Resources/Borsaci (Durum 0 bekle, 1 yürü, 2 otur, 3 konuş; Hiz yürüme hızı).
/// Uzaktaki karakterler LODGroup ile iki parçalı hafif bir vekile döner (telefonda akıcılık için).
/// Modeller yüklenemezse eski ilkel gövdeye (Avatar) düşer.
/// </summary>
public static class Characters
{
    public static readonly string[] Names = { "Louise", "Josh", "Joe" };
    public static readonly string[] Labels = { "Kadın", "Erkek (takım)", "Erkek (spor)" };
    static readonly Color[] ProxyColors = { new Color(0.25f, 0.22f, 0.30f), new Color(0.16f, 0.18f, 0.26f), new Color(0.22f, 0.26f, 0.22f) };

    static GameObject[] prefabs;
    static RuntimeAnimatorController ctrl;
    static bool loaded;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        prefabs = new GameObject[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            try { prefabs[i] = Resources.Load<GameObject>("Characters/" + Names[i]); }
            catch (System.Exception e) { Debug.LogWarning(e); }
        }
        try { ctrl = Resources.Load<RuntimeAnimatorController>("Borsaci"); } catch { }
    }

    public static bool Available
    {
        get
        {
            Load();
            if (ctrl == null) return false;
            foreach (var p in prefabs) if (p != null) return true;
            return false;
        }
    }

    public static int Count => Names.Length;

    const string PrefKey = "tmtbs_salon_model";

    public static int SavedModel
    {
        get { try { return Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 1), 0, Names.Length - 1); } catch { return 1; } }
        set { try { PlayerPrefs.SetInt(PrefKey, value); PlayerPrefs.Save(); } catch { } }
    }

    /// <summary>
    /// owner altına "Gorunum" adlı karakteri kurar (eskisini siler). lod: uzakta vekile dön.
    /// Dönüş: kurulan karakterin animatörü (model yoksa null; ilkel gövde kurulur).
    /// </summary>
    public static CharAnim Spawn(Transform owner, int model, bool lod, Look fallback, float height = 1.76f)
    {
        Load();
        var old = owner.Find("Gorunum");
        if (old != null) { old.name = "Gorunum_eski"; Object.Destroy(old.gameObject); }

        model = Mathf.Abs(model) % Names.Length;
        GameObject prefab = prefabs != null ? prefabs[model] : null;
        if (prefab == null) for (int i = 0; prefabs != null && i < prefabs.Length && prefab == null; i++) prefab = prefabs[i];
        if (prefab == null || ctrl == null)
        {
            Avatar.Rebuild(owner, fallback);
            return null;
        }

        var root = new GameObject("Gorunum").transform;
        root.SetParent(owner, false);
        var go = Object.Instantiate(prefab, root, false);
        go.name = "Model";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        SetLayer(go.transform, 2); // Ignore Raycast: kamera çarpışması ve tıklama karakterlere takılmasın

        // Boyu ölç, hedef boya getir (Mixamo genelde ~1,75 m gelir; birim farkı olursa düzelt)
        var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        float h = 0f;
        foreach (var r in smrs)
        {
            if (r.sharedMesh == null) continue;
            var b = r.sharedMesh.bounds;
            float s = r.transform.lossyScale.y / Mathf.Max(0.0001f, owner.lossyScale.y);
            h = Mathf.Max(h, b.size.y * s, b.size.z * s);
        }
        if (h > 0.01f && (h < 1.3f || h > 2.3f)) go.transform.localScale *= height / h;

        foreach (var r in smrs)
        {
            r.quality = SkinQuality.Bone2;
            r.updateWhenOffscreen = false;
            r.skinnedMotionVectors = false;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FixMaterials(r);
        }

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        if (lod)
        {
            var proxy = new GameObject("Vekil").transform;
            proxy.SetParent(root, false);
            var mat = Gfx.Lit(ProxyColors[model], 0.3f);
            var body = Gfx.Prim(PrimitiveType.Capsule, "Govde", proxy, new Vector3(0, 0.78f, 0), new Vector3(0.48f, 0.78f, 0.32f), mat, false);
            var head = Gfx.Prim(PrimitiveType.Sphere, "Bas", proxy, new Vector3(0, 1.62f, 0), Vector3.one * 0.24f, Gfx.Lit(new Color(0.8f, 0.64f, 0.5f), 0.3f), false);
            body.layer = head.layer = 2;
            var lg = root.gameObject.AddComponent<LODGroup>();
            var r0 = new List<Renderer>(smrs).ToArray();
            lg.SetLODs(new[]
            {
                new LOD(0.09f, r0),
                new LOD(0.008f, new Renderer[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>() }),
            });
            lg.RecalculateBounds();
        }

        var ca = owner.GetComponent<CharAnim>();
        if (ca == null) ca = owner.gameObject.AddComponent<CharAnim>();
        ca.anim = anim;
        return ca;
    }

    /// <summary>Uzaktaki karakter için iki parçalı vekil (gövde + baş).</summary>
    public static void Proxy(Transform owner, int model)
    {
        var old = owner.Find("Gorunum");
        if (old != null) { old.name = "Gorunum_eski"; Object.Destroy(old.gameObject); }
        var root = new GameObject("Gorunum").transform;
        root.SetParent(owner, false);
        var body = Gfx.Prim(PrimitiveType.Capsule, "Govde", root, new Vector3(0, 0.8f, 0), new Vector3(0.5f, 0.8f, 0.34f), Gfx.Lit(ProxyColors[Mathf.Abs(model) % ProxyColors.Length], 0.3f), false);
        var head = Gfx.Prim(PrimitiveType.Sphere, "Bas", root, new Vector3(0, 1.64f, 0), Vector3.one * 0.25f, Gfx.Lit(new Color(0.8f, 0.64f, 0.5f), 0.3f), false);
        body.layer = head.layer = 2;
    }

    static readonly Dictionary<Material, Material> fixedMats = new Dictionary<Material, Material>();

    /// <summary>Desteklenmeyen shader'lı malzemeyi (WebGL) salonun malzemesiyle değiştirir; doku korunur.</summary>
    static void FixMaterials(Renderer r)
    {
        var mats = r.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null || (m.shader != null && m.shader.isSupported)) continue;
            if (!fixedMats.TryGetValue(m, out var nm))
            {
                nm = new Material(Gfx.Lit(Color.white, 0.2f));
                if (m.HasProperty("_MainTex")) nm.mainTexture = m.mainTexture;
                if (m.HasProperty("_Color")) nm.color = m.color;
                fixedMats[m] = nm;
            }
            mats[i] = nm;
            changed = true;
        }
        if (changed) r.sharedMaterials = mats;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
    }
}

/// <summary>
/// Kalabalık için: karakter kameraya yakınken gerçek (animasyonlu) model, uzaktayken hafif vekil.
/// Böylece salonda aynı anda yalnız yakındaki birkaç düzine model çizilir (telefonda bellek ve hız için).
/// </summary>
public class LazyChar : MonoBehaviour
{
    public int model;
    public Look look;
    public int idleState;
    public float near = 22f, far = 30f;

    static int budget; // bir karede en fazla birkaç model kur (takılmayı önler)
    static int budgetFrame;
    bool real;
    bool started;
    float nextCheck;

    void Start()
    {
        nextCheck = Random.value * 0.5f;
        if (!Characters.Available)
        {
            Avatar.Rebuild(transform, look);
            enabled = false;
            return;
        }
        Characters.Proxy(transform, model);
        started = true;
    }

    void Update()
    {
        if (!started || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.4f + Random.value * 0.2f;
        var cam = Camera.main;
        if (cam == null) return;
        float d = (cam.transform.position - transform.position).magnitude;
        if (!real && d < near)
        {
            if (budgetFrame != Time.frameCount) { budgetFrame = Time.frameCount; budget = 0; }
            if (budget >= 3) { nextCheck = Time.time; return; }
            budget++;
            var ca = Characters.Spawn(transform, model, false, look);
            if (ca != null) ca.idleState = idleState;
            real = true;
        }
        else if (real && d > far)
        {
            Characters.Proxy(transform, model);
            var ca = GetComponent<CharAnim>();
            if (ca != null) ca.anim = null;
            real = false;
        }
    }
}

/// <summary>
/// Karakter animasyonunu sürer: hıza göre bekle/yürü; oturma ve konuşma durumları dışarıdan verilir.
/// </summary>
public class CharAnim : MonoBehaviour
{
    public Animator anim;
    public int idleState = 0;   // 0 bekle, 3 konuş (görevliler)
    public bool sitting;
    public float walkClipSpeed = 1.45f; // Walking (In Place) klibinin m/s karşılığı

    Rigidbody rb;
    Vector3 lastPos;
    int lastState = -1;
    Animator lastAnim;
    static readonly int DurumId = Animator.StringToHash("Durum");
    static readonly int HizId = Animator.StringToHash("Hiz");

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lastPos = transform.position;
        if (anim != null) anim.SetFloat(HizId, 1f);
    }

    void Update()
    {
        if (anim == null) return;
        if (anim != lastAnim) { lastAnim = anim; lastState = -1; }
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = rb != null && !rb.isKinematic ? rb.GetVelocity().Flat().magnitude : (transform.position - lastPos).Flat().magnitude / dt;
        lastPos = transform.position;

        int st = sitting ? 2 : speed > 0.35f ? 1 : idleState;
        if (st != lastState)
        {
            anim.SetInteger(DurumId, st);
            lastState = st;
        }
        anim.SetFloat(HizId, st == 1 ? Mathf.Clamp(speed / walkClipSpeed, 0.6f, 2.6f) : 1f);
    }
}
