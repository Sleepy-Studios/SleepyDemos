# 接入 Sleepy Loop Scroll

## 前置条件

独立包位于 D:/Unity/Unity_Project/SleepyLoopScroll，开发期保持本地包依赖。Canvas 下创建 GameObject/UI/Sleepy Loop Scroll，配置 Viewport、Content、隐藏模板和尺寸。模板包含 LoopCell，业务 ItemView 仍为普通 C# 类。

## 注册一次，再提交集合

```csharp
this.RegisterLoopScrollRect<InventoryItemView>(list, OnItemsRectData);
this.RegisterLoopScrollClick(list, OnItemsClick);
this.RegisterLoopScrollItemHide(list, OnItemsItemHide);
list.SetTotalCount(items, getItemKey: item => ((ItemData)item).Id.ToString());
list.RefreshCells();
list.RefillCells(new RefillOptions(20, ScrollAlignment.Center));
list.ScrollToCell(20, ScrollAlignment.Center, new ScrollAnimation(.2f));

private void OnItemsRectData(ItemView cell, int index, CellBindContext context)
{
    ((InventoryItemView)cell).SetData(items[index]);
}
private void OnItemsClick(ItemView cell, int index, CellBindContext context) { }
private void OnItemsItemHide(ItemView cell, CellBindContext context) { }
```

绑定回调读取调用方集合；解绑回调释放业务资源。异步写入前检查 context.IsCurrent，动态内容改变后调用 InvalidateCellSize(context.Index)。Token 取消不保证旧任务停止。

MvcBind 只选择 RectData、Click、ItemHide，签名同上。生成的非泛型注册不推断工厂类型，提交前显式 list.ItemViews().Configure<InventoryItemView>()。View 自动持有订阅；嵌套 ItemView 使用同名方法返回的 IDisposable，回收时释放。

SetTotalCount(null) 清空。每次提交完整重填，默认起点；贴底使用 RefillCells(new RefillOptions(ScrollAnchorPolicy.StickToEnd))。RefreshCells 只刷新当前项，结构变化先修改集合再用 ApplyChanges/Append/Prepend 或 RefillCells 通知。稳定 Key 唯一且来自业务 ID。不能修改 Content 坐标。

多类型高级源使用 SetDataSource，在 BindCell 中 GetOrCreate<TItemView>(cell,context)，UnbindCell 中 TryGetItemView。每个物理 Cell 固定一种 ItemView 类型；事件仍走同一桥接。

## 带偏移的定位与取消

```csharp
list.ScrollToCell(20, ScrollAlignment.Center, new ScrollAnimation(.25f), 60, result =>
{
    if (result.Status == ScrollStatus.Completed) ShowCompleted();
    else ShowCanceled(result.CancelReason);
});
list.CancelAnimation();
```

最终位置 = 对齐位置 - offsetPixels，随后钳制。+60 让目标相对中心线向下/右移动 60 Canvas UI 像素，-60 反向。ScrollToOffset(offset, animation, onFinished) 同样报告唯一终止结果。

完成通知表示当前布局已稳定且误差 ≤1 UI 像素，不等未来异步内容；IsAnimating 仅表示插值。新有效请求、拖动、手动取消、成功数据更新/重填、禁用、销毁或执行中零尺寸会取消。无效请求同步报错并保留旧请求；RefreshCells/动态测量不取消。inactive 或任一轴零尺寸时提交暂存，恢复后用原动画时长执行。

回调内可再次定位/刷新/重填，异常记录日志。无需锁住按钮等动画结束；示例 MvcBind 页面提供普通中心定位、±60 偏移、取消按钮和中英完成/取消状态，拖动打断也更新状态。

## 示例与验证

打开 Assets/Samples/SleepyLoopScroll/Showcase/Main.unity 或 Tools/Sleepy Loop Scroll/Open Showcase，进入四个包子场景及宿主 MvcBind 页面。首次中文，主菜单切换语言并持久化，子场景可返回。

重建前保存场景，执行 Build Imported Sample Scenes，再 Build MvcBind Example；重复执行不重复追加入口。Editor 不修改 Build Settings，独立 Player 显式包含全部场景。验证只运行包测试、LoopScrollMvcGenerationTests、LoopScrollItemViewBridgeTests、LoopScrollShowcaseTests 和直接受影响类。

常见问题：Content 布局冲突、缺失 Type 0、重复 Key、异步不检查 IsCurrent。见包 Troubleshooting 和 UnifiedValidation。
