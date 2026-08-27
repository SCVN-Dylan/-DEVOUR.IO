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

    [Tooltip("SKIN NGUOI CHOI = INDEX trong danh sach 'skins' cua kho skin ngay tren.\n\n" +
             "-1 = khong dat gi, giu nguyen material co san tren Player.prefab (mac dinh).\n\n" +
             "Dung chung kho voi bot, nen index nay VAN nam trong tui xao cua bot: mot con bot hoan\n" +
             "toan co the boc trung skin nguoi choi. Muon loai tru thi bao - sua mot dong trong\n" +
             "SkinSet.Draw la duoc.")]
    [SerializeField] private int _playerSkinIndex = -1;

    [Tooltip("SO BOSS - moi con nhan tron mot KHU (mot phan tu ban do). 0 = khong co boss, tat ca\n" +
             "bot sinh nhu nhau.\n\n" +
             "Map la luoi 4x4 o, va moi PHAN TU chua dung mot o moi loai (A1 bai an -> A2 -> A4 ->\n" +
             "A3 o cuoi game). Tuc bon khu la bon tuyen tien hoa y het nhau. Moi khu mot boss thi\n" +
             "bon con manh ngang nhau, moi con mot goc, va chung PHAI tranh an cua nhau de lon.\n\n" +
             "Boss dat o NUA NGOAI cua khu (0.5..0.9 ban kinh) - do la phia o A3, cho co 24/28 mon\n" +
             "hang 6 cua ca ban do.")]
    [SerializeField] private int _bossCount = 4;

    [Tooltip("Do manh cua BOSS - cung nghia levelBias: level nham toi = level nguoi choi x (1 + bias).\n" +
             "0.5 = boss luon nham cao hon nguoi choi 50%. Bot thuong dang la -0.30 .. 0.20.")]
    [SerializeField] private float _bossBiasMin = 0.45f;

    [SerializeField] private float _bossBiasMax = 0.65f;

    [Tooltip("Ban kinh vung sinh, tinh tu spawnCenter. NEN BANG NUA BE NGANG vung co item\n" +
             "(map hien tai: cac o trai tu -65 den +65, nen de 65).\n\n" +
             "De nho thi bot don cuc mot cho: ban cu de 35 va lay vi tri NGUOI CHOI lam tam, ket qua\n" +
             "la ca 16 con nam gon trong 20% ban do quanh mot goc, 9/16 o khong co con nao - ke ca\n" +
             "bon o A3 giu do hang 6.")]
    [SerializeField] private float _spawnRadius = 65f;

    [Tooltip("Khong sinh bot gan nguoi choi hon khoang nay (world). Do bang khoang cach TOI NGUOI\n" +
             "CHOI, khong phai toi tam vong sinh - hai cho do gio khac nhau.")]
    [SerializeField] private float _spawnMinDistance = 12f;

    [Tooltip("Tam vung sinh. De TRONG = tam ban do (goc toa do).\n\n" +
             "KHONG con lay vi tri nguoi choi nua: nguoi choi xuat phat o mot goc, lay no lam tam\n" +
             "thi toan bo bot do vao goc do.")]
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
    [Tooltip("CO TONG cua ca he ghim cap.\n\n" +
             "BAT: level bot luon bam quanh level nguoi choi (moc = level player x (1 + levelBias)),\n" +
             "keo theo ca phan bu level khi bot khuat man hinh o duoi - no nam BEN TRONG co nay.\n\n" +
             "TAT: bot TU AN TU LON. An duoc bao nhieu len bay nhieu, khong ghim, khong bu.\n" +
             "Luc test o che do nay bot da len toi Lv1550 trong 40 giay.\n\n" +
             "DOI DUOC GIUA LUC DANG CHAY: tat la he so an cua moi con duoc tra ve 1 ngay lap tuc\n" +
             "(xem ReleaseAiGrowth). Khong co buoc tra lai do thi con dang bi ham se ket o he so cu\n" +
             "- co the la 0 - va dung hinh het van.")]
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
    private bool _balanceLast = true;
    private bool _matchRunning;

    /// <summary>Van da bat dau chua (da de bot ra map chua). UIManager bat co nay khi bam Play.</summary>
    public bool MatchRunning { get { return _matchRunning; } }

    /// <summary>Co KHOA ITEM co dang bat khong. PhysicsDevourable doc moi lan va cham.</summary>
    public bool PushLockEnabled { get { return _pushLock; } }

    /// <summary>
    /// Bot co dang bi GHIM CAP theo nguoi choi khong. Doi duoc luc dang chay (nut debug, man
    /// setting): Update bat duoc luc co doi va tra he so an ve 1 cho moi con.
    ///
    /// TAT = bot tu an tu lon, khong ghim, khong bu level khi khuat man hinh.
    /// </summary>
    public bool BalanceAiLevel
    {
        get { return _balanceAiLevel; }
        set { _balanceAiLevel = value; }
    }

    /// <summary>Con nguoi choi dieu khien. null = chua vao van hoac da bi nuot.</summary>
    public Creature Player { get { return _player; } }

    /// <summary>Index skin nguoi choi dang dung. -1 = giu nguyen material tren prefab.</summary>
    public int PlayerSkinIndex { get { return _playerSkinIndex; } }

    /// <summary>
    /// Doi skin nguoi choi luc dang chay (man chon skin goi vao day). Ap dung ngay neu player da co
    /// mat; chua co thi so duoc nho lai va Register ap khi player dang ky.
    /// </summary>
    public void SetPlayerSkin(int index)
    {
        _playerSkinIndex = index;
        ApplyPlayerSkin();
    }

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

        // TAT co GHIM CAP giua luc dang chay -> tra he so an ve 1 cho moi bot NGAY.
        //
        // Bat buoc phai co, cung ly do voi khoi _pushLock ngay tren: co nay khong duoc doc luc bot
        // an, BalanceAiLevels chi GHI mot he so len tung SimpleSuction roi thoi. Con nao dang vuot
        // moc thi he so da bi ep ve 0 (_stopGrowRatio) - tat co ma khong tra lai thi no ket o 0
        // VINH VIEN, tuc bot dung hinh chu khong phai "tu an tu lon".
        if (_balanceAiLevel != _balanceLast)
        {
            _balanceLast = _balanceAiLevel;
            if (!_balanceAiLevel) ReleaseAiGrowth();
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
        // Nguoi choi da chet -> khong con moc nao de bam. THA THAT SU (tra he so ve 1) chu khong
        // chi ngung cap nhat: ngung khong thi con dang bi ham giu nguyen he so cu - co the la 0 -
        // va nam do tro het van, dung cai ma comment ben duoi hua la "tha bot chay tu do".
        if (_player == null) { ReleaseAiGrowth(); return; }
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

        // TAM BAN DO, khong phai vi tri nguoi choi - xem tooltip cua _spawnCenter.
        Vector3 center = _spawnCenter != null ? _spawnCenter.position : Vector3.zero;

        // Tui skin song xuyen cac lan Play trong Editor (ScriptableObject khong bi huy) - do lai
        // de van nao cung bat dau bang mot luot day du, khong dau vao khuc thua cua van truoc
        if (_aiSkins != null) _aiSkins.ResetBag();

        // ---------------------------------------------------------------- chia o
        //
        // RAI THEO LUOI VUONG, KHONG THEO VONG TRON. Vung co item la mot hinh VUONG (luoi 4x4 o
        // 28x28, trai tu -65 den +65), ma vong tron ban kinh 65 thi KHONG voi toi bon goc: tam o
        // goc cach goc toa do 72u. Do that o ban dung vong tron: bon o goc khong bao gio co bot,
        // 6/16 o trong.
        //
        // Moi o dung MOT bot: het canh 3.7 con chen nhau mot cho, va khong con o nao de player
        // vao farm mot minh.
        int gridN = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(_aiCount)));
        int cells = gridN * gridN;
        float cellSize = _spawnRadius * 2f / gridN;

        // GIA TRI tung o = tong xpValue cua item dang nam trong do. Do TAI CHO chu khong doc ten
        // 'Area N': doi bo cuc map thi cho nay tu theo, khong phai sua gi.
        float[] value = new float[cells];
        foreach (PhysicsDevourable d in Object.FindObjectsByType<PhysicsDevourable>(FindObjectsSortMode.None))
        {
            int ci = CellIndex(d.transform.position, center, gridN, cellSize);
            if (ci >= 0) value[ci] += Mathf.Max(0, d.xpValue);
        }

        int bosses = Mathf.Clamp(_bossCount, 0, _aiCount);
        int playerCell = _player != null ? CellIndex(_player.transform.position, center, gridN, cellSize) : -1;
        var bossCells = PickBossCells(value, gridN, bosses, playerCell);

        // Thu tu o cho bot thuong: o GIAU di truoc, de neu thieu bot thi phan trong roi vao o ngheo.
        var order = new System.Collections.Generic.List<int>();
        for (int i = 0; i < cells; i++) if (!bossCells.Contains(i)) order.Add(i);
        order.Sort((a, b) => value[b].CompareTo(value[a]));

        for (int i = 0; i < _aiCount; i++)
        {
            bool isBoss = i < bosses;
            int cellIdx = isBoss
                ? bossCells[i]
                : (order.Count > 0 ? order[(i - bosses) % order.Count] : 0);

            Vector3 pos;
            if (!FindSpawnPointInCell(center, cellIdx, gridN, cellSize, out pos)) continue;

            GameObject go = Object.Instantiate(_aiPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.name = isBoss ? "Boss " + (i + 1) : "Bot " + (i + 1 - bosses);

            Creature c = go.GetComponent<Creature>();
            if (c != null)
            {
                c.isPlayer = false;
                c.displayName = go.name;
                c.levelBias = isBoss
                    ? Random.Range(_bossBiasMin, _bossBiasMax)
                    : BiasForIndex(i - bosses, Mathf.Max(1, _aiCount - bosses));
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
        ApplySkin(go, c, _aiSkins.Draw());
    }

    /// <summary>
    /// Dat skin cho NGUOI CHOI theo _playerSkinIndex. Goi luc player dang ky, va moi lan doi skin
    /// qua SetPlayerSkin.
    ///
    /// Index sai thi BAO WARNING roi bo qua, khong tu kep ve dau/cuoi danh sach: kep im lang thi
    /// nguoi choi chon skin so 9 lai nhan skin so 3 ma khong ai hieu tai sao.
    /// </summary>
    private void ApplyPlayerSkin()
    {
        if (_player == null) return;

        // -1 = co y TAT, khong phai loi -> im lang.
        if (_playerSkinIndex < 0) return;

        // Nhung da chon skin ma kho skin de trong thi CHAC CHAN la quen gan, khong phai y do.
        // Im lang o day nghia la nguoi dung set skin, khong thay gi doi, va khong co manh moi nao
        // de lan - dung canh da xay ra that o scene Main (_aiSkins bo trong).
        if (_aiSkins == null)
        {
            Debug.LogWarning("[GameManager] Da chon playerSkinIndex = " + _playerSkinIndex +
                             " nhung o 'Ai Skins' dang TRONG - khong co kho skin de lay. " +
                             "Keo SkinSet.asset vao o do. (Bot cung dang khong co skin vi ly do nay.)", this);
            return;
        }

        if (_playerSkinIndex >= _aiSkins.Count)
        {
            Debug.LogWarning("[GameManager] playerSkinIndex = " + _playerSkinIndex +
                             " nhung kho skin chi co " + _aiSkins.Count +
                             " skin (index hop le 0.." + (_aiSkins.Count - 1) + "). Giu nguyen material tren prefab.", this);
            return;
        }

        ApplySkin(_player.gameObject, _player, _aiSkins.skins[_playerSkinIndex]);
    }

    /// <summary>
    /// Gan mot skin cu the: material cho than + mau cho hat VFX.
    ///
    /// PlayerVisual.SetSkin dung sharedMaterial chu khong phai material, nen cac con trung skin
    /// van dung CHUNG mot material - khong sinh material instance, khong pha batch/GPU instancing.
    /// Do la ly do o day khong dung MaterialPropertyBlock hay to mau tung con.
    /// </summary>
    private void ApplySkin(GameObject go, Creature c, SkinSet.Skin skin)
    {
        if (skin == null || go == null) return;

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
    /// THA BOT TU AN TU LON: tra he so nhan XP ve 1 cho moi con dang song.
    ///
    /// Ton tai vi he so ghim la mot gia tri DUOC GHI LEN tung SimpleSuction, khong phai mot phep
    /// tinh doc co moi lan an. Ngung ghi (tat co / player chet) khong dong nghia voi tha ra: con
    /// dang bi ham se giu nguyen he so cuoi cung, ma he so do co the bang 0.
    ///
    /// Duyet ca danh sach ke ca player: he so cua player luon la 1 san nen ghi lai khong doi gi,
    /// doi lai khoi phai them mot phep loc trong vong lap.
    /// </summary>
    private void ReleaseAiGrowth()
    {
        for (int i = 0; i < _creatures.Count; i++)
        {
            Creature c = _creatures[i];
            if (c == null || c.Suction == null) continue;
            c.Suction.xpGainMultiplier = 1f;
        }
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

    /// <summary>O thu may trong luoi chua diem nay. -1 = nam ngoai luoi.</summary>
    private int CellIndex(Vector3 p, Vector3 center, int gridN, float cellSize)
    {
        int cx = Mathf.FloorToInt((p.x - center.x + _spawnRadius) / cellSize);
        int cz = Mathf.FloorToInt((p.z - center.z + _spawnRadius) / cellSize);
        if (cx < 0 || cz < 0 || cx >= gridN || cz >= gridN) return -1;
        return cz * gridN + cx;
    }

    /// <summary>
    /// Chon o cho boss: o GIAU NHAT, va moi PHAN TU ban do chi mot con.
    ///
    /// Rai deu theo phan tu chu khong chi lay top gia tri: bon o giau nhat rat de nam canh nhau
    /// (map doi xung nen bon o A3 giong het nhau), luc do bon boss se dinh mot cuc va nua ban do
    /// khong co ai canh.
    /// </summary>
    private System.Collections.Generic.List<int> PickBossCells(float[] value, int gridN, int bosses, int avoidCell)
    {
        var chosen = new System.Collections.Generic.List<int>();
        var usedQuad = new System.Collections.Generic.HashSet<int>();

        var byValue = new System.Collections.Generic.List<int>();
        for (int i = 0; i < value.Length; i++) byValue.Add(i);
        byValue.Sort((a, b) => value[b].CompareTo(value[a]));

        int half = gridN / 2;
        // Luot 1: moi phan tu mot con, BO QUA o cua nguoi choi.
        //
        // Vi sao bo qua: boss nham cao hon nguoi choi ~50% level. Do that o ban dung o nay: mot con
        // boss sinh cach nguoi choi 14u, tuc ngay trong o xuat phat - vua vao van la dung canh mot
        // con gap ruoi minh. _spawnMinDistance (12u) khong cuu duoc vi no chi chan cai kho chiu
        // "de len dau", khong chan duoc cai chet nguoi "cung o".
        foreach (int idx in byValue)
        {
            if (chosen.Count >= bosses) break;
            if (idx == avoidCell) continue;
            int cx = idx % gridN, cz = idx / gridN;
            int quad = (cz >= half ? 2 : 0) + (cx >= half ? 1 : 0);
            if (usedQuad.Contains(quad)) continue;
            usedQuad.Add(quad);
            chosen.Add(idx);
        }
        // Luot 2: con thieu (boss nhieu hon so phan tu) thi lap day theo gia tri - van tranh o player
        foreach (int idx in byValue)
        {
            if (chosen.Count >= bosses) break;
            if (idx == avoidCell || chosen.Contains(idx)) continue;
            chosen.Add(idx);
        }
        return chosen;
    }

    /// <summary>Cham mot diem trong DUNG mot o cua luoi, chua 15% le trong de bot khong dinh mep o.</summary>
    private bool FindSpawnPointInCell(Vector3 center, int cellIdx, int gridN, float cellSize, out Vector3 pos)
    {
        int cx = cellIdx % gridN, cz = cellIdx / gridN;
        float x0 = center.x - _spawnRadius + cx * cellSize;
        float z0 = center.z - _spawnRadius + cz * cellSize;
        float pad = cellSize * 0.15f;

        pos = new Vector3(x0 + cellSize * 0.5f, center.y, z0 + cellSize * 0.5f);
        Vector3 playerPos = _player != null ? _player.transform.position : center;
        float safe = Mathf.Max(0f, _spawnMinDistance);

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector3 probe = new Vector3(Random.Range(x0 + pad, x0 + cellSize - pad), center.y,
                                        Random.Range(z0 + pad, z0 + cellSize - pad));

            if (_player != null)
            {
                Vector2 flat = new Vector2(probe.x - playerPos.x, probe.z - playerPos.z);
                if (flat.sqrMagnitude < safe * safe) continue;   // qua sat nguoi choi
            }
            if (GroundAt(probe, out pos)) return true;
        }
        return false;
    }

    /// <summary>Ban tia tu tren cao xuong tim mat dat o cho nay. Bo qua item/sinh vat (co Rigidbody).</summary>
    private bool GroundAt(Vector3 probe, out Vector3 pos)
    {
        pos = probe;
        int n = Physics.RaycastNonAlloc(probe + Vector3.up * 50f, Vector3.down, _groundBuf, 200f,
            _groundLayers, QueryTriggerInteraction.Ignore);

        float bestY = float.MinValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (_groundBuf[i].rigidbody != null) continue;
            if (_groundBuf[i].point.y > bestY) { bestY = _groundBuf[i].point.y; found = true; }
        }
        if (!found) return false;

        Vector3 candidate = new Vector3(probe.x, bestY + 0.1f, probe.z);
        if (IsSpawnBlocked(candidate)) return false;

        pos = candidate;
        return true;
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

        if (c.isPlayer)
        {
            if (_player == null) _player = c;

            // AP SKIN O NGOAI nhanh "_player == null".
            //
            // Truoc day lenh nay nam trong nhanh do, nen chi chay khi GameManager phai TU DI TIM
            // player. Scene nao keo san player vao o Inspector (MapTest dang vay) thi _player da
            // khac null tu dau -> khong bao gio ap skin, dat playerSkinIndex bao nhieu cung vo ich.
            if (_player == c) ApplyPlayerSkin();
        }
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
    /// SO BOT CON SONG - cho UI doc. Dung chung phep dem voi cua thang/thua ben duoi, de khong
    /// bao gio co canh HUD hien "con 1" trong khi luat da tinh la het.
    /// </summary>
    public int AiAlive { get { return CountAiAlive(); } }

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
