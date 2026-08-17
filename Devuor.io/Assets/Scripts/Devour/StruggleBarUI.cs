using UnityEngine;

/// <summary>
/// THANH GHI tren dau nhan vat - dong ho dem nguoc toi luc nan nhan gianh lai duoc toc do.
///
/// Nam tren CANVAS WORLD SPACE co san trong prefab (cai dang chay PlayerNameTag), khong de canvas
/// rieng: mot canvas moi cho moi con la mot batch rieng, ma PlayerNameTag thi da tu quay ca cum
/// ve phia camera roi - thanh nay an theo mien phi.
///
/// VI SAO KEO localScale MA KHONG DUNG Image.fillAmount:
/// fillAmount sinh lai mesh cua Image -> danh dau Canvas ban -> Canvas.BuildBatch chay lai. Thanh
/// tut muot co nghia la MOI FRAME mot lan rebuild, nhan voi so con trong van. Doi localScale cua
/// mot RectTransform con thi chi dung toi transform, khong dung toi mesh, canvas khong ban.
/// Cung ly do: KHONG doi mau theo % (ghi Image.color cung ghi lai vertex color -> ban canvas).
/// </summary>
[DisallowMultipleComponent]
public class StruggleBarUI : MonoBehaviour
{
    [Header("Tham chieu (keo vao Inspector)")]
    [Tooltip("Con so huu thanh nay. De trong = tu tim Creature o cha")]
    [SerializeField] private Creature _creature;

    [Tooltip("RUOT thanh - cai bi keo ngan lai. PIVOT X phai = 0 (mep trai), khong thi no co vao\n" +
             "giua thay vi rut tu phai sang")]
    [SerializeField] private RectTransform _fill;

    [Tooltip("Cac Image tao nen thanh (nen + ruot). De trong = tu gom moi Graphic trong con.\n" +
             "An thanh = tat 'enabled' cua chung, KHONG phai SetActive - xem ghi chu trong code")]
    [SerializeField] private UnityEngine.UI.Graphic[] _graphics;

    [Header("Hien/an")]
    [Tooltip("BAT: chi hien khi dang danh nhau hoac thanh chua hoi day.\n" +
             "TAT: hien suot (de soi luc chinh)")]
    public bool autoHide = true;

    [Tooltip("BAT: con dang o vai KE HUT thi khong hien thanh. Ke hut khong co dong ho dem nguoc,\n" +
             "hien mot thanh day im lia chi lam nguoi choi hieu nham")]
    public bool hideForAttacker = true;

    private float _lastShown = -1f;
    private bool _visible = true;

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake()
    {
        AutoFill();
        ApplyVisible(false);   // vao van la an, chua danh nhau thi khong co gi de xem
    }

    private void AutoFill()
    {
        if (_creature == null) _creature = GetComponentInParent<Creature>();
        if (_fill == null)
        {
            Transform f = transform.Find("Fill");
            if (f != null) _fill = f as RectTransform;
        }
        if (_graphics == null || _graphics.Length == 0)
            _graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
    }

    /// <summary>
    /// Chay o LateUpdate: Creature cap nhat thanh trong Update, doc o day thi luon lay duoc
    /// gia tri cua CHINH frame nay chu khong phai frame truoc.
    /// </summary>
    void LateUpdate()
    {
        if (_creature == null) return;

        float v = _creature.Struggle;

        bool show = !autoHide || _creature.InCombat || v < 0.999f;
        if (show && hideForAttacker && _creature.IsAttackerRole) show = false;

        if (show != _visible) ApplyVisible(show);
        if (!show) return;

        // Chi ghi khi so THUC SU doi: dung im ma van gan localScale moi frame la ep Unity tinh
        // lai ma tran cua ca nhanh transform, cho khong duoc gi
        if (_fill != null && !Mathf.Approximately(v, _lastShown))
        {
            Vector3 s = _fill.localScale;
            s.x = v;
            _fill.localScale = s;
            _lastShown = v;
        }
    }

    /// <summary>
    /// An/hien bang cach TAT 'enabled' cua tung Graphic, KHONG dung SetActive.
    ///
    /// Ban dau ham nay SetActive(false) chinh object dang mang component - va the la LateUpdate
    /// ngung chay, thanh khong bao gio tu bat lai duoc nua. Bay ay im lang tuyet doi: object van
    /// nam do, ref van du, chi la khong bao gio hien.
    ///
    /// Tat Graphic thi component van song, ma canvas cung khong con phai ve chung nua. Chi ton
    /// mot lan dung lai batch o luc CHUYEN trang thai (hai lan moi tran danh), khong phai moi frame.
    /// </summary>
    private void ApplyVisible(bool show)
    {
        _visible = show;
        if (_graphics == null) return;
        for (int i = 0; i < _graphics.Length; i++)
            if (_graphics[i] != null && _graphics[i].enabled != show) _graphics[i].enabled = show;
    }
}
