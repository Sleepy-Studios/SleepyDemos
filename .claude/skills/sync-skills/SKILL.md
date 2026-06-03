---
name: sync-skills
description: "同步项目级共享技能和规则。检查 .claude 与 .codex 两边是否缺失 skills/rules 并补齐，或指定单个技能从一边覆盖同步到另一边时使用。"
---

# Sync Skills

项目级同步范围明确限定为 `.claude` 与 `.codex`：

- `skills`：技能目录，目录内必须包含 `SKILL.md`。
- `rules`：规则文件或规则目录。

默认逻辑是双向查漏补缺：如果一边缺少另一边已有的技能或规则，就补齐缺失项。

同名技能或同名规则默认不覆盖、不合并、不比较内容差异。

指定 `-SkillName`、`-From`、`-To` 时进入单技能方向同步模式：覆盖同步指定技能，并默认把 `rules` 也按同方向覆盖同步。

## 常用命令

检查两边 `skills` 和 `rules` 并补齐缺失项：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1
```

预览将要补齐哪些技能：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -DryRun
```

把 Claude 中改过的单个技能同步到 Codex，并同步 Claude 的 rules 到 Codex：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -SkillName 技能名 -From claude -To codex
```

把 Codex 中改过的单个技能同步到 Claude，并同步 Codex 的 rules 到 Claude：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -SkillName 技能名 -From codex -To claude
```

只同步指定技能，不同步 rules：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -SkillName 技能名 -From claude -To codex -NoRules
```

需要本机链接而不是复制时，加 `-UseLinks`：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -UseLinks
```

## 规则

- `sync-skills` 自己不会发布或覆盖。
- 只有包含 `SKILL.md` 的目录会被当作技能。
- 默认模式只复制缺失的技能目录和规则项，已存在的同名项不会被覆盖。
- 单技能方向同步模式会覆盖目标侧同名技能；未加 `-NoRules` 时也会覆盖目标侧同名 rules。
- 同步新技能后，通常需要重启 Codex 或开启新会话。
