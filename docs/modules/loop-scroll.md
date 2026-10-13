# Loop Scroll 宿主桥接

## 职责与边界

SleepyLoopScroll 是宿主之外的独立 UPM 包，负责虚拟化、布局、对象池和场景扩展。SleepyDemos 的 Core.Runtime 仅适配普通 C# ItemView 与 View 订阅生命周期；Core.Editor 提供 UIBind 接入。包不引用 Core、Hotfix、UIBind、UniTask 或资源框架。

## 入口与主链路

- `Core/Runtime/Components/LoopScrollItemViewBridge.cs`：每个物理 Cell 缓存一个 ItemView。
- `Core/Runtime/Components/LoopScrollRegisterExtend.cs`：提供 ItemViews() 和三种 `[ComponentAttribute]` 注册方法。
- `Core/Runtime/Components/LoopScrollMenuNavigation.cs` / `LoopScrollMenuButton.cs`：公共虚拟列表菜单焦点、Grid 方向导航与按 Key 定位，不承载业务图片、文字或页面输入作用域。
- `LoopScrollInputHost`：已保存示例场景的输入桥接，动态页面建立后接入公共状态和菜单作用域，不修改包源码。
- `Hotfix/Module/LoopScroll/Examples/LoopScrollUIBindExample.cs`：本地 View/ItemView 接入示例。

先 RegisterLoopScrollRect<TView> 配置工厂与绑定，再注册 Click/ItemHide，最后 SetTotalCount 提交真实集合。ListDataSource 进入包的统一 Commit/Reconcile，桥接更新 ItemView.Index 和 context，触发唯一一组 CellBound/CellUnbound/CellClicked 事件。解绑前旧 context 已失效并取消 Token，点击读取当前身份。

UIBind 只发现 RectData/Click/ItemHide 三种普通回调。绑定与点击为 `(ItemView,index)`，解绑为 `(ItemView)`；普通业务 Item 不保存 `CellBindContext`。手写页面可用 `RegisterLoopScrollRect<TItem>(list, Action<TItem,int>)` 同时配置工厂，Click 与 ItemHide 也支持 typed 回调。生成的基础 ItemView 回调不推断工厂，提交前显式 `Configure<TItem>`。生命周期仍由 View.AddBinding 收口，重复注册同一委托只触发一次，typed 包装在桥接内复用并随订阅释放。

带 `CellBindContext` 的高级重载继续服务需要身份 Token 的异步绑定和多类型数据源，取消旧 Token、点击当前身份的保护仍在 Core/包内部。高级重载不参与 UIBind 普通回调发现，避免同一回调名出现两套签名。不要为同步 `SetData` 或按钮点击要求业务项保存物理 Cell 上下文。

公共菜单导航读取包的 `LayoutLaneCount`，不从当前实例数量推导 Grid 列数。菜单按钮在指针按下时保存绑定身份，抬手前换绑则取消点击；手柄提交只接受重新选中的有效身份。业务入口不需要再维护这些保护。

## 生命周期与维护

物理 Cell 的 ItemView 可复用，不能把索引当永久业务身份。普通图片使用已有 UIImageLoader，由 Item 的数据刷新与回收维护，不新建另一套 Loader。需要跨绑定完成的特殊异步任务才使用高级 Context 回调，绑定 CancellationToken 并在写入前检查 IsCurrent。隐藏列表仍可提交数据，启用后完整 reconcile。桥接保持创建时 `Init/InitComponent`，随后包 CellBound 才触发业务数据回调的既有时序。

注册通过 View.AddBinding 持有；重复注册同一委托不会重复触发，View 销毁解除。桥接销毁解除包事件并清理缓存。普通 ItemView 内部事件在 InitComponent 中只注册一次，回收时业务解绑负责释放其业务资源。

嵌套 ItemView 同样支持三个注册入口，返回 IDisposable，宿主在嵌套 ItemView 回收时释放，列表组件销毁也会清除事件。一个物理 Cell 必须对应固定 ItemView 类型，不同类型使用不同 Prefab 类型池。普通 Item 自己声明具体 SetData，桥接先初始化控件再回调数据；复用不重复初始化。Item 不缓存初始化前的数据，不使用父类自动 RefreshUI 模板。具体初始化契约见 [Core UI 运行时](ui-runtime.md#view-生命周期)。

新增业务行为放 Hotfix。包公共 API 与算法在独立仓库维护；不得把宿主类型加入包。Core.Runtime/Core.Editor 单向引用包程序集，Core.Editor 不引用 Hotfix。Hotfix 的 Loop Scroll 宿主示例引用已导入的示例共享程序集，复用导航、字体和翻译；Core 和包运行时不依赖示例程序集。

## 定位结果

完成/取消与像素偏移由独立包的 ScrollToCell/ScrollToOffset 统一维护，桥接无需包装请求。Hotfix 的 UIBind 页面直接用 ScrollResult 更新状态，提供 ±60 偏移和 CancelAnimation；操作按钮保持可用。刷新翻译不改变业务身份，也不取消当前定位。禁用和销毁可能先以 Disabled 终止，回调只报告首次原因。

## 菜单导航

规则列表与 Grid 在列表对象挂 `LoopScrollMenuNavigation`，Cell 的唯一主按钮使用 `LoopScrollMenuButton`。组件从实际绑定几何位置推导 Grid 行列，不让左右移动从一行末端跳到下一行；主轴移动可定位未实例化目标，随后按 Key 选择对应当前按钮，自动跳过不可用目标。稳定 Key 随数据集合提交，回收时清除物理按钮旧焦点；按钮提交仍由 Core 已有 UI Module 派发，并验证该按钮当前身份与选中 Key 一致。

页面使用同一个 `MenuInputScope` 和组件的 `FirstSelection` 管理初始/恢复焦点，每帧 `Update(FirstSelection)` 更新当前有效默认按钮，保持等待 Submit/Cancel 释放的门闩。组件不模拟提交、不新增 EventSystem、不保存业务数据或重建页面 Scope。一个 Cell 有多个独立导航动作时，需要按其明确交互另行设计，不能默认把内部所有 Button 当作列表项。

## 验证

包测试独立留在包 Tests；宿主测试进入现有 Tests.EditMode/Tests.PlayMode。直接目标为 LoopScrollUIBindGenerationTests、LoopScrollItemViewBridgeTests、LoopScrollShowcaseTests，另检查 TestAssemblyBoundaryTests。不运行无关全量测试。

详见 [接入与运行示例](../runbooks/use-loop-scroll.md) 与包 Documentation~/Validation.md。

## 模块与示例归属

Loop Scroll 属于公共 UI 模块，不登记为玩法 Demo。宿主示例代码位于 `Assets/Scripts/Hotfix/Module/LoopScroll/Examples/`，命名空间为 `Hotfix.LoopScroll.Examples`；它引用导入后的 Samples.Common，保持在 Hotfix 程序集内，不上提至 Core。

已导入的包 Showcase 位于 `Assets/Samples/SleepyLoopScroll/Showcase/`；宿主 UIBind 场景位于 `Assets/Samples/SleepyLoopScroll/Host/Scenes/LoopScrollUIBindExample.unity`。Catalog 追加宿主页面，仍由 Showcase 主页进入，不加入正式 Hub、YooAsset 启动链路或 Build Settings。包源码、Core 桥接和注册 API 不随目录整理改变。

宿主场景通过 LoopScrollInputHost 接入公共菜单作用域与按钮五态；FocusBorder 为一张正式序列化 Sprite 的 Sliced Image，装饰不拦截射线，五态保留控件原制作 scale 与点击矩形。六个项目宿主场景绑定公共 FocusBorder 和 StartupLoading Prefab，不从独立包或未初始化的 ResourceServices 获取这些资源。

子例返回仍由 LoopSamplePage 导航至 menu，不强制显示全屏 Loading。Showcase menu 返回 AppEntrance 时由宿主实例化已绑定的 StartupLoadingView，先跨过实际呈现帧，再启动原生异步入口加载；显示 AsyncOperation 的真实进度，不用时间或进度/0.9 推造百分比。完成加载后释放激活闸，正式 AppEntrance 自己接管启动 Loading 生命周期。宿主返回门闩与页面交互保护防止重复提交，失败清理本次 Loading 并恢复原交互；销毁终止呈现等待并释放已开始操作的激活闸，避免挂住原生场景队列。

页面销毁解除返回订阅并释放本页面创建的 EventSystem 及 Loading，不保存跨会话对象。Showcase 测试归 `Assets/Scripts/Tests/PlayMode/Module/UI/` 与 `Tests.Module`，继续使用已有 Tests.PlayMode。

2026-10-11 目录整理验证：UIBind 3/3、ItemView 桥接 7/7、Showcase 8/8、程序集边界 5/5 通过；六个示例场景完成类型标识与缺失脚本检查，两个脚本、宿主场景和测试文件的 GUID 保持不变。迁移使用 Unity AssetDatabase 与序列化 API，临时迁移工具在验证后清理。未执行项目全量测试、独立包回归或 Player 构建。
