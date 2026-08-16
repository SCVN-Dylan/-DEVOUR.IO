using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kho TE BAO: sinh - thu hoi - dung lai cac vien te bao rot ra khi mot con bi hut.
///
/// Te bao chinh la mot PhysicsDevourable binh thuong, nen no duoc HUT bang dung bo may co san
/// (vung hut keo vao, luat gianh quyen cua M2, cong XP luc nuot). Class nay khong biet gi ve
/// chuyen hut - no chi lo VONG DOI cua vien te bao.
///
/// VI SAO PHAI CO POOL: moi vien la mot Rigidbody + collider + renderer. Bon con danh nhau o
/// level cao co the doi hoi hang chuc vien moi giay; Instantiate/Destroy voi nhip do la rac GC
/// that su tren mobile. Nen o day giu san mot ro, dung xong tra lai.
///
/// BA CAI TRAN, cai nao cham truoc thi cai do chan:
///   1. maxAlive  - so vien song cung luc. Vuot thi thu hoi vien GIA NHAT.
///   2. lifetime  - khong ai an thi tu ve kho, khong thi map ngap te bao.
///   3. nhip nha  - nam ben Creature (moi nan nhan mot nhip rieng).
/// </summary>
[DisallowMultipleComponent]
public class CellPool : MonoBehaviour
{
    private static CellPool _instance;

    /// <summary>Kho te bao trong scene. null = scene khong co -> khong sinh te bao (khong loi).</summary>
    public static CellPool Instance
    {
        get
        {
            if (_instance == null) _instance = Object.FindAnyObjectByType<CellPool>();
            return _instance;
        }
    }

    public static bool HasInstance { get { return _instance != null; } }

    [Header("Prefab")]
    [Tooltip("Prefab vien te bao (Cell.prefab). De trong = khong sinh te bao nao")]
    [SerializeField] private GameObject _cellPrefab;

    [Header("Tran")]
    [Tooltip("So vien song cung luc toi da. Vuot qua thi vien GIA NHAT bi thu hoi ngay.\n" +
             "Day la cai chan cuoi cung giu cho may khong chet khi 4 con danh nhau o level cao")]
    [SerializeField] private int _maxAlive = 80;

    [Tooltip("Khong ai an thi sau bao lau tu ve kho (giay)")]
    [SerializeField] private float _lifetime = 6f;

    [Tooltip("Tao san bao nhieu vien luc bat dau van, de khong bi khung lag o cu danh nhau dau tien")]
    [SerializeField] private int _prewarm = 24;

    [Header("Luc bat ra")]
    [Tooltip("Van toc bat NGANG khoi than nan nhan (u/s)")]
    [SerializeField] private float _popSpeed = 2.5f;

    [Tooltip("Van toc bat LEN cao (u/s) - de vien nay len mot chut cho de nhin")]
    [SerializeField] private float _popUp = 2.5f;

    [Range(0f, 1f)]
    [Tooltip("Do LECH ngau nhien so voi huong mom ke hut. 0 = bay thang vao mom ke hut (no an het),\n" +
             "1 = van tung tung phia - con thu ba de cuop hon")]
    [SerializeField] private float _popSpread = 0.55f;

    /// <summary>So vien dang song (de debug / do dac).</summary>
    public int AliveCount { get { return _alive.Count; } }

    private struct Live { public PhysicsDevourable cell; public float dieAt; }

    private readonly List<Live> _alive = new List<Live>();
    private readonly Stack<PhysicsDevourable> _idle = new Stack<PhysicsDevourable>();
    private Transform _root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;

        _root = new GameObject("Cells").transform;
        _root.SetParent(transform, false);

        for (int i = 0; i < _prewarm; i++)
        {
            PhysicsDevourable c = Create();
            if (c == null) break;
            c.gameObject.SetActive(false);
            _idle.Push(c);
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        // Quet het han: duyet nguoc de xoa tai cho khong lam xao tron chi so
        float now = Time.time;
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            Live l = _alive[i];
            if (l.cell == null) { _alive.RemoveAt(i); continue; }
            if (now < l.dieAt) continue;
            if (l.cell.Owner != null) continue;   // dang bi keo vao mom thi cho no an xong da

            Recycle(l.cell);
        }
    }

    /// <summary>
    /// Nha MOT vien te bao tai vi tri nan nhan, bat lech ve phia mom ke hut.
    /// Tra ve null neu chua cau hinh prefab.
    /// </summary>
    public PhysicsDevourable Spawn(Vector3 position, Vector3 towardAttacker, int xpValue)
    {
        if (_cellPrefab == null) return null;

        // Cham tran thi thu hoi vien GIA NHAT - vien moi bat ra la vien dang duoc nhin nhat
        if (_maxAlive > 0 && _alive.Count >= _maxAlive) Recycle(_alive[0].cell);

        PhysicsDevourable cell = _idle.Count > 0 ? _idle.Pop() : Create();
        if (cell == null) return null;

        cell.transform.SetPositionAndRotation(position, Random.rotationUniform);
        cell.gameObject.SetActive(true);
        cell.ResetForReuse();

        cell.xpValue = Mathf.Max(1, xpValue);
        cell.scoreValue = cell.xpValue;

        Vector3 dir = towardAttacker;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Random.insideUnitSphere;

        Vector2 jitter = Random.insideUnitCircle * _popSpread;
        Vector3 outDir = (dir + new Vector3(jitter.x, 0f, jitter.y)).normalized;

        cell.Launch(outDir * _popSpeed + Vector3.up * _popUp);

        _alive.Add(new Live { cell = cell, dieAt = Time.time + _lifetime });
        return cell;
    }

    /// <summary>
    /// NO TUNG mot cuc XP thanh nhieu vien cung luc (dung khi mot con bi nuot - M7).
    /// Chia deu XP cho so vien, phan du don vao vien dau.
    /// </summary>
    public void SpawnBurst(Vector3 position, int totalXp, int cellCount)
    {
        if (totalXp <= 0 || cellCount <= 0) return;

        int per = Mathf.Max(1, totalXp / cellCount);
        int left = totalXp;
        for (int i = 0; i < cellCount && left > 0; i++)
        {
            int xp = (i == cellCount - 1) ? left : Mathf.Min(per, left);
            Vector2 d = Random.insideUnitCircle.normalized;
            Spawn(position, new Vector3(d.x, 0f, d.y), xp);
            left -= xp;
        }
    }

    private PhysicsDevourable Create()
    {
        if (_cellPrefab == null) return null;

        GameObject go = Object.Instantiate(_cellPrefab, _root);
        PhysicsDevourable cell = go.GetComponent<PhysicsDevourable>();
        if (cell == null)
        {
            Debug.LogError("[CellPool] Prefab te bao thieu PhysicsDevourable: " + _cellPrefab.name, _cellPrefab);
            Destroy(go);
            return null;
        }

        // Nuot xong thi ve kho chu khong Destroy. Item thuong tren map khong gan callback nay
        // nen van bi huy nhu cu.
        cell.onReleaseToPool = Recycle;
        return cell;
    }

    private void Recycle(PhysicsDevourable cell)
    {
        if (cell == null) return;

        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i].cell == cell) { _alive.RemoveAt(i); break; }

        cell.gameObject.SetActive(false);
        if (!_idle.Contains(cell)) _idle.Push(cell);
    }
}
