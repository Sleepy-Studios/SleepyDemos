using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using UnityEngine;

namespace Hotfix
{
    /// 全局画面设置的偏好与实际生效快照，原生资源由Streamline服务持有。
    public sealed class GraphicsSettingsData : IData
    {
        public List<IHandler> Handlers { get; } = new()
        {
            new GraphicsSettingsHandler()
        };

        /// 用户请求的质量模式；null 为关闭。
        public StreamlineDlssMode? RequestedMode { get; internal set; }

        /// 原生服务真实生效模式，可能与请求模式不同。
        public StreamlineDlssMode? EffectiveMode { get; internal set; }

        /// 服务是否正在切换 GPU 设置。
        public bool IsBusy { get; internal set; }

        /// 当前真实输入渲染尺寸。
        public Vector2Int InputSize { get; internal set; }

        /// 当前真实输出尺寸。
        public Vector2Int OutputSize { get; internal set; }

        /// 原生服务提供的生效或失败说明。
        public string Status { get; internal set; }

        /// 全局 UserData 中的同一份硬件信息。
        public UserData.HardwareProfile Hardware => GlobalData.Get<UserData>()?.Hardware;

        /// 重置业务状态，使上一轮请求结果失效。
        public void ClearData()
        {
            RequestedMode = EffectiveMode = null;
            IsBusy = false;
            InputSize = OutputSize = default;
            Status = null;
        }
    }
}
