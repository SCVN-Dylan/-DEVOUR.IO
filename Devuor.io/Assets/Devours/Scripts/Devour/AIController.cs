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
        if (_creature == null) _creature = GetComponent<Creature>();
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

        PhysicsDevourable best = null;
        float bestSqr = float.MaxValue;
        int stage = _suction.Stage;

        for (int i = 0; i < n; i++)
        {
            if (_searchBuf[i] == null) continue;
            PhysicsDevourable it = _searchBuf[i].GetComponentInParent<PhysicsDevourable>();
            if (it == null || it.Consumed) continue;
            if (_suction.UseLevelGate && _suction.StageAtLevel(it.RequiredLevel) > stage) continue;

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

        Vector3 aim = _target != null ? _target.Center : _wanderPoint;

        Vector3 to = aim - transform.position;
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
        if (_escapeCone) { Gizmos.color = new Color(1f, 0.35f, 0f); t = _threat != null ? _threat.Center : _threatPos; }
        else if (_fleeTimer > 0f) { Gizmos.color = Color.yellow; t = _threat != null ? _threat.Center : _threatPos; }
        else if (_prey != null) { Gizmos.color = Color.magenta; t = _prey.Center; }
        else if (_target != null) { Gizmos.color = Color.red; t = _target.Center; }
        else { Gizmos.color = Color.cyan; t = _wanderPoint; }

        Gizmos.DrawLine(from, t);
        Gizmos.DrawWireSphere(t, 0.3f);
    }
}
