# LoopScroll 运行时

## 目标

LoopScroll 是 Core.Runtime 的公共 UGUI 循环列表模块，用于承载大量数据、无限滚动、Grid 和瀑布流等界面需求。它参考钓鱼项目的 `LoopScrollRect` 调用方式，但实现收口在 SleepyDemos 自己的 `Core.Runtime` 命名空间，不依赖旧项目或第三方 SuperScrollView 源码。

## 关键结构

- `LoopScrollRect`：循环列表基类，负责 ScrollRect 绑定、可见项复用、数据刷新、跳转、选中态和回收。
- `LoopVerticalScrollRect` / `LoopHorizontalScrollRect`：普通垂直或水平循环列表。
- `LoopGridView`：固定行或固定列 Grid，提供 index 与 row/column 互转。
- `LoopStaggeredGridView`：瀑布流布局，支持按 item 提供尺寸。
- `LoopScrollMultiItemSource`：多预制体/多类型 item 源，按 type id 分池复用，支持每个数据索引映射到不同 `ItemView` 类型和不同 prefab。
- `RegisterExtend`：提供钓鱼式注册 API，例如 `RegisterLoopScrollRect<TView>()`、`RegisterLoopScrollClick<TView>()`。

## 使用边界

- 大量数据、滚动复用、无限滚动、Grid 或瀑布流：使用 LoopScroll。
- 少量固定项、只需要显示/隐藏和选择：继续使用 `ViewList` 或 `UITab`。
- 不在 Hotfix 中复制钓鱼项目 `Game.Main`、`UnityEngine.UI.LoopScrollRect` 或 SuperScrollView 代码。

## 生命周期

1. 预制体上配置 `ScrollRect`、`Viewport`、`Content` 和对应 LoopScroll 组件。
2. View 初始化时调用 `RegisterLoopScrollRect<TItemView>()` 注册填充回调。
3. 多预制体列表先调用 `RegisterLoopScrollRectMulti()`，再用 `SetMultiListData(typeList, typeToView, typeToPrefab)` 配置类型表。
4. 数据变化时调用 `SetTotalCount(list)` 或 `RefreshCells()`。
5. 需要定位时调用 `ScrollToCell()` 或 `ScrollToCellWithinTime()`。
6. View 销毁时由组件回收池对象并释放临时节点。

## 多预制体规则

- `typeList[index]` 表示第 `index` 条数据使用的类型 id。
- `typeToView[typeId]` 必须指向 `ItemView` 派生类。
- `typeToPrefab[typeId]` 可选；缺省时运行时会创建空 `RectTransform` 节点，正式 UI 应提供 prefab。
- 回收池按 type id 隔离，不会把 A 类型 prefab 复用到 B 类型数据上。
- 无限列表中 type id 会按 `typeList` 长度循环取值。

## 验证重点

- 滚动后已创建 item 数量应接近可见区容量，而不是跟随数据总数增长。
- `ViewList` 不应出现循环列表或无限滚动逻辑。
- Hotfix 可引用 `Core.Runtime.LoopScrollRect` 系列类型，但不应引用旧项目命名空间。
