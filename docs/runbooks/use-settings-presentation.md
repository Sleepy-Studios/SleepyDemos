# 公共设置与演出接入

## 设置候选

在资源服务就绪后等待 `GameSettingsService.InitializeAsync()`。每次打开页面使用 `Current` 创建候选，控件编辑候选字段。应用经 SettingsApplyAction 或直接等待 ApplyAsync；检查 SettingsApplyResult，不以控件请求值替代生效状态。显示变更的确认任务要能独立于设置页存活，并尊重取消令牌。

恢复分类默认值从 GetDefaults 取该分类，覆盖当前候选对应部分，随后仍须用户应用。关页丢掉候选；不能向制作 SO 写玩家数据。Demo 设置弹窗隐藏语言分类，画质快捷入口直接选择画质页。

## 文案与字体

将完整中英句式写入 LanguageCatalog，动态内容使用 `{0}` 等参数，不拼接译文。配置 FontId 在两种语言下的字体与材质；普通缺字合并公共字符集并通过原 Font Builder 更新资产、保留 GUID。SetText 的 FontId 覆盖留空表示跟词条，显式零表示默认组。

制作检查菜单为 `Tools/SleepyDemos/Settings/Validate Global Settings Config`，只读检查正式配置、词表参数、字体/材质和字形覆盖；缺字报告给出公共字符集路径。

Hub 提交语言调用 HubLanguageFlow，并提供重新打开语言页的可等待回调，不能捕获即将被销毁的控件。准备失败不销毁现页；关闭后失败会恢复旧语言与 Hub。无需语言事件，无需全局 TMP 搜索。

## 输入与音量

GameplayInputRouter / InputActionSession 直接读取自身 Settings；不要给 Demo 再复制死区、反转和震动底座。提示绑定会话 Asset，不能绑定未生效的源动作。InputBindingPrompt.SetCaptionKey 在页面绑定时赋说明，SetBindingsVisible 控制辅助偏好；触控按钮仍保留可读 caption。

改键使用 InputRebindSession 构造候选，Begin 监听单个普通/复合子绑定，取消调用 Cancel，重置调用 ResetAction。ExportOverrides 保存到候选 BindingOverrides[SourceId]，只有统一设置提交成功后才生效。新手柄添加布局识别及 InputGlyphCatalog 映射，不仅替换面键图案。

普通 AudioSource 接 AudioBusSource 分类；动态本地音量调用 SetLocalVolume。DSP 自己在音乐/音效/UI 分类处使用 AudioSettingsRuntime.GetGain，保留自己的静音与音量限制，保留播放进度。

## 时间与画面作用域

手动模拟用 GameTimeService.DeltaTime；Input 更新、菜单和长按继续 unscaled。持有 GameTimeService.Push 的 Scope，结束、取消和场景卸载时 Dispose。短定格用 FreezeAsync，持续冻结用 Freeze 租约，嵌套释放后恢复仍有效的倍率。不得改 fixedDeltaTime 或 DSP 时钟。

世界相机由 UIRootManager.BindToBaseCamera 接入公共显示和后处理服务。PostProcessRuntime.Push 接正式预设，调用方保存并释放句柄。关闭某类效果或减少动态可能压制预设，体验页应显示实际允许状态。

SetBasePreset 指定当前相机基底，null清除；切镜时清理。Push 返回Scope，Update(preset)切预设或Update(profile => ...)编辑已有原生参数的候选副本。不能增删组件、销毁候选或修改共享纹理；异常和非有限参数不会替换当前效果，也不会改变作用域优先级。

## 电影演出

制作正式表现 Prefab 时提供三个独立通道：全屏遮幕 Image/CanvasGroup、顶部/底部黑边、Title/SubTitle TMP/CanvasGroup。遮幕使用白 Sprite 和 UI/CinematicCurtain 材质；黑边位于实际容器内。初始化后在 OnShow Register；OnHide/OnDestroy Unregister，View 与 Loader 按 UIManager 生命周期释放。

PlayTransitionAsync 的中点接收取消令牌。正常流程等待中点完成再开幕；取消会解除视觉等待、清幕并释放队列，即使中点没有响应令牌。已启动的导航或加载不会被强制终止，中点业务必须自行完成事务或处理取消、回滚；调用方不能把视觉任务已取消当成业务已经停止。中点不能等待同一黑幕通道的另一场转场。

PushLetterbox 返回 CinematicPresenter.LetterboxHandle，仍可作为 IDisposable 持有。调用时明确传入需要隐藏的 HUD。正常结束等待 handle.ExitAsync()，默认沿用预设黑边时长，也可指定退出时长；它停止自身进入动画，再缩回到下层有效黑边并淡入允许恢复的 HUD。重复调用可同时等待同一退出任务，沿用首次参数与取消令牌；下层 owner 仍隐藏同一 HUD 时不会提前显现。finally、异常或场景卸载时 Dispose 立即恢复，无需等待动画。Exit 的主线程取消也立即清理，其它线程取消在主线程循环清理，取消异常仍传给等待者。

Title 使用稳定语言键。黑边进入和退出均使用未缩放时间，减少动态时动画时长为零，时间冻结仍可执行这些 API。切换或销毁表现根会取消排队和正在执行的视觉任务并立即恢复 HUD，已启动中点业务仍按自己的事务收口。

## 排障

初始化失败先查 ConfigAddress、YooAsset Config 采集和 SO 引用；字体失败查 FontId 双语言映射及材质 atlas。设置保存失败查 Feedback 和 LocalDataManager 的大小/校验反馈，不能直接覆写新默认值掩盖损坏。画质降级查 DisplaySettingsRuntime.Status 与 StreamlineRuntime.Status。演出缺根先完成正式 View 注册，禁止用临时 Canvas 兜底。
