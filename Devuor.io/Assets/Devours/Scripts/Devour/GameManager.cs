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

    [Tooltip("Ban kinh khoang TRONG bat buoc phai co o cho dat chan (world).\n\n" +
             "Khong co cai nay thi bot co the sinh ra NGAY TRONG LONG mot toa nha: tia do dat co y bo\n" +
             "qua moi vat co Rigidbody (de bot khong dung tren nong cai banh mi), nen no khong he biet\n" +
             "cho do dang co nha, cu tha bot xuong nen dat ben duoi.\n\n" +
             "Ket trong long collider la ket VINH VIEN: raycast khong tinh la trung khi tia xuat phat\n" +
             "tu ben trong, nen tia rau cua bot bao trong o ca 8 huong trong khi no dang bi chan cung.")]
    [SerializeField] private float _spawnClearance = 0.6f;

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

    [Header("Khoa item khi ke huc vao qua yeu")]
    [Tooltip("BAT (mac dinh): item hon ke huc vao tu 3 hang tro len se KHOA CUNG, huc vao nhu huc\n" +
             "tuong. Bao nhieu hang moi khoa thi chinh o tung item (PhysicsDevourable.pushLockStageDiff).\n\n" +
             "TAT: khong con khoa gi ca, dung cai gi cung day duoc nhu vat ly binh thuong.\n\n" +
             "Tat GIUA LUC DANG CHAY thi moi item dang bi khoa duoc tha ra ngay - khong con con nao\n" +
             "ket cung lai giua map.")]
    [SerializeField] private bool _pushLock = true;

    [Header("Bu level khi bot KHUAT khoi camera")]
    [Tooltip("BAT: bot dang nam ngoai khung hinh thi duoc keo level len thang toi moc, khong phai\n" +
             "cho an du item.\n\n" +
             "VI SAO CAN: co che ghim cu chi vat TOC DO AN, ma nguon thu cua bot la nhat item -\n" +
             "trong khi mot pha bi hut mat 10-100 level/giay. He so nhan khong bao gio khep lai duoc\n" +
             "mot khoang cach da gian ra. Con day la keo thang.\n\n" +
             "Chi lam khi nguoi choi KHONG NHIN THAY, nen khong bao giay hien tuong so nhay truoc mat.")]
    [SerializeField] private bool _offscreenCatchUp = true;

    [Range(0f, 1f)]
    [Tooltip("Moi GIAY dong bao nhieu PHAN khoang cach toi moc. 0.5 = mot nua.\n" +
             "Lech 100 level thi giay dau bu 50, giay sau 25... khoang 4 giay la gan khop.\n\n" +
             "Dong theo TI LE chu khong phai so level co dinh: lech nhieu thi tu nhanh, gan toi thi\n" +
             "tu cham lai, khong co bac thang.")]
    [SerializeField] private float _offscreenCatchUpFraction = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Phai ra khoi mep khung THEM bao nhieu phan kich thuoc khung moi tinh la KHUAT.\n" +
             "0.15 = 15%. Co bien nay de con vua le nua nguoi ngoai mep khong bi coi la khuat.")]
    [SerializeField] private float _offscreenMargin = 0.15f;

    [Tooltip("Camera dung de xet khuat. De trong = tu lay Camera.main")]
    [SerializeField] private Camera _viewCamera;

    private readonly List<Creature> _creatures = new List<Creature>();
    private static readonly RaycastHit[] _groundBuf = new RaycastHit[8];
    private static readonly Collider[] _clearBuf = new Collider[8];
    private float _balanceTimer;
    private bool _pushLockLast = true;
    private bool _matchRunning;

    /// <summary>Van da bat dau chua (da de bot ra map chua). UIManager bat co nay khi bam Play.</summary>
    public bool MatchRunning { get { return _matchRunning; } }

    /// <summary>Co KHOA ITEM co dang bat khong. PhysicsDevourable doc moi lan va cham.</summary>
    public bool PushLockEnabled { get { return _pushLock; } }

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
        // UIManager la thang cam nhip van (Home -> bam Play -> vao choi), no se goi StartMatch.
        // Scene KHONG co UIManager (scene test) thi vao van ngay - giu nguyen hanh vi cu.
        if (UIManager.Instance == null) StartMatch();
    }

    /// <summary>
    /// VAO VAN: de bot ra map. Goi tu UIManager.StartMatch luc bam Play.
    ///
    /// Vi sao khong spawn o Start nhu truoc: luc do 8 con bot da chay long nhong sau tam BG cua
    /// man Home, an mat item va lon len trong khi nguoi choi chua bam gi. Nang hon nua la dieu
    /// kien THANG dem "con bao nhieu bot" - dem luc chua spawn thi ra 0, thang ngay tu man Home.
    ///
    /// Goi lai lan hai khong lam gi (khong sinh them mot lua bot nua).
    /// </summary>
    public void StartMatch()
    {
        if (_matchRunning) return;
        _matchRunning = true;

        SpawnAI();
    }

    void Update()
    {
        // TAT co giua luc dang chay -> tha ngay moi item dang bi khoa. Khong co doan nay thi
        // nhung con da khoa se ket cung vinh vien, vi khoa chi duoc go o luc va cham / bi hut.
        if (_pushLock != _pushLockLast)
        {
            _pushLockLast = _pushLock;
            if (!_pushLock) ReleaseAllPushLocks();
        }

        if (!_balanceAiLevel || !_matchRunning) return;

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

        // Doi "dong X phan khoang cach moi GIAY" sang "moi NHIP": co vay chinh _balanceInterval
        // khong lam toc do bu nhanh cham theo. Nhip 1 giay thi step = fraction.
        float step = _offscreenCatchUp
            ? 1f - Mathf.Pow(1f - Mathf.Clamp01(_offscreenCatchUpFraction), Mathf.Max(0.1f, _balanceInterval))
            : 0f;

        for (int i = 0; i < _creatures.Count; i++)
        {
            Creature c = _creatures[i];
            if (c == null || c == _player || c.isPlayer || c.IsDead || c.Suction == null) continue;

            int target = Mathf.Max(1, Mathf.RoundToInt(playerLevel * (1f + c.levelBias)));

            // BU LEVEL - chi khi bot dang KHUAT, va chi keo LEN.
            //
            // Khong keo xuong: mot con dang to ma tu nhien teo di la thu nguoi choi se nhan ra
            // ngay khi no quay lai khung, du luc teo khong ai nhin. Con vuot moc van bi ham bang
            // xpGainMultiplier nhu cu - cham hon nhung khong bao gio lo.
            //
            // Dat TRUOC khi tinh ratio: he so an item ngay duoi phai doc level MOI, khong thi bot
            // vua duoc bu xong con bi cho an nhanh gap 2 lan nua trong ca nhip nay.
            if (step > 0f && c.Level < target && IsOffscreen(c))
            {
                int boosted = Mathf.Min(target, Mathf.CeilToInt(Mathf.Lerp(c.Level, target, step)));
                if (boosted > c.Level) c.Suction.SetLevel(boosted);   // CeilToInt de khong ket lai o 1 level cuoi
            }

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
    /// Bot co dang NAM NGOAI khung hinh khong.
    ///
    /// Xet bang VIEWPORT chu khong bang khoang cach: camera zoom to dan theo level nguoi choi
    /// (ortho size 5 -> 100 theo bang moc), nen mot khoang cach co dinh se luc thi nam ngoai luc
    /// thi nam trong. Viewport thi tu co gian theo dung cai khung dang hien.
    ///
    /// Khong tim thay camera thi tra FALSE - coi nhu dang bi nhin. Doan mu ma bu level la kieu
    /// loi im lang toi nhat: khong ai thay gi sai cho toi khi bot phinh ra giua man hinh.
    /// </summary>
    private bool IsOffscreen(Creature c)
    {
        if (_viewCamera == null) _viewCamera = Camera.main;
        if (_viewCamera == null) return false;

        Vector3 v = _viewCamera.WorldToViewportPoint(c.Center);
        if (v.z < 0f) return true;   // nam sau lung camera

        float m = Mathf.Max(0f, _offscreenMargin);
        return v.x < -m || v.x > 1f + m || v.y < -m || v.y > 1f + m;
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

            // TINH CACH bam theo do lech level cua CHINH con do: con duoc ghim manh hon nguoi choi
            // thi hung (dam an ca con ngang co), con bi ghim yeu hon thi nhat (chay som).
            //
            // Suy tu levelBias chu khong tu chi so i: nhu vay tinh cach va suc manh khong bao gio
            // noi nguoc nhau, va phan rung cua BiasForIndex duoc thua huong luon.
            AIController ai = go.GetComponent<AIController>();
            if (ai != null && c != null) ai.SetAggression(AggressionForBias(c.levelBias));

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
    /// Tha het item dang bi khoa. Chi goi luc co VUA bi tat, khong phai moi frame.
    /// </summary>
    private static void ReleaseAllPushLocks()
    {
        int n = 0;
        foreach (PhysicsDevourable it in Object.FindObjectsByType<PhysicsDevourable>(FindObjectsSortMode.None))
        {
            if (it == null) continue;
            it.ClearPushLock();
            n++;
        }
        Debug.Log("[GameManager] Da tat khoa item, tha " + n + " item.");
    }

    /// <summary>
    /// DO HUNG HANG 0..1 cua mot bot, suy tu do lech level cua no trong khoang [min..max].
    /// Yeu nhat -> 0 (nhat), manh nhat -> 1 (hung). Xem AIController.SetAggression.
    /// </summary>
    private float AggressionForBias(float bias)
    {
        float span = _aiBiasMax - _aiBiasMin;
        if (span < 0.0001f) return 0.5f;   // hai dau bang nhau: khong co gi de chia, tat ca dung giua
        return Mathf.Clamp01((bias - _aiBiasMin) / span);
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

            Vector3 candidate = new Vector3(probe.x, bestY + 0.1f, probe.z);
            if (IsSpawnBlocked(candidate)) continue;   // cho nay dang co nha/item dung - boc cho khac

            pos = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Cho dinh tha bot co bi vat gi CHIEM CHO khong.
    ///
    /// Do o TAM THAN chu khong o chan: dat qua thap thi qua cau nao cung cham mat dat.
    /// Bo qua nhung gi nam TRON VEN duoi chan (mat dat, via he, buc them phang) - do la cho dung
    /// chu khong phai vat can.
    /// </summary>
    private bool IsSpawnBlocked(Vector3 pos)
    {
        float r = Mathf.Max(0.05f, _spawnClearance);
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up * r, r, _clearBuf, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            Collider c = _clearBuf[i];
            if (c == null) continue;
            if (c.bounds.max.y <= pos.y + 0.05f) continue;   // nam duoi chan - la cho dung, khong phai vat can
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
            if (UIManager.Instance != null) UIManager.Instance.EndMatch(false);
            victim.gameObject.SetActive(false);
            return;
        }

        Destroy(victim.gameObject);

        // BOT CUOI CUNG chet -> THANG.
        //
        // Dem o day (su kien chet) chu khong quet moi frame: mot van chi co dam chuc lan chet, va
        // moi lan chi duyet mot List <10 phan tu - re hon nhieu so voi mot phep dem moi frame.
        if (_matchRunning && CountAiAlive() == 0 && UIManager.Instance != null)
            UIManager.Instance.EndMatch(true);
    }

    /// <summary>
    /// So bot CON SONG. victim da bi Unregister ngay dau ReportDeath nen no khong con bi dem.
    ///
    /// Loc ca IsDead: con dang bay vao mom ke giet da rut ten khoi danh sach roi, nhung con nao
    /// vua goi Die() trong cung frame ma chua toi luot ReportDeath thi van con trong list.
    /// </summary>
    private int CountAiAlive()
    {
        int n = 0;
        for (int i = 0; i < _creatures.Count; i++)
        {
            Creature c = _creatures[i];
            if (c == null || c == _player || c.isPlayer || c.IsDead) continue;
            n++;
        }
        return n;
    }

    /// <summary>Creature tu goi luc OnDisable / bi nuot.</summary>
    public void Unregister(Creature c)
    {
        if (c == null) return;
        _creatures.Remove(c);

        if (_player == c) _player = null;
    }
}
