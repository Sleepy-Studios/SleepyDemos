# 接入 Sleepy Loop Scroll

## 前置条件

项目通过 `Packages/manifest.json` 从公开组织仓库 `https://github.com/Sleepy-Studios/SleepyLoopScroll.git#v0.1.0` 拉取独立包，使用发布标签固定版本；`Packages/packages-lock.json` 随项目提交，其中的提交哈希由 Unity 记录，不依赖开发者本机的包目录。更新包时先提交、推送包仓库并发布新的 `v` 标签，再更新 manifest 标签，由 Unity 解析并同步 lock；已发布标签保持不变。

插件仓库与 SleepyDemos 一样公开，拉包无需 GitHub 登录或额外权限。打开 Unity 等待 Package Manager 解析即可。

Canvas 下创建 GameObject/UI/Sleepy Loop Scroll，配置 Viewport、Content、隐藏模板和尺寸。模板包含 LoopCell，业务 ItemView 仍为普通 C# 类。

## 注册一次，再提交集合

```csharp
this.RegisterLoopScrollRect<InventoryItemView>(list, OnItemsRectData);
this.RegisterLoopScrollClick<InventoryItemView>(list, OnItemsClick);
this.RegisterLoopScrollItemHide<InventoryItemView>(list, OnItemsItemHide);
list.SetTotalCount(items, getItemKey: item => ((ItemData)item).Id.ToString());
list.RefreshCells();
list.RefillCells(new RefillOptions(20, ScrollAlignment.Center));
list.ScrollToCell(20, ScrollAlignment.Center, new ScrollAnimation(.2f));

private void OnItemsRectData(InventoryItemView item, int index)
{
    item.SetData(items[index]);
}
private void OnItemsClick(InventoryItemView item, int index) { }
private void OnItemsItemHide(InventoryItemView item) { }
```

绑定回调读取调用方集合；解绑回调释放业务资源。普通 Item 保存自己的业务数据，通过 UIBind 绑定控件，按钮调用 TriggerClick，图片使用已有 UIImageLoader；不保存 CellBindContext、Cell、版本号或业务按钮字典。动态内容改变后用当前索引通知列表 InvalidateCellSize。只有确实需要异步写回身份保护时才使用下文高级回调。

UIBind 只选择 RectData、Click、ItemHide，生成默认签名分别为 `(ItemView item, int index)`、`(ItemView item, int index)`、`(ItemView item)`；在回调中转换为业务项即可。生成的非泛型注册不推断工厂类型，提交前显式 list.ItemViews().Configure<InventoryItemView>()。手写 typed 注册不需要转换。View 自动持有订阅；嵌套 ItemView 使用同名方法返回的 IDisposable，回收时释放。

SetTotalCount(null) 清空。每次提交完整重填，默认起点；贴底使用 RefillCells(new RefillOptions(ScrollAnchorPolicy.StickToEnd))。RefreshCells 只刷新当前项，结构变化先修改集合再用 ApplyChanges/Append/Prepend 或 RefillCells 通知。稳定 Key 唯一且来自业务 ID。不能修改 Content 坐标。

多类型高级源使用 SetDataSource，在 BindCell 中 GetOrCreate<TItemView>(cell,context)，UnbindCell 中 TryGetItemView。每个物理 Cell 固定一种 ItemView 类型；事件仍走同一桥接。

## 特殊异步绑定与菜单导航

只有自定义异步操作可能晚于 Cell 换绑完成、必须检查写入身份时，才显式选择高级重载：

```csharp
this.RegisterLoopScrollRect<InventoryItemView>(list, (ItemView item, int index, CellBindContext context) =>
{
    // 自定义任务使用 context.CancellationToken；写入前仍检查 context.IsCurrent。
});
```

Token 取消不保证旧任务停止。Context 不进入普通 Item 数据或 UIBind 默认回调；多类型数据源仍使用包已有 BindCell / UnbindCell。业务不为同步 SetData 引入 Context。

需要键盘/手柄跨虚拟项导航时，在 LoopScrollView 同对象添加 `LoopScrollMenuNavigation`，将隐藏模板的主操作按钮替换为 `LoopScrollMenuButton`，保持原 Button.onClick/UIBind 绑定。每项唯一主按钮，不向公共组件塞图片、标题或描述引用。列表提交唯一稳定 Key，导航只依赖布局和当前绑定身份。

页面沿用 Core.MenuInputScope，只创建一个作用域；首次 `SetContext(Menu, navigation.FirstSelection)`，之后每帧调用 `menuScope.Update(navigation.FirstSelection)`。重载仅更新当前可用默认焦点并驱动原门闩，不重置等待按键释放。上下移动按 Grid 行、左右移动按列，超出当前池范围的目标由 ScrollToCell 正式定位。组件不模拟 Submit，不触发点击监听器；回收旧焦点的物理按钮不会把后续提交转给新数据身份。Pointer 仍直接操作同一个按钮。

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

回调内可再次定位/刷新/重填，异常记录日志。无需锁住按钮等动画结束；示例 UIBind 页面提供普通中心定位、±60 偏移、取消按钮和中英完成/取消状态，拖动打断也更新状态。

## 示例与验证

打开 Assets/Samples/SleepyLoopScroll/Showcase/Main.unity 或 Tools/Sleepy Loop Scroll/Open Showcase，进入四个包子场景及宿主 UIBind 页面。首次中文，主菜单切换语言并持久化，子场景可返回。

宿主 UIBind 示例场景已保存，一次性 Build UIBind Example 已删除。重导包示例时保留宿主场景和 LoopScrollInputHost 接线。Editor 不修改 Build Settings，独立 Player 显式包含全部场景。验证只运行包测试、LoopScrollUIBindGenerationTests、LoopScrollItemViewBridgeTests、LoopScrollShowcaseTests 和直接受影响类。

常见问题：Content 布局冲突、缺失 Type 0、重复 Key、异步不检查 IsCurrent。见包 Troubleshooting 和 UnifiedValidation。
