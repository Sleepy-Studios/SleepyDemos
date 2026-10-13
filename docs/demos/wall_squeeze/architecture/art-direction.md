# 夹爆它界面美术维护

夹爆它保持纯色、直角、黑框分格和清晰怪物图例；不使用圆角卡片、渐变或阴影。当前正式入口是 `Assets/LoadResources/Demos/wall_squeeze/Prefabs/UI/WallSqueezeHudView.prefab` 与 `WallSqueezeMenuView.prefab`，[已确认概念图](../art/gameplay-concept.png)只提供外观依据，实际结构以正式资源为准。

## 颜色、字体与图片

| 用途 | 当前保存值 |
|---|---|
| HUD 按钮 | 暖白 `#F4F1EA`，黑色文字 |
| 菜单按钮 | 黑底白字，26 / Normal |
| HUD 暂停/重试 | 52 / Bold、48 / Bold；沿用全拉伸制作矩形 |
| 菜单标题与描述 | 50 / Normal、25 / Normal；HUD 标题 62 / Bold，数字与标签按下表区分 |

矩形填色使用公共 `UI/Common/Sprites/White.png`，三个按钮风格 Variant 的完整直角焦点边使用公共 `UI/Common/Sprites/Surfaces/FocusBorderSquare.png`（24×24、四边 Border 3、Sliced、Fill Center 关闭）。怪物和墙体保留各自专属图形。红怪、蓝住户与盔甲/钻缝纹理是玩法识别信息，不能由公共按钮色板覆盖。

中文及混排使用公共 `Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset`，英文通过 FontId 使用公共 `Fonts/TMP_FontAssets/EN/BebasNeue_EN.asset` 与既有回退；两份字体源均为 Bold。词条和字库维护遵守[公共字体规范](../../../architecture/localization-fonts.md)，不得新增 Demo 专属或补字字体。

## 当前制作样本

以下为正式 Prefab 的 Canvas 设计单位，不等于最终画幅像素。Bold/Normal 是 TMP `fontStyle`：HUD 固定文字保存 `fontWeight=700`、Bold；HUD 两按钮 Label 继承 400 并保存 Bold；菜单标题、正文和按钮为 400、Normal。公共 HarmonyOS_CN 字体源本身为 Bold，沿用这些既有宿主值，不额外创建字库或统一重染字重。

| 层级 | 实际节点 | 字号 / 字重 |
|---|---|---|
| 页面标题 | HUD / Header / TitleCell / Title；Menu / Panel / Title | 62 / Bold；50 / Normal，二者不合并成同一个标题规格 |
| 分区 / 关卡信息 | HUD / Header / StageCell / Stats | 34 / Bold |
| 主要动作 | HUD / Header / Pause / Label；Footer / Retry / Label | 52 / Bold；48 / Bold |
| 正文 / 菜单动作 | Menu / Panel / Detail；Retry、Continue、Next、Hub / Label | 25 / Normal；26 / Normal |
| 辅助说明 | HUD / Footer / LegendCell / Hint | 23 / Bold |
| 主要数值 | HUD / Header / RemainingCell / Remaining、ProtectCell / Residents | 基础 35 / Bold；数字由 `WallSqueezeHudPanel.Render` 的 `<size=150%>` 放大为 52.5 |
| 时间数值 | HUD / StageCell / Clock | 28 / Bold；剩余时间 ≤10 秒时改为 `#D0302A` |
| 怪物图例 | Normal / Armored / Slipper 的 Label | 名称 37 / Bold；现有 `<size=65%>` 说明为 24.05，不能误记为整行 37 |

| 几何 / 层次 | 当前代表样本 |
|---|---|
| HUD 分区高度 | Header 高 118、贴顶 y=-59；Footer 高 140、贴底 y=70；宽度随父 Canvas 拉伸 |
| 黑框与留白 | Stage / Remaining / Protect Cell 使用 `sizeDelta=(-16,-32)`；Title / Legend Cell 为 `(-32,-32)`。保留黑底露出的分格与上下 16 留白，不增加一套阴影边框 |
| HUD 按钮矩形 | Pause 的 x 锚点 `[0.935,1]`，Retry 为 `[0.76,1]`；均 y 锚点 `[0,1]`、`sizeDelta=(-16,-32)`、x=-8。以所在 Header/Footer 的可用尺寸计算，不能套 Variant 默认 280×64 |
| 菜单面板与按钮 | Panel 720×590；四个按钮根均为 520×64。Continue / Next 共用 y=30 且按状态互斥，Retry y=-55，Hub y=-225；相邻主按钮中心间距 85、可见边间距 21 |
| 图标比例 | 普通 / 顶墙图例 Icon 58×58；钻缝 Icon 58×30，保留细扁识别；Label 左侧预留 84，不把三种图例都拉成方形 |
| 焦点边 / 圆角 / 阴影 | 三个按钮 Variant 使用 `FocusBorderSquare.png`：24×24、Border 3、Sliced、Fill Center 关闭、PPU multiplier=1；完整直角轮廓。面板及 HUD 不添加圆角、渐变或软硬阴影 |

样本来源为 [HUD Prefab](../../../../Assets/LoadResources/Demos/wall_squeeze/Prefabs/UI/WallSqueezeHudView.prefab)、[Menu Prefab](../../../../Assets/LoadResources/Demos/wall_squeeze/Prefabs/UI/WallSqueezeMenuView.prefab) 及下列 Variant；数值字号与菜单互斥由 [HudPanel.Render](../../../../Assets/Scripts/Hotfix/Demos/WallSqueeze/WallSqueezeHudPanel.cs)、[MenuPanel.Render](../../../../Assets/Scripts/Hotfix/Demos/WallSqueeze/WallSqueezeMenuPanel.cs) 确认。这是当前制作样本，不是新的动效或全页面固定尺寸规范。

## 完整基础控件库与源码颜色

基础控件统一从 `Assets/LoadResources/Demos/wall_squeeze/Prefabs/UI/Foundation/` 取用，文件以 `Wall` 开头，按动作或控件用途命名；名字不编码字号或生成哈希。每一项公共基础控件均有本 Demo 的真实 Variant，尚未使用或与公共外观一致的控件同样保留入口。覆盖清单及组合内部子变体契约见[基础资源维护](../../../modules/ui-foundation-assets.md#四-demo-完整风格库)。Loading 继续使用正式全局与场景流程，不进入这套 Foundation。

新默认库的矩形表面使用现有 `Art/UI/Square.png`，Filled 填充使用公共 `White.png`，边框使用 `FocusBorderSquare.png`；不沿用带透明圆角的 `Surface.png`、`CapsuleSurface.png` 或胶囊边框。ImageExtend 的圆角、渐变及阴影同时关闭，不能只关闭组件效果却保留图片本身的圆角。已有用途 Variant 与已确认页面继续保留各自作者参数。

固定 UI/HUD 源码颜色从 `UIColors.WallSqueeze` 读取；透明度和富文本复用 `WithAlpha`、`WrapText`。Prefab/Variant 的外观仍直接保存 RGBA、图片和字体，不绑定运行时色板。角色配置、动态颜色和 Shader 参数继续由其原契约维护。

## 控件继承与覆盖

普通按钮已关联 `Prefabs/UI/Foundation/` 的三个风格 Variant：菜单黑底白字与 HUD 暖白黑字分别维护，继承公共 CommonButton 的结构、UIState 五态和取消转发。页面保留按钮矩形、字号层级、业务文字、点击事件和导航；根节点按下时不缩放点击区域。

已关联对象为 Menu 的 Panel / Retry、Continue、Next、Hub，以及 HUD 的 Header / Pause、Footer / Retry。Label 与 InteractionFeedback 按完整层级原生匹配并保留对象引用；Header/Footer 的黑框布局保留在页面。提示 Variant 位于 `Prefabs/UI/Foundation/WallInputPrompt*.prefab`，继续消费公共输入行为。Foundation 同时维护 Toggle、Dropdown、Tab 等完整基础控件风格，当前页面未使用不影响其复用入口。

| Variant（位于 `Prefabs/UI/Foundation/`） | 正式使用入口 |
|---|---|
| `WallMenuButton.prefab` | `WallSqueezeMenuView / Panel / Retry、Continue、Next、Hub`，26 / Normal、黑底白字 |
| `WallPauseButton.prefab` | `WallSqueezeHudView / Header / Pause`，52 / Bold、暖白黑字 |
| `WallRetryButton.prefab` | `WallSqueezeHudView / Footer / Retry`，48 / Bold、暖白黑字 |

三者继承公共 `UI/Common/_TemplateInstantiatePrefab/Btns/CommonButton.prefab`。换风格沿真实 Variant→上述页面入口核对；固定文本节点的 62 / 50 等页面层级不由按钮源承担。

## 可读性与换风格

黑框和直角色块为主要分区，焦点边与按下色必须清晰，禁用降低强调但保留可读文字；Selected 与按钮交互态不能混用。装饰、怪物图例与反馈边不拦截点击，怪物识别不能只靠文字。

页面 CanvasGroup 只由既有 View 显隐负责，InteractionFeedback 透明度只属于按钮反馈。更换风格先维护本 Demo 的黑/白按钮 Variant，再清除页面已交给 Variant 的色值、图片和状态外观覆盖；保留 HUD 字号及伸缩布局。验证普通游戏、暂停、失败重试、过关与返回 Hub，并确认窄横屏下数字和怪物图例仍可读。公共选型见[基础 UI 组件](../../../runbooks/use-core-ui-components.md)。

场景 Loading 复用已批准的公共瑞士抽象背景；本 Demo HubPreview 含真实 HUD 文案与读数，只用于卡片预览。HUD/Menu 属于 World 会话，成功卸载及公共场景重载由 OnDestroy 按实例关闭；返回失败保留原 HUD/Menu 实例与输入上下文。
