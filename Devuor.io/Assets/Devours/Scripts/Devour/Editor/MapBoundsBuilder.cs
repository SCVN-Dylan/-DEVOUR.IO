using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dung TUONG BAO vo hinh quanh ria map, de item va sinh vat khong van ra ngoai roi rot xuong hu vo.
///
/// VI SAO KHONG PHAI MOT COLLIDER DUY NHAT: map la HINH TRON (Ground_Circle). BoxCollider thi vuong,
/// ma Unity khong co collider "hop rong ruot" - MeshCollider lom (concave) thi khong dung lam vat
/// chan dong duoc. Nen o day rai N hop nho theo mot da giac deu quanh vong tron: re, chinh xac du,
/// va van la static collider (khong Rigidbody) nen PhysX khong ton gi de mo phong.
///
/// Sai so cua da giac 32 canh o ban kinh 60 chi la 0.29 don vi - khong the nhin thay.
///
/// TUONG NAY CHAN CA BOT: AIController ne vat can bang 3 tia rau va BO QUA moi thu co Rigidbody,
/// ma tuong thi khong co - nen bot tu dong biet ne ria map thay vi ui thang ra ngoai.
///
/// Chay lai bao nhieu lan cung duoc: lan sau tu xoa cai cu roi dung lai.
///
/// Menu: Tools/Devour/Dung tuong bao quanh map
/// </summary>
public static class MapBoundsBuilder
{
    private const string MenuRoot = "Tools/Devour/";
    private const string RootName = "MapBounds";

    /// <summary>So canh cua da giac. Cao hon = bam vong tron sat hon, doi lai them collider.</summary>
    private const int Segments = 32;

    /// <summary>Cao bao nhieu tinh tu mat dat len. Item bi hut bay ngang la chinh, 30 la du thua.</summary>
    private const float WallHeight = 30f;

    /// <summary>Day tuong. Mong qua thi vat bay nhanh co the xuyen qua giua hai buoc physics.</summary>
    private const float WallThickness = 2f;

    /// <summary>Lui vao trong so voi mep dat, de vat cham tuong TRUOC khi loi ra khoi phan nhin thay.</summary>
    private const float InsetFromEdge = 0.5f;

    /// <summary>Chon them xuong duoi mat dat, chan cua vat truot sat dat lot qua chan tuong.</summary>
    private const float SinkBelowGround = 2f;

    /// <summary>Noi rong be ngang moi hop, de hai hop lien tiep GIAO nhau chu khong ho khe o goc.</summary>
    private const float SegmentOverlap = 1.15f;

    // ---------------------------------------------------------------- lenh menu

    [MenuItem(MenuRoot + "Dung tuong bao quanh map", false, 130)]
    private static void Build()
    {
        Renderer ground;
        if (!FindGround(out ground))
        {
            EditorUtility.DisplayDialog("Khong tim thay mat dat",
                "Khong thay object ten 'Ground_Circle', cung khong thay renderer nao du lon de coi la mat dat.\n\n" +
                "Mo scene co map roi chay lai.", "OK");
            return;
        }

        Bounds b = ground.bounds;
        float radius = Mathf.Max(b.extents.x, b.extents.z);
        float top = b.max.y;
        Vector3 center = new Vector3(b.center.x, 0f, b.center.z);

        if (!EditorUtility.DisplayDialog("Dung tuong bao quanh map",
                "Mat dat: " + ground.name + "\n" +
                "  tam = (" + center.x.ToString("F1") + ", " + center.z.ToString("F1") + ")\n" +
                "  ban kinh = " + radius.ToString("F1") + "\n" +
                "  mat tren y = " + top.ToString("F2") + "\n\n" +
                "Se dung " + Segments + " hop chan, cao " + WallHeight + ", day " + WallThickness + ".\n" +
                "Tuong cu (neu co) se bi xoa truoc.",
                "Dung", "Huy"))
            return;

        Remove(false);

        GameObject root = new GameObject(RootName);
        root.transform.position = center;
        Undo.RegisterCreatedObjectUndo(root, "Dung tuong bao quanh map");

        // Mep TRONG cua tuong nam o day; tam hop phai day ra them nua be day
        float innerR = radius - InsetFromEdge;
        float centerR = innerR + WallThickness * 0.5f;

        // Day cung cua mot canh da giac noi tiep. Nhan them SegmentOverlap de hai hop lien tiep
        // chong len nhau - khong co no thi moi goi noi la mot khe hep, va vat nho lot qua duoc.
        float chord = 2f * innerR * Mathf.Sin(Mathf.PI / Segments) * SegmentOverlap;

        float height = WallHeight + SinkBelowGround;
        float centerY = top + (WallHeight - SinkBelowGround) * 0.5f;

        for (int i = 0; i < Segments; i++)
        {
            float ang = (360f / Segments) * i;
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;

            GameObject seg = new GameObject("Wall_" + i.ToString("00"));
            seg.transform.SetParent(root.transform, false);
            seg.transform.position = center + dir * centerR + Vector3.up * centerY;

            // +Z cua hop huong RA NGOAI -> be day nam theo Z, be ngang nam theo X
            seg.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            BoxCollider col = seg.AddComponent<BoxCollider>();
            col.size = new Vector3(chord, height, WallThickness);

            GameObjectUtility.SetStaticEditorFlags(seg, StaticEditorFlags.BatchingStatic);
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[MapBounds] Da dung " + Segments + " hop chan quanh '" + ground.name +
                  "', ban kinh trong " + innerR.ToString("F1") + ", cao " + WallHeight + ". Nho LUU SCENE.");
    }

    [MenuItem(MenuRoot + "Xoa tuong bao quanh map", false, 131)]
    private static void RemoveMenu() { Remove(true); }

    // ---------------------------------------------------------------- phu tro

    private static void Remove(bool log)
    {
        int n = 0;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null || go.name != RootName || go.transform.parent != null) continue;
            Undo.DestroyObjectImmediate(go);
            n++;
        }
        if (log) Debug.Log("[MapBounds] Da xoa " + n + " tuong bao.");
    }

    /// <summary>
    /// Tim mat dat: uu tien object ten 'Ground_Circle'. Khong co thi lay renderer co DIEN TICH XZ
    /// lon nhat ma van DET (cao khong qua 1/4 be ngang) - de khong nham vao mot toa cao oc.
    /// </summary>
    private static bool FindGround(out Renderer ground)
    {
        ground = null;
        float bestArea = 0f;

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r == null || r is ParticleSystemRenderer) continue;

            if (r.gameObject.name == "Ground_Circle") { ground = r; return true; }

            Bounds b = r.bounds;
            float w = Mathf.Max(b.size.x, b.size.z);
            if (w < 10f) continue;                 // qua nho, khong phai mat dat
            if (b.size.y > w * 0.25f) continue;    // qua cao so voi be ngang -> la nha, khong phai dat

            float area = b.size.x * b.size.z;
            if (area > bestArea) { bestArea = area; ground = r; }
        }
        return ground != null;
    }
}
