# 构建与验证 Streamline / DLSS

## 适用环境

Windows x64、Unity 6000.3.15f1、URP 17.3.0、VS 2022 C++ 工具链和 Windows SDK。先阅读[当前支持状态](../modules/streamline.md)，不要把接入规划当作已支持列表。

## SDK 与构建

SDK 固定为 Streamline v2.14.1，使用官方 Windows x64 Release。依赖准备必须校验 SHA-256，运行时检查 NVIDIA interposer 签名；发布包使用官方 production 库并保留许可证。原生工程独立于 Unity 自动生成的 sln/csproj。

在仓库根目录运行（UnityEditor 参数指向本机对应版本）：

```powershell
./scripts/streamline/Prepare-Dependencies.ps1
./scripts/streamline/Build-Bridge.ps1 -UnityEditor 'D:/Unity/Unity_Editor/6000.3.15f1/Editor/Unity.exe' -Deploy
```

锁文件为 `Native/Streamline/dependencies.lock.json`。SDK、Vulkan-Headers 与原生构建缓存位于 `Library/Streamline`，桥接输出位于 `Assets/Plugins/Streamline/x86_64`。SDK DLL 位于 Unity 忽略导入的 `Streamline~` 子目录，通过绝对路径显式加载；构建处理器负责复制到 Player。部署脚本对变化的文件执行原子替换，并将旧文件保存在 `Library/Streamline/deployed-backups`。已经加载插件的 Editor/Player 必须重启才能验证新映像，新启动的 Player 使用磁盘上的新版本。

`Tools > Rendering > Streamline Diagnostics` 提供“提交渲染线程探测”和“读取最近结果”。探测不会启用图像效果，`renderingEnabled=false` 是此阶段的预期值。

自动化入口为 Unity Test Runner：

- **EditMode** `Tests.Module.StreamlineNativeProbeTests`：当前 Editor 后端的渲染线程探测，证据位于 `Library/Streamline/evidence`。
- **EditMode** `Tests.Module.StreamlineCameraHistoryTests`：相机重投影与历史失效规则，不需要 SDK 或 RTX GPU。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.QualityAndDlaaWriteFiniteNonBlankGpuImages`：DX12/Vulkan 原生合成输入验证（各后端通过状态见模块文档），图像位于 `Library/Streamline/evidence/<GraphicsDeviceType>/images`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.QualityModesUseSdkRecommendedInputSizes`：输出 1280×720，逐档查询 Quality、Balanced、Performance、Ultra Performance、DLAA 的 SDK 推荐尺寸与范围，使用推荐尺寸出图，每档结束释放 viewport。结果在 `Library/Streamline/evidence/<GraphicsDeviceType>/quality-modes`，不要把枚举存在当作该档已获硬件支持。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.UrpQualityModesUseSdkDimensionsAndReachCameraTarget`：对五档推荐设置调用 `TryGetUrpRenderScale`，应用到临时 URP 管线，检查真实捕获尺寸、运动矢量和最终相机输出；同一 viewport 在档位变化前显式释放。`urp-quality-modes/*-resolution.json` 同时记录推荐尺寸、实际尺寸、SDK 范围与 renderScale。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.InvalidAndStaleSettingsRequestsAreRejected`：验证无效模式返回失败，随后正常 Quality/DLAA 查询可继续工作；拒绝旧请求编号和零编号，结果位于 `Library/Streamline/evidence/<GraphicsDeviceType>/settings-rejection`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.HdrBrightnessStepsRecoverWithoutHistoryReset`：保持 viewport 与历史，合成 HDR 输入亮度按 `1 → 8 → 0.125 → 1` 切换，每段 8 帧；记录段内第 0/1/7 帧的线性亮度与配对图像，位于 `Library/Streamline/evidence/<GraphicsDeviceType>/brightness-images`。末帧平均亮度相对输入误差须小于 15%，并检查采样图像无 NaN/Inf；这不等同于逐像素画质或场景曝光系统验收。预览 PNG 除以该段输入倍率以便观察，JSON 保留原始线性 HDR 均值。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.UrpSceneInputsProduceQualityAndDlaaImages`：临时克隆 URP 配置并创建几何场景，捕获真实渲染输入；输出、深度、运动与 jitter 记录位于 `Library/Streamline/evidence/<GraphicsDeviceType>/urp-images`。用例结束恢复原管线，不保存测试对象。当前用例通过状态以模块文档为准。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.CameraMovementAndCutProduceValidTemporalInputs`：连续移动相机并在第 12 帧主动切镜，每模式运行 24 帧；记录相机/物体运动、重置帧，以及第 11/12/13 帧的输入和输出图像，位于 `Library/Streamline/evidence/<GraphicsDeviceType>/camera-images`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.CameraRotationProducesValidTemporalInputs`：每模式 24 帧，同时改变俯仰、偏航和滚转；检查旋转产生的静态几何运动及移动物体运动，保存第 11/12/13 帧图像，位于 `Library/Streamline/evidence/<GraphicsDeviceType>/rotation-images`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.TransparentSurfaceChangesReachDlssOutput`：URP Alpha 混合面片按 `1 → 0.45 → 0` 切换，每状态 8 帧；用不透明/隐藏状态推算半透明输入，比较同一区域的 DLSS 输出，证据位于 `Library/Streamline/evidence/<GraphicsDeviceType>/transparency-images`。该用例验证静态面片透明度变化，不覆盖运动粒子、折射或边缘拖影。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.SameViewportHandlesModeAndOutputResize`：同一相机、历史对象和原生 viewport 执行 SR → DLAA → SR，输出依次 768×432、960×540、1152×648；每段捕获第 0/1/7 帧，位于 `Library/Streamline/evidence/<GraphicsDeviceType>/resize-images`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.DlssOutputReachesCameraTarget`：显式将输出交回 URP，使用无色调映射的临时 Volume 比较最终相机目标与 DLSS 输出，位于 `Library/Streamline/evidence/<GraphicsDeviceType>/camera-output`。普通项目的 Neutral 色调映射会改变颜色，不应直接以 HDR 原始输出作逐像素相等比较。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.SustainedFramesRecordDlssGpuTiming`：每模式连续渲染 240 帧，物体往返运动；剔除前 64 帧，记录 `sustained-images/*-gpu.json` 中的原始 GPU 毫秒样本、均值与 P95。只计原生 DLSS 事件包围的命令，不含前面的输入复制与后面的读回，不能当作整帧收益或发布性能。GPU Recorder 不可用、样本为零或不足 100 个会失败，不以 CPU 或 Test Runner 时间代替。

GPU 采样使用 [CustomSampler.Create 的 collectGpuData](https://docs.unity3d.com/cn/6000.0/ScriptReference/Profiling.CustomSampler.Create.html) 与命令缓冲 Begin/EndSample。Recorder 的 GPU 数据延迟三帧，见[官方说明](https://docs.unity3d.com/cn/6000.0/ScriptReference/Profiling.Recorder-gpuElapsedNanoseconds.html)；用例预热包含这一延迟，每帧仅采纳一个非零 GPU block。

查询配置时调用 `StreamlineDlssValidation.TryEnqueueOptimalSettings`，提交命令并通过命令流回读确认事件完成，再以返回的请求编号调用 `TryGetOptimalSettings`。查询在渲染线程初始化/绑定 SDK，与图像执行共享结果槽；读出结果之前不要提交另一个查询或图像请求。失败返回诊断，不回退到硬编码比例。改变已用 viewport 的模式/尺寸仍须先按下文释放。

对 URP 配置调用 `settings.TryGetUrpRenderScale` 取得统一比例及实际输入尺寸；仅应用到宿主拥有的独立管线配置。URP 17.3 会分别截断输出宽高与比例的乘积，推荐尺寸可能不能精确表示，例如 1280×720 的 Balanced 推荐 742×418，实际可取 743×418。必须使用返回的实际尺寸分配输入，并在捕获时再次校验；换算失败则保留原渲染路径，不强行创建越界输入。

图像验证会话先等待 GPU 完成，再结束原生会话并释放资源。测试通过不表示项目主相机已经启用 DLSS。每次只运行要验证的具体方法，输出文件需结合对应任务结果判断，失败运行可能只更新部分模式文件。

新版桥接会报告清理阶段的 `releaseResult`、`shutdownResult` 和 `sdkStillInitialized`；图像测试将清理报告保存到 `Library/Streamline/evidence/<GraphicsDeviceType>/cleanup` 并检查 SDK 返回值。SDK 日志位于 `Assets/Plugins/Streamline/x86_64/Streamline~/logs/sl.log`，该目录被 Unity 和 Git 忽略。命令流回读确认 GPU 完成后，仍须单独检查 SDK 清理结果。目录中的 `<GraphicsDeviceType>` 为 `Direct3D12` 或 `Vulkan`。

当前只推进 Editor 验证。Player 构建、交换链与呈现转发在 FG/发布阶段另行验收，不在原生探测测试中自动构建 Player。

Vulkan 验证需先保存并关闭 Editor，再使用 `-force-vulkan -streamline-interpose` 启动当前项目。确认 UnitySkills 新实例的项目路径和端口后，运行 `NativeTextureAccessPreservesGpuWrittenPixels`、`ProbeOnRenderThreadReportsDeviceWithoutEnablingEffects` 和 `VulkanPresentInterceptionAdvances`。前两项也适用于 DX12，最后一项仅验证 Vulkan。

原生探测通过同一命令流中的 `RequestAsyncReadback` 确认完成；不能在 `supportsAsyncCompute=false` 时查询 AsyncQueue fence 的 `passed`。Unity 的检查逻辑见[官方 GraphicsFence 源码](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/GraphicsFence.bindings.cs)。

这些是显式选择的原生集成验证，需要先部署 SDK；默认不扩展到项目全量测试。不要手动将开发版 DLL 复制到 Player 或修改 PackageCache。

## 运行验证

1. 按项目路径发现 UnitySkills 实例，核对项目名与版本。
2. 分别以 D3D12 和 Vulkan 启动，记录 GPU、驱动、SDK 版本及功能查询结果。
3. 检查原生初始化、设备访问与清理，确认无图形验证错误。
4. 使用独立验证场景检查颜色、深度、运动矢量、曝光和历史，再验证最终画面。
5. 验证 Overlay UI、场景切换、Resize、暂停恢复、设置切换和异常回退。
6. 使用独立 IL2CPP Player 复测；FG/MFG 记录实际测试 GPU，不能以 RTX 3050 的不支持结果代替成功验收。

## 证据与排障

每次记录版本、API、硬件、操作步骤、实际输出及未验证项。错误要区分缺失库、签名无效、初始化过晚、设备/驱动不支持、公开接口限制和渲染输入错误。

编译成功不能证明时域画面正确。自动化验证使用项目现有 Unity Test Runner，视觉结果需要场景运行证据。

时域验证必须让相机连续渲染。EditMode 的帧计数、单次 RenderRequest 以及每帧反复启停相机不足以证明 previous model matrices 正确。URP 17.3 RenderGraph 下旧兼容 `GetGPUProjectionMatrix(NoJitter)` 可能只返回零矩阵；验证相机通过公开投影接口构造参数，并以可逆性断言阻止无效矩阵进入 SDK。

后续建立 Player 验证时必须使用明确保存的场景资产；空 scenes 数组会尝试构建 Test Runner 的未命名场景，引发 `Cannot build untitled scene`。不要在仍然阻塞的构建期间启动第二次构建。

若 HybridCLR 报包与安装版本不一致，先使用其 Installer 更新**项目本地**运行库，再执行既有 Generate/All。2026-09-22 已完成 8.12.0 → 8.14.1 修复，旧本地工具链保存在 `Library/Streamline/hybridclr-backup`；Unity 安装目录未被修改。
