using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using UnityEngine;

namespace Hotfix
{
    /// 全局画面设置的偏好与实际生效快照，原生资源由Streamline服务持有。
    public sealed class GraphicsSettingsData : IData
    {
        public List<IHandler> Handlers { get; } = new() { new GraphicsSettingsHandler() };
        public StreamlineDlssMode? RequestedMode { get; internal set; }
        public StreamlineDlssMode? EffectiveMode { get; internal set; }
        public bool IsBusy { get; internal set; }
        public Vector2Int InputSize { get; internal set; }
        public Vector2Int OutputSize { get; internal set; }
        public string Status { get; internal set; }
        public UserData.HardwareProfile Hardware => GlobalData.Get<UserData>()?.Hardware;
        public void ClearData() { RequestedMode=EffectiveMode=null; IsBusy=false; InputSize=OutputSize=default; Status=null; }
    }
}
