---
name: git-commit
description: >-
  根据 staged changes 生成 Conventional Commits 格式提交信息，执行 commit 并 push 到远端。
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
<type>(<scope>): <subject>

[optional body — 说明 why，Unity 场景/Prefab/热更上下文写这里]

[optional footer — Fixes: #123]
```

### 基本规则

- **祈使语气**：Add / Fix / Refactor，不用 Added / Fixing
- **subject ≤ 50 字符**，不加句号
- **body 每行 ≤ 72 字符**
- 自检：「If applied, this commit will **\<subject\>**」读起来通顺

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

### Step 2 — 分析 staged

- **无 staged**：停止，提示用户先 `git add`
- **无关改动混在一起**：列出文件，建议拆成多次 commit；用户坚持则在一个 body 里分 bullet 说明
- **与最近 commit 风格对齐**（`git log`），但优先 Conventional Commits

### Step 3 — 生成 commit message

1. 定 **type**（用户视角：新能力→feat，修 bug→fix）
2. 定 **scope**（上表）
3. 写 **subject**（一条清晰变更，不用「修正」「更新一下」）
4. 复杂变更写 **body**；有关联 issue 写 `Fixes: #N`

向用户**展示**拟用的完整 message（subject + body），再执行 commit。

### Step 4 — 提交

PowerShell 多行 message：

```powershell
git commit -m "feat(hotfix): add demo entry list to main menu" -m "Wire DemoRegistry into MainMenuView.`n`nRelated to: #42"
```

或 here-string：

```powershell
@'
feat(hotfix): add demo entry list to main menu

Wire DemoRegistry into MainMenuView.

Related to: #42
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
feat(core): add resource startup state to pipeline

Insert ResourceStartupState before hotfix assembly load.

Related to: #12
```

**Staged**：`Assets/Scenes/Hub.unity` + Prefab 引用修复

```
fix(scene): restore hub spawn point after prefab merge

Re-applied missing Transform on SpawnRoot after Smart Merge.
```

**Staged**：仅 `docs/architecture/code-layout.md`

```
docs: clarify LoadResources vs Scenes ownership
```

**Staged**：`Packages/manifest.json` 升级 YooAsset

```
build(deps): upgrade YooAsset to 2.x
```

## 禁止的 message

- `修正`、`更新`、`TMP`、`misc`、`WIP`
- 纯 emoji 标题（`:sparkles: 添加插件`）
- 无 type 的长句标题

## 可选参考

更完整的业界说明见 [Conventional Commits](https://www.conventionalcommits.org)；Unity 协作习惯见 Unity 官方 version control 指南。
