# 运行期场景导航

## 负责什么

运行期场景导航负责在 Hotfix 接管后，以 `AppEntrance` 为常驻 Hub 壳加载、激活和卸载 Demo 场景，并在整个事务期间协调通用 Loading、UI Camera、基础相机和 AudioListener。

它不负责启动期资源初始化，也不会重新加载 `AppEntrance` 或重新运行 `CoreEntrance`。

Unity Editor 直接运行 Demo 场景是受限适配路径：`DemoIslandEditorBootstrap` 先补齐资源、UI 和 Hotfix Boot，再用 `EditorDirectGameSceneRuntime` 把当前场景登记为业务场景。该路径不进入 Development/Release。

## 代码位置

- Core 资源协议：`Assets/Scripts/Core/Runtime/Resource/IResourceSceneLoader.cs`
- YooAsset 适配：`Assets/Scripts/Core/Runtime/Resource/YooAssetResourceSceneLoader.cs`
- Hotfix 业务导航：`Assets/Scripts/Hotfix/Module/Common/SceneManagement/`
- 运行期 Loading：`Assets/Scripts/Hotfix/Module/Common/CommonLoadingView/`

## 主链路

进入 Demo：

1. `GameSceneNavigator.SwitchAsync(GameSceneId)` 拒绝重复或并发请求。
2. Presenter 先预加载 `CommonLoadingView`，配置实际目标标题和场景背景。Hub 来源以无动画 Page 替换主菜单；Demo 来源以不隐藏来源的 Page 覆盖原界面，保留原实例及业务绑定。跨过完整下一帧的 `LastPostLateUpdate` 后才启动场景操作，让 Canvas 和 Camera 获得呈现机会。
3. 按场景定义的 `LanguageCatalogAddress` 加载并注册私有词表，再由 `IResourceSceneLoader` 通过 YooAsset Additive 加载目标场景并报告真实进度；注册早于目标 Awake/OnEnable。
4. 校验目标场景唯一 `MainCamera` 与 `AudioListener`。
5. `UIRootManager.BindToBaseCamera` 把持久化 UI Camera 移入新 URP Camera Stack。
6. 激活目标场景、停用来源相机并提交场景句柄后，扫描目标 Active Scene 中的 `IGameScenePreparation` 并逐个等待真实准备；目标业务页面可在 Loading 下完成首帧，全部成功后才关闭 Loading。

返回 Hub：

1. 显示 `CommonLoadingView`。
2. 恢复 Hub 相机、AudioListener、UI Camera Stack 和 Active Scene。
3. 使用原加载句柄卸载 Demo，释放私有词表作用域及对应资源句柄。
4. 用 `MainMenuView` 替换 Loading。

重新运行当前 Demo：

1. 通过公共导航发起重载；Loading 覆盖来源 Page，旧协调器在成功卸载时按捕获的具体 View 实例清理自己的界面，不要求调用方提前销毁。
2. `GameSceneNavigator.ReloadCurrentAsync()` 拒绝 Hub 和并发请求。
3. 显示 Loading，先通过原资源句柄完整卸载当前 Demo 并临时恢复常驻 Hub 表现层。
4. 按同一资源地址创建新的场景句柄，重新绑定 Camera、AudioListener 与 UI Camera Stack。
5. 新协调器的 `Start` 与导航共享同一个准备任务；导航等待准备和首帧完成后才结束 Loading，不复用旧场景玩法状态。准备任务不能先等待 `WaitUntilStableAsync`，否则双方互等。

Editor 直启重载：

1. 独立的 `DemoIslandEditorBootstrap` 根对象保持常驻，玩法协调器仍属于 Demo 场景。
2. `ReloadCurrentAsync()` 通过资源 Loader Additive 加载新实例，切换 Camera/UI Camera Stack 后卸载旧实例。
3. 新场景自带的 Bootstrap 因 Navigator 已存在而退出；新玩法协调器正常启动，避免双 HUD 或双输入。
4. Backspace 清理直启导航和宿主，进入 AppEntrance 后由正式启动链接管。

## 场景与场内准备

`IGameScenePreparation.PrepareSceneAsync(Action<float,string> report, CancellationToken cancellationToken)` 负责本场资源、配置、音频与首屏的真实就绪。场景协调器若由 `Start` 同时发起，必须共享同一个初始化任务，不重复预热。导航将报告映射到场景加载之后的准备区间，不等待任意计时延迟。准备期间打开业务页面使用 `hidePrevious:false`，只按捕获实例关闭本场页面，保留公共 Loading。

`RunPreparationAsync(title, prepare, token)` 用于当前场景内的开赛等准备，使用独立 Presenter 遮挡页面；完成、失败或取消均在 finally 恢复当前页面并释放导航忙碌状态。它不包裹会再次导航的方法。Loading 保持 Base Page，显示时临时挂到既有 Tip 层并提升该层排序遮挡下方业务页，根节点不添加 Canvas，事务结束恢复界面。

## 生命周期与失败恢复

- Build Settings 只包含 `AppEntrance`；Demo 场景由 YooAsset Collector 收集。
- 每个成功加载的场景句柄必须由创建它的 `IResourceSceneLoader` 配对卸载。
- 加载、相机校验或旧场景卸载失败时，恢复来源 Active Scene、相机、AudioListener 和 UI。
- 资源 Loader 抛出初始化或加载异常时，同样恢复来源 AudioListener，不能只关闭 Loading 而留下静音场景。
- 目标场景提交后的准备失败与加载失败分开处理：来源已经卸载，必须通过原运行时返回 Hub 并卸载目标；只有返回成功才将 `CurrentScene` 改为 Hub 并恢复主菜单。返回也失败时保留实际目标状态、关闭本事务 Loading，并在原准备错误后追加返回错误，允许再次返回或重载；不假称已恢复来源。
- UI 打开、完成或恢复失败均返回 `Failed`；恢复失败的诊断追加到原错误，不掩盖首次失败。单纯 UI 收尾失败仍按已提交的 `CurrentScene` 收口，不因页面失败卸载已经准备好的目标；最终释放忙碌状态，允许再次导航。
- Editor 直启重载后的准备失败同样请求返回 AppEntrance；Single 交接已释放旧导航时，由正式启动链创建 Hub 页面；旧 Presenter 仍清理自己的 Loading 并恢复持久化 UI 层遮挡，不抢建主菜单。原准备异常仍记录并返回 `Failed`，不把返回启动入口当作进入目标成功。
- 旧场景在 `OnDestroy` 中只能按捕获的具体 View 实例清理自己的 UI，不能按类型误关新场景实例。场景导航返回 `Succeeded`、`Ignored`、`Busy` 或 `Failed`，不返回 UI 操作的 `Canceled`；只有 `Failed` 记录 Error。Wall/Kart 返回请求在忙碌时保留来源状态，所有非成功结果都解除退出锁并恢复输入，不能提前销毁来源菜单。
- 场景切换不支持提交后的任意取消；Unity/YooAsset 已开始的场景操作必须收口到成功或明确回滚。
- Loading 进度单调递增；没有真实字节信息时不显示虚假大小。
- Loading 标题显示目标场景的本地化名称。背景由 Presenter 按现有场景目录选择，复用 View 的 Loader；装饰背景不可用时显示中性底色，不中断导航。全屏背景使用 `AspectRatioFitter.EnvelopeParent` 等比覆盖，更新 Sprite 时同步宽高比；品牌、阶段、说明和进度保持在独立信息层，不参与背景裁切。
- Loading 保持 Base Page。Presenter 暂时关闭现有 Foreground、Pop、Decorate 层 Canvas 与射线，借用 Tip 层显示 Loading 并在结束后恢复其排序，并暂时关闭已加载高层 View 根 CanvasGroup 的交互，保留原值、alpha、激活状态与实例；收口的 finally 恢复原值，失败不销毁来源 Modal/Widget，加载期间目标控件仍可初始化。旧场景的 Widget/Modal 由其 owner 销毁时按捕获实例关闭，不能只依赖普通退出按钮，否则公共重载会留下高层 UI。
- Loading 的呈现等待使用 `UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate)`，不依赖 Game View 聚焦才能推进的 `WaitForEndOfFrame`；百分比来自资源加载/卸载回调及实际完成阶段，不添加固定延时或计时假进度。快速成功与快速失败也先获得呈现机会。
- Hub 的已接通 Demo 通过共用卡片模板和 `MainMenuView.EnterDemoAsync` 进入。进入期间禁用全部开放入口，未接通卡片标注“未开放”；正常返回后随导航事务结束恢复入口，具体展示数据与模板见 [Hotfix 主入口](hotfix-main.md#主菜单卡片)。
- MainMenuView 使用一个公共 `LoopScrollMenuNavigation` 提供列表有效焦点，一个 `MenuInputScope` 等待确认/方向键释放并管理页面输入生命周期；每帧更新当前 `FirstSelection`，不持有已回收按钮作为永久默认焦点。指针和 Submit 仍由现有 Core UI Module 派发，不增加业务输入适配器或 EventSystem。
- Loading 替换会销毁原 MainMenuView。失败回滚后的提示及按钮恢复必须查找当前可见的 Hub 实例，并确认导航仍为同一实例、已稳定停留 Hub，不能继续操作原页面或把已进入 Demo 的场景强行切回 Hub。

## 边界规则

- Core 只接收资源地址和 Unity 场景语义，不认识 `DroneFlight` 等业务枚举。
- `GameSceneId` 与地址目录只放 Hotfix；新增 Demo 必须登记到 `GameSceneCatalog`。
- `GameSceneId.Dlss` 加载 `Assets/LoadResources/Diagnostics/DlssLab/Scenes/Main.unity`，由 `Tools/Rendering/DLSS Lab` 复用正式启动与导航进入，不作为 Hub 游戏卡片。场景提供标准主相机；公共主相机绑定入口负责结束旧 DLSS 会话、恢复 UI 栈并为目标相机应用全局设置，失败回滚也走同一入口，详见 [PC DLSS 实验室](dlss-lab.md)。
- Hotfix 不直接持有 YooAsset `SceneHandle`，也不直接调用 `SceneManager.LoadSceneAsync` 绕过全局导航。
- `GameSceneId.KagitKart` 加载 `Assets/LoadResources/Demos/kagit_kart/Scenes/Main.unity`，从 Hub 的“纸艺卡丁车”进入，Editor 直启复用同一 Bootstrap；当前驾驶原型和剩余移植边界见[纸艺卡丁车](../demos/kagit_kart/README.md)。
- Demo 场景必须且只能提供一个带 `MainCamera` Tag 的 Camera 和一个 AudioListener。
- `StartupLoading` 与 `CommonLoadingView` 生命周期不同，不共享脚本或 Presenter。
- Editor 直启不运行 HybridCLR、Hotfix 程序集装配或完整 `HotfixEntry`；`HotfixBootService` 必须幂等。
- Editor 直启返回 `AppEntrance` 时，先确认 Unity 创建了场景加载操作，再释放直启 Navigator 与宿主；请求创建失败时保留来源供重试。操作开始后的 Single 场景交接仍由正式启动链负责。
- Editor 直启由 `GameSceneCatalog` 按当前保存的 `Scene.path` 解析实际 Demo 标识，未登记路径明确报错；导航从该 Demo 状态开始，适用于无人机和夹爆它等已登记场景。

## Loading 资源配置

启动 v3 与运行期 v2 效果图已分别批准并装配，审批记录见[公共 UI 美术确认](../agent/art/ui-foundation/README.md)。`StartupLoading.prefab` 直接引用瑞士抽象背景与公共字体，保持原独立 Overlay；`CommonLoading.prefab` 保持 Base Page，顶部与底部使用 ImageExtend 渐变 scrim，保留原七项绑定与进度组件。

Presenter 的背景地址按目标场景选择，下表相对于 `Assets/LoadResources/`；非无人机目标复用已有图片，不生成未经确认的新美术。

| 目标 | 当前背景 |
|---|---|
| Hub | `UI/Common/Loading/Art/StartupBackdrop.png` |
| DroneFlight | `Demos/drone_flight/Art/UI/LoadingBackdrop.png` |
| Dlss | `Diagnostics/DlssLab/Art/UI/Preview.png` |
| WallSqueeze | `UI/Common/Loading/Art/StartupBackdrop.png`，复用已批准的抽象背景 |
| KagitKart | `Demos/kagit_kart/Art/UI/HubPreview.png`，1280×720 无文字纸赛道 |

背景变化时同步 EnvelopeParent 的宽高比；标题来自目标 DisplayNameKey，说明与大小为空时隐藏。原生装配与 Canvas 预览不代替正式入口往返、失败恢复或画幅验收，应逐背景核对顶部文字对比度与底部进度可读性。

不要按文件名选择全屏图：夹爆它 HubPreview 含 HUD 与业务读数，不作 Loading 主视觉；纸艺 Tracks/Desk 是透明缩略图，也不作全屏背景。

## 验证重点

- Hub → Demo → Hub 往返后，Active Scene、基础相机与 UI Camera Stack 正确。
- 任意稳定时刻只有一个启用的 AudioListener。
- 返回 Hub 不重新运行启动状态机，Demo 场景及资源句柄完成卸载。
- 提交前失败恢复后来源场景仍可操作，Loading 不残留；提交后准备失败须验证目标已经卸载、Hub Active Scene/相机/AudioListener/UI 恢复，并核对返回失败诊断及后续重试。
- 快速切换时 Loading 跨帧获得呈现机会；重复 Switch 和 Reload 请求返回 `Busy`，失败收口后可以重试。

## 相关文档

- [资源系统设计原则](../architecture/resource-system.md)
- [Core UI 渲染设计原则](../architecture/ui-rendering.md)
- [新增 Demo](../runbooks/add-demo.md)
- [接入运行期场景导航](../runbooks/use-scene-navigation.md)
- [在 Unity Editor 直接运行 Demo 岛](../runbooks/run-demo-island-directly.md)

## 独立包启动模式

`GameSceneNavigator.Initialize` 在首次初始化时接收独立包的唯一 Demo 入口，`StandaloneScene` 记录此约束；后续初始化不修改已有实例。`HotfixEntry` 根据 `HotfixConfig.GameId` 区分 Hub 与独立发布：只有非 `hub` 游戏才把解析后的 `StartupScene` 作为独立目标。Editor SessionState 指定的 DLSS 等初始导航在 Hub 配置下只决定首次进入哪里，不启用独立包策略，仍可正常回 Hub。

独立包内各 Demo 原有的 `SwitchAsync(Hub)` 由 Navigator 统一转为 `ReloadCurrentAsync`，完整卸载并重建本游戏初始菜单；请求其他 Demo 直接返回 `Failed`，不打开 Loading 或加载未发布场景。普通 Hub 及 Editor 直启仍保持正常返回 Hub。若场景准备失败确实回到常驻 Hub，`MainMenuView` 在绑定列表前只保留该独立包自己的入口并重置选择，允许重试，不显示其他 Demo 或未开放卡片。独立模式不绕过资源初始化、场景句柄或公共 UI 清理。
