using UnityEngine;

/// <summary>
/// Ve VUNG HUT ra man hinh: mot hinh quat (fan) ban trong suot trai tren mat dat truoc
/// nhan vat, khop dung range + coneAngle cua SimpleSuction va NO RA khi len cap.
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

    [Tooltip("Mau vung hut (co alpha)")]
    public Color color = new Color(1f, 0.35f, 0.05f, 0.32f);

    [Tooltip("So mieng tam giac cua quat - cao = muot hon")]
    [Range(6, 64)] public int segments = 28;

    [Tooltip("Ve cao hon mat dat mot chut cho khoi z-fighting")]
    public float groundY = 0.06f;

    private MeshFilter _mf;
    private MeshRenderer _mr;
    private Mesh _mesh;
    private Material _mat;

    // Cache de KHONG dung lai mesh moi frame (tranh GC tren mobile) - chi rebuild khi thong so doi
    private Vector3[] _verts;
    private int[] _tris;
    private int _lastSeg = -1;
    private float _lastRadius = -1f, _lastAngle = -1f, _lastInvSx = -1f, _lastInvSz = -1f;
    private Color _lastColor;

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

        Refresh(); // dung mesh ngay, khong cho tick dau tien
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
        if (mouth == null) mouth = suction.mouth != null ? suction.mouth : suction.transform;
        if (_mesh == null) return;

        // Dat tam quat tai mieng, chieu xuong mat dat; huong theo mouth.forward phang tren XZ
        Vector3 p = mouth.position;
        Vector3 fwd = mouth.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
        fwd.Normalize();

        transform.position = new Vector3(p.x, groundY, p.z);
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        // Bu lai scale cua cha (player scale khi len cap) de radius the giu dung don vi world
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x;
        float sz = Mathf.Approximately(ls.z, 0f) ? 1f : 1f / ls.z;

        Rebuild(suction.CurrentRange, suction.coneAngle, sx, sz);

        // Mau chi set khi doi (tranh SetColor moi frame)
        if (_mat != null && color != _lastColor)
        {
            _mat.color = color;
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", color);
            _lastColor = color;
        }
    }

    /// <summary>Dung lai mesh CHI KHI thong so doi (range/angle/scale/segments) - con lai skip, khong ton gi.</summary>
    private void Rebuild(float radius, float angle, float invSx, float invSz)
    {
        if (_mesh == null) return;

        int seg = Mathf.Clamp(segments, 6, 64);
        if (seg == _lastSeg
            && Mathf.Approximately(radius, _lastRadius) && Mathf.Approximately(angle, _lastAngle)
            && Mathf.Approximately(invSx, _lastInvSx) && Mathf.Approximately(invSz, _lastInvSz))
            return;   // khong doi -> khoi dung lai

        _lastSeg = seg; _lastRadius = radius; _lastAngle = angle; _lastInvSx = invSx; _lastInvSz = invSz;

        if (_verts == null || _verts.Length != seg + 2)
        {
            _verts = new Vector3[seg + 2];
            _tris = new int[seg * 3];
            for (int i = 0; i < seg; i++)   // topology co dinh, chi tinh 1 lan
            {
                _tris[i * 3] = 0;
                _tris[i * 3 + 1] = i + 1;
                _tris[i * 3 + 2] = i + 2;
            }
        }

        float half = angle * 0.5f * Mathf.Deg2Rad;
        _verts[0] = Vector3.zero;
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)seg);
            _verts[i + 1] = new Vector3(Mathf.Sin(a) * radius * invSx, 0f, Mathf.Cos(a) * radius * invSz);
        }

        _mesh.Clear();
        _mesh.vertices = _verts;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }

    private Material BuildMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.hideFlags = HideFlags.DontSave;

        // Cau hinh trong suot cho URP/Unlit
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);           // Transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);              // Alpha
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        return m;
    }

    private static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
