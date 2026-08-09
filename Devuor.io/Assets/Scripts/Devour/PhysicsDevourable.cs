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

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _centerLocal = CalcCenterLocal();
        _startScale = transform.localScale;
        _noiseSeed = Random.value * 10f;
        _swirlSign = Random.value < 0.5f ? -1f : 1f;
        _helixPhaseOffset = Random.value * Mathf.PI * 2f;
    }

    void Start()
    {
        EnterSleep();
    }

    /// <summary>
    /// SimpleSuction goi moi frame khi item nam trong non.
    /// Keo bang VAT LY: dat van toc huong ve mieng + (neu useHelixSpiral) logic SPIRAL HELIX hoi tu.
    /// Item bay theo duong xoan oc 3D, ban kinh giam dan khi gan mieng.
    /// </summary>
    public void Pull(Vector3 target, float targetSpeed, float accel)
    {
        if (_state != State.Sucked) EnterSucked();

        Vector3 to = target - Center;
        float dist = to.magnitude;
        Vector3 axis = dist > 0.001f ? to / dist : Vector3.zero;

        Vector3 desiredVel = axis * targetSpeed;

        if (useHelixSpiral && dist > 0.05f)
        {
            float fade = Mathf.Clamp01(dist / Mathf.Max(0.01f, helixFadeDistance));

            // SPIRAL HELIX: ban kinh + phase angle -> vi tri tren helix -> vector toi do
            float radius = dist * helixRadiusFactor * fade;
            float phase = (1f - fade) * helixPitch * Mathf.PI * 2f + _helixPhaseOffset;

            // Tao 2 vector VUONG GOC voi truc spiral (up, right cua he toa do local)
            Vector3 up = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 right = Vector3.Cross(axis, up).normalized;
            up = Vector3.Cross(right, axis).normalized;

            // Vi tri tren helix circle
            Vector3 circleOffset = radius * (Mathf.Cos(phase) * right + Mathf.Sin(phase) * up);
            Vector3 helixPos = target + circleOffset;
            Vector3 toHelix = helixPos - Center;

            // Them van toc TIEP TUYEN: keo item toi vi tri helix
            desiredVel = Vector3.Lerp(axis * targetSpeed, toHelix.normalized * targetSpeed, fade * 0.8f);

            // LAM VAT XOAY TIT quanh truc spiral
            if (helixSpin > 0.1f)
                _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, axis * (_swirlSign * helixSpin * Mathf.Deg2Rad), 0.1f);
        }

        _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, desiredVel, accel * Time.fixedDeltaTime);

        // Thu nho theo CO ITEM: vat to bat dau nho tu xa hon nen kip co lai truoc khi bi an
        float shrinkStart = _radius * shrinkRadiusMul;
        float f = shrinkStart > 0.01f
            ? Mathf.Lerp(minShrink, 1f, Mathf.Clamp01(dist / shrinkStart))
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
            StartCoroutine(SwallowAnim(swallowTarget));
        else
            Destroy(gameObject);
    }

    /// <summary>Hieu ung nuot: xoay tit + teo nho lao thang vao mom roi bien mat.</summary>
    private System.Collections.IEnumerator SwallowAnim(Transform target)
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
        float t = 0f;
        while (t < swallowDuration)
        {
            t += Time.deltaTime;
            float ease = Mathf.Clamp01(t / swallowDuration);
            ease *= ease;                                                 // gia toc lao vao mom
            Vector3 tp = target != null ? target.position : startPos;
            transform.position = Vector3.Lerp(startPos, tp, ease);
            transform.localScale = startScale * (1f - ease);             // teo dan ve 0
            transform.Rotate(Vector3.up, swallowSpin * Time.deltaTime, Space.Self);   // xoay tit
            yield return null;
        }
        Destroy(gameObject);
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
