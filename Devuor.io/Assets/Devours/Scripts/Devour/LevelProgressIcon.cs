using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MOT MOC tren thanh tien trinh. Chi lo phan NHIN: bat/tat hai the (chua toi / da dat) va nhun
/// mot cai dung luc vua dat.
///
/// VI SAO TACH KHOI LevelProgressUI: moi quyet dinh ve MY THUAT nam gon trong prefab - doi mau,
/// doi anh, them hao quang, doi ca bo cuc icon deu khong phai sua mot dong code nao.
/// LevelProgressUI chi biet mot cau duy nhat: SetReached(true/false).
///
/// Nho vay icon PHONG TO va icon TIEN HOA dung chung mot script, khac nhau hoan toan o prefab.
/// </summary>
[DisallowMultipleComponent]
public class LevelProgressIcon : MonoBehaviour
{
    [Tooltip("Nhanh hien khi CHUA toi moc (ban xam/mo). De trong = khong tat bat gi ca.\n" +
             "Reset/them component se tu tim object con ten 'Locked'.")]
    [SerializeField] private GameObject _locked;

    [Tooltip("Nhanh hien khi DA dat moc (ban sang). De trong = khong tat bat gi ca.\n" +
             "Reset/them component se tu tim object con ten 'Reached'.")]
    [SerializeField] private GameObject _reached;

    [Tooltip("Object nhan cu nhun luc vua dat moc. De trong = nhun chinh object nay.")]
    [SerializeField] private RectTransform _punchTarget;

    [Header("Anh LOI cua moc - de SetSprite doi duoc")]
    [Tooltip("Image chua hinh loi cua ban CHUA DAT (bong den). De trong = SetSprite bo qua ban nay.\n" +
             "Reset/them component se tu tim 'Locked/Core/Image'.")]
    [SerializeField] private Image _lockedArt;

    [Tooltip("Image chua hinh loi cua ban DA DAT (sang). De trong = SetSprite bo qua ban nay.\n" +
             "Reset/them component se tu tim 'Reached/Core/Image'.")]
    [SerializeField] private Image _reachedArt;

    [Header("Cu nhun luc vua dat moc")]
    [Range(0f, 1.5f)]
    [Tooltip("Do manh cu nhun. 0 = tat han")]
    public float punch = 0.45f;

    [Tooltip("Mot cu nhun keo dai bao lau (giay)")]
    public float punchDuration = 0.35f;

    [Range(0, 10)]
    [Tooltip("So nhip rung trong mot cu nhun")]
    public int punchVibrato = 6;

    private bool _state;
    private bool _known;   // da tung duoc dat trang thai chua - de lan dau khong tinh la "vua doi"

    void Reset() { AutoFill(); }

    void Awake() { AutoFill(); }

    /// <summary>Luoi an toan cho ref quen keo. Ref da co san tren prefab thi khong dong toi.</summary>
    private void AutoFill()
    {
        if (_punchTarget == null) _punchTarget = transform as RectTransform;

        if (_locked == null)
        {
            Transform t = transform.Find("Locked");
            if (t != null) _locked = t.gameObject;
        }
        if (_reached == null)
        {
            Transform t = transform.Find("Reached");
            if (t != null) _reached = t.gameObject;
        }

        // Anh LOI nam sau mot lop Core (Core la cai dia tron co mau, Image la hinh tren dia).
        // Tim theo duong dan chu khong GetComponentInChildren: nhanh Locked/Reached moi nhanh co
        // toi 3 Image (Ring, Core, Image) - vo duoc cai dau tien la doi trung cai dia.
        if (_lockedArt == null) _lockedArt = FindArt("Locked");
        if (_reachedArt == null) _reachedArt = FindArt("Reached");
    }

    private Image FindArt(string branch)
    {
        Transform t = transform.Find(branch + "/Core/Image");
        return t != null ? t.GetComponent<Image>() : null;
    }

    /// <summary>
    /// DOI HINH LOI cua moc nay, ap cho CA HAI ban (chua dat + da dat).
    ///
    /// Chi doi sprite, KHONG dong toi mau: ban Locked dang duoc to den thanh bong, ban Reached de
    /// trang. Ghi de mau o day thi bong den sang chóe len - ma do lai la thu prefab dang lo.
    ///
    /// null = giu nguyen hinh co san trong prefab. Nho vay danh sach sprite ben LevelProgressUI
    /// thieu phan tu cung khong lam trong icon.
    /// </summary>
    public void SetSprite(Sprite sprite)
    {
        if (sprite == null) return;
        if (_lockedArt != null) _lockedArt.sprite = sprite;
        if (_reachedArt != null) _reachedArt.sprite = sprite;
    }

    /// <summary>
    /// Dat trang thai moc.
    ///
    /// 'animate' = false khi dung thanh lan dau / reset van: khong the de ca 5 icon cung nhun mot
    /// luc ngay khoanh khac vao tran. Chi nhun khi trang thai THUC SU vua doi tu chua-dat sang
    /// da-dat - goi lai voi cung gia tri thi im.
    /// </summary>
    public void SetReached(bool reached, bool animate)
    {
        bool justReached = animate && reached && (!_known || !_state);

        _known = true;
        _state = reached;

        if (_locked != null) _locked.SetActive(!reached);
        if (_reached != null) _reached.SetActive(reached);

        if (!justReached || punch <= 0f || _punchTarget == null) return;

        _punchTarget.DOKill(true);

        // SetUpdate(true) = chay theo GIO THAT. Luc vuot moc, SimpleSuction ha timeScale xuong
        // 0.05 de lam hitstop; de cu nhun chay theo gio thuong thi no bi keo cham thanh ie oai
        // dung khoanh khac dang can dut khoat nhat.
        _punchTarget.DOPunchScale(Vector3.one * punch, punchDuration, punchVibrato, 1f)
                    .SetUpdate(true);
    }

    void OnDestroy()
    {
        if (_punchTarget != null) _punchTarget.DOKill();
    }
}
