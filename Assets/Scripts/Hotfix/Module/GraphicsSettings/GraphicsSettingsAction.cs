using Core.Runtime;
using Core.Runtime.Rendering.Streamline;

namespace Hotfix
{
    /// 请求公共渲染服务切换质量模式。
    public sealed class GraphicsSettingsSetModeAction : IAction
    {
        public StreamlineDlssMode? Mode;

        /// <summary>
        /// 请求公共渲染服务切换质量模式。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="mode">目标 SDK 质量档；null 表示关闭 DLSS。</param>
        public GraphicsSettingsSetModeAction(StreamlineDlssMode? mode)
        {
            Mode = mode;
        }
    }
}
