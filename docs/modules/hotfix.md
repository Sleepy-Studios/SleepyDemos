# Hotfix 热更新模块

## 负责什么

热更新模块负责启动期 AOT 元数据补充、热更程序集加载，以及编辑器侧热更构建和本地资源调试。它属于 Core 底座能力，不承载具体 Hotfix 业务逻辑。

## 代码位置

运行时：

- `Assets/Scripts/Core/Runtime/Hotfix/HotfixConfig.cs`
- `Assets/Scripts/Core/Runtime/Hotfix/HybridAotAssemblyLoader.cs`
- `Assets/Scripts/Core/Runtime/Hotfix/HotfixAssemblyLoader.cs`
- `Assets/Scripts/Core/Runtime/Startup/Systems/HybridMetadataSystem.cs`
- `Assets/Scripts/Core/Runtime/Startup/Systems/HotfixAssemblySystem.cs`

编辑器：

- `Assets/Scripts/Core/Editor/Hotfix/HotfixBuildWindow.cs`
- `Assets/Scripts/Core/Editor/Hotfix/HotfixAssemblyDefinitionFilter.cs`
- `Assets/Scripts/Core/Editor/Hotfix/LocalBundleHttpServer.cs`

配置与资源：

- 默认配置资产：`Assets/LoadResources/Config/HotfixConfig.asset`
- 热更 DLL 构建产物：`Assets/LoadResources/Codes/**`
- SSH 私钥默认位置：`Assets/Settings/Hotfix/key`

## 启动链路

热更新运行在 `BeforeHotfixStartupState` 阶段：

1. `HybridMetadataSystem` 读取 `HotfixConfig.AotAssemblies`，通过 `HybridAotAssemblyLoader` 补充 HybridCLR 泛型元数据。
2. `HotfixAssemblySystem` 读取 `HotfixConfig.HotfixAssemblies`，通过 `HotfixAssemblyLoader` 加载热更程序集。
3. `UITypeReflection` 初始化已加载 Hotfix 程序集的页面索引，并扫描 Core 与启动宿主程序集；HotfixEntry 补充扫描自身程序集，重复调用不重复枚举类型。
4. `HotfixEntrySystem` 才会进入 Hotfix 业务入口。

## 编辑器入口

| 菜单路径 | 说明 |
|----------|------|
| `Tools/UI Framework/Hotfix Build` | 热更构建主窗口：HybridCLR 生成、YooAsset 构建、本地 Mock Server、远端部署 |
| `Tools/一键打包工具` | 同一窗口的中文入口 |

本地 Bundle HTTP 服务通过 `HotfixBuildWindow` 的 Mock Server 区启停，底层实现为 `LocalBundleHttpServer`。

## 边界规则

- Hotfix 业务代码不要直接操作 HybridCLR 或 YooAsset 的底层句柄。
- 热更程序集列表、AOT 元数据列表优先通过 `HotfixConfig` 管理。
- HybridCLR 上游的 `hotUpdate*` 配置键与 `HybridCLRData/HotUpdateDlls` 生成目录是插件固定约定，不属于项目自有命名，禁止改写。
- 自动填写热更程序集时统一过滤 Test Assembly、`.Tests` 后缀和 Editor-only asmdef。
- 测试 asmdef 禁止放在 Hotfix 目录；框架相关测试统一放到对应 TestMode 的 `Assets/Scripts/Tests/.../Module/<模块>`，Demo 专属测试放到 `.../Demo/<Demo>`。
- 热更 DLL 等构建产物位于 `LoadResources/Codes/**`，资源命名校验会跳过该目录。
- 修改热更构建窗口、热更配置字段、启动期热更装配顺序时，必须同步更新本文和 [启动与热更流程](../architecture/startup-flow.md)。

## 验证重点

### Editor Offline 实际首包验收

用户已将本任务及后续功能任务原定 Windows Player 最终验收调整为 Editor Offline Play Mode 与实际首包验收，默认不构建 Player。继续使用既有 `Hotfix Build` 和 YooAsset 打包入口，不新增启动或验证框架。

- 验收前记录正式 `Assets/LoadResources/Config/HotfixConfig.asset` 的原状态，包括资源模式、包名、启动场景与路径；仅临时切换到 `OfflinePlayMode`，保留原配置以供结束或失败后完整恢复。
- 使用本轮最新源码与资源生成的首包，核对实际 `StreamingAssets` 中的版本、清单与 Bundle / DLL 资源；旧版本或 EditorSimulate 的开发目录不能代替本轮首包。通过正式 `Assets/Scenes/AppEntrance.unity` 启动，核对 Offline 包初始化、Hub 与目标 Demo 的实际资源加载和交互，结束后恢复原配置。
- `YooAssetInitializeSystem` 将配置模式交给资源服务；`YooAssetResourceSystem` 的 Offline 分支使用内置文件系统。`HotfixAssemblyLoader` 在 `UNITY_EDITOR` 下优先复用已加载的 Hotfix 程序集，因此此验收覆盖正式首包资源与 Editor 启动流程，不能证明首包 DLL 的独立加载、IL2CPP 编译或裁剪结果。
- 报告分别记录首包构建与文件核对、Editor Offline 实际运行、配置恢复及未完成项。IL2CPP 编译 / 裁剪、独立 Player 和原生发布验证后置，真实手柄与触屏仍待实际设备验收；不将这些后置项目记录为本轮已通过。

已记录的公共设置首包结果与验证限制见 [公共设置与表现交付状态](settings-presentation.md#交付状态与验证边界)。旧生成基线、Player 构建失败及机器修复顺序见 [历史实施记录](../agent/archive/hotfix-build-history.md)，本模块不把历史环境状态作为实时结论。

### 接入检查

- Editor 中能打开 `Tools/UI Framework/Hotfix Build`。
- 启动时 `HybridMetadataSystem` 和 `HotfixAssemblySystem` 不报错。
- 热更程序集加载后，具体 View 自动按类名与 UIBind 别名进入索引，新增页面无需手写清单；未知名称只查缓存，不扫描其它程序集兜底。
- `Tests.Module.HotfixAssemblyDefinitionFilterTests` 和 `Tests.Module.TestAssemblyBoundaryTests` 全部通过。
- `HotfixConfig.HotfixAssemblies` 当前只包含 `Hotfix.dll`，热更代码目录不存在 `.Tests.dll`。
- 如果使用本地 Mock Server，资源包 URL 能访问并被 YooAsset 正常下载。
