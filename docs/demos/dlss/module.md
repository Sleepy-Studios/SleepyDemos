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
- 公共面板维护已保存 Prefab，通过现有 MvcBind 更新绑定；一次性面板生成器已删除。
- 实验室维护已保存 Main 场景，不再提供一次性重建菜单。

原生依赖和 Vulkan 启动方式见[接入手册](../../runbooks/use-streamline.md)。当前只支持 Windows Editor，Player、Reflex 和帧生成后续再接。

## 验证

相关 PlayMode 用例 `Tests.Demo.DlssDemoFlowTests.FormalStartupModesAndHubReturn` 检查正式启动、DroneFlight 实际选择和退出、Hub 与实验室沿用设置、各档画面、面板开关、窗口变化、暂停恢复、屏幕坐标及不支持相机回退。2026-09-22 双后端公共流程已通过，另已通过失败回滚、Editor 直启与设置持久化检查。未执行全量测试。本地提交后暂停 Goal，等待用户体验。

截图和原始报告只放临时目录；用户体验重点是画面差异、相机控制和 UI 手感。

观察输入使用 Core InputActionSession；设置打开时停止 Observe，关闭后经过中立门闩恢复。DLSS 功能仍受 GPU/平台限制，移动端使用已有不支持时的原生渲染回退；三端输入接入不表示 Android 支持 DLSS。


## 公共 UI 复用（2026-10-05）

观察控件继续保存于 DlssControls.prefab，按钮复用 InputCommandButton、UIStateInteraction；DlssControlsPresenter 重复 Bind 先解除旧订阅，销毁时配对释放并清除保持输入。公共设置以普通 Pop 打开期间，观察动作切换到 Menu 并隐藏观察控件；关闭销毁 Pop 后恢复 Observe 和原观察会话。

画质设置继续复用公共 DlssSettingsView，不另建 Demo 设置页。画质模式的 Normal/Selected 由各按钮 UIState 持有，独立反馈层只处理焦点边框、按压缩放等交互属性，避免 RefreshState 与交互反馈覆盖同一底色。

本次定向生命周期测试 fb10ffa1（1/1）验证重复绑定只有一份按钮订阅、隐藏释放保持输入、再次显示不延续 Sprint、销毁配对解除。此次不运行 GPU 模式/图像质量全量测试；模式底色与交互反馈分离属于公共画质界面修改。
