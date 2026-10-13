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

需要完整卸载并重新建立当前 Demo 时等待 `ReloadCurrentAsync()`；玩法内重置车辆、赛局或关卡继续走 Demo 自身逻辑。切换及重载共用 Loading Presenter，打开后跨过完整下一帧获得呈现机会再执行场景操作，不在调用方添加延时、另开 Loading 或模拟百分比。未完成的 Switch / Reload 期间均返回 `Busy`。

Loading 的目标标题与背景由 Presenter 在显示前配置；新增场景时同步 `BackgroundAddress` 的资源映射，复用已确认的本 Demo 图片。背景加载失败使用中性底色；换图后同步 `AspectRatioFitter` 宽高比，信息层保留 Canvas 边距。启动期背景直接由 `StartupLoading` Prefab 引用，不经运行期资源服务。

全屏 Loading 期间，高于 Base 的现有层临时停止绘制和射线，高层已加载 View 暂时停止交互，成功/失败均恢复原值。不要为了盖住 Loading 提前销毁来源 Modal/Widget；场景成功卸载时由 owner 按旧 View 实例清理。独立 World 的 OnDestroy 同样覆盖公共 `ReloadCurrentAsync`，不能只在返回按钮路径关闭界面。

Demo 来源的 Page 使用可等待 `OpenAsync` 且 `hidePrevious:false` 覆盖，失败关闭 Loading 后保留同一页面及绑定；Hub 进入仍替换原主菜单。来源业务命令须拒绝正在进行的导航，返回请求的 `Busy`/`Ignored` 也须解除自身退出锁，不能当作成功销毁界面。不要在等待关闭动画后才检查导航忙碌，否则并发导航可能已开始。

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
- Loading 打开或收口失败：检查结果中的原 UI 错误；恢复 UI 也失败时，附加诊断保留在同一 `Error` 中。场景已经提交的失败按 `CurrentScene` 收口，不应由调用方强行恢复旧场景。
- UI 在新场景不可见：检查目标 Camera 是否为 URP Base Camera，并确认切换经过全局 Navigator。
- 返回 Hub 后重复初始化：说明仍有代码在用 `LoadSceneMode.Single` 重载 `AppEntrance`，应删除直载入口。
- Player 找不到 Demo：检查 YooAsset `Demos` Collector，不要用“加入 Build Settings”绕过采集问题。

## 验证方式

- 从 `AppEntrance` 冷启动进入主菜单，再往返目标 Demo。
- 正式入口的视觉证据从实际 Game View 帧末即时取图，记录 PNG 尺寸、活动场景、相机、可见 View 与 World 结构；等待设置分类条目、当前分页及图片/Shader 就绪，再跨真实渲染帧采样。设置分类当前采用同步 `UITab.Init`，不能把稳定后仍空白的侧栏归因于条目异步创建。
- 按本次要求核对 1920×1080、1280×720、2560×1080、2340×1080 的 Hub/设置页；改变临时 Game View 画幅后须恢复原选择，不修改生产显示设置或保存偏好。图03设置行已装配，使用当前正式页面截图核对；此前PRE-03记录只用于追溯，不能用基础板预览替代页面验收。
- 检查 Active Scene、相机输出、UI、输入和 AudioListener。
- 分别核对快速成功、加载失败、UI 收口失败、连续点击和当前场景重载：Loading 获得渲染机会，进度由实际操作回调推进，收口后不残留；失败后可再次操作。Editor 直启返回 Hub 需同时核对正式启动交接。
- 运行场景导航 EditMode 测试与 `GameSceneRuntimePlayModeTests`。
- 修改 Demo 来源生命周期时运行 `Tests.Demo.WallSqueezeRuntimeTests.DemoNavigationFailureAndBusyPreserveSourceViewsAndCommands`：正式启动与真实来源 View，资源事务故障注入，覆盖 Wall/Kart 公共重载、跨 Demo、返回失败及忙碌拒绝，验证原页面实例和业务命令恢复。成功卸载清理由同类 `PublicSceneReloadAndSwitchReleaseOwnedWidgetsAndModals` 验证。
- 修改 Hub 进入流程时运行 `MainMenuNavigationPlayModeTests.FailedEntryRestoresNewMenuAndAllowsRetry`：真实指针进入、重复点击、失败恢复到新页面及再次点击。成功往返使用目标 Demo 的入口回归验证，不调用按钮监听器代替操作。
- 修改Hub焦点与菜单输入时，运行同类的`KeyboardAndGamepadNavigateEnterAndRecoverWithoutRepeatingHeldSubmit`，覆盖方向键、Enter、手柄方向键/A、失败恢复和按住确认键时不误提交；实际硬件与选中态可读性仍须另验。
- 修改虚拟列表绑定或菜单导航时，运行同类的 `GamepadScrollsRecycledCardsAndEntersCurrentIdentity`，验证真实手柄输入越过回收边界后，卡片显示和实际进入目标仍对应当前数据；不以直接调用按钮监听器替代输入验收。

