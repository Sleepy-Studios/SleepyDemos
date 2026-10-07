# 开发和验证夹爆它

## 接手已有 MVP

先读 [README](../README.md)、[验证记录](validation.md)、[模块维护](../module.md)，再查看实际保存的 Scene / World / UI Prefab 与 [当前验证案](../architecture/mvp-validation.md)。先体验已有版本，按本轮新指令迭代；场景和资源是正式真源，不重复生成或重建已验收资产。

## 新对话启动提示词

```text
请接手 SleepyDemos 已实现的《夹爆它》MVP。先读 AGENTS.md、docs/demos/wall_squeeze/README.md、module.md、runbooks/validation.md，并查看 Assets/LoadResources/Demos/wall_squeeze/Scenes/Main.unity、Prefabs/World.prefab 与 Prefabs/UI 的真实资源。

先体验已有六关验证版，再按我本轮提供的新指令迭代，不重复实现整套，也不生成 Builder 重建已验收 Scene / Prefab。规则、布局和美术方向以当前验证案为准；人工体验重点是拖墙、成群夹爆、特殊怪夹法和保护住户。

继续复用公共 UI、GameplayInputRouter、暂停、资源和场景导航。保护已有未提交工作，保留保存资产的人工调整；已有普通、顶墙、钻缝三种怪与限时救援。其他新类型、成长和计分按新需求决定。

按本次迭代选择最小直接相关 Unity Test Runner 范围，检查 Editor 编译与 Console，区分模拟输入、真实硬件、手感和视觉验收。不要使用 dotnet/MSBuild、第二个 BatchMode，不主动确认 Hot Reload。不自动 commit/push。

完成后同步本 Demo 文档与实际结果；当前已通过的范围不机械重复或扩大。
```

## 操作约定

键鼠：按住墙拖动，或 Q/E 选墙、WASD/方向键沿轴移动。触屏：单指拖墙。手柄：肩键选墙，左摇杆沿轴移动。模拟设备接线已验证，真实触屏和手柄仍待人工验收。

使用项目 Hub 进入“夹爆它”；暂停或结果菜单返回 Hub。Editor 直接打开 `Assets/LoadResources/Demos/wall_squeeze/Scenes/Main.unity` 后 Play，公共 Bootstrap 按保存路径识别此 Demo。

## 完成报告

记录实际 Unity 测试类与结果、是否全量、受影响保存关卡参考路径、Hub 往返、输入取消和多设备验证。把拖墙手感、特殊怪夹法、夹爆反馈和可读性作为人工体验结论单独报告，未执行项明确标出。
