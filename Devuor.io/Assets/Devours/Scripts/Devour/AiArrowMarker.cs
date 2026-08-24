using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MOT MUI TEN chi huong bot. Chi la cai VO de AiArrowUI dieu khien - no khong tu biet gi ve bot,
/// ve camera hay ve khoang cach.
///
/// Tach ra thanh prefab de art sua duoc hinh thuc (doi sprite, doi font, them bong/vien, them
/// hieu ung) ma khong phai dong toi code. Truoc day AiArrowUI tu new GameObject nen moi thu deu
/// bi chot cung trong code.
///
/// CAU TRUC BAT BUOC:
///   AiArrow          (goc - AiArrowUI dat vi tri, KHONG xoay)
///     - Icon         (Image tam giac - AiArrowUI xoay RIENG cai nay)
///     - Distance     (TMP_Text so met - KHONG xoay, de con doc duoc)
///
/// Goc khong xoay, chi Icon xoay: chu ma xoay theo mui ten thi nghieng 130 do, khong doc noi -
/// ma doc duoc moi la ly do no ton tai.
/// </summary>
[DisallowMultipleComponent]
public class AiArrowMarker : MonoBehaviour
{
    [Tooltip("Tam giac chi huong. AiArrowUI se XOAY va TO MAU cai nay")]
    [SerializeField] private Image _icon;

    [Tooltip("So met. De trong = khong hien khoang cach, va AiArrowUI se tu bo qua")]
    [SerializeField] private TMP_Text _distance;

    /// <summary>Tam giac - ben dieu khien xoay va to mau.</summary>
    public Image Icon { get { return _icon; } }

    /// <summary>Chu so met. Co the null neu prefab khong dung toi.</summary>
    public TMP_Text Distance { get { return _distance; } }

    /// <summary>RectTransform cua tam giac, cache san de khoi hoi lai moi frame.</summary>
    public RectTransform IconRect { get { return _iconRect; } }

    /// <summary>RectTransform cua chu, cache san.</summary>
    public RectTransform DistanceRect { get { return _distanceRect; } }

    private RectTransform _iconRect;
    private RectTransform _distanceRect;

    void Reset() { AutoFill(); }

    void Awake()
    {
        if (_icon == null || _distance == null) AutoFill();
        if (_icon != null) _iconRect = _icon.rectTransform;
        if (_distance != null) _distanceRect = _distance.rectTransform;
    }

    /// <summary>Vua gan component / thieu ref thi tu tim theo ten con - luoi an toan, khong bat buoc.</summary>
    private void AutoFill()
    {
        if (_icon == null) _icon = GetComponentInChildren<Image>(true);
        if (_distance == null) _distance = GetComponentInChildren<TMP_Text>(true);
    }
}
