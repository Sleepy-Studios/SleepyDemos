# 使用 Core 基础 UI 组件

## 目标

本 runbook 面向 Hotfix 页面和 Demo 入口，说明 `Core.Runtime` 中基础交互组件、通用 UGUI/TMP 表现组件和全局扩展的选型规则。交互组件默认同步初始化；需要分帧或异步资源加载时显式传 `isAsync: true`。

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
| `TMPAutoFitLayoutElement` | TMP 文本按最大宽高自适应 LayoutElement 或自身尺寸 |
| `TMPAutoScrollEnableBehaviour` | 单行 TMP 超宽后横向自动滚动 |
| `TMP_UGUI_Extend` | TMP 渐变、描边、阴影、倾斜复合效果 |
| `GradientUI` | 任意 UGUI Graphic 的简单水平或垂直渐变 |
| `FlipImage` | 任意 UGUI Graphic 顶点翻转或旋转 |
| `RoundedCorners` | Image 等 Graphic 的四角圆角和可选 Mask |
| `UIVertexRectMask2D` | 不改材质的矩形硬裁剪 |
| `PyramidLayoutGroup` | 固定尺寸品字或三角形布局 |
| `FlowLayoutGroup` | 不同尺寸子项按宽度换行或按高度换列 |
| `TMPLinkHandler` | 监听 TMP `<link>` 的 linkId 点击事件 |

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

```csharp
gameObject.Show();
button.Hide();
LayoutElement layout = gameObject.GetOrAddComponent<LayoutElement>();
rectTransform.SetSize(new Vector2(320f, 80f));
rectTransform.SetAnchoredPositionX(24f);
canvasGroup.SetCanvasGroupVisible(false);
```

`Show/Hide` 只操作 GameObject 激活状态。CanvasGroup 可见性必须使用 `SetCanvasGroupVisible`，避免调用方误以为两者生命周期一致。

自动滚动既可在 Inspector 绑定 viewport 和 TMP，也可显式初始化：

```csharp
autoScroll.Initialize(viewport, titleText);
autoScroll.SetOptions(TMPAutoScrollOptions.Default);
autoScroll.SetText(title, shouldAutoStart: true);
```

组件会监听 TMP 顶点脏回调，因此业务继续直接写 `titleText.text = title` 也能重新测宽；viewport 默认补 `RectMask2D`。短文本不滚动并恢复原位置、尺寸和 TMP 格式。

`RoundedCorners` 所需 Shader 位于 `Assets/LoadResources/Art/Shaders/UIRoundedCorners.shader`。挂载后组件自动开启 Canvas 的 TexCoord1/TexCoord2；若启用 `Use As Mask` 会补 `Mask`。Item 列表优先复用相同半径以命中材质缓存。

`TMP_UGUI_Extend` 的描边应配合 `TextMeshPro/Mobile/Distance Field` 材质使用。项目版本在 Unity 6 Shader 基础上保留了钓鱼项目的外描边字面补偿；重新导入 TMP Essential Resources 后需要确认 `TMP_SDF-Mobile.shader` 没有被覆盖。项目新建 TMP 文本的默认字体由 `Assets/TextMesh Pro/Resources/TMP Settings.asset` 指向 `HarmonyOS_CN.asset`，已有 TMP 组件仍保留各自序列化字体，需要按需批量或手动替换。

`FlowLayoutGroup` 默认按子节点 `LayoutElement` 或 Graphic/TMP 提供的 preferred size 从左到右排列，宽度不足时换行。需要从上到下排满后换列时将 `Start Axis` 改为 `Vertical`；`Spacing.x/y` 分别表示水平/垂直间距。它适合数量有限且尺寸不同的标签、按钮和筛选项；大量动态数据仍应使用带复用机制的列表，不要把全部 Item 常驻在 LayoutGroup 下。

## 常见误用

- 不要再使用 `ItemImageLoader`，公共图片加载组件统一为 `UIImageLoader`。
- 不要给 Tab、Dropdown 或 Accordion 传图片缩放参数；需要布局尺寸时改 prefab 布局。
- 不要把 `Register` 当覆盖回调用；重复打开页面时应成对 `Unregister`，或使用组件提供的覆盖入口。
- 不要在 `ViewList` 初始化回调里依赖外部循环变量；使用 `Action<TView, TData, int>` 的 index 参数。
- `ViewList` 仅适合有限数量的 View 项；大量数据列表需要重新评估并单独设计。
- 不要把 `CanvasGroup.SetCanvasGroupVisible` 当作 GameObject 激活切换，也不要再增加同名 `CanvasGroup.Show/Hide`。
- 不要从 Hotfix 直接复制大型 `UIUtil` 或第三方包的内部扩展到 `Core.Runtime/Extends`。
- `TMPAutoScrollView.cs` 属于钓鱼项目生成的 ItemView 适配层；当前项目直接使用 `TMPAutoScrollEnableBehaviour`，只有真实 View 调用方需要时才补薄适配。

## 验证

修改这些组件或调用方式后，运行：

- `Core.Tests.UI.UIViewPrefabConventionTests`
- `Core.Tests.UI.CoreUIComponentMigrationTests`

圆角、渐变、翻转、矩形裁剪和自动滚动还应在真实 Canvas 中做一次 Play Mode 目视检查；当前仓库没有可用的 `UIFrameworkValidation` 页面或菜单，按业务 Prefab 接入后验证即可。

完整步骤见 [运行 Unity 自动化测试](./run-unity-tests.md)。
