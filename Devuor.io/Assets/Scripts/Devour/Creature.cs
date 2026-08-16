using UnityEngine;

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

    public SimpleSuction Suction { get { return _suction; } }
    public RbMovement Movement { get { return _movement; } }

    /// <summary>Cap do hien tai (lay tu SimpleSuction, khong luu ban sao rieng de khoi lech).</summary>
    public int Level { get { return _suction != null ? _suction.Level : 1; } }

    /// <summary>Tam than trong toa do world, da tinh ca scale khi len cap.</summary>
    public Vector3 Center { get { return transform.TransformPoint(centerOffset); } }

    /// <summary>Chay khi VUA GAN component trong Editor: dien san ref de khoi phai keo tay.</summary>
    void Reset() { AutoFill(); }

    void Awake()
    {
        // Ref da keo san tren prefab thi khong dong toi. AutoFill chi la LUOI AN TOAN cho
        // truong hop quen keo (prefab cu, object dung tay trong scene test).
        if (_suction == null || _movement == null) AutoFill();
    }

    private void AutoFill()
    {
        if (_suction == null) _suction = GetComponent<SimpleSuction>();
        if (_movement == null) _movement = GetComponent<RbMovement>();
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
