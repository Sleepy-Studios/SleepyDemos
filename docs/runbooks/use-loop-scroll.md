# 接入 Sleepy Loop Scroll

## 前置条件

本地独立仓库与包根目录为 `D:/Unity/Unity_Project/SleepyLoopScroll`。开发期通过 Package Manager 的本地 package.json 接入；发布授权前不切换 Git URL。包文档位于其 README 和 Documentation~。

## 操作

1. Canvas 下创建 `GameObject/UI/Sleepy Loop Scroll`，配置 Viewport、Content、隐藏 Cell 模板和尺寸。
2. 模板含 LoopCell，宿主业务 ItemView 继续是普通 C# 类，不必改成 MonoBehaviour。
3. MvcBind 勾选列表组件的 CellBind、CellUnbind、CellClick 注册入口，生成 `On{FieldName}CellBind/CellUnbind/CellClick`。
4. 数据提交使用桥接，随后集合变化使用包的 ReloadData/ApplyChanges/Append/Prepend。

```csharp
list.ItemViews().SetItems<ItemData, InventoryItemView>(items,
    (cell, item, context) => cell.SetData(item),
    (cell, context) => cell.ReleaseBusinessResources(),
    item => item.Id.ToString());
```

业务资源释放方法由 ItemView 自己实现。业务 ID 的字符串 Key 必须唯一稳定。不要修改 Content 坐标，也不要使用平行类型/尺寸集合。

多类型高级数据源在 BindCell 中使用 `list.ItemViews().GetOrCreate<TItemView>(cell, context)`，按当前类型选择对应 ItemView；UnbindCell 使用 TryGetItemView 读取缓存并释放业务资源。MvcBind 三种回调仍沿同一桥接触发。

异步回调：先检查 `context.IsCurrent`，再写入 ItemView；动态尺寸改变后调用 `list.InvalidateCellSize(context.Index)`。禁用、回收、重绑和销毁均取消旧 Token，取消不等于保证旧任务不会继续运行。

## 示例与验证

通过 `Tools/Sleepy Loop Scroll/Build MvcBind Example` 生成本地示例，打开 `Assets/LoadResources/Demos/loop_scroll/Scenes/LoopScrollMvcExample.unity`。这是直接运行的示例入口，不新增 Hub 导航。

包的四个 Samples 分别独立导入、打开对应场景运行。测试使用 [Unity 测试流程](run-unity-tests.md)，限定包测试、LoopScrollMvcGenerationTests、LoopScrollItemViewBridgeTests 和直接受影响类。

常见问题：Content 布局冲突、缺失 Type 0 模板、重复 Key、误把 inactive 当作数据提交失败、异步完成不检查 IsCurrent。详见包 Troubleshooting。
