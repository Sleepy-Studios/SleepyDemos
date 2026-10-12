---
name: git-commit
description: "用户要求提交代码、阶段性本地提交、生成 Conventional Commit 信息或明确推送时使用；默认只 commit，不自动 push。"
---

# Git Commit（SleepyDemos）

提交格式使用 Conventional Commits，type / scope 用英文，subject / body 用中文。只询问提交信息时只交付文案。

## 范围与安全

- 已授权任务范围可自主 stage 和本地 commit；未指定范围时，以已有 staged 内容为准，没有 staged 且范围不清楚才询问。
- 先读 `git status`、staged / unstaged diff、最近提交及分支。核对代码、资源、`.meta` 和文档完整，排除无关改动。
- 混合文件按 hunk 暂存：优先 `git add -p`，非交互环境可用范围明确的 `git apply --cached` patch。提交前再次检查 staged diff，不通过还原工作区提取改动。
- 疑似密钥不得提交；不改 Git 配置，不使用 `--no-verify`、`git commit -a` 或 force push。PowerShell 命令分开执行。
- 默认不 push。只有用户明确要求推送才执行；远端分歧先检查，不自动把无关历史 rebase 或 merge 进当前任务。

## 提交信息

```text
<type>(<scope>): <中文 subject>

<必要时说明原因、影响面和验证边界>
```

subject 尽量不超过 50 字符，不加句号；body 不逐文件罗列。示例：

```text
refactor(core): 收拢公共输入设备识别

让 Demo 复用已有输入服务，保留各自动作配置和专属界面。
```

常用 type：`feat`、`fix`、`refactor`、`perf`、`docs`、`test`、`build`、`chore`、`asset`、`locale`。

| 路径 | scope |
|---|---|
| `Assets/Scripts/Core/Runtime` | `core` |
| `Assets/Scripts/Core/Editor` | `editor` |
| `Assets/Scripts/Hotfix` | `hotfix` |
| `Assets/LoadResources/Demos/<DemoId>` | `demo/<DemoId>` |
| `Assets/LoadResources/UI` | `ui` |
| `docs` | `docs` |
| `Packages`、`ProjectSettings` | `build` |
| `.agents/skills`、`.claude/skills` | `skills` |

跨层选主要 scope；确实不可拆分时可省略。Scene / Prefab 保留 `.meta` 与 GUID，验证按项目测试政策选择，不为文档提交启动 Unity。

## 执行与回报

授权已明确时展示拟用 message 后继续提交，无需再确认。多行 message 用临时文本文件加 `git commit -F <file>`，或 PowerShell here-string 管道到 `git commit -F -`。

hook 失败先修复问题再提交，不擅自 amend 已有 HEAD；仅在用户要求且目标明确时 amend。用户要求 push 时先确认当前分支与 remote，再执行普通 push；无 upstream 时可用 `git push -u origin HEAD`。

最后回读 `git status` 和 `git log -1 --format=fuller`，报告 commit hash、分支、是否推送、保留的无关工作与未验证项。
