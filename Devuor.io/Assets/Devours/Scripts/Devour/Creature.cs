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

    [Tooltip("MAU HAT VFX bay ra khi con NAY bi hut - hat mang mau cua nan nhan, khong phai\n" +
             "cua ke hut, nen nhin la biet dang an thang nao.\n\n" +
             "BOT: GameManager ghi de theo skin boc duoc luc sinh.\n" +
             "PLAYER: khong ai ghi de, nen dat san o day cho khop material tren prefab.")]
    public Color skinColor = Color.white;

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

    [Tooltip("He so toc do khi minh la NAN NHAN (con YEU hon) VA dang nam trong non hut.\n" +
             "0.5 = giam 50% toc do. Ra khoi non la nhay thang ve 1 - khong ease, de nguoi choi cam\n" +
             "duoc dung khoanh khac gianh lai duoc toc do")]
    [Range(0.05f, 1f)] public float victimSlow = 0.5f;

    [Header("But toc - cua so vung ra")]
    [Range(0.05f, 0.99f)]
    [Tooltip("Thanh ghi VUA CAT XUONG DUOI muc nay thi nan nhan BUT TOC mot cu.\n" +
             "0.6 = tut qua 60% la but.\n\n" +
             "Bat theo kieu 'vua cat xuong duoi' chu khong phai 'bang dung 60%': thanh tut theo\n" +
             "NAC (moi nhip rut mot nac), tran nho chi co 3 nac (100% -> 67% -> 33% -> 0) nen no\n" +
             "nhay qua moc 60% ma khong bao gio dung o do.")]
    public float burstThreshold = 0.6f;

    [Range(1f, 2f)]
    [Tooltip("He so toc do trong luc BUT. 1.2 = nhanh hon binh thuong 20%.\n\n" +
             "Tran cua kenh nay la RbMovement.MaxCombatMultiplier (2). Dat cao hon la bi kep im.")]
    public float burstSpeed = 1.2f;

    [Tooltip("But toc keo dai bao lau (giay). Het gio thi ve 100% - KHONG ve lai victimSlow:\n" +
             "but duoc mot cu la coi nhu da vung ra khoi the ghi, con trong non cung khong ghi lai\n" +
             "duoc nua cho toi khi thoat han va bi hut lai tu dau.")]
    public float burstDuration = 1f;

    [Tooltip("SAN THOI GIAN (giay) - CHI dung cho mot ca: dinh vao non luc level DA nam duoi muc\n" +
             "bi nuot san (vd Lv3 lot vao non con Lv100). Luc do thanh khong con nac nao de tut,\n" +
             "dang le bang 0 ngay frame dau va nguoi choi chi thay minh bien mat. San nay bat thanh\n" +
             "tut deu tu day ve 0 trong so giay nay roi moi chet.\n\n" +
             "Cac truong hop con lai thanh chay THUAN theo level, san khong dinh gi toi.\n\n" +
             "0 = tat han, yeu qua la bi nuot ngay lap tuc")]
    public float devourGraceTime = 0.6f;

    // KHONG CON LUC KEO. Ban truoc nan nhan bi keo ve phia mom (creaturePullSpeed), va do chinh
    // la nguon goc cua canh "dung yen": luc keo 2.5 xap xi bang toc do chay 2.13-2.9 nen hai ben
    // triet tieu nhau, nhin ra thanh nhan vat dung im giua khong trung. Chinh cho no thang thi
    // thanh nan nhan luon di ra xa (hut ma khong lai gan). Bo han di thi khong con bai toan can
    // luc nao ca - chi con MOT thu duy nhat lam cham: he so toc do.

    [Header("Bi nuot vao mom")]
    [Range(0.05f, 0.99f)]
    [Tooltip("Level minh duoi bao nhieu PHAN TRAM level KE HUT thi bi nuot.\n" +
             "0.5 = yeu hon nua level ke hut la bi hut thang vao mom nhu mot mon do an.\n\n" +
             "DAY CUNG LA DAY CUA THANH GHI: thanh do dung quang duong tu level luc vua dinh non\n" +
             "xuong toi muc nay, nen thanh can va bi nuot luon xay ra cung mot frame.\n\n" +
             "Muc nay KHOA lai theo level ke hut o khoanh khac vua dinh non va khong doi nua trong\n" +
             "ca pha hut - xem TickStruggle().")]
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
    private int _popStreak;               // so te bao da bi rut trong PHA hut nay - de tieng Pop len dan
    private bool _dead;
    private Creature _rival;              // doi thu MANH NHAT cua tran DANG dien ra
    private int _rivalLevel;
    private float _lastCombatTime = -999f;
    private float _struggle = 1f;
    private int _drainBaseLevel = 1;      // level cua MINH luc vua dinh non - moc DAY cua thanh
    private int _drainFloorLevel;         // muc bi nuot, KHOA luc vua dinh non - moc CAN cua thanh
    private float _drainStartTime = -999f;
    private bool _burstFired;             // pha hut nay da xai cu but toc chua
    private float _burstUntil = -999f;    // but toc con hieu luc toi gio nay

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

    /// <summary>Dang trong 1 giay BUT TOC (vung ra khoi the bi ghi). Dung cho VFX/SFX/anim.</summary>
    public bool IsBursting { get { return Time.time < _burstUntil; } }

    /// <summary>Dang o vai NAN NHAN (con yeu hon, hoac hoa level).</summary>
    public bool IsVictimRole { get { return InCombat && Level <= _rivalLevel; } }

    /// <summary>
    /// THANH GHI, 0..1. Day = vua dinh vao non hut; ve 0 = level da tut xuong duoi muc bi nuot.
    ///
    /// Do bang LEVEL chu khong bang thoi gian - xem TickStruggle(). Chi tut khi minh dang o vai
    /// NAN NHAN va dang thuc su nam trong non.
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
        if (!IsBeingDrained)
        {
            _drainedTotal = 0;
            _popStreak = 0;              // pha moi -> tieng Pop keu lai tu do to/cao thap nhat
            // CHOT CA HAI DAU CUA THANH cho pha nay va khong doi nua: thanh vi the co so nac
            // co dinh (Lv100 dinh mot con Lv120 -> can o 60 -> thanh dung 41 nac), moi nhip rut
            // tut dung mot nac. Moc can KHONG bo len theo ke hut: no len 1 level moi nhip no rut
            // duoc, tinh song thi moc dam vao level minh tu duoi len va thanh cong lai con 27 nac
            // - nhin thanh khong con dem duoc con bao nhieu cua nua.
            _drainBaseLevel = Level;
            _drainFloorLevel = DevourFloorLevel(attacker.Level);
            _drainStartTime = Time.time;   // moc dem san thoi gian (devourGraceTime)
            _struggle = 1f;
            _burstFired = false;           // pha moi = duoc mot cu but toc moi
            _burstUntil = -999f;
            _suction.PrimeDrain();   // nhip DAU TIEN no ngay, khong bat nguoi choi cho
        }

        _lastAttacker = attacker;
        _lastDrainTime = Time.time;
        NoteCombat(attacker);   // ghi so doi thu -> bo phan vai biet minh la ke hut hay nan nhan

        int lost = _suction.DrainXp(xpAmount);
        if (lost > 0)
        {
            _drainedTotal += lost;

            // XP KHONG cong ngay nua: no BAY THEO OBJECT va chi vao bung ke hut khi object cham
            // mom (DevourVfx.Deliver). Chi khi ke hut khong co VFX thi DevourVfx moi tra ngay -
            // no tu lo, o day khong can biet.
            //
            // Chi con mot cua duy nhat de XP di qua, nen khong the co canh nan nhan mat 30 ma ke
            // hut an 28 nhu ban te-bao-vat-ly ngay xua.
            if (attacker.Vfx == null && attacker.Suction != null) attacker.Suction.GainXp(lost);

            // Hat bay tu than MINH ve mom KE HUT. He hat nam ben ke hut nen moi hat trong do deu
            // ve cung mot mom - khong phai gan dich cho tung hat.
            // skinColor VA co than deu la cua MINH - tuc cua nan nhan, dung y do: nhin luong hat
            // la biet dang an con nao va con do co bao nhieu
            if (attacker.Vfx != null)
                attacker.Vfx.EmitDrain(Center, lost, skinColor, transform.lossyScale.x);

            // TIENG POP: mot te bao vua bi rut ra khoi minh. Hut cang lien tuc thi cang to va cang
            // cao - chuoi tu tut ve 0 o khoi reset phia tren khi thoat duoc mot luc.
            //
            // Chi keu khi co NGUOI CHOI o mot trong hai dau: hai bot an nhau o goc map ma van keu
            // thi ca van chi con mot trang tieng pop khong ro cua ai.
            _popStreak++;
            if ((isPlayer || attacker.isPlayer) && SoundManager.HasInstance)
                SoundManager.Instance.PlaySfxStreak(SoundManager.Sfx.Drain, _popStreak);
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

        // MINH chet: so XP dang bay tren duong (do minh rut duoc cua nguoi khac) phai duoc tra
        // het truoc khi object bi huy, khong thi no boc hoi.
        if (_vfx != null) _vfx.FlushAll();

        int remain = _suction != null ? _suction.Xp : 0;
        bool killerAlive = killer != null && !killer.IsDead;
        if (killerAlive)
        {
            // XP con lai BAY THEO OBJECT ve mom ke giet, khong cong ngay. Khong co VFX thi
            // EmitDeath tu tra ngay - xem DevourVfx.SpawnOne.
            if (killer.Vfx != null)
                killer.Vfx.EmitDeath(Center, skinColor, transform.lossyScale.x, remain);
            else if (remain > 0 && killer.Suction != null)
                killer.Suction.GainXp(remain);
        }

        // TIENG NUOT DUT. Chi khi co NGUOI CHOI o mot trong hai dau - AI an AI thi im, dung yeu cau.
        // Keu o day chu khong o GameManager.ReportDeath: ReportDeath chay SAU khi xac bay xong vao
        // mom, tieng se tre nua giay so voi cai nhin thay.
        if (killerAlive && (isPlayer || killer.isPlayer) && SoundManager.HasInstance)
            SoundManager.Instance.PlaySfx(SoundManager.Sfx.EatHead);

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

        // Khong con bi hut -> nhip rut NGUOI DAN ve goc. Goi o day chu khong o ben ke hut:
        // nan nhan phai tu nguoi ke ca khi ke hut da bo di / da chet.
        if (!IsBeingDrained && _suction != null) _suction.CoolDrain(Time.deltaTime);
    }

    /// <summary>
    /// CAN THANH thi bi hut thang vao mom. Thanh do dung khoang cach level toi muc bi nuot nen
    /// hai dieu kien nay that ra la MOT - phep kiem tra level o day chi la de chac chan hai ben
    /// khong bao gio lech nhau (xem DevourFloorLevel).
    ///
    /// Van chay MOI FRAME: ra khoi non thi thanh reset ve day o TickStruggle ngay truoc do, nen
    /// tu no da chan mat cua chet - khong can co rieng nao o day.
    /// </summary>
    private void TickDevourCheck()
    {
        if (_struggle > 0f) return;              // con thanh = con cua chung minh minh khong phai do an
        if (!IsVictimRole || _rival == null || _rival.IsDead) return;
        if (Level >= _drainFloorLevel) return;    // van du suc, chua toi luot bi an

        Die(_rival, true);
    }

    /// <summary>
    /// THANH GHI - do bang LEVEL, KHONG phai bang thoi gian.
    ///
    ///   day = khoanh khac vua dinh vao non hut (chot moc _drainBaseLevel = level cua minh luc do)
    ///   can = level da tut xuong duoi muc bi nuot (devourLevelRatio x level ke hut)
    ///
    /// Mot thanh day vi the dung bang "so nhip rut con lai truoc khi bi an": nhin thanh la biet
    /// chinh xac minh con bao nhieu cua, khong con canh dem nguoc het 2.5 giay roi moi biet minh
    /// song hay chet.
    ///
    /// CA HAI DAU CHOT LUC VUA DINH NON va khong doi nua: Lv100 dinh phai mot con Lv120 thi can
    /// o Lv60, thanh la dung 41 nac va moi nhip rut di dung mot nac. Thu tinh moc can SONG theo
    /// level ke hut thi no bo len 1 level moi nhip (no an dung phan minh mat), thanh cong lai con
    /// 27 nac va tut nhanh hon so level minh thuc su mat - nhin thanh khong dem duoc gi nua.
    ///
    /// RA KHOI NON = RESET NGAY ve day. Khong con hoi dan theo thoi gian nua: thanh gio do khoang
    /// cach level, ma level thi khong tu moc lai duoc. Lach ra roi vao lai KHONG PHAI la lai suc -
    /// no chi chot mot moc day MOI o level da thap hon, tuc quang duong toi cho chet ngan hon that.
    ///
    /// Ke hut van khong co thanh: no bi cham suot tran, do la cai gia cua viec cu ghi mai.
    /// </summary>
    private void TickStruggle()
    {
        if (!IsBeingDrained || !IsVictimRole || _rival == null || _rival.IsDead)
        {
            _struggle = 1f;
            return;
        }

        // +1 o ca tu va mau: de thanh cham 0 o DUNG frame TickDevourCheck ban. Khong co no thi
        // level == muc can se cho ra thanh rong ma con chua chet - can im lia mot nhip.
        int span = _drainBaseLevel - _drainFloorLevel + 1;
        if (span > 0)
        {
            _struggle = Mathf.Clamp01((Level - _drainFloorLevel + 1) / (float)span);
            TryFireBurst();
            return;
        }

        // SAN THOI GIAN - CHI cho mot ca duy nhat: dinh vao non luc da qua yeu san (level da nam
        // duoi muc bi nuot ngay tu dau, span <= 0). Thanh khong co nac nao de tut ca, khong co san
        // thi no bang 0 ngay frame dau va nguoi choi chi thay minh bien mat.
        //
        // KHONG dung san nay lam tran cho ca truong hop binh thuong: lam vay la thanh chay theo
        // THOI GIAN chu khong theo level, va level se tut qua muc can tu lau truoc khi thanh kip
        // ve 0 (do thuc te: Lv20 dinh Lv20 mat 16 level trong khi thanh chi co 11 nac).
        _struggle = devourGraceTime > 0.001f
            ? Mathf.Clamp01(1f - (Time.time - _drainStartTime) / devourGraceTime)
            : 0f;
        TryFireBurst();
    }

    /// <summary>
    /// BUT TOC: khoanh khac thanh VUA CAT XUONG DUOI burstThreshold, nan nhan vung mot cu -
    /// burstSpeed trong burstDuration giay, roi ve 100% (khong ve lai victimSlow).
    ///
    /// Chi ban MOT lan mot pha hut (_burstFired). Khong co co nay thi moi frame sau do deu con
    /// thoa "thanh &lt; moc" - cua so se bi gia han lien tuc va nan nhan but toc vinh vien.
    ///
    /// Bat theo "&lt; moc" chu khong phai "bang moc": thanh tut theo NAC (mot nac moi nhip rut),
    /// tran nho chi co 3 nac nen no nhay thang tu 67% xuong 33%, khong bao gio dung o 60%.
    /// </summary>
    private void TryFireBurst()
    {
        if (_burstFired || _struggle >= burstThreshold) return;

        _burstFired = true;
        _burstUntil = Time.time + burstDuration;
    }

    /// <summary>
    /// LEVEL THAP NHAT ma minh con CHUA bi nuot khi doi dau mot con level 'rivalLevel'.
    ///
    /// Cung mot luat voi ban cu (Level &lt; rivalLevel x devourLevelRatio la bi an), chi doi sang
    /// so nguyen: goi DUNG MOT LAN luc vua dinh non roi cat vao _drainFloorLevel, de THANH GHI va
    /// CAI CHET dung chung mot cai moc duy nhat - khong bao gio co canh thanh da can ma con van
    /// song, hay chet trong khi thanh con mot doan.
    /// </summary>
    private int DevourFloorLevel(int rivalLevel)
    {
        return Mathf.CeilToInt(rivalLevel * devourLevelRatio);
    }

    /// <summary>
    /// Ap he so toc do theo VAI - bac thang cung, khong ease.
    ///
    ///   ke hut                     -> attackerSlow, suot tran
    ///   nan nhan DANG but toc      -> burstSpeed (tren 100% - xem TryFireBurst)
    ///   nan nhan BUT XONG          -> 1, du van con trong non: but duoc mot cu la coi nhu da
    ///                                 vung ra khoi the ghi cua pha nay
    ///   nan nhan CHUA but, trong non -> victimSlow
    ///   ra khoi non                -> 1 (nhay thang, de cam duoc dung khoanh khac gianh lai toc do)
    ///
    /// Doc IsBeingDrained chu KHONG doc thanh ghi: thanh gio do khoang cach level, no dung yen
    /// gan nhu suot pha hut nen khong con dung lam dong ho tra lai toc do duoc nua. Lay thang
    /// "co dang nam trong non khong" thi luat cung de hieu hon: bi ghi la vi dang bi hut.
    /// </summary>
    private void ApplyCombatSpeed()
    {
        if (_movement == null) return;

        float m = 1f;
        if (InCombat)
        {
            if (Level > _rivalLevel) m = attackerSlow;
            else if (IsBursting) m = burstSpeed;
            else if (_burstFired) m = 1f;
            else if (IsBeingDrained) m = victimSlow;
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
