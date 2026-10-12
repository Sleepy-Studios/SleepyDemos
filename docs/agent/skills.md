# 项目 Skill 入口

自研技能源只维护 `.agents/skills/<name>/SKILL.md`；`.claude/skills` 是发布副本，`.codex` 仅放配置。上游 `unity-skills` 独立维护，不参与自研发布。共同协作规则只在 [AGENTS.md](../../AGENTS.md)，Claude 通过根 `CLAUDE.md` 导入。

## 发现与选择

用户输入 `/skill`、`技能列表` 或同类意图时，运行实时清单：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/agent/list_skills.ps1
powershell -ExecutionPolicy Bypass -File scripts/agent/list_skills.ps1 -Filter module
```

脚本只扫描权威源，读取 frontmatter，并附 Claude 副本链接，不把副本重复计为另一个技能。列表后按用户选择读取对应入口与必要支持文件，不维护静态完整名单。Claude `/skill` 命令只转入此流程。

## 发布

修改权威源后使用 [sync-skills](../../.agents/skills/sync-skills/SKILL.md) 预览并单向发布。脚本不会反向合并、不复制上游、不创建链接，也不写 `.codex`。新增技能元数据需新会话才能可靠发现，清单脚本即时读取磁盘。

技能使用按实际工作流选择；技能不能扩大用户授权。提交默认本地 commit，只有明确要求才 push。测试保留与执行范围统一遵守 [测试架构](../architecture/testing.md)。
