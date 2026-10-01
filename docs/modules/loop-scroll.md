# Loop Scroll 宿主桥接

## 职责与边界

SleepyLoopScroll 是宿主之外的独立 UPM 包，负责虚拟化、布局、对象池和场景扩展。SleepyDemos 的 Core.Runtime 仅适配普通 C# ItemView 与 View 订阅生命周期；Core.Editor 提供 MvcBind 接入和本地示例构建。包不引用 Core、Hotfix、MvcBind、UniTask 或资源框架。

## 入口与主链路

- `Core/Runtime/Components/LoopScrollItemViewBridge.cs`：每个物理 Cell 缓存一个 ItemView。
- `Core/Runtime/Components/LoopScrollRegisterExtend.cs`：提供 ItemViews() 和三种 `[ComponentAttribute]` 注册方法。
- `Core/Editor/MvcBind/LoopScrollExampleBuilder.cs`：创建宿主本地示例场景。
- `Hotfix/Demos/LoopScroll/LoopScrollMvcExample.cs`：本地 View/ItemView 接入示例。

调用方提交集合 → ItemViews().SetItems → 包 SetData → 缓存/更新 ItemView.Index → 业务绑定 → MvcBind CellBind。回收先由包失效上下文并取消 Token，再调用业务解绑和 CellUnbind。点击读取当前 context 的 Index/Key。

## 生命周期与维护

物理 Cell 的 ItemView 可复用，不能把索引当永久业务身份。异步资源绑定 CancellationToken，写入前检查 IsCurrent。隐藏列表仍可提交数据，启用后完整 reconcile。

注册通过 View.AddBinding 持有；重复注册同一委托不会重复触发，View 销毁解除。桥接销毁解除包事件并清理缓存。普通 ItemView 内部事件在 InitComponent 中只注册一次，回收时业务解绑负责释放其业务资源。

嵌套 ItemView 同样支持三个注册入口，其订阅随子列表组件销毁解除，并返回可显式 Dispose 的句柄；不改造 ItemView 基类的生命周期。一个物理 Cell 必须对应固定 ItemView 类型，不同类型使用不同 Prefab 类型池。

新增业务行为放 Hotfix。包公共 API 与算法在独立仓库维护；不得把宿主类型加入包。Core.Runtime/Core.Editor 单向引用包程序集，Core.Editor 不引用 Hotfix，示例构建按类型名称定位 Hotfix 示例入口。

## 验证

包测试独立留在包 Tests；宿主测试进入现有 Tests.EditMode/Tests.PlayMode。直接目标为 LoopScrollMvcGenerationTests、LoopScrollItemViewBridgeTests，另检查 TestAssemblyBoundaryTests。不运行无关全量测试。

详见 [接入步骤](../runbooks/use-loop-scroll.md)、[示例](../demos/loop_scroll/README.md) 与包 Documentation~/Validation.md。
