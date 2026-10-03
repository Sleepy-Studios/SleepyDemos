# Loop Scroll 示例

稳定内部标识：`loop_scroll`。示例展示独立 UPM 虚拟列表与宿主 ItemView/MvcBind 的薄桥接，通过示例主页进入各个页面，不加入项目正式 Hub 菜单或资源启动链路。

- 统一运行入口：`Assets/Samples/SleepyLoopScroll/Showcase/Main.unity`，或菜单 `Tools/Sleepy Loop Scroll/Open Showcase`。
- 主菜单提供基础、多类型、聊天、轮播分页与宿主 MvcBind 示例；子场景有返回按钮。
- 首次中文；主菜单 `中文 / English` 切换并保留选择，示例稳定 ID 不随语言变化。
- 宿主 MvcBind 页面展示中心定位、±60 Canvas UI 像素偏移、手动取消和完成/取消状态；拖动可打断定位，按钮始终可用。
- 维护已保存宿主场景；一次性 Build MvcBind Example 已删除。各示例场景通过 LoopScrollInputHost 接入公共三端导航与反馈。
- [宿主模块](../../modules/loop-scroll.md) 与 [接入步骤](../../runbooks/use-loop-scroll.md)。

包的一个 Showcase Sample 包含 Main 与四个独立子场景。纯包目录不包含宿主 MvcBind 入口；宿主示例场景仍位于 `Assets/LoadResources/Demos/loop_scroll/Scenes/LoopScrollMvcExample.unity`。Editor 导航不修改 Build Settings，独立 Player 演示需要显式传入场景组。包 API、架构及发布验证在独立仓库维护。

调用统一为先注册再 SetTotalCount，操作使用 RefreshCells、RefillCells(RefillOptions)、ScrollToCell；宿主只生成 RectData/Click/ItemHide，所有回调携带 CellBindContext。当前验收和未完成门槛见包 Documentation~/UnifiedValidation.md。

此前 API 收口阶段于 2026-10-01 完成 Unity 6 宿主正式编译及相关 Test Runner 验收：MvcBind 2/2、桥接 5/5、Showcase 6/6、程序集边界 5/5，Console 无错误；未执行全量测试。按钮/拖拽为事件模拟，实际触摸和独立 Player 构建仍按包发布检查表验收。

本轮定位完成/取消/偏移验收：最终代码双版本包 PlayMode 各 40/40、EditMode 各 2/2；Unity 6 宿主 LoopScrollShowcaseTests 7/7，Console Error 为 0。已检查中英完成/拖动取消截图；无宿主全量测试、真实鼠标/触摸或 Player 构建验证。原始证据与当前完整契约见包验收记录和 APIReference。
