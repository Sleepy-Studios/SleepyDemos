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
- `Assets/Scripts/Core/Editor/Hotfix/GitHubPagesDeployment.cs`

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

“一键编译并同步”依次执行 HybridCLR 全量生成与编译、刷新 AOT/热更程序集列表、AOT 剥离，再把剥离后的 AOT DLL 与热更 DLL 同步为资源目录中的 `.dll.bytes`。单独的“同步当前 DLL 到资源目录”仍直接使用未剥离的 AOT 源目录。

构建平台以 `EditorUserBuildSettings.activeBuildTarget` 为唯一来源，窗口保留 PC/Android 按钮并显示当前平台的选中状态。点击另一平台只提示先到 `File > Build Profiles` 切换，不直接切换 Unity 平台或修改路径。打开窗口、切换配置或检测到 Unity 平台变化时，自动同步 AOT、热更 DLL 源目录和当前平台最新的 Bundle 目录；手动选择的路径可在本轮操作中继续使用，再次同步平台时会恢复该平台的默认路径。新建配置也使用当前构建平台。HybridCLR 编译与 YooAsset 资源打包应使用同一平台；SSH 部署保留 Windows64 的 `/PC` 目录，Android 使用 `/Android`。

### GitHub Pages 资源环境

`HotfixBuildWindow` 提供 `SSH` / `GitHub Pages` 上传方式。Pages 的本地仓库路径、网站根 URL、Unity 项目目录名由 Core.Editor 使用 `EditorPrefs` 按 Unity 项目和配置保存；`HotfixConfig` 不增加部署字段，只保留运行时资源地址等现有配置。窗口负责设置、当前平台展示、URL 同步、按钮与清空确认；`GitHubPagesDeployment` 负责目标路径和发布白名单、仓库与登录校验、增量复制、当前平台清理，以及异步 Git 提交和推送，不参与运行时资源加载。

固定目标为 `unity/<项目>/hotupdate/<当前平台>/`：`StandaloneWindows64` 映射 `PC`，`Android` 映射 `Android`，其他平台沿用 `BuildTarget` 名称。Pages 模式从网站根 URL、项目和平台自动生成 `RemoteBaseServerURL`；关闭本地模拟后 `BaseServerURL` 使用该地址，SSH 模式仍保留手工 URL。当前 PC 地址为 `https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/PC`，日常 `EditorSimulateMode` 不变。

Pages 默认只复制变更的下载文件并保留旧包；独立的“清空当前平台目录”操作经确认后删除目标文件并保留 `.gitkeep`。两种操作均执行提交、推送流程，只作用于当前项目和平台；含子目录或链接的目标拒绝操作。本地仓库须为干净的 `main`，远端不能领先或分歧；仅允许当前平台资源与站点骨架存在未推送提交，其他本地待发布文件会被拒绝，工具不自动合并。

公开仓库 `H-Bole/H-Bole.io` 已开启 `main` 根目录 Pages。2026-10-14 首次发布 Android 版本 `2026-10-14-86`，Pages 部署成功，全部 129 个下载文件的 SHA256 与源文件一致；同包再次增量上传未产生新提交。PC 仍只有 `.gitkeep`，此次未清空目录。首次发布核对时 Android 源配置为 HostPlayMode、两个远端 URL 为空；随后已修正源工作树 Pages 设置，两个 URL 均指向 `https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/Android`，保留 HostPlayMode 与已有构建。APK 未重建、设备实际热更未验证，尚未核查 APK 内配置；源配置修正和上传成功不代表已安装包连接 Pages。发布记录、窗口步骤及验证边界见 [用 GitHub Pages 发布热更资源](../runbooks/deploy-github-pages-hotupdate.md)。其他 Unity 项目并列放在 `unity/` 下，网站根目录可容纳其他用途，不预建未知用途目录。

## 边界规则

- Hotfix 业务代码不要直接操作 HybridCLR 或 YooAsset 的底层句柄。
- 热更程序集列表、AOT 元数据列表优先通过 `HotfixConfig` 管理。
- HybridCLR 上游的 `hotUpdate*` 配置键与 `HybridCLRData/HotUpdateDlls` 生成目录是插件固定约定，不属于项目自有命名，禁止改写。
- 自动填写热更程序集时统一过滤 Test Assembly、`.Tests` 后缀和 Editor-only asmdef。
- 测试 asmdef 禁止放在 Hotfix 目录；框架相关测试统一放到对应 TestMode 的 `Assets/Scripts/Tests/.../Module/<模块>`，Demo 专属测试放到 `.../Demo/<Demo>`。
- 热更 DLL 等构建产物位于 `LoadResources/Codes/**`，资源命名校验会跳过该目录。
- 修改热更构建窗口、热更配置字段、启动期热更装配顺序时，必须同步更新本文和 [启动与热更流程](../architecture/startup-flow.md)。

## 验证重点

### Editor 运行验证

默认模式遵循 [根验证规则](../../AGENTS.md#验证与-unity-editor)。普通功能验证使用 `HotfixConfig.PlayMode = EditorSimulateMode`，通过正式 `AppEntrance` 启动，检查 Hub 与受影响 Demo 的资源加载和交互。复用 Unity Editor 已编译的代码及开发资源，无需重编译 DLL 或构建首包；临时改变配置时记录原值并在结束或失败后恢复。

### 按明确要求执行 Offline 首包验收

只有用户明确要求 Offline 验收时，才使用既有 `Hotfix Build` 和 YooAsset 打包入口执行以下流程，不新增启动或验证框架。

- 验收前记录正式 `Assets/LoadResources/Config/HotfixConfig.asset` 的原状态，包括资源模式、包名、启动场景与路径；仅临时切换到 `OfflinePlayMode`，保留原配置以供结束或失败后完整恢复。
- 使用本轮最新源码与资源生成的首包，核对实际 `StreamingAssets` 中的版本、清单与 Bundle / DLL 资源；旧版本或 EditorSimulate 的开发目录不能代替本轮首包。通过正式 `Assets/Scenes/AppEntrance.unity` 启动，核对 Offline 包初始化、Hub 与目标 Demo 的实际资源加载和交互，结束后恢复原配置。
- `YooAssetInitializeSystem` 将配置模式交给资源服务；`YooAssetResourceSystem` 的 Offline 分支使用内置文件系统。`HotfixAssemblyLoader` 在 `UNITY_EDITOR` 下优先复用已加载的 Hotfix 程序集，因此此验收覆盖正式首包资源与 Editor 启动流程，不能证明首包 DLL 的独立加载、IL2CPP 编译或裁剪结果。
- 报告分别记录首包构建与文件核对、Editor Offline 实际运行、配置恢复及未完成项。IL2CPP 编译 / 裁剪、独立 Player 和原生发布验证后置，真实手柄与触屏仍待实际设备验收；不将这些后置项目记录为本轮已通过。

已记录的公共设置首包结果与验证限制见 [公共设置与表现交付状态](settings-presentation.md#交付状态与验证边界)。旧生成基线、Player 构建失败及机器修复顺序见 [历史实施记录](../agent/archive/hotfix-build-history.md)，本模块不把历史环境状态作为实时结论。

### 接入检查

- Editor 中能打开 `Tools/UI Framework/Hotfix Build`。
- Windows64 与 Android 下，窗口平台显示和 AOT、热更 DLL、Bundle 路径均与 Unity 当前构建平台一致；重新打开窗口不会回到固定 PC 状态。
- 点击非当前平台按钮只出现切换提示，选中状态和路径保持当前平台；在 Build Profiles 完成切换后自动更新。
- 启动时 `HybridMetadataSystem` 和 `HotfixAssemblySystem` 不报错。
- 热更程序集加载后，具体 View 自动按类名与 UIBind 别名进入索引，新增页面无需手写清单；未知名称只查缓存，不扫描其它程序集兜底。
- `Tests.Module.HotfixAssemblyDefinitionFilterTests` 和 `Tests.Module.TestAssemblyBoundaryTests` 全部通过。
- `HotfixConfig.HotfixAssemblies` 当前只包含 `Hotfix.dll`，热更代码目录不存在 `.Tests.dll`。
- 如果使用本地 Mock Server，资源包 URL 能访问并被 YooAsset 正常下载。
