using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Bang ten + cap do cho nhan vat, dat tren mot CANVAS World Space nam TRONG prefab Player.
/// Component gan tren object Canvas (con cua Player): moi frame quay Canvas ve phia camera
/// (billboard) va cap nhat chu = ten + "Lv N" (doc tu SimpleSuction o cha).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayerNameTag : MonoBehaviour
{
    [Header("Noi dung")]
    public string playerName = "Player";

    [Tooltip("Doc cap do. De trong = tu tim SimpleSuction o cha")]
    public SimpleSuction suction;

    [Tooltip("TMP hien chu. De trong = tu tim TMP_Text trong con")]
    public TMP_Text label;

    [Tooltip("Dinh dang: {0}=ten, {1}=cap.\n" +
             "Mac dinh: CAP o tren va co chu day du, TEN o duoi va nho lai 65% - cap la thu nguoi\n" +
             "choi phai doc trong tich tac de biet co nen lao vao hay chay, con ten chi de nhan dien")]
    public string format = "Lv {1}\n<size=65%>{0}</size>";

    [Header("Billboard")]
    [Tooltip("Camera de quay mat vao. De trong = Camera.main")]
    public Camera cam;

    public bool billboard = true;

    [Tooltip("Giu kich thuoc canvas KHONG doi du player scale to (bu lai theo scale cha)")]
    public bool keepConstantSize = false;

    [Tooltip("Scale world giu co dinh khi bat keepConstantSize")]
    public float constantScale = 0.01f;

    [Header("Nhun mot cai khi LEN CAP")]
    [Range(0f, 1f)]
    [Tooltip("Do manh cu nhun khi level TANG (0.25 = phinh them 25% roi co ve). 0 = tat.\n\n" +
             "Chi nhun khi len cap, KHONG nhun khi tut cap: tut cap la luc dang bi hut, man hinh\n" +
             "da du thu dang dong day roi, them mot cai nhay nua chi lam roi them.")]
    public float levelPunch = 0.25f;

    [Tooltip("Mot cai nhun keo dai bao lau (giay)")]
    public float levelPunchDuration = 0.25f;

    [Range(0, 10)]
    [Tooltip("So nhip rung trong mot cai nhun. 2 = phinh-co-phinh nhe roi dung")]
    public int levelPunchVibrato = 2;

    void OnEnable() { Resolve(); Refresh(); }
    void LateUpdate() { Resolve(); Refresh(); }

    private void Resolve()
    {
        if (suction == null) suction = GetComponentInParent<SimpleSuction>();
        if (cam == null) cam = Camera.main;
        if (label == null) label = GetComponentInChildren<TMP_Text>();

        // Chup scale goc MOT LAN: cu nhun tra ve dung day nay, khong bao gio troi di sau nhieu lan
        if (label != null && !_baseCaptured) { _baseScale = label.transform.localScale; _baseCaptured = true; }
    }

    private void Refresh()
    {
        if (billboard && cam != null)
            // COPY THANG rotation cua camera, KHONG dung LookRotation(vi_tri - vi_tri_cam).
            //
            // Camera cua game la ORTHOGRAPHIC: phep chieu song song, moi vat len man hinh theo
            // CUNG MOT huong nhin. Ngam theo "huong tu camera toi vat" la moi bang ten ra mot
            // forward khac nhau, roi LookRotation truc giao hoa 'up' theo cai forward do -> vat
            // cang lech khoi truc camera thi bang ten cang bi XOAY NGHIENG.
            //
            // So do that (camera chuc xuong 55 do): nguoi choi dung giua man hinh chi nghieng 2 do
            // nen khong ai de y, nhung bot o ria nghieng toi 41.6 do. Do la ly do loi nay chi
            // "thay o bot" - that ra player cung sai, chi la sai it.
            //
            // Copy rotation thi moi bang ten deu song song mat phang camera: khong nghieng, khong
            // meo, va tat ca giong het nhau. +Z van huong ra xa camera nen chu doc binh thuong.
            transform.rotation = cam.transform.rotation;

        if (keepConstantSize && transform.parent != null)
        {
            Vector3 ls = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                constantScale / Mathf.Max(0.0001f, ls.x),
                constantScale / Mathf.Max(0.0001f, ls.y),
                constantScale / Mathf.Max(0.0001f, ls.z));
        }

        if (label != null)
        {
            int lvl = suction != null ? suction.Level : 1;
            // Chi doi text khi level/ten thay doi -> tranh string.Format + regen mesh TMP moi frame (nang tren mobile)
            if (lvl != _lastLevel || playerName != _lastName)
            {
                label.text = string.Format(format, playerName, lvl);

                // _lastLevel = -1 la lan ve DAU TIEN (vua bat len), khong phai len cap -> khong nhun
                bool leveledUp = _lastLevel > 0 && lvl > _lastLevel;

                _lastLevel = lvl;
                _lastName = playerName;

                if (leveledUp) PlayLevelPunch();
            }
        }
    }

    /// <summary>
    /// NHUN MOT CAI: phinh nhanh roi co ve dung scale goc.
    ///
    /// Nhun tren LABEL chu khong tren ca NameTag: NameTag con cong them thanh ghi (StruggleBar),
    /// nhun ca cum thi thanh ghi cung giat theo - ma no dang la thu nguoi choi phai doc chinh xac
    /// trong luc bi hut.
    ///
    /// GIET CU CU TRUOC KHI BAN CU MOI va tra scale ve day: an lien tuc thi level nhay may lan
    /// trong mot giay, hai tween chong nhau se cong don scale va chu cu the phinh to mai.
    /// </summary>
    private void PlayLevelPunch()
    {
        if (!Application.isPlaying) return;   // [ExecuteAlways]: dung ban tween trong Edit mode
        if (label == null || levelPunch <= 0.001f || levelPunchDuration <= 0.001f) return;

        Transform t = label.transform;
        t.DOKill();
        t.localScale = _baseScale;
        t.DOPunchScale(Vector3.one * levelPunch, levelPunchDuration, levelPunchVibrato, 0.6f)
            .SetTarget(t);
    }

    void OnDisable()
    {
        // Tween con song ma object da tat/huy thi DOTween nem loi - va scale se ket lai o giua cu nhun
        if (label != null)
        {
            label.transform.DOKill();
            if (_baseCaptured) label.transform.localScale = _baseScale;
        }
    }

    private int _lastLevel = -1;
    private string _lastName;
    private Vector3 _baseScale = Vector3.one;
    private bool _baseCaptured;
}
