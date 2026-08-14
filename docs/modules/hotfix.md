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
3. 加载到的热更程序集会交给 `UITypeReflection.Init / Scan`，供后续 Hotfix View 注册使用。
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

- Editor 中能打开 `Tools/UI Framework/Hotfix Build`。
- 启动时 `HybridMetadataSystem` 和 `HotfixAssemblySystem` 不报错。
- 热更程序集加载后，Hotfix View 能被 `UITypeReflection` 扫描到。
- `Tests.Module.HotfixAssemblyDefinitionFilterTests` 和 `Tests.Module.TestAssemblyBoundaryTests` 全部通过。
- `HotfixConfig.HotfixAssemblies` 当前只包含 `Hotfix.dll`，热更代码目录不存在 `.Tests.dll`。
- 如果使用本地 Mock Server，资源包 URL 能访问并被 YooAsset 正常下载。
