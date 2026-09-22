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
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.QualityAndDlaaWriteFiniteNonBlankGpuImages`：DX12 原生合成输入验证，图像位于 `Library/Streamline/evidence/images`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.UrpSceneInputsProduceQualityAndDlaaImages`：临时克隆 URP 配置并创建几何场景，捕获真实渲染输入；输出、深度、运动与 jitter 记录位于 `Library/Streamline/evidence/urp-images`。用例结束恢复原管线，不保存测试对象。当前用例通过状态以模块文档为准。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.CameraMovementAndCutProduceValidTemporalInputs`：连续移动相机并在第 12 帧主动切镜，每模式运行 24 帧；记录相机/物体运动、重置帧，以及第 11/12/13 帧的输入和输出图像，位于 `Library/Streamline/evidence/camera-images`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.SameViewportHandlesModeAndOutputResize`：同一相机、历史对象和原生 viewport 执行 SR → DLAA → SR，输出依次 768×432、960×540、1152×648；每段捕获第 0/1/7 帧，位于 `Library/Streamline/evidence/resize-images`。
- **PlayMode** `Tests.Module.StreamlineDlssImageTests.DlssOutputReachesCameraTarget`：显式将输出交回 URP，使用无色调映射的临时 Volume 比较最终相机目标与 DLSS 输出，位于 `Library/Streamline/evidence/camera-output`。普通项目的 Neutral 色调映射会改变颜色，不应直接以 HDR 原始输出作逐像素相等比较。

图像验证会话先等待 GPU 完成，再结束原生会话并释放资源。测试通过不表示项目主相机已经启用 DLSS。每次只运行要验证的具体方法，输出文件需结合对应任务结果判断，失败运行可能只更新部分模式文件。

新版桥接会报告清理阶段的 `releaseResult`、`shutdownResult` 和 `sdkStillInitialized`；图像测试将清理报告保存到 `Library/Streamline/evidence/cleanup` 并检查 SDK 返回值。SDK 日志位于 `Assets/Plugins/Streamline/x86_64/Streamline~/logs/sl.log`，该目录被 Unity 和 Git 忽略。GPU fence 完成与 SDK 清理成功必须分别确认。

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
