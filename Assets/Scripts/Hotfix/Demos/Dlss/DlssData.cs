using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;

namespace Hotfix.Dlss
{
    /// 当前场景的业务状态与查询入口。
    public sealed class DlssData : IData
    {
        public List<IHandler> Handlers { get; }

        internal DlssHandler Handler { get; }

        /// 是否正在返回大厅。
        public bool IsExiting { get; internal set; }

        /// 公共设置是否正在阻断观察输入。
        public bool SettingsOpen { get; internal set; }

        /// 触屏加速是否保持；关闭控件时释放。
        public bool Sprint { get; internal set; }

        /// 公共输入服务当前识别的设备类型。
        public InputDeviceKind DeviceKind { get; internal set; }

        internal DlssData(DlssDemoController scene)
        {
            Handler = new DlssHandler(scene);
            Handlers = new()
            {
                Handler
            };
        }

        /// 重置业务状态，使上一轮请求结果失效。
        public void ClearData()
        {
            IsExiting = SettingsOpen = Sprint = false;
        }
    }
}
