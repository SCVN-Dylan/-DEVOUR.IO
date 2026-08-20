using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HINH DANG player theo tung lan TIEN HOA. Class nay CHI lo phan nhin thay - khong biet gi ve
/// level, stage hay XP. Ai goi SetForm(n) cung duoc; hien tai SimpleSuction goi moi khi qua mot
/// moc co isEvolution.
///
/// Moi lan tien hoa co the lam MOT hoac CA HAI viec, cam cai nao chay cai do:
///   - target : bat object len (rang, chi tiet than, ham...)
///   - skin   : doi material cho than player
/// De trong ca hai thi lan do khong doi gi ve hinh (van ban onFormChanged de cam VFX/SFX).
///
/// CONG DON: qua lan 3 thi lan 1 va 2 VAN GIU. Material lay cua lan gan nhat co gan skin,
/// khong gan thi giu material truoc do; ve dang goc (SetForm 0) thi tra lai material ban dau.
/// </summary>
[DisallowMultipleComponent]
public class PlayerVisual : MonoBehaviour
{
    [System.Serializable] public class FormEvent : UnityEvent<int> { }

    /// <summary>Mot lan tien hoa.</summary>
    [System.Serializable]
    public class Form
    {
        [Tooltip("Ten cho de doc, khong anh huong logic")]
        public string name = "Tien hoa";

        [Tooltip("Object bat len o lan tien hoa nay (rang, chi tiet than...).\nDe trong = lan nay khong bat object nao")]
        public GameObject target;

        [Tooltip("Material doi cho than player o lan tien hoa nay.\nDe trong = giu nguyen material dang co")]
        public Material skin;
    }

    [Tooltip("Renderer nhan material khi doi skin.\nDe trong = tu tim SkinnedMeshRenderer dau tien trong con")]
    public Renderer skinTarget;

    [Tooltip("Danh sach cac lan TIEN HOA theo thu tu.\n" +
             "forms[0] = lan tien hoa THU NHAT. Dang goc khong nam trong danh sach nay -\n" +
             "no la nhung gi dang co san tren prefab, duoc chup lai luc Awake.")]
    public List<Form> forms = new List<Form>();

    [Tooltip("Ban moi khi doi dang, tham so = so lan tien hoa da qua (0 = dang goc). Cam VFX/SFX vao day")]
    public FormEvent onFormChanged;

    /// <summary>So lan tien hoa dang ap dung. 0 = dang goc.</summary>
    public int CurrentForm { get { return _current; } }

    private int _current;
    private Material _baseSkin;
    private bool _ready;

    void Awake() { Prepare(); }

    private void Prepare()
    {
        if (_ready) return;
        if (skinTarget == null) skinTarget = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skinTarget == null) skinTarget = GetComponentInChildren<Renderer>(true);

        // Chup material goc de con duong lui khi SetForm(0)
        if (skinTarget != null) _baseSkin = skinTarget.sharedMaterial;

        _ready = true;
        _current = 0;
        ApplyState(0);
    }

    /// <summary>
    /// Dat so lan tien hoa da qua. 0 = dang goc, 1 = sau lan tien hoa dau tien...
    /// Goi lai cung mot so thi khong lam gi (re, goi thoai mai).
    /// </summary>
    public void SetForm(int index)
    {
        Prepare();
        index = Mathf.Clamp(index, 0, forms != null ? forms.Count : 0);
        if (index == _current) return;

        _current = index;
        ApplyState(index);

        if (onFormChanged != null) onFormChanged.Invoke(index);
    }

    /// <summary>
    /// Doi material THAN, va dat luon lam MATERIAL NEN cho cac lan tien hoa sau.
    ///
    /// VI SAO PHAI GHI CA _baseSkin: ApplyState() moi lan tien hoa deu bat dau tu _baseSkin roi
    /// moi chong material cua cac form len. _baseSkin duoc chup trong Prepare() luc Awake - tuc
    /// TRUOC khi GameManager kip gan skin ngau nhien cho bot. Neu chi doi sharedMaterial ma khong
    /// doi _baseSkin thi bot mac dung skin luc sinh ra, den moc tien hoa dau tien (Lv10) la
    /// ApplyState keo material ve mac dinh cua prefab - skin bien mat, ma khong mot dong loi nao.
    ///
    /// Truyen null = tra ve material goc cua prefab (con duong lui).
    /// </summary>
    public void SetSkin(Material skin)
    {
        Prepare();
        if (skinTarget == null) return;

        if (skin != null)
        {
            if (_prefabSkin == null) _prefabSkin = _baseSkin;   // giu material goc de con duong lui
            _baseSkin = skin;
        }
        else
        {
            _baseSkin = _prefabSkin != null ? _prefabSkin : _baseSkin;
        }

        // Ap lai theo dang hien tai, khong gan thang: dang o lan tien hoa co material rieng thi
        // material do van phai thang - doi skin nen khong duoc pha dang dang mac.
        ApplyState(_current);
    }

    private Material _prefabSkin;   // material goc tren prefab, chup lan dau khi bi doi skin

    private void ApplyState(int index)
    {
        if (forms == null) return;

        // Cong don: bat moi lan tien hoa <= index, tat phan con lai
        for (int i = 0; i < forms.Count; i++)
        {
            Form f = forms[i];
            if (f == null || f.target == null) continue;
            bool on = (i + 1) <= index;
            if (f.target.activeSelf != on) f.target.SetActive(on);
        }

        // Material: lay cua lan tien hoa GAN NHAT co gan skin; khong co thi ve material goc
        Material m = _baseSkin;
        for (int i = 0; i < forms.Count && (i + 1) <= index; i++)
            if (forms[i] != null && forms[i].skin != null) m = forms[i].skin;

        if (skinTarget != null && m != null && skinTarget.sharedMaterial != m)
            skinTarget.sharedMaterial = m;
    }
}
