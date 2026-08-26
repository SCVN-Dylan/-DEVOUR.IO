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
/// COLLIDER NAM O DAU
/// ------------------------------------------------------------------------------------------
/// Item la Prefab Variant cua 'Item_Base LvN'. Collider cua base nam tren object 'Cube' - chinh la
/// CUBE THUOC DO cua hang do (localScale = 0.3 / 0.5 / 1 / 2 / 4 / 10 theo hang).
///
/// Nen co collider trong world = m_Size x scale cua Cube. Doc so m_Size tho trong file .prefab ma
/// khong nhan scale la doc sai - day la ly do tool nay phai chay trong Unity chu khong doc file.
///
/// ------------------------------------------------------------------------------------------
/// DO CAI GI
/// ------------------------------------------------------------------------------------------
/// Bounds cua MOI renderer dang thuc su hien, TRU:
///   - ParticleSystemRenderer (khong co vo)
///   - renderer tat, hoac nam tren GameObject dang tat
///   - CHINH CUBE THUOC DO: no la thuoc do, khong phai than item. Do ca no thi hop se om lay cai
///     cube chu khong om model - sai hoan toan, va sai mot cach rat kho nhin ra.
///
/// ------------------------------------------------------------------------------------------
/// SUA THE NAO
/// ------------------------------------------------------------------------------------------
/// Ghi de m_Size/m_Center cua DUNG collider co san (kieu Lv_1 dang lam), va go moi collider THUA
/// tren cac object con. Moi item chi con DUNG MOT hop - khong bao giờ co hai hop chong nhau.
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

    /// <summary>Ten object mang cube thuoc do - do khong phai than item, khong duoc tinh vao bounds.</summary>
    private const string RulerName = "Cube";

    /// <summary>Lech duoi nguong nay (ti le) thi coi nhu da khop, khong bao.</summary>
    private const float Tolerance = 0.05f;

    // ---------------------------------------------------------------- lenh

    [MenuItem(MenuRoot + "Kiem tra collider item", false, 150)]
    private static void CheckMenu() { Run(false); }

    [MenuItem(MenuRoot + "Sua collider item cho khop", false, 151)]
    private static void FixMenu()
    {
        if (!EditorUtility.DisplayDialog("Sua collider item",
                "Se GHI DE m_Size/m_Center collider cua moi item trong:\n  " +
                string.Join("\n  ", Folders) +
                "\n\nVa go cac collider THUA tren object con (moi item chi con 1 hop).\n\n" +
                "Chay 'Kiem tra collider item' truoc de xem truoc se doi nhung gi.",
                "Sua", "Thoi")) return;
        Run(true);
    }

    // ---------------------------------------------------------------- chay

    private static void Run(bool apply)
    {
        List<string> paths = CollectItemPaths();
        if (paths.Count == 0)
        {
            Debug.LogWarning("[ItemCollider] Khong tim thay item nao (prefab co PhysicsDevourable) trong:\n  " +
                             string.Join("\n  ", Folders));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(apply ? "[ItemCollider] DA SUA" : "[ItemCollider] KIEM TRA (khong sua gi)");
        sb.AppendLine("prefab | hop hien tai (world) | hop dung (world) | ket qua");
        sb.AppendLine(new string('-', 100));

        int ok = 0, changed = 0, noRenderer = 0, noCollider = 0, extraRemoved = 0;

        for (int i = 0; i < paths.Count; i++)
        {
            string path = paths[i];
            string shortName = path.Substring("Assets/Devours/Prefabs/".Length);

            EditorUtility.DisplayProgressBar("Collider item", shortName, (float)i / paths.Count);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                BoxCollider box = FindTargetBox(root);
                if (box == null)
                {
                    sb.AppendLine(shortName + " | - | - | KHONG CO BoxCollider");
                    noCollider++;
                    continue;
                }

                Bounds want;
                if (!MeasureBody(root, out want))
                {
                    sb.AppendLine(shortName + " | - | - | KHONG CO RENDERER NAO (ngoai cube thuoc do)");
                    noRenderer++;
                    continue;
                }

                Vector3 curSizeWorld = WorldSize(box);
                Vector3 wantSizeWorld = want.size;

                bool fits = SizeClose(curSizeWorld, wantSizeWorld)
                         && CenterClose(BoxWorldCenter(box), want.center,
                                        Mathf.Max(0.01f, wantSizeWorld.magnitude * Tolerance));

                int extras = CountExtraColliders(root, box);

                if (fits && extras == 0) { ok++; continue; }

                sb.AppendLine(shortName +
                              " | " + Fmt(curSizeWorld) +
                              " | " + Fmt(wantSizeWorld) +
                              " | " + (fits ? "hop dung" : "LECH " + Ratio(curSizeWorld, wantSizeWorld)) +
                              (extras > 0 ? "  (+" + extras + " collider thua)" : ""));

                if (!apply) { changed++; continue; }

                // --- ghi
                Transform t = box.transform;
                box.center = t.InverseTransformPoint(want.center);
                box.size = DivideScale(want.size, t.lossyScale);

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
        sb.AppendLine("Tong " + paths.Count + " item:  da khop " + ok +
                      (apply ? "  |  vua sua " : "  |  can sua ") + changed +
                      "  |  khong co renderer " + noRenderer +
                      "  |  khong co collider " + noCollider +
                      (apply && extraRemoved > 0 ? "  |  go " + extraRemoved + " collider thua" : ""));

        if (apply) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }

        Debug.Log(sb.ToString());
    }

    // ---------------------------------------------------------------- do dac

    /// <summary>
    /// Bounds THAN item trong world. BO cube thuoc do, he hat, va moi thu dang tat - xem ghi chu
    /// "DO CAI GI" o dau file.
    /// </summary>
    private static bool MeasureBody(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(root.transform.position, Vector3.zero);
        bool has = false;

        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            Renderer r = rends[i];
            if (r == null || r is ParticleSystemRenderer) continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (IsRuler(r.transform)) continue;

            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }

    /// <summary>Object nay (hoac cha no) co phai cube thuoc do khong.</summary>
    private static bool IsRuler(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name == RulerName) return true;
        return false;
    }

    /// <summary>
    /// Collider se duoc chinh: uu tien cai nam tren cube thuoc do (cai thua ke tu Item_Base - dung
    /// kieu ma Lv_1 dang lam). Khong co thi lay BoxCollider dau tien tim duoc.
    /// </summary>
    private static BoxCollider FindTargetBox(GameObject root)
    {
        BoxCollider[] all = root.GetComponentsInChildren<BoxCollider>(true);
        for (int i = 0; i < all.Length; i++)
            if (IsRuler(all[i].transform)) return all[i];

        return all.Length > 0 ? all[0] : null;
    }

    private static int CountExtraColliders(GameObject root, BoxCollider keep)
    {
        Collider[] all = root.GetComponentsInChildren<Collider>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i] != keep) n++;
        return n;
    }

    private static int RemoveExtraColliders(GameObject root, BoxCollider keep)
    {
        Collider[] all = root.GetComponentsInChildren<Collider>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i] == keep) continue;
            Object.DestroyImmediate(all[i], true);
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- so hoc

    private static Vector3 WorldSize(BoxCollider b)
    {
        Vector3 s = b.transform.lossyScale;
        return new Vector3(b.size.x * Mathf.Abs(s.x), b.size.y * Mathf.Abs(s.y), b.size.z * Mathf.Abs(s.z));
    }

    private static Vector3 BoxWorldCenter(BoxCollider b)
    {
        return b.transform.TransformPoint(b.center);
    }

    private static Vector3 DivideScale(Vector3 v, Vector3 scale)
    {
        return new Vector3(
            v.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            v.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            v.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }

    /// <summary>Hai co bang nhau chua - so theo TI LE tung truc, vi item to nho lech nhau hang tram lan.</summary>
    private static bool SizeClose(Vector3 a, Vector3 b)
    {
        return Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);
    }

    /// <summary>Hai tam co trung nhau chua - so theo KHOANG CACH tuyet doi.</summary>
    private static bool CenterClose(Vector3 a, Vector3 b, float allowed)
    {
        return (a - b).sqrMagnitude <= allowed * allowed;
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

    private static List<string> CollectItemPaths()
    {
        var list = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", Folders);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);

            // Base la khuon (cube thuoc do la than cua no - dung sua). Fly tu quan ly collider rieng.
            if (file.StartsWith("Item_Base") || file.StartsWith("Item_Fly")) continue;

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null || go.GetComponent<PhysicsDevourable>() == null) continue;

            list.Add(path);
        }
        list.Sort();
        return list;
    }
}
