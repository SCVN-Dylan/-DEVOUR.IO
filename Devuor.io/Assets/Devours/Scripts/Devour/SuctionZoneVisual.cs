using UnityEngine;

/// <summary>
/// Ve VUNG HUT ra man hinh: mot hinh quat (fan) ban trong suot trai tren mat dat truoc
/// nhan vat, khop dung range + coneAngle cua SimpleSuction va NO RA khi len cap.
///
/// Dai mau (gradient): Sat mieng (hut nhanh) = MAU DO, o xa (hut cham) = MAU XANH DUNG.
///
/// Chay ca trong Edit mode (ExecuteAlways) de xem truoc trong Scene/Game view. Mesh va
/// material tao runtime, danh dau DontSave nen khong ghi rac vao scene/prefab.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DisallowMultipleComponent]
public class SuctionZoneVisual : MonoBehaviour
{
    [Tooltip("De trong se tu tim SimpleSuction o cha")]
    public SimpleSuction suction;

    [Tooltip("Goc non (mac dinh lay tu suction.mouth)")]
    public Transform mouth;

    [Header("Dai mau theo khoang cach")]
    [Tooltip("Mau vung gan mieng (sat mouth, hut nhanh) - Do")]
    public Color nearColor = new Color(1f, 0.15f, 0.15f, 0.55f);

    [Tooltip("Mau vung o xa (ria ngoai, hut cham) - Xanh duong")]
    public Color farColor = new Color(0f, 0.55f, 1f, 0.35f);

    [Header("Do chi tiet Mesh")]
    [Tooltip("So mieng tam giac theo chieu ngang quat - cao = muot hon")]
    [Range(6, 64)] public int segments = 28;

    [Tooltip("So vong dong tam tu trong ra ngoai - cao = chuyen mau min hon")]
    [Range(1, 32)] public int rings = 8;

    [Tooltip("Ve cao hon chan/pivot player mot chut cho khoi z-fighting")]
    public float groundY = 0.06f;

    private MeshFilter _mf;
    private MeshRenderer _mr;
    private Mesh _mesh;
    private Material _mat;

    // Cache de KHONG dung lai mesh moi frame (tranh GC tren mobile)
    private Vector3[] _verts;
    private Color32[] _colors;
    private int[] _tris;
    private int _lastSeg = -1;
    private int _lastRings = -1;
    private float _lastRadius = -1f, _lastAngle = -1f, _lastInvSx = -1f, _lastInvSz = -1f;
    private Color _lastNearColor, _lastFarColor;

    void OnEnable()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();

        if (suction == null) suction = GetComponentInParent<SimpleSuction>();
        if (mouth == null && suction != null) mouth = suction.mouth != null ? suction.mouth : suction.transform;

        _mesh = new Mesh { name = "SuctionZone" };
        _mesh.hideFlags = HideFlags.DontSave;
        _mf.sharedMesh = _mesh;

        _mat = BuildMaterial();
        _mr.sharedMaterial = _mat;
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows = false;

        Refresh();
    }

    void OnDisable()
    {
        if (_mesh != null) SafeDestroy(_mesh);
        if (_mat != null) SafeDestroy(_mat);
    }

    void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (suction == null) { suction = GetComponentInParent<SimpleSuction>(); if (suction == null) return; }

        // CHU HET HUT thi non cung phai tat theo.
        //
        // Cu the la luc con nay CHET: Creature.PlaySwallowedInto tat SimpleSuction de no thoi rut
        // XP con khac, nhung non van ve nhu thuong - thanh ra mot cai nón trong suot bay vao mom
        // ke giet, van quet qua quet lai nhu con dang san moi.
        //
        // Kiem o day chu khong bat Creature tu tay tat: lam o day thi MOI cho tat SimpleSuction
        // (chet, doi scene, cheat, tam dung) deu tu dong dung - khong phai nho ai goi ho.
        if (!suction.isActiveAndEnabled)
        {
            if (_mr != null && _mr.enabled) _mr.enabled = false;
            return;
        }
        if (_mr != null && !_mr.enabled) _mr.enabled = true;

        if (mouth == null) mouth = suction.mouth != null ? suction.mouth : suction.transform;
        if (_mesh == null) return;

        Transform originTF = suction.transform;
        Vector3 p = originTF.position;
        Vector3 fwd = mouth != null ? mouth.forward : originTF.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = originTF.forward;
        fwd.y = 0f;
        fwd.Normalize();

        transform.position = new Vector3(p.x, p.y + groundY, p.z);
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x;
        float sz = Mathf.Approximately(ls.z, 0f) ? 1f : 1f / ls.z;

        Rebuild(suction.CurrentRange, suction.coneAngle, sx, sz);
    }

    private void Rebuild(float radius, float angle, float invSx, float invSz)
    {
        if (_mesh == null) return;

        int seg = Mathf.Clamp(segments, 6, 64);
        int rng = Mathf.Clamp(rings, 1, 32);

        if (seg == _lastSeg && rng == _lastRings
            && Mathf.Approximately(radius, _lastRadius) && Mathf.Approximately(angle, _lastAngle)
            && Mathf.Approximately(invSx, _lastInvSx) && Mathf.Approximately(invSz, _lastInvSz)
            && nearColor == _lastNearColor && farColor == _lastFarColor)
            return;

        _lastSeg = seg; _lastRings = rng;
        _lastRadius = radius; _lastAngle = angle;
        _lastInvSx = invSx; _lastInvSz = invSz;
        _lastNearColor = nearColor; _lastFarColor = farColor;

        int vertCount = (rng + 1) * (seg + 1);
        int triCount = rng * seg * 6;

        if (_verts == null || _verts.Length != vertCount)
        {
            _verts = new Vector3[vertCount];
            _colors = new Color32[vertCount];
            _tris = new int[triCount];

            int tIdx = 0;
            for (int r = 0; r < rng; r++)
            {
                for (int s = 0; s < seg; s++)
                {
                    int curr = r * (seg + 1) + s;
                    int next = curr + seg + 1;

                    _tris[tIdx++] = curr;
                    _tris[tIdx++] = next;
                    _tris[tIdx++] = curr + 1;

                    _tris[tIdx++] = curr + 1;
                    _tris[tIdx++] = next;
                    _tris[tIdx++] = next + 1;
                }
            }
        }

        float half = angle * 0.5f * Mathf.Deg2Rad;
        Color32 nearC = nearColor;
        Color32 farC = farColor;

        int vIdx = 0;
        for (int r = 0; r <= rng; r++)
        {
            float rFrac = (float)r / rng;
            float currentR = radius * rFrac;
            Color32 c = Color32.Lerp(nearC, farC, rFrac);

            for (int s = 0; s <= seg; s++)
            {
                float a = Mathf.Lerp(-half, half, (float)s / seg);
                _verts[vIdx] = new Vector3(Mathf.Sin(a) * currentR * invSx, 0f, Mathf.Cos(a) * currentR * invSz);
                _colors[vIdx] = c;
                vIdx++;
            }
        }

        _mesh.Clear();
        _mesh.vertices = _verts;
        _mesh.colors32 = _colors;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }

    private Material BuildMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.hideFlags = HideFlags.DontSave;

        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);

        return m;
    }

    private static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
