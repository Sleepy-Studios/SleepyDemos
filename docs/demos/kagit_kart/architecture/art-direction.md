# 纸艺卡丁车界面美术维护

保留纸卡、墨色、胶带、纸边和硬阴影，页面露出立体书舞台。正式资源位于 `Assets/LoadResources/Demos/kagit_kart/Prefabs/UI/`；普通控件集中样式不能把纸艺页面改成 Hub 面板。

## 颜色、字体与图片

| 用途 | 当前保存值与资源 |
|---|---|
| 纸面 | 暖纸色约 `#FAF4E4`；普通纸卡 Image 为白色乘纸纹 |
| 正文/墨色按钮 | `#2D2A32`；较早普通文字约 RGB `(0.18, 0.16, 0.19)` |
| 重点红 | `#D9483B`；成绩估算等辅助文字用墨色 alpha 0.72 |
| 纸卡图片 | `Art/UI/PaperCard.png`，保留 Portrait、Tape、Bookmark、纸边与独立硬影 |
| 常规字号 | 普通菜单按钮 31，赛道卡 28，角色卡 18；正文、辅助与比赛数值按下表区分 |

比赛大数字、倒计时与结算等级保持现有字号层级；Countdown 等专属组件运行时调整字号，不套用普通按钮字号。中文及混排复用公共 `Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset`，英文通过 FontId 使用公共 `Fonts/TMP_FontAssets/EN/BebasNeue_EN.asset` 与既有回退；两份字体源均为 Bold，按[公共字体规范](../../../architecture/localization-fonts.md)维护，不新增 Demo 专属字体或音频。

## 当前制作样本

下表记录正式 Prefab 的 Canvas 设计单位，不是所有画幅的最终像素。Bold/Normal 指 TMP `fontStyle`；这些纸艺文字样本保存 `fontWeight=400`、`fontStyle=Bold`，沿用现有宿主配置，不据此给公共控件再合成粗体。字体源与新公共文字的 Normal 约定见[公共美术规范](../../../architecture/ui-art-direction.md#字体与层级)。

| 层级 | 实际节点样本 | 字号 / 字重 |
|---|---|---|
| 页面标题 | Mode / HeadingCard / Heading；Settings / Sheet / Heading；Results / Sheet / Title | 44 / Bold；48 / Bold；60 / Bold |
| 分区与卡片标题 | Settings 的 MusicTitle、SfxTitle 等；ModeCard / Label；ClassCard / PaperName | 30 / Bold；36 / Bold；44 / Bold |
| 正文 | ModeCard / Description；ClassCard / Description | 23 / Bold；26 / Bold |
| 辅助 | Mode / Step；Settings / Status；Results / Feedback | 20 / Bold；23 / Bold；20 / Bold |
| 小型玩法标签 | HUD / ItemNameTag / Item；CountTag / ItemCount | 16 / Bold；21 / Bold，仅用于道具槽，不能作为菜单正文默认值 |
| 设置数值 | Settings / MusicValue、SfxValue | 28 / Bold |
| 比赛主要数值 | HUD / Place；LapCard / Lap、Clock | 124 / Bold；46 / Bold、36 / Bold |
| 比赛辅助数值 | HUD / PlaceCard / Of | 保存 21.08 / Bold；`KagitKartHudNotice.ApplySizing` 在显示触控时改为 29.76 |

Results 复用真实 `Widgets/KagitKartResultRow.prefab`，保留以下字级，不四舍五入成统一 24 或 32：

| Results 区域 | 节点与准确字号（均 Bold） |
|---|---|
| 页面与表头 | Title 60，Kicker 24；Header 的 HeadingPlace / Driver / Time / Best / Points / Portrait 为 18.72 |
| 成绩行主要信息 | Place 42；Driver 26.88；Time 与 BestLap 24；Points 32.4 |
| 成绩行辅助信息 | Bonus 25.92；Movement 20.4；BestTag 22 |
| 反馈与纪录 | Feedback 20；NewRecord / RecordText 45.6 |

倒计时是专属例外：`KagitKartHudNotice.ApplySizing / ShowCountdown` 以 `Screen.height <= 520` 为小画幅分支，数字使用 200 / 260；倒计时结束词“出发！”使用 130 / 150。数字交给 `KagitKartCountdownCard` 显示，TMP 数字节点在数字阶段隐藏；600 宽的倒计时容器高度为数字字号 × 1.23，结束词高度为其字号。这里的结束词不是冲线提示：HUD / FinishPaper / Text 另存 84 / Bold。父级仍受现有 `emScale` 与 Canvas 缩放影响，不能把设计字号当作最终屏幕像素或改成普通按钮层级。

| 几何 / 层次 | 当前代表样本 |
|---|---|
| 设置行间距与高度 | Music / Sfx 根为 510×48，中心 y=275 / 207，间距 68；标题矩形高 54，数值矩形高 52；选择纸卡常用 180×53 |
| 标题页与结算按钮 | Title / OpenGroup 640×86、HubGroup 200×60；Results / Restart、Next 为 570×74，Hub 为 285×74。Foundation 根 280×64 只是源默认，不是页面统一尺寸 |
| 成绩表 | Results / Header 1140×36；8 个 ResultRow 为 1140×56、中心间隔 56。结果板宽 1240，普通比赛高 790、计时赛高 560；`KagitKartResultsView.Fit` 按安全区可用宽高减 72 后等比适配 |
| 图标比例 | ResultRow / Portrait 显示矩形 57.6×52，`preserveAspect=true`；维持头像纹理比例，不按行高拉成长方形 |
| 分隔与边框 | Settings / TitleLine 为 920×3；Results / HeaderRule 高 3.36，NewRecord 四边条厚 3.65；ResultRow / TopRule 高 3.36、BottomRule 高 1.92，玩家框四边厚 2.4 |
| 圆角边界 | 纸边由 PaperCard 的 Sliced 图形维护，不套 Hub 几何圆角；KagitSlider 的继承手柄 ImageExtend 禁用，保留 25×48 的墨色矩形手柄 |
| 硬影 | Settings 的 SheetShadow / 选择卡 Shadow、Mode 三卡 Shadow 位移均为 `(8,-10)`，墨色 alpha=1；Results 的 Restart / Next / Hub Shadow 位移 `(5.0727277,-6.2)`、墨色 alpha=0.8。均是独立纸卡 Image 层，不改成模糊阴影 |

字级来源是 [Results 页面](../../../../Assets/LoadResources/Demos/kagit_kart/Prefabs/UI/KagitKartResultsView.prefab)、[成绩行](../../../../Assets/LoadResources/Demos/kagit_kart/Prefabs/UI/Widgets/KagitKartResultRow.prefab)、Settings / Mode / HUD Prefab 及对应 Foundation Variant；运行尺寸分支见 [HudNotice](../../../../Assets/Scripts/Hotfix/Demos/KagitKart/UI/KagitKartHudNotice.cs) 与 [ResultsView](../../../../Assets/Scripts/Hotfix/Demos/KagitKart/UI/KagitKartResultsView/View/KagitKartResultsView.cs)。这些样本用于延续现有制作，不规定新的动效、时长或曲线。

## 控件继承与覆盖

普通纸卡按钮、设置选择钮及 HUD 命令按钮已关联 `Prefabs/UI/Foundation/` 风格 Variant，公共 CommonButton 提供点击、取消与五态，Demo Variant 集中纸纹、墨色、字形尺寸及局部装饰。页面保留布局矩形、文案、业务事件、预览对象和导航；不将整页、Sheet 或外层纸卡 Group 转成普通控件。

KagitKartChoiceCard 是角色/章节焦点预览桥，保留原组件与事件。已有标签如 BackLabel、Track0Label 可在原生关联前改名为 Label，保持对象与 UIBind 引用；Portrait、Tape、SelectionMark 等保留为 Demo 专属附加节点。设置页的 14 个选项仍是 Button + UIState 的业务 Selected，不擅自换成 Toggle 或滑块开关；两条音量 Slider 保留滑轨、范围和数值事件，不直接匹配按钮源。

两条音量 Slider 共用 `Foundation/KagitSlider.prefab`，它继承公共 `Common/_TemplateInstantiatePrefab/CommonSlider.prefab`。Variant 集中矩形墨色轨道、填充、手柄及子节点布局，保留纸艺外观；不启用继承手柄的圆角、描边或阴影。页面维护根位置、0–10 的整数音量值和业务事件，不重复保存这些图片样式；同名 Track／Fill Area／Handle Area 子节点保留原组件引用。

设置选择钮与设备分类选择钮在 Choice 风格 Variant 的同一 UIState 上追加 SelectionMark 的 Selected 业务态，页面保留选择值和事件；不复制另一套选中管理器。三张纸重卡通过 ClassCard Variant 继承按钮基底，保留 Grammage、PaperName、Description、Gauge 与分格线；显示内容、特有纸色和原矩形继续按纸重角色维护，不把多标签卡简化成只有一个 Label。

三张比赛模式卡使用 ModeCard Variant，保留 Label、Description 和原专属信息节点；各模式文字及布局继续属于页面。14 个设置选择钮与 3 个设备选择钮的 Selected 目标、值及 Normal 复位字段保留在同一 UIState 契约中。

下表路径以 `Prefabs/UI/` 为根，列出真实嵌套使用入口；前述同类外观在对应 Variant 修改，使用方只维护必要根布局与业务内容。

| Foundation Variant | 真实使用方样本 |
|---|---|
| `KagitKartPaperButton31_0db8ba13.prefab` | Title / Open、Hub；Mode / Settings、Controls；Pause / Resume、Restart；Results / Hub；Settings / Back |
| `KagitKartInkButton31_c2574bf7.prefab` | Results / Restart、Next；Standings / Next；Podium / Menu |
| `KagitKartChoicePaperButton31_0db8ba13.prefab` | Settings 的 14 个选择项；Controls / Keyboard、Gamepad、Touch |
| `KagitKartModeCard_aefbb962.prefab` | Mode / SingleRace、GrandPrix、TimeTrial |
| `KagitKartClassCard_5032af98.prefab`、`KagitKartClassCard_4cdd508f.prefab`、`KagitKartClassCard_eec291ae.prefab` | Class / Class80、Class120、Class200，按此顺序对应 |
| `KagitKartPaperButton18_d30e1c21.prefab` | Character / Character0–7 |
| `KagitKartPaperButton28_76978498.prefab` | Track / Track0–3 |
| `KagitKartPaperButton23_be787881.prefab` | Pause / Hub |
| `KagitKartPaperCommandButton_0db8ba13.prefab` | HUD / Throttle、Brake、Drift、Item、LookBack |
| `KagitKartPaperCommandButton_65be1b45.prefab` | HUD / ItemCard，保留道具槽专属内容 |
| `KagitSlider.prefab` | Settings / Viewport / SheetGroup / Sheet / Music、Sfx；公共源为 `UI/Common/_TemplateInstantiatePrefab/CommonSlider.prefab` |

Title 的 CoverHit 保留透明全屏翻书点击区。HUD 的离散/保持命令按钮复用 CommandButton 风格 Variant，并保留原 InputCommandButton 与业务组件；转向、道具槽、倒计时和外层纸卡 Group 仍按玩法结构维护。正式提示 Variant 位于 `Prefabs/UI/Widgets/KagitKartInputPrompt*.prefab`，保持动作绑定与隐藏释放语义。

## 可读性与换风格

暖纸底用墨色文字，墨色主要按钮用暖纸文字；焦点边、业务选中标记与禁用表现分别可辨。纸影独立于点击目标，装饰不拦截射线。普通控件反馈不能覆盖父 Group 的缩放/旋转或改变根点击尺寸。

KagitKartMenuPaperMotion 持有外层卡片 Group 的 CanvasGroup，既有 Book/Sheet 动作和生命周期继续维护；按钮的 InteractionFeedback 只使用自己局部的透明度。不得把所有 CanvasGroup 归入按钮风格，或移除父 Group 上的现有组件。

全屏 Loading 使用 `Art/UI/HubPreview.png` 的无文字纸赛道，`Tracks/Desk.png` 是透明缩略图，不能拉满屏幕。World 销毁时按捕获的旧 options/menu/modal/hud/hints 实例清理，覆盖公共场景重载；新会话的同类型 View 不由旧 owner 按类型关闭。

换风格先编辑 Demo Foundation Variant 的纸纹、墨色与局部装饰，再逐项清除页面已转交给 Variant 的同属性覆盖；保留外层 Group、Bookmark 路径、引用、布局和业务 Selected。检查模式、纸重、角色、赛道、暂停、设置及结算，确保舞台仍可见、长中文仍可读、真实键鼠/手柄/触控焦点不误提交。公共规范见[基础 UI 组件](../../../runbooks/use-core-ui-components.md)与[原作维护](../runbooks/source-migration.md)。
