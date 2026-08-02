using UnityEngine;

/// <summary>
/// Do vat co dinh phan ung voi luong gio hut: cong nguoi ve phia mieng va dap
/// phanh phach, nhung khong bi nuot. Chinh la may canh cua tu locker trong Kirby.
///
/// Day la thu lam cho vung hut "co that": neu chi nhung vat bi hut moi dong day
/// thi ca khung canh van dung im, nhin ra ngay la gia. Cho cay, bien bao, cua,
/// rem... hoi nghieng theo la ca man hinh song lai.
///
/// Gan vao GameObject muon cong. Neu vat the dat o goc duoi chan (cua, cay) thi
/// de nguyen pivot; neu pivot nam giua thi tao mot object cha o chan roi gan vao do,
/// nguoc lai vat the se xoay quanh chinh no thay vi cong len.
///
/// Muon vat the cong mot luc roi bat ra bay vao mieng thi gan them Devourable
/// va bat Break Free.
/// </summary>
[DisallowMultipleComponent]
public class SuctionReactor : MonoBehaviour
{
    [Header("Cong nguoi")]
    [Tooltip("Transform bi cong. De trong = chinh object nay")]
    public Transform bendPivot;

    [Tooltip("Do (degree) cong toi da khi gio manh nhat")]
    [Range(0f, 90f)] public float maxBendAngle = 28f;

    [Tooltip("Toc do doi theo gio. Thap = nang ne, cao = nhay tanh tach")]
    public float bendResponse = 6f;

    [Header("Dap phanh phach")]
    [Tooltip("Bien do rung cong them, tinh theo do")]
    public float flutterAngle = 7f;

    [Tooltip("Tan so rung")]
    public float flutterSpeed = 22f;

    [Tooltip("Bien do xe dich vi tri. De 0 cho vat neo chac nhu cua, tang len cho vat nhe")]
    public float shakeAmount = 0f;

    [Header("But khoi cho")]
    [Tooltip("Cho phep vat the bat ra va bi hut that su. Can co component Devourable")]
    public bool breakFree = false;

    [Tooltip("Gio phai manh hon muc nay moi tinh la dang bi giat")]
    [Range(0f, 1f)] public float breakFreeStrength = 0.55f;

    [Tooltip("Giu du manh trong bao nhieu giay thi bat ra")]
    public float breakFreeTime = 1.2f;

    [Tooltip("Ban ra dung luc bat ra: am thanh go vo, particle manh vun...")]
    public UnityEngine.Events.UnityEvent onBreakFree;

    /// <summary>Do manh cua gio dang thoi vao, 0..1. Dung cho animator/shader neu can.</summary>
    public float WindStrength { get { return _strength; } }

    private Devourable _devourable;
    private Quaternion _startLocalRotation;
    private Vector3 _startLocalPosition;
    private Vector3 _windDirection = Vector3.up;
    private float _strength;
    private float _grip;
    private float _noiseSeed;

    void Awake()
    {
        if (bendPivot == null) bendPivot = transform;

        _startLocalRotation = bendPivot.localRotation;
        _startLocalPosition = bendPivot.localPosition;
        _noiseSeed = Random.Range(0f, 100f);

        _devourable = GetComponent<Devourable>();

        // Khoa lai cho den khi but ra, nguoc lai MouthSuction se hut ngay tu dau
        // va man cong nguoi khong bao gio duoc nhin thay.
        if (breakFree && _devourable != null) _devourable.CanBeCaptured = false;
    }

    void OnDisable()
    {
        if (bendPivot == null) return;
        bendPivot.localRotation = _startLocalRotation;
        bendPivot.localPosition = _startLocalPosition;
    }

    void Update()
    {
        // Da bi hut that su thi Devourable cam lai transform, minh khong tranh nua
        if (_devourable != null && _devourable.IsCaptured)
        {
            enabled = false;
            return;
        }

        float deltaTime = Time.deltaTime;

        Vector3 toMouth;
        float target;
        if (!MouthSuction.SampleWind(bendPivot.position, out toMouth, out target)) target = 0f;

        if (target > 0f && toMouth.sqrMagnitude > 0.0001f)
        {
            Vector3 dir = toMouth.normalized;
            _windDirection = _windDirection.sqrMagnitude > 0.0001f
                ? Vector3.Slerp(_windDirection, dir, Mathf.Clamp01(bendResponse * deltaTime))
                : dir;
        }

        _strength = Mathf.MoveTowards(_strength, target, bendResponse * deltaTime);

        TickBreakFree(target, deltaTime);
        ApplyBend(deltaTime);
    }

    /// <summary>Cong truc dung cua vat the ve phia mieng, cong them nhip dap phanh phach.</summary>
    private void ApplyBend(float deltaTime)
    {
        if (_strength <= 0.0001f)
        {
            bendPivot.localRotation = Quaternion.RotateTowards(
                bendPivot.localRotation, _startLocalRotation, 180f * deltaTime);
            bendPivot.localPosition = Vector3.MoveTowards(
                bendPivot.localPosition, _startLocalPosition, 2f * deltaTime);
            return;
        }

        float flutter = flutterAngle * _strength *
                        (Mathf.PerlinNoise(Time.time * flutterSpeed * 0.1f, _noiseSeed) - 0.5f) * 2f;
        float angle = Mathf.Max(0f, maxBendAngle * _strength + flutter);

        // Tinh trong world roi doi nguoc ve local: nho vay parent co xoay kieu gi
        // thi huong cong van dung ve phia mieng.
        Quaternion parentRotation = bendPivot.parent != null ? bendPivot.parent.rotation : Quaternion.identity;
        Quaternion startWorld = parentRotation * _startLocalRotation;

        Vector3 baseUp = startWorld * Vector3.up;
        Vector3 bentUp = Vector3.RotateTowards(baseUp, _windDirection, angle * Mathf.Deg2Rad, 0f);
        Quaternion targetWorld = Quaternion.FromToRotation(baseUp, bentUp) * startWorld;

        bendPivot.localRotation = Quaternion.Inverse(parentRotation) * targetWorld;

        if (shakeAmount > 0.0001f)
        {
            float t = Time.time * flutterSpeed + _noiseSeed;
            Vector3 jitter = new Vector3(
                Mathf.PerlinNoise(t, _noiseSeed) - 0.5f,
                Mathf.PerlinNoise(_noiseSeed, t) - 0.5f,
                Mathf.PerlinNoise(t, t) - 0.5f) * (2f * shakeAmount * _strength);

            bendPivot.localPosition = _startLocalPosition + jitter;
        }
    }

    /// <summary>
    /// Gio du manh va du lau thi mo khoa cho MouthSuction hut that. Gio yeu di thi
    /// grip tut ve, nen ke bam ria non se cong mai chu khong bao gio bat ra.
    /// </summary>
    private void TickBreakFree(float targetStrength, float deltaTime)
    {
        if (!breakFree || _devourable == null) return;

        if (targetStrength >= breakFreeStrength)
        {
            _grip += deltaTime;
            if (_grip >= breakFreeTime)
            {
                _devourable.CanBeCaptured = true;
                if (onBreakFree != null) onBreakFree.Invoke();
                enabled = false;
            }
            return;
        }

        _grip = Mathf.Max(0f, _grip - deltaTime * 0.5f);
    }

    void OnValidate()
    {
        bendResponse = Mathf.Max(0.1f, bendResponse);
        breakFreeTime = Mathf.Max(0.05f, breakFreeTime);
        shakeAmount = Mathf.Max(0f, shakeAmount);
    }
}
