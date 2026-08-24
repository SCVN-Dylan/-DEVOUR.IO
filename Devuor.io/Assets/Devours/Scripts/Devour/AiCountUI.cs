using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// SO BOT CON LAI tren HUD - kem mot icon de doc duoc ngay ma khong can chu giai.
///
/// Bot trong game nay KHONG hoi sinh: chet la mat han, va con cuoi cung chet la NGUOI CHOI THANG
/// (xem GameManager.ReportDeath). Nen con so nay khong phai trang tri - no la thanh tien do di
/// toi chien thang, va moi lan tut mot nac la mot moc dang duoc bao.
///
/// Doc thang GameManager.AiAlive chu khong tu dem: dem lai la som muon cung lech voi luat thang
/// thua (con dang bay vao mom ke giet da bi rut ten nhung chua Destroy...). Mot nguon duy nhat thi
/// HUD khong bao gio hien "con 1" trong khi man THANG da bat len.
/// </summary>
[DisallowMultipleComponent]
public class AiCountUI : MonoBehaviour
{
    [Header("Tham chieu")]
    [Tooltip("Chu hien so. Dat font/mau/vi tri trong prefab - code chi doi noi dung")]
    [SerializeField] private TMP_Text _label;

    [Tooltip("Object duoc NHUN moi khi so thay doi. De trong = nhun chinh cai chu.\n" +
             "Keo ca cum (icon + chu) vao day thi ca hai cung nhun")]
    [SerializeField] private RectTransform _punchTarget;

    [Header("Hien thi")]
    [Tooltip("Dinh dang: {0} = so bot con lai. Vi du 'x{0}' hay '{0} con'")]
    public string format = "{0}";

    [Header("Nhun khi so thay doi")]
    [Range(0f, 1.5f)]
    [Tooltip("Do manh cu nhun khi mot con bot vua chet. 0 = tat")]
    public float punch = 0.4f;

    [Tooltip("Mot cu nhun keo dai bao lau (giay)")]
    public float punchDuration = 0.3f;

    [Range(0, 10)]
    [Tooltip("So nhip rung trong mot cu nhun")]
    public int punchVibrato = 3;

    private int _lastCount = int.MinValue;
    private Vector3 _homeScale = Vector3.one;
    private RectTransform _target;

    void Awake()
    {
        if (_label == null)
        {
            Debug.LogError("[AiCountUI] Chua keo chu vao o 'Label'.", this);
            enabled = false;
            return;
        }

        _target = _punchTarget != null ? _punchTarget : _label.rectTransform;
        _homeScale = _target.localScale;
    }

    void Update()
    {
        if (!GameManager.HasInstance) return;

        int now = GameManager.Instance.AiAlive;
        if (now == _lastCount) return;

        // Lan ve DAU TIEN (lastCount = MinValue) chi dat chu, khong nhun: vao van ma HUD tu nhien
        // giat mot cai la vo duyen, cu nhun de danh cho luc co con bot that su chet.
        bool first = _lastCount == int.MinValue;
        _lastCount = now;
        _label.text = string.Format(format, now);
        if (!first) Punch();
    }

    /// <summary>Giet cu cu truoc khi ban cu moi: hai con chet lien tiep thi hai tween se cong don scale.</summary>
    private void Punch()
    {
        if (!Application.isPlaying || punch <= 0.001f || punchDuration <= 0.001f || _target == null) return;

        _target.DOKill();
        _target.localScale = _homeScale;
        _target.DOPunchScale(Vector3.one * punch, punchDuration, punchVibrato, 0.6f).SetTarget(_target);
    }

    void OnDisable()
    {
        if (_target == null) return;
        _target.DOKill();
        _target.localScale = _homeScale;
    }
}
