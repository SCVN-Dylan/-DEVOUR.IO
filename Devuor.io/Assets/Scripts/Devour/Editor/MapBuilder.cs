using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dung map thanh pho tu mot bang chu ASCII.
///
/// Vi sao khong dat tay tung khoi: map co bien bat quy tac, hon 300 o, va moi lan
/// muon doi bo cuc la phai keo lai ca tram object. Voi bang chu thi sua vai ky tu
/// la xong, va nhin vao la thay ngay hinh dang map - thu ma nhin danh sach GameObject
/// trong Hierarchy khong bao gio thay duoc.
///
/// Bang chu o duoi doc tu tren xuong = tu Bac xuong Nam (z giam dan).
///
/// Ky tu:
///   .  ngoai map (khong co gi, se thanh tuong bao)
///   #  duong
///   x  duong co vach qua duong
///   S  block nha nho, day dac
///   W  thap cao (nguon vat the bac 3-4, mo khoa o cap 20 va 30)
///   L  block nha hinh L
///   P  cong vien co
///   O  quang truong tron
///   H  quang truong bat giac
///   T  vuon cay
///   C  bai do xe
///
/// Sau khi dung xong, cong cu tu kiem tra: block nao khong giap duong, va duong nao
/// bi tach roi khoi mang chinh. Hai loi nay khong nhin ra bang mat khi map to.
/// </summary>
public static class MapBuilder
{
    private const string MenuRoot = "Tools/Devour/";
    private const string MapRootName = "Map";
    private const string OldMapName = "Map_Old";

    /// <summary>Canh mot o, tinh bang don vi world.</summary>
    private const float Cell = 7f;

    private const float RoadY = 0.02f;      // duong dat hoi cao hon via he cho khong bi z-fighting
    private const float MarkY = 0.05f;      // vach ke cao hon duong
    private const float WallHeight = 3f;
    private const float WallThickness = 1.2f;

    /// <summary>
    /// Bo cuc map. Moi dong dai bang nhau, moi ky tu la mot o.
    ///
    /// Hinh dang: mot nhanh hep o phia Bac, phinh ra o giua, hai canh tay trai/phai
    /// duoc noi bang mot truc duong lon chay ngang, roi mot vung rong hon o phia Nam
    /// lech sang trai. Bien vien rang cua chu khong phai hinh chu nhat.
    /// </summary>
    private static readonly string[] Layout = new string[]
    {
        //         1111111111
        //234567890123456789012
        "..........#######.....", //  0
        "..........#SW#PP#.....", //  1
        "..........#SS#PP#.....", //  2
        "..........#x###x#.....", //  3
        "..........#PP#SW#H....", //  4
        "..........#PP#SS##....", //  5
        ".....##############...", //  6
        "....#SW#O#SS#LLL#S#...", //  7
        "....#SS###SW#LLL#W#...", //  8
        "....#x#####x####x##...", //  9
        "....#SS#SW#TT#SS#S#...", // 10
        "....#SS#SS#TT#SW#W#...", // 11
        "######################", // 12  <- truc duong lon, keo dai ra hai canh tay
        "....#SW#LL#OO#SS#S#...", // 13
        "....#SS#LL#OO#SW#W#...", // 14
        "....##x####x###x###...", // 15
        "...#PP#CC#SW#SS#......", // 16
        "...#PP#CC#SS#SW#......", // 17
        "...####x########......", // 18
        "...#SW#SS#LL#S#.......", // 19
        "...#SS#SW#LL#W#.......", // 20
        "...#############......", // 21
        "....#SS#SSW#S#........", // 22
        "....#SW#SSS#W#........", // 23
        "....###########.......", // 24
    };

    private static Material _road, _sidewalk, _grass, _wall, _mark, _accent;
    private static GameObject[] _buildingPrefabs;
    private static GameObject _treePrefab;
    private static GameObject[] _carPrefabs;

    [MenuItem(MenuRoot + "Dung lai map tu bang chu", false, 100)]
    private static void Build()
    {
        if (!EditorUtility.DisplayDialog("Dung lai map",
            "Map hien tai se duoc doi ten thanh '" + OldMapName + "' va tat di (khong xoa), " +
            "roi dung map moi tu bang chu trong MapBuilder.cs.\n\nTiep tuc?", "Dung", "Thoi"))
            return;

        BuildNow();
    }

    /// <summary>Dung map, khong hoi gi. Tach ra de goi duoc tu script/cong cu khac.</summary>
    public static void BuildNow()
    {
        if (!LoadAssets()) return;

        int rows = Layout.Length;
        int cols = Layout[0].Length;
        for (int r = 0; r < rows; r++)
        {
            if (Layout[r].Length == cols) continue;
            Debug.LogError("[MapBuilder] Dong " + r + " dai " + Layout[r].Length + " chu, khac cac dong khac (" + cols + ").");
            return;
        }

        // Doi ten map cu thay vi xoa: sai gi con bat lai duoc
        GameObject old = GameObject.Find(MapRootName);
        if (old != null)
        {
            Undo.RecordObject(old, "Dung lai map");
            old.name = OldMapName;
            old.SetActive(false);
        }

        GameObject root = new GameObject(MapRootName);
        Undo.RegisterCreatedObjectUndo(root, "Dung lai map");

        Transform ground = NewGroup(root.transform, "Ground");
        Transform roads = NewGroup(root.transform, "Roads");
        Transform walls = NewGroup(root.transform, "Walls");
        Transform blocks = NewGroup(root.transform, "Blocks");
        Transform props = NewGroup(root.transform, "Props");

        BuildGround(ground, roads);
        BuildWalls(walls);
        BuildBlocks(blocks, props);

        Validate();

        Selection.activeGameObject = root;
        EditorSceneManagerMarkDirty();

        Debug.Log("[MapBuilder] Xong. Map " + (cols * Cell) + " x " + (rows * Cell) + " don vi, "
                + CountCells('#', 'x') + " o duong, " + (root.GetComponentsInChildren<Transform>().Length - 1) + " object.");
    }

    // ------------------------------------------------------------------ tien ich

    private static char At(int col, int row)
    {
        if (row < 0 || row >= Layout.Length) return '.';
        if (col < 0 || col >= Layout[row].Length) return '.';
        return Layout[row][col];
    }

    private static bool IsVoid(int col, int row) { return At(col, row) == '.'; }
    private static bool IsRoad(int col, int row) { char c = At(col, row); return c == '#' || c == 'x'; }
    private static bool IsBlock(int col, int row) { return !IsVoid(col, row) && !IsRoad(col, row); }

    /// <summary>Tam cua o trong world. Hang 0 nam o phia z lon nhat (Bac).</summary>
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

    private static int CountCells(params char[] chars)
    {
        int n = 0;
        for (int r = 0; r < Layout.Length; r++)
            for (int c = 0; c < Layout[r].Length; c++)
                for (int k = 0; k < chars.Length; k++)
                    if (Layout[r][c] == chars[k]) n++;
        return n;
    }

    private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go;
    }

    private static GameObject Cyl(Transform parent, string name, Vector3 center, float diameter, float height, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center + Vector3.up * (height * 0.5f);
        go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);   // cylinder cao 2 don vi
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go;
    }

    private static bool LoadAssets()
    {
        _road = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Road.mat");
        _sidewalk = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Sidewalk.mat");
        _grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Grass.mat");
        _wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Wall.mat");
        _accent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Accent.mat");
        _mark = _sidewalk;

        if (_road == null || _sidewalk == null || _grass == null || _wall == null)
        {
            Debug.LogError("[MapBuilder] Thieu material trong Assets/Materials (M_Road, M_Sidewalk, M_Grass, M_Wall).");
            return false;
        }

        List<GameObject> bs = new List<GameObject>();
        string[] names = new string[] { "A", "B", "C", "D", "E" };
        for (int i = 0; i < names.Length; i++)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/Building_" + names[i] + ".prefab");
            if (p != null) bs.Add(p);
        }
        _buildingPrefabs = bs.ToArray();

        List<GameObject> cs = new List<GameObject>();
        string[] cars = new string[] { "A", "B", "C" };
        for (int i = 0; i < cars.Length; i++)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Car_" + cars[i] + ".prefab");
            if (p != null) cs.Add(p);
        }
        _carPrefabs = cs.ToArray();

        _treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Tree.prefab");

        if (_buildingPrefabs.Length == 0)
        {
            Debug.LogError("[MapBuilder] Khong tim thay prefab toa nha nao trong Assets/Prefabs/Buildings.");
            return false;
        }
        return true;
    }

    // -------------------------------------------------------------- nen va duong

    /// <summary>
    /// Nen va duong duoc gop theo tung doan ngang lien nhau thay vi moi o mot khoi.
    /// Map nay hon 300 o; khong gop thi rieng phan nen da hon 300 renderer, ma no
    /// chi la may cai mat phang phang li.
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
                Vector3 size = new Vector3(len * Cell, road ? 0.04f : 0.2f, Cell);

                if (road) Box(roads, "Road_" + r + "_" + start, center, size, _road);
                else Box(ground, "Ground_" + r + "_" + start, center, size, _sidewalk);
            }
        }

        // Vach qua duong o cac o danh dau 'x'
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
                    Vector3 ss = vertical ? new Vector3(0.55f, 0.03f, Cell * 0.62f) : new Vector3(Cell * 0.62f, 0.03f, 0.55f);
                    Box(roads, "Crosswalk_" + r + "_" + c + "_" + i, sp, ss, _mark);
                }
            }
    }

    /// <summary>
    /// Tuong bao: quet moi canh cua o co noi dung ma phia ben kia la khoang trong.
    /// Nho vay bien map bat quy tac den dau cung tu co tuong dung, khong phai ve tay.
    /// Cac doan thang lien nhau duoc gop lai lam mot.
    /// </summary>
    private static void BuildWalls(Transform walls)
    {
        int rows = Layout.Length, cols = Layout[0].Length;
        float half = Cell * 0.5f;

        // Canh ngang (tuong chay theo truc X), quet tung hang
        for (int r = 0; r <= rows; r++)
            for (int side = 0; side < 2; side++)
            {
                int c = 0;
                while (c < cols)
                {
                    bool here = !IsVoid(c, r - 1 + side * 1) && IsVoid(c, r + side * 1);
                    bool need = side == 0
                        ? (!IsVoid(c, r) && IsVoid(c, r - 1))     // canh phia Bac cua o
                        : (!IsVoid(c, r) && IsVoid(c, r + 1));    // canh phia Nam
                    if (!need) { c++; continue; }

                    int start = c;
                    while (c < cols && (side == 0 ? (!IsVoid(c, r) && IsVoid(c, r - 1))
                                                  : (!IsVoid(c, r) && IsVoid(c, r + 1)))) c++;

                    int len = c - start;
                    Vector3 a = CellCenter(start, r);
                    float z = a.z + (side == 0 ? half : -half);
                    Vector3 center = new Vector3(a.x + (len - 1) * Cell * 0.5f, WallHeight * 0.5f, z);
                    Box(walls, "Wall_H_" + r + "_" + start, center,
                        new Vector3(len * Cell + WallThickness, WallHeight, WallThickness), _wall);
                }
            }

        // Canh doc (tuong chay theo truc Z), quet tung cot
        for (int c = 0; c <= cols; c++)
            for (int side = 0; side < 2; side++)
            {
                int r = 0;
                while (r < rows)
                {
                    bool need = side == 0
                        ? (!IsVoid(c, r) && IsVoid(c - 1, r))     // canh phia Tay
                        : (!IsVoid(c, r) && IsVoid(c + 1, r));    // canh phia Dong
                    if (!need) { r++; continue; }

                    int start = r;
                    while (r < rows && (side == 0 ? (!IsVoid(c, r) && IsVoid(c - 1, r))
                                                  : (!IsVoid(c, r) && IsVoid(c + 1, r)))) r++;

                    int len = r - start;
                    Vector3 a = CellCenter(c, start);
                    float x = a.x + (side == 0 ? -half : half);
                    Vector3 center = new Vector3(x, WallHeight * 0.5f, a.z - (len - 1) * Cell * 0.5f);
                    Box(walls, "Wall_V_" + c + "_" + start, center,
                        new Vector3(WallThickness, WallHeight, len * Cell + WallThickness), _wall);
                }
            }
    }

    // ------------------------------------------------------------------- block

    private static void BuildBlocks(Transform blocks, Transform props)
    {
        Random.InitState(20260802);     // co dinh hat de dung lai bao nhieu lan cung ra y het

        int rows = Layout.Length, cols = Layout[0].Length;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                char k = At(c, r);
                Vector3 p = CellCenter(c, r);

                if (k == 'S') BuildSmallBlock(blocks, p, r, c);
                else if (k == 'W') BuildTower(blocks, p, r, c);
                else if (k == 'L') BuildLBlock(blocks, p, r, c);
                else if (k == 'P') BuildPark(blocks, props, p, r, c);
                else if (k == 'O') BuildRound(blocks, p, r, c);
                else if (k == 'H') BuildOcta(blocks, p, r, c);
                else if (k == 'T') BuildTrees(props, p, r, c);
                else if (k == 'C') BuildParking(blocks, props, p, r, c);
            }
    }

    private static GameObject SpawnBuilding(Transform parent, Vector3 pos, float w, float d, float h, string name)
    {
        GameObject prefab = _buildingPrefabs[Random.Range(0, _buildingPrefabs.Length)];
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(w, h, d);
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        Transform[] kids = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < kids.Length; i++) GameObjectUtility.SetStaticEditorFlags(kids[i].gameObject, 0);
        return go;
    }

    private static void BuildSmallBlock(Transform parent, Vector3 p, int r, int c)
    {
        // Bon o vuong nho trong mot o, bo bot ngau nhien mot vai cai cho khoi deu tam tap
        float q = Cell * 0.25f;
        for (int i = 0; i < 4; i++)
        {
            if (Random.value < 0.25f) continue;
            float ox = (i % 2 == 0 ? -q : q);
            float oz = (i < 2 ? q : -q);
            float w = Random.Range(2.0f, 2.9f);
            float h = Random.Range(2.5f, 7.5f);
            SpawnBuilding(parent, new Vector3(p.x + ox, 0f, p.z + oz), w, w, h, "Bld_" + r + "_" + c + "_" + i);
        }
    }

    /// <summary>
    /// Thap cao. Day la nguon vat the bac 3 va bac 4 duy nhat cua map.
    ///
    /// Ban kinh mot vat the tinh tu bounds, ma nha thap thi ban kinh khong bao gio
    /// vuot nguong bac 3 (4.4) du co ke sat nhau bao nhieu - chieu cao moi la thu
    /// keo ban kinh len. Khong co may cai thap nay thi len cap 20 va 30 se mo khoa
    /// ra mot danh sach rong.
    /// </summary>
    private static void BuildTower(Transform parent, Vector3 p, int r, int c)
    {
        int n = Random.value < 0.45f ? 2 : 1;
        for (int i = 0; i < n; i++)
        {
            float w = Random.Range(3.2f, 4.4f);
            float h = Random.Range(9.5f, 16f);          // >=9 ra bac 3, >=11 ra bac 4
            float ox = n == 1 ? 0f : (i == 0 ? -Cell * 0.2f : Cell * 0.2f);
            float oz = n == 1 ? 0f : (i == 0 ? Cell * 0.16f : -Cell * 0.16f);
            if (n == 2) { w *= 0.72f; h *= 0.92f; }
            SpawnBuilding(parent, new Vector3(p.x + ox, 0f, p.z + oz), w, w, h, "Tower_" + r + "_" + c + "_" + i);
        }
    }

    private static void BuildLBlock(Transform parent, Vector3 p, int r, int c)
    {
        // Nha hinh L: hai thanh ghep vuong goc, xoay ngau nhien mot trong bon huong
        float h = Random.Range(3f, 5.5f);
        float armLong = Cell * 0.72f, armShort = Cell * 0.3f;
        int dir = Random.Range(0, 4);
        float sx = (dir == 0 || dir == 3) ? 1f : -1f;
        float sz = (dir < 2) ? 1f : -1f;

        SpawnBuilding(parent, new Vector3(p.x, 0f, p.z + sz * (Cell * 0.5f - armShort * 0.5f) * 0.6f),
                      armLong, armShort, h, "BldL_" + r + "_" + c + "_a");
        SpawnBuilding(parent, new Vector3(p.x + sx * (Cell * 0.5f - armShort * 0.5f) * 0.6f, 0f, p.z),
                      armShort, armLong, h, "BldL_" + r + "_" + c + "_b");
    }

    private static void BuildPark(Transform parent, Transform props, Vector3 p, int r, int c)
    {
        Box(parent, "Park_" + r + "_" + c, new Vector3(p.x, 0.12f, p.z),
            new Vector3(Cell * 0.86f, 0.16f, Cell * 0.86f), _grass);

        if (_treePrefab == null) return;
        int n = Random.Range(2, 5);
        for (int i = 0; i < n; i++)
        {
            Vector3 tp = new Vector3(p.x + Random.Range(-Cell * 0.32f, Cell * 0.32f), 0f,
                                     p.z + Random.Range(-Cell * 0.32f, Cell * 0.32f));
            GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(_treePrefab, props);
            t.name = "Tree_" + r + "_" + c + "_" + i;
            t.transform.position = tp;
            float s = Random.Range(0.75f, 1.1f);
            t.transform.localScale = new Vector3(s, s, s);
            GameObjectUtility.SetStaticEditorFlags(t, 0);
        }
    }

    private static void BuildRound(Transform parent, Vector3 p, int r, int c)
    {
        Box(parent, "Plaza_" + r + "_" + c, new Vector3(p.x, 0.1f, p.z),
            new Vector3(Cell * 0.9f, 0.12f, Cell * 0.9f), _sidewalk);

        GameObject go = Cyl(parent, "Round_" + r + "_" + c, new Vector3(p.x, 0f, p.z),
                            Cell * 0.6f, Random.Range(3f, 6f), _accent != null ? _accent : _sidewalk);
        Devourable d = go.AddComponent<Devourable>();
        d.autoLevelFromSize = true;
    }

    private static void BuildOcta(Transform parent, Vector3 p, int r, int c)
    {
        Box(parent, "Plaza_" + r + "_" + c, new Vector3(p.x, 0.1f, p.z),
            new Vector3(Cell * 0.9f, 0.12f, Cell * 0.9f), _sidewalk);

        // "Bat giac" gan dung: hai khoi hop xoay 45 do chong len nhau
        float w = Cell * 0.5f, h = Random.Range(2.5f, 4f);
        GameObject a = Box(parent, "Octa_" + r + "_" + c + "_a", new Vector3(p.x, h * 0.5f, p.z),
                           new Vector3(w, h, w), _grass);
        GameObject b = Box(parent, "Octa_" + r + "_" + c + "_b", new Vector3(p.x, h * 0.5f, p.z),
                           new Vector3(w, h * 0.98f, w), _grass);
        b.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        b.transform.SetParent(a.transform, true);
        a.AddComponent<Devourable>().autoLevelFromSize = true;
    }

    private static void BuildTrees(Transform props, Vector3 p, int r, int c)
    {
        if (_treePrefab == null) return;
        for (int i = 0; i < 4; i++)
        {
            float ox = (i % 2 == 0 ? -1f : 1f) * Cell * 0.22f;
            float oz = (i < 2 ? 1f : -1f) * Cell * 0.22f;
            GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(_treePrefab, props);
            t.name = "Tree_" + r + "_" + c + "_" + i;
            t.transform.position = new Vector3(p.x + ox, 0f, p.z + oz);
            float s = Random.Range(0.8f, 1.15f);
            t.transform.localScale = new Vector3(s, s, s);
            GameObjectUtility.SetStaticEditorFlags(t, 0);
        }
    }

    private static void BuildParking(Transform parent, Transform props, Vector3 p, int r, int c)
    {
        Box(parent, "Lot_" + r + "_" + c, new Vector3(p.x, 0.06f, p.z),
            new Vector3(Cell * 0.9f, 0.08f, Cell * 0.9f), _road);

        for (int i = 0; i < 4; i++)
            Box(parent, "LotLine_" + r + "_" + c + "_" + i,
                new Vector3(p.x - Cell * 0.3f + i * Cell * 0.2f, 0.1f, p.z),
                new Vector3(0.15f, 0.03f, Cell * 0.6f), _sidewalk);

        if (_carPrefabs.Length == 0) return;
        int n = Random.Range(2, 4);
        for (int i = 0; i < n; i++)
        {
            GameObject prefab = _carPrefabs[Random.Range(0, _carPrefabs.Length)];
            GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props);
            car.name = "Car_" + r + "_" + c + "_" + i;
            car.transform.position = new Vector3(p.x - Cell * 0.22f + i * Cell * 0.22f, 0f, p.z);
            car.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            GameObjectUtility.SetStaticEditorFlags(car, 0);
        }
    }

    // ---------------------------------------------------------------- kiem tra

    /// <summary>
    /// Hai loi khong nhin ra bang mat khi map to:
    ///   1. Block khong giap duong nao -> nguoi choi khong bao gio toi duoc
    ///   2. Duong bi tach thanh nhieu manh roi rac -> co vung khong di sang duoc
    /// </summary>
    private static void Validate()
    {
        int rows = Layout.Length, cols = Layout[0].Length;

        int orphanBlocks = 0;
        string firstOrphan = string.Empty;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!IsBlock(c, r)) continue;
                if (IsRoad(c - 1, r) || IsRoad(c + 1, r) || IsRoad(c, r - 1) || IsRoad(c, r + 1)) continue;
                orphanBlocks++;
                if (firstOrphan == string.Empty) firstOrphan = "(cot " + c + ", dong " + r + ")";
            }

        // Loang tu o duong dau tien de xem co bao nhieu o duong toi duoc
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
                Vector2Int[] next = new Vector2Int[] {
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

        if (orphanBlocks > 0)
            Debug.LogWarning("[MapBuilder] " + orphanBlocks + " block khong giap duong nao, vd " + firstOrphan
                           + ". Nguoi choi khong toi duoc cho do.");

        if (reached < total)
            Debug.LogWarning("[MapBuilder] Duong bi tach roi: chi " + reached + "/" + total
                           + " o duong noi voi nhau. Co vung khong di sang duoc.");

        if (orphanBlocks == 0 && reached == total)
            Debug.Log("[MapBuilder] Kiem tra dat: moi block deu giap duong, " + total + " o duong lien mach.");
    }

    private static void EditorSceneManagerMarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
