using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>Mot dong trong bang: rai bao nhieu cai cua prefab nao.</summary>
[System.Serializable]
public class AreaItemEntry
{
    [Tooltip("Prefab item. Keo prefab tu Project window vao day.")]
    public GameObject prefab;

    [Min(0)]
    [Tooltip("So luong rai trong vung nay.")]
    public int count = 10;
}

/// <summary>
/// RAI ITEM TRONG MOT VUNG (cong cu Editor, khong chay luc build).
///
/// Gan len chinh object 'Area N' trong scene Main. Vung rai = BOUNDS CUA RENDERER object do, nen
/// keo to Area trong scene la vung rai to theo - khong co so kich thuoc nao phai dong bo bang tay.
///
/// ------------------------------------------------------------------------------------------
/// CACH DAT: nem phi tieu (dart throwing), khong phai chia o luoi
/// ------------------------------------------------------------------------------------------
/// Chia o luoi thi item dung thang hang, nhin ra ngay la do may rai. Nem phi tieu = boc diem random
/// roi TU CHOI neu dam vao cai da dat. Khoang cach toi thieu giua hai item lay theo BAN KINH THAT
/// cua tung cai (do tu bounds renderer), nen cai banh mi va cai o to khong bi ep cung mot khoang.
///
/// ------------------------------------------------------------------------------------------
/// TIM MAT DAT / NE NHA: dung lai y het GameManager.FindSpawnPoint va IsSpawnBlocked
/// ------------------------------------------------------------------------------------------
/// Ban tia tu tren cao 50 xuong, BO QUA nhung gi co Rigidbody (item/sinh vat - dat len dau nhau thi
/// no roi ngay), lay diem cao nhat.
///
/// Do vat can thi do o TAM THAN chu khong o chan, va BO QUA collider nam TRON VEN duoi chan: do la
/// mat dat/via he/buc them - cho de dung, khong phai vat can. Nho meo do ma khong can khai bao mask
/// rieng cho dat, va no dung y het luat ma bot dang dung de chon cho sinh.
///
/// Item gen ra nam trong object con 'Items_Generated' de xoa sach mot lenh, khong lan voi do dat tay.
/// </summary>
[DisallowMultipleComponent]
public class AreaItemSpawner : MonoBehaviour
{
    /// <summary>Ten object con chua toan bo item gen ra.</summary>
    public const string ContainerName = "Items_Generated";

    [Header("Bang rai")]
    [Tooltip("Moi dong: mot loai item + so luong. Tong cac dong la so item se gen.")]
    public List<AreaItemEntry> entries = new List<AreaItemEntry>();

    [Header("Khoang cach")]
    [Tooltip("Ho THEM giua hai item, cong vao ban kinh that cua ca hai (world).\n" +
             "0 = cho phep hai item cham vo nhau. Nang len thi thoang hon nhung nhet duoc it hon.")]
    public float padding = 0.15f;

    [Tooltip("Chua item vao trong vien vung bao nhieu (world), de khong co cai nao thò ra ngoai Area.")]
    public float edgeMargin = 0.5f;

    [Tooltip("So lan thu cho MOI item truoc khi bo cuoc. Vung chat thi nang len, nhung nang qua\n" +
             "thi bam Gen cho lau ma van khong nhet them duoc bao nhieu.")]
    [Min(1)]
    public int attemptsPerItem = 40;

    [Header("Dat xuong dat")]
    [Tooltip("Layer duoc coi la mat dat khi ban tia tim cao do.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Nhac item len khoi mat dat mot chut cho khoi ket trong dat.")]
    public float groundOffset = 0.05f;

    [Header("Ngau nhien")]
    [Tooltip("Xoay ngau nhien quanh truc Y - khong thi ca dong item quay cung mot huong, nhin nhu xep ke.")]
    public bool randomYaw = true;

    [Tooltip("0 = moi lan Gen ra mot bo cuc khac. Khac 0 = rai lai ra DUNG bo cuc cu,\n" +
             "tien khi muon thu chinh padding ma van so sanh duoc hai lan.")]
    public int seed = 0;

    [Header("Go")]
    [Tooltip("Ve khung vung rai trong Scene view.")]
    public bool drawGizmos = true;

    // ------------------------------------------------------------------ vung rai

    /// <summary>
    /// Vung rai = bounds renderer cua chinh object nay. Area 1..4 la mesh Plane nen bounds chinh la
    /// o vuong nhin thay tren scene.
    /// </summary>
    public bool TryGetZone(out Bounds zone)
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null) { zone = r.bounds; return true; }

        zone = new Bounds(transform.position, Vector3.zero);
        return false;
    }

    public int TotalRequested()
    {
        int n = 0;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].prefab != null) n += Mathf.Max(0, entries[i].count);
        return n;
    }

#if UNITY_EDITOR

    // ------------------------------------------------------------------ lenh

    [ContextMenu("Gen item")]
    public void Generate()
    {
        Bounds zone;
        if (!TryGetZone(out zone))
        {
            Debug.LogError("[AreaItemSpawner] '" + name + "' khong co Renderer nen khong biet vung rai o dau. " +
                           "Gan script nay len object Area (mesh Plane) hoac them Renderer cho no.", this);
            return;
        }

        int want = TotalRequested();
        if (want <= 0)
        {
            Debug.LogWarning("[AreaItemSpawner] '" + name + "': bang rai dang trong hoac so luong = 0.", this);
            return;
        }

        Clear();

        Transform container = new GameObject(ContainerName).transform;
        Undo.RegisterCreatedObjectUndo(container.gameObject, "Gen item");
        container.SetParent(transform, false);

        // CHI dung/tra state khi dung SEED CO DINH.
        //
        // Truoc day doan nay luu va tra state VO DIEU KIEN, ke ca khi seed = 0. Hau qua: moi lan Gen
        // deu tua bo sinh so ve dung cho cu, nen lan sau bam Gen lai xuat phat tu do va ra Y HET bo
        // cuc cu - seed = 0 mat sach tac dung "moi lan mot kieu".
        //
        // Gio seed = 0 thi KHONG dung gi den state: cu de no chay tiep tu nhien -> moi lan mot khac.
        // Con seed khac 0 thi van lua state ve roi tra lai, de viec dat seed khong lam lech nhung
        // thu khac dang dung Random trong cung phien Editor.
        bool fixedSeed = seed != 0;
        Random.State prevState = default(Random.State);
        if (fixedSeed)
        {
            prevState = Random.state;
            Random.InitState(seed);
        }

        var placed = new List<PlacedItem>(want);
        var radiusCache = new Dictionary<GameObject, float>();

        int made = 0, failed = 0;
        for (int e = 0; e < entries.Count; e++)
        {
            AreaItemEntry entry = entries[e];
            if (entry == null || entry.prefab == null || entry.count <= 0) continue;

            float radius = FootprintRadius(entry.prefab, radiusCache);

            for (int i = 0; i < entry.count; i++)
            {
                Vector3 pos;
                if (!FindSpot(zone, radius, placed, out pos)) { failed++; continue; }

                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, container);
                go.transform.position = pos;
                if (randomYaw) go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                Undo.RegisterCreatedObjectUndo(go, "Gen item");
                placed.Add(new PlacedItem { pos = pos, radius = radius });
                made++;
            }
        }

        if (fixedSeed) Random.state = prevState;
        EditorSceneManager.MarkSceneDirty(gameObject.scene);

        string msg = "[AreaItemSpawner] '" + name + "': dat duoc " + made + "/" + want + " item" +
                     " trong vung " + zone.size.x.ToString("F1") + " x " + zone.size.z.ToString("F1") + "u.";
        if (failed > 0)
            Debug.LogWarning(msg + " THIEU " + failed + " cai vi het cho - ha padding, giam so luong, " +
                             "keo Area to ra, hoac nang attemptsPerItem.", this);
        else
            Debug.Log(msg, this);
    }

    [ContextMenu("Xoa item da gen")]
    public void Clear()
    {
        Transform old = transform.Find(ContainerName);
        if (old == null) return;

        int n = old.childCount;
        Undo.DestroyObjectImmediate(old.gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[AreaItemSpawner] '" + name + "': da xoa " + n + " item da gen.", this);
    }

    // ------------------------------------------------------------------ thuat toan

    private struct PlacedItem
    {
        public Vector3 pos;
        public float radius;
    }

    /// <summary>
    /// Nem phi tieu: boc diem random trong vung, ban tia xuong tim dat, tu choi neu dam vao item da
    /// dat hoac dam vao nha. Het luot thu thi tra false - GOI BEN NGOAI DEM va bao, khong im lang bo.
    /// </summary>
    private bool FindSpot(Bounds zone, float radius, List<PlacedItem> placed, out Vector3 pos)
    {
        pos = Vector3.zero;

        float inset = radius + edgeMargin;
        float minX = zone.min.x + inset, maxX = zone.max.x - inset;
        float minZ = zone.min.z + inset, maxZ = zone.max.z - inset;

        // Item to hon ca vung thi khong co diem nao hop le - thu bao nhieu lan cung vo ich.
        if (minX > maxX || minZ > maxZ) return false;

        float top = zone.max.y + 50f;

        for (int attempt = 0; attempt < attemptsPerItem; attempt++)
        {
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);

            float groundY;
            if (!SampleGround(new Vector3(x, top, z), out groundY)) continue;

            Vector3 candidate = new Vector3(x, groundY + groundOffset, z);

            if (Overlaps(candidate, radius, placed)) continue;
            if (Blocked(candidate, radius)) continue;

            pos = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ban tia tu tren cao xuong. BO QUA vat co Rigidbody (item/sinh vat da nam san) - dat len dau
    /// nhau thi no roi ngay khi vao game. Lay diem CAO NHAT: dung tren via he chu khong chui xuong
    /// mat duong ben duoi.
    /// </summary>
    private bool SampleGround(Vector3 from, out float y)
    {
        y = 0f;
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 200f, groundLayers,
                                               QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].rigidbody != null) continue;
            if (hits[i].point.y > best) { best = hits[i].point.y; found = true; }
        }
        if (found) y = best;
        return found;
    }

    private bool Overlaps(Vector3 pos, float radius, List<PlacedItem> placed)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            float need = radius + placed[i].radius + padding;
            Vector3 d = placed[i].pos - pos;
            d.y = 0f;                                  // do theo mat bang: cao thap khong tinh la chen nhau
            if (d.sqrMagnitude < need * need) return true;
        }
        return false;
    }

    /// <summary>
    /// Cho nay co nha/vat gi dung san khong. Do o TAM THAN chu khong o chan, va BO QUA collider nam
    /// tron ven duoi chan (mat dat, via he, buc them phang) - do la cho dung chu khong phai vat can.
    /// Y het GameManager.IsSpawnBlocked.
    /// </summary>
    private bool Blocked(Vector3 pos, float radius)
    {
        float r = Mathf.Max(0.05f, radius);
        Collider[] hits = Physics.OverlapSphere(pos + Vector3.up * r, r, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;
            if (c.bounds.max.y <= pos.y + 0.05f) continue;   // nam duoi chan -> cho dung
            return true;
        }
        return false;
    }

    /// <summary>
    /// BAN KINH CHAN DE THAT cua prefab, do tren mat bang XZ.
    ///
    /// Phai DUNG THU MOT CAI ra roi do chu khong doc bounds thang tu asset: item la Prefab Variant
    /// long nhieu tang (vd Item_Lv5_Car boc car_003.prefab), bounds tren asset khong phai luc nao
    /// cung da duoc gop day du. Dung thu roi huy ngay, moi prefab chi lam mot lan roi cache.
    /// </summary>
    public static float FootprintRadius(GameObject prefab, Dictionary<GameObject, float> cache)
    {
        float r;
        if (cache.TryGetValue(prefab, out r)) return r;

        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Quaternion.identity;

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool has = false;
        Renderer[] rends = probe.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null || rends[i] is ParticleSystemRenderer) continue;
            if (!has) { b = rends[i].bounds; has = true; }
            else b.Encapsulate(rends[i].bounds);
        }

        Object.DestroyImmediate(probe);

        r = has ? Mathf.Max(b.size.x, b.size.z) * 0.5f : 0.25f;
        if (r < 0.05f) r = 0.05f;
        cache[prefab] = r;
        return r;
    }

#endif

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Bounds zone;
        if (!TryGetZone(out zone)) return;

        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(zone.center, new Vector3(zone.size.x, 0.05f, zone.size.z));

        float inset = edgeMargin;
        if (inset > 0f && zone.size.x > inset * 2f && zone.size.z > inset * 2f)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(zone.center,
                new Vector3(zone.size.x - inset * 2f, 0.05f, zone.size.z - inset * 2f));
        }
    }
}
