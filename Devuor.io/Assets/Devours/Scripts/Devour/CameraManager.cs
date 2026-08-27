using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// CUA DUY NHAT de doi camera. Hien tai lo mot viec: DOAN INTRO dau van.
///
///   bam Play -> CAT sang CM_Intro (khung rong nhin ca ban do)
///            -> giu 1 giay
///            -> tat CM_Intro, Brain BLEND 2 giay ve CM_Player
///            -> blend xong = "camera da ve toi player" -> tra moi thu ve binh thuong
///
/// ------------------------------------------------------------------------------------------
/// VI SAO CAT VAO NHUNG BLEND RA
/// ------------------------------------------------------------------------------------------
/// Luc bat intro, man Home van con che kin man hinh nen khong ai nhin thay cu cat. Neu de Brain
/// blend VAO thi 2 giay dau cua intro la mot canh troi giua khung player va khung rong - vo nghia,
/// va an mat dung doan dang le phai thay ca ban do.
///
/// Chieu RA thi nguoc lai: blend chinh la thu nguoi choi can thay (khung hut dan ve nhan vat).
/// Nen ham nay MUON DefaultBlend cua Brain trong dung mot frame de cat vao, roi tra lai ngay.
///
/// ------------------------------------------------------------------------------------------
/// HAI THU AN THEO DOAN INTRO
/// ------------------------------------------------------------------------------------------
/// 1. FLYER HIEN MESH (StageReveal.SetIntroReveal). Binh thuong khinh khi cau/bong bay bi an,
///    chi thay bong - vi nguoi choi Lv1 chua du hang de an chung. Trong intro thi phai thay, do
///    la thu dang gia nhat trong khung rong.
/// 2. KHOA DI CHUYEN nguoi choi. Khong khoa thi suot 3 giay intro joystick da an, nguoi choi bam
///    ma khong thay minh dau - vua mat phuong huong vua tuong game do.
///
/// Ca hai deu duoc tra ve binh thuong o CUNG mot moc: luc blend ket thuc.
/// </summary>
[DisallowMultipleComponent]
public class CameraManager : MonoBehaviour
{
    private static CameraManager _instance;

    /// <summary>Da co CameraManager trong scene chua. KHONG tu tao ra cai moi.</summary>
    public static bool HasInstance { get { return _instance != null; } }

    /// <summary>Truy cap tu ngoai. Scene khong co thi tra null - ben goi tu bo qua.</summary>
    public static CameraManager Instance { get { return _instance; } }

    [Header("Tham chieu (Reset tu dien)")]
    [Tooltip("CinemachineBrain tren Main Camera")]
    [SerializeField] private CinemachineBrain _brain;

    [Tooltip("Camera doan INTRO - khung rong nhin ca ban do. De trong = bo qua intro.")]
    [SerializeField] private CinemachineCamera _introCam;

    [Tooltip("Camera bam NGUOI CHOI - camera chinh ca van")]
    [SerializeField] private CinemachineCamera _playerCam;

    [Header("Nhip intro")]
    [Min(0f)]
    [Tooltip("Giu khung rong bao lau truoc khi bat dau keo ve player (giay)")]
    [SerializeField] private float _holdTime = 1f;

    [Min(0f)]
    [Tooltip("Thoi gian BLEND tu khung rong ve player (giay). Tong doan intro = giu + blend.")]
    [SerializeField] private float _blendTime = 2f;

    [Tooltip("BAT: khoa di chuyen nguoi choi suot doan intro, tra lai khi camera ve toi noi.")]
    [SerializeField] private bool _lockPlayerDuringIntro = true;

    [Tooltip("BAT: cho flyer hien mesh suot doan intro (xem StageReveal).")]
    [SerializeField] private bool _revealFlyersDuringIntro = true;

    [Header("Go")]
    [Tooltip("In ra Console tung moc cua doan intro kem thoi diem. Tat trong ban build.")]
    [SerializeField] private bool _log = false;

    [Min(0.1f)]
    [Tooltip("LUOI AN TOAN: cho blend toi da bao nhieu giay. Het gio ma Brain van bao dang blend\n" +
             "thi cu coi nhu xong - tha khoa ra. Khong co no thi mot loi o Cinemachine se khoa\n" +
             "nguoi choi dung im vinh vien, va khong co dau hieu nao de lan ra.")]
    [SerializeField] private float _blendTimeout = 6f;

    /// <summary>Doan intro co dang chay khong.</summary>
    public bool IntroPlaying { get; private set; }

    private Coroutine _routine;
    private System.Action _onFinished;

    /// <summary>
    /// Xoa static khi vao van moi. Bat buoc neu du an bat "Enter Play Mode Options" (tat domain
    /// reload) - luc do static khong tu reset giua hai lan chay. Cung ly do voi GameManager.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; }

    private void Reset() { AutoFill(); }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;

        AutoFill();

        // Vao scene thi CM_Player phai la camera dang song: man Home nhin thay nhan vat, va cu CAT
        // sang intro luc bam Play moi co nghia. De intro bat san thi khong con gi de "bat" ca.
        if (_introCam != null) _introCam.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Luoi an toan cho ref quen keo. Ref da co san thi khong dong toi.</summary>
    private void AutoFill()
    {
        if (_brain == null) _brain = GetComponentInChildren<CinemachineBrain>(true);
        if (_introCam == null || _playerCam == null)
        {
            foreach (CinemachineCamera c in GetComponentsInChildren<CinemachineCamera>(true))
            {
                if (c == null) continue;
                // Phan biet bang FOLLOW chu khong bang ten: camera bam player thi CO muc tieu,
                // camera intro la khung tinh nen khong. Doi ten object khong lam hong gi.
                if (c.Follow != null) { if (_playerCam == null) _playerCam = c; }
                else { if (_introCam == null) _introCam = c; }
            }
        }
    }

    /// <summary>
    /// CHAY DOAN INTRO. UIManager.StartMatch goi vao day.
    ///
    /// 'onFinished' duoc ban dung luc camera VE TOI nguoi choi - do la moc UIManager cho de bat
    /// dau dem gio va de bot ra map. Ban ca o duong BO QUA intro (thieu do nghe), nen khong ton
    /// tai nhanh nao ma van ket lai o man hinh trong.
    ///
    /// Goi lai trong luc dang chay thi khong lam gi - khong chong hai doan intro len nhau.
    /// </summary>
    public void PlayIntro(System.Action onFinished = null)
    {
        if (IntroPlaying) return;

        _onFinished = onFinished;

        // Thieu do nghe thi BO QUA intro cho gon, nhung phai chac chan khong de lai trang thai
        // khoa: bo qua ma van khoa nguoi choi la loi te nhat co the co.
        if (_brain == null || _introCam == null)
        {
            EndIntroState();
            return;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(IntroRoutine());
    }

    private void Log(string phase)
    {
        if (_log) Debug.Log("[CameraManager] t=" + Time.unscaledTime.ToString("F2") + "  " + phase, this);
    }

    private IEnumerator IntroRoutine()
    {
        IntroPlaying = true;
        Log("BAT DAU intro");

        if (_revealFlyersDuringIntro) StageReveal.SetIntroReveal(true);
        SetPlayerMovable(!_lockPlayerDuringIntro);

        // ---- CAT sang khung rong (xem ghi chu dau file ve ly do cat chu khong blend) ----
        CinemachineBlendDefinition keep = _brain.DefaultBlend;
        _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        _introCam.gameObject.SetActive(true);

        // CHO TOI KHI BRAIN THUC SU NHAN CAMERA INTRO roi moi tra DefaultBlend lai.
        //
        // KHONG dem frame. Ban dau o day la 'yield return null' (doi dung 1 frame) va no SAI: Brain
        // chay o LateUpdate voi execution order 100, so frame no can de nhan camera moi phu thuoc
        // thu tu thuc thi lan toc do may. Tra DefaultBlend lai som mot nhip la cu CAT bien thanh cu
        // BLEND 2 giay - tuc mat dung doan dang le phai thay ca ban do. Do that luc test: log bao
        // 'da CAT sang khung rong' nhung live van la CM_Player va ortho van 5.
        //
        // Bam vao TRANG THAI THAT (IsLive) thi may nhanh hay cham deu cho ra dung mot ket qua.
        float t = 0f;
        while (!CinemachineCore.IsLive(_introCam) && t < _blendTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _blendTime);
        Log("da CAT sang khung rong - ortho=" + _brain.OutputCamera.orthographicSize.ToString("F1")
            + " live=" + (_brain.ActiveVirtualCamera != null ? _brain.ActiveVirtualCamera.Name : "-"));

        // ---- GIU khung rong ----
        // WaitForSecondsRealtime: doan intro phai dai dung bang nhau du timeScale co bi ai do doi
        // (hitstop luc len moc chay o 0.05x).
        if (_holdTime > 0f) yield return new WaitForSecondsRealtime(_holdTime);

        // ---- TAT intro -> Brain tu blend ve CM_Player ----
        Log("het giu, TAT CM_Intro -> bat dau blend " + _blendTime + "s");
        _introCam.gameObject.SetActive(false);

        // CHO TOI KHI CAMERA VE HAN TOI PLAYER. Dieu kien gom ca hai ve:
        //   - con dang blend                      -> chua toi noi
        //   - CM_Player chua duoc coi la live      -> Brain chua kip bat dau blend
        // Chi hoi IsBlending thoi la hong: ngay sau SetActive(false), Brain chua chay nen
        // IsBlending con FALSE, vong lap thoat ngay va ta bao "da toi noi" trong khi camera van
        // dang o khung rong.
        float waited = 0f;
        while (waited < _blendTimeout && (_brain.IsBlending || !CinemachineCore.IsLive(_playerCam)))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Log("blend XONG sau " + waited.ToString("F2") + "s - ortho=" + _brain.OutputCamera.orthographicSize.ToString("F1")
            + " live=" + (_brain.ActiveVirtualCamera != null ? _brain.ActiveVirtualCamera.Name : "-"));
        _brain.DefaultBlend = keep;
        EndIntroState();
        Log("da tra flyer + di chuyen ve binh thuong");
        _routine = null;
    }

    /// <summary>
    /// TRA MOI THU VE BINH THUONG. Goi o duy nhat mot cho (luc blend xong) va o ca duong bo qua
    /// intro - de khong the co nhanh nao thoat ra ma con de lai khoa.
    /// </summary>
    private void EndIntroState()
    {
        IntroPlaying = false;
        StageReveal.SetIntroReveal(false);
        SetPlayerMovable(true);

        // XOA TRUOC KHI GOI. Ben nhan hoan toan co the goi lai PlayIntro ngay trong callback
        // (choi lai van chang han) - khong xoa truoc thi callback cu con nam do va ban lan hai.
        System.Action cb = _onFinished;
        _onFinished = null;
        if (cb != null) cb();
    }

    /// <summary>
    /// Khoa/mo di chuyen nguoi choi. Khong dong toi bot - chung khong co camera de lac huong.
    ///
    /// Doc qua GameManager chu khong giu ref: nguoi choi co the chua dang ky xong luc goi.
    /// </summary>
    private void SetPlayerMovable(bool movable)
    {
        if (!GameManager.HasInstance) return;

        Creature p = GameManager.Instance.Player;
        if (p == null || p.Movement == null) return;

        p.Movement.IsMovable = movable;
    }
}
