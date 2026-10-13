using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace Core.Editor.PerformanceTools
{
    /// Owns only this tool's recording/analysis task. All entry points run on the Editor main thread.
    public static class ProfilerSession
    {
        private const string TaskKey = "SleepyDemos.PerformanceTools.Task";
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented
        };
        private static ProfilerTask task;
        private static IEnumerator analysis;
        private static ProfilerReport report;
        private static double deadline;
        private static bool previousProfileEditor;
        private static bool previousCpuEnabled;
        private static ProfilerMemoryRecordMode previousMemoryRecordMode;
        private static ProfilerMemoryRecordMode captureMemoryRecordMode;
        private static int? sourceMemoryRecordMode;
        private static bool ownsRecording;
        private static bool changingBuffer;
        private static bool bufferChanged;
        private static int analysisTarget;
        private static string source = "current-buffer";
        private static long sourceDataLength;
        private static string sourceDataHash;
        private static bool? sourceProfileEditor;
        private static bool? sourceDeepProfiling;
        private static string sourceTarget;
        private static string sourceContext;
        private static string outputDataPath;
        private static int sourceFirstFrame = -1;
        private static int sourceLastFrame = -1;
        private static string sourceCaptureId;
        private static string sourceUnityVersion;
        private static string sourceWarning;
        private static double stopStarted;
        private static double lastPendingFrameAt;
        private static int quietUpdates;

        static ProfilerSession()
        {
            // Initialize only on first use, never during unattended batchmode builds.
            if (Application.isBatchMode)
                return;
            var saved = SessionState.GetString(TaskKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
                task = JsonConvert.DeserializeObject<ProfilerTask>(saved);
            if (task != null && IsRunning)
            {
                task.State = "interrupted";
                task.Message = "Editor domain reloaded; start a new task.";
                Persist();
            }
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.quitting += BeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ProfilerDriver.NewProfilerFrameRecorded += OnFrame;
            ProfilerDriver.profileLoaded += OnBufferChanged;
            ProfilerDriver.profileCleared += OnBufferChanged;
        }

        public static bool IsRunning => task != null &&
            (task.State == "recording" || task.State == "stopping" || task.State == "analyzing");
        public static string OutputDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/PerformanceTools"));

        public static JsonSerializer CreateSerializer() => JsonSerializer.Create(jsonSettings);

        public static object Status()
        {
            if (Application.isBatchMode)
                return new { success = true, protocolVersion = 2, available = false,
                    reason = "Performance tools are disabled in batchmode; no callbacks or recording were started." };
            return new
            {
                success = true, protocolVersion = 2, available = !BuildPipeline.isBuildingPlayer,
                unityVersion = Application.unityVersion,
                recording = ProfilerDriver.enabled, profileEditor = ProfilerDriver.profileEditor,
                deepProfiling = ProfilerDriver.deepProfiling,
                memoryRecordMode = ProfilerDriver.memoryRecordMode.ToString(),
                cpuEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU),
                targetId = ProfilerDriver.connectedProfiler, target = TargetName(),
                firstFrame = ProfilerDriver.firstFrameIndex, lastFrame = ProfilerDriver.lastFrameIndex,
                isPlaying = EditorApplication.isPlaying, isPaused = EditorApplication.isPaused,
                activeTask = task, outputDirectory = OutputDirectory,
                source, sourceCaptureId, sourceFirstFrame, sourceLastFrame,
                sourceProfileEditor, sourceDeepProfiling, sourceWarning,
                capabilities = new[] { "bounded_capture", "save_load", "cpu", "gc_alloc", "compare", "cancel" },
                limitations = "Uses the currently connected target. Does not switch target, toggle Deep Profile, capture memory snapshots or drive Frame Debugger."
            };
        }

        /// <summary>开始有界采集；拒绝接管已有录制，备份旧缓冲并恢复本工具临时设置。</summary>
        /// <param name="frameCount">帧数上限，0 表示只使用时长，最多 100000。</param>
        /// <param name="durationSeconds">录制秒数上限，0 表示只使用帧数；双界限先到者停止。</param>
        /// <param name="timeoutSeconds">任务超时秒数，必须覆盖请求时长。</param>
        /// <param name="profileEditor">是否包含 Editor 自身采样，默认 false。</param>
        /// <param name="context">实际场景阶段与条件说明，用于同条件比较。</param>
        /// <param name="recordGcCallstacks">仅诊断时捕获 GC.Alloc 栈，默认 false，结束恢复原设置。</param>
        public static ProfilerTask Capture(int frameCount, double durationSeconds, double timeoutSeconds,
            bool profileEditor, string context, bool recordGcCallstacks = false)
        {
            RequireIdle();
            if (ProfilerDriver.enabled)
                throw new InvalidOperationException("Profiler is already recording. Stop the existing recording yourself before starting a tool-owned session.");
            if (ProfilerDriver.deepProfiling)
                throw new InvalidOperationException("Deep Profile is enabled. Disable it manually before a normal capture; this tool never triggers its reload.");
            if (frameCount < 0 || frameCount > 100000 || !Finite(durationSeconds) || durationSeconds < 0 || durationSeconds > 300 ||
                !Finite(timeoutSeconds) || timeoutSeconds < 1 || timeoutSeconds > 600 ||
                (frameCount == 0 && durationSeconds == 0))
                throw new ArgumentException("Use frameCount 0..100000 and durationSeconds 0..300 (at least one positive), timeoutSeconds 1..600.");
            if (durationSeconds > timeoutSeconds)
                throw new ArgumentException("timeoutSeconds must cover durationSeconds.");
            string backupPath = null;
            if (HasFrames() && !CanRestoreSource())
            {
                backupPath = NewOutputPath("before_capture_" + NewId() + ".data");
                SaveBuffer(backupPath);
                SaveMetadata(backupPath);
            }
            changingBuffer = true;
            try { ProfilerDriver.ClearAllFrames(); }
            finally { changingBuffer = false; }
            previousProfileEditor = ProfilerDriver.profileEditor;
            previousCpuEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU);
            previousMemoryRecordMode = ProfilerDriver.memoryRecordMode;
            captureMemoryRecordMode = recordGcCallstacks ? previousMemoryRecordMode | ProfilerMemoryRecordMode.GCAlloc
                : previousMemoryRecordMode & ~ProfilerMemoryRecordMode.GCAlloc;
            task = NewTask("capture", "recording", context);
            task.BackupPath = backupPath;
            task.RecordGcCallstacks = recordGcCallstacks;
            task.RequestedFrames = frameCount;
            task.ReceivedFrameCallbacks = 0;
            task.RequestedDurationSeconds = durationSeconds;
            task.ProfileEditor = profileEditor;
            analysisTarget = ProfilerDriver.connectedProfiler;
            deadline = EditorApplication.timeSinceStartup + (durationSeconds > 0 ? durationSeconds : timeoutSeconds);
            outputDataPath = NewOutputPath(task.Id + ".data");
            source = "live-buffer:" + task.Id;
            sourceProfileEditor = profileEditor;
            sourceDeepProfiling = false;
            sourceMemoryRecordMode = (int)captureMemoryRecordMode;
            sourceTarget = task.Target;
            sourceContext = context;
            sourceFirstFrame = sourceLastFrame = -1;
            sourceCaptureId = task.Id;
            sourceUnityVersion = Application.unityVersion;
            sourceWarning = null;
            bufferChanged = false;
            report = null;
            ownsRecording = true;
            Persist();
            try
            {
                ProfilerDriver.profileEditor = profileEditor;
                ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true);
                ProfilerDriver.memoryRecordMode = captureMemoryRecordMode;
                ProfilerDriver.enabled = true;
            }
            catch
            {
                RestoreRecording();
                task.State = "failed";
                task.Message = "Could not enable recording.";
                Persist();
                throw;
            }
            return task;
        }

        /// <summary>停止指定的本工具录制，待处理帧稳定后保存。</summary>
        /// <param name="taskId">本工具返回的最新任务 ID。</param>
        public static ProfilerTask Stop(string taskId)
        {
            RequireTask(taskId);
            if (task.State != "recording")
                return task;
            try
            {
                BeginStop("Stopped by caller.");
            }
            catch (Exception ex)
            {
                EndTask("failed", ex.Message);
                throw;
            }
            return task;
        }

        /// <summary>取消指定任务并恢复采集设置，不删除已保存证据。</summary>
        /// <param name="taskId">本工具返回的最新任务 ID。</param>
        public static ProfilerTask Cancel(string taskId)
        {
            RequireTask(taskId);
            if (!IsRunning)
                return task;
            EndTask("cancelled", "Cancelled by caller; existing buffer retained.");
            return task;
        }

        /// <summary>查询最新任务；其它任务请读取已返回的报告路径。</summary>
        /// <param name="taskId">本工具返回的最新任务 ID。</param>
        public static ProfilerTask GetTask(string taskId)
        {
            RequireTask(taskId);
            return task;
        }

        /// <summary>保存停止缓冲和绑定元数据；同名文件存在时拒绝覆盖。</summary>
        /// <param name="fileName">输出目录内的 .data 文件名，null 时生成唯一名称。</param>
        public static object Save(string fileName)
        {
            RequireStopped();
            if (!string.IsNullOrEmpty(fileName) && !fileName.EndsWith(".data", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Profiler data fileName must end in .data.");
            var path = NewOutputPath(string.IsNullOrEmpty(fileName) ? NewId() + ".data" : fileName);
            SaveBuffer(path);
            var metadataPath = SaveMetadata(path);
            return new { success = true, dataPath = path, metadataPath, firstFrame = ProfilerDriver.firstFrameIndex,
                lastFrame = ProfilerDriver.lastFrameIndex };
        }

        /// <summary>备份停止缓冲后加载采样，校验元数据并映射帧索引。</summary>
        /// <param name="path">已存在的 .data 绝对路径。</param>
        public static object Load(string path)
        {
            RequireStopped();
            path = Path.GetFullPath(path);
            if (!path.EndsWith(".data", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new ArgumentException("An existing Unity Profiler .data path is required.");
            // Loading replaces the global Profiler buffer. Back up existing evidence first.
            string backupPath = null;
            if (HasFrames() && CanRestoreSource())
            {
                backupPath = source;
            }
            else if (HasFrames())
            {
                backupPath = NewOutputPath("before_load_" + NewId() + ".data");
                SaveBuffer(backupPath);
                SaveMetadata(backupPath);
            }
            changingBuffer = true;
            try
            {
                if (!ProfilerDriver.LoadProfile(path, false))
                    throw new InvalidOperationException("Unity could not load profile: " + path);
                source = path;
                sourceProfileEditor = null;
                sourceDeepProfiling = null;
                sourceMemoryRecordMode = null;
                sourceTarget = "imported/unknown";
                sourceContext = null;
                sourceFirstFrame = sourceLastFrame = -1;
                sourceCaptureId = null;
                sourceUnityVersion = null;
                sourceWarning = null;
                RestoreMetadata(path);
                report = null;
                return new { success = true, dataPath = path, backupPath,
                    firstFrame = ProfilerDriver.firstFrameIndex, lastFrame = ProfilerDriver.lastFrameIndex,
                    sourceFirstFrame, sourceLastFrame, sourceCaptureId, sourceWarning,
                    sourceProfileEditor, sourceDeepProfiling };
            }
            catch
            {
                if (backupPath != null)
                    ProfilerDriver.LoadProfile(backupPath, false);
                // A failed load/rollback cannot prove which buffer is now available.
                changingBuffer = false;
                OnBufferChanged();
                throw;
            }
            finally
            {
                changingBuffer = false;
            }
        }

        /// <summary>增量分析稳定的停止缓冲，设置未知时不补造来源信息。</summary>
        /// <param name="firstFrame">原始帧起点，-1 使用已保存的采集保留范围。</param>
        /// <param name="lastFrame">原始帧终点，-1 使用已保存的采集保留范围。</param>
        /// <param name="threadFilter">线程组或名称子串，null 分析所有有效线程。</param>
        /// <param name="context">实际场景阶段与条件说明，用于同条件比较。</param>
        public static ProfilerTask Analyze(int firstFrame, int lastFrame, string threadFilter, string context)
        {
            RequireStopped();
            if (!HasFrames())
                throw new InvalidOperationException("No Profiler frames are available.");
            // Default to our most recent capture's retained range; .data files may also contain earlier frames.
            if (firstFrame == -1)
                firstFrame = sourceFirstFrame >= 0 ? Math.Max(sourceFirstFrame, ProfilerDriver.firstFrameIndex) : ProfilerDriver.firstFrameIndex;
            if (lastFrame == -1)
                lastFrame = sourceLastFrame >= 0 ? Math.Min(sourceLastFrame, ProfilerDriver.lastFrameIndex) : ProfilerDriver.lastFrameIndex;
            if (firstFrame < ProfilerDriver.firstFrameIndex || lastFrame > ProfilerDriver.lastFrameIndex || lastFrame < firstFrame)
                throw new ArgumentException("Requested range is outside the available buffer.");
            if (lastFrame - firstFrame >= 100000)
                throw new ArgumentException("Analyze at most 100000 frames per task.");
            task = NewTask("analysis", "analyzing", context ?? sourceContext);
            task.ProfileEditor = sourceProfileEditor;
            task.DeepProfiling = sourceDeepProfiling;
            task.Target = sourceTarget ?? TargetName();
            task.FirstFrame = firstFrame;
            task.LastFrame = lastFrame;
            task.RequestedFrames = lastFrame - firstFrame + 1;
            task.ReportPath = NewOutputPath(task.Id + ".json");
            report = new ProfilerReport
            {
                Id = task.Id, CreatedUtc = DateTime.UtcNow.ToString("O"), UnityVersion = sourceUnityVersion ?? "unknown",
                Source = source, Target = sourceTarget ?? "unknown", Context = task.Context,
                ThreadFilter = threadFilter ?? string.Empty, FirstFrame = firstFrame, LastFrame = lastFrame,
                ProfileEditor = sourceProfileEditor, DeepProfiling = sourceDeepProfiling, MemoryRecordMode = sourceMemoryRecordMode
            };
            if (!string.IsNullOrEmpty(sourceWarning))
                report.Warnings.Add(sourceWarning);
            bufferChanged = false;
            analysisTarget = ProfilerDriver.connectedProfiler;
            analysis = ProfilerAnalysis.Read(report, count => task.CompletedFrames = count);
            Persist();
            return task;
        }

        /// <summary>分页读取完成报告的帧数据。</summary>
        /// <param name="reportPath">完成报告的 JSON 路径，null 使用内存中的最新完成报告。</param>
        /// <param name="start">零基分页起点。</param>
        /// <param name="count">返回数量，1 到 200。</param>
        public static object Frames(string reportPath, int start, int count)
        {
            ValidatePage(start, count);
            var value = ReadReport(reportPath);
            return new { success = true, total = value.Frames.Count, start, frames = value.Frames.Skip(start).Take(count).ToArray() };
        }

        /// <summary>读取停止缓冲的精确样本层级、分配大小和已采集的栈。</summary>
        /// <param name="frame">停止缓冲中可用的原始帧索引。</param>
        /// <param name="threadIndex">目标线程索引，默认主线程 0。</param>
        /// <param name="start">零基分页起点。</param>
        /// <param name="count">返回数量，1 到 200。</param>
        public static object FrameSamples(int frame, int threadIndex, int start, int count)
        {
            RequireStopped();
            ValidatePage(start, count);
            if (threadIndex < 0 || start > 1000000 || frame < ProfilerDriver.firstFrameIndex || frame > ProfilerDriver.lastFrameIndex)
                throw new ArgumentException("Use an available frame, nonnegative threadIndex and start <= 1000000.");
            using (var raw = ProfilerDriver.GetRawFrameDataView(frame, threadIndex))
            {
                if (!raw.valid)
                    throw new ArgumentException("Thread is unavailable in this frame.");
                var rows = new List<object>();
                var parents = new List<int>();
                var ends = new List<int>();
                for (var i = 0; i < Math.Min(raw.sampleCount, start + count); i++)
                {
                    while (ends.Count > 0 && i >= ends[ends.Count - 1])
                    {
                        ends.RemoveAt(ends.Count - 1);
                        parents.RemoveAt(parents.Count - 1);
                    }
                    var end = i + raw.GetSampleChildrenCountRecursive(i) + 1;
                    if (i >= start)
                        rows.Add(new { sampleIndex = i, parentIndex = parents.Count == 0 ? -1 : parents[parents.Count - 1],
                            depth = parents.Count, name = raw.GetSampleName(i), totalMs = raw.GetSampleTimeMs(i),
                            selfMs = ProfilerFrameReader.GetSelfTimeMs(raw, i), nextSiblingIndex = end,
                            childrenCount = raw.GetSampleChildrenCount(i),
                            gcBytes = raw.GetSampleName(i) == "GC.Alloc" ? ProfilerFrameReader.GetGcAllocBytes(raw, i) : 0,
                            callstack = raw.GetSampleName(i) == "GC.Alloc" ? ProfilerFrameReader.ReadCallstack(raw, i) : null });
                    if (end > i + 1)
                    {
                        parents.Add(i);
                        ends.Add(end);
                    }
                }
                return new { success = true, frame, threadIndex, raw.threadName, raw.threadId,
                    total = raw.sampleCount, start, samples = rows };
            }
        }

        /// <summary>按指定指标读取完成报告中的 CPU 或 GC 热点。</summary>
        /// <param name="reportPath">完成报告的 JSON 路径，null 使用内存中的最新完成报告。</param>
        /// <param name="kind">cpu 或 gc。</param>
        /// <param name="topCount">热点数量上限，1 到 200。</param>
        /// <param name="sortBy">排序指标，self、total、calls 或 bytes。</param>
        public static object Hotspots(string reportPath, string kind, int topCount, string sortBy)
        {
            ValidatePage(0, topCount);
            var value = ReadReport(reportPath);
            if (kind != "cpu" && kind != "gc")
                throw new ArgumentException("kind must be cpu or gc.");
            var entries = kind == "cpu" ? value.Cpu : value.Gc;
            if (sortBy != "self" && sortBy != "total" && sortBy != "calls" && sortBy != "bytes")
                throw new ArgumentException("sortBy must be self, total, calls or bytes.");
            Func<ProfilerHotspot, double> order = x => sortBy == "total" ? x.TotalMs :
                sortBy == "calls" ? x.Calls : sortBy == "bytes" ? x.GcBytes : x.SelfMs;
            return new { success = true, summary = ProfilerAnalysis.Summary(value), total = entries.Count,
                hotspots = entries.OrderByDescending(order).Take(topCount).ToArray() };
        }

        /// <summary>按每帧指标比较两份完成报告；条件不同会返回警告。</summary>
        /// <param name="baselinePath">优化前完成报告路径。</param>
        /// <param name="currentPath">优化后完成报告路径。</param>
        /// <param name="topCount">热点数量上限，1 到 200。</param>
        public static object Compare(string baselinePath, string currentPath, int topCount)
        {
            if (string.IsNullOrWhiteSpace(baselinePath) || string.IsNullOrWhiteSpace(currentPath))
                throw new ArgumentException("Both baselinePath and currentPath are required.");
            ValidatePage(0, topCount);
            return ProfilerAnalysis.Compare(ReadReport(baselinePath), ReadReport(currentPath), topCount);
        }

        private static void Update()
        {
            if (!IsRunning)
                return;
            try
            {
                if (task.State == "stopping" && ProfilerDriver.enabled)
                {
                    // Do not stop a new recording started by the user while ours was draining.
                    ownsRecording = false;
                    EndTask("interrupted", "Recording restarted externally while capture was stopping.");
                    return;
                }
                if (ProfilerDriver.connectedProfiler != analysisTarget)
                {
                    // The newly selected target is not owned by this capture.
                    ownsRecording = false;
                    EndTask("interrupted", "Profiler target changed externally.");
                    return;
                }
                if (BuildPipeline.isBuildingPlayer)
                {
                    EndTask("interrupted", "Player build started; performance task stopped.");
                    return;
                }
                if (bufferChanged || ProfilerDriver.connectedProfiler != analysisTarget)
                {
                    EndTask("interrupted", "Profiler buffer or connected target changed externally.");
                    return;
                }
                if (task.Kind == "capture" && (ProfilerDriver.deepProfiling != task.DeepProfiling ||
                    ProfilerDriver.profileEditor != task.ProfileEditor || !ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU)
                    || ProfilerDriver.memoryRecordMode != captureMemoryRecordMode))
                {
                    EndTask("interrupted", "Profiler capture settings changed externally; mixed settings are not a valid baseline.");
                    return;
                }
                if (task.State == "recording")
                {
                    if (!ProfilerDriver.enabled)
                    {
                        BeginStop("Recording stopped externally.");
                        return;
                    }
                    if ((task.RequestedFrames > 0 && task.CompletedFrames >= task.RequestedFrames) ||
                        EditorApplication.timeSinceStartup >= deadline)
                    {
                        task.TimedOut = task.RequestedDurationSeconds == 0 && task.CompletedFrames < task.RequestedFrames;
                        BeginStop(task.TimedOut ? "Capture timed out before requested frame count." : "Capture limit reached.");
                    }
                }
                else if (task.State == "stopping")
                {
                    quietUpdates++;
                    var now = EditorApplication.timeSinceStartup;
                    if (quietUpdates >= 2 && now - lastPendingFrameAt >= 0.1)
                        CompleteCapture();
                    else if (now - stopStarted >= 2)
                        EndTask("failed", "Profiler buffer did not settle after stopping; buffer retained, no completed capture claimed.");
                }
                else if (ProfilerDriver.enabled)
                    EndTask("interrupted", "Recording started externally during analysis.");
                else if (!analysis.MoveNext())
                {
                    File.WriteAllText(task.ReportPath, JsonConvert.SerializeObject(report, jsonSettings));
                    EndTask("completed", "Analysis report saved.");
                }
            }
            catch (Exception ex)
            {
                EndTask("failed", ex.Message);
            }
        }

        private static void OnFrame(int connectionId, int frame)
        {
            if (!ownsRecording)
            {
                // A user's later recording invalidates the provenance of an earlier capture.
                OnBufferChanged();
                return;
            }
            if ((task.State != "recording" && task.State != "stopping") || connectionId != analysisTarget)
                return;
            task.ReceivedFrameCallbacks++;
            if (task.FirstFrame < 0)
                task.FirstFrame = frame;
            task.LastFrame = Math.Max(task.LastFrame, frame);
            task.CompletedFrames = task.LastFrame - task.FirstFrame + 1;
            if (task.State == "stopping")
            {
                lastPendingFrameAt = EditorApplication.timeSinceStartup;
                quietUpdates = 0;
            }
        }

        private static void BeginStop(string message)
        {
            task.State = "stopping";
            task.Message = message;
            stopStarted = lastPendingFrameAt = EditorApplication.timeSinceStartup;
            quietUpdates = 0;
            // Keep ownership until pending frame callbacks have drained, preserving capture provenance.
            ProfilerDriver.enabled = false;
            Persist();
        }

        private static void CompleteCapture()
        {
            if (task.CompletedFrames == 0 || !HasFrames())
            {
                EndTask("failed", "No frames received. Check the target, Play Mode and CPU module.");
                return;
            }
            task.RetainedFirstFrame = Math.Max(task.FirstFrame, ProfilerDriver.firstFrameIndex);
            task.RetainedLastFrame = Math.Min(task.LastFrame, ProfilerDriver.lastFrameIndex);
            task.RetainedFrames = Math.Max(0, task.RetainedLastFrame - task.RetainedFirstFrame + 1);
            task.DroppedFrames = task.CompletedFrames - task.RetainedFrames;
            if (task.RetainedFrames == 0)
            {
                EndTask("failed", "No captured frames remain in the buffer.");
                return;
            }
            SaveBuffer(outputDataPath);
            task.DataPath = outputDataPath;
            source = outputDataPath;
            sourceProfileEditor = task.ProfileEditor;
            sourceDeepProfiling = task.DeepProfiling;
            sourceTarget = task.Target;
            sourceContext = task.Context;
            sourceFirstFrame = task.RetainedFirstFrame;
            sourceLastFrame = task.RetainedLastFrame;
            task.MetadataPath = SaveMetadata(outputDataPath);
            task.Message += task.DroppedFrames > 0 ? " Ring buffer dropped early frames; report uses retained frames." : string.Empty;
            task.State = task.TimedOut ? "timed_out" : "completed";
            RestoreRecording();
            File.WriteAllText(outputDataPath + ".json", JsonConvert.SerializeObject(task, jsonSettings));
            Persist();
        }

        private static void RestoreRecording()
        {
            if (!ownsRecording)
                return;
            // 收尾后用户重新录制或切换目标时，取消与重载也不得接管其录制及设置。
            if (ProfilerDriver.connectedProfiler != analysisTarget ||
                (task.State == "stopping" && ProfilerDriver.enabled))
            {
                ownsRecording = false;
                return;
            }
            ownsRecording = false;
            ProfilerDriver.enabled = false;
            if (ProfilerDriver.profileEditor == task.ProfileEditor)
                ProfilerDriver.profileEditor = previousProfileEditor;
            if (ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU))
                ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, previousCpuEnabled);
            if (ProfilerDriver.memoryRecordMode == captureMemoryRecordMode)
                ProfilerDriver.memoryRecordMode = previousMemoryRecordMode;
        }

        private static void EndTask(string state, string message)
        {
            RestoreRecording();
            if (task.Kind == "capture")
                OnBufferChanged();
            (analysis as IDisposable)?.Dispose();
            analysis = null;
            task.State = state;
            task.Message = message;
            Persist();
        }

        private static void OnBufferChanged()
        {
            if (changingBuffer)
                return;
            bufferChanged = true;
            source = "external-buffer";
            sourceDataHash = null;
            sourceDataLength = 0;
            sourceProfileEditor = null;
            sourceDeepProfiling = null;
            sourceMemoryRecordMode = null;
            sourceTarget = null;
            sourceContext = null;
            sourceFirstFrame = sourceLastFrame = -1;
            sourceCaptureId = null;
            sourceUnityVersion = null;
            sourceWarning = "Buffer changed outside the tool; capture settings and range are unknown.";
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (IsRunning && (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode))
                EndTask("interrupted", "Play Mode changed; capture context is no longer stable.");
        }

        private static void BeforeReload()
        {
            if (IsRunning)
                EndTask("interrupted", "Editor reload or shutdown interrupted the task.");
        }

        private static ProfilerTask NewTask(string kind, string state, string context)
        {
            return new ProfilerTask { Id = NewId(), Kind = kind, State = state, Context = context,
                Target = TargetName(), StartedUtc = DateTime.UtcNow.ToString("O"),
                ProfileEditor = ProfilerDriver.profileEditor, DeepProfiling = ProfilerDriver.deepProfiling };
        }

        private static string TargetName() => ProfilerDriver.GetConnectionIdentifier(ProfilerDriver.connectedProfiler);
        private static string NewId() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool HasFrames() => ProfilerDriver.firstFrameIndex >= 0 && ProfilerDriver.lastFrameIndex >= ProfilerDriver.firstFrameIndex;
        private static void Persist() => SessionState.SetString(TaskKey, JsonConvert.SerializeObject(task, jsonSettings));

        private static void RequireTask(string id)
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Performance tools are disabled in batchmode.");
            if (task == null || task.Id != id)
                throw new ArgumentException("Unknown taskId. Use sd_profiler_get_status; only the latest task is retained.");
        }

        private static void RequireIdle()
        {
            if (Application.isBatchMode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Performance tools are disabled during batchmode or Player builds.");
            if (IsRunning)
                throw new InvalidOperationException("A profiler task is active; query, stop or cancel it first.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for Editor compilation/import to finish.");
        }

        private static void RequireStopped()
        {
            RequireIdle();
            if (ProfilerDriver.enabled)
                throw new InvalidOperationException("Stop recording before saving, loading or analyzing data.");
        }

        private static string NewOutputPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                (!fileName.EndsWith(".data", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Use a plain file name ending in .data or .json, without directories.");
            Directory.CreateDirectory(OutputDirectory);
            var path = Path.Combine(OutputDirectory, fileName);
            if (File.Exists(path))
                throw new IOException("Output already exists: " + path);
            return path;
        }

        private static void SaveBuffer(string path)
        {
            if (!HasFrames())
                throw new InvalidOperationException("No Profiler frames are available.");
            if (!ProfilerDriver.SaveProfile(path))
                throw new IOException("Unity could not save profile: " + path);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new IOException("Unity did not produce a nonempty profile file.");
        }

        private static string DataHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        // 文件可能被外部覆盖；只有内容仍匹配才可代替真实停止缓冲的备份。
        private static bool CanRestoreSource()
        {
            if (string.IsNullOrEmpty(sourceDataHash))
                return false;
            try
            {
                var file = new FileInfo(source);
                return file.Exists && file.Length == sourceDataLength &&
                    string.Equals(DataHash(source), sourceDataHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string SaveMetadata(string path)
        {
            var first = ProfilerDriver.firstFrameIndex;
            var last = ProfilerDriver.lastFrameIndex;
            var metadata = new ProfilerDataMetadata
            {
                DataLength = new FileInfo(path).Length, DataSha256 = DataHash(path),
                CaptureId = sourceCaptureId, UnityVersion = sourceUnityVersion ?? "unknown",
                Target = sourceTarget ?? "unknown", Context = sourceContext,
                ProfileEditor = sourceProfileEditor, DeepProfiling = sourceDeepProfiling, MemoryRecordMode = sourceMemoryRecordMode,
                BufferFirstFrame = first, BufferLastFrame = last,
                FirstFrame = sourceFirstFrame >= 0 ? Math.Max(sourceFirstFrame, first) : first,
                LastFrame = sourceLastFrame >= 0 ? Math.Min(sourceLastFrame, last) : last
            };
            if (string.Equals(source, path, StringComparison.OrdinalIgnoreCase))
            {
                sourceDataHash = metadata.DataSha256;
                sourceDataLength = metadata.DataLength;
            }
            var metadataPath = path + ".metadata.json";
            File.WriteAllText(metadataPath, JsonConvert.SerializeObject(metadata, jsonSettings));
            return metadataPath;
        }

        private static void RestoreMetadata(string path)
        {
            sourceDataLength = new FileInfo(path).Length;
            sourceDataHash = DataHash(path);
            var metadataFile = new FileInfo(path + ".metadata.json");
            sourceWarning = "No verified metadata sidecar; imported capture settings are unknown.";
            if (!metadataFile.Exists)
                return;
            try
            {
                if (metadataFile.Length > 64 * 1024)
                    throw new InvalidDataException("Metadata exceeds 64 KiB.");
                var metadata = JsonConvert.DeserializeObject<ProfilerDataMetadata>(File.ReadAllText(metadataFile.FullName));
                if (metadata == null || metadata.SchemaVersion != 1 || metadata.DataLength != new FileInfo(path).Length ||
                    !string.Equals(metadata.DataSha256, sourceDataHash, StringComparison.OrdinalIgnoreCase) ||
                    metadata.BufferFirstFrame < 0 || metadata.FirstFrame < metadata.BufferFirstFrame ||
                    metadata.LastFrame < metadata.FirstFrame || metadata.LastFrame > metadata.BufferLastFrame ||
                    (long)metadata.BufferLastFrame - metadata.BufferFirstFrame !=
                    (long)ProfilerDriver.lastFrameIndex - ProfilerDriver.firstFrameIndex)
                    throw new InvalidDataException("Metadata does not match the saved data or loaded frame range.");
                var offset = ProfilerDriver.firstFrameIndex - metadata.BufferFirstFrame;
                sourceFirstFrame = metadata.FirstFrame + offset;
                sourceLastFrame = metadata.LastFrame + offset;
                sourceCaptureId = metadata.CaptureId;
                sourceUnityVersion = metadata.UnityVersion;
                sourceTarget = metadata.Target;
                sourceContext = metadata.Context;
                sourceProfileEditor = metadata.ProfileEditor;
                sourceDeepProfiling = metadata.DeepProfiling;
                sourceMemoryRecordMode = metadata.MemoryRecordMode;
                sourceWarning = null;
            }
            catch (Exception ex)
            {
                // Loading valid .data remains useful even when its optional sidecar cannot be trusted.
                sourceWarning = "Metadata ignored; capture settings remain unknown: " + ex.Message;
            }
        }

        private static ProfilerReport ReadReport(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                if (report == null || task == null || task.Kind != "analysis" || task.State != "completed")
                    throw new InvalidOperationException("No completed analysis in memory. Supply reportPath or run analysis.");
                return report;
            }
            var file = new FileInfo(Path.GetFullPath(path));
            if (!file.Exists || file.Length > 128 * 1024 * 1024 || file.Extension != ".json")
                throw new ArgumentException("Expected an existing report JSON no larger than 128 MB.");
            var value = JsonConvert.DeserializeObject<ProfilerReport>(File.ReadAllText(file.FullName));
            if (value == null || value.SchemaVersion != 1 || value.Frames == null || value.Frames.Count == 0 || value.Cpu == null || value.Gc == null)
                throw new ArgumentException("Not a supported performance report.");
            return value;
        }

        private static void ValidatePage(int start, int count)
        {
            if (start < 0 || count < 1 || count > 200)
                throw new ArgumentException("Use start >= 0 and count/topCount 1..200.");
        }
    }
}
