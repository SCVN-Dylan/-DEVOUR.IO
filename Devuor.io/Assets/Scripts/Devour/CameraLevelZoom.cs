using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zoom camera bang FOV theo CAP DO nguoi choi, dinh nghia bang mot LIST cac moc.
///
/// Moi moc co 2 gia tri: (level, fov). Khi nguoi choi dat level >= moc thi camera doi FOV
/// sang gia tri cua moc do. FOV CANG LON = nhin cang rong = ZOOM OUT.
///   fov 50 -> goc thuong
///   fov 88 -> rong (thay nhieu hon, moi thu nho lai)
///
/// Camera KHONG doi vi tri (CameraFollow lo phan bam), chi doi FOV. Doi muot theo lerpSpeed.
/// Vi du list: (1 -> 50), (4 -> 62), (7 -> 74), (10 -> 88).
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
        [Tooltip("FOV luc dat cap nay (do). Cang lon = zoom out cang nhieu")]
        public float fov = 50f;
    }

    [Tooltip("De trong = tu tim SimpleSuction trong scene")]
    public SimpleSuction player;

    [Tooltip("De trong = lay Camera tren chinh object nay")]
    public Camera cam;

    [Tooltip("Danh sach moc: dat level nao thi FOV bao nhieu. Khong can sap xep san.")]
    public List<Step> steps = new List<Step>
    {
        new Step { level = 1, fov = 50f },
        new Step { level = 4, fov = 62f },
        new Step { level = 7, fov = 74f },
        new Step { level = 10, fov = 88f },
    };

    [Tooltip("Toc do doi FOV muot khi len cap (do/giay). 0 = doi ngay")]
    public float lerpSpeed = 25f;

    private float _fov;

    void OnEnable()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (player == null) player = Object.FindAnyObjectByType<SimpleSuction>();
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

    /// <summary>FOV ung voi cap hien tai: lay moc co level lon nhat ma <= cap nguoi choi.</summary>
    public float TargetFov()
    {
        int level = player != null ? player.Level : 1;
        float fov = cam != null ? cam.fieldOfView : 60f;
        int best = int.MinValue;
        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                if (s == null) continue;
                if (s.level <= level && s.level > best) { best = s.level; fov = s.fov; }
            }
        }
        return Mathf.Clamp(fov, 1f, 179f);
    }

    private void Apply()
    {
        if (cam != null) cam.fieldOfView = _fov;
    }
}
