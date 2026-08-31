using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quan ly toan bo UI cua man choi: HOME (nut Play), HUD (diem, dong ho), joystick,
/// va hai man ket thuc THANG / THUA. Truy cap tu gameplay qua UIManager.Instance.
///
/// Day cung la thang cam NHIP MOT VAN:
///
///   Home ---(bam Play)---> Playing ---+-- het bot      --> Ended(THANG)
///                                     +-- bi an        --> Ended(THUA)
///                                     +-- het gio      --> Ended(THUA)
///
/// Khac GameManager (lo PHE - ai dang trong van, ai la nguoi choi), UIManager lo LUC NAO:
/// bao gio van bat dau, bao gio ket thuc. Bot chi duoc de ra luc bam Play chu khong phai luc
/// load scene - khong thi o man Home da co 8 con chay long nhong sau tam BG.
///
/// Scene KHONG gan homePanel (scene test) thi vao thang Playing nhu truoc, khong doi gi.
/// </summary>
public class UIManager : MonoBehaviour
{
    /// <summary>Ba trang thai cua mot van. Xem so do o dau file.</summary>
    /// <summary>
    /// Intro nam GIUA Home va Playing: da bam Play, UI da an het, nhung van CHUA bat dau -
    /// dong ho chua dem, bot chua duoc sinh. Xem StartMatch / BeginPlay.
    /// </summary>
    public enum MatchState { Home, Intro, Playing, Ended }

    public static UIManager Instance { get; private set; }

    [Header("Input")]
    public VirtualJoystick joystick;

    [Header("Home")]
    [Tooltip("Man hinh chao co nut Play. De trong = bo qua Home, vao van ngay khi load scene")]
    public GameObject homePanel;

    [Tooltip("Nut Play tren man Home")]
    public Button playButton;

    [Header("HUD")]
    [Tooltip("Object cha cua diem + dong ho. De trong = khong an/hien HUD theo trang thai")]
    public GameObject hudRoot;

    public TMP_Text scoreText;
    public TMP_Text timerText;

    [Header("Ket thuc - THUA")]
    // FormerlySerializedAs: ba truong nay truoc ten la gameOverPanel / finalScoreText /
    // restartButton va DANG duoc keo san trong MapTest. Khong co attribute nay thi doi ten =
    // Unity coi la truong moi, ba o Inspector tu nhien rong ma khong bao loi gi.
    [FormerlySerializedAs("gameOverPanel")] public GameObject losePanel;
    [FormerlySerializedAs("finalScoreText")] public TMP_Text loseScoreText;
    [FormerlySerializedAs("restartButton")] public Button loseButton;

    [Header("Ket thuc - THANG")]
    public GameObject winPanel;
    public TMP_Text winScoreText;
    public Button winButton;

    [Header("Ket thuc van")]
    [Tooltip("BAT: ket thuc van thi DONG BANG the gioi (timeScale = 0).\n\n" +
             "TAT (mac dinh): the gioi chay tiep phia sau man ket thuc - bot van di lai, item van\n" +
             "bay, nhac van chay. Man hinh ket thuc chi la mot lop UI dat len tren.")]
    public bool freezeOnEnd = false;

    [Header("Match")]
    [Tooltip("Thoi gian moi van tinh bang giay. De 0 de tat dong ho")]
    public float matchDuration = 120f;

    [Header("Performance")]
    [Tooltip("Khoa framerate cho nhip khung hinh deu. Tren mobile vSync bi bo qua nen can gia tri nay. 0 = khong khoa")]
    public int targetFrameRate = 60;

    public int Score { get; private set; }
    public float TimeLeft { get; private set; }

    /// <summary>Van dang o buoc nao. Chi UIManager duoc doi.</summary>
    public MatchState State { get; private set; }

    /// <summary>Giu lai cho code cu: van da ket thuc chua.</summary>
    public bool IsGameOver { get { return State == MatchState.Ended; } }

    /// <summary>
    /// Bam PLAY AGAIN thi van moi vao THANG gameplay, khong quay lai man Home.
    ///
    /// Phai la static vi cach choi lai la LOAD LAI SCENE - moi object (ke ca UIManager nay) deu
    /// bi huy, chi static la song sot qua duoc lan load do.
    /// </summary>
    private static bool _skipHome;

    /// <summary>
    /// Xoa static khi bat dau mot lan Play moi. Bat buoc phai co neu du an tat Domain Reload:
    /// luc do _skipHome se con giu gia tri cua lan chay truoc, va lan chay sau se nhay thang vao
    /// gameplay - mat luon man Home ma khong hieu vi sao.
    ///
    /// Ham nay chi chay MOT LAN moi lan vao Play chu khong phai moi lan load scene, nen viec bam
    /// PLAY AGAIN roi load lai scene van giu duoc co.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        _skipHome = false;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        HookClickSound();

        if (playButton != null) playButton.onClick.AddListener(StartMatch);
        if (loseButton != null) loseButton.onClick.AddListener(Replay);
        if (winButton != null) winButton.onClick.AddListener(Replay);

        if (losePanel != null) losePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);

        SetScore(0);
        TimeLeft = matchDuration;
        RefreshTimer();

        if (homePanel == null || _skipHome)
        {
            _skipHome = false;
            StartMatch();
        }
        else
        {
            ShowHome();
        }
    }

    void Update()
    {
        if (State != MatchState.Playing || matchDuration <= 0f) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            RefreshTimer();
            EndMatch(false);   // het gio = THUA
            return;
        }
        RefreshTimer();
    }

    /// <summary>
    /// Gan tieng CLICK vao MOI nut nam duoi canvas nay - ke ca nut trong panel dang tat.
    ///
    /// Quet mot lan luc Start thay vi gan tay tung nut: them nut moi vao prefab UI la no co tieng
    /// ngay, khong ai phai nho ra day noi day. Doi lai, nut duoc TAO LUC CHAY se khong co tieng -
    /// hien khong co cai nao nhu vay.
    ///
    /// Gan truoc cac listener khac nen tieng keu ngay khi bam, khong doi viec cua nut xong (nut
    /// Replay se load lai scene - gan sau thi khong bao gio toi luot no).
    /// </summary>
    private void HookClickSound()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].onClick.AddListener(PlayClick);
        }
    }

    private void PlayClick()
    {
        if (SoundManager.HasInstance) SoundManager.Instance.PlaySfx(SoundManager.Sfx.Click);
    }

    /// <summary>
    /// Bat mot panel len, va ep moi Animator ben trong chay theo GIO THAT.
    ///
    /// VI SAO PHAI EP: ca ba man (Home, Thang, Thua) deu hien luc timeScale = 0. Animator mac dinh
    /// chay theo gio DA NHAN timeScale, nen o timeScale = 0 no dung im o frame dau tien - ma frame
    /// dau cua mot animation "bung ra" thi moi thu deu o scale 0. Ket qua: panel bat len that,
    /// activeInHierarchy = true, moi Image alpha = 1, nhung tren man hinh KHONG THAY GI.
    ///
    /// Da dinh dung canh do: WinPanel/Main mang controller C_Win, cac con deu nam o scale 0.00.
    ///
    /// Ep trong code chu khong sua tay tung Animator trong prefab: lam tay thi popup nao them sau
    /// nay cung se tat mot lan nua, va khong ai nho ra vi sao.
    /// </summary>
    private void ShowPanel(GameObject panel)
    {
        if (panel == null) return;

        panel.SetActive(true);

        Animator[] anims = panel.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i] == null) continue;
            anims[i].updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    /// <summary>
    /// Man chao: dong bang the gioi cho toi khi bam Play.
    ///
    /// timeScale = 0 dung mot phat tat het: physics khong tick nen khong ai di chuyen duoc, dong
    /// ho khong chay, AI dung im. Nut UI van bam duoc vi he thong su kien chay theo gio that.
    /// </summary>
    private void ShowHome()
    {
        State = MatchState.Home;
        Time.timeScale = 0f;

        ShowPanel(homePanel);
        if (hudRoot != null) hudRoot.SetActive(false);
        if (joystick != null) joystick.gameObject.SetActive(false);
    }

    /// <summary>
    /// VAO VAN. Gan vao nut Play.
    ///
    /// Bot duoc de ra o DAY chu khong phai luc load scene - xem GameManager.StartMatch.
    /// </summary>
    public void StartMatch()
    {
        if (State == MatchState.Intro || State == MatchState.Playing) return;

        State = MatchState.Intro;
        Time.timeScale = 1f;

        // AN SACH UI. HUD va joystick chi bat len o BeginPlay: trong doan intro khong co gi de doc
        // (diem 0, dong ho chua chay) va cung khong dieu khien duoc gi.
        if (homePanel != null) homePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(false);
        if (joystick != null) joystick.gameObject.SetActive(false);

        // VAN CHUA VAO TRAN. Ba thu giu cho van chua chay, khong can dong toi timeScale:
        //   - State chua phai Playing -> Update() thoat som -> dong ho dung yen
        //   - GameManager.StartMatch chua duoc goi  -> chua co con bot nao tren map
        //   - CameraManager khoa di chuyen nguoi choi suot intro
        //
        // KHONG dat timeScale = 0: dong bang thi 3 con khinh khi cau treo bat dong giua troi, khung
        // intro nhin nhu game do. De gio chay thuong thi chi rieng chung bay - vua du song.
        //
        // HasInstance chu khong Instance: scene test khong co CameraManager thi vao tran thang,
        // khong the de ket lai o mot man hinh trong khong loi gi.
        if (CameraManager.HasInstance) CameraManager.Instance.PlayIntro(BeginPlay);
        else BeginPlay();
    }

    /// <summary>
    /// VAO TRAN THAT SU - CameraManager goi khi camera da ve toi nguoi choi.
    ///
    /// Moi thu bat dau dem tu day: dong ho, diem, nhac nen, va bot moi duoc de ra map. Tach khoi
    /// StartMatch de doan intro khong an mat giay nao cua van dau.
    /// </summary>
    public void BeginPlay()
    {
        if (State == MatchState.Playing) return;

        State = MatchState.Playing;
        Time.timeScale = 1f;

        if (hudRoot != null) hudRoot.SetActive(true);
        if (joystick != null) joystick.gameObject.SetActive(true);

        SetScore(0);
        TimeLeft = matchDuration;
        RefreshTimer();

        // KHONG dong toi nhac nen o day. Nhac chay XUYEN SUOT: SoundManager phat _musicOnStart
        // ngay trong Awake va song qua scene (DontDestroyOnLoad), nen bam PLAY AGAIN thi bai nhac
        // cu chay tiep lien mach thay vi giat ve dau moi van.
        //
        // Ban cu goi RestartMusic() o day. Bo di la co y: cat ngang bai nhac o dung khoanh khac
        // vao tran nghe nhu game vua bi khuc mot cai, va moi van deu bat dau bang cung mot doan
        // intro cua bai - nghe lap rat nhanh.

        // HasInstance chu khong phai Instance: Instance se TU TAO mot GameManager rong neu scene
        // khong co san, tao ra roi cung khong sinh duoc bot nao (thieu prefab) - rac vo nghia.
        if (GameManager.HasInstance) GameManager.Instance.StartMatch();
    }

    /// <summary>
    /// Cong them diem, dung khi nhan vat "nuot" duoc mot vat the.
    ///
    /// KHONG cong nua sau khi van da ket thuc: the gioi van chay tiep phia sau man ket thuc (xem
    /// freezeOnEnd), nen nguoi choi con song sau khi THANG van tiep tuc hut item duoc. Khong chan
    /// thi so diem tren HUD cu chay len trong khi man ket thuc dang trung ra mot con so khac -
    /// hai con so cua cung mot van ma khong khop nhau.
    /// </summary>
    public void AddScore(int amount)
    {
        if (State == MatchState.Ended) return;
        SetScore(Score + amount);
    }

    public void SetScore(int value)
    {
        Score = value;
        if (scoreText != null) scoreText.text = value.ToString();
    }

    /// <summary>
    /// KET THUC VAN, hien man tong ket tuong ung.
    ///
    /// Chi an tu trang thai Playing: con bot cuoi va nguoi choi co the chet trong cung mot frame,
    /// luc do lenh goi thu hai roi vao day va bi chan - khong bao gio hien ca hai man mot luc.
    /// </summary>
    public void EndMatch(bool win)
    {
        if (State != MatchState.Playing) return;
        State = MatchState.Ended;

        if (freezeOnEnd) Time.timeScale = 0f;
        if (joystick != null) joystick.gameObject.SetActive(false);

        // Tieng ket thuc van. Ten chua gan clip trong bang thi SoundManager tu bo qua, khong loi.
        if (SoundManager.HasInstance)
            SoundManager.Instance.PlaySfx(win ? SoundManager.Sfx.Win : SoundManager.Sfx.Lose);

        TMP_Text scoreLabel = win ? winScoreText : loseScoreText;
        GameObject panel = win ? winPanel : losePanel;

        if (scoreLabel != null) scoreLabel.text = "SCORE  " + Score;
        ShowPanel(panel);
    }

    /// <summary>Giu lai cho code cu goi: thua = ket thuc van ma khong thang.</summary>
    public void GameOver()
    {
        EndMatch(false);
    }

    /// <summary>CHOI LAI: load lai scene roi vao THANG van moi, khong qua man Home.</summary>
    public void Replay()
    {
        _skipHome = true;
        Restart();
    }

    /// <summary>Load lai scene. Khong dat _skipHome nen se dung lai o man Home.</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void RefreshTimer()
    {
        if (timerText == null) return;

        if (matchDuration <= 0f)
        {
            timerText.text = string.Empty;
            return;
        }

        int total = Mathf.CeilToInt(TimeLeft);
        timerText.text = (total / 60).ToString() + ":" + (total % 60).ToString("00");
    }
}
