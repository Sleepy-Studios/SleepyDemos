# 公共设置与表现能力

## 职责与入口

`Core.Runtime/Settings` 是本机公共设置的唯一保存者；`Localization`、`Input`、`Rendering`、`TimeControl`、`Presentation` 分别落实语言字体、输入、渲染、玩法时间和电影演出。Core 不引用 Hotfix。`Hotfix/Module/Settings` 提供设置 Flux 状态及 Hub 语言重建协调；专属飞控、赛车和触控布局仍由 Demo 维护。

制作配置位于 `Assets/LoadResources/Config/Settings`，电影预设位于 `Config/Presentation`。SO 只保存制作数据。`GameSettingsService.Current` 和 `GetDefaults()` 返回独立快照，玩家候选写入 `LocalDataManager` 的 `SleepyDemos.Settings`，不修改 SO。当前没有 Luban 双来源。

制作允许范围和帧率选项由 SettingsOptions 配置，服务 Options 返回副本；候选仍通过公共安全边界校验，制作数据只能收紧范围。InputHosts 给控制页面提供宿主身份与源动作，页面先克隆再编辑；语言名称和格式文化来自 LanguageService.Languages。

## 启动与事务

资源服务就绪后，正式启动的 `GameSettingsInitializeSystem` 在公共 UI 初始化之前加载 `GameSettingsConfig`。Editor Demo 直启调用同一个 `InitializeAsync`；并发等待共用完成源，不能用只适合顺序复用的 UniTask Preserve 代替并发共享。

无有效记录时使用制作默认值；损坏记录保留反馈。只有不存在新记录时迁移旧 `SleepyDemos.Graphics.DlssMode`；新记录成功落盘后删除旧键。

设置页持有完整候选，应用顺序为显示/画质、音量、后处理、保存、语言选择、发布公共参数通知。关闭或取消页面丢弃候选；恢复本页默认只改该页候选。显示模式与分辨率改变必须提供独立确认任务，15 秒取消或拒绝时恢复之前实际显示及旧偏好。输入会话在提交通知后应用验证过的参数与 override。通知不用于文本刷新。

显示服务克隆管线、RendererData 和制作 Feature，释放 DLSS 后再换管线，最终重绑相机。共享资产不写回。公共 Overlay 相机栈不能使用原生 TAA；能力查询返回禁用原因，强制请求时反馈 SMAA 实际状态。请求 DLSS 档位与 SDK 实际档位分别读取，不因设备不支持丢掉请求。

## 语言与字体

首版语言 `zh-CN` / `en`，默认中文。文案表字段固定 `Key / FontId / Chinese / English`；模块使用键前缀分组。FontId 映射支持一个语言多种字体及配套材质，多个编号可复用同一字体。默认编号为零，覆盖参数的 null 和零含义不同。

`LanguageService.Get/Format/SetText` 在页面创建、数据刷新和条目绑定时一次性执行。SetText 只设文案、字体和材质。字重、字号、颜色及对齐由 TMP/Prefab 维护，不给每个文本添加监听组件。

语言入口仅 Hub 开放。`HubLanguageFlow` 在场景导航空闲时校验资源，关闭并销毁全部 Hub 页面（含缓存），移除语言派生的 MainMenuData，应用保存语言，再显示新的 Hub 和语言设置页。协调任务不属于旧设置页。失败尝试恢复旧偏好并重建，反馈实际恢复结果；不会虚报成功。Demo 内固定当前语言，返回 Hub 后才能再次切换。

## 输入、音频与时间

现有 InputActionSession / GameplayInputRouter 复用公共参数和有效绑定。InputRebindSession 只改未启用候选副本，按稳定源动作 ID 和绑定 ID 导出；确认、返回和 UI 导航保留安全入口。InputBindingPrompt 的动作说明用语言键赋值，设备与绑定变化刷新键帽表现。最近实际使用的设备决定提示，漂移和连接本身不提交动作。

普通音源使用 AudioBusSource；音乐、音效和 UI 分类增益与音源自己的音量相乘。KagitKart 的 DSP 在分类混音处乘公共增益，不调整 DSP 时钟，不用 Stop 实现静音。辅助偏好约束 Demo 表现，专属开关继续可进一步限制。

GameTimeService 是 timeScale 的唯一公共演出入口，手动玩法使用 DeltaTime；Unity 物理保留固定步长，不再重复乘倍率。最近倍率作用域生效，任何冻结租约优先。Scope 由调用方拥有，页面/场景退出释放。UI、采样、门闩、长按、渐变和演出使用未缩放时间；LocalPauseState 继续保存失焦/断连及明确确认继续规则。

## 后处理与电影演出

PostProcessRuntime 使用原生 URP Volume，按参数覆盖场景效果；预设和临时 Scope 克隆 Profile，最近有效作用域只覆盖勾选参数。玩家效果允许开关、亮度及减少动态最后约束输出，公共 UI 不参与世界后处理。相机解绑恢复并清理临时作用域，赛车纸印 Feature 保留自身生命周期。

CinematicRuntime 要求 UIManager 托管的正式 View 显式初始化并 Register CinematicPresenter；不创建替代 Canvas。黑幕和 Title 各自串行。闭幕后等待中点任务，正常完成再开幕；失败或取消清掉本次遮幕并释放黑幕队列。中点不响应令牌时，取消只解除视觉等待，已启动业务仍继续，由业务自己的事务负责完成、取消或回滚。

黑边/HUD 使用可释放嵌套 LetterboxHandle，HUD 只控制明确传入的 CanvasGroup。正常 ExitAsync 停止自身进入动画，按未缩放时间将黑边过渡回下层有效状态，同时把自身 HUD 隐藏贡献降为零；其它 owner 仍隐藏的 HUD 不提前显现。重复 Exit 共用完成源，支持同时等待；减少动态时退出时长为零。Dispose、表现根解绑及主线程取消 Exit 立即移除覆盖并恢复有效状态；其它线程取消在主线程循环清理。最后释放恢复原透明度与交互。

中点可等待场景加载、切镜、字幕和黑边，**不能等待同一个黑幕通道的另一场转场**，否则队列会等待自身。字幕在显示时解析语言/FontId。未注册表现根时明确报错；G10 正式 UI 装配后才能从页面体验。

## 验证边界

直接相关测试覆盖快照、字体、输入候选、音量增益、后处理参数、管线资产保护、时间租约、遮幕队列/取消与 HUD 恢复。启动、Hub 重建及各 Demo 通过保存的正式资源另做集成回归。测试结果与阶段状态见[实施记录](../agent/settings-goal-progress.md)。模拟设备不能替代真实手柄；截图、Player 与硬件结果分别记录。

接入步骤见[公共设置与演出接入](../runbooks/use-settings-presentation.md)。
