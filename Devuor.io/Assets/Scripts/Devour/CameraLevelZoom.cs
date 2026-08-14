using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zoom camera bang FOV theo CAP DO, CONG DON qua tung moc (khong phai set tuyet doi).
///
/// Ho tro 2 che do / ket hop:
/// 1. zoomEveryLevel: Cong 'addPerLevel' cho moi level.
/// 2. useSteps: Cong gia tri 'add' khi dat cac moc 'level' trong danh sach steps.
/// 3. skipAddPerLevelOnStep: Khi bat ca 2, tai level co trong steps se dung 'add' cua Step va KHONG cong 'addPerLevel'.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraLevelZoom : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [Tooltip("Cap do dat toi")]
        public int level = 1;
        [Tooltip("CONG THEM bao nhieu do FOV khi dat cap nay (cong don)")]
        public float add = 12f;
    }

    [Tooltip("De trong = tu tim SimpleSuction trong scene")]
    public SimpleSuction player;

    [Tooltip("De trong = lay Camera tren chinh object nay")]
    public Camera cam;

    [Tooltip("FOV goc (khi chua qua moc nao). Cac moc cong don len tren nay")]
    public float baseFov = 50f;

    [Header("Zoom theo moc (Steps)")]
    [Tooltip("Co bat/tat su dung danh sach moc (Steps)")]
    public bool useSteps = true;

    [Tooltip("Danh sach moc: dat level nay thi CONG THEM 'add' do. Cong don qua nhieu moc.")]
    public List<Step> steps = new List<Step>
    {
        new Step { level = 4, add = 12f },
        new Step { level = 7, add = 12f },
        new Step { level = 10, add = 14f },
    };

    [Header("Zoom nho moi cap (Add Per Level)")]
    [Tooltip("BAT: moi lan len cap deu CONG THEM addPerLevel do FOV.\nTAT: khong cong per level.")]
    public bool zoomEveryLevel = true;

    [Tooltip("Do FOV cong them cho MOI cap da len (khi bat zoomEveryLevel). Vi du: 0.2 = len 5 cap thi +1 do")]
    public float addPerLevel = 0.2f;

    [Header("Ket hop Steps & Add Per Level")]
    [Tooltip("BAT: Khi dung dong thoi Steps va AddPerLevel, tai level co trong Steps se lay data cua Step va KHONG cong addPerLevel cho level do.\nTAT: Cong ca hai tai level co Step.")]
    public bool skipAddPerLevelOnStep = true;

    [Header("Muot ma")]
    [Tooltip("Toc do doi FOV muot (he so Lerp/giay). 8-12 = phan hoi nhanh luc dau, em o cuoi. 0 = doi ngay")]
    public float lerpSpeed = 8f;

    private float _fov;

    void OnEnable()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (player == null) player = Object.FindAnyObjectByType<SimpleSuction>();
        if (baseFov <= 0f && cam != null) baseFov = cam.fieldOfView;
        _fov = TargetFov();
        Apply();
    }

    void Update()
    {
        if (cam == null || player == null) return;

        float target = TargetFov();
        _fov = (Application.isPlaying && lerpSpeed > 0f)
            ? Mathf.Lerp(_fov, target, lerpSpeed * Time.deltaTime)
            : target;

        Apply();
    }

    /// <summary>
    /// Tinh FOV muc tieu dua tren level hien tai cua player.
    /// Cong dồn qua tung level tu 2 den level hien tai.
    /// </summary>
    public float TargetFov()
    {
        int currentLevel = player != null ? player.Level : 1;
        float fov = baseFov;

        if (currentLevel <= 1) return Mathf.Clamp(fov, 1f, 179f);

        // Map cac step theo level de tra cuu nhanh
        Dictionary<int, float> stepMap = null;
        if (useSteps && steps != null && steps.Count > 0)
        {
            stepMap = new Dictionary<int, float>();
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                if (s != null && s.level > 1)
                {
                    if (stepMap.ContainsKey(s.level))
                        stepMap[s.level] += s.add;
                    else
                        stepMap[s.level] = s.add;
                }
            }
        }

        // Duyet qua tung level tu level 2 den currentLevel
        for (int lvl = 2; lvl <= currentLevel; lvl++)
        {
            float stepAdd = 0f;
            bool hasStep = stepMap != null && stepMap.TryGetValue(lvl, out stepAdd);

            if (hasStep)
            {
                fov += stepAdd;
                if (zoomEveryLevel && !skipAddPerLevelOnStep)
                {
                    fov += addPerLevel;
                }
            }
            else if (zoomEveryLevel)
            {
                fov += addPerLevel;
            }
        }

        return Mathf.Clamp(fov, 1f, 179f);
    }

    private void Apply()
    {
        if (cam != null) cam.fieldOfView = _fov;
    }
}
