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

Hub 的新增入口在 `MainMenuView` 集合中增加一项 `MainMenuDemoEntry`，配置稳定 Key、预览地址、标题、描述和已登记的目标场景。预览放在 `Assets/LoadResources/UI/Hall/Art/`，由卡片的 `UIImageLoader` 加载；共用 `MainMenuDemoItemView` 模板和统一 `EnterDemoAsync(target)`，不新增固定按钮字段或 `OpenXxxAsync`。未接通入口的目标场景设为 null，保持不可用。失败时独立 Status 控件显示可重试提示，详细错误写入 Console；加载会销毁旧 Hub，异步结束后只能更新当前恢复出来的页面。页面与卡片维护入口见 [Hotfix 主入口](../modules/hotfix-main.md#主菜单卡片)。

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
- 修改Hub焦点与菜单输入时，运行同类的`KeyboardAndGamepadNavigateEnterAndRecoverWithoutRepeatingHeldSubmit`，覆盖方向键、Enter、手柄方向键/A、失败恢复和按住确认键时不误提交；实际硬件与选中态可读性仍须另验。
- 修改虚拟列表绑定或菜单导航时，运行同类的 `GamepadScrollsRecycledCardsAndEntersCurrentIdentity`，验证真实手柄输入越过回收边界后，卡片显示和实际进入目标仍对应当前数据；不以直接调用按钮监听器替代输入验收。

