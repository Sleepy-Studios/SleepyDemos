---
name: sync-skills
description: "将 SleepyDemos 的 .agents/skills 自研技能源单向发布到 .claude/skills，或预览发布差异；不双向同步规则、不复制上游 unity-skills。"
---

# 发布项目技能

`.agents/skills` 是自研技能唯一维护源，`.claude/skills` 是 Claude 适配副本；`.codex` 只维护配置。不要在副本修改再反向合并，不新增 Junction 或符号链接，上游 `unity-skills` 单独管理。

预览与发布：

```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/sync-skills/scripts/sync_skills.ps1 -DryRun
powershell -ExecutionPolicy Bypass -File .agents/skills/sync-skills/scripts/sync_skills.ps1
```

只发布一个自研技能时加 `-SkillName <name>`。发布包含 `sync-skills` 自己；Claude 副本不带 Codex 的 `agents/openai.yaml`。脚本按文件比较更新，在明确目标技能边界内清理源中已删除的文件与空目录；遇到链接停止，不递归删除目录。不会清理目标侧未知顶层技能或上游目录。

修改技能只更新源，再预览并发布；新会话才能可靠发现新的技能元数据。清单由 `scripts/agent/list_skills.ps1` 实时读取权威源，不另写静态完整清单。
