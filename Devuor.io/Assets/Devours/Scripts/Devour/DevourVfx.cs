using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HIEU UNG HUT: sinh mot OBJECT bay tu than nan nhan vao mom con dang hut. Toi mom thi ke hut
/// moi thuc su AN duoc so XP mang theo.
///
/// MOI OBJECT = MOT NHIP RUT (mac dinh), mang theo dung so level cua nhip do - khong con quan he
/// "1 level ra N hat" nhu ban ParticleSystem cu. Dau tran moi nhip mot level nen object = level,
/// cuoi tran bac rut len cao thi mot object mang ca cum (mergeDrainFlyer).
///
/// MOI OBJECT MOT CO NHU NHAU, du no mang 1 hay 60 level. Co chi doi theo THAN con nho hon trong
/// hai con (CurrentSize) - dung mot luat duy nhat tu truoc toi gio.
///
/// VI SAO LA OBJECT CHU KHONG PHAI PARTICLE: de art thay VFX ma khong dong toi code. Prefab keo
/// vao 'flyPrefab' chi can la mot object RONG, ben trong nhet gi tuy y (ParticleSystem, mesh,
/// trail...). Class nay chi dat vi tri / co / mau roi keo no ve mom.
///
/// KHONG Rigidbody, KHONG collider: bay bang cach ghi thang transform.position moi frame. Nho vay
/// object CHAC CHAN toi noi - khac han ban te-bao-vat-ly rat xa xua (vien roi ra dat, het han giua
/// duong, nan nhan mat 30 ma ke hut chi an 28, so lieu khong bao gio khop).
///
/// DUONG BAY khong thang: moi object boc mot huong lech vuong goc rieng (arcSpread), phinh to nhat
/// o giua duong va bang 0 o hai dau - dan object dang bay hop lai thanh mot chum HINH BAU DUC noi
/// than nan nhan voi mom. Doc theo duong do object TEO DAN, cham co 0 dung luc cham mom.
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
    [Range(0.01f, 1f)]
    [Tooltip("Co object = he so nay x than con NHO HON trong hai con (minh va nan nhan).\n" +
             "0.4 = 40% than con nho hon.\n\n" +
             "VI SAO LAY CON NHO HON chu khong phai lay rieng nan nhan: hat phai doc duoc o CA HAI\n" +
             "phia. Lay theo nan nhan thi con Lv1 hut mot con Lv500 se de ra nhung vien to gap may\n" +
             "lan chinh no, nhin nhu vien bi nuot nguoc. Lay con nho hon thi A(than 10) hut B(than\n" +
             "20) ra hat 4, ma B hut A cung ra 4 - cung mot cap dau thi cung mot co hat.")]
    public float victimSizeFactor = 0.4f;

    [Tooltip("BAT (mac dinh): mot NHIP RUT chi ra DUNG MOT object, du nhip do rut 1 hay 8 level -\n" +
             "vien to len theo so level no mang. Cuoi pha hut bac len cao thi day la khac biet lon:\n" +
             "1 object thay vi 8 cai bung ra cung frame.\n\n" +
             "TAT: quay ve luat cu 1 object = 1 level (dem object bay la ra so level dang chuyen,\n" +
             "doi lai late-game moi nhip bung ra ca nam.")]
    public bool mergeDrainFlyer = true;

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

    [Range(0f, 1f)]
    [Tooltip("DO PHINH cua duong bay, tinh theo PHAN TRAM QUANG DUONG BAY. 0 = bay thang.\n" +
             "0.25 = moi hat lech toi da 25% quang duong sang mot huong ngau nhien vuong goc voi\n" +
             "truc bay, phinh to nhat o giua duong va bang 0 o hai dau.\n\n" +
             "Hut lien tuc thi ca dan hat moi cai cong mot kieu -> nhin ra mot chum HINH BAU DUC\n" +
             "noi tu than nan nhan toi mom, thay vi mot vach thang.\n\n" +
             "Tinh theo % quang duong chu khong theo don vi world: tam hut o Lv1 chi ~1.5u ma Lv100\n" +
             "toi vai chuc u - dat so co dinh thi mot dau phinh loe loet, dau kia gan nhu thang.")]
    public float arcSpread = 0.25f;

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
        public Vector3 arc;       // vector lech VUONG GOC voi truc bay - do cong rieng cua hat nay
        public float size;        // co luc vua sinh; tu day teo dan ve 0 khi toi mom
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

        // GOP: mot nhip rut = MOT vien, mang ca cum level cua nhip do (xem mergeDrainFlyer).
        //
        // CO VIEN KHONG DOI theo so level no mang - moi vien luon bang nhau, du la 1 hay 60 level.
        // Da thu cho vien gop to len theo the tich va BO DI: cuoi tran dai bac rut leo len vai
        // chuc level/nhip, vien phinh ra thanh mot cuc to lan ngang man hinh, che mat ca hai con.
        // Co vien gio chi con phu thuoc DUNG MOT thu nhu bao lau nay: than con nho hon trong hai
        // con (xem CurrentSize).
        if (mergeDrainFlyer)
        {
            SpawnOne(fromWorld, victimColor, victimScale, levels);
            return;
        }

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

        _alive.Add(new Flyer
        {
            go = go, tf = tf, xp = xp, from = start, bornAt = Time.time,
            arc = RandomArc(start), size = s
        });
    }

    /// <summary>
    /// Co object = victimSizeFactor x than con NHO HON trong hai con.
    /// victimScale &lt;= 0 = ben goi khong biet co nan nhan -> chi do theo than minh.
    /// </summary>
    private float CurrentSize(float victimScale)
    {
        float body = _body != null ? _body.lossyScale.x : 1f;
        float smaller = victimScale > 0.0001f ? Mathf.Min(body, victimScale) : body;
        return Mathf.Max(0.001f, smaller * victimSizeFactor);
    }

    /// <summary>
    /// VECTOR LECH cua duong bay: mot huong ngau nhien VUONG GOC voi truc bay, do dai =
    /// arcSpread x quang duong. Chot MOT LAN luc sinh, khong tinh lai moi frame.
    ///
    /// Random.insideUnitCircle (dia dac) chu khong phai onUnitCircle (vanh): lay vanh thi moi hat
    /// deu lech dung mot khoang, ca dan hop lai thanh cai ONG rong ruot. Dia dac thi do lech trai
    /// deu tu 0 toi max, dan hat DAY o giua - do moi ra khoi bau duc.
    /// </summary>
    private Vector3 RandomArc(Vector3 start)
    {
        if (arcSpread <= 0.001f || _mouth == null) return Vector3.zero;

        Vector3 axis = _mouth.position - start;
        float dist = axis.magnitude;
        if (dist < 0.0001f) return Vector3.zero;
        axis /= dist;

        // Truc bay gan nhu thang dung thi Cross voi up ra vector 0 - doi sang right de lay duoc
        // mot phap tuyen that
        Vector3 u = Vector3.Cross(axis, Vector3.up);
        if (u.sqrMagnitude < 0.0001f) u = Vector3.Cross(axis, Vector3.right);
        u.Normalize();
        Vector3 v = Vector3.Cross(axis, u);   // axis va u deu la don vi + vuong goc -> v cung la don vi

        Vector2 p = Random.insideUnitCircle;
        return (u * p.x + v * p.y) * (dist * arcSpread);
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
            Vector3 pos = Vector3.Lerp(f.from, target, e);

            // PHINH RA GIUA DUONG. sin(pi*t) bang 0 o CA HAI DAU nen do cong khong an gian mot ly
            // nao: hat van roi ra dung than nan nhan va ve DUNG mom. Moi hat mot huong lech rieng
            // -> hut lien tuc thi ca dan hop lai thanh chum bau duc.
            if (f.arc.sqrMagnitude > 0.0001f) pos += f.arc * Mathf.Sin(Mathf.PI * t);
            f.tf.position = pos;

            // TEO DAN VE 0. Bam theo t (thoi gian) chu KHONG theo e (duong tang toc): bam theo e
            // voi flyAccel = 2 thi hat giu gan nguyen co suot 3/4 duong roi sup trong hai frame
            // cuoi, khong ai kip nhin thay no teo. Cham 0 dung luc cham mom.
            float sc = f.size * (1f - t);
            f.tf.localScale = new Vector3(sc, sc, sc);
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
