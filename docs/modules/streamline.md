# Streamline / DLSS 模块

## 职责

提供 DLSS SR/DLAA、Reflex、FG/MFG 的能力检测、配置与原生渲染接入。RR 等待光追输入，本轮仅预留契约。

## 当前阶段

当前完成 G0/G1 的 Editor 原生基线，继续实施 G2。DX12 已执行真实 DLSS SR/DLAA 并读回 GPU 图像；Vulkan 图像执行、连续运行和完整画质/性能验收仍在进行。Player 与交换链移到 FG/发布验证阶段，不阻挡当前 SR/DLAA 验证。

| 阶段 | 状态 | 验收证据 |
|---|---|---|
| G0 | 完成 | SDK SHA-256 校验通过，NVIDIA interposer 签名 Valid，MSVC 原生构建通过；URP 声明已对齐 17.3.0 |
| G1 | 完成（Editor 范围） | 同版本桥接：DX12 原生 2 项通过、Vulkan 原生 3 项通过；资源往返、Vulkan 设备关闭与 DX12 SDK 清理证据齐全 |
| G2 | 进行中 | DX12 图像测试 6/6、相机历史 7/7 通过；新增模式/尺寸切换和相机输出回接；完整画质、性能与 Vulkan 待验收 |
| G3–G5 | 待实施 | 依赖各后端 G1/G2 结论 |
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

调用者持有输入/输出纹理，等待 GPU fence 后再结束会话；原生层释放各 viewport 资源并关闭本次 SDK 会话，清理 fence 通过后销毁纹理。有限会话的成功不能证明尚未接通的 Present 维护可支持长期运行。

改变同一 viewport 的模式或输出尺寸前，必须等待 GPU 完成，调用 `TryReleaseViewport`，提交并等释放事件 fence，检查 `ViewportReleased` 成功后再重建。桥接拒绝省略此步骤的重配置。SDK 异常会使会话进入需重启状态，阻止继续调用已经部分关闭的插件。

`CaptureSession.PublishToCamera` 可把结果交回验证相机的 URP 后处理链：更新公开颜色资源和输出尺寸，更新 `_ScreenSize`，只关闭本帧后续 TAA。当前入口明确限定离屏透视相机、无 Camera Stack、Linear 缩放及有效处理回调；尚未接入项目主相机与 UI。

`StreamlineCameraHistory` 为单个相机填充非抖动投影、当前/上一帧裁剪空间变换与运动尺度。首次使用、相机或 viewport 变化、帧编号中断、尺寸/模式/投影变化会重置；宿主在切镜、停用或卸载时调用 `Reset()`，提交失败后也必须重置。当前只接受可逆的有限投影矩阵和透视相机。验证场景已使用该运行时类，不再自行维护一套历史计算。

预期顺序：原生插件加载 → Streamline 初始化 → Unity 图形设备建立 → 按设备查询能力 → 相机提交帧参数与资源 → 渲染线程执行 → GPU 完成后释放 → 设备关闭时清理。

每项能力分别记录 SDK 需求、硬件支持、集成状态与实际启用状态。SDK 调用失败时保留返回值和诊断，禁用受影响路径并回退。

## 验证矩阵

| 功能 | D3D12 | Vulkan |
|---|---|---|
| SR / DLAA | Editor 有限会话出图与基础时域输入检查通过，完整画质验证中 | 原生前置验证通过，图像执行待实现 |
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

这完成的是已调整后的 G1 Editor 范围。Vulkan ImageView/SL 资源封装和图像评估属于下一阶段；交换链控制和 Player 验证按用户安排后置。已有 G2 的 DX12 验证实现会随本次原生基线提交保存，G2 状态保持进行中。

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
- 下一入口：构造 Vulkan ImageView、填充 SL Vulkan 资源描述，并接入实际 SR/DLAA 评估；当前 `StreamlineDlssValidation.TryEnqueue` 仍仅接受 DX12。

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

## 相关文档

- [接入设计与路线](../architecture/streamline-integration.md)
- [构建与验证手册](../runbooks/use-streamline.md)
