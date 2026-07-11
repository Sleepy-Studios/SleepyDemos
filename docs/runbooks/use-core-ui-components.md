# 使用 Core 基础 UI 组件

## 目标

本 runbook 面向 Hotfix 页面和 Demo 入口，说明 `Core.Runtime` 中 8 个基础 UI 组件的选型和调用规则。组件默认使用同步初始化；需要分帧或异步资源加载时显式传 `isAsync: true`。

## 通用规则

- 每个组件只使用一个主入口：`Init(...)`、`SetData(...)` 或 `SetImage(...)`。
- `Register` 表示追加回调，`Unregister` 表示移除回调，`SetAction` 表示覆盖回调。
- `notify` 控制本次设置是否触发回调；静默回显或初始化默认值时传 `false`。
- `isAsync` 控制初始化和资源加载方式；`false` 为同步，`true` 为异步。
- 图片参数统一传 Sprite 资源路径，不传图集名、不传缩放值。

## 组件选型

| 组件 | 适用场景 |
| --- | --- |
| `UITab` | 有限数量 Tab，只负责选中态、文案、图标和点击回调 |
| `AccordionTab` | 两级手风琴 Tab，回调使用扁平化叶子索引 |
| `ViewTab` | 用 `UITab` 驱动多个 View 或本地分页对象 |
| `AccordionViewTab` | 用 `AccordionTab` 叶子索引驱动多个 View 或本地分页对象 |
| `ViewList` | 有限 View 列表，不承担循环滚动 |
| `UIDropdown` | 基于 `UITab` 的基础下拉选择 |
| `UIBtnSwitch` | 二态按钮开关 |
| `UIImageLoader` | 按资源路径给 `Image` 加载 Sprite |

## 常用调用

```csharp
tab.Init(
    desc: labels,
    itemImages: iconPaths,
    initIndex: 0,
    notify: false,
    action: OnTabReady,
    isAsync: true);
tab.Register(OnTabSelected);
```

```csharp
viewTab.Init(
    desc: labels,
    views: views,
    itemImages: iconPaths,
    index: 0,
    enableAnimation: true,
    isAsync: false);
viewTab.Register(OnViewTabSelected);
```

```csharp
dropdown.SetData(
    values: options,
    action: OnDropdownSelected,
    selectedIndex: 0,
    selectedText: null,
    itemImages: optionIconPaths,
    showStateChanged: OnDropdownShowStateChanged,
    isAsync: true);
dropdown.SetSelectedIndex(savedIndex);
```

```csharp
switchButton.SetAction(OnSwitchChanged);
switchButton.SetStatus(savedValue, notify: false);
switchButton.Register(OnSwitchAnalytics);
```

```csharp
imageLoader.SetImage("LoadResources/UI/Icons/IconStart", setNativeSize: true, isAsync: false);
imageLoader.Clear();
```

## 常见误用

- 不要再使用 `ItemImageLoader`，公共图片加载组件统一为 `UIImageLoader`。
- 不要给 Tab、Dropdown 或 Accordion 传图片缩放参数；需要布局尺寸时改 prefab 布局。
- 不要把 `Register` 当覆盖回调用；重复打开页面时应成对 `Unregister`，或使用组件提供的覆盖入口。
- 不要在 `ViewList` 初始化回调里依赖外部循环变量；使用 `Action<TView, TData, int>` 的 index 参数。
- 不要把无限滚动需求塞进 `ViewList`；大量列表使用 LoopScroll 组件。

## 验证

修改这些组件或调用方式后，运行：

- `Core.Tests.UI.UIViewPrefabConventionTests`
- 可用时运行 `Tools/SleepyDemos/UI Framework Validation/Validate Generated Prefabs`

完整步骤见 [运行 Unity 自动化测试](./run-unity-tests.md)。
