using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quan ly toan bo UI cua man choi: HUD (diem, dong ho), joystick,
/// va man hinh ket thuc. Truy cap tu gameplay qua UIManager.Instance.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Input")]
    public VirtualJoystick joystick;

    [Header("HUD")]
    public Text scoreText;
    public Text timerText;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Text finalScoreText;
    public Button restartButton;

    [Header("Match")]
    [Tooltip("Thoi gian moi van tinh bang giay. De 0 de tat dong ho")]
    public float matchDuration = 120f;

    [Header("Performance")]
    [Tooltip("Khoa framerate cho nhip khung hinh deu. Tren mobile vSync bi bo qua nen can gia tri nay. 0 = khong khoa")]
    public int targetFrameRate = 60;

    public int Score { get; private set; }
    public float TimeLeft { get; private set; }
    public bool IsGameOver { get; private set; }

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
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        IsGameOver = false;
        TimeLeft = matchDuration;
        SetScore(0);
        RefreshTimer();
    }

    void Update()
    {
        if (IsGameOver || matchDuration <= 0f) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            RefreshTimer();
            GameOver();
            return;
        }
        RefreshTimer();
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

    /// <summary>Ket thuc van, hien man hinh tong ket.</summary>
    public void GameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        if (joystick != null) joystick.gameObject.SetActive(false);
        if (finalScoreText != null) finalScoreText.text = "SCORE  " + Score;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

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
