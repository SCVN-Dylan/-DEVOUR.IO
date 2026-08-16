using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DAU MOI cua mot van: giu NGUOI CHOI va DANH SACH moi sinh vat dang song.
///
/// Sinh ra de tra loi hai cau hoi ma truoc day code phai doan bang
/// FindAnyObjectByType&lt;SimpleSuction&gt;() - luc scene chi co 1 con thi doan lam sao cung dung,
/// nhung them 3 con AI la doan trung bot ma khong bao loi gi, chi sai am tham (camera zoom theo
/// bot, diem cua bot nhay len HUD nguoi choi...).
///
///   GameManager.Instance.Player     -> con nguoi choi dieu khien
///   GameManager.Instance.Creatures  -> tat ca, de AI do doi thu bang KHOANG CACH
///
/// Danh sach nay la nguon duy nhat: AI khong phai OverlapSphere di tim nhau, chi duyet mot List
/// 4 phan tu - gan nhu mien phi so voi mot physics query.
///
/// Khac UIManager (lo HUD, dong ho, man ket thuc), GameManager lo PHE - ai dang trong van, ai la
/// nguoi choi. Hai thang khong nuot viec cua nhau.
/// </summary>
[DefaultExecutionOrder(-100)]   // Awake chay TRUOC moi Creature de dang ky khong bi hut
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    /// <summary>
    /// Truy cap tu bat ky dau. Scene chua dat san GameManager (scene test) thi TU TAO mot cai -
    /// nho vay Creature khong phai rai null-check khap noi, va scene test cu van chay nhu cu.
    /// </summary>
    public static GameManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Object.FindAnyObjectByType<GameManager>();
            if (_instance == null && Application.isPlaying)
            {
                GameObject go = new GameObject("GameManager (auto)");
                _instance = go.AddComponent<GameManager>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// Da co GameManager song chua - KHONG tu tao ra cai moi nhu Instance.
    /// Dung luc go bo (OnDisable/OnDestroy): thoat Play mode thi GameManager co the bi huy
    /// truoc cac Creature, luc do goi Instance se di tao mot GameManager moi chi de rut ten ra
    /// khoi no roi bo di - rac vo nghia.
    /// </summary>
    public static bool HasInstance { get { return _instance != null; } }

    [Tooltip("Con do NGUOI CHOI dieu khien. De trong = tu nhan ra qua Creature.isPlayer khi no vao van.\n" +
             "Gan tay o day cung duoc, luc do coi nhu chot cung.")]
    [SerializeField] private Creature _player;

    private readonly List<Creature> _creatures = new List<Creature>();

    /// <summary>Con nguoi choi dieu khien. null = chua vao van hoac da bi nuot.</summary>
    public Creature Player { get { return _player; } }

    /// <summary>Moi sinh vat dang song. CHI DOC - them/bot phai qua Register/Unregister.</summary>
    public IReadOnlyList<Creature> Creatures { get { return _creatures; } }

    /// <summary>Con nguoi choi con song khong (dung cho AI: het player thi khoi ngam).</summary>
    public bool HasPlayer { get { return _player != null; } }

    /// <summary>
    /// Xoa static khi bat dau van moi. Bat buoc phai co neu du an bat "Enter Play Mode Options"
    /// (tat domain reload cho vao Play nhanh): luc do static KHONG tu reset giua hai lan chay,
    /// Instance se con tro vao GameManager cua van truoc (da bi huy).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Creature tu goi luc OnEnable. Goi lai nhieu lan cung khong vao trung.</summary>
    public void Register(Creature c)
    {
        if (c == null || _creatures.Contains(c)) return;
        _creatures.Add(c);

        if (c.isPlayer && _player == null) _player = c;
    }

    /// <summary>Creature tu goi luc OnDisable / bi nuot.</summary>
    public void Unregister(Creature c)
    {
        if (c == null) return;
        _creatures.Remove(c);

        if (_player == c) _player = null;
    }
}
