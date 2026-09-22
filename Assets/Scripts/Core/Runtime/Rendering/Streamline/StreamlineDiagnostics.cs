using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Runtime.Rendering.Streamline
{
    /// 原生能力探测入口；能力查询不表示对应效果已接入或启用。
    public static class StreamlineDiagnostics
    {
        private const string LibraryName = "GfxPluginSleepyStreamline";

        /// <summary>
        /// 将原生探测加入命令缓冲，在渲染线程读取设备和 SDK 能力。
        /// </summary>
        /// <param name="commands">由调用者提交和释放的命令缓冲。</param>
        /// <param name="reason">无法提交时返回原因；成功仅表示事件已录入。</param>
        /// <returns>是否已将探测事件录入命令缓冲。</returns>
        public static bool TryEnqueueProbe(CommandBuffer commands, out string reason)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            reason = GetPlatformReason();
            if (reason != null) return false;
            try
            {
                IntPtr callback = SleepyStreamlineGetProbeEvent();
                if (callback == IntPtr.Zero)
                {
                    reason = "原生桥接未提供渲染事件。";
                    return false;
                }
                commands.IssuePluginEvent(callback, SleepyStreamlineGetProbeEventId());
                return true;
            }
            catch (Exception exception) when (IsNativeLoadingFailure(exception))
            {
                reason = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 读取最近一次完成的原生探测快照；不会触发 SDK 初始化。
        /// </summary>
        /// <param name="json">探测 JSON，失败时为空。</param>
        /// <param name="reason">平台、加载或 ABI 错误。</param>
        /// <returns>是否成功取得快照；NotProbed 状态仍表示尚未执行探测。</returns>
        public static bool TryGetReport(out string json, out string reason)
        {
            return TryReadReport(SleepyStreamlineCopyReport, out json, out reason);
        }

        /// <summary>读取最近一次原生纹理访问结果。</summary>
        /// <param name="json">纹理尺寸、格式与命令缓冲可用性。</param>
        /// <param name="reason">加载或读取失败原因。</param>
        /// <returns>是否取得报告。</returns>
        public static bool TryGetResourceReport(out string json, out string reason)
        {
            return TryReadReport(SleepyStreamlineCopyResourceReport, out json, out reason);
        }

        /// <summary>在渲染线程通过 Unity 原生接口访问纹理并记录读取状态转换。</summary>
        /// <param name="commands">需要提交的命令缓冲。</param>
        /// <param name="texture">必须存活到同一命令流的回读完成。</param>
        /// <param name="reason">平台或接口不可用原因。</param>
        /// <returns>是否录入探测，实际结果需读取资源报告。</returns>
        public static bool TryEnqueueResourceProbe(CommandBuffer commands, Texture texture, out string reason)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            reason = GetPlatformReason();
            if (reason != null) return false;
            try
            {
                commands.IssuePluginEventAndData(SleepyStreamlineGetResourceProbeEvent(), SleepyStreamlineGetResourceProbeEventId(), texture.GetNativeTexturePtr());
                return true;
            }
            catch (Exception exception) when (IsNativeLoadingFailure(exception))
            {
                reason = exception.Message;
                return false;
            }
        }

        private static bool TryReadReport(Func<byte[], int, int> read, out string json, out string reason)
        {
            json = null;
            reason = GetPlatformReason();
            if (reason != null) return false;
            try
            {
                // 原生端两次调用之间可能完成新探测，因此按返回的所需容量重试。
                int capacity = read(null, 0);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (capacity <= 0 || capacity > 1024 * 1024)
                    {
                        reason = "原生报告容量无效，检查桥接 ABI 版本。";
                        return false;
                    }
                    byte[] buffer = new byte[capacity];
                    int required = read(buffer, buffer.Length);
                    if (required > 0 && required <= buffer.Length)
                    {
                        json = Encoding.UTF8.GetString(buffer, 0, required - 1);
                        return true;
                    }
                    capacity = required;
                }
                reason = "原生报告正在更新，请重试。";
                return false;
            }
            catch (Exception exception) when (IsNativeLoadingFailure(exception))
            {
                reason = exception.Message;
                return false;
            }
        }

        private static string GetPlatformReason()
        {
            if ((Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                || IntPtr.Size != 8)
                return "Streamline 桥接仅支持 Windows x64。";
            return null;
        }

        private static bool IsNativeLoadingFailure(Exception exception)
        {
            return exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is BadImageFormatException;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SleepyStreamlineGetProbeEvent();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SleepyStreamlineGetProbeEventId();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SleepyStreamlineCopyReport([Out] byte[] destination, int capacity);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SleepyStreamlineGetResourceProbeEvent();
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern int SleepyStreamlineGetResourceProbeEventId();
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern int SleepyStreamlineCopyResourceReport([Out] byte[] destination, int capacity);
    }
}
