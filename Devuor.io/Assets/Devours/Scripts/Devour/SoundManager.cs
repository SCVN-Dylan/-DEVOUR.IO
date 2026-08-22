using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tay cam cua mot am thanh dang phat. Chi can giu lai khi con dinh TAT no (sfx loop).
/// Sfx ban mot phat roi thoi thi khong can giu.
///
/// Co mang theo SO THE HE: o phat am so 3 duoc cho muon di cho muon lai nhieu lan trong mot van,
/// tay cam cu chi ghi "o so 3" thi mot lenh Stop cham chan se tat nham am thanh cua nguoi den sau.
/// So the he tang moi lan o do duoc cho muon, nen tay cam het han tu chet, khong tat nham ai.
/// </summary>
public struct SoundHandle
{
    internal int slot;
    internal int generation;

    public static readonly SoundHandle None = default(SoundHandle);
    public bool IsValid { get { return generation > 0; } }
}

/// <summary>
/// AM THANH cho ca game: mot nhac nen chay lien tuc, va sfx ban chong len nhau.
///
///   SoundManager.Instance.PlayMusic(clip);          nhac nen, loop
///   SoundManager.Instance.PlaySfx(clip);            mot phat, CHONG LEN am dang phat
///   SoundHandle h = SoundManager.Instance.PlayLoop(clip);   sfx lap - nho giu tay cam
///   SoundManager.Instance.Stop(h);
///   SoundManager.Instance.MusicVolume = 0.5f;       0..1, tu luu lai cho lan sau
///
/// SONG XUYEN SCENE (DontDestroyOnLoad): bam PLAY AGAIN la load lai scene, neu chet theo scene thi
/// nhac nen giat lai tu dau moi van. Doi lai phai tu don sfx loop khi doi man - xem OnSceneLoaded.
///
/// KHONG CO Update(): o phat am duoc thu hoi ngay luc di muon cai tiep theo chu khong phai quet moi
/// frame. Mot game mobile khong nen tra 12 lan hoi "xong chua" moi khung hinh de doi lay mot thu ma
/// khong ai dang nhin.
/// </summary>
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    private const string PrefMaster = "snd.master";
    private const string PrefMusic = "snd.music";
    private const string PrefSfx = "snd.sfx";
    private const string PrefMusicMuted = "snd.musicMuted";
    private const string PrefSfxMuted = "snd.sfxMuted";

    private static SoundManager _instance;

    /// <summary>
    /// Goi tu bat ky dau. Scene chua dat san thi TU TAO - nho vay khong ai phai rai null-check,
    /// va scene test cu van chay.
    /// </summary>
    public static SoundManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Object.FindAnyObjectByType<SoundManager>();
            if (_instance == null && Application.isPlaying)
            {
                GameObject go = new GameObject("SoundManager (auto)");
                _instance = go.AddComponent<SoundManager>();
            }
            return _instance;
        }
    }

    /// <summary>Da co SoundManager song chua - KHONG tu tao ra cai moi nhu Instance.</summary>
    public static bool HasInstance { get { return _instance != null; } }

    /// <summary>
    /// TEN cua tung tieng dong trong game. Goi bang ten chu khong keo AudioClip di khap noi:
    /// doi file am thanh thi sua mot cho trong bang duoi, khong phai lan ra tung script.
    ///
    /// Them tieng moi: them mot dong vao day roi keo clip vao bang trong Inspector.
    /// </summary>
    public enum Sfx
    {
        None = 0,
        Pop = 1,        // mot te bao bi rut ra khoi con dang bi hut
        Sucking = 2,    // tieng hut, chay lap trong suot luc anim hut dang mo
        EatHead = 3,    // nuot dut mot con - chi khi co nguoi choi o mot trong hai dau
        Upgrade = 4,    // vua vuot mot moc trong LevelSteps
        EatFeed = 5,    // nuot mot mon item
    }

    [System.Serializable]
    public struct SfxEntry
    {
        public Sfx id;
        public AudioClip clip;

        [Tooltip("Do to rieng cua clip nay (0..1). De 0 = coi nhu 1 - de trong khong bao gio\n" +
                 "bien thanh 'cam tieng', vi mot am thanh im lang khong bao gio la y do cua ai")]
        [Range(0f, 1f)] public float volume;
    }

    [Header("Bang am thanh")]
    [SerializeField] private SfxEntry[] _sfxTable;

    [Header("Chuoi - lam lien tuc thi TO DAN (khong nhanh dan)")]
    [Tooltip("Bao nhieu lan lien tiep thi am luong cham tran.\n\n" +
             "Dung chung cho Pop va EatFeed. CHI doi am luong, KHONG doi pitch: doi pitch la doi\n" +
             "cao do, nghe thanh mot tieng khac chu khong phai tieng cu to hon.")]
    [SerializeField] private int _streakSteps = 8;

    [Range(0f, 1f)] [SerializeField] private float _streakVolumeFrom = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float _streakVolumeTo = 1f;

    [Tooltip("Chi dung cho PlaySfxStreakTimed: ngung lau hon bao nhieu giay thi chuoi ve day.\n" +
             "An item khong co khai niem 'mot pha' nhu luc bi hut, nen phai dem bang khoang lang.")]
    [SerializeField] private float _streakGap = 0.6f;

    [Header("Nhac nen")]
    [Tooltip("Bai nhac phat ngay khi vao game. De trong = khong tu phat gi, cho code goi PlayMusic")]
    [SerializeField] private AudioClip _musicOnStart;

    [Header("Am luong (0..1)")]
    [Tooltip("Nhan len TAT CA - ca nhac lan sfx")]
    [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;

    [Range(0f, 1f)] [SerializeField] private float _musicVolume = 0.5f;

    [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1f;

    [SerializeField] private bool _musicMuted;
    [SerializeField] private bool _sfxMuted;

    [Header("Ho phat sfx")]
    [Tooltip("So am thanh co the keu CUNG LUC. Het cho thi cai phat lau nhat bi cuop.\n\n" +
             "12 la thoai mai cho game nay: mot pha hut nhieu lam ba bon tieng chong nhau. Nang len\n" +
             "khong ton CPU may (AudioSource ranh gan nhu mien phi) nhung nang len vo ich thi ca chuc\n" +
             "tieng keu cung luc chi thanh mot dam bui.")]
    [SerializeField] private int _sfxVoices = 12;

    [Tooltip("BAT: doi scene thi tat het sfx dang loop. Gan nhu luon phai bat - con da phat tieng\n" +
             "hut da bi huy theo scene cu, khong con ai goi Stop cho no nua, tieng do se keu mai mai")]
    [SerializeField] private bool _stopSfxOnSceneChange = true;

    /// <summary>Mot o phat am trong ho.</summary>
    private class Voice
    {
        public AudioSource source;
        public int generation = 1;
        public float baseVolume = 1f;   // do to cua rieng phat nay, chua nhan he so
        public bool loop;
        public float startTime;         // de biet cai nao phat lau nhat khi phai cuop cho
    }

    private Voice[] _voices;
    private AudioSource _musicSource;
    private int _nextSlot;
    private SfxEntry[] _byId;      // tra cuu theo (int)Sfx - nhanh hon duyet bang moi lan phat
    private int[] _streakCount;    // chuoi dang dem cho PlaySfxStreakTimed, theo tung Sfx
    private float[] _streakLast;   // lan goi gan nhat cua tung Sfx

    /// <summary>Nhac dang phat. null = khong co bai nao.</summary>
    public AudioClip CurrentMusic { get { return _musicSource != null ? _musicSource.clip : null; } }

    public float MasterVolume
    {
        get { return _masterVolume; }
        set { _masterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefMaster, _masterVolume); ApplyVolumes(); }
    }

    public float MusicVolume
    {
        get { return _musicVolume; }
        set { _musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefMusic, _musicVolume); ApplyVolumes(); }
    }

    public float SfxVolume
    {
        get { return _sfxVolume; }
        set { _sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefSfx, _sfxVolume); ApplyVolumes(); }
    }

    public bool MusicMuted
    {
        get { return _musicMuted; }
        set { _musicMuted = value; PlayerPrefs.SetInt(PrefMusicMuted, value ? 1 : 0); ApplyVolumes(); }
    }

    public bool SfxMuted
    {
        get { return _sfxMuted; }
        set { _sfxMuted = value; PlayerPrefs.SetInt(PrefSfxMuted, value ? 1 : 0); ApplyVolumes(); }
    }

    /// <summary>
    /// Xoa static khi bat dau van moi. Bat buoc phai co neu du an tat Domain Reload: luc do static
    /// KHONG tu reset giua hai lan chay, Instance se con tro vao SoundManager cua lan truoc (da huy).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    void Awake()
    {
        // Load lai scene se de ra mot SoundManager thu hai (cai cu song xuyen scene). Cai moi tu bo di.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPrefs();
        BuildSources();
        BuildTable();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (_musicOnStart != null) PlayMusic(_musicOnStart);
    }

    void OnDestroy()
    {
        if (_instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        _instance = null;
    }

    /// <summary>
    /// Ghi PlayerPrefs xuong dia khi game bi day xuong nen. Cac setter chi ghi vao bo nho - dien
    /// thoai bi kill trong nen thi khong co luc nao de ghi ra nua.
    /// </summary>
    void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    /// <summary>Keo thanh am luong trong Inspector luc dang chay thi nghe doi ngay.</summary>
    void OnValidate()
    {
        _sfxVoices = Mathf.Max(1, _sfxVoices);
        if (Application.isPlaying && _voices != null) ApplyVolumes();
    }
#endif

    // ------------------------------------------------------------------ NHAC NEN

    /// <summary>
    /// Phat nhac nen. Goi lai voi DUNG bai dang phat thi khong lam gi - nhac chay tiep, khong giat
    /// ve dau. Nho vay goi PlayMusic o dau man choi bao nhieu lan cung duoc.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (_musicSource == null) BuildSources();
        if (clip == null) { StopMusic(); return; }

        if (_musicSource.clip == clip && _musicSource.isPlaying)
        {
            _musicSource.loop = loop;
            return;
        }

        _musicSource.clip = clip;
        _musicSource.loop = loop;
        _musicSource.volume = MusicLevel();
        _musicSource.Play();
    }

    public void StopMusic()
    {
        if (_musicSource == null) return;
        _musicSource.Stop();
        _musicSource.clip = null;
    }

    /// <summary>Tam dung, giu nguyen cho dang phat - dung khi mo bang tam dung.</summary>
    public void PauseMusic()
    {
        if (_musicSource != null) _musicSource.Pause();
    }

    public void ResumeMusic()
    {
        if (_musicSource != null) _musicSource.UnPause();
    }

    // ------------------------------------------------------------------ SFX

    /// <summary>
    /// Ban MOT PHAT roi thoi. Moi lan goi lay mot o rieng nen tieng nay CHONG LEN tieng dang phat
    /// chu khong cat no - hai con cung bi nuot mot luc thi nghe thay ca hai.
    /// </summary>
    public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        Play(clip, volume, pitch, false);
    }

    /// <summary>Ban mot phat theo TEN trong bang. Ten chua gan clip thi im lang bo qua.</summary>
    public void PlaySfx(Sfx id, float volume = 1f, float pitch = 1f)
    {
        SfxEntry e;
        if (!TryGet(id, out e)) return;
        Play(e.clip, volume * EntryVolume(e), pitch, false);
    }

    /// <summary>
    /// Ban mot phat trong mot CHUOI dang len: cang lam nhieu lan lien tiep thi cang TO.
    ///
    /// CHI to dan, KHONG nhanh dan. Doi pitch la doi cao do - tai nghe ra mot tieng KHAC chu khong
    /// phai tieng cu manh hon, va sau vai lan thi no thanh tieng chit.
    ///
    /// Ben goi tu dem va tu reset khi chuoi dut (dung khi co moc reset chinh xac, nhu 'pha hut'
    /// cua Pop). Khong co moc nao ro rang thi dung PlaySfxStreakTimed.
    ///
    /// streak dem tu 1.
    /// </summary>
    public void PlaySfxStreak(Sfx id, int streak)
    {
        SfxEntry e;
        if (!TryGet(id, out e)) return;
        Play(e.clip, StreakVolume(streak) * EntryVolume(e), 1f, false);
    }

    /// <summary>
    /// Nhu PlaySfxStreak nhung TU DEM: ngung goi lau hon _streakGap giay thi chuoi tu ve day.
    ///
    /// Dung cho tieng an item - an lien tuc trong mot dong thi to dan, roi dong di cho khac thi ve
    /// nho. Ben goi khong phai giu bien dem nao ca, chi goi moi lan an duoc mot mon.
    /// </summary>
    public void PlaySfxStreakTimed(Sfx id)
    {
        SfxEntry e;
        if (!TryGet(id, out e)) return;

        int i = (int)id;
        if (_streakCount == null || _streakCount.Length != _byId.Length)
        {
            _streakCount = new int[_byId.Length];
            _streakLast = new float[_byId.Length];
        }

        // unscaledTime: man ket thuc dat timeScale = 0, Time.time dung han thi chuoi khong bao gio dut
        float now = Time.unscaledTime;
        if (now - _streakLast[i] > Mathf.Max(0f, _streakGap)) _streakCount[i] = 0;

        _streakCount[i]++;
        _streakLast[i] = now;

        Play(e.clip, StreakVolume(_streakCount[i]) * EntryVolume(e), 1f, false);
    }

    /// <summary>Do to cua lan thu <paramref name="streak"/> trong chuoi (dem tu 1).</summary>
    private float StreakVolume(int streak)
    {
        int steps = Mathf.Max(1, _streakSteps);
        float t = Mathf.Clamp01((streak - 1) / (float)steps);
        return Mathf.Lerp(_streakVolumeFrom, _streakVolumeTo, t);
    }

    /// <summary>
    /// Sfx LAP LIEN TUC (tieng hut, tieng chay...). Nho GIU tay cam tra ve de con tat:
    /// khong ai tat thi no keu den het van.
    /// </summary>
    public SoundHandle PlayLoop(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        return Play(clip, volume, pitch, true);
    }

    /// <summary>Sfx lap theo TEN trong bang. Ten chua gan clip thi tra ve tay cam rong.</summary>
    public SoundHandle PlayLoop(Sfx id, float volume = 1f, float pitch = 1f)
    {
        SfxEntry e;
        if (!TryGet(id, out e)) return SoundHandle.None;
        return Play(e.clip, volume * EntryVolume(e), pitch, true);
    }

    /// <summary>Tat mot am thanh theo tay cam. Tay cam het han thi khong lam gi - khong tat nham ai.</summary>
    public void Stop(SoundHandle handle)
    {
        Voice v = Resolve(handle);
        if (v == null) return;

        v.source.Stop();
        v.source.clip = null;
        v.loop = false;
        v.generation++;          // tay cam cu tu het hieu luc
    }

    /// <summary>Con dang keu khong - de biet co can phat lai khong.</summary>
    public bool IsPlaying(SoundHandle handle)
    {
        Voice v = Resolve(handle);
        return v != null && v.source.isPlaying;
    }

    /// <summary>Tat het sfx (ca mot phat lan loop). Khong dung toi nhac nen.</summary>
    public void StopAllSfx()
    {
        if (_voices == null) return;

        for (int i = 0; i < _voices.Length; i++)
        {
            Voice v = _voices[i];
            if (v.source == null) continue;

            v.source.Stop();
            v.source.clip = null;
            v.loop = false;
            v.generation++;
        }
    }

    // ------------------------------------------------------------------ BEN TRONG

    private SoundHandle Play(AudioClip clip, float volume, float pitch, bool loop)
    {
        if (clip == null) return SoundHandle.None;
        if (_voices == null) BuildSources();

        int slot;
        Voice v = TakeVoice(out slot);
        if (v == null) return SoundHandle.None;

        v.baseVolume = Mathf.Clamp01(volume);
        v.loop = loop;
        v.startTime = Time.unscaledTime;   // unscaled: man ket thuc dat timeScale = 0, Time.time dung han

        AudioSource s = v.source;
        s.clip = clip;
        s.loop = loop;
        s.pitch = pitch;
        s.volume = v.baseVolume * SfxLevel();
        s.Play();

        SoundHandle h;
        h.slot = slot;
        h.generation = v.generation;
        return h;
    }

    /// <summary>
    /// Muon mot o phat am.
    ///
    /// Thu hoi o LUC DI MUON chu khong quet moi frame: o da phat xong thi tu no im, khong can ai
    /// danh dau. Nho vay ca he thong khong ton mot chut CPU nao khi dang khong phat gi.
    ///
    /// Het cho that thi cuop cai phat LAU NHAT - cat mot tieng gan tan con hon bo mot tieng vua bat
    /// dau. Khong bao gio cuop cua sfx loop: nhung tieng do co chu, con ai dang giu tay cam cua no.
    /// </summary>
    private Voice TakeVoice(out int index)
    {
        index = -1;
        int n = _voices.Length;

        // Bat dau tu cho ke tiep lan truoc: trai deu ra ca ho thay vi bam mai vao o dau
        for (int k = 0; k < n; k++)
        {
            int i = (_nextSlot + k) % n;
            Voice v = _voices[i];
            if (v.source != null && !v.source.isPlaying)
            {
                _nextSlot = (i + 1) % n;
                v.generation++;              // tay cam cua nguoi muon truoc het han tu day
                index = i;
                return v;
            }
        }

        Voice oldest = null;
        int oldestIndex = -1;
        for (int i = 0; i < n; i++)
        {
            Voice v = _voices[i];
            if (v.source == null || v.loop) continue;                       // loop co chu, khong cuop
            if (oldest == null || v.startTime < oldest.startTime) { oldest = v; oldestIndex = i; }
        }

        if (oldest == null) return null;      // ca ho deu la loop - khong con gi de cuop

        _nextSlot = (oldestIndex + 1) % n;
        oldest.source.Stop();
        oldest.generation++;
        index = oldestIndex;
        return oldest;
    }

    private Voice Resolve(SoundHandle handle)
    {
        if (_voices == null || !handle.IsValid) return null;
        if (handle.slot < 0 || handle.slot >= _voices.Length) return null;

        Voice v = _voices[handle.slot];
        if (v.generation != handle.generation) return null;   // o nay da cho nguoi khac muon roi
        return v.source != null ? v : null;
    }

    private void BuildSources()
    {
        if (_voices != null) return;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;      // 2D: game nhin tu tren xuong, khong can dinh vi
        _musicSource.volume = MusicLevel();

        int n = Mathf.Max(1, _sfxVoices);
        _voices = new Voice[n];
        for (int i = 0; i < n; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;

            Voice v = new Voice();
            v.source = s;
            _voices[i] = v;
        }
    }

    /// <summary>
    /// Do bang ra mang tra cuu theo chi so enum. Bang chi co vai dong nen duyet cung khong sao,
    /// nhung Pop keu vai lan moi giay trong suot tran - tra cuu O(1) thi khoi phai nghi.
    /// </summary>
    private void BuildTable()
    {
        int max = 0;
        if (_sfxTable != null)
            for (int i = 0; i < _sfxTable.Length; i++) max = Mathf.Max(max, (int)_sfxTable[i].id);

        _byId = new SfxEntry[max + 1];
        if (_sfxTable == null) return;

        for (int i = 0; i < _sfxTable.Length; i++)
        {
            SfxEntry e = _sfxTable[i];
            if (e.id == Sfx.None || e.clip == null) continue;
            _byId[(int)e.id] = e;
        }
    }

    private bool TryGet(Sfx id, out SfxEntry entry)
    {
        entry = default(SfxEntry);
        if (_byId == null) BuildTable();

        int i = (int)id;
        if (i <= 0 || i >= _byId.Length) return false;
        if (_byId[i].clip == null) return false;

        entry = _byId[i];
        return true;
    }

    /// <summary>Do to rieng cua mot dong trong bang. De trong (0) thi coi nhu 1, khong phai cam tieng.</summary>
    private static float EntryVolume(SfxEntry e)
    {
        return e.volume > 0f ? e.volume : 1f;
    }

    private void LoadPrefs()
    {
        _masterVolume = PlayerPrefs.GetFloat(PrefMaster, _masterVolume);
        _musicVolume = PlayerPrefs.GetFloat(PrefMusic, _musicVolume);
        _sfxVolume = PlayerPrefs.GetFloat(PrefSfx, _sfxVolume);
        _musicMuted = PlayerPrefs.GetInt(PrefMusicMuted, _musicMuted ? 1 : 0) != 0;
        _sfxMuted = PlayerPrefs.GetInt(PrefSfxMuted, _sfxMuted ? 1 : 0) != 0;
    }

    private float MusicLevel() { return _musicMuted ? 0f : _musicVolume * _masterVolume; }
    private float SfxLevel() { return _sfxMuted ? 0f : _sfxVolume * _masterVolume; }

    /// <summary>Am luong vua doi - moi thu DANG phat phai doi theo ngay, khong doi phat lai.</summary>
    private void ApplyVolumes()
    {
        if (_musicSource != null) _musicSource.volume = MusicLevel();
        if (_voices == null) return;

        float lvl = SfxLevel();
        for (int i = 0; i < _voices.Length; i++)
        {
            Voice v = _voices[i];
            if (v.source != null) v.source.volume = v.baseVolume * lvl;
        }
    }

    /// <summary>
    /// Doi scene: sfx loop cua scene cu phai tat.
    ///
    /// Con da goi PlayLoop (vi du tieng hut cua mot con bot) da bi huy theo scene cu, khong con ai
    /// goi Stop cho no - ma SoundManager thi song tiep, nen tieng do se keu den het game. Nhac nen
    /// KHONG dung toi: song xuyen scene la ca muc dich cua no.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_stopSfxOnSceneChange) StopAllSfx();
    }
}
