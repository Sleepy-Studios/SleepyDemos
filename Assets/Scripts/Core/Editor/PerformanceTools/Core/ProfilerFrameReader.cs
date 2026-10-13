using System;
using System.Collections.Generic;
using UnityEditor.Profiling;

namespace Core.Editor.PerformanceTools
{
    /// CPU 与 GC 共用的原始帧解码，不维护第二个 GC 分析器。
    public static class ProfilerFrameReader
    {
        /// <summary>读取 GC.Alloc 数值元数据；大小不可用时返回零并由分析器标记未知。</summary>
        /// <param name="raw">有效的原始帧视图。</param>
        /// <param name="sampleIndex">GC.Alloc 样本索引。</param>
        public static long GetGcAllocBytes(RawFrameDataView raw, int sampleIndex)
        {
            var metadataCount = raw.GetSampleMetadataCount(sampleIndex);
            long bestValue = 0;
            for (var i = 0; i < metadataCount; i++)
            {
                // 优先读取 64 位分配量，无法读取时尝试 32 位；未知大小由报告单列。
                TryUseMetadataValue(raw, sampleIndex, i, ref bestValue);
            }
            return bestValue;
        }

        private static void TryUseMetadataValue(RawFrameDataView raw, int sampleIndex, int metadataIndex, ref long bestValue)
        {
            try
            {
                bestValue = Math.Max(bestValue, raw.GetSampleMetadataAsLong(sampleIndex, metadataIndex));
                return;
            }
            catch
            {
            }
            try
            {
                bestValue = Math.Max(bestValue, raw.GetSampleMetadataAsInt(sampleIndex, metadataIndex));
            }
            catch
            {
            }
        }

        /// <summary>从 Total 扣除直接子节点耗时，不重复扣除后代。</summary>
        /// <param name="raw">有效的原始帧视图。</param>
        /// <param name="sample">原始样本索引。</param>
        public static double GetSelfTimeMs(RawFrameDataView raw, int sample)
        {
            double childrenMs = 0;
            var child = sample + 1;
            var count = raw.GetSampleChildrenCount(sample);
            for (var i = 0; i < count && child < raw.sampleCount; i++)
            {
                childrenMs += raw.GetSampleTimeMs(child);
                child += raw.GetSampleChildrenCountRecursive(child) + 1;
            }
            return Math.Max(0, raw.GetSampleTimeMs(sample) - childrenMs);
        }

        /// <summary>读取采样已有的地址与可解析符号；没有捕获栈时返回 null。</summary>
        /// <param name="raw">有效的原始帧视图。</param>
        /// <param name="sample">原始样本索引。</param>
        /// <param name="symbols">仅当前停止缓冲使用的符号缓存；null 时直接解析。</param>
        public static List<string> ReadCallstack(RawFrameDataView raw, int sample, Dictionary<ulong, string> symbols = null)
        {
            var addresses = new List<ulong>();
            raw.GetSampleCallstack(sample, addresses);
            if (addresses.Count == 0)
                return null;
            var result = new List<string>();
            foreach (var address in addresses)
            {
                if (symbols == null || !symbols.TryGetValue(address, out var text))
                {
                    var method = raw.ResolveMethodInfo(address);
                    text = "0x" + address.ToString("x");
                    if (!string.IsNullOrEmpty(method.methodName))
                        text += " " + method.methodName;
                    if (!string.IsNullOrEmpty(method.sourceFileName))
                        text += " (" + method.sourceFileName + ":" + method.sourceFileLine + ")";
                    if (symbols != null)
                    {
                        if (symbols.Count >= 100000)
                            throw new InvalidOperationException("Too many callstack symbols; narrow the frame range.");
                        symbols.Add(address, text);
                    }
                }
                result.Add(text);
                if (result.Count == 64)
                    break;
            }
            return result;
        }
    }
}
