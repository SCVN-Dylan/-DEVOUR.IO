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

    [Tooltip("Dinh dang: {0}=ten, {1}=cap")]
    public string format = "{0}\n<size=65%>Lv {1}</size>";

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
            // Dung cam.up (khong phai world up) de nhan quay THANG mat vao camera ke ca khi
            // camera nhin cheo xuong (khong bi mong/nghieng). +Z huong ra xa camera = mat doc.
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);

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
            label.text = string.Format(format, playerName, lvl);
        }
    }
}
