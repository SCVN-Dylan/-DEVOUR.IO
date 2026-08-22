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
///   - skin   : doi material cho than player (moi Renderer trong skinTargets)
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

    [Tooltip("Cac Renderer nhan material khi doi skin. Tat ca deu mac CUNG mot material.\n\n" +
             "De TRONG = tu tim SkinnedMeshRenderer dau tien trong con (khong vo het): Crown va Teeth\n" +
             "cung la SkinnedMeshRenderer nhung co material rieng (Gold, M_Body) - vo ca vao day thi\n" +
             "vuong mien va rang bi son cung mau voi than.")]
    public List<Renderer> skinTargets = new List<Renderer>();

    [Tooltip("Danh sach cac lan TIEN HOA theo thu tu.\n" +
             "forms[0] = lan tien hoa THU NHAT. Dang goc khong nam trong danh sach nay -\n" +
             "no la nhung gi dang co san tren prefab, duoc chup lai luc Awake.")]
    public List<Form> forms = new List<Form>();

    [Tooltip("Ban moi khi doi dang, tham so = so lan tien hoa da qua (0 = dang goc). Cam VFX/SFX vao day")]
    public FormEvent onFormChanged;

    /// <summary>So lan tien hoa dang ap dung. 0 = dang goc.</summary>
    public int CurrentForm { get { return _current; } }

    private int _current;
    private bool _ready;

    private Material[] _prefabSkins;   // material goc cua TUNG renderer, chup luc Awake
    private Material _override;        // material do SetSkin dat. null = dung material goc

    void Awake() { Prepare(); }

    private void Prepare()
    {
        if (_ready) return;

        if (skinTargets == null) skinTargets = new List<Renderer>();

        // Don o trong truoc: mot o rong giua danh sach khong duoc lam lech chi so cua _prefabSkins
        for (int i = skinTargets.Count - 1; i >= 0; i--)
            if (skinTargets[i] == null) skinTargets.RemoveAt(i);

        // De trong thi tu tim - chi MOT cai, giong hanh vi cu. Vo het SkinnedMeshRenderer se dinh
        // ca Crown/Teeth von co material rieng.
        if (skinTargets.Count == 0)
        {
            Renderer r = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (r == null) r = GetComponentInChildren<Renderer>(true);
            if (r != null) skinTargets.Add(r);
        }

        // Chup material goc cua tung cai de con duong lui khi SetForm(0) / SetSkin(null).
        // Chup RIENG tung renderer chu khong chung mot cai: chung thi luc tra ve, moi cai deu bi
        // son cung mot material - cai nao von khac mau se mat mau vinh vien.
        _prefabSkins = new Material[skinTargets.Count];
        for (int i = 0; i < skinTargets.Count; i++)
            _prefabSkins[i] = skinTargets[i].sharedMaterial;

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
    /// Doi material THAN cho MOI renderer trong skinTargets, va giu no lam material NEN cho cac
    /// lan tien hoa sau.
    ///
    /// VI SAO PHAI GIU: ApplyState() moi lan tien hoa deu dung lai material tu dau. Neu chi gan
    /// thang sharedMaterial mot lan thi bot mac dung skin luc sinh ra, den moc tien hoa dau tien
    /// (Lv10) la ApplyState keo material ve mac dinh cua prefab - skin bien mat, ma khong mot dong
    /// loi nao. Giu trong _override thi no song qua moi lan doi dang.
    ///
    /// Form nao co skin rieng thi skin do VAN THANG - doi skin nen khong duoc pha dang dang mac.
    ///
    /// Truyen null = tra tung renderer ve material goc cua chinh no.
    /// </summary>
    public void SetSkin(Material skin)
    {
        Prepare();
        _override = skin;
        ApplyState(_current);
    }

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

        // Material theo thu tu uu tien:
        //   1. skin cua lan tien hoa GAN NHAT co gan skin
        //   2. skin do SetSkin dat (skin ngau nhien cua bot)
        //   3. material goc cua CHINH renderer do
        Material formSkin = null;
        for (int i = 0; i < forms.Count && (i + 1) <= index; i++)
            if (forms[i] != null && forms[i].skin != null) formSkin = forms[i].skin;

        if (skinTargets == null) return;

        for (int i = 0; i < skinTargets.Count; i++)
        {
            Renderer r = skinTargets[i];
            if (r == null) continue;

            Material want = formSkin;
            if (want == null) want = _override;
            if (want == null && _prefabSkins != null && i < _prefabSkins.Length) want = _prefabSkins[i];

            if (want != null && r.sharedMaterial != want) r.sharedMaterial = want;
        }
    }
}
