using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DAU MOI cua mot van: giu NGUOI CHOI va DANH SACH moi sinh vat dang song.
///
/// Sinh ra de tra loi hai cau hoi ma truoc day code phai doan bang
/// FindAnyObjectByType&lt;SimpleSuction&gt;() - luc scene chi co 1 con thi doan lam sao cung dung,
/// nhung them 3 con AI la doan trung bot ma khong bao loi gi, chi sai am tham (camera zoom theo
/// bot, diem cua bot nhay len HUD nguoi choi...).
///
///   GameManager.Instance.Player     -> con nguoi choi dieu khien
///   GameManager.Instance.Creatures  -> tat ca, de AI do doi thu bang KHOANG CACH
///
/// Danh sach nay la nguon duy nhat: AI khong phai OverlapSphere di tim nhau, chi duyet mot List
/// 4 phan tu - gan nhu mien phi so voi mot physics query.
///
/// Khac UIManager (lo HUD, dong ho, man ket thuc), GameManager lo PHE - ai dang trong van, ai la
/// nguoi choi. Hai thang khong nuot viec cua nhau.
/// </summary>
[DefaultExecutionOrder(-100)]   // Awake chay TRUOC moi Creature de dang ky khong bi hut
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    /// <summary>
    /// Truy cap tu bat ky dau. Scene chua dat san GameManager (scene test) thi TU TAO mot cai -
    /// nho vay Creature khong phai rai null-check khap noi, va scene test cu van chay nhu cu.
    /// </summary>
    public static GameManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Object.FindAnyObjectByType<GameManager>();
            if (_instance == null && Application.isPlaying)
            {
                GameObject go = new GameObject("GameManager (auto)");
                _instance = go.AddComponent<GameManager>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// Da co GameManager song chua - KHONG tu tao ra cai moi nhu Instance.
    /// Dung luc go bo (OnDisable/OnDestroy): thoat Play mode thi GameManager co the bi huy
    /// truoc cac Creature, luc do goi Instance se di tao mot GameManager moi chi de rut ten ra
    /// khoi no roi bo di - rac vo nghia.
    /// </summary>
    public static bool HasInstance { get { return _instance != null; } }

    [Tooltip("Con do NGUOI CHOI dieu khien. De trong = tu nhan ra qua Creature.isPlayer khi no vao van.\n" +
             "Gan tay o day cung duoc, luc do coi nhu chot cung.")]
    [SerializeField] private Creature _player;

    [Header("Sinh AI")]
    [Tooltip("Prefab bot (Prefab Variant cua Player). De trong = khong sinh bot nao")]
    [SerializeField] private GameObject _aiPrefab;

    [Tooltip("So bot trong mot van")]
    [SerializeField] private int _aiCount = 3;

    [Tooltip("Kho skin cho bot. De trong = bot giu nguyen material tren prefab (tat ca giong nhau).\n" +
             "Skin duoc boc kieu 'tui xao' nen so bot <= so skin thi chac chan khong con nao trung")]
    [SerializeField] private SkinSet _aiSkins;

    [Tooltip("Bot sinh ra trong vanh khan quanh tam: tu spawnMinDistance den spawnRadius.\n" +
             "Co vanh trong de bot khong de ngay tren dau nguoi choi luc bat dau van")]
    [SerializeField] private float _spawnRadius = 35f;

    [SerializeField] private float _spawnMinDistance = 12f;

    [Tooltip("Tam vong sinh. De trong = lay vi tri nguoi choi")]
    [SerializeField] private Transform _spawnCenter;

    [Tooltip("Layer tinh la MAT DAT khi ban tia xuong tim cho dat chan")]
    [SerializeField] private LayerMask _groundLayers = ~0;

    [Header("Can bang level AI theo nguoi choi")]
    [Tooltip("BAT: level bot luon bam quanh level nguoi choi. TAT: bot choi song phang, muon len\n" +
             "bao nhieu thi len (luc test bot da len toi Lv1550 trong 40 giay)")]
    [SerializeField] private bool _balanceAiLevel = true;

    [Tooltip("Do lech YEU NHAT (-0.30 = co con chiu kem nguoi choi 30%)")]
    [SerializeField] private float _aiBiasMin = -0.30f;

    [Tooltip("Do lech MANH NHAT (+0.20 = co con manh hon nguoi choi 20%)")]
    [SerializeField] private float _aiBiasMax = 0.20f;

    [Tooltip("Bao lau tinh lai he so mot lan (giay). Khong can moi frame - level doi cham")]
    [SerializeField] private float _balanceInterval = 1f;

    [Tooltip("Bot dang THUA moc nhieu nhat thi an nhanh gap may lan")]
    [SerializeField] private float _catchUpMax = 2.5f;

    [Tooltip("Bot VUOT moc bao nhieu lan thi NGUNG LON han (1.5 = vuot 50% la dung).\n" +
             "Ham lai bang cach ngung lon chu khong tru level nguoc - tru nguoc se thay bot tu\n" +
             "nhien teo di khong ly do")]
    [SerializeField] private float _stopGrowRatio = 1.5f;

    private readonly List<Creature> _creatures = new List<Creature>();
    private static readonly RaycastHit[] _groundBuf = new RaycastHit[8];
    private float _balanceTimer;

    /// <summary>Con nguoi choi dieu khien. null = chua vao van hoac da bi nuot.</summary>
    public Creature Player { get { return _player; } }

    /// <summary>Moi sinh vat dang song. CHI DOC - them/bot phai qua Register/Unregister.</summary>
    public IReadOnlyList<Creature> Creatures { get { return _creatures; } }

    /// <summary>Con nguoi choi con song khong (dung cho AI: het player thi khoi ngam).</summary>
    public bool HasPlayer { get { return _player != null; } }

    /// <summary>
    /// Xoa static khi bat dau van moi. Bat buoc phai co neu du an bat "Enter Play Mode Options"
    /// (tat domain reload cho vao Play nhanh): luc do static KHONG tu reset giua hai lan chay,
    /// Instance se con tro vao GameManager cua van truoc (da bi huy).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        SpawnAI();
    }

    void Update()
    {
        if (!_balanceAiLevel) return;

        _balanceTimer -= Time.deltaTime;
        if (_balanceTimer > 0f) return;
        _balanceTimer = Mathf.Max(0.1f, _balanceInterval);

        BalanceAiLevels();
    }

    /// <summary>
    /// Giu level bot BAM QUANH level nguoi choi bang cach chinh TOC DO AN, khong chinh level.
    ///
    /// Moi bot co mot moc rieng = level_nguoi_choi x (1 + bias). Thua moc thi an nhanh len, vuot
    /// moc thi cham lai, vuot qua _stopGrowRatio thi ngung lon han. Nho vay level bot khong bao
    /// gio nhay giat - nhin ngoai chi thay con nay choi gioi, con kia choi do.
    ///
    /// Nguoi choi chet roi thi tha bot chay tu do (khong con moc de bam).
    /// </summary>
    private void BalanceAiLevels()
    {
        if (_player == null) return;
        int playerLevel = Mathf.Max(1, _player.Level);

        for (int i = 0; i < _creatures.Count; i++)
        {
            Creature c = _creatures[i];
            if (c == null || c == _player || c.isPlayer || c.IsDead || c.Suction == null) continue;

            int target = Mathf.Max(1, Mathf.RoundToInt(playerLevel * (1f + c.levelBias)));
            float ratio = (float)c.Level / target;

            float mul;
            if (ratio < 1f)
                mul = Mathf.Lerp(_catchUpMax, 1f, ratio);                                  // thua moc -> an nhanh hon
            else
                mul = Mathf.Lerp(1f, 0f, (ratio - 1f) / Mathf.Max(0.01f, _stopGrowRatio - 1f));   // vuot moc -> cham dan roi dung

            c.Suction.xpGainMultiplier = Mathf.Max(0f, mul);
        }
    }

    /// <summary>
    /// De bot ra map. Goi luc Start (sau khi nguoi choi da dang ky xong o OnEnable) nen lay
    /// duoc vi tri nguoi choi lam tam vong sinh.
    /// </summary>
    public void SpawnAI()
    {
        if (_aiPrefab == null || _aiCount <= 0) return;

        Vector3 center = _spawnCenter != null
            ? _spawnCenter.position
            : (_player != null ? _player.transform.position : transform.position);

        // Tui skin song xuyen cac lan Play trong Editor (ScriptableObject khong bi huy) - do lai
        // de van nao cung bat dau bang mot luot day du, khong dau vao khuc thua cua van truoc
        if (_aiSkins != null) _aiSkins.ResetBag();

        for (int i = 0; i < _aiCount; i++)
        {
            Vector3 pos;
            if (!FindSpawnPoint(center, out pos)) continue;

            GameObject go = Object.Instantiate(_aiPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.name = "Bot " + (i + 1);

            Creature c = go.GetComponent<Creature>();
            if (c != null)
            {
                c.isPlayer = false;
                c.displayName = go.name;
                c.levelBias = BiasForIndex(i, _aiCount);
            }

            PlayerNameTag tag = go.GetComponentInChildren<PlayerNameTag>(true);
            if (tag != null) tag.playerName = go.name;

            ApplyRandomSkin(go, c);
        }
    }

    /// <summary>
    /// Boc mot skin trong _aiSkins gan cho bot vua sinh: material cho than + mau cho hat VFX.
    ///
    /// PlayerVisual.SetSkin dung sharedMaterial chu khong phai material, nen cac bot trung skin
    /// van dung CHUNG mot material - khong sinh material instance, khong pha batch/GPU instancing.
    /// Do la ly do o day khong dung MaterialPropertyBlock hay to mau tung con.
    /// </summary>
    private void ApplyRandomSkin(GameObject go, Creature c)
    {
        if (_aiSkins == null) return;

        SkinSet.Skin skin = _aiSkins.Draw();
        if (skin == null) return;

        if (skin.material != null)
        {
            PlayerVisual visual = go.GetComponentInChildren<PlayerVisual>(true);
            if (visual != null) visual.SetSkin(skin.material);
        }

        // Mau nay duoc doc luc con NAY bi hut, de hat bay ra mang mau cua no
        if (c != null) c.skinColor = skin.particleColor;
    }

    /// <summary>
    /// Do lech level cho bot thu i - TRAI DEU trong khoang [min..max] roi rung nhe, chu KHONG
    /// random thuan tung con.
    ///
    /// Random thuan 3 con hoan toan co the ra ca 3 deu yeu hon nguoi choi (xac suat khong nho:
    /// gan 1/8), luc do van choi khong con ai de so. Trai deu thi luon chac chan co ca con yeu
    /// lan con manh, phan rung chi de hai van khong giong het nhau.
    /// </summary>
    private float BiasForIndex(int index, int count)
    {
        if (count <= 1) return Random.Range(_aiBiasMin, _aiBiasMax);

        float t = (index + 0.5f) / count;
        float bias = Mathf.Lerp(_aiBiasMin, _aiBiasMax, t);

        float slot = (_aiBiasMax - _aiBiasMin) / count;
        return bias + Random.Range(-slot * 0.35f, slot * 0.35f);
    }

    /// <summary>
    /// Cham mot diem ngau nhien trong vanh khan roi ban tia tu tren cao xuong tim mat dat.
    /// Khong tim duoc dat thi bo qua lan do - tha thieu mot bot con hon de no roi mai xuong duoi map.
    /// </summary>
    private bool FindSpawnPoint(Vector3 center, out Vector3 pos)
    {
        pos = center;
        float minR = Mathf.Min(_spawnMinDistance, _spawnRadius);

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            float r = Random.Range(minR, Mathf.Max(minR, _spawnRadius));
            Vector3 probe = center + new Vector3(dir.x * r, 0f, dir.y * r);

            int n = Physics.RaycastNonAlloc(probe + Vector3.up * 50f, Vector3.down, _groundBuf, 200f,
                _groundLayers, QueryTriggerInteraction.Ignore);

            float bestY = float.MinValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                // Bo qua item/sinh vat (co Rigidbody): de bot len dau cai banh mi thi no roi xuong ngay
                if (_groundBuf[i].rigidbody != null) continue;
                if (_groundBuf[i].point.y > bestY) { bestY = _groundBuf[i].point.y; found = true; }
            }
            if (!found) continue;

            pos = new Vector3(probe.x, bestY + 0.1f, probe.z);
            return true;
        }
        return false;
    }

    /// <summary>Creature tu goi luc OnEnable. Goi lai nhieu lan cung khong vao trung.</summary>
    public void Register(Creature c)
    {
        if (c == null || _creatures.Contains(c)) return;
        _creatures.Add(c);

        if (c.isPlayer && _player == null) _player = c;
    }

    /// <summary>
    /// MOT CHO DUY NHAT quyet dinh "ai chet thi lam gi". Creature.Die() goi vao day.
    ///
    ///   nguoi choi chet -> Game Over, than bien mat
    ///   bot chet        -> huy han, KHONG hoi sinh
    ///
    /// Item con nay dang giu tu duoc tha ra trong SimpleSuction.OnDestroy, khong phai lo o day.
    /// </summary>
    public void ReportDeath(Creature victim, Creature killer)
    {
        if (victim == null) return;
        Unregister(victim);

        if (victim.isPlayer)
        {
            if (UIManager.Instance != null) UIManager.Instance.GameOver();
            victim.gameObject.SetActive(false);
            return;
        }

        Destroy(victim.gameObject);
    }

    /// <summary>Creature tu goi luc OnDisable / bi nuot.</summary>
    public void Unregister(Creature c)
    {
        if (c == null) return;
        _creatures.Remove(c);

        if (_player == c) _player = null;
    }
}
