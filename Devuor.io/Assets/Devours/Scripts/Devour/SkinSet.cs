using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KHO SKIN cho bot. Moi skin = material cho than + mau cho hat VFX luc con do BI hut.
///
/// CACH BOC LA "TUI XAO", KHONG PHAI RANDOM THUAN - cung ly le voi GameManager.BiasForIndex:
/// random thuan 4 skin cho 4 bot thi xac suat co it nhat hai con trung skin la ~70%, nguoi choi
/// nhin ra nhu loi spawn. Tui xao dam bao n con dau tien (n = so skin) chac chan khac nhau; het
/// tui thi xao lai va phat tiep, nen so bot nhieu hon so skin van chay binh thuong, chi la bat
/// dau lap lai - va lap deu chu khong don cuc.
///
/// LUU Y VE TRANG THAI: ScriptableObject song xuyen cac lan Play trong Editor, nen cai tui cung
/// song theo. Vi vay GameManager goi ResetBag() truoc moi dot spawn - khong thi van thu hai se
/// dau vao khuc thua cua van truoc. Tui KHONG serialize nen khong ghi ban vao file asset.
/// </summary>
[CreateAssetMenu(fileName = "SkinSet", menuName = "Devour/Skin Set", order = 100)]
public class SkinSet : ScriptableObject
{
    [System.Serializable]
    public class Skin
    {
        [Tooltip("Ten cho de doc trong Inspector, khong anh huong logic")]
        public string name = "Skin";

        [Tooltip("Material gan cho THAN con vat (qua PlayerVisual.SetSkin)")]
        public Material material;

        [Tooltip("Mau HAT VFX bay ra khi con mang skin nay BI hut.\n" +
                 "Nen lay tong mau cua material cho khop - nhin ra ngay dang an thang nao")]
        public Color particleColor = Color.white;
    }

    [Tooltip("Danh sach skin. Bot boc theo kieu 'tui xao': moi luot phat het mot vong, het thi xao lai")]
    public List<Skin> skins = new List<Skin>();

    /// <summary>So skin dang co.</summary>
    public int Count { get { return skins != null ? skins.Count : 0; } }

    // Tui chi song luc chay - khong [SerializeField] nen Unity khong ghi vao asset
    [System.NonSerialized] private List<int> _bag;
    [System.NonSerialized] private int _bagFor = -1;    // tui nay dung cho danh sach dai bao nhieu
    [System.NonSerialized] private int _lastDrawn = -1;

    /// <summary>Do tui ve trang thai dau. Goi truoc moi dot spawn.</summary>
    public void ResetBag()
    {
        if (_bag != null) _bag.Clear();
        _bagFor = -1;
        _lastDrawn = -1;
    }

    /// <summary>
    /// Boc mot skin. Tra ve null neu danh sach rong (chu goi tu lo phan do).
    /// </summary>
    public Skin Draw()
    {
        int n = Count;
        if (n == 0) return null;

        if (_bag == null) _bag = new List<int>(n);

        // Xao lai khi het tui, HOAC khi danh sach vua bi sua trong Editor (tui cu giu index cu)
        if (_bag.Count == 0 || _bagFor != n) Refill(n);

        int last = _bag.Count - 1;
        int idx = _bag[last];
        _bag.RemoveAt(last);
        _lastDrawn = idx;

        return skins[idx];
    }

    /// <summary>
    /// Do day tui roi xao (Fisher-Yates).
    ///
    /// Con mot buoc nho o cuoi: neu con SAP boc lai dung bang con VUA boc o tui truoc thi doi cho
    /// no. Khong co buoc nay, cho NOI hai tui van co the ra hai con lien tiep trung skin - dung cai
    /// canh de dap vao mat nguoi choi nhat, va cung la thu ma tui xao sinh ra de tranh.
    /// </summary>
    private void Refill(int n)
    {
        _bag.Clear();
        for (int i = 0; i < n; i++) _bag.Add(i);

        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int t = _bag[i]; _bag[i] = _bag[j]; _bag[j] = t;
        }

        // Boc tu CUOI mang, nen phan tu cuoi la con se ra dau tien
        if (n > 1 && _bag[n - 1] == _lastDrawn)
        {
            _bag[n - 1] = _bag[0];
            _bag[0] = _lastDrawn;
        }

        _bagFor = n;
    }
}
