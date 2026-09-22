using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Runtime.Rendering.Streamline
{
    /// Editor 内的显式 DLSS 验证会话；不代表 Player 呈现链路已经接入。
    public static class StreamlineDlssValidation
    {
        private const string LibraryName = "GfxPluginSleepyStreamline";

        /// <summary>将一帧 DLSS 处理录入命令缓冲；调用者负责提交以及 GPU 完成前的资源寿命。</summary>
        /// <param name="commands">即将提交到 Unity 渲染线程的命令缓冲。</param>
        /// <param name="frame">同一相机、同一帧的纹理与不含 jitter 的相机参数。</param>
        /// <param name="request">用于匹配结果的请求编号。</param>
        /// <param name="reason">平台、ABI 或原生加载错误；SDK 执行错误通过报告返回。</param>
        /// <returns>是否录入请求；不表示 SDK 执行或 GPU 工作完成。</returns>
        public static bool TryEnqueue(CommandBuffer commands, StreamlineDlssFrame frame, out ulong request, out string reason)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            request = 0;
            reason = null;
            if (Application.platform != RuntimePlatform.WindowsEditor || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
            {
                reason = "当前图像验证路径需要 Windows Editor + D3D12。";
                return false;
            }
            try
            {
                if (SleepyStreamlineGetFrameSize() != Marshal.SizeOf<StreamlineDlssFrame>())
                {
                    reason = "DLSS 帧 ABI 不匹配，请重新构建并重启 Editor 加载桥接。";
                    return false;
                }
                IntPtr callback = SleepyStreamlineGetDlssEvent();
                request = SleepyStreamlineQueueFrame(ref frame);
                if (request == 0)
                {
                    reason = "原生待处理帧队列已满。";
                    return false;
                }
                commands.IssuePluginEventAndData(callback, SleepyStreamlineGetProbeEventId() + 1, new IntPtr(unchecked((long)request)));
                return true;
            }
            catch (Exception exception) when (IsLoadingFailure(exception))
            {
                reason = "原生图像接口未加载，请重启 Editor：" + exception.Message;
                return false;
            }
            catch
            {
                if (request != 0) SleepyStreamlineCancelFrame(request);
                throw;
            }
        }

        /// <summary>读取最近一次图像请求报告；应检查其中 requestId 与本次请求一致。</summary>
        /// <param name="json">包含 SDK 结果、执行阶段及输入输出尺寸的 JSON。</param>
        /// <param name="reason">原生加载或报告容量错误。</param>
        /// <returns>是否取得报告；GPU 完成情况仍应通过 fence 确认。</returns>
        public static bool TryGetReport(out string json, out string reason)
        {
            json = null;
            reason = null;
            try
            {
                int capacity = SleepyStreamlineCopyFrameReport(null, 0);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (capacity <= 0 || capacity > 1024 * 1024) break;
                    byte[] buffer = new byte[capacity];
                    int required = SleepyStreamlineCopyFrameReport(buffer, capacity);
                    if (required > 0 && required <= capacity)
                    {
                        json = Encoding.UTF8.GetString(buffer, 0, required - 1);
                        return true;
                    }
                    capacity = required;
                }
                reason = "原生图像报告尚不可读。";
                return false;
            }
            catch (Exception exception) when (IsLoadingFailure(exception))
            {
                reason = exception.Message;
                return false;
            }
        }

        /// <summary>在所有图像请求的 GPU fence 完成后，录入会话清理事件。</summary>
        /// <param name="commands">用于提交清理的命令缓冲；资源应保持到清理后的 fence 完成。</param>
        /// <param name="reason">原生加载错误。</param>
        /// <returns>是否录入清理事件。</returns>
        public static bool TryEndSession(CommandBuffer commands, out string reason)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            reason = null;
            try
            {
                commands.IssuePluginEventAndData(SleepyStreamlineGetDlssEvent(), SleepyStreamlineGetProbeEventId() + 2, IntPtr.Zero);
                return true;
            }
            catch (Exception exception) when (IsLoadingFailure(exception))
            {
                reason = exception.Message;
                return false;
            }
        }

        /// <summary>在 GPU 完成后释放旧 viewport，之后才能改变模式或输出尺寸。</summary>
        /// <param name="commands">清理命令缓冲；提交后应等待 fence 再重建资源。</param>
        /// <param name="viewport">需要释放的相机历史编号。</param>
        /// <param name="reason">原生接口不可用的原因。</param>
        /// <returns>是否录入；通过 ViewportReleased 报告检查 SDK 结果。</returns>
        public static bool TryReleaseViewport(CommandBuffer commands, uint viewport, out string reason)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            reason = null;
            try
            {
                commands.IssuePluginEventAndData(SleepyStreamlineGetDlssEvent(), SleepyStreamlineGetReleaseViewportEventId(), new IntPtr((long)viewport + 1));
                return true;
            }
            catch (Exception exception) when (IsLoadingFailure(exception))
            {
                reason = exception.Message;
                return false;
            }
        }

        private static bool IsLoadingFailure(Exception exception)
        {
            return exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is BadImageFormatException;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern int SleepyStreamlineGetFrameSize();
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SleepyStreamlineGetDlssEvent();
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern int SleepyStreamlineGetProbeEventId();
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern int SleepyStreamlineGetReleaseViewportEventId();
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong SleepyStreamlineQueueFrame(ref StreamlineDlssFrame frame);
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern void SleepyStreamlineCancelFrame(ulong request);
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] private static extern int SleepyStreamlineCopyFrameReport([Out] byte[] destination, int capacity);
    }
}
