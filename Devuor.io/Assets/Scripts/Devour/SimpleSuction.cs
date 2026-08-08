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

    [Tooltip("Toc do keo item ve mieng (don vi/giay)")]
    public float pullSpeed = 10f;

    [Tooltip("Tam item vao gan mieng hon khoang nay thi nuot")]
    public float swallowDistance = 0.6f;

    [Tooltip("Layer duoc phep hut")]
    public LayerMask suckableLayers = ~0;

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
    [Range(0f, 0.5f)]
    [Tooltip("Moi cap nhan vat to them bao nhieu (0.12 = +12%/cap)")]
    public float scalePerLevel = 0.12f;

    [Range(0f, 0.5f)]
    [Tooltip("Moi cap non hut dai ra bao nhieu (0.15 = +15%/cap)")]
    public float rangePerLevel = 0.15f;

    [Header("Su kien")]
    public UnityEvent onDevour;
    public UnityEvent onLevelUp;

    public int Level { get { return level; } }
    public int Xp { get { return _xp; } }
    public int XpToNext { get { return XpToNextAt(level); } }
    public float CurrentRange { get { return range * (1f + rangePerLevel * (level - 1)); } }

    private Vector3 _baseScale;
    private int _xp;
    private readonly HashSet<PhysicsDevourable> _prev = new HashSet<PhysicsDevourable>();
    private readonly HashSet<PhysicsDevourable> _curr = new HashSet<PhysicsDevourable>();
    private static readonly Collider[] _hits = new Collider[128];

    void Awake()
    {
        _baseScale = transform.localScale;
        if (mouth == null)
        {
            Transform m = transform.Find("Mouth");
            mouth = m != null ? m : transform;
        }
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
        Vector3 mp = mouth.position;
        Vector3 fwd = mouth.forward;
        float eff = CurrentRange;
        float half = coneAngle * 0.5f;

        _curr.Clear();

        int n = Physics.OverlapSphereNonAlloc(mp, eff, _hits, suckableLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (_hits[i] == null) continue;
            PhysicsDevourable it = _hits[i].GetComponentInParent<PhysicsDevourable>();
            if (it == null || it.Consumed || _curr.Contains(it)) continue;
            if (useLevelGate && it.RequiredLevel > level) continue;   // qua cap thi khong hut

            Vector3 to = it.Center - mp;
            float dist = to.magnitude;
            if (dist > eff) continue;
            if (dist > 0.001f && Vector3.Angle(fwd, to) > half) continue;

            _curr.Add(it);

            if (dist <= swallowDistance) { Swallow(it); continue; }
            it.Pull(mp, pullSpeed * Time.fixedDeltaTime);
        }

        // Item khong con trong non nua -> tha ra cho tu roi
        foreach (PhysicsDevourable it in _prev)
            if (it != null && !_curr.Contains(it)) it.Release();

        _prev.Clear();
        foreach (PhysicsDevourable it in _curr) _prev.Add(it);
    }

    private void Swallow(PhysicsDevourable it)
    {
        if (UIManager.Instance != null) UIManager.Instance.AddScore(it.scoreValue);
        _prev.Remove(it);
        AddXp(it.xpValue);
        it.Devour();
        if (onDevour != null) onDevour.Invoke();
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
        transform.localScale = _baseScale * (1f + scalePerLevel * (level - 1));
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
