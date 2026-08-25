using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// AN MESH CHO TOI KHI DU HANG: item chua an duoc thi chi thay CAI BONG truot tren dat, khong thay
/// than. Du hang roi thi than hien ra.
///
/// ------------------------------------------------------------------------------------------
/// CACH AN: shadowCastingMode = ShadowsOnly
/// ------------------------------------------------------------------------------------------
/// Renderer bi loai khoi moi pass ve thuong nhung VAN ve vao shadow map - dung mot phep gan enum,
/// khong dung toi material lan GameObject.
///
/// Khong dung SetActive(false): tat object la mat luon cai bong, ma cai bong moi la thu can giu.
/// Khong dung doi material/alpha: OccluderFade CUNG ghi sharedMaterials cua item (lam mo vat che
/// player) va nho mang goc de tra lai - xem ghi chu o PhysicsDevourable.SetHighlight. Hai he cung
/// ghi mot cho la de nhau, ma "item vua che player vua doi hien" la chuyen thuong.
///
/// ------------------------------------------------------------------------------------------
/// DOI LUC NAO: doi bay KHUAT KHOI CAMERA roi moi doi
/// ------------------------------------------------------------------------------------------
/// Bat mesh ngay giua man hinh thi no BUP ra tu hu khong - nhin nhu loi ve. Nen khi level doi,
/// thanh phan nay chi GHI NHO trang thai muon, roi cho toi luc vat ra khoi khung hinh moi ap dung.
/// Nguoi choi khong bao gio thay khoanh khac chuyen - lan sau bay ngang qua thi no da hien san.
///
/// KHONG CO Update(): binh thuong thanh phan nay ngu hoan toan. SimpleSuction da co san onLevelUp/
/// onLevelDown (UnityEvent) nen chi can bam vao do. Chi khi dang co viec cho (level vua doi ma vat
/// van trong khung hinh) moi chay mot coroutine do 4 lan/giay, xong viec la tat.
/// </summary>
[RequireComponent(typeof(PhysicsDevourable))]
[DisallowMultipleComponent]
public class StageReveal : MonoBehaviour
{
    [Header("Tham chieu (Reset tu dien)")]
    [SerializeField] private PhysicsDevourable _item;

    [Tooltip("Cac renderer bi an. Reset tu lay het renderer con, bo he hat.")]
    [SerializeField] private Renderer[] _renderers;

    [Tooltip("De trong = dung Camera.main.")]
    [SerializeField] private Camera _camera;

    [Header("Cho ra khoi khung hinh")]
    [Tooltip("Giay giua 2 lan do 'da ra khoi camera chua'. CHI chay khi dang co viec cho,\n" +
             "binh thuong khong ton gi. 0.25 = 4 lan/giay, du nhanh de bat khoanh khac vat khuat.")]
    [SerializeField] private float _checkInterval = 0.25f;

    [Tooltip("LUOI AN TOAN: cho toi da bao nhieu giay. Het gio ma vat VAN chua ra khoi khung hinh\n" +
             "thi doi dai - tha cho nguoi choi thay mot cu bup con hon de trang thai ket sai vinh vien\n" +
             "(vd camera bam sat vat, hoac map qua nho nen no khong bao gio khuat).")]
    [SerializeField] private float _maxWait = 10f;

    [Header("Go")]
    [Tooltip("In ra Console moi lan doi trang thai va ly do.")]
    [SerializeField] private bool _log = false;

    // ------------------------------------------------------------------ trang thai chay

    private SimpleSuction _player;      // nguon su that ve level/hang, khong luu ban sao
    private bool _applied;              // dang HIEN mesh hay khong
    private bool _bound;                // da dang ky vao su kien cua player chua
    private Coroutine _waiting;

    /// <summary>Dung chung, khong cap phat moi lan do. CalculateFrustumPlanes co ban non-alloc.</summary>
    private static readonly Plane[] _planes = new Plane[6];

    // ------------------------------------------------------------------ vong doi

    private void Reset()
    {
        _item = GetComponent<PhysicsDevourable>();
        _renderers = CollectRenderers();
    }

    private void Start()
    {
        if (_item == null) _item = GetComponent<PhysicsDevourable>();
        if (_renderers == null || _renderers.Length == 0) _renderers = CollectRenderers();
        if (_camera == null) _camera = Camera.main;

        // Frame dau thi ap dung THANG, khong cho ra khoi camera: chua co gi de "thay chuyen doi",
        // ma cho o day thi vat se hien nham suot may giay dau van.
        _applied = true;                 // ep khac trang thai muon de Apply chac chan chay
        Apply(WantVisible(), "khoi tao");

        StartCoroutine(BindToPlayer());
    }

    private void OnDestroy() { Unbind(); }

    /// <summary>
    /// Player co the chua ton tai luc Start (GameManager gan _player trong Register, con bot thi
    /// sinh sau). Cho toi khi co roi moi dang ky - cho xong la coroutine chet, khong o lai.
    /// </summary>
    private IEnumerator BindToPlayer()
    {
        while (_player == null)
        {
            if (GameManager.HasInstance && GameManager.Instance.Player != null)
                _player = GameManager.Instance.Player.Suction;

            if (_player == null) yield return new WaitForSeconds(0.25f);
        }

        _player.onLevelUp.AddListener(OnLevelChanged);
        _player.onLevelDown.AddListener(OnLevelChanged);
        _bound = true;

        // Level co the da doi trong luc con dang cho player - do lai mot lan cho chac.
        OnLevelChanged();
    }

    private void Unbind()
    {
        if (!_bound || _player == null) return;
        _player.onLevelUp.RemoveListener(OnLevelChanged);
        _player.onLevelDown.RemoveListener(OnLevelChanged);
        _bound = false;
    }

    // ------------------------------------------------------------------ logic

    /// <summary>Du hang de an chua. Hoi thang SimpleSuction chu khong che lai luat hang o day.</summary>
    private bool WantVisible()
    {
        if (_player == null || _item == null) return false;
        return _player.Stage >= _player.StageAtLevel(_item.requiredLevel);
    }

    private void OnLevelChanged()
    {
        bool want = WantVisible();
        if (want == _applied) return;             // khong doi gi -> khoi cho, khoi coroutine

        if (_waiting != null) StopCoroutine(_waiting);
        _waiting = StartCoroutine(WaitOffscreenThenApply(want));
    }

    /// <summary>
    /// Cho toi khi vat khuat khoi khung hinh roi moi doi. Ra khoi camera NGAY tu lan do dau thi
    /// doi luon trong 1/4 giay - khong co do tre nao dang ke.
    /// </summary>
    private IEnumerator WaitOffscreenThenApply(bool want)
    {
        float waited = 0f;
        var tick = new WaitForSeconds(Mathf.Max(0.02f, _checkInterval));

        while (OnScreen())
        {
            if (waited >= _maxWait)
            {
                Apply(want, "het gio cho (" + _maxWait + "s) - doi dai");
                _waiting = null;
                yield break;
            }
            waited += _checkInterval;
            yield return tick;

            // Level co the doi nguoc lai trong luc dang cho -> khong con viec gi de lam nua.
            if (WantVisible() != want) { _waiting = null; yield break; }
        }

        Apply(want, "da ra khoi khung hinh");
        _waiting = null;
    }

    /// <summary>Con nam trong khung hinh khong. Khong co camera thi coi nhu khong - cu doi ngay.</summary>
    private bool OnScreen()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return false;

        Bounds b;
        if (!MeasureBounds(out b)) return false;

        GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
        return GeometryUtility.TestPlanesAABB(_planes, b);
    }

    private void Apply(bool visible, string why)
    {
        if (visible == _applied) return;
        _applied = visible;

        ShadowCastingMode mode = visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].shadowCastingMode = mode;

        if (_log)
            Debug.Log("[StageReveal] " + name + (visible ? " HIEN mesh" : " AN mesh (con bong)") + " - " + why);
    }

    // ------------------------------------------------------------------ phan viec nho

    /// <summary>He hat khong co vo de an/hien, bo ra - giong cach PhysicsDevourable loc renderer.</summary>
    private Renderer[] CollectRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && !(all[i] is ParticleSystemRenderer)) n++;

        Renderer[] keep = new Renderer[n];
        int k = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && !(all[i] is ParticleSystemRenderer)) keep[k++] = all[i];
        return keep;
    }

    private bool MeasureBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool has = false;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || !_renderers[i].enabled) continue;
            if (!has) { bounds = _renderers[i].bounds; has = true; }
            else bounds.Encapsulate(_renderers[i].bounds);
        }
        return has;
    }
}
