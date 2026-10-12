---
description: 打开项目 Skill 集合入口，自动扫描 .codex/skills 与 .claude/skills，输出可点击的技能列表。可传入关键词筛选，例如 /skill linq。
---

# Skill 集合入口

当用户输入 `/skill`、`/skills`、`技能列表`、`skill列表`、`有哪些skill`，或想从项目技能中选择一个能力时：

1. 运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/agent/list_skills.ps1 -Prefer codex -Filter "$ARGUMENTS"
```

2. 用中文将脚本结果整理成 Markdown 表格展示给用户，保留编号、技能名、用途、链接、状态。
3. 提示用户可以回复编号或技能名继续选择。
4. 用户选择后，读取对应 `SKILL.md` 并按该技能工作。

约束：
- 不维护静态技能名单，始终以脚本扫描结果为准。
- 如果用户没有传入筛选词，展示全部技能。
- 如果某个技能只存在于 `.codex` 或 `.claude` 一侧，照常展示，并在状态列中保留同步提示。
