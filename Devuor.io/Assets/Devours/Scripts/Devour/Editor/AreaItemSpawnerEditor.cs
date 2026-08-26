using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector cho AreaItemSpawner: KEO PREFAB TRUC TIEP vao tung dong, khong quet thu vien.
///
/// Truoc day dong nay la mot dropdown quet tu Props/Lv_* + Prefabs/Items, ep item phai nam dung
/// thu muc va co PhysicsDevourable moi hien ra. Gio ObjectField nhan bat ky prefab nao, keo tha
/// thang tu Project window - linh hoat hon nhung khong con canh bao "sai thu muc/thieu component".
/// </summary>
[CustomEditor(typeof(AreaItemSpawner))]
public class AreaItemSpawnerEditor : Editor
{
    /// <summary>
    /// Cache ban kinh, SONG QUA nhieu lan repaint.
    ///
    /// FootprintRadius phai dung thu prefab ra do roi huy. OnInspectorGUI chay lai moi lan re chuot,
    /// nen cache tao moi trong ham ve dong nghia voi dung/huy prefab hang chuc lan MOI GIAY - Editor
    /// giat va scene bi danh dau ban lien tuc.
    /// </summary>
    private readonly Dictionary<GameObject, float> _radiusCache = new Dictionary<GameObject, float>();

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

        int removeAt = -1;
        for (int i = 0; i < me.entries.Count; i++)
        {
            AreaItemEntry entry = me.entries[i];
            if (entry == null) { me.entries[i] = entry = new AreaItemEntry(); }

            EditorGUILayout.BeginHorizontal();

            GameObject next = (GameObject)EditorGUILayout.ObjectField(
                entry.prefab, typeof(GameObject), false);
            if (next != entry.prefab)
            {
                Undo.RecordObject(me, "Doi item");
                entry.prefab = next;
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
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(me, "Xoa dong");
            me.entries.RemoveAt(removeAt);
            EditorUtility.SetDirty(me);
        }

        if (GUILayout.Button("+ Them dong"))
        {
            Undo.RecordObject(me, "Them dong");
            me.entries.Add(new AreaItemEntry());
            EditorUtility.SetDirty(me);
        }

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
}
