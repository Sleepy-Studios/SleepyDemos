# DLSS 实验室

## 体验入口

在 Editor 打开 `Assets/Scenes/AppEntrance.unity` 并运行，从 Hub 点击「DLSS 实验室」。默认启用 Quality，右侧面板可以关闭效果，或切换 Quality、Balanced、Performance、Ultra Performance、DLAA。面板显示实际输入/输出尺寸、首帧等待状态和错误。

按住鼠标右键转动视角，WASD 移动，左 Shift 加速，R 重置相机；点击返回按钮或按 Backspace 返回 Hub。场景包含移动物体、细线和透明面，可观察静态细节与运动表现。

当前体验范围为 Windows Editor。默认图形 API 为 DX12；Vulkan 需要关闭 Editor，再使用 `-force-vulkan -streamline-interpose` 启动。原生依赖准备见[接入手册](../runbooks/use-streamline.md)。本轮不提供 Player、Reflex 或帧生成体验。

## 职责与资源

- `Core.Runtime/Rendering/Streamline/StreamlineRuntime.cs`：正式启动注册与相机会话、设置切换、资源清理。
- `Hotfix/Demos/Dlss/DlssDemoController.cs`：场景交互、分辨率变化、进入退出。
- `Hotfix/Demos/Dlss/Adapters/UI/DlssSettingsView`：使用既有 UIManager 和 MvcBind 的设置面板。
- `Hotfix/Editor/Dlss/DlssDemoBuilder.cs`：生成 Demo 资源与 Hub 入口。
- `Assets/LoadResources/Demos/dlss`：场景、面板、材质、呈现 Shader 和独立 URP 配置。

`GameSceneId.Dlss` 经现有 GameSceneNavigator/YooAsset 加载 `Assets/LoadResources/Demos/dlss/Scenes/Main.unity`。Build Settings 仍只保存 AppEntrance。当前 Demo 依赖正式启动，不支持直接打开 Demo 场景运行。

## 画面与生命周期

世界相机离屏渲染，按 SDK 推荐尺寸提供 DLSS 输入；公共 Overlay UI 以目标分辨率合成。独立管线保持 renderScale=1，面板 RawImage 显示完整输出，呈现 Shader 使用 RGB 并保持世界画面不透明。关闭时使用原分辨率世界画面，不启用抗锯齿。该组合不代表任意现有 Camera Stack 都能直接启用 DLSS。

设置切换先停止捕获，等待 GPU 完成并释放 viewport，再查询新尺寸和重建资源。窗口尺寸变化稳定 0.25 秒后重配置。首个相机帧与首个成功 DLSS 评估分别记录，避免刚分配的空纹理被当作画面就绪。

退出先关闭本 Demo 的 View，再结束会话、恢复原管线和相机设置，最后返回 Hub。不能确认 GPU/SDK 清理成功时保留被引用资源并提示重启 Editor，避免提前释放。

## 编辑器入口

`Tools > SleepyDemos > DLSS` 下提供：

- 「生成设置面板」：重新生成 Prefab，并通过 MvcBind 更新绑定代码。
- 「生成体验场景与Hub入口」：重建 Demo 场景与专用管线，补齐菜单按钮。
- 「配置Windows图形后端」：将 Windows 图形 API 设置为 DX12、Vulkan，关闭自动选择。

生成场景会重建其布局，手动调整过场景后不要随意重跑。日常体验无需运行生成菜单。

## 验证与进度

2026-09-22，直接相关的 PlayMode 用例 `Tests.Demo.DlssDemoFlowTests.FormalStartupModesAndHubReturn` 在 DX12、Vulkan 各通过 1 项，覆盖正式启动、Hub 按钮、所有档位、关闭和两次进出场景。DX12 默认启动生效，Console 错误为 0，设置面板与场景画面已检查。未运行项目全量测试。

截图和原始报告只用于开发期间检查，不作为长期交付文档。本次体验版本已完成，Goal 在本地提交后暂停，等待反馈。用户体验重点是移动时画面、档位差异、窗口调整和操作手感；窗口 Resize、暂停恢复和不支持硬件的完整交互验收仍待后续补齐，Player 与交换链留在 FG/发布阶段。
