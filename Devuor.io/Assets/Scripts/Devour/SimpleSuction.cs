using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Vung hut hinh NON phia truoc nhan vat - ban gon, it thong so (thay cho MouthSuction cu).
///
/// Moi FixedUpdate: quet OverlapSphere quanh mieng, loc theo goc non + cap do, roi keo
/// (Pull) cac PhysicsDevourable trong non ve phia mieng. Cham mieng thi nuot (cong XP/diem).
/// Item ra khoi non thi duoc Release de tu roi.
///
/// Len cap: an du XP thi level++, nhan vat SCALE to len dong thoi NON HUT cung dai ra
/// (scalePerLevel / rangePerLevel).
/// </summary>
[DisallowMultipleComponent]
public class SimpleSuction : MonoBehaviour
{
    [Header("Vung hut (non phia truoc)")]
    [Tooltip("De trong se tu tim object con 'Mouth'. Non toa theo mouth.forward.")]
    public Transform mouth;

    [Tooltip("Chieu dai non (base, luc level 1). Len cap se dai ra theo rangePerLevel")]
    public float range = 4f;

    [Range(5f, 179f)]
    [Tooltip("Goc mo cua non (do). 70 = xoe 35 do moi ben")]
    public float coneAngle = 75f;

    [Tooltip("Van toc keo item luc SAT MIENG (don vi/giay). Cang gan mieng hut cang manh")]
    public float pullSpeed = 12f;

    [Range(0.05f, 1f)]
    [Tooltip("Ti le toc do o RIA XA nhat cua non so voi sat mieng. 0.25 = ria hut cham (1/4), gan mieng manh dan len 1x")]
    public float farSpeedFactor = 0.25f;

    [Tooltip("Gia toc keo (u/s^2) = do QUAN TINH. Thap = nang, tang toc tu tu; cao = bat toc nhanh")]
    public float pullAccel = 18f;

    [Tooltip("Tam item vao gan mieng hon khoang nay thi nuot")]
    public float swallowDistance = 0.6f;

    [Tooltip("Layer duoc phep hut. Nen dat item vao layer rieng de OverlapSphere quet it collider hon")]
    public LayerMask suckableLayers = ~0;

    [Tooltip("Giay giua 2 lan QUET vung hut (OverlapSphere - phan dat nhat). Keo item van muot moi frame. 0 = quet moi frame")]
    public float scanInterval = 0.05f;

    [Header("An khi cham than")]
    [Tooltip("Item cham vao than nhan vat la nuot luon - NHUNG VAN PHAI DAT CAP (requiredLevel <= level).\n" +
             "Item qua cap thi cham vao khong an duoc (PhysicsDevourable tu bat va cham roi goi EatByContact).")]
    public bool eatOnContact = true;

    [Header("Cap do")]
    public bool useLevelGate = true;
    public int level = 1;
    public int maxLevel = 10;

    [Tooltip("XP can de len cap 2")]
    public int xpToNextBase = 3;

    [Range(1f, 2f)]
    [Tooltip("Moi cap sau can nhieu XP hon bao nhieu lan")]
    public float xpGrowth = 1.25f;

    [Header("Len cap thi to len")]
    [Range(0f, 100f)]
    [Tooltip("Moi cap nhan vat to them bao nhieu (0.12 = +12%/cap)")]
    public float scalePerLevel = 0.12f;

    [Tooltip("TRAN kich thuoc: to toi da = scale goc x so nay, du level bao nhieu cung khong vuot.\n" +
             "0 = khong gioi han (to mai theo cap)")]
    public float maxScale = 2f;

    [Range(0f, 100f)]
    [Tooltip("Moi cap non hut dai ra bao nhieu (0.15 = +15%/cap)")]
    public float rangePerLevel = 0.15f;

    [Header("Hieu ung 'uc' khi nuot (gulp)")]
    [Range(0f, 0.6f)]
    [Tooltip("Do manh cai giat squash-stretch moi lan nuot (0.18 = phinh cao / co ngang 18%). 0 = tat")]
    public float gulpPunch = 0.18f;

    [Tooltip("Thoi gian 1 cai 'uc' (giay)")]
    public float gulpDuration = 0.22f;

    [Range(0.5f, 4f)]
    [Tooltip("So nhip nhun len-xuong trong 1 cai uc. Cao = rung nhieu lan")]
    public float gulpWobbles = 1.3f;

    [Header("Su kien")]
    public UnityEvent onDevour;
    public UnityEvent onLevelUp;

    public int Level { get { return level; } }
    public int Xp { get { return _xp; } }
    public int XpToNext { get { return XpToNextAt(level); } }
    public float CurrentRange { get { return range * (1f + rangePerLevel * (level - 1)); } }

    /// <summary>Dang co it nhat 1 item nam trong vung/non hut hay khong.</summary>
    public bool HasItemsInRange
    {
        get
        {
            if (_active.Count == 0) return false;
            foreach (var item in _active)
            {
                if (item != null && !item.Consumed) return true;
            }
            return false;
        }
    }

    private Vector3 _baseScale;
    private int _xp;
    private float _scanTimer;
    private float _gulpTimer;
    private readonly HashSet<PhysicsDevourable> _active = new HashSet<PhysicsDevourable>();
    private readonly HashSet<PhysicsDevourable> _found = new HashSet<PhysicsDevourable>();
    private readonly List<PhysicsDevourable> _toRemove = new List<PhysicsDevourable>();
    private static readonly Collider[] _hits = new Collider[128];
    private Collider[] _ownCols;

    void Awake()
    {
        _baseScale = transform.localScale;
        if (mouth == null)
        {
            Transform m = transform.Find("Mouth");
            mouth = m != null ? m : transform;
        }
        _ownCols = GetComponentsInChildren<Collider>(true);
        ApplyLevelScale();
    }

    /// <summary>PhysicsDevourable goi khi no cham vao than nhan vat: an neu du cap.</summary>
    public void EatByContact(PhysicsDevourable it)
    {
        if (!eatOnContact || it == null || it.Consumed) return;
        if (useLevelGate && it.RequiredLevel > level) return;   // qua cap thi cham cung khong an
        Swallow(it);
    }

    void FixedUpdate()
    {
        // QUET (dat: OverlapSphere + GetComponentInParent) chay theo scanInterval,
        // con KEO/GIANG CO chay moi frame cho muot -> nhe hon nhieu tren mobile.
        _scanTimer -= Time.fixedDeltaTime;
        if (_scanTimer <= 0f) { Scan(); _scanTimer = Mathf.Max(0f, scanInterval); }

        ApplyActive();
    }

    void Update()
    {
        // Hieu ung 'uc': squash-stretch tren than nhan vat, chong len scale cap do, tat dan.
        if (_gulpTimer <= 0f) return;
        _gulpTimer -= Time.deltaTime;
        if (_gulpTimer <= 0f) { ApplyScale(Vector3.one); return; }    // xong: tra ve chuan

        float k = 1f - _gulpTimer / gulpDuration;                     // 0..1 tien do
        float amp = gulpPunch * (1f - k) * Mathf.Sin(k * Mathf.PI * 2f * gulpWobbles);
        ApplyScale(new Vector3(1f - amp, 1f + amp, 1f - amp));        // cao len / dep ngang -> 'uc'
    }

    /// <summary>Quet lai danh sach item nam trong non (phan dat tien). Item roi khoi non -> tha ra.</summary>
    private void Scan()
    {
        Vector3 origin = transform.position;   // Vi tri chân player
        Vector3 fwd = mouth != null ? mouth.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
        fwd.y = 0f;
        fwd.Normalize();

        float eff = CurrentRange, half = coneAngle * 0.5f;

        _found.Clear();
        int n = Physics.OverlapSphereNonAlloc(origin, eff, _hits, suckableLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (_hits[i] == null) continue;
            PhysicsDevourable it = _hits[i].GetComponentInParent<PhysicsDevourable>();
            if (it == null || it.Consumed || _found.Contains(it)) continue;

            int diff = it.RequiredLevel - level;
            if (useLevelGate && diff >= 2) continue;   // hon 2+ cap: khong tac dong gi

            Vector3 to = it.Center - origin;
            to.y = 0f;   // Kiem tra goc va khoang cach phang tren mat dat
            float dist = to.magnitude;
            if (dist > eff) continue;
            if (dist > 0.001f && Vector3.Angle(fwd, to) > half) continue;

            _found.Add(it);
        }

        foreach (PhysicsDevourable it in _active)
            if (it != null && !_found.Contains(it)) it.Release();

        _active.Clear();
        foreach (PhysicsDevourable it in _found) _active.Add(it);
    }

    /// <summary>Keo (hut) hoac giang co cac item dang trong non - chay moi frame, khong quet lai.</summary>
    private void ApplyActive()
    {
        if (_active.Count == 0) return;

        Vector3 mp = mouth.position;
        Vector3 originPos = transform.position;
        float eff = CurrentRange;
        _toRemove.Clear();

        foreach (PhysicsDevourable it in _active)
        {
            if (it == null || it.Consumed) { _toRemove.Add(it); continue; }

            int diff = it.RequiredLevel - level;   // len cap thi item giang co tu chuyen sang hut

            if (!useLevelGate || diff <= 0)
            {
                // Item dang bay vao mom thi khong duoc phep DAY nguoi choi: bo qua cap va cham
                // item x player (collider van bat nen Scan/OverlapSphere van thay item).
                it.SetPlayerCollision(_ownCols, true);

                Vector3 to = it.Center - mp;
                float dist = to.magnitude;
                if (dist <= swallowDistance) { Swallow(it); _toRemove.Add(it); continue; }
                float nearness = 1f - Mathf.Clamp01(dist / eff);
                float speed = pullSpeed * Mathf.Lerp(farSpeedFactor, 1f, nearness);
                it.Pull(mp, originPos, coneAngle, speed, pullAccel);
            }
            else
            {
                it.SetPlayerCollision(_ownCols, false);   // qua cap: van chan duong nguoi choi
                it.Struggle(mp);   // diff == 1: rung tai cho
            }
        }

        for (int i = 0; i < _toRemove.Count; i++) _active.Remove(_toRemove[i]);
    }

    private void Swallow(PhysicsDevourable it)
    {
        if (UIManager.Instance != null) UIManager.Instance.AddScore(it.scoreValue);
        AddXp(it.xpValue);
        it.Devour(mouth);        // item xoay tit + teo lao vao mom
        PlayGulp();              // than nhan vat 'uc' mot cai
        if (onDevour != null) onDevour.Invoke();
    }

    /// <summary>Kich hoat (hoac restart) cai 'uc' squash-stretch khi vua nuot.</summary>
    private void PlayGulp()
    {
        if (gulpPunch <= 0f || gulpDuration <= 0f) return;
        _gulpTimer = gulpDuration;
    }

    public int XpToNextAt(int lvl)
    {
        if (lvl >= maxLevel) return 0;
        return Mathf.Max(1, Mathf.CeilToInt(xpToNextBase * Mathf.Pow(xpGrowth, lvl - 1)));
    }

    private void AddXp(int amount)
    {
        if (level >= maxLevel) return;
        _xp += Mathf.Max(1, amount);
        while (level < maxLevel)
        {
            int need = XpToNext;
            if (need <= 0 || _xp < need) break;
            _xp -= need;
            LevelUp();
        }
        if (level >= maxLevel) _xp = 0;
    }

    private void LevelUp()
    {
        level++;
        ApplyLevelScale();
        if (onLevelUp != null) onLevelUp.Invoke();
    }

    /// <summary>Dat thang cap (cheat/test).</summary>
    public void SetLevel(int value)
    {
        level = Mathf.Clamp(value, 1, maxLevel);
        _xp = 0;
        ApplyLevelScale();
    }

    private void ApplyLevelScale()
    {
        ApplyScale(Vector3.one);
    }

    /// <summary>Dat scale = base * (he so cap do, chan tran maxScale) * (punch squash-stretch cua gulp).</summary>
    private void ApplyScale(Vector3 punch)
    {
        float lvl = 1f + scalePerLevel * (level - 1);
        if (maxScale > 0f) lvl = Mathf.Min(lvl, maxScale);
        transform.localScale = Vector3.Scale(_baseScale * lvl, punch);
    }

    void OnDrawGizmosSelected()
    {
        Transform m = mouth != null ? mouth : transform;
        float eff = range * (1f + rangePerLevel * (Mathf.Max(1, level) - 1));
        Vector3 f = m.forward;
        Vector3 p = m.position;

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        Gizmos.DrawLine(p, p + f * eff);

        float half = coneAngle * 0.5f;
        Vector3[] axes = { m.up, -m.up, m.right, -m.right };
        for (int i = 0; i < axes.Length; i++)
        {
            Vector3 rotAxis = Vector3.Cross(f, axes[i]);
            if (rotAxis.sqrMagnitude < 0.0001f) continue;
            Vector3 edge = Quaternion.AngleAxis(half, rotAxis) * f;
            Gizmos.DrawLine(p, p + edge * eff);
        }
    }
}
