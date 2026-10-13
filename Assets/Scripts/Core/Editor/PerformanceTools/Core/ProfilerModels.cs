using System;
using System.Collections.Generic;

namespace Core.Editor.PerformanceTools
{
    /// Portable evidence produced from a stopped Profiler buffer.
    [Serializable]
    public sealed class ProfilerReport
    {
        public int SchemaVersion = 1;
        public string Id;
        public string CreatedUtc;
        public string UnityVersion;
        public string Source;
        public string Target;
        public string Context;
        public string ThreadFilter;
        public bool? DeepProfiling;
        public bool? ProfileEditor;
        public int? MemoryRecordMode;
        public int FirstFrame;
        public int LastFrame;
        public List<ProfilerFrame> Frames = new List<ProfilerFrame>();
        public List<ProfilerHotspot> Cpu = new List<ProfilerHotspot>();
        public List<ProfilerHotspot> Gc = new List<ProfilerHotspot>();
        public List<string> Warnings = new List<string>();
    }

    /// Frame time is taken from the first valid thread, not summed across threads.
    [Serializable]
    public sealed class ProfilerFrame
    {
        public int Index;
        public double TimeMs;
        public long GcBytes;
        public int GcEvents;
        public int GcUnknownSizeEvents;
        public int Threads;
    }

    /// Aggregated by thread and marker; GC entries also retain the sampled parent path.
    [Serializable]
    public sealed class ProfilerHotspot
    {
        public string Thread;
        public string Marker;
        public string SamplePath;
        public long Calls;
        public double TotalMs;
        public double SelfMs;
        public long GcBytes;
        public int UnknownSizeEvents;
        public List<string> Callstack;
        public string ProjectMethod;
        public string AllocationSite;
    }

    /// Only the latest task is retained in memory; reports survive independently on disk.
    [Serializable]
    public sealed class ProfilerTask
    {
        public string Id;
        public string Kind;
        public string State;
        public string Message;
        /// 采集为首末收到帧索引跨度；分析为已经完成的分析帧数。
        public int CompletedFrames;
        /// 实际收到的目标帧回调数；旧任务未知时为 null，不代表上游生成帧数。
        public int? ReceivedFrameCallbacks;
        public int RequestedFrames;
        public double RequestedDurationSeconds;
        public bool TimedOut;
        public int FirstFrame = -1;
        public int LastFrame = -1;
        /// 采集索引范围被环形缓冲裁掉的数量，不包含上游未送达帧。
        public int DroppedFrames;
        public int RetainedFirstFrame = -1;
        public int RetainedLastFrame = -1;
        /// 采集索引范围与当前环形缓冲的交集帧数。
        public int RetainedFrames;
        public string DataPath;
        public string MetadataPath;
        public string BackupPath;
        public string ReportPath;
        public string Context;
        public string Target;
        public string StartedUtc;
        public bool? ProfileEditor;
        public bool? DeepProfiling;
        public bool RecordGcCallstacks;
    }

    /// File-bound provenance; frame indices describe the saved buffer before loading reindexes it.
    [Serializable]
    public sealed class ProfilerDataMetadata
    {
        public int SchemaVersion = 1;
        public long DataLength;
        public string DataSha256;
        public string CaptureId;
        public string UnityVersion;
        public string Target;
        public string Context;
        public bool? ProfileEditor;
        public bool? DeepProfiling;
        public int? MemoryRecordMode;
        public int BufferFirstFrame;
        public int BufferLastFrame;
        public int FirstFrame;
        public int LastFrame;
    }
}
