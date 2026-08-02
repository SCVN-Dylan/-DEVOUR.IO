using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Danh dau mot vat the co the bi hut vao mieng nhan vat.
///
/// Component nay giu du lieu + trang thai + phan dien hoat (rung, nga, keo dai...),
/// nhung khong tu quyet dinh di dau. Toan bo duong bay do MouthSuction dieu khien
/// de moi vat the trong vung hut deu chung mot nhip, khong ai tu tinh kieu rieng.
///
/// Vong doi mot vat the:
///   OnCaptured -> TickStruggle nhieu frame (rung tai cho, nga ve phia mieng)
///              -> BeginFlight  (Grip du resistance thi but ra khoi cho)
///              -> TickFlight nhieu frame (bay xoan oc, keo dai, nho dan)
///              -> Devour
///
/// Gan vao GameObject GOC cua vat the, collider nam o cac object con van nhan dien duoc
/// vi MouthSuction do bang GetComponentInParent.
/// </summary>
[DisallowMultipleComponent]
public class Devourable : MonoBehaviour
{
    [Header("Gia tri")]
    [Tooltip("Diem cong vao UIManager khi bi nuot")]
    public int scoreValue = 1;

    [Header("Cap do")]
    [Tooltip("Nhan vat phai dat CAP NAY tro len moi hut noi. Xe/cay = 1, nha nho = 10, nha to = 20...")]
    public int requiredLevel = 1;

    [Tooltip("XP nhan duoc khi nuot vat nay")]
    public int xpValue = 1;

    [Tooltip("Tu tinh Required Level va Xp Value theo kich thuoc that luc Awake.\n" +
             "Bat cai nay khi nhieu instance dung chung mot prefab nhung scale khac nhau\n" +
             "(vd 46 toa nha deu tu 5 prefab): moi instance se tu nhan bac dung theo co cua no.\n" +
             "Tat neu muon tu tay dat cho tung vat")]
    public bool autoLevelFromSize = true;

    [Header("Suc chong cu")]
    [Tooltip("Bao lau (giay) o giua tam non thi but khoi cho. 0 = bi hut ngay lap tuc.\n" +
             "La cay ~0.05, thung rac ~0.4, xe hoi ~1.2. Vat cang nang thi de o ria non se khong bao gio vao duoc")]
    public float resistance = 0.35f;

    [Tooltip("Do bam bi quen di bao nhieu moi giay khi da tuot khoi vung hut.\n" +
             "Thap = quet qua quet lai vai lan la lua duoc, cao = phai hut mot hoi lien tuc")]
    public float gripDecay = 0.5f;

    [Tooltip("Bien do rung khi dang giang co, tinh bang don vi world")]
    public float struggleShake = 0.06f;

    [Tooltip("Tan so rung. Cao = rung rat gap, thap = lac lu")]
    public float struggleShakeSpeed = 26f;

    [Tooltip("Do (degree) nga ve phia mieng khi bi gio keo. 0 = dung yen khong nga")]
    public float struggleLean = 22f;

    [Header("Hieu ung khi dang bay")]
    [Tooltip("Quay dau vat the ve huong bay, giong nhu bi luong gio be lai")]
    public bool alignToFlight = true;

    [Tooltip("Do/giay khi be huong vat the theo duong bay")]
    public float alignSpeed = 900f;

    [Tooltip("Do/giay xoay quanh truc bay. 0 = khong xoay")]
    public float spinSpeed = 540f;

    [Tooltip("Keo dai vat the theo huong bay, phan con lai bop lai cho giu the tich.\n" +
             "Chi co tac dung khi bat Align To Flight")]
    [Range(0f, 1.5f)] public float flightStretch = 0.4f;

    [Tooltip("Toc do bay ma tai do dat muc keo dai toi da")]
    public float stretchAtSpeed = 18f;

    [Tooltip("Thu nho dan khi den gan mieng cho khoi loi ra khoi model nhan vat")]
    public bool shrinkWhileFlying = true;

    [Header("Su kien")]
    [Tooltip("Ban ra dung luc bi nuot: am thanh, popup diem, particle...")]
    public UnityEvent onDevoured;

    [Tooltip("Ban ra dung luc but khoi cho va bat dau bay")]
    public UnityEvent onPulled;

    /// <summary>Dang bi mot MouthSuction giu hay khong.</summary>
    public bool IsCaptured { get; private set; }

    /// <summary>Da but khoi cho va dang bay vao mieng chua.</summary>
    public bool IsFlying { get; private set; }

    /// <summary>Tat de tam khoa khong cho hut (vd: dang bi SuctionReactor neo vao canh).</summary>
    public bool CanBeCaptured { get; set; } = true;

    /// <summary>Luong "do bam" cong don, du Resistance thi but ra. MouthSuction ghi.</summary>
    public float Grip { get; set; }

    /// <summary>Nguong de but khoi cho, doc tu resistance.</summary>
    public float Resistance { get { return Mathf.Max(0f, resistance); } }

    /// <summary>Vi tri goc luc giang co. MouthSuction keo diem nay troi dan ve mieng.</summary>
    public Vector3 StruggleAnchor { get; set; }

    /// <summary>Toc do hut hien tai, MouthSuction tang dan theo tung frame.</summary>
    public float PullSpeed { get; set; }

    /// <summary>Chieu xoan oc (+1 hoac -1), moi vat mot chieu cho khoi deu tap the.</summary>
    public float SwirlSign { get; private set; } = 1f;

    /// <summary>
    /// Tam thuc te cua vat the (tam bounds), khong phai pivot.
    /// Pivot cua Tree/Car nam duoi chan nen neu bay theo pivot thi nhin nhu
    /// vat the chui qua duoi mieng.
    /// </summary>
    public Vector3 Center { get { return transform.TransformPoint(_centerLocal); } }

    /// <summary>
    /// Ban kinh xap xi cua vat the, do tu bounds luc Awake.
    ///
    /// MouthSuction dung cai nay de kiem tra non theo hinh CAU chu khong phai theo
    /// mot diem: cai cay cao 3m dung ngay truoc mat co tam bounds nam cao qua dau,
    /// neu chi do tam thi no roi ra ngoai non du than cay dang cham vao mat nhan vat.
    /// </summary>
    public float Radius { get { return _radius; } }

    private Rigidbody _rb;
    private Collider[] _colliders;
    private bool[] _colliderStates;
    private bool _rbWasKinematic;
    private bool _rbUsedGravity;
    private Vector3 _startScale;
    private Vector3 _centerLocal;
    private float _radius;
    private Quaternion _captureRotation;
    private Vector3 _spinAxis = Vector3.up;
    private float _roll;
    private float _noiseSeed;
    private float _releasedAt = -9999f;
    private bool _resisting;
    private Vector3 _resistPosition;
    private Quaternion _resistRotation;

    /// <summary>Cach nhau bao nhieu cap thi mo khoa mot BAC vat the moi.</summary>
    public const int LevelsPerTier = 10;

    /// <summary>
    /// Nguong ban kinh chia vat the thanh cac BAC. R nho hon phan tu 0 la bac 1,
    /// nho hon phan tu 1 la bac 2... lon hon tat ca thi la bac cuoi.
    ///
    /// Do theo scene hien tai: Car R=1.54, Tree R=1.65..2.04, Building R=2.80..7.51.
    /// Bac 1 gop ca xe lan cay lai (deu hut duoc tu dau), tu bac 2 tro len la nha.
    /// </summary>
    public static readonly float[] TierRadiusSteps = new float[] { 2.2f, 4.4f, 5.8f };

    /// <summary>So bac vat the.</summary>
    public static int TierCount { get { return TierRadiusSteps.Length + 1; } }

    /// <summary>Cap cao nhat con y nghia: qua cap nay thi da mo khoa het moi thu.</summary>
    public static int TopRequiredLevel { get { return RequiredLevelForTier(TierCount); } }

    /// <summary>Quy ban kinh ra bac. Dung chung cho ca luc chay lan luc bake trong Editor.</summary>
    public static int TierForRadius(float radius)
    {
        for (int i = 0; i < TierRadiusSteps.Length; i++)
            if (radius < TierRadiusSteps[i]) return i + 1;

        return TierRadiusSteps.Length + 1;
    }

    /// <summary>Bac 1 mo tu dau (cap 1), moi bac sau lui them LevelsPerTier cap.</summary>
    public static int RequiredLevelForTier(int tier)
    {
        return tier <= 1 ? 1 : (tier - 1) * LevelsPerTier;
    }

    /// <summary>
    /// Bac cang cao thi nuot cang duoc nhieu XP.
    ///
    /// Bat dau tu 2 chu khong phai 1: mot cap re nhat cung ton 1 XP, ma tu cap 1 len
    /// cap 10 la 9 cap, nen neu vat bac 1 chi cho 1 XP thi so vat bac 1 tren map phai
    /// nhieu hon so cap can leo - vua du la ket. Cho 2 XP la co gap doi bien an toan.
    /// </summary>
    public static int XpForTier(int tier)
    {
        return Mathf.Max(1, tier) + 1;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _colliderStates = new bool[_colliders.Length];
        _startScale = transform.localScale;
        _centerLocal = CalcCenterLocal();
        _noiseSeed = Random.Range(0f, 100f);

        if (autoLevelFromSize)
        {
            int tier = TierForRadius(_radius);
            requiredLevel = RequiredLevelForTier(tier);
            xpValue = XpForTier(tier);
        }

        WarnIfBatchingStatic();
    }

    /// <summary>
    /// Batching Static gop mesh cua vat the vao mot mesh chung o world space, nen
    /// transform co bay vao mieng thi mesh van dung y nguyen cho cu - nguoi choi chi
    /// thay vat the bien mat cai bup, khong co doan bay nao ca.
    ///
    /// Loi nay rat kho doan vi moi thu khac deu dung: vat the bi tom, bay, cong diem,
    /// bi huy. Chi rieng phan nhin thay duoc la khong nhuc nhich.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void WarnIfBatchingStatic()
    {
#if UNITY_EDITOR
        var flags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(gameObject);
        if ((flags & UnityEditor.StaticEditorFlags.BatchingStatic) == 0) return;

        Debug.LogWarning("[Devourable] " + name + " dang bat Batching Static nen se KHONG bay duoc, " +
                         "chi bien mat tai cho. Tat Batching Static di, hoac chay " +
                         "Tools/Devour/Xoa Static Flags cho moi Devourable.", this);
#endif
    }

    /// <summary>MouthSuction goi khi vat the lot vao vung hut.</summary>
    public void OnCaptured()
    {
        if (IsCaptured) return;

        StopResist();          // dang rung vi qua level, gio du cap roi thi tra ve cho cu truoc da

        IsCaptured = true;
        IsFlying = false;
        PullSpeed = 0f;

        // Giu lai do bam cua nhung lan bi quet truoc, chi tru di phan da quen trong
        // luc nam ngoai vung hut. Neu reset thang ve 0 thi nguoi choi vua di vua quet
        // se khong bao gio lua noi vat nao: cu sap but ra la lai tuot khoi non va lam lai tu dau.
        float elapsed = Time.time - _releasedAt;
        Grip = Mathf.Max(0f, Grip - elapsed * Mathf.Max(0f, gripDecay));
        StruggleAnchor = transform.position;
        _captureRotation = transform.rotation;
        _roll = 0f;
        SwirlSign = Random.value < 0.5f ? -1f : 1f;
        _spinAxis = Random.onUnitSphere;

        if (_rb != null)
        {
            _rbWasKinematic = _rb.isKinematic;
            _rbUsedGravity = _rb.useGravity;

            // Xoa van toc truoc khi chuyen kinematic, nguoc lai Unity se canh bao
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.useGravity = false;
            _rb.isKinematic = true;
        }

        // Tat collider de vat the khong day nguoc nhan vat hay ket vao tuong tren duong bay
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] == null) continue;
            _colliderStates[i] = _colliders[i].enabled;
            _colliders[i].enabled = false;
        }
    }

    /// <summary>But khoi cho, chuyen sang bay vao mieng.</summary>
    public void BeginFlight(float initialSpeed)
    {
        if (!IsCaptured || IsFlying) return;

        IsFlying = true;
        PullSpeed = initialSpeed;

        if (onPulled != null) onPulled.Invoke();
    }

    /// <summary>Tra vat the ve trang thai binh thuong khi thoat vung hut.</summary>
    public void OnReleased()
    {
        if (!IsCaptured) return;

        // Chi giang co roi duoc tha thi dat lai goc nghieng, con dang bay ma tha
        // thi giu nguyen tu the roi cho vat ly xu ly tiep.
        if (!IsFlying) transform.rotation = _captureRotation;

        IsCaptured = false;
        IsFlying = false;
        PullSpeed = 0f;
        _releasedAt = Time.time;   // Grip giu nguyen, se tu tru dan o lan bi tom sau

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] == null) continue;
            _colliders[i].enabled = _colliderStates[i];
        }

        if (_rb != null)
        {
            _rb.isKinematic = _rbWasKinematic;
            _rb.useGravity = _rbUsedGravity;
        }

        transform.localScale = _startScale;
    }

    /// <summary>
    /// Dang bam tru: rung tai cho quanh StruggleAnchor va nga dan ve phia mieng.
    /// tension 0..1 = sap but ra den dau, dung de tang dan bien do rung.
    /// </summary>
    public void TickStruggle(float deltaTime, Vector3 mouthPosition, float strength, float tension)
    {
        float amount = Mathf.Clamp01(strength) * Mathf.Lerp(0.35f, 1f, tension);

        // Perlin cho ra kieu rung run ray, khac han rung deu bang sin
        float t = Time.time * struggleShakeSpeed + _noiseSeed;
        Vector3 jitter = new Vector3(
            Mathf.PerlinNoise(t, _noiseSeed) - 0.5f,
            Mathf.PerlinNoise(_noiseSeed, t) - 0.5f,
            Mathf.PerlinNoise(t, t) - 0.5f) * (2f * struggleShake * amount);

        transform.position = StruggleAnchor + jitter;

        if (struggleLean <= 0.01f) return;

        // Be truc dung cua vat the ve phia mieng: y het canh cua tu locker bi gio keo cong
        Vector3 toMouth = mouthPosition - transform.position;
        if (toMouth.sqrMagnitude < 0.0001f) return;

        Vector3 baseUp = _captureRotation * Vector3.up;
        float leanRad = struggleLean * Mathf.Deg2Rad * amount;
        Vector3 bentUp = Vector3.RotateTowards(baseUp, toMouth.normalized, leanRad, 0f);

        transform.rotation = Quaternion.FromToRotation(baseUp, bentUp) * _captureRotation;
    }

    /// <summary>Phan nhin thay duoc cua duong bay: be theo huong bay, keo dai, xoay va nho dan.</summary>
    public void TickFlight(float deltaTime, Vector3 move, float distanceToMouth, float shrinkDistance)
    {
        float shrink = 1f;
        if (shrinkWhileFlying && shrinkDistance > 0.001f)
            shrink = Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(distanceToMouth / shrinkDistance));

        if (alignToFlight && move.sqrMagnitude > 0.000001f)
        {
            _roll += spinSpeed * deltaTime;

            Vector3 dir = move.normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Quaternion target = Quaternion.LookRotation(dir, up) * Quaternion.AngleAxis(_roll, Vector3.forward);

            transform.rotation = alignSpeed > 0f
                ? Quaternion.RotateTowards(transform.rotation, target, alignSpeed * deltaTime)
                : target;

            // Keo dai theo truc bay (+Z local sau khi da be huong), bop hai truc con lai
            float speedRatio = stretchAtSpeed > 0.01f ? Mathf.Clamp01(PullSpeed / stretchAtSpeed) : 1f;
            float stretch = 1f + flightStretch * speedRatio;
            float squash = 1f / Mathf.Sqrt(stretch);

            transform.localScale = new Vector3(
                _startScale.x * squash * shrink,
                _startScale.y * squash * shrink,
                _startScale.z * stretch * shrink);
            return;
        }

        if (spinSpeed > 0f)
            transform.Rotate(_spinAxis, spinSpeed * deltaTime, Space.Self);

        transform.localScale = _startScale * shrink;
    }

    /// <summary>
    /// Vat qua level: nam trong vung hut nhung khong bi tom, chi rung nhe tai cho.
    ///
    /// Khong co phan nay thi vat qua co se dung im nhu khong co gi xay ra, nguoi choi
    /// tuong game hong chu khong hieu la "chua du cap". Rung nhe la cach re nhat de noi
    /// "no co cam nhan duoc luc hut day, nhung may chua du suc".
    ///
    /// Vat co Rigidbody dong thi bo qua: physics dang giu transform, gianh voi no se giat.
    /// </summary>
    public void TickResist(float deltaTime, Vector3 mouthPosition, float strength)
    {
        if (IsCaptured) return;
        if (_rb != null && !_rb.isKinematic) return;

        if (!_resisting)
        {
            _resisting = true;
            _resistPosition = transform.position;
            _resistRotation = transform.rotation;
        }

        float amount = Mathf.Clamp01(strength);
        float t = Time.time * struggleShakeSpeed * 0.7f + _noiseSeed;

        Vector3 jitter = new Vector3(
            Mathf.PerlinNoise(t, _noiseSeed) - 0.5f,
            Mathf.PerlinNoise(_noiseSeed, t) - 0.5f,
            Mathf.PerlinNoise(t, t) - 0.5f) * (2f * struggleShake * 0.4f * amount);

        transform.position = _resistPosition + jitter;

        if (struggleLean <= 0.01f) return;

        Vector3 toMouth = mouthPosition - transform.position;
        if (toMouth.sqrMagnitude < 0.0001f) return;

        Vector3 baseUp = _resistRotation * Vector3.up;
        float leanRad = struggleLean * 0.25f * Mathf.Deg2Rad * amount;
        Vector3 bentUp = Vector3.RotateTowards(baseUp, toMouth.normalized, leanRad, 0f);

        transform.rotation = Quaternion.FromToRotation(baseUp, bentUp) * _resistRotation;
    }

    /// <summary>Thoi khong bi gio thoi vao nua: tra ve dung tu the cu.</summary>
    public void StopResist()
    {
        if (!_resisting) return;
        _resisting = false;

        transform.position = _resistPosition;
        transform.rotation = _resistRotation;
    }

    /// <summary>Cham toi mieng: cong diem roi bien mat.</summary>
    public void Devour()
    {
        if (UIManager.Instance != null) UIManager.Instance.AddScore(scoreValue);
        if (onDevoured != null) onDevoured.Invoke();

        Destroy(gameObject);
    }

    private Vector3 CalcCenterLocal()
    {
        Bounds bounds;
        bool has = TryMeasureBounds(gameObject, out bounds);

        _radius = has ? bounds.extents.magnitude : 0f;

        return has ? transform.InverseTransformPoint(bounds.center) : Vector3.zero;
    }

    /// <summary>
    /// Do bounds world cua mot vat the: uu tien collider, khong co thi dung renderer.
    ///
    /// De static va public de Editor goi duoc luc chua chay game (Awake chua chay nen
    /// Radius van dang bang 0), nho vay cong cu bake level trong Editor va code luc chay
    /// dung chung dung mot cach do, khong bao gio lech nhau.
    /// </summary>
    public static bool TryMeasureBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;

        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null) continue;
            if (!has) { bounds = colliders[i].bounds; has = true; }
            else bounds.Encapsulate(colliders[i].bounds);
        }

        if (has) return true;

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!has) { bounds = renderers[i].bounds; has = true; }
            else bounds.Encapsulate(renderers[i].bounds);
        }

        return has;
    }

    /// <summary>Ban kinh do truc tiep tu GameObject, dung duoc ca khi Awake chua chay.</summary>
    public static float MeasureRadius(GameObject go)
    {
        Bounds bounds;
        return TryMeasureBounds(go, out bounds) ? bounds.extents.magnitude : 0f;
    }

    void OnValidate()
    {
        resistance = Mathf.Max(0f, resistance);
        struggleShake = Mathf.Max(0f, struggleShake);
        stretchAtSpeed = Mathf.Max(0.01f, stretchAtSpeed);
    }
}
