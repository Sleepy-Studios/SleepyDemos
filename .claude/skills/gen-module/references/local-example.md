## 完整本地示例

以下三件套可独立编译；按实际模块替换名称和业务内容，不额外生成网络骨架。

```csharp
using Core.Runtime;

namespace Hotfix
{
    /// 修改示例功能的启用状态。
    public sealed class ExampleSetEnabledAction : IAction
    {
        /// 本次请求的启用状态。
        public bool Enabled;

        /// 是否接受请求；未注册 Data 时为 false。
        public bool Result { get; internal set; }

        /// <summary>
        /// 请求修改启用状态；构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="enabled">true 启用，false 关闭。</param>
        public ExampleSetEnabledAction(bool enabled)
        {
            Enabled = enabled;
        }
    }
}
```

```csharp
using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix
{
    /// 示例功能的状态与读取入口。
    public sealed class ExampleData : IData
    {
        public List<IHandler> Handlers { get; } = new List<IHandler>
        {
            new ExampleHandler()
        };

        /// 功能是否启用；初始为 false。
        public bool Enabled { get; internal set; }

        /// 恢复初始状态。
        public void ClearData()
        {
            Enabled = false;
        }
    }
}
```

```csharp
using Core.Runtime;

namespace Hotfix
{
    /// 接受启用请求并发布状态。
    public sealed class ExampleHandler : HandlerBase<ExampleSetEnabledAction, ExampleData>
    {
        /// <summary>
        /// 同步处理请求；订阅回调执行前先写回结果。
        /// </summary>
        /// <param name="action">当前启用请求。</param>
        protected override void Reduce(ExampleSetEnabledAction action)
        {
            State.Enabled = action.Enabled;
            action.Result = true;
            ApplyState();
        }
    }
}
```
