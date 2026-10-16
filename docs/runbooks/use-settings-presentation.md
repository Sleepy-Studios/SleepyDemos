# 公共设置与演出接入

场景加载文案从 GameSceneNavigator 交付明确键和格式参数，不根据已有显示文字猜翻译键。CommonLoadingView 在进度更新时使用一次性文本 API，未准备语言时保留原文；启动壳 Loading 不依赖业务词表。Hub 常规失败设置 FeedbackKey，任意诊断设置 Feedback，两条通路不能同时保留。

## 设置候选

在资源服务就绪后等待 `GameSettingsService.InitializeAsync()`。仅从 Hub 调用 `SettingsFlow.OpenAsync(category, token)`，默认进入画质页，传 Audio／Input 等分类可定位对应独立分页。资源为 `UI/Settings/HubSettingsView`；API 会拒绝非 Hub 打开。Demo 使用自己的玩法设置，消费公共偏好和服务 API。

每次打开主设置 View 从 Current 创建完整独立候选。分类直接使用 `ViewTab.Init(labels, views: views)`，传入 GraphicsSettingsPanelView、AudioSettingsPanelView、InputSettingsPanelView、LanguageSettingsPanelView、AccessibilitySettingsPanelView。先 SetData 交付候选，再交给 ViewTab 按选择异步加载；Main Prefab 只保留公共挂载根，不制作 pageRoots 或 SettingsPage。AddSubView 持有子页销毁所有权，分页自身显示时回显、隐藏时释放编辑会话。分页资源位于 `UI/Settings/Panels/`。新增项在对应 View 直接使用 UISettingRow 与基础控件，修改引用后通过正式 UIBind 重新生成。

应用由 SettingsFlow 经 SettingsApplyAction 协调，Core 使用者也可直接等待 GameSettingsService.ApplyAsync。检查 SettingsApplyResult，不以控件请求值替代生效状态。显示模式或分辨率改变时，正式 DisplayConfirmView 在未缩放的 15 秒内等待保留或恢复；任务由 Flow 持有，尊重取消令牌。超时、拒绝、取消或失败恢复实际显示与旧偏好。正在提交时禁用重复应用，关页按原生命周期取消编辑。

恢复分类默认值从 GetDefaults 取该分类，覆盖当前候选对应部分，随后仍须用户应用。关页丢掉候选，不向制作 SO 写玩家数据。Demo 不提供公共设置入口；专属飞控／赛车参数继续放原 Demo 页面，保留纸卡、触控和业务菜单的原布局。

手动抗锯齿编辑 `candidate.Graphics.Antialiasing`；选 MSAA 时同时设置 `MsaaSamples`（2、4、8，默认4）。用 `DisplaySettingsRuntime.CanUseAntialiasing(mode, samples, out reason)` 查询当前档位支持情况。FXAA 等后处理抗锯齿关闭原生 MSAA；MSAA 关闭后处理抗锯齿。相机默认保留制作配置。

DLSS/DLAA 实际生效后禁用手动抗锯齿控件，并展示 `IsAntialiasingManagedByUpscaler`；等待首帧和请求失败分别显示 SDK 状态，不能只凭 `DlssMode` 请求值显示已接管。玩家选择保留在候选里，关闭或失败后恢复；实际原生模式/采样分别读取 `EffectiveAntialiasing` / `EffectiveMsaaSamples`。Deferred、XR、硬件或外部目标纹理不支持所选 MSAA 时显示降级原因，禁止修改共享管线或重建调用方目标纹理。

## 文案与字体

将完整中英句式写入 LanguageCatalog，动态内容使用 `{0}` 等参数，不拼接译文。配置 FontId 在两种语言下的字体与材质；普通缺字合并公共字符集并通过原 Font Builder 更新资产、保留 GUID。SetText 的 FontId 覆盖留空表示跟词条，显式零表示默认组。

制作检查菜单为 `Tools/SleepyDemos/Settings/Validate Global Settings Config`，只读检查正式配置、词表参数、字体/材质和字形覆盖；缺字报告给出公共字符集路径。

语言仅从 Hub 设置页提交。使用 SettingsFlow 保持外部协调：HubLanguageFlow 在场景导航空闲时校验资源，关闭并销毁旧 Hub 页面与缓存，移除语言派生数据，保存选择后重建 Hub 并重新打开语言页。重开回调只持有页面类型与分类，不能捕获旧控件。准备失败保留现页；关闭后失败尝试恢复旧偏好和 Hub，并回显实际失败／恢复结果。Demo 内不切语言。无需语言事件或全局 TMP 搜索；字体制作遵循[公共字体规范](../architecture/localization-fonts.md)。

## 输入与音量

GameplayInputRouter / InputActionSession 直接读取自身 Settings；不要给 Demo 再复制死区、反转和震动底座。提示绑定会话 Asset，不能绑定未生效的源动作。InputBindingPrompt.SetCaptionKey 在页面绑定时赋说明，SetBindingsVisible 控制辅助偏好；触控按钮仍保留可读 caption。

公共输入页只维护灵敏度、死区、反转、震动和图标风格，不配置 Demo 动作目录，不展示集中改键列表。恢复公共输入默认不删除旧改键。本期不新增 Demo 改键 UI；已有 InputRebindSession、InputSettingsCatalog 类型、SettingsBindingItemView 和 InputSettingsBindingRow 继续保留，未来宿主按自己的源资产使用候选编辑和 GameSettingsService.ApplyBindingOverridesAsync 提交，详见[公共输入接入](use-gameplay-input.md)。

普通 AudioSource 接 AudioBusSource 分类；动态本地音量调用 SetLocalVolume。DSP 自己在音乐/音效/UI 分类处使用 AudioSettingsRuntime.GetGain，保留自己的静音与音量限制，保留播放进度。

## 时间与画面作用域

手动模拟用 GameTimeService.DeltaTime；Input 更新、菜单和长按继续 unscaled。持有 GameTimeService.Push 的 Scope，结束、取消和场景卸载时 Dispose。短定格用 FreezeAsync，持续冻结用 Freeze 租约，嵌套释放后恢复仍有效的倍率。不得改 fixedDeltaTime 或 DSP 时钟。

世界相机由 UIRootManager.BindToBaseCamera 接入公共显示和后处理服务。PostProcessRuntime.Push 接正式预设，调用方保存并释放句柄。关闭某类效果或减少动态可能压制预设，体验页应显示实际允许状态。

SetBasePreset 指定当前相机基底，null清除；切镜时清理。Push 返回Scope，Update(preset)切预设或Update(profile => ...)编辑已有原生参数的候选副本。不能增删组件、销毁候选或修改共享纹理；异常和非有限参数不会替换当前效果，也不会改变作用域优先级。

## 电影演出

从稳定 Hub 体验公共能力时调用 `PresentationLabFlow.OpenAsync(token)`；该入口加载正式世界租约和 PresentationLabView，再打开 CinematicOverlayView 并交付预览容器。关闭使用 Flow 持有的具体实例，先停止／恢复本次效果，再归还表现 View 与世界资源并返回 Hub。体验页的后处理、时间和电影操作使用同一套正式公共 API。

其他调用方按需使用 UIManager 显示 `CinematicOverlayView`，可用 SetData(viewport) 指定画面容器，null 为全屏。正式资源为 `UI/Presentation/CinematicOverlayView.prefab`；View 持有 Core 的 CinematicPresenter，隐藏或销毁时立即 ResetEffects，由 UIManager／Loader 负责释放。固定引用通过正式 UIBind 维护，不经过静态注册。制作资源保留遮幕 Image/CanvasGroup、顶部/底部黑边、Title/SubTitle TMP/CanvasGroup；遮幕使用白 Sprite 和 UI/CinematicCurtain 材质，黑边位于目标容器内。

直接在取得的 View 实例上调用普通 void 方法，视觉完成使用回调，不包装成 UniTask：

```csharp
overlay.StartTransition(onCovered: world.ResetMotion,
    style: CinematicTransitionStyle.Wipe,
    closeDuration: 0.4f, openDuration: 0.4f,
    direction: CinematicWipeDirection.LeftToRight);
overlay.ShowLetterboxForAspect(2.39f, duration: 0.3f);
overlay.ShowTitle("Presentation.Chapter", "Presentation.Location");
overlay.HideLetterbox(onComplete: overlay.ResetEffects);
```

StartTransition 的 onCovered 是闭幕后执行的同步操作，closedHold 是该回调完成后的保持秒数。异步导航或加载拆成 CloseCurtain → 闭幕完成回调发起既有业务事务 → 业务结束后 OpenCurtain；错误处理、取消及回滚仍由业务负责。IsTransitioning 为 true 时不要再次发起遮幕；普通完成回调只在动画正常完成时调用，ResetEffects 停止时不调用，页面退出时也应使旧业务回调失效。

ShowLetterbox(heightRatio) 按目标容器高度比例显示黑边；ShowLetterboxForAspect(targetAspect) 按内容宽高比计算，HideLetterbox 缩回。标题使用稳定语言键，ShowTitle 可传 fadeIn、hold、fadeOut；缓动参数使用 DOTween Ease。动画采用未缩放时间，减少动态时进入和退出时长为零，时间冻结仍可执行。

公共 Presenter 不控制 HUD。页面保存明确传入的 CanvasGroup 原 alpha、interactable 和 blocksRaycasts，再管理自己的淡出 Tween；停止或卸载必须恢复这三个原值。体验页已由 PresentationCinematicPanel 完成这项工作。它保留七种样式、四方向、开闭时长、宽高比、HUD 和标题选项；播放中停止立即 ResetEffects，播放完成只剩黑边时以 HideLetterbox 回调收尾。页面同时只执行一个操作，重新播放先清理上次体验，所有完成和失败回调核对操作版本。关闭不等待视觉退出，先立即恢复，再沿用 UIManager 导航事务。

## 排障

初始化失败先查 ConfigAddress、YooAsset Config 采集和 SO 引用；字体失败查 FontId 双语言映射及材质 atlas。设置保存失败查 SettingsApplyResult、Feedback 与实际 IO/领域校验异常，页面提示一次，不把保存失败写成成功；读取失败统一回默认且不自动写回，不区分读取故障。画质降级查 DisplaySettingsRuntime.Status 与 StreamlineRuntime.Status。演出缺根先通过 UIManager 打开正式 View 并交付具体实例，禁止用临时 Canvas 兜底。

电脑端最低验收尺寸为 1920×1080，低于此范围不支持；不要按 Editor 面板的实际窗口尺寸启动 Hub 交互验收。现有 MainMenuNavigationPlayModeTests 复用 GameViewResolution 临时固定最低尺寸，结束后恢复用户选择。按直接受影响范围验证 Hub 设置打开和返回、各 Demo 公共入口已移除、分页候选保留与释放、绑定列表回收、显示确认恢复、Hub 语言重建以及演出停止／退出；实际交付结果和未验证项统一维护在 [模块状态](../modules/settings-presentation.md#交付状态与验证边界)。持续接入使用上述公共入口与正式资源，不依赖一次性制作菜单。
