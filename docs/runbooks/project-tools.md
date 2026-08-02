# 项目工具与 Agent 技能总览

本文档汇总 SleepyDemos 仓库内**可直接使用**的 Unity 编辑器工具、第三方插件入口，以及供 Codex / Claude / Cursor 等 Agent 使用的**仓库内** Skill 与 Rule。新增菜单或 Skill 时请同步更新本页。

## 如何判断工具归属

| 类型 | 代码/配置位置 | 文档落点 |
|------|----------------|----------|
| 项目自研编辑器工具 | `Assets/Scripts/Core/Editor` | 本文 + 必要时单独 `runbooks/*.md` |
| 仓库内插件（随项目提交） | `Assets/Plugins/` | 本文「第三方插件」 |
| UPM 开发依赖 | `Packages/manifest.json` | 本文「UPM 包」+ 上游链接 |
| Agent Skill / Rule | `.claude/`、`.codex/`、`.cursor/` | 本文「Agent」+ [项目 Skill 入口](../agent/skills.md) |

---

## 一、项目自研工具（Core.Editor）

实现目录：`Assets/Scripts/Core/Editor/`。模块说明见 [Core.Editor](../modules/core-editor.md)。

### 1. 热更与打包

| 菜单路径 | 说明 | 源码 |
|----------|------|------|
| `Tools/UI Framework/Hotfix Build` | 热更构建主窗口：HybridCLR 生成、YooAsset 构建、本地 Mock 服务器、远端部署等 | `Hotfix/HotfixBuildWindow.cs` |
| `Tools/一键打包工具` | 与上一项同一窗口（中文入口） | 同上 |

窗口内嵌能力（无独立菜单）：

- **本地 Bundle HTTP 服务**：`Hotfix/LocalBundleHttpServer.cs`，在 Hotfix 窗口「Mock Server」区启停，用于本机调试资源包。
- **配置资产**：默认 `Assets/LoadResources/Config/HotfixConfig.asset`。
- **SSH 私钥**：默认 `Assets/Settings/Hotfix/key`（不进 YooAsset 采集，勿放回 `LoadResources/Config`）。

### 2. UI 绑定（MvcBind）

| 菜单路径 | 说明 | 源码 |
|----------|------|------|
| `Tools/UI Framework/MvcBind` | View / Prefab 绑定扫描与编辑；Prefab 模式下可打开组件绑定子窗口 | `MvcBind/MvcBindWindow.cs`、`MvcPrefabScanner.cs`（`MvcComponentBindWindow`） |
| `Tools/SleepyDemos/UI Framework Validation/Build Validation Prefabs` | 生成基础 UI 框架验证预制体，并复用 MvcBind 生成 View Component | `UIFrameworkValidation/UIFrameworkValidationPrefabBuilder.cs` |
| `Tools/SleepyDemos/UI Framework Validation/Validate Generated Prefabs` | 检查验证预制体绑定数量，并自动覆盖 `UITab`、`UIBtnSwitch`、`UIDropdown`、`ViewTab`、`AccordionTab`、`AccordionViewTab` 基础交互 | 同上 |

使用要点：

- 在 Prefab Mode 中新增 UI 节点后，Hierarchy 右侧的绑定勾选和组件下拉会自动延迟刷新。
- 点击 `Create` 会先把 `ComponentItemIndex` 绑定保存到当前 Prefab，再生成 View 脚本，避免脚本重编译导致绑定未落盘。
- 手动点击 Prefab Mode 的 `Save` 时，如果当前 Hierarchy 已有勾选组件，也会按当前选择同步绑定并重新生成脚本。
- 基础 UI 验证预制体位于 `Assets/LoadResources/UI/UIFrameworkValidation/`，验证入口从 `MainMenuView` 的「基础 UI 验证」按钮打开。
- `ViewTab` 验证使用 `UITab + ViewRoot/Parent` 结构；`UIDropdown` 可滚动验证使用外层 `ScrollRect / Content`；手风琴模板位于 `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Accordion/`。

### 3. TextMesh Pro 字体

| 菜单路径 | 说明 | 源码 / 文档 |
|----------|------|-------------|
| `Tools/SleepyDemos/TextMesh Pro/Font Builder` | 从 Source 字体与字符集生成 TMP 资源 | `TextMeshPro/TMPFontBuilderWindow.cs` |
| `Tools/SleepyDemos/TextMesh Pro/Build Default CN EN Fonts` | 一键构建默认中英字体 | 同上 |
| — | 详细步骤 | [TMP 字体生产流程](./tmp-font-workflow.md) |

### 4. 资源命名校验

| 菜单路径 | 说明 | 源码 / 文档 |
|----------|------|-------------|
| `Tools/SleepyDemos/校验 LoadResources 资源命名` | 扫描 `Assets/LoadResources` 是否符合目录矩阵、扩展名绑定与三级命名规则 | `AssetNaming/LoadResourcesAssetNamingValidator.cs`、`AssetNaming/LoadResourcesNamingSpec.cs` |
| `Tools/SleepyDemos/同步 LoadResources 资产 Label` | 扫描既有 `Assets/LoadResources` 资产，按目录矩阵补齐 Unity Asset Label；会移除过期托管标签并保留人工标签 | `AssetNaming/LoadResourcesAssetNamingPostprocessor.cs`、`AssetNaming/LoadResourcesNamingSpec.cs` |
| `Tools/SleepyDemos/把 LoadResources 下的目录加入 YooAsset 打包采集配置` | 把 Demos/Art/Audio/VFX/Scenes/Config/Fonts 等公共目录写入 YooAsset 采集配置并统一全路径地址；拉分支或 Setting 被改乱时点一次即可 | `AssetNaming/LoadResourcesYooAssetCollectorSetup.cs` |
| — | 规则说明 | [资源命名规范](../architecture/asset-naming.md) |

新导入到 `LoadResources` 的资产若违反规则会在 Console 输出 Error；导入、移动或 Reimport 时会自动同步 Unity Asset Label。从 git 拉取或批量改名后，可手动运行 Label 同步菜单补齐既有资产。

### 5. Luban 配置

| 菜单路径 | 说明 | 源码 / 文档 |
|----------|------|-------------|
| `Tools/SleepyDemos/Luban/生成客户端配置` | 校验并原子生成 C#、bytes、JSON，再刷新 Tables 访问器、采集与 Label | `Config/LubanEditorTools.cs` |
| `Tools/SleepyDemos/Luban/仅校验配置` | 只检查定义与数据，不替换生成目录 | 同上 |
| `Tools/SleepyDemos/Luban/重新生成 Tables 访问器` | 从实际 `GeneratedTables.cs` 重建静态 facade | `Config/LubanTemplateClassGenerator.cs` |
| `Tools/SleepyDemos/Luban/打开策划配置目录` | 打开 `SleepyConfigs` 子模块 | `Config/LubanEditorTools.cs` |
| — | 完整使用与排障步骤 | [使用 Luban 配置](./use-luban-config.md) |

### 6. 运行时基础设施校验

| 菜单路径 | 说明 | 源码 / 文档 |
|----------|------|-------------|
| — | Unity Test Runner / UnitySkills 自动化验证步骤 | [运行 Unity 自动化测试](./run-unity-tests.md) |

### 7. Hot Reload 辅助（项目封装）

在官方 Hot Reload 包之上，仓库内增加了排障与授权查看工具（`Assets/Scripts/Core/Editor/HotReload/`）：

| 菜单路径 | 说明 |
|----------|------|
| `Tools/Hot Reload/授权信息查看器` | 查看/模拟授权状态快照 |
| `Tools/Hot Reload/打印 LoginStatusResponse 签名` | 打印 DTO 字段签名，便于对接 Hot Reload 版本 |
| `Tools/Hot Reload/运行反射抓取 LoginStatusResponse` | 反射抓取运行时 DTO 结构 |

---

## 二、仓库内第三方插件（Assets/Plugins）

以下为随仓库提交的编辑器增强，**非** `Core.Editor` 自研，但协作中常用。

### Find Reference 2（FR2）

- **主窗口**：`Window/Find Reference 2`
- **资源上下文**：`Assets/FR2/*`（查引用、导出依赖、刷新索引等）
- **包信息**：`Assets/Plugins/FindReference2/package.json`
- **用途**：查资源引用、未使用资源、Prefab 依赖链

### vTabs

- **菜单根**：`Tools/vTabs/`（标签样式、快捷键、触控板滚动等）
- **用途**：Editor 多标签页体验增强

### Odin Inspector（Sirenix）

- **路径**：`Assets/Plugins/Sirenix/`
- **用途**：Inspector 绘制与序列化增强；无统一「项目菜单」，随组件与窗口生效

### 其他 Plugins

- 具体能力以各插件自带说明为准；新增常用插件时在本节补一行，避免协作者只在 Project 窗口里摸入口。

---

## 三、UPM 开发包（远程仓库）

在 `Packages/manifest.json` 中声明，Unity 导入后提供编辑器菜单或 API。构建流程多由 **Hotfix Build** 窗口统一调用，日常也可直接用上菜单。

| 包 ID | 用途 | 远程仓库 |
|-------|------|----------|
| `com.besty.unity-skills` | Editor 已打开时，通过 REST 执行菜单、读 Console 等（Agent/脚本自动化） | [Besty0728/Unity-Skills](https://github.com/Besty0728/Unity-Skills) |
| `com.code-philosophy.hybridclr` | 热更程序集、AOT 补充元数据生成 | [focus-creative-games/hybridclr_unity](https://github.com/focus-creative-games/hybridclr_unity) |
| `com.code-philosophy.luban` | Luban 生成代码运行时与 `ByteBuf` 支持，固定 `v1.2.0` | [focus-creative-games/luban_unity](https://github.com/focus-creative-games/luban_unity) |
| `com.tuyoogame.yooasset` | 资源包构建与运行时加载（OpenUPM 版本号见 manifest） | [tuyoogame/YooAsset](https://github.com/tuyoogame/YooAsset) |
| `com.singularitygroup.hotreload` | 编辑器热重载 | GitLab：`git+https://gitlab.hotreload.net/root/hot-reload-releases.git`（版本见 manifest） |
| `com.cysharp.unitask` | 异步任务库 | [Cysharp/UniTask](https://github.com/Cysharp/UniTask) |
| `com.cysharp.zstring` | 零分配字符串 | [Cysharp/ZString](https://github.com/Cysharp/ZString) |

### UnitySkills 常用入口

- **启动服务**：`Window > UnitySkills > Start Server`
- **健康检查**：从 registry 获取当前端口后调用 `/health`，核对项目名、Unity 版本和实例 ID
- **示例**：测试发现、运行与轮询见 [运行 Unity 自动化测试](./run-unity-tests.md)

---

## 四、流程类 Runbook（非菜单，但属「项目工具链」）

| 文档 | 场景 |
|------|------|
| [新增 Demo](./add-demo.md) | 新建 Demo 资源与 Hotfix 接入 |
| [TMP 字体生产流程](./tmp-font-workflow.md) | TMP 字体资产生产 |
| [运行 Unity 自动化测试](./run-unity-tests.md) | Core.Tests 发现、运行与排障 |
| [使用 Luban 配置](./use-luban-config.md) | 配置修改、生成、读取、提交与排障 |

---

## 五、Agent Skill（仓库内）

项目级技能的入口和自动扫描方式见 [项目 Skill 入口](../agent/skills.md)。完整清单不在本文手写维护，以脚本输出为准：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/agent/list_skills.ps1
```

项目约定：

- **Codex**：`.codex/skills/`
- **Claude**：`.claude/skills/`
- **Cursor**：`.cursor/skills/`（当前仅同步部分技能，以目录实际文件为准）

当前需要特别知道的仓库内 Skill：

| Skill | 路径 | 适用 Agent | 何时使用 |
|-------|------|------------|----------|
| **git-commit** | `.claude/skills/git-commit/`<br>`.cursor/skills/git-commit/`<br>`.codex/skills/git-commit/` | Claude、Cursor、Codex | 根据 **staged** 变更生成 Conventional Commits（中文 subject）、commit、push |
| **sync-skills** | `.claude/skills/sync-skills/`<br>`.codex/skills/sync-skills/` | Claude、Codex | 双向查漏补缺 `.claude` / `.codex` 的 skill 和 rule；单技能模式可方向覆盖同步 |
| **task-retrospective** | `.claude/skills/task-retrospective/`<br>`.codex/skills/task-retrospective/` | Claude、Codex | 任务结束后复盘、沉淀最优提示词（保存路径见 skill 内说明） |
| **gen-module** | `.claude/skills/gen-module/`<br>`.codex/skills/gen-module/` | Claude、Codex | 生成或修改 Flux 模块 Action / Data / Handler；从钓鱼项目迁入，使用前确认当前模块结构、协议类型和网络发送方式匹配 |
| **module-docs** | `.claude/skills/module-docs/`<br>`.codex/skills/module-docs/` | Claude、Codex | 做完大型模块或调整模块文档边界时，按 architecture / modules / runbooks 三层体系补齐文档 |

### 远程 / 非仓库 Skill

以下**不在**本仓库 `.claude/skills` 中，但通过 UPM 或本机 Cursor 插件使用，链接供查阅：

| 名称 | 类型 | 链接 / 说明 |
|------|------|-------------|
| **Unity-Skills** | UPM 包（Editor REST） | [github.com/Besty0728/Unity-Skills](https://github.com/Besty0728/Unity-Skills) — 见上文「UnitySkills 常用入口」 |
| **Cursor 内置 / 用户级 Skills** | 本机 `~/.cursor/skills*` 或插件缓存 | 不在仓库内；不写入本表，避免与项目 Skill 混淆 |

同步 Claude / Codex 技能（预览 / 执行）：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -DryRun
```

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1
```

---

## 六、Agent Rule（仓库内）

与 Skill 不同，Rule 在会话开始时由 Agent 自动加载，用于协作约束。

| 规则文件 | 路径 | 作用 |
|----------|------|------|
| **pre-task-validation** | `.claude/rules/pre-task-validation.md`<br>`.codex/rules/pre-task-validation.md` | 非平凡任务动手前：先读代码/Prefab/调用链，再向用户提问 |

入口导航中的硬规则仍以 [AGENTS.md](../../AGENTS.md)、[CLAUDE.md](../../CLAUDE.md) 与 `docs/architecture/` 为准。

---

## 七、维护本页

在以下情况**必须**更新本文（同一 PR / 任务内）：

- 新增或删除 `Core.Editor` 的 `[MenuItem]` 入口
- 新增/变更仓库内 Agent Skill 或 Rule，并同步检查 [项目 Skill 入口](../agent/skills.md)
- 新增常用 UPM 工具包或更换其 Git 地址
- Runbook 更名或职责变化导致上表链接失效

仅改实现细节、菜单文案不变时，可只改代码与注释，不必改本文。
