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

    [Header("Giang co (item hon player dung 1 cap)")]
    [Tooltip("Bien do RUNG tai cho (don vi world). Item hon 1 cap chi rung, khong bi hut/di chuyen")]
    public float struggleShake = 0.08f;

    [Tooltip("Tan so rung. Cao = rung gap")]
    public float struggleFreq = 26f;

    [Tooltip("Do (degree) lac nghieng nhe khi rung. 0 = khong lac")]
    public float struggleTilt = 5f;

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
    private Collider[] _cols;
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
    /// Overload tuong thich cu - khong biet swallowDistance nen dung 0 (teo het o dung tam mom).
    /// </summary>
    public void Pull(Vector3 target, float targetSpeed, float accel)
    {
        Pull(target, targetSpeed, accel, 0f);
    }

    /// <summary>
    /// SimpleSuction goi moi FixedUpdate khi item nam trong non. Item bay THANG mot duong vao mom,
    /// vua bay vua teo - va teo het DUNG LUC bi nuot.
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
    public void Pull(Vector3 mouthPos, float targetSpeed, float accel, float swallowDistance)
    {
        EnsureReferences();
        if (_state != State.Sucked) EnterSucked();

        Vector3 toMouth = mouthPos - Center;
        float distToMouth = toMouth.magnitude;
        Vector3 pullDir = distToMouth > 0.001f ? toMouth / distToMouth : Vector3.up;

        // Bay thang: van toc ramp dan toi targetSpeed theo accel (quan tinh), khong lech truc
        _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, pullDir * targetSpeed,
                                                accel * Time.fixedDeltaTime);

        // shrinkStart phai lon hon nguong nuot, khong thi InverseLerp dao dau va item phinh nguoc
        float shrinkStart = Mathf.Max(_radius * shrinkRadiusMul, swallowDistance * 2f);
        float t = Mathf.InverseLerp(swallowDistance, shrinkStart, distToMouth);   // 0 tai nguong nuot, 1 tai shrinkStart
        transform.localScale = _startScale * Mathf.Lerp(minShrink, 1f, t);
    }

    /// <summary>
    /// GIANG CO: item hon player dung 1 cap. Chi RUNG LAC TAI CHO (khong bi hut, khong di chuyen,
    /// khong nuot). Kinematic + jitter transform quanh diem neo bang Perlin noise cho tu nhien.
    /// </summary>
    public void Struggle(Vector3 mouthPos)
    {
        if (_state != State.Struggling) EnterStruggle();

        float t = Time.time * struggleFreq + _noiseSeed;
        Vector3 jitter = new Vector3(
            Mathf.PerlinNoise(t, _noiseSeed) - 0.5f,
            Mathf.PerlinNoise(_noiseSeed, t) - 0.5f,
            Mathf.PerlinNoise(t, t) - 0.5f) * (2f * struggleShake);
        transform.position = _anchor + jitter;

        if (struggleTilt > 0.01f)
        {
            float rx = (Mathf.PerlinNoise(t, 1.7f) - 0.5f) * 2f * struggleTilt;
            float rz = (Mathf.PerlinNoise(1.7f, t) - 0.5f) * 2f * struggleTilt;
            transform.rotation = _anchorRot * Quaternion.Euler(rx, 0f, rz);
        }
    }

    /// <summary>
    /// Het bi hut/giang co: bat lai trong luc, tra ve kich thuoc goc, tu roi xuong. Sau sleepDelay ngu.
    ///
    /// CHI CHU moi tha duoc. Con khac goi vao day khong co tac dung - neu khong thi con A vua di
    /// ngang qua het tam la tha luon cai item con B dang keo do dang.
    /// </summary>
    public void Release(SimpleSuction by)
    {
        if (by != null && _owner != null && _owner != by) return;

        _owner = null;
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
    /// Xu ly va cham: neu cham vao PLAYER (co SimpleSuction) thi bao no an minh (van theo cap).
    /// Cham vat khac / mat dat thi lo phan ngu-thuc: dang ngu bi dung -> thuc + dem gio ngu lai.
    /// </summary>
    private void Contact(Collider other)
    {
        if (_consumed) return;

        SimpleSuction suction = other.GetComponentInParent<SimpleSuction>();
        if (suction != null)
        {
            suction.EatByContact(this);   // du cap thi bi nuot; qua cap thi khong
            if (_consumed) return;
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
        _state = State.Sucked;
        _rb.isKinematic = false;   // VAT LY, khong ngu
        _rb.useGravity = false;    // bay thang vao mieng, khong bi trong luc keo xuong
        _rb.WakeUp();
    }

    private void EnterStruggle()
    {
        _state = State.Struggling;
        _rb.isKinematic = true;    // rung tai cho bang transform, khong cho physics keo di
        _anchor = transform.position;
        _anchorRot = transform.rotation;
    }

    private void EnterSleep()
    {
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
