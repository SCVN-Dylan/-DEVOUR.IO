using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dung prefab VAT BAY (khinh khi cau / khi cau / may bay) tu model co san trong FBXs/Lv_50.
///
/// Vat bay = Prefab Variant cua Item_Base (y het moi item khac - Rigidbody + PhysicsDevourable)
/// cong them PlaneFlyer de no tu bay vong vong. Khong che kieu prefab moi, khong them manager nao.
///
/// TAI SAO CAN MOT LAYER RIENG: OccluderFade dang de _occluderLayers = Everything va ban tia TU
/// CAMERA XUONG PLAYER. Vat bay o do cao 9u nam dung giua duong tia do moi lan no qua dau player
/// -> se bi lam mo nhap nhay. Tool nay tao layer rieng, dat cho vat bay, roi GO layer do khoi mask
/// cua OccluderFade trong scene dang mo.
///
/// Chay: Tools/Devour/Dung prefab vat bay
/// </summary>
public static class PlaneFlyerBuilder
{
    private const string MenuRoot = "Tools/Devour/";

    private const string ModelFolder = "Assets/ithappy/Megacity/Traffic/Prefabs/Cars/Planes";
    private const string ItemFolder = "Assets/Devours/Prefabs/Items";

    /// <summary>
    /// Base HANG E. Moi hang co mot base rieng trong Prefabs/Props/Lv_N va da mang san bo so
    /// requiredLevel/xp/score cua hang do - Lv5 = 110/12/22. Lam variant cua no thi KHONG PHAI
    /// ghi de so nao ca, doi bang hang ve sau la vat bay tu theo. Dung Items/Item_Base.prefab
    /// (hang A, 1/1/1) roi ghi de tay la tu tach minh ra khoi bang hang.
    /// </summary>
    private const string BasePath = "Assets/Devours/Prefabs/Props/Lv_5/Item_Base Lv5.prefab";

    /// <summary>Cube thuoc do trong base - variant phai tat no di, y het Item_Lv5_Car dang lam.</summary>
    private const string RulerCube = "Cube";

    private const string FlyLayer = "Flying";

    /// <summary>Model nao duoc coi la vat bay. Doi list nay la doi luon danh sach prefab dung ra.</summary>
    private static readonly string[] ModelNames =
    {
        "airship_001", "airship_002", "air_balloon_001", "air_balloon_002",
    };

    // --- so mac dinh, khop voi spec da chot ---

    /// <summary>
    /// Do cao bay. KHOA voi requiredLevel cua base (hang E = 110).
    ///
    /// Non hut toa NGANG, nua goc 30 do, nen do cao voi toi duoc = 0.5 x tam hut + chieu cao mom:
    ///   Lv50  -> 3.9u   |  Lv90  -> 4.4u   |  Lv110 -> 8.4u   |  Lv250 -> 17.7u
    /// Lay 7.5 chu khong phai 8.4: cong them heightWobble (0.6) thi dinh song moi la 8.1, van con
    /// duoi tam voi. Dat sat 8.4 thi nua so vong bay vat se nhap len ngoai tam, nguoi choi chia mom
    /// dung cho ma khong hut duoc - trong nhu bug.
    /// </summary>
    private const float FlyHeight = 7.5f;

    private const float Speed = 4f;
    private const float TurnRate = 40f;

    /// <summary>
    /// Chieu dai lon nhat cua than sau khi scale (world). Lay 4 cho bang CUBE THUOC DO cua base
    /// hang E (Cube scale 4) - do la co chuan cua hang nay theo bang item, khong phai so tu nghi ra.
    /// </summary>
    private const float BodySize = 4f;

    // ---------------------------------------------------------------- lenh menu

    [MenuItem(MenuRoot + "Dung prefab vat bay", false, 140)]
    private static void Build()
    {
        GameObject baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        if (baseAsset == null)
        {
            EditorUtility.DisplayDialog("Khong thay Item_Base",
                "Khong doc duoc '" + BasePath + "'.\n\nItem_Base la prefab goc cua moi item - " +
                "khong co no thi khong dung variant duoc.", "OK");
            return;
        }

        List<GameObject> models = FindModels();
        if (models.Count == 0)
        {
            EditorUtility.DisplayDialog("Khong thay model vat bay",
                "Khong thay model nao trong '" + ModelFolder + "' khop danh sach:\n  " +
                string.Join(", ", ModelNames), "OK");
            return;
        }

        int layer = LayerMask.NameToLayer(FlyLayer);
        bool needLayer = layer < 0;

        OccluderFade occ = Object.FindFirstObjectByType<OccluderFade>();
        Renderer ground;
        bool hasGround = FindGround(out ground);

        string msg = "Se dung " + models.Count + " prefab vat bay trong '" + ItemFolder + "':\n";
        for (int i = 0; i < models.Count; i++) msg += "  Item_Fly_" + models[i].name + "\n";
        msg += "\nbase = 'Item_Base Lv5' (hang E: requiredLevel 110, xp 12, score 22 - THUA KE, khong ghi de)" +
               "\ndo cao bay = " + FlyHeight + " (Lv110 voi toi 8.4u)  |  toc do = " + Speed + "  |  turnRate = " + TurnRate +
               "\nthan scale ve dai nhat = " + BodySize + " (bang cube thuoc do cua hang E)" +
               "\n+ Tat cube thuoc do trong tung variant" +
               "\n+ Gan StageReveal: chua du hang thi chi thay BONG, du hang moi hien mesh" +
               "\n+ BAT do bong (bat buoc - bong la thu duy nhat thay khi chua du hang)\n";

        msg += needLayer
            ? "\n+ TAO layer moi '" + FlyLayer + "' (ghi vao TagManager)\n"
            : "\n+ Dung layer '" + FlyLayer + "' co san\n";

        msg += occ != null
            ? "+ GO layer do khoi _occluderLayers cua OccluderFade trong scene (chong nhap nhay)\n"
            : "+ Scene dang mo khong co OccluderFade - bo qua buoc go mask\n";

        msg += hasGround
            ? "+ Lay tam/ban kinh map tu '" + ground.name + "'\n"
            : "+ Khong thay Ground_Circle - de tam map o goc toa do, ban tu chinh sau\n";

        msg += "\nPrefab trung ten se bi GHI DE.";

        if (!EditorUtility.DisplayDialog("Dung prefab vat bay", msg, "Dung", "Thoi")) return;

        if (needLayer)
        {
            layer = CreateLayer(FlyLayer);
            if (layer < 0)
            {
                EditorUtility.DisplayDialog("Het cho layer",
                    "32 layer da dung het, khong tao duoc '" + FlyLayer + "'.\n\n" +
                    "Xoa bot mot layer trong Project Settings > Tags and Layers roi chay lai.", "OK");
                return;
            }
        }

        Transform center = hasGround ? ground.transform : null;
        float radius = hasGround ? Mathf.Max(ground.bounds.extents.x, ground.bounds.extents.z) : 35f;

        int made = 0;
        for (int i = 0; i < models.Count; i++)
        {
            if (CreateFlyer(models[i], baseAsset, layer, center, radius) != null) made++;
        }

        int cleared = occ != null ? ClearOccluderBit(occ, layer) : -1;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string done = "[PlaneFlyer] Da dung " + made + " prefab vat bay trong " + ItemFolder +
                      ", layer '" + FlyLayer + "' (" + layer + "), ban kinh map " + radius.ToString("F1") + ".";
        if (cleared == 1) done += " Da go layer do khoi OccluderFade.";
        else if (cleared == 0) done += " OccluderFade von da khong tinh layer do.";
        Debug.Log(done);
    }

    [MenuItem(MenuRoot + "Tha mot vat bay vao scene", false, 141)]
    private static void DropOne()
    {
        string[] guids = AssetDatabase.FindAssets("Item_Fly_ t:Prefab", new[] { ItemFolder });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Chua co prefab vat bay",
                "Chay 'Tools/Devour/Dung prefab vat bay' truoc da.", "OK");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[Random.Range(0, guids.Length)]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        Renderer ground;
        Vector3 c = FindGround(out ground) ? ground.bounds.center : Vector3.zero;
        go.transform.position = new Vector3(c.x, FlyHeight, c.z);

        Undo.RegisterCreatedObjectUndo(go, "Tha vat bay");
        Selection.activeGameObject = go;
        Debug.Log("[PlaneFlyer] Da tha '" + go.name + "' vao scene o do cao " + FlyHeight + ".");
    }

    // ---------------------------------------------------------------- dung prefab

    private static GameObject CreateFlyer(GameObject model, GameObject baseAsset, int layer,
                                          Transform center, float radius)
    {
        string path = ItemFolder + "/Item_Fly_" + model.name + ".prefab";

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
        inst.name = "Item_Fly_" + model.name;

        Transform slot = inst.transform.Find("Model");
        if (slot == null) slot = inst.transform;

        GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(model, slot);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = Vector3.one;

        PrepModel(m);

        // Tat cube thuoc do cua base - khong tat thi variant deo nguyen khoi lap phuong 4u ben canh
        // model. Go luon renderer/mesh/collider cua no cho khoi con gi tham gia physics.
        HideRuler(inst);

        // KHONG ghi de requiredLevel/xp/score: base 'Item_Base Lv5' da la hang E (110/12/22).
        PhysicsDevourable pd = inst.GetComponent<PhysicsDevourable>();

        PlaneFlyer flyer = inst.GetComponent<PlaneFlyer>();
        if (flyer == null) flyer = inst.AddComponent<PlaneFlyer>();
        ApplyFlyerSettings(flyer, pd, inst.GetComponent<Rigidbody>(), layer, center, radius);

        // An mesh cho toi khi player du hang - chi thay bong truot tren dat.
        if (inst.GetComponent<StageReveal>() == null) inst.AddComponent<StageReveal>();

        SetLayerRecursive(inst, layer);
        EnableShadows(inst);

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
        return asset;
    }

    /// <summary>
    /// PlaneFlyer de field private + [SerializeField] (theo lo^i quen cua project) nen phai ghi
    /// qua SerializedObject chu khong gan thang duoc.
    /// </summary>
    private static void ApplyFlyerSettings(PlaneFlyer flyer, PhysicsDevourable pd, Rigidbody rb,
                                           int layer, Transform center, float radius)
    {
        SerializedObject so = new SerializedObject(flyer);

        so.FindProperty("_item").objectReferenceValue = pd;
        so.FindProperty("_rb").objectReferenceValue = rb;
        so.FindProperty("_mapCenter").objectReferenceValue = center;
        so.FindProperty("_mapRadius").floatValue = radius;
        so.FindProperty("_flyHeight").floatValue = FlyHeight;
        so.FindProperty("_speed").floatValue = Speed;
        so.FindProperty("_turnRate").floatValue = TurnRate;

        // Bo chinh layer cua minh ra khoi mask do vat can - khong thi no tu do trung chinh no.
        so.FindProperty("_obstacleLayers").intValue = ~(1 << layer);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Don model va boc mot BoxCollider quanh no - giong PrepModel cua ItemLevelTestBuilder,
    /// them buoc CHUAN HOA KICH THUOC vi model goc trong Megacity to nho rat lech nhau.
    /// </summary>
    private static void PrepModel(GameObject go)
    {
        DestroyAll(go.GetComponentsInChildren<Rigidbody>(true));
        DestroyAll(go.GetComponentsInChildren<Collider>(true));

        Transform[] kids = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < kids.Length; i++)
            GameObjectUtility.SetStaticEditorFlags(kids[i].gameObject, 0);

        Bounds b;
        if (!MeasureRenderers(go, out b)) return;

        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f)
        {
            float k = BodySize / longest;
            go.transform.localScale = go.transform.localScale * k;
            MeasureRenderers(go, out b);   // do lai sau khi scale, khong thi collider sai co
        }

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = go.transform.InverseTransformPoint(b.center);
        bc.size = go.transform.InverseTransformVector(b.size);
    }

    // ---------------------------------------------------------------- lat vat

    /// <summary>Go bit cua layer khoi _occluderLayers. Tra 1 = vua go, 0 = von da khong co.</summary>
    private static int ClearOccluderBit(OccluderFade occ, int layer)
    {
        SerializedObject so = new SerializedObject(occ);
        SerializedProperty p = so.FindProperty("_occluderLayers");
        if (p == null) return 0;

        int bit = 1 << layer;
        if ((p.intValue & bit) == 0) return 0;

        p.intValue = p.intValue & ~bit;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(occ);
        return 1;
    }

    /// <summary>Them mot layer vao o trong dau tien tu 8 tro di. Tra ve chi so, -1 neu het cho.</summary>
    private static int CreateLayer(string name)
    {
        SerializedObject tag = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tag.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty s = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(s.stringValue)) continue;
            s.stringValue = name;
            tag.ApplyModifiedProperties();
            return i;
        }
        return -1;
    }

    /// <summary>Tat cube thuoc do (child 'Cube' cua Model) - variant nao cung phai lam buoc nay.</summary>
    private static void HideRuler(GameObject root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != RulerCube) continue;

            GameObject cube = all[i].gameObject;
            DestroyAll(cube.GetComponents<Collider>());
            DestroyAll(cube.GetComponents<Renderer>());
            DestroyAll(cube.GetComponents<MeshFilter>());
            cube.SetActive(false);
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        Transform[] all = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
    }

    /// <summary>
    /// BAT do bong - va day la bat buoc, khong phai tuy chon.
    ///
    /// StageReveal an mesh bang shadowCastingMode = ShadowsOnly, tuc CAI BONG chinh la thu duy nhat
    /// nguoi choi thay khi chua du hang. Tat do bong o day la an luon ca bong -> item bien mat sach,
    /// khong con gi bao hieu tren dat.
    ///
    /// receiveShadows thi tat: mot vat tren troi khong can an bong cua toa nha ha xuong no.
    /// </summary>
    private static void EnableShadows(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            rends[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rends[i].receiveShadows = false;
        }
    }

    private static List<GameObject> FindModels()
    {
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < ModelNames.Length; i++)
        {
            string p = ModelFolder + "/" + ModelNames[i] + ".prefab";
            GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (g != null) list.Add(g);
        }
        return list;
    }

    private static bool FindGround(out Renderer ground)
    {
        ground = null;
        GameObject g = GameObject.Find("Ground_Circle");
        if (g == null) return false;
        ground = g.GetComponentInChildren<Renderer>();
        return ground != null;
    }

    private static bool MeasureRenderers(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds(go.transform.position, Vector3.zero);
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null || rends[i] is ParticleSystemRenderer) continue;
            if (!has) { bounds = rends[i].bounds; has = true; }
            else bounds.Encapsulate(rends[i].bounds);
        }
        return has;
    }

    private static void DestroyAll(Component[] comps)
    {
        for (int i = 0; i < comps.Length; i++)
            if (comps[i] != null) Object.DestroyImmediate(comps[i]);
    }
}
