using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MUI TEN CHI HUONG BOT - cho biet con nao dang o ngoai khung hinh va nam phia nao.
///
/// Gan len mot object UI PHU KIN CANVAS (vd HUD trong UI.prefab). Moi bot dang song ma nam NGOAI
/// khung hinh se duoc mot mui ten bam o mep man hinh, quay ve phia no.
///
/// CHI HIEN KHI O NGOAI KHUNG: bot dang nam giua man hinh thi nguoi choi nhin thay no roi, them
/// mui ten chi tro lai chinh no la thua va con che mat. Vao khung la mui ten tat.
///
/// MAU LAY TU Creature.skinColor - dung mau ma DevourVfx dung cho hat bay ra khi hut con do. Nho
/// vay nhin mau mui ten la biet con nao dang toi, khop voi cai minh thay luc danh nhau.
///
/// KHONG co OverlapSphere, khong quet gi ca: danh sach sinh vat da nam san trong GameManager, moi
/// frame chi la vai phep chieu toa do. Mui ten duoc GOM LAI DUNG (pool) chu khong sinh/huy.
/// </summary>
[DisallowMultipleComponent]
public class AiArrowUI : MonoBehaviour
{
    [Header("Tham chieu")]
    [Tooltip("PREFAB mot mui ten (AiArrow). Cau truc: goc - Icon (Image tam giac) - Distance (TMP so met).\n" +
             "Sua hinh thuc thi sua thang trong prefab, khong phai dong toi code.\n\n" +
             "Tam giac trong prefab phai CHI SANG PHAI (+X): code xoay theo goc tuyet doi nen huong goc\n" +
             "sai la mui ten chi lech dung bay nhieu do.")]
    [SerializeField] private AiArrowMarker _arrowPrefab;

    [Tooltip("Camera dung de chieu toa do. De trong = Camera.main")]
    [SerializeField] private Camera _cam;

    [Header("Hinh thuc")]
    [Tooltip("LE AN TOAN tinh tu mep man hinh vao (pixel theo do phan giai tham chieu cua Canvas).\n" +
             "De mui ten khong bi tai tho hay notch cat mat, va khong dam vao joystick")]
    public float edgePadding = 90f;

    [Tooltip("Co mui ten (pixel theo do phan giai tham chieu cua Canvas).\n" +
             "0 = GIU NGUYEN co da dung trong prefab - dat 0 neu ban muon chinh co bang tay trong prefab")]
    public float arrowSize = 90f;

    [Tooltip("BAT: to mui ten theo mau skin cua chinh con bot do - nhin mau la biet con nao.\n" +
             "TAT: moi mui ten deu dung fallbackColor")]
    public bool colorFromSkin = true;

    [Tooltip("Mau dung khi tat colorFromSkin (hoac bot khong co mau)")]
    public Color fallbackColor = Color.white;

    [Tooltip("BAT: chi tro MOI con bot dang song va o ngoai khung hinh, xa may cung tro.\n" +
             "TAT: chi tro nhung con nam trong nearbyRadius - man hinh sach hon, va mui ten tro thanh\n" +
             "canh bao 'co dua nao ke ben' thay vi ban do toan cuc.")]
    public bool showAllAi = true;

    [Tooltip("CHI DUNG KHI TAT showAllAi: chi tro bot nam trong ban kinh nay (world unit).\n" +
             "Do bang khoang cach PHANG tren mat dat, khong tinh cao thap.")]
    public float nearbyRadius = 30f;

    [Tooltip("TRAN so mui ten hien cung luc. Hien tai van chi co 3 bot, nhung neu sau nay nang so\n" +
             "bot len thi day la cai chan man hinh khoi day mui ten")]
    public int maxArrows = 8;

    [Header("Khoang cach")]
    [Tooltip("BAT: hien them so met duoi mui ten")]
    public bool showDistance = true;

    [Tooltip("Co chu so met. 0 = giu nguyen co da dat trong prefab")]
    public float distanceFontSize = 34f;

    [Tooltip("So met dat cach mui ten bao nhieu, tinh VAO PHIA TRONG man hinh.\n" +
             "Dat vao trong chu khong ra ngoai: ra ngoai la chu bi mep man hinh cat mat")]
    public float distanceOffset = 70f;

    private readonly List<AiArrowMarker> _pool = new List<AiArrowMarker>();
    private RectTransform _self;
    private bool _warned;

    void Awake()
    {
        _self = transform as RectTransform;
        if (_self == null)
            Debug.LogError("[AiArrowUI] Phai gan len object UI co RectTransform (vd HUD trong UI.prefab).", this);
    }

    /// <summary>
    /// Chay o LateUpdate chu khong Update: camera bam theo nguoi choi va duoc cap nhat trong
    /// LateUpdate (CameraFollow). Chieu toa do o Update la dung vi tri camera cua FRAME TRUOC,
    /// mui ten se tre mot nhip va rung khi nguoi choi chay nhanh.
    /// </summary>
    void LateUpdate()
    {
        if (_self == null) { HideFrom(0); return; }
        if (_arrowPrefab == null)
        {
            if (!_warned) { Debug.LogWarning("[AiArrowUI] Chua keo prefab mui ten vao o 'Arrow Prefab'.", this); _warned = true; }
            HideFrom(0);
            return;
        }
        if (!GameManager.HasInstance) { HideFrom(0); return; }

        Camera cam = _cam != null ? _cam : Camera.main;
        Creature player = GameManager.Instance.Player;
        if (cam == null || player == null || player.IsDead) { HideFrom(0); return; }

        IReadOnlyList<Creature> all = GameManager.Instance.Creatures;
        Vector2 half = new Vector2(Screen.width, Screen.height) * 0.5f;

        // Le tinh bang don vi Canvas -> doi sang pixel that. Khong nhan scaleFactor thi tren may
        // do phan giai cao le se bi teo lai con vai pixel, mui ten dinh sat mep.
        float scale = _self.lossyScale.x > 0.0001f ? _self.lossyScale.x : 1f;
        float padPx = edgePadding * scale;
        Vector2 limit = new Vector2(Mathf.Max(1f, half.x - padPx), Mathf.Max(1f, half.y - padPx));

        int used = 0;
        for (int i = 0; i < all.Count && used < Mathf.Max(0, maxArrows); i++)
        {
            Creature c = all[i];
            if (c == null || c == player || c.IsDead) continue;

            // Do khoang cach PHANG truoc: no vua la bo loc 'gan thoi', vua la so met hien ra sau do.
            // Loc bang phep tru vector RE HON nhieu so voi WorldToScreenPoint nen dat len truoc.
            Vector3 d3 = c.Center - player.Center;
            d3.y = 0f;
            float flatDist = d3.magnitude;
            if (!showAllAi && flatDist > nearbyRadius) continue;

            Vector3 sp = cam.WorldToScreenPoint(c.Center);

            // z < 0 = nam SAU camera: toa do chieu ra bi lat nguoc ca hai truc, khong lat lai thi
            // mui ten chi dung huong nguoc. Camera game la ortho chuc xuong nen ca nay hiem, nhung
            // de day cho chac neu sau doi sang perspective.
            bool behind = sp.z < 0f;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            bool onScreen = !behind && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
            if (onScreen) continue;   // dang nhin thay no roi, khong can chi tro

            Vector2 dir = new Vector2(sp.x, sp.y) - half;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

            // DAY RA MEP: keo dai vector tam->bot cho toi khi cham canh gan nhat cua khung an toan.
            // Lay MIN cua hai he so de diem cham nam tren canh dau tien gap, khong loi ra ngoai goc.
            float sx = limit.x / Mathf.Max(0.0001f, Mathf.Abs(dir.x));
            float sy = limit.y / Mathf.Max(0.0001f, Mathf.Abs(dir.y));
            Vector2 edgePx = half + dir * Mathf.Min(sx, sy);

            AiArrowMarker a = Get(used);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_self, edgePx, null, out local);

            // GOC dat tai mep va KHONG xoay; chi rieng Icon xoay theo huong bot.
            RectTransform rt = (RectTransform)a.transform;
            rt.anchoredPosition = local;

            Color want = colorFromSkin ? c.skinColor : fallbackColor;
            want.a = 1f;

            if (a.IconRect != null)
            {
                a.IconRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                if (arrowSize > 0.001f) a.IconRect.sizeDelta = new Vector2(arrowSize, arrowSize);
            }
            if (a.Icon != null && a.Icon.color != want) a.Icon.color = want;

            // SO MET: day LUI VAO PHIA TRONG man hinh so voi mui ten, va KHONG xoay theo no.
            bool wantTxt = showDistance && a.Distance != null;
            if (wantTxt)
            {
                Vector2 inward = dir.sqrMagnitude > 0.0001f ? -dir.normalized : Vector2.down;
                a.DistanceRect.anchoredPosition = inward * distanceOffset;   // local, tinh tu goc mui ten
                a.DistanceRect.localRotation = Quaternion.identity;
                if (distanceFontSize > 0.001f) a.Distance.fontSize = distanceFontSize;
                if (a.Distance.color != want) a.Distance.color = want;

                string label = FormatDistance(flatDist);
                if (a.Distance.text != label) a.Distance.text = label;
            }
            if (a.Distance != null && a.Distance.gameObject.activeSelf != wantTxt)
                a.Distance.gameObject.SetActive(wantTxt);

            used++;
        }

        HideFrom(used);
    }

    /// <summary>
    /// RUT GON SO MET de no khong bao gio dai qua 3 chu so.
    ///
    ///   duoi 1000  -> "23m"     (lam tron, khong lay so le: chenh nhau nua met khong ai quan tam)
    ///   tu 1000    -> "1.2km"
    ///
    /// Chinh la de "so khong qua day": man hinh dien thoai, chu be, mot day "1247m" doc rat met
    /// ma cung khong noi them duoc gi so voi "1.2km".
    /// </summary>
    private static string FormatDistance(float d)
    {
        if (d >= 1000f) return (d / 1000f).ToString("0.0") + "km";
        return Mathf.RoundToInt(d) + "m";
    }

    /// <summary>Lay mui ten thu i trong pool, thieu thi Instantiate them. Khong bao gio Destroy.</summary>
    private AiArrowMarker Get(int i)
    {
        while (_pool.Count <= i)
        {
            AiArrowMarker a = Instantiate(_arrowPrefab, _self);
            a.name = "AiArrow_" + _pool.Count;

            // Neo o TAM: anchoredPosition tinh tu tam man hinh, dung dung he toa do ma phep day
            // ra mep dang tinh. Prefab co the duoc dung voi neo khac nen ep lai o day cho chac.
            RectTransform rt = (RectTransform)a.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;

            _pool.Add(a);
        }

        if (!_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(true);
        return _pool[i];
    }

    /// <summary>Tat cac mui ten tu chi so 'from' tro di - so bot dang ngoai khung thay doi lien tuc.</summary>
    private void HideFrom(int from)
    {
        for (int i = from; i < _pool.Count; i++)
            if (_pool[i] != null && _pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
    }

    void OnDisable() { HideFrom(0); }
}
