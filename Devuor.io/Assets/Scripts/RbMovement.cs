using UnityEngine;

/// <summary>
/// Ap luc di chuyen len Rigidbody. Nhan huong da normalize tu ben ngoai qua SetDir.
///
/// Nguyen tac giu cho muot:
/// - Chi gan velocity trong FixedUpdate, khong cong don, khong drift
/// - Huong luon la unit vector nen duong cheo khong nhanh hon truc thang
/// - Roi tu do cho den khi CHAM DAT, sau do KHOA CUNG truc Y (xem _lockYOnGround)
/// - Khoa toan bo xoay vat ly, huong nhin do code dat
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RbMovement : MonoBehaviour
{
    [Header("Rigidbody")]
    [SerializeField] private Rigidbody _rb;

    [Header("Khoa truc Y")]
    [Tooltip("Cham dat lan dau la KHOA CUNG truc Y. Item bay vao / va cham khong the hat nhan vat len xuong nua.\n" +
             "Tat neu sau nay can nhay hoac co dia hinh cao thap.")]
    [SerializeField] private bool _lockYOnGround = true;

    [Range(0.1f, 1f)]
    [Tooltip("Contact phai co normal.y tu nguong nay tro len moi tinh la MAT DAT (loc va cham ngang)")]
    [SerializeField] private float _groundNormalY = 0.5f;

    [Header("Speed")]
    [SerializeField] private float _speed = 8f;

    [Range(0.1f, 2f)]
    [Tooltip("HE SO CO HUU cua con nay, nhan chong len moi he so khac (AI de 0.85 = chay cham hon\n" +
             "nguoi choi 15%).\n\n" +
             "Phai la mot bien RIENG chu khong sua thang _speed: SimpleSuction ghi de toc do moi lan\n" +
             "len cap (theo co than) nen moi chinh sua truc tiep vao _speed deu bi xoa ngay sau do.")]
    [SerializeField] private float _speedHandicap = 1f;

    [Header("Rotation")]
    [Tooltip("Do/giay khi quay mat theo huong di. 0 = snap tuc thi")]
    [SerializeField] private float _turnSpeed = 0f;

    [Header("Smoothing")]
    [Tooltip("Giay de dat toc do toi da. 0 = gan velocity thang, chay/dung tuc thi")]
    [SerializeField] private float _accelerationTime = 0f;

    [Tooltip("Giay de dung han khi tha tay. 0 = dung ngay")]
    [SerializeField] private float _decelerationTime = 0f;

    private Vector3 _dir;
    private Vector3 _planarVelocity;
    private Vector3 _externalVelocity;
    private bool _yLocked;
    private float _baseSpeed;
    private bool _baseSpeedCached;

    /// <summary>Da cham dat va khoa truc Y chua.</summary>
    public bool IsYLocked { get { return _yLocked; } }

    public float Speed
    {
        get { return _speed; }
        set { _speed = value > 0f ? value : 0f; }
    }

    /// <summary>Toc do goc authored tren prefab, khong bi he so cap do ghi de.</summary>
    public float BaseSpeed { get { CacheBaseSpeed(); return _baseSpeed; } }

    /// <summary>
    /// Dat toc do = toc do GOC x he so. Goi bao nhieu lan cung ra cung ket qua, khong
    /// nhan chong len nhau (khac voi ghi thang vao Speed).
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        CacheBaseSpeed();
        _speed = _baseSpeed * Mathf.Max(0f, multiplier) * Mathf.Max(0.01f, _speedHandicap);
    }

    /// <summary>He so co huu (AI cham hon nguoi choi). Doi luc chay cung duoc.</summary>
    public float SpeedHandicap
    {
        get { return _speedHandicap; }
        set { _speedHandicap = Mathf.Max(0.01f, value); }
    }

    /// <summary>
    /// Chot toc do goc. Goi ca trong Awake lan trong SetSpeedMultiplier vi thu tu Awake giua
    /// cac component tren cung GameObject la khong dam bao - neu SimpleSuction.Awake chay truoc
    /// ma chua chot thi _baseSpeed = 0 va nhan vat dung im.
    /// </summary>
    private void CacheBaseSpeed()
    {
        if (_baseSpeedCached) return;
        _baseSpeed = _speed;
        _baseSpeedCached = true;
    }

    /// <summary>
    /// CONG THEM mot van toc tu ben ngoai cho FixedUpdate ke tiep (vd: bi con khac hut keo ve
    /// phia mom no). Tieu thu xong la tu xoa, nen ben goi phai goi lai moi frame khi con muon keo.
    ///
    /// Phai co ham nay vi Move() GHI DE thang linearVelocity moi FixedUpdate - moi luc/van toc
    /// do noi khac dat vao deu bi xoa sach ngay frame sau, AddForce cung vo dung.
    ///
    /// Cong don: hai con cung hut mot nan nhan thi luc keo cong lai chu khong phai cai sau de len
    /// cai truoc.
    /// </summary>
    public void AddExternalVelocity(Vector3 velocity)
    {
        _externalVelocity += velocity;
    }

    /// <summary>Van toc ngoai dang cho ap vao FixedUpdate ke tiep (dung de doc/debug).</summary>
    public Vector3 ExternalVelocity { get { return _externalVelocity; } }

    /// <summary>Tat de khoa cung nhan vat (cutscene, ket thuc van...).</summary>
    public bool IsMovable { get; set; } = true;

    /// <summary>Dung cho animator: dang co lenh di chuyen hay khong.</summary>
    public bool IsMoving { get { return _dir.sqrMagnitude > 0f; } }

    public Vector3 Direction { get { return _dir; } }

    void Reset()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        CacheBaseSpeed();

        // Khoa toan bo xoay vat ly: dam vao toa nha hay xe khong the lam nhan vat quay.
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        UpdateFacing();
    }

    void FixedUpdate()
    {
        Move();
    }

    void OnCollisionEnter(Collision collision) { TryLockY(collision); }
    void OnCollisionStay(Collision collision) { TryLockY(collision); }

    /// <summary>
    /// Cham mat dat lan dau -> khoa truc Y vinh vien.
    ///
    /// MOI vat co Rigidbody deu KHONG tinh la dat: dat va nha cua deu la vat tinh (khong
    /// Rigidbody), con item bay vao mom hay mot con khac dam vao suon deu co the tao contact
    /// normal huong len. Ban cu chi loai tru PhysicsDevourable nen dam vao SINH VAT khac cung
    /// bi tinh la "tiep dat".
    /// </summary>
    private void TryLockY(Collision collision)
    {
        if (_yLocked || !_lockYOnGround) return;
        if (collision.rigidbody != null) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < _groundNormalY) continue;
            LockY();
            return;
        }
    }

    /// <summary>
    /// Khoa cung truc Y bang FreezePositionY. Tu day khong con luc nao (item bay vao, vat the
    /// dam, trong luc) doi duoc do cao nhan vat nua.
    ///
    /// Khong can bu lai do cao khi nhan vat LEN CAP TO RA: pivot cua model nam ngay duoi chan
    /// nen scale to ra thi than cao len, chan van dung yen tren dat (da do: scale 5 -> mesh
    /// min.y = 0.046, khong lun). Chi rieng capsule collider thop xuong duoi mat dat mot chut,
    /// khong anh huong gi.
    /// </summary>
    private void LockY()
    {
        _yLocked = true;
        _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;
    }

    void OnDestroy()
    {
        _rb = null;
    }

    public void SetDir(Vector3 dir)
    {
        _dir = dir;
    }

    public void Move()
    {
        if (!IsMovable)
        {
            _dir = Vector3.zero;
            _planarVelocity = Vector3.zero;
            _externalVelocity = Vector3.zero;
            Vector3 stopped = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(0f, stopped.y, 0f);
            return;
        }

        Vector3 target = _dir * _speed;

        // Ca hai thoi gian = 0 thi gan thang nhu ban goc.
        // Dat > 0 neu muon truot nhe luc chay/dung.
        float rampTime = target.sqrMagnitude >= _planarVelocity.sqrMagnitude
            ? _accelerationTime
            : _decelerationTime;

        _planarVelocity = rampTime > 0.0001f
            ? Vector3.MoveTowards(_planarVelocity, target, _speed / rampTime * Time.fixedDeltaTime)
            : target;

        // Van toc ngoai CONG THEM chu khong thay the: dang chay tron ma bi hut thi van thoat duoc,
        // chi cham lai - do la cho de nguoi choi con cua giay giua.
        Vector3 velocity = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(
            _planarVelocity.x + _externalVelocity.x,
            velocity.y,
            _planarVelocity.z + _externalVelocity.z);

        _externalVelocity = Vector3.zero;   // tieu thu xong, ai con muon keo thi frame sau goi lai
    }

    /// <summary>
    /// Chay o Update chu khong phai FixedUpdate de khi dat _turnSpeed > 0
    /// thi goc quay muot theo framerate thay vi giat tung nac 50Hz.
    /// </summary>
    private void UpdateFacing()
    {
        if (_dir.sqrMagnitude <= 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(_dir);

        transform.rotation = _turnSpeed > 0f
            ? Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime)
            : look;
    }
}
