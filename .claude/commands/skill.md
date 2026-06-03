---
description: 打开项目 Skill 集合入口，自动扫描 .claude/skills 与 .codex/skills，输出可点击的技能列表。可传入关键词筛选，例如 /skill linq。
---

# Skill 集合入口

执行下面的脚本获取技能数据：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/agent/list_skills.ps1 -Prefer claude -Filter "$ARGUMENTS"
```

展示时用中文整理成 Markdown 表格，保留编号、技能名、用途、链接、状态。展示后，提示用户可以回复编号或技能名继续选择。用户选择后，读取对应 `SKILL.md` 并按该技能工作。

约束：
- 不维护静态技能名单，始终以脚本扫描结果为准。
- 如果用户传入了 `$ARGUMENTS`，只展示匹配到的技能。
- 如果某个技能只存在于 `.claude` 或 `.codex` 一侧，照常展示，并在状态列中保留同步提示。
