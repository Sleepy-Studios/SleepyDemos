# Streamline / DLSS 模块

## 职责

负责项目公共 Streamline 渲染接入，目前实现 DLSS SR/DLAA 与全局设置。Reflex、FG/MFG 留在后续阶段，RR 本轮仅预留输入契约。

## 当前阶段

G0–G2 已完成 Editor 基线与独立画面闭环；G3 公共主相机接入与全局设置版本已完成，双后端相关验证通过，本地提交后暂停 Goal 等待用户体验。Player 与交换链仍留在 FG/发布阶段。

| 阶段 | 状态 | 简明结果 |
|---|---|---|
| G0 | 完成 | Unity/URP、SDK 和原生构建基线已固定 |
| G1 | 完成（Editor） | 双后端设备、原生执行、资源访问与清理通过 |
| G2 | 完成（Editor） | DX12/Vulkan 图像测试各 13 项通过 |
| G3 | 公共版本完成（Editor） | 全局设置、双后端跨场景/全档位/后处理/Resize/暂停恢复/相机回退及失败回滚通过，Editor 直启通过 |
| G4–G5 | 等待用户体验后继续 | Reflex/PCL、Player 呈现、FG/MFG |
| G6 | 部分实现 | RR 契约已定义；发布和最终矩阵待完成 |
## 环境基线

2026-09-22 本机核对：

- Unity 6000.3.15f1，URP/Core 实际缓存及 packages-lock 均为 17.3.0；manifest 已对齐。
- RTX 3050，驱动 616.92。FG/MFG 需要其他支持硬件的成功验收。
- Visual Studio 2022 Community 17.14.40，Windows SDK 10.0.26100.0。
- Streamline v2.14.1，Windows x64 官方 Release zip；校验值由依赖锁文件维护。

## 主链路与生命周期

代码入口：`Core.Runtime/Rendering/Streamline`、`Core.Editor/Streamline`、`Native/Streamline/src/Bridge.cpp`。`StreamlineCaptureFeature` 捕获指定验证相机的 URP 颜色、深度和运动矢量，`AfterCapture` 在同一命令缓冲内录入处理；`StreamlineDlssValidation` 复制帧参数并向渲染线程提交原生事件，桥接执行 `slEvaluateFeature(kFeatureDLSS)`。独立图像测试与正式 Demo 共用该捕获入口，全局 StreamlineRuntime 管理当前主相机的 StreamlineCameraSession。

调用者持有输入/输出纹理，通过同一命令流回读确认 GPU 完成后再结束会话；原生层释放各 viewport 资源，确认清理事件完成及 SDK 返回成功后销毁纹理。SDK 帧令牌使用 Streamline 内部连续计数，不复用每个相机从零开始的历史序号，避免 Vulkan 保持初始化时快速重建会话发生常量冲突。DX12 会关闭本次 SDK 会话，Vulkan interposer 会保持初始化至设备关闭。有限会话的成功不能证明尚未接通的 Present 维护可支持长期运行。

改变同一 viewport 的模式或输出尺寸前，必须等待 GPU 完成，调用 `TryReleaseViewport`，提交并通过命令流回读确认释放事件完成，检查 `ViewportReleased` 成功后再重建。桥接拒绝省略此步骤的重配置。SDK 异常会使会话进入需重启状态，阻止继续调用已经部分关闭的插件。

`CaptureSession.PublishToCamera` 可把结果交回验证相机的 URP 后处理链：更新公开颜色资源和输出尺寸，更新 `_ScreenSize`，只关闭本帧后续 TAA。当前入口明确限定离屏透视相机、无 Camera Stack、Linear 缩放及有效处理回调；通用接入临时分离玩法主相机与公共 UI，重建和原后处理完成后，由公共呈现相机合成 Overlay UI，关闭时恢复原栈。

`StreamlineCameraHistory` 为单个相机填充非抖动投影、当前/上一帧裁剪空间变换与运动尺度。首次使用、相机或 viewport 变化、帧编号中断、尺寸/模式/投影变化会重置；宿主在切镜、停用或卸载时调用 `Reset()`，提交失败后也必须重置。当前只接受可逆的有限投影矩阵和透视相机。验证场景已使用该运行时类，不再自行维护一套历史计算。

预期顺序：原生插件加载 → Streamline 初始化 → Unity 图形设备建立 → 按设备查询能力 → 相机提交帧参数与资源 → 渲染线程执行 → GPU 完成后释放 → 设备关闭时清理。

每项能力分别记录 SDK 需求、硬件支持、集成状态与实际启用状态。SDK 调用失败时保留返回值和诊断，禁用受影响路径并回退。

## 全局设置与相机接入

`StreamlineRuntime` 对外提供 `SetMode`、`RequestedMode`、`EffectiveMode`、`Status`、尺寸及 `Changed`；`CanEnable` 查询当前后端和相机兼容条件，实际 GPU 支持仍以 SDK 运行结果为准。默认关闭，偏好保存在 PlayerPrefs 的 `SleepyDemos.Graphics.DlssMode`。未知保存值按关闭处理；设备或相机不支持不会覆盖偏好。

`UIRootManager.BindToBaseCamera` 先解除旧会话，再绑定目标相机及公共 UI 相机；场景导航回滚经过同一个入口。运行时服务由正式启动和 UI 初始化共用，不由 Demo 创建。当前支持屏幕透视主相机与单个公共 UI Overlay，额外相机栈、正交、XR 或局部视口回退。

启用期间克隆当前 URP 配置，保留 Renderer 和 Volume 设置，不写回项目资产；只在世界相机渲染时使用 SDK 比例。目标纹理维持全输出尺寸，以保留玩法相机的屏幕坐标语义。DLSS 在后处理之前输出，公共呈现相机随后以比例 1 合成 UI。原来未启用后处理的相机临时使用空 Volume 层，以获取时域抖动且不引入额外场景效果。关闭恢复目标、抗锯齿、后处理、深度、动态分辨率及原管线。

公共面板位于 `Hotfix/Module/GraphicsSettings`，使用既有 UIManager Widget，打开和关闭面板不改变效果。资源位于 `LoadResources/UI/GraphicsSettings`；实验室只包含场景与观察交互。Renderer 配置工具属于 Core.Editor，公共面板生成工具属于 Hotfix.Editor。

## 验证矩阵

| 功能 | D3D12 | Vulkan |
|---|---|---|
| SR / DLAA | Editor 公共主相机、五档及场景流程通过 | Editor 公共主相机、五档及场景流程通过 |
| Reflex / PCL | 待接入 | 待接入 |
| FG / MFG | 待公开呈现接口验证、待支持硬件 | 待公开呈现接口验证、待支持硬件 |
| RR | 等待光追输入 | 等待光追输入 |

## 本次公共版本验证

2026-09-22，Unity 6000.3.15f1 / URP 17.3.0 / Streamline 2.14.1，RTX 3050、驱动 616.92：

- DX12、Vulkan 均通过 `FormalStartupModesAndHubReturn`：正式启动、DroneFlight 实际选择/退出、Hub 与实验室沿用设置、所有档位、关闭面板、后处理曝光、1280×720 Resize 与恢复、暂停恢复、屏幕坐标和正交相机回退。
- 双后端均通过 `FailedUnloadRestoresDlssCameraAndPreference`：卸载失败后重新绑定原相机、保留偏好并恢复实际 DLSS 执行。
- 双后端 Editor 直启入口验证通过；`StreamlineSettingsTests` 的 4 项 EditMode 测试通过。没有执行项目全量测试或 Player 构建。
- 原生桥接已重新构建并部署，SDK 帧计数跨会话复用问题已修复；最终 Console 错误查询为 0。窗口交互手感和更复杂场景画质由用户继续体验。

## 维护与验证

G2 的双后端图像测试各 13 项通过，相机历史 EditMode 测试 7 项通过，未运行项目全量测试。覆盖五档设置、真实 URP 输入输出、相机运动、切镜、尺寸变化、HDR 亮度与透明度变化。GPU 采样只覆盖原生 DLSS 命令，不能代表整帧收益或长期稳定性。

停止捕获须调用 `CaptureSession.StopCapture()`，阻止已经录入的 RenderGraph 回调继续生产工作。停止不等于释放，仍须等待 GPU 与 SDK 清理成功。

阶段截图、日志、JSON 和性能样本仅放在 Git 忽略的 `Library/Streamline/evidence`，阶段结束可删除，今后不再提交。文档保留当前实现、简明结果、操作步骤和待办。历史原始材料已删除。

`IStreamlineRayReconstructionInputProvider` 预留同相机同帧的含噪颜色、深度、运动矢量、反照率、法线粗糙度及镜面运动/命中距离。纹理由提供者持有至 GPU 消费结束；当前没有 RR 执行器。

## 相关文档

- [DLSS Demo 体验与维护](./dlss-demo.md)
- [接入设计与路线](../architecture/streamline-integration.md)
- [构建与验证手册](../runbooks/use-streamline.md)