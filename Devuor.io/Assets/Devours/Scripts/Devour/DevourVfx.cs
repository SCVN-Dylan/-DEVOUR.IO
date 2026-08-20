using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HIEU UNG HUT: sinh mot OBJECT bay tu than nan nhan vao mom con dang hut. Toi mom thi ke hut
/// moi thuc su AN duoc so XP mang theo.
///
/// MOI OBJECT = MOT LEVEL. Khong con quan he "1 level ra N hat" nhu ban ParticleSystem cu -
/// nhin dong object bay la dem duoc dung so level dang chuyen chu.
///
/// VI SAO LA OBJECT CHU KHONG PHAI PARTICLE: de art thay VFX ma khong dong toi code. Prefab keo
/// vao 'flyPrefab' chi can la mot object RONG, ben trong nhet gi tuy y (ParticleSystem, mesh,
/// trail...). Class nay chi dat vi tri / co / mau roi keo no ve mom.
///
/// KHONG Rigidbody, KHONG collider: bay bang cach ghi thang transform.position moi frame. Nho vay
/// object CHAC CHAN toi noi - khac han ban te-bao-vat-ly rat xa xua (vien roi ra dat, het han giua
/// duong, nan nhan mat 30 ma ke hut chi an 28, so lieu khong bao gio khop).
///
/// CO POOL: nhip rut ngan dan nen mot tran keo dai co the sinh vai chuc object. Instantiate/Destroy
/// tung cai la rac cho GC; lay ra - tra ve thi khong ton gi. Pool nam tren tung con, khong dung
/// chung, de con nay chet khong keo theo object dang bay cua con khac.
///
/// Component nay nam tren con DI HUT (khong phai nan nhan): moi object trong pool deu bay ve dung
/// mot dich la mom cua chinh chu.
/// </summary>
[DisallowMultipleComponent]
public class DevourVfx : MonoBehaviour
{
    [Header("Tham chieu (keo vao Inspector)")]
    [Tooltip("MOM - diem moi object bay ve. De trong = tu tim object ten 'Mouth' o cha")]
    [SerializeField] private Transform _mouth;

    [Tooltip("THAN con nay - dung de do CO, cho object to theo khi len cap.\n" +
             "De trong = tu tim Creature o cha")]
    [SerializeField] private Transform _body;

    [Tooltip("PREFAB bay ve mom. Nen la object RONG, ben trong nhet VFX gi tuy art.\n" +
             "KHONG can Rigidbody/collider - class nay tu keo bang transform.position.\n" +
             "De trong = khong co hieu ung, nhung XP van chuyen binh thuong (cong ngay tuc thi).")]
    public GameObject flyPrefab;

    [Header("Co object")]
    [Tooltip("Co object = CO THAN KE HUT x he so nay. 0.5 = bang nua than minh")]
    [Range(0.01f, 2f)] public float bodySizeFactor = 0.5f;

    [Tooltip("BAT: kep co object khong bao gio vuot qua CO NAN NHAN - luong object noi dung mot\n" +
             "su that: dang an con co nao. TAT: luon bang bodySizeFactor x than minh")]
    public bool clampToVictim = true;

    [Header("Duong bay")]
    [Tooltip("THOI LUONG bay: object luon mat dung bay nhieu GIAY de toi mom, xa hay gan cung the.\n\n" +
             "VI SAO KHONG DUNG VAN TOC (u/s): tam hut o level thap chi ~1.5 don vi, ma van toc cu\n" +
             "dat 30-40 u/s -> object bay het quang duong trong 10ms, tuc CHUA DUOC MOT FRAME, sinh\n" +
             "ra va bien mat trong cung mot khung hinh nen khong ai nhin thay. Doi sang thoi luong\n" +
             "thi object luon hien du lau de doc, o moi level va moi khoang cach.")]
    public float flyDuration = 0.35f;

    [Range(0.5f, 4f)]
    [Tooltip("Do CONG cua duong bay theo thoi gian. 1 = deu deu; >1 = luc dau cham roi LAO nhanh\n" +
             "vao mom (dung cam giac bi hut). 2 = binh phuong.")]
    public float flyAccel = 2f;

    [Tooltip("Do TAN ngau nhien quanh diem xuat phat (world). 0 = moi object ra tu dung mot diem")]
    public float scatter = 0.6f;

    // KHONG con maxFlyTime: doi sang bay theo THOI LUONG thi moi object deu ve dich sau dung
    // flyDuration giay, khong the ket lai giua duong nen khong can luoi an toan theo gio nua.

    [Header("Pool")]
    [Tooltip("So object tao san luc dau. Dat khoang bang so object bay cung luc luc cao diem")]
    public int prewarm = 8;

    [Tooltip("TRAN so object song cung luc. Vuot qua thi object CU NHAT bi thu hoi som (va van\n" +
             "tra du XP) - tha xau mot nhip con hon de so luong troi tu do")]
    public int maxAlive = 64;

    /// <summary>Co san sang khong (du mom). Thieu flyPrefab thi van 'san sang' - chi la khong co hinh.</summary>
    public bool IsReady { get { return _mouth != null; } }

    /// <summary>Mot object dang bay.</summary>
    private class Flyer
    {
        public GameObject go;
        public Transform tf;
        public int xp;            // so level dang mang - toi mom thi ke hut an bay nhieu
        public Vector3 from;      // diem xuat phat, de noi suy theo thoi gian
        public float bornAt;      // gio sinh ra
    }

    private readonly List<Flyer> _alive = new List<Flyer>();
    private readonly Stack<GameObject> _pool = new Stack<GameObject>();
    private SimpleSuction _suction;   // cua chinh chu - nhan XP khi object ve toi mom

    void Reset() { AutoFill(); }

    void Awake()
    {
        AutoFill();
        Prewarm();
    }

    private void AutoFill()
    {
        if (_body == null)
        {
            Creature c = GetComponentInParent<Creature>();
            _body = c != null ? c.transform : transform.parent;
        }

        if (_mouth == null)
        {
            SimpleSuction s = GetComponentInParent<SimpleSuction>();
            if (s != null && s.mouth != null) _mouth = s.mouth;
            else if (_body != null)
            {
                Transform m = _body.Find("Mouth");
                if (m != null) _mouth = m;
            }
            if (_mouth == null) _mouth = transform;
        }

        if (_suction == null) _suction = GetComponentInParent<SimpleSuction>();
    }

    private void Prewarm()
    {
        if (flyPrefab == null) return;
        for (int i = 0; i < prewarm; i++) _pool.Push(CreateInstance());
    }

    private GameObject CreateInstance()
    {
        GameObject go = Instantiate(flyPrefab);
        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// RUT: nan nhan vua tut 'levels' cap -> ban ra bay nhieu object, moi cai mang 1 level.
    /// Goi tren VFX cua KE HUT, truyen tam than + mau skin + co than cua NAN NHAN.
    /// </summary>
    public void EmitDrain(Vector3 fromWorld, int levels, Color victimColor, float victimScale)
    {
        if (levels <= 0) return;
        for (int i = 0; i < levels; i++) SpawnOne(fromWorld, victimColor, victimScale, 1);
    }

    /// <summary>CHET: mot phat that da, tat ca XP con lai bay ve ke giet trong MOT object.</summary>
    public void EmitDeath(Vector3 fromWorld, Color victimColor, float victimScale, int xp)
    {
        SpawnOne(fromWorld, victimColor, victimScale, Mathf.Max(0, xp));
    }

    /// <summary>
    /// Sinh mot object mang 'xp' level. Thieu flyPrefab thi TRA XP NGAY - khong co hinh nhung so
    /// lieu van khop, khong bao gio boc hoi.
    /// </summary>
    private void SpawnOne(Vector3 center, Color color, float victimScale, int xp)
    {
        if (!IsReady || flyPrefab == null) { Deliver(xp); return; }

        if (_alive.Count >= Mathf.Max(1, maxAlive)) RetireOldest();

        GameObject go = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
        Transform tf = go.transform;

        float s = CurrentSize(victimScale);
        Vector3 start = center + Random.insideUnitSphere * scatter;
        tf.position = start;
        tf.rotation = Quaternion.identity;
        tf.localScale = Vector3.one * s;
        go.SetActive(true);

        Tint(go, color);

        _alive.Add(new Flyer { go = go, tf = tf, xp = xp, from = start, bornAt = Time.time });
    }

    /// <summary>
    /// Co object: NUA than KE HUT, nhung khong bao gio to hon NAN NHAN.
    /// victimScale &lt;= 0 = ben goi khong biet co nan nhan -> bo qua tran.
    /// </summary>
    private float CurrentSize(float victimScale)
    {
        float body = _body != null ? _body.lossyScale.x : 1f;
        float s = body * bodySizeFactor;
        if (clampToVictim && victimScale > 0.0001f) s = Mathf.Min(s, victimScale);
        return Mathf.Max(0.001f, s);
    }

    /// <summary>
    /// To mau object theo skin nan nhan. Ghi vao startColor cua MOI ParticleSystem con - khong
    /// dung sharedMaterial: mau la thu doi theo tung nan nhan, ghi vao material dung chung la
    /// doi mau cho tat ca object khac dang bay.
    /// </summary>
    private void Tint(GameObject go, Color color)
    {
        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule m = systems[i].main;
            m.startColor = color;
            systems[i].Clear(true);
            systems[i].Play(true);   // lay tu pool ra: phai choi lai tu dau chu khong tiep ban cu
        }

        // Object co the la MESH chu khong phai he hat (vd mot Sphere). To bang
        // MaterialPropertyBlock chu KHONG dung renderer.material: material se clone mot ban moi
        // cho tung object moi lan doi mau - ro ri material instance, dung thu giet fps mobile.
        // PropertyBlock ghi thang vao lenh ve, khong sinh material nao.
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] is ParticleSystemRenderer) continue;   // he hat da xu ly o tren
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            rends[i].GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(ColorId, color);      // ghi ca hai ten: URP dung _BaseColor, Built-in dung _Color
            rends[i].SetPropertyBlock(_mpb);
        }
    }

    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>
    /// KEO ca dan object ve phia mom, moi frame.
    ///
    /// Chay o LateUpdate chu khong Update: mom bam theo xuong nhan vat, ma xuong thi duoc Animator
    /// ghi o giai doan sau Update. Keo o Update la doc vi tri mom cua FRAME TRUOC, object se lun
    /// nhun mot nhip khi nhan vat chay nhanh.
    /// </summary>
    void LateUpdate()
    {
        if (_alive.Count == 0 || _mouth == null) return;

        Vector3 target = _mouth.position;
        float now = Time.time;
        float dur = Mathf.Max(0.01f, flyDuration);

        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            Flyer f = _alive[i];
            if (f.go == null) { _alive.RemoveAt(i); continue; }

            float t = (now - f.bornAt) / dur;

            if (t >= 1f)
            {
                Deliver(f.xp);          // TOI MOM = ke hut an duoc so XP object nay mang
                Recycle(f);
                _alive.RemoveAt(i);
                continue;
            }

            // Noi suy tu diem xuat phat toi MOM HIEN TAI (doc lai moi frame): nguoi choi dang
            // chay thi mom di chuyen, ban toi diem cu se thay object lao tret ra sau lung.
            float e = flyAccel <= 1.001f ? t : Mathf.Pow(t, flyAccel);
            f.tf.position = Vector3.Lerp(f.from, target, e);
        }
    }

    /// <summary>Ke hut an so XP mot object mang ve.</summary>
    private void Deliver(int xp)
    {
        if (xp <= 0) return;
        if (_suction == null) _suction = GetComponentInParent<SimpleSuction>();
        if (_suction != null) _suction.GainXp(xp);
    }

    private void Recycle(Flyer f)
    {
        if (f.go == null) return;
        f.go.SetActive(false);
        _pool.Push(f.go);
    }

    /// <summary>Cham tran maxAlive: thu hoi cai CU NHAT nhung VAN TRA DU XP cua no.</summary>
    private void RetireOldest()
    {
        if (_alive.Count == 0) return;
        Flyer f = _alive[0];
        Deliver(f.xp);
        Recycle(f);
        _alive.RemoveAt(0);
    }

    /// <summary>
    /// Chu chet giua chung: TRA HET XP dang bay roi don sach.
    ///
    /// Khong co buoc nay thi so XP dang tren duong bay boc hoi - dung cai loi ma ban te-bao-vat-ly
    /// ngay xua mac phai (nan nhan mat 30, ke hut an 28). Creature.Die goi ham nay truoc khi huy.
    /// </summary>
    public void FlushAll()
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            Deliver(_alive[i].xp);
            Recycle(_alive[i]);
        }
        _alive.Clear();
    }

    void OnDestroy()
    {
        // Khong goi Deliver o day: object cha dang bi huy, GainXp luc nay la ghi vao xac chet.
        // Ben nao can giu XP thi goi FlushAll() TRUOC khi huy.
        _alive.Clear();
        _pool.Clear();
    }
}
