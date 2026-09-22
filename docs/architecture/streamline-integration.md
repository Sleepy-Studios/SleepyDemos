# Streamline / DLSS 接入设计

## 目标与边界

面向 Windows x64，在 Unity 6000.3.15f1 / URP 17.3.0 中通过 Streamline 接入 DLSS SR、DLAA、Reflex 和 FG/MFG，分别验证 D3D12 与 Vulkan。RR 仅定义输入契约，等待真实光追信号接入。

使用 Unity 公开 Native Rendering Plugin 和渲染扩展接口。单个功能受接口或硬件限制时保留可复现证据，不将“DLL 已加载”“硬件支持”或接口占位当作功能已运行。

## 分层

- Core.Runtime 负责能力、设置、原生桥接、渲染与生命周期；Core.Editor 负责配置、构建与诊断。
- 原生源码位于 `Native/Streamline`，构建与依赖准备脚本位于 `scripts/streamline`。编译产物与 SDK 缓存位于忽略目录，不手动维护生成的工程文件。
- Hotfix 仅消费公开能力和设置接口。原生二进制随 Player 发布，不经 HybridCLR 热更。
- 验证 Demo 只提供场景、交互和比较入口，不持有全局设备状态。

## 关键约束

1. Streamline 初始化早于需要拦截的图形 API；先验证 Unity 的公开加载时机，再决定哪些插件可启用。
2. 两个后端共享帧编号、设置和能力语义，但分别管理 D3D12 资源状态与 Vulkan 图像布局、队列及同步。
3. DLSS 需要真实的低分辨率颜色、深度、运动矢量及目标分辨率输出。DLAA 使用目标分辨率输入。相机历史在切换、Resize、暂停恢复和失效时重置。
4. Overlay Camera Stack 单独验收；UI 应以目标分辨率合成，不能混入时域历史。不能正确处理的组合回退。
5. FG/MFG 需要可控制的呈现链路和在呈现期间有效的输入；获取交换链指针本身不能证明集成成功。
6. 功能设置必须返回实际状态及不可用原因，不将用户请求直接作为生效值。

相机时域常量统一由 `StreamlineCameraHistory` 管理。每个相机持有独立实例，投影/尺寸/模式/viewport 或帧连续性失效时自动重置；宿主负责在主动切镜、停用和卸载时重置。资源寿命由渲染会话管理，历史对象不持有纹理或原生 SDK 资源。

## 阶段路线

| 阶段 | 交付与出口 |
|---|---|
| G0 | 文档、Unity/URP 与 SDK 基线、可复现原生工具链 |
| G1 | Editor 内原生设备、命令执行与 SDK 绑定验证；交换链记录为待验证项 |
| G2 | Editor 内 SR/DLAA 独立画面闭环，输入与耗时证据；先 DX12 再 Vulkan |
| G3 | Editor 内 UI、Hub/Demo、生命周期和异常回退 |
| G4 | Reflex/PCL 时序、低延迟控制与验证 |
| G5 | Player 交换链验证、FG/MFG 呈现、窗口变化与支持硬件验收 |
| G6 | RR 输入契约、IL2CPP 发布验证、最终矩阵和交付文档 |

阶段完成状态与简明验证结果统一维护在[模块文档](../modules/streamline.md)，操作见[接入手册](../runbooks/use-streamline.md)。截图、日志与原始测试报告只作临时开发材料，放在 Git 忽略的 Library 目录，阶段结束可删除，不再提交。

每个阶段通过验收并同步文档后，单独进行本地 Git 提交，不推送远端。HybridCLR 等独立环境变更另行提交。

当前用户体验交付点：G2 完成后先本地提交；随后接入正式 `AppEntrance → Hub → DLSS Demo`，在 Demo 内提供关闭、各 SR 质量档和 DLAA 切换面板。完成该体验版本、更新进度并再次本地提交后暂停 Goal，等待用户实际体验，再决定后续推进。不在等待体验前继续实施 Reflex、FG/MFG 或发布验证。

2026-09-22 实施顺序调整：先完成 Editor 原生接入、SR/DLAA 和画面验证。Player 构建不作为 G1/G2 的门槛，交换链在 FG 或发布验证阶段处理。Editor 画面证据不等同于发布呈现链路验收。

## 官方依据

- [Streamline v2.14.1](https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.1)
- [编程指南](https://github.com/NVIDIA-RTX/Streamline/blob/v2.14.1/docs/ProgrammingGuide.md)
- [手动图形接口集成](https://github.com/NVIDIA-RTX/Streamline/blob/v2.14.1/docs/ProgrammingGuideManualHooking.md)
- [DLSS](https://github.com/NVIDIA-RTX/Streamline/blob/v2.14.1/docs/ProgrammingGuideDLSS.md)
- [FG](https://github.com/NVIDIA-RTX/Streamline/blob/v2.14.1/docs/ProgrammingGuideDLSS_G.md)

每次 SDK 升级重新核对输入、生命周期及二进制依赖，不直接追踪 main。
