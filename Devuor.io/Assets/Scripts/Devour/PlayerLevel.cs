using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cap do cua nhan vat: quyet dinh hut noi nhung gi.
///
/// Luat: chi hut duoc vat co Devourable.requiredLevel nho hon hoac bang cap hien tai.
/// Cu moi Devourable.LevelsPerTier cap (mac dinh 10) thi mo khoa mot BAC vat the moi:
///
///   cap  1 -> bac 1: xe, cay
///   cap 10 -> bac 2: nha nho
///   cap 20 -> bac 3: nha to
///   cap 30 -> bac 4: nha to nhat
///
/// XP dung duong cong luy tien theo cong thuc chu khong phai bang so viet tay: voi
/// 30 cap thi bang so se dai 29 dong, sua mot phat la phai tinh lai het. Cong thuc
/// chi co hai so - XP cap dau va he so nhan moi cap - keo ca duong cong mot luc.
///
/// Component nay chi lo phan tien cap. Viec chan hut nam trong MouthSuction
/// (useLevelGate / suctionLevel), o day chi ghi so cap xuong do moi khi doi.
///
/// Chay Tools/Devour/Kiem tra can bang level de xem map co du XP cho nguoi choi
/// leo den cap mo khoa bac tiep theo khong.
/// </summary>
[RequireComponent(typeof(MouthSuction))]
[DisallowMultipleComponent]
public class PlayerLevel : MonoBehaviour
{
    [System.Serializable]
    public class LevelEvent : UnityEvent<int> { }

    [Header("Cap do")]
    [Tooltip("Cap luc bat dau van")]
    public int startLevel = 1;

    [Tooltip("Cap toi da. Nen bang cap mo khoa bac cuoi cung tro len, khong thi bac cuoi khong bao gio hut duoc")]
    public int maxLevel = 30;

    [Header("Duong cong XP")]
    [Tooltip("XP can de len cap 2")]
    public float baseXp = 1.2f;

    [Tooltip("Moi cap sau dat hon cap truoc bao nhieu lan. 1 = moi cap deu nhu nhau, 1.06 = doc dan")]
    [Range(1f, 1.5f)] public float xpGrowthPerLevel = 1.06f;

    [Tooltip("De trong = dung cong thuc ben tren. Dien vao de tu tay dat XP cho tung cap")]
    public int[] xpCurveOverride;

    [Tooltip("Nhan vao XP nhan duoc. Tang len de len cap nhanh hon ma khong phai sua duong cong")]
    public float xpMultiplier = 1f;

    [Header("Suc chua theo cap")]
    [Tooltip("So vat hut duoc CUNG LUC o tung bac. Phan tu 0 la bac 1 (tu cap 1),\n" +
             "phan tu 1 la bac 2 (tu cap 10), phan tu 2 la bac 3 (tu cap 20)...\n\n" +
             "Day la nhip lon len chinh cua van dau: cap thap thi moi lan chi lua duoc\n" +
             "hai mon, len cap moi ngoam duoc ca chum.\n\n" +
             "De trong = khong dong vao maxCaptured cua MouthSuction")]
    public int[] captureCapacityPerTier = new int[] { 2, 3, 5, 8 };

    [Header("Moi cap manh len bao nhieu")]
    [Tooltip("TAM HUT moi cap rong them bao nhieu phan tram cua tam hut GOC.\n" +
             "0.12 = cap 2 rong hon cap 1 12%, cap 3 hon 24%... (cong don, khong nhan chong)\n\n" +
             "He so nay nhan chong len phan da no theo kich thuoc than cua PlayerGrowth,\n" +
             "chu khong thay the. De 0 = tam hut chi phu thuoc kich thuoc nhu cu.\n\n" +
             "Bam chuot phai vao component roi chon 'In bang suc manh theo cap' de xem\n" +
             "so cu the tung cap truoc khi chinh")]
    [Range(0f, 0.5f)] public float rangeGainPerLevel = 0.12f;

    [Tooltip("KICH THUOC than moi cap to them bao nhieu phan tram. 0.05 = cap 2 to hon cap 1 5%.\n\n" +
             "Van bi chan boi PlayerGrowth.maxScale. Luu y: kich thuoc lai keo tam hut theo\n" +
             "(mu 0.5), nen bat cai nay len thi tam hut an hai lan - nho lai Range Gain Per Level.\n\n" +
             "De 0 (mac dinh) = than chi to len theo luong da an nhu cu")]
    [Range(0f, 0.5f)] public float sizeGainPerLevel = 0f;

    [Tooltip("GOC NON moi cap mo them bao nhieu DO. 1.5 = cap 2 rong hon cap 1 1.5 do.\n\n" +
             "Day la so do tuyet doi chu khong phai phan tram, vi goc non bi chan trong\n" +
             "khoang 5..179 do - nhan phan tram thi cham tran rat nhanh.\n\n" +
             "De 0 (mac dinh) = goc non khong doi theo cap")]
    [Range(0f, 10f)] public float coneAngleGainPerLevel = 0f;

    [Tooltip("Tran cua goc non, du len bao nhieu cap cung khong vuot qua.\n" +
             "Goc qua rong thi vung hut gan thanh hinh cau, quay huong nao cung hut duoc")]
    [Range(5f, 179f)] public float maxConeAngle = 120f;

    [Header("Su kien")]
    [Tooltip("Ban ra moi lan len cap, kem so cap moi")]
    public LevelEvent onLevelUp;

    [Tooltip("Ban ra khi mo khoa mot bac vat the moi (cap 10, 20, 30...), kem so cap")]
    public LevelEvent onTierUnlocked;

    [Tooltip("Ban ra khi ngam trung vat chua du cap, kem CAP CAN CO. Dung de hien 'Can cap N'")]
    public LevelEvent onBlocked;

    /// <summary>Cap hien tai.</summary>
    public int Level { get { return _level; } }

    /// <summary>XP da tich duoc trong cap hien tai.</summary>
    public int Xp { get { return _xp; } }

    /// <summary>XP can de len cap tiep theo, 0 nghia la da kich cap.</summary>
    public int XpToNext { get { return XpToNextAt(_level); } }

    /// <summary>Tien do trong cap hien tai 0..1, dung ve thanh progress bar.</summary>
    public float Progress01
    {
        get
        {
            int need = XpToNext;
            return need > 0 ? Mathf.Clamp01((float)_xp / need) : 1f;
        }
    }

    public bool IsMaxLevel { get { return _level >= maxLevel; } }

    /// <summary>Bac vat the cao nhat dang mo khoa o cap hien tai.</summary>
    public int UnlockedTier
    {
        get
        {
            int tier = 1;
            for (int t = 2; t <= Devourable.TierCount; t++)
                if (_level >= Devourable.RequiredLevelForTier(t)) tier = t;

            return tier;
        }
    }

    private MouthSuction _suction;
    private PlayerGrowth _growth;
    private float _baseRange = -1f;
    private float _baseConeAngle = -1f;
    private int _level = 1;
    private int _xp;

    /// <summary>XP can de len cap tiep theo khi dang o cap nao do.</summary>
    public int XpToNextAt(int level)
    {
        if (level >= maxLevel) return 0;

        if (xpCurveOverride != null && xpCurveOverride.Length > 0)
        {
            int index = Mathf.Clamp(level - 1, 0, xpCurveOverride.Length - 1);
            return Mathf.Max(1, xpCurveOverride[index]);
        }

        return Mathf.Max(1, Mathf.CeilToInt(baseXp * Mathf.Pow(xpGrowthPerLevel, level - 1)));
    }

    void Awake()
    {
        _suction = GetComponent<MouthSuction>();
        _level = Mathf.Clamp(startLevel, 1, Mathf.Max(1, maxLevel));
        _xp = 0;
        PushLevelToSuction();
    }

    void OnEnable()
    {
        _suction.Swallowed += OnSwallowed;
        _suction.Blocked += OnBlocked;
    }

    void OnDisable()
    {
        _suction.Swallowed -= OnSwallowed;
        _suction.Blocked -= OnBlocked;
    }

    private void OnSwallowed(Devourable target)
    {
        if (target == null) return;
        AddXp(Mathf.Max(1, target.xpValue));
    }

    private void OnBlocked(Devourable target)
    {
        if (target == null || onBlocked == null) return;
        onBlocked.Invoke(target.requiredLevel);
    }

    /// <summary>Cong XP, tu len cap khi du. Dung duoc tu ngoai cho quest, nhat vat pham...</summary>
    public void AddXp(int amount)
    {
        if (amount <= 0 || IsMaxLevel) return;

        _xp += Mathf.Max(1, Mathf.RoundToInt(amount * xpMultiplier));

        // Vong lap chu khong phai if: nuot mot toa nha to co the du XP nhay may cap mot luc
        while (!IsMaxLevel)
        {
            int need = XpToNext;
            if (need <= 0 || _xp < need) break;

            _xp -= need;
            LevelUpOnce();
        }

        if (IsMaxLevel) _xp = 0;
    }

    private void LevelUpOnce()
    {
        int tierBefore = UnlockedTier;
        _level++;

        PushLevelToSuction();
        if (onLevelUp != null) onLevelUp.Invoke(_level);

        if (UnlockedTier > tierBefore && onTierUnlocked != null) onTierUnlocked.Invoke(_level);
    }

    /// <summary>Dat thang cap, dung cho cheat/test hoac power-up.</summary>
    public void SetLevel(int value)
    {
        int clamped = Mathf.Clamp(value, 1, Mathf.Max(1, maxLevel));
        if (clamped == _level) return;

        int tierBefore = UnlockedTier;
        bool up = clamped > _level;

        _level = clamped;
        _xp = 0;

        PushLevelToSuction();
        if (up && onLevelUp != null) onLevelUp.Invoke(_level);
        if (UnlockedTier > tierBefore && onTierUnlocked != null) onTierUnlocked.Invoke(_level);
    }

    private void PushLevelToSuction()
    {
        if (_suction == null) _suction = GetComponent<MouthSuction>();
        if (_suction == null) return;

        _suction.suctionLevel = _level;
        PushPowerForLevel();

        if (captureCapacityPerTier == null || captureCapacityPerTier.Length == 0) return;

        // Suc chua lay theo BAC dang mo, khong phai theo tung cap: nhu vay no nhay
        // dung cung luc voi viec mo khoa loai vat the moi, nguoi choi cam nhan mot
        // buoc tien ro rang thay vi nhich len tung ti mot.
        int index = Mathf.Clamp(UnlockedTier - 1, 0, captureCapacityPerTier.Length - 1);
        _suction.maxCaptured = Mathf.Max(1, captureCapacityPerTier[index]);
    }

    /// <summary>
    /// Ap dung ba con so "moi cap manh len bao nhieu" cho cap hien tai.
    ///
    /// Tam hut va kich thuoc phai di vong qua PlayerGrowth: no ghi de ca hai thu do moi
    /// frame trong ApplyScale, ghi thang vao MouthSuction o day thi frame sau la bay sach.
    /// Dua he so cho no nhan vao moi giu duoc ca hai nguon phong to.
    ///
    /// Goc non thi khong ai tranh nen ghi thang, chi can nho goc GOC truoc khi ghi lan
    /// dau - khong thi moi lan len cap lai cong don len ket qua cua lan truoc.
    /// </summary>
    private void PushPowerForLevel()
    {
        int steps = _level - 1;

        if (_growth == null) _growth = GetComponent<PlayerGrowth>();

        if (_growth != null)
        {
            _growth.LevelRangeMultiplier = 1f + rangeGainPerLevel * steps;
            _growth.LevelScaleMultiplier = 1f + sizeGainPerLevel * steps;
        }
        else
        {
            if (_baseRange < 0f) _baseRange = _suction.range;
            _suction.range = _baseRange * (1f + rangeGainPerLevel * steps);
        }

        if (_baseConeAngle < 0f) _baseConeAngle = _suction.coneAngle;
        _suction.coneAngle = Mathf.Min(_baseConeAngle + coneAngleGainPerLevel * steps, maxConeAngle);
    }

    /// <summary>
    /// In ra bang suc manh tung cap de can bang, khong phai bam Play va choi thu.
    /// Bam chuot phai vao header cua component trong Inspector de goi.
    ///
    /// Cot kich thuoc va tam hut la o trang thai CHUA an gi (than x1). Luc choi that
    /// con nhan them phan an duoc, nen day la san duoi chu khong phai so cuoi cung.
    /// </summary>
    [ContextMenu("In bang suc manh theo cap")]
    private void LogPowerTable()
    {
        MouthSuction suction = _suction != null ? _suction : GetComponent<MouthSuction>();
        if (suction == null) return;

        PlayerGrowth growth = _growth != null ? _growth : GetComponent<PlayerGrowth>();

        float baseRange = growth != null ? growth.BaseRange : (_baseRange >= 0f ? _baseRange : suction.range);
        float baseAngle = _baseConeAngle >= 0f ? _baseConeAngle : suction.coneAngle;
        float exponent = growth != null ? growth.suctionRangeExponent : 1f;

        var text = new System.Text.StringBuilder();
        text.AppendLine("[PlayerLevel] Bang suc manh theo cap (than chua an gi):");
        text.AppendLine("cap |  than | tam hut | goc non | suc chua | XP len cap");

        for (int level = 1; level <= Mathf.Max(1, maxLevel); level++)
        {
            int steps = level - 1;

            float bodyScale = 1f + sizeGainPerLevel * steps;
            if (growth != null) bodyScale = Mathf.Min(bodyScale, Mathf.Max(1f, growth.maxScale));

            float range = baseRange * Mathf.Pow(bodyScale, exponent) * (1f + rangeGainPerLevel * steps);
            float angle = Mathf.Min(baseAngle + coneAngleGainPerLevel * steps, maxConeAngle);

            int tier = 1;
            for (int t = 2; t <= Devourable.TierCount; t++)
                if (level >= Devourable.RequiredLevelForTier(t)) tier = t;

            int capacity = captureCapacityPerTier != null && captureCapacityPerTier.Length > 0
                ? Mathf.Max(1, captureCapacityPerTier[Mathf.Clamp(tier - 1, 0, captureCapacityPerTier.Length - 1)])
                : suction.maxCaptured;

            text.AppendLine(level.ToString().PadLeft(3)
                + " | x" + bodyScale.ToString("F2")
                + " | " + range.ToString("F2").PadLeft(7)
                + " | " + angle.ToString("F1").PadLeft(7)
                + " | " + capacity.ToString().PadLeft(8)
                + " | " + (level < maxLevel ? XpToNextAt(level).ToString() : "-"));
        }

        Debug.Log(text.ToString(), this);
    }

    /// <summary>Suc chua o cap hien tai, de UI hien "2/3 mon".</summary>
    public int CaptureCapacity
    {
        get
        {
            if (captureCapacityPerTier == null || captureCapacityPerTier.Length == 0)
                return _suction != null ? _suction.maxCaptured : 1;

            int index = Mathf.Clamp(UnlockedTier - 1, 0, captureCapacityPerTier.Length - 1);
            return Mathf.Max(1, captureCapacityPerTier[index]);
        }
    }

    void OnValidate()
    {
        startLevel = Mathf.Max(1, startLevel);
        maxLevel = Mathf.Max(1, maxLevel);
        baseXp = Mathf.Max(0.1f, baseXp);
        xpMultiplier = Mathf.Max(0.01f, xpMultiplier);

        if (captureCapacityPerTier != null)
            for (int i = 0; i < captureCapacityPerTier.Length; i++)
                captureCapacityPerTier[i] = Mathf.Max(1, captureCapacityPerTier[i]);

        if (xpCurveOverride == null) return;
        for (int i = 0; i < xpCurveOverride.Length; i++)
            xpCurveOverride[i] = Mathf.Max(1, xpCurveOverride[i]);
    }
}
