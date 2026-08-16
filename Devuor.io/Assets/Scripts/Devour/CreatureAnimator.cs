using UnityEngine;

/// <summary>
/// Bat animation theo trang thai cua sinh vat - DUNG CHUNG cho nguoi choi va AI.
///
/// Truoc day logic nay nam trong PlayerController. Ma PlayerController thi doc joystick va ban
/// phim, bot khong dung duoc nen phai tat di - tat xong la bot chay khap map trong tu the dung
/// im. Tach ra day thi ai gan cung co animation, khong dinh gi toi nguon dieu khien.
///
///   co item trong vung hut -> RunSucking
///   dang di chuyen         -> Run
///   con lai                -> Idle (tat ca hai co)
/// </summary>
[DisallowMultipleComponent]
public class CreatureAnimator : MonoBehaviour
{
    [Header("Tham chieu (keo vao Inspector)")]
    [Tooltip("De trong = tu tim Animator dau tien trong con (thuong nam tren object 'Graphic')")]
    [SerializeField] private Animator _animator;

    [SerializeField] private RbMovement _movement;
    [SerializeField] private SimpleSuction _suction;

    private static readonly int RunSuckingHash = Animator.StringToHash("RunSucking");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private bool _lastRun;
    private bool _lastSucking;
    private bool _first = true;

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake() { AutoFill(); }

    private void AutoFill()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        if (_movement == null) _movement = GetComponent<RbMovement>();
        if (_suction == null) _suction = GetComponent<SimpleSuction>();
    }

    void Update()
    {
        if (_animator == null) return;

        bool sucking = _suction != null && _suction.HasItemsInRange;
        bool run = !sucking && _movement != null && _movement.IsMoving;

        // Chi goi SetBool khi trang thai THUC SU DOI. Ban cu goi moi frame; voi 4 con x 2 co x
        // 60fps la 480 lan ghi vao animator moi giay ma khong doi gi ca.
        if (_first || sucking != _lastSucking)
        {
            _animator.SetBool(RunSuckingHash, sucking);
            _lastSucking = sucking;
        }
        if (_first || run != _lastRun)
        {
            _animator.SetBool(RunHash, run);
            _lastRun = run;
        }
        _first = false;
    }
}
