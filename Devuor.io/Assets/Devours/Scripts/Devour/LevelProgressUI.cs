using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// THANH TIEN TRINH LEVEL tren HUD: chay tu Lv1 toi moc CUOI CUNG duoc lay trong
/// SuctionConfig.levelSteps, co vach ngan giua cac moc va mot icon tren moi moc.
///
/// ------------------------------------------------------------------------------------------
/// CHIA DEU THEO SO MOC, KHONG CHIA THEO LEVEL THAT
/// ------------------------------------------------------------------------------------------
/// Moi moc chiem dung 1/N be ngang thanh, du khoang cach level giua chung lech nhau bao nhieu.
///
/// Vi sao khong chia tuyen tinh theo level: bang hien tai co moc o 15/30/150/350/600, tuc khoang
/// cach la 14/15/120/200/250. Chia tuyen tinh thi moc Lv15 nam o 2.3% va moc Lv30 o 4.8% - cach
/// nhau 2.5% be ngang thanh, khoang 17px tren man doc, trong khi icon rong 44px. Hai icon dau se
/// chong de len nhau va khong doc duoc gi.
///
/// Doi lai: thanh chay KHONG DEU (doan 2 chi ton ~7 mieng an, doan 3 ton ~30). Da biet va chap
/// nhan - do la thuoc tinh cua bang can bang, khong phai cua UI.
///
/// ------------------------------------------------------------------------------------------
/// VI SAO POLL LEVEL TRONG Update THAY VI NGHE onLevelUp
/// ------------------------------------------------------------------------------------------
/// SimpleSuction co san UnityEvent onLevelUp/onLevelDown, nhung dang ky nghe thi phai lo ca doi
/// huy: nguoi choi CHET giua tran (object bi tat), van moi sinh mot player khac, doi scene...
/// Bo nao quen huy la giu tham chieu vao mot object da chet.
///
/// So sanh MOT so nguyen moi frame la gan nhu mien phi, va tu no mien nhiem voi moi ca tren:
/// player bien mat thi ham thoat som, player moi thi tu dung lai thanh.
/// </summary>
[DisallowMultipleComponent]
public class LevelProgressUI : MonoBehaviour
{
    [Header("So moc lay tu levelSteps")]
    [Min(1)]
    [Tooltip("CHI LAY BAY NHIEU MOC DAU TIEN trong levelSteps, du asset co nhieu hon.\n\n" +
             "VI SAO CAN TRAN CUNG: thanh chia deu theo so moc, nen them moc thu 6-7 vao bang can\n" +
             "bang la thanh tu chia lai 6-7 doan - icon nho di, chu de len nhau, bo cuc HUD vo ma\n" +
             "khong ai dong toi UI ca. Chan o day thi tune bang can bang bao nhieu lan cung khong\n" +
             "the lam hong thanh.\n\n" +
             "Asset co IT hon so nay thi thanh TU ha xuong cho khop, kem mot dong canh bao.")]
    [SerializeField] private int _stepCount = 5;

    [Header("Tham chieu")]
    [Tooltip("Anh phan DA DAY. Image Type phai la FILLED, Fill Method = Horizontal.\n" +
             "Khong phai Filled thi fillAmount khong co tac dung nao va thanh dung im.")]
    [SerializeField] private Image _fill;

    [Tooltip("Object CHA rong cho VACH NGAN - dat TRUNG voi thanh (vach nam trong long thanh).\n\n" +
             "LUU Y: moi con cua no bi XOA SACH moi lan dung lai thanh - dung nhet gi khac vao day.\n" +
             "Phai trum DUNG be ngang thanh, vi vach duoc neo theo ti le be ngang cua no.")]
    [SerializeField] private RectTransform _tickRoot;

    [Tooltip("Object CHA rong cho ICON - dat CAO HON thanh (icon nam tren thanh).\n\n" +
             "Tach rieng khoi _tickRoot de chinh do cao hang icon ngay trong Editor, khong phai\n" +
             "sua so trong code. Cung bi xoa sach moi lan dung lai thanh.\n" +
             "Phai trum DUNG be ngang thanh, giong _tickRoot.")]
    [SerializeField] private RectTransform _iconRoot;

    [Tooltip("Prefab icon cho moc CHI PHONG TO (isEvolution = false). Nen co LevelProgressIcon.")]
    [SerializeField] private GameObject _scaleIconPrefab;

    [Tooltip("Prefab icon cho moc CO TIEN HOA (isEvolution = true). De trong = dung luon prefab\n" +
             "phong to cho moi moc.")]
    [SerializeField] private GameObject _evolutionIconPrefab;

    [Tooltip("HINH cho tung moc TIEN HOA, xep theo THU TU GAP: phan tu 0 cho moc tien hoa dau tien\n" +
             "tren thanh, 1 cho moc thu hai, 2 cho moc thu ba...\n\n" +
             "Danh so theo THU TU TIEN HOA chu khong phai thu tu moc: bang hien tai co moc tien hoa\n" +
             "o vi tri 2, 4, 5 tren thanh -> ba hinh nay roi vao dung ba cho do, cac moc phong to\n" +
             "xen giua khong an mat mot so nao.\n\n" +
             "De TRONG hoac thieu phan tu = moc do giu nguyen hinh trong prefab tien hoa. Doi hinh\n" +
             "o day KHONG can sua prefab, va them mot moc tien hoa vao bang can bang cung khong lam\n" +
             "vo gi - no chi dung lai hinh cua prefab cho toi khi ban them sprite vao day.")]
    [SerializeField] private Sprite[] _evolutionSprites;

    [Tooltip("Prefab VACH NGAN giua hai moc. De trong = khong ve vach.\n" +
             "Chi ve o cac moc GIUA, khong ve o moc cuoi (do la mep phai cua thanh).")]
    [SerializeField] private GameObject _tickPrefab;

    [Tooltip("Chu ben TRAI - level hien tai. De trong = khong hien")]
    [SerializeField] private TMP_Text _levelLabel;

    [Tooltip("Chu ben PHAI - level cua moc cuoi, doi thanh MAX khi da cham. De trong = khong hien")]
    [SerializeField] private TMP_Text _targetLabel;

    [Header("Hien thi")]
    [Tooltip("Dinh dang chu ben trai. {0} = level hien tai")]
    [SerializeField] private string _levelFormat = "Lv {0}";

    [Tooltip("Dinh dang chu ben phai. {0} = level cua moc cuoi")]
    [SerializeField] private string _targetFormat = "Lv {0}";

    [Tooltip("Chu thay cho nhan phai khi da cham moc cuoi.\n\n" +
             "Van 180s cham moc cuoi tu khoang giay 144, tuc thanh nam DAY suot 36 giay cuoi -\n" +
             "phai co chu de no doc ra la 'xong roi' chu khong phai 'UI dung hinh'.")]
    [SerializeField] private string _maxText = "MAX";

    [Tooltip("Icon cua moc TIEN HOA duoc nhac cao them bao nhieu px so voi icon thuong.\n" +
             "0 = moi icon cung mot hang.")]
    [SerializeField] private float _evolutionRaise = 10f;

    [Tooltip("Thoi gian truot thanh khi level doi (giay). 0 = nhay thang, khong tween")]
    [SerializeField] private float _fillTweenTime = 0.25f;

    private SimpleSuction _suction;
    private readonly List<LevelStep> _steps = new List<LevelStep>();
    private readonly List<LevelProgressIcon> _icons = new List<LevelProgressIcon>();
    private Tween _fillTween;
    private int _lastLevel = int.MinValue;
    private bool _built;

    /// <summary>So moc thanh dang thuc su ve (da qua tran _stepCount va da loc moc khong hop le).</summary>
    public int StepCount { get { return _steps.Count; } }

    void Reset() { AutoFill(); }

    private void AutoFill()
    {
        if (_tickRoot == null)
        {
            Transform t = transform.Find("Bar/Ticks");
            if (t != null) _tickRoot = t as RectTransform;
        }
        if (_iconRoot == null)
        {
            Transform t = transform.Find("Icons");
            if (t != null) _iconRoot = t as RectTransform;
        }
        if (_fill == null)
        {
            Transform t = transform.Find("Fill");
            if (t != null) _fill = t.GetComponent<Image>();
        }
    }

    void Update()
    {
        SimpleSuction s = CurrentSuction();
        if (s == null) return;

        // Player moi (van moi, hoi sinh) -> dung lai thanh tu dau.
        if (s != _suction)
        {
            _suction = s;
            _built = false;
        }

        if (!_built)
        {
            Build();
            _built = true;
            _lastLevel = int.MinValue;
        }

        if (_steps.Count == 0) return;

        int level = _suction.Level;
        if (level == _lastLevel) return;

        bool first = _lastLevel == int.MinValue;
        _lastLevel = level;
        Apply(level, !first);   // lan dat dau tien: khong tween, khong nhun
    }

    /// <summary>
    /// Con nguoi choi dang song. Khong co (dang o man Home, hoac vua bi nuot) thi tra null va
    /// Update thoat som - thanh giu nguyen thu dang ve cuoi cung.
    /// </summary>
    private SimpleSuction CurrentSuction()
    {
        if (!GameManager.HasInstance) return null;
        Creature p = GameManager.Instance.Player;
        return p != null ? p.Suction : null;
    }

    /// <summary>
    /// Dung lai toan bo icon + vach ngan TU levelSteps.
    ///
    /// Dung tu bang can bang chu khong keo tay tung icon: doi mot moc trong SuctionConfig la thanh
    /// tu theo. Keo tay thi som muon cung co ngay bang can bang mot dang con HUD mot dang, va cai
    /// lech do khong bao gio bao loi.
    /// </summary>
    private void Build()
    {
        ClearSlots();
        _steps.Clear();

        // Config bat buoc phai co - SimpleSuction.LevelSteps doc thang Config.levelSteps nen thieu
        // la NullReference. Awake ben do da bao loi roi, o day chi can thoat cho gon.
        if (_suction == null || _suction.Config == null) return;

        List<LevelStep> src = _suction.LevelSteps;
        if (src == null) return;

        for (int i = 0; i < src.Count; i++)
        {
            LevelStep s = src[i];
            if (s == null || s.level < 2) continue;   // cung luat loc voi SimpleSuction.StageAtLevel
            _steps.Add(s);
        }

        // Sap tang dan: phep chia doan o duoi coi moc i-1 la day cua doan i. Bang can bang khong
        // bat buoc phai nhap theo thu tu, va StageAtLevel cung khong can thu tu - nen phai tu sap.
        _steps.Sort(CompareStepLevel);

        int want = Mathf.Max(1, _stepCount);
        if (_steps.Count > want)
        {
            _steps.RemoveRange(want, _steps.Count - want);
        }
        else if (_steps.Count < want)
        {
            Debug.LogWarning("[LevelProgressUI] Xin " + want + " moc nhung levelSteps chi co " +
                             _steps.Count + " moc dung duoc (level >= 2). Thanh se chia " +
                             _steps.Count + " doan.", this);
        }

        if (_steps.Count == 0) return;

        int n = _steps.Count;
        int evoIndex = 0;   // dem RIENG cac moc tien hoa - moc phong to xen giua khong lam nhay so
        for (int i = 0; i < n; i++)
        {
            float x = (i + 1) / (float)n;   // moc nam o CUOI doan cua no

            // Vach ngan chi ve o cac moc GIUA. Moc cuoi trung mep phai cua thanh, ve vach o do
            // chi de mot gach de len duong vien.
            if (_tickPrefab != null && _tickRoot != null && i < n - 1) Spawn(_tickPrefab, _tickRoot, x, 0f);

            bool evo = _steps[i].isEvolution;
            GameObject prefab = evo && _evolutionIconPrefab != null ? _evolutionIconPrefab : _scaleIconPrefab;

            // TANG SO NGAY O DAY, truoc moi loi thoat som: neu bo qua mot moc tien hoa vi thieu
            // prefab/_iconRoot ma khong tang, moi moc tien hoa phia sau se an nham hinh cua moc
            // truoc no - lech mot cach im lang, khong bao loi nao.
            int slot = evo ? evoIndex++ : -1;

            if (prefab == null || _iconRoot == null) continue;

            GameObject go = Spawn(prefab, _iconRoot, x, evo ? _evolutionRaise : 0f);
            LevelProgressIcon icon = go.GetComponent<LevelProgressIcon>();

            // Moc PHONG TO khong co danh sach rieng: no chi co mot hinh duy nhat nen de prefab lo.
            if (icon != null && slot >= 0 && _evolutionSprites != null && slot < _evolutionSprites.Length)
                icon.SetSprite(_evolutionSprites[slot]);

            _icons.Add(icon);
        }
    }

    private static int CompareStepLevel(LevelStep a, LevelStep b) { return a.level.CompareTo(b.level); }

    /// <summary>
    /// De mot object vao thanh o ti le x (0..1) be ngang.
    ///
    /// Neo bang anchor chu khong dat toa do px: Canvas Scaler co gian thanh theo do phan giai may,
    /// neo theo ti le thi icon tu bam dung cho o moi co man - dat px thi may nao khac ti le la
    /// icon truot khoi vach ngan.
    /// </summary>
    private GameObject Spawn(GameObject prefab, RectTransform root, float x, float raise)
    {
        GameObject go = Instantiate(prefab, root);
        RectTransform rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(x, 0.5f);
            rt.anchorMax = new Vector2(x, 0.5f);
            rt.anchoredPosition = new Vector2(0f, raise);
        }
        return go;
    }

    private void ClearSlots()
    {
        _icons.Clear();
        ClearChildren(_tickRoot);
        ClearChildren(_iconRoot);
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject go = root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }

    /// <summary>
    /// Do level hien tai ra: phan da day cua thanh, trang thai tung icon, hai nhan chu.
    ///
    /// Doan i chay tu moc i-1 den moc i (doan dau tien chay tu Lv1). Trong mot doan thi day theo
    /// TI LE LEVEL, nen thanh van truot muot giua hai moc chu khong nhay tung cuc.
    /// </summary>
    private void Apply(int level, bool animate)
    {
        int n = _steps.Count;
        int reached = 0;
        float fill = 1f;   // vuot het moi moc thi vong lap chay het ma khong break -> thanh day

        for (int i = 0; i < n; i++)
        {
            int segStart = i == 0 ? 1 : _steps[i - 1].level;
            int segEnd = _steps[i].level;

            if (level >= segEnd) { reached = i + 1; continue; }

            float span = Mathf.Max(1, segEnd - segStart);
            float t = Mathf.Clamp01((level - segStart) / span);
            fill = (i + t) / n;
            break;
        }

        SetFill(fill, animate);

        for (int i = 0; i < _icons.Count; i++)
            if (_icons[i] != null) _icons[i].SetReached(i < reached, animate);

        if (_levelLabel != null) _levelLabel.text = string.Format(_levelFormat, level);

        if (_targetLabel != null)
        {
            int last = _steps[n - 1].level;
            _targetLabel.text = level >= last ? _maxText : string.Format(_targetFormat, last);
        }
    }

    /// <summary>
    /// Truot thanh toi gia tri moi.
    ///
    /// Dung DOVirtual.Float chu khong dung DOFillAmount: DOFillAmount nam trong module
    /// DOTweenModuleUI (phai duoc bat rieng trong DOTween Utility Panel), con DOVirtual la module
    /// loi - luon co. Khong danh doi mot dong tien nghi lay rui ro ban build khong bien dich.
    ///
    /// SetUpdate(true) = gio that, cung ly do voi cu nhun icon: vuot moc co hitstop.
    /// </summary>
    private void SetFill(float value, bool animate)
    {
        if (_fill == null) return;
        value = Mathf.Clamp01(value);

        if (_fillTween != null && _fillTween.IsActive()) _fillTween.Kill();
        _fillTween = null;

        if (!animate || _fillTweenTime <= 0f || !Application.isPlaying)
        {
            _fill.fillAmount = value;
            return;
        }

        float from = _fill.fillAmount;
        _fillTween = DOVirtual.Float(from, value, _fillTweenTime, OnFillStep)
                              .SetEase(Ease.OutQuad)
                              .SetTarget(this)
                              .SetUpdate(true);
    }

    private void OnFillStep(float v)
    {
        if (_fill != null) _fill.fillAmount = v;
    }

    void OnDestroy()
    {
        if (_fillTween != null && _fillTween.IsActive()) _fillTween.Kill();
        _fillTween = null;
        DOTween.Kill(this);
    }
}
