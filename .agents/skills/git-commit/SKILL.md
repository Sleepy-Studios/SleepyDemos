---
name: git-commit
description: >-
  根据 staged changes 生成 Conventional Commits 格式提交信息（中文 subject/body），执行 commit 并 push 到远端。
  用户说提交代码、生成 commit、commit 并推送、push staged changes、帮我 commit 时使用。
---

# Git Commit（SleepyDemos）

根据 **已暂存（staged）** 变更生成提交信息，提交并推送到远端。

## 触发

- 「帮我 commit / 提交 / 推送」
- 「根据 staged changes 生成 commit」
- 「commit 并 push」

## 安全规则（必须遵守）

- **只提交 staged 文件**；未 stage 的不自动 `git add`，除非用户明确要求
- **不提交** 疑似密钥文件（`.env`、`credentials.json`、含 token 的配置等）；发现则中止并警告
- **不** `git config`、**不** `--no-verify`、**不** force push
- **不** 用 `git commit -a` 一把梭
- Windows 下命令**分开执行**，不用 `&&`
- 用户仅问「怎么写 commit message」时，只生成文案，不执行 commit/push

## 提交格式

```plaintext
<type>(<scope>): <中文 subject>

[optional body — 中文，说明 why，Unity 场景/Prefab/热更上下文写这里]

[optional footer — 修复: #123 / 关联: #456]
```

- **type / scope 保持英文**（便于工具解析与分类）
- **subject / body 优先中文**（团队阅读友好）

### 基本规则

- **祈使语气**：用「添加 / 修复 / 重构」，不用「添加了 / 正在修复 / 已修复」
- **subject ≤ 50 字符**（中文按字计），不加句号
- **body 每行 ≤ 72 字符**
- 自检：「这次提交将会 **\<subject\>**」读起来通顺

### type

| type | 用途 |
|------|------|
| `feat` | 新功能、新玩法、新模块 |
| `fix` | Bug 修复 |
| `refactor` | 不改行为的重构 |
| `perf` | 性能优化 |
| `docs` | 仅文档 |
| `test` | 测试 |
| `build` | 构建、依赖、Unity 版本、HybridCLR/YooAsset |
| `ci` | CI/CD |
| `chore` | 杂项维护、.gitignore |
| `style` | 格式，不改逻辑 |
| `asset` | 模型/贴图/音频等资源（无代码逻辑） |
| `level` | 关卡/场景内容设计 |
| `locale` | 本地化 |

### scope（按路径推断）

| 路径 | scope |
|------|-------|
| `Assets/Scripts/Core/Runtime` | `core` |
| `Assets/Scripts/Core/Editor` | `editor` |
| `Assets/Scripts/Hotfix` | `hotfix` |
| `Assets/LoadResources/Demos/<DemoId>/` | `demo/<DemoId>` |
| `Assets/LoadResources/UI` | `ui` |
| `Assets/LoadResources/Art` | `art` |
| `Assets/LoadResources/Audio` | `audio` |
| `Assets/LoadResources/VFX` | `vfx` |
| `Assets/Scenes`、`Assets/LoadResources/Scenes` | `scene` |
| `docs/` | `docs` |
| `Packages/`、`ProjectSettings/` | `build` |
| `Assets/Plugins/` | `deps` |
| `.claude/skills/`、`.cursor/skills/`、`.codex/skills/` | `skills` |

多 scope 时选**主要变更**；确实无法拆分且跨层时可用 `core,hotfix` 或省略 scope。

### Unity 注意

- Scene/Prefab 常连带 `.meta`；subject 写意图，body 说明是否 Smart Merge、是否需 Reimport
- 每个 commit 应尽量**可编译**；混入无关 ProjectSettings/Library 噪音要提醒用户
- 描述 **why**，不是逐文件罗列

## 工作流

### Step 1 — 并行收集上下文

```powershell
git status
git diff --staged --stat
git diff --staged
git log --oneline -10
git branch -vv
```

### Step 2 — 分析 staged 与 scope

- **无 staged**：
  - 用户**未**指定提交范围 → 停止，提示先 `git add`
  - 用户**已**指定 scope（如「只提交菜单项更改」）→ 进入下方「按 scope 提取改动」，提取后再 stage
- **无关改动混在一起**（跨文件或同文件多 hunk）：
  - 默认拆成多次 commit；列出各 scope 对应文件/hunk
  - 同文件混合时**必须提取**，不得整文件跳过或整文件混入
- **与最近 commit 风格对齐**（`git log`），但优先 Conventional Commits + 中文 subject

### 按 scope 从混合文件提取改动（必须执行）

当用户指定提交范围，但某文件同时含有**无关 hunk** 时：

- **禁止**整文件 `git add` 把无关改动一并提交
- **禁止**因文件混合而整文件放弃提交、导致文档/代码不同步
- **必须**只 stage 本次 scope 内的 hunk，其余改动留在工作区

提取手法（Windows / 非交互环境，按优先级）：

1. **`git add -p <file>`**：终端支持交互时逐 hunk 选择
2. **临时拆文件**（非交互时推荐）：
   - 备份当前文件全文
   - `git checkout HEAD -- <file>` 还原到上次提交
   - 只写回本次 scope 需要的改动
   - `git add <file>` → 进入 Step 3～4 提交
   - 将备份中**未提交**的 hunk 写回工作区（恢复「已提交 + 仍待提交」状态）
3. **手工 patch**：从 `git diff` 截取目标 hunk 生成 patch，`git apply --cached`（提交前核对路径与上下文）

提交前用 `git diff --staged` **复核**：staged 内容仅含用户要求的 scope；向用户说明哪些文件/hunk 留待下次提交。

### Step 3 — 生成 commit message

1. 定 **type**（用户视角：新能力→feat，修 bug→fix）
2. 定 **scope**（上表）
3. 写**中文 subject**（一条清晰变更；避免空泛的「修正」「更新一下」）
4. 复杂变更写**中文 body**；有关联 issue 写 `修复: #N` 或 `关联: #N`

向用户**展示**拟用的完整 message（subject + body），再执行 commit。

### Step 4 — 提交

PowerShell 多行 message：

```powershell
git commit -m "feat(hotfix): 主菜单接入 Demo 入口列表" -m "将 DemoRegistry 接入 MainMenuView，从配置加载入口项。`n`n关联: #42"
```

或 here-string：

```powershell
@'
feat(hotfix): 主菜单接入 Demo 入口列表

将 DemoRegistry 接入 MainMenuView，从配置加载入口项。

关联: #42
'@ | git commit -F -
```

hook 失败：**不要 amend**（除非本对话中你刚创建 HEAD 且用户要求 amend）；修问题后**新 commit**。

### Step 5 — 推送

```powershell
git push
```

- 当前分支无 upstream：`git push -u origin HEAD`
- push 被拒（远端有新提交）：`git pull --rebase` 后再 push；**不要** force push，除非用户明确要求
- push 前若用户只要 commit 不要 push，跳过本步

### Step 6 — 确认

```powershell
git status
git log -1 --format=fuller
```

回报：commit hash、分支、是否已 push、是否需 Unity 内验证。

## 示例

**Staged**：`Assets/Scripts/Core/Runtime/Startup/StartupPipeline.cs` 新增状态

```
feat(core): 启动流水线新增资源初始化状态

在热更程序集加载前插入 ResourceStartupState。

关联: #12
```

**Staged**：`Assets/Scenes/Hub.unity` + Prefab 引用修复

```
fix(scene): 修复 Prefab 合并后 Hub 出生点丢失

Smart Merge 后补回 SpawnRoot 缺失的 Transform。
```

**Staged**：仅 `docs/architecture/code-layout.md`

```
docs: 明确 LoadResources 与 Scenes 的职责边界
```

**Staged**：`Packages/manifest.json` 升级 YooAsset

```
build(deps): 升级 YooAsset 至 2.x
```

**Staged**：`.claude/skills/git-commit/SKILL.md` 等 Agent 技能

```
chore(skills): 新增 git-commit 提交技能

定义 staged 分析、中文 Conventional Commits 格式与 push 流程。
```

**用户要求**：「只提交菜单项更改」；`docs/architecture/asset-naming.md` 同文件还含字体命名文档改动

→ 用「临时拆文件」只写回 YooAsset 菜单路径段落并 stage，字体相关 hunk 保留在工作区，**不要**整文件跳过或整文件提交。

## 禁止的 message

- 空泛标题：`修正`、`更新`、`TMP`、`misc`、`WIP`
- 纯 emoji 标题（`:sparkles: 添加插件`）
- 无 type 的长句标题
- 中英混排且语义不清（如 `fix: 修正bug`）

## 可选参考

更完整的业界说明见 [Conventional Commits](https://www.conventionalcommits.org)；Unity 协作习惯见 Unity 官方 version control 指南。
