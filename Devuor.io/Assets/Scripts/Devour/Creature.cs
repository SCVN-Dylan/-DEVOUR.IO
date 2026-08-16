using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DANH TINH chung cho moi sinh vat trong van - nguoi choi va ca 3 con AI deu gan component nay.
///
/// Component nay chi lo phan DANH TINH cua mot con (la ai, tam than o dau, cap may). Con
/// "trong van dang co nhung ai" thi hoi GameManager - Creature tu dang ky vao do luc OnEnable
/// va tu rut ra luc OnDisable, khong ai phai di quet scene tim nhau.
/// </summary>
[RequireComponent(typeof(SimpleSuction))]
[DisallowMultipleComponent]
public class Creature : MonoBehaviour
{
    [Tooltip("Con nay do NGUOI CHOI dieu khien. Trong scene chi bat DUNG MOT con.\n" +
             "Dung de quyet dinh: ai duoc cong diem len HUD, camera bam ai, ai thua thi Game Over.")]
    public bool isPlayer;

    [Tooltip("Ten hien thi (dung chung voi PlayerNameTag khi can). Khong anh huong logic")]
    public string displayName = "Player";

    [Tooltip("CHI DUNG CHO AI - 'tinh cach' cua con bot nay: no nham toi level bang\n" +
             "level_nguoi_choi x (1 + bias).\n" +
             "  -0.25 = con nay chiu yeu hon nguoi choi 25%\n" +
             "  +0.20 = con nay manh hon nguoi choi 20%\n" +
             "GameManager boc so nay luc sinh bot, trai deu trong khoang cau hinh de luon co\n" +
             "ca con yeu lan con manh. Nguoi choi khong dung toi truong nay.")]
    public float levelBias;

    [Tooltip("TAM THAN (toa do local) - diem ma combat sau nay ngam vao khi hut/keo con khac.\n\n" +
             "KHONG lay tam bang bounds cua renderer nhu ben item: model nhan vat co animation,\n" +
             "bounds phinh/co theo tung frame nen tam se rung. Offset co dinh thi on dinh, va\n" +
             "TransformPoint tu nhan theo scale nen len cap la tam tu dang cao theo than.")]
    public Vector3 centerOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Tham chieu (keo vao Inspector)")]
    [Tooltip("Vung hut cua chinh con nay. Reset/them component la tu dien san, keo tay de doi")]
    [SerializeField] private SimpleSuction _suction;

    [Tooltip("Bo di chuyen cua chinh con nay")]
    [SerializeField] private RbMovement _movement;

    public SimpleSuction Suction { get { return _suction; } }
    public RbMovement Movement { get { return _movement; } }

    /// <summary>Cap do hien tai (lay tu SimpleSuction, khong luu ban sao rieng de khoi lech).</summary>
    public int Level { get { return _suction != null ? _suction.Level : 1; } }

    /// <summary>Tam than trong toa do world, da tinh ca scale khi len cap.</summary>
    public Vector3 Center { get { return transform.TransformPoint(centerOffset); } }

    /// <summary>Con vua hut minh gan day nhat. null = chua bi ai hut.</summary>
    public Creature LastAttacker { get { return _lastAttacker; } }

    /// <summary>Tong XP da bi rut trong lan bi hut NAY (reset khi thoat ra duoc mot luc).</summary>
    public int DrainedTotal { get { return _drainedTotal; } }

    /// <summary>
    /// Dang bi hut hay khong. Co do TRE (drainMemory) chu khong phai dung mot frame la tat:
    /// nan nhan chi mat XP nguyen o vai frame le te (phan le duoc tich luy), neu doc dung frame
    /// khong mat gi ma bao "thoat roi" thi co nay se nhap nhay lien tuc.
    /// </summary>
    public bool IsBeingDrained { get { return Time.time - _lastDrainTime <= drainMemory; } }

    [Tooltip("Bao lau khong bi hut them thi moi coi la DA THOAT (giay). Xem IsBeingDrained")]
    public float drainMemory = 0.5f;

    [Header("Te bao roi ra khi bi hut")]
    [Tooltip("Cach nhau it nhat bao nhieu giay moi nha 1 vien (0.05 = toi da 20 vien/giay).\n" +
             "Day la VAN AN TOAN: mat 1 XP la rot 1 vien, ma o level cao moi giay mat hang chuc XP -\n" +
             "khong chan nhip thi mot minh nan nhan co the de ra ca tram Rigidbody moi giay.")]
    public float cellInterval = 0.05f;

    [Tooltip("Do dai toi da cua HANG DOI te bao chua kip nha. Vuot qua thi vien nha ra se ganh\n" +
             "NHIEU HON 1 XP de don cho kip - choi binh thuong khong bao gio cham toi muc nay.\n" +
             "Khong co tran thi tran danh xong roi te bao van con rot ra ca phut sau.")]
    public int maxPendingCells = 60;

    [Header("Bi nuot")]
    [Range(0.01f, 0.99f)]
    [Tooltip("Tut xuong duoi bao nhieu PHAN TRAM so voi level luc BAT DAU bi hut thi bi nuot.\n" +
             "0.2 = vao tran o Lv100, con duoi Lv20 la bi nuot.")]
    public float eatPercent = 0.2f;

    [Tooltip("Thoat khoi moi vung hut bao lau (giay) thi MOC LEVEL duoc dat lai - coi nhu tran moi.\n\n" +
             "Tach rieng khoi drainMemory (chi 0.5s, dung cho animation/co trang thai): neu dung\n" +
             "chung thi chi can lach ra khoi non nua giay la 'thanh mau' day lai, danh nhau vo nghia.")]
    public float anchorResetDelay = 3f;

    [Range(0f, 2f)]
    [Tooltip("CHAM THAN nhau: phai hon bao nhieu phan tram level moi nuot duoc.\n" +
             "0 = chi can cao hon mot chut la nuot (Lv101 nuot Lv100).\n" +
             "0.1 = phai hon 10% moi nuot, hai con xap xi thi hue nhau.")]
    public float contactEatMargin = 0f;

    [Tooltip("Luc CHET thi no ra bao nhieu vien te bao, chia deu XP con lai.\n\n" +
             "Phai chot so vien co dinh chu khong theo '1 vien = 1 XP' nhu luc bi rut dan: con Lv800\n" +
             "chet ma de ra 799 Rigidbody trong mot frame la treo may.")]
    public int deathCellCount = 12;

    [Tooltip("Ban khi con nay bi nuot. Cam VFX/SFX vao day")]
    public UnityEvent onDied;

    /// <summary>Da bi nuot chua (chong xu ly chet hai lan trong cung mot frame).</summary>
    public bool IsDead { get { return _dead; } }

    /// <summary>Level luc BAT DAU bi hut - moc de tinh nguong bi nuot. 0 = khong o trong tran nao.</summary>
    public int AnchorLevel { get { return _anchorLevel; } }

    /// <summary>Duoi muc nay la bi nuot. 0 = khong o trong tran nao.</summary>
    public int EatThreshold
    {
        get
        {
            if (_anchorLevel <= 0) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(_anchorLevel * eatPercent));
        }
    }

    private Creature _lastAttacker;
    private float _lastDrainTime = -999f;
    private int _drainedTotal;
    private int _anchorLevel;
    private bool _dead;
    private int _pendingCells;
    private float _cellTimer;
    private CellPool _pool;

    /// <summary>
    /// BI CON KHAC HUT. Ke hut goi ham nay moi FixedUpdate khi minh nam trong non hut cua no.
    ///
    ///   xpAmount  = luong XP bi rut trong frame nay (da nhan fixedDeltaTime)
    ///   mouthPos  = mom ke hut, de biet keo ve huong nao
    ///   pullSpeed = van toc keo, CONG THEM vao van toc di chuyen chu khong thay the
    ///
    /// KHONG cong XP cho ke hut o day: so XP mat di se bien thanh TE BAO (M6) roi ke hut phai
    /// hap thu moi an duoc - cong thang o day nua la cong doi.
    /// </summary>
    public void ReceiveDrain(Creature attacker, float xpAmount, Vector3 mouthPos, float pullSpeed)
    {
        if (attacker == null || attacker == this || _suction == null || _dead) return;

        // Thoat duoc mot luc roi bi hut lai = tran moi, dem lai tu dau
        if (!IsBeingDrained) _drainedTotal = 0;

        // MOC LEVEL cua tran nay - phai chot TRUOC khi tru XP dau tien, va phai so voi
        // _lastDrainTime CU (truoc khi ghi de o duoi)
        if (_anchorLevel <= 0 || Time.time - _lastDrainTime > anchorResetDelay) _anchorLevel = Level;
        else if (Level > _anchorLevel) _anchorLevel = Level;   // an te bao len lai giua tran thi moc dang theo

        _lastAttacker = attacker;
        _lastDrainTime = Time.time;

        int lost = _suction.DrainXp(xpAmount);
        if (lost > 0)
        {
            _drainedTotal += lost;
            _pendingCells += lost;   // 1 XP mat di = 1 vien te bao se rot ra
        }

        if (_movement != null && pullSpeed > 0f)
        {
            Vector3 dir = mouthPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) _movement.AddExternalVelocity(dir.normalized * pullSpeed);
        }

        if (lost > 0 && EatThreshold > 0 && Level <= EatThreshold) Die(attacker);
    }

    /// <summary>
    /// CHAM THAN nhau: con THAP LEVEL hon chet.
    ///
    /// Moi con tu hoi "minh co phai dua thap hon khong" roi tu chet - doi xung nen khong phu
    /// thuoc con nao nhan va cham truoc, va khong bao gio xay ra canh ca hai cung chet.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (_dead) return;

        // Loc re truoc: dat va nha khong co Rigidbody. Item/te bao co Rigidbody nhung khong co
        // Creature -> chi ton dung mot GetComponent, khong phai GetComponentInParent.
        Rigidbody rb = collision.rigidbody;
        if (rb == null) return;

        Creature other = rb.GetComponent<Creature>();
        if (other == null || other == this || other.IsDead) return;

        if (other.Level > Level * (1f + contactEatMargin)) Die(other);
    }

    /// <summary>
    /// BI NUOT. XP con lai no thanh te bao - ke giet KHONG duoc cong thang XP, phai hut duoc thi
    /// moi an, va con thu ba van co cua chen vao cuop.
    /// </summary>
    public void Die(Creature killer)
    {
        if (_dead) return;
        _dead = true;

        int remain = _suction != null ? _suction.Xp : 0;
        if (remain > 0 && deathCellCount > 0)
        {
            if (_pool == null) _pool = CellPool.Instance;
            if (_pool != null) _pool.SpawnBurst(Center, remain, deathCellCount);
        }

        if (onDied != null) onDied.Invoke();

        if (GameManager.HasInstance) GameManager.Instance.ReportDeath(this, killer);
        else Destroy(gameObject);
    }

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake()
    {
        // Ref da keo san tren prefab thi khong dong toi. AutoFill chi la LUOI AN TOAN cho
        // truong hop quen keo (prefab cu, object dung tay trong scene test).
        if (_suction == null || _movement == null) AutoFill();
    }

    private void AutoFill()
    {
        if (_suction == null) _suction = GetComponent<SimpleSuction>();
        if (_movement == null) _movement = GetComponent<RbMovement>();
    }

    void Update()
    {
        EmitCells();
    }

    /// <summary>
    /// Nha te bao ra tu hang doi. XP mat di KHONG bay thang sang ke hut - no bien thanh vien te
    /// bao nam ngoai the gioi, ke hut phai hut duoc thi moi an duoc. Nho vay con thu ba luon co
    /// cua chen vao cuop.
    ///
    /// Binh thuong 1 vien = 1 XP. Chi khi rut nhanh hon nhip nha (hang doi vuot maxPendingCells)
    /// thi vien moi ganh nhieu XP hon - de hang doi khong dai vo tan.
    /// </summary>
    private void EmitCells()
    {
        if (_pendingCells <= 0) return;

        if (_pool == null) _pool = CellPool.Instance;
        if (_pool == null) { _pendingCells = 0; return; }   // scene khong co kho te bao: khong tich rac

        _cellTimer -= Time.deltaTime;
        if (_cellTimer > 0f) return;
        _cellTimer = Mathf.Max(0f, cellInterval);

        int xp = 1;
        if (maxPendingCells > 0 && _pendingCells > maxPendingCells)
            xp = Mathf.CeilToInt((float)_pendingCells / maxPendingCells);
        xp = Mathf.Min(xp, _pendingCells);

        Vector3 toward = _lastAttacker != null ? (_lastAttacker.Center - Center) : Vector3.zero;
        _pool.Spawn(Center, toward, xp);
        _pendingCells -= xp;
    }

    void OnEnable()
    {
        if (Application.isPlaying && GameManager.Instance != null) GameManager.Instance.Register(this);
    }

    void OnDisable()
    {
        // Doc thang _instance qua Instance: luc thoat Play mode GameManager co the bi huy truoc,
        // getter se di tao mot GameManager moi chi de rut ten ra khoi no - vo ich.
        if (Application.isPlaying && GameManager.HasInstance) GameManager.Instance.Unregister(this);
    }
}
