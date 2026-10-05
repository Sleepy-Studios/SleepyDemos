using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;

namespace Hotfix.Dlss
{
    public sealed class DlssData : IData
    {
        public List<IHandler> Handlers { get; }
        internal DlssHandler Handler { get; }
        public bool IsExiting { get; internal set; }
        public bool SettingsOpen { get; internal set; }
        public bool Sprint { get; internal set; }
        public InputDeviceKind DeviceKind { get; internal set; }
        internal DlssData(DlssDemoController scene)
        { Handler=new DlssHandler(scene); Handlers=new() { Handler }; }
        public void ClearData() { IsExiting=SettingsOpen=Sprint=false; }
    }
}
