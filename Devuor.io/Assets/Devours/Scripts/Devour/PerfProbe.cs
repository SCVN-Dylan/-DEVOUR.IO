using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

/// <summary>
/// CONG CU DO hieu nang - gan tam vao scene khi can do, khong phai thu cua gameplay.
///
/// Doc thang cac counter cua Unity Profiler (ProfilerRecorder) roi tinh trung binh tren mot cua
/// so N frame. Dung de tra loi "phan nao dang an bao nhieu ms" bang SO DO THAT thay vi doan.
///
/// Vi sao khong do bang Stopwatch: nhung thu nhu mo phong vat ly, animation, render deu nam
/// ngoai code C# cua minh; Stopwatch quanh mot ham chi thay duoc phan cua ham do.
///
/// Ten marker khac nhau theo phien ban Unity - cai nao khong co thi bao "khong co counter",
/// khong phai loi.
/// </summary>
[DisallowMultipleComponent]
public class PerfProbe : MonoBehaviour
{
    [Tooltip("Trung binh tren bao nhieu frame gan nhat")]
    public int windowFrames = 120;

    [Tooltip("Ten cac counter cua Profiler can theo doi")]
    public List<string> markers = new List<string>
    {
        "PlayerLoop",
        "BehaviourUpdate",
        "FixedBehaviourUpdate",
        "Physics.Processing",
        "Physics.Simulate",
    };

    private class Track
    {
        public string name;
        public ProfilerRecorder rec;
        public double sumMs;
        public double maxMs;
        public int frames;
    }

    private readonly List<Track> _tracks = new List<Track>();

    /// <summary>So frame da gom duoc tu lan Reset gan nhat.</summary>
    public int SampledFrames { get; private set; }

    void OnEnable()
    {
        _tracks.Clear();

        // Tra HANDLE theo ten thay vi doan category: ten marker nam rai o nhieu category khac
        // nhau (Scripts, Physics, Internal...) va bo category cung doi theo phien ban Unity.
        var available = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(available);

        for (int i = 0; i < markers.Count; i++)
        {
            var t = new Track { name = markers[i] };
            foreach (var h in available)
            {
                if (ProfilerRecorderHandle.GetDescription(h).Name != markers[i]) continue;
                t.rec = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default);
                t.rec.Start();
                break;
            }
            _tracks.Add(t);
        }
        ResetStats();
    }

    /// <summary>Liet ke moi counter Unity dang cung cap - de biet co the do duoc nhung gi.</summary>
    public static string ListAvailable(string filter = null)
    {
        var sb = new System.Text.StringBuilder();
        var available = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(available);

        foreach (var h in available)
        {
            string n = ProfilerRecorderHandle.GetDescription(h).Name;
            if (!string.IsNullOrEmpty(filter) && n.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            sb.AppendLine("  " + n);
        }
        return sb.ToString();
    }

    void OnDisable()
    {
        for (int i = 0; i < _tracks.Count; i++)
            if (_tracks[i].rec.Valid) _tracks[i].rec.Dispose();
        _tracks.Clear();
    }

    public void ResetStats()
    {
        SampledFrames = 0;
        for (int i = 0; i < _tracks.Count; i++)
        {
            _tracks[i].sumMs = 0;
            _tracks[i].maxMs = 0;
            _tracks[i].frames = 0;
        }
    }

    void LateUpdate()
    {
        if (SampledFrames >= windowFrames) return;
        SampledFrames++;

        for (int i = 0; i < _tracks.Count; i++)
        {
            Track t = _tracks[i];
            if (!t.rec.Valid) continue;

            double ms = t.rec.LastValue * 1e-6;   // nanosecond -> millisecond
            if (ms <= 0) continue;

            t.sumMs += ms;
            if (ms > t.maxMs) t.maxMs = ms;
            t.frames++;
        }
    }

    /// <summary>Bang ket qua da doc duoc, moi dong mot counter.</summary>
    public string Report()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("da gom " + SampledFrames + " frame:");
        for (int i = 0; i < _tracks.Count; i++)
        {
            Track t = _tracks[i];
            if (!t.rec.Valid || t.frames == 0)
            {
                sb.AppendLine("  " + t.name + " = (khong co counter nay)");
                continue;
            }
            sb.AppendLine("  " + t.name + " = trung binh " + (t.sumMs / t.frames).ToString("F3")
                          + " ms | cao nhat " + t.maxMs.ToString("F3") + " ms");
        }
        return sb.ToString();
    }
}
