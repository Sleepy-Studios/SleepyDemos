# 公共设置与表现能力

## 职责与入口

`Core.Runtime/Settings` 是本机公共设置的唯一保存者；`Localization`、`Input`、`Rendering`、`TimeControl`、`Presentation` 分别落实语言字体、输入、渲染、玩法时间和电影演出。Core 不引用 Hotfix。`Hotfix/Module/Settings` 提供设置 Flux 状态及 Hub 语言重建协调；专属飞控、赛车和触控布局仍由 Demo 维护。

制作配置位于 `Assets/LoadResources/Config/Settings`；电影演出直接使用方法参数，不再维护预设 SO。设置 SO 只保存制作数据。`GameSettingsService.Current` 和 `GetDefaults()` 返回独立快照，玩家候选写入 `LocalDataManager` 的 `SleepyDemos.Settings`，不修改 SO。当前没有 Luban 双来源。

制作允许范围和帧率选项由 SettingsOptions 配置，服务 Options 返回副本；候选仍通过公共安全边界校验，制作数据只能收紧范围。InputHosts 给控制页面提供宿主身份与源动作，页面先克隆再编辑；语言名称和格式文化来自 LanguageService.Languages。

## Hub 设置界面

五个分类图标使用 `UI/Common/Sprites/Settings/*.svg` 的同一套 SVGImage，样式与缩放约定见[公共美术规范](../architecture/ui-art-direction.md)。画质页的默认继承选项显示具体窗口模式、当前窗口像素、平台画质名称和作者帧率上限，不显示泛泛的“当前配置”；候选仍保留原 `-1`、零尺寸与 `Current`，刷新标签不写偏好。帧率默认必须读 `DisplaySettingsRuntime.AuthoredFrameLimit`，不能用已经被玩家上限覆盖的 `Application.targetFrameRate` 假装作者默认；桌面VSync开启时继续显示原有不可用原因，不把刷新率当实测FPS。

`SettingsFlow.OpenAsync(category, token)` 仅在 Hub 通过 UIManager 按需打开 `HubSettingsView`；资源为 `UI/Settings/HubSettingsView.prefab`。Demo 没有公共设置按钮、快捷入口或专属公共设置弹窗，继续使用自己的玩法设置，并通过公共 API 消费 Hub 中保存的全局偏好。

Main View 直接持有 UITab／ViewTab、候选和提交生命周期。Graphics、Audio、Input、Language、Accessibility 各自是独立 `*SettingsPanelView`，直接使用 UIBind 生成的基础控件引用。主页面只创建 View 对象并交付候选，先登记 ViewTab，再同步绑定五个 UITab 分类；ViewTab 在选中时异步加载对应 Prefab，避免逐帧分类初始化覆盖早点击。不内嵌五页，也不保留 SettingsScreen／SettingsPage 包装层或多宿主根基类。五页共享一份独立候选，显示时回显、隐藏时释放编辑会话；AddSubView 持有销毁所有权。

子页位于 `UI/Settings/Panels/`。固定表单复用 UISettingRow、Dropdown、Slider、Switch、Button；控制页的同类绑定条目使用 LoopScrollView 和 `UI/Settings/SettingsBindingItemView.prefab` 回收复用。引用按实际节点通过正式 UIBind 生成，不维护额外地址路由。公共输入提示和各 Demo 原有基础 UI Variant 继续维护各自外观，卡丁车保留纸艺风格。
## 启动与事务

资源服务就绪后，正式启动的 `GameSettingsInitializeSystem` 在公共 UI 初始化之前加载 `GameSettingsConfig`。Editor Demo 直启调用同一个 `InitializeAsync`；并发等待共用完成源，不能用只适合顺序复用的 UniTask Preserve 代替并发共享。

本机记录不存在、读取/反序列化失败或完整候选校验失败时，统一使用制作默认值，不暴露读取故障分类，也不自动写默认值；`Feedback` 初始化为空，只记录实际应用反馈。仅维护当前 `SleepyDemos.Settings` 契约，不迁移旧独立 DLSS 键。保存失败继续抛给应用流程，页面提示一次；成功保存后才提交当前快照。

设置页持有完整候选，应用顺序为显示/画质、音量、后处理、保存、语言选择、发布公共参数通知。关闭或取消页面丢弃候选；恢复本页默认只改该页候选。SettingsFlow 通过 SettingsApplyAction 等待实际提交结果，忙碌或改键监听期间不接受第二次应用。显示模式与分辨率改变打开独立 `DisplayConfirmView`，使用未缩放的 15 秒确认期限；超时、取消、拒绝或确认失败恢复之前实际显示及旧偏好。确认任务由外部 Flow 持有，退出时归还确认 View。输入会话在提交通知后应用验证过的参数与 override。通知不用于文本刷新。

显示服务克隆管线、RendererData 和制作 Feature，释放 DLSS 后再换管线，最终重绑相机。共享资产不写回。公共 Overlay 相机栈不能使用原生 TAA；能力查询返回禁用原因，强制请求时反馈 SMAA 实际状态。请求 DLSS 档位与 SDK 实际档位分别读取，不因设备不支持丢掉请求。

手动原生抗锯齿包含 FXAA 和 MSAA（2/4/8样本，默认4），与显式关闭、SMAA、TAA分别选择；相机默认保留平台制作行为。DLSS/DLAA实际接管时保留玩家的手动候选，停止原生抗锯齿，关闭或降级后恢复手动配置。管线中保留的原生回退采样数不等于DLSS输出正在使用MSAA。接口分别反馈 EffectiveAntialiasing、EffectiveMsaaSamples 和 IsAntialiasingManagedByUpscaler；MSAA还需Renderer和硬件支持，不把有Overlay误判为不支持。

FXAA/SMAA/TAA 必须启用实际后处理路径；仅为抗锯齿开启时，不额外激活作者原先关闭的画面效果。切镜、解绑和退出服务恢复原相机抗锯齿、MSAA允许状态与后处理状态；先清理会刷新相机的临时效果，再恢复作者状态。

## 语言与字体

首版语言 `zh-CN` / `en`，默认中文。文案表字段固定 `Key / FontId / Chinese / English`；模块使用键前缀分组。FontId 映射支持一个语言多种字体及配套材质，多个编号可复用同一字体。默认编号为零，覆盖参数的 null 和零含义不同。

`LanguageService.Get/Format/SetText` 在页面创建、数据刷新和条目绑定时一次性执行。SetText 只设文案、字体和材质。字重、字号、颜色及对齐由 TMP/Prefab 维护，不给每个文本添加监听组件。

公共场景 Loading 由导航源交付稳定文案键与名称参数，View 更新进度时一次性赋值文字与 FontId；语言尚未准备时保留原文行为，启动壳的 StartupLoadingView 继续独立。Hub 常规反馈使用 FeedbackKey，任意诊断文本使用 Feedback；两者互斥，页面重建清理派生状态。

语言入口仅 Hub 开放。`HubLanguageFlow` 在场景导航空闲时校验资源，关闭并销毁全部 Hub 页面（含缓存），移除语言派生的 MainMenuData，应用保存语言，再显示新的 Hub 和语言设置页。协调任务不属于旧设置页。失败尝试恢复旧偏好并重建，反馈实际恢复结果；不会虚报成功。Demo 内固定当前语言，返回 Hub 后才能再次切换。

## 输入、音频与时间

现有 InputActionSession / GameplayInputRouter 复用公共参数和有效绑定。InputSettingsCatalog.Entry.Actions 明确保存实际动作 GUID 与文案键，设置页按宿主／设备生成稳定绑定条目；每行用 BindBinding 显示精确索引，别名、复合子项和不同 Map 的同名动作保持独立身份。InputRebindSession 只改未启用候选副本，按稳定源动作 ID 和绑定 ID 导出；CanRebind 保留确认、返回和 UI 导航安全入口。公共 InputPrompt／InputPromptButton 与各 Demo Variant 提供图标及动作说明，InputPromptGroup 保存明确制作引用。最近实际使用的设备决定提示类别与物理用途，漂移和连接本身不提交动作。输入页的“手柄图标”下拉选择 Xbox／PS5／NS，当前绑定列表局部预览候选，成功应用后由 GameSettingsService.Changed 刷新其他提示；默认 Xbox，不按硬件品牌自动切换。旧记录缺字段或0仅补 Xbox，不丢弃其余设置。完整接入见[公共输入](gameplay-input.md)。

普通音源使用 AudioBusSource；音乐、音效和 UI 分类增益与音源自己的音量相乘。KagitKart 的 DSP 在分类混音处乘公共增益，不调整 DSP 时钟，不用 Stop 实现静音。辅助偏好约束 Demo 表现，专属开关继续可进一步限制。

GameTimeService 是 timeScale 的唯一公共演出入口，手动玩法使用 DeltaTime；Unity 物理保留固定步长，不再重复乘倍率。最近倍率作用域生效，任何冻结租约优先。Scope 由调用方拥有，页面/场景退出释放。UI、采样、门闩、长按、渐变和演出使用未缩放时间；LocalPauseState 继续保存失焦/断连及明确确认继续规则。

## 后处理与电影演出

PostProcessRuntime 使用原生 URP Volume，按参数覆盖场景效果；预设和临时 Scope 克隆 Profile，最近有效作用域只覆盖勾选参数。玩家效果允许开关、亮度及减少动态最后约束输出，公共 UI 不参与世界后处理。相机解绑恢复并清理临时作用域，赛车纸印 Feature 保留自身生命周期。

CinematicPresenter 在 Core 维护三个独立表现通道：遮幕、黑边与标题，动画直接使用 DOTween。正式 `CinematicOverlayView` 由 UIManager 托管并持有 Presenter，默认全屏，也可用 SetData 传入目标画面容器；隐藏和销毁立即 ResetEffects。调用方持有具体 View，不使用静态注册、预设 SO、队列或嵌套句柄，也不创建替代 Canvas。

公开演出接口为普通 void 方法与完成回调。StartTransition 在闭幕完成时调用同步 onCovered，随后按 closedHold 保持并开幕；耗时场景加载或导航应使用 CloseCurtain 的完成回调启动业务，业务完成后再 OpenCurtain。视觉回调不转换成 UniTask，不把画面完成误当业务事务完成。转场忙碌时拒绝重复请求；黑边和标题各自维护当前动画，ResetEffects 停止动画且不触发旧完成回调。字幕在显示时解析语言和 FontId。

黑边通过 ShowLetterbox／ShowLetterboxForAspect／HideLetterbox 控制，不管理 HUD。HUD 淡出由明确持有其 CanvasGroup 的页面或 Panel 负责，停止或退出恢复原 alpha、interactable 和 blocksRaycasts。各通道使用未缩放时间，减少动态时动画时长为零，时间冻结仍能执行。

Hub 的公共效果入口调用 `PresentationLabFlow.OpenAsync`，加载正式 PresentationWorld 世界租约、PresentationLabView 与 CinematicOverlayView；体验页分别调用公共后处理、时间和电影 API。同一时间只播放一个操作，重新播放前清理本页上次体验，不排队或叠加；保留七种遮幕、方向、开闭时长、宽高比、HUD 和标题选项。PresentationCinematicPanel 自管 HUD 淡出与恢复，演出完成保留黑边，正常停止通过 HideLetterbox 完成回调恢复；播放中停止、异常或关闭立即复位。页面操作版本阻止旧回调改写新操作。退出仍通过 UIManager.ReplaceAsync 返回 Hub，成功后销毁体验页并归还世界资源；失败且体验页仍可见时恢复表现根。停止和退出只释放本次时间和后处理作用域，不解除其它调用方的冻结。该体验页不会把暂时的速度、黑边或后处理预览写成玩家设置。正式 UI 位于 `UI/Presentation/`，世界资源位于 `Art/Presentation/PresentationWorld.prefab`。

## 交付状态与验证边界

按 2026-10-12 最终授权范围，G0–G11 的电脑端交付已完成：公共设置仅在 Hub 打开，五个独立分页与效果体验复用现有基础 UI；Demo 保留自己的玩法设置、公共 API 接入和美术 Variant。七张效果图已确认并装配，Hub 风格不覆盖 Demo。详细 API 与生命周期以上述正文为准，不再维护并行进度清单。

本节区分本次定向验证与历史交付记录，不由历史结果推断最新代码已生效：

- 2026-10-14 电影演出重构已通过 Unity Editor 编译；`CinematicGeometryTests` 2/2、`CinematicRuntimeTests` 4/4、`TestAssemblyBoundaryTests` 5/5。七种图案及四向擦幕取得 Direct3D12 像素验证，标题保留中英 FontId 与冻结时间验证，停止、忙碌拒绝、完成重入和黑边替换均通过。
- 正式 AppEntrance 的效果体验入口 1/1，覆盖 1920×1080、1920×1200、3440×1440 三画幅的正常播放后收回黑边、冻结中停止、HUD 原透明度及交互恢复、退出归还世界并返回 Hub；已查看三画幅运行截图。UnitySkills 回调恢复曾发生文件占用，最终通过结果以原生 Test Runner XML 为证据。没有执行全量回归或 Player 构建。

以下为重构前的历史记录：

- Hub、五分类与体验三分类，中英两语言、1920×1080 / 2560×1440 / 3440×1440 共 54 张运行截图；实际切页、取消、Esc 返回与三画幅关闭按钮均有定向通过记录。最低支持尺寸为 1920×1080，旧窄屏试验不作为验收要求。
- 正式 AppEntrance / Editor Offline 实际首包 `2026-10-12-165911` 的五分页、候选保存、语言重建、显示确认、冻结及四 Demo 往返为 1/1（`70d5d059`）；128 项包文件与 StreamingAssets 哈希一致，原配置已恢复。Editor 复用已加载 Hotfix 程序集，不能据此证明首包 DLL 独立加载。
- 七类后处理在 Direct3D12 正式场景取得独立图像变化、UI 隔离、释放恢复和制作资产保护证据。四 Demo 的 0.25× / 1× / 2×实际运动或计时及释放恢复有定向通过记录；Kart 在低帧率受作者每帧最多 8 个固定步骤限制，不能保证任意倍率完整兑现。
- 历史 Windows Player 构建失败与后来获授权修复 AutoRun 的记录均保留在归档；系统修复未重新执行 Player 构建，不把旧失败写成仍在阻塞当前 Editor 交付，也不把修复当作 Player 已通过。
- 没有执行全量回归。独立 Player / IL2CPP、裁剪、其它图形 API 与真实手柄 / 触屏均按授权后置；定向流程和模拟设备不代表所有玩法或硬件已验。

阶段报告、job ID、机器修复顺序与早期失败见 [历史实施记录](../agent/archive/settings-goal-progress.md)。后续新增结果只更新对应实际范围，不由历史通过推断最新代码已生效。

接入步骤见 [公共设置与演出接入](../runbooks/use-settings-presentation.md)。
