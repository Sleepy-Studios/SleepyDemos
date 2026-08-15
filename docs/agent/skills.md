# 项目 Skill 入口

本文记录 SleepyDemos 仓库内 `.codex/skills/` 与 `.claude/skills/` 的项目级技能入口。技能清单以脚本实时扫描结果为准，不手写维护完整静态列表。

## 使用方式

- Codex：输入 `/skill`、`技能列表`、`有哪些skill` 等意图时，按 `.codex/commands/skill.md` 执行。
- Claude Code：输入 `/skill` 时，按 `.claude/commands/skill.md` 执行。
- 可传入关键词筛选，例如 `/skill module`、`/skill 提交`。
- 列表展示后，回复编号或技能名即可选择具体 skill 继续执行。

## 自动扫描

命令入口会运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/agent/list_skills.ps1
```

脚本读取每个 `SKILL.md` 的 frontmatter，输出带链接的 Markdown 表格。新增技能只要放到以下任一目录，下次运行 `/skill` 就会自动出现在列表中：

- `.codex/skills/<name>/SKILL.md`
- `.claude/skills/<name>/SKILL.md`

新增或修改技能后，通常需要重启 Claude / Codex 或开启新会话，模型侧才会自动发现新技能；但 `/skill` 入口列表本身会即时扫描最新文件。

## 同步技能

`.codex/skills/sync-skills` 负责同步 `.codex` 与 `.claude` 两侧的项目级技能和规则。

预览：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1 -DryRun
```

执行：

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/sync-skills/scripts/sync_skills.ps1
```

同步规则：

- 默认双向查漏补缺，已存在的同名技能或规则不覆盖。
- `sync-skills` 自己不会被脚本自动发布或覆盖。
- 使用 `-SkillName`、`-From`、`-To` 可对单个技能做方向同步。

## 当前重点技能

完整列表以脚本输出为准。当前需要特别知道的项目技能：

| Skill | 主要用途 | 注意事项 |
|------|----------|----------|
| `git-commit` | 根据 staged changes 生成中文 Conventional Commits，并提交 / 推送 | 只处理已 staged 的变更 |
| `sync-skills` | 同步 `.codex` 与 `.claude` 两侧的技能和规则 | 默认不覆盖同名项 |
| `task-retrospective` | 任务完成后沉淀复盘和更优提示词 | 按技能内保存路径执行 |
| `gen-module` | 生成或修改 Flux 模块的 Action / Data / Handler 三件套 | 从钓鱼项目迁入；使用前必须确认 SleepyDemos 当前模块结构、协议类型和网络发送方式是否匹配 |
| `module-docs` | 按 `architecture` / `modules` / `runbooks` 三层体系补齐模块文档 | 做完大型模块或调整模块文档边界时使用 |
| `unity-demo-model-pipeline` | 从 Unity 玩法需求形成建模契约，完成 Blender、FBX、Unity 装配与验收闭环 | 建模前必须确认尺寸、轴向、层级、Pivot、材质、碰撞和交付路径 |
