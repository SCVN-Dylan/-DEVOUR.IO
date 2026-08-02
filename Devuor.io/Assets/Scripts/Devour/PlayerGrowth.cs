using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Nuot duoc vat the thi nhan vat to len, kieu hole.io.
///
/// Cong theo THE TICH chu khong cong theo scale:
///     the tich moi = the tich cu + the tich vat the * absorbEfficiency
///     scale        = can bac ba (the tich moi / the tich ban dau)
///
/// Can bac ba la thu lam cho duong cong tang truong tu can bang: luc con be, nuot
/// mot cai cay la to len thay ro; luc da to gap 3, cung cai cay do gan nhu khong
/// nhuc nhich. Neu cong thang vao scale thi cang to cang to nhanh, chi vai giay
/// la nhan vat phu kin man hinh.
///
/// Component nay giu doc quyen localScale cua nhan vat. Dung set them
/// MouthSuction.bodyTransform ve chinh transform nay, hai ben se danh nhau.
/// </summary>
[RequireComponent(typeof(MouthSuction))]
[DisallowMultipleComponent]
public class PlayerGrowth : MonoBehaviour
{
    [Header("Lon len")]
    [Tooltip("Bao nhieu phan the tich cua vat the bien thanh the tich nhan vat.\n" +
             "0.1 = phai an rat nhieu moi to, 0.5 = to nhanh")]
    [Range(0.01f, 1f)] public float absorbEfficiency = 0.25f;

    [Tooltip("Tran kich thuoc, tinh theo lan so voi luc dau.\n" +
             "Phai du cao de con duong tang truong o cap cuoi: an sach map hien tai ra khoang x8.9,\n" +
             "de tran 4 thi vua nuot mot toa nha la da dung im, an tiep khong thay to them gi nua")]
    public float maxScale = 10f;

    [Tooltip("Giay de scale duoi kip muc tieu. 0 = to giat cuc tuc thi")]
    public float growSmoothTime = 0.25f;

    [Header("Nay len khi nuot")]
    [Tooltip("Bien do phinh ra luc nuot. 0 = tat")]
    [Range(0f, 0.5f)] public float popAmount = 0.16f;

    [Tooltip("Giay cua mot nhip nay")]
    public float popDuration = 0.2f;

    [Header("Keo theo kich thuoc")]
    [Tooltip("Phong to luon vung hut. Nen bat, khong thi nhan vat to ra se nuot chung ca cai non hut")]
    public bool scaleSuctionRange = true;

    [Tooltip("So mu khi phong to TAM HUT theo kich thuoc.\n\n" +
             "1 = tam hut nhan thang theo scale. Nghe thi hop ly nhung tao ra vong lap tu khuech dai:\n" +
             "to hon -> quet rong hon -> an nhanh hon -> lai to hon. Do duoc scale 1 len 6.8 trong 5 giay,\n" +
             "tam hut phinh tu 6 len 40.8 tren map rong 154.\n\n" +
             "0.5 = tam hut theo can bac hai cua scale. To gap 9 lan thi tam hut chi gap 3.")]
    [Range(0.2f, 1f)] public float suctionRangeExponent = 0.5f;

    [Tooltip("To hon thi chay nhanh hon bao nhieu. 0 = giu nguyen toc do, 1 = nhanh dung theo ti le scale")]
    [Range(0f, 1f)] public float speedGain = 0.35f;

    [Tooltip("Camera lui ra khi nhan vat to len. De trong = tu tim CameraFollow trong scene")]
    public CameraFollow cameraFollow;

    [Tooltip("Camera lui ra theo ti le nao. 0 = khong lui, 1 = lui dung theo scale")]
    [Range(0f, 1f)] public float cameraZoomGain = 0.7f;

    [Header("Su kien")]
    [Tooltip("Ban ra moi lan to them: am thanh, particle, rung man hinh...")]
    public UnityEvent onGrew;

    /// <summary>Kich thuoc hien tai so voi luc bat dau van.</summary>
    public float Scale { get { return _scale; } }

    /// <summary>Kich thuoc dang huong toi (scale nhay den day roi moi dung).</summary>
    public float TargetScale { get { return _targetScale; } }

    private MouthSuction _suction;
    private RbMovement _movement;
    private Rigidbody _rb;

    private float _volume;
    private float _baseVolume;
    private float _baseHalfHeight = 0.5f;

    private float _scale = 1f;
    private float _targetScale = 1f;
    private float _scaleVelocity;
    private float _appliedScale = 1f;

    private float _popTimer;

    private Vector3 _baseLocalScale = Vector3.one;
    private float _baseRange, _baseSwallow, _baseShrink;
    private float _baseSpeed;
    private float _baseCameraHeight, _baseCameraDistance;

    void Awake()
    {
        _suction = GetComponent<MouthSuction>();
        _movement = GetComponent<RbMovement>();
        _rb = GetComponent<Rigidbody>();

        if (cameraFollow == null) cameraFollow = FindAnyObjectByType<CameraFollow>();

        _baseLocalScale = transform.localScale;

        _baseRange = _suction.range;
        _baseSwallow = _suction.swallowDistance;
        _baseShrink = _suction.shrinkDistance;

        if (_movement != null) _baseSpeed = _movement.Speed;
        if (cameraFollow != null)
        {
            _baseCameraHeight = cameraFollow.height;
            _baseCameraDistance = cameraFollow.distance;
        }

        MeasureBody();

        if (_suction.bodyTransform == transform)
            Debug.LogWarning("[PlayerGrowth] MouthSuction.bodyTransform dang tro vao chinh nhan vat. " +
                             "Hai component se tranh nhau localScale, hay de trong o do.", this);
    }

    void OnEnable()
    {
        _suction.Swallowed += OnSwallowed;
    }

    void OnDisable()
    {
        _suction.Swallowed -= OnSwallowed;
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        _scale = growSmoothTime > 0.001f
            ? Mathf.SmoothDamp(_scale, _targetScale, ref _scaleVelocity, growSmoothTime)
            : _targetScale;

        if (_popTimer > 0f) _popTimer = Mathf.Max(0f, _popTimer - deltaTime);

        ApplyScale();
    }

    /// <summary>
    /// Do kich thuoc goc cua nhan vat de lay moc quy chieu. Do bang collider truoc,
    /// khong co thi do bang renderer, cung khong co nua thi doan dai mot ban kinh 0.5.
    /// </summary>
    private void MeasureBody()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool has = false;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger) continue;
            if (!has) { bounds = colliders[i].bounds; has = true; }
            else bounds.Encapsulate(colliders[i].bounds);
        }

        if (!has)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!has) { bounds = renderers[i].bounds; has = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
        }

        // Quy ve scale 1 de moc do khong phu thuoc luc do dang to bao nhieu
        float uniform = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float radius = has ? bounds.extents.magnitude / uniform : 0.5f;

        _baseHalfHeight = has ? bounds.extents.y / uniform : 0.5f;
        _baseVolume = SphereVolume(Mathf.Max(0.01f, radius));
        _volume = _baseVolume;
    }

    /// <summary>MouthSuction ban su kien nay ngay truoc khi vat the bi huy.</summary>
    private void OnSwallowed(Devourable target)
    {
        if (target == null) return;
        Grow(SphereVolume(target.Radius) * absorbEfficiency);
    }

    /// <summary>Cong thang mot luong the tich. Dung duoc tu ngoai cho power-up, boss...</summary>
    public void Grow(float addedVolume)
    {
        if (addedVolume <= 0f) return;

        _volume += addedVolume;

        float raw = Mathf.Pow(_volume / _baseVolume, 1f / 3f);
        _targetScale = Mathf.Clamp(raw, 1f, Mathf.Max(1f, maxScale));

        if (popAmount > 0f && popDuration > 0.001f) _popTimer = popDuration;
        if (onGrew != null) onGrew.Invoke();
    }

    /// <summary>Dat lai ve kich thuoc dau van.</summary>
    public void ResetSize()
    {
        _volume = _baseVolume;
        _targetScale = 1f;
        _scale = 1f;
        _scaleVelocity = 0f;
        _popTimer = 0f;
        ApplyScale();
    }

    private void ApplyScale()
    {
        // Nhip nay khi nuot: be ngang phinh ra, chieu cao thut lai mot chut roi ve cho cu
        float pop = 0f;
        if (_popTimer > 0f && popDuration > 0.001f)
            pop = Mathf.Sin(_popTimer / popDuration * Mathf.PI) * popAmount;

        transform.localScale = new Vector3(
            _baseLocalScale.x * _scale * (1f + pop),
            _baseLocalScale.y * _scale * (1f - pop * 0.6f),
            _baseLocalScale.z * _scale * (1f + pop));

        // Nhan vat to ra tu tam, khong nang len thi nua nguoi duoi se thut xuong dat
        // roi bi physics day nguoc len -> giat nay tung nhip.
        float lift = (_scale - _appliedScale) * _baseHalfHeight;
        if (Mathf.Abs(lift) > 0.0001f)
        {
            if (_rb != null && !_rb.isKinematic) _rb.position += Vector3.up * lift;
            else transform.position += Vector3.up * lift;
        }
        _appliedScale = _scale;

        if (scaleSuctionRange)
        {
            // Tam voi thi duoi theo can bac hai, con kich thuoc mieng thi theo dung than
            float reach = Mathf.Pow(Mathf.Max(0.01f, _scale), suctionRangeExponent);
            _suction.range = _baseRange * reach;
            _suction.swallowDistance = _baseSwallow * _scale;
            _suction.shrinkDistance = _baseShrink * _scale;
        }

        if (_movement != null && _baseSpeed > 0f)
            _movement.Speed = _baseSpeed * Mathf.Lerp(1f, _scale, speedGain);

        if (cameraFollow != null)
        {
            float zoom = Mathf.Lerp(1f, _scale, cameraZoomGain);
            cameraFollow.height = _baseCameraHeight * zoom;
            cameraFollow.distance = _baseCameraDistance * zoom;
        }
    }

    private static float SphereVolume(float radius)
    {
        return 4f / 3f * Mathf.PI * radius * radius * radius;
    }

    void OnValidate()
    {
        maxScale = Mathf.Max(1f, maxScale);
        growSmoothTime = Mathf.Max(0f, growSmoothTime);
        popDuration = Mathf.Max(0.001f, popDuration);
    }
}
