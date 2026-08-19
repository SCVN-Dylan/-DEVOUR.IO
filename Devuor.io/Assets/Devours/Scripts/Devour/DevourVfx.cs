using UnityEngine;

/// <summary>
/// HIEU UNG HUT: nhung hat sang bay tu than nan nhan vao mom con dang hut.
///
/// VI SAO KHONG CON TE BAO VAT LY: ban cu moi vien la mot Rigidbody + collider + renderer roi ra
/// dat, va ke hut phai HUT LAI moi an duoc. Nhin thi dep nhung tra gia hai dau:
///   - dat: mot tran o level cao de ra hang tram rigidbody, dung cai loai chi phi giet fps mobile
///   - XP ROI RAI: do thuc te duoc 28/30 vien toi mom, 2 XP boc hoi vi vien van ra ngoai tam
///     roi het han. Nan nhan mat 30 ma ke hut chi an 28 - so lieu khong bao gio khop.
/// Gio XP di THANG tu nan nhan sang ke hut (xem Creature.ReceiveDrain), con class nay chi lo
/// phan NHIN. Khong physics, khong collider, khong GameObject nao duoc sinh ra.
///
/// HAT BAM THEO MOM chu khong bay theo duong thang chot san: moi LateUpdate ta keo ca mang hat
/// ve phia mom hien tai. Ban thang thi ke hut dang chay se thay hat ban truot ra sau lung.
///
/// Component nay nam tren con DI HUT (khong phai nan nhan): moi hat trong he deu bay ve dung mot
/// dich la mom cua chinh chu he. Nho vay khong phai nhet "dich den" vao tung hat - thu ma
/// ParticleSystem khong co cho de luu tu nhien.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class DevourVfx : MonoBehaviour
{
    [Header("Tham chieu (keo vao Inspector)")]
    [Tooltip("MOM - diem moi hat bay ve. De trong = tu tim object ten 'Mouth' o cha")]
    [SerializeField] private Transform _mouth;

    [Tooltip("De trong = lay ParticleSystem tren chinh object nay")]
    [SerializeField] private ParticleSystem _ps;

    [Tooltip("THAN con nay - chi dung de do CO, cho hat to theo khi len cap.\n" +
             "De trong = tu tim Creature o cha")]
    [SerializeField] private Transform _body;

    [Header("So hat")]
    [Tooltip("Moi LEVEL nan nhan bi rut thi bay ra bao nhieu hat.\n\n" +
             "Thuan MY THUAT: so hat KHONG dinh gi toi XP nua (XP da cong thang roi). Tang bao\n" +
             "nhieu tuy thich, khong lam lech can bang mot chut nao.")]
    public int particlesPerLevel = 6;

    [Tooltip("TRAN so hat cho MOT lan goi. O level cao mot frame co the mat vai chuc level -\n" +
             "khong chan thi mot frame de ra ca nghin hat.")]
    public int maxPerEmit = 24;

    [Tooltip("So hat bat ra khi mot con BI NUOT - mot phat that da")]
    public int deathParticles = 60;

    [Tooltip("TRAN so hat song cung luc trong he nay")]
    public int maxParticles = 400;

    [Header("Duong bay")]
    [Tooltip("Van toc hat luc con o XA mom (u/s)")]
    public float flySpeed = 9f;

    [Tooltip("Van toc luc SAT mom (u/s). De cao hon flySpeed thi cang gan cang lao nhanh -\n" +
             "dung cam giac bi hut, thay vi troi deu deu vao")]
    public float flySpeedNear = 26f;

    [Tooltip("Khoang cach de noi suy giua flySpeed va flySpeedNear (world).\n" +
             "Xa hon khoang nay thi bay o flySpeed, cham mom thi dat flySpeedNear")]
    public float speedRampDistance = 5f;

    [Tooltip("Gan mom hon khoang nay thi coi nhu da toi -> hat tat")]
    public float arriveDistance = 0.3f;

    [Tooltip("Do TAN ngau nhien quanh diem xuat phat (world). 0 = moi hat ra tu dung mot diem")]
    public float scatter = 0.6f;

    [Tooltip("Toi da bao lau mot hat duoc phep song (giay). Chi la luoi an toan - binh thuong hat\n" +
             "tu tat khi cham mom. Co no de hat khong ket lai vinh vien neu mom bien mat giua chung")]
    public float maxFlyTime = 3f;

    [Header("Mau hat")]
    [Tooltip("BAT: hat mang mau skin cua NAN NHAN (Creature.skinColor) - an con xanh thi thay hat\n" +
             "xanh bay vao mom, hut nhieu con cung luc thi phan biet duoc tung luong.\n" +
             "TAT: giu nguyen mau dat trong ParticleSystem, de art tu chinh")]
    public bool tintByVictim = true;

    [Header("Co hat")]
    [Tooltip("Co hat = CO THAN KE HUT x he so nay. 0.5 = bang nua than minh.\n" +
             "Khong con so tuyet doi nhu ban cu (size 0.25 x he so, tran maxSizeMul 4): tran do cat\n" +
             "tu khoang Lv100 nen moi con lon deu ra hat co 1.0 giong het nhau.")]
    [Range(0.01f, 2f)] public float bodySizeFactor = 0.5f;

    [Tooltip("BAT: kep co hat khong bao gio vuot qua CO NAN NHAN.\n\n" +
             "Khong co no thi con Lv500 an mot con Lv5 van tuon ra nhung cuc to bang nua than minh -\n" +
             "to hon ca con dang bi an. Kep lai thi luong hat noi dung mot su that: dang an con co nao.\n" +
             "TAT: hat luon bang bodySizeFactor x than minh, khong quan tam an ai.")]
    public bool clampToVictim = true;

    /// <summary>Co san sang ban khong (du mom + he hat).</summary>
    public bool IsReady { get { return _ps != null && _mouth != null; } }

    private ParticleSystem.Particle[] _buf;

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake()
    {
        AutoFill();
        ConfigureSystem();
    }

    /// <summary>
    /// LUOI AN TOAN cho ref bi quen keo. Ref da co san tren prefab thi khong ham nao o day dong toi.
    /// </summary>
    private void AutoFill()
    {
        if (_ps == null) _ps = GetComponent<ParticleSystem>();

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
    }

    /// <summary>
    /// EP nhung thiet lap ma logic o day PHU THUOC VAO, ngay trong code.
    ///
    /// Khong de mac cho Inspector: chinh nham mot o trong ParticleSystem (vd doi simulationSpace
    /// ve Local) la hat bay sai hoan toan ma khong bao loi gi - rat kho lan ra. Nhung thu thuoc
    /// ve MY THUAT (material, mau, hinh dang hat) thi van de nguyen cho art chinh thoai mai.
    /// </summary>
    private void ConfigureSystem()
    {
        if (_ps == null) return;

        ParticleSystem.MainModule main = _ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // ta tu dat vi tri world moi frame
        main.scalingMode = ParticleSystemScalingMode.Shape;           // co hat do code quyet, khong nhan theo scale cha
        main.playOnAwake = false;
        main.gravityModifier = 0f;                                    // hat bay vao mom, khong roi
        main.maxParticles = Mathf.Max(16, maxParticles);
        main.startLifetime = Mathf.Max(0.1f, maxFlyTime);

        // Ta tu goi Emit(), khong dung nhip tu dong cua module emission
        ParticleSystem.EmissionModule em = _ps.emission;
        em.rateOverTime = 0f;
        em.rateOverDistance = 0f;

        // Vi tri xuat phat do EmitParams quyet dinh, khong phai hinh dang shape
        ParticleSystem.ShapeModule shape = _ps.shape;
        shape.enabled = false;

        _ps.Play();   // "dang chay" thi Emit() moi vao duoc
    }

    /// <summary>
    /// Co hat hien tai: NUA than KE HUT, nhung khong bao gio to hon NAN NHAN.
    ///
    ///   co = than_ke_hut x bodySizeFactor,  roi kep xuong <= than_nan_nhan
    ///
    /// victimScale &lt;= 0 nghia la ben goi khong biet co nan nhan (vd overload cu) -> bo qua tran.
    /// </summary>
    private float CurrentSize(float victimScale)
    {
        float body = _body != null ? _body.lossyScale.x : 1f;
        float s = body * bodySizeFactor;
        if (clampToVictim && victimScale > 0.0001f) s = Mathf.Min(s, victimScale);
        return Mathf.Max(0.001f, s);
    }

    /// <summary>
    /// RUT: nan nhan vua tut 'levelsLost' cap -> rac ra mot nhum hat tu than no.
    /// Goi tren VFX cua KE HUT, truyen vao tam than + MAU SKIN + CO THAN cua NAN NHAN.
    /// </summary>
    public void EmitDrain(Vector3 fromWorld, int levelsLost, Color victimColor, float victimScale)
    {
        if (levelsLost <= 0) return;
        int n = Mathf.Clamp(levelsLost * particlesPerLevel, 1, Mathf.Max(1, maxPerEmit));
        Spawn(fromWorld, n, victimColor, victimScale);
    }

    /// <summary>CHET: mot phat that da, tat ca XP con lai da ve ke giet.</summary>
    public void EmitDeath(Vector3 fromWorld, Color victimColor, float victimScale)
    {
        Spawn(fromWorld, Mathf.Max(1, deathParticles), victimColor, victimScale);
    }

    /// <summary>
    /// Ban 'count' hat quanh mot diem. Ban TUNG hat mot chu khong Emit(params, count):
    /// mot lan goi voi count &gt; 1 se de moi hat vao DUNG MOT diem, nhin nhu mot cham duy nhat.
    /// </summary>
    private void Spawn(Vector3 center, int count, Color color, float victimScale)
    {
        if (!IsReady) return;

        float s = CurrentSize(victimScale);
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.applyShapeToPosition = false;
        ep.startLifetime = Mathf.Max(0.1f, maxFlyTime);
        ep.velocity = Vector3.zero;    // duong bay do LateUpdate lo, khong de physics cua PS xen vao

        // Chi ghi mau khi duoc bat: tat thi EmitParams khong mang startColor, hat lay mau cua
        // ParticleSystem nhu cu - art chinh tay van an
        if (tintByVictim) ep.startColor = color;

        for (int i = 0; i < count; i++)
        {
            ep.position = center + Random.insideUnitSphere * scatter;
            ep.startSize = s * Random.Range(0.75f, 1.25f);
            _ps.Emit(ep, 1);
        }
    }

    /// <summary>
    /// KEO ca mang hat ve phia mom, moi frame.
    ///
    /// Chay o LateUpdate chu khong Update: mom bam theo xuong nhan vat, ma xuong thi duoc
    /// Animator ghi o giai doan sau Update. Keo o Update la doc vi tri mom cua FRAME TRUOC,
    /// hat se lun nhun mot nhip khi nhan vat chay nhanh.
    ///
    /// Thoat som khi khong co hat nao - day la truong hop THUONG GAP nhat (khong danh nhau),
    /// va no re bang dung mot phep so sanh.
    /// </summary>
    void LateUpdate()
    {
        if (_ps == null || _mouth == null) return;

        int alive = _ps.particleCount;
        if (alive == 0) return;

        if (_buf == null || _buf.Length < alive)
            _buf = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(Mathf.Max(16, alive))];

        int n = _ps.GetParticles(_buf);
        Vector3 target = _mouth.position;
        float dt = Time.deltaTime;
        float ramp = Mathf.Max(0.01f, speedRampDistance);

        for (int i = 0; i < n; i++)
        {
            Vector3 p = _buf[i].position;
            Vector3 to = target - p;
            float d = to.magnitude;

            if (d <= arriveDistance)
            {
                _buf[i].remainingLifetime = 0f;   // toi mom = tat, khong cho bay xuyen qua roi vong lai
                continue;
            }

            // Cang gan mom cang nhanh: giong bi hut, khac han voi troi deu mot toc do
            float nearness = 1f - Mathf.Clamp01(d / ramp);
            float step = Mathf.Lerp(flySpeed, flySpeedNear, nearness) * dt;

            _buf[i].position = step >= d ? target : p + to * (step / d);
        }

        _ps.SetParticles(_buf, n);
    }
}
