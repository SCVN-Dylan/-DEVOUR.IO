using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// LAM MO VAT CHE: player bi vat gi do che KHUAT HAN thi vat do mo di cho nhin xuyen qua.
///
/// Gan len chinh Camera (canh CameraFollow / CameraLevelZoom).
///
/// CACH DO KHUAT: ban mot chum tia TU CAMERA XUONG player - toi diem giua cong mot vanh diem quanh
/// no. Vanh nam trong MAT PHANG MAN HINH (dung theo camera.right/up) chu khong phai mat phang ngang:
/// "bi che" la chuyen cua man hinh, khong phai cua the gioi.
///
/// VI SAO BAN TU CAMERA XUONG chu khong phai tu player len: Unity KHONG tinh la trung khi tia xuat
/// phat tu BEN TRONG mot collider. Ban tu player len thi luc player dung lot trong long mot toa nha
/// - dung luc bi che kin nhat - moi tia deu bao "duong thong" va khong co gi mo ca. Ban tu camera
/// xuong thi tia luon xuat phat ngoai troi va dam vao mat ngoai vat can.
///
/// Camera cua game la ORTHOGRAPHIC va khong bao gio xoay, nen huong nhin la mot hang so - moi tia
/// deu song song, khong phai tinh phoi canh gi ca.
///
/// CU CO VAT CHAN LA MO: khong doi phai che kin. Mot toa nha an mot goc player cung duoc lam mo.
///
/// CHI ITEM duoc tinh la vat che (nha cua trong game nay deu la item). Mat dat, tuong bao va sinh
/// vat khong bao gio bi lam mo - xem OccluderOf.
///
/// CACH LAM MO: moi material goc sinh MOT ban trong suot dung chung (project nay ca map chi co 8
/// material). Do mo cua tung vat dat rieng bang MaterialPropertyBlock, nen khong con nao phai sinh
/// material instance rieng - do la cho ton kem nhat neu lam kieu ngay tho.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class OccluderFade : MonoBehaviour
{
    [Header("Muc tieu can nhin thay")]
    [Tooltip("De trong = tu hoi GameManager.Instance.Player. Chi dat tay khi test scene rieng")]
    [SerializeField] private Creature _target;

    [Header("Do khuat")]
    [Tooltip("Layer duoc phep BAN TIA vao. Day chi la buoc loc tho - vat co qua duoc hay khong con\n" +
             "phai la ITEM nua, xem OccluderOf. Ca map dang o layer Default nen de Everything")]
    [SerializeField] private LayerMask _occluderLayers = ~0;

    [Tooltip("So diem tren VANH quanh player (chua ke diem giua). Ban toi nhieu diem thi bat duoc ca\n" +
             "truong hop vat can chi an mot goc player, khong phai an dung giua. 8 la du cho mot bong tron")]
    [Range(3, 16)]
    [SerializeField] private int _ringSamples = 8;

    [Tooltip("He so ban kinh vanh so voi be ngang that cua player.\n" +
             "1 = dung be ngang. Ha xuong = de mo hon (chi can che phan giua). Nang len = kho mo hon")]
    [SerializeField] private float _radiusScale = 1f;

    [Tooltip("Bao lau do mot lan (giay). Khong can moi frame - mot toa nha truot qua dau player la\n" +
             "chuyen dien ra trong nhieu phan muoi giay, va con co fadeTime lam muot phia sau.\n" +
             "Ha xuong 0.05 thi nhay gap doi ma ton gap doi - do la doi 0.06% khung hinh lay mot thu\n" +
             "mat thuong khong phan biet duoc.")]
    [SerializeField] private float _checkInterval = 0.1f;

    [Header("Lam mo")]
    [Range(0f, 1f)]
    [Tooltip("Do duc con lai khi da mo han. 0.3 = con thay dang toa nha nhung nhin xuyen duoc")]
    [SerializeField] private float _fadedAlpha = 0.3f;

    [Tooltip("Thoi gian mo dan / hien lai (giay). 0 = doi ngay lap tuc")]
    [SerializeField] private float _fadeTime = 0.15f;

    [Tooltip("BAT: vat dang mo thi thoi do bong. Khong tat thi cai bong van nam nguyen tren dat,\n" +
             "lo ra la toa nha van con do - nhin rat ky")]
    [SerializeField] private bool _dropShadowWhileFaded = true;

    [Header("Debug")]
    [Tooltip("Ve chum tia trong SCENE VIEW (khong hien trong game).\n\n" +
             "  xanh la = tia thong, khong co gi chan\n" +
             "  do      = tia bi chan, cham do la diem cham vat can\n" +
             "  vang    = vanh lay mau quanh player\n\n" +
             "Chi ve trong Editor, khong ton gi trong ban build.")]
    [SerializeField] private bool _drawRays = true;

    /// <summary>Mot vat dang duoc lam mo. Giu du thu de tra ve nguyen trang.</summary>
    private class Ghosted
    {
        public Renderer renderer;
        public Material[] original;       // material goc, de tra lai
        public ShadowCastingMode shadow;  // che do do bong goc
        public Color[] baseColors;        // mau goc cua tung material, chi doi rieng phan alpha
        public float alpha = 1f;          // 1 = duc hoan toan
        public bool occluding;            // nhip do vua roi no co con che khong
    }

    private readonly Dictionary<Renderer, Ghosted> _ghosted = new Dictionary<Renderer, Ghosted>();
    private readonly Dictionary<Material, Material> _ghostMats = new Dictionary<Material, Material>();
    private readonly List<Renderer> _hitBuf = new List<Renderer>(8);
    private readonly List<Renderer> _doneBuf = new List<Renderer>(8);

    // Collider -> Renderer se lam mo. Gia tri null nghia la "collider nay khong bao gio lam mo"
    // (sinh vat, hoac vat co collider ma khong ve gi). Tra loi mot lan roi nho: khong co cai nay
    // thi moi nhip do lai chay GetComponent tren dung may vat da hoi cach day mot phan muoi giay.
    private readonly Dictionary<Collider, Renderer> _occluderOf = new Dictionary<Collider, Renderer>();

    // 16 cho: tia ban tu player ve camera co the xuyen qua nhieu vat trong khu pho dong.
    // RaycastNonAlloc khong sap theo khoang cach, day buffer la no bo phan con lai tuy y - bo sot
    // mot vat che nghia la vat do dung im khong mo trong khi hang xom cua no mo.
    private static readonly RaycastHit[] _rayBuf = new RaycastHit[16];

    private Camera _cam;
    private MaterialPropertyBlock _mpb;
    private Transform _targetRoot;
    private Renderer[] _targetRenderers;
    private float _baseRadius;        // ban kinh bong o scale 1. <=0 = chua do
    private float _measuredScale;     // scale luc do _baseRadius
    private float _timer;

    void Reset() { _cam = GetComponent<Camera>(); }

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _mpb = new MaterialPropertyBlock();
    }

    void OnDisable()
    {
        RestoreAll();
    }

    void OnDestroy()
    {
        RestoreAll();

        // Ban ghost la material tao luc chay - khong ai huy thi no ket lai trong bo nho
        foreach (KeyValuePair<Material, Material> kv in _ghostMats)
            if (kv.Value != null) Destroy(kv.Value);
        _ghostMats.Clear();
    }

    /// <summary>
    /// LateUpdate chu khong Update: camera bam theo player trong LateUpdate (CameraFollow), do o
    /// Update la do bang vi tri camera cua frame TRUOC.
    /// </summary>
    void LateUpdate()
    {
        // unscaledDeltaTime chu khong phai deltaTime: man ket thuc dat timeScale = 0, luc do
        // deltaTime = 0 -> khong quet lai va khong mo/hien dan nua -> vat dang mo KET CUNG o
        // trang thai do cho toi het man hinh ket thuc.
        _timer -= Time.unscaledDeltaTime;
        if (_timer <= 0f)
        {
            _timer = Mathf.Max(0f, _checkInterval);
            Recheck();
        }

        UpdateFades();
    }

    /// <summary>Do lai xem player co bi che khuat han khong, va dang bi nhung vat nao che.</summary>
    private void Recheck()
    {
        foreach (KeyValuePair<Renderer, Ghosted> kv in _ghosted) kv.Value.occluding = false;

        Creature t = ResolveTarget();
        if (t == null || _cam == null) return;      // khong co ai de nhin -> moi vat tu hien lai

        Vector3 center;
        float radius;
        if (!TargetSilhouette(t, out center, out radius)) return;

        _hitBuf.Clear();
        CollectOccluders(center, radius);
        if (_hitBuf.Count == 0) return;                 // khong co gi chan -> khong mo gi

        for (int i = 0; i < _hitBuf.Count; i++)
        {
            Renderer r = _hitBuf[i];
            if (r == null) continue;

            Ghosted g;
            if (!_ghosted.TryGetValue(r, out g))
            {
                g = Capture(r);
                if (g == null) continue;
                _ghosted[r] = g;
            }
            g.occluding = true;
        }
    }

    /// <summary>
    /// Gom moi vat dang NAM GIUA camera va player, do vao _hitBuf.
    ///
    /// Ban toi diem giua truoc roi toi cac diem tren vanh: vat can an dung giua nguoi la truong hop
    /// hay gap nhat, con vanh de bat nhung vat chi an mot goc.
    ///
    /// KHONG thoat som nhu ban truoc: gio cu co vat chan la lam mo, nen phai gom HET, khong phai
    /// tim mot tia lot roi bo cuoc.
    /// </summary>
    private void CollectOccluders(Vector3 center, float radius)
    {
        Transform ct = _cam.transform;
        Vector3 fwd = ct.forward;
        Vector3 right = ct.right;
        Vector3 up = ct.up;

        int n = Mathf.Max(3, _ringSamples);
        for (int i = 0; i <= n; i++)
        {
            Vector3 p = center;
            if (i > 0)
            {
                float a = (i - 1) * Mathf.PI * 2f / n;
                p += (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
            }
            CollectAlongRay(p, fwd, radius);
        }
    }

    /// <summary>
    /// Mot tia: tu MAT PHANG CAMERA ban xuong mot diem tren than player, ghi lai moi vat chan duoc.
    ///
    /// Camera la orthographic nen "mat" cua tia nay khong phai vi tri camera ma la hinh chieu cua
    /// chinh diem do len mat phang camera - co vay moi tia moi song song dung nhu cach camera nhin.
    ///
    /// Cat NGAN mot doan bang ban kinh than truoc khi toi noi: khong cat thi mon do an dang bay sat
    /// mom cung bi tinh la vat che, va no nhap nhay theo tung mieng bay vao.
    /// </summary>
    private void CollectAlongRay(Vector3 target, Vector3 fwd, float radius)
    {
        float dist = Vector3.Dot(target - _cam.transform.position, fwd);
        if (dist <= 0.01f) return;                       // diem nam sau lung camera

        Vector3 origin = target - fwd * dist;            // chieu nguoc len mat phang camera
        float len = dist - Mathf.Max(0f, radius);        // dung lai truoc khi cham than player
        if (len <= 0.01f) return;

        int n = Physics.RaycastNonAlloc(origin, fwd, _rayBuf, len, _occluderLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = _rayBuf[i].collider;
            if (c == null) continue;

            Renderer r = OccluderOf(c);
            if (r == null) continue;

            if (!_hitBuf.Contains(r)) _hitBuf.Add(r);
        }
    }

    /// <summary>
    /// Vat nay co phai VAT CHE khong, va neu phai thi lam mo Renderer nao.
    ///
    /// CHI ITEM (PhysicsDevourable) moi duoc tinh. Moi thu khac - mat dat, tuong bao, sinh vat -
    /// deu tra ve null.
    ///
    /// VI SAO PHAI CHAT NHU VAY: da tung dinh dung canh mat dat va tuong bao bi lam mo trong khi
    /// player dung giua dong trong (Ground_Circle + Wall_44 + Wall_45 cung mo mot luc). Trong game
    /// nay thu duy nhat that su che khuat la nha cua, ma nha cua deu la item (CoffeeShop la item
    /// Lv250) - nen loc theo item vua dung y do vua het sach ca lop nhieu do.
    ///
    /// GetComponentInParent chu khong phai GetComponent: collider nam o object con
    /// (Model/coffee_shop_001) con PhysicsDevourable nam o goc cua item.
    /// </summary>
    private Renderer OccluderOf(Collider c)
    {
        Renderer r;
        if (_occluderOf.TryGetValue(c, out r)) return r;   // null cung la mot cau tra loi da luu

        r = null;
        if (c.GetComponentInParent<PhysicsDevourable>() != null)
        {
            r = c.GetComponent<Renderer>();
            if (r == null) r = c.GetComponentInChildren<Renderer>();
        }

        // Chan phinh vo han: mot van chay qua rat nhieu vat, va item bi an xong thi khoa o lai day
        // mai. Day thi do di hoi lai tu dau - mat vai GetComponent chu khong ro ri bo nho.
        if (_occluderOf.Count >= 512) _occluderOf.Clear();

        _occluderOf[c] = r;
        return r;
    }

    /// <summary>
    /// BONG cua player tren man hinh: tam va ban kinh.
    ///
    /// Ban kinh do MOT LAN roi nhan theo scale hien tai, khong duyet lai 19 renderer moi nhip:
    /// phep duyet do ton 3.6 us mot lan (da do), ma hinh dang player thi khong doi - chi co scale
    /// lon dan theo level. Scale nhay qua nguong thi do lai.
    ///
    /// Bo qua hat va vet (ParticleSystem / Trail / Line): chung phun ra rat rong va doi tung frame,
    /// tinh vao thi ban kinh nhay lung tung, luc mo luc khong.
    /// </summary>
    private bool TargetSilhouette(Creature t, out Vector3 center, out float radius)
    {
        center = t.Center;

        float scale = Mathf.Abs(t.transform.lossyScale.x);
        if (scale < 0.0001f) scale = 1f;

        // Lan dau, hoac player da phinh/teo qua 5% so voi luc do
        if (_baseRadius <= 0f || Mathf.Abs(scale - _measuredScale) > _measuredScale * 0.05f)
            MeasureBaseRadius(t, scale);

        radius = Mathf.Max(0.05f, _baseRadius * scale * Mathf.Max(0f, _radiusScale));
        return true;
    }

    private void MeasureBaseRadius(Creature t, float scale)
    {
        _measuredScale = scale;
        _baseRadius = 0.5f;
        if (_targetRenderers == null || _targetRenderers.Length == 0) return;

        Bounds b = new Bounds(t.Center, Vector3.zero);
        bool any = false;
        for (int i = 0; i < _targetRenderers.Length; i++)
        {
            Renderer r = _targetRenderers[i];
            if (r == null || !r.enabled) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return;

        _baseRadius = Mathf.Max(b.extents.x, b.extents.z) / scale;   // quy ve scale 1 de con nhan lai
    }

    /// <summary>Player hien tai. Doi con khac (choi lai / chet) thi lay lai danh sach renderer.</summary>
    private Creature ResolveTarget()
    {
        Creature t = _target;
        if (t == null && GameManager.HasInstance) t = GameManager.Instance.Player;

        if (t == null)
        {
            _targetRoot = null;
            _targetRenderers = null;
            return null;
        }

        if (_targetRoot != t.transform)
        {
            _targetRoot = t.transform;
            _targetRenderers = t.GetComponentsInChildren<Renderer>(true);   // chi lay lai khi DOI con
            _baseRadius = 0f;                                              // con khac, do lai bong
        }
        return t;
    }

    /// <summary>Keo do mo cua tung vat ve dich, va tra vat da hien lai day du ve nguyen trang.</summary>
    private void UpdateFades()
    {
        if (_ghosted.Count == 0) return;

        float step = _fadeTime > 0.0001f ? Time.unscaledDeltaTime / _fadeTime : 1f;
        _doneBuf.Clear();

        foreach (KeyValuePair<Renderer, Ghosted> kv in _ghosted)
        {
            Ghosted g = kv.Value;
            if (g.renderer == null) { _doneBuf.Add(kv.Key); continue; }

            float goal = g.occluding ? Mathf.Clamp01(_fadedAlpha) : 1f;
            g.alpha = Mathf.MoveTowards(g.alpha, goal, step);

            ApplyAlpha(g);

            // Da hien lai day du va khong con che nua -> khong con viec gi de lam voi no
            if (!g.occluding && g.alpha >= 0.999f) _doneBuf.Add(kv.Key);
        }

        for (int i = 0; i < _doneBuf.Count; i++)
        {
            Ghosted g;
            if (_ghosted.TryGetValue(_doneBuf[i], out g)) Restore(g);
            _ghosted.Remove(_doneBuf[i]);
        }
    }

    /// <summary>Chuyen mot vat sang ban ghost va nho lai moi thu de con tra ve.</summary>
    private Ghosted Capture(Renderer r)
    {
        Material[] src = r.sharedMaterials;
        if (src == null || src.Length == 0) return null;

        Material[] ghosts = new Material[src.Length];
        Color[] colors = new Color[src.Length];
        bool anyGhost = false;

        for (int i = 0; i < src.Length; i++)
        {
            Material m = src[i];
            if (m == null) { ghosts[i] = null; colors[i] = Color.white; continue; }

            ghosts[i] = GhostOf(m);
            colors[i] = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            if (ghosts[i] != null) anyGhost = true;
        }
        if (!anyGhost) return null;

        Ghosted g = new Ghosted();
        g.renderer = r;
        g.original = src;
        g.baseColors = colors;
        g.shadow = r.shadowCastingMode;
        g.alpha = 1f;

        r.sharedMaterials = ghosts;
        if (_dropShadowWhileFaded) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    private void Restore(Ghosted g)
    {
        if (g.renderer == null) return;
        g.renderer.SetPropertyBlock(null);
        g.renderer.sharedMaterials = g.original;
        g.renderer.shadowCastingMode = g.shadow;
    }

    /// <summary>
    /// Do mo dat bang MaterialPropertyBlock chu khong bang material rieng: nho vay ca chuc toa nha
    /// dung CHUNG mot ban ghost van mo khac nhau duoc, va khong sinh material instance nao.
    /// </summary>
    private void ApplyAlpha(Ghosted g)
    {
        _mpb.Clear();

        // Tat ca material tren cung mot renderer chia nhau mot property block, nen lay mau cua
        // material dau lam chuan - cac vat trong map deu mot material mot renderer.
        Color c = g.baseColors.Length > 0 ? g.baseColors[0] : Color.white;
        c.a = g.alpha;
        _mpb.SetColor("_BaseColor", c);

        g.renderer.SetPropertyBlock(_mpb);
    }

    /// <summary>
    /// Ban TRONG SUOT cua mot material, tao mot lan roi dung lai mai.
    ///
    /// Phai tu tay set blend + keyword + render queue: dat _Surface = 1 KHONG tu doi nhung thu do -
    /// trong Editor viec ay do ShaderGUI cua URP lam khi ban bam vao dropdown, luc chay thi khong ai
    /// lam ho. Thieu mot mieng la vat ra den si hoac bien mat han.
    /// </summary>
    private Material GhostOf(Material src)
    {
        Material ghost;
        if (_ghostMats.TryGetValue(src, out ghost) && ghost != null) return ghost;

        ghost = new Material(src);
        ghost.name = src.name + " (ghost)";

        ghost.SetFloat("_Surface", 1f);          // 0 = Opaque, 1 = Transparent
        ghost.SetFloat("_Blend", 0f);            // 0 = Alpha
        ghost.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        ghost.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        ghost.SetFloat("_ZWrite", 0f);
        ghost.SetFloat("_AlphaClip", 0f);
        ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        ghost.DisableKeyword("_ALPHATEST_ON");
        ghost.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        ghost.SetOverrideTag("RenderType", "Transparent");
        ghost.renderQueue = (int)RenderQueue.Transparent;

        _ghostMats[src] = ghost;
        return ghost;
    }

    /// <summary>Tra moi vat ve nguyen trang ngay lap tuc, khong mo dan.</summary>
    private void RestoreAll()
    {
        foreach (KeyValuePair<Renderer, Ghosted> kv in _ghosted) Restore(kv.Value);
        _ghosted.Clear();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Ve chum tia ra SCENE VIEW de nhin tan mat no dang do cai gi.
    ///
    /// OnDrawGizmos chu khong phai OnDrawGizmosSelected: khoi phai chon Main Camera moi thay.
    /// Toan bo ham nay bi cat khoi ban build (Unity khong bien dich Gizmo vao player), nen khong
    /// ton gi ca - phep raycast o day chi chay trong Editor.
    ///
    /// Ban lai tia mot lan nua thay vi luu ket qua cua Recheck: Recheck chay theo nhip 0.1s con
    /// gizmo ve moi lan Scene view repaint, luu lai thi duong ve giat theo nhip do.
    /// </summary>
    void OnDrawGizmos()
    {
        if (!_drawRays) return;
        if (_cam == null) _cam = GetComponent<Camera>();
        if (_cam == null) return;

        Creature t = _target;
        if (t == null && GameManager.HasInstance) t = GameManager.Instance.Player;
        if (t == null) return;

        Vector3 center;
        float radius;
        if (_targetRoot != t.transform)
        {
            _targetRoot = t.transform;
            _targetRenderers = t.GetComponentsInChildren<Renderer>(true);
            _baseRadius = 0f;
        }
        if (!TargetSilhouette(t, out center, out radius)) return;

        Transform ct = _cam.transform;
        Vector3 fwd = ct.forward, right = ct.right, up = ct.up;

        // Vanh lay mau
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        int n = Mathf.Max(3, _ringSamples);
        Vector3 prev = center + (right * Mathf.Cos(0f) + up * Mathf.Sin(0f)) * radius;
        for (int i = 1; i <= n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector3 cur = center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }

        for (int i = 0; i <= n; i++)
        {
            Vector3 target = center;
            if (i > 0)
            {
                float a = (i - 1) * Mathf.PI * 2f / n;
                target += (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
            }
            DrawOneRay(target, fwd, radius);
        }
    }

    private void DrawOneRay(Vector3 target, Vector3 fwd, float radius)
    {
        float dist = Vector3.Dot(target - _cam.transform.position, fwd);
        if (dist <= 0.01f) return;

        Vector3 origin = target - fwd * dist;
        float len = dist - Mathf.Max(0f, radius);
        if (len <= 0.01f) return;

        // Tim vat chan GAN CAMERA NHAT de biet cat duong ve o dau
        float bestDist = -1f;
        int hits = Physics.RaycastNonAlloc(origin, fwd, _rayBuf, len, _occluderLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider c = _rayBuf[i].collider;
            if (c == null || c.GetComponentInParent<PhysicsDevourable>() == null) continue;   // chi item moi tinh
            if (bestDist < 0f || _rayBuf[i].distance < bestDist) bestDist = _rayBuf[i].distance;
        }

        Vector3 end = origin + fwd * len;
        if (bestDist < 0f)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.75f);   // thong suot
            Gizmos.DrawLine(origin, end);
            return;
        }

        Vector3 hit = origin + fwd * bestDist;
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.75f);
        Gizmos.DrawLine(origin, hit);                          // doan truoc vat can

        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.9f);
        Gizmos.DrawLine(hit, end);                             // doan bi chan
        Gizmos.DrawSphere(hit, 0.15f);                         // diem cham
    }
#endif
}
