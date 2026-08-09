using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zoom camera bang FOV theo CAP DO, CONG DON qua tung moc (khong phai set tuyet doi).
///
/// FOV = baseFov + tong (add) cua MOI moc co level <= cap nguoi choi.
/// Moi moc: (level, add) = dat cap nay thi CONG THEM 'add' do vao FOV.
///   baseFov = 50, moc (4,+12),(7,+12),(10,+14):
///     cap 1..3  -> 50
///     cap 4..6  -> 62   (50+12)
///     cap 7..9  -> 74   (50+12+12)
///     cap 10    -> 88   (50+12+12+14)
/// FOV cang lon = nhin cang rong = ZOOM OUT. Doi muot theo lerpSpeed.
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
        [Tooltip("CONG THEM bao nhieu do FOV khi dat cap nay (cong don voi cac moc truoc)")]
        public float add = 12f;
    }

    [Tooltip("De trong = tu tim SimpleSuction trong scene")]
    public SimpleSuction player;

    [Tooltip("De trong = lay Camera tren chinh object nay")]
    public Camera cam;

    [Tooltip("FOV goc (khi chua qua moc nao). Cac moc cong don len tren nay")]
    public float baseFov = 50f;

    [Tooltip("Danh sach moc: dat level nay thi CONG THEM 'add' do. Cong don qua nhieu moc.")]
    public List<Step> steps = new List<Step>
    {
        new Step { level = 4, add = 12f },
        new Step { level = 7, add = 12f },
        new Step { level = 10, add = 14f },
    };

    [Tooltip("Toc do doi FOV muot (do/giay). 0 = doi ngay")]
    public float lerpSpeed = 25f;

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
            ? Mathf.MoveTowards(_fov, target, lerpSpeed * Time.deltaTime)
            : target;

        Apply();
    }

    /// <summary>FOV = baseFov + tong add cua cac moc da dat (cong don).</summary>
    public float TargetFov()
    {
        int level = player != null ? player.Level : 1;
        float fov = baseFov;
        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                if (s != null && level >= s.level) fov += s.add;   // CONG DON
            }
        }
        return Mathf.Clamp(fov, 1f, 179f);
    }

    private void Apply()
    {
        if (cam != null) cam.fieldOfView = _fov;
    }
}
