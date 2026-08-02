using UnityEditor;
using UnityEngine;

/// <summary>
/// Vai lenh menu cho do phai keo tha tay tung vat the:
/// - Gan Devourable hang loat cho prop trong scene hoac cho prefab trong Project
/// - Gan MouthSuction + tao san object con Mouth cho nhan vat
/// </summary>
public static class DevourMenu
{
    private const string MenuRoot = "Tools/Devour/";
    private const string VfxPrefabPath = "Assets/Prefabs/VFX/SuctionVFX.prefab";

    [MenuItem(MenuRoot + "Gan Devourable cho doi tuong dang chon", false, 10)]
    private static void AddDevourableToSelection()
    {
        int added = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(go);

            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                if (root.GetComponent<Devourable>() == null)
                {
                    root.AddComponent<Devourable>();
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    added++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            else if (go.GetComponent<Devourable>() == null)
            {
                Undo.AddComponent<Devourable>(go);
                added++;
            }
        }

        Debug.Log("[Devour] Da gan Devourable cho " + added + " doi tuong.");
    }

    [MenuItem(MenuRoot + "Gan Devourable cho doi tuong dang chon", true)]
    private static bool AddDevourableToSelectionValidate()
    {
        return Selection.gameObjects.Length > 0;
    }

    /// <summary>
    /// Vat the co Batching Static se khong bay duoc: Unity gop mesh cua no vao mot mesh
    /// chung o world space, transform di chuyen thi mesh dung yen. Nhin ra ngoai thi
    /// vat the bi "bien mat tai cho" thay vi bay vao mieng.
    ///
    /// Chay lenh nay sau moi lan them Devourable cho do trang tri map (toa nha, tuong...)
    /// vi may thu do gan nhu luon duoc danh dau static san.
    /// </summary>
    [MenuItem(MenuRoot + "Xoa Static Flags cho moi Devourable", false, 13)]
    private static void ClearStaticFlagsOnDevourables()
    {
        Devourable[] all = Object.FindObjectsByType<Devourable>(FindObjectsSortMode.None);
        int cleared = 0;

        for (int i = 0; i < all.Length; i++)
        {
            Transform[] kids = all[i].GetComponentsInChildren<Transform>(true);
            for (int k = 0; k < kids.Length; k++)
            {
                GameObject go = kids[k].gameObject;
                if (GameObjectUtility.GetStaticEditorFlags(go) == 0) continue;

                Undo.RecordObject(go, "Xoa Static Flags");
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                EditorUtility.SetDirty(go);
                cleared++;
            }
        }

        Debug.Log("[Devour] Da xoa static flags tren " + cleared + " object (" + all.Length + " Devourable).");
    }

    [MenuItem(MenuRoot + "Setup vung hut cho nhan vat dang chon", false, 11)]
    private static void SetupMouthSuction()
    {
        GameObject player = Selection.activeGameObject;
        if (player == null) return;

        MouthSuction suction = player.GetComponent<MouthSuction>();
        if (suction == null) suction = Undo.AddComponent<MouthSuction>(player);

        if (suction.mouth == null)
        {
            Transform mouth = player.transform.Find(MouthSuction.MouthObjectName);
            if (mouth == null)
            {
                GameObject go = new GameObject(MouthSuction.MouthObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Tao Mouth");
                Undo.SetTransformParent(go.transform, player.transform, "Tao Mouth");

                go.transform.localPosition = suction.mouthLocalOffset;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                mouth = go.transform;
            }

            Undo.RecordObject(suction, "Setup vung hut");
            suction.mouth = mouth;
            EditorUtility.SetDirty(suction);
        }

        Selection.activeGameObject = player;
        Debug.Log("[Devour] Da setup MouthSuction tren " + player.name + ".");
    }

    [MenuItem(MenuRoot + "Setup vung hut cho nhan vat dang chon", true)]
    private static bool SetupMouthSuctionValidate()
    {
        return Selection.activeGameObject != null && AssetDatabase.GetAssetPath(Selection.activeGameObject) == string.Empty;
    }

    [MenuItem(MenuRoot + "Gan SuctionReactor (cong theo gio) cho doi tuong dang chon", false, 12)]
    private static void AddReactorToSelection()
    {
        int added = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(go);

            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                if (root.GetComponent<SuctionReactor>() == null)
                {
                    root.AddComponent<SuctionReactor>();
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    added++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            else if (go.GetComponent<SuctionReactor>() == null)
            {
                Undo.AddComponent<SuctionReactor>(go);
                added++;
            }
        }

        Debug.Log("[Devour] Da gan SuctionReactor cho " + added + " doi tuong.");
    }

    [MenuItem(MenuRoot + "Gan SuctionReactor (cong theo gio) cho doi tuong dang chon", true)]
    private static bool AddReactorToSelectionValidate()
    {
        return Selection.gameObjects.Length > 0;
    }

    [MenuItem(MenuRoot + "Gan prefab VFX vao nhan vat dang chon", false, 30)]
    private static void AttachVfxPrefab()
    {
        GameObject player = Selection.activeGameObject;
        if (player == null) return;

        MouthSuction suction = player.GetComponent<MouthSuction>();
        if (suction == null)
        {
            Debug.LogWarning("[Devour] " + player.name + " chua co MouthSuction. Chay 'Setup vung hut...' truoc.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[Devour] Khong tim thay prefab o " + VfxPrefabPath);
            return;
        }

        Transform mouth = suction.EnsureMouth();

        // Da co roi thi khong nhan ban them, chi noi lai reference cho chac
        SuctionConeVfx existing = mouth.GetComponentInChildren<SuctionConeVfx>(true);
        if (existing == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mouth);
            Undo.RegisterCreatedObjectUndo(instance, "Gan prefab VFX");

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            existing = instance.GetComponent<SuctionConeVfx>();
        }

        Undo.RecordObject(suction, "Gan prefab VFX");
        suction.vfx = existing;
        EditorUtility.SetDirty(suction);

        Debug.Log("[Devour] Da gan prefab VFX vao " + player.name + ".");
    }

    [MenuItem(MenuRoot + "Gan prefab VFX vao nhan vat dang chon", true)]
    private static bool AttachVfxPrefabValidate()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<MouthSuction>() != null;
    }

    [MenuItem(MenuRoot + "Khop VFX voi Range/Cone Angle cua nhan vat dang chon", false, 31)]
    private static void FitVfxToCone()
    {
        GameObject player = Selection.activeGameObject;
        if (player == null) return;

        MouthSuction suction = player.GetComponent<MouthSuction>();
        if (suction == null) return;

        SuctionConeVfx vfx = suction.vfx != null
            ? suction.vfx
            : suction.EnsureMouth().GetComponentInChildren<SuctionConeVfx>(true);

        if (vfx == null)
        {
            Debug.LogWarning("[Devour] Chua gan prefab VFX cho " + player.name + ".");
            return;
        }

        Undo.RecordObject(vfx, "Khop VFX voi vung hut");
        vfx.FitToCone(suction.range, suction.coneAngle);
        EditorUtility.SetDirty(vfx);

        Debug.Log("[Devour] Da khop VFX theo range " + suction.range + " / goc " + suction.coneAngle + ".");
    }

    [MenuItem(MenuRoot + "Khop VFX voi Range/Cone Angle cua nhan vat dang chon", true)]
    private static bool FitVfxToConeValidate()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<MouthSuction>() != null;
    }

    /// <summary>
    /// In ra bang can bang theo BAC: moi bac co bao nhieu vat, cho bao nhieu XP, va
    /// XP do co du de leo den cap mo khoa bac ke tiep khong.
    ///
    /// Day la cho de ket nhat cua he thong cap: neu bac 1 chi cho 18 XP ma leo tu cap 1
    /// len cap 10 can 25 XP thi nguoi choi tac o cap 9 vinh vien, an sach map cung
    /// khong mo noi bac 2. Chay lai moi lan sua map hoac sua duong cong XP.
    /// </summary>
    [MenuItem(MenuRoot + "Kiem tra can bang level", false, 50)]
    private static void CheckLevelBalance()
    {
        Devourable[] all = Object.FindObjectsByType<Devourable>(FindObjectsSortMode.None);
        if (all.Length == 0)
        {
            Debug.Log("[Devour] Khong tim thay Devourable nao trong scene.");
            return;
        }

        int tierCount = Devourable.TierCount;
        int[] countByTier = new int[tierCount + 2];
        int[] xpByTier = new int[tierCount + 2];

        for (int i = 0; i < all.Length; i++)
        {
            int tier;
            int xp;

            if (all[i].autoLevelFromSize)
            {
                tier = Devourable.TierForRadius(Devourable.MeasureRadius(all[i].gameObject));
                xp = Devourable.XpForTier(tier);
            }
            else
            {
                // Suy nguoc ra bac tu cap yeu cau ma designer dat tay
                tier = 1;
                for (int t = 2; t <= tierCount; t++)
                    if (all[i].requiredLevel >= Devourable.RequiredLevelForTier(t)) tier = t;
                xp = all[i].xpValue;
            }

            tier = Mathf.Clamp(tier, 1, tierCount);
            countByTier[tier]++;
            xpByTier[tier] += Mathf.Max(1, xp);
        }

        PlayerLevel player = Object.FindAnyObjectByType<PlayerLevel>();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[Devour] Can bang level (" + all.Length + " vat trong scene, "
                    + Devourable.LevelsPerTier + " cap mo mot bac)");
        sb.AppendLine("bac | mo o cap | so vat | XP cua bac | XP cong don | XP can de toi cap mo bac sau");

        int cumulativeXp = 0;
        for (int tier = 1; tier <= tierCount; tier++)
        {
            cumulativeXp += xpByTier[tier];
            int unlockAt = Devourable.RequiredLevelForTier(tier);

            string need = "-";
            string warn = string.Empty;

            if (player != null && tier < tierCount)
            {
                int nextUnlock = Devourable.RequiredLevelForTier(tier + 1);

                // Tong XP phai tich duoc de di tu cap 1 len cap mo bac ke tiep
                int required = 0;
                for (int level = 1; level < nextUnlock; level++) required += player.XpToNextAt(level);

                need = required.ToString();

                // Khong chi bao khi THIEU: vua du cung la hong, vi nguoi choi bo sot
                // mot vat la ket vinh vien. Doi hoi du ra it nhat 30%.
                if (cumulativeXp < required)
                    warn = "   <-- KET: thieu " + (required - cumulativeXp) + " XP, khong mo noi bac " + (tier + 1);
                else if (cumulativeXp < required * 1.3f)
                    warn = "   <-- SAT NUT: chi du " + (cumulativeXp * 100 / Mathf.Max(1, required))
                         + "%, bo sot vai vat la ket";
            }

            sb.AppendLine("  " + tier
                        + " |    " + unlockAt.ToString().PadLeft(3)
                        + "   |   " + countByTier[tier].ToString().PadLeft(3)
                        + "  |    " + xpByTier[tier].ToString().PadLeft(4)
                        + "   |    " + cumulativeXp.ToString().PadLeft(4)
                        + "   |  " + need.PadLeft(5) + warn);
        }

        if (player != null)
        {
            int toMax = 0;
            for (int level = 1; level < player.maxLevel; level++) toMax += player.XpToNextAt(level);

            sb.AppendLine("Tong XP de len cap " + player.maxLevel + " = " + toMax
                        + " | tong XP co trong map = " + cumulativeXp);

            int topUnlock = Devourable.TopRequiredLevel;
            if (player.maxLevel < topUnlock)
                sb.AppendLine("CANH BAO: maxLevel " + player.maxLevel + " thap hon cap mo bac cuoi ("
                            + topUnlock + ") nen bac cuoi khong bao gio hut duoc.");
        }
        else
        {
            sb.AppendLine("(Chua co PlayerLevel trong scene nen khong doi chieu duoc duong cong XP)");
        }

        Debug.Log(sb.ToString());
    }
}
