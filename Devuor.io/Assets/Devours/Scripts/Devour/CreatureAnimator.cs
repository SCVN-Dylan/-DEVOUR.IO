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

    [Tooltip("De doc VAI trong tran (ke hut / nan nhan). De trong = tu tim Creature tren object nay")]
    [SerializeField] private Creature _creature;

    [Header("Toc do animation theo VAI")]
    [Tooltip("Con dang DI HUT (level cao hon): anim CHAM lai cho nang ne, i ach - khop voi viec\n" +
             "no cung bi giam toc do di chuyen manh nhat (attackerSlow)")]
    [Range(0.1f, 3f)] public float attackerAnimSpeed = 0.7f;

    [Tooltip("Con dang BI HUT: anim NHANH len trong khi than lai di cham - chinh cai nghich ly do\n" +
             "tao ra cam giac VUNG VAY, quay cuong ma khong thoat duoc.\n" +
             "Chi ap khi thanh ghi con; het thanh la ve binh thuong cung luc voi toc do")]
    [Range(0.1f, 3f)] public float victimAnimSpeed = 1.6f;

    [Tooltip("BAT: con dang di hut cung bat animation 'RunSucking' nhu luc hut item.\n" +
             "TAT: chi item moi kich hoat anim do, danh nhau thi van chay/dung binh thuong")]
    public bool combatDrivesSuckAnim = true;

    private static readonly int RunSuckingHash = Animator.StringToHash("RunSucking");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private bool _lastRun;
    private bool _lastSucking;
    private float _lastSpeed = -1f;
    private bool _first = true;

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake() { AutoFill(); }

    private void AutoFill()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        if (_movement == null) _movement = GetComponent<RbMovement>();
        if (_suction == null) _suction = GetComponent<SimpleSuction>();
        if (_creature == null) _creature = GetComponent<Creature>();
    }

    void Update()
    {
        if (_animator == null) return;

        bool attacker = _creature != null && _creature.IsAttackerRole;
        bool victim = _creature != null && _creature.IsVictimRole;

        bool sucking = (_suction != null && _suction.HasItemsInRange)
                    || (combatDrivesSuckAnim && attacker);
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

        ApplyRoleSpeed(attacker, victim);
        _first = false;
    }

    /// <summary>
    /// TOC DO ANIMATION theo vai - nguoc chieu voi toc do DI CHUYEN, va do la co y:
    ///
    ///   ke hut  : than cham (0.4) + anim cham (0.7)  -> nang ne, i ach, dang ghi mot cai gi do
    ///   nan nhan: than cham (0.5) + anim NHANH (1.6) -> chan quay tit ma nguoi khong nhuc nhich,
    ///             dung cam giac dang vung vay de thoat
    ///
    /// Nan nhan het thanh ghi thi ca toc do lan anim cung ve binh thuong mot luc - IsVictimRole
    /// van dung nhung ta doc them Struggle de hai thu khong lech nhau mot nhip.
    ///
    /// Ghi vao Animator.speed chu khong them tham so vao Animator Controller: controller hien chi
    /// co 2 bool va 3 state, them mot float SpeedMultiplier phai sua tay tung state moi an.
    /// </summary>
    private void ApplyRoleSpeed(bool attacker, bool victim)
    {
        float speed = 1f;
        if (attacker) speed = attackerAnimSpeed;
        else if (victim && _creature.Struggle > 0f) speed = victimAnimSpeed;

        // Gate y het hai co tren: dung im ma van ghi Animator.speed moi frame la lang phi
        if (!_first && Mathf.Approximately(speed, _lastSpeed)) return;

        _animator.speed = speed;
        _lastSpeed = speed;
    }
}
