# 公共设置与演出接入

场景加载文案从 GameSceneNavigator 交付明确键和格式参数，不根据已有显示文字猜翻译键。CommonLoadingView 在进度更新时使用一次性文本 API，未准备语言时保留原文；启动壳 Loading 不依赖业务词表。Hub 常规失败设置 FeedbackKey，任意诊断设置 Feedback，两条通路不能同时保留。

## 设置候选

在资源服务就绪后等待 `GameSettingsService.InitializeAsync()`。仅从 Hub 调用 `SettingsFlow.OpenAsync(category, token)`，默认进入画质页，传 Audio／Input 等分类可定位对应独立分页。资源为 `UI/Settings/HubSettingsView`；API 会拒绝非 Hub 打开。Demo 使用自己的玩法设置，消费公共偏好和服务 API。

每次打开主设置 View 从 Current 创建完整独立候选。分类直接使用 `ViewTab.Init(labels, views: views)`，传入 GraphicsSettingsPanelView、AudioSettingsPanelView、InputSettingsPanelView、LanguageSettingsPanelView、AccessibilitySettingsPanelView。先 SetData 交付候选，再交给 ViewTab 按选择异步加载；Main Prefab 只保留公共挂载根，不制作 pageRoots 或 SettingsPage。AddSubView 持有子页销毁所有权，分页自身显示时回显、隐藏时释放编辑会话。分页资源位于 `UI/Settings/Panels/`。新增项在对应 View 直接使用 UISettingRow 与基础控件，修改引用后通过正式 UIBind 重新生成。

应用由 SettingsFlow 经 SettingsApplyAction 协调，Core 使用者也可直接等待 GameSettingsService.ApplyAsync。检查 SettingsApplyResult，不以控件请求值替代生效状态。显示模式或分辨率改变时，正式 DisplayConfirmView 在未缩放的 15 秒内等待保留或恢复；任务由 Flow 持有，尊重取消令牌。超时、拒绝、取消或失败恢复实际显示与旧偏好。正在提交或监听改键时禁用重复应用，关页按原生命周期取消编辑。

恢复分类默认值从 GetDefaults 取该分类，覆盖当前候选对应部分，随后仍须用户应用。关页丢掉候选，不向制作 SO 写玩家数据。Demo 不提供公共设置入口；专属飞控／赛车参数继续放原 Demo 页面，保留纸卡、触控和业务菜单的原布局。

手动抗锯齿编辑 `candidate.Graphics.Antialiasing`；选 MSAA 时同时设置 `MsaaSamples`（2、4、8，默认4）。用 `DisplaySettingsRuntime.CanUseAntialiasing(mode, samples, out reason)` 查询当前档位支持情况。FXAA 等后处理抗锯齿关闭原生 MSAA；MSAA 关闭后处理抗锯齿。相机默认保留制作配置。

DLSS/DLAA 实际生效后禁用手动抗锯齿控件，并展示 `IsAntialiasingManagedByUpscaler`；等待首帧和请求失败分别显示 SDK 状态，不能只凭 `DlssMode` 请求值显示已接管。玩家选择保留在候选里，关闭或失败后恢复；实际原生模式/采样分别读取 `EffectiveAntialiasing` / `EffectiveMsaaSamples`。Deferred、XR、硬件或外部目标纹理不支持所选 MSAA 时显示降级原因，禁止修改共享管线或重建调用方目标纹理。

## 文案与字体

将完整中英句式写入 LanguageCatalog，动态内容使用 `{0}` 等参数，不拼接译文。配置 FontId 在两种语言下的字体与材质；普通缺字合并公共字符集并通过原 Font Builder 更新资产、保留 GUID。SetText 的 FontId 覆盖留空表示跟词条，显式零表示默认组。

制作检查菜单为 `Tools/SleepyDemos/Settings/Validate Global Settings Config`，只读检查正式配置、词表参数、字体/材质和字形覆盖；缺字报告给出公共字符集路径。

语言仅从 Hub 设置页提交。使用 SettingsFlow 保持外部协调：HubLanguageFlow 在场景导航空闲时校验资源，关闭并销毁旧 Hub 页面与缓存，移除语言派生数据，保存选择后重建 Hub 并重新打开语言页。重开回调只持有页面类型与分类，不能捕获旧控件。准备失败保留现页；关闭后失败尝试恢复旧偏好和 Hub，并回显实际失败／恢复结果。Demo 内不切语言。无需语言事件或全局 TMP 搜索；字体制作遵循[公共字体规范](../architecture/localization-fonts.md)。

## 输入与音量

GameplayInputRouter / InputActionSession 直接读取自身 Settings；不要给 Demo 再复制死区、反转和震动底座。提示绑定会话 Asset，不能绑定未生效的源动作。InputBindingPrompt.SetCaptionKey 在页面绑定时赋说明，SetBindingsVisible 控制辅助偏好；触控按钮仍保留可读 caption。

输入页使用 LoopScrollView 与 SettingsBindingItemView 显示同类绑定行；正式模板为 `UI/Settings/SettingsBindingItemView.prefab`，InputSettingsBindingRow 保存提示、上下文和按钮引用。根据正式 InputSettingsCatalog.Entry.Actions 的动作 GUID／文案键枚举单绑定和复合子项，行 Key 包含 SourceId、action.id 与 binding.id。每行调用 BindBinding 显示准确索引，不把同动作中的其他键位别名画成当前绑定。公共 UI／Menu 的安全动作通过 CanRebind 保持不可编辑。

改键使用 InputRebindSession 构造候选，Begin 监听单个普通/复合子绑定，取消调用 Cancel，重置调用 ResetAction。回收行时 Clear 原候选与按钮回调，隐藏输入页或关闭宿主时释放会话。ExportOverrides 保存到候选 BindingOverrides[SourceId]，只有统一设置提交成功后才生效；运行会话保留原上下文，并等待提交时已按住的新绑定松开。提示 Prefab、Kenney 图标和新手柄映射的具体接入见[公共输入接入](use-gameplay-input.md)。

普通 AudioSource 接 AudioBusSource 分类；动态本地音量调用 SetLocalVolume。DSP 自己在音乐/音效/UI 分类处使用 AudioSettingsRuntime.GetGain，保留自己的静音与音量限制，保留播放进度。

## 时间与画面作用域

手动模拟用 GameTimeService.DeltaTime；Input 更新、菜单和长按继续 unscaled。持有 GameTimeService.Push 的 Scope，结束、取消和场景卸载时 Dispose。短定格用 FreezeAsync，持续冻结用 Freeze 租约，嵌套释放后恢复仍有效的倍率。不得改 fixedDeltaTime 或 DSP 时钟。

世界相机由 UIRootManager.BindToBaseCamera 接入公共显示和后处理服务。PostProcessRuntime.Push 接正式预设，调用方保存并释放句柄。关闭某类效果或减少动态可能压制预设，体验页应显示实际允许状态。

SetBasePreset 指定当前相机基底，null清除；切镜时清理。Push 返回Scope，Update(preset)切预设或Update(profile => ...)编辑已有原生参数的候选副本。不能增删组件、销毁候选或修改共享纹理；异常和非有限参数不会替换当前效果，也不会改变作用域优先级。

## 电影演出

从稳定 Hub 体验公共能力时调用 `PresentationLabFlow.OpenAsync(token)`；该入口加载正式世界租约和 PresentationLabView，再打开 CinematicOverlayView 并交付预览容器。关闭使用 Flow 持有的具体实例，先停止／恢复本次效果，再归还表现 View 与世界资源并返回 Hub。体验页的后处理、时间和电影操作使用同一套正式公共 API。

其他调用方按需使用 UIManager 显示 `CinematicOverlayView`，可用 SetData(viewport) 指定画面容器，null 为全屏。正式资源为 `UI/Presentation/CinematicOverlayView.prefab`；View 通过 CinematicOverlayBindings 初始化，在 OnShow Register、OnHide/OnDestroy Unregister，并由 UIManager／Loader 负责释放。若调整制作资源，保留三个独立通道：遮幕 Image/CanvasGroup、顶部/底部黑边、Title/SubTitle TMP/CanvasGroup。遮幕使用白 Sprite 和 UI/CinematicCurtain 材质，黑边位于目标容器内，固定引用通过正式 UIBind 维护。

PlayTransitionAsync 的中点接收取消令牌。正常流程等待中点完成再开幕；取消会解除视觉等待、清幕并释放队列，即使中点没有响应令牌。已启动的导航或加载不会被强制终止，中点业务必须自行完成事务或处理取消、回滚；调用方不能把视觉任务已取消当成业务已经停止。中点不能等待同一黑幕通道的另一场转场。

PushLetterbox 返回 CinematicPresenter.LetterboxHandle，仍可作为 IDisposable 持有。调用时明确传入需要隐藏的 HUD。正常结束等待 handle.ExitAsync()，默认沿用预设黑边时长，也可指定退出时长；它停止自身进入动画，再缩回到下层有效黑边并淡入允许恢复的 HUD。重复调用可同时等待同一退出任务，沿用首次参数与取消令牌；下层 owner 仍隐藏同一 HUD 时不会提前显现。finally、异常或场景卸载时 Dispose 立即恢复，无需等待动画。Exit 的主线程取消也立即清理，其它线程取消在主线程循环清理，取消异常仍传给等待者。

标题调用 CinematicRuntime.ShowTitleAsync，使用稳定语言键；遮幕使用 PlayTransitionAsync，黑边与 HUD 淡出使用 PushLetterbox 的句柄。黑边进入和退出均使用未缩放时间，减少动态时动画时长为零，时间冻结仍可执行这些 API。切换或销毁表现根会取消排队和正在执行的视觉任务并立即恢复 HUD，已启动中点业务仍按自己的事务收口。

## 排障

初始化失败先查 ConfigAddress、YooAsset Config 采集和 SO 引用；字体失败查 FontId 双语言映射及材质 atlas。设置保存失败查 Feedback 和 LocalDataManager 的大小/校验反馈，不能直接覆写新默认值掩盖损坏。画质降级查 DisplaySettingsRuntime.Status 与 StreamlineRuntime.Status。演出缺根先完成正式 View 注册，禁止用临时 Canvas 兜底。

正式 Hub-only 主 ViewTab、五独立分页与体验页已完成本轮电脑端定向运行及中英三画幅验收；最终 Editor Offline 实际首包已通过；独立 Player/IL2CPP 与真实硬件按用户要求后置。电脑端最低验收尺寸为 1920×1080，低于此范围不支持；不要按 Editor 面板的实际窗口尺寸启动 Hub 交互验收。现有 MainMenuNavigationPlayModeTests 复用 GameViewResolution 临时固定最低尺寸，结束后恢复用户选择。按直接受影响范围验证Hub 设置打开和返回及各 Demo 公共入口已移除、分页候选保留与释放、绑定列表回收、显示确认恢复、Hub 语言重建以及演出停止／退出；测试与真实设备结果记录到[实施记录](../agent/settings-goal-progress.md)。持续接入使用上述公共入口与正式资源，不依赖一次性制作菜单。
