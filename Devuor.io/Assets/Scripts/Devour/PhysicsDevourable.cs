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

    [Header("Giang co (item hon player dung 1 cap)")]
    [Tooltip("Bien do RUNG tai cho (don vi world). Item hon 1 cap chi rung, khong bi hut/di chuyen")]
    public float struggleShake = 0.08f;

    [Tooltip("Tan so rung. Cao = rung gap")]
    public float struggleFreq = 26f;

    [Tooltip("Do (degree) lac nghieng nhe khi rung. 0 = khong lac")]
    public float struggleTilt = 5f;

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
    private Vector3 _startScale = Vector3.one;
    private Vector3 _anchor;
    private Quaternion _anchorRot;
    private float _noiseSeed;
    private float _sleepAt;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _centerLocal = CalcCenterLocal();
        _startScale = transform.localScale;
        _noiseSeed = Random.value * 10f;
    }

    void Start()
    {
        EnterSleep();
    }

    /// <summary>
    /// SimpleSuction goi moi frame khi item nam trong non.
    /// Keo bang VAT LY (van dynamic, khong ngu): dat van toc huong ve mieng, item van va cham
    /// binh thuong tren duong bay. Cang gan mieng thi cang THU NHO lai.
    /// </summary>
    public void Pull(Vector3 target, float targetSpeed, float accel, float shrinkDistance)
    {
        if (_state != State.Sucked) EnterSucked();

        Vector3 to = target - Center;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : Vector3.zero;

        // QUAN TINH: van toc ramp dan toi van toc mong muon (khong dat tuc thi). Xa thi
        // targetSpeed nho -> bay cham; cang gan mieng targetSpeed cang lon -> gia toc len nhanh dan.
        Vector3 desiredVel = dir * targetSpeed;
        _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, desiredVel, accel * Time.fixedDeltaTime);

        float f = shrinkDistance > 0.01f
            ? Mathf.Lerp(minShrink, 1f, Mathf.Clamp01(dist / shrinkDistance))
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

    /// <summary>Cham mieng / cham than player: ban su kien roi bien mat.</summary>
    public void Devour()
    {
        if (_consumed) return;
        _consumed = true;
        if (onDevoured != null) onDevoured.Invoke();
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
    void OnCollisionStay(Collision collision) { Contact(collision.collider); }

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
        return has ? transform.InverseTransformPoint(b.center) : Vector3.zero;
    }
}
