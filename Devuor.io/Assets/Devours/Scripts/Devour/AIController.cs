using UnityEngine;

/// <summary>
/// BOT: thay cho joystick cua nguoi choi. Chi lam mot viec - quyet dinh di huong nao roi day
/// vao RbMovement.SetDir, y het duong ma PlayerController day input vao. Nho vay bot chay bang
/// dung bo di chuyen cua nguoi choi, khong co duong rieng de lech hanh vi.
///
/// Ban M3 moi co hai viec: LANG THANG va DI AN ITEM. Chua danh nhau (de M8).
///
/// NHIP SUY NGHI: phan dat tien (quet item, do vat can) chay theo thinkInterval chu KHONG phai
/// moi frame; giua hai lan nghi thi bot cu bam theo huong da chon. Day la khac biet lon nhat ve
/// hieu nang so voi kieu "moi bot OverlapSphere moi frame".
///
/// Map chua bake NavMesh nen ne vat can bang 3 tia RAU (rays) chu khong dung dieu huong.
/// </summary>
[RequireComponent(typeof(RbMovement))]
[DisallowMultipleComponent]
public class AIController : MonoBehaviour
{
    [Header("Tham chieu (keo vao Inspector)")]
    [SerializeField] private RbMovement _movement;
    [SerializeField] private SimpleSuction _suction;
    [SerializeField] private Creature _creature;

    [Header("Nhip suy nghi")]
    [Tooltip("Giay giua 2 lan bot NGHI LAI (quet item + do vat can). Giua 2 lan thi cu bam huong cu.\n" +
             "Ha xuong = bot nhanh nhen hon nhung ton nhieu physics query hon. 0 = nghi moi frame (dung dat).")]
    public float thinkInterval = 0.35f;

    [Header("Tim moi")]
    [Tooltip("Ban kinh tim item = TAM HUT x so nay. Bot phai nhin xa hon tam hut thi moi co cai\n" +
             "de tien toi, khong thi no chi thay nhung mon da nam san trong mom")]
    public float searchRangeMul = 3f;

    [Tooltip("TRAN ban kinh tim moi (don vi world). Len cap cao tam hut rat dai, khong chan lai thi\n" +
             "moi lan nghi la mot cu quet ca goc map")]
    public float searchRangeMax = 25f;

    [Tooltip("So collider toi da doc moi lan quet tim moi. Day thi bo bot - bot khong can thay het,\n" +
             "chi can thay mot mon dang de an")]
    public int maxSearchHits = 48;

    [Header("Vung phat hien sinh vat")]
    [Tooltip("Ban kinh nhin thay sinh vat khac = TAM HUT x so nay. Vung nay la HINH TRON 360 do,\n" +
             "khac vung hut (hinh non phia truoc): thay o sau lung thi con biet ma chay")]
    public float detectRangeMul = 4f;

    [Tooltip("TRAN ban kinh phat hien (world), de o level cao bot khong 'nhin thay' ca map")]
    public float detectRangeMax = 30f;

    [Header("Di con yeu")]
    [Range(0.1f, 1f)]
    [Tooltip("Chi di con co level <= level minh x so nay (0.9 = doi thu phai yeu hon 10%).\n" +
             "Khoang giua nguong nay va nguong tron la vung NGANG CO - ke nhau, lo di an")]
    public float huntLevelRatio = 0.9f;

    [Tooltip("Di toi da bao nhieu giay. Het gio ma chua nuot duoc thi bo")]
    public float huntDuration = 5f;

    [Tooltip("Bo roi thi bao lau moi duoc ngam lai DUNG CON DO (giay).\n" +
             "Khong co khoang nguoi nay thi vua bo xong no ngam lai ngay con cu -> di vinh vien")]
    public float huntCooldown = 8f;

    [Header("Tron con manh")]
    [Tooltip("Con co level >= level minh x so nay thi bo chay (1.1 = manh hon 10% la chay)")]
    public float fleeLevelRatio = 1.1f;

    [Tooltip("Chay them bao lau sau khi de doa da khuat khoi vung phat hien (giay)")]
    public float fleeDuration = 3f;

    [Header("Di lang thang")]
    [Tooltip("Khong thay moi thi di loanh quanh trong ban kinh nay quanh diem xuat phat")]
    public float wanderRadius = 20f;

    [Tooltip("Bao lau thi doi diem lang thang khac (giay), du chua toi noi")]
    public float wanderRepickTime = 5f;

    [Tooltip("Toi gan diem den hon khoang nay thi coi nhu da toi, chon diem khac")]
    public float arriveDistance = 1.5f;

    [Header("Ne vat can")]
    [Tooltip("Do dai tia ra do vat can phia truoc (world). Nen dai hon ban kinh than mot chut")]
    public float whiskerLength = 3f;

    [Range(10f, 80f)]
    [Tooltip("Goc lech cua 2 tia rau hai ben so voi huong dang di")]
    public float whiskerAngle = 35f;

    [Tooltip("Layer tinh la VAT CAN. Vat co Rigidbody (item, sinh vat khac) tu dong KHONG tinh -\n" +
             "khong thi bot se ne chinh cai no dinh an")]
    public LayerMask obstacleLayers = ~0;

    [Header("Chong ket")]
    [Tooltip("Cu bao nhieu giay thi kiem tra xem co nhich duoc khong")]
    public float stuckCheckTime = 1.5f;

    [Tooltip("Trong khoang thoi gian tren ma di duoc it hon khoang nay thi coi la KET -> doi muc tieu")]
    public float stuckDistance = 0.4f;

    /// <summary>Bot dang lam gi - de doc trong Inspector / debug.</summary>
    public enum Mode { Wander, Item, Hunt, Flee }

    /// <summary>Item bot dang nham toi. null = khong nham item nao.</summary>
    public PhysicsDevourable Target { get { return _target; } }

    /// <summary>Con bot dang di. null = khong di ai.</summary>
    public Creature Prey { get { return _prey; } }

    /// <summary>Con bot dang tron. null = khong tron ai.</summary>
    public Creature Threat { get { return _threat; } }

    /// <summary>Trang thai hien tai.</summary>
    public Mode State
    {
        get
        {
            if (_fleeTimer > 0f) return Mode.Flee;
            if (_prey != null) return Mode.Hunt;
            if (_target != null) return Mode.Item;
            return Mode.Wander;
        }
    }

    private PhysicsDevourable _target;
    private Creature _prey;
    private Creature _threat;
    private Vector3 _threatPos;        // vi tri de doa lan cuoi thay - con chay tiep khi no da khuat
    private float _huntTimer;
    private float _fleeTimer;
    private Creature _gaveUpOn;        // con vua bo cuoc
    private float _gaveUpUntil;
    private Vector3 _home;
    private Vector3 _wanderPoint;
    private Vector3 _avoidDir;          // Vector3.zero = duong thong, khong phai ne
    private float _thinkTimer;
    private float _wanderTimer;
    private float _stuckTimer;
    private Vector3 _stuckLastPos;

    // Buffer dung CHUNG cho moi bot: cac bot deu nghi tren main thread va khong long nhau,
    // nen mot mang la du - khong can moi bot mot mang rieng.
    private static Collider[] _searchBuf;
    private static readonly RaycastHit[] _rayBuf = new RaycastHit[4];

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake()
    {
        AutoFill();
        _home = transform.position;
        _stuckLastPos = transform.position;
        PickWanderPoint();

        // Lech pha nhip nghi cua tung bot: khong co dong nay thi 3 bot sinh cung mot frame se
        // nghi cung mot frame mai mai - cu 0.35s lai co mot frame ganh ca 3 cu quet.
        _thinkTimer = Random.value * Mathf.Max(0.01f, thinkInterval);
    }

    private void AutoFill()
    {
        if (_movement == null) _movement = GetComponent<RbMovement>();
        if (_suction == null) _suction = GetComponent<SimpleSuction>();
        if (_creature == null) _creature = GetComponent<Creature>();
    }

    void Update()
    {
        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer <= 0f)
        {
            Think();
            _thinkTimer = Mathf.Max(0f, thinkInterval);
        }

        CheckStuck();
        _movement.SetDir(SteerDirection());
    }

    /// <summary>
    /// Phan DAT TIEN: nhin quanh roi quyet dinh lam gi. Chay theo thinkInterval.
    ///
    /// THU TU UU TIEN - song truoc, an sau:
    ///   1. TRON  con manh hon (bo het moi viec dang lam)
    ///   2. DI    con yeu hon, co han gio
    ///   3. AN    item gan nhat
    ///   4. LANG THANG
    /// </summary>
    private void Think()
    {
        int myLevel = _suction.Level;
        float detect = Mathf.Min(_suction.CurrentRange * detectRangeMul, detectRangeMax);

        // --- 1. TRON ---
        Creature threat = FindThreat(detect, myLevel);
        if (threat != null)
        {
            _threat = threat;
            _threatPos = threat.Center;
            _fleeTimer = fleeDuration;   // con thay no thi dong ho chay lai tu dau
        }
        else if (_fleeTimer > 0f)
        {
            _fleeTimer -= thinkInterval;
            if (_fleeTimer <= 0f) _threat = null;
        }

        if (_fleeTimer > 0f)
        {
            _prey = null;
            _target = null;
            SenseObstacles(DesiredDirection());
            return;
        }

        // --- 2. DI ---
        if (_prey != null && !CanHunt(_prey, myLevel, detect)) _prey = null;

        if (_prey != null)
        {
            _huntTimer -= thinkInterval;
            if (_huntTimer <= 0f)
            {
                _gaveUpOn = _prey;                          // bo cuoc: ghi so de khong ngam lai ngay
                _gaveUpUntil = Time.time + huntCooldown;
                _prey = null;
            }
        }

        if (_prey == null)
        {
            Creature p = FindPrey(detect, myLevel);
            if (p != null) { _prey = p; _huntTimer = huntDuration; }
        }

        if (_prey != null)
        {
            _target = null;
            SenseObstacles(DesiredDirection());
            return;
        }

        // --- 3. AN ITEM / 4. LANG THANG ---
        if (_target != null && (_target.Consumed || !_target.isActiveAndEnabled)) _target = null;
        _target = FindItem();

        if (_target == null)
        {
            _wanderTimer -= thinkInterval;
            Vector3 flat = _wanderPoint - transform.position;
            flat.y = 0f;
            if (_wanderTimer <= 0f || flat.sqrMagnitude < arriveDistance * arriveDistance) PickWanderPoint();
        }

        SenseObstacles(DesiredDirection());
    }

    /// <summary>
    /// Con MANH HON gan nhat trong vung phat hien. Duyet GameManager.Creatures chu khong
    /// OverlapSphere - danh sach chi co vai con, so khoang cach la xong, khong ton physics query.
    /// </summary>
    private Creature FindThreat(float detect, int myLevel)
    {
        if (!GameManager.HasInstance || _creature == null) return null;

        var all = GameManager.Instance.Creatures;
        Creature best = null;
        float bestSqr = detect * detect;

        for (int i = 0; i < all.Count; i++)
        {
            Creature c = all[i];
            if (c == null || c == _creature || c.IsDead) continue;
            if (c.Level < myLevel * fleeLevelRatio) continue;

            Vector3 d = c.Center - transform.position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = c;
        }
        return best;
    }

    /// <summary>Con YEU HON gan nhat trong vung phat hien, bo qua con vua bo cuoc.</summary>
    private Creature FindPrey(float detect, int myLevel)
    {
        if (!GameManager.HasInstance || _creature == null) return null;

        var all = GameManager.Instance.Creatures;
        Creature best = null;
        float bestSqr = detect * detect;

        for (int i = 0; i < all.Count; i++)
        {
            Creature c = all[i];
            if (c == null || c == _creature || c.IsDead) continue;
            if (c == _gaveUpOn && Time.time < _gaveUpUntil) continue;   // vua bo con nay, cho nguoi da
            if (c.Level > myLevel * huntLevelRatio) continue;

            Vector3 d = c.Center - transform.position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = c;
        }
        return best;
    }

    /// <summary>Con moi dang di con dang di duoc khong (con song, con yeu, con trong tam nhin).</summary>
    private bool CanHunt(Creature c, int myLevel, float detect)
    {
        if (c == null || c.IsDead || !c.isActiveAndEnabled) return false;
        if (c.Level > myLevel * huntLevelRatio) return false;   // no an len ngang co roi -> thoi

        Vector3 d = c.Center - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= detect * detect;
    }

    /// <summary>
    /// Item GAN NHAT ma bot AN DUOC. Dung dung luat hang cua SimpleSuction (StageAtLevel) chu
    /// khong so level tho - khong thi bot se hi hui duoi theo mot cai nha ma no khong nuot noi.
    /// </summary>
    private PhysicsDevourable FindItem()
    {
        float radius = Mathf.Min(_suction.CurrentRange * searchRangeMul, searchRangeMax);
        if (radius <= 0.01f) return null;

        int cap = Mathf.Max(8, maxSearchHits);
        if (_searchBuf == null || _searchBuf.Length < cap) _searchBuf = new Collider[cap];

        // KHONG tu noi buffer nhu ben SimpleSuction: o day day mang chi co nghia la "co qua nhieu
        // do an quanh day", bo bot vai mon khong sao - bot chi can MOT mon de tien toi.
        int n = Physics.OverlapSphereNonAlloc(transform.position, radius, _searchBuf,
            _suction.suckableLayers, QueryTriggerInteraction.Ignore);

        PhysicsDevourable best = null;
        float bestSqr = float.MaxValue;
        int stage = _suction.Stage;

        for (int i = 0; i < n; i++)
        {
            if (_searchBuf[i] == null) continue;
            PhysicsDevourable it = _searchBuf[i].GetComponentInParent<PhysicsDevourable>();
            if (it == null || it.Consumed) continue;
            if (_suction.useLevelGate && _suction.StageAtLevel(it.RequiredLevel) > stage) continue;

            Vector3 d = it.Center - transform.position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = it; }
        }
        return best;
    }

    /// <summary>
    /// Huong bot MUON di (chua tinh vat can).
    ///
    /// Luc TRON thi chay NGUOC huong de doa, va chay theo vi tri THAY LAN CUOI - de doa da khuat
    /// tam nhin roi van phai chay tiep chu khong dung lai quay dau.
    /// </summary>
    private Vector3 DesiredDirection()
    {
        if (_fleeTimer > 0f)
        {
            Vector3 away = transform.position - (_threat != null ? _threat.Center : _threatPos);
            away.y = 0f;
            return away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.zero;
        }

        Vector3 aim;
        if (_prey != null) aim = _prey.Center;
        else if (_target != null) aim = _target.Center;
        else aim = _wanderPoint;

        Vector3 to = aim - transform.position;
        to.y = 0f;
        return to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero;
    }

    /// <summary>Huong THUC SU di: huong muon di, hoac huong ne neu phia truoc bi chan.</summary>
    private Vector3 SteerDirection()
    {
        Vector3 desired = DesiredDirection();
        if (_avoidDir.sqrMagnitude > 0.0001f) return _avoidDir;
        return desired;
    }

    /// <summary>
    /// Ba tia RAU: thang, lech trai, lech phai. Thang bi chan thi re sang ben con trong.
    ///
    /// Vat co Rigidbody bi loai khoi danh sach vat can: item va sinh vat khac deu co Rigidbody,
    /// con nha cua/dia hinh thi khong - nho vay bot ne nha ma khong ne do an. Cach nay re hon
    /// nhieu so voi GetComponentInParent tren tung tia.
    /// </summary>
    private void SenseObstacles(Vector3 desired)
    {
        _avoidDir = Vector3.zero;
        if (desired.sqrMagnitude < 0.0001f) return;

        Vector3 origin = transform.position + Vector3.up * 0.3f;
        float len = whiskerLength;

        if (!Blocked(origin, desired, len)) return;

        Vector3 left = Quaternion.Euler(0f, -whiskerAngle, 0f) * desired;
        Vector3 right = Quaternion.Euler(0f, whiskerAngle, 0f) * desired;

        if (!Blocked(origin, left, len)) { _avoidDir = left; return; }
        if (!Blocked(origin, right, len)) { _avoidDir = right; return; }

        _avoidDir = -desired;   // bit ca ba huong: lui ra roi tinh sau
    }

    private bool Blocked(Vector3 origin, Vector3 dir, float len)
    {
        int n = Physics.RaycastNonAlloc(origin, dir, _rayBuf, len, obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (_rayBuf[i].rigidbody != null) continue;          // item / sinh vat -> khong phai vat can
            if (_rayBuf[i].collider == null) continue;
            if (_rayBuf[i].collider.transform.IsChildOf(transform)) continue;   // collider cua chinh minh
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ket vao goc tuong / chen nhau: mot lat khong nhich duoc thi bo muc tieu, chon cho khac.
    /// Khong co cai nay thi bot co the day mat vao tuong ca van.
    /// </summary>
    private void CheckStuck()
    {
        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < stuckCheckTime) return;

        Vector3 moved = transform.position - _stuckLastPos;
        moved.y = 0f;
        if (moved.magnitude < stuckDistance)
        {
            // Ket thi bo muc tieu hien tai - KE CA con moi dang di: rat co the dang uc dau vao
            // mot buc tuong nam giua minh va no
            _target = null;
            if (_prey != null) { _gaveUpOn = _prey; _gaveUpUntil = Time.time + huntCooldown; _prey = null; }
            PickWanderPoint();
            _avoidDir = Vector3.zero;
        }

        _stuckTimer = 0f;
        _stuckLastPos = transform.position;
    }

    private void PickWanderPoint()
    {
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        _wanderPoint = _home + new Vector3(r.x, 0f, r.y);
        _wanderTimer = wanderRepickTime;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 from = transform.position + Vector3.up * 0.3f;

        // Vung phat hien sinh vat (hinh tron 360 do)
        if (_suction != null)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Min(_suction.CurrentRange * detectRangeMul, detectRangeMax));
        }

        Vector3 t;
        if (_fleeTimer > 0f) { Gizmos.color = Color.yellow; t = _threat != null ? _threat.Center : _threatPos; }
        else if (_prey != null) { Gizmos.color = Color.magenta; t = _prey.Center; }
        else if (_target != null) { Gizmos.color = Color.red; t = _target.Center; }
        else { Gizmos.color = Color.cyan; t = _wanderPoint; }

        Gizmos.DrawLine(from, t);
        Gizmos.DrawWireSphere(t, 0.3f);
    }
}
