using System.Collections.Generic;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Zoom camera theo CAP DO, CONG DON qua tung moc (khong phai set tuyet doi).
///
/// CHI CHAY CAMERA ORTHOGRAPHIC. Moi con so o day - baseSize, maxSize, addPerLevel va cot
/// 'zoomAdd' trong SuctionConfig.levelSteps - deu la DON VI WORLD cua orthographicSize. Go bao
/// nhieu la ra bay nhieu, khong quy doi qua bat cu thang do nao.
///
/// Ban truoc tinh duong cong bang DO FOV roi anh xa sang orthographicSize. Cach do de lai hai cai
/// bay rat kho lan ra:
///   1. Con so trong Inspector khong co nghia truc tiep: 'zoomAdd = 50' thuc ra chi la ~6 don vi
///      world, vi 159 do duoc nen vao khoang 19 don vi.
///   2. Tran 179 (gioi han cung cua fieldOfView trong Unity) bi ap cho ca camera ortho - dat
///      maxZoom = 500 thi Inspector nhan, file serialize ra 500, nhung moi phep tinh ben trong
///      deu doc thanh 179 va camera dung zoom tu Lv150, khong mot dong loi nao.
/// Gio khong con thang do trung gian nen ca hai bay do khong con cho ton tai.
///
/// KHONG CO Update: size chi duoc tinh lai khi SimpleSuction goi ApplyForLevel() luc len level.
///
/// Ho tro 2 che do / ket hop:
/// 1. zoomEveryLevel: Cong 'addPerLevel' cho moi level.
/// 2. useSteps: Cong 'zoomAdd' khi dat cac moc trong SuctionConfig.levelSteps.
/// 3. skipAddPerLevelOnStep: Bat ca 2 thi level trung moc chi an 'zoomAdd', khong cong addPerLevel.
/// 4. zoomOnlyAfterLastStep: Chi cong 'addPerLevel' cho level VUOT MOC CUOI - vung ma than van to
///    deu ma khong con moc nao de day camera ra.
///
/// CHI CON MOT diem dung: maxSize - tran do chinh minh dat.
///
/// Truoc day co diem dung thu hai: SimpleSuction.ZoomLevel bi dong bang khi than cham maxScale,
/// y la than dung to thi camera cung phai dung zoom ra. Da BO. Doi lai: qua nguong do than khong
/// to them nua ma khung van rong ra, nen nhan vat se teo dan tren man hinh - can maxSize hoac
/// bang moc de tu chan.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraLevelZoom : MonoBehaviour
{
    [Tooltip("De trong = tu tim SimpleSuction trong scene.\n" +
             "BAT BUOC co de lay danh sach moc - moc khai bao ben SuctionConfig.levelSteps")]
    public SimpleSuction player;

    [Tooltip("De trong = lay Camera tren chinh object nay")]
    public Camera cam;

    [Tooltip("CAMERA CINEMACHINE dang lai khung hinh. Keo CM_Player vao day.\n\n" +
             "VI SAO BAT BUOC KHI DUNG CINEMACHINE: CinemachineBrain GHI DE Camera.orthographicSize\n" +
             "moi LateUpdate tu lens cua vcam dang hoat dong. Ghi thang vao Camera nhu ban cu thi bi\n" +
             "xoa ngay frame do - zoom theo level CHET IM LANG, khong loi, khong warning, chi la\n" +
             "camera khong zoom nua.\n\n" +
             "DE TRONG thi component tu quay ve ghi thang vao Camera nhu cu. Do la duong danh cho\n" +
             "scene TEST (ItemLevelTestBuilder tu them component nay len mot Camera tran, khong co\n" +
             "Cinemachine) - de trong o do van chay dung nhu truoc.")]
    [SerializeField] private CinemachineCamera _vcam;

    [Header("Kich thuoc khung (orthographicSize)")]
    [Min(0.01f)]
    [Tooltip("Co khung luc CHUA qua moc nao (Lv1). Cac moc cong don len tren so nay")]
    public float baseSize = 5f;

    [Min(0.01f)]
    [Tooltip("TRAN: cong don toi day thi DUNG, len bao nhieu level nua cung khong zoom them.\n" +
             "Muon dung het bang moc thi de so nay >= baseSize + tong zoomAdd cua levelSteps")]
    public float maxSize = 24f;

    [Header("Zoom theo moc (Steps)")]
    [Tooltip("Co bat/tat su dung danh sach moc.\n" +
             "Danh sach nam ben SuctionConfig.levelSteps (cot 'zoomAdd') - khai bao mot lan cho ca\n" +
             "scale lan camera, doi moc khong phai sua hai noi")]
    public bool useSteps = true;

    [Header("Zoom nho moi cap (Add Per Level)")]
    [Tooltip("BAT: moi lan len cap deu CONG THEM addPerLevel vao co khung.\nTAT: khong cong per level")]
    public bool zoomEveryLevel = true;

    [Tooltip("Co khung cong them cho MOI cap da len, don vi world.\n" +
             "Vi du 0.05 = len 20 cap thi khung rong them 1 don vi")]
    public float addPerLevel = 0.05f;

    [Header("Ket hop Steps & Add Per Level")]
    [Tooltip("BAT: level co trong Steps thi lay 'zoomAdd' cua Step va KHONG cong addPerLevel cho\n" +
             "level do.\nTAT: cong ca hai tai level co Step")]
    public bool skipAddPerLevelOnStep = true;

    [Tooltip("BAT: CHI cong addPerLevel cho nhung level VUOT QUA MOC CUOI CUNG trong levelSteps.\n" +
             "Giua cac moc thi camera van dung im theo bac thang nhu cu.\n\n" +
             "VI SAO CAN: qua moc cuoi la het moc, camera dong bang - nhung THAN VAN TO DEU theo\n" +
             "scalePerLevel. Do that voi bang hien tai: tu Lv2000 (than 42.1u, khung 91.5) den luc\n" +
             "than cham maxScale o Lv4598 (than 58.0u), khung KHONG doi mot don vi nao - nhan vat\n" +
             "phinh tu 23% len 31.7% chieu cao man hinh.\n\n" +
             "BAT RIENG CAI NAY LA DU, khong can bat 'Zoom Every Level'. Bat ca hai thi cai nay\n" +
             "thang: van chi cong sau moc cuoi.")]
    public bool zoomOnlyAfterLastStep = false;

    [Header("Nay nguoc chieu khi vuot moc")]
    // Cong tac BAT/TAT nam ben SimpleSuction.popAffectsCamera, khong dat o day: de ca hai noi
    // cung co mot cong tac thi tat mot cai ma van nay, khong ai hieu tai sao.
    // Ben do tat thi no truyen steppedUp = false xuong day, coi nhu khong co moc nao.

    [Tooltip("BAT: CHI nay o moc co isEvolution (moc doi hinh dang) - de danh cu nay cho nhung lan\n" +
             "dang gia, con moc thuong thi doi size im lang.\n" +
             "TAT: nay o MOI moc.\n\n" +
             "Bang hien tai co 6 moc, trong do 4 la tien hoa (Lv10/50/250/500).")]
    public bool punchOnlyOnEvolution = false;

    [Range(0f, 0.4f)]
    [Tooltip("Vuot moc thi khung ZOOM VAO truoc bao nhieu roi moi bung ra co moi (0.12 = thu vao 12%).\n" +
             "Cung nhip 'lay da' voi cu pop cua than - khong co no thi len moc chi la mot cai truot\n" +
             "size, mat khong doc ra la su kien.")]
    public float stepPunch = 0.12f;

    [Tooltip("Thoi gian nhip zoom VAO (giay). Nen ngan hon tweenDuration de nen nhanh - bung cham")]
    public float stepPunchTime = 0.1f;

    [Header("Muot ma (DOTween)")]
    [Tooltip("Thoi gian doi co khung sang gia tri moi (giay). 0 = doi ngay tuc thi")]
    public float tweenDuration = 0.3f;

    [Tooltip("Kieu easing khi doi co khung")]
    public Ease tweenEase = Ease.OutQuad;

    private Tween _tween;

    /// <summary>
    /// CO KHUNG dang hien hanh. Doc tu vcam neu co, khong thi doc thang Camera.
    ///
    /// Phai di qua mot cua duy nhat: neu cho noi doc noi ghi khac nhau thi cu 'lay da' se tinh do
    /// nen tu mot con so ma no khong he dieu khien.
    /// </summary>
    private float CurrentSize
    {
        get
        {
            if (_vcam != null) return _vcam.Lens.OrthographicSize;
            return cam != null ? cam.orthographicSize : baseSize;
        }
    }

    /// <summary>Ghi co khung. Uu tien vcam - xem tooltip cua _vcam ve ly do.</summary>
    private void WriteSize(float value)
    {
        if (_vcam != null) _vcam.Lens.OrthographicSize = value;
        else if (cam != null) cam.orthographicSize = value;
    }

    void OnEnable()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (player == null) player = ResolvePlayer();

        if (cam != null && !cam.orthographic)
            Debug.LogWarning("[CameraLevelZoom] Camera dang o che do Perspective. Component nay chi " +
                             "dieu khien orthographicSize - Unity se bo qua hoan toan. Bat " +
                             "Projection = Orthographic tren camera.", this);

        ApplyForLevel(player != null ? player.ZoomLevel : 1, true);
    }

    void OnDisable() { KillTween(); }
    void OnDestroy() { KillTween(); }

    /// <summary>
    /// Tim SimpleSuction cua NGUOI CHOI.
    ///
    /// FindAnyObjectByType tra ve con DAU TIEN no gap, khong he uu tien ai - scene co them 3 con
    /// AI thi camera hoan toan co the di zoom theo mot con bot ma khong bao loi gi. Nen uu tien
    /// hoi GameManager.Player (con co co isPlayer), het cach moi quay ve kieu cu cho scene test
    /// chua gan Creature.
    ///
    /// Binh thuong khong toi luot ham nay chay: SimpleSuction cua nguoi choi tu gan minh vao
    /// camera ngay trong Awake (som hon OnEnable nay).
    /// </summary>
    private SimpleSuction ResolvePlayer()
    {
        if (GameManager.HasInstance)
        {
            Creature p = GameManager.Instance.Player;
            if (p != null && p.Suction != null) return p.Suction;
        }
        return Object.FindAnyObjectByType<SimpleSuction>();
    }

    /// <summary>
    /// SimpleSuction goi ham nay MOI KHI LEN LEVEL (khong co poll). Tinh co khung dich 1 lan roi
    /// tween toi do. 'instant' = bo tween, dat thang (dung luc khoi tao / trong Edit mode).
    /// </summary>
    public void ApplyForLevel(int level, bool instant = false, bool steppedUp = false, bool evolved = false)
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        float target = TargetSizeFor(level);
        KillTween();

        if (instant || tweenDuration <= 0f || !Application.isPlaying)
        {
            WriteSize(target);
            return;
        }

        // VUOT MOC: khung hinh cung LAY DA - zoom VAO mot nhip roi moi bung ra co moi. Cung
        // nguyen tac anticipation voi cu pop cua than: khong co nhip nen truoc thi cu bung sau
        // chi la mot cai truot size, khong doc ra la su kien.
        bool wantPunch = stepPunch > 0.001f
                      && (punchOnlyOnEvolution ? evolved : steppedUp);

        if (wantPunch)
        {
            float dip = Mathf.Max(0.01f, CurrentSize * (1f - stepPunch));

            // Nhip BUNG bat dau tu DIP chu khong tu CurrentSize: DOVirtual.Float chot moc dau ngay
            // luc DUNG sequence, con cam.DOOrthoSize cu thi chot luc tween BAT DAU CHAY. Truyen
            // thang 'dip' vao la khoi phu thuoc vao khac biet do - nhip hai noi lien mach, khong
            // co cu giat nguoc ve co cu giua chung.
            _tween = DOTween.Sequence()
                .Append(DOVirtual.Float(CurrentSize, dip, stepPunchTime, WriteSize).SetEase(Ease.OutQuad))
                .Append(DOVirtual.Float(dip, target, tweenDuration, WriteSize).SetEase(tweenEase))
                .SetUpdate(true);   // hitstop ha timeScale - cu nay phai chay theo gio that
            return;
        }

        _tween = DOVirtual.Float(CurrentSize, target, tweenDuration, WriteSize).SetEase(tweenEase);
    }

    /// <summary>Co khung dang huong toi, theo level hien hanh cua player.</summary>
    public float TargetZoom()
    {
        return TargetSizeFor(player != null ? player.ZoomLevel : 1);
    }

    /// <summary>
    /// CO KHUNG (orthographicSize) muc tieu cho mot level bat ky. O(so moc), khong cap phat.
    ///
    ///   size = baseSize
    ///        + addPerLevel x (so level da len, TRU cac level trung moc neu skipAddPerLevelOnStep)
    ///        + tong 'zoomAdd' cua moi moc da qua
    ///   roi kep lai trong [0.01 .. maxSize]
    ///
    /// Nhieu Step cung mot 'level' thi 'zoomAdd' van cong don het, nhung chi tinh la MOT lan
    /// trung moc.
    /// </summary>
    public float TargetSizeFor(int level)
    {
        float size = baseSize;
        int levelsGained = Mathf.Max(0, level - 1);

        float stepAdd = 0f;
        int distinctStepLevels = 0;
        int lastStepLevel = 0;      // moc CAO NHAT trong bang, ke ca chua toi

        List<LevelStep> steps = (useSteps && player != null) ? player.LevelSteps : null;
        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                LevelStep s = steps[i];
                if (s == null || s.level < 2) continue;

                // Ghi nhan moc cao nhat TRUOC khi loc theo level hien tai: zoomOnlyAfterLastStep
                // can biet moc cuoi cua CA BANG, khong phai moc cuoi da di qua.
                if (s.zoomAdd != 0f && s.level > lastStepLevel) lastStepLevel = s.level;

                if (s.level > level) continue;
                if (s.zoomAdd == 0f) continue;   // moc chi dong toi scale -> khong tinh la moc cua camera

                stepAdd += s.zoomAdd;

                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    LevelStep q = steps[j];
                    if (q != null && q.zoomAdd != 0f && q.level == s.level && q.level >= 2 && q.level <= level) { duplicate = true; break; }
                }
                if (!duplicate) distinctStepLevels++;
            }
        }

        if (zoomEveryLevel || zoomOnlyAfterLastStep)
        {
            int normal;
            if (zoomOnlyAfterLastStep)
            {
                // Chi dem cac level NAM TREN moc cuoi. Chua toi moc cuoi thi bang 0 - camera y het
                // bac thang cu, khong doi gi.
                normal = level - Mathf.Max(1, lastStepLevel);
            }
            else
            {
                normal = skipAddPerLevelOnStep ? levelsGained - distinctStepLevels : levelsGained;
            }
            size += addPerLevel * Mathf.Max(0, normal);
        }
        size += stepAdd;

        return Mathf.Clamp(size, 0.01f, Mathf.Max(0.01f, maxSize));
    }

    /// <summary>Da zoom het co chua (cham maxSize).</summary>
    public bool IsAtMaxZoom
    {
        get { return TargetSizeFor(player != null ? player.ZoomLevel : 1) >= Mathf.Max(0.01f, maxSize) - 0.001f; }
    }

    private void KillTween()
    {
        if (_tween != null && _tween.IsActive()) _tween.Kill();
        _tween = null;
    }
}
