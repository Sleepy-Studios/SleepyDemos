# 在 Unity Editor 直接运行 Demo 岛

## 适用范围

此能力只用于 Unity Editor 中直接打开 Demo 场景后 Play。Development Build 和 Release 必须从 `Assets/Scenes/AppEntrance.unity` 启动。

## Demo 场景要求

1. 场景仍位于 `Assets/LoadResources/Demos/<demo_id>/Scenes/`，由 YooAsset Collector 收集，不加入 Build Settings。
2. 入口统一登记在 `GameSceneCatalog`，业务不直接调用 `SceneManager`。
3. 场景必须且只能包含一个 MainCamera 和一个 AudioListener。
4. 新增独立根对象 `DemoIslandEditorBootstrap` 并挂同名组件；不要把它挂到玩法协调器、UI Controller 或会随场景卸载的组合根上。
5. 玩法协调器在 `DemoIslandEditorBootstrap.EnsureReadyAsync()` 完成前不得显示 View、生成玩法对象或启用输入。

## 最小启动内容

当正式 `GameSceneNavigator` 已存在时，Bootstrap 立即退出，不重复初始化。

Editor 直启时只复用以下正式能力：

- `HotfixConfig` 与 `ResourceServices`。
- YooAsset 初始化。
- `UIManager.InitializeAsync()` 与 `UITypeReflection`。
- 幂等的 `HotfixBootService`。
- `HotfixWorldTransitionProvider`。
- `EditorDirectGameSceneRuntime` 与 `GameSceneNavigator`。

直启不执行 HybridCLR 元数据补充、热更新程序集加载、完整 `HotfixEntry`，也不显示 MainMenuView。

## 重载与返回

- 当前 Demo 的重载统一调用 `GameSceneNavigator.ReloadCurrentAsync()`。Editor 直启运行时仍由资源 Loader Additive 加载新场景并配对卸载旧场景。
- Bootstrap 必须是独立持久根；旧玩法协调器随旧场景销毁，新场景创建新的协调器，避免重复 UI 或重复输入。
- Backspace 调用导航器返回 Hub。Editor 直启实现会清理直启实例并以 Single 模式进入 AppEntrance，随后由正式 Core/Hotfix 启动链接管。

## 验证清单

1. 确认 Build Settings 仍只有 AppEntrance。
2. Editor 直接打开目标 Demo，Play 后只创建一套 UIRoot、UICamera 和 EventSystem。
3. 场景业务在正式 UI 出现前不响应输入。
4. 重载三次，确认旧会话 UI 先关闭；每次只出现一个机型选择、一个玩法协调器、一个 MainCamera、一个 AudioListener，Console 不把正常 `Canceled` 记为 Error。
5. Backspace 进入 AppEntrance 后不重复注册 Hotfix Boot 系统，主菜单可正常操作。
6. 再从 Hub 进入同一 Demo，正式 Additive 往返行为保持不变。

## 常见问题

- 直启没有 UI：检查 Bootstrap 是否独立存在、HotfixConfig 路径和 YooAsset PlayMode 日志。
- 重载后出现两个 HUD：检查是否把 Bootstrap 与场景协调器放在同一 DontDestroyOnLoad 根对象。
- 两个 AudioListener：检查 Demo 中是否预放额外无人机 Camera，或玩法 Camera 是否在选择机型前已启用。
- 选择机型后立即起飞/翻转：确认 Variant 根为单位 Transform，生成使用失活临时父节点，并按起落架 `Foot` 最低点计算出生高度；不要把贴地 `SpawnPoint.y` 直接当根节点高度。
- 正式入口重复初始化：检查 Bootstrap 的“Navigator 已存在即退出”和 `HotfixBootService` 幂等门禁。

场景运行时设计见[运行期场景导航](../modules/scene-runtime.md)，新增 Demo 的完整资源接入见[新增 Demo](./add-demo.md)。
