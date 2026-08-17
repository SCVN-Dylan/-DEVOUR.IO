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

    [Header("Bi nuot")]
    [Range(0.01f, 0.99f)]
    [Tooltip("Tut xuong duoi bao nhieu PHAN TRAM so voi level luc BAT DAU bi hut thi bi nuot.\n" +
             "0.2 = vao tran o Lv100, con duoi Lv20 la bi nuot.")]
    public float eatPercent = 0.2f;

    [Tooltip("Thoat khoi moi vung hut bao lau (giay) thi MOC LEVEL duoc dat lai - coi nhu tran moi.\n\n" +
             "Tach rieng khoi drainMemory (chi 0.5s, dung cho animation/co trang thai): neu dung\n" +
             "chung thi chi can lach ra khoi non nua giay la 'thanh mau' day lai, danh nhau vo nghia.")]
    public float anchorResetDelay = 3f;

    [Range(0f, 2f)]
    [Tooltip("CHAM THAN nhau: phai hon bao nhieu phan tram level moi nuot duoc.\n" +
             "0 = chi can cao hon mot chut la nuot (Lv101 nuot Lv100).\n" +
             "0.1 = phai hon 10% moi nuot, hai con xap xi thi hue nhau.")]
    public float contactEatMargin = 0f;

    [Tooltip("Ban khi con nay bi nuot. Cam VFX/SFX vao day")]
    public UnityEvent onDied;

    /// <summary>Da bi nuot chua (chong xu ly chet hai lan trong cung mot frame).</summary>
    public bool IsDead { get { return _dead; } }

    /// <summary>Level luc BAT DAU bi hut - moc de tinh nguong bi nuot. 0 = khong o trong tran nao.</summary>
    public int AnchorLevel { get { return _anchorLevel; } }

    /// <summary>Duoi muc nay la bi nuot. 0 = khong o trong tran nao.</summary>
    public int EatThreshold
    {
        get
        {
            if (_anchorLevel <= 0) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(_anchorLevel * eatPercent));
        }
    }

    private Creature _lastAttacker;
    private float _lastDrainTime = -999f;
    private int _drainedTotal;
    private int _anchorLevel;
    private bool _dead;

    /// <summary>
    /// BI CON KHAC HUT. Ke hut goi ham nay moi FixedUpdate khi minh nam trong non hut cua no.
    ///
    ///   xpAmount  = luong XP bi rut trong frame nay (da nhan fixedDeltaTime)
    ///   mouthPos  = mom ke hut, de biet keo ve huong nao
    ///   pullSpeed = van toc keo, CONG THEM vao van toc di chuyen chu khong thay the
    ///
    /// XP CHUYEN THANG sang ke hut. Ban truoc thi khong: XP mat di bien thanh TE BAO vat ly nam
    /// ngoai the gioi, ke hut phai hut lai moi an duoc - co y de con thu ba co cua chen vao cuop.
    /// Da bo cua do (xem DevourVfx): doi lai khong con hang tram Rigidbody moi tran, va so lieu
    /// khop tuyet doi - do thuc te ban cu chi co 28/30 vien toi duoc mom, 2 XP boc hoi.
    /// </summary>
    public void ReceiveDrain(Creature attacker, float xpAmount, Vector3 mouthPos, float pullSpeed)
    {
        if (attacker == null || attacker == this || _suction == null || _dead) return;

        // Thoat duoc mot luc roi bi hut lai = tran moi, dem lai tu dau
        if (!IsBeingDrained) _drainedTotal = 0;

        // MOC LEVEL cua tran nay - phai chot TRUOC khi tru XP dau tien, va phai so voi
        // _lastDrainTime CU (truoc khi ghi de o duoi)
        if (_anchorLevel <= 0 || Time.time - _lastDrainTime > anchorResetDelay) _anchorLevel = Level;
        else if (Level > _anchorLevel) _anchorLevel = Level;   // an te bao len lai giua tran thi moc dang theo

        _lastAttacker = attacker;
        _lastDrainTime = Time.time;

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

        if (_movement != null && pullSpeed > 0f)
        {
            Vector3 dir = mouthPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) _movement.AddExternalVelocity(dir.normalized * pullSpeed);
        }

        if (lost > 0 && EatThreshold > 0 && Level <= EatThreshold) Die(attacker);
    }

    /// <summary>
    /// CHAM THAN nhau: con THAP LEVEL hon chet.
    ///
    /// Moi con tu hoi "minh co phai dua thap hon khong" roi tu chet - doi xung nen khong phu
    /// thuoc con nao nhan va cham truoc, va khong bao gio xay ra canh ca hai cung chet.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (_dead) return;

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
    public void Die(Creature killer)
    {
        if (_dead) return;
        _dead = true;

        int remain = _suction != null ? _suction.Xp : 0;
        if (killer != null && !killer.IsDead)
        {
            if (remain > 0 && killer.Suction != null) killer.Suction.GainXp(remain);
            if (killer.Vfx != null) killer.Vfx.EmitDeath(Center);
        }

        if (onDied != null) onDied.Invoke();

        if (GameManager.HasInstance) GameManager.Instance.ReportDeath(this, killer);
        else Destroy(gameObject);
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

    // KHONG CON Update: hang doi te bao da bo, XP di thang sang ke hut ngay trong ReceiveDrain.
    // Bon con nhan mot Update rong moi frame chi de kiem tra mot bien luon bang 0 la lang phi.

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
