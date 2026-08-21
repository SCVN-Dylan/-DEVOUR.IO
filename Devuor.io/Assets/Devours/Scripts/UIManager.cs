using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
    public enum MatchState { Home, Playing, Ended }

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

    public Text scoreText;
    public Text timerText;

    [Header("Ket thuc - THUA")]
    // FormerlySerializedAs: ba truong nay truoc ten la gameOverPanel / finalScoreText /
    // restartButton va DANG duoc keo san trong MapTest. Khong co attribute nay thi doi ten =
    // Unity coi la truong moi, ba o Inspector tu nhien rong ma khong bao loi gi.
    [FormerlySerializedAs("gameOverPanel")] public GameObject losePanel;
    [FormerlySerializedAs("finalScoreText")] public Text loseScoreText;
    [FormerlySerializedAs("restartButton")] public Button loseButton;

    [Header("Ket thuc - THANG")]
    public GameObject winPanel;
    public Text winScoreText;
    public Button winButton;

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
    /// Man chao: dong bang the gioi cho toi khi bam Play.
    ///
    /// timeScale = 0 dung mot phat tat het: physics khong tick nen khong ai di chuyen duoc, dong
    /// ho khong chay, AI dung im. Nut UI van bam duoc vi he thong su kien chay theo gio that.
    /// </summary>
    private void ShowHome()
    {
        State = MatchState.Home;
        Time.timeScale = 0f;

        if (homePanel != null) homePanel.SetActive(true);
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
        if (State == MatchState.Playing) return;

        State = MatchState.Playing;
        Time.timeScale = 1f;

        if (homePanel != null) homePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(true);
        if (joystick != null) joystick.gameObject.SetActive(true);

        SetScore(0);
        TimeLeft = matchDuration;
        RefreshTimer();

        // HasInstance chu khong phai Instance: Instance se TU TAO mot GameManager rong neu scene
        // khong co san, tao ra roi cung khong sinh duoc bot nao (thieu prefab) - rac vo nghia.
        if (GameManager.HasInstance) GameManager.Instance.StartMatch();
    }

    /// <summary>Cong them diem, dung khi nhan vat "nuot" duoc mot vat the.</summary>
    public void AddScore(int amount)
    {
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

        Time.timeScale = 0f;
        if (joystick != null) joystick.gameObject.SetActive(false);

        Text scoreLabel = win ? winScoreText : loseScoreText;
        GameObject panel = win ? winPanel : losePanel;

        if (scoreLabel != null) scoreLabel.text = "SCORE  " + Score;
        if (panel != null) panel.SetActive(true);
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
        timerText.text = (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
