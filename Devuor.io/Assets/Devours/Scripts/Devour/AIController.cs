using UnityEngine;

/// <summary>
/// BOT: thay cho joystick cua nguoi choi. Chi lam mot viec - quyet dinh di huong nao roi day
/// vao RbMovement.SetDir, y het duong ma PlayerController day input vao. Nho vay bot chay bang
/// dung bo di chuyen cua nguoi choi, khong co duong rieng de lech hanh vi.
///
/// Bon viec: VUNG RA khi bi hut, TRON con manh, DI con yeu, AN item, LANG THANG - xem Think().
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
    [SerializeField] private Rigidbody _rb;

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

    [Header("Tinh cach - GameManager dat luc sinh, theo levelBias")]
    [Range(0.1f, 1.5f)]
    [Tooltip("Nguong SAN cua con NHAT nhat. 0.75 = doi thu phai yeu hon 25% no moi dam di.\n\n" +
             "VI SAO PHAI CHIA TINH CACH: mot bo nguong dung chung (0.9 / 1.1) de lai mot VUNG CHET -\n" +
             "doi thu trong khoang 0.9..1.1 lan level minh thi khong luat nao chay, hai con ngang co\n" +
             "lo nhau hoan toan. Ma levelBias lai CO TINH ghim level bot quanh level nguoi choi, nen\n" +
             "do la trang thai mac dinh chu khong phai ca hiem. Chia tinh cach thi vung chet cua tung\n" +
             "con LECH NHAU - khong bao gio ca map cung dung nhin nhau.\n\n" +
             "Bot dat tay trong scene (khong qua GameManager) nhan do hung hang 0.5 = dung so cu.")]
    public float huntRatioShy = 0.75f;

    [Range(0.1f, 1.5f)]
    [Tooltip("Nguong SAN cua con HUNG nhat. 1.05 = dam di ca con nhinh hon minh 5%")]
    public float huntRatioBold = 1.05f;

    [Range(0.5f, 2f)]
    [Tooltip("Nguong TRON cua con NHAT nhat. 1.0 = manh hon mot chut la chay")]
    public float fleeRatioShy = 1f;

    [Range(0.5f, 2f)]
    [Tooltip("Nguong TRON cua con HUNG nhat. 1.25 = phai manh hon 25% no moi chiu chay")]
    public float fleeRatioBold = 1.25f;

    [Header("Di con yeu")]
    [Range(0.2f, 1.5f)]
    [Tooltip("VAO TRONG bao nhieu phan TAM HUT thi thoi lao toi, chuyen sang VON quanh moi.\n" +
             "0.6 = vao trong 60% tam la bat dau von.\n\n" +
             "Ha xuong thi von sat hon, hut nhanh hon (he so hut tinh theo KHOANG CACH), nhung de\n" +
             "cham than nhau hon. Nang len thi an toan ma hut cham.")]
    public float holdRangeFactor = 0.6f;

    [Range(0f, 29f)]
    [Tooltip("Goc LECH khi von quanh moi (do). PHAI DUOI nua goc non hut (coneAngle/2, dang la 30)\n" +
             "- lech qua nua goc la moi roi ra khoi non, von ma khong hut duoc gi.\n\n" +
             "0 = lao thang vao moi nhu ban cu (huc dau, bi physics day ra, huc lai).")]
    public float orbitAngle = 25f;

    [Tooltip("Di toi da bao nhieu giay. Het gio ma chua nuot duoc thi bo")]
    public float huntDuration = 5f;

    [Tooltip("Bo roi thi bao lau moi duoc ngam lai DUNG CON DO (giay).\n" +
             "Khong co khoang nguoi nay thi vua bo xong no ngam lai ngay con cu -> di vinh vien")]
    public float huntCooldown = 8f;

    [Header("Vung ra khi bi hut")]
    [Tooltip("BAT: dang bi con khac HUT thi bo het moi viec va vung ra - KE CA khi ke hut yeu hon\n" +
             "minh (luat tron thuong chi kich hoat khi doi thu manh hon 10%).\n\n" +
             "TAT thi bot dung im cho hut toi chet neu ke hut khong du manh de kich hoat luat tron -\n" +
             "ma level bot lai duoc ghim bam quanh level nguoi choi, nen do la canh THUONG XUYEN.")]
    public bool escapeWhenDrained = true;

    [Range(0f, 1f)]
    [Tooltip("Do PHA khi bot chon duong LACH NGANG de ra khoi non.\n" +
             "0 = chay thang nguoc ke hut. 1 = chay vuong goc han voi truc mom no.\n" +
             "0.7 = chech ra: vua ra khoi goc vua xa dan, de ke hut xoay theo cung kho bat lai.\n\n" +
             "KHONG phai luc nao bot cung lach ngang: no do ca hai duong (lach toi vanh non / lui\n" +
             "ra khoi tam) roi chon duong ngan hon - xem EscapeConeDirection. So nay chi an khi\n" +
             "duong lach ngang thang.")]
    public float escapeLateral = 0.7f;

    [Header("Tron con manh")]
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

    [Tooltip("BAT: tam do LON THEO CO THAN va TOC DO, thay vi cu dinh o whiskerLength.\n\n" +
             "VI SAO CAN: whiskerLength la mot so CUNG, con than bot thi phinh tu 0.61u (Lv1) len\n" +
             "18.3u (Lv600). Ngay tai MOC Lv350 ban kinh than nhay len 3.52u - DAI HON ca tia rau\n" +
             "3u, tuc dau tia van con nam trong than bot. Ma Blocked() bo qua collider cua chinh\n" +
             "minh, nen tia luon bao 'thong': tu do tro di bot lai ma KHONG CO MAT, huc thang vao\n" +
             "tuong va day mai mot cho.\n\n" +
             "Tam do = ban kinh than + toc do x lookAheadTime + whiskerLength.\n" +
             "TAT = quay ve hanh vi cu y nguyen, tien de so sanh.")]
    public bool scaleWhiskerWithBody = true;

    [Min(0f)]
    [Tooltip("Nhin truoc bao nhieu GIAY duong di. 0.4 = thay vat can truoc 0.4 giay.\n\n" +
             "Tinh theo THOI GIAN chu khong theo met: bot chay 2.1 u/s luc Lv1 nhung 17.6 u/s cuoi\n" +
             "van, mot khoang cach co dinh se thanh vo nghia khi toc do gap 8 lan.")]
    public float lookAheadTime = 0.4f;

    [Range(0f, 1f)]
    [Tooltip("Do DAY cua chum do, tinh theo ban kinh than. 0 = ban mot tia manh nhu ban cu.\n\n" +
             "VI SAO CAN: bot rong toi 18u nhung do duong bang MOT DUONG THANG vo cung manh ke tu\n" +
             "tam. Goc tuong nam lech khoi dung duong do la khong thay - no di bang ca cai than ma\n" +
             "do bang mot diem. 0.6 = do bang hinh cau bang 60% ban kinh than.")]
    public float whiskerRadiusMul = 0.6f;

    [Range(10f, 80f)]
    [Tooltip("Goc lech cua 2 tia rau hai ben so voi huong dang di")]
    public float whiskerAngle = 35f;

    [Tooltip("Layer tinh la VAT CAN. Vat co Rigidbody (item, sinh vat khac) tu dong KHONG tinh -\n" +
             "khong thi bot se ne chinh cai no dinh an.\n\n" +
             "NGOAI TRU item qua to voi con nay: no se KHOA CUNG khi bi huc (xem\n" +
             "PhysicsDevourable.pushLockStageDiff), tuc la mot buc tuong that su - xem IsWallLikeItem.")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Bao lau QUET vat can mot lan (giay). TACH RIENG khoi thinkInterval vi hai viec nay khac\n" +
             "nhip nhau: nghi (quet item, chon moi) la viec dat va cham doi, con ne tuong phai NHANH.\n\n" +
             "Voi thinkInterval 0.35s va toc do ~6u/s thi bot di duoc ~2u trong mu - dai hon ca tia rau\n" +
             "(whiskerLength 3), nen no dam vao tuong roi moi biet.\n\n" +
             "Dang BI HUT thi bo qua nhip nay, quet moi frame - do la luc mot buoc di sai la mat may level.")]
    public float avoidInterval = 0.1f;

    [Tooltip("Sau khi cham vat can, NHO ben da re trong bao lau (giay).\n\n" +
             "Khong nho thi o goc tuong bot chon lai ben moi lan quet: trai - phai - trai, dung tai cho\n" +
             "ma khong thoat ra duoc. Nho ben roi thi no re dut khoat mot huong cho toi khi ra khoi goc.")]
    public float avoidCommitTime = 0.6f;

    [Header("Chong ket")]
    [Tooltip("Cu bao nhieu giay thi kiem tra xem co nhich duoc khong")]
    public float stuckCheckTime = 1.5f;

    [Tooltip("Trong khoang thoi gian tren ma di duoc it hon khoang nay thi coi la KET -> doi muc tieu")]
    public float stuckDistance = 0.4f;

    /// <summary>Bot dang lam gi - de doc trong Inspector / debug.</summary>
    public enum Mode { Wander, Item, Hunt, Flee, Escape }

    /// <summary>Item bot dang nham toi. null = khong nham item nao.</summary>
    public PhysicsDevourable Target { get { return _target; } }

    /// <summary>Con bot dang di. null = khong di ai.</summary>
    public Creature Prey { get { return _prey; } }

    /// <summary>Con bot dang tron. null = khong tron ai.</summary>
    public Creature Threat { get { return _threat; } }

    /// <summary>Do HUNG HANG 0..1 (0 = nhat nhat, 1 = hung nhat). 0.5 = bot dat tay trong scene.</summary>
    public float Aggression { get { return _aggression; } }

    /// <summary>Nguong SAN dang dung (da tinh theo tinh cach).</summary>
    public float HuntRatio { get { return _huntRatio; } }

    /// <summary>Nguong TRON dang dung (da tinh theo tinh cach).</summary>
    public float FleeRatio { get { return _fleeRatio; } }

    /// <summary>
    /// GameManager goi NGAY SAU khi sinh bot, truyen do hung hang suy tu levelBias cua chinh con do.
    /// Khong goi thi bot giu 0.5 - dung bang bo nguong cu (0.9 / 1.125).
    /// </summary>
    public void SetAggression(float t01)
    {
        _aggression = Mathf.Clamp01(t01);
        ApplyAggression();
    }

    /// <summary>Trang thai hien tai.</summary>
    public Mode State
    {
        get
        {
            if (_escapeCone) return Mode.Escape;
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
    private bool _escapeCone;          // dang vung ra khoi NON HUT (khac tron thuong: chay lech ngang)
    private float _aggression = 0.5f;
    private float _huntRatio = 0.9f;
    private float _fleeRatio = 1.1f;
    private int _orbitSide;            // von ben nao: +1 / -1. 0 = chua vao tam, chua chon
    private float _huntTimer;
    private float _fleeTimer;
    private Creature _gaveUpOn;        // con vua bo cuoc
    private float _gaveUpUntil;
    private Vector3 _home;
    private Vector3 _wanderPoint;
    private Vector3 _avoidDir;          // Vector3.zero = duong thong, khong phai ne
    private int _avoidSide;             // ben dang re: -1 trai, +1 phai. 0 = chua chot ben nao
    private float _avoidSideUntil;      // giu ben da chot toi gio nay
    private float _avoidTimer;          // nhip quet vat can, chay rieng voi _thinkTimer
    private Vector3 _hitNormal;         // phap tuyen cua vat vua chan duong - de chon ben re
    private Vector3 _unstickDir;        // huong THOAT KET, de cao hon moi huong khac. zero = khong ket
    private float _unstickUntil;        // giu huong thoat ket toi gio nay
    private float _thinkTimer;
    private float _wanderTimer;
    private float _stuckTimer;
    private Vector3 _stuckLastPos;

    // Buffer dung CHUNG cho moi bot: cac bot deu nghi tren main thread va khong long nhau,
    // nen mot mang la du - khong can moi bot mot mang rieng.
    private static Collider[] _searchBuf;
    // 8 cho chu khong phai 4: RaycastNonAlloc KHONG sap theo khoang cach, day buffer la no bo phan
    // con lai tuy y. Map nay day item (320 mon Lv1), ma item an duoc thi bi loc ra khong tinh la vat
    // can - 4 cho rat de bi may mon do chiem het truoc khi den luot buc tuong that su.
    private static readonly RaycastHit[] _rayBuf = new RaycastHit[8];
    private Collider _body;             // collider than - de biet bot dang to co nao
    private static readonly Collider[] _overlapBuf = new Collider[8];

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake()
    {
        AutoFill();
        ApplyAggression();   // bot dat tay trong scene: 0.5 = bo nguong cu
        _home = transform.position;
        _stuckLastPos = transform.position;
        PickWanderPoint();

        // Lech pha nhip nghi cua tung bot: khong co dong nay thi 3 bot sinh cung mot frame se
        // nghi cung mot frame mai mai - cu 0.35s lai co mot frame ganh ca 3 cu quet.
        _thinkTimer = Random.value * Mathf.Max(0.01f, thinkInterval);
    }

    private void ApplyAggression()
    {
        _huntRatio = Mathf.Lerp(huntRatioShy, huntRatioBold, _aggression);
        _fleeRatio = Mathf.Lerp(fleeRatioShy, fleeRatioBold, _aggression);
    }

    private void AutoFill()
    {
        if (_movement == null) _movement = GetComponent<RbMovement>();
        if (_suction == null) _suction = GetComponent<SimpleSuction>();
        if (_body == null) _body = GetComponent<Collider>();
        if (_creature == null) _creature = GetComponent<Creature>();
        if (_rb == null) _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // BI HUT thi KHONG doi het nhip nghi. Nhip rut la 0.1 giay/level, ma nhip nghi 0.35 giay -
        // cho het mot nhip nghi la mat toi 3 level truoc khi bot kip nhan ra co chuyen gi. Hai phep
        // doc bool moi frame, khong ton gi so voi mot cu quet.
        if (escapeWhenDrained && !_escapeCone && _creature != null
            && _creature.IsBeingDrained && _creature.IsVictimRole) _thinkTimer = 0f;

        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer <= 0f)
        {
            Think();                                      // Think() tu goi SenseObstacles o cuoi moi nhanh
            _thinkTimer = Mathf.Max(0f, thinkInterval);
            _avoidTimer = Mathf.Max(0f, avoidInterval);    // vua quet xong, khoi quet lai ngay trong frame nay
        }

        // QUET VAT CAN theo nhip RIENG, day hon nhip nghi - xem avoidInterval.
        // Dang bi hut thi ep quet moi frame: mot buoc di sai luc do la mat may level.
        if (_escapeCone) _avoidTimer = 0f;

        _avoidTimer -= Time.deltaTime;
        if (_avoidTimer <= 0f)
        {
            SenseObstacles(DesiredDirection());
            _avoidTimer = Mathf.Max(0f, avoidInterval);
        }

        CheckStuck();
        _movement.SetDir(SteerDirection());
    }

    /// <summary>
    /// Phan DAT TIEN: nhin quanh roi quyet dinh lam gi. Chay theo thinkInterval.
    ///
    /// THU TU UU TIEN - song truoc, an sau:
    ///   0. VUNG RA khi DANG BI HUT (bat ke ke hut manh hay yeu)
    ///   1. TRON  con manh hon (bo het moi viec dang lam)
    ///   2. DI    con yeu hon, co han gio
    ///   3. AN    item gan nhat
    ///   4. LANG THANG
    /// </summary>
    private void Think()
    {
        int myLevel = _suction.Level;
        float detect = Mathf.Min(_suction.CurrentRange * detectRangeMul, detectRangeMax);

        // --- 0. VUNG RA KHOI NON ---
        // Phai dung TRUOC va CHEN QUA luat tron thuong. Luat tron chi kich hoat khi doi thu manh
        // hon 10%, ma ke dang hut mom vao minh thi khong nhat thiet manh hon - khong co nhanh nay
        // thi bot dung yen an du bao nhieu level cung khong buon nhuc nhich.
        //
        // Cung khong de FindThreat ghi de: con manh nhat gan day chua chac la con dang ngam minh.
        Creature drainer = escapeWhenDrained ? DrainAttacker() : null;
        _escapeCone = drainer != null;

        // --- 1. TRON ---
        if (drainer != null)
        {
            _threat = drainer;
            _threatPos = drainer.Center;
            _fleeTimer = fleeDuration;
        }
        else
        {
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
            if (p != null) { _prey = p; _huntTimer = huntDuration; _orbitSide = 0; }
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
    /// KE DANG HUT MINH, neu minh dang o cua duoi. null = khong bi hut, hoac minh moi la ben tren.
    ///
    /// Doc IsVictimRole chu khong chi doc IsBeingDrained: hai con chia mom vao nhau thi CA HAI deu
    /// dang bi hut, con to van la ben an duoc - no ma bo chay thi khong bao gio ket thuc duoc pha
    /// nao ca.
    /// </summary>
    private Creature DrainAttacker()
    {
        if (_creature == null || !_creature.IsBeingDrained || !_creature.IsVictimRole) return null;

        Creature a = _creature.LastAttacker;
        if (a == null || a.IsDead || !a.isActiveAndEnabled) return null;
        return a;
    }

    /// <summary>
    /// HUONG VUNG RA khoi non hut - khac han huong tron thuong.
    ///
    /// Non hut sau hang chuc don vi nhung chi rong 60 do, nen chay THANG NGUOC la chay doc theo
    /// truc no: quang duong dai nhat co the, va van nam trong goc suot ca doan. Lach VUONG GOC voi
    /// truc mom thi chi phai di 0.58 lan khoang cach toi mom la thoat khoi goc.
    ///
    /// NHUNG KHONG PHAI LUC NAO LACH NGANG CUNG NGAN NHAT: non XOE RA theo do sau. Sat mom thi no
    /// hep, lach vai gang tay la ra; o cuoi tam thi no da rong ca met, luc do LUI THANG ra khoi
    /// TAM lai gan hon nhieu. So do that voi non 60 do sau 3.8u: dung o 20% tam thi lech ngang can
    /// 0.44u con lui ra can 3.05u; dung o 80% tam thi nguoc lai - lech ngang 1.75u, lui ra 0.78u.
    /// Nen o day DO CA HAI DUONG roi chon duong ngan hon.
    ///
    /// escapeLateral chi con la do PHA khi da chon duong lach ngang: pha them chut lui ra de ke
    /// hut xoay theo cung khong bat lai duoc ngay.
    /// </summary>
    private Vector3 EscapeConeDirection(Creature attacker)
    {
        if (attacker == null) return Vector3.zero;

        SimpleSuction sk = attacker.Suction;
        Transform mouth = sk != null && sk.mouth != null ? sk.mouth : attacker.transform;

        Vector3 axis = mouth.forward;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f) return Vector3.zero;
        axis.Normalize();

        Vector3 to = transform.position - mouth.position;
        to.y = 0f;

        float along = Vector3.Dot(to, axis);        // dang o do sau nao trong non
        Vector3 perp = to - axis * along;
        float side = perp.magnitude;                // da lech khoi truc bao nhieu

        // Dung DUNG tren truc non (bi ngam thang mat): khong ben nao gan hon, chon bua mot ben
        if (side < 0.0001f) { perp = Vector3.Cross(axis, Vector3.up); side = 0f; }
        else perp /= side;

        Vector3 away = to.sqrMagnitude > 0.0001f ? to.normalized : -axis;
        if (sk == null) return Vector3.Lerp(away, perp, Mathf.Clamp01(escapeLateral)).normalized;

        // Con bao nhieu nua thi ra khoi non, do theo CA HAI duong
        float half = Mathf.Deg2Rad * sk.coneAngle * 0.5f;
        float lateralLeft = Mathf.Max(0f, along * Mathf.Tan(half) - side);   // toi vanh non
        float backLeft = Mathf.Max(0f, sk.CurrentRange - to.magnitude);      // toi cuoi tam

        if (lateralLeft > backLeft) return away;

        Vector3 dir = Vector3.Lerp(away, perp, Mathf.Clamp01(escapeLateral));
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : perp;
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
            if (c.Level < myLevel * _fleeRatio) continue;

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
            if (c.Level > myLevel * _huntRatio) continue;

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
        if (c.Level > myLevel * _huntRatio) return false;   // no an len ngang co roi -> thoi

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

        PhysicsDevourable best = null;      // NGOAI non - mieng ke tiep de di toi
        float bestSqr = float.MaxValue;
        PhysicsDevourable mine = null;      // TRONG non va con phan minh - an cho xong da
        float mineSqr = float.MaxValue;
        int stage = _suction.Stage;
        float coneSqr = _suction.CurrentRange * _suction.CurrentRange;

        for (int i = 0; i < n; i++)
        {
            if (_searchBuf[i] == null) continue;
            PhysicsDevourable it = _searchBuf[i].GetComponentInParent<PhysicsDevourable>();
            if (it == null || it.Consumed) continue;
            if (_suction.UseLevelGate && _suction.StageAtLevel(it.RequiredLevel) > stage) continue;

            Vector3 d = it.Center - transform.position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;

            if (sqr <= coneSqr)
            {
                // DA TRONG NON: khong con la thu de "di toi" nua, no tu bay vao mom.
                // Con phan minh (hoac chua ai giu) thi van giu lam target de ItemDirection biet
                // ma dung yen huong cho no bay vao. Con da vao tay con khac thi BO HAN - di toi
                // cung khong gianh duoc, chi ton mot cu ngoat dau.
                if ((it.Owner == _suction || it.Owner == null) && sqr < mineSqr) { mineSqr = sqr; mine = it; }
                continue;
            }

            if (sqr < bestSqr) { bestSqr = sqr; best = it; }
        }

        // AN CHO XONG MIENG DANG BAY TRUOC, roi moi ngam mieng ke tiep.
        //
        // Nguoc voi truc giac ("ngam luon mieng sau cho do phi thoi gian") nhung do la vi _turnSpeed
        // = 0: bot XOAY TUC THI. Ngoat sang mieng khac giua chung la giat ca cai non ra khoi mieng
        // dang bay -> no bi Release va rot xuong dat. Ma mieng dang bay chi can ~0.17 giay de toi
        // mom (1.12u tu dung yen, gia toc 78.5), trong khi nhip nghi la 0.35 giay - tuc ngoat som
        // se cat ngang khoang mot nua so lan. Cho 0.17 giay re hon nhieu so voi tha mot mieng.
        return mine != null ? mine : best;
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
            if (_escapeCone)
            {
                Vector3 esc = EscapeConeDirection(_threat);
                if (esc.sqrMagnitude > 0.0001f) return esc;
            }

            Vector3 away = transform.position - (_threat != null ? _threat.Center : _threatPos);
            away.y = 0f;
            return away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.zero;
        }

        if (_prey != null) return HuntDirection(_prey);
        if (_target != null) return ItemDirection(_target);

        Vector3 to = _wanderPoint - transform.position;
        to.y = 0f;
        return to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero;
    }

    /// <summary>
    /// HUONG DI SAN. Xa thi lao toi, vao trong tam roi thi VON QUANH moi chu khong uc thang vao.
    ///
    /// VI SAO KHONG LAO THANG: cham than nhau KHONG an duoc gi (eatOnBodyContact dang tat), chi
    /// bi physics day ra roi lao vao lai. Te hon nua la non hut chi rong 30 do moi ben - uc sat
    /// thi moi truot sang hong hoac ra sau lung, roi HAN khoi non. Bot huc ca ngay ma khong rut
    /// duoc mot level nao.
    ///
    /// VI SAO KHONG DUNG LAI CHO HUT: mom = huong di (RbMovement khong co kenh xoay rieng). Dat
    /// huong ve 0 la mom NGUNG QUAY THEO moi luon - no chay vong mot cai la ra khoi non.
    ///
    /// VON O GOC DUOI NUA GOC NON thi giai duoc ca hai: bot di vong quanh nen khong bao gio dam
    /// vao, ma moi van nam trong non vi 25 do < 30 do. He so hut tinh theo KHOANG CACH chu khong
    /// theo goc (xem SimpleSuction.DrainCreatures), nen von sat ma lech goc van hut du manh.
    ///
    /// Chon ben MOT LAN roi giu: tinh lai moi frame thi bot lac qua lac lai quanh moi.
    /// </summary>
    private Vector3 HuntDirection(Creature prey)
    {
        Vector3 to = prey.Center - transform.position;
        to.y = 0f;

        float dist = to.magnitude;
        if (dist < 0.0001f) return Vector3.zero;
        Vector3 dir = to / dist;

        float hold = _suction != null ? _suction.CurrentRange * holdRangeFactor : 0f;
        if (dist > hold || orbitAngle < 0.01f)
        {
            _orbitSide = 0;   // ra ngoai tam roi: lan sau vao lai duoc chon ben moi
            return dir;
        }

        if (_orbitSide == 0) _orbitSide = OrbitSide(dir);
        return Quaternion.Euler(0f, orbitAngle * _orbitSide, 0f) * dir;
    }

    /// <summary>
    /// Von ben nao: chon ben ma bot DANG huong toi san, de khong phai be lai gat mot phat khi vua
    /// vao tam. Cross(dir, huong_dang_di).y > 0 nghia la huong dang di nam ben +goc cua dir.
    /// </summary>
    private int OrbitSide(Vector3 dir)
    {
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return 1;
        return Vector3.Cross(dir, fwd).y >= 0f ? 1 : -1;
    }

    /// <summary>
    /// HUONG DI TOI ITEM. Da lot vao NON HUT roi thi GIU NGUYEN HUONG, khong lai theo no nua.
    ///
    /// VI SAO: trong non la item dang bi CHINH MINH giat ve mom o toi 50 u/s, trong khi bot chi di
    /// 3.7 u/s. No vot qua mom, van sang ben hoac ra sau lung, roi ra khoi non va bi tha. Bot van
    /// ngam no nen quay 180 do lai; quay xong non quet ngang qua no, hut lai, lap vo tan. Bot dang
    /// duoi theo dung cai thu ma luc hut cua no dang quang di - cho duoi duoi cua chinh no.
    ///
    /// SO DO THAT (bot Lv2, chay o timeScale 0.1 cho min):
    ///   - mot con quay 151 do trong 0.6 giay
    ///   - bam CUNG MOT mieng suot hon 4 giay ma khong an noi
    ///   - mau cuoi: no dang quay lung 179 do vao dung cai item no dang ngam
    ///   - moi bot o trang thai Wander thi yaw KHONG DOI mot do nao -> chi state Item moi quay
    ///
    /// Giu nguyen huong thi non dung yen tren item, va no chui gon vao mom trong ~0.17 giay.
    /// </summary>
    private Vector3 ItemDirection(PhysicsDevourable item)
    {
        Vector3 to = item.Center - transform.position;
        to.y = 0f;

        float range = _suction != null ? _suction.CurrentRange : 0f;
        if (range > 0.01f && to.sqrMagnitude <= range * range)
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f) return fwd.normalized;
        }

        return to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero;
    }

    /// <summary>
    /// Huong THUC SU di. Uu tien tu cao xuong thap:
    ///   1. THOAT KET - dang nam trong long mot vat, phai ra khoi da
    ///   2. NE VAT CAN - phia truoc bi chan
    ///   3. Huong muon di
    ///
    /// Thoat ket phai dung TREN ne vat can: luc ket trong long collider thi tia rau bao TRONG o moi
    /// huong (Unity khong tinh la trung khi tia xuat phat tu ben trong), nen _avoidDir luon bang 0 -
    /// de no quyet dinh thi bot cu di thang vao tuong vo hinh mai.
    /// </summary>
    private Vector3 SteerDirection()
    {
        if (_unstickDir.sqrMagnitude > 0.0001f)
        {
            if (Time.time < _unstickUntil) return _unstickDir;
            _unstickDir = Vector3.zero;
        }

        Vector3 desired = DesiredDirection();
        if (_avoidDir.sqrMagnitude > 0.0001f) return _avoidDir;
        return desired;
    }

    /// <summary>
    /// TIA RAU: ban thang truoc mat, bi chan thi RE MOT GOC roi di thang - khong bam dinh mat tuong.
    ///
    /// Quat cac goc tu HEP toi RONG (1x, 2x, 3x whiskerAngle) va thu BEN DA CHOT truoc. Ban cu chi
    /// thu dung +-whiskerAngle roi bo cuoc: tuong cheo hay goc tuong la ca hai ben deu dinh, roi roi
    /// vao nhanh LUI - lui ra xong nhip sau lai nham dung muc tieu cu va dam lai y het. Do la cai
    /// vong "quay qua dam vao tuong lien tuc".
    ///
    /// Goc re la BOI SO cua whiskerAngle chu khong phai so moi: chinh mot cho van doi ca quat.
    ///
    /// Vat co Rigidbody bi loai khoi danh sach vat can (item, sinh vat khac deu co Rigidbody, nha cua
    /// thi khong) - TRU item qua to voi con nay, xem IsWallLikeItem.
    /// </summary>
    private void SenseObstacles(Vector3 desired)
    {
        _avoidDir = Vector3.zero;

        // Het han nho ben: quen di de lan ket sau duoc chon lai tu dau theo dia hinh moi
        if (_avoidSide != 0 && Time.time > _avoidSideUntil) _avoidSide = 0;

        if (desired.sqrMagnitude < 0.0001f) return;

        Vector3 origin = SenseOrigin();
        float len = SenseLength();

        if (!Blocked(origin, desired, len)) return;

        // Da cham vat can: chot mot ben (neu chua co) va gia han thoi gian nho ben do
        if (_avoidSide == 0) _avoidSide = PickAvoidSide(desired);
        _avoidSideUntil = Time.time + Mathf.Max(0f, avoidCommitTime);

        // Goc hep truoc (lech it nhat ma van di duoc), moi goc thu ben da chot truoc
        for (int step = 1; step <= 3; step++)
        {
            float angle = Mathf.Min(whiskerAngle * step, 150f);

            Vector3 a = Quaternion.Euler(0f, angle * _avoidSide, 0f) * desired;
            if (!Blocked(origin, a, len)) { _avoidDir = a; return; }

            Vector3 b = Quaternion.Euler(0f, -angle * _avoidSide, 0f) * desired;
            if (!Blocked(origin, b, len)) { _avoidDir = b; return; }
        }

        _avoidDir = LastResort(desired);
    }

    /// <summary>
    /// RE VE BEN NAO khi vua cham vat can. Chot mot lan roi giu - xem avoidCommitTime.
    ///
    /// Dang BI HUT: chon ben lam minh XA ke hut ra - do la ca muc dich cua pha nay.
    /// Binh thuong: re theo huong ma MAT TUONG dang quay ve (phap tuyen), tuc la truot ra phia
    /// thoang thay vi hup vao goc. Dam vuong goc that (phap tuyen nguoc han huong di) thi khong ben
    /// nao hon ben nao, boc bua mot ben.
    /// </summary>
    private int PickAvoidSide(Vector3 desired)
    {
        Vector3 right = Vector3.Cross(Vector3.up, desired);
        if (right.sqrMagnitude < 0.0001f) return Random.value < 0.5f ? -1 : 1;
        right.Normalize();

        if (_escapeCone) return AwaySide(right);

        float d = Vector3.Dot(right, _hitNormal);
        if (Mathf.Abs(d) < 0.05f) return Random.value < 0.5f ? -1 : 1;
        return d > 0f ? 1 : -1;
    }

    /// <summary>
    /// BIT MOI HUONG. Binh thuong thi lui ra roi tinh sau, y nhu cu.
    ///
    /// NHUNG dang BI HUT thi TUYET DOI khong lui: luc do desired la huong thoat khoi non, nen
    /// -desired la huong lao thang vao mom ke dang hut minh. Bot se ra khoi tuong roi lai bi hut
    /// vao, ra roi lai vao - dung cai canh "dam dau vao con hut roi quay qua dam vao tuong".
    ///
    /// Thay bang DI NGANG: bo vuong goc voi huong thoat, chon ben xa ke hut hon. Di ngang doc tuong
    /// thi it ra khoang cach toi mom con tang len, con lui la chac chan chet.
    /// </summary>
    private Vector3 LastResort(Vector3 desired)
    {
        if (!_escapeCone) return -desired;

        Vector3 right = Vector3.Cross(Vector3.up, desired);
        if (right.sqrMagnitude < 0.0001f) return -desired;
        right.Normalize();

        return right * AwaySide(right);
    }

    /// <summary>Ben nao cua truc <paramref name="right"/> la ben XA ke hut ra.</summary>
    private int AwaySide(Vector3 right)
    {
        Vector3 away = transform.position - (_threat != null ? _threat.Center : _threatPos);
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) return Random.value < 0.5f ? -1 : 1;

        return Vector3.Dot(right, away) >= 0f ? 1 : -1;
    }

    /// <summary>Ban kinh than THUC TE luc nay (world). Doc bounds nen tu dung o moi co than.</summary>
    private float BodyRadius()
    {
        if (_body == null) return 0f;
        Vector3 e = _body.bounds.extents;
        return Mathf.Max(e.x, e.z);
    }

    /// <summary>
    /// TAM DO thuc te. Xem tooltip cua scaleWhiskerWithBody ve ly do khong dung so cung.
    ///
    ///   Lv1   : 0.31 + 2.1x0.4  + 3 =  4.2u  (than 0.61u)
    ///   Lv350 : 3.52 + 12.2x0.4 + 3 = 11.4u  (than 7.05u)
    ///   Lv600 : 9.16 + 17.6x0.4 + 3 = 19.2u  (than 18.3u)
    /// </summary>
    private float SenseLength()
    {
        if (!scaleWhiskerWithBody) return whiskerLength;
        float spd = _movement != null ? Mathf.Max(0f, _movement.Speed) : 0f;
        return BodyRadius() + spd * Mathf.Max(0f, lookAheadTime) + whiskerLength;
    }

    /// <summary>
    /// Diem xuat phat cua chum do: TAM COLLIDER, khong phai pivot + 0.3u.
    /// O co than lon, 0.3u nam sat got chan - gan nhu duoi mat dat.
    /// </summary>
    private Vector3 SenseOrigin()
    {
        return _body != null ? _body.bounds.center : transform.position + Vector3.up * 0.3f;
    }

    private bool Blocked(Vector3 origin, Vector3 dir, float len)
    {
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir = dir.normalized;

        // SphereCast thay vi Raycast khi bot da to: do bang dung cai khoi minh dang lai.
        // Ban kinh 0 thi tu quay ve Raycast nhu ban cu, khong ton them gi.
        float r = scaleWhiskerWithBody ? BodyRadius() * whiskerRadiusMul : 0f;
        int n = r > 0.01f
            ? Physics.SphereCastNonAlloc(origin, r, dir, _rayBuf, len, obstacleLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, dir, _rayBuf, len, obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = _rayBuf[i].collider;
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue;    // collider cua chinh minh

            Rigidbody rb = _rayBuf[i].rigidbody;
            if (rb != null && !IsWallLikeItem(rb)) continue;      // item day duoc / sinh vat -> khong phai vat can

            _hitNormal = _rayBuf[i].normal;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Vat co Rigidbody nay co phai la BUC TUONG doi voi rieng con nay khong.
    ///
    /// Item hon minh tu pushLockStageDiff hang tro len se KHOA CUNG ngay khi bi huc vao
    /// (PhysicsDevourable), tuc no dung nghia la tuong - nhung vi no co Rigidbody nen tia rau van
    /// bo qua, va bot cu the ma huc mai. Day la cho sua.
    ///
    /// TINH LAI theo hang cua CHINH MINH chu khong doc co khoa cua item: co do duoc dat theo con
    /// VUA HUC vao gan nhat, mot con Lv1 huc phai toa nha la co bat len - doc co do thi con Lv500
    /// di ngang qua se ne chinh mon no an duoc.
    ///
    /// Chi ton mot GetComponent tren nhung tia trung vat CO Rigidbody, do duoc 0.091 us/lan.
    /// </summary>
    private bool IsWallLikeItem(Rigidbody rb)
    {
        if (_suction == null) return false;

        PhysicsDevourable it = rb.GetComponent<PhysicsDevourable>();
        if (it == null || it.Consumed) return false;      // sinh vat khac / vat vo danh -> khong ne
        if (it.pushLockStageDiff <= 0) return false;      // item nay khong bao gio khoa

        // Co khoa toan cuc dang TAT thi khong co gi khoa ca - day duoc het, khong phai ne
        if (GameManager.HasInstance && !GameManager.Instance.PushLockEnabled) return false;

        return _suction.StageAtLevel(it.RequiredLevel) - _suction.Stage >= it.pushLockStageDiff;
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

            // Doi muc tieu khong cuu duoc neu dang KET TRONG LONG mot vat - muc tieu moi cung nam
            // ben kia buc tuong do. Phai chu dong go ra.
            Unstick();
        }

        _stuckTimer = 0f;
        _stuckLastPos = transform.position;
    }

    /// <summary>
    /// GO KET. Hai canh khac han nhau nen cach xu ly cung khac:
    ///
    ///   1. LOT HAN vao trong long mot ITEM qua to  -> DAT THANG ra ngoai
    ///   2. Ty vao tuong / vat can tu ben ngoai      -> lai huong ra, di bang chan
    ///
    /// VI SAO ca 1 phai dat thang ra: do duoc roi - bot lot trong long toa nha van chon dung huong
    /// va van day, nhung chi nhich 0.083 u/s (vantoc lenh 1.28 bi solver day nguoc lai ~94%). No bi
    /// ep giua nen dat va day khoi hop, moi luc di ngang bi an gan het. Thoat 5.5u mat ~45 giay -
    /// gan nua van dau. Lai huong tu te den may cung khong cuu duoc canh nay.
    ///
    /// Dat thang ra CHI ap cho item (vat co Rigidbody), khong ap cho dia hinh tinh: item dung khoi
    /// hop nen phep thu "co nam trong khong" la chinh xac, con tuong/nha cua dung MeshCollider khong
    /// loi thi ClosestPoint tra ve chinh diem hoi - moi con di sat tuong deu bi cho la dang o trong.
    ///
    /// Nguoi choi khong thay cu dat nay: luc do bot dang khuat HAN trong long toa nha.
    ///
    /// Chi chay khi CheckStuck bao ket (1.5 giay mot lan, va chi khi that su khong nhich duoc).
    /// </summary>
    private void Unstick()
    {
        Vector3 center = transform.position + Vector3.up * 0.3f;
        float r = ProbeRadius;

        int n = Physics.OverlapSphereNonAlloc(center, r, _overlapBuf, obstacleLayers, QueryTriggerInteraction.Ignore);

        Vector3 push = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Collider c = _overlapBuf[i];
            if (c == null || c.transform.IsChildOf(transform)) continue;

            Bounds b = c.bounds;
            if (b.max.y <= transform.position.y + 0.05f) continue;   // mat dat duoi chan, khong phai cai giu minh

            Rigidbody rb = c.attachedRigidbody;
            if (rb != null && !IsWallLikeItem(rb)) continue;          // do an / sinh vat khac: khong phai vat can

            // Nam TRON trong long mot item: ClosestPoint tra ve chinh minh nghia la diem nam ben trong
            if (rb != null && b.Contains(center) && (c.ClosestPoint(center) - center).sqrMagnitude < 0.0001f)
            {
                TeleportOut(b, c.name);
                return;
            }

            Vector3 away = center - c.ClosestPoint(center);
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = NearestExitVector(b, center);
            if (away.sqrMagnitude < 0.0001f) continue;

            push += away.normalized;
        }

        push.y = 0f;
        _unstickDir = push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.zero;
        _unstickUntil = _unstickDir.sqrMagnitude > 0.0001f ? Time.time + stuckCheckTime : 0f;
    }

    /// <summary>
    /// Dat bot ra NGOAI khoi hop, theo mat gan nhat, cong them mot doan le cho khoi ty lai vao no.
    /// Giu nguyen do cao - chi dich tren mat phang ngang.
    /// </summary>
    private void TeleportOut(Bounds b, string what)
    {
        Vector3 p = transform.position;
        Vector3 v = NearestExitVector(b, p + Vector3.up * 0.3f);
        if (v.sqrMagnitude < 0.0001f) return;

        Vector3 target = p + v.normalized * (v.magnitude + ProbeRadius);
        target.y = p.y;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.position = target;
        }
        transform.position = target;

        _unstickDir = Vector3.zero;
        _unstickUntil = 0f;
        _stuckLastPos = target;

        // Canh nay le ra khong duoc xay ra (GameManager da chan cho sinh bi chiem). Con thay log nay
        // tuc la co duong khac lot vao trong ma minh chua biet - dung nuot im.
        Debug.LogWarning("[AIController] " + name + " ket trong long " + what + ", da dat ra " + target.ToString("0.0"), this);
    }

    /// <summary>
    /// Vector ra MAT GAN NHAT cua khoi hop tren mat phang ngang: HUONG la huong thoat, DO DAI la
    /// doan con phai di. Chay ra xa tam hop thi cung ra duoc, nhung dung gan giua mot toa nha 15u
    /// thi duong do dai gap ba lan.
    /// </summary>
    private static Vector3 NearestExitVector(Bounds b, Vector3 point)
    {
        float xMin = point.x - b.min.x;
        float xMax = b.max.x - point.x;
        float zMin = point.z - b.min.z;
        float zMax = b.max.z - point.z;

        float best = xMin;
        Vector3 dir = Vector3.left;
        if (xMax < best) { best = xMax; dir = Vector3.right; }
        if (zMin < best) { best = zMin; dir = Vector3.back; }
        if (zMax < best) { best = zMax; dir = Vector3.forward; }

        return dir * Mathf.Max(0f, best);
    }

    /// <summary>Ban kinh do quanh than khi go ket - du de tim cai dang om lay minh, khong quet ca khu.</summary>
    private float ProbeRadius { get { return Mathf.Max(0.5f, whiskerLength * 0.35f); } }

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
        if (_escapeCone) { Gizmos.color = new Color(1f, 0.35f, 0f); t = _threat != null ? _threat.Center : _threatPos; }
        else if (_fleeTimer > 0f) { Gizmos.color = Color.yellow; t = _threat != null ? _threat.Center : _threatPos; }
        else if (_prey != null) { Gizmos.color = Color.magenta; t = _prey.Center; }
        else if (_target != null) { Gizmos.color = Color.red; t = _target.Center; }
        else { Gizmos.color = Color.cyan; t = _wanderPoint; }

        Gizmos.DrawLine(from, t);
        Gizmos.DrawWireSphere(t, 0.3f);
    }
}
