using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// DEM NGUOC CUOI VAN - con dung 35 giay thi mot con so TO hien len giua-tren man hinh, dem vai
/// nhip (35, 34, 33) kem cu nhun moi nhip, roi BAY THU NHO nhap vao dong ho tren HUD va bien mat.
///
/// Muc dich la mot cu HICH SU CHU Y: nguoi choi dang mai an, khong ai ngoi nhin dong ho goc man
/// hinh. Con so to nhay ra giua man bat buoc phai thay, va cu bay ve dong ho o cuoi chinh la cau
/// "tu gio nhin cho nay ma dem" - noi cai vua thay voi cho no se nam tiep.
///
/// CHI DEM VAI NHIP ROI BIEN: de nguyen suot 35 giay thi no che dung doan can nhin nhat, ma cung
/// khong con luc nao de bay ve dong ho nua.
///
/// Gan len object UI bat ky duoi Canvas (vd HUD). Con so la mot object RIENG keo vao o Label -
/// dat no o dau, font gi, mau gi la viec cua prefab, code khong dung toi.
/// </summary>
[DisallowMultipleComponent]
public class FinalCountdownUI : MonoBehaviour
{
    [Header("Tham chieu")]
    [Tooltip("Con so TO. Dat vi tri / font / mau ngay trong prefab - code chi doi chu va cho bay")]
    [SerializeField] private TMP_Text _label;

    [Tooltip("Bay VE dau. De trong = tu lay timerText cua UIManager (dong hho chinh)")]
    [SerializeField] private RectTransform _flyTarget;

    [Header("Thoi diem")]
    [Tooltip("Con bao nhieu giay thi con so to nhay ra")]
    public float triggerAt = 35f;

    [Tooltip("Dem bao nhieu nhip roi bay ve dong ho. 3 = hien 35, 34, 33 roi bay")]
    public int ticks = 3;

    [Header("Cu nhun moi nhip")]
    [Range(0f, 1.5f)]
    [Tooltip("Do manh cu nhun moi lan con so doi. 0 = tat")]
    public float tickPunch = 0.45f;

    [Tooltip("Mot cu nhun keo dai bao lau (giay)")]
    public float tickPunchDuration = 0.3f;

    [Range(0, 10)]
    [Tooltip("So nhip rung trong mot cu nhun")]
    public int tickPunchVibrato = 3;

    [Header("Cu bay ve dong ho")]
    [Tooltip("Bay het bao lau (giay)")]
    public float flyDuration = 0.5f;

    [Tooltip("BAT (mac dinh): tu tinh co cuoi sao cho con so TRUNG KHIT co chu cua dong ho - bay toi\n" +
             "noi la no nhap lam mot voi dong ho, khong phai teo roi tan bien.\n" +
             "TAT: dung flyEndScale ben duoi.")]
    public bool matchClockSize = true;

    [Range(0f, 1f)]
    [Tooltip("CHI DUNG KHI TAT matchClockSize: bay toi noi thi teo con bao nhieu phan co ban dau")]
    public float flyEndScale = 0.25f;

    [Tooltip("BAT: mo dan trong luc bay. TAT (mac dinh): giu ro nguyen den luc cham dong ho roi tat\n" +
             "ngay - de mat nhin ra no NHAP VAO dong ho chu khong phai bien mat giua duong")]
    public bool fadeWhileFlying = false;

    private RectTransform _labelRt;
    private RectTransform _parentRt;
    private Vector2 _homePos;
    private Vector3 _homeScale;
    private int _lastShown = int.MinValue;
    private int _ticksDone;
    private bool _running;
    private bool _doneThisMatch;
    private Tween _fly;

    void Awake()
    {
        if (_label == null)
        {
            Debug.LogError("[FinalCountdownUI] Chua keo con so vao o 'Label'.", this);
            enabled = false;
            return;
        }

        _labelRt = _label.rectTransform;
        _parentRt = _labelRt.parent as RectTransform;

        CenterPivot();

        // Chup cho dung / co GOC ngay tu dau: moi van deu tra ve day nay, khong bao gio troi di
        // sau nhieu lan bay.
        _homePos = _labelRt.anchoredPosition;
        _homeScale = _labelRt.localScale;

        _label.gameObject.SetActive(false);
    }

    void Update()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null) return;

        // Van moi (bam Replay / vao van khac): TimeLeft nhay tro lai cao hon moc -> mo khoa de
        // van sau con so lai duoc hien. Khong co dong nay thi tinh nang chi chay duoc DUNG MOT VAN.
        if (ui.TimeLeft > triggerAt + 1f)
        {
            _doneThisMatch = false;
            _running = false;
            _ticksDone = 0;
            _lastShown = int.MinValue;
            return;
        }

        if (_doneThisMatch || ui.State != UIManager.MatchState.Playing || ui.matchDuration <= 0f) return;
        if (ui.TimeLeft > triggerAt) return;

        if (!_running)
        {
            _running = true;
            _ticksDone = 0;
            _lastShown = int.MinValue;
            _labelRt.anchoredPosition = _homePos;
            _labelRt.localScale = _homeScale;
            SetAlpha(1f);
            _label.gameObject.SetActive(true);
        }

        int now = Mathf.CeilToInt(ui.TimeLeft);
        if (now == _lastShown) return;

        _lastShown = now;
        _label.text = now.ToString();
        Punch();

        _ticksDone++;
        if (_ticksDone >= Mathf.Max(1, ticks)) FlyToClock();
    }

    /// <summary>
    /// DUA PIVOT VE TAM (0.5, 0.5), bu lai vi tri de tren man hinh khong xe dich mot ly nao.
    ///
    /// Vi sao phai lam: pivot dung dau (0.5, 1) thi khi TEO NHO, con so co lai quanh CANH TREN -
    /// tam cua no truot len tren trong luc bay, ha canh xong se lech han so voi dong ho. Pivot o
    /// tam thi teo doi xung, tam dung yen, ha canh dung cho.
    ///
    /// Lam trong code chu khong bat art dat tay: dat pivot trong prefab la thay bo cuc nhay mot cai
    /// luc dung, rat de bi sua nguoc lai ma khong ai nho tai sao.
    /// </summary>
    private void CenterPivot()
    {
        Vector2 want = new Vector2(0.5f, 0.5f);
        Vector2 delta = want - _labelRt.pivot;
        if (delta.sqrMagnitude < 0.000001f) return;

        Vector2 size = _labelRt.rect.size;
        _labelRt.pivot = want;
        _labelRt.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
    }

    /// <summary>Nhun mot cai moi lan con so doi. Giet cu cu truoc khi ban cu moi de scale khong cong don.</summary>
    private void Punch()
    {
        if (tickPunch <= 0.001f || tickPunchDuration <= 0.001f) return;

        _labelRt.DOKill();
        _labelRt.localScale = _homeScale;
        _labelRt.DOPunchScale(Vector3.one * tickPunch, tickPunchDuration, tickPunchVibrato, 0.6f)
            .SetTarget(_labelRt);
    }

    /// <summary>
    /// BAY VE DONG HO: truot toi vi tri dong ho chinh, teo nho va mo dan, xong thi tat.
    ///
    /// Doc vi tri dong ho NGAY LUC BAY chu khong chup san tu Awake: HUD co the duoc bat/tat hoac
    /// doi bo cuc giua chung, chup san la bay ve mot cho khong con ai o do.
    /// </summary>
    private void FlyToClock()
    {
        _doneThisMatch = true;
        _running = false;

        RectTransform dst = _flyTarget;
        if (dst == null && UIManager.Instance != null && UIManager.Instance.timerText != null)
            dst = UIManager.Instance.timerText.rectTransform;

        // BAY BANG TOA DO WORLD, khong quy doi sang anchoredPosition.
        //
        // anchoredPosition tinh tu DIEM NEO cua chinh object, ma hai cai neo o hai goc khac nhau:
        // con so neo giua-tren (0, 960), dong ho neo goc phai-tren (540, 960). Gan thang mot diem
        // local cua cha vao anchoredPosition la cong dom them ca khoang cach giua hai neo - do
        // thuc te no bay toi y=1850 trong khi man hinh chi cao +-960, tuc vot han ra ngoai dinh.
        //
        // Toa do world thi khong co cai bay do: hai ben cung mot canvas, lay tam la ra tam.
        Vector3 endWorld = _labelRt.position;
        if (dst != null) endWorld = RectCenterWorld(dst);

        // CO CUOI: tinh sao cho chu cua minh bang dung chu cua dong ho -> cham noi la trung khit,
        // nhin ra "con so to bien thanh cai dong ho" chu khong phai "no teo roi bien mat".
        float endScale = Mathf.Max(0.01f, flyEndScale);
        if (matchClockSize)
        {
            float dstFont = TargetFontSize(dst);
            float myFont = _label.fontSize;
            if (dstFont > 0.01f && myFont > 0.01f) endScale = dstFont / myFont;
        }

        _labelRt.DOKill();
        if (_fly != null && _fly.IsActive()) _fly.Kill();

        Sequence seq = DOTween.Sequence().SetTarget(_labelRt);
        seq.Join(_labelRt.DOMove(endWorld, flyDuration).SetEase(Ease.InQuad));
        seq.Join(_labelRt.DOScale(_homeScale * endScale, flyDuration).SetEase(Ease.InQuad));
        if (fadeWhileFlying) seq.Join(DOTween.To(() => 1f, SetAlpha, 0f, flyDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            if (_label == null) return;
            _label.gameObject.SetActive(false);
            _labelRt.anchoredPosition = _homePos;
            _labelRt.localScale = _homeScale;
            SetAlpha(1f);
        });
        _fly = seq;
    }

    /// <summary>
    /// TAM THAT cua mot RectTransform trong toa do world - lay tu 4 goc chu khong lay .position.
    ///
    /// .position tra ve cho dat PIVOT. Dong ho neo goc phai-tren voi pivot (1,1) nen .position cua
    /// no la GOC TREN-PHAI cua o chu, khong phai tam - bay toi do la con so nam lech han sang trai
    /// va xuong duoi mot nua o.
    /// </summary>
    private static Vector3 RectCenterWorld(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    /// <summary>
    /// Co chu cua cai dich bay toi. Dong ho hien la Text (UI cu) nhung de cho ca TMP phong khi
    /// sau nay doi - doi mot cai thi khong phai nho quay lai sua cho nay.
    /// </summary>
    private static float TargetFontSize(RectTransform dst)
    {
        if (dst == null) return 0f;
        UnityEngine.UI.Text legacy = dst.GetComponent<UnityEngine.UI.Text>();
        if (legacy != null) return legacy.fontSize;
        TMP_Text tmp = dst.GetComponent<TMP_Text>();
        if (tmp != null) return tmp.fontSize;
        return 0f;
    }

    private void SetAlpha(float a)
    {
        if (_label == null) return;
        Color c = _label.color;
        c.a = a;
        _label.color = c;
    }

    void OnDisable()
    {
        // Tween con song ma object da tat thi DOTween nem loi, va con so se ket lai giua duong bay
        if (_labelRt != null) _labelRt.DOKill();
        if (_fly != null && _fly.IsActive()) _fly.Kill();
        if (_label != null) _label.gameObject.SetActive(false);
    }
}
