using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Item an duoc, chay bang PHYSICS + co vong doi NGU/THUC don gian.
///
/// Trang thai:
///   Asleep  - Rigidbody ngu (khong mo phong). Luc moi vao scene o trang thai nay,
///             nam yen tren dat. Va cham thi Unity tu danh thuc -> chuyen Falling.
///   Sucked  - dang bi hut: Rigidbody kinematic (VAN "ngu", khong chiu physics),
///             SimpleSuction keo truc tiep bang MovePosition.
///   Falling - het bi hut / vua bi va cham: bat lai physics, tu roi theo trong luc,
///             va cham day nhau. Sau sleepDelay giay ma da nam yen thi tu NGU lai.
///
/// Chi giu 3 thong so gia tri (requiredLevel/xp/score) + sleepDelay. Khong con dong
/// thong so rung/bay/keo dai nhu Devourable cu.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PhysicsDevourable : MonoBehaviour
{
    [Header("Gia tri")]
    [Tooltip("Player phai dat cap nay tro len moi hut duoc")]
    public int requiredLevel = 1;

    [Tooltip("XP cong cho player khi bi nuot")]
    public int xpValue = 1;

    [Tooltip("Diem cong vao UIManager khi bi nuot")]
    public int scoreValue = 1;

    [Header("Khoa khi ke huc vao qua yeu")]
    [Tooltip("Hon MAY HANG thi item khoa cung lai, huc vao nhu huc tuong. 0 = tat han.\n\n" +
             "3 = con hon minh 3 hang tro len thi day khong nhuc nhich. Hon 2 hang van day duoc\n" +
             "(nang hay nhe la do mass), hon 1 hang thi da di duong RUNG TAI CHO cua he hut roi.\n\n" +
             "Kiem NGAY LUC VA CHAM, lay dung hang cua con vua huc vao - khong phai doan theo mot\n" +
             "con nao co dinh. Nho vay cung mot toa nha co the bat dong voi con Lv1 va an duoc voi\n" +
             "con Lv50, khong can trang thai toan cuc nao ca.")]
    public int pushLockStageDiff = 3;

    [Header("Ngu/thuc")]
    [Tooltip("Sau bao lau (giay) khong con bi hut / khong con va cham thi tu ngu lai")]
    public float sleepDelay = 5f;

    [Header("Bay vao mieng")]
    [Tooltip("Co con lai DUNG LUC BI NUOT (luc cham swallowDistance cua nguoi hut).\n" +
             "0 = teo han ve 0 vua kip luc bien mat - khong thay cu 'pop'.\n" +
             "Dat 0.05-0.1 neu muon item con nhin thay mot chut o khoanh khac cuoi.")]
    [Range(0f, 1f)] public float minShrink = 0f;

    [Tooltip("Bat dau thu nho tu khoang cach = ban kinh item * so nay. Vat cang TO nho tu cang xa\n" +
             "(fix vat level cao khong kip co lai vi bi an tu xa)")]
    public float shrinkRadiusMul = 2.5f;

    [Header("Xoan nhe khi bay vao")]
    [Tooltip("BAT: item luon nhe quanh truc toi mom cho do don. TAT: bay thang tap")]
    public bool useSwirl = true;

    [Tooltip("Do rong xoan = KHOANG CACH CON LAI x he so nay.\n\n" +
             "Vi ban kinh ti le voi khoang cach nen no TU VE 0 khi cham mom - hoi tu duoc bao dam\n" +
             "ve mat toan hoc, khac han ban cu cong van toc tiep tuyen (van toc khong tu triet tieu\n" +
             "nen item lech mai roi bay xuyen qua nguoi choi).\n\n" +
             "0.12 = luon rong bang 12% quang duong con lai. Tren 0.25 la bat dau nhin loan.")]
    [Range(0f, 0.4f)] public float swirlAmount = 0.2f;

    [Tooltip("So vong luon moi giay. Cao qua thi item rung chu khong con ra hinh xoan")]
    public float swirlTurnsPerSecond = 1.8f;

    [Tooltip("KEP do rong xoan trong bao nhieu phan ban kinh non hut tai cho do.\n" +
             "0.5 = khong bao gio ra qua nua non -> item luon nam trong vung hut, khong bi Scan tha ra")]
    [Range(0.1f, 1f)] public float swirlConeFraction = 0.5f;

    [Tooltip("Duoi bao nhieu LAN nguong nuot thi TAT DAN xoan de ban thang vao mom.\n" +
             "4 = tu 4 lan nguong nuot tro vao, ban kinh xoan tan dan ve 0.\n\n" +
             "VI SAO CAN: xoan la mot diem ngam LECH TRUC, ma o doan cuoi khong con quyen lai nao\n" +
             "de sua cai lech do. De bam theo diem ngam dang quay can gia toc ngang bang\n" +
             "toc_do x toc_do_quay - o so hien tai la 565 u/s2 trong khi pullAccel chi co 78.5, tuc\n" +
             "van toc chi theo kip 14%. Phan khong theo kip la mot sai lech ngang dung vao khoanh\n" +
             "khac quyet dinh trung hay truot mom, ma truot mot lan la item vot qua roi mat han\n" +
             "(no ra sau lung -> lot khoi non -> Scan tha ra -> rot xuong dat).\n\n" +
             "0 = khong tat, xoan toi tan mom nhu ban cu.")]
    [Range(0f, 8f)] public float swirlLockInMul = 4f;

    [Tooltip("Item XOAY quanh dung truc dang bi keo (do/giay) - khong phai quay lung tung.\n" +
             "De THAP de con nhin ro do vat la gi. Tren ~600 la thanh vet mo, khong doc duoc hinh")]
    public float swirlSpin = 360f;

    [Header("Nghieng theo luc hut")]
    [Tooltip("Item NGHIENG DAU ve phia mom khi bi hut - nhin nhu bi giat di chu khong phai troi\n" +
             "deu giu nguyen the.\n\n" +
             "0 = giu nguyen tu the luc bi tom (nhu cu)\n" +
             "1 = cam thang dau vao mom\n" +
             "0.6-0.8 thuong dep nhat: co huong ro nhung van con nhan ra hinh dang goc")]
    [Range(0f, 1f)] public float leanIntoPull = 0.8f;

    [Tooltip("Toc do quay dau theo huong hut (do/giay).\n" +
             "THAP = nang ne, item lieng tu tu moi bat duoc huong (do vat to nen de thap)\n" +
             "CAO = giat ngay vao huong, dut khoat")]
    public float leanSpeed = 540f;

    [Header("Giang co (item hon player dung 1 cap)")]
    [Tooltip("Do NGHIENG NEN ve phia mom (degree). Item hon 1 cap khong bi keo di, nhung nguon\n" +
             "chui ve phia ke dang hut - nhu mot cai cay bi keo ma re con bam dat.\n" +
             "0 = dung thang, chi lac lu tai cho")]
    public float struggleLean = 10f;

    [Tooltip("Bien do LAC LU quanh the nghieng do (degree). 0 = nghieng cung mot goc, khong dong dua")]
    public float struggleTilt = 5f;

    [Tooltip("Toc do lac lu (radian/giay). ~4-6 la dong dua nang ne; 20+ thanh rung ban bat")]
    public float struggleFreq = 5f;

    [Header("Hieu ung nuot (swallow)")]
    [Tooltip("Thoi gian item xoay tit + teo lao vao mom truoc khi bien mat (giay). 0 = bien mat ngay")]
    public float swallowDuration = 0.12f;

    [Tooltip("Toc do xoay tit khi bi nuot (do/giay)")]
    public float swallowSpin = 1200f;

    [Header("Su kien")]
    public UnityEvent onDevoured;

    /// <summary>Cap yeu cau, cho SimpleSuction doc.</summary>
    public int RequiredLevel { get { return requiredLevel; } }

    /// <summary>
    /// Con dang GIU item nay - CHI DUY NHAT mot con. null = dang tu do.
    ///
    /// Co no vi item chay bang mot bo trang thai vat ly DUNG NHAT (kinematic, gravity, scale,
    /// velocity). Hai con cung tac dong thi:
    ///   - ca hai cung ghi linearVelocity moi FixedUpdate -> item giat qua giat lai giua 2 mom
    ///   - mot con Pull (kinematic = false) con mot con Struggle (kinematic = true) -> lat trang
    ///     thai moi frame, item vua bay vua dung hinh
    ///   - con A ra khoi tam goi Release -> bat lai trong luc, tra ve co goc, trong khi con B
    ///     dang keo do dang
    /// Nen quyen dieu khien phai la doc quyen, cac con khac chi duoc DANH nhau de gianh.
    /// </summary>
    public SimpleSuction Owner { get { return _owner; } }

    /// <summary>Tam thuc te (tam bounds) de suction keo cho dung, khong lech theo pivot.</summary>
    public Vector3 Center { get { return transform.TransformPoint(_centerLocal); } }

    /// <summary>
    /// Toc do THUC TE cua item (u/s) - lay tu rigidbody chu khong phai toc do dat lenh, vi
    /// MoveTowards lam van toc that luon bam sau lenh mot nhip. SimpleSuction doc de noi rong
    /// nguong nuot cho item bay nhanh (chong nhay coc qua vung nuot giua hai buoc physics).
    /// </summary>
    public float Speed { get { return _rb != null ? _rb.linearVelocity.magnitude : 0f; } }

    private enum State { Asleep, Sucked, Struggling, Falling }
    private State _state = State.Asleep;
    private Rigidbody _rb;
    private Vector3 _centerLocal;
    private float _radius = 0.5f;
    private Vector3 _startScale = Vector3.one;
    private Vector3 _anchor;
    private Quaternion _anchorRot;
    private float _noiseSeed;
    private float _sleepAt;
    private bool _pushLocked;          // dang khoa cung vi ke huc vao qua yeu
    private float _startDrag = -1f;    // drag authored tren prefab. -1 = chua chup
    private float _grabDist = -1f;     // khoang cach toi mom luc VUA BI TOM. -1 = chua tom
    private float _swirlAngle;
    private float _swirlSign;          // moi item luon mot chieu, cho khong dong loat giong nhau
    private float _swirlPhase;         // lech pha ban dau, cung ly do
    private Quaternion _grabRot = Quaternion.identity;   // the cua item luc vua bi tom
    private Quaternion _leanRot = Quaternion.identity;   // huong dau dang nghieng toi
    private float _spinAngle;
    private Collider[] _cols;
    private static readonly RaycastHit[] _groundBuf = new RaycastHit[8];
    private Collider[] _ignoredCols;   // collider cua CHU dang duoc bo qua va cham (null = khong bo qua ai)
    private Tween _swallowTween;

    private SimpleSuction _owner;
    private float _ownerDist;          // khoang cach chu -> item, chu tu cap nhat moi lan quet
    private bool _ownerCanEat;         // chu du cap NUOT hay chi lam item giay tai cho

    void OnDestroy()
    {
        // Tween con song ma target da chet thi DOTween nem loi
        if (_swallowTween != null && _swallowTween.IsActive()) _swallowTween.Kill();
        _swallowTween = null;
    }

    void Awake()
    {
        EnsureReferences();
        _centerLocal = CalcCenterLocal();
        _startScale = transform.localScale;
        _noiseSeed = Random.value * 10f;
        _swirlSign = Random.value < 0.5f ? -1f : 1f;
        _swirlPhase = Random.value * Mathf.PI * 2f;
    }

    private void EnsureReferences()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        EnterSleep();
    }

    /// <summary>Bien mat sau khi bi nuot.</summary>
    private void Vanish()
    {
        Destroy(gameObject);
    }

    /// <summary>
    /// XIN QUYEN GIU item. SimpleSuction goi moi lan quet, cho MOI item nam trong non.
    /// Tra ve true = duoc keo con nay frame nay; false = con khac dang giu, dung dong vao.
    ///
    /// Luat gianh, xet theo thu tu:
    ///   1. AN DUOC an dut CHI LAM NO GIAY. Neu khong co luat nay thi mot con nhoc chi du suc
    ///      lam cai xe rung tai cho van khoa duoc cai xe do, con to dung ngay ben canh khong
    ///      dong vao duoc - vo ly va rat uc che.
    ///   2. Cung hang thi GAN MOM HON thang, nhung phai gan hon HAN mot khoang (stealMargin),
    ///      khong thi hai con xap xi nhau se giat qua giat lai moi lan quet.
    /// </summary>
    public bool TryClaim(SimpleSuction claimer, float dist, bool canEat, float stealMargin)
    {
        if (claimer == null || _consumed) return false;

        // Dang la chu roi: chi cap nhat lai so lieu cho ke thach dau sau nay so
        if (_owner == claimer)
        {
            _ownerDist = dist;
            _ownerCanEat = canEat;
            return true;
        }

        if (_owner != null)
        {
            bool win = canEat != _ownerCanEat
                ? canEat                                   // an duoc thi gianh dut, khong can gan hon
                : dist < _ownerDist * stealMargin;         // cung hang: phai gan hon han moi gianh
            if (!win) return false;
            // KHONG goi nguoc vao chu cu o day: no tu thay minh mat quyen o vong lap ke tiep
            // (Owner != this) roi tu bo ra khoi danh sach - tranh sua collection cua object khac
            // ngay giua luc no co the dang duyet.
        }

        _owner = claimer;
        _ownerDist = dist;
        _ownerCanEat = canEat;
        return true;
    }

    /// <summary>
    /// BO QUA VA CHAM voi than player trong luc item dang bay vao mom.
    ///
    /// Luc bi hut, item la Rigidbody DONG lao thang vao nguoi choi -> solver se day nguoi choi
    /// van ra. Tat va cham cap collider (item x player) trong giai doan nay la het bi day, ma
    /// collider VAN BAT (khong disable) nen OverlapSphere cua SimpleSuction van quet thay item.
    /// Nuot van chay binh thuong nho swallowDistance trong SimpleSuction.ApplyActive.
    ///
    /// Item qua cap (Struggle / bi bo qua) KHONG goi ham nay -> van chan duong nguoi choi.
    /// </summary>
    public void SetPlayerCollision(Collider[] playerCols, bool ignore)
    {
        Collider[] want = ignore ? playerCols : null;
        if (want == _ignoredCols) return;

        // Doi chu: phai TRA LAI va cham cho chu cu truoc roi moi bo qua cho chu moi.
        // Ban cu chi co mot co bool nen doi chu la sai ca hai dau - chu cu vinh vien khong bi
        // item dam nua, con chu moi thi bi item lao vao day van ra.
        ApplyIgnore(_ignoredCols, false);
        ApplyIgnore(want, true);
        _ignoredCols = want;
    }

    private void ApplyIgnore(Collider[] target, bool ignore)
    {
        if (target == null) return;
        if (_cols == null) _cols = GetComponentsInChildren<Collider>(false);

        for (int i = 0; i < _cols.Length; i++)
        {
            // Physics.IgnoreCollision bao loi neu collider dang tat / object inactive
            if (_cols[i] == null || !_cols[i].enabled || !_cols[i].gameObject.activeInHierarchy) continue;
            for (int j = 0; j < target.Length; j++)
            {
                if (target[j] == null || !target[j].enabled || !target[j].gameObject.activeInHierarchy) continue;
                Physics.IgnoreCollision(_cols[i], target[j], ignore);
            }
        }
    }

    /// <summary>
    /// Overload tuong thich cu - khong biet non hut nen bay thang, teo het o dung tam mom.
    /// </summary>
    public void Pull(Vector3 target, float targetSpeed, float accel)
    {
        Pull(target, target, 0f, targetSpeed, accel, 0f);
    }

    /// <summary>
    /// SimpleSuction goi moi FixedUpdate khi item nam trong non. Item bay vao mom (thang, hoac
    /// luon nhe neu bat useSwirl), vua bay vua teo - va teo het DUNG LUC bi nuot.
    ///
    /// XOAN BANG CACH DOI DIEM NGAM, khong phai cong van toc tiep tuyen nhu ban cu. Khac biet
    /// quan trong: ban kinh xoan ti le voi khoang cach con lai nen no tu ve 0 khi item cham mom,
    /// tuc diem ngam hoi tu ve dung mom -> item chac chan toi noi. Ban cu cong van toc ngang ma
    /// van toc thi khong tu triet tieu, item lech truc 20-25 do suot duong bay roi vong qua mom,
    /// khong bao gio cham nguong nuot va bay xuyen qua nguoi choi.
    ///
    /// TEO NEO VAO swallowDistance, KHONG PHAI vao tam mom. Item bien mat tai
    /// dist == swallowDistance chu khong phai tai dist == 0, nen neo vao tam mom thi luc bien mat
    /// no van con nguyen mot cuc - dung cai "pop" nhin thay duoc. Neo dung cho thi scale cham 0
    /// vua kip khoanh khac Swallow() chay.
    ///
    /// TOC DO TEO TU BAM THEO TOC DO HUT, khong can them logic: scale la ham cua KHOANG CACH nen
    ///     d(scale)/dt = f'(dist) x d(dist)/dt = f'(dist) x toc_do_hut
    /// Toc do hut o ria chi bang farSpeedFactor (~26%) con sat mom la 100%, nen item tu dong teo
    /// cham o ngoai va gap ~4 lan khi vao vung hut manh.
    /// </summary>
    public void Pull(Vector3 mouthPos, Vector3 originPos, float coneAngleDeg,
                     float targetSpeed, float accel, float swallowDistance)
    {
        EnsureReferences();
        if (_state != State.Sucked) EnterSucked();

        Vector3 center = Center;
        Vector3 toMouth = mouthPos - center;
        float distToMouth = toMouth.magnitude;

        // MOC TEO: khoang cach o lan Pull DAU TIEN cua pha nay. EnterSucked ngay tren da tra no
        // ve -1 nen moi lan bi tom lai la chot lai moc moi.
        if (_grabDist < 0f) _grabDist = distToMouth;
        Vector3 pullDir = distToMouth > 0.001f ? toMouth / distToMouth : Vector3.up;

        // DIEM NGAM: mom, hoac mot diem luon quanh mom neu bat xoan
        Vector3 aim = mouthPos;

        if (useSwirl && swirlAmount > 0.0001f && distToMouth > 0.001f)
        {
            _swirlAngle += swirlTurnsPerSecond * 2f * Mathf.PI * Time.fixedDeltaTime;

            // Ban kinh TI LE KHOANG CACH CON LAI -> tien toi mom thi tu co ve 0, hoi tu bao dam
            float r = distToMouth * swirlAmount;

            // Kep trong non hut de Scan khong tha item ra giua chung
            if (coneAngleDeg > 0.01f)
            {
                Vector2 flat = new Vector2(center.x - originPos.x, center.z - originPos.z);
                float coneR = flat.magnitude * Mathf.Sin(coneAngleDeg * 0.5f * Mathf.Deg2Rad);
                r = Mathf.Min(r, coneR * swirlConeFraction);
            }

            // TAN DAN VE 0 o cu ly gan: doan cuoi ban thang tam mom, doan xa van xoan nhu cu.
            // Neo vao swallowDistance (nguong nuot THUC cua frame nay, da noi rong theo toc do)
            // chu khong phai mot so tuyet doi - tune pullSpeed thi nguong tu doi theo.
            if (swallowDistance > 0.0001f && swirlLockInMul > 0.0001f)
            {
                float lockIn = swallowDistance * swirlLockInMul;
                if (lockIn > swallowDistance) r *= Mathf.InverseLerp(swallowDistance, lockIn, distToMouth);
            }

            Vector3 right = Vector3.Cross(Vector3.up, pullDir);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;    // pull thang dung
            right.Normalize();
            Vector3 upPerp = Vector3.Cross(pullDir, right);

            float a = _swirlAngle * _swirlSign + _swirlPhase;
            aim = mouthPos + (Mathf.Cos(a) * right + Mathf.Sin(a) * upPerp) * r;
        }

        ApplyPullRotation(pullDir);

        // Bay ve DIEM NGAM. Toc do luon dung bang targetSpeed (khong cong them vector nao) nen
        // pullSpeed van tune dung nghia, va item khong bao gio vot nhanh hon so cau hinh.
        Vector3 toAim = aim - center;
        Vector3 dir = toAim.sqrMagnitude > 0.000001f ? toAim.normalized : pullDir;
        _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, dir * targetSpeed,
                                                accel * Time.fixedDeltaTime);

        // Teo theo khoang cach toi MOM (khong phai toi diem ngam): mom moi la cho item bien mat.
        // shrinkStart phai lon hon nguong nuot, khong thi InverseLerp dao dau va item phinh nguoc.
        // TEO SUOT QUANG BAY: lay luon khoang cach luc bi tom lam diem bat dau, nen item DAY
        // nguyen co o khoanh khac bi tom va bang 0 dung luc bien mat - o moi level, moi tam hut,
        // moi toc do, khong co so nao phai tune.
        //
        // Khong co no thi diem bat dau la max(ban kinh x 2.5, nguong nuot x 2). Voi item NHO thi
        // ve cuc sau thang: nguong nuot x 2 = dung 3 buoc physics, tru phan cua bat con 1.5 buoc.
        // Do that: hamburger o pullSpeed 50 chi duoc 1.5 buoc de teo -> no render o co 1.0 roi
        // 0.33 roi bien mat. Item TO thi khong dinh vi ban kinh x 2.5 da du rong (xe hoi: 7.18u).
        //
        // Van giu ca hai moc cu lam SAN: item bi tom ngay sat mat van co mot doan de teo, va vat
        // to van bat dau teo tu xa nhu truoc.
        float shrinkStart = Mathf.Max(Mathf.Max(_radius * shrinkRadiusMul, swallowDistance * 2f), _grabDist);
        float t = Mathf.InverseLerp(swallowDistance, shrinkStart, distToMouth);   // 0 tai nguong nuot, 1 tai shrinkStart
        transform.localScale = _startScale * Mathf.Lerp(minShrink, 1f, t);
    }

    /// <summary>
    /// TU THE khi dang bi hut: nghieng dau ve phia mom roi XOAY quanh dung truc do.
    ///
    /// Khong dung angularVelocity nhu ban cu: giao physics mot van toc goc thi item quay quanh
    /// truc cu cua no, khong he biet mom o dau - nhin ra la "do vat dang quay" chu khong phai
    /// "do vat dang bi giat vao mom". O day ta dung ca hai thanh phan:
    ///   _leanRot : huong "dau" item, quay DAN ve pullDir voi toc do co han (leanSpeed) nen co
    ///              do i - vat to lieng tu tu, dung cam giac bi luc keo be huong.
    ///   _spinAngle: goc xoay CHONG LEN tren truc vua nghieng toi -> xoay quanh chinh duong bay.
    ///
    /// angularVelocity phai ve 0: de nguyen thi physics va MoveRotation danh nhau, item giat cuc.
    /// </summary>
    private void ApplyPullRotation(Vector3 pullDir)
    {
        if (leanIntoPull <= 0.001f && swirlSpin <= 0.1f) return;

        float dt = Time.fixedDeltaTime;
        _rb.angularVelocity = Vector3.zero;

        Quaternion look = Quaternion.LookRotation(pullDir, Vector3.up);
        Quaternion want = leanIntoPull >= 0.999f ? look : Quaternion.Slerp(_grabRot, look, leanIntoPull);
        _leanRot = Quaternion.RotateTowards(_leanRot, want, leanSpeed * dt);

        if (swirlSpin > 0.1f)
        {
            _spinAngle += swirlSpin * _swirlSign * dt;
            if (_spinAngle > 360f || _spinAngle < -360f) _spinAngle %= 360f;   // khoi tran float sau vai phut
        }

        _rb.MoveRotation(_leanRot * Quaternion.AngleAxis(_spinAngle, Vector3.forward));
    }

    /// <summary>
    /// GIANG CO: item hon player dung 1 cap. Khong bi keo di, khong bi nuot - nhung NGHIENG VE PHIA
    /// MOM va dong dua quanh the nghieng do.
    ///
    /// Truc xoay chinh la duong vuong goc voi huong toi mom (Cross(up, huong)), nen goc duong lam
    /// NGON item chui dung ve phia ke dang hut - khong phai nghieng bua mot ben. Kem mot truc phu
    /// lac ngang cho chuyen dong khong nam gon trong mot mat phang.
    ///
    /// DUNG YEN MOT CHO: chan van o nguyen diem neo. Ban truoc rung ca vi tri bang Perlin noise,
    /// nhin ra vat nhe bi rung bần bật - nguoc voi y do, vi day toan la vat TO qua tam nguoi choi.
    ///
    /// mouthPos truoc day duoc truyen vao nhung khong dung toi; gio no la ca huong nghieng.
    /// </summary>
    public void Struggle(Vector3 mouthPos)
    {
        if (_state != State.Struggling) EnterStruggle();

        transform.position = _anchor;   // giang co la GHIM TAI CHO, khong nhuc nhich

        Vector3 toMouth = mouthPos - transform.position;
        toMouth.y = 0f;
        if (toMouth.sqrMagnitude < 0.0001f) { transform.rotation = _anchorRot; return; }
        toMouth.Normalize();

        Vector3 axisMain = Vector3.Cross(Vector3.up, toMouth);   // nghieng TOI / LUI theo huong mom
        if (axisMain.sqrMagnitude < 0.0001f) { transform.rotation = _anchorRot; return; }
        axisMain.Normalize();

        float t = Time.time * struggleFreq + _noiseSeed;

        // HAI SIN LECH TAN thay vi mot sin thuan. Mot sin deu tam tap nghe thi hay nhung nhin thi
        // ra ngay may danh nhip - vat dang co tru lai khong bao gio dao dong deu nhu the. Ti so
        // 1.7 la so vo ti nen hai song khong bao gio khop lai, chu ky tong khong lap.
        float sway = Mathf.Sin(t) * 0.65f + Mathf.Sin(t * 1.7f + 1.3f) * 0.35f;

        // Ghim goc trong khoang [0 .. lean + tilt]: KHONG cho lac qua dau ve ben kia.
        // Nga nguoc ra xa mom giua luc dang bi keo la vo ly, va do chinh la cai lam no trong gia.
        float angle = Mathf.Max(0f, struggleLean + struggleTilt * sway);

        // Lac ngang mot chut quanh chinh truc huong mom, tan so khac han -> chuyen dong khong
        // nam gon trong mot mat phang, nhin moi ra "vung vay" thay vi "gat nuoc mua"
        float side = struggleTilt * 0.35f * Mathf.Sin(t * 0.63f + _noiseSeed * 2f);

        transform.rotation = Quaternion.AngleAxis(angle, axisMain)
                           * Quaternion.AngleAxis(side, toMouth)
                           * _anchorRot;
    }

    /// <summary>
    /// Het bi hut/giang co: bat lai trong luc, tra ve kich thuoc goc, tu roi xuong. Sau sleepDelay ngu.
    ///
    /// CHI CHU moi tha duoc. Con khac goi vao day khong co tac dung - neu khong thi con A vua di
    /// ngang qua het tam la tha luon cai item con B dang keo do dang.
    /// </summary>
    /// <summary>
    /// KHOA CUNG item lai: ke vua huc vao qua yeu so voi no, nen no dung im nhu mot buc tuong.
    ///
    /// DUNG constraints CHU KHONG DUNG isKinematic. isKinematic dang duoc BA cho khac bat/tat
    /// (EnterSleep, EnterSucked, PlaySwallow); chen them nguoi thu tu vao cung mot co la kieu bug
    /// im lang toi nhat - item tu nhien bat dong khong ly do, va khong lan ra duoc ai ghi cuoi.
    /// constraints la o rieng, khong ai dong toi.
    ///
    /// Body van la DONG (khong kinematic) nen callback va cham van ban binh thuong - do la duong
    /// de sau nay co con du cap den mo khoa.
    /// </summary>
    /// <summary>Chup drag authored tren prefab, mot lan duy nhat - de con tra lai sau khi bi hut.</summary>
    private void CacheStartDrag()
    {
        if (_startDrag < 0f && _rb != null) _startDrag = _rb.linearDamping;
    }

    /// <summary>Tra drag ve nhu prefab. Goi o moi cua RA khoi trang thai dang bi hut.</summary>
    private void RestoreDrag()
    {
        if (_startDrag >= 0f && _rb != null) _rb.linearDamping = _startDrag;
    }

    private void SetPushLock(bool locked)
    {
        EnsureReferences();
        if (_rb == null) return;

        RigidbodyConstraints want = RigidbodyConstraints.None;
        if (locked)
        {
            want = RigidbodyConstraints.FreezePositionX
                 | RigidbodyConstraints.FreezePositionZ
                 | RigidbodyConstraints.FreezeRotation;

            // KHOA CA Y - nhung CHI KHI DANG DUNG TREN DAT. Khoa Y vo dieu kien thi mon nao bi
            // khoa dung luc dang bay se TREO LO LUNG mai mai: no khong roi duoc nua, va cai duy
            // nhat mo khoa duoc no la mot con du cap di ngang qua - co the khong bao gio xay ra.
            //
            // Dang bay thi chi khoa ngang thoi: no van roi binh thuong, cham dat, roi cu cham ke
            // tiep se nang len thanh khoa du. So sanh 'want' voi constraints hien tai (chu khong
            // so voi co _pushLocked) chinh la de cai nang cap do chay duoc.
            if (IsGrounded()) want |= RigidbodyConstraints.FreezePositionY;
        }

        if (_rb.constraints == want) { _pushLocked = locked; return; }
        _pushLocked = locked;

        _rb.constraints = want;

        if (locked)
        {
            // Cu huc cua frame nay da kip truyen van toc TRUOC khi ta khoa (OnCollisionEnter ban
            // sau khi PhysX giai xong va cham) - xoa di, khong thi item van truot/vot len them mot
            // doan theo quan tinh du da khoa.
            Vector3 v = _rb.linearVelocity;
            bool yLocked = (want & RigidbodyConstraints.FreezePositionY) != 0;
            _rb.linearVelocity = yLocked ? Vector3.zero : new Vector3(0f, v.y, 0f);
            _rb.angularVelocity = Vector3.zero;
        }
        else _rb.WakeUp();
    }

    /// <summary>
    /// Item co dang DUNG TREN mot cai gi do khong - ban mot tia ngan tu day than xuong.
    ///
    /// Bat dau tia tu day bounds cong mot chut, va bo qua collider CUA CHINH MINH: ban tu tam than
    /// thi tia dam vao chinh no truoc va luc nao cung tra ve "dang dung dat".
    ///
    /// Khong do duoc bounds thi tra TRUE - tha khoa nham con hon de mot mon treo lo lung.
    /// </summary>
    private bool IsGrounded()
    {
        if (_cols == null || _cols.Length == 0) _cols = GetComponentsInChildren<Collider>(false);
        if (_cols == null || _cols.Length == 0) return true;

        Bounds b = new Bounds(); bool has = false;
        for (int i = 0; i < _cols.Length; i++)
        {
            if (_cols[i] == null || !_cols[i].enabled) continue;
            if (!has) { b = _cols[i].bounds; has = true; }
            else b.Encapsulate(_cols[i].bounds);
        }
        if (!has) return true;

        Vector3 origin = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, _groundBuf, 0.4f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = _groundBuf[i].collider;
            if (c == null) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;   // chinh minh
            return true;
        }
        return false;
    }

    /// <summary>Tha khoa tu ben ngoai - GameManager goi khi co khoa bi tat giua chung.</summary>
    public void ClearPushLock() { SetPushLock(false); }

    public void Release(SimpleSuction by)
    {
        if (by != null && _owner != null && _owner != by) return;

        _owner = null;
        _grabDist = -1f;
        RestoreDrag();
        SetPlayerCollision(null, false);   // bat lai va cham voi player
        if (_state != State.Sucked && _state != State.Struggling) return;

        if (_state == State.Struggling) transform.rotation = _anchorRot;   // het rung, tra ve the dung

        _state = State.Falling;
        _rb.isKinematic = false;
        _rb.useGravity = true;
        transform.localScale = _startScale;
        _rb.WakeUp();
        _sleepAt = Time.time + sleepDelay;
    }

    /// <summary>Da bi nuot chua (chong nuot trung trong cung mot frame).</summary>
    public bool Consumed { get { return _consumed; } }
    private bool _consumed;

    /// <summary>
    /// Cham mieng / cham than player: ban su kien roi choi HIEU UNG NUOT (xoay + teo lao vao mom)
    /// truoc khi bien mat. swallowTarget = transform mom de item bay dung vao do.
    /// </summary>
    public void Devour(Transform swallowTarget = null)
    {
        if (_consumed) return;
        _consumed = true;
        _owner = null;                 // da bi nuot, khong con gi de gianh
        if (onDevoured != null) onDevoured.Invoke();

        if (swallowDuration > 0f && isActiveAndEnabled)
            PlaySwallow(swallowTarget);
        else
            Vanish();
    }

    /// <summary>
    /// Hieu ung nuot: xoay tit + teo nho lao thang vao mom roi bien mat.
    ///
    /// Chay bang DOTween thay cho coroutine cu. Van BAM THEO mom (doc target.position moi frame
    /// trong callback) chu khong bay toi mot diem co dinh - nguoi choi dang chay thi mom di
    /// chuyen, ban toi diem cu se thay item lao tret ra sau lung.
    /// </summary>
    private void PlaySwallow(Transform target)
    {
        _state = State.Sucked;                       // khoa: physics/suction khong con xen vao
        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        _rb.isKinematic = true;
        _rb.useGravity = false;

        Collider[] cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;   // dang bay vao, khong xo day thu khac

        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        Transform tf = transform;

        tf.DOKill();
        _swallowTween = DOTween.To(() => 0f, k =>
        {
            if (tf == null) return;
            Vector3 tp = target != null ? target.position : startPos;
            tf.position = Vector3.Lerp(startPos, tp, k);
            tf.localScale = startScale * (1f - k);
            tf.Rotate(Vector3.up, swallowSpin * Time.deltaTime, Space.Self);
        }, 1f, swallowDuration)
            .SetEase(Ease.InQuad)                    // gia toc lao vao mom, giong ease*ease cu
            .OnComplete(() => { if (this != null) Vanish(); });
    }

    void FixedUpdate()
    {
        if (_state != State.Falling) return;
        if (Time.time < _sleepAt) return;

        // Chi ngu khi da nam tuong doi yen; con dang lao thi khoan, cho them chut
        if (_rb.linearVelocity.sqrMagnitude < 0.04f && _rb.angularVelocity.sqrMagnitude < 0.04f)
            EnterSleep();
        else
            _sleepAt = Time.time + 0.5f;
    }

    void OnCollisionEnter(Collision collision) { Contact(collision.collider); }

    /// <summary>
    /// MOM la collider TRIGGER, ma trigger thi KHONG BAO GIO ban OnCollisionEnter - phai bat rieng
    /// o day. Khong co ham nay thi cham mom khong co chuyen gi xay ra ca.
    ///
    /// KHONG goi Contact(): phan danh thuc item trong do la danh cho va cham VAT LY that. Di qua
    /// trigger nao cung danh thuc item thi bat ky vung trigger nao trong scene cung du de dung day
    /// ca bai do an dang nam ngu.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;

        SimpleSuction suction = other.GetComponentInParent<SimpleSuction>();
        if (suction != null) suction.EatByContact(this, other);
    }

    /// <summary>
    /// Xu ly va cham: neu cham vao PLAYER (co SimpleSuction) thi bao no an minh (van theo cap).
    /// Cham vat khac / mat dat thi lo phan ngu-thuc: dang ngu bi dung -> thuc + dem gio ngu lai.
    /// </summary>
    private void Contact(Collider other)
    {
        if (_consumed) return;

        SimpleSuction suction = other.GetComponentInParent<SimpleSuction>();
        if (suction != null)
        {
            suction.EatByContact(this, other);   // du cap thi bi nuot; qua cap thi khong
            if (_consumed) return;

            // KHOA / MO KHOA theo dung hang cua ke VUA HUC VAO.
            //
            // Khoa NGAY tu cu cham dau tien, khong hoi han gi them: khoa chi chan truc ngang nen
            // vat dang roi van roi binh thuong, khong co canh treo lo lung de phai de phong.
            // Dang bi hut / dang rung thi he hut lo, o day khong dong vao.
            // Co TAT thi khong khoa gi het - va van tha cai dang khoa (neu co) de khong con nao
            // ket lai. Khong co GameManager (scene test) thi coi nhu BAT, giu nguyen hanh vi cu.
            bool allowed = !GameManager.HasInstance || GameManager.Instance.PushLockEnabled;

            if (pushLockStageDiff > 0 && allowed && _state != State.Sucked && _state != State.Struggling)
            {
                int diff = suction.StageAtLevel(requiredLevel) - suction.Stage;
                SetPushLock(diff >= pushLockStageDiff);
            }
            else if (!allowed) SetPushLock(false);
        }

        if (_state == State.Sucked || _state == State.Struggling) return;   // suction dang dieu khien, physics khong xen vao

        if (_state == State.Asleep)
        {
            _state = State.Falling;
            _sleepAt = Time.time + sleepDelay;
        }
        else if (_state == State.Falling)
        {
            _sleepAt = Time.time + sleepDelay;
        }
    }

    private void EnterSucked()
    {
        EnsureReferences();

        // MO KHOA NGAY TU DAY, truoc moi dong ghi van toc trong Pull(). Day la CUA VAO cua he hut,
        // nen dat o day thi khong ton tai duong nao ma mot con du cap lai bi cai khoa can. Dat o
        // cho va cham thi hut TU XA se khong mo duoc - do moi la lo hong that.
        SetPushLock(false);

        // TAT DRAG trong luc bay vao mom. Drag sinh ra de item bi DAY thi dung lai som (vat cang
        // to cang dung nhanh = cam giac nang), no khong co viec gi trong pha hut.
        //
        // De nguyen thi no bop chet toc do hut: moi buoc Pull chi cong duoc accel x dt = 1.57 u/s,
        // con drag cat di v x (1 - 1/(1+drag x dt)) - can bang lai thi drag 4 chi con hut duoc
        // 21 u/s thay vi 50, drag 12 con 7.6. Item cang to (drag cang cao) cang bi hut cham nhu rua,
        // dung nguoc voi y do.
        CacheStartDrag();
        _rb.linearDamping = 0f;

        _state = State.Sucked;
        _rb.isKinematic = false;   // VAT LY, khong ngu
        _rb.useGravity = false;    // bay thang vao mieng, khong bi trong luc keo xuong

        // Chup the hien tai lam goc: co no thi leanIntoPull < 1 moi pha tron duoc, va item bat
        // dau lieng TU tu the that cua no chu khong nhay cai sang huong moi
        _grabRot = transform.rotation;
        _leanRot = _grabRot;
        _spinAngle = 0f;
        _grabDist = -1f;   // pha moi -> chot lai moc teo o lan Pull dau tien

        _rb.WakeUp();
    }

    private void EnterStruggle()
    {
        RestoreDrag();
        SetPushLock(false);   // di duong RUNG TAI CHO: he hut lo phan dung yen, khong phai cai khoa
        _state = State.Struggling;
        _rb.isKinematic = true;    // rung tai cho bang transform, khong cho physics keo di
        _anchor = transform.position;
        _anchorRot = transform.rotation;
    }

    private void EnterSleep()
    {
        RestoreDrag();
        _state = State.Asleep;
        _owner = null;             // nam yen tren dat roi thi ai gianh cung duoc
        if (_rb.isKinematic) _rb.isKinematic = false;
        _rb.useGravity = true;
        transform.localScale = _startScale;   // phong to lai neu vua bi hut do
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.Sleep();
    }

    private Vector3 CalcCenterLocal()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool has = false;
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null || rends[i] is ParticleSystemRenderer) continue;
            if (!has) { b = rends[i].bounds; has = true; }
            else b.Encapsulate(rends[i].bounds);
        }
        _radius = has ? b.extents.magnitude : 0.5f;   // ban kinh world luc dau (cho shrink theo co)
        return has ? transform.InverseTransformPoint(b.center) : Vector3.zero;
    }
}
