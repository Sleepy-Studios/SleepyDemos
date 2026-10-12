---
name: hotreload-log
description: Use when Unity Play Mode 中使用 Hot Reload，需要判断 C# 改动是否已应用，读取 Hot Reload Timeline 证据，检查 Reload finished、Changes partially applied、patches.json、modifiedMethods、failures、newFields，或决定是否退出 Play Mode 正常编译。
---

# Hot Reload Log

## 概述

判断 Hot Reload 是否成功时先看证据，不要只凭 Unity Console 猜。Hot Reload 面板 Timeline 是人工入口，但 Timeline 详情不一定完整写入 Unity Console 或 `Editor.log`。

## 快速判断

| 证据 | 判断 |
|---|---|
| 面板显示 `Reload finished`；`patches.json` 包含本次修改的方法；`failures` 为空 | 继续在 Play Mode 内验证 |
| 面板显示 `Changes partially applied`；关键方法已出现在 `modifiedMethods` 且 `failures` 为空 | 可以继续，但要说明 partial 状态并实测行为 |
| 关键方法缺失、`failures` 非空，或出现 unsupported changes | 退出 Play Mode，让 Unity 正常重新编译 |
| 出现新增字段、删除字段、enum 成员变化 | 即使方法补丁成功也要谨慎，结构变化需要重点验证 |

## 读取位置

- 人工查看：`Window > Hot Reload` 面板 Timeline。
- 机器读取：`Library/com.singularitygroup.hotreload/patches.json`。
- 补丁临时目录：`%LOCALAPPDATA%\singularitygroup-hotreload\HotReloadServerTemp\<project-id>\MethodPatches`。

`<project-id>` 由插件生成，例如 `fishinggameplay-4F48AC`。不要硬编码，按当前项目名去目录里找。

## 读取 patches.json

在 Unity 项目根目录执行：

```powershell
@'
import json
from pathlib import Path

p = Path(r"Library/com.singularitygroup.hotreload/patches.json")
text = p.read_text(encoding="utf-8") if p.exists() else ""
data = json.loads(text) if text.strip() else []

for gi, group in enumerate(data[-8:], max(0, len(data) - 8)):
    methods = []
    failures = []
    new_fields = []
    deleted_fields = []

    for patch in group.get("patches", []):
        methods += [m.get("displayName") for m in patch.get("modifiedMethods", [])]
        failures += patch.get("failures", []) or []
        new_fields += patch.get("newFields", []) or []
        deleted_fields += patch.get("deletedFields", []) or []

    if methods or failures or new_fields or deleted_fields:
        print(f"[{gi}] methods={len(methods)} failures={len(failures)} newFields={len(new_fields)} deletedFields={len(deleted_fields)}")
        for m in methods[-12:]:
            print("  method:", m)
        for f in failures[:5]:
            print("  failure:", f)
        if new_fields:
            print("  newFields:", [x.get("fieldName") for x in new_fields[:12]])
        if deleted_fields:
            print("  deletedFields:", [x.get("fieldName") for x in deleted_fields[:12]])
'@ | python -
```

## 回报格式

给用户结论时，带上具体证据：

- Hot Reload 面板或 `patches.json` 的最新状态。
- 本次修改的方法是否出现在 `modifiedMethods`。
- 是否存在 `failures`、`newFields`、`deletedFields`。
- 最终判断：继续 Play Mode 验证，还是退出 Play Mode 正常编译。

## 常见错误

- 不要假设 Unity Console 一定有完整 Hot Reload Timeline。
- 不要把 `Changes partially applied` 直接等同失败，要看关键方法是否已应用。
- 不要把方法补丁成功等同于结构变化也安全，字段和 enum 成员变化要单独看。
