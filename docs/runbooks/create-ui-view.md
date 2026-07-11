# 接入 Core UI View

## 适用场景

用于在 Hotfix 或 Demo 中新增业务 View，或整理已有 UI Prefab，使其进入 Core UI 的固定层级 Canvas。

## 前置条件

- View 资源位于 `Assets/LoadResources/UI` 或对应 Demo 的可加载资源目录。
- View 代码继承 `Core.Runtime.View` 或其泛型版本。
- 通过 `UIManager.Show<T>()`、`Preload<T>()` 和 `Close<T>()` 管理生命周期。

## 制作 Prefab

1. 使用全屏或内容尺寸明确的 `RectTransform` 作为 Prefab 根节点。
2. 根节点使用 UI Layer。
3. 不要在根节点添加以下组件：
   - `Canvas`
   - `CanvasScaler`
   - `GraphicRaycaster`
4. 按现有 MvcBind 流程生成组件绑定和 View 代码。
5. 在生成的 View Component 中选择正确的 `UILayer`。

固定层的用途如下：

| 层级 | 用途 |
| --- | --- |
| `Underground` | 业务 UI 下方的纯展示内容，不接收 UI 射线 |
| `Base` | 主页面和常规全屏界面 |
| `Foreground` | Base 上方的前景内容 |
| `Pop` | 弹窗及其遮罩 |
| `Decorate` | 挂件、HUD 和装饰性浮层 |
| `Tip` | 最高优先级提示和临时交互 |

## 制作透视效果

整个 View 都需要透视旋转时，直接旋转根 `RectTransform`：

```csharp
view.transform.localRotation = Quaternion.Euler(10f, -20f, 0f);
```

只需要旋转弹窗主体时，在 Prefab 内增加普通 `PerspectiveRoot`：

```text
ViewRoot
├── Blocker
└── PerspectiveRoot
    └── Panel
```

旋转 `PerspectiveRoot`，让全屏遮罩、适配根节点和点击区域保持平面。`PerspectiveRoot` 只是命名约定，不需要添加框架组件。

## 局部 Sub-Canvas

不要为了独立排序默认添加 Sub-Canvas。只有 Unity Profiler 显示某个持续动画使整个固定层频繁重建时，才在对应视觉节点增加局部 Canvas。

局部 Canvas 必须遵守：

- 不添加 `CanvasScaler`。
- 默认 `overrideSorting=false`，继承所属固定层排序。
- 只覆盖高频变化的视觉子树。
- 修改前后对比 Canvas Build Batch、CPU 和 Draw Call，确认优化有效。

## 常见问题

### View 显示顺序不对

先检查 `UILayer` 和同层 sibling 顺序。不要给 View 根节点添加 Canvas 或自行修改全局 `sortingOrder`。

### 绕 X/Y 轴旋转没有透视

确认 View 由 Core UI 打开，并位于 `UIRootCanvas` 的固定子 Canvas 下。独立 Overlay Canvas 不经过透视 `UICamera`。

### 点击区域跟着主体倾斜

将全屏 Blocker 留在 View 根节点，只把需要倾斜的主体放进 `PerspectiveRoot`。

## 验证方式

1. 运行 `Core.Tests.UI.UIViewPrefabConventionTests`，确认 Prefab 根节点规则通过。
2. 运行 `Core.Tests.UI.UIRootManagerPlayModeTests`。
3. 从 `AppEntrance` 进入主界面，验证显示、点击、关闭和返回。
4. 临时旋转 View 或 `PerspectiveRoot` 的 X/Y 轴，确认透视效果和射线区域符合预期。
5. 在 `16:9`、超宽和窄屏 Game View 下检查布局。
