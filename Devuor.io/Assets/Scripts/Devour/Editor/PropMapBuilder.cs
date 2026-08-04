using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dung map thanh pho tu pack Megacity (ithappy) thay cho map khoi hop cua MapBuilder.
///
/// Khac MapBuilder o cho: MapBuilder tu nan hinh khoi bang primitive nen muon to nho
/// bao nhieu cung duoc, con o day moi prefab co kich thuoc CO DINH cua nghe si. Khong
/// the ep mot toa nha 30 don vi vao mot o 14 don vi ma khong lam no lun vao long duong.
/// Nen cong cu nay DO bounds that cua tung prefab luc dung, roi moi chon cai vua o -
/// khong hardcode ten prefab nao vao dau, pack co them bot file thi van chay.
///
/// Bang chu doc tu tren xuong = tu Bac xuong Nam (z giam dan).
///
/// Ky tu:
///   .  ngoai map (thanh tuong bao)
///   #  duong
///   x  duong co vach qua duong
///   b  lo dat nho  (1 o)   -> mot toa nha be ngang <= 1 o
///   B  lo dat lon  (2x2 o) -> mot toa nha be ngang <= 2 o; B le loi thi tinh nhu 'b'
///   p  cong vien   (co, cay, bui + mot landmark)
///   q  quang truong (nen lat + landmark lon + ghe)
///   c  khu ca phe  (o, ban, ghe)
///   s  khu nghi    (ghe tam nang, o)
///   v  via he trong (do duong pho rai rac + landmark neu con du)
///
/// MOI prop tha ra deu duoc gan Devourable requiredLevel = 1 (autoLevelFromSize = false),
/// tuc la an duoc ngay tu cap 1. Toa nha thi KHONG gan - chung la canh nen.
/// </summary>
public static class PropMapBuilder
{
    private const string MenuRoot = "Tools/Devour/";
    private const string MapRootName = "Map";
    private const string OldMapName = "Map_Old";

    private const string PropFolder = "Assets/ithappy/Megacity/Prefabs/Props";
    private const string BuildingFolder = "Assets/ithappy/Megacity/Prefabs/Buildings";

    /// <summary>Canh mot o. 14 vi lo dat 1 o phai chua duoc cac cua hang be ngang 10-13.</summary>
    private const float Cell = 14f;

    private const float RoadY = 0.02f;      // duong cao hon via he mot chut cho khoi z-fighting
    private const float MarkY = 0.05f;      // vach ke cao hon duong

    /// <summary>Mat tren cua via he. Prop dung trong o xay nha dat chan o day.</summary>
    private const float GroundTop = 0.1f;

    /// <summary>
    /// Mat tren cua tam nen lat cho cac khu chuc nang (cong vien, quang truong, ca phe...).
    ///
    /// Phai tach hai do cao ra chu khong dung chung mot so: tam nen day 0.16 dat cao hon
    /// via he nen mat cua no o 0.2, con via he tran o 0.1. Cho chan prop nham mot trong
    /// hai la sai 0.1 don vi - nghe thi nho, nhung cai nap cong cao 0.06 se lo lung han
    /// tren khong, nhin phat ra ngay.
    /// </summary>
    private const float PatchTop = 0.2f;
    private const float WallHeight = 9f;    // nha o pack nay cao 12-120 nen tuong 3 don vi nhin nhu bo via
    private const float WallThickness = 2f;

    /// <summary>Vung quanh cho Player dung khong tha prop, khong thi vua vao game da ket trong dong do.</summary>
    private const float SpawnClearRadius = 9f;

    private const int Seed = 20260804;

    /// <summary>
    /// Bo cuc map. Moi dong dai bang nhau, moi ky tu la mot o.
    /// 20 cot x 19 dong x 14 = 280 x 266 don vi.
    /// </summary>
    private static readonly string[] Layout = new string[]
    {
        //         1111111111
        //01234567890123456789
        "....##########......", //  0
        "....#bb#BB#pp#......", //  1
        "....#bb#BB#pp#......", //  2
        "....#x##x###x#......", //  3
        "..###BB#qq#bb###....", //  4
        "..#v#BB#qq#bb#v#....", //  5
        "####x######x#x###...", //  6
        "#bb#BB#cc#BB#bb#v#..", //  7
        "#bb#BB#cc#BB#bb#v#..", //  8
        "#x###x##x##x###x#...", //  9
        "#pp#bb#qqq#BB#pp#...", // 10
        "#pp#bb#qqq#BB#pp#...", // 11
        "####################", // 12  <- truc duong lon chay het chieu ngang
        "..#BB#ss#bb#pp#vv#..", // 13
        "..#BB#ss#bb#pp#vv#..", // 14
        "..##x###x##x###x#...", // 15
        "..#bb#cc#BB#bb#p#...", // 16
        "..#bb#cc#BB#bb#p#...", // 17
        "..###############...", // 18
    };

    // ------------------------------------------------------------------ kho asset

    /// <summary>Mot prefab da do san kich thuoc, do mot lan luc load chu khong do lai moi lan tha.</summary>
    private struct Piece
    {
        public GameObject prefab;
        public string name;
        public Vector3 size;        // be ngang / cao / be doc o scale 1
        public float footprint;     // canh lon nhat cua hinh chieu xuong dat
        public float radius;        // ban kinh XZ, dung de tranh chong len nhau
        public float bottom;        // day nam duoi pivot bao nhieu (pack nay hau het = 0)
    }

    private static Material _road, _sidewalk, _grass, _wall, _mark;
    private static List<Piece> _street, _green, _leisure, _landmark;
    private static List<Piece> _smallBld, _bigBld;
    private static readonly List<string> _oversizeBld = new List<string>();

    /// <summary>Vong tron da chiem cho tren mat bang (x, z, ban kinh). Chong prop dam vao nhau.</summary>
    private static List<Vector3> _taken;

    private static int _propCount, _bldCount, _skippedProps;

    // -------------------------------------------------------------------- lenh menu

    [MenuItem(MenuRoot + "Dung map tu pack Megacity", false, 101)]
    private static void Build()
    {
        int choice = EditorUtility.DisplayDialogComplex(
            "Dung map tu pack Megacity",
            "Dung map moi tu prefab trong:\n"
            + "  " + PropFolder + "  (prop -> Devourable cap 1)\n"
            + "  " + BuildingFolder + "  (toa nha -> canh nen)\n\n"
            + "Map hien tai dang ten '" + MapRootName + "' se bi thay the.",
            "Dung, xoa map cu",          // 0
            "Thoi",                       // 1
            "Dung, giu map cu (tat di)"); // 2

        if (choice == 1) return;
        BuildNow(deleteOld: choice == 0);
    }

    /// <summary>Dung map, khong hoi gi. Tach ra de goi duoc tu script khac.</summary>
    public static void BuildNow(bool deleteOld)
    {
        if (!ValidateLayout()) return;
        if (!LoadAssets()) return;

        ReplaceOldMap(deleteOld);

        Random.InitState(Seed);
        _taken = new List<Vector3>(1024);
        _propCount = 0;
        _bldCount = 0;
        _skippedProps = 0;

        GameObject root = new GameObject(MapRootName);
        Undo.RegisterCreatedObjectUndo(root, "Dung map tu pack");

        Transform ground = NewGroup(root.transform, "Ground");
        Transform roads = NewGroup(root.transform, "Roads");
        Transform walls = NewGroup(root.transform, "Walls");
        Transform buildings = NewGroup(root.transform, "Buildings");
        Transform props = NewGroup(root.transform, "Props");

        ReserveSpawn();

        BuildGround(ground, roads);
        BuildWalls(walls);
        BuildBuildings(buildings);

        // Landmark truoc do lat vat: no to nhat nen kho cho nhat. Rai ghe va bui cay truoc
        // thi den luot dai phun nuoc khong con o nao du rong, ma bo mot cai dai phun nuoc
        // de lay ba cai ghe thi hong ca quang truong.
        BuildLandmarks(props);
        BuildDistricts(ground, props);
        BuildStreetFurniture(props);

        Validate();

        Selection.activeGameObject = root;
        EditorSceneManager_MarkDirty();

        Debug.Log("[PropMapBuilder] Xong. Map " + (Layout[0].Length * Cell) + " x " + (Layout.Length * Cell)
                + " don vi | " + _bldCount + " toa nha (canh nen) | " + _propCount + " prop Devourable cap 1"
                + (_skippedProps > 0 ? " | " + _skippedProps + " prop bo qua vi khong con cho trong" : "")
                + (_oversizeBld.Count > 0 ? "\nToa nha qua kho nen khong dung: " + string.Join(", ", _oversizeBld.ToArray()) : ""));
    }

    [MenuItem(MenuRoot + "Xoa cac map cu (Map_Old*)", false, 102)]
    private static void DeleteOldMaps()
    {
        List<GameObject> olds = FindOldMaps();
        if (olds.Count == 0) { Debug.Log("[PropMapBuilder] Khong con map cu nao."); return; }

        if (!EditorUtility.DisplayDialog("Xoa map cu",
            "Xoa han " + olds.Count + " map cu khoi scene? (Ctrl+Z van hoan tac duoc)", "Xoa", "Thoi"))
            return;

        for (int i = 0; i < olds.Count; i++) Undo.DestroyObjectImmediate(olds[i]);
        EditorSceneManager_MarkDirty();
        Debug.Log("[PropMapBuilder] Da xoa " + olds.Count + " map cu.");
    }

    // ------------------------------------------------------------------ tien ich o

    private static char At(int col, int row)
    {
        if (row < 0 || row >= Layout.Length) return '.';
        if (col < 0 || col >= Layout[row].Length) return '.';
        return Layout[row][col];
    }

    private static bool IsVoid(int col, int row) { return At(col, row) == '.'; }
    private static bool IsRoad(int col, int row) { char c = At(col, row); return c == '#' || c == 'x'; }
    private static bool IsBlock(int col, int row) { return !IsVoid(col, row) && !IsRoad(col, row); }

    /// <summary>O chuc nang co tam nen lat, nen mat san cua no cao hon via he tran.</summary>
    private static bool HasPatch(char k) { return k == 'p' || k == 'q' || k == 'c' || k == 's' || k == 'v'; }

    /// <summary>Do cao dat chan prop trong o nay.</summary>
    private static float SurfaceOf(int col, int row) { return HasPatch(At(col, row)) ? PatchTop : GroundTop; }

    /// <summary>Tam cua o trong world. Dong 0 nam o phia z lon nhat (Bac).</summary>
    private static Vector3 CellCenter(int col, int row)
    {
        float w = Layout[0].Length * Cell;
        float h = Layout.Length * Cell;
        return new Vector3(col * Cell - w * 0.5f + Cell * 0.5f, 0f, h * 0.5f - row * Cell - Cell * 0.5f);
    }

    private static Transform NewGroup(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    private static bool ValidateLayout()
    {
        int cols = Layout[0].Length;
        for (int r = 0; r < Layout.Length; r++)
        {
            if (Layout[r].Length == cols) continue;
            Debug.LogError("[PropMapBuilder] Dong " + r + " dai " + Layout[r].Length
                         + " chu, khac cac dong khac (" + cols + ").");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Quet root cua scene chu khong dung GameObject.Find: map cu da bi TAT, ma
    /// GameObject.Find bo qua object dang tat - dung no thi lan nao cung tuong la
    /// chua co map cu nao, roi de ra hai object trung ten 'Map_Old'.
    /// </summary>
    private static List<GameObject> FindRoots(string prefix)
    {
        List<GameObject> found = new List<GameObject>();
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name.StartsWith(prefix)) found.Add(roots[i]);
        return found;
    }

    private static List<GameObject> FindOldMaps() { return FindRoots(OldMapName); }

    private static GameObject FindCurrentMap()
    {
        List<GameObject> roots = FindRoots(MapRootName);
        for (int i = 0; i < roots.Count; i++)
            if (roots[i].name == MapRootName) return roots[i];
        return null;
    }

    /// <summary>
    /// Map cu: xoa han hoac doi ten + tat di.
    ///
    /// Neu giu lai thi phai doi ten khong trung: scene nay da co san mot 'Map_Old' tu lan
    /// dung truoc, hai object cung ten thi GameObject.Find sau nay bat nham cai nao khong biet.
    /// </summary>
    private static void ReplaceOldMap(bool deleteOld)
    {
        GameObject current = FindCurrentMap();

        if (deleteOld)
        {
            List<GameObject> olds = FindOldMaps();
            for (int i = 0; i < olds.Count; i++) Undo.DestroyObjectImmediate(olds[i]);
            if (current != null) Undo.DestroyObjectImmediate(current);
            return;
        }

        if (current == null) return;

        List<GameObject> taken = FindOldMaps();
        string name = OldMapName;
        for (int i = 1; NameTaken(taken, name); i++) name = OldMapName + "_" + i;

        Undo.RecordObject(current, "Dung map tu pack");
        current.name = name;
        current.SetActive(false);
    }

    private static bool NameTaken(List<GameObject> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].name == name) return true;
        return false;
    }

    // ---------------------------------------------------------------- load prefab

    private static bool LoadAssets()
    {
        _road = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Road.mat");
        _sidewalk = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Sidewalk.mat");
        _grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Grass.mat");
        _wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Wall.mat");
        _mark = _sidewalk;

        if (_road == null || _sidewalk == null || _grass == null || _wall == null)
        {
            Debug.LogError("[PropMapBuilder] Thieu material trong Assets/Materials (M_Road, M_Sidewalk, M_Grass, M_Wall).");
            return false;
        }

        List<Piece> props = LoadFolder(PropFolder);
        if (props.Count == 0)
        {
            Debug.LogError("[PropMapBuilder] Khong tim thay prefab nao trong " + PropFolder + ".");
            return false;
        }

        SortProps(props);
        SortBuildings(LoadFolder(BuildingFolder));

        Debug.Log("[PropMapBuilder] Kho asset: " + _street.Count + " do duong pho, " + _green.Count + " cay/bui, "
                + _leisure.Count + " do nghi duong, " + _landmark.Count + " landmark | "
                + _smallBld.Count + " nha lo nho, " + _bigBld.Count + " nha lo lon.");
        return true;
    }

    private static List<Piece> LoadFolder(string folder)
    {
        List<Piece> list = new List<Piece>();
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning("[PropMapBuilder] Khong co thu muc " + folder + ", bo qua.");
            return list;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Bounds b;
            if (!MeasurePrefab(prefab, out b)) continue;   // prefab rong, khong do duoc

            Piece p = new Piece();
            p.prefab = prefab;
            p.name = prefab.name;
            p.size = b.size;
            p.footprint = Mathf.Max(b.size.x, b.size.z);
            p.radius = new Vector2(b.size.x, b.size.z).magnitude * 0.5f;
            p.bottom = b.min.y - prefab.transform.position.y;
            list.Add(p);
        }

        // Sap theo ten cho thu tu on dinh giua cac may: FindAssets tra ve theo GUID nen
        // cung mot pack tren hai may co the ra hai thu tu, keo theo Random lech het.
        list.Sort((a, c) => string.CompareOrdinal(a.name, c.name));
        return list;
    }

    /// <summary>
    /// Do bounds cua mot prefab ASSET (chua instantiate).
    ///
    /// Do bang Renderer chu khong dung Devourable.TryMeasureBounds: ham do uu tien Collider,
    /// ma Collider.bounds tren prefab asset chua vao scene thi khong dam bao co gia tri that.
    /// Trong pack nay collider la MeshCollider boc dung mesh do nen hai cach ra cung mot so.
    /// </summary>
    private static bool MeasurePrefab(GameObject prefab, out Bounds bounds)
    {
        bounds = new Bounds(prefab.transform.position, Vector3.zero);

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!has) { bounds = renderers[i].bounds; has = true; }
            else bounds.Encapsulate(renderers[i].bounds);
        }
        return has;
    }

    /// <summary>
    /// Chia prop vao bon nhom. Uu tien khop theo TEN vi ten trong pack co quy tac ro rang;
    /// ten la thi roi ve chia theo KICH THUOC de pack co them prefab moi van khong bi rot.
    /// </summary>
    private static void SortProps(List<Piece> all)
    {
        string[] streetNames = { "lamp_post", "hydrant", "trash", "bench", "advertising", "phone_booth",
                                 "bike_parking", "bus_stop", "manhole", "chain", "pillar", "fence", "fencing" };
        string[] greenNames = { "bush", "tree", "palm" };
        string[] leisureNames = { "umbrella", "table", "chair", "sunbed" };
        string[] landmarkNames = { "fountain", "monument", "decoration", "decorative_construction", "marina",
                                   "rescue_tower", "billboard", "road_sign", "tent" };

        _street = new List<Piece>();
        _green = new List<Piece>();
        _leisure = new List<Piece>();
        _landmark = new List<Piece>();

        for (int i = 0; i < all.Count; i++)
        {
            Piece p = all[i];

            // Landmark truoc: 'decoration' phai an truoc 'decorative_construction' thi khong sao,
            // nhung 'road_sign' ma xet sau 'fence' thi khong viec gi - chi can landmark truoc street.
            if (StartsWithAny(p.name, landmarkNames)) _landmark.Add(p);
            else if (StartsWithAny(p.name, greenNames)) _green.Add(p);
            else if (StartsWithAny(p.name, leisureNames)) _leisure.Add(p);
            else if (StartsWithAny(p.name, streetNames)) _street.Add(p);
            else if (p.footprint <= 3f) _street.Add(p);
            else if (p.footprint <= 8f) _green.Add(p);
            else _landmark.Add(p);
        }

        // Landmark to nhat duoc chia cho o rong nhat, nen sap san tu to xuong nho
        _landmark.Sort((a, b) => b.footprint.CompareTo(a.footprint));
    }

    /// <summary>
    /// Chia toa nha theo lo dat vua duoc, chua bao gio theo cong nang.
    ///
    /// Nha vuot ca lo 2x2 (san bay 300x210, san vu tru 75x90, ga tau 74x63, depot 60x75)
    /// thi khong dung: nhet vao la no nuot tron may khu ben canh, ma thu nho lai vua o
    /// thi cua ra vao be bang nguoi choi, nhin ra ngay la sai ti le.
    /// </summary>
    private static void SortBuildings(List<Piece> all)
    {
        _smallBld = new List<Piece>();
        _bigBld = new List<Piece>();
        _oversizeBld.Clear();

        float smallFit = Cell * 0.95f;
        float bigFit = Cell * 2f * 0.95f;

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].footprint <= smallFit) _smallBld.Add(all[i]);
            else if (all[i].footprint <= bigFit) _bigBld.Add(all[i]);
            else _oversizeBld.Add(all[i].name);
        }

        // Lo nho het nha thi muon tam nha lo lon, thu nho lai cho vua
        if (_smallBld.Count == 0) _smallBld = _bigBld;
    }

    private static bool StartsWithAny(string name, string[] prefixes)
    {
        for (int i = 0; i < prefixes.Length; i++)
            if (name.StartsWith(prefixes[i])) return true;
        return false;
    }

    // ------------------------------------------------------------- cho da chiem

    private static void ReserveSpawn()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("Player");
        if (player == null) return;

        Vector3 p = player.transform.position;
        _taken.Add(new Vector3(p.x, p.z, SpawnClearRadius));
    }

    private static bool IsFree(float x, float z, float radius)
    {
        for (int i = 0; i < _taken.Count; i++)
        {
            Vector3 t = _taken[i];
            float dx = t.x - x, dz = t.y - z;
            float r = t.z + radius;
            if (dx * dx + dz * dz < r * r) return false;
        }
        return true;
    }

    private static void Occupy(float x, float z, float radius)
    {
        _taken.Add(new Vector3(x, z, radius));
    }

    // -------------------------------------------------------------- tha prefab

    /// <summary>
    /// Tha mot prefab xuong (x, z), day cua no dat dung mat via he.
    /// Tra ve null neu cho do da co vat khac.
    /// </summary>
    private static GameObject Place(Transform parent, Piece piece, float x, float z, float yaw,
                                    float scale, float surfaceY, float spacing)
    {
        float radius = piece.radius * scale * spacing;
        if (!IsFree(x, z, radius)) return null;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, parent);
        go.transform.position = new Vector3(x, surfaceY - piece.bottom * scale, z);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;

        Occupy(x, z, radius);
        return go;
    }

    /// <summary>
    /// Prop: gan Devourable cap 1 va XOA static flags.
    ///
    /// Xoa static la bat buoc chu khong phai cho gon: Batching Static gop mesh vao mot mesh
    /// chung o world space, transform bay vao mieng thi mesh dung yen - nguoi choi thay vat
    /// the bien mat tai cho, khong co doan bay nao. Prefab trong pack thuong duoc danh dau
    /// static san nen khong xoa la dinh ngay.
    /// </summary>
    private static void MakeProp(GameObject go, string name)
    {
        go.name = name;

        Transform[] kids = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < kids.Length; i++) GameObjectUtility.SetStaticEditorFlags(kids[i].gameObject, 0);

        Devourable d = go.GetComponent<Devourable>();
        if (d == null) d = go.AddComponent<Devourable>();

        // Tat autoLevelFromSize chu khong chi dat requiredLevel: neu de bat thi Awake luc
        // vao game se do lai ban kinh va ghi de len 1 - cai thap 8 don vi se tu nhay len
        // cap 20, dat tay o Editor bao nhieu cung vo nghia.
        d.autoLevelFromSize = false;
        d.requiredLevel = 1;
        d.xpValue = Devourable.XpForTier(1);
        d.scoreValue = 1;

        EditorUtility.SetDirty(go);
        _propCount++;
    }

    /// <summary>Toa nha: canh nen, khong an duoc, de static cho gop mesh lai.</summary>
    private static void MakeBuilding(GameObject go, string name)
    {
        go.name = name;
        Transform[] kids = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < kids.Length; i++)
            GameObjectUtility.SetStaticEditorFlags(kids[i].gameObject, StaticEditorFlags.BatchingStatic);
        _bldCount++;
    }

    private static Piece Pick(List<Piece> pool)
    {
        return pool[Random.Range(0, pool.Count)];
    }

    private static float RandomYaw()
    {
        return Random.Range(0, 4) * 90f + Random.Range(-6f, 6f);
    }

    // -------------------------------------------------------------- nen va duong

    /// <summary>
    /// Nen va duong gop theo tung doan ngang lien nhau thay vi moi o mot khoi: map nay
    /// hon 200 o, khong gop thi rieng phan nen da hon 200 renderer cho may cai mat phang.
    /// </summary>
    private static void BuildGround(Transform ground, Transform roads)
    {
        int rows = Layout.Length, cols = Layout[0].Length;

        for (int r = 0; r < rows; r++)
        {
            int c = 0;
            while (c < cols)
            {
                if (IsVoid(c, r)) { c++; continue; }

                bool road = IsRoad(c, r);
                int start = c;
                while (c < cols && !IsVoid(c, r) && IsRoad(c, r) == road) c++;

                int len = c - start;
                Vector3 a = CellCenter(start, r);
                Vector3 center = new Vector3(a.x + (len - 1) * Cell * 0.5f, road ? RoadY : 0f, a.z);
                Vector3 size = new Vector3(len * Cell, road ? 0.04f : GroundTop * 2f, Cell);

                if (road) Box(roads, "Road_" + r + "_" + start, center, size, _road);
                else Box(ground, "Ground_" + r + "_" + start, center, size, _sidewalk);
            }
        }

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (At(c, r) != 'x') continue;
                bool vertical = IsRoad(c, r - 1) || IsRoad(c, r + 1);
                Vector3 p = CellCenter(c, r);
                for (int i = 0; i < 5; i++)
                {
                    float t = (i - 2) * (Cell / 6f);
                    Vector3 sp = vertical ? new Vector3(p.x + t, MarkY, p.z) : new Vector3(p.x, MarkY, p.z + t);
                    Vector3 ss = vertical ? new Vector3(0.9f, 0.03f, Cell * 0.62f) : new Vector3(Cell * 0.62f, 0.03f, 0.9f);
                    Box(roads, "Crosswalk_" + r + "_" + c + "_" + i, sp, ss, _mark);
                }
            }
    }

    /// <summary>
    /// Tuong bao: quet moi canh cua o co noi dung ma phia ben kia la khoang trong. Nho vay
    /// bien map bat quy tac den dau cung tu co tuong dung, khong phai ve tay. Doan thang
    /// lien nhau duoc gop lam mot.
    /// </summary>
    private static void BuildWalls(Transform walls)
    {
        int rows = Layout.Length, cols = Layout[0].Length;
        float half = Cell * 0.5f;

        for (int r = 0; r <= rows; r++)
            for (int side = 0; side < 2; side++)
            {
                int c = 0;
                while (c < cols)
                {
                    bool need = side == 0
                        ? (!IsVoid(c, r) && IsVoid(c, r - 1))
                        : (!IsVoid(c, r) && IsVoid(c, r + 1));
                    if (!need) { c++; continue; }

                    int start = c;
                    while (c < cols && (side == 0 ? (!IsVoid(c, r) && IsVoid(c, r - 1))
                                                  : (!IsVoid(c, r) && IsVoid(c, r + 1)))) c++;

                    int len = c - start;
                    Vector3 a = CellCenter(start, r);
                    float z = a.z + (side == 0 ? half : -half);
                    Box(walls, "Wall_H_" + r + "_" + start,
                        new Vector3(a.x + (len - 1) * Cell * 0.5f, WallHeight * 0.5f, z),
                        new Vector3(len * Cell + WallThickness, WallHeight, WallThickness), _wall);
                }
            }

        for (int c = 0; c <= cols; c++)
            for (int side = 0; side < 2; side++)
            {
                int r = 0;
                while (r < rows)
                {
                    bool need = side == 0
                        ? (!IsVoid(c, r) && IsVoid(c - 1, r))
                        : (!IsVoid(c, r) && IsVoid(c + 1, r));
                    if (!need) { r++; continue; }

                    int start = r;
                    while (r < rows && (side == 0 ? (!IsVoid(c, r) && IsVoid(c - 1, r))
                                                  : (!IsVoid(c, r) && IsVoid(c + 1, r)))) r++;

                    int len = r - start;
                    Vector3 a = CellCenter(c, start);
                    float x = a.x + (side == 0 ? -half : half);
                    Box(walls, "Wall_V_" + c + "_" + start,
                        new Vector3(x, WallHeight * 0.5f, a.z - (len - 1) * Cell * 0.5f),
                        new Vector3(WallThickness, WallHeight, len * Cell + WallThickness), _wall);
                }
            }
    }

    // ---------------------------------------------------------------- toa nha

    /// <summary>
    /// Lo 'B' di theo cum 2x2. Quet tim goc Tay-Bac cua cum roi danh dau ca bon o la da
    /// dung, khong thi mot cum 2x2 se de ra bon toa nha chong len nhau.
    /// 'B' le loi (khong du cum 2x2) duoc ha xuong dung nhu lo 'b'.
    /// </summary>
    private static void BuildBuildings(Transform parent)
    {
        int rows = Layout.Length, cols = Layout[0].Length;
        bool[,] used = new bool[cols, rows];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (At(c, r) != 'B' || used[c, r]) continue;
                if (At(c + 1, r) != 'B' || At(c, r + 1) != 'B' || At(c + 1, r + 1) != 'B') continue;
                if (used[c + 1, r] || used[c, r + 1] || used[c + 1, r + 1]) continue;

                used[c, r] = used[c + 1, r] = used[c, r + 1] = used[c + 1, r + 1] = true;

                Vector3 a = CellCenter(c, r);
                PlaceBuilding(parent, _bigBld.Count > 0 ? _bigBld : _smallBld,
                              a.x + Cell * 0.5f, a.z - Cell * 0.5f, Cell * 2f, "BldBig_" + r + "_" + c);
            }

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                char k = At(c, r);
                if ((k != 'b' && k != 'B') || used[c, r]) continue;

                Vector3 p = CellCenter(c, r);
                PlaceBuilding(parent, _smallBld, p.x, p.z, Cell, "Bld_" + r + "_" + c);
            }
    }

    /// <summary>
    /// Dat mot toa nha vua lo. Neu prefab rong hon lo thi thu nho DEU lai cho vua chu khong
    /// bop rieng mot truc: bop lech truc thi cua so va cua ra vao meo hinh, nhin la biet lien.
    /// </summary>
    private static void PlaceBuilding(Transform parent, List<Piece> pool, float x, float z, float lot, string name)
    {
        if (pool == null || pool.Count == 0) return;

        Piece piece = Pick(pool);

        // 0.75 chu khong phai sat mep lo: phan chua lai chinh la via he. Ep nha rong bang
        // ca lo thi tuong nha dung ngay mep duong, khong con cho cam cot den hay ke ghe -
        // ma do duong pho moi la thu nguoi choi cap 1 an duoc.
        float fit = lot * 0.75f;
        float scale = piece.footprint > fit ? fit / piece.footprint : 1f;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, parent);
        go.transform.position = new Vector3(x, GroundTop - piece.bottom * scale, z);
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
        if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;

        MakeBuilding(go, name);

        // Chiem cho theo NUA BE NGANG chu khong theo duong cheo bounds: lay duong cheo thi
        // vong tron chiem cho phinh ra qua ca via he, khong con prop nao dat duoc quanh nha.
        Occupy(x, z, piece.footprint * 0.5f * scale * 0.95f);
    }

    // -------------------------------------------------------------- cac khu chuc nang

    /// <summary>
    /// Moi o chuc nang deu duoc lat mot tam nen rieng, ke ca o 'v' chi de trong.
    ///
    /// Lat het chu khong lat mot vai o: co lat thi mat san la PatchTop, khong lat thi la
    /// GroundTop, ma landmark duoc chia cho vung gom ca 'q' lan 'p' lan 'v' - de lan lon
    /// hai loai o trong cung mot vung thi chan landmark biet dat vao do cao nao.
    /// </summary>
    private static void BuildDistricts(Transform ground, Transform props)
    {
        int rows = Layout.Length, cols = Layout[0].Length;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                char k = At(c, r);
                if (k != 'p' && k != 'q' && k != 'c' && k != 's' && k != 'v') continue;

                Vector3 p = CellCenter(c, r);
                Patch(ground, k, p, r, c);

                if (k == 'p') BuildPark(props, p);
                else if (k == 'q') BuildPlaza(props, p);
                else if (k == 'c') BuildCafe(props, p);
                else if (k == 's') BuildLeisure(props, p);
                else if (k == 'v') BuildOpenWalk(props, p);
            }
    }

    /// <summary>Tam nen cua mot o chuc nang. Mat tren cua no dung bang PatchTop.</summary>
    private static void Patch(Transform ground, char kind, Vector3 p, int r, int c)
    {
        float thickness = 0.16f;
        Box(ground, (kind == 'p' ? "Grass_" : "Deck_") + r + "_" + c,
            new Vector3(p.x, PatchTop - thickness * 0.5f, p.z),
            new Vector3(Cell * 0.94f, thickness, Cell * 0.94f),
            kind == 'p' ? _grass : _sidewalk);
    }

    private static void BuildPark(Transform props, Vector3 p)
    {
        Scatter(props, _green, p, Cell * 0.36f, Random.Range(3, 6), PatchTop, 1f, "Green");
        Scatter(props, _street, p, Cell * 0.4f, Random.Range(1, 3), PatchTop, 1f, "Street");
    }

    private static void BuildPlaza(Transform props, Vector3 p)
    {
        // Landmark chinh do BuildLandmarks dat sau (no can biet ca vung moi tinh duoc co vua khong),
        // o day chi rai ghe va do lat vat quanh ria cho quang truong khong tron truong
        Scatter(props, _street, p, Cell * 0.42f, Random.Range(2, 4), PatchTop, 1f, "Street");
    }

    private static void BuildCafe(Transform props, Vector3 p)
    {
        Scatter(props, _leisure, p, Cell * 0.36f, Random.Range(5, 9), PatchTop, 0.85f, "Leisure");
        Scatter(props, _green, p, Cell * 0.42f, Random.Range(1, 3), PatchTop, 1f, "Green");
    }

    private static void BuildLeisure(Transform props, Vector3 p)
    {
        Scatter(props, _leisure, p, Cell * 0.38f, Random.Range(6, 10), PatchTop, 0.85f, "Leisure");
    }

    private static void BuildOpenWalk(Transform props, Vector3 p)
    {
        Scatter(props, _street, p, Cell * 0.38f, Random.Range(3, 6), PatchTop, 1f, "Street");
        Scatter(props, _green, p, Cell * 0.38f, Random.Range(0, 2), PatchTop, 1f, "Green");
    }

    /// <summary>
    /// Rai n mon quanh tam o. Thu nhieu lan moi mon vi cho co the da bi chiem; het luot thu
    /// thi bo, chu khong day sang o ben canh - day sang la prop tran ra long duong.
    /// </summary>
    private static void Scatter(Transform parent, List<Piece> pool, Vector3 center, float spread,
                                int count, float surfaceY, float spacing, string tag)
    {
        if (pool == null || pool.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            Piece piece = Pick(pool);
            GameObject go = null;

            for (int attempt = 0; attempt < 6 && go == null; attempt++)
            {
                float x = center.x + Random.Range(-spread, spread);
                float z = center.z + Random.Range(-spread, spread);
                go = Place(parent, piece, x, z, RandomYaw(), Random.Range(0.9f, 1.12f), surfaceY, spacing);
            }

            if (go != null) MakeProp(go, tag + "_" + piece.name + "_" + _propCount);
            else _skippedProps++;
        }
    }

    // ---------------------------------------------------------------- landmark

    /// <summary>
    /// Landmark to (dai phun nuoc, tuong dai, bang quang cao, ben tau...) can ca mot VUNG
    /// chu khong vua mot o. Nen o day gom cac o 'q'/'p'/'v' lien nhau thanh tung vung, roi
    /// chia landmark to nhat cho vung rong nhat.
    ///
    /// Cai nao van con qua kho so voi vung duoc chia thi thu nho DEU lai. Nguoi dung da chon
    /// "dua vao het, cap 1 het" nen khong duoc phep bo cai nao chi vi no to.
    /// </summary>
    private static void BuildLandmarks(Transform props)
    {
        if (_landmark == null || _landmark.Count == 0) return;

        // Cho rong nhat len truoc de bat cap voi landmark to nhat (_landmark da sap tu to xuong)
        List<Vector3> anchors = FindAnchors();
        anchors.Sort((a, b) => b.z.CompareTo(a.z));
        bool[] used = new bool[anchors.Count];

        List<string> shrunk = new List<string>();
        List<string> failed = new List<string>();

        for (int i = 0; i < _landmark.Count; i++)
        {
            Piece piece = _landmark[i];
            GameObject go = null;

            // Duyet het cac cho con trong chu khong chi thu mot cho: landmark nao cung phai
            // len map (nguoi dung chon "dua vao het"), nen tha xuong cho hep hon con hon la bo.
            for (int a = 0; a < anchors.Count && go == null; a++)
            {
                if (used[a]) continue;

                Vector3 anchor = anchors[a];
                float room = anchor.z * 0.85f;
                float scale = piece.footprint > room ? room / piece.footprint : 1f;

                go = Place(props, piece, anchor.x, anchor.y, RandomYaw(), scale, PatchTop, 0.8f);
                if (go == null) continue;

                used[a] = true;
                if (scale < 0.99f) shrunk.Add(piece.name + " x" + scale.ToString("F2"));
            }

            if (go != null) MakeProp(go, "Landmark_" + piece.name);
            else { _skippedProps++; failed.Add(piece.name); }
        }

        if (shrunk.Count > 0)
            Debug.Log("[PropMapBuilder] Landmark thu nho cho vua cho: " + string.Join(", ", shrunk.ToArray()));
        if (failed.Count > 0)
            Debug.LogWarning("[PropMapBuilder] Khong con cho cho landmark: " + string.Join(", ", failed.ToArray())
                           + ". Them o 'q'/'p'/'v' vao Layout neu muon chung len map.");
    }

    /// <summary>
    /// Cac cho co the dat landmark, moi cho la (x, z, be rong dung duoc).
    ///
    /// Sinh ra HAI muc: tam cua ca mot vung o lien nhau (rong nhat, danh cho cai to nhat),
    /// va tam cua tung o le trong vung do. Chi lay tam vung thi 11 vung khong du cho 14
    /// landmark; chi lay tung o thi khong cho nao du rong cho ben tau hay khu trien lam.
    /// Hai muc cung nam trong danh sach, cho nao bi chiem roi thi vong tron chiem cho tu loai.
    /// </summary>
    private static List<Vector3> FindAnchors()
    {
        int rows = Layout.Length, cols = Layout[0].Length;
        bool[,] seen = new bool[cols, rows];
        List<Vector3> anchors = new List<Vector3>();

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                char k = At(c, r);
                if (seen[c, r] || (k != 'q' && k != 'p' && k != 'v')) continue;

                int minC = c, maxC = c, minR = r, maxR = r;
                List<Vector2Int> cells = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(c, r));
                seen[c, r] = true;

                while (queue.Count > 0)
                {
                    Vector2Int cur = queue.Dequeue();
                    cells.Add(cur);
                    minC = Mathf.Min(minC, cur.x); maxC = Mathf.Max(maxC, cur.x);
                    minR = Mathf.Min(minR, cur.y); maxR = Mathf.Max(maxR, cur.y);

                    Vector2Int[] next = {
                        new Vector2Int(cur.x - 1, cur.y), new Vector2Int(cur.x + 1, cur.y),
                        new Vector2Int(cur.x, cur.y - 1), new Vector2Int(cur.x, cur.y + 1) };

                    for (int i = 0; i < next.Length; i++)
                    {
                        Vector2Int p = next[i];
                        if (p.x < 0 || p.x >= cols || p.y < 0 || p.y >= rows) continue;
                        if (seen[p.x, p.y] || At(p.x, p.y) != k) continue;
                        seen[p.x, p.y] = true;
                        queue.Enqueue(p);
                    }
                }

                Vector3 a = CellCenter(minC, minR);
                Vector3 b = CellCenter(maxC, maxR);
                float shortSide = Mathf.Min(maxC - minC + 1, maxR - minR + 1) * Cell;

                if (cells.Count > 1)
                    anchors.Add(new Vector3((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f, shortSide));

                for (int i = 0; i < cells.Count; i++)
                {
                    Vector3 p = CellCenter(cells[i].x, cells[i].y);
                    anchors.Add(new Vector3(p.x, p.z, Cell));
                }
            }

        return anchors;
    }

    // ------------------------------------------------------- do duong pho ven duong

    /// <summary>
    /// Do duong pho dat theo VIEN o giap duong chu khong rai deu trong long o: cot den giua
    /// bai co thi vo ly, ma cot den doc via he thi tu no ve ra hinh con pho. Day cung la
    /// nguon prop chinh cho nguoi choi cap thap - di doc duong la an duoc lien tuc.
    /// </summary>
    private static void BuildStreetFurniture(Transform props)
    {
        if (_street == null || _street.Count == 0) return;

        int rows = Layout.Length, cols = Layout[0].Length;
        float inset = Cell * 0.46f;     // sat mep bo via, cho cua do duong pho

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!IsBlock(c, r)) continue;
                Vector3 p = CellCenter(c, r);
                float surface = SurfaceOf(c, r);

                for (int side = 0; side < 4; side++)
                {
                    int dc = side == 0 ? -1 : side == 1 ? 1 : 0;
                    int dr = side == 2 ? -1 : side == 3 ? 1 : 0;
                    if (!IsRoad(c + dc, r + dr)) continue;

                    // Doc mot canh dat 2 mon, lech nhau cho khong thanh hang thang tap
                    for (int i = 0; i < 2; i++)
                    {
                        float along = (i == 0 ? -1f : 1f) * Random.Range(Cell * 0.12f, Cell * 0.3f);
                        float x = p.x + dc * inset + (dc == 0 ? along : 0f);
                        float z = p.z - dr * inset + (dr == 0 ? along : 0f);

                        // Mat san lay theo dung o dang dung: o xay nha la via he tran (GroundTop),
                        // o chuc nang co tam nen lat day 0.16 nen cao hon (PatchTop). Dat nham
                        // la cot den lun mot doan vao nen hoac dung lo lung tren khong.
                        Piece piece = Pick(_street);
                        GameObject go = Place(props, piece, x, z, RandomYaw(), Random.Range(0.92f, 1.08f),
                                              surface, 1f);

                        if (go != null) MakeProp(go, "Street_" + piece.name + "_" + _propCount);
                        else _skippedProps++;
                    }
                }
            }
    }

    // ---------------------------------------------------------------- kiem tra

    /// <summary>
    /// Hai loi khong nhin ra bang mat khi map to:
    ///   1. O co noi dung nhung khong giap duong nao -> nguoi choi khong bao gio toi duoc
    ///   2. Duong bi tach thanh nhieu manh roi rac -> co vung khong di sang duoc
    /// </summary>
    private static void Validate()
    {
        int rows = Layout.Length, cols = Layout[0].Length;

        int orphans = 0;
        string firstOrphan = string.Empty;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!IsBlock(c, r)) continue;
                if (IsRoad(c - 1, r) || IsRoad(c + 1, r) || IsRoad(c, r - 1) || IsRoad(c, r + 1)) continue;
                orphans++;
                if (firstOrphan == string.Empty) firstOrphan = "(cot " + c + ", dong " + r + ")";
            }

        int total = 0;
        Vector2Int seed = new Vector2Int(-1, -1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (IsRoad(c, r)) { total++; if (seed.x < 0) seed = new Vector2Int(c, r); }

        int reached = 0;
        if (seed.x >= 0)
        {
            bool[,] seen = new bool[cols, rows];
            Queue<Vector2Int> q = new Queue<Vector2Int>();
            q.Enqueue(seed); seen[seed.x, seed.y] = true;
            while (q.Count > 0)
            {
                Vector2Int cur = q.Dequeue();
                reached++;
                Vector2Int[] next = {
                    new Vector2Int(cur.x - 1, cur.y), new Vector2Int(cur.x + 1, cur.y),
                    new Vector2Int(cur.x, cur.y - 1), new Vector2Int(cur.x, cur.y + 1) };
                for (int i = 0; i < next.Length; i++)
                {
                    Vector2Int n = next[i];
                    if (n.x < 0 || n.x >= cols || n.y < 0 || n.y >= rows) continue;
                    if (seen[n.x, n.y] || !IsRoad(n.x, n.y)) continue;
                    seen[n.x, n.y] = true;
                    q.Enqueue(n);
                }
            }
        }

        if (orphans > 0)
            Debug.LogWarning("[PropMapBuilder] " + orphans + " o khong giap duong nao, vd " + firstOrphan
                           + ". Nguoi choi khong toi duoc cho do.");

        if (reached < total)
            Debug.LogWarning("[PropMapBuilder] Duong bi tach roi: chi " + reached + "/" + total
                           + " o duong noi voi nhau. Co vung khong di sang duoc.");

        if (orphans == 0 && reached == total)
            Debug.Log("[PropMapBuilder] Kiem tra dat: moi o deu giap duong, " + total + " o duong lien mach.");
    }

    private static void EditorSceneManager_MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
