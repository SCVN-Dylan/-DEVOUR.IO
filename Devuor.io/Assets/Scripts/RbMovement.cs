using UnityEngine;

/// <summary>
/// Ap luc di chuyen len Rigidbody. Nhan huong da normalize tu ben ngoai qua SetDir.
///
/// Nguyen tac giu cho muot:
/// - Chi gan velocity trong FixedUpdate, khong cong don, khong drift
/// - Huong luon la unit vector nen duong cheo khong nhanh hon truc thang
/// - Giu nguyen velocity.y de trong luc va va cham hoat dong binh thuong
/// - Khoa toan bo xoay vat ly, huong nhin do code dat
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RbMovement : MonoBehaviour
{
    [Header("Rigidbody")]
    [SerializeField] private Rigidbody _rb;

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
