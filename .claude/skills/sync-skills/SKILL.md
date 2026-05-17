---
name: sync-skills
description: "把项目里的 Claude/Agents 技能与规则迁移到 Codex，并清理旧的 .agents 目录。用户说同步技能、补齐技能、同步 Claude/Codex skills 时使用。"
---

# Sync Skills

这是 Codex 专用技能，用来把项目内 `.claude` / `.agents` 中 Codex 需要的内容同步到 `.codex`。

当前约定：

- Codex 侧最终只使用 `.codex/skills`
- Claude / Agents 中已有的技能，只在 `.codex/skills` 缺失时复制过去
- Claude / Agents 中已有的规则，只在 `.codex/rules` 缺失时复制过去
- 已存在于 `.codex` 的内容不覆盖
- `sync-skills` 自己跳过，不从旧目录反向复制
- 同步完成后删除整个 `.agents` 目录
- `.claude` 整体保留，不做删除

说明：

- `.claude` 是项目组目录，继续保留；脚本只读取其中的技能和规则，不会删除或覆盖 `.claude`

## 使用

预览将要同步和删除的内容：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -DryRun
```

执行迁移：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1
```

如果当前仍是通过旧入口触发，也可以临时使用：

```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/sync-skills/scripts/sync_skills.ps1 -DryRun
```

迁移后如 Codex 没有立刻识别新规则或技能，重启客户端或开启新会话。
