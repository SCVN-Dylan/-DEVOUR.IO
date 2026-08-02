using UnityEngine;

/// <summary>
/// Dieu khien cuong do cua prefab VFX vung hut.
///
/// Component nay KHONG tao particle bang code. Toan bo hinh dang, mau sac, so hat,
/// vat lieu... nam trong prefab Assets/Prefabs/VFX/SuctionVFX.prefab va duoc chinh
/// thang trong Inspector nhu moi VFX binh thuong khac.
///
/// Viec duy nhat cua no luc chay: nhan cuong do 0..1 tu MouthSuction roi nhan vao
/// emission rate + alpha cua tung he particle con, de luong gio manh dan / tan dan
/// thay vi bat tat cai bup.
///
/// Cau truc prefab:
///   SuctionVFX          (component nay + he particle "Streak")
///     +- Debris         (bui nho bay lan trong luong gio)
///     +- Ring           (vanh xoay sat mieng)
/// </summary>
[DisallowMultipleComponent]
public class SuctionConeVfx : MonoBehaviour
{
    [Header("He particle chiu dieu khien")]
    [Tooltip("De trong se tu lay het ParticleSystem trong object nay va cac object con")]
    public ParticleSystem[] systems;

    [Header("Cach doi theo cuong do")]
    [Tooltip("Cuong do duoi muc nay coi nhu tat han: dung emit va xoa sach hat dang bay")]
    [Range(0f, 0.2f)] public float cutoff = 0.02f;

    [Tooltip("Alpha tut cham hon so hat. 1 = tut cung nhip, >1 = con thay mo mo khi gio yeu")]
    [Range(0.5f, 3f)] public float alphaBoost = 1.3f;

    [Header("Khop voi vung hut (chi dung luc setup)")]
    [Tooltip("Cac he particle sinh ra o DAY non. Menu Tools/Devour/Khop VFX... se resize\n" +
             "dung cac he nay theo Range va Cone Angle cua MouthSuction.\n" +
             "Vanh o mieng thi dung cho vao day, khong no se bi phong to theo")]
    public ParticleSystem[] coneLayers;

    /// <summary>Cuong do dang ap dung, 0..1.</summary>
    public float Intensity { get; private set; }

    private float[] _baseRates;
    private ParticleSystem.MinMaxGradient[] _baseColors;
    private float _applied = -1f;

    void Awake()
    {
        EnsureCached();
        SetIntensity(0f);
    }

    /// <summary>
    /// Chinh cuong do 0..1. MouthSuction goi moi frame nen chi dung cham vao
    /// particle system khi gia tri thuc su doi, tranh set property vo ich.
    /// </summary>
    public void SetIntensity(float value)
    {
        Intensity = Mathf.Clamp01(value);
        if (Mathf.Abs(Intensity - _applied) < 0.01f) return;

        _applied = Intensity;
        ApplyIntensity();
    }

    /// <summary>
    /// Cache dung MOT lan.
    ///
    /// Quan trong: MouthSuction.Awake co the chay truoc Awake cua component nay
    /// (Unity khong dam bao thu tu cha/con). Neu no goi SetIntensity(0) truoc roi
    /// sau do minh cache lai thi se cache nham rate = 0, VFX cam nin luon.
    /// </summary>
    private void EnsureCached()
    {
        if (_baseRates != null && systems != null && _baseRates.Length == systems.Length) return;
        CacheBaseValues();
    }

    /// <summary>
    /// Doc lai emission rate va mau goc do designer set trong prefab. Moi thu ve sau
    /// deu la nhan voi cac gia tri nay, nen chinh trong Inspector la an ngay.
    ///
    /// Chi goi khi cuong do dang la 1 (hoac chua chay), nguoc lai se cache phai
    /// gia tri da bi nhan roi.
    /// </summary>
    public void CacheBaseValues()
    {
        if (systems == null || systems.Length == 0)
            systems = GetComponentsInChildren<ParticleSystem>(true);

        _baseRates = new float[systems.Length];
        _baseColors = new ParticleSystem.MinMaxGradient[systems.Length];

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            _baseRates[i] = systems[i].emission.rateOverTime.constantMax;
            _baseColors[i] = systems[i].main.startColor;
        }

        _applied = -1f;
    }

    private void ApplyIntensity()
    {
        EnsureCached();
        if (systems == null) return;

        bool off = Intensity <= cutoff;

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null) continue;

            var emission = ps.emission;
            emission.rateOverTime = _baseRates[i] * Intensity;

            ScaleAlpha(ps, _baseColors[i], Mathf.Clamp01(Intensity * alphaBoost));

            if (!Application.isPlaying) continue;

            if (off)
            {
                if (ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (!ps.isPlaying)
            {
                ps.Play(false);
            }
        }
    }

    /// <summary>
    /// Nhan alpha cua Start Color. Chi xu ly duoc mode mot mau va hai mau; neu
    /// designer dung Gradient thi de nguyen, luc do rieng emission rate lo phan giam.
    /// </summary>
    private static void ScaleAlpha(ParticleSystem ps, ParticleSystem.MinMaxGradient baseColor, float scale)
    {
        var main = ps.main;

        if (baseColor.mode == ParticleSystemGradientMode.Color)
        {
            Color c = baseColor.color;
            c.a *= scale;
            main.startColor = c;
            return;
        }

        if (baseColor.mode == ParticleSystemGradientMode.TwoColors)
        {
            Color min = baseColor.colorMin;
            Color max = baseColor.colorMax;
            min.a *= scale;
            max.a *= scale;
            main.startColor = new ParticleSystem.MinMaxGradient(min, max);
        }
    }

    /// <summary>
    /// Resize cac lop o day non cho khop voi Range / Cone Angle cua MouthSuction.
    /// Chi goi luc setup trong Editor (menu Tools/Devour), khong chay luc game chay:
    /// prefab van la noi giu su that, day chi la nut bam cho do phai tinh tay.
    /// </summary>
    public void FitToCone(float range, float coneAngle)
    {
        if (coneLayers == null) return;

        float halfAngle = Mathf.Clamp(coneAngle, 5f, 179f) * 0.5f * Mathf.Deg2Rad;
        float baseRadius = Mathf.Max(0.01f, Mathf.Tan(halfAngle) * range);

        for (int i = 0; i < coneLayers.Length; i++)
        {
            ParticleSystem ps = coneLayers[i];
            if (ps == null) continue;

            bool wasPlaying = ps.isPlaying;
            if (wasPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            var shape = ps.shape;
            Vector3 position = shape.position;
            position.z = range;
            shape.position = position;
            shape.radius = baseRadius;

            // Vong doi vua du de hat tat dung luc cham mieng, khong bay vot ra sau lung
            float speed = Mathf.Abs(ps.velocityOverLifetime.radial.constantMax);
            if (speed > 0.01f)
            {
                var main = ps.main;
                main.startLifetime = range / speed;
            }

            if (wasPlaying) ps.Play(false);
        }
    }
}
