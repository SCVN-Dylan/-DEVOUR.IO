using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Vung hut hinh non kieu Kirby toa tu mieng nhan vat ve phia truoc.
///
/// Pipeline: Scan (OverlapSphere + loc goc non)
///        -> Capture (tat physics)
///        -> Struggle (vat the rung + nga ve phia mieng, chua di dau ca)
///        -> Flight  (but khoi cho, bay xoan oc vao mieng, nhanh dan)
///        -> Devour  (cong diem, huy)
///
/// Diem khac ban cu: co giai doan giang co truoc khi bay, va duong bay la xoan oc
/// chu khong phai duong thang. Do la hai thu lam nen cam giac "bi hut" thay vi
/// "bi teleport ve mieng".
///
/// Luc hut khong phai bat/tat: moi diem trong non co mot do manh 0..1 tinh theo
/// khoang cach + do lech goc. Vat nang (resistance cao) nam ria non se rung mai
/// ma khong vao duoc, phai lai gan va nhin thang vao no.
///
/// Huong non bam theo mouth.forward, ma mouth la con cua nhan vat nen non tu quay
/// theo huong nhin do RbMovement dat. Khong can code xoay rieng.
///
/// De trong o Mouth va VFX thi luc chay se tu tao object con "Mouth" + "SuctionVFX".
/// </summary>
[DisallowMultipleComponent]
public class MouthSuction : MonoBehaviour
{
    public const string MouthObjectName = "Mouth";

    [Header("Mieng")]
    [Tooltip("Diem hut. De trong se tu tao object con ten Mouth theo offset ben duoi")]
    public Transform mouth;

    [Tooltip("Vi tri mieng theo local space cua nhan vat, chi dung khi tu tao Mouth")]
    public Vector3 mouthLocalOffset = new Vector3(0f, 0f, 0.55f);

    [Header("Vung hut hinh non")]
    [Tooltip("Chieu dai hinh non tinh tu mieng")]
    public float range = 6f;

    [Tooltip("Goc mo cua non tinh bang do. 60 = xoe 30 do moi ben")]
    [Range(5f, 179f)] public float coneAngle = 60f;

    [Tooltip("Cat bo phan non thut xuong duoi mat dat, chi giu tu mat dat tro len.\n\n" +
             "Mieng cao 1m ma day non rong 3.46m thi non thoc xuong tan -2.46m: mot nua\n" +
             "vung hut nam trong long duong, VFX phun vao do coi nhu mat trang")]
    public bool clipBelowGround = true;

    [Tooltip("Cao do mat dat trong world space. Diem thap hon muc nay coi nhu ngoai vung hut")]
    public float groundY = 0f;

    [Tooltip("Layer duoc phep hut")]
    public LayerMask suckableLayers = ~0;

    [Tooltip("Giay giua hai lan quet. Nho hon = nhay hon nhung ton CPU hon")]
    public float scanInterval = 0.08f;

    [Tooltip("Quet rong hon Range bao nhieu roi moi loc lai theo non.\n" +
             "Can bang do rong nhat cua vat the to nhat, khong thi vat nam sat ria non\n" +
             "se bao la trong non ma khong bao gio bi quet thay")]
    public float scanPadding = 6f;

    [Tooltip("So vat the hut cung luc toi da. PlayerLevel ghi de o nay moi khi len cap:\n" +
             "cap cang cao thi ngoam duoc cang nhieu mon mot luc.\n\n" +
             "Day la cai van chinh nhip lon len - khong phai toc do bay, ma la so mon\n" +
             "duoc phep bay cung luc")]
    public int maxCaptured = 2;

    [Header("Nhip nuot")]
    [Tooltip("Gioi han so vat nuot duoc moi giay. 0 = TAT (mac dinh).\n\n" +
             "De 0 thi vat toi mieng la nuot ngay, nhip lon len do maxCaptured quyet dinh.\n" +
             "Chi bat neu muon them mot tran cung tren so vat/giay")]
    public float swallowsPerSecond = 0f;

    [Tooltip("Chi co tac dung khi Swallows Per Second > 0: so luot duoc phep don lai")]
    [Range(1f, 10f)] public float swallowBurst = 2f;

    [Tooltip("BAT: da lot vao non roi thi hut den cung, ke ca khi nhan vat quay di huong khac.\n" +
             "TAT: ra khoi vung hut la nha ngay, vat the roi xuong dat (Devourable lo phan roi)")]
    public bool keepWhenOutOfCone = false;

    [Tooltip("Tinh ca kich thuoc vat the khi kiem tra non. 1 = vat to thi de hut hon,\n" +
             "0 = coi moi vat la mot diem (rat kho ngam vat cao dung sat nguoi)")]
    [Range(0f, 1f)] public float targetSizeAssist = 1f;

    [Tooltip("Tran (do) ma kich thuoc vat the duoc phep noi rong goc non.\n\n" +
             "Khong co tran nay thi vat cang lai gan goc cang no ra vo han: xe o 2m duoc\n" +
             "tinh la trong non tan 80 do, toa nha trong 5m thi quay huong nao cung dinh -\n" +
             "tuc la da hut la hut den cung, khong bao gio nha ra duoc.\n\n" +
             "15 do vua du de ngam trung cai cay cao dung sat nguoi, ma quay di la roi ngay")]
    [Range(0f, 60f)] public float maxSizeAssistAngle = 15f;

    [Header("Cap do")]
    [Tooltip("Chi hut duoc vat co Required Level <= Suction Level. Tat di thi hut duoc tat ca")]
    public bool useLevelGate = true;

    [Tooltip("Cap hut hien tai. PlayerLevel ghi de moi khi len cap")]
    public int suctionLevel = 1;

    [Tooltip("Vat qua level nam trong non se rung nhe tai cho, cho nguoi choi biet\n" +
             "la no co cam nhan duoc luc hut nhung minh chua du cap")]
    public bool shakeTooBigTargets = true;

    [Header("An khi cham")]
    [Tooltip("Dung vao vat the la nuot luon, khong can nam trong non hut.\n" +
             "Van phai du cap: vat qua cap thi cham vao chi bi chan duong nhu buc tuong")]
    public bool devourOnContact = true;

    [Header("Do manh")]
    [Tooltip("He so nhan chung. Tang len thi vat nang cung bi lua di nhanh hon")]
    public float suctionPower = 1f;

    [Tooltip("Do manh con lai o RIA XA nhat cua non. 1 = xa gan deu nhu nhau, 0 = ra toi day non thi het luc")]
    [Range(0f, 1f)] public float rangeEdgeStrength = 0.45f;

    [Tooltip("Do manh con lai o VIEN non. 1 = ca non manh nhu nhau, 0 = ria non khong hut duoc gi")]
    [Range(0f, 1f)] public float coneEdgeStrength = 0.55f;

    [Tooltip("Do cong khi giam theo khoang cach. >1 = giu manh lau roi moi tut o gan day non")]
    [Range(0.5f, 4f)] public float distanceFalloff = 1.5f;

    [Tooltip("Do cong khi giam theo goc lech. >1 = giu manh lau roi moi tut o sat vien")]
    [Range(0.5f, 4f)] public float angleFalloff = 1.5f;

    [Tooltip("Giay de luc hut len/xuong het cong suat khi bat/tat. Tranh bat tat giat cuc")]
    public float rampTime = 0.12f;

    [Header("Che do hut")]
    [Tooltip("GameManager ghi de o nay moi khi doi. BAT: bo qua pha giang co, item vao non\n" +
             "la bay vao mieng ngay o toc do toi da.\n\n" +
             "Khong lien quan toi vung hut: ra khoi non thi van bi nha ra nhu thuong")]
    public bool instantDevour = false;

    [Header("Giai doan giang co")]
    [Tooltip("Toc do vat the troi ve mieng trong luc con dang giang co. De qua thap thi\n" +
             "giai doan giang co nhin nhu vat the dung im, nguoi choi tuong la hut khong an")]
    public float struggleDrift = 1.6f;

    [Tooltip("Nhan vao resistance cua vat the. Tang len = moi thu deu lau but ra hon")]
    public float struggleTimeScale = 1f;

    [Header("Duong bay xoan oc")]
    [Tooltip("Toc do luc vua but ra, de vat the giat nhe roi moi lao vao")]
    public float startPullSpeed = 2.5f;

    [Tooltip("Toc do khi vat the con o RIA XA cua non. De thap cho no le te troi vao tu tu")]
    public float farPullSpeed = 4f;

    [Tooltip("Toc do khi vat the da SAT MIENG. Chenh lech voi Far Pull Speed cang lon thi cu vut cuoi cang da")]
    public float maxPullSpeed = 34f;

    [Tooltip("Cu vut don ve gan mieng den muc nao.\n" +
             "1 = tang deu tu xa vao gan, 3 = giu cham gan het duong roi bung toc o doan cuoi")]
    [Range(0.5f, 5f)] public float nearBoostSharpness = 2.5f;

    [Tooltip("Don vi/giay^2. Tran gia toc, quyet dinh vat the bam sat duong cong toc do den dau.\n" +
             "De thap (~70) thi vat the cham toi mieng TRUOC khi kip tang toc, cu vut cuoi bi cut ngang.\n" +
             "Chi ha xuong khi co chu y muon vat nang y ach duoi khong kip luc hut")]
    public float pullAcceleration = 300f;

    [Tooltip("Do xoan quanh truc non. 0 = bay thang nhu ban cu, 1.5 = xoay ro ret")]
    [Range(0f, 3f)] public float swirl = 1.1f;

    [Tooltip("Cach mieng duoi khoang nay thi coi nhu da nuot")]
    public float swallowDistance = 0.3f;

    [Tooltip("Bat dau thu nho vat the khi con cach mieng bao nhieu")]
    public float shrinkDistance = 1.6f;

    [Header("Than nhan vat")]
    [Tooltip("Model phong len khi dang hut. De trong = bo qua")]
    public Transform bodyTransform;

    [Tooltip("Phong to bao nhieu phan tram khi hut het cong suat")]
    [Range(0f, 0.5f)] public float bodyInflate = 0.12f;

    [Tooltip("Nhip rung cua than khi dang hut")]
    public float bodyPulseSpeed = 14f;

    [Header("VFX")]
    [Tooltip("Prefab VFX da dat san duoi Mouth. De trong se tu tim trong cac object con cua Mouth.\n" +
             "Prefab goc: Assets/Prefabs/VFX/SuctionVFX.prefab")]
    public SuctionConeVfx vfx;

    [Tooltip("Canh bao trong Console neu khong tim thay VFX nao")]
    public bool warnIfVfxMissing = true;

    [Header("Su kien")]
    [Tooltip("Ban ra khi mot vat the but khoi cho va bat dau bay vao mieng")]
    public UnityEvent onTargetPulled;

    [Tooltip("Ban ra moi lan nuot xong mot vat the")]
    public UnityEvent onSwallowed;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0.35f, 0.9f, 1f, 0.9f);

    /// <summary>
    /// Ban ra NGAY TRUOC khi vat the bi huy, nen listener con doc duoc scoreValue,
    /// Radius, transform... cua no. PlayerGrowth dung cai nay de tinh do to len.
    /// </summary>
    public event System.Action<Devourable> Swallowed;

    /// <summary>
    /// Ban ra khi nguoi choi ngam trung mot vat CHUA DU CAP de hut (lan dau no lot
    /// vao non). PlayerLevel bat cai nay de hien "Can cap N".
    /// </summary>
    public event System.Action<Devourable> Blocked;

    /// <summary>Tat de khoa hut tam thoi (cutscene, dang hoi chieu...).</summary>
    public bool SuctionEnabled { get; set; } = true;

    /// <summary>So vat the dang bi giu (ca dang giang co lan dang bay).</summary>
    public int CapturedCount { get { return _captured.Count; } }

    /// <summary>So vat qua cap dang nam trong non va rung tai cho.</summary>
    public int ResistingCount { get { return _resisting.Count; } }

    /// <summary>Cong suat hut hien tai 0..1, dung cho VFX va animation.</summary>
    public float Intensity { get { return _intensity; } }

    public Vector3 MouthPosition { get { return EnsureMouth().position; } }

    /// <summary>Danh sach cac vung hut dang bat, de SuctionReactor lay gio ma phan ung.</summary>
    private static readonly List<MouthSuction> _activeFields = new List<MouthSuction>();

    private readonly List<Devourable> _captured = new List<Devourable>();
    private readonly List<Devourable> _resisting = new List<Devourable>();
    private Collider[] _hits = new Collider[64];
    private float _scanTimer;
    private float _intensity;
    private float _swallowBudget;
    private Vector3 _bodyStartScale = Vector3.one;
    private bool _bodyCached;

    void Awake()
    {
        EnsureMouth();
        EnsureVfx();
        CacheBody();
    }

    void OnEnable()
    {
        if (!_activeFields.Contains(this)) _activeFields.Add(this);
    }

    void OnDisable()
    {
        _activeFields.Remove(this);
        ReleaseAll();
        _intensity = 0f;
        ApplyBodyScale();
        if (vfx != null) vfx.SetIntensity(0f);
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        // Len/xuong cong suat tu tu. Nho vay khi game over hay bi khoa hut thi
        // luong gio tat dan chu khong bien mat giua chung.
        float target = CanSuck() ? 1f : 0f;
        _intensity = rampTime > 0.0001f
            ? Mathf.MoveTowards(_intensity, target, deltaTime / rampTime)
            : target;

        // Gao nuoc: moi giay do them swallowsPerSecond luot, day thi thoi
        _swallowBudget = Mathf.Min(_swallowBudget + swallowsPerSecond * deltaTime, Mathf.Max(1f, swallowBurst));

        if (vfx != null) vfx.SetIntensity(_intensity);
        ApplyBodyScale();

        if (_intensity <= 0f)
        {
            ReleaseAll();
            return;
        }

        _scanTimer -= deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = Mathf.Max(scanInterval, 0.01f);
            Scan();
        }

        TickCaptured(deltaTime);
        TickResisting(deltaTime);
    }

    /// <summary>
    /// Nhung vat qua level dang nam trong non: rung nhe cho co tin hieu, va bo ra khoi
    /// danh sach ngay khi tuot khoi vung hut hoac khi nhan vat da len du cap.
    /// </summary>
    private void TickResisting(float deltaTime)
    {
        if (_resisting.Count == 0) return;

        Vector3 mouthPosition = EnsureMouth().position;

        for (int i = _resisting.Count - 1; i >= 0; i--)
        {
            Devourable target = _resisting[i];
            if (target == null)
            {
                _resisting.RemoveAt(i);
                continue;
            }

            float strength = StrengthAt(target.Center, TargetRadius(target));
            bool stillTooBig = useLevelGate && target.requiredLevel > suctionLevel;

            if (strength <= 0f || !stillTooBig || !shakeTooBigTargets)
            {
                target.StopResist();
                _resisting.RemoveAt(i);
                continue;
            }

            target.TickResist(deltaTime, mouthPosition, strength);
        }
    }

    // ---------------------------------------------------------------- truy van

    /// <summary>Diem bat ky co nam trong non hut khong.</summary>
    public bool IsInCone(Vector3 worldPoint)
    {
        return IsInCone(worldPoint, 0f);
    }

    /// <summary>
    /// Hinh cau (tam + ban kinh) co cham vao non hut khong.
    ///
    /// Coi vat the la mot diem la sai lam de thay nhat: cai cay cao 3m dung ngay
    /// truoc mat co tam bounds nam cao qua dau, do goc theo tam thi ra 30.3 do va
    /// bi loai khoi non 30 do, trong khi than cay dang gan nhu cham vao mieng.
    /// </summary>
    public bool IsInCone(Vector3 worldPoint, float radius)
    {
        if (IsUnderGround(worldPoint, radius)) return false;

        Transform m = EnsureMouth();
        Vector3 toPoint = worldPoint - m.position;
        float distance = toPoint.magnitude;

        if (distance - radius > range) return false;
        if (distance < 0.0001f) return true;

        float angle = Vector3.Angle(m.forward, toPoint) - AngularAssist(distance, radius);
        return angle <= coneAngle * 0.5f;
    }

    /// <summary>
    /// Goc duoc noi them nho vat the co kich thuoc, da chan tran.
    ///
    /// Khong chan tran thi asin(banKinh/khoangCach) tien toi 90 do khi vat lai gan,
    /// nghia la non phinh ra thanh gan nhu hinh cau: vat da bi tom thi cang bay vao
    /// cang khong the thoat, quay nguoi di huong nao no cung van "trong non".
    /// Do la ly do phai co maxSizeAssistAngle.
    /// </summary>
    private float AngularAssist(float distance, float radius)
    {
        if (radius <= 0f || distance <= 0.0001f) return 0f;

        float raw = Mathf.Asin(Mathf.Clamp01(radius / distance)) * Mathf.Rad2Deg;
        return Mathf.Min(raw, maxSizeAssistAngle);
    }

    /// <summary>
    /// Hinh cau nam tron duoi mat dat thi coi nhu ngoai vung hut.
    ///
    /// Xet ca ban kinh chu khong chi cai tam: vat the dat tren mat dat co tam nam thap
    /// hon groundY (vd tam bounds cua mot tam tham) van con phan noi len tren, van an duoc.
    /// Chi bo khi khong con mot ti nao o tren mat dat.
    /// </summary>
    private bool IsUnderGround(Vector3 worldPoint, float radius)
    {
        return clipBelowGround && worldPoint.y + radius < groundY;
    }

    /// <summary>
    /// Khoang cach tu mat dat len toi mieng, dung cho SuctionConeVfx.FitToCone de biet
    /// phai cat day non o dau. Tra ve so am khi khong cat.
    /// </summary>
    public float MouthHeightAboveGround
    {
        get { return clipBelowGround ? EnsureMouth().position.y - groundY : -1f; }
    }

    /// <summary>
    /// Do manh cua luc hut tai mot diem, 0 = ngoai vung.
    /// Gan mieng va nam giua tam non thi manh nhat, ra ria va ra xa thi yeu dan.
    ///
    /// Quan trong: o ria non luc hut tut ve rangeEdgeStrength/coneEdgeStrength chu
    /// KHONG ve 0. Neu cho ve 0 thi nhan hai he so lai se ra gan bang khong o phan
    /// lon the tich non - nhin nhu vung hut chi rong 2m trong khi gizmo ve 6m.
    /// </summary>
    public float StrengthAt(Vector3 worldPoint)
    {
        return StrengthAt(worldPoint, 0f);
    }

    /// <summary>
    /// Nhu tren nhung tinh theo hinh cau: vat cang to thi cang de lot vao non va cang
    /// bi hut manh, vi phan gan nhat cua no moi la phan chiu gio chu khong phai cai tam.
    /// </summary>
    public float StrengthAt(Vector3 worldPoint, float radius)
    {
        if (_intensity <= 0f) return 0f;
        if (IsUnderGround(worldPoint, radius)) return 0f;

        Transform m = EnsureMouth();
        Vector3 toPoint = worldPoint - m.position;
        float distance = toPoint.magnitude;

        if (distance - radius > range) return 0f;
        if (distance < 0.0001f) return _intensity * suctionPower;

        float halfAngle = coneAngle * 0.5f;
        float angle = Mathf.Max(0f, Vector3.Angle(m.forward, toPoint) - AngularAssist(distance, radius));
        if (angle > halfAngle) return 0f;

        float effectiveDistance = Mathf.Max(0f, distance - radius);

        float byDistance = Mathf.Lerp(1f, rangeEdgeStrength, Mathf.Pow(effectiveDistance / range, distanceFalloff));
        float byAngle = Mathf.Lerp(1f, coneEdgeStrength, Mathf.Pow(angle / halfAngle, angleFalloff));

        return _intensity * suctionPower * byDistance * byAngle;
    }

    /// <summary>
    /// Lay luong gio manh nhat dang thoi vao mot diem. SuctionReactor dung cai nay
    /// de cong nguoi/rung lac ma khong can biet vung hut nao dang o gan.
    /// </summary>
    public static bool SampleWind(Vector3 worldPoint, out Vector3 towardMouth, out float strength)
    {
        towardMouth = Vector3.zero;
        strength = 0f;

        for (int i = 0; i < _activeFields.Count; i++)
        {
            MouthSuction field = _activeFields[i];
            if (field == null) continue;

            float s = field.StrengthAt(worldPoint);
            if (s <= strength) continue;

            strength = s;
            towardMouth = field.MouthPosition - worldPoint;
        }

        return strength > 0f;
    }

    // ------------------------------------------------------------------ setup

    /// <summary>Tao (hoac tim lai) object con lam diem hut.</summary>
    public Transform EnsureMouth()
    {
        if (mouth != null) return mouth;

        Transform found = transform.Find(MouthObjectName);
        if (found == null)
        {
            GameObject go = new GameObject(MouthObjectName);
            found = go.transform;
            found.SetParent(transform, false);
            found.localPosition = mouthLocalOffset;
            found.localRotation = Quaternion.identity;
            found.localScale = Vector3.one;
        }

        mouth = found;
        return mouth;
    }

    /// <summary>
    /// Tim prefab VFX da duoc dat san duoi Mouth. Khong tu tao particle bang code
    /// nua: muon doi hinh thu luong gio thi mo prefab ra sua, khong dong vao script.
    /// </summary>
    private void EnsureVfx()
    {
        if (vfx == null) vfx = EnsureMouth().GetComponentInChildren<SuctionConeVfx>(true);

        if (vfx != null)
        {
            vfx.SetIntensity(0f);
            return;
        }

        if (warnIfVfxMissing)
            Debug.LogWarning("[MouthSuction] Chua co VFX. Keo Assets/Prefabs/VFX/SuctionVFX.prefab vao duoi " +
                             MouthObjectName + " cua " + name + ", hoac chay Tools/Devour/Gan prefab VFX vao nhan vat dang chon.", this);
    }

    private void CacheBody()
    {
        if (bodyTransform == null) return;
        _bodyStartScale = bodyTransform.localScale;
        _bodyCached = true;
    }

    private float TargetRadius(Devourable target)
    {
        return target.Radius * targetSizeAssist;
    }

    /// <summary>
    /// Tru mot luot nuot. Het luot thi tra ve false va vat the phai cho.
    ///
    /// Dung chung cho ca hut lan an-khi-cham: neu chi chan mot duong thi nguoi choi
    /// lao vao dong do va an sach bang duong con lai, gioi han thanh vo nghia.
    /// </summary>
    private bool TryConsumeSwallow()
    {
        if (swallowsPerSecond <= 0f) return true;      // 0 = tat gioi han
        if (_swallowBudget < 1f) return false;

        _swallowBudget -= 1f;
        return true;
    }

    // ------------------------------------------------------------- an khi cham

    // Ca bon callback: Enter bat luc dam vao, Stay bat truong hop vat the nam tua vao
    // nguoi ma khong sinh them va cham moi (vd nhan vat dung yen ep vao mot cai xe).
    void OnCollisionEnter(Collision collision) { TryDevourOnContact(collision.collider); }
    void OnCollisionStay(Collision collision) { TryDevourOnContact(collision.collider); }
    void OnTriggerEnter(Collider other) { TryDevourOnContact(other); }
    void OnTriggerStay(Collider other) { TryDevourOnContact(other); }

    /// <summary>
    /// Cham vao vat the thi nuot luon neu du cap.
    ///
    /// Khong kiem tra non hut o day: da dung vao nguoi roi thi huong nhin khong con
    /// y nghia gi nua. Nhung cong chan level thi van giu - vat qua cap phai la vat
    /// can duong, khong phai vat an duoc bang cach di dam vao.
    /// </summary>
    private void TryDevourOnContact(Collider col)
    {
        if (!devourOnContact || col == null) return;
        if (!CanSuck()) return;

        // Vat dang bi hut da tat collider nen khong vao day duoc, nhung cu chan cho chac
        Devourable target = col.GetComponentInParent<Devourable>();
        if (target == null || target.IsCaptured || !target.CanBeCaptured) return;

        if (target.transform == transform || target.transform.IsChildOf(transform)) return;
        if ((suckableLayers.value & (1 << col.gameObject.layer)) == 0) return;
        if (useLevelGate && target.requiredLevel > suctionLevel) return;
        if (!TryConsumeSwallow()) return;              // het luot thi frame sau an tiep

        _resisting.Remove(target);
        _captured.Remove(target);
        target.StopResist();

        if (Swallowed != null) Swallowed(target);

        target.Devour();
        if (onSwallowed != null) onSwallowed.Invoke();
    }

    private bool CanSuck()
    {
        if (!SuctionEnabled) return false;
        return UIManager.Instance == null || !UIManager.Instance.IsGameOver;
    }

    // ------------------------------------------------------------------- quet

    /// <summary>
    /// Quet hinh cau ban kinh range roi loc lai theo goc non. Re hon nhieu so voi
    /// dung collider trigger hinh non vi khong can mesh collider loi.
    /// </summary>
    private void Scan()
    {
        if (_captured.Count >= maxCaptured) return;

        Transform m = EnsureMouth();

        // Quet rong hon range mot chut roi moi loc lai bang IsInCone.
        //
        // Phai the vi hai ben dung hai thuoc do khac nhau: OverlapSphere tim theo hinh
        // that cua collider, con IsInCone lai cong them Devourable.Radius (duong cheo cua
        // bounds, luon lon hon be ngang). Vat nam trong khe chenh do se bao "trong non"
        // ma khong bao gio bi quet thay - nhin ra la vat sat ngay truoc mat khong bi hut.
        float scanRadius = range + scanPadding;
        int count = Physics.OverlapSphereNonAlloc(m.position, scanRadius, _hits, suckableLayers, QueryTriggerInteraction.Ignore);

        // Mang day nghia la con collider bi bo sot, noi mang ra cho lan quet sau
        if (count >= _hits.Length && _hits.Length < 512)
        {
            _hits = new Collider[_hits.Length * 2];
            count = Physics.OverlapSphereNonAlloc(m.position, scanRadius, _hits, suckableLayers, QueryTriggerInteraction.Ignore);
        }

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i];
            if (col == null) continue;

            Devourable target = col.GetComponentInParent<Devourable>();
            if (target == null || target.IsCaptured || !target.CanBeCaptured) continue;

            // Khong tu hut chinh minh hay cac bo phan cua minh
            if (target.transform == transform || target.transform.IsChildOf(transform)) continue;

            if (!IsInCone(target.Center, TargetRadius(target))) continue;

            // Qua level thi khong tom, chi cho rung tai cho lam tin hieu
            if (useLevelGate && target.requiredLevel > suctionLevel)
            {
                if (shakeTooBigTargets && !_resisting.Contains(target))
                {
                    _resisting.Add(target);
                    if (Blocked != null) Blocked(target);
                }
                continue;
            }

            _resisting.Remove(target);
            target.OnCaptured();
            _captured.Add(target);

            // Khong can cho bay o day: TickCaptured chay ngay sau Scan trong cung
            // mot Update, che do tuc thi duoc xu ly gon o do.

            if (_captured.Count >= maxCaptured) return;
        }
    }

    // --------------------------------------------------------------- xu ly moi frame

    private void TickCaptured(float deltaTime)
    {
        if (_captured.Count == 0) return;

        Transform m = EnsureMouth();
        Vector3 mouthPosition = m.position;
        Vector3 axis = m.forward;

        for (int i = _captured.Count - 1; i >= 0; i--)
        {
            Devourable target = _captured[i];
            if (target == null)
            {
                _captured.RemoveAt(i);
                continue;
            }

            Vector3 center = target.Center;
            float radius = TargetRadius(target);
            float strength = StrengthAt(center, radius);

            // Ra khoi vung hut la nha, KE CA o che do tuc thi.
            //
            // Truoc day che do tuc thi bo qua luon buoc nay ("da hut la hut den cung"),
            // nhung nhu vay hai thiet lap da nhau: bat Ins len thi cai cong vung hut
            // thanh vo nghia. Gio Ins chi con mot y nghia duy nhat la bo qua pha giang
            // co, con vung hut thi luc nao cung la vung hut.
            bool lost = strength <= 0f && (!target.IsFlying || !keepWhenOutOfCone);

            if (!keepWhenOutOfCone && !target.IsFlying && !IsInCone(center, radius))
                lost = true;

            if (lost)
            {
                target.OnReleased();
                _captured.RemoveAt(i);
                continue;
            }

            // Che do tuc thi: cho bay thang o toc do toi da, bo qua pha giang co.
            // Kiem tra o day chu khong phai luc Scan de khi bat cong tac giua chung thi
            // ca nhung vat DANG giang co cung duoc cho bay luon, khong ket lai nua chung.
            if (instantDevour && !target.IsFlying) target.BeginFlight(maxPullSpeed);

            if (!target.IsFlying)
            {
                TickStruggle(target, mouthPosition, strength, deltaTime);
                continue;
            }

            if (TickFlight(target, mouthPosition, axis, deltaTime))
            {
                // Da toi mieng nhung het luot nuot: cu de no lo lung o day cho den luot.
                // TickFlight se lai tra ve true o frame sau ma khong day them, nen vat the
                // dung yen sat mieng - thanh mot hang doi nhin thay duoc.
                if (!TryConsumeSwallow()) continue;

                _captured.RemoveAt(i);

                // Ban truoc Devour() vi Devour() se Destroy vat the
                if (Swallowed != null) Swallowed(target);

                target.Devour();
                if (onSwallowed != null) onSwallowed.Invoke();
            }
        }
    }

    /// <summary>
    /// Giai doan giang co: vat the rung tai cho, nga ve phia mieng va troi vao rat cham.
    /// Grip cong don theo do manh cua gio, du resistance thi moi but ra.
    /// </summary>
    private void TickStruggle(Devourable target, Vector3 mouthPosition, float strength, float deltaTime)
    {
        float scale = Mathf.Max(0.01f, struggleTimeScale);
        target.Grip += strength * deltaTime / scale;

        // Cang sap but ra thi rung cang du
        float tension = target.Resistance > 0.0001f
            ? Mathf.Clamp01(target.Grip / target.Resistance)
            : 1f;

        target.StruggleAnchor = Vector3.MoveTowards(
            target.StruggleAnchor, mouthPosition, struggleDrift * strength * deltaTime);

        target.TickStruggle(deltaTime, mouthPosition, strength, tension);

        if (target.Grip >= target.Resistance)
        {
            target.BeginFlight(startPullSpeed);
            if (onTargetPulled != null) onTargetPulled.Invoke();
        }
    }

    /// <summary>
    /// Duong bay xoan oc: van toc = thanh phan huong thang ve mieng + thanh phan
    /// tiep tuyen quay quanh truc non. Thanh phan tiep tuyen nho dan khi lai gan
    /// nen vat the khong xoay vong vo tan ma cuon dan vao giua roi bi nuot.
    /// </summary>
    /// <returns>true khi da cham mieng.</returns>
    private bool TickFlight(Devourable target, Vector3 mouthPosition, Vector3 axis, float deltaTime)
    {
        Vector3 center = target.Center;
        Vector3 toMouth = mouthPosition - center;
        float distance = toMouth.magnitude;

        if (distance <= swallowDistance) return true;

        Vector3 inward = toMouth / distance;

        // Toc do bam theo KHOANG CACH chu khong theo thoi gian da bay.
        //
        // Neu chi cong gia toc theo thoi gian nhu truoc thi vat bi tom tu xa da chay
        // het toc do tu giua duong, doan cuoi khong con gi de "vut" ca. Bam theo
        // khoang cach thi vat nao cung the: le te troi vao, den gan mieng moi bung toc.
        float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, range));
        float targetSpeed = instantDevour
            ? maxPullSpeed
            : Mathf.Lerp(farPullSpeed, maxPullSpeed, Mathf.Pow(closeness, nearBoostSharpness));

        target.PullSpeed = Mathf.MoveTowards(target.PullSpeed, targetSpeed, pullAcceleration * deltaTime);
        float step = target.PullSpeed * deltaTime;

        // Buoc tien vao mieng, chan lai de khong vot qua
        Vector3 move = inward * Mathf.Min(step, distance);

        // Buoc xoay quanh truc non. Cang gan mieng thi vong xoay cang thit lai.
        if (swirl > 0.001f)
        {
            Vector3 radial = Vector3.ProjectOnPlane(-toMouth, axis);
            if (radial.sqrMagnitude > 0.0001f)
            {
                Vector3 tangent = Vector3.Cross(axis, radial.normalized) * target.SwirlSign;
                float taper = Mathf.Clamp01(distance / Mathf.Max(0.01f, range));
                move += tangent * (step * swirl * taper);
            }
        }

        // Day theo tam bounds chu khong theo pivot, roi bu lai vao transform
        Vector3 nextCenter = center + move;
        target.transform.position += nextCenter - center;

        float nextDistance = Vector3.Distance(nextCenter, mouthPosition);
        target.TickFlight(deltaTime, move, nextDistance, shrinkDistance);

        return nextDistance <= swallowDistance;
    }

    private void ApplyBodyScale()
    {
        if (bodyTransform == null) return;
        if (!_bodyCached) CacheBody();
        if (!_bodyCached) return;

        // Phong len khi hut, kem mot nhip rung nhe cho ra ve dang gang suc
        float pulse = _intensity > 0f ? Mathf.Sin(Time.time * bodyPulseSpeed) * 0.25f : 0f;
        float amount = 1f + bodyInflate * _intensity * (1f + pulse);

        // Phong ngang nhieu hon phong doc: kieu ma nhan vat hop hoi
        bodyTransform.localScale = new Vector3(
            _bodyStartScale.x * amount,
            _bodyStartScale.y * Mathf.Lerp(1f, amount, 0.4f),
            _bodyStartScale.z * amount);
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < _captured.Count; i++)
        {
            if (_captured[i] != null) _captured[i].OnReleased();
        }
        _captured.Clear();

        for (int i = 0; i < _resisting.Count; i++)
        {
            if (_resisting[i] != null) _resisting[i].StopResist();
        }
        _resisting.Clear();
    }

    void OnValidate()
    {
        range = Mathf.Max(0.1f, range);
        scanInterval = Mathf.Max(0.01f, scanInterval);
        swallowDistance = Mathf.Max(0.01f, swallowDistance);
        farPullSpeed = Mathf.Max(0.1f, farPullSpeed);
        maxPullSpeed = Mathf.Max(maxPullSpeed, farPullSpeed);
        pullAcceleration = Mathf.Max(1f, pullAcceleration);
        maxCaptured = Mathf.Max(1, maxCaptured);
        suctionPower = Mathf.Max(0f, suctionPower);
        rampTime = Mathf.Max(0f, rampTime);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 origin = mouth != null ? mouth.position : transform.TransformPoint(mouthLocalOffset);
        Quaternion rotation = mouth != null ? mouth.rotation : transform.rotation;

        Vector3 forward = rotation * Vector3.forward;
        Vector3 up = rotation * Vector3.up;
        Vector3 right = rotation * Vector3.right;

        float halfAngle = coneAngle * 0.5f * Mathf.Deg2Rad;
        float baseRadius = Mathf.Tan(halfAngle) * range;
        Vector3 baseCenter = origin + forward * range;

        Gizmos.color = gizmoColor;
        GizmoLine(origin, baseCenter + up * baseRadius);
        GizmoLine(origin, baseCenter - up * baseRadius);
        GizmoLine(origin, baseCenter + right * baseRadius);
        GizmoLine(origin, baseCenter - right * baseRadius);

        DrawCircle(baseCenter, up, right, baseRadius);
        DrawCircle(origin + forward * (range * 0.5f), up, right, baseRadius * 0.5f);

        // Ve thu mot duong xoan oc de uoc luong do swirl truoc khi bam Play
        DrawSpiral(origin, forward, up, right, baseRadius);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireSphere(origin, swallowDistance);
    }

    private void DrawCircle(Vector3 center, Vector3 up, Vector3 right, float radius)
    {
        const int segments = 24;
        Vector3 previous = center + right * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 current = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            GizmoLine(previous, current);
            previous = current;
        }
    }

    /// <summary>
    /// Ve mot doan gizmo, cat bo phan chim duoi mat dat. Nho vay gizmo ve dung cai
    /// vung ma IsInCone/StrengthAt thuc su chap nhan, khong ve mot cai non day du roi
    /// de nguoi doc tuong la hut duoc ca duoi long duong.
    /// </summary>
    private void GizmoLine(Vector3 a, Vector3 b)
    {
        if (clipBelowGround)
        {
            bool aAbove = a.y >= groundY;
            bool bAbove = b.y >= groundY;

            if (!aAbove && !bAbove) return;

            if (aAbove != bAbove)
            {
                Vector3 clipped = Vector3.Lerp(a, b, (groundY - a.y) / (b.y - a.y));
                if (aAbove) b = clipped;
                else a = clipped;
            }
        }

        Gizmos.DrawLine(a, b);
    }

    private void DrawSpiral(Vector3 origin, Vector3 forward, Vector3 up, Vector3 right, float baseRadius)
    {
        if (swirl <= 0.001f) return;

        const int segments = 48;
        Vector3 previous = origin + forward * range + right * baseRadius;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;              // 0 o day non, 1 o mieng
            float distance = range * (1f - t);
            float radius = baseRadius * (1f - t);
            float angle = swirl * t * Mathf.PI * 2f;

            Vector3 current = origin + forward * distance
                + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;

            GizmoLine(previous, current);
            previous = current;
        }
    }
}
