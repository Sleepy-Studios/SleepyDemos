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

本轮最终首包为 `2026-10-12-165911`，同步最新 Hotfix DLL 与公共字体资源；128项包文件与StreamingAssets哈希一致。正式AppEntrance、五个设置分页与四Demo实际往返70d5d059为1/1通过，配置模式和未保存选择已恢复，窗口自动更新的最新首包路径保留。当前Core.Runtime生命周期修复通过Editor源码运行；AOT裁剪快照不作为这次修改的独立Player证据。详细范围见[实施记录](../agent/settings-goal-progress.md)。

### 2026-09-22 本地生成基线

- 当前 HybridCLR 包和项目本地 `libil2cpp-version.txt` 均为 8.14.1；旧本地运行库与源码缓存备份在 `Library/Streamline/hybridclr-backup`，未修改 Unity 安装目录。
- 已执行既有 Generate/All，更新 `Assets/HybridCLRGenerate/link.xml` 与 `AOTGenericReferences.cs`。AOT 列表补入 `Unity.InputSystem.dll`，并同步保存 AOT/Hotfix 程序集快照。
- `Codes/Aot` 中 6 个文件的 SHA-256 均与配置的 `AssembliesPostIl2CppStrip/StandaloneWindows64` 来源一致；`Codes/Hotfix/Hotfix.dll.bytes` 与 `HotUpdateDlls/StandaloneWindows64/Hotfix.dll` 一致。
- 本地 Bundle 路径当前使用 `DefaultPackage/Simulate`。这些生成物是当前开发快照；后续源码变更后发布前仍须重新生成并验证 IL2CPP Player，不能将快照提交当成发布验收。
- `FlowLayoutGroup.OnValidate` 增加 Editor 条件编译，避免 Player 覆写不存在的 uGUI 方法；对应横向、纵向两个精确 EditMode 测试均通过。

### 2026-10-12 公共设置本地生成历史快照

- 使用 `Tools/一键打包工具`（同 `Tools/UI Framework/Hotfix Build`）现有的一键编译并同步流程；同步时 Hotfix 与十项 AOT 来源/目标 SHA256 核对一致，列表新增 Newtonsoft.Json、SleepyStudios.LoopScroll、URP Core 和 JSONSerializeModule。
- 通过现有 YooAsset Viewer 的 ScriptableBuildPipeline 构建 `2026-10-12-144329`，125 个 Bundle；StreamingAssets 的 128 个对应文件哈希一致，HotfixConfig 保存最新本地路径。仅作为当前开发快照，不等同 Player 验收。
- Windows IL2CPP Player `c6a073ba` 在 C++ 阶段因用户级 CMD AutoRun 中的相对 `chcp` 命令失败。当时系统配置由用户自行处理，未修改注册表或 PATH，也未将失败结果计为通过。失败构建后 PostIl2CppStrip 来源缓存为空，目标快照仍保留；后续正式发布重新同步 AOT 时须先成功重生成来源，不能拿旧 StrippedAOTAssembly2 缓存代替。本次 Editor Offline 验收不以重打 Player 或重生成裁剪来源为前置。
- 用户随后明确授权主代理修复该系统配置：先备份 `HKCU\Software\Microsoft\Command Processor\AutoRun` 的原值、值类型及存在状态，再仅将其中相对 `chcp` 改为实际 Windows `System32\chcp.com` 的绝对路径；不改 PATH、其他值或系统级键。已按授权完成备份和修复，AutoRun原类型String保持，回读为`"C:\Windows\System32\chcp.com" 65001 >nul`；其余注册表值比较无变化，不含System32的受限子进程PATH下CMD退出码0、无错误。备份与回读证据位于`Library/GlobalSettings/registry-backup`；不为验证此修复重新构建 Player。旧 Player 失败记录继续保留为历史。

### 接入检查

- Editor 中能打开 `Tools/UI Framework/Hotfix Build`。
- 启动时 `HybridMetadataSystem` 和 `HotfixAssemblySystem` 不报错。
- 热更程序集加载后，具体 View 自动按类名与 UIBind 别名进入索引，新增页面无需手写清单；未知名称只查缓存，不扫描其它程序集兜底。
- `Tests.Module.HotfixAssemblyDefinitionFilterTests` 和 `Tests.Module.TestAssemblyBoundaryTests` 全部通过。
- `HotfixConfig.HotfixAssemblies` 当前只包含 `Hotfix.dll`，热更代码目录不存在 `.Tests.dll`。
- 如果使用本地 Mock Server，资源包 URL 能访问并被 YooAsset 正常下载。
