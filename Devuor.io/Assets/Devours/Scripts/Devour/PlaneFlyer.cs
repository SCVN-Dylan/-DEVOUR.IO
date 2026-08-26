using UnityEngine;

/// <summary>
/// VAT BAY LANG THANG (khinh khi cau / khi cau / may bay): bay vong vong tren map theo duong LUON
/// ngau nhien, ai du HANG va du TAM HUT thi keo xuong an duoc nhu mot item binh thuong.
///
/// ------------------------------------------------------------------------------------------
/// CHIA QUYEN VOI PhysicsDevourable - day la diem cot loi cua ca file nay
/// ------------------------------------------------------------------------------------------
/// PhysicsDevourable la mot may trang thai DA SO HUU isKinematic/useGravity o BON cho
/// (EnterSleep / EnterSucked / EnterStruggle / Release). Chinh comment trong file do da canh bao
/// "isKinematic dang duoc BA cho khac bat/tat". Neu file nay cung thò tay vao hai co do moi frame
/// thi thanh cho thu NAM tranh nhau mot bien -> bug kieu "dang bi hut tu nhien roi bich xuong dat",
/// khong bao gio tim ra vi no phu thuoc thu tu goi trong mot frame.
///
/// Nen luat o day gon trong mot dong: CHI LAI KHI _item.Owner == null. Co chu roi thi dung yen,
/// nhuong het.
///
/// ------------------------------------------------------------------------------------------
/// TAI SAO PHAI CAT VA CHAM VAT LY KHI DANG BAY - bug "giat len giat xuong"
/// ------------------------------------------------------------------------------------------
/// Ghi chu "Ne vat can" ben duoi noi rang kinematic cham collider TINH thi Unity khong sinh su
/// kien. Dung, nhung TOA NHA TRONG GAME KHONG TINH: moi item deu la PhysicsDevourable voi rigidbody
/// DONG dang ngu. Kinematic dam vao dong-dang-ngu thi OnCollisionEnter CO ban.
///
/// Va do la ca mot day domino:
///   1. bay o y = 7.5, nha Lv_6 cao 10.3 - 13.4  -> bay xuyen qua nha
///   2. OnCollisionEnter -> PhysicsDevourable.Contact() -> _state: Asleep sang Falling
///   3. FixedUpdate cua no bat dau chay, sau sleepDelay (5s) va van toc ~ 0 thi goi EnterSleep()
///   4. EnterSleep() dat isKinematic = false, useGravity = true
///   5. tu giay do: trong luc keo XUONG, MovePosition o day keo LEN, moi buoc vat ly mot lan
///      -> GIAT LEN GIAT XUONG, vinh vien, vi Start() da chay xong tu doi nao
/// Kem theo: vat bay con XO DO NHA, vi kinematic day duoc vat dong.
///
/// Chua lieu duy nhat dung cho la KHONG DE CU VA CHAM DO XAY RA. Dung Rigidbody.excludeLayers:
/// collider VAN duoc OverlapSphere cua SimpleSuction quet thay (da do: excludeLayers khong dinh
/// gi toi scene query - Overlap/Raycast/SphereCast deu van trung), nhung khong con sinh tiep xuc
/// vat ly voi ai. Giu rieng layer Mouth de cham mom van an duoc nhu thuong.
///
/// Het bay (bi gianh) thi TRA LAI excludeLayers = 0 - luc do no la item thuong, phai nam duoc
/// tren dat chu khong roi xuyen qua.
///
/// Con mot luoi an toan nua: dang bay ma isKinematic bi ai do tat thi gianh lai ngay. Cai nay
/// KHONG pha luat o tren - no chi chay khi _flying, ma _flying doi hoi Owner == null, tuc la he
/// hut chua vao cuoc. Khong co canh nao hai ben cung ghi.
///
/// HUT HUT THI SAO: Release() cua PhysicsDevourable tu chuyen sang Falling (bat lai trong luc) nen
/// vat bay roi xuong dat, nam lai thanh item thuong, ai toi truoc an. KHONG co dong code nao o day
/// lo viec do - va cung vi the ma no khong bao gio bay len lai: da bi giành mot lan la nghi huu
/// (_flying = false, khong bao gio bat lai). Do la thiet ke da chot, khong phai thieu sot.
///
/// ------------------------------------------------------------------------------------------
/// DUONG BAY
/// ------------------------------------------------------------------------------------------
/// Random mot diem trong DIA map roi lai toi - y het PickWanderPoint cua AIController, dung lai
/// dung cong thuc do chu khong che kieu moi.
///
/// Khac mot cho: goc quay bi GIOI HAN bang turnRate. Chinh cai gioi han do bien duong GAP KHUC
/// thanh duong CONG - khong co no thi vat bay be gap 90 do tai moi waypoint, nhin nhu con ruoi
/// chu khong phai vat bay.
///
/// KHONG dung physics de di chuyen: MovePosition tren rigidbody KINEMATIC, khong luc, khong query
/// nao ngoai mot SphereCast moi probeInterval giay. Mot object nhu the ton gan nhu 0 tren mobile.
/// </summary>
[RequireComponent(typeof(PhysicsDevourable))]
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PlaneFlyer : MonoBehaviour
{
    [Header("Tham chieu (Reset tu dien)")]
    [SerializeField] private PhysicsDevourable _item;
    [SerializeField] private Rigidbody _rb;

    [Header("Vung bay")]
    [Tooltip("Tam map. De trong = Reset di tim 'Ground_Circle'; van trong thi lay goc toa do.")]
    [SerializeField] private Transform _mapCenter;

    [Tooltip("Ban kinh map (world). Reset tu do theo bounds cua Ground_Circle, y het MapBoundsBuilder.")]
    [SerializeField] private float _mapRadius = 35f;

    [Range(0.3f, 1f)]
    [Tooltip("Chi bay trong bao nhieu phan ban kinh map. 0.85 = chua toi tuong bao,\n" +
             "de khong bao gio thay canh vat bay dam vao khong khi o ria man hinh.")]
    [SerializeField] private float _radiusUse = 0.85f;

    [Header("Do cao")]
    [Tooltip("Do cao bay (world y).\n\n" +
             "SO NAY KHOA VOI requiredLevel cua item, dung doi mot minh no. Non hut toa NGANG, nua goc\n" +
             "30 do, nen do cao voi toi duoc chi bang 0.5 x tam hut + chieu cao cai mom. Do thuc te:\n" +
             "  Lv50  voi toi 3.9u   |  Lv90  voi toi 4.4u\n" +
             "  Lv100 voi toi 8.3u   |  Lv110 voi toi 8.4u   |  Lv250 voi toi 17.7u\n\n" +
             "Gate an di theo HANG chu khong theo level: hang E mo tu Lv100 (moc stage thu 5), o do\n" +
             "tam voi la 8.3u - DAY moi la tran that su, khong phai 8.4u cua Lv110. Lay 7.5 chu\n" +
             "khong lay 8.4: cong heightWobble (0.6) thi dinh song moi 8.1, van duoi tran. Dat sat tran\n" +
             "thi nua so vong bay vat nhap len NGOAI tam, nguoi choi chia mom dung cho ma khong hut\n" +
             "duoc - trong y het bug.\n\n" +
             "Ha do cao ma khong ha requiredLevel thi vat bay thanh mieng moi de an som hon du kien;\n" +
             "nang len ma khong nang hang thi no sang len 'an duoc' ma ca van khong ai toi - buc hon\n" +
             "la cam han.")]
    [SerializeField] private float _flyHeight = 7.5f;

    [Tooltip("Bien do nhap nho do cao (world). 0 = bay phang li.")]
    [SerializeField] private float _heightWobble = 0.6f;

    [Tooltip("Nhip nhap nho do cao. Thap = trôi cham, sang trong; cao = rung lac.")]
    [SerializeField] private float _wobbleSpeed = 0.15f;

    [Header("Bay")]
    [Tooltip("Toc do bay (u/s). Khinh khi cau ~4, truc thang ~7, phan luc ~18.")]
    [SerializeField] private float _speed = 4f;

    [Tooltip("TRAN goc quay (do/giay) - thu quyet dinh duong bay cong hay gap.\n\n" +
             "Ban kinh vong cua = speed / (turnRate x Deg2Rad). O 4 u/s va 40 do/s thi ban kinh cua\n" +
             "khoang 5.7u - vua dep tren map ban kinh 35. Ha turnRate xuong thi cua rong ra, co the\n" +
             "rong hon ca map neu ha qua tay.")]
    [SerializeField] private float _turnRate = 40f;

    [Tooltip("Bao lau doi diem den khac (giay), du chua toi noi.")]
    [SerializeField] private float _repickTime = 6f;

    [Tooltip("Vao gan diem den hon khoang nay (do phang, bo qua do cao) thi coi nhu toi noi.")]
    [SerializeField] private float _arriveDistance = 2f;

    [Header("Nghieng than khi cua")]
    [Tooltip("Goc nghieng toi da khi cua gat (do). 0 = tat han.")]
    [SerializeField] private float _bankAngle = 18f;

    [Tooltip("Toc do vao/ra khoi the nghieng (do/giay). Thap = nghieng mem, cao = giat.")]
    [SerializeField] private float _bankSpeed = 60f;

    [Header("Va cham khi bay")]
    [Tooltip("Nhung layer CON duoc cham vao vat bay trong luc no dang bay. Moi layer khac bi cat\n" +
             "bang Rigidbody.excludeLayers - xem 'TAI SAO PHAI CAT VA CHAM VAT LY' o dau file.\n\n" +
             "De TRONG (Nothing) = tu dung layer ten '" + MouthLayerName + "': cham mom van an duoc\n" +
             "nhu thuong, con lai khong ai cham noi nen khong con cai domino\n" +
             "Contact -> Falling -> EnterSleep -> bat lai trong luc.\n\n" +
             "Prefab cu chua co o nay se roi vao dung truong hop de trong o tren.")]
    [SerializeField] private LayerMask _touchWhileFlying = 0;

    /// <summary>Layer cua mom player. De trong o Inspector thi Start() tu tra ten nay ra chi so.</summary>
    private const string MouthLayerName = "Mouth";

    [Header("Ne vat can")]
    [Tooltip("BAT: thay vat can phia truoc thi lai gat sang ben trong cho toi khi qua duoc.\n\n" +
             "VI SAO CAN: vat bay khong con va cham vat ly (xem 'TAI SAO PHAI CAT VA CHAM' o dau\n" +
             "file) nen no XUYEN THANG qua toa nha chu khong dung lai. Ne la viec cua rieng cho nay.\n" +
             "Bay du cao khoi moi mai nha thi tat co nay di cho re.")]
    [SerializeField] private bool _avoidObstacles = true;

    [Tooltip("Layer duoc tinh la vat can. Bo layer cua chinh vat bay ra cho re.")]
    [SerializeField] private LayerMask _obstacleLayers = ~0;

    [Tooltip("BAT (nen bat): Start() TU DO be ngang / chieu dai than tu collider that, roi ep\n" +
             "probeRadius va probeDistance khong duoc nho hon so do.\n\n" +
             "VI SAO CAN: hai so duoi day phai khop voi TUNG con, ma dat tay thi sai la chac. So\n" +
             "authored dang la probeRadius = 1.5 cho ca 5 prefab, trong khi airship rong 33.7 va\n" +
             "khinh khi cau rong 18.7 - chum do phu chua toi 9% be ngang, nen hai canh quet qua\n" +
             "toa nha ma chum khong he thay gi.\n\n" +
             "probeDistance cung vay: phai du xa de CUA XONG truoc khi dam, tuc >= ban kinh vong\n" +
             "cua (speed / turnRate) cong nua chieu dai than. Airship can 18.3u, authored cho 8.")]
    [SerializeField] private bool _autoProbeSize = true;

    [Tooltip("Nhin xa bao nhieu (world). Bat autoProbeSize thi day chi con la muc SAN.")]
    [SerializeField] private float _probeDistance = 8f;

    [Tooltip("Be ngang chum do - nen bang nua be ngang than. Bat autoProbeSize thi day chi la muc SAN.")]
    [SerializeField] private float _probeRadius = 1.5f;

    [Tooltip("Giay giua 2 lan do vat can. KHONG do moi frame - mot toa nha khong tu moc len giua\n" +
             "hai lan do. 0.35 giay = ~3 SphereCast moi giay cho MOT object, coi nhu mien phi.")]
    [SerializeField] private float _probeInterval = 0.35f;

    [Header("Go")]
    [Tooltip("Ve duong bay va diem den trong Scene view khi chon object nay.")]
    [SerializeField] private bool _drawGizmos = true;

    // ------------------------------------------------------------------ trang thai chay

    private Vector3 _center;      // tam map, da lam phang y = 0
    private Vector3 _target;      // diem den hien tai, da lam phang
    private Vector3 _heading;     // huong bay, luon phang va da chuan hoa
    private float _repickTimer;
    private float _probeTimer;
    private float _noiseSeed;
    private float _bank;

    /// <summary>
    /// Huong dang lai de NE, hoac zero neu truoc mat trong. Con giu huong nay thi diem den bi gac
    /// lai - phai qua duoc cai da roi hang di dau thi di.
    /// </summary>
    private Vector3 _avoidDir;

    /// <summary>
    /// Day than so voi pivot (world y). Dung de biet vat can nao la "cao": dinh no thap hon day
    /// than thi bay qua duoc, khong viec gi phai ne - khong co no thi vat bay ne ca cai bui va ne
    /// ca mat dat.
    /// </summary>
    private float _bodyBottom;

    /// <summary>Chum do ban ra hai ben lech bao nhieu do. Hep qua thi ne khong du, rong qua thi cua gat.</summary>
    private const float SideProbeAngle = 45f;

    /// <summary>Chieu cao than - be day hanh lang do theo truc dung.</summary>
    private float _bodyHeight = 1f;

    /// <summary>Dung chung cho moi lan do, khong cap phat giua game.</summary>
    private readonly Collider[] _probeBuf = new Collider[24];

    /// <summary>
    /// Con dang tu lai khong. Mot khi bi giành (Owner != null) la tat VINH VIEN - roi la roi han,
    /// khong bao gio bay len lai. Xem phan "HUT HUT THI SAO" o dau file.
    /// </summary>
    private bool _flying = true;

    // ------------------------------------------------------------------ vong doi

    /// <summary>Keo ref san trong Editor de khoi GetComponent luc chay.</summary>
    private void Reset()
    {
        _item = GetComponent<PhysicsDevourable>();
        _rb = GetComponent<Rigidbody>();
        AutoFindMap();
    }

    private void Start()
    {
        // Luoi an toan: prefab dung tay quen keo ref thi van chay duoc.
        if (_item == null) _item = GetComponent<PhysicsDevourable>();
        if (_rb == null) _rb = GetComponent<Rigidbody>();

        // LAN DUY NHAT file nay ghi hai co physics.
        //
        // Phai o Start CHU KHONG PHAI Awake: PhysicsDevourable.Awake() goi EnterSleep(), trong do
        // dat isKinematic = false / useGravity = true / rb.Sleep(). Ghi o Awake thi thu tu hai
        // Awake la khong xac dinh - nua so lan chay se bi EnterSleep de len va vat bay roi ngay
        // frame dau. Start luon chay sau moi Awake nen khong co cua thua.
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.excludeLayers = ~TouchMask();   // cat va cham vat ly - xem ghi chu dau file
        }

        _center = Vector3.zero;
        if (_mapCenter != null)
        {
            Vector3 c = _mapCenter.position;
            _center = new Vector3(c.x, 0f, c.z);
        }

        // Moi con mot seed rieng, khong thi dat nhieu vat bay chung se nhap nho DONG PHA voi nhau.
        _noiseSeed = Random.value * 100f;

        Vector3 p = transform.position;
        p.y = _flyHeight;
        transform.position = p;

        _heading = transform.forward;
        _heading.y = 0f;
        if (_heading.sqrMagnitude < 0.0001f) _heading = Vector3.forward;
        _heading.Normalize();

        MeasureBody();
        PickTarget();
    }

    /// <summary>
    /// FixedUpdate + MovePosition chu khong phai Update + transform.position: rigidbody nay dang bat
    /// Interpolate, ma noi suy chi hieu duong MovePosition. Ghi thang transform thi noi suy va lenh
    /// ghi da nhau, nhin ra giat o may yeu.
    /// </summary>
    private void FixedUpdate()
    {
        // MOT DONG LUAT: co chu roi thi PhysicsDevourable dang lai - khong tranh, va nghi huu luon.
        if (_flying && _item != null && _item.Owner != null) Retire();
        if (!_flying) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        // LUOI AN TOAN: con bay ma co ai do tat kinematic (EnterSleep/Release cua PhysicsDevourable)
        // thi gianh lai NGAY - de nguyen mot buoc thoi la trong luc da kip cong van toc, va cai
        // van toc do se con o lai sau khi doi ve kinematic. Chi chay khi _flying (Owner == null)
        // nen khong bao gio danh nhau voi he hut.
        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        // --- doi diem den khi toi noi hoac het gio
        _repickTimer -= dt;
        Vector3 flat = _target - transform.position;
        flat.y = 0f;
        if (_repickTimer <= 0f || flat.sqrMagnitude < _arriveDistance * _arriveDistance) PickTarget();

        // --- do vat can (theo nhip, khong phai moi frame)
        if (_avoidObstacles)
        {
            _probeTimer -= dt;
            if (_probeTimer <= 0f)
            {
                _probeTimer = _probeInterval;
                UpdateAvoid();
            }
        }
        else _avoidDir = Vector3.zero;

        // --- lai: dang ne thi NE TRUOC, diem den de do. Qua duoc roi (_avoidDir ve zero) thi tu
        //     dong quay lai bam diem den o nhip do ke tiep.
        Vector3 dir = _avoidDir.sqrMagnitude > 0.0001f ? _avoidDir : (_target - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 prev = _heading;
            _heading = Vector3.RotateTowards(_heading, dir.normalized, _turnRate * Mathf.Deg2Rad * dt, 0f);
            _heading.y = 0f;
            if (_heading.sqrMagnitude < 0.0001f) _heading = prev;
            _heading.Normalize();

            // Do GAT cua cua = da quay het bao nhieu phan cua tran turnRate trong buoc nay.
            // Lay ti le chu khong lay goc tho: nho vay doi turnRate khong lam sai the nghieng.
            float turned = Vector3.SignedAngle(prev, _heading, Vector3.up);
            float full = _turnRate * dt;
            float t = full > 0.0001f ? Mathf.Clamp(turned / full, -1f, 1f) : 0f;
            _bank = Mathf.MoveTowards(_bank, -t * _bankAngle, _bankSpeed * dt);
        }
        else
        {
            _bank = Mathf.MoveTowards(_bank, 0f, _bankSpeed * dt);
        }

        // --- tien len
        Vector3 pos = transform.position + _heading * (_speed * dt);

        // LEO CUNG: du turnRate co thap den may, du diem den co ky quac the nao, khong bao gio ra
        // khoi map. Cham vanh la ep quay dau luon.
        Vector3 off = pos - _center;
        off.y = 0f;
        float max = Mathf.Max(0.1f, _mapRadius * _radiusUse);
        if (off.sqrMagnitude > max * max)
        {
            pos = _center + off.normalized * max;
            PickTarget();
        }

        pos.y = _flyHeight + (Mathf.PerlinNoise(_noiseSeed, Time.time * _wobbleSpeed) - 0.5f) * 2f * _heightWobble;

        _rb.MovePosition(pos);
        _rb.MoveRotation(Quaternion.LookRotation(_heading, Vector3.up) * Quaternion.Euler(0f, 0f, _bank));
    }

    // ------------------------------------------------------------------ phan viec nho

    /// <summary>
    /// Nghi huu: het bay han. TRA LAI va cham that - tu day no la item thuong, phai nam duoc tren
    /// dat va bi huc duoc nhu moi mon khac, chu khong roi xuyen qua moi thu.
    /// </summary>
    private void Retire()
    {
        _flying = false;
        if (_rb != null) _rb.excludeLayers = 0;
    }

    /// <summary>Layer con duoc cham. De trong o Inspector thi lay layer mom.</summary>
    private int TouchMask()
    {
        if (_touchWhileFlying.value != 0) return _touchWhileFlying.value;

        int mouth = LayerMask.NameToLayer(MouthLayerName);
        return mouth >= 0 ? (1 << mouth) : 0;
    }

    /// <summary>Bốc mot diem den moi trong dia map - dung cong thuc cua AIController.PickWanderPoint.</summary>
    private void PickTarget()
    {
        Vector2 r = Random.insideUnitCircle * (_mapRadius * _radiusUse);
        _target = _center + new Vector3(r.x, 0f, r.y);
        _repickTimer = _repickTime;
    }

    /// <summary>
    /// Do be ngang / chieu dai than that, roi ep hai so probe khong duoc nho hon no.
    ///
    /// Cach cu de designer tu dien probeRadius va ca 5 prefab deu de nguyen 1.5, trong khi airship
    /// rong 33.7 - chum do phu 9% be ngang nen hai canh quet qua toa nha ma no khong thay gi. Do
    /// tu collider thi khong con cho nao de dien sai.
    ///
    /// probeDistance phai du de CUA XONG truoc khi dam: ban kinh vong cua = speed / turnRate(rad),
    /// cong them nua chieu dai than vi cai duoi con phai qua duoc cho do nua.
    /// </summary>
    private void MeasureBody()
    {
        // PHAI SYNC TRUOC. Unity 6 de autoSyncTransforms = false, ma Start() vua doi transform len
        // flyHeight - collider.bounds luc nay van la vi tri CU ben PhysX. Doc thang thi bodyBottom
        // ra -10.50 thay vi 0, tuc chenh dung bang doan vua dich, va bo loc "cao hon than" se sai
        // ca met ma khong bao gi.
        Physics.SyncTransforms();

        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool has = false;

        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || cols[i].isTrigger) continue;
            if (!has) { b = cols[i].bounds; has = true; }
            else b.Encapsulate(cols[i].bounds);
        }
        if (!has) return;

        _bodyBottom = b.min.y - transform.position.y;
        _bodyHeight = Mathf.Max(1f, b.size.y);
        if (!_autoProbeSize) return;

        // Lay nua chieu NGANG LON NHAT chu khong phai be ngang theo huong bay: huong bay doi lien
        // tuc, chon so lon la chum do khong bao gio hep hon than o bat ky goc nao.
        float halfSpan = Mathf.Max(b.extents.x, b.extents.z);
        float turnRadius = _speed / Mathf.Max(0.0001f, _turnRate * Mathf.Deg2Rad);

        _probeRadius = Mathf.Max(_probeRadius, halfSpan);
        _probeDistance = Mathf.Max(_probeDistance, turnRadius + halfSpan);
    }

    /// <summary>
    /// Truoc mat trong thi thoi ne. Bi chan thi chon BEN THOANG HON va giu huong do cho toi khi qua
    /// duoc - khong bốc diem den ngau nhien nhu truoc.
    ///
    /// VI SAO KHONG BOC NGAU NHIEN: diem moi hoan toan co the nam dung sau cai toa nha vua chan,
    /// hoac sau mot cai khac cung chan. Vat bay do dung vao roi lai bốc, do lai dung vao, thanh ra
    /// no rung rinh truoc mat toa nha chu khong bao gio vong qua. Chon ben theo do THOANG thi moi
    /// lan do la mot lan tien them, va het chan la tu dong ve lai diem den.
    /// </summary>
    private void UpdateAvoid()
    {
        Vector3 toTarget = _target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) toTarget = _heading;
        else toTarget.Normalize();

        // THOI NE khi CA duong toi diem den LAN truoc mat deu trong.
        //
        // Chi hoi moi "truoc mat" thoi la khong du, va day la cai bay da do duoc bang so: vua gat
        // sang huong trong thi lan do ke tiep do theo huong MOI, thay trong, tat ne - va vat bay
        // lai nham thang vao dung toa nha cu. Do that o ban do: xuyen nha 47.2% so frame ma co ne
        // duoc 2.6%. Phai hoi them "duong toi dich da thong chua" thi moi biet la da vong qua that.
        if (Clearance(toTarget) >= _probeDistance && Clearance(_heading) >= _probeDistance)
        {
            _avoidDir = Vector3.zero;
            return;
        }

        Vector3 left = Quaternion.Euler(0f, -SideProbeAngle, 0f) * _heading;
        Vector3 right = Quaternion.Euler(0f, SideProbeAngle, 0f) * _heading;

        float dl = Clearance(left);
        float dr = Clearance(right);

        // DANG NE BEN NAO THI BAM BEN DO neu no con thoang. Doi ben lien tuc thi vat bay dung do
        // lac qua lac lai giua hai toa nha chu khong vong duoc qua cai nao.
        if (_avoidDir.sqrMagnitude > 0.0001f)
        {
            bool goingLeft = Vector3.Dot(_avoidDir, left) > Vector3.Dot(_avoidDir, right);
            if ((goingLeft ? dl : dr) >= _probeDistance)
            {
                _avoidDir = goingLeft ? left : right;
                return;
            }
        }

        // Hai ben thoang ngang nhau thi theo ben nao gan diem den hon - ne xong khong bi lech han
        // ra khoi huong dang muon di.
        if (Mathf.Abs(dl - dr) < _probeRadius)
        {
            _avoidDir = Vector3.Dot(toTarget, left) >= Vector3.Dot(toTarget, right) ? left : right;
            return;
        }

        _avoidDir = dl > dr ? left : right;
    }

    /// <summary>
    /// Con bao xa moi cham vat can theo huong nay - probeDistance nghia la trong.
    ///
    /// DUNG OverlapBox CHU KHONG DUNG SphereCast. SphereCast co mot cai bay da lam hong ban dau
    /// tien cua ham nay: no KHONG bao cao collider da chong len qua cau NGAY TAI CHO XUAT PHAT.
    /// Ban tu tam than thi qua cau nam trong collider cua chinh minh, nen phai day diem xuat phat
    /// ve truoc - ma probeRadius gio la 16.85 (nua be ngang airship) chu khong con la 1.5, nen day
    /// ve truoc 16.85u la dung ngay cai toa nha sap dam vao NAM TRON trong qua cau xuat phat va
    /// khong bao gio duoc bao. Do that: xuyen nha 50.8% so frame ma _avoidDir chua bat len mot lan.
    ///
    /// OverlapBox khong co khai niem "diem xuat phat" nen khong dinh bay do: quet dung mot hanh
    /// lang hop tu than ra truoc probeDistance, rong probeRadius moi ben, cao bang than.
    ///
    /// BO QUA vat THAP hon day than: bay qua duoc thi ne lam gi. Khong co bo loc nay thi voi hanh
    /// lang to bang nua than, vat bay se ne ca bui co lan mat dat.
    /// </summary>
    private float Clearance(Vector3 dir)
    {
        float half = _probeDistance * 0.5f;
        Vector3 center = transform.position + dir * half;
        Vector3 halfExtents = new Vector3(_probeRadius, _bodyHeight * 0.5f, half);
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        float bodyBottomWorld = transform.position.y + _bodyBottom;

        int n = Physics.OverlapBoxNonAlloc(center, halfExtents, _probeBuf, rot,
                                           _obstacleLayers, QueryTriggerInteraction.Ignore);
        float best = _probeDistance;

        for (int i = 0; i < n; i++)
        {
            Collider c = _probeBuf[i];
            if (c == null) continue;

            // Luoi an toan cho truong hop obstacleLayers van con chua layer cua chinh minh.
            Transform t = c.transform;
            if (t == transform || t.IsChildOf(transform)) continue;

            if (c.bounds.max.y < bodyBottomWorld) continue;   // thap hon than -> bay qua duoc

            // Con bao xa: chieu khoang cach len DUNG HUONG BAY, khong lay khoang cach thang - vat
            // nam lech han sang ben thi khong phai la thu chan duong.
            float d = Mathf.Max(0f, Vector3.Dot(c.bounds.center - transform.position, dir));
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>Do tam + ban kinh map tu Ground_Circle, y het cach MapBoundsBuilder lam.</summary>
    private void AutoFindMap()
    {
        GameObject g = GameObject.Find("Ground_Circle");
        if (g == null) return;

        Renderer r = g.GetComponentInChildren<Renderer>();
        if (r == null) return;

        Bounds b = r.bounds;
        _mapCenter = g.transform;
        _mapRadius = Mathf.Max(b.extents.x, b.extents.z);
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos) return;

        Vector3 c = _mapCenter != null ? _mapCenter.position : Vector3.zero;
        c = new Vector3(c.x, _flyHeight, c.z);

        // vanh bay
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        DrawCircle(c, _mapRadius * _radiusUse);

        if (!Application.isPlaying) return;

        // diem den + duong toi do
        Gizmos.color = _flying ? Color.cyan : Color.gray;
        Vector3 t = new Vector3(_target.x, _flyHeight, _target.z);
        Gizmos.DrawLine(transform.position, t);
        Gizmos.DrawWireSphere(t, _arriveDistance);

        // ba chum do: giua + hai ben
        if (_avoidObstacles)
        {
            DrawProbe(_heading, Color.yellow);
            DrawProbe(Quaternion.Euler(0f, -SideProbeAngle, 0f) * _heading, new Color(1f, 0.8f, 0.2f, 0.5f));
            DrawProbe(Quaternion.Euler(0f, SideProbeAngle, 0f) * _heading, new Color(1f, 0.8f, 0.2f, 0.5f));

            // huong dang ne (do = dang gac diem den lai de vong qua vat can)
            if (_avoidDir.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + _avoidDir.normalized * _probeDistance);
            }
        }
    }

    /// <summary>Ve dung cai hanh lang ma Clearance() quet, khong ve xap xi.</summary>
    private void DrawProbe(Vector3 dir, Color color)
    {
        Gizmos.color = color;
        float half = _probeDistance * 0.5f;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + dir * half,
                                      Quaternion.LookRotation(dir, Vector3.up), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_probeRadius * 2f, _bodyHeight, _probeDistance));
        Gizmos.matrix = old;
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        const int seg = 48;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
