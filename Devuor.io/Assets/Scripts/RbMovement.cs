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
    private bool _yLocked;

    /// <summary>Da cham dat va khoa truc Y chua.</summary>
    public bool IsYLocked { get { return _yLocked; } }

    public float Speed
    {
        get { return _speed; }
        set { _speed = value > 0f ? value : 0f; }
    }

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
    /// Cham mat dat lan dau -> khoa truc Y vinh vien. Va cham voi ITEM khong tinh la dat
    /// (item bay vao mom van co the co contact normal huong len).
    /// </summary>
    private void TryLockY(Collision collision)
    {
        if (_yLocked || !_lockYOnGround) return;
        if (collision.rigidbody != null && collision.rigidbody.GetComponent<PhysicsDevourable>() != null) return;

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

        Vector3 velocity = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(_planarVelocity.x, velocity.y, _planarVelocity.z);
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
