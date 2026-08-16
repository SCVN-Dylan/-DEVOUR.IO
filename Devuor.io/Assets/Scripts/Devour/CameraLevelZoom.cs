using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Zoom camera theo CAP DO, CONG DON qua tung moc (khong phai set tuyet doi).
///
/// Chay duoc CA HAI loai camera, tu nhan biet qua cam.orthographic:
///   - Perspective : dieu khien fieldOfView, don vi do.
///   - Orthographic: dieu khien orthographicSize, don vi world (baseSize..maxSize).
/// Duong cong moc (steps/addPerLevel) dung chung, chi doi hai dau mut - tune mot lan la xong.
///
/// KHONG CO Update: FOV chi duoc tinh lai khi SimpleSuction goi ApplyForLevel() luc len level.
/// Ban cu duyet tu level 2 den level hien tai + cap phat mot Dictionary MOI FRAME (o Lv150 la
/// ~9000 vong lap/giay, cang len level cang nang). Gio la O(so moc), goi 1 lan moi khi len level.
///
/// Ho tro 2 che do / ket hop:
/// 1. zoomEveryLevel: Cong 'addPerLevel' cho moi level.
/// 2. useSteps: Cong gia tri 'add' khi dat cac moc 'level' trong danh sach steps.
/// 3. skipAddPerLevelOnStep: Khi bat ca 2, tai level co trong steps se dung 'add' cua Step va KHONG cong 'addPerLevel'.
///
/// Co HAI diem dung, cai nao toi truoc thi dung o do:
///   - maxZoom: tran FOV do chinh minh dat.
///   - SimpleSuction.ZoomLevel: dong bang khi than cham maxScale (than dung to thi camera
///     cung dung zoom ra, khong thi nhan vat teo dan tren man hinh).
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraLevelZoom : MonoBehaviour
{
    [Tooltip("De trong = tu tim SimpleSuction trong scene.\n" +
             "BAT BUOC co de lay danh sach moc - moc gio khai bao ben SimpleSuction.levelSteps")]
    public SimpleSuction player;

    [Tooltip("De trong = lay Camera tren chinh object nay")]
    public Camera cam;

    [Tooltip("FOV goc (khi chua qua moc nao). Cac moc cong don len tren nay")]
    public float baseFov = 50f;

    [Range(1f, 179f)]
    [Tooltip("TRAN ZOOM: cong don toi day thi DUNG, len bao nhieu level nua cung khong zoom them.\n" +
             "Truoc day khong co bien nay nen FOV cong mai toi khi dam vao gioi han cung 179 do cua\n" +
             "Unity - luc do hinh meo nang. Dat so nay de tu quyet dinh diem dung.")]
    public float maxZoom = 80f;

    [Header("Camera Orthographic")]
    [Tooltip("Camera ortho KHONG dung fieldOfView - Unity bo qua. Khi cam.orthographic = true thi\n" +
             "component tu chuyen sang dieu khien orthographicSize, dung Y NGUYEN duong cong moc\n" +
             "o tren (steps/addPerLevel), chi doi hai dau mut sang don vi world.\n\n" +
             "baseSize = co khung luc chua qua moc nao (tuong ung baseFov).")]
    public float baseSize = 5f;

    [Tooltip("Co khung khi da zoom het co (tuong ung maxZoom). Toi day thi dung.")]
    public float maxSize = 24f;

    [Header("Zoom theo moc (Steps)")]
    [Tooltip("Co bat/tat su dung danh sach moc.\n" +
             "Danh sach moc gio nam ben SimpleSuction.levelSteps (cot 'zoomAdd') - khai bao mot lan\n" +
             "cho ca scale lan camera, doi moc khong phai sua 2 noi nua.")]
    public bool useSteps = true;

    [Header("Zoom nho moi cap (Add Per Level)")]
    [Tooltip("BAT: moi lan len cap deu CONG THEM addPerLevel do FOV.\nTAT: khong cong per level.")]
    public bool zoomEveryLevel = true;

    [Tooltip("Do FOV cong them cho MOI cap da len (khi bat zoomEveryLevel). Vi du: 0.2 = len 5 cap thi +1 do")]
    public float addPerLevel = 0.2f;

    [Header("Ket hop Steps & Add Per Level")]
    [Tooltip("BAT: Khi dung dong thoi Steps va AddPerLevel, tai level co trong Steps se lay data cua Step va KHONG cong addPerLevel cho level do.\nTAT: Cong ca hai tai level co Step.")]
    public bool skipAddPerLevelOnStep = true;

    [Header("Muot ma (DOTween)")]
    [Tooltip("Thoi gian doi FOV sang gia tri moi (giay). 0 = doi ngay tuc thi.\n" +
             "Thay cho 'lerpSpeed' cu - gio la tween chay 1 lan roi tu chet, khong con Update.")]
    public float tweenDuration = 0.3f;

    [Tooltip("Kieu easing khi doi FOV")]
    public Ease tweenEase = Ease.OutQuad;

    private Tween _tween;

    void OnEnable()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (player == null) player = ResolvePlayer();
        if (cam != null)
        {
            if (baseFov <= 0f) baseFov = cam.fieldOfView;
            if (baseSize <= 0f) baseSize = cam.orthographicSize;
        }

        ApplyForLevel(player != null ? player.ZoomLevel : 1, true);
    }

    void OnDisable() { KillTween(); }
    void OnDestroy() { KillTween(); }

    /// <summary>
    /// Tim SimpleSuction cua NGUOI CHOI.
    ///
    /// FindAnyObjectByType tra ve con DAU TIEN no gap, khong he uu tien ai - scene co them 3 con
    /// AI thi camera hoan toan co the di zoom theo mot con bot ma khong bao loi gi. Nen uu tien
    /// hoi Creature.Player (con co co isPlayer), het cach moi quay ve kieu cu cho scene test
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
    /// SimpleSuction goi ham nay MOI KHI LEN LEVEL (khong co poll). Tinh FOV dich 1 lan roi
    /// tween toi do. 'instant' = bo tween, dat thang (dung luc khoi tao / trong Edit mode).
    /// </summary>
    public void ApplyForLevel(int level, bool instant = false)
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        bool ortho = cam.orthographic;
        float target = ortho ? TargetSizeFor(level) : TargetFovFor(level);
        KillTween();

        if (instant || tweenDuration <= 0f || !Application.isPlaying)
        {
            if (ortho) cam.orthographicSize = target;
            else cam.fieldOfView = target;
            return;
        }

        _tween = ortho
            ? cam.DOOrthoSize(target, tweenDuration).SetEase(tweenEase)
            : cam.DOFieldOfView(target, tweenDuration).SetEase(tweenEase);
    }

    /// <summary>
    /// orthographicSize muc tieu cho mot level. Dung CHUNG duong cong voi TargetFovFor: lay
    /// vi tri tuong doi cua FOV trong khoang [baseFov .. maxZoom] roi anh sang [baseSize .. maxSize].
    /// Nho vay bang 'steps' chi phai tune MOT lan, doi qua ortho khong phai lam lai.
    /// </summary>
    public float TargetSizeFor(int level)
    {
        float span = Mathf.Max(0.0001f, Mathf.Clamp(maxZoom, 1f, 179f) - baseFov);
        float t = Mathf.Clamp01((TargetFovFor(level) - baseFov) / span);
        return Mathf.Lerp(baseSize, maxSize, t);
    }

    /// <summary>Gia tri camera dang huong toi, dung don vi cua che do hien tai.</summary>
    public float TargetZoom()
    {
        int lv = player != null ? player.ZoomLevel : 1;
        return (cam != null && cam.orthographic) ? TargetSizeFor(lv) : TargetFovFor(lv);
    }

    /// <summary>FOV muc tieu ung voi level dang hien hanh cua player.</summary>
    public float TargetFov()
    {
        return TargetFovFor(player != null ? player.ZoomLevel : 1);
    }

    /// <summary>
    /// FOV muc tieu cho mot level bat ky. O(so moc), khong cap phat.
    ///
    ///   fov = baseFov
    ///       + addPerLevel x (so level da len, TRU cac level trung moc neu skipAddPerLevelOnStep)
    ///       + tong 'add' cua moi moc da qua
    ///
    /// Nhieu Step cung mot 'level' thi 'add' van cong don het, nhung chi tinh la MOT lan trung
    /// moc (giong het ban cu dung Dictionary gop theo level).
    /// </summary>
    public float TargetFovFor(int level)
    {
        float fov = baseFov;
        int levelsGained = Mathf.Max(0, level - 1);

        float stepAdd = 0f;
        int distinctStepLevels = 0;

        List<SimpleSuction.LevelStep> steps = (useSteps && player != null) ? player.levelSteps : null;
        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                SimpleSuction.LevelStep s = steps[i];
                if (s == null || s.level < 2 || s.level > level) continue;
                if (s.zoomAdd == 0f) continue;   // moc chi dong toi scale -> khong tinh la moc cua camera

                stepAdd += s.zoomAdd;

                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    SimpleSuction.LevelStep q = steps[j];
                    if (q != null && q.zoomAdd != 0f && q.level == s.level && q.level >= 2 && q.level <= level) { duplicate = true; break; }
                }
                if (!duplicate) distinctStepLevels++;
            }
        }

        if (zoomEveryLevel)
        {
            int normal = skipAddPerLevelOnStep ? levelsGained - distinctStepLevels : levelsGained;
            fov += addPerLevel * Mathf.Max(0, normal);
        }
        fov += stepAdd;

        // Chan bang maxZoom (trong khoang hop le 1..179 cua Unity): toi tran thi dung han
        return Mathf.Clamp(fov, 1f, Mathf.Clamp(maxZoom, 1f, 179f));
    }

    /// <summary>Da zoom het co chua (dung cho ca 2 che do - do tren duong cong goc).</summary>
    public bool IsAtMaxZoom
    {
        get { return TargetFov() >= Mathf.Clamp(maxZoom, 1f, 179f) - 0.001f; }
    }

    private void KillTween()
    {
        if (_tween != null && _tween.IsActive()) _tween.Kill();
        _tween = null;
    }
}
