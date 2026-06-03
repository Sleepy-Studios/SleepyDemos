---
name: module-docs
description: "按 SleepyDemos 的 architecture / modules / runbooks 三层文档体系补齐模块文档；适用于创建或更新模块文档、完成大型模块后补文档，或判断内容应落在架构说明、模块维护说明还是操作 runbook 中。"
---

# Module Docs

为 SleepyDemos 模块补文档时使用本技能。目标是把“开发思路”“模块维护”“使用步骤”分开，避免一个 md 同时服务所有读者。

## 三层判断

- `docs/architecture/`：给开发人员看思路。写设计原则、分层理由、边界取舍、跨模块约束、为什么不能这么做。
- `docs/modules/*.md`：给维护模块的人看。写模块职责、代码入口、主链路、生命周期、边界、修改注意事项、验证重点。
- `docs/runbooks/*.md`：给使用者或接入者看。写具体任务步骤、操作命令、接入示例、常见错误和排障路径。

不要把 API 参数解释堆进 md；业务侧实际调用的底层 public / protected 方法，尤其带 bool、command、Type、回调、异步返回值的入口，用 C# XML 注释说明参数语义。

## 工作流程

1. 先读 `docs/README.md`、`docs/architecture/documentation-rules.md` 和目标模块现有文档。
2. 查目标模块代码入口，确认真实职责和调用链，不凭文件名臆测。
3. 判断本次需要哪几层：
   - 涉及设计原则或跨模块边界：补 `architecture`。
   - 涉及关键模块维护：补 `modules`。
   - 涉及接入步骤或操作流程：补 `runbooks`。
4. 更新 `docs/README.md` 导航；必要时更新相关模块之间的交叉链接。
5. 做基础验证：检查路径是否存在、链接是否合理、`git diff --check` 是否通过。

## 模板

### architecture

```markdown
# <主题>设计原则

## 目标
## 为什么这样设计
## 分层边界
## 关键取舍
## 与其它模块的关系
## 修改原则
```

### modules

```markdown
# <模块名>

## 负责什么
## 不负责什么
## 代码位置
## 主链路
## 生命周期
## 边界规则
## 修改这里时注意什么
## 验证重点
## 相关文档
```

### runbooks

```markdown
# <任务名>

## 适用场景
## 前置条件
## 操作步骤
## 示例
## 常见问题
## 验证方式
```

## 收口规则

- 模块文档保持短而可维护；详细“怎么用”移到 runbook。
- runbook 面向完成任务，不解释太多设计历史。
- architecture 不写流水账步骤。
- 小型 Data 三件套可以不单独写模块文档，除非出现独立入口、复杂规则或多人高频修改边界。
