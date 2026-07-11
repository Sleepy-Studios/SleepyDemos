# 使用 LoopScroll 循环列表

## 适用场景

- 奖励、排行、背包等大量数据列表。
- 横向分页、轮播、Spin 类无限滚动。
- 固定行列 Grid。
- 不同高度或宽度的瀑布流。

少量静态项继续用 `ViewList` 或 `UITab`。

## 接入步骤

1. 在 UI 预制体上创建标准 UGUI ScrollRect 结构：
   - 根节点挂 `ScrollRect`。
   - 子节点 `Viewport` 挂 `RectMask2D`。
   - `Viewport/Content` 作为列表内容根。
2. 在根节点添加对应组件：
   - 垂直列表：`LoopVerticalScrollRect`
   - 水平列表：`LoopHorizontalScrollRect`
   - Grid：`LoopGridView`
   - 瀑布流：`LoopStaggeredGridView`
3. 在 View 中注册数据回调：

```csharp
this.RegisterLoopScrollRect<RewardItemView>(LoopVerticalScrollRect_RewardList, OnRewardListRectData);
this.RegisterLoopScrollClick<RewardItemView>(LoopVerticalScrollRect_RewardList, OnRewardListClick);
```

4. 数据变化时刷新：

```csharp
LoopVerticalScrollRect_RewardList.SetTotalCount(rewards, index: 0, async: true, refreshWhenDisable: true);
```

5. 局部数据变化时：

```csharp
LoopVerticalScrollRect_RewardList.RefreshCells();
```

6. 需要定位或选中时：

```csharp
LoopVerticalScrollRect_RewardList.ScrollToCellWithinTime(targetIndex, 0.5f);
LoopVerticalScrollRect_RewardList.SetSelectIndex(targetIndex);
```

## Grid

```csharp
LoopGridView_AwardGrid.SetGridFixedGroupCount(GridFixedType.ColumnCountFixed, 4);
LoopGridView_AwardGrid.SetTotalCount(awards);
```

`GetRowColumnByItemIndex()` 和 `GetItemIndexByRowColumn()` 可用于行列与数据索引互转。

## 多预制体/多类型列表

同一个循环列表里需要混排标题、普通项、广告项或不同尺寸模板时，使用多类型入口。公共注册 API 仍走钓鱼式 `RegisterLoopScroll...` 命名。

```csharp
private readonly List<int> itemTypes = new List<int>();
private readonly Dictionary<int, Type> typeToView = new Dictionary<int, Type>
{
    { 1, typeof(RewardTitleItemView) },
    { 2, typeof(RewardNormalItemView) },
};
private readonly Dictionary<int, GameObject> typeToPrefab = new Dictionary<int, GameObject>
{
    { 1, RewardTitlePrefab },
    { 2, RewardNormalPrefab },
};

private void OnInit()
{
    this.RegisterLoopScrollRectMulti(LoopVerticalScrollRect_RewardList, OnRewardListRectDataMulti);
    this.RegisterLoopScrollMultiClick(LoopVerticalScrollRect_RewardList, OnRewardListMultiClick);

    LoopVerticalScrollRect_RewardList.SetMultiListData(itemTypes, typeToView, typeToPrefab);
    LoopVerticalScrollRect_RewardList.SetTotalCount(rewards);
}

private void OnRewardListRectDataMulti(ItemView item, int index)
{
    switch (item)
    {
        case RewardTitleItemView title:
            title.SetData(rewards[index]);
            break;
        case RewardNormalItemView normal:
            normal.SetData(rewards[index]);
            break;
    }
}
```

`itemTypes.Count` 应与数据列表数量一致；无限列表会按 `itemTypes` 长度循环取 type id。切换数据结构后先更新 `itemTypes`，再调用 `SetMultiListData()` 和 `SetTotalCount()`。

## 瀑布流

```csharp
LoopStaggeredGridView_Waterfall.ResetGridViewLayoutParam(
    items.Count,
    new LoopStaggeredLayoutParam
    {
        ColumnOrRowCount = 2,
        ItemWidthOrHeight = 220f
    },
    index => (items[index].Height, 8f));
```

## 验证

- 当前没有 LoopScroll 专属自动化测试，不需要因此运行整个 `Core.Tests`。未来补充专属测试后，按 [运行 Unity 自动化测试](./run-unity-tests.md) 只运行对应方法或测试类。
- PlayMode 下滚动到列表中后段，确认可见项数量稳定。
- 修改数据后调用 `RefreshCells()`，确认当前可见项刷新但没有大量新对象。
