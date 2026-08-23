using UnityEngine;

/// <summary>
/// Bat animation theo trang thai cua sinh vat - DUNG CHUNG cho nguoi choi va AI.
///
/// Truoc day logic nay nam trong PlayerController. Ma PlayerController thi doc joystick va ban
/// phim, bot khong dung duoc nen phai tat di - tat xong la bot chay khap map trong tu the dung
/// im. Tach ra day thi ai gan cung co animation, khong dinh gi toi nguon dieu khien.
///
/// Day ra HAI CO doc lap, khong cai nao chen cai nao:
///   Sucking = co item trong vung hut, hoac dang trong tran
///   Run     = than dang di chuyen
///
/// Controller tu ghep 4 to hop:
///   Run 0 / Suck 0 -> A_Idle          Run 1 / Suck 0 -> A_Running
///   Run 0 / Suck 1 -> A_IdleSucking   Run 1 / Suck 1 -> A_RunSucking
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

    [Tooltip("BAT: dang TRONG TRAN la ha mieng (co 'Sucking') nhu luc hut item - KHONG phan vai,\n" +
             "ca ke hut lan nan nhan deu ha, vi thuc te ca hai deu dang hut nhau.\n" +
             "TAT: chi item moi kich hoat anim do, danh nhau thi van chay/dung binh thuong")]
    public bool combatDrivesSuckAnim = true;

    private static readonly int SuckingHash = Animator.StringToHash("Sucking");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private bool _lastRun;
    private bool _lastSucking;
    private SoundHandle _suckLoop;   // tieng hut dang keu. Rong = khong keu
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

        // DANG TRONG TRAN la ha mieng, KHONG PHAN VAI.
        //
        // Ban truoc chi ke hut (level cao hon) moi ha mieng. Nhung ve mat co che thi CA HAI con
        // deu dang hut nhau that - ca hai deu goi DrainCreatures len nhau, ca hai deu tut XP.
        // Vai tro (attacker/victim) chi de chia CAM GIAC ke tren ke duoi, khong phai de quyet
        // ai dang hut. Nen de nan nhan ngam mieng trong khi no van dang rut XP cua doi thu la
        // hinh anh noi doi.
        //
        // InCombat gom dung ca hai vai (IsAttackerRole | IsVictimRole) va da co san do TRE
        // drainMemory, nen mieng khong bi dong-mo giat cuc khi hai con luot qua nhau.
        bool inCombat = _creature != null && _creature.InCombat;

        bool sucking = (_suction != null && _suction.HasItemsInRange)
                    || (combatDrivesSuckAnim && inCombat);
        // KHONG con '!sucking &&' o day. Ep Run ve false luc dang hut thi hai to hop (hut-dung-yen
        // va hut-dang-chay) doi ra CUNG mot cap co, va A_IdleSucking khong bao gio co duong vao -
        // clip nam trong controller ca thang khong ai goi toi. Hai co gio doc lap hoan toan, viec
        // ghep chung thanh state la cua controller.
        bool run = _movement != null && _movement.IsMoving;

        // Chi goi SetBool khi trang thai THUC SU DOI. Ban cu goi moi frame; voi 4 con x 2 co x
        // 60fps la 480 lan ghi vao animator moi giay ma khong doi gi ca.
        if (_first || sucking != _lastSucking)
        {
            _animator.SetBool(SuckingHash, sucking);
            _lastSucking = sucking;
            // UpdateSuckLoop(sucking);   // tieng hut di CUNG NHIP voi anim, khong co dong ho rieng
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
    /// Than bi tat (nguoi choi bi nuot -> SetActive(false)) thi tieng hut phai tat theo.
    /// Khong co ham nay thi tieng do keu den het van: object da tat, Update khong con chay, khong
    /// ai con goi Stop cho no nua.
    /// </summary>
    void OnDisable()
    {
        if (SoundManager.HasInstance) SoundManager.Instance.Stop(_suckLoop);
        _suckLoop = SoundHandle.None;

        _lastSucking = false;
        _first = true;      // bat lai thi ghi lai ca hai co tu dau
    }

    /// <summary>
    /// TIENG HUT chay dung bang nhip voi anim hut: bat luc anim ha mieng, tat luc anim ngam lai.
    /// Goi tu dung cho SetBool nen khong bao gio lech nhau.
    ///
    /// CHI NGUOI CHOI. He am thanh dang la 2D (khong theo khoang cach), nen 8 bot cung loop mot
    /// tieng hut se thanh mot lop on nen deu deu suot van, ma khong con nao trong so do la minh.
    /// </summary>
    private void UpdateSuckLoop(bool sucking)
    {
        if (_creature == null || !_creature.isPlayer || !SoundManager.HasInstance) return;

        if (sucking)
        {
            if (!SoundManager.Instance.IsPlaying(_suckLoop))
                _suckLoop = SoundManager.Instance.PlayLoop(SoundManager.Sfx.Sucking);
        }
        else
        {
            SoundManager.Instance.Stop(_suckLoop);
            _suckLoop = SoundHandle.None;
        }
    }

    /// <summary>
    /// TOC DO ANIMATION theo vai - nguoc chieu voi toc do DI CHUYEN, va do la co y:
    ///
    ///   ke hut  : than cham (0.4) + anim cham (0.7)  -> nang ne, i ach, dang ghi mot cai gi do
    ///   nan nhan: than cham (0.5) + anim NHANH (1.6) -> chan quay tit ma nguoi khong nhuc nhich,
    ///             dung cam giac dang vung vay de thoat
    ///
    /// Nan nhan ra khoi non thi ca toc do lan anim cung ve binh thuong mot luc - IsVictimRole
    /// van dung nhung ta doc them IsBeingDrained de hai thu khong lech nhau mot nhip. KHONG doc
    /// thanh ghi nua: thanh gio do khoang cach level, no dung yen gan nhu suot pha hut.
    ///
    /// Ghi vao Animator.speed chu khong them tham so vao Animator Controller: them mot float
    /// SpeedMultiplier thi phai vao sua tay TUNG state moi an, con Animator.speed thi trum het.
    /// </summary>
    private void ApplyRoleSpeed(bool attacker, bool victim)
    {
        float speed = 1f;
        if (attacker) speed = attackerAnimSpeed;
        else if (victim && _creature.IsBeingDrained) speed = victimAnimSpeed;

        // Gate y het hai co tren: dung im ma van ghi Animator.speed moi frame la lang phi
        if (!_first && Mathf.Approximately(speed, _lastSpeed)) return;

        _animator.speed = speed;
        _lastSpeed = speed;
    }
}
