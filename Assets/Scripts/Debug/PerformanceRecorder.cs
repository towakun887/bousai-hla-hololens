using Unity.Profiling;
using UnityEngine;

public class PerformanceRecorder : MonoBehaviour
{
    [SerializeField] private bool logSummary;
    [SerializeField, Min(1f)] private float logInterval = 5f;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private ProfilerRecorder mainThread;
    private ProfilerRecorder gcAllocated;
    private ProfilerRecorder drawCalls;
    private ProfilerRecorder triangles;
    private float nextLogTime;

    public long MainThreadNanoseconds => mainThread.Valid ? mainThread.LastValue : 0;
    public long GcAllocatedBytes => gcAllocated.Valid ? gcAllocated.LastValue : 0;
    public long DrawCalls => drawCalls.Valid ? drawCalls.LastValue : 0;
    public long Triangles => triangles.Valid ? triangles.LastValue : 0;

    private void OnEnable()
    {
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 120);
        gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 120);
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 120);
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 120);
    }

    private void Update()
    {
        if (!logSummary || Time.unscaledTime < nextLogTime) return;
        nextLogTime = Time.unscaledTime + logInterval;
        Debug.Log($"[Performance] main={MainThreadNanoseconds / 1000000f:F2}ms, gc={GcAllocatedBytes}B, draws={DrawCalls}, triangles={Triangles}");
    }

    private void OnDisable()
    {
        mainThread.Dispose();
        gcAllocated.Dispose();
        drawCalls.Dispose();
        triangles.Dispose();
    }
#endif
}
