# 大型项目 UI 导航与过渡系统实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 将现有 Core UI 升级为可等待、可取消、可回滚，并能由 Hotfix 扩展 UI 与世界/相机过渡的大型项目 UI 运行时。

**架构：** 保留 UIManager 门面，把写栈和切换顺序收口到单写入者 `UINavigationCoordinator`。View 稳定持有 UI Transition；Core 只定义 World Transition Provider，具体 Camera/场景联动由 Hotfix 实现。所有导航能力先写精确测试，再做最小实现。

**技术栈：** Unity 6000.3、uGUI、UniTask、YooAsset、DOTween、HybridCLR、NUnit、Unity Test Runner、MvcBind。

---

## 实施边界与文件结构

### 创建

- `Assets/Scripts/Core/Runtime/UI/Navigation/UINavigationContracts.cs`：ViewMode、Action、Status、Options、Result、TransitionContext。
- `Assets/Scripts/Core/Runtime/UI/Navigation/UINavigationCoordinator.cs`：FIFO 操作队列、当前操作取消、事务执行和回滚。
- `Assets/Scripts/Core/Runtime/UI/Transition/UITransitionInterfaces.cs`：UI / World Transition 及 Provider 接口。
- `Assets/Scripts/Core/Runtime/UI/Transition/EmptyUITransition.cs`：无动画稳定终态实现。
- `Assets/Scripts/Core/Runtime/UI/Transition/FadeScaleUITransition.cs`：DOTween 默认 UI 过渡。
- `Assets/Scripts/Core/Runtime/UI/Transition/UIInteractionGate.cs`：过渡期间透明射线拦截与引用计数。
- `Assets/Scripts/Hotfix/AppDelegate/HotfixWorldTransitionProvider.cs`：Hotfix 世界过渡解析入口，首版返回空实现。
- `Assets/Scripts/Tests/Core/EditMode/UINavigationContractsTests.cs`：纯契约和默认映射测试。
- `Assets/Scripts/Tests/Core/EditMode/UIStackTests.cs`：Page / Modal / Widget 纯栈测试。
- `Assets/Scripts/Tests/Core/EditMode/MvcBindTransitionGenerationTests.cs`：稳定 Transition 实例生成测试。
- `Assets/Scripts/Tests/Core/PlayMode/UIViewLifecyclePlayModeTests.cs`：View 状态、加载和 Transition 生命周期测试。
- `Assets/Scripts/Tests/Core/PlayMode/UIManagerNavigationPlayModeTests.cs`：队列、取消、回滚、Back、Mask 测试。
- `Assets/Scripts/Tests/Core/PlayMode/UITransitionPlayModeTests.cs`：默认过渡与 InteractionGate 测试。
- `Assets/Scripts/Tests/Core/PlayMode/UIWorldTransitionPlayModeTests.cs`：Provider 和世界过渡时序测试。

### 修改

- `Assets/Scripts/Core/Runtime/UI/Core/UIEnums.cs`：将 ViewState 改为单值生命周期；保留 UILayer / MaskType。
- `Assets/Scripts/Core/Runtime/UI/Core/View.cs`：稳定 Transition 所有权、单值状态、可取消加载和完整生命周期。
- `Assets/Scripts/Core/Runtime/UI/Manager/UIManager.cs`：异步门面、兼容 API、事件和 Coordinator 装配。
- `Assets/Scripts/Core/Runtime/UI/Manager/UIStack.cs`：收敛为纯导航状态模型。
- `Assets/Scripts/Core/Runtime/UI/Manager/StackData.cs`：拆分 Page / Modal 数据，不保存进行中操作。
- `Assets/Scripts/Core/Runtime/UI/Manager/UICache.cs`：失败实例移除和只读快照。
- `Assets/Scripts/Core/Runtime/UI/Manager/UIRootManager.cs`：创建 InteractionGate，公开初始化后的层 Root。
- `Assets/Scripts/Core/Runtime/UI/Animation/UIAnimationInterfaces.cs`：删除旧契约；迁移完成后删除文件与 meta。
- `Assets/Scripts/Core/Editor/MvcBind/MvcBindData.cs`：新增 ViewMode 和 Transition 类型配置。
- `Assets/Scripts/Core/Editor/MvcBind/MvcBindWindow.cs`：更新选择控件和命名。
- `Assets/Scripts/Core/Editor/MvcBind/MvcCodeGenerator.cs`：生成缓存字段，不生成表达式体 `new`。
- `Assets/Scripts/Core/Runtime/Core.Runtime.asmdef`：显式引用 `DOTween.Modules`；若使用自定义 UniTask 适配，不增加不存在的 `UNITASK_DOTWEEN_SUPPORT`。
- `Assets/Scripts/Hotfix/AppDelegate/HotfixEntry.cs`：启动时注册 World Transition Provider，并等待主菜单打开结果。
- `docs/architecture/ui-rendering.md`：补 InteractionGate 与 Transition 渲染边界。
- `docs/modules/ui-runtime.md`：补导航事务、状态机和扩展点。
- `docs/runbooks/create-ui-view.md`：补 ViewMode、Transition 和异步 API 接入步骤。
- `docs/runbooks/run-unity-tests.md`：修正不存在的项目技能路径，统一指向当前可用 UnitySkills 入口。

### 删除

- `Assets/Scripts/Core/Runtime/UI/Animation/UIAnimationInterfaces.cs`
- `Assets/Scripts/Core/Runtime/UI/Animation/UIAnimationInterfaces.cs.meta`

删除只在新 Transition 契约编译通过、所有旧引用迁移完成后执行。

## 统一验证方式

每次调用 UnitySkills 前：

1. 读取 `%USERPROFILE%/.unity_skills/registry.json`，按绝对项目路径 `D:/Unity/Unity_Project/SleepyDemos` 取端口。
2. registry 不可信时扫描 `http://localhost:8090-8100/health`。
3. 核对 `projectName=SleepyDemos`、`unityVersion=6000.3.15f1` 和 `instanceId`。
4. 调用 `test_run_by_name(testName="完整测试类或方法名", testMode="EditMode|PlayMode")`。
5. 保存返回的 `jobId`，只轮询 `test_get_result(jobId)`；完成前不启动第二个 TestRunner job。
6. 每个任务只运行列出的精确类；本计划不授权全量测试。

不得使用 `dotnet build`、`msbuild` 或另起 BatchMode Unity。

### 任务 1：建立导航契约和默认映射

**文件：**

- 创建：`Assets/Scripts/Core/Runtime/UI/Navigation/UINavigationContracts.cs`
- 创建：`Assets/Scripts/Tests/Core/EditMode/UINavigationContractsTests.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Core/UIEnums.cs`

- [ ] **步骤 1：编写失败的契约测试**

```csharp
using Core.Runtime;
using NUnit.Framework;

namespace Core.Tests.UI
{
    public sealed class UINavigationContractsTests
    {
        [TestCase(UILayer.Base, UIViewMode.Page)]
        [TestCase(UILayer.Foreground, UIViewMode.Page)]
        [TestCase(UILayer.Pop, UIViewMode.Modal)]
        [TestCase(UILayer.Decorate, UIViewMode.Widget)]
        [TestCase(UILayer.Tip, UIViewMode.Widget)]
        public void ResolveDefaultViewMode_ReturnsCompatibilityMapping(UILayer layer, UIViewMode expected)
        {
            Assert.That(UIViewModeResolver.Resolve(layer), Is.EqualTo(expected));
        }

        [Test]
        public void SucceededResult_ContainsViewAndNoException()
        {
            var view = new View();
            var result = UIOperationResult.Succeeded(7, UINavigationAction.Push, view);
            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(result.OperationId, Is.EqualTo(7));
            Assert.That(result.View, Is.SameAs(view));
            Assert.That(result.Exception, Is.Null);
        }
    }
}
```

- [ ] **步骤 2：运行 `Core.Tests.UI.UINavigationContractsTests`，确认因类型不存在而失败**

调用 `test_run_by_name(testName="Core.Tests.UI.UINavigationContractsTests", testMode="EditMode")`；预期 FAIL，错误包含 `UIViewMode` 或 `UIOperationResult` 未定义。

- [ ] **步骤 3：实现完整契约**

`UINavigationContracts.cs` 至少定义以下公共 API，名称在后续任务中不得改变：

```csharp
using System;
using System.Threading;

namespace Core.Runtime
{
    public enum UIViewMode { Page, Modal, Widget }
    public enum UINavigationAction { Push, Replace, Close, Back, Preload, CloseAll }
    public enum UIOperationStatus { Succeeded, Canceled, Failed, Ignored }
    public enum UITransitionDirection { Enter, Exit }

    public readonly struct UIShowOptions
    {
        private readonly bool? animated;
        public UIShowOptions(bool animated) => this.animated = animated;
        public bool Animated => animated ?? true;
    }

    public readonly struct UIOperationResult
    {
        private UIOperationResult(long operationId, UINavigationAction action,
            UIOperationStatus status, View view, Exception exception)
        {
            OperationId = operationId;
            Action = action;
            Status = status;
            View = view;
            Exception = exception;
        }

        public long OperationId { get; }
        public UINavigationAction Action { get; }
        public UIOperationStatus Status { get; }
        public View View { get; }
        public Exception Exception { get; }

        public static UIOperationResult Succeeded(long id, UINavigationAction action, View view) =>
            new UIOperationResult(id, action, UIOperationStatus.Succeeded, view, null);
        public static UIOperationResult Ignored(long id, UINavigationAction action, View view) =>
            new UIOperationResult(id, action, UIOperationStatus.Ignored, view, null);
        public static UIOperationResult Canceled(long id, UINavigationAction action, View view) =>
            new UIOperationResult(id, action, UIOperationStatus.Canceled, view, null);
        public static UIOperationResult Failed(long id, UINavigationAction action, View view, Exception exception) =>
            new UIOperationResult(id, action, UIOperationStatus.Failed, view, exception);
    }

    public static class UIViewModeResolver
    {
        public static UIViewMode Resolve(UILayer layer) => layer switch
        {
            UILayer.Pop => UIViewMode.Modal,
            UILayer.Decorate => UIViewMode.Widget,
            UILayer.Tip => UIViewMode.Widget,
            _ => UIViewMode.Page
        };
    }
}
```

将 `ViewState` 改成 `Created / Loading / LoadedHidden / Entering / Visible / Exiting / Destroying / Destroyed / Faulted` 单值枚举。

- [ ] **步骤 4：再次运行精确测试并确认通过**

预期 `totalTests=6`、`failedTests=0`。

- [ ] **步骤 5：提交**

```powershell
git add Assets/Scripts/Core/Runtime/UI/Core/UIEnums.cs Assets/Scripts/Core/Runtime/UI/Navigation Assets/Scripts/Tests/Core/EditMode/UINavigationContractsTests.cs
git commit -m "feat(ui): 定义导航事务契约"
```

### 任务 2：把 UIStack 收敛为纯状态模型

**文件：**

- 创建：`Assets/Scripts/Tests/Core/EditMode/UIStackTests.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UIStack.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/StackData.cs`
- 修改：`Assets/Scripts/Core/Editor/Properties/AssemblyInfo.cs`

- [ ] **步骤 1：把 `UIStack` 暴露给 EditMode 测试并编写失败测试**

在现有 `AssemblyInfo.cs` 增加 `InternalsVisibleTo("Core.Tests.EditMode")` 时先检查是否已存在，禁止重复。测试覆盖：Push Page、Present Modal、Add Widget、Back 优先 Modal、关闭 Widget 不改变 Page。

```csharp
[Test]
public void Back_WhenModalExists_RemovesModalBeforePage()
{
    var stack = new UIStack();
    var page = new FakeView(UIViewMode.Page, UILayer.Base);
    var modal = new FakeView(UIViewMode.Modal, UILayer.Pop);
    stack.CommitShow(page);
    stack.CommitShow(modal);

    var removed = stack.CommitBack();

    Assert.That(removed, Is.SameAs(modal));
    Assert.That(stack.CurrentPage, Is.SameAs(page));
}
```

- [ ] **步骤 2：运行 `Core.Tests.UI.UIStackTests`，确认旧 API 不满足测试**

- [ ] **步骤 3：实现纯状态 API**

`UIStack` 不再持有 Mask、Button 或调用 `View.Show/Hide`，只保留：

```csharp
internal View CurrentPage { get; }
internal View TopModal { get; }
internal IReadOnlyList<View> Pages { get; }
internal IReadOnlyList<View> Modals { get; }
internal IReadOnlyList<View> Widgets { get; }
internal void CommitShow(View view);
internal bool CommitClose(View view);
internal View CommitBack();
internal UIStackSnapshot Capture();
internal void Restore(UIStackSnapshot snapshot);
internal void Clear();
```

`CommitShow` 按 `View.ViewMode` 分流；同一个单实例 View 重复提交只调整到对应集合顶部，不重复增加引用。

- [ ] **步骤 4：运行 `UIStackTests` 并确认通过**

- [ ] **步骤 5：提交**

```powershell
git add Assets/Scripts/Core/Runtime/UI/Manager/UIStack.cs Assets/Scripts/Core/Runtime/UI/Manager/StackData.cs Assets/Scripts/Core/Editor/Properties/AssemblyInfo.cs Assets/Scripts/Tests/Core/EditMode/UIStackTests.cs
git commit -m "refactor(ui): 分离导航栈与界面表现"
```

### 任务 3：修复 MvcBind Transition 实例所有权

**文件：**

- 创建：`Assets/Scripts/Core/Runtime/UI/Transition/UITransitionInterfaces.cs`
- 创建：`Assets/Scripts/Core/Runtime/UI/Transition/EmptyUITransition.cs`
- 创建：`Assets/Scripts/Tests/Core/EditMode/MvcBindTransitionGenerationTests.cs`
- 修改：`Assets/Scripts/Core/Editor/MvcBind/MvcBindData.cs`
- 修改：`Assets/Scripts/Core/Editor/MvcBind/MvcBindWindow.cs`
- 修改：`Assets/Scripts/Core/Editor/MvcBind/MvcCodeGenerator.cs`

- [ ] **步骤 1：编写生成器回归测试**

测试用 `MvcBindSettings.uiTransitionType = typeof(EmptyUITransition).FullName` 生成文本，断言生成工厂方法，不覆盖每次读取都会创建对象的属性：

```csharp
StringAssert.Contains("protected override IUITransition CreateUITransition()", source);
StringAssert.Contains("return new Core.Runtime.EmptyUITransition();", source);
StringAssert.DoesNotContain("public override IUITransition UITransition", source);
```

需要先把“生成 Component 文本”抽成不写磁盘的 `internal static string CreateComponentScriptText(...)`，生产写文件和测试共用该方法。

- [ ] **步骤 2：运行 `MvcBindTransitionGenerationTests`，确认失败**

- [ ] **步骤 3：定义 Transition 接口**

```csharp
public interface IUITransition : IDisposable
{
    void Initialize(Transform root);
    UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken);
    UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken);
    void CompleteImmediately(UITransitionDirection direction);
}

public interface IUIWorldTransition
{
    UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken);
    UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken);
    void CompleteImmediately(UITransitionDirection direction);
}

public interface IUIWorldTransitionProvider
{
    IUIWorldTransition Resolve(View view);
}
```

`UITransitionContext` 放在导航契约文件，字段为 OperationId、Action、EnteringView、ExitingView、Animated。

- [ ] **步骤 4：更新 MvcBind 配置和生成器**

将 `uiAnimationType` 改为 `uiTransitionType`；将 `cameraAnimationType` 改为 `worldTransitionKey`。生成的 partial View 只覆盖 `CreateUITransition()`，缓存和 Dispose 由 View 基类负责；ViewMode 必须显式生成。World Transition 不在生成类里 `new`，只生成字符串 Key 或由 Provider 按 View 类型解析。

- [ ] **步骤 5：运行生成器测试并确认通过**

- [ ] **步骤 6：提交**

```powershell
git add Assets/Scripts/Core/Runtime/UI/Transition Assets/Scripts/Core/Editor/MvcBind Assets/Scripts/Tests/Core/EditMode/MvcBindTransitionGenerationTests.cs
git commit -m "fix(editor): 生成稳定的 UI Transition 实例"
```

### 任务 4：重构 View 生命周期并保证资源回收

**文件：**

- 创建：`Assets/Scripts/Tests/Core/PlayMode/UIViewLifecyclePlayModeTests.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Core/View.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UICache.cs`

- [ ] **步骤 1：编写 Fake Loader 与生命周期失败测试**

测试类内部实现 `FakeResourceLoader`，记录 InstantiateAsync、ReleaseInstance、Dispose 次数；`FakeTransition` 记录 Initialize/Enter/Exit/Dispose 顺序。覆盖加载成功、加载 null、销毁等待加载、Transition 始终为同一实例。

```csharp
[UnityTest]
public IEnumerator LoadAsync_WhenResourceIsNull_ReturnsFalseAndDisposesLoader()
{
    var loader = new FakeResourceLoader { AsyncResult = null };
    var view = new FakeView(loader, new FakeTransition());
    var parent = new GameObject("ViewParent");
    yield return view.LoadAsync(parent.transform, CancellationToken.None).ToCoroutine();
    Object.Destroy(parent);
    Assert.That(view.State, Is.EqualTo(ViewState.Faulted));
    Assert.That(loader.DisposeCount, Is.EqualTo(1));
}
```

- [ ] **步骤 2：运行 `Core.Tests.UI.UIViewLifecyclePlayModeTests`，确认失败**

- [ ] **步骤 3：实现 View 新生命周期**

新增并固定以下入口：

```csharp
public virtual UIViewMode ViewMode => UIViewModeResolver.Resolve(Level);
public IUITransition UITransition { get; private set; }
protected virtual IUITransition CreateUITransition() => new EmptyUITransition();
public async UniTask<bool> LoadAsync(Transform parent, CancellationToken cancellationToken);
internal async UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken);
internal async UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken);
public async UniTask DestroyAsync();
```

规则：Transition 只在 Load 成功后 Initialize 一次；State 在调用生命周期 Hook 前先切到对应进行态；取消抛 `OperationCanceledException` 给 Coordinator；加载 null 设置 Faulted、释放 Loader；Destroy 对同一 View 幂等。

- [ ] **步骤 4：运行 View 生命周期测试并确认通过**

- [ ] **步骤 5：提交**

```powershell
git add Assets/Scripts/Core/Runtime/UI/Core/View.cs Assets/Scripts/Core/Runtime/UI/Manager/UICache.cs Assets/Scripts/Tests/Core/PlayMode/UIViewLifecyclePlayModeTests.cs
git commit -m "refactor(ui): 建立可取消的 View 生命周期"
```

### 任务 5：实现导航队列、异步门面和事务回滚

**文件：**

- 创建：`Assets/Scripts/Core/Runtime/UI/Navigation/UINavigationCoordinator.cs`
- 创建：`Assets/Scripts/Tests/Core/PlayMode/UIManagerNavigationPlayModeTests.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UIManager.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UIStack.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UICache.cs`

- [ ] **步骤 1：编写最关键竞态测试**

至少先写并确认失败：

```csharp
[UnityTest]
public IEnumerator CloseDuringLoad_DoesNotShowGhostView()
{
    var showTask = manager.ShowAsync<SlowFakeView>();
    var closeTask = manager.CloseAsync<SlowFakeView>();
    loader.CompleteInstantiation();
    yield return UniTask.WhenAll(showTask, closeTask).ToCoroutine();
    Assert.That(manager.Get<SlowFakeView>(), Is.Null);
    Assert.That(manager.StackCount, Is.Zero);
}
```

同时覆盖不同 Page 快速 Show 保持调用顺序、加载失败恢复旧 Page、重复 Show 返回 Ignored。

- [ ] **步骤 2：运行 `UIManagerNavigationPlayModeTests`，确认失败**

- [ ] **步骤 3：实现 FIFO Coordinator**

Coordinator 使用 `Queue<IQueuedUIOperation>`、单个 Pump 和 `UniTaskCompletionSource<UIOperationResult>`。Plain C# Coordinator 自己持有 `CancellationTokenSource`，不调用 `GetCancellationTokenOnDestroy()`。同类型反向操作调用当前 CTS.Cancel；`CloseAll` 取消当前并把待执行项完成为 Canceled。

不要重复 await 同一个 UniTask；需被多个路径等待的加载任务在 View 内使用 `.Preserve()`。

- [ ] **步骤 4：在 UIManager 增加异步 API**

```csharp
public UniTask<UIOperationResult> ShowAsync<T>(UIShowOptions options = default,
    CancellationToken cancellationToken = default) where T : View;
public UniTask<UIOperationResult> ReplaceAsync<T>(UIShowOptions options = default,
    CancellationToken cancellationToken = default) where T : View;
public UniTask<UIOperationResult> CloseAsync<T>(bool animated = true,
    CancellationToken cancellationToken = default) where T : View;
public UniTask<UIOperationResult> BackAsync(bool animated = true,
    CancellationToken cancellationToken = default);
public UniTask<UIOperationResult> CloseAllAsync(CancellationToken cancellationToken = default);
```

旧 `Show<T>()` / `Close<T>()` 只作为兼容包装，包装任务必须 `.Forget(LogOperationFailure)`，禁止静默丢异常。

- [ ] **步骤 5：实现事务快照和失败回滚**

正式栈只在 Load 成功后提交。异常捕获顺序必须区分 `OperationCanceledException` 和其它 Exception；失败 View 从 Cache 移除并 Destroy；旧 View 恢复 Visible、Mask 和 sibling。

- [ ] **步骤 6：运行导航测试并确认通过**

- [ ] **步骤 7：提交**

```powershell
git add Assets/Scripts/Core/Runtime/UI/Navigation Assets/Scripts/Core/Runtime/UI/Manager Assets/Scripts/Tests/Core/PlayMode/UIManagerNavigationPlayModeTests.cs
git commit -m "feat(ui): 增加可回滚的异步导航事务"
```

### 任务 6：增加默认 UI Transition、交互锁和 Mask 协调

**文件：**

- 创建：`Assets/Scripts/Core/Runtime/UI/Transition/FadeScaleUITransition.cs`
- 创建：`Assets/Scripts/Core/Runtime/UI/Transition/UIInteractionGate.cs`
- 创建：`Assets/Scripts/Tests/Core/PlayMode/UITransitionPlayModeTests.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UIRootManager.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UIManager.cs`
- 修改：`Assets/Scripts/Core/Runtime/Core.Runtime.asmdef`

- [ ] **步骤 1：编写 Transition 与 Gate 失败测试**

覆盖 Enter 最终 alpha=1/scale=1，Exit 最终 alpha=0，取消后 `CompleteImmediately` 落到指定终态，以及嵌套 Acquire/Release 引用计数。

- [ ] **步骤 2：运行 `Core.Tests.UI.UITransitionPlayModeTests`，确认失败**

- [ ] **步骤 3：显式引用 DOTween Modules 并实现默认过渡**

`Core.Runtime.asmdef` 增加 `DOTween.Modules`。使用 `CanvasGroup.DOFade`、`Transform.DOScale`、`Sequence.Join`、`SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)`。

当前项目的 UniTask DOTween asmdef 依赖包名 `com.demigiant.dotween`，而 DOTween 是 Assets 插件，不能假设 `ToUniTask` 已启用。使用项目内适配：`UniTaskCompletionSource` + `OnComplete` + CancellationToken 注册；取消时 `Kill(false)` 并 `TrySetCanceled(token)`。不得修改 `Library/PackageCache` 或全局添加 `UNITASK_DOTWEEN_SUPPORT`。

- [ ] **步骤 4：创建 InteractionGate**

由 UIRootManager 在 Tip 层创建透明全屏 Image，默认禁用 Raycast；`Acquire()` 从 0→1 时启用，`Release()` 到 0 时禁用。重复 Release 记录错误并归零，不允许负计数。

- [ ] **步骤 5：让 Coordinator 在 try/finally 中成对 Acquire/Release**

Mask 只跟随 TopModal；Gate 只跟随进行中的导航操作。测试确认两者互不改变 alpha、parent 和 interactable。

- [ ] **步骤 6：运行 Transition 测试和既有 Root 测试**

依次运行：

- `Core.Tests.UI.UITransitionPlayModeTests`
- `Core.Tests.UI.UIRootManagerPlayModeTests`

两次必须串行；预期均 `failedTests=0`。

- [ ] **步骤 7：提交**

```powershell
git add Assets/Scripts/Core/Runtime/Core.Runtime.asmdef Assets/Scripts/Core/Runtime/UI/Transition Assets/Scripts/Core/Runtime/UI/Manager/UIRootManager.cs Assets/Scripts/Core/Runtime/UI/Manager/UIManager.cs Assets/Scripts/Tests/Core/PlayMode/UITransitionPlayModeTests.cs
git commit -m "feat(ui): 增加可取消的默认过渡与交互锁"
```

### 任务 7：建立 Hotfix World / Camera Transition 扩展点

**文件：**

- 创建：`Assets/Scripts/Hotfix/AppDelegate/HotfixWorldTransitionProvider.cs`
- 创建：`Assets/Scripts/Tests/Core/PlayMode/UIWorldTransitionPlayModeTests.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Manager/UIManager.cs`
- 修改：`Assets/Scripts/Core/Runtime/UI/Navigation/UINavigationCoordinator.cs`
- 修改：`Assets/Scripts/Hotfix/AppDelegate/HotfixEntry.cs`

- [ ] **步骤 1：使用 Fake Provider 编写时序测试**

断言 Push B 的顺序为：B Load → A UI Exit 与 A World Exit → 栈提交 → B World Enter 与 B UI Enter → B Entered。UI 与 World 同一阶段用 `UniTask.WhenAll` 并行，前后阶段不得交错。

- [ ] **步骤 2：运行 `Core.Tests.UI.UIWorldTransitionPlayModeTests`，确认失败**

- [ ] **步骤 3：在 UIManager 注册 Provider**

```csharp
public IUIWorldTransitionProvider WorldTransitionProvider { get; private set; }

public void RegisterWorldTransitionProvider(IUIWorldTransitionProvider provider)
{
    WorldTransitionProvider = provider;
}
```

Coordinator 在每次操作开始时只 Resolve 一次进入和退出 Transition，保证同一事务实例稳定。Provider 为空时使用 Empty World Transition。

- [ ] **步骤 4：实现 Hotfix Provider 并在入口注册**

首版 `HotfixWorldTransitionProvider.Resolve(View view)` 返回 null，不创建虚假的相机移动。添加按 `Type` 注册工厂的字典 API，未来真实 Demo 在 Hotfix 注册具体实现，不修改 Core。

- [ ] **步骤 5：让 HotfixEntry 等待主菜单稳定进入**

将入口改为 await `UIManager.Instance.ShowAsync<MainMenuView>()`，Status 为 Failed 时抛出包含 Exception 的启动错误；Canceled 视为启动中断。

- [ ] **步骤 6：运行 World Transition 和导航测试**

串行运行：

- `Core.Tests.UI.UIWorldTransitionPlayModeTests`
- `Core.Tests.UI.UIManagerNavigationPlayModeTests`

- [ ] **步骤 7：提交**

```powershell
git add Assets/Scripts/Core/Runtime/UI Assets/Scripts/Hotfix/AppDelegate Assets/Scripts/Tests/Core/PlayMode/UIWorldTransitionPlayModeTests.cs
git commit -m "feat(ui): 提供 Hotfix 世界过渡扩展点"
```

### 任务 8：删除旧接口、更新工具文档并完成最小回归

**文件：**

- 删除：`Assets/Scripts/Core/Runtime/UI/Animation/UIAnimationInterfaces.cs`
- 删除：`Assets/Scripts/Core/Runtime/UI/Animation/UIAnimationInterfaces.cs.meta`
- 修改：`docs/architecture/ui-rendering.md`
- 修改：`docs/modules/ui-runtime.md`
- 修改：`docs/runbooks/create-ui-view.md`
- 修改：`docs/runbooks/run-unity-tests.md`
- 修改：`docs/README.md`

- [ ] **步骤 1：扫描旧 API 引用**

```powershell
rg -n "IUIAnimation|ICameraAnimation|UIAnimation|CameraAnimation|ShowAsync\(.*Forget" Assets/Scripts docs
```

预期只剩迁移说明；若生产代码仍有旧接口引用，不得删除文件。

- [ ] **步骤 2：删除旧接口并让 Unity 正式编译**

使用 `apply_patch` 删除 C# 文件；meta 通过 Unity AssetDatabase 删除或项目既有安全资产删除流程处理。不要直接编辑生成的 `.csproj`。

- [ ] **步骤 3：更新三层文档**

架构文档说明 UI/World Transition 边界；模块文档说明状态机、事务、缓存和错误；runbook 给出 `ShowAsync`、ViewMode、Transition 配置、Camera 扩展和精确测试步骤。修正项目级 UnitySkills 技能路径失效问题，不再引用不存在的 `.codex/skills/unity-skills/SKILL.md`。

- [ ] **步骤 4：运行本计划直接影响的测试类**

按以下顺序逐个运行，不并行：

1. `Core.Tests.UI.UINavigationContractsTests`（EditMode）
2. `Core.Tests.UI.UIStackTests`（EditMode）
3. `Core.Tests.UI.MvcBindTransitionGenerationTests`（EditMode）
4. `Core.Tests.UI.UIViewLifecyclePlayModeTests`（PlayMode）
5. `Core.Tests.UI.UIManagerNavigationPlayModeTests`（PlayMode）
6. `Core.Tests.UI.UITransitionPlayModeTests`（PlayMode）
7. `Core.Tests.UI.UIWorldTransitionPlayModeTests`（PlayMode）
8. `Core.Tests.UI.UIRootManagerPlayModeTests`（PlayMode）
9. `Core.Tests.UI.UIViewPrefabConventionTests`（EditMode）

预期所有 job 为 Completed 且 `failedTests=0`。这是受影响类回归，不是项目全量测试。

- [ ] **步骤 5：Unity 手动验证**

从 AppEntrance 进入主菜单，验证：启动无黑帧；主菜单只进入一次；快速重复点击不会重复开页；Modal 遮罩和过渡交互锁互不干扰；退出 Play Mode 后 Console 无新增 Error/Exception。Game View 至少检查 16:9、超宽和窄屏。

- [ ] **步骤 6：提交**

```powershell
git add Assets/Scripts/Core/Runtime/UI Assets/Scripts/Hotfix docs
git commit -m "docs(ui): 完成导航与过渡系统接入说明"
```

## 计划完成后的明确延后项

以下内容不属于本计划，不能在执行时顺手加入：

- 同类型顶级 View 多实例。
- BackTo、深链、命名栈和路由图。
- 真实 Camera 移动、Cinemachine 或 Timeline 实现；必须由首个真实 Demo 需求驱动。
- UI 状态磁盘持久化。
- 全量 Core.Tests、第三方 Package 测试或性能压测。
- 将现有有限列表扩成循环虚拟列表。

这些能力以后各自经过规格讨论再进入独立计划。
