using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Vung hut hinh NON phia truoc nhan vat - ban gon, it thong so (thay cho MouthSuction cu).
///
/// Moi FixedUpdate: quet OverlapSphere quanh mieng, loc theo goc non + cap do, roi keo
/// (Pull) cac PhysicsDevourable trong non ve phia mieng. Cham mieng thi nuot (cong XP/diem).
/// Item ra khoi non thi duoc Release de tu roi.
///
/// LEVEL = 1 + TONG XP da an. An 1 XP la len 1 level, KHONG CO TRAN, reset moi van.
/// Mon cang to (xpValue cang lon) thi nhay cang nhieu level trong mot mieng an.
/// Level hien thi tren PlayerNameTag ("Lv N" tren dau nhan vat).
/// </summary>
[DisallowMultipleComponent]
public class SimpleSuction : MonoBehaviour
{
    // Class LevelStep da chuyen ra file rieng (SuctionConfig.cs) vi 'levelSteps' gio song trong
    // SuctionConfig - de nguyen o day thi ScriptableObject phai tham chieu nguoc lai mot
    // MonoBehaviour chi de lay mot kieu du lieu.

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

    [Tooltip("Bao lau nhac lai tieng 'khong an noi' mot lan (giay), khi con chia mom vao vat qua cap.\n" +
             "Phai >= do dai clip (access denied dai 1.4s), khong thi tieng sau de len tieng truoc")]
    public float deniedSoundInterval = 1.5f;

    [Tooltip("Tam item vao gan mieng hon khoang nay thi nuot")]
    public float swallowDistance = 0.6f;

    [Tooltip("LUOI AN TOAN chong lot. Item bay nhanh co the nhay TU ngoai vung nuot RA HAN sau mom\n" +
             "trong dung MOT buoc physics - nguong nuot khong bao gio duoc kiem tra, item xuyen qua\n" +
             "nguoi choi (va cham item x player dang tat trong luc hut nen khong co gi chan).\n\n" +
             "Nguong nuot duoc noi rong toi thieu bang: quang duong item di trong 1 buoc x he so nay.\n" +
             "0 = tat, chi dung swallowDistance.\n\n" +
             "O pullSpeed mac dinh (12) he so nay KHONG an gi: 12 x 0.02 x 1.5 = 0.36 < 0.6. No chi\n" +
             "bat dau co tac dung khi toc do thuc vuot ~20 u/s - dung la luoi cho luc tune manh tay.")]
    public float swallowSpeedMargin = 1.5f;

    [Tooltip("Layer duoc phep hut. Nen dat item vao layer rieng de OverlapSphere quet it collider hon")]
    public LayerMask suckableLayers = ~0;

    [Tooltip("Giay giua 2 lan QUET vung hut (OverlapSphere - phan dat nhat). Keo item van muot moi frame. 0 = quet moi frame")]
    public float scanInterval = 0.05f;

    [Tooltip("TRAN so item duoc hut cung luc. Vuot qua thi CAT THEO KHOANG CACH: giu nhung cai\n" +
             "GAN nhat, bo phan ngoai ria truoc (khong phai bo ngau nhien).\n" +
             "0 = khong gioi han, hut het moi thu trong non.")]
    public int maxActiveItems = 64;

    [Range(0.5f, 1f)]
    [Tooltip("GIANH ITEM tu tay con khac: phai gan mom hon chu cu it nhat bay nhieu lan moi gianh\n" +
             "duoc (0.85 = phai gan hon 15%). Mot item chi duoc DUNG MOT con keo.\n\n" +
             "De 1 = ai gan hon mot ti cung gianh -> hai con xap xi nhau se giat item qua lai moi\n" +
             "lan quet. Cang nho cang 'lo lang', chu cu giu chac hon.\n" +
             "Rieng con AN DUOC thi gianh dut khoi tay con chi lam item giay, khong xet khoang cach.")]
    public float itemStealMargin = 0.85f;

    [Header("Hut SINH VAT khac (combat)")]
    [Tooltip("BAT: con nao lot vao non hut cua minh thi bi rut XP -> teo dan.\n" +
             "KHONG co gate cap do: con yeu van hut duoc con manh, hai con hut nhau thi ca hai cung tut.")]
    public bool drainCreatures = true;

    [Range(0f, 1f)]
    [Tooltip("NHIP RUT ban dau (giay): cu bay nhieu giay thi nan nhan mat 1 level va mot object\n" +
             "VFX bay ve mom minh. 1.0 = 1 level/giay luc vua dinh.")]
    public float drainInterval = 1f;

    [Range(0.1f, 0.99f)]
    [Tooltip("Moi lan rut xong thi nhip NHAN voi so nay -> o trong vung cang lau, rut cang nhanh.\n" +
             "0.8 = moi nhip ngan di 20%: 1.0 -> 0.8 -> 0.64 -> 0.51 -> ... cho toi san.")]
    public float drainIntervalDecay = 0.8f;

    [Range(0.05f, 1f)]
    [Tooltip("SAN: nhip khong duoc ngan hon bao nhieu PHAN so voi nhip goc.\n" +
             "0.3 = dung lai o 30% (1.0 -> 0.3 giay, tuc toi da 3.33 level/giay).\n" +
             "Khong co san thi nhip ve 0 va thanh rut vo han moi frame.")]
    public float drainIntervalFloor = 0.3f;

    [Tooltip("NGUOI DAN khi thoat ra: moi giay ngoai vung hut thi nhip hoi lai bao nhieu (giay/giay).\n" +
             "Nguoi dan chu khong reset ngay - reset ngay thi nguoi choi lach ra lach vao la xoa\n" +
             "sach do ghi, tran danh khong bao gio tich duoc ap luc.")]
    public float drainIntervalRecover = 0.5f;

    [Range(0.05f, 1f)]
    [Tooltip("Ti le toc do rut o RIA XA nhat cua non so voi sat mom. Dung chung duong cong voi\n" +
             "farSpeedFactor cua item: dung ria thi bi gam nhe, bi dua vao sat mom thi tan rat nhanh")]
    public float creatureFarDrainFactor = 0.35f;

    // KHONG CON creaturePullSpeed. Nan nhan khong bi keo ve phia mom nua, chi bi CHAM LAI
    // (Creature.victimSlow). Luc keo cu la 2.5 u/s - chinh theo toc do 5 cua Player.prefab, nhung
    // AI.prefab ha speed xuong 2.5 x 0.85 = 2.13 nen luc keo thanh xap xi 100% toc do chay: hai
    // ben triet tieu nhau va nhan vat dung im giua khong trung. Chinh cho no yeu di thi nan nhan
    // luon di ra xa (hut ma khong lai gan). Bo han thi khong con bai toan can luc nao nua.

    [Header("Bang can bang (dung chung)")]
    [Tooltip("BAT BUOC. Asset chua toan bo so can bang: eatOnContact, useLevelGate, scalePerLevel,\n" +
             "levelSteps, maxScale, speedFollowScale, rangePerLevel.\n\n" +
             "Player va bot tro chung mot asset -> doi mot cho la ca van theo, khong con canh moc\n" +
             "nam tren prefab mot kieu con instance trong scene mot kieu.\n" +
             "Muon bot khac player thi tao asset thu hai roi keo vao prefab bot.")]
    [SerializeField] private SuctionConfig _config;

    /// <summary>Bang can bang dang dung. BAT BUOC keo vao - Awake se bao loi neu de trong.</summary>
    public SuctionConfig Config { get { return _config; } }

    public bool EatOnContact { get { return Config.eatOnContact; } }
    public bool UseLevelGate { get { return Config.useLevelGate; } }
    public float ScalePerLevel { get { return Config.scalePerLevel; } }
    public List<LevelStep> LevelSteps { get { return Config.levelSteps; } }
    public float MaxScale { get { return Config.maxScale; } }
    public float SpeedFollowScale { get { return Config.speedFollowScale; } }
    public float RangePerLevel { get { return Config.rangePerLevel; } }

    // LEVEL = 1 + TONG XP da an. An 1 XP la len 1 level, KHONG CO TRAN, reset moi van.
    //
    // KHONG ve len Inspector va KHONG serialize: day la TRANG THAI RUNTIME chu khong phai so can
    // bang. De public thi vua bi nham la "chinh o day de dat level khoi dau" (thuc ra Awake ghi
    // de ngay), vua bi ghi lai moi mieng an nen gia tri luu trong prefab khong co y nghia gi.
    // Doc tu ngoai: dung property Level. Dat level (cheat/test): dung SetLevel().
    private int level = 1;

    [Header("Hieu ung 'uc' khi nuot (gulp)")]
    [Range(0f, 0.6f)]
    [Tooltip("Do manh cai giat squash-stretch moi lan nuot (0.18 = phinh cao / co ngang 18%). 0 = tat")]
    public float gulpPunch = 0.18f;

    [Tooltip("Thoi gian 1 cai 'uc' (giay)")]
    public float gulpDuration = 0.22f;

    [Range(0.5f, 4f)]
    [Tooltip("So nhip nhun len-xuong trong 1 cai uc. Cao = rung nhieu lan")]
    public float gulpWobbles = 1.3f;

    [Tooltip("Object nhan cai giat 'uc'. De trong = tu tim con ten 'Graphic'.\n" +
             "Phai KHAC object goc: goc dang duoc tween scale theo level, hai tween cung ghi vao\n" +
             "mot localScale se da nhau. Punch tren Graphic con loi nua: capsule collider khong bi\n" +
             "bop theo nen khong sinh giat vat ly.")]
    public Transform punchTarget;

    [Header("Cu POP khi vuot MOC level")]
    [Tooltip("BAT: vuot mot moc CO LAM TO THAN thi than NEN LAI lay da roi BUNG vot qua co moi.\n" +
             "Len level thuong (giua hai moc) khong co gi - de moc moi la su kien.\n\n" +
             "CHI NO KHI THAN THAT SU TO RA. Moc chi dong toi camera hoac chi doi hinh dang\n" +
             "(add = 0) thi khong pop, va moc dang bi maxScale chan cung khong pop - bung ra\n" +
             "trong khi co van y nguyen la noi doi.")]
    public bool popOnStep = true;

    [Tooltip("BAT: cu pop keo theo ca CAMERA - khung hinh cung nen vao mot nhip roi bung ra.\n" +
             "TAT: chi THAN pop, camera doi co khung thang, khong lay da.\n\n" +
             "Camera VAN doi size theo moc trong ca hai truong hop - cai nay chi tat phan HIEU UNG\n" +
             "(nhip nen truoc khi bung), khong tat zoom.")]
    public bool popAffectsCamera = true;

    [Range(0f, 0.5f)]
    [Tooltip("Do NEN luc lay da (0.15 = bep xuong 15%). Nen truoc thi cu bung sau moi 'co da' -\n" +
             "nguyen tac anticipation: muon vat bung manh phai keo lui no truoc")]
    public float popSquash = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Do BUNG vot QUA co moi (0.35 = vot qua 35% roi moi lun ve)")]
    public float popStretch = 0.35f;

    [Tooltip("Thoi gian nhip NEN (giay)")]
    public float popSquashTime = 0.12f;

    [Tooltip("Thoi gian nhip BUNG (giay)")]
    public float popStretchTime = 0.10f;

    [Tooltip("Thoi gian nhip LUN ve co chuan (giay)")]
    public float popSettleTime = 0.22f;

    [Range(0f, 0.3f)]
    [Tooltip("HITSTOP: khung ca game lai bao lau tai dinh bung (giay). 0 = tat.\n\n" +
             "CHI CHAY CHO NGUOI CHOI. Bot len moc lien tuc - de bot cung lam khung thi game giat\n" +
             "suot ca van.")]
    public float popHitstop = 0.05f;

    [Range(0.01f, 1f)]
    [Tooltip("Trong luc hitstop, timeScale ha xuong con bao nhieu. 0.05 = gan nhu dung han.\n" +
             "Khong dat 0 tuyet doi de cac he chay theo timeScale khong bi chia cho 0")]
    public float popHitstopScale = 0.05f;

    [Tooltip("Object VFX bat len dung khoanh khac BUNG roi tat di (vd mot ParticleSystem vong sang).\n" +
             "De trong = khong co VFX. Object nay nen TAT san trong prefab.")]
    public GameObject popVfx;

    [Tooltip("Bat popVfx trong bao lau roi tat (giay)")]
    public float popVfxDuration = 0.6f;

    [Header("Muot ma (DOTween)")]
    [Tooltip("Thoi gian phong to sang co moi khi len level (giay). 0 = to ngay tuc thi")]
    public float scaleTweenDuration = 0.25f;

    [Tooltip("Kieu easing khi phong to")]
    public Ease scaleTweenEase = Ease.OutBack;

    [Tooltip("Kieu easing khi TEO LAI (bi con khac hut mat level). Tach rieng khoi luc to ra:\n" +
             "OutBack vot qua diem dich roi moi ve - luc to ra thi day la cai nay 'bung' ra dep,\n" +
             "nhung luc teo lai se thanh co qua muc roi phinh nguoc, nhin nhu bi giat.")]
    public Ease shrinkTweenEase = Ease.OutQuad;

    [Tooltip("De trong = tu tim CameraLevelZoom trong scene. Len level thi day FOV moi sang no")]
    public CameraLevelZoom cameraZoom;

    [Tooltip("De trong = tu tim PlayerVisual tren chinh object nay. Qua moc co isEvolution thi\n" +
             "day so lan tien hoa sang no de doi hinh dang")]
    public PlayerVisual playerVisual;

    [Tooltip("VFX ban ra moi lan TIEN HOA (moc co isEvolution). De trong = tu tim ParticleSystem\n" +
             "ten 'LevelupCylinderBlue' trong con.\n\n" +
             "Ban bang Play(true) nen ba he hat con (than tru + Dust + Lines) cung no mot luot")]
    public ParticleSystem evolveVfx;

    [Header("Tham chieu (keo vao Inspector)")]
    [Tooltip("Bo di chuyen - nhan he so toc do theo co than. Reset/them component la tu dien san")]
    [SerializeField] private RbMovement _movement;

    [Tooltip("Danh tinh cua con nay (nguoi choi hay AI). Thieu = coi nhu nguoi choi")]
    [SerializeField] private Creature _creature;

    [Header("Su kien")]
    public UnityEvent onDevour;
    public UnityEvent onLevelUp;

    [Tooltip("Ban khi TUT level (bi con khac hut mat XP). Cam VFX/SFX 'bi teo' vao day")]
    public UnityEvent onLevelDown;

    public int Level { get { return level; } }

    /// <summary>Tong XP da an trong van nay. Level = 1 + Xp.</summary>
    public int Xp { get { return _xp; } }

    /// <summary>
    /// STAGE noi bo (1..so moc + 1), KHONG hien thi cho nguoi choi.
    /// = 1 + so moc trong LevelSteps ma Level da voi toi.
    ///   Lv 1-9 -> stage 1 | Lv 10-24 -> stage 2 | Lv 25-49 -> stage 3 | ...
    /// Dung lam COT TRAI cua ma tran gate an item.
    /// </summary>
    public int Stage { get { return _stage; } }

    /// <summary>So lan TIEN HOA da qua (so moc isEvolution ma Level da voi toi). 0 = dang goc.</summary>
    public int EvolutionCount { get { return _evolutionCount; } }

    /// <summary>
    /// Level dung de tinh zoom camera - bang thang Level, KHONG con bi chan.
    ///
    /// Truoc day gia tri nay bi DONG BANG khi he so scale cham MaxScale, y la "than dung to thi
    /// camera cung phai dung zoom ra". Hau qua thuc te: bang LevelSteps vot qua MaxScale o Lv150
    /// (scale 24.39 -> 32.39 trong khi tran la 30) nen camera ket cung o orthographicSize 18.25
    /// tu do tro di, du maxSize dat toi 100 va bang moc con chay tiep.
    ///
    /// Da bo chan theo yeu cau: camera zoom tiep theo dung bang moc, diem dung duy nhat con lai
    /// la CameraLevelZoom.maxSize.
    /// </summary>
    public int ZoomLevel { get { return level; } }
    /// <summary>
    /// Chieu dai non hut hien tai.
    ///
    ///   = range x (1 + RangePerLevel x (level-1))   <- phan tang deu, GIU NGUYEN cach cu
    ///   + tong rangeAdd cua moi moc da qua          <- phan giat o moc, cong THEM len tren
    ///
    /// Hien tai moi rangeAdd deu = 0 nen ket qua y het truoc khi co cot nay.
    /// Khac voi scale: level trung moc o day VAN duoc cong RangePerLevel (khong tru di), de
    /// phan tang deu dung y nguyen cong thuc cu.
    /// </summary>
    public float CurrentRange
    {
        get
        {
            float r = range * (1f + RangePerLevel * (level - 1));
            if (LevelSteps != null)
            {
                for (int i = 0; i < LevelSteps.Count; i++)
                {
                    LevelStep s = LevelSteps[i];
                    if (s == null || s.level < 2 || s.level > level) continue;
                    r += s.rangeAdd;
                }
            }
            return r;
        }
    }

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
    private float _scaleFactor = 1f;   // cache, chi tinh lai khi level doi
    private int _stage = 1;            // cache, tinh lai cung luc voi _scaleFactor
    private int _evolutionCount;       // so moc isEvolution da qua
    private int _scaleStepHits;        // so moc CO 'add' (thuc su lam to than) da qua
    private Tween _scaleTween;
    /// <summary>
    /// HE SO NHAN XP khi nuot. GameManager chinh so nay cho AI de giu level bot bam quanh level
    /// nguoi choi (xem GameManager.BalanceAiLevels). Nguoi choi luon = 1.
    ///
    /// Chinh TOC DO AN chu khong chinh thang level: keo level nhay phat mot thi nguoi choi nhin
    /// thay ngay, va no pha luong XP di qua te bao.
    /// </summary>
    [System.NonSerialized] public float xpGainMultiplier = 1f;

    private int _xp;
    private float _gainRemainder;      // phan XP le khi he so nhan khac 1
    private float _drainClock;         // quy thoi gian da tich cho nhip rut hien tai
    private float _drainInterval;      // nhip rut hien tai (giay/level) - siet dan khi bi ghi lau
    private float _scanTimer;
    private readonly HashSet<PhysicsDevourable> _active = new HashSet<PhysicsDevourable>();
    private readonly HashSet<PhysicsDevourable> _found = new HashSet<PhysicsDevourable>();
    private readonly List<PhysicsDevourable> _toRemove = new List<PhysicsDevourable>();
    private struct Candidate { public PhysicsDevourable item; public float dist; public bool canEat; }
    private readonly List<Candidate> _candidates = new List<Candidate>();

    private const int MaxOverlapBuffer = 4096;   // chan tren, phong truong hop map dien ro
    private static Collider[] _hits = new Collider[128];
    private Collider[] _ownCols;

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    /// <summary>
    /// LUOI AN TOAN cho ref bi quen keo (prefab cu, object dung tay trong scene test).
    /// Ref da co san tren prefab thi khong ham nao o day dong toi.
    /// </summary>
    private void AutoFill()
    {
        if (_movement == null) _movement = GetComponent<RbMovement>();
        if (_creature == null) _creature = GetComponent<Creature>();
        if (playerVisual == null) playerVisual = GetComponent<PlayerVisual>();

        if (evolveVfx == null)
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
                if (ps != null && ps.name == "LevelupCylinderBlue") { evolveVfx = ps; break; }
        if (mouth == null)
        {
            Transform m = transform.Find("Mouth");
            mouth = m != null ? m : transform;
        }
        if (punchTarget == null)
        {
            Transform g = transform.Find("Graphic");
            punchTarget = g != null ? g : transform;
        }
    }

    void Awake()
    {
        // Bao NGAY tu Awake thay vi de NullReference no ra giua tran: loi o day co kem object
        // nen bam vao la nhay dung con thieu, con NullReference trong FixedUpdate thi chi thay
        // stack trace giua vong lap nong, khong biet con nao.
        if (_config == null)
            Debug.LogError("[SimpleSuction] Chua keo SuctionConfig vao '" + name +
                           "'. Keo asset Assets/Devours/Data/SuctionConfig.asset vao o 'Config'.", this);

        _baseScale = transform.localScale;
        AutoFill();

        // KHONG serialize mang nay: no la toan bo collider con cua chinh minh, quet mot lan luc
        // Awake. Keo tay thi them/bot mot collider trong prefab la mang nam im khong ai biet,
        // va loi do (item van dam vao nguoi choi) rat kho lan ra.
        // LOAI COLLIDER CUA MOM ra khoi mang nay. Mang nay di vao Physics.IgnoreCollision de item
        // dang bay khong day duoc nguoi choi - ma IgnoreCollision tat luon ca CALLBACK TRIGGER.
        // Nhet mom vao day thi item dang bi hut se khong bao gio ban OnTriggerEnter voi mom, tuc
        // an-bang-mom chet dung voi nhung item can no nhat.
        //
        // An toan: mom la trigger, no khong day duoc cai gi ca - de ngoai danh sach khong mat gi.
        Collider[] allCols = GetComponentsInChildren<Collider>(true);
        List<Collider> keep = new List<Collider>(allCols.Length);
        for (int i = 0; i < allCols.Length; i++)
        {
            Collider c = allCols[i];
            if (c == null) continue;
            if (mouth != null && (c.transform == mouth || c.transform.IsChildOf(mouth))) continue;
            keep.Add(c);
        }
        _ownCols = keep.ToArray();

        ResolveCameraZoom();

        // Giu bat bien Level = 1 + Xp ngay tu dau (level authored tren prefab = level khoi dau)
        if (level < 1) level = 1;
        _xp = level - 1;

        ApplyProgression(true);
    }

    void OnDestroy()
    {
        // TRA TIME SCALE truoc moi thu khac. Player chet dung luc dang hitstop thi callback tra
        // lai timeScale nam trong tween cua chinh object nay - object bi huy, tween bi kill, va
        // ca game ket vinh vien o 0.05x. Loi kieu nay khong bao gio bat duoc luc test binh thuong.
        if (_hitstopActive) { Time.timeScale = 1f; _hitstopActive = false; }

        if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Kill();
        _scaleTween = null;
        if (punchTarget != null) punchTarget.DOKill();
        DOTween.Kill(this);

        // Dang go ca scene (thoat Play, doi man) thi khoi tra item: moi thu sap bi huy het,
        // dung vao Physics.IgnoreCollision luc nay chi to sinh loi.
        if (!gameObject.scene.isLoaded) return;

        // Chet giua chung (bi con khac nuot) thi phai TRA het item dang giu. Khong tra thi chung
        // ket vinh vien o trang thai Sucked - kinematic, khong trong luc, dang teo nho - va treo
        // lo lung giua khong trung vi khong con ai keo nua.
        foreach (PhysicsDevourable it in _active)
            if (it != null && it.Owner == this) it.Release(this);
        _active.Clear();
    }

    /// <summary>
    /// Con nay co phai NGUOI CHOI khong. Chua gan Creature (scene cu, scene test) thi coi nhu
    /// LA nguoi choi - de nhung scene dang chay khong doi hanh vi gi sau khi them he AI.
    /// </summary>
    private bool IsPlayerOwned { get { return _creature == null || _creature.isPlayer; } }

    /// <summary>
    /// CHI nguoi choi moi duoc cam camera.
    ///
    /// Ban cu: moi SimpleSuction deu tu FindAnyObjectByType&lt;CameraLevelZoom&gt;() - luc scene chi
    /// co 1 con thi dung, nhung 3 con AI (clone tu chinh prefab player, mang theo ca tham chieu
    /// da serialize) cung se nam chung mot camera va bot len level la camera zoom theo bot.
    /// Nen o day AI bi CAT hang thang, con nguoi choi thi tu dang ky nguoc lai vao camera de
    /// CameraLevelZoom khong phai di do xem ai la player.
    /// </summary>
    private void ResolveCameraZoom()
    {
        if (!IsPlayerOwned) { cameraZoom = null; return; }

        if (cameraZoom == null) cameraZoom = Object.FindAnyObjectByType<CameraLevelZoom>();
        if (cameraZoom != null) cameraZoom.player = this;
    }

    /// <summary>
    /// Nac thu may (1..n) ung voi mot moc level bat ky. Dung chung cho ca player lan item:
    ///   player: StageAtLevel(level)          -> dang o nac nao
    ///   item  : StageAtLevel(requiredLevel)  -> thuoc HANG nao (A=1, B=2, ... F=6)
    /// Nho vay khong phai them field 'tier' vao item - requiredLevel 1/10/25/50/90/150 tu no
    /// da chi ra hang roi, doi moc trong LevelSteps la ca hai ben tu theo.
    /// </summary>
    public int StageAtLevel(int lv)
    {
        int s = 1;
        if (LevelSteps == null) return s;
        for (int i = 0; i < LevelSteps.Count; i++)
        {
            LevelStep st = LevelSteps[i];
            if (st == null || st.level < 2 || lv < st.level) continue;
            s++;
        }
        return s;
    }

    /// <summary>
    /// PhysicsDevourable goi khi no cham vao mot collider cua minh: an neu cham dung MOM va du HANG.
    ///
    /// CHI MOM MOI AN DUOC, khong phai ca than. Cham than chi day nhau ra - muon an thi phai chia
    /// mom vao. Ban truoc bat ca than: chay ngang qua dong do an la nuot sach, toan bo pha hut
    /// (bay - xoan - teo - vao mom) khong bao gio duoc nhin thay.
    ///
    /// 'hit' la collider CUA MINH ma item vua cham vao, khong phai collider cua item.
    /// </summary>
    public void EatByContact(PhysicsDevourable it, Collider hit)
    {
        if (!EatOnContact || it == null || it.Consumed) return;
        if (mouth == null || hit == null) return;
        if (hit.transform != mouth && !hit.transform.IsChildOf(mouth)) return;

        if (UseLevelGate && StageAtLevel(it.RequiredLevel) > _stage) return;   // qua hang thi cham cung khong an
        Swallow(it);
    }

    void FixedUpdate()
    {
        // QUET (dat: OverlapSphere + GetComponentInParent) chay theo scanInterval,
        // con KEO/GIANG CO chay moi frame cho muot -> nhe hon nhieu tren mobile.
        _scanTimer -= Time.fixedDeltaTime;
        if (_scanTimer <= 0f) { Scan(); _scanTimer = Mathf.Max(0f, scanInterval); }

        ApplyActive();
        DrainCreatures();
    }

    /// <summary>
    /// Huong truc cua non hut, da lam phang xuong mat dat. Dung chung cho ca quet item lan quet
    /// sinh vat de hai ben khong bao gio lech nhau mot goc nao.
    /// </summary>
    private Vector3 ConeForward()
    {
        Vector3 fwd = mouth != null ? mouth.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
        fwd.y = 0f;
        fwd.Normalize();
        return fwd;
    }

    /// <summary>
    /// RUT XP cac sinh vat khac dang nam trong non hut cua minh.
    ///
    /// KHONG di qua OverlapSphere: danh sach sinh vat chi co vai con va da nam san trong
    /// GameManager, duyet thang la xong. Nhet chung vao vong quet item se phai them mot
    /// GetComponentInParent&lt;Creature&gt;() cho TUNG collider trong vong lap nong nhat cua game -
    /// dat gap nhieu lan ma khong duoc them gi.
    ///
    /// Chay moi FixedUpdate (khong theo scanInterval nhu item): phep tinh chi la vai phep tru
    /// vector, ma rut XP thi can lien tuc cho muot.
    /// </summary>
    private void DrainCreatures()
    {
        if (!drainCreatures || _creature == null || !GameManager.HasInstance) return;

        var all = GameManager.Instance.Creatures;
        if (all.Count < 2) return;

        Vector3 origin = transform.position;
        Vector3 fwd = ConeForward();
        float eff = CurrentRange;
        float half = coneAngle * 0.5f;
        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < all.Count; i++)
        {
            Creature c = all[i];
            if (c == null || c == _creature) continue;

            Vector3 to = c.Center - origin;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > eff) continue;
            if (dist > 0.001f && Vector3.Angle(fwd, to) > half) continue;

            float nearness = 1f - Mathf.Clamp01(dist / eff);
            float prox = Mathf.Lerp(creatureFarDrainFactor, 1f, nearness);

            // Truyen QUY THOI GIAN da nhan prox, khong phai luong XP: dung ria thi dong ho chay
            // cham (nhip thua ra), sat mom thi chay du toc. Nan nhan tu giu nhip cua rieng no.
            c.ReceiveDrain(_creature, dt * prox);

            // Ghi so cho CHINH MINH nua: ReceiveDrain chi bao cho nan nhan biet no dang danh nhau
            // voi ai. Khong co dong nay thi con di hut khong he biet minh dang o trong tran, va
            // bo phan vai (VaiTro theo level) khong co du lieu de chay.
            _creature.NoteCombat(c);
        }
    }

    // KHONG CO Update: cai giat 'uc' gio la DOPunchScale, scale/speed/camera chi tinh lai
    // trong ApplyProgression() luc len level.

    /// <summary>
    /// Quet het collider trong ban kinh, TU NOI BUFFER neu chat.
    ///
    /// OverlapSphereNonAlloc khong bao gio ghi qua kich thuoc mang: day 200 collider vao mang 128
    /// thi no tra ve dung 128 va IM LANG bo 72 cai - khong loi, khong warning. Ma thu tu dien mang
    /// la thu tu broadphase cua PhysX, khong theo khoang cach, nen 72 cai bi bo la ngau nhien:
    /// con xe ngay truoc mom hoan toan co the bi loai. Phai nhin thay HET thi moi chon duoc
    /// "gan nhat" cho dung.
    ///
    /// Dau hieu day khit (n == Length) = gan nhu chac chan da bi cat -> nhan doi mang, quet lai.
    /// Mang la static nen chi phinh vai lan dau van, sau do dung lai, khong sinh rac moi frame.
    /// </summary>
    private int OverlapAll(Vector3 origin, float radius)
    {
        int n = Physics.OverlapSphereNonAlloc(origin, radius, _hits, suckableLayers, QueryTriggerInteraction.Ignore);
        while (n >= _hits.Length && _hits.Length < MaxOverlapBuffer)
        {
            _hits = new Collider[Mathf.Min(_hits.Length * 2, MaxOverlapBuffer)];
            n = Physics.OverlapSphereNonAlloc(origin, radius, _hits, suckableLayers, QueryTriggerInteraction.Ignore);
        }
        return n;
    }

    private static int CompareByDist(Candidate a, Candidate b) { return a.dist.CompareTo(b.dist); }

    /// <summary>Quet lai danh sach item nam trong non (phan dat tien). Item roi khoi non -> tha ra.</summary>
    private void Scan()
    {
        Vector3 origin = transform.position;   // Vi tri chân player
        Vector3 fwd = ConeForward();

        float eff = CurrentRange, half = coneAngle * 0.5f;

        _found.Clear();
        _candidates.Clear();

        int n = OverlapAll(origin, eff);
        for (int i = 0; i < n; i++)
        {
            if (_hits[i] == null) continue;
            PhysicsDevourable it = _hits[i].GetComponentInParent<PhysicsDevourable>();
            if (it == null || it.Consumed) continue;

            int diff = StageAtLevel(it.RequiredLevel) - _stage;   // HIEU HANG, khong phai hieu level
            if (UseLevelGate && diff >= 2) continue;   // hon 2+ hang: khong tac dong gi

            Vector3 to = it.Center - origin;
            to.y = 0f;   // Kiem tra goc va khoang cach phang tren mat dat
            float dist = to.magnitude;
            if (dist > eff) continue;
            if (dist > 0.001f && Vector3.Angle(fwd, to) > half) continue;

            if (_found.Add(it))
                _candidates.Add(new Candidate { item = it, dist = dist, canEat = !UseLevelGate || diff <= 0 });
        }

        // Qua dong thi CAT THEO KHOANG CACH: giu cac item gan nhat, bo phan ngoai ria truoc.
        if (maxActiveItems > 0 && _candidates.Count > maxActiveItems)
        {
            _candidates.Sort(CompareByDist);
            _found.Clear();
            for (int i = 0; i < maxActiveItems; i++) _found.Add(_candidates[i].item);
        }

        // GIANH QUYEN GIU: item nao con khac dang giu chac hon thi loai khoi danh sach cua minh.
        // Phai lam SAU khi cat theo maxActiveItems - khong thi minh di gianh ca nhung item ma
        // chinh minh cung se vut di, cuop khong cua con khac roi bo do.
        for (int i = 0; i < _candidates.Count; i++)
        {
            Candidate c = _candidates[i];
            if (!_found.Contains(c.item)) continue;
            if (!c.item.TryClaim(this, c.dist, c.canEat, itemStealMargin)) _found.Remove(c.item);
        }

        // Tha nhung item da ra khoi non - nhung CHI nhung cai MINH con dang giu. Cai da bi con
        // khac gianh mat thi de yen: no dang duoc keo do dang, tha ra la no rot xuong dat.
        foreach (PhysicsDevourable it in _active)
            if (it != null && !_found.Contains(it) && it.Owner == this) it.Release(this);

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

        int pulled = 0, struggling = 0;

        foreach (PhysicsDevourable it in _active)
        {
            // Owner != this = vua bi con khac gianh mat giua hai lan quet. Bo ra ngay va KHONG
            // goi Release: chu moi dang keo, minh tha ra la pha cua no.
            if (it == null || it.Consumed || it.Owner != this) { _toRemove.Add(it); continue; }

            int diff = StageAtLevel(it.RequiredLevel) - _stage;   // len hang thi item giang co tu chuyen sang hut

            if (!UseLevelGate || diff <= 0)
            {
                // Item dang bay vao mom thi khong duoc phep DAY nguoi choi: bo qua cap va cham
                // item x player (collider van bat nen Scan/OverlapSphere van thay item).
                it.SetPlayerCollision(_ownCols, true);

                Vector3 to = it.Center - mp;
                float dist = to.magnitude;

                // Nguong nuot noi rong theo toc do THUC cua item - xem tooltip swallowSpeedMargin.
                // Dung it.Speed chu khong dung bien 'speed' tinh o duoi: 'speed' la toc do RA LENH,
                // con van toc that bam sau no mot nhip qua MoveTowards. Lay so that thi luoi nay
                // khong bao gio nuot som hon can thiet.
                float capture = swallowDistance;
                if (swallowSpeedMargin > 0f)
                    capture = Mathf.Max(capture, it.Speed * Time.fixedDeltaTime * swallowSpeedMargin);

                if (dist <= capture) { Swallow(it); _toRemove.Add(it); continue; }
                float nearness = 1f - Mathf.Clamp01(dist / eff);
                float speed = pullSpeed * Mathf.Lerp(farSpeedFactor, 1f, nearness);

                // Truyen 'capture' chu khong phai swallowDistance: day moi la nguong item THUC SU
                // bien mat o frame nay. Item teo ve 0 dung tai do, khong hut chet mot cuc.
                it.Pull(mp, originPos, coneAngle, speed, pullAccel, capture);
                pulled++;
            }
            else
            {
                it.SetPlayerCollision(_ownCols, false);   // qua cap: van chan duong nguoi choi
                it.Struggle(mp);   // diff == 1: nghieng ve phia mom + lac lu, khong di chuyen
                struggling++;
            }
        }

        // TIENG "KHONG AN NOI": trong non khong con gi hut duoc, ma van co vat dang giang co.
        //
        // Doi CA HAI dieu kien: dang vua hut duoc mon gi do vua co mot toa nha to trong tam thi
        // khong phai luc bao "khong an duoc" - nguoi choi dang an binh thuong.
        //
        // Ham nay chay moi nhip vat ly (50 lan/giay) nen bat buoc phai co cooldown, khong thi mot
        // giay 50 tieng chong len nhau.
        if (pulled == 0 && struggling > 0 && IsPlayerOwned && SoundManager.HasInstance)
            SoundManager.Instance.PlaySfxCooldown(SoundManager.Sfx.AccessDenied, deniedSoundInterval);

        for (int i = 0; i < _toRemove.Count; i++) _active.Remove(_toRemove[i]);
    }

    private void Swallow(PhysicsDevourable it)
    {
        // Diem tren HUD la diem cua NGUOI CHOI. Bo gate nay thi 3 con AI an item cung nhay diem
        // cho nguoi choi - van chua choi gi diem da tu chay.
        if (IsPlayerOwned && UIManager.Instance != null) UIManager.Instance.AddScore(it.scoreValue);

        // TIENG AN ITEM, CAO DAN khi an lien tuc. SoundManager tu dem, tu reset sau khoang lang,
        // va tu chan neu hai mon vao mom qua sat nhau (nuot 4 mon trong mot frame ma keu ca 4 thi
        // bien do cong don vuot tran roi vo tieng).
        //
        // Chi nguoi choi: mot con bot an ca tram mon moi van, 8 con thi thanh tieng ran ri lien tuc.
        if (IsPlayerOwned && SoundManager.HasInstance)
            SoundManager.Instance.PlaySfxRising(SoundManager.Sfx.EatFeed);

        AddXp(it.xpValue);
        it.Devour(mouth);        // item xoay tit + teo lao vao mom
        PlayGulp();              // than nhan vat 'uc' mot cai
        if (onDevour != null) onDevour.Invoke();
    }

    /// <summary>
    /// Cai giat 'uc' squash-stretch khi vua nuot: bop ngang / keo cao.
    ///
    /// Chay tren punchTarget (Graphic) chu khong phai object goc, vi goc dang co tween scale
    /// theo level - hai tween cung ghi mot localScale se da nhau.
    ///
    /// DOKill(true) truoc khi punch moi: an lien tuc thi cai cu bi ket thuc va TRA VE scale
    /// chuan da, neu khong punch chong punch se lam Graphic tro tho lech han khoi 1.
    /// </summary>
    private void PlayGulp()
    {
        if (gulpPunch <= 0f || gulpDuration <= 0f || punchTarget == null) return;

        punchTarget.DOKill(true);
        punchTarget.DOPunchScale(
            new Vector3(-gulpPunch, gulpPunch, -gulpPunch),
            gulpDuration,
            Mathf.Max(1, Mathf.RoundToInt(gulpWobbles * 2f)),
            1f);
    }

    /// <summary>
    /// CU POP khi vuot MOC: NEN lay da -> BUNG vot qua -> LUN ve.
    ///
    /// Chay tren punchTarget giong PlayGulp, khong dung transform goc: goc dang co tween scale
    /// theo level chay song song, hai tween cung ghi mot localScale la da nhau.
    ///
    /// SQUASH-STRETCH chu khong scale deu 3 truc: luc nen thi BEP xuong (thap & rong), luc bung
    /// thi VUON len (cao & hep). Scale deu nhin nhu bom bong; squash-stretch nhin co suc song,
    /// ma cung tung ay dong code.
    ///
    /// HITSTOP + CAMERA chi chay cho NGUOI CHOI: 8 con bot len moc lien tuc, de chung cung lam
    /// khung hinh thi ca van giat khong luc nao yen.
    /// </summary>
    private void PlayStepPop()
    {
        if (!popOnStep || punchTarget == null) return;

        punchTarget.DOKill(true);

        float sq = popSquash, st = popStretch;
        Sequence seq = DOTween.Sequence().SetTarget(punchTarget);

        // 1. NEN: bep xuong, phinh ngang
        seq.Append(punchTarget.DOScale(new Vector3(1f + sq, 1f - sq, 1f + sq), popSquashTime)
                              .SetEase(Ease.OutQuad));

        // 2. BUNG: vuon cao, hep ngang - vot QUA co chuan
        seq.Append(punchTarget.DOScale(new Vector3(1f - st * 0.4f, 1f + st, 1f - st * 0.4f), popStretchTime)
                              .SetEase(Ease.OutQuad));

        // Dinh bung: bat VFX + hitstop
        seq.AppendCallback(() =>
        {
            if (popVfx != null) ShowPopVfx();
            if (IsPlayerOwned && popHitstop > 0f) DoHitstop();
        });

        // 3. LUN ve chuan, nay nhe mot cai cho khoi cung
        seq.Append(punchTarget.DOScale(Vector3.one, popSettleTime).SetEase(Ease.OutBack));

        // Sequence chay theo gio KHONG SCALE: hitstop ha timeScale, de gio thuong thi chinh cai
        // tween nay cung bi lam cham -> cu bung tro nen ie oai dung luc dang can dut khoat
        seq.SetUpdate(true);
    }

    /// <summary>Bat object VFX mot luc roi tat. Dung DOTween timer cho khoi phai coroutine rieng.</summary>
    private void ShowPopVfx()
    {
        popVfx.SetActive(false);   // tat truoc de ParticleSystem playOnAwake chay lai tu dau
        popVfx.SetActive(true);

        DOVirtual.DelayedCall(Mathf.Max(0.05f, popVfxDuration), () =>
        {
            if (popVfx != null) popVfx.SetActive(false);
        }, true).SetTarget(this);
    }

    /// <summary>
    /// Lam cham ca game trong tich tac roi tra lai. Khong dat timeScale = 0 tuyet doi: mot so he
    /// chia cho timeScale, ve 0 la sinh Infinity/NaN.
    /// </summary>
    private void DoHitstop()
    {
        if (_hitstopActive) return;   // moc lien tiep: khong chong hitstop len nhau

        _hitstopActive = true;
        Time.timeScale = popHitstopScale;
        DOVirtual.DelayedCall(popHitstop, () =>
        {
            Time.timeScale = 1f;
            _hitstopActive = false;
        }, true).SetTarget(this);
    }

    private bool _hitstopActive;

    /// <summary>
    /// 1 XP = 1 LEVEL. Khong con duong cong XP, khong con tran: Level = 1 + tong XP da an.
    /// An mon to (xpValue lon) thi nhay nhieu level trong mot mieng.
    /// </summary>
    private void AddXp(int amount)
    {
        int a = Mathf.Max(1, amount);

        if (xpGainMultiplier == 1f) { SetXp(_xp + a); return; }

        // He so co the < 1 (bot dang vuot moc, bi ham lai) nen phai giu phan le - lam tron moi
        // mieng an se thanh "he so 0.4 va 0.9 deu = 1 XP", ca bang can bang mat tac dung.
        _gainRemainder += a * Mathf.Max(0f, xpGainMultiplier);
        int whole = Mathf.FloorToInt(_gainRemainder);
        if (whole <= 0) return;

        _gainRemainder -= whole;
        SetXp(_xp + whole);
    }

    /// <summary>
    /// AN XP RUT DUOC tu con khac. Cua duy nhat de cong XP tu ben ngoai.
    ///
    /// Di qua dung AddXp nhu luc nuot item, chu khong ghi thang vao _xp: co vay moi con dinh
    /// xpGainMultiplier - GameManager.BalanceAiLevels ghim level bot bang he so do, bo qua no la
    /// bot an cua nhau se vot len khong ai ham lai duoc.
    /// </summary>
    public void GainXp(int amount)
    {
        if (amount <= 0) return;
        AddXp(amount);
    }

    /// <summary>
    /// MAT XP - bi con khac hut. Tra ve so XP THUC SU mat duoc (da bi san Lv1 chan lai), de ben
    /// goi biet nan nhan tut dung bao nhieu: ke hut an dung bang so do, khong sinh XP tu khong.
    /// </summary>
    public int LoseXp(int amount)
    {
        if (amount <= 0) return 0;
        int before = _xp;
        SetXp(_xp - amount);
        return before - _xp;
    }

    /// <summary>
    /// RUT THEO NHIP - goi moi FixedUpdate khi dang bi hut. 'amount' la QUY THOI GIAN
    /// (deltaTime da nhan he so gan/xa), KHONG phai luong XP.
    ///
    /// Du mot nhip thi mat DUNG 1 LEVEL va sinh DUNG 1 object VFX - dem object bay la biet chinh
    /// xac bao nhieu level dang chuyen chu.
    ///
    /// NHIP SIET DAN: moi lan rut xong, nhip nhan voi drainIntervalDecay -> nam trong vung cang
    /// lau, level tut cang nhanh. Ep xuong san drainIntervalFloor de nhip khong ve 0.
    ///
    /// Tra ve so level thuc su mat trong lan goi nay (0 hoac 1).
    /// </summary>
    public int DrainXp(float amount)
    {
        if (amount <= 0f) return 0;

        _drainClock += amount;                     // 'amount' la QUY THOI GIAN da nhan prox
        if (_drainClock < CurrentDrainInterval) return 0;

        _drainClock = 0f;                          // moi nhip bat dau dem lai tu 0

        int lost = LoseXp(1);                      // DUNG 1 LEVEL mot nhip - 1 level = 1 object bay
        if (lost <= 0) return 0;                   // cham san Lv1: khong rut, khong siet nhip

        // SIET NHIP: o trong vung cang lau, moi nhip lai ngan di, level tut nhanh dan.
        // Ep xuong san de nhip khong ve 0 (ve 0 la rut vo han moi frame).
        _drainInterval = Mathf.Max(FloorInterval, _drainInterval * drainIntervalDecay);
        return lost;
    }

    /// <summary>
    /// VUA DINH VAO NON: nap day dong ho de nhip DAU TIEN no ngay lap tuc, khong phai cho.
    ///
    /// Khong co buoc nay thi dong ho dem tu 0, ma o ria non no chay voi he so prox chi 0.35 -
    /// nguoi choi cham duoc nan nhan roi phai cho toi ~2.9 giay moi thay object dau tien bay ra,
    /// khong biet minh da an duoc hay chua.
    ///
    /// Khong so bi lam dung bang cach ra-vao lien tuc: Creature chi goi ham nay khi IsBeingDrained
    /// dang FALSE, ma co do co san do tre drainMemory (0.5s). Muon nap lai phai roi han nua giay,
    /// tuc cham hon la cu dung yen ma hut.
    /// </summary>
    public void PrimeDrain()
    {
        _drainClock = CurrentDrainInterval;
    }

    /// <summary>Nhip rut hien tai (giay/level). Khoi tao bang drainInterval, siet dan khi bi ghi.</summary>
    private float CurrentDrainInterval
    {
        get
        {
            if (_drainInterval <= 0f) _drainInterval = Mathf.Max(0.01f, drainInterval);
            return _drainInterval;
        }
    }

    private float FloorInterval
    {
        get { return Mathf.Max(0.01f, drainInterval * drainIntervalFloor); }
    }

    /// <summary>
    /// NGUOI DAN khi khong con bi hut: nhip bo ve lai gia tri goc.
    ///
    /// Nguoi dan chu KHONG reset ngay. Reset ngay thi nguoi choi chi can lach ra khoi non nua
    /// giay la xoa sach toan bo do ghi da tich - tran danh khong bao gio don duoc ap luc, va
    /// ke hut vinh vien khong ket thuc duoc mot pha nao.
    /// </summary>
    public void CoolDrain(float dt)
    {
        float baseInterval = Mathf.Max(0.01f, drainInterval);
        if (_drainInterval >= baseInterval) { _drainClock = 0f; return; }

        _drainInterval = Mathf.Min(baseInterval, _drainInterval + drainIntervalRecover * dt);
    }

    /// <summary>Dat thang cap (cheat/test). XP dong bo theo cho dung Level = 1 + Xp.</summary>
    public void SetLevel(int value)
    {
        SetXp(Mathf.Max(1, value) - 1);
    }

    /// <summary>
    /// CUA DUY NHAT de doi tong XP. Moi duong (an item / bi hut / cheat) deu chui qua day nen
    /// khong the co chuyen mot duong nao do quen cap nhat scale - camera - hinh dang.
    ///
    /// Chieu XUONG khong can code rieng: RecalcScaleFactor, CameraLevelZoom.ApplyForLevel va
    /// PlayerVisual.SetForm deu tinh theo level tuyet doi chu khong cong don, nen tra level ve
    /// nho la ca ba tu lui theo - ke ca TUT TIEN HOA (SetForm nhan index nho hon thi tat lai
    /// nhung object da bat va tra material ve dang truoc).
    /// </summary>
    private void SetXp(int value)
    {
        int newXp = Mathf.Max(0, value);
        if (newXp == _xp) return;

        bool up = newXp > _xp;
        _xp = newXp;
        level = 1 + _xp;

        ApplyProgression(false);

        if (up)
        {
            if (onLevelUp != null) onLevelUp.Invoke();
        }
        else if (onLevelDown != null)
        {
            onLevelDown.Invoke();
        }
    }

    [ContextMenu("TEST - Level +10")]
    private void DebugLevelUp() { SetLevel(level + 10); }

    [ContextMenu("TEST - Level -10")]
    private void DebugLevelDown() { SetLevel(level - 10); }

    /// <summary>
    /// Toc do di chuyen bam theo he so scale (da bi MaxScale chan) - than dung to thi toc
    /// cung dung tang. Khong co bang moc rieng: scale da chua san creep + cu nhay o moc.
    /// </summary>
    private void ApplySpeed()
    {
        if (_movement == null) return;
        _movement.SetSpeedMultiplier(1f + (_scaleFactor - 1f) * SpeedFollowScale);
    }

    /// <summary>
    /// MOT HAM DUY NHAT chay moi khi level doi - an item goi vao day. Khong co Update nao
    /// poll lai nhung thu duoi day.
    ///   1. tinh lai he so scale
    ///   2. speed  = base x (1 + (he so scale - 1) x SpeedFollowScale)
    ///   3. scale  -> tween transform
    ///   4. camera -> day level (da dong bang khi scale cham tran) sang CameraLevelZoom
    /// 'instant' = bo tween, dat thang (luc Awake / trong Edit mode).
    /// </summary>
    private void ApplyProgression(bool instant)
    {
        int stageBefore = _stage;
        int evoBefore = _evolutionCount;
        int scaleStepsBefore = _scaleStepHits;
        float scaleBefore = _scaleFactor;

        RecalcScaleFactor();
        ApplySpeed();
        ApplyScaleTween(instant);

        // VUOT MOC = stage tang. Khong tinh luc 'instant' (Awake / Edit mode) va khong tinh khi TUT
        // mot moc - bung ra trong luc dang bi an mat level la nguoc nghia hoan toan.
        bool steppedUp = !instant && _stage > stageBefore;

        // TIEN HOA = moc vua vuot co isEvolution. Tap con cua steppedUp, tach ra de camera co the
        // chi nay o nhung lan dang gia thay vi moi moc.
        bool evolved = !instant && _evolutionCount > evoBefore;

        // POP CHI NO KHI THAN THAT SU TO RA - hai dieu kien, thieu mot la khong no:
        //   1. vua vuot mot moc CO 'add'  (moc chi dong toi camera/tien hoa thi than dung yen)
        //   2. va _scaleFactor thuc su tang (moc co 'add' nhung dang bi maxScale chan thi cung
        //      chang to them duoc mm nao - bung ra luc do la noi doi)
        bool scaleStepped = !instant
                         && _scaleStepHits > scaleStepsBefore
                         && _scaleFactor > scaleBefore + 0.0001f;

        // popAffectsCamera tat = bao camera "khong co moc nao ca" -> no chi doi size thang,
        // khong chay nhip nen-bung. Zoom van dung, chi mat phan hieu ung.
        bool camStep = steppedUp && popAffectsCamera;
        bool camEvo = evolved && popAffectsCamera;

        if (cameraZoom != null && IsPlayerOwned)
            cameraZoom.ApplyForLevel(ZoomLevel, instant, camStep, camEvo);

        if (scaleStepped) PlayStepPop();

        // TIEN HOA: ban VFX + tieng gam. 'evolved' la moc co isEvolution, da loai san hai ca khong
        // duoc keu - luc tut nguoc mot moc, va luc dat level o Awake / Edit mode.
        //
        // Chi nguoi choi: level bot bam theo level nguoi choi (xem GameManager.BalanceAiLevels) nen
        // ca 8 con deu tien hoa quanh cung mot luc - 8 tieng gam chong nhau va 8 cot anh sang moc
        // len khap map cho mot khoanh khac le ra chi cua nguoi choi.
        if (evolved && IsPlayerOwned)
        {
            if (evolveVfx != null) evolveVfx.Play(true);   // true = keo theo ca he hat con
            if (SoundManager.HasInstance) SoundManager.Instance.PlaySfx(SoundManager.Sfx.Evolve);
        }

        // TIENG UPGRADE: vua vuot mot moc trong LevelSteps. Dung 'steppedUp' co san chu khong tu do
        // level: bien do da loai san hai ca khong duoc keu - luc tut mot moc, va luc dat level o
        // Awake / Edit mode.
        //
        // Chi nguoi choi: bot len moc lien tuc ca van, va moc cua chung khong phai thanh tuu cua ai.
        if (steppedUp && IsPlayerOwned && SoundManager.HasInstance)
            SoundManager.Instance.PlaySfx(SoundManager.Sfx.Upgrade);
        if (playerVisual != null) playerVisual.SetForm(_evolutionCount);   // SetForm tu bo qua neu khong doi
    }

    private void ApplyScaleTween(bool instant)
    {
        Vector3 target = _baseScale * _scaleFactor;

        if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Kill();
        _scaleTween = null;

        if (instant || scaleTweenDuration <= 0f || !Application.isPlaying)
        {
            transform.localScale = target;
            return;
        }

        // To ra va teo lai dung hai kieu easing khac nhau - xem tooltip cua shrinkTweenEase
        bool shrinking = target.sqrMagnitude < transform.localScale.sqrMagnitude;
        _scaleTween = transform.DOScale(target, scaleTweenDuration)
            .SetEase(shrinking ? shrinkTweenEase : scaleTweenEase);
    }
    /// <summary>
    /// Tinh lai he so scale theo level. CHI goi khi level doi (khong goi moi frame): duyet
    /// het danh sach moc chu khong duyet tung level, nen bao nhieu level cung la O(so moc).
    ///
    ///   he so = 1
    ///         + ScalePerLevel x (so level da len, TRU cac level trung moc)
    ///         + tong 'add' cua moi moc ma level da qua
    ///
    /// Level trung moc thi khong cong ScalePerLevel nua - chi an 'add' cua moc do.
    /// </summary>
    private void RecalcScaleFactor()
    {
        int levelsGained = Mathf.Max(0, level - 1);

        float stepAdd = 0f;
        int stepHits = 0;
        int evo = 0;
        if (LevelSteps != null)
        {
            for (int i = 0; i < LevelSteps.Count; i++)
            {
                LevelStep s = LevelSteps[i];
                if (s == null || s.level < 2 || s.level > level) continue;

                if (s.isEvolution) evo++;         // dem TRUOC khi loc theo add, moc tien hoa co the khong lam to them

                if (s.add == 0f) continue;        // moc chi dong toi camera -> khong tinh la moc cua scale
                stepAdd += s.add;
                stepHits++;
            }
        }
        _evolutionCount = evo;
        _scaleStepHits = stepHits;   // so moc CO LAM TO THAN da qua - de biet luc nao dang cu pop

        _stage = StageAtLevel(level);

        int normalLevels = Mathf.Max(0, levelsGained - stepHits);
        float raw = 1f + ScalePerLevel * normalLevels + stepAdd;

        // MaxScale chi con chan CO THAN, khong con dong bang zoom camera nua (xem ghi chu o ZoomLevel)
        _scaleFactor = (MaxScale > 0f && raw >= MaxScale) ? MaxScale : raw;
    }

    void OnDrawGizmosSelected()
    {
        Transform m = mouth != null ? mouth : transform;
        float eff = CurrentRange;   // dung chung nguon voi gameplay, khong chep lai cong thuc
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
