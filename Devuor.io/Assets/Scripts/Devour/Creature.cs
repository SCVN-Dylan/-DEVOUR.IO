using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DANH TINH chung cho moi sinh vat trong van - nguoi choi va ca 3 con AI deu gan component nay.
///
/// Component nay chi lo phan DANH TINH cua mot con (la ai, tam than o dau, cap may). Con
/// "trong van dang co nhung ai" thi hoi GameManager - Creature tu dang ky vao do luc OnEnable
/// va tu rut ra luc OnDisable, khong ai phai di quet scene tim nhau.
/// </summary>
[RequireComponent(typeof(SimpleSuction))]
[DisallowMultipleComponent]
public class Creature : MonoBehaviour
{
    [Tooltip("Con nay do NGUOI CHOI dieu khien. Trong scene chi bat DUNG MOT con.\n" +
             "Dung de quyet dinh: ai duoc cong diem len HUD, camera bam ai, ai thua thi Game Over.")]
    public bool isPlayer;

    [Tooltip("Ten hien thi (dung chung voi PlayerNameTag khi can). Khong anh huong logic")]
    public string displayName = "Player";

    [Tooltip("CHI DUNG CHO AI - 'tinh cach' cua con bot nay: no nham toi level bang\n" +
             "level_nguoi_choi x (1 + bias).\n" +
             "  -0.25 = con nay chiu yeu hon nguoi choi 25%\n" +
             "  +0.20 = con nay manh hon nguoi choi 20%\n" +
             "GameManager boc so nay luc sinh bot, trai deu trong khoang cau hinh de luon co\n" +
             "ca con yeu lan con manh. Nguoi choi khong dung toi truong nay.")]
    public float levelBias;

    [Tooltip("TAM THAN (toa do local) - diem ma combat sau nay ngam vao khi hut/keo con khac.\n\n" +
             "KHONG lay tam bang bounds cua renderer nhu ben item: model nhan vat co animation,\n" +
             "bounds phinh/co theo tung frame nen tam se rung. Offset co dinh thi on dinh, va\n" +
             "TransformPoint tu nhan theo scale nen len cap la tam tu dang cao theo than.")]
    public Vector3 centerOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Tham chieu (keo vao Inspector)")]
    [Tooltip("Vung hut cua chinh con nay. Reset/them component la tu dien san, keo tay de doi")]
    [SerializeField] private SimpleSuction _suction;

    [Tooltip("Bo di chuyen cua chinh con nay")]
    [SerializeField] private RbMovement _movement;

    [Tooltip("He hat 'hut' cua chinh con nay - ban khi CON NAY di hut con khac.\n" +
             "De trong = tu tim DevourVfx trong cac object con")]
    [SerializeField] private DevourVfx _vfx;

    public SimpleSuction Suction { get { return _suction; } }
    public RbMovement Movement { get { return _movement; } }

    /// <summary>He hat cua con nay. Ke hut goi vao day de rac hat tu than nan nhan ve mom minh.</summary>
    public DevourVfx Vfx { get { return _vfx; } }

    /// <summary>Cap do hien tai (lay tu SimpleSuction, khong luu ban sao rieng de khoi lech).</summary>
    public int Level { get { return _suction != null ? _suction.Level : 1; } }

    /// <summary>Tam than trong toa do world, da tinh ca scale khi len cap.</summary>
    public Vector3 Center { get { return transform.TransformPoint(centerOffset); } }

    /// <summary>Con vua hut minh gan day nhat. null = chua bi ai hut.</summary>
    public Creature LastAttacker { get { return _lastAttacker; } }

    /// <summary>Tong XP da bi rut trong lan bi hut NAY (reset khi thoat ra duoc mot luc).</summary>
    public int DrainedTotal { get { return _drainedTotal; } }

    /// <summary>
    /// Dang bi hut hay khong. Co do TRE (drainMemory) chu khong phai dung mot frame la tat:
    /// nan nhan chi mat XP nguyen o vai frame le te (phan le duoc tich luy), neu doc dung frame
    /// khong mat gi ma bao "thoat roi" thi co nay se nhap nhay lien tuc.
    /// </summary>
    public bool IsBeingDrained { get { return Time.time - _lastDrainTime <= drainMemory; } }

    [Tooltip("Bao lau khong bi hut them thi moi coi la DA THOAT (giay). Xem IsBeingDrained")]
    public float drainMemory = 0.5f;

    [Header("Danh nhau - cham lai + thanh ghi")]
    [Tooltip("He so toc do khi minh la KE HUT (con MANH hon trong tran).\n" +
             "0.4 = giam 60% toc do. De THAP HON victimSlow: ke hut tra gia cho viec cu ghi mai")]
    [Range(0.05f, 1f)] public float attackerSlow = 0.4f;

    [Tooltip("He so toc do khi minh la NAN NHAN (con YEU hon) VA thanh ghi con > 0.\n" +
             "0.5 = giam 50% toc do. Thanh ve 0 la nhay thang ve 1 - khong ease, de nguoi choi cam\n" +
             "duoc dung khoanh khac gianh lai duoc toc do")]
    [Range(0.05f, 1f)] public float victimSlow = 0.5f;

    [Tooltip("Bao lau (giay) thi thanh ghi tut het tu day xuong 0. Het thanh = nan nhan ve lai\n" +
             "toc do binh thuong va co cua chay thoat")]
    public float struggleTime = 2.5f;

    [Tooltip("Thoat khoi combat roi phai cho bao lau (giay) thanh moi BAT DAU hoi.\n\n" +
             "Phai TACH khoi drainMemory (0.5s): dung chung thi nan nhan chi can lach ra khoi non\n" +
             "nua giay la duoc cap lai nguyen 2.5s ghi moi - lach ra lach vao vo han")]
    public float struggleRefillDelay = 1f;

    [Tooltip("Hoi day thanh mat bao lau (giay)")]
    public float struggleRefillTime = 3f;

    // KHONG CON LUC KEO. Ban truoc nan nhan bi keo ve phia mom (creaturePullSpeed), va do chinh
    // la nguon goc cua canh "dung yen": luc keo 2.5 xap xi bang toc do chay 2.13-2.9 nen hai ben
    // triet tieu nhau, nhin ra thanh nhan vat dung im giua khong trung. Chinh cho no thang thi
    // thanh nan nhan luon di ra xa (hut ma khong lai gan). Bo han di thi khong con bai toan can
    // luc nao ca - chi con MOT thu duy nhat lam cham: he so toc do.

    [Header("Bi nuot vao mom")]
    [Range(0.05f, 0.99f)]
    [Tooltip("HET THANH GHI thi xet: level minh duoi bao nhieu PHAN TRAM level KE HUT thi bi nuot.\n" +
             "0.5 = yeu hon nua level ke hut la bi hut thang vao mom nhu mot mon do an.\n\n" +
             "Con >= muc nay thi het thanh la duoc THA, tra ve 100% toc do va chay thoat. Thanh ghi\n" +
             "vi the la 'cua so chung minh minh khong phai do an' - het gio ma van qua yeu thi bi an.\n\n" +
             "Van bi rut XP tiep sau khi duoc tha, nen tut dan xuong duoi muc nay thi van bi nuot -\n" +
             "phep kiem tra chay lien tuc chu khong chi dung mot lan luc thanh vua can.")]
    public float devourLevelRatio = 0.5f;

    [Tooltip("Thoi gian xac bay xoay + teo lao vao mom (giay). Dung kieu voi luc nuot item")]
    public float devouredDuration = 0.35f;

    [Tooltip("Toc do xoay tit khi bi hut vao mom (do/giay)")]
    public float devouredSpin = 900f;

    [Tooltip("BAT: cham than nhau la con thap level hon CHET NGAY. TAT (mac dinh): cham nhau chi\n" +
             "day nhau ra, MUON AN THI PHAI HUT.\n\n" +
             "Da tat vi no an mat ca pha hut: tam hut o Lv1-20 chi 1.5-2.9u, xap xi chieu dai than,\n" +
             "nen hai con gan nhu luon cham nhau TRUOC khi kip hut cai gi. Nguoi choi chi thay doi\n" +
             "phuong bien mat dot ngot - toan bo phan giang co, thanh ghi, hat bay deu khong bao gio\n" +
             "duoc nhin thay.")]
    public bool eatOnBodyContact = false;

    [Range(0f, 2f)]
    [Tooltip("Chi dung khi eatOnBodyContact BAT: phai hon bao nhieu phan tram level moi nuot duoc.\n" +
             "0 = chi can cao hon mot chut la nuot (Lv101 nuot Lv100)")]
    public float contactEatMargin = 0f;

    [Tooltip("Ban khi con nay bi nuot. Cam VFX/SFX vao day")]
    public UnityEvent onDied;

    /// <summary>Da bi nuot chua (chong xu ly chet hai lan trong cung mot frame).</summary>
    public bool IsDead { get { return _dead; } }

    private Creature _lastAttacker;
    private float _lastDrainTime = -999f;
    private int _drainedTotal;
    private bool _dead;
    private Creature _rival;              // doi thu MANH NHAT cua tran DANG dien ra
    private int _rivalLevel;
    private float _lastCombatTime = -999f;
    private float _struggle = 1f;
    private float _refillDelayLeft;

    /// <summary>
    /// Dang dinh mot tran nao do khong - KE CA khi minh la ben di hut. Khac IsBeingDrained (chi
    /// dung khi minh la ben BI hut).
    /// </summary>
    public bool InCombat { get { return Time.time - _lastCombatTime <= drainMemory; } }

    /// <summary>
    /// MINH LA KE HUT hay NAN NHAN - quyet dinh bang LEVEL, khong phai bang "ai nam trong non ai".
    ///
    /// Hai con chia mom vao nhau thi CA HAI deu dang hut nhau (luat cu van giu nguyen, ca hai
    /// cung tut XP). Nhung ve mat CAM GIAC thi phai co ke tren ke duoi: con to nhan vai ke hut
    /// (i, anim cham), con nho nhan vai nan nhan (vung vay, co thanh ghi). Neu de moi con tu thay
    /// minh vua hut vua bi hut thi ca hai cung dung hinh - nhin nhu treo may.
    ///
    /// HOA LEVEL thi ca hai deu la nan nhan: hai con ngang co vat nhau, ai cung co thanh, het
    /// thanh thi cung thoat. Hop ly hon la ca hai cung i o attackerSlow roi ghi nhau vo tan -
    /// va dau van ai cung Lv1-5 nen hoa nhau la chuyen thuong xuyen.
    /// </summary>
    public bool IsAttackerRole { get { return InCombat && Level > _rivalLevel; } }

    /// <summary>Dang o vai NAN NHAN (con yeu hon, hoac hoa level).</summary>
    public bool IsVictimRole { get { return InCombat && Level <= _rivalLevel; } }

    /// <summary>
    /// THANH GHI, 0..1. Day = vua vao tran; ve 0 = het bi ghi, toc do tra ve binh thuong.
    /// Chi tut khi minh dang o vai NAN NHAN.
    /// </summary>
    public float Struggle { get { return _struggle; } }

    /// <summary>
    /// GHI SO mot doi thu trong tran nay. Ca hai ben deu goi: nan nhan goi qua ReceiveDrain,
    /// ke hut goi thang tu SimpleSuction.DrainCreatures.
    ///
    /// Giu level CAO NHAT trong so doi thu: dang bi con Lv500 ghi ma tien the ghi mot con Lv10
    /// thi van phai la nan nhan - con to moi la cai quyet dinh so phan tran nay.
    /// </summary>
    public void NoteCombat(Creature other)
    {
        if (other == null || other == this || other.IsDead) return;

        if (!InCombat) { _rivalLevel = 0; _rival = null; }   // tran truoc da nguoi -> quen doi thu cu
        if (other.Level > _rivalLevel || _rival == null) { _rivalLevel = other.Level; _rival = other; }
        _lastCombatTime = Time.time;
    }

    /// <summary>
    /// BI CON KHAC HUT. Ke hut goi ham nay moi FixedUpdate khi minh nam trong non hut cua no.
    ///
    ///   xpAmount  = luong XP bi rut trong frame nay (da nhan fixedDeltaTime)
    ///
    /// XP CHUYEN THANG sang ke hut. Ban truoc thi khong: XP mat di bien thanh TE BAO vat ly nam
    /// ngoai the gioi, ke hut phai hut lai moi an duoc - co y de con thu ba co cua chen vao cuop.
    /// Da bo cua do (xem DevourVfx): doi lai khong con hang tram Rigidbody moi tran, va so lieu
    /// khop tuyet doi - do thuc te ban cu chi co 28/30 vien toi duoc mom, 2 XP boc hoi.
    ///
    /// KHONG con tham so mom/luc keo: nan nhan khong bi keo di dau ca, chi bi cham lai.
    /// </summary>
    public void ReceiveDrain(Creature attacker, float xpAmount)
    {
        if (attacker == null || attacker == this || _suction == null || _dead) return;

        // Thoat duoc mot luc roi bi hut lai = tran moi, dem lai tu dau
        if (!IsBeingDrained) _drainedTotal = 0;

        _lastAttacker = attacker;
        _lastDrainTime = Time.time;
        NoteCombat(attacker);   // ghi so doi thu -> bo phan vai biet minh la ke hut hay nan nhan

        int lost = _suction.DrainXp(xpAmount);
        if (lost > 0)
        {
            _drainedTotal += lost;

            // Minh tut bao nhieu, ke hut an dung bay nhieu - va an NGAY, khong qua trung gian nao.
            // Van di qua GainXp de con dinh xpGainMultiplier: GameManager.BalanceAiLevels ghim
            // level bot bang he so do, bo qua la bot an cua nhau se vot len khong ai ham duoc.
            if (attacker.Suction != null) attacker.Suction.GainXp(lost);

            // Hat bay tu than MINH ve mom KE HUT. He hat nam ben ke hut nen moi hat trong do deu
            // ve cung mot mom - khong phai gan dich cho tung hat.
            if (attacker.Vfx != null) attacker.Vfx.EmitDrain(Center, lost);
        }

        // KHONG con cua chet nao o day. Chet chi xay ra o mot cho duy nhat: TickDevourCheck(),
        // khi thanh ghi da can VA minh qua yeu so voi ke hut. Truoc day co them mot cua nua (tut
        // duoi eatPercent cua moc level dau tran) - hai luat chet song song thi khong bao gio doan
        // duoc con nao se chet luc nao, va do chinh la kieu "sua xong lai ra bug moi".
    }

    /// <summary>
    /// CHAM THAN nhau. MAC DINH KHONG LAM GI - muon an nhau thi phai HUT (xem eatOnBodyContact).
    ///
    /// Khi bat len: con THAP LEVEL hon chet. Moi con tu hoi "minh co phai dua thap hon khong" roi
    /// tu chet - doi xung nen khong phu thuoc con nao nhan va cham truoc, va khong bao gio xay ra
    /// canh ca hai cung chet.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (_dead || !eatOnBodyContact) return;

        // Loc re truoc: dat va nha khong co Rigidbody. Item/te bao co Rigidbody nhung khong co
        // Creature -> chi ton dung mot GetComponent, khong phai GetComponentInParent.
        Rigidbody rb = collision.rigidbody;
        if (rb == null) return;

        Creature other = rb.GetComponent<Creature>();
        if (other == null || other == this || other.IsDead) return;

        if (other.Level > Level * (1f + contactEatMargin)) Die(other);
    }

    /// <summary>
    /// BI NUOT. TOAN BO XP con lai ve thang ke giet, kem mot phat hat that da.
    ///
    /// Ban cu no thanh 12 vien te bao vat ly de con thu ba con cua cuop. Da bo cua do cung luc voi
    /// he te bao (xem DevourVfx) - doi lai khoanh khac chet khong con de ra mot lot Rigidbody, va
    /// ke giet an dung so chu khong phu thuoc no nhat lai duoc may vien.
    ///
    /// killer co the null (chet khong do ai): luc do XP bien mat cung nan nhan, khong ai duoc gi.
    /// </summary>
    public void Die(Creature killer) { Die(killer, false); }

    /// <summary>
    /// swallowIntoMouth = xac bay xoay tit + teo lao vao mom ke giet roi moi bien mat, dung kieu
    /// voi luc nuot mot mon item. Tat thi bien mat ngay tai cho.
    /// </summary>
    public void Die(Creature killer, bool swallowIntoMouth)
    {
        if (_dead) return;
        _dead = true;

        // XP + hat trao NGAY, khong doi animation xong: neu doi thi tween bi ngat giua chung
        // (ke giet chet theo, doi scene) la XP boc hoi mat.
        int remain = _suction != null ? _suction.Xp : 0;
        bool killerAlive = killer != null && !killer.IsDead;
        if (killerAlive)
        {
            if (remain > 0 && killer.Suction != null) killer.Suction.GainXp(remain);
            if (killer.Vfx != null) killer.Vfx.EmitDeath(Center);
        }

        if (onDied != null) onDied.Invoke();

        if (swallowIntoMouth && killerAlive && devouredDuration > 0f && isActiveAndEnabled)
        {
            PlaySwallowedInto(killer);   // ReportDeath se duoc goi khi bay xong
            return;
        }

        if (GameManager.HasInstance) GameManager.Instance.ReportDeath(this, killer);
        else Destroy(gameObject);
    }

    /// <summary>
    /// Xac bay vao mom ke giet - sao chep dung cach item lam (PhysicsDevourable.PlaySwallow):
    /// kinematic, tat collider, tween vi tri + teo + xoay tit, xong thi bien mat.
    ///
    /// BAM THEO mom moi frame chu khong ban toi mot diem co dinh: ke giet dang chay thi mom di
    /// chuyen, ban toi diem cu se thay xac lao tret ra sau lung no.
    ///
    /// RUT TEN khoi GameManager NGAY tu dau chu khong doi bay xong: trong lue bay no khong con la
    /// muc tieu hop le nua, de lai trong danh sach thi cac con khac van ngam vao no de di/tron, va
    /// SimpleSuction cua chung van rut XP mot cai xac.
    /// </summary>
    private void PlaySwallowedInto(Creature killer)
    {
        if (GameManager.HasInstance) GameManager.Instance.Unregister(this);

        // Cat moi duong con nay con tu tac dong ra ben ngoai
        if (_movement != null) { _movement.CombatSpeedMultiplier = 1f; _movement.enabled = false; }
        if (_suction != null) _suction.enabled = false;   // dang bi nuot ma van di rut XP con khac thi vo ly
        AIController ai = GetComponent<AIController>();
        if (ai != null) ai.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }

        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].enabled = false;   // khong xo day gi tren duong bay

        Transform mouth = killer.Suction != null && killer.Suction.mouth != null
            ? killer.Suction.mouth : killer.transform;

        Transform tf = transform;
        Vector3 startPos = tf.position;
        Vector3 startScale = tf.localScale;

        tf.DOKill();
        DOTween.To(() => 0f, k =>
        {
            if (tf == null) return;
            Vector3 tp = mouth != null ? mouth.position : startPos;
            tf.position = Vector3.Lerp(startPos, tp, k);
            tf.localScale = startScale * (1f - k);
            tf.Rotate(Vector3.up, devouredSpin * Time.deltaTime, Space.Self);
        }, 1f, devouredDuration)
            .SetEase(Ease.InQuad)
            .SetTarget(tf)
            .OnComplete(() =>
            {
                if (this == null) return;
                if (GameManager.HasInstance) GameManager.Instance.ReportDeath(this, killer);
                else Destroy(gameObject);
            });
    }

    void OnDestroy()
    {
        transform.DOKill();   // tween con song ma target da chet thi DOTween nem loi
    }

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake()
    {
        // Ref da keo san tren prefab thi khong dong toi. AutoFill chi la LUOI AN TOAN cho
        // truong hop quen keo (prefab cu, object dung tay trong scene test).
        if (_suction == null || _movement == null || _vfx == null) AutoFill();
    }

    private void AutoFill()
    {
        if (_suction == null) _suction = GetComponent<SimpleSuction>();
        if (_movement == null) _movement = GetComponent<RbMovement>();
        if (_vfx == null) _vfx = GetComponentInChildren<DevourVfx>(true);
    }

    void Update()
    {
        if (_dead) return;   // dang bay vao mom roi, dung tinh toc do/thanh nua

        TickStruggle();
        ApplyCombatSpeed();
        TickDevourCheck();
    }

    /// <summary>
    /// HET THANH GHI thi xet: qua yeu so voi ke hut thi bi hut thang vao mom.
    ///
    /// Chay MOI FRAME chu khong chi mot lan tai khoanh khac thanh vua can: duoc tha roi minh van
    /// bi rut XP tiep, tut dan xuong duoi nguong luc nao la bi an luc do. Neu chi xet mot lan thi
    /// con nao qua duoc dung giay do se mien nhiem vinh vien trong ca tran.
    /// </summary>
    private void TickDevourCheck()
    {
        if (_struggle > 0f) return;              // con thanh = con cua chung minh minh khong phai do an
        if (!IsVictimRole || _rival == null || _rival.IsDead) return;
        if (Level >= _rival.Level * devourLevelRatio) return;   // van du suc, duoc tha cho chay

        Die(_rival, true);
    }

    /// <summary>
    /// THANH GHI tut / hoi.
    ///
    /// Chi tut khi minh dang o vai NAN NHAN. Ke hut khong co thanh: no bi cham suot tran chu
    /// khong co dong ho dem nguoc nao cuu no ca - do chinh la cai gia cua viec cu ghi mai.
    /// </summary>
    private void TickStruggle()
    {
        float dt = Time.deltaTime;

        if (IsVictimRole)
        {
            if (struggleTime > 0.001f) _struggle -= dt / struggleTime;
            else _struggle = 0f;
            _refillDelayLeft = struggleRefillDelay;   // con trong tran thi dong ho hoi cu bi day lui
        }
        else
        {
            if (_refillDelayLeft > 0f) _refillDelayLeft -= dt;
            else if (_struggle < 1f)
            {
                if (struggleRefillTime > 0.001f) _struggle += dt / struggleRefillTime;
                else _struggle = 1f;
            }
        }

        _struggle = Mathf.Clamp01(_struggle);
    }

    /// <summary>
    /// Ap he so toc do theo VAI - bac thang cung, khong ease.
    ///
    ///   ke hut                     -> attackerSlow, suot tran
    ///   nan nhan CON thanh ghi     -> victimSlow
    ///   nan nhan HET thanh / ngoai tran -> 1 (nhay thang, de cam duoc dung luc gianh lai toc do)
    /// </summary>
    private void ApplyCombatSpeed()
    {
        if (_movement == null) return;

        float m = 1f;
        if (InCombat)
        {
            if (Level > _rivalLevel) m = attackerSlow;
            else if (_struggle > 0f) m = victimSlow;
        }

        _movement.CombatSpeedMultiplier = m;
    }

    void OnEnable()
    {
        if (Application.isPlaying && GameManager.Instance != null) GameManager.Instance.Register(this);
    }

    void OnDisable()
    {
        // Doc thang _instance qua Instance: luc thoat Play mode GameManager co the bi huy truoc,
        // getter se di tao mot GameManager moi chi de rut ten ra khoi no - vo ich.
        if (Application.isPlaying && GameManager.HasInstance) GameManager.Instance.Unregister(this);
    }
}
