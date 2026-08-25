using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector cho AreaItemSpawner: cho chon item bang DROPDOWN thay vi keo tha prefab.
///
/// Thu vien duoc QUET tu thu muc chu khong khai bao cung trong code - tha prefab moi vao
/// Props/Lv_3 la no tu hien trong dropdown, khong phai sua gi.
///
/// LEVEL DOC TU DU LIEU, KHONG DOAN THEO TEN: item la Prefab Variant nen requiredLevel la thua ke -
/// doc bang GetComponent tren asset da load ra so that. Doan theo ten thu muc/ten file thi mot cai
/// dat nham cho la ca bang sai ma khong ai biet.
///
/// Nhan dropdown hinh 'Lv1/Bottle' - dau / lam Unity tu bo thanh menu con, nen chon level truoc
/// roi chon item, dung nhu cach nghi khi rai do.
/// </summary>
[CustomEditor(typeof(AreaItemSpawner))]
public class AreaItemSpawnerEditor : Editor
{
    private static readonly string[] ScanFolders =
    {
        "Assets/Devours/Prefabs/Props",
        "Assets/Devours/Prefabs/Items",
    };

    private GameObject[] _libPrefabs;
    private string[] _libLabels;

    /// <summary>
    /// Cache ban kinh, SONG QUA nhieu lan repaint.
    ///
    /// FootprintRadius phai dung thu prefab ra do roi huy. OnInspectorGUI chay lai moi lan re chuot,
    /// nen cache tao moi trong ham ve dong nghia voi dung/huy prefab hang chuc lan MOI GIAY - Editor
    /// giat va scene bi danh dau ban lien tuc. Giu o day va chi xoa khi quet lai thu vien.
    /// </summary>
    private readonly Dictionary<GameObject, float> _radiusCache = new Dictionary<GameObject, float>();

    private void OnEnable() { ScanLibrary(); }

    public override void OnInspectorGUI()
    {
        var me = (AreaItemSpawner)target;

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "entries");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawZoneInfo(me);

        EditorGUILayout.Space();
        DrawEntries(me);

        EditorGUILayout.Space();
        DrawButtons(me);
    }

    // ---------------------------------------------------------------- vung

    private void DrawZoneInfo(AreaItemSpawner me)
    {
        Bounds zone;
        if (!me.TryGetZone(out zone))
        {
            EditorGUILayout.HelpBox(
                "Object nay khong co Renderer nen khong biet vung rai o dau.\n" +
                "Gan script len object 'Area N' (mesh Plane) thi vung rai tu lay theo no.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Vung rai",
            zone.size.x.ToString("F1") + " x " + zone.size.z.ToString("F1") + " u" +
            "   (dien tich " + (zone.size.x * zone.size.z).ToString("F0") + " u2)");
    }

    // ---------------------------------------------------------------- bang rai

    private void DrawEntries(AreaItemSpawner me)
    {
        EditorGUILayout.LabelField("Bang rai", EditorStyles.boldLabel);

        if (_libPrefabs == null || _libPrefabs.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Khong quet duoc item nao. Item phai la prefab CO PhysicsDevourable, nam trong:\n  " +
                string.Join("\n  ", ScanFolders), MessageType.Warning);
        }

        int removeAt = -1;
        for (int i = 0; i < me.entries.Count; i++)
        {
            AreaItemEntry entry = me.entries[i];
            if (entry == null) { me.entries[i] = entry = new AreaItemEntry(); }

            EditorGUILayout.BeginHorizontal();

            int cur = IndexOf(entry.prefab);
            int next = EditorGUILayout.Popup(cur, Labels());
            if (next != cur && next >= 0 && next < _libPrefabs.Length)
            {
                Undo.RecordObject(me, "Doi item");
                entry.prefab = _libPrefabs[next];
                EditorUtility.SetDirty(me);
            }

            int count = EditorGUILayout.IntField(entry.count, GUILayout.Width(60));
            if (count != entry.count)
            {
                Undo.RecordObject(me, "Doi so luong");
                entry.count = Mathf.Max(0, count);
                EditorUtility.SetDirty(me);
            }

            if (GUILayout.Button("X", GUILayout.Width(22))) removeAt = i;

            EditorGUILayout.EndHorizontal();

            if (entry.prefab != null && cur < 0)
                EditorGUILayout.HelpBox("'" + entry.prefab.name + "' khong con trong thu vien.",
                                        MessageType.Warning);
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(me, "Xoa dong");
            me.entries.RemoveAt(removeAt);
            EditorUtility.SetDirty(me);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Them dong"))
        {
            Undo.RecordObject(me, "Them dong");
            me.entries.Add(new AreaItemEntry());
            EditorUtility.SetDirty(me);
        }
        if (GUILayout.Button("Quet lai thu vien")) ScanLibrary();
        EditorGUILayout.EndHorizontal();

        DrawCapacity(me);
    }

    /// <summary>
    /// Uoc luong truoc xem co nhet vua khong. Xep tron ngau nhien thi hieu suat khoang 0.7 dien tich
    /// - bao TRUOC con hon de nguoi dung bam Gen roi moi thay thieu 30 cai.
    /// </summary>
    private void DrawCapacity(AreaItemSpawner me)
    {
        Bounds zone;
        if (!me.TryGetZone(out zone)) return;

        int total = me.TotalRequested();
        if (total <= 0) return;

        float need = 0f;
        for (int i = 0; i < me.entries.Count; i++)
        {
            AreaItemEntry e = me.entries[i];
            if (e == null || e.prefab == null || e.count <= 0) continue;
            float r = AreaItemSpawner.FootprintRadius(e.prefab, _radiusCache) + me.padding * 0.5f;
            need += e.count * Mathf.PI * r * r;
        }

        float have = zone.size.x * zone.size.z * 0.7f;
        string line = "Tong: " + total + " item   |   chiem ~" + need.ToString("F0") +
                      " u2 / suc chua ~" + have.ToString("F0") + " u2";

        if (need > have)
            EditorGUILayout.HelpBox(line + "\nQUA TAI - gen se thieu. Giam so luong, ha padding, hoac keo Area to ra.",
                                    MessageType.Warning);
        else
            EditorGUILayout.HelpBox(line, MessageType.Info);
    }

    // ---------------------------------------------------------------- nut

    private void DrawButtons(AreaItemSpawner me)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Gen item", GUILayout.Height(26))) me.Generate();
        if (GUILayout.Button("Xoa item da gen", GUILayout.Height(26))) me.Clear();
        EditorGUILayout.EndHorizontal();

        Transform c = me.transform.Find(AreaItemSpawner.ContainerName);
        EditorGUILayout.LabelField("Dang co tren scene",
            c == null ? "0 item" : c.childCount + " item");
    }

    // ---------------------------------------------------------------- thu vien

    /// <summary>
    /// HANG (1..6) suy ra tu requiredLevel qua dung bang moc trong SuctionConfig - khong doc ten
    /// thu muc, khong doc ten file. Doi moc trong config la ca tool tu theo.
    ///
    /// Vi sao phai hien hang chu khong chi hien requiredLevel: nguoi dung nghi theo "level 1..6",
    /// con so that trong data la 1/10/25/50/110/250. Chi hien "Lv10" thi ai cung tuong do la hang 10.
    /// </summary>
    private static int TierOf(int requiredLevel, List<LevelStep> steps)
    {
        if (steps == null) return 0;
        int tier = 1;
        for (int i = 0; i < steps.Count; i++)
        {
            LevelStep st = steps[i];
            if (st == null || st.level < 2 || requiredLevel < st.level) continue;
            tier++;
        }
        return tier;
    }

    private static List<LevelStep> LoadLevelSteps()
    {
        string[] guids = AssetDatabase.FindAssets("t:SuctionConfig");
        if (guids.Length == 0) return null;

        var cfg = AssetDatabase.LoadAssetAtPath<SuctionConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        return cfg != null ? cfg.levelSteps : null;
    }

    private void ScanLibrary()
    {
        _radiusCache.Clear();

        var prefabs = new List<GameObject>();
        var levels = new List<int>();
        List<LevelStep> steps = LoadLevelSteps();

        string[] guids = AssetDatabase.FindAssets("t:Prefab", ScanFolders);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);

            // Base la khuon de lam variant, Fly la vat bay tu quan ly vi tri - ca hai khong phai
            // do de rai tren dat.
            if (file.StartsWith("Item_Base") || file.StartsWith("Item_Fly")) continue;

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            PhysicsDevourable pd = go.GetComponent<PhysicsDevourable>();
            if (pd == null) continue;             // khong an duoc thi khong phai item

            prefabs.Add(go);
            levels.Add(pd.requiredLevel);
        }

        // Sap theo hang roi theo ten, de menu con 'Lv1 / Lv10 / Lv25...' co thu tu on dinh.
        int[] order = new int[prefabs.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        System.Array.Sort(order, (a, b) =>
        {
            int d = levels[a].CompareTo(levels[b]);
            return d != 0 ? d : string.Compare(prefabs[a].name, prefabs[b].name, System.StringComparison.Ordinal);
        });

        _libPrefabs = new GameObject[order.Length];
        _libLabels = new string[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            int k = order[i];
            _libPrefabs[i] = prefabs[k];
            int tier = TierOf(levels[k], steps);
            _libLabels[i] = (tier > 0 ? "Hang " + tier + " - Lv" + levels[k] : "Lv" + levels[k])
                            + "/" + prefabs[k].name;
        }

        Repaint();
    }

    private string[] Labels()
    {
        return _libLabels != null ? _libLabels : new string[0];
    }

    private int IndexOf(GameObject prefab)
    {
        if (prefab == null || _libPrefabs == null) return -1;
        for (int i = 0; i < _libPrefabs.Length; i++)
            if (_libPrefabs[i] == prefab) return i;
        return -1;
    }
}
