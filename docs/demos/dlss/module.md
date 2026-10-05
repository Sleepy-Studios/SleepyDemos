# DLSS 实验室

## 体验方式

在 Editor 运行 `Assets/Scenes/AppEntrance.unity`，从 Hub 进入「DLSS 实验室」。在 Hub 的画面设置入口或实验室已有操作控件中按需打开公共 Pop，可选择关闭、Quality、Balanced、Performance、Ultra Performance 或 DLAA。没有全局右上角悬浮按钮。首次默认关闭，之后记住选择；关闭面板不关闭效果。

键鼠维持右键转向、WASD 移动、Shift 加速、R 重置、Backspace 返回；手柄左摇杆移动、右摇杆转向、左肩加速、西侧键重置、返回键退出、Menu 打开设置；触屏使用移动区/视角拖动区及按钮。观察动作保存在 Data/Dlss.inputactions，触控界面保存在 Prefabs/UI/DlssControls.prefab。场景包含移动物体、细线和透明面，用于观察静态细节和运动表现。

## 模块边界

实验室只负责场景、演示物体和视角交互，代码位于 `Hotfix/Demos/Dlss`，资源位于 `LoadResources/Demos/dlss`。它使用标准 MainCamera 和公共场景导航，不持有 DLSS 会话、不设置管线、不提供自己的画质 UI。

通用 DLSS 由 [Streamline 模块](../../modules/streamline.md) 管理。公共面板代码位于 `Hotfix/Module/GraphicsSettings`，资源位于 `LoadResources/UI/GraphicsSettings`；图像输出由 Core 的渲染流程完成，与面板显示无关。

`GameSceneId.Dlss` 仍表示实验室场景，不是 DLSS 功能开关。场景地址为 `Assets/LoadResources/Demos/dlss/Scenes/Main.unity`。Build Settings 仍只包含 AppEntrance。本实验室仍从正式入口进入；已有 DroneFlight Editor 直启路径也初始化公共渲染服务和设置入口。

## 编辑器维护

- `Tools > Rendering > Streamline > 配置项目Renderer`：为现有 Renderer 补齐 Feature，不替换原配置。
- 公共面板维护已保存 Prefab，通过现有 UIBind 更新绑定；一次性面板生成器已删除。
- 实验室维护已保存 Main 场景，不再提供一次性重建菜单。

原生依赖和 Vulkan 启动方式见[接入手册](../../runbooks/use-streamline.md)。当前只支持 Windows Editor，Player、Reflex 和帧生成后续再接。

## 验证

相关 PlayMode 用例 `Tests.Demo.DlssDemoFlowTests.FormalStartupModesAndHubReturn` 检查正式启动、DroneFlight 实际选择和退出、Hub 与实验室沿用设置、各档画面、面板开关、窗口变化、暂停恢复、屏幕坐标及不支持相机回退。2026-09-22 双后端公共流程已通过，另已通过失败回滚、Editor 直启与设置持久化检查。未执行全量测试。

截图和原始报告只放临时目录；用户体验重点是画面差异、相机控制和 UI 手感。

观察输入使用 Core InputActionSession；设置打开时停止 Observe，关闭后经过中立门闩恢复。DLSS 功能仍受 GPU/平台限制，移动端使用已有不支持时的原生渲染回退；三端输入接入不表示 Android 支持 DLSS。


## 状态与页面生命周期

`DlssAction → DlssHandler → DlssData` 管理重置、设置、退出和触屏加速请求；场景控制器保留相机移动、连续轴采样与演示物体动画。Data 随场景注册和移除，页面加载使用场景取消令牌，完成后核对注册实例。

观察控件仍使用原地址 `DlssControls.prefab`，现在由 `DlssControlsView` 和 UIManager 管理。UIBind 生成组件引用和输入回调，View 声明一次 Data 绑定；隐藏前由 Core 退订，输入组件禁用时释放保持操作。打开设置关闭观察 View，关闭设置重新显示缓存 View，离场销毁该 View。

公共 `GraphicsSettingsData/Handler` 随 FluxService 注册。模式按钮派发 Action，Handler 调用现有 Streamline 服务；Data 发布服务实际请求档位、生效档位、尺寸和反馈。显示状态不推测 GPU 执行结果。

本轮定向验证：观察控件生命周期、设置阻断恢复、Editor 直启和偏好测试通过。GPU 综合用例在原有“画面不能为空”断言失败，截图为全黑；运行时相机已启用，Game View 未呈现，画面验收仍待解决。没有运行全项目测试。


2026-10-05本轮Flux验收：控制页、设置阻断/恢复及直接启动专项通过；正式模式回归4c951732仍在Off画面像素断言失败（黑帧）。不能据状态检查通过宣称视觉验收通过，完整范围见[实施记录](../../agent/flux-ui-refactor-2026-10-05.md)。
