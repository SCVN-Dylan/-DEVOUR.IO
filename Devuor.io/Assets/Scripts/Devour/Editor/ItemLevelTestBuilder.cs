using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dung scene TEST CHOI DUOC cho he hut MOI (SimpleSuction + PhysicsDevourable):
///
///   Item_Base.prefab       : root "Item" co Rigidbody + PhysicsDevourable + node "Model" (slot).
///   Item_&lt;model&gt;.prefab     : Prefab Variant cua Item_Base, cam model rieng vao slot + 1 BoxCollider
///                            bao quanh (bo MeshCollider cu vi non-convex khong hop Rigidbody dong).
///
/// Player: go het hut cu (MouthSuction/PlayerLevel/PlayerGrowth), gan SimpleSuction (non truoc mat,
/// len cap thi scale + non no ra). Item scale theo co mieng (range): L1 = 10% range, L10 = 80% range.
///
/// Scene: nhan ban rig choi duoc tu Main, xoa Map, + san phang + 10 khu (moi khu 5 item variant,
/// requiredLevel = so cua khu). Bam Play: lai Player an khu level 1 -> len cap -> hut duoc khu cao hon.
///
/// Chay: Tools/Devour/Dung scene test Item theo level
/// </summary>
public static class ItemLevelTestBuilder
{
    private const string MenuRoot = "Tools/Devour/";
    private const string MainPath = "Assets/Scenes/Main.unity";
    private const string ScenePath = "Assets/Scenes/ItemLevelTest.unity";

    private const string ItemFolderParent = "Assets/Prefabs";
    private const string ItemFolder = "Assets/Prefabs/Items";
    private const string BasePath = "Assets/Prefabs/Items/Item_Base.prefab";

    private static readonly string[] PropFolders =
    {
        "Assets/Adrift Team/Food Pack - Low Poly Assets/Prefabs",
        "Assets/ithappy/Megacity/Prefabs/Props",
        "Assets/Prefabs/Props",
        "Assets/Prefabs/Buildings",
    };

    private const float MaxNaturalRadius = 6f;

    private const int Levels = 10;
    private const int ItemsPerLevel = 5;
    private const int Cols = 5;

    // Co mieng (tam hut) luc level 1. Item scale theo con so nay cho on dinh.
    private const float SuctionRange = 4f;
    private const float RangeFracL1 = 0.10f;   // item L1  = 10% tam hut
    private const float RangeFracL10 = 0.80f;  // item L10 = 80% tam hut

    private const float PatchThick = 0.06f;
    private const float PatchTop = 0.04f;

    private static Vector3 _camPos;

    // ---------------------------------------------------------------- lenh menu

    [MenuItem(MenuRoot + "Dung scene test Item theo level", false, 120)]
    private static void BuildWithDialog()
    {
        if (!EditorUtility.DisplayDialog(
            "Dung scene test Item (he hut moi)",
            "1) Tao prefab Item trong " + ItemFolder + " (Item_Base + variant, co Rigidbody + PhysicsDevourable).\n"
            + "2) Nhan ban rig tu Main -> ghi de " + ScenePath + ", xoa Map, + san + " + Levels + " khu.\n"
            + "3) Player: go hut cu, gan SimpleSuction (non truoc mat).\n\n"
            + "Item co physic + ngu/thuc, scale theo co mieng (L1=10%, L10=80% range).",
            "Dung", "Thoi"))
            return;

        BuildPlayable();
    }

    public static void BuildPlayable()
    {
        List<Piece> pool = LoadPool();
        if (pool.Count == 0)
        {
            Debug.LogError("[ItemLevelTest] Khong tim thay prefab nao trong cac pack.");
            return;
        }

        Piece[][] picks = new Piece[Levels][];
        HashSet<GameObject> unique = new HashSet<GameObject>();
        for (int level = 1; level <= Levels; level++)
        {
            List<Piece> p = PickForLevel(pool, level);
            picks[level - 1] = p.ToArray();
            for (int i = 0; i < p.Count; i++) unique.Add(p[i].prefab);
        }

        // --- 1) He prefab Item ---
        EnsureFolder();
        GameObject baseAsset = CreateBaseTemplate();
        Dictionary<GameObject, GameObject> variant = new Dictionary<GameObject, GameObject>();
        int variantCount = 0;
        foreach (GameObject model in unique)
        {
            GameObject v = CreateVariant(model, baseAsset);
            if (v != null) { variant[model] = v; variantCount++; }
        }
        AssetDatabase.SaveAssets();

        // --- 2) Nhan ban rig choi duoc tu Main ---
        Scene main = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(main, ScenePath, true);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int removed = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name.StartsWith("Map")) { UnityEngine.Object.DestroyImmediate(roots[i]); removed++; }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -1f, 0f);
        ground.transform.localScale = new Vector3(400f, 2f, 400f);
        SetMaterial(ground, "Assets/Materials/M_Sidewalk.mat");
        GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

        // --- 3) Player: go hut cu + gan SimpleSuction ---
        GameObject player = FindPlayer();
        if (player != null) SetupPlayer(player, SuctionRange);

        // --- Kich thuoc item theo co mieng ---
        float r1 = SuctionRange * RangeFracL1;
        float r10 = SuctionRange * RangeFracL10;
        float growth = Mathf.Pow(r10 / r1, 1f / (Levels - 1));

        float maxR = r1 * Mathf.Pow(growth, Levels - 1);
        float maxPitch = ClusterPitch(maxR);
        float cellX = 2f * maxPitch + 2f * maxR + 4f;
        float cellZ = 1.5f * maxPitch + 2f * maxR + 4f;
        int rows = Mathf.CeilToInt((float)Levels / Cols);

        float gridW = Cols * cellX, gridDepth = rows * cellZ;
        Vector3 gridCenter = new Vector3(0f, 0f, -(rows - 1) * cellZ * 0.5f);
        float frameRadius = new Vector2(gridW, gridDepth).magnitude * 0.5f + 2f;

        Random.InitState(20260808);
        GameObject rootGO = new GameObject("ItemZones");
        Material[] floor = LoadFloorPalette();
        int itemCount = 0;
        var sizeLog = new System.Text.StringBuilder();

        for (int level = 1; level <= Levels; level++)
        {
            int idx = level - 1, col = idx % Cols, rowi = idx / Cols;
            float targetR = r1 * Mathf.Pow(growth, level - 1);
            float pitch = ClusterPitch(targetR);
            Vector3 center = new Vector3((col - (Cols - 1) * 0.5f) * cellX, 0f, -rowi * cellZ);

            GameObject zoneGO = new GameObject("Zone_LV" + level.ToString("00"));
            zoneGO.transform.SetParent(rootGO.transform, false);
            zoneGO.transform.position = center;
            Transform zone = zoneGO.transform;

            float patchW = 2f * pitch + 2f * targetR + 1.2f;
            float patchD = 1.5f * pitch + 2f * targetR + 1.2f;
            BuildPatch(zone, center, patchW, patchD, floor[(level - 1) % floor.Length]);
            BuildLabel(zone, center, patchD, level, targetR * 2f);

            Vector3[] slots =
            {
                new Vector3(-1f, 0f, -0.5f), new Vector3(0f, 0f, -0.5f), new Vector3(1f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
            };
            for (int i = 0; i < ItemsPerLevel; i++)
            {
                Piece pc = picks[level - 1][i];
                GameObject vAsset;
                if (!variant.TryGetValue(pc.prefab, out vAsset)) continue;
                Vector3 pos = center + new Vector3(slots[i].x * pitch, 0f, slots[i].z * pitch);
                PlaceVariant(zone, vAsset, pc.radius, pos, targetR, level, i, ref itemCount);
            }

            sizeLog.Append("L" + level + " Ø" + (targetR * 2f).ToString("F2") + "  ");
        }

        if (player != null)
        {
            player.transform.position = new Vector3(0f, 1f, cellZ * 0.9f);
            player.transform.rotation = Quaternion.identity;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            float dist = frameRadius / Mathf.Sin(Mathf.Max(1f, cam.fieldOfView) * 0.5f * Mathf.Deg2Rad) * 1.05f;
            _camPos = gridCenter + new Vector3(0f, dist * 0.72f, dist * 0.72f);
            cam.transform.position = _camPos;
            cam.transform.rotation = Quaternion.LookRotation((gridCenter - _camPos).normalized, Vector3.up);
        }
        FaceLabelsToCamera(rootGO);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[ItemLevelTest] " + (saved ? "Xong" : "LUU THAT BAI")
                + ": prefab Item = 1 base + " + variantCount + " variant. Scene xoa " + removed + " Map, "
                + Levels + " khu x " + ItemsPerLevel + " = " + itemCount + " item (physic + ngu/thuc)."
                + "\nPlayer: SimpleSuction non truoc mat, range=" + SuctionRange + ", len cap -> scale + non no."
                + "\nĐK item/level: " + sizeLog + "\nScene: " + ScenePath + " — bam Play de test.");
    }

    private static float ClusterPitch(float targetR) { return 2f * targetR * 1.25f + 0.4f; }

    // ---------------------------------------------------------------- player

    private static void SetupPlayer(GameObject player, float range)
    {
        // Go het logic hut cu (thu tu: bo cai phu thuoc truoc)
        RemoveIfPresent(player, typeof(PlayerGrowth));
        RemoveIfPresent(player, typeof(PlayerLevel));
        RemoveIfPresent(player, typeof(MouthSuction));

        // Tat VFX non cu
        Transform vfx = player.transform.Find("Mouth/SuctionVFX");
        if (vfx != null) vfx.gameObject.SetActive(false);

        SimpleSuction s = player.GetComponent<SimpleSuction>();
        if (s == null) s = player.AddComponent<SimpleSuction>();

        Transform mouth = player.transform.Find("Mouth");
        s.mouth = mouth != null ? mouth : player.transform;
        s.range = range;
        s.coneAngle = 75f;
        s.pullSpeed = 10f;
        s.swallowDistance = 0.6f;
        s.useLevelGate = true;
        s.level = 1;
        s.maxLevel = Levels;
        s.xpToNextBase = 3;
        s.xpGrowth = 1.25f;
        s.scalePerLevel = 0.12f;
        s.rangePerLevel = 0.15f;

        // Ve vung hut ra (quat ban trong suot tren mat dat)
        Transform zoneT = player.transform.Find("SuctionZone");
        GameObject zoneGO = zoneT != null ? zoneT.gameObject : new GameObject("SuctionZone");
        zoneGO.transform.SetParent(player.transform, false);
        SuctionZoneVisual viz = zoneGO.GetComponent<SuctionZoneVisual>();
        if (viz == null) viz = zoneGO.AddComponent<SuctionZoneVisual>();
        viz.suction = s;
        viz.mouth = s.mouth;
    }

    private static void RemoveIfPresent(GameObject go, System.Type type)
    {
        Component c = go.GetComponent(type);
        if (c != null) UnityEngine.Object.DestroyImmediate(c);
    }

    // ---------------------------------------------------------------- he prefab Item

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(ItemFolder))
            AssetDatabase.CreateFolder(ItemFolderParent, "Items");
    }

    /// <summary>Item_Base: root "Item" (Rigidbody + PhysicsDevourable) + node con "Model" (slot).</summary>
    private static GameObject CreateBaseTemplate()
    {
        GameObject root = new GameObject("Item");

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        PhysicsDevourable d = root.AddComponent<PhysicsDevourable>();
        d.requiredLevel = 1;
        d.xpValue = 1;
        d.scoreValue = 1;
        d.sleepDelay = 5f;

        GameObject model = new GameObject("Model");
        model.transform.SetParent(root.transform, false);

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, BasePath);
        UnityEngine.Object.DestroyImmediate(root);
        return asset;
    }

    private static GameObject CreateVariant(GameObject model, GameObject baseAsset)
    {
        string path = ItemFolder + "/Item_" + Sanitize(model.name) + ".prefab";

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
        Transform slot = inst.transform.Find("Model");
        if (slot == null) slot = inst.transform;

        GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(model, slot);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = Vector3.one;
        PrepModel(m);

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(inst, path);
        UnityEngine.Object.DestroyImmediate(inst);
        return asset;
    }

    /// <summary>
    /// Chuan bi model: go het logic cu (Devourable/PhysicsDevourable/Rigidbody/SuctionReactor)
    /// + go moi collider co san (nhieu la MeshCollider non-convex, khong dung duoc voi Rigidbody
    /// dong) roi them 1 BoxCollider bao quanh. Xoa Static Flags de item bay/roi duoc.
    /// </summary>
    private static void PrepModel(GameObject go)
    {
        DestroyAll(go.GetComponentsInChildren<Devourable>(true));
        DestroyAll(go.GetComponentsInChildren<PhysicsDevourable>(true));
        DestroyAll(go.GetComponentsInChildren<SuctionReactor>(true));
        DestroyAll(go.GetComponentsInChildren<Rigidbody>(true));
        DestroyAll(go.GetComponentsInChildren<Collider>(true));

        Transform[] kids = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < kids.Length; i++)
            GameObjectUtility.SetStaticEditorFlags(kids[i].gameObject, 0);

        Bounds b;
        if (MeasureRenderers(go, out b))
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = b.center - go.transform.position;   // go dang o goc, scale 1 => world = local
            bc.size = b.size;
        }
    }

    private static void DestroyAll(Component[] comps)
    {
        for (int i = 0; i < comps.Length; i++)
            if (comps[i] != null) UnityEngine.Object.DestroyImmediate(comps[i]);
    }

    private static string Sanitize(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- dat item

    private static void PlaceVariant(Transform parent, GameObject variantAsset, float naturalR, Vector3 pos,
                                     float targetR, int level, int slot, ref int count)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(variantAsset, parent);
        go.transform.position = new Vector3(pos.x, 0f, pos.z);
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f + Random.Range(-10f, 10f), 0f);

        float scale = naturalR > 0.001f ? targetR / naturalR : 1f;
        go.transform.localScale = Vector3.one * scale;

        Bounds b;
        if (MeasureRenderers(go, out b))
            go.transform.position += new Vector3(0f, PatchTop - b.min.y, 0f);

        PhysicsDevourable d = go.GetComponent<PhysicsDevourable>();
        if (d != null)
        {
            d.requiredLevel = level;
            d.xpValue = level;
            d.scoreValue = level;
        }

        go.name = "Item_LV" + level.ToString("00") + "_" + (slot + 1) + "_" + variantAsset.name;
        count++;
    }

    // ---------------------------------------------------------------- chon asset

    private struct Piece { public GameObject prefab; public float radius; }

    private static List<Piece> PickForLevel(List<Piece> pool, int level)
    {
        int n = pool.Count;
        int lo = Mathf.FloorToInt((level - 1) * n / (float)Levels);
        int hi = Mathf.Max(lo, Mathf.FloorToInt(level * n / (float)Levels) - 1);

        List<Piece> band = new List<Piece>();
        for (int i = lo; i <= hi && i < n; i++) band.Add(pool[i]);
        if (band.Count == 0) band.Add(pool[Mathf.Clamp(lo, 0, n - 1)]);

        List<Piece> picks = new List<Piece>();
        for (int i = 0; i < ItemsPerLevel; i++)
        {
            int bi = band.Count >= ItemsPerLevel
                ? Mathf.RoundToInt(i * (band.Count - 1) / (float)(ItemsPerLevel - 1))
                : i % band.Count;
            picks.Add(band[bi]);
        }
        return picks;
    }

    private static List<Piece> LoadPool()
    {
        List<Piece> list = new List<Piece>();
        for (int f = 0; f < PropFolders.Length; f++)
        {
            string folder = PropFolders[f];
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                float r = MeasureRadius(prefab);
                if (r <= 0.02f || r > MaxNaturalRadius) continue;

                list.Add(new Piece { prefab = prefab, radius = r });
            }
        }
        list.Sort((a, b) => a.radius.CompareTo(b.radius));
        return list;
    }

    private static float MeasureRadius(GameObject prefab)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        Bounds b;
        bool has = MeasureRenderers(go, out b);
        UnityEngine.Object.DestroyImmediate(go);
        return has ? b.extents.magnitude : 0f;
    }

    // ---------------------------------------------------------------- khu / nen / label

    private static void BuildPatch(Transform parent, Vector3 center, float w, float d, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Floor";
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(center.x, PatchTop - PatchThick * 0.5f, center.z);
        go.transform.localScale = new Vector3(w, PatchThick, d);
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        Collider col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);   // tam nen chi lam mau, khong chan
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    private static void BuildLabel(Transform parent, Vector3 center, float patchD, int level, float diameter)
    {
        GameObject go = new GameObject("Label_LV" + level.ToString("00"));
        go.transform.SetParent(parent, false);
        float y = Mathf.Max(2.5f, diameter * 1.3f + 1.2f);
        go.transform.position = new Vector3(center.x, y, center.z - patchD * 0.5f + 0.8f);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = "LV " + level + "\n<size=45%>req " + level + "  Ø" + diameter.ToString("F2") + "</size>";
        tmp.fontSize = Mathf.Max(6f, diameter * 3f + 4f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(Mathf.Max(6f, diameter * 4f), 4f);
    }

    private static void FaceLabelsToCamera(GameObject root)
    {
        var labels = root.GetComponentsInChildren<TextMeshPro>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Transform t = labels[i].transform;
            t.rotation = Quaternion.LookRotation((t.position - _camPos).normalized, Vector3.up);
        }
    }

    // ---------------------------------------------------------------- tien ich

    private static GameObject FindPlayer()
    {
        SimpleSuction ss = Object.FindAnyObjectByType<SimpleSuction>();
        if (ss != null) return ss.gameObject;
        MouthSuction ms = Object.FindAnyObjectByType<MouthSuction>();
        if (ms != null) return ms.gameObject;
        return GameObject.Find("Player");
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

    private static void SetMaterial(GameObject go, string path)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
    }

    private static Material[] LoadFloorPalette()
    {
        string[] names =
        {
            "M_Grass", "M_Building_A", "M_Building_B", "M_Building_C", "M_Building_D",
            "M_Building_E", "M_Car_A", "M_Car_B", "M_Car_C", "M_Accent"
        };
        List<Material> list = new List<Material>();
        for (int i = 0; i < names.Length; i++)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + names[i] + ".mat");
            if (m != null) list.Add(m);
        }
        if (list.Count == 0)
        {
            Material fb = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Sidewalk.mat");
            if (fb != null) list.Add(fb);
        }
        return list.ToArray();
    }
}
