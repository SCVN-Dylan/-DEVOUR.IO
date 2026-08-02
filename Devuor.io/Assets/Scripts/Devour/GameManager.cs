using UnityEngine;

/// <summary>
/// Cac cong tac chung cua man choi. Cho de dat nhung thu anh huong toan cuc,
/// khac voi UIManager (chi lo HUD, dong ho, man ket thuc).
///
/// Nguyen tac: GameManager la noi giu su that, no GHI thiet lap xuong cac component
/// lien quan chu khong bat ai phai hoi nguoc len. Nho vay MouthSuction khong can
/// biet GameManager co ton tai hay khong - thieu GameManager thi no van chay bang
/// gia tri cua chinh no.
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Hut")]
    [Tooltip("BAT: item lot vao vung hut la bay thang vao mieng ngay o toc do toi da.\n" +
             "Khong co pha giang co, khong rung, va khong bao gio bi bo lai du nhan vat co quay di huong khac.\n\n" +
             "TAT: giang co theo resistance cua tung vat nhu binh thuong, tuot khoi non thi bi bo lai.\n\n" +
             "Khong anh huong toi cong chan level: vat qua cap van khong hut duoc")]
    public bool instantDevour = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Apply();
    }

    void Start()
    {
        // Chay lai o Start vi Unity khong dam bao Awake cua GameManager chay truoc
        // Awake cua may cai MouthSuction. Start thi chac chan sau moi Awake.
        Apply();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Bat/tat luc dang chay, ap dung ngay lap tuc.</summary>
    public void SetInstantDevour(bool value)
    {
        instantDevour = value;
        Apply();
    }

    /// <summary>Day thiet lap xuong moi vung hut dang co trong scene.</summary>
    public void Apply()
    {
        MouthSuction[] suctions = FindObjectsByType<MouthSuction>(FindObjectsSortMode.None);
        for (int i = 0; i < suctions.Length; i++)
        {
            if (suctions[i] == null) continue;
            suctions[i].instantDevour = instantDevour;
        }
    }

    void OnValidate()
    {
        // Tich vao o Instant Devour giua luc dang Play la thay doi ngay, khong phai Play lai
        if (Application.isPlaying) Apply();
    }
}
