# 接入运行期场景导航

## 适用场景

用于从 Hub 进入可加载 Demo，或从 Demo 返回 Hub。启动期仍由 `CoreEntrance` 和 `StartupLoading` 负责，不使用本流程。

## 前置条件

- Demo 场景位于 `Assets/LoadResources/Demos/<demo_id>/Scenes/` 并被 YooAsset Collector 收集。
- 场景包含且只包含一个 `MainCamera` 和一个 AudioListener。
- 场景不加入 Build Settings；Build Settings 只保留 `Assets/Scenes/AppEntrance.unity`。

## 操作步骤

1. 在 `GameSceneId` 增加业务枚举值。
2. 在 `GameSceneCatalog` 登记显示名和 LoadResources 场景地址。
3. 入口调用并等待 `GameSceneNavigator.Instance.SwitchAsync(target)`。
4. 按 `GameSceneSwitchStatus` 处理结果：`Succeeded` 已完成，`Ignored` 已在目标，`Busy` 稍后重试，`Failed` 展示或记录 `Error`。
5. Demo 返回入口统一切换到 `GameSceneId.Hub`。

Hub 的新增按钮应复用 `MainMenuView.EnterDemoAsync(target)`，并加入该页的入口禁用/恢复列表，不再复制单独的 `OpenXxxAsync`。失败时页面标题显示可重试提示，详细错误写入 Console；未接通入口保持不可用。加载过程会销毁旧 Hub，因此异步结束后只能更新当前恢复出来的页面。

## 示例

```csharp
var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.DroneFlight);
if (result.Status == GameSceneSwitchStatus.Failed)
{
    Debug.LogError(result.Error);
}
```

## 常见问题

- Loading 出现但场景切换失败：检查目标场景的 MainCamera Tag 与 AudioListener 数量。
- UI 在新场景不可见：检查目标 Camera 是否为 URP Base Camera，并确认切换经过全局 Navigator。
- 返回 Hub 后重复初始化：说明仍有代码在用 `LoadSceneMode.Single` 重载 `AppEntrance`，应删除直载入口。
- Player 找不到 Demo：检查 YooAsset `Demos` Collector，不要用“加入 Build Settings”绕过采集问题。

## 验证方式

- 从 `AppEntrance` 冷启动进入主菜单，再往返目标 Demo。
- 检查 Active Scene、相机输出、UI、输入和 AudioListener。
- 运行场景导航 EditMode 测试与 `GameSceneRuntimePlayModeTests`。
- 修改 Hub 进入流程时运行 `MainMenuNavigationPlayModeTests.FailedEntryRestoresNewMenuAndAllowsRetry`：真实指针进入、重复点击、失败恢复到新页面及再次点击。成功往返使用目标 Demo 的入口回归验证，不调用按钮监听器代替操作。

