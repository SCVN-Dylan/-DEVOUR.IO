using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// KIEM TRA / SUA BoxCollider cua item cho khop co THAT cua model.
///
/// VI SAO CAN: SimpleSuction.Scan() quet bang OverlapSphere TREN COLLIDER. Hop sai co lam item bi
/// hut sai tam - hop to qua thi hut duoc tu xa vo ly, hop nho qua thi phai di sat moi an duoc. Day
/// khong phai chuyen tham my.
///
/// ------------------------------------------------------------------------------------------
/// DAU LA CUBE THUOC DO, DAU LA MODEL
/// ------------------------------------------------------------------------------------------
/// Item la Prefab Variant cua 'Item_Base LvN'. Base co mot object 'Cube' mang CUBE THUOC DO cua
/// hang do (localScale = 0.3 / 0.5 / 1 / 2 / 4 / 10 theo hang).
///
/// CAI BAY: khi lam item that, mesh model duoc thay VAO CHINH object do - object van giu ten 'Cube'
/// nhung mesh ben trong da la chair_001 / boat_001 / car_001... Nhan dang thuoc do bang TEN object
/// la sai, va sai kieu im lang: 11 item bi bao "khong co renderer" roi khong bao gio duoc sua, con
/// 17 con xe thi bi cham nham vao hop cua BANH XE.
///
/// Nen o day thuoc do duoc nhan bang MESH: mesh primitive dung san cua Unity (nam trong
/// 'Library/unity default resources'). Prefab ma MOI renderer deu la mesh primitive thi do la KHUON
/// (Item_Base...), khong phai item - bo qua.
///
/// ------------------------------------------------------------------------------------------
/// ITEM CON (BANH XE)
/// ------------------------------------------------------------------------------------------
/// Xe co 4 'Wheel' con, moi banh co Rigidbody + PhysicsDevourable + collider RIENG - an duoc rieng.
/// Collider cua banh KHONG phai collider thua. Moi renderer/collider deu duoc quy ve PhysicsDevourable
/// gan nhat tinh nguoc len; chi nhung cai thuoc ve CHINH root moi duoc do va duoc don.
///
/// ------------------------------------------------------------------------------------------
/// DO TRONG LOCAL SPACE CUA HOP
/// ------------------------------------------------------------------------------------------
/// BoxCollider.center/size nam trong local space cua object mang no. Nhieu item co mesh xoay 270
/// (Bottle, Can, Trash_01/02, barbershop_001), nen so 'size x lossyScale' voi AABB thegioi la so hai
/// dai luong khac he - bao lech gia, va neu ghi de thi collider thanh sai THAT.
///
/// Vi vay bounds duoc gom bang 8 goc mesh.bounds -> world -> LOCAL SPACE CUA HOP. Ket qua gan thang
/// vao center/size, khong can chia lossyScale.
///
/// Dung PrefabUtility.LoadPrefabContents nen KHONG dung toi scene dang mo.
///
/// Chay: Tools/Devour/Kiem tra collider item  (chi bao cao, khong sua gi)
///       Tools/Devour/Sua collider item cho khop
/// </summary>
public static class ItemColliderFitter
{
    private const string MenuRoot = "Tools/Devour/";

    private static readonly string[] Folders =
    {
        "Assets/Devours/Prefabs/Props",
        "Assets/Devours/Prefabs/Items",
    };

    /// <summary>Lech duoi nguong nay (ti le) thi coi nhu da khop, khong bao.</summary>
    private const float Tolerance = 0.05f;

    // ---------------------------------------------------------------- lenh

    [MenuItem(MenuRoot + "Kiem tra collider item", false, 150)]
    private static void CheckMenu() { Debug.Log(Run(false)); }

    [MenuItem(MenuRoot + "Sua collider item cho khop", false, 151)]
    private static void FixMenu()
    {
        if (!EditorUtility.DisplayDialog("Sua collider item",
                "Se GHI DE m_Size/m_Center collider cua moi item trong:\n  " +
                string.Join("\n  ", Folders) +
                "\n\nVa go cac collider THUA cua chinh item do (collider cua item con nhu banh xe\n" +
                "duoc giu nguyen).\n\n" +
                "Chay 'Kiem tra collider item' truoc de xem truoc se doi nhung gi.",
                "Sua", "Thoi")) return;
        Debug.Log(Run(true));
    }

    // ---------------------------------------------------------------- chay

    /// <summary>Chay va TRA VE bao cao. Tach khoi Debug.Log de goi duoc tu script khac.</summary>
    public static string Run(bool apply)
    {
        List<string> paths = CollectItemPaths();

        var sb = new StringBuilder();
        sb.AppendLine(apply ? "[ItemCollider] DA SUA" : "[ItemCollider] KIEM TRA (khong sua gi)");
        sb.AppendLine("prefab | hop hien tai (world) | hop dung (world) | ket qua");
        sb.AppendLine(new string('-', 100));

        int ok = 0, changed = 0, khuon = 0, noCollider = 0, extraRemoved = 0;

        for (int i = 0; i < paths.Count; i++)
        {
            string path = paths[i];
            string shortName = path.Substring("Assets/Devours/Prefabs/".Length);

            EditorUtility.DisplayProgressBar("Collider item", shortName, (float)i / paths.Count);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!HasBodyModel(root)) { khuon++; continue; }

                BoxCollider box = FindTargetBox(root);
                if (box == null)
                {
                    sb.AppendLine(shortName + " | - | - | KHONG CO BoxCollider");
                    noCollider++;
                    continue;
                }

                Bounds want;
                if (!MeasureBody(root, box.transform, out want))
                {
                    sb.AppendLine(shortName + " | - | - | KHONG DO DUOC BOUNDS");
                    noCollider++;
                    continue;
                }

                Transform t = box.transform;
                Vector3 scale = Abs(t.lossyScale);

                Vector3 curWorld = Mul(box.size, scale);
                Vector3 wantWorld = Mul(want.size, scale);
                float dCenter = (t.TransformPoint(box.center) - t.TransformPoint(want.center)).magnitude;
                float allowed = Mathf.Max(0.01f, wantWorld.magnitude * Tolerance);

                bool fits = SizeClose(curWorld, wantWorld) && dCenter <= allowed;
                int extras = CountExtraColliders(root, box);

                if (fits && extras == 0) { ok++; continue; }

                sb.AppendLine(shortName +
                              " | " + Fmt(curWorld) +
                              " | " + Fmt(wantWorld) +
                              " | " + (fits ? "hop dung" : "LECH " + Ratio(curWorld, wantWorld)) +
                              (dCenter > allowed ? "  tam lech " + dCenter.ToString("F2") : "") +
                              (extras > 0 ? "  (+" + extras + " collider thua)" : ""));

                if (!apply) { changed++; continue; }

                // --- ghi: want da o dung local space cua hop
                box.center = want.center;
                box.size = want.size;

                extraRemoved += RemoveExtraColliders(root, box);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        EditorUtility.ClearProgressBar();

        sb.AppendLine(new string('-', 100));
        sb.AppendLine("Tong " + paths.Count + " prefab:  khuon (bo qua) " + khuon +
                      "  |  da khop " + ok +
                      (apply ? "  |  vua sua " : "  |  can sua ") + changed +
                      "  |  khong co collider " + noCollider +
                      (apply && extraRemoved > 0 ? "  |  go " + extraRemoved + " collider thua" : ""));

        if (apply) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }

        return sb.ToString();
    }

    // ---------------------------------------------------------------- do dac

    /// <summary>
    /// Bounds THAN item, tra ve trong LOCAL SPACE cua <paramref name="space"/>. Bo he hat, thu dang
    /// tat, cube thuoc do, va moi thu thuoc ve item con - xem ghi chu dau file.
    /// </summary>
    private static bool MeasureBody(GameObject root, Transform space, out Bounds local)
    {
        local = new Bounds();
        bool has = false;

        Matrix4x4 w2l = space.worldToLocalMatrix;
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < rends.Length; i++)
        {
            Renderer r = rends[i];
            if (!IsBodyRenderer(root, r)) continue;

            // Uu tien mesh.bounds x ma tran cua chinh no: chat hon renderer.bounds (von la AABB
            // the gioi, da noi ra roi) - quan trong voi model xoay.
            Mesh mesh = MeshOf(r);
            Bounds src = mesh != null ? mesh.bounds : r.bounds;
            Matrix4x4 l2w = mesh != null ? r.transform.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 m = w2l * l2w;

            Vector3 c = src.center, e = src.extents;
            for (int k = 0; k < 8; k++)
            {
                Vector3 corner = c + new Vector3(
                    (k & 1) == 0 ? -e.x : e.x,
                    (k & 2) == 0 ? -e.y : e.y,
                    (k & 4) == 0 ? -e.z : e.z);

                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!has) { local = new Bounds(p, Vector3.zero); has = true; }
                else local.Encapsulate(p);
            }
        }
        return has;
    }

    /// <summary>Renderer nay co phai than cua CHINH item nay khong.</summary>
    private static bool IsBodyRenderer(GameObject root, Renderer r)
    {
        if (r == null || r is ParticleSystemRenderer) return false;
        if (!r.enabled || !r.gameObject.activeInHierarchy) return false;
        if (IsRuler(r)) return false;
        return OwnerItem(r.transform) == root.transform;
    }

    /// <summary>Prefab nay co model that khong, hay chi la khuon toan cube thuoc do.</summary>
    private static bool HasBodyModel(GameObject root)
    {
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
            if (IsBodyRenderer(root, rends[i])) return true;
        return false;
    }

    /// <summary>
    /// Cube thuoc do nhan bang MESH chu khong bang ten object - object mang model that van co the
    /// ten 'Cube'. Mesh primitive cua Unity nam trong 'Library/unity default resources'.
    /// </summary>
    private static bool IsRuler(Renderer r)
    {
        Mesh mesh = MeshOf(r);
        if (mesh == null) return false;

        string path = AssetDatabase.GetAssetPath(mesh);
        return string.IsNullOrEmpty(path) || path.StartsWith("Library/");
    }

    private static Mesh MeshOf(Renderer r)
    {
        var smr = r as SkinnedMeshRenderer;
        if (smr != null) return smr.sharedMesh;

        var mf = r.GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }

    /// <summary>Item nao dang so huu transform nay - PhysicsDevourable gan nhat tinh nguoc len.</summary>
    private static Transform OwnerItem(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.GetComponent<PhysicsDevourable>() != null) return p;
        return null;
    }

    /// <summary>Collider nay co thuoc ve CHINH item nay khong (khong phai cua banh xe con).</summary>
    private static bool IsBodyCollider(GameObject root, Collider c)
    {
        return c != null && OwnerItem(c.transform) == root.transform;
    }

    /// <summary>
    /// Collider se duoc chinh: uu tien hop nam ngay tren object mang mesh model TO NHAT - do la than
    /// item. Khong co thi lay hop dau tien thuoc ve chinh item.
    /// </summary>
    private static BoxCollider FindTargetBox(GameObject root)
    {
        Renderer main = null;
        float best = -1f;

        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (!IsBodyRenderer(root, rends[i])) continue;

            float v = rends[i].bounds.size.sqrMagnitude;
            if (v > best) { best = v; main = rends[i]; }
        }

        if (main != null)
        {
            var onMain = main.GetComponent<BoxCollider>();
            if (onMain != null) return onMain;
        }

        BoxCollider[] all = root.GetComponentsInChildren<BoxCollider>(true);
        for (int i = 0; i < all.Length; i++)
            if (IsBodyCollider(root, all[i])) return all[i];

        return null;
    }

    private static int CountExtraColliders(GameObject root, BoxCollider keep)
    {
        Collider[] all = root.GetComponentsInChildren<Collider>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != keep && IsBodyCollider(root, all[i])) n++;
        return n;
    }

    private static int RemoveExtraColliders(GameObject root, BoxCollider keep)
    {
        Collider[] all = root.GetComponentsInChildren<Collider>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == keep || !IsBodyCollider(root, all[i])) continue;
            Object.DestroyImmediate(all[i], true);
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- so hoc

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static Vector3 Mul(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    /// <summary>Hai co bang nhau chua - so theo TI LE tung truc, vi item to nho lech nhau hang tram lan.</summary>
    private static bool SizeClose(Vector3 a, Vector3 b)
    {
        return Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);
    }

    private static bool Near(float a, float b)
    {
        float big = Mathf.Max(Mathf.Abs(a), Mathf.Abs(b));
        if (big < 0.0001f) return true;
        return Mathf.Abs(a - b) / big <= Tolerance;
    }

    private static string Fmt(Vector3 v)
    {
        return "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
    }

    private static string Ratio(Vector3 cur, Vector3 want)
    {
        float c = Mathf.Max(cur.x, Mathf.Max(cur.y, cur.z));
        float w = Mathf.Max(want.x, Mathf.Max(want.y, want.z));
        if (w < 0.0001f) return "?";

        float k = c / w;
        return k >= 1f ? "TO gap " + k.ToString("F1") + "x" : "NHO con " + (k * 100f).ToString("F0") + "%";
    }

    // ---------------------------------------------------------------- gom item

    /// <summary>
    /// Moi prefab co PhysicsDevourable o root. KHONG loc theo ten: 'Item_Base Lv6 Variant*' la item
    /// that (skyscraper) chu khong phai khuon. Khuon duoc loai o HasBodyModel - toan cube thuoc do.
    /// </summary>
    private static List<string> CollectItemPaths()
    {
        var list = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", Folders);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null || go.GetComponent<PhysicsDevourable>() == null) continue;

            list.Add(path);
        }
        list.Sort();
        return list;
    }
}
