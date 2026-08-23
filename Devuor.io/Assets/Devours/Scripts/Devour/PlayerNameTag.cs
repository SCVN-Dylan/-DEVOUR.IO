using TMPro;
using UnityEngine;

/// <summary>
/// Bang ten + cap do cho nhan vat, dat tren mot CANVAS World Space nam TRONG prefab Player.
/// Component gan tren object Canvas (con cua Player): moi frame quay Canvas ve phia camera
/// (billboard) va cap nhat chu = ten + "Lv N" (doc tu SimpleSuction o cha).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayerNameTag : MonoBehaviour
{
    [Header("Noi dung")]
    public string playerName = "Player";

    [Tooltip("Doc cap do. De trong = tu tim SimpleSuction o cha")]
    public SimpleSuction suction;

    [Tooltip("TMP hien chu. De trong = tu tim TMP_Text trong con")]
    public TMP_Text label;

    [Tooltip("Dinh dang: {0}=ten, {1}=cap.\n" +
             "Mac dinh: CAP o tren va co chu day du, TEN o duoi va nho lai 65% - cap la thu nguoi\n" +
             "choi phai doc trong tich tac de biet co nen lao vao hay chay, con ten chi de nhan dien")]
    public string format = "Lv {1}\n<size=65%>{0}</size>";

    [Header("Billboard")]
    [Tooltip("Camera de quay mat vao. De trong = Camera.main")]
    public Camera cam;

    public bool billboard = true;

    [Tooltip("Giu kich thuoc canvas KHONG doi du player scale to (bu lai theo scale cha)")]
    public bool keepConstantSize = false;

    [Tooltip("Scale world giu co dinh khi bat keepConstantSize")]
    public float constantScale = 0.01f;

    void OnEnable() { Resolve(); Refresh(); }
    void LateUpdate() { Resolve(); Refresh(); }

    private void Resolve()
    {
        if (suction == null) suction = GetComponentInParent<SimpleSuction>();
        if (cam == null) cam = Camera.main;
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    private void Refresh()
    {
        if (billboard && cam != null)
            // COPY THANG rotation cua camera, KHONG dung LookRotation(vi_tri - vi_tri_cam).
            //
            // Camera cua game la ORTHOGRAPHIC: phep chieu song song, moi vat len man hinh theo
            // CUNG MOT huong nhin. Ngam theo "huong tu camera toi vat" la moi bang ten ra mot
            // forward khac nhau, roi LookRotation truc giao hoa 'up' theo cai forward do -> vat
            // cang lech khoi truc camera thi bang ten cang bi XOAY NGHIENG.
            //
            // So do that (camera chuc xuong 55 do): nguoi choi dung giua man hinh chi nghieng 2 do
            // nen khong ai de y, nhung bot o ria nghieng toi 41.6 do. Do la ly do loi nay chi
            // "thay o bot" - that ra player cung sai, chi la sai it.
            //
            // Copy rotation thi moi bang ten deu song song mat phang camera: khong nghieng, khong
            // meo, va tat ca giong het nhau. +Z van huong ra xa camera nen chu doc binh thuong.
            transform.rotation = cam.transform.rotation;

        if (keepConstantSize && transform.parent != null)
        {
            Vector3 ls = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                constantScale / Mathf.Max(0.0001f, ls.x),
                constantScale / Mathf.Max(0.0001f, ls.y),
                constantScale / Mathf.Max(0.0001f, ls.z));
        }

        if (label != null)
        {
            int lvl = suction != null ? suction.Level : 1;
            // Chi doi text khi level/ten thay doi -> tranh string.Format + regen mesh TMP moi frame (nang tren mobile)
            if (lvl != _lastLevel || playerName != _lastName)
            {
                label.text = string.Format(format, playerName, lvl);
                _lastLevel = lvl;
                _lastName = playerName;
            }
        }
    }

    private int _lastLevel = -1;
    private string _lastName;
}
