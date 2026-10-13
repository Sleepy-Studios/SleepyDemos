using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace Core.Editor.PerformanceTools
{
    /// Incremental CPU and GC aggregation over a stable, stopped buffer.
    public static class ProfilerAnalysis
    {
        // Bound aggregation growth without silently dropping evidence.
        private const int MaxHotspots = 100000;

        /// <summary>在停止且稳定的缓冲中分片读取原始帧，CPU 和 GC 共用一次扫描。</summary>
        /// <param name="report">有效报告对象，读取期间缓冲必须保持稳定。</param>
        /// <param name="onFrame">每完成一帧的进度回调。</param>
        public static IEnumerator Read(ProfilerReport report, Action<int> onFrame)
        {
            var cpu = new Dictionary<string, ProfilerHotspot>(StringComparer.Ordinal);
            var gc = new Dictionary<string, ProfilerHotspot>(StringComparer.Ordinal);
            var symbols = new Dictionary<ulong, string>();
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var scriptsPath = (Application.dataPath + "/Scripts/").Replace('\\', '/');
            var projectAssemblies = CompilationPipeline.GetAssemblies()
                .Where(assembly => assembly.sourceFiles.Any(file => Path.GetFullPath(
                    Path.IsPathRooted(file) ? file : Path.Combine(projectRoot, file)).Replace('\\', '/')
                    .StartsWith(scriptsPath, StringComparison.OrdinalIgnoreCase)))
                .Select(assembly => assembly.name + ".dll!").ToArray();
            var clock = Stopwatch.StartNew();
            for (var frame = report.FirstFrame; frame <= report.LastFrame; frame++)
            {
                var summary = new ProfilerFrame { Index = frame };
                var foundFrame = false;
                for (var thread = 0; ; thread++)
                {
                    using (var raw = ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!raw.valid)
                            break;
                        if (!foundFrame)
                        {
                            summary.TimeMs = raw.frameTimeMs;
                            foundFrame = true;
                        }
                        var threadName = raw.threadGroupName + "/" + raw.threadName;
                        if (!string.IsNullOrEmpty(report.ThreadFilter) &&
                            threadName.IndexOf(report.ThreadFilter, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        summary.Threads++;
                        var ancestors = new List<string>();
                        var ends = new List<int>();
                        for (var sample = 0; sample < raw.sampleCount; sample++)
                        {
                            while (ends.Count > 0 && sample >= ends[ends.Count - 1])
                            {
                                ends.RemoveAt(ends.Count - 1);
                                ancestors.RemoveAt(ancestors.Count - 1);
                            }
                            var name = raw.GetSampleName(sample) ?? "<unnamed>";
                            var item = Get(cpu, threadName + "\n" + name, threadName, name, null);
                            if (item.Calls == 0)
                                item.SamplePath = string.Join(" > ", ancestors.Concat(new[] { name }));
                            item.Calls++;
                            item.TotalMs += raw.GetSampleTimeMs(sample);
                            item.SelfMs += ProfilerFrameReader.GetSelfTimeMs(raw, sample);
                            if (name == "GC.Alloc")
                            {
                                var bytes = ProfilerFrameReader.GetGcAllocBytes(raw, sample);
                                var path = string.Join(" > ", ancestors);
                                var parent = ancestors.Count == 0 ? "<unknown>" : ancestors[ancestors.Count - 1];
                                var stack = !report.MemoryRecordMode.HasValue ||
                                    (report.MemoryRecordMode.Value & (int)ProfilerMemoryRecordMode.GCAlloc) != 0
                                    ? ProfilerFrameReader.ReadCallstack(raw, sample, symbols) : null;
                                string site = stack == null ? null : string.Join(" > ", stack.Where(value => value.Contains(".dll!"))
                                    .Select(value => value.Substring(value.IndexOf(' ') + 1)));
                                if (stack != null && string.IsNullOrEmpty(site))
                                    site = string.Join(" > ", stack);
                                var alloc = Get(gc, threadName + "\n" + path + "\n" + site, threadName, parent, path);
                                alloc.AllocationSite = site;
                                alloc.Calls++;
                                alloc.GcBytes += bytes;
                                if (alloc.ProjectMethod == null)
                                {
                                    var method = stack?.Select(value => value.StartsWith("0x", StringComparison.Ordinal)
                                            ? value.Substring(value.IndexOf(' ') + 1) : value)
                                        .FirstOrDefault(value => projectAssemblies.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal)));
                                    alloc.ProjectMethod = method ?? ancestors.LastOrDefault(value =>
                                        projectAssemblies.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal)));
                                }
                                if (alloc.Callstack == null)
                                    alloc.Callstack = stack;
                                summary.GcBytes += bytes;
                                summary.GcEvents++;
                                if (bytes == 0)
                                {
                                    alloc.UnknownSizeEvents++;
                                    summary.GcUnknownSizeEvents++;
                                }
                            }
                            var descendants = raw.GetSampleChildrenCountRecursive(sample);
                            if (descendants > 0)
                            {
                                ancestors.Add(name);
                                ends.Add(sample + descendants + 1);
                            }
                            if (sample % 256 == 0 && clock.ElapsedMilliseconds >= 8)
                            {
                                yield return null;
                                clock.Restart();
                            }
                        }
                    }
                    if (clock.ElapsedMilliseconds >= 8)
                    {
                        yield return null;
                        clock.Restart();
                    }
                }
                if (!foundFrame)
                    throw new InvalidOperationException("Frame is unavailable: " + frame);
                report.Frames.Add(summary);
                onFrame(report.Frames.Count);
                if (clock.ElapsedMilliseconds >= 8)
                {
                    yield return null;
                    clock.Restart();
                }
            }
            report.Cpu = cpu.Values.OrderByDescending(x => x.SelfMs).ToList();
            report.Gc = gc.Values.OrderByDescending(x => x.GcBytes).ToList();
            if (report.Frames.All(x => x.Threads == 0))
                report.Warnings.Add("No thread matched the filter.");
            if (report.Frames.Any(x => x.GcUnknownSizeEvents > 0))
                report.Warnings.Add("Some GC.Alloc sizes are unavailable; byte totals are lower bounds.");
            if (report.Gc.Any(value => value.AllocationSite?.StartsWith("0x", StringComparison.Ordinal) == true))
                report.Warnings.Add("Unresolved GC allocation sites use captured addresses; addresses may differ across Editor sessions.");
            report.Warnings.Add("CPU includes wait/idle markers. Total time overlaps parent/child samples and threads; do not sum it as frame time.");
            report.Warnings.Add("Threads with identical group/name are aggregated. CPU samplePath is one observed example; use frame samples for the exact hierarchy.");
            report.Warnings.Add("Sample paths are Profiler marker ancestry, not complete managed stacks. Callstacks contain captured addresses and available symbols, or null when unavailable.");
        }

        private static ProfilerHotspot Get(Dictionary<string, ProfilerHotspot> entries, string key,
            string thread, string marker, string path)
        {
            if (entries.TryGetValue(key, out var result))
                return result;
            if (entries.Count >= MaxHotspots)
                throw new InvalidOperationException("Too many unique hotspots; narrow frame range or thread filter.");
            result = new ProfilerHotspot { Thread = thread, Marker = marker, SamplePath = path };
            entries.Add(key, result);
            return result;
        }

        /// <summary>生成报告的帧分位与分配摘要，不把线程时间相加。</summary>
        /// <param name="report">有效报告对象，读取期间缓冲必须保持稳定。</param>
        public static object Summary(ProfilerReport report)
        {
            var times = report.Frames.Select(x => x.TimeMs).OrderBy(x => x).ToArray();
            var count = times.Length;
            return new
            {
                report.Id, frameCount = count,
                meanFrameMs = count == 0 ? 0 : times.Average(),
                p50FrameMs = count == 0 ? 0 : times[(int)Math.Ceiling(count * 0.50) - 1],
                p95FrameMs = count == 0 ? 0 : times[(int)Math.Ceiling(count * 0.95) - 1],
                p99FrameMs = count == 0 ? 0 : times[(int)Math.Ceiling(count * 0.99) - 1],
                maxFrameMs = count == 0 ? 0 : times[count - 1],
                over16MsFrames = times.Count(value => value > 1000.0 / 60),
                over33MsFrames = times.Count(value => value > 1000.0 / 30),
                over50MsFrames = times.Count(value => value > 50),
                gcBytesPerFrame = count == 0 ? 0 : report.Frames.Sum(x => (double)x.GcBytes) / count,
                gcEventsPerFrame = count == 0 ? 0 : report.Frames.Sum(x => (double)x.GcEvents) / count,
                unknownSizeEvents = report.Frames.Sum(x => x.GcUnknownSizeEvents),
                cpuHotspots = report.Cpu.Count, gcHotspots = report.Gc.Count,
                report.Warnings
            };
        }

        /// <summary>按每帧指标比较两份完成报告；条件不同会返回警告。</summary>
        /// <param name="baseline">优化前的有效完成报告。</param>
        /// <param name="current">优化后的有效完成报告。</param>
        /// <param name="topCount">热点数量上限，1 到 200。</param>
        public static object Compare(ProfilerReport baseline, ProfilerReport current, int topCount)
        {
            if (baseline.Frames.Count == 0 || current.Frames.Count == 0)
                throw new ArgumentException("Both reports must contain frames.");
            var warnings = new List<string>();
            if (baseline.UnityVersion != current.UnityVersion || baseline.Target != current.Target ||
                baseline.ThreadFilter != current.ThreadFilter || baseline.Context != current.Context ||
                baseline.ProfileEditor != current.ProfileEditor || baseline.DeepProfiling != current.DeepProfiling
                || baseline.MemoryRecordMode != current.MemoryRecordMode)
                warnings.Add("Capture context, target or settings differ. Deltas are descriptive, not a controlled regression verdict.");
            if (baseline.DeepProfiling == null || current.DeepProfiling == null ||
                baseline.ProfileEditor == null || current.ProfileEditor == null ||
                baseline.MemoryRecordMode == null || current.MemoryRecordMode == null)
                warnings.Add("Capture settings are unknown for imported data.");
            Func<ProfilerReport, Dictionary<string, ProfilerHotspot>> index = r =>
                r.Cpu.ToDictionary(x => x.Thread + "\n" + x.Marker, StringComparer.Ordinal);
            var before = index(baseline);
            var after = index(current);
            var changes = before.Keys.Union(after.Keys).Select(key =>
            {
                before.TryGetValue(key, out var b);
                after.TryGetValue(key, out var a);
                var oldMs = (b?.SelfMs ?? 0) / baseline.Frames.Count;
                var newMs = (a?.SelfMs ?? 0) / current.Frames.Count;
                return new { (a ?? b).Thread, (a ?? b).Marker, baselineSelfMsPerFrame = oldMs,
                    currentSelfMsPerFrame = newMs, deltaSelfMsPerFrame = newMs - oldMs };
            }).OrderByDescending(x => Math.Abs(x.deltaSelfMsPerFrame)).Take(topCount).ToArray();
            var beforeGc = baseline.Gc.ToDictionary(x => x.Thread + "\n" + x.SamplePath + "\n" + x.AllocationSite, StringComparer.Ordinal);
            var afterGc = current.Gc.ToDictionary(x => x.Thread + "\n" + x.SamplePath + "\n" + x.AllocationSite, StringComparer.Ordinal);
            var gcChanges = beforeGc.Keys.Union(afterGc.Keys).Select(key =>
            {
                beforeGc.TryGetValue(key, out var b);
                afterGc.TryGetValue(key, out var a);
                var oldBytes = (double)(b?.GcBytes ?? 0) / baseline.Frames.Count;
                var newBytes = (double)(a?.GcBytes ?? 0) / current.Frames.Count;
                return new { (a ?? b).Thread, (a ?? b).SamplePath, (a ?? b).AllocationSite, baselineBytesPerFrame = oldBytes,
                    currentBytesPerFrame = newBytes, deltaBytesPerFrame = newBytes - oldBytes };
            }).OrderByDescending(x => Math.Abs(x.deltaBytesPerFrame)).Take(topCount).ToArray();
            return new { baseline = Summary(baseline), current = Summary(current), cpuChanges = changes, gcChanges, warnings };
        }
    }
}
