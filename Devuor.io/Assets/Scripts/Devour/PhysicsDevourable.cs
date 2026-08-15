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
    [Tooltip("Khi dang bi hut, thu nho con bao nhieu luc cham mieng (0.12 = con 12%)")]
    [Range(0.01f, 1f)] public float minShrink = 0.12f;

    [Tooltip("Bat dau thu nho tu khoang cach = ban kinh item * so nay. Vat cang TO nho tu cang xa\n" +
             "(fix vat level cao khong kip co lai vi bi an tu xa)")]
    public float shrinkRadiusMul = 2.5f;

    [Header("Bay vao spiral helix")]
    [Tooltip("BAT: item bay theo DUONG XOAN OC 3D hoi tu vao mom (helix). TAT: bay thang.")]
    public bool useHelixSpiral = true;

    [Tooltip("Ban kinh spiral o diem start = khoang cach * he so nay.\n" +
             "0.5 = spiral rong bang 50% khoang cach item->mom")]
    [Range(0.1f, 1f)] public float helixRadiusFactor = 0.4f;

    [Tooltip("Zo cua helix: bao nhieu vong xoan trong 1 don vi khoang cach. 2 = 2 vong tren 1 don vi")]
    [Range(0.5f, 5f)] public float helixPitch = 1.5f;

    [Tooltip("Item xoay tit quanh truc spiral (do/giay). Cao = xoay nhanh")]
    public float helixSpin = 1080f;

    [Tooltip("Gan mom hon khoang nay thi fade spiral -> bay thang vao mom")]
    public float helixFadeDistance = 1.0f;

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

    /// <summary>Tam thuc te (tam bounds) de suction keo cho dung, khong lech theo pivot.</summary>
    public Vector3 Center { get { return transform.TransformPoint(_centerLocal); } }

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
    private float _swirlSign;
    private float _helixPhaseOffset;   // random phase offset moi item cho spiral khong dong pha
    private float _spiralAngle;
    private Collider[] _cols;
    private Collider[] _ignoredCols;   // collider player dang duoc bo qua va cham
    private bool _playerIgnored;
    private Tween _swallowTween;

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
        _helixPhaseOffset = Random.value * Mathf.PI * 2f;
    }

    private void EnsureReferences()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        EnterSleep();
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
        if (ignore == _playerIgnored) return;
        Collider[] target = ignore ? playerCols : _ignoredCols;
        if (target == null) { _playerIgnored = ignore; return; }

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

        _ignoredCols = ignore ? playerCols : null;
        _playerIgnored = ignore;
    }

    /// <summary>
    /// Overload tuong thich cu.
    /// </summary>
    public void Pull(Vector3 target, float targetSpeed, float accel)
    {
        Pull(target, target, 75f, targetSpeed, accel);
    }

    /// <summary>
    /// SimpleSuction goi moi frame khi item nam trong non.
    /// Keo bang VAT LY: dat van toc huong ve mieng + (neu useHelixSpiral) logic SPIRAL HELIX HOI TU TRONG VUNG HUT.
    /// Item bay theo duong xoan oc 3D thu nho va giu CHUAN TRONG VUNG NON HUT phia tren mat dat.
    /// </summary>
    public void Pull(Vector3 mouthPos, Vector3 originPos, float coneAngleDeg, float targetSpeed, float accel)
    {
        EnsureReferences();
        if (_state != State.Sucked) EnterSucked();

        Vector3 center = Center;

        // Vector huong ve mieng
        Vector3 toMouth = mouthPos - center;
        float distToMouth = toMouth.magnitude;
        Vector3 pullDir = distToMouth > 0.001f ? toMouth / distToMouth : Vector3.up;

        Vector3 desiredVel = pullDir * targetSpeed;

        if (useHelixSpiral && distToMouth > 0.05f)
        {
            _spiralAngle += (helixPitch * 360f) * Mathf.Deg2Rad * Time.fixedDeltaTime;

            // Tinh khoang cach XZ toi goc quat hut (chan player)
            Vector3 originXZ = new Vector3(originPos.x, 0f, originPos.z);
            Vector3 centerXZ = new Vector3(center.x, 0f, center.z);
            float distXZ = Vector3.Distance(originXZ, centerXZ);

            // Gioi han ban kinh xoan oc: phai luon nam TRONG VUNG NON HUT (cone)
            float halfAngleRad = (coneAngleDeg * 0.5f) * Mathf.Deg2Rad;
            float maxConeRadius = distXZ * Mathf.Sin(halfAngleRad);

            // Ban kinh spiral duoc khong che = maxConeRadius * helixRadiusFactor (vd 40-50% do rong vung hut)
            float spiralRadius = Mathf.Min(maxConeRadius * helixRadiusFactor, distToMouth * 0.4f);

            if (spiralRadius > 0.01f)
            {
                // Lay 2 truc vuong goc voi pullDir
                Vector3 right = Vector3.Cross(Vector3.up, pullDir).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                Vector3 upPerp = Vector3.Cross(pullDir, right).normalized;

                float angle = _spiralAngle * _swirlSign + _helixPhaseOffset;
                Vector3 tangentDir = Mathf.Cos(angle) * right + Mathf.Sin(angle) * upPerp;

                // Van toc quy dao xoay quanh truc pull
                float tangentialSpeed = targetSpeed * (spiralRadius / Mathf.Max(0.1f, distXZ + 0.2f)) * 2f;
                tangentialSpeed = Mathf.Clamp(tangentialSpeed, 0.3f, targetSpeed * 0.9f);

                desiredVel += tangentDir * tangentialSpeed;
            }

            // Xoay vat quanh truc de tao hieu ung xoay tit
            if (helixSpin > 0.1f)
            {
                _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, pullDir * (_swirlSign * helixSpin * Mathf.Deg2Rad), 0.15f);
            }
        }

        _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, desiredVel, accel * Time.fixedDeltaTime);

        // Thu nho vat khi lai gan mieng
        float shrinkStart = _radius * shrinkRadiusMul;
        float f = shrinkStart > 0.01f
            ? Mathf.Lerp(minShrink, 1f, Mathf.Clamp01(distToMouth / shrinkStart))
            : 1f;
        transform.localScale = _startScale * f;
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

    /// <summary>Het bi hut/giang co: bat lai trong luc, tra ve kich thuoc goc, tu roi xuong. Sau sleepDelay ngu.</summary>
    public void Release()
    {
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
        if (onDevoured != null) onDevoured.Invoke();

        if (swallowDuration > 0f && isActiveAndEnabled)
            PlaySwallow(swallowTarget);
        else
            Destroy(gameObject);
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
            .OnComplete(() => { if (this != null) Destroy(gameObject); });
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
        _spiralAngle = 0f;
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
