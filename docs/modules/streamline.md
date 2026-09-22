# Streamline / DLSS 模块

## 职责

提供 DLSS SR/DLAA、Reflex、FG/MFG 的能力检测、配置与原生渲染接入。RR 等待光追输入，本轮仅预留契约。

## 当前阶段

G0–G2 已完成当前约定的 Editor 基线与独立画面闭环。DX12/Vulkan 五档、真实 URP 相机输出、输入和 GPU 耗时均有运行证据。下一步按用户指定交付正式启动、Hub 入口、DLSS Demo 和设置面板，完成后更新进度、本地提交并暂停 Goal，等待实际体验。Player 与交换链仍留在 FG/发布验证阶段。

| 阶段 | 状态 | 验收证据 |
|---|---|---|
| G0 | 完成 | SDK SHA-256 校验通过，NVIDIA interposer 签名 Valid，MSVC 原生构建通过；URP 声明已对齐 17.3.0 |
| G1 | 完成（Editor 范围） | 同版本桥接：DX12 原生 2 项通过、Vulkan 原生 3 项通过；资源往返、Vulkan 设备关闭与 DX12 SDK 清理证据齐全 |
| G2 | 完成（Editor 独立画面闭环） | 最终 DX12 `028f696a`、Vulkan `2fdb0e38` 各 13/13；五档 SDK 查询、真实 URP 输出、时域/透明度/HDR 检查和 GPU 采样齐全 |
| G3 | 下一交付 | 正式 AppEntrance/Hub → DLSS Demo，关闭/五档设置面板，往返与资源清理；提交后暂停等待用户体验 |
| G4–G5 | 等待体验后继续 | Reflex/PCL、Player 交换链、FG/MFG |
| G6 | 部分实现 | RR 输入接口已定义，最终矩阵待运行验证 |

## 环境基线

2026-09-22 本机核对：

- Unity 6000.3.15f1，URP/Core 实际缓存及 packages-lock 均为 17.3.0；manifest 已对齐。
- RTX 3050，驱动 616.92。FG/MFG 需要其他支持硬件的成功验收。
- Visual Studio 2022 Community 17.14.40，Windows SDK 10.0.26100.0。
- Streamline v2.14.1，Windows x64 官方 Release zip；校验值由依赖锁文件维护。
- 初始检查中 SleepyDemos Editor 未运行；8090 对应 FishingMaster，不能对其执行本任务。

## 主链路与生命周期

代码入口：`Core.Runtime/Rendering/Streamline`、`Core.Editor/Streamline`、`Native/Streamline/src/Bridge.cpp`。`StreamlineCaptureFeature` 捕获指定验证相机的 URP 颜色、深度和运动矢量，`AfterCapture` 在同一命令缓冲内录入处理；`StreamlineDlssValidation` 复制帧参数并向渲染线程提交原生事件，桥接执行 `slEvaluateFeature(kFeatureDLSS)`。当前是显式 Editor 验证会话，未替换项目 Game View 输出。

调用者持有输入/输出纹理，通过同一命令流回读确认 GPU 完成后再结束会话；原生层释放各 viewport 资源，确认清理事件完成及 SDK 返回成功后销毁纹理。DX12 会关闭本次 SDK 会话，Vulkan interposer 会保持初始化至设备关闭。有限会话的成功不能证明尚未接通的 Present 维护可支持长期运行。

改变同一 viewport 的模式或输出尺寸前，必须等待 GPU 完成，调用 `TryReleaseViewport`，提交并通过命令流回读确认释放事件完成，检查 `ViewportReleased` 成功后再重建。桥接拒绝省略此步骤的重配置。SDK 异常会使会话进入需重启状态，阻止继续调用已经部分关闭的插件。

`CaptureSession.PublishToCamera` 可把结果交回验证相机的 URP 后处理链：更新公开颜色资源和输出尺寸，更新 `_ScreenSize`，只关闭本帧后续 TAA。当前入口明确限定离屏透视相机、无 Camera Stack、Linear 缩放及有效处理回调；尚未接入项目主相机与 UI。

`StreamlineCameraHistory` 为单个相机填充非抖动投影、当前/上一帧裁剪空间变换与运动尺度。首次使用、相机或 viewport 变化、帧编号中断、尺寸/模式/投影变化会重置；宿主在切镜、停用或卸载时调用 `Reset()`，提交失败后也必须重置。当前只接受可逆的有限投影矩阵和透视相机。验证场景已使用该运行时类，不再自行维护一套历史计算。

预期顺序：原生插件加载 → Streamline 初始化 → Unity 图形设备建立 → 按设备查询能力 → 相机提交帧参数与资源 → 渲染线程执行 → GPU 完成后释放 → 设备关闭时清理。

每项能力分别记录 SDK 需求、硬件支持、集成状态与实际启用状态。SDK 调用失败时保留返回值和诊断，禁用受影响路径并回退。

## 验证矩阵

| 功能 | D3D12 | Vulkan |
|---|---|---|
| SR / DLAA | Editor 五档独立场景、输入与局部 GPU 耗时验证通过；正式 Demo 接入待完成 | Editor 五档独立场景、输入与局部 GPU 耗时验证通过；正式 Demo 接入待完成 |
| Reflex / PCL | 待接入 | 待接入 |
| FG / MFG | 待公开呈现接口验证、待支持硬件 | 待公开呈现接口验证、待支持硬件 |
| RR | 等待光追输入 | 等待光追输入 |

### 2026-09-22 DX12 Editor 证据

`Tests.Module.StreamlineNativeProbeTests.ProbeOnRenderThreadReportsDeviceWithoutEnablingEffects`：1/1 通过。原生编译使用 MSVC 19.44.35228.0。

原始查询结果、运行时间和实际加载桥接的 SHA-256 见[DX12 Editor 证据](./streamline-evidence/2026-09-22-dx12-editor.json)。

- Unity renderer=18 (D3D12)，deviceAvailable=true，commandListAvailable=true。
- swapchainAvailable=false，符合本机 Unity PluginAPI 文档中 Editor 不提供交换链的约束。
- `slGetFeatureRequirements` 四项均返回 0；`slIsFeatureSupported` 对 DLSS(0)、Reflex(3)、RR(1001) 返回 0，对 FG(1000) 返回 6 (`eErrorNoSupportedAdapterFound`)。
- `renderingEnabled=false`；这些结果只证明查询和设备访问，不证明效果、SDK 设备绑定或呈现维护已完成。
- 此次证据对应第一版探测桥接，仅观察 Vulkan 初始化时机。后续增强桥接的运行结果须另行记录，不能复用这一版二进制的通过结论。

### DX12 Editor 图像验证

- 加载桥接 SHA-256：`B97A05C982ADA14AFEA638953174786E0EA8C9F5244E6CCF4D906C7359AC7BB4`。
- Unity Test Runner **PlayMode**：`Tests.Module.StreamlineDlssImageTests`，任务 `3a9f181f`，**2/2 通过**。详细范围见[图像验收记录](./streamline-evidence/2026-09-22-dx12-editor-images.json)。
- `QualityAndDlaaWriteFiniteNonBlankGpuImages`：Quality 输入 512×288、输出 768×432；DLAA 输入输出均 768×432。验证原生 ABI、资源同步、非空有限 GPU readback。合成图案不构成画质优劣对比。
- `UrpSceneInputsProduceQualityAndDlaaImages`：正常启用的相机连续渲染，每模式 8 帧；包含运动方块、球体和细杆，HDR 颜色、深度、运动输入均来自 URP RenderGraph。验证投影可逆、jitter 变化、深度范围、移动物体方向/尺度及静止几何。
- SR 的移动物体平均 UV 运动量 `(0.002316024, 0)`，投影参考值 `(0.002329051, 0)`；DLAA 为 `(0.002318206, 0)`。所选静止几何最大运动量为 0。最终请求均返回 SDK 结果 0；图像见 [SR](./streamline-evidence/urp-images/Quality.png)、[DLAA](./streamline-evidence/urp-images/Dlaa.png)，相邻 `*-inputs.json`、深度和运动 PNG 保存输入证据。
- 回归覆盖了实现中发现的两类问题：RenderGraph 下兼容 GPU 投影接口返回零矩阵，改用 `GetProjectionMatrix` 与 `GL.GetGPUProjectionMatrix`；EditMode/手动 RenderRequest/反复启停相机不能代表连续物体历史，改用正常 PlayMode 相机与同帧处理。早期仅“SDK 成功且非空”的用例不作为时域正确性的依据。
- 当前使用 HDR 与自动曝光，手动曝光输入和画质对比尚未验收。测试总耗时不是 GPU 耗时，尚无性能结论。

当前桥接的设备探测也已重跑：任务 `9ab78f6c`，1/1 通过，`deviceBindingResult=0`。见[设备绑定证据](./streamline-evidence/2026-09-22-dx12-editor-bound-probe.json)。

### 相机平移与切镜验证

- `StreamlineCameraHistoryTests`，EditMode 任务 `6906ffe7`，7/7 通过：验证静态世界点在两帧裁剪空间间的双向映射，以及主动重置、尺寸、模式、帧中断、投影、viewport 变化。
- `StreamlineDlssImageTests`，PlayMode 任务 `9aaab913`，3/3 通过，包含前述两项图像回归和新增的 `CameraMovementAndCutProduceValidTemporalInputs`。
- 每模式连续 24 帧，同时平移相机与方块，第 12 帧切镜。相机历史只在第 0/12 帧请求重置；移动方块和固定球体的运动方向/尺度分别与几何投影比较。SR 移动物体平均 UV 位移约 0.001322，参考 0.001331；固定球体因相机运动产生约 -0.000917，参考 -0.000945。
- 通过命令缓冲异步读取第 11/12/13 帧输入及 DLSS 输出，避免后续帧覆盖证据。切镜当帧物体中心偏差为 SR 0.181、DLAA 0.236 个输出像素；三个采样帧均通过有限颜色和中心位置检查。见 [SR 切镜当帧](./streamline-evidence/camera-images/Quality-cut-12.png)、[DLAA 切镜当帧](./streamline-evidence/camera-images/Dlaa-cut-12.png)、[完整测试记录](./streamline-evidence/2026-09-22-dx12-camera-history.json)。
- 此结果不等于通用无拖影验收：旋转、透明物、曝光变化、真实 Resize/模式切换、较长运行与 GPU 耗时仍待验证。尺寸等失效规则目前有单元测试，尚未据此宣称 GPU Resize 已通过。

### 下一阶段入口

继续在 Editor 完成相机旋转、透明物和曝光变化、GPU 耗时及 Vulkan 验证，再接入项目主相机与 UI。持续运行仍依赖 Present 维护的支持结论；交换链、独立 Player 与 IL2CPP 发布验收留到 G5/G6。

### G1 出口

同一桥接 `4A4709DA6B841F4A643DF34DC14FCF206447B2DF3259D00B2280D822C6E07DE1` 已完成双后端 Editor 原生验证。[G1 汇总证据](./streamline-evidence/2026-09-22-g1-native-baseline.json)：DX12 原生任务 `a3017a3e` 为 2 项通过、1 项 Vulkan 专用测试跳过；DX12 图像回归任务 `375fe518` 为 6/6 通过；Vulkan 基础与 Present 验证分别为 2/2、1/1 通过，关闭日志记录插件关闭和空延迟销毁列表。未执行全量测试。

G1 提交 `e3de506` 完成的是已调整后的 Editor 原生范围，并保存了当时已实现的 DX12 图像验证。Vulkan 图像执行和后续 G2 验收见下文；交换链控制与 Player 验证仍按用户安排后置。

### 同 viewport 切换、最终相机输出与清理

- 最新桥接 SHA-256：`AD625AC960E97B22BD36F1D353D5C9DDEC7C6179DDCF4750453C00C67FE958AF`。PlayMode 任务 `4ae331a7`，`StreamlineDlssImageTests` **6/6 通过**，没有运行全量测试。[测试与全部清理结果](./streamline-evidence/2026-09-22-dx12-resize-and-output.json)。
- 同一相机、历史对象和 viewport 31：SR 768×432 → DLAA 960×540 → SR 1152×648；每段 8 帧，每次重配置前显式释放成功。检查切换后的第 0/1/7 帧及最终输入。漏掉释放的负向用例在进入 SDK 重配置前返回错误 19、阶段 `ReleaseViewportBeforeReconfigure`，且之后正常清理。
- `DlssOutputReachesCameraTarget` 验证输出经过 URP 到达最终相机目标，保留下一帧 jitter 配置并关闭本帧后续 TAA。测试使用明确的 `Tonemapping.None` 临时配置；最终目标与 DLSS 的平均 RGB 绝对误差为 SR 0.000426、DLAA 0.000418，最大 0.001953。[SR 相机目标](./streamline-evidence/camera-output/Quality-camera.png) · [DLAA 相机目标](./streamline-evidence/camera-output/Dlaa-camera.png)。
- 六个用例的 `slFreeResources`、`slShutdown` 均返回 0，结束时 `sdkStillInitialized=false`、`restartRequired=false`。失败路径也会恢复项目管线和移除验证相机；异常情况下原生输入资源保留到 Editor 重启。
- 修复依据：旧用例 `e027d059` 在自动 Resize 后的 `slShutdown` 返回 24，日志停在 common 的延迟销毁 lambda。SDK `sl.dlss/dlssEntry.cpp` 在模式/输出尺寸改变时延迟 3 个呈现帧销毁旧 NGX 实例；手动集成指南要求每帧调用 Present 维护。当前 Editor 未接入该维护，不能依赖这种延迟释放。改为 GPU 完成后通过公开 `slFreeResources` 释放，再重建，并增加原生重配置防护与清理断言。旧的“GPU fence 完成即可判清理成功”结论不再采用。
- **边界仍然存在**：此修复验证的是有限 Editor 会话，不是 Present 维护的替代方案，不能据此声明持续渲染、长期显存稳定或发布链路完成。

### 当前增强桥接

- DX12 增加 `slSetD3DDevice` 绑定结果，分别记录设备、SDK 绑定及交换链可用性。
- Vulkan 使用公开 `IUnityGraphicsVulkanV2.InterceptInitialization`；仅在显式传入 `-streamline-interpose` 时，将函数查询转发给官方 interposer，并统计成功的 Present 调用。普通启动保持原加载器。
- DLSS 图像执行结果通过独立帧报告查询；能力探测报告的 `renderingEnabled=false` 仅表示探测本身不启用效果。Reflex 控制与帧生成尚未实现。
- Unity D3D12v8 提供交换链读取，但没有将 Streamline 代理交换链写回 Unity 的公开接口。SDK `slUpgradeInterface` 为交换链创建另一代理对象，不能把局部变量替换当成 Unity 已使用该代理。DX12 的呈现接入仍是未解决项。

### Vulkan Editor 原生前置验证

- 启动参数 `-force-vulkan -streamline-interpose`。桥接 SHA-256：`4A4709DA6B841F4A643DF34DC14FCF206447B2DF3259D00B2280D822C6E07DE1`。
- EditMode `StreamlineNativeProbeTests` 两项基础测试，任务 `b0ab031c`，2/2 通过；`VulkanPresentInterceptionAdvances`，任务 `f7331fda`，1/1 通过。未运行全量测试。[原始结果](./streamline-evidence/2026-09-22-vulkan-editor.json)。
- 初始化拦截已注册、被调用且启用 interposer；SDK 初始化结果 0，Unity Vulkan 设备和命令缓冲可访问。DLSS/Reflex/RR 的 SDK 支持查询返回 0，FG 返回 6；这些不是效果启用结果。
- 16×16 RGBA8 纹理由 Unity GPU 清色，经 `IUnityGraphicsVulkanV2.AccessTexture` 转为 Shader Read 状态，随后回读 256 个像素全部匹配；原生格式 37、命令缓冲有效，回读完成后释放纹理。
- 同时覆盖实例级和设备级函数查询后，观察到穿过 interposer 的 `vkQueuePresentKHR`，两次采样计数 920 → 922。它证明这条 Editor 呈现调用路径存在，不代表逐帧时序、帧生成或长期稳定性验收。
- 本机 Vulkan 的 AsyncQueue fence 查询因 `supportsAsyncCompute=false` 抛错；只读探测现使用同一命令流的异步回读确认完成。`slSetD3DDevice` 结果对 Vulkan 不适用，报告值为 null。
- 此次 Vulkan Editor 正常退出；SDK 日志记录各插件依次关闭，common 的延迟销毁列表为 0。完整本地日志保留于 `Library/Streamline/evidence/vulkan-device-lifecycle.log`。
- 原生源码已增加 Vulkan 图像执行分支：通过 `AccessTexture` 声明计算读写依赖，创建按 viewport/输入槽持有的 ImageView，填写 SL 资源描述，并在所有资源访问之后取得命令缓冲用于 tagging/evaluate。仅接受单层、单 mip、非 MSAA 的 2D 颜色格式纹理；深度输入使用现有捕获流程的 R32 颜色纹理。
- ImageView 不拥有 Unity 图像或内存。调用方等待 GPU 完成、SDK 成功释放 viewport 后才能销毁视图；替换纹理必须先释放 viewport。SDK 释放失败时保留视图，避免提前销毁仍可能被引用的资源。
- 本次原生构建并部署的 SHA-256 `883C50F145BAA801CEF827DA53DA8CA1A550D191C10356F141D5AFA83B97CD7D`；以 `-force-vulkan -streamline-interpose` 重启后完成六项精确图像测试。
- 托管入口已接受 Windows Editor DX12/Vulkan；图像用例通过命令流回读等待 GPU 完成，证据分存于 `Library/Streamline/evidence/Direct3D12` 和 `Vulkan`。DX12 先在 G1 桥接上通过等待逻辑回归（任务 `97172671`，1/1），不代表新桥接的 DX12 回归。Vulkan 新桥接的合成输入 `9f63f96f`、URP 场景 `b7c6e44e` 均 1/1 通过，清理返回 0。随后相机切镜 `ab1dca9f`、Resize `b83d224d`、漏释放保护 `36170da9`、相机目标回接 `4c318794` 均 1/1 通过。[完整结果与图像](./streamline-evidence/2026-09-22-vulkan-editor-images.json)。同版本 DX12 回归也已通过，见下节。后续画质场景与最终回归见下文；长期稳定性与整帧收益不由这些有限场景结果证明。

### 同版 DX12 回归与初步 GPU 采样

- 桥接 `883C50F145BAA801CEF827DA53DA8CA1A550D191C10356F141D5AFA83B97CD7D`，重启为 DX12 后六项图像用例 6/6 通过，任务 `004319c6`。[回归结果](./streamline-evidence/2026-09-22-dx12-vulkan-bridge-regression.json)。
- 新增精确用例 `SustainedFramesRecordDlssGpuTiming`，任务 `6a1546ce`，1/1 通过。SR 和 DLAA 各 240 帧，预热 64 帧后各 176 个有效 GPU 样本，无剔除样本；SDK 最终清理成功，Editor 退出 PlayMode，错误查询 0。
- 768×432 输出、Quality 输入 512×288、DLAA 输入 768×432。原生 DLSS 命令平均 / P95：Quality **2.973 / 3.549 ms**，DLAA **2.550 / 3.165 ms**。[原始样本和边界](./streamline-evidence/2026-09-22-dx12-sustained-timing.json)。
- 采样不含输入复制、回读、整个相机渲染；当前 Editor 也没有隔离其他 GPU 应用，不能据此比较模式优劣、声明帧率提升或代表发布性能。240 帧不是长期显存稳定性验收。Vulkan 同一用例也已通过，任务 `c4b941b5`，每模式各 176 个有效样本、无剔除样本；Quality 平均 / P95 为 **2.872 / 3.336 ms**，DLAA 为 **2.587 / 3.174 ms**。[Vulkan 原始样本](./streamline-evidence/2026-09-22-vulkan-sustained-timing.json)。两次运行未控制 GPU 外部负载，不能据此作跨 API 性能排名。
- Vulkan 图像测试后的正常设备关闭日志保留于 `Library/Streamline/evidence/vulkan-image-session-sdk.log`，SDK common 延迟销毁列表为 0。
### 相机旋转覆盖

- Vulkan 精确用例 `CameraRotationProducesValidTemporalInputs`，任务 `e5ba97c2`，1/1 通过；每模式 24 帧，三轴旋转，检查静态和移动几何的运动方向/尺度、历史重置以及连续帧输出重心。[结果与图像](./streamline-evidence/2026-09-22-vulkan-rotation.json)。
- DX12 同一精确用例也已通过，任务 `5e46f066`，1/1。[DX12 结果](./streamline-evidence/2026-09-22-dx12-rotation.json)。透明物仍需独立验证，HDR 亮度覆盖见下节。仅运行相关精确用例，没有全量测试。

### HDR 亮度阶跃覆盖

- Vulkan 精确用例 `HdrBrightnessStepsRecoverWithoutHistoryReset`，任务 `e36bb7a9`，1/1 通过；SR/DLAA 各 32 帧，输入倍率 `1 → 8 → 0.125 → 1`，不因亮度变化重置历史。每段保存第 0/1/7 帧有限性、线性亮度和配对图像，末帧均满足平均亮度误差小于 15% 的要求。[结果与图像](./streamline-evidence/2026-09-22-vulkan-brightness.json)。
- PNG 按输入倍率归一显示，JSON 为原始线性 HDR 数值；平均亮度恢复不能代表逐像素细节、透明物或项目曝光控制器的验收。DX12 同用例也已通过，任务 `befcfb09`，1/1。[DX12 结果与图像](./streamline-evidence/2026-09-22-dx12-brightness.json)。

### 透明物覆盖与 G2 剩余实现

- DX12 精确用例 `TransparentSurfaceChangesReachDlssOutput`，任务 `91c01bc3`，1/1 通过；实际 URP Alpha 面片按 `1 → 0.45 → 0` 切换，检查输入混合方程和输出内部区域均值。[DX12 结果与图像](./streamline-evidence/2026-09-22-dx12-transparency.json)。Vulkan 同用例也已通过，任务 `d45c97de`，1/1。[Vulkan 结果与图像](./streamline-evidence/2026-09-22-vulkan-transparency.json)。
- 此用例不覆盖运动粒子、折射和边缘拖影。五档质量模式和 SDK 推荐输入尺寸查询已进入运行验证，状态见下节；不能将已有出图等同于 G2 全部交付。

### 质量档与推荐输入尺寸

- `StreamlineDlssMode` 增加 Balanced、Performance、UltraPerformance；Quality/DLAA 保持原数值。原生侧按模式白名单验证，SDK 不支持的模式/尺寸会返回失败。
- `TryEnqueueOptimalSettings` 使用独立渲染事件调用 `slDLSSGetOptimalSettings`，复用设备初始化与串行生命周期；`TryGetOptimalSettings` 按请求编号返回推荐输入、最小/最大范围和原始失败诊断。它不分配 viewport，不启用图像处理，也不以固定比例代替 SDK 返回值。
- 新桥接 SHA-256 `0CF97B4F9A7E39AEEB8716B5986918F4B3069747866B8DEA5427802FB6B30C3E`。DX12 精确测试 `QualityModesUseSdkRecommendedInputSizes`，任务 `45a13b4a`，1/1 通过：五档各 8 帧合成输入出图、逐档释放及最终清理。[原始结果](./streamline-evidence/2026-09-22-dx12-quality-modes.json)。
- 输出 1280×720 时，SDK 推荐 Quality 853×480、Balanced 742×418、Performance 640×360、UltraPerformance 427×240、DLAA 1280×720；推荐值随 SDK/目标尺寸变化，不能当作全局常量。
- 同版 Vulkan 五档验证已通过，任务 `e0d55510`，1/1。[Vulkan 五档结果](./streamline-evidence/2026-09-22-vulkan-quality-modes.json)。无效/过期查询精确用例分别通过 DX12 `489293ce` 和 Vulkan `d8471ce6`，均 1/1；验证无效模式拒绝后有效查询可继续，旧编号及零编号不会取得新结果。[DX12 拒绝路径](./streamline-evidence/2026-09-22-dx12-settings-rejection.json)、[Vulkan 拒绝路径](./streamline-evidence/2026-09-22-vulkan-settings-rejection.json)。下一入口为真实 URP 配置使用推荐尺寸：URP 17.3 按 `(int)(输出尺寸 × renderScale)` 分别截断宽高，必须校验实际输入仍位于 SDK 范围，不能假定一个比例能精确表示推荐宽高。未运行全量测试。

### 五档真实 URP 配置

- `StreamlineDlssOptimalSettings.TryGetUrpRenderScale` 计算统一比例及实际尺寸，验证 SDK 允许范围；函数不修改管线资产。精确宽高无法同时表达时保留合法取整结果，宿主按实际尺寸分配输入，并用真实捕获尺寸复核。
- Vulkan 精确用例 `UrpQualityModesUseSdkDimensionsAndReachCameraTarget`，任务 `7c9a92b3`，1/1 通过；五档顺序查询、配置临时 URP、检查运动输入并回接相机目标，每档切换前释放 viewport。[结果与图像](./streamline-evidence/2026-09-22-vulkan-urp-quality-modes.json)。1280×720 输出下 Balanced 实际输入为 743×418，其余四档匹配 SDK 推荐尺寸。
- 首轮 `bf1ce479` 暴露验证相机超过预定采样数量，jitter 数组越界。已在最后一个渲染回调停止相机，并在 TearDown 等待 GPU 前停用生产者和回调；SDK 清理未失败。原始失败保留于本地 Library，重跑通过，没有忽略日志错误。
- 最终 DX12/Vulkan 均已运行包含本用例的 13 项图像类回归并通过，见 G2 收口记录；未运行项目全量测试。

### Player 构建前置环境

- Console 曾报告 Visual Studio IDE 包 2.0.28 下载认证错误；不据此宣称 Console 全部干净。
- 项目 HybridCLR 包为 8.14.1，本地运行库原为 8.12.0，首次 Player 构建被版本检查阻止。已通过官方 InstallerController 更新项目本地运行库至 8.14.1，版本检查通过；旧运行库和源码缓存保存在 `Library/Streamline/hybridclr-backup`。
- 修复 `FlowLayoutGroup.OnValidate` 的 Player 编译错误：覆写方法仅在 `UNITY_EDITOR` 编译，与 uGUI 基类一致。布局运行时算法不变。
- 对该条件编译修复，仅运行既有横向/纵向 FlowLayoutGroup 两个精确 EditMode 测试，任务 `469e0b98`、`bd02e1c3`，均 1/1 通过；未运行 UI 模块全量测试。
- 已通过项目现有 `hybridclr_generate_all` 完成编译、IL2CPP 定义、link.xml、AOT 裁剪、方法桥与泛型引用生成（232.42 秒）；同步更新了两个既有 Assets 生成物。
- Player 探测曾因未命名场景失败；最后一次用例在检查到 Test Runner 默认 Camera/Light 后提前退出，没有运行构建。已移除这项提前引入的 Player 用例和专用启动组件。后续 FG/发布阶段使用正式保存的验证场景。当前没有运行中的 Player 构建测试。

### RR 输入扩展点

`IStreamlineRayReconstructionInputProvider` 提供同一相机、同一帧的含噪颜色、深度、运动矢量、漫反射/镜面反照率、法线粗糙度和镜面运动矢量或命中距离。纹理归提供者所有，应保持有效至 GPU 消费结束。当前没有 RR 执行器，接口成功不代表效果启用。

验证记录必须区分编译、SDK 能力查询、渲染效果和发布包。真实渲染帧率与生成后的呈现帧率分开统计。Unity Test Runner 仅运行直接相关测试；尚未执行全量测试。

本轮结束检查：Editor 已退出测试 PlayMode，无正在进行的编译；Console 错误查询为 0。原项目管线已恢复，未保存验证相机或临时管线资产。

## G2 收口与下一入口

[最终双后端验收记录](./streamline-evidence/2026-09-22-g2-final.json)绑定原生 DLL 与源文件 SHA-256，DX12 `028f696a`、Vulkan `2fdb0e38` 各 13/13。记录包含 SDK 推荐尺寸、实际 URP 尺寸、输入/输出图像、GPU 样本和清理结果。当前阶段验收范围是 Editor 独立画面闭环；不包含项目 UI/Hub、Player 呈现、Reflex 或 FG。

最终回归前的 DX12 `384081c0` 为 12/13，Resize 在采样结束后又收到一次 RenderGraph 回调。只禁用 Camera 不足以处理已录入工作；新增 `CaptureSession.StopCapture()`，在录入及执行边界检查停止状态，并在等待 GPU 前停止生产者。重跑双后端各 13/13，无忽略错误日志。停止捕获不会释放 GPU 资源，仍须确认已提交工作和 SDK 清理完成后才能 Dispose。

下一交付使用正式启动链和场景导航：新增 DLSS Demo，进入后提供关闭、Quality、Balanced、Performance、UltraPerformance、DLAA 控制及实际生效状态；验证返回 Hub 与退出清理。按用户要求更新进度、第二次本地提交后暂停 Goal，等待用户体验；暂不推进 G4–G6。
## 相关文档

- [接入设计与路线](../architecture/streamline-integration.md)
- [构建与验证手册](../runbooks/use-streamline.md)
