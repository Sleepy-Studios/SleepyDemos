using System;
using Newtonsoft.Json.Linq;
using UnitySkills;

namespace Core.Editor.PerformanceTools.Bridge
{
    /// The only dependency on UnitySkills; core profiling remains project-owned.
    public static class ProfilerSkills
    {
        [UnitySkill("sd_profiler_get_status", "Get project profiler capabilities, target, buffer and latest task",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Query, ReadOnly = true, Mode = SkillMode.SemiAuto,
            Tags = new[] { "profiler", "performance", "capture", "status" })]
        public static object GetStatus() => Invoke(ProfilerSession.Status);

        [UnitySkill("sd_profiler_capture_start", "Start bounded CPU/GC capture on current target; does not enter Play Mode. Saves retained buffer automatically",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Execute,
            Tags = new[] { "profiler", "performance", "record", "capture", "start" })]
        /// <summary>通过当前 UnitySkills 实例启动有界采集，不切换目标或 Play Mode。</summary>
        /// <param name="frameCount">帧数上限，0 表示只使用时长，最多 100000。</param>
        /// <param name="durationSeconds">录制秒数上限，0 表示只使用帧数；双界限先到者停止。</param>
        /// <param name="timeoutSeconds">任务超时秒数，必须覆盖请求时长。</param>
        /// <param name="profileEditor">是否包含 Editor 自身采样，默认 false。</param>
        /// <param name="context">实际场景阶段与条件说明，用于同条件比较。</param>
        /// <param name="recordGcCallstacks">仅诊断时捕获 GC.Alloc 栈，默认 false，结束恢复原设置。</param>
        public static object CaptureStart(int frameCount = 300, double durationSeconds = 0,
            double timeoutSeconds = 60, bool profileEditor = false, string context = null, bool recordGcCallstacks = false) =>
            Invoke(() => ProfilerSession.Capture(frameCount, durationSeconds, timeoutSeconds, profileEditor, context, recordGcCallstacks));

        [UnitySkill("sd_profiler_capture_stop", "Stop this tool's recording and save retained data",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Execute,
            RequiredParams = new[] { "taskId" },
            Tags = new[] { "profiler", "capture", "stop" })]
        /// <summary>停止本工具指定采集并保存。</summary>
        /// <param name="taskId">本工具返回的最新任务 ID。</param>
        public static object CaptureStop(string taskId) => Invoke(() => ProfilerSession.Stop(taskId));

        [UnitySkill("sd_profiler_job_status", "Read latest profiler capture/analysis task progress",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Query, ReadOnly = true, Mode = SkillMode.SemiAuto,
            RequiredParams = new[] { "taskId" },
            Tags = new[] { "profiler", "job", "progress" })]
        /// <summary>查询本工具最新任务的进度与终态。</summary>
        /// <param name="taskId">本工具返回的最新任务 ID。</param>
        public static object JobStatus(string taskId) => Invoke(() => ProfilerSession.GetTask(taskId));

        [UnitySkill("sd_profiler_job_cancel", "Cancel this tool's capture/analysis and restore recording settings",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Execute,
            RequiredParams = new[] { "taskId" },
            Tags = new[] { "profiler", "job", "cancel" })]
        /// <summary>取消本工具指定任务并恢复设置。</summary>
        /// <param name="taskId">本工具返回的最新任务 ID。</param>
        public static object JobCancel(string taskId) => Invoke(() => ProfilerSession.Cancel(taskId));

        [UnitySkill("sd_profiler_save", "Save stopped Profiler buffer under Temp/PerformanceTools without overwriting",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Execute, LongRunning = true,
            Tags = new[] { "profiler", "data", "save", "export" })]
        /// <summary>保存停止缓冲和绑定元数据；同名文件存在时拒绝覆盖。</summary>
        /// <param name="fileName">输出目录内的 .data 文件名，null 时生成唯一名称。</param>
        public static object Save(string fileName = null) => Invoke(() => ProfilerSession.Save(fileName));

        [UnitySkill("sd_profiler_load", "Load .data into global stopped Profiler buffer; backs up current frames first",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Execute, LongRunning = true,
            RequiredParams = new[] { "path" },
            Tags = new[] { "profiler", "data", "load", "import" })]
        /// <summary>备份停止缓冲后加载采样，校验元数据并映射帧索引。</summary>
        /// <param name="path">已存在的 .data 绝对路径。</param>
        public static object Load(string path) => Invoke(() => ProfilerSession.Load(path));

        [UnitySkill("sd_profiler_analyze_start", "Incrementally analyze stopped frame range to CPU/GC report; -1 selects retained capture/buffer range",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Analyze,
            Tags = new[] { "profiler", "performance", "analyze", "cpu", "gc", "hotspot" })]
        /// <summary>启动稳定缓冲的增量 CPU 与 GC 分析。</summary>
        /// <param name="firstFrame">原始帧起点，-1 使用已保存的采集保留范围。</param>
        /// <param name="lastFrame">原始帧终点，-1 使用已保存的采集保留范围。</param>
        /// <param name="threadFilter">线程组或名称子串，null 分析所有有效线程。</param>
        /// <param name="context">实际场景阶段与条件说明，用于同条件比较。</param>
        public static object AnalyzeStart(int firstFrame = -1, int lastFrame = -1, string threadFilter = null, string context = null) =>
            Invoke(() => ProfilerSession.Analyze(firstFrame, lastFrame, threadFilter, context));

        [UnitySkill("sd_profiler_get_frames", "Page frame times and sampled GC counts from a completed analysis; omit reportPath for latest",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Query, ReadOnly = true, Mode = SkillMode.SemiAuto,
            Tags = new[] { "profiler", "frame", "time", "gc" })]
        /// <summary>分页读取完成报告的帧数据。</summary>
        /// <param name="reportPath">完成报告的 JSON 路径，null 使用内存中的最新完成报告。</param>
        /// <param name="start">零基分页起点。</param>
        /// <param name="count">返回数量，1 到 200。</param>
        public static object GetFrames(string reportPath = null, int start = 0, int count = 50) =>
            Invoke(() => ProfilerSession.Frames(reportPath, start, count));

        [UnitySkill("sd_profiler_get_frame_samples", "Read exact raw CPU sample hierarchy of a stopped frame/thread with parent indices",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Query, ReadOnly = true, Mode = SkillMode.SemiAuto,
            Tags = new[] { "profiler", "cpu", "frame", "hierarchy", "sample" })]
        /// <summary>读取停止缓冲中指定帧与线程的原始样本。</summary>
        /// <param name="frame">停止缓冲中可用的原始帧索引。</param>
        /// <param name="threadIndex">目标线程索引，默认主线程 0。</param>
        /// <param name="start">零基分页起点。</param>
        /// <param name="count">返回数量，1 到 200。</param>
        public static object GetFrameSamples(int frame, int threadIndex = 0, int start = 0, int count = 50) =>
            Invoke(() => ProfilerSession.FrameSamples(frame, threadIndex, start, count));

        [UnitySkill("sd_profiler_get_cpu_hotspots", "CPU markers grouped by thread with Total/Self/Calls; sortBy self,total,calls",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Query, ReadOnly = true, Mode = SkillMode.SemiAuto,
            Tags = new[] { "profiler", "cpu", "hotspot", "self", "total" })]
        /// <summary>读取按线程聚合的 CPU Self、Total 和 Calls。</summary>
        /// <param name="reportPath">完成报告的 JSON 路径，null 使用内存中的最新完成报告。</param>
        /// <param name="topCount">热点数量上限，1 到 200。</param>
        /// <param name="sortBy">排序指标，self、total、calls 或 bytes。</param>
        public static object GetCpuHotspots(string reportPath = null, int topCount = 20, string sortBy = "self") =>
            Invoke(() => ProfilerSession.Hotspots(reportPath, "cpu", topCount, sortBy));

        [UnitySkill("sd_profiler_get_gc_allocations", "GC bytes/events by sampled parent path, with captured stack addresses when available",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Query, ReadOnly = true, Mode = SkillMode.SemiAuto,
            Tags = new[] { "profiler", "gc", "allocation", "callstack" })]
        /// <summary>读取按样本路径与已采集调用栈聚合的分配热点。</summary>
        /// <param name="reportPath">完成报告的 JSON 路径，null 使用内存中的最新完成报告。</param>
        /// <param name="topCount">热点数量上限，1 到 200。</param>
        public static object GetGcAllocations(string reportPath = null, int topCount = 20) =>
            Invoke(() => ProfilerSession.Hotspots(reportPath, "gc", topCount, "bytes"));

        [UnitySkill("sd_profiler_compare", "Compare two report JSON files using per-frame CPU/GC metrics and context warnings",
            Category = SkillCategory.Profiler, Operation = SkillOperation.Analyze, ReadOnly = true, Mode = SkillMode.SemiAuto,
            RequiredParams = new[] { "baselinePath", "currentPath" },
            Tags = new[] { "profiler", "compare", "baseline", "regression" })]
        /// <summary>按每帧指标比较两份完成报告；条件不同会返回警告。</summary>
        /// <param name="baselinePath">优化前完成报告路径。</param>
        /// <param name="currentPath">优化后完成报告路径。</param>
        /// <param name="topCount">热点数量上限，1 到 200。</param>
        public static object Compare(string baselinePath, string currentPath, int topCount = 20) =>
            Invoke(() => ProfilerSession.Compare(baselinePath, currentPath, topCount));

        private static object Invoke(Func<object> action)
        {
            try
            {
                return JToken.FromObject(action(), ProfilerSession.CreateSerializer());
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message,
                    suggestedFixes = new[] { "Check sd_profiler_get_status and .agents/skills/unity-performance/SKILL.md." } };
            }
        }
    }
}
