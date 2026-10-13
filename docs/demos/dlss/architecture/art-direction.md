# DLSS 实验室界面美术维护

实验室使用深蓝底、青色重点与少量屏幕信息，让世界中的细节和运动差异承担主要展示。保存的 UI 入口是 `Assets/LoadResources/Demos/dlss/Prefabs/UI/DlssControls.prefab`，不额外制作公共设置页面。

## 颜色、字体与图片

当前触控命令底色为 RGBA `(0.08, 0.17, 0.24, 0.94)`，约 `#142B3D`；移动/观察 Caption 为白色，普通输入提示沿用 RGB `(0.96, 0.94, 0.91)` 的暖白。FocusBorder 使用 RGB `(0.2, 0.85, 1)`，约 `#33D9FF`，只表示操作焦点，不把整屏实验内容染成高亮色。提示图标使用公共输入图标资产，实际键名与动作说明来自本场动作绑定。

中文及混排使用公共 `Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset`，英文按 FontId 使用公共 `Fonts/TMP_FontAssets/EN/BebasNeue_EN.asset`。两份字体资产原字重均为 Bold，当前 Caption、KeyText 和分隔符使用 TMP `FontStyles.Normal`；不再合成加粗来强调普通动作。字体、材质和回退按[公共字体](../../../architecture/localization-fonts.md)维护，不为实验室另建字体、缺字补丁或专属音频。

### 当前文字制作样本

| 角色 | 当前字号与正式样本 |
|---|---|
| 控件说明／辅助文字 | `DlssControls.prefab` 的 Move/Caption、Look/Caption 为 20；DeviceHints 内部分普通提示实例 Caption 也保留 20 |
| 动作说明 | `Parts/DlssInputPrompt.prefab` 的 Caption 为 22；触控 Sprint、Reset、Exit 由内部提示显示实际动作文案，命令按钮的基础 Label 默认隐藏 |
| 键名与组合键分隔符 | 普通提示 KeyText 为 18、分隔符为 22，字体与原图标颜色一起在提示 Variant 维护 |
| 页面／分区标题、长正文、实验读数 | 当前正式控件页没有独立制作样本；后续增加时另出设计确认，不把公共展示板字号写成已落地样式 |

采集来源为 `DlssControls.prefab`、`Parts/DlssInputPrompt.prefab` 与 `Foundation/DlssInkCommandButton_600d2e89.prefab`；动态动作文案接入见 `Assets/Scripts/Hotfix/Demos/Dlss/DlssControlsView/View/DlssControlsView.cs`。以上是 Canvas 设计单位，不代表实际屏幕像素；没有额外建立字号主题或统一缩字逻辑。

### 当前尺寸与图形制作样本

| 样本 | 当前保存值与适用范围 |
|---|---|
| 触控命令 | TouchControls 的 Sprint、Reset、Exit 作者矩形均为 156×48；Reset 与 Exit 的中心纵距 60，即边缘留白 12。风格源默认 280×64 不覆盖页面矩形 |
| 触控移动／观察区 | Move、Look 作者矩形为 200×200，Caption 为 190×44；保留手势区、位置与真实命令，不按普通按钮表面重做 |
| 普通提示图标与间距 | Glyph Icon 最小宽/高 36、首选高 36，KeyText 18；真实图标 Simple + Preserve Aspect，图标与文字比例约 36:22；Caption 与 Glyphs 间距 10，槽/分隔符间距 3 |
| 提示行宿主 | DeviceHints 作者矩形 500×42，三条普通提示横向中心相隔 160；提示自身宽度仍由文本与真实图标布局计算，不将作者宽度当成截断上限 |
| 命令表面与边界 | 根 Image 为无 Sprite 的 Simple 深蓝色块；BaseBorder 默认隐藏。完整 FocusBorder 复用公共 Surfaces/FocusBorder，Sprite 四边 Border 16、Sliced、Fill Center 关闭、PPU Multiplier 1；不另加投影 |

新的图标比例、分区间距或阴影没有现成样本，须设计确认；普通键帽长名称扩宽遵循公共提示能力，不拉伸真实手柄/鼠标 Sprite。

## 控件继承与覆盖

Exit / Sprint / Reset 已关联 `Prefabs/UI/Foundation/DlssInkCommandButton_600d2e89.prefab`，继承公共 CommonButton 的结构和五态行为，只集中深蓝/青色外观。它们都保留原 InputCommandButton，属于玩法命令控件；根 Button、业务组件、提示绑定及持续按压/释放契约保持原引用，不重建为普通点击回调。

正式提示 Variant 入口为 `Prefabs/UI/Parts/DlssInputPrompt*.prefab`。页面保留命令位置、尺寸、动作与可见性；公共输入提示源负责结构和行为，本 Demo 的提示 Variant 负责外观。当前没有普通 Toggle、Dropdown 或 Tab，不为目录齐全创建无使用方控件。

路径相对于 `Assets/LoadResources/Demos/dlss/Prefabs/UI/`：

| 风格 Variant | 实际使用方 |
|---|---|
| `Foundation/DlssInkCommandButton_600d2e89.prefab` | DlssControls/TouchControls 的 Sprint、Reset、Exit；它们的命令提示仍是内部嵌套组件 |
| `Parts/DlssInputPrompt.prefab` | DlssControls/DeviceHints 与 TouchControls 命令内的实际提示 |
| `Parts/DlssInputPrompt2.prefab`、`Parts/DlssInputPromptButton.prefab` | 已有右对齐／可点击风格源；当前正式 UI Prefab 没有直接嵌套使用，不为使用这两个源新增入口 |

## 可读性与换风格

命令底色须让白字在明亮和暗色场景上都可读，关键信息保持在安全区；设备图标不挤占动作说明。焦点与禁用分别可辨，装饰不拦截射线，触控根矩形不因按下反馈缩小。

CanvasGroup 的命令区显隐交给既有设备识别与 View 生命周期，局部反馈透明度只控制 InteractionFeedback；不要用整屏 alpha 掩盖实际可操作状态。换风格先改 Demo Variant，再检查页面遗留色值/字体/图片覆盖；不修改公共源让其他 Demo 一起变色。原生替换与覆盖保护按[制作与更换控件风格](../../../runbooks/use-core-ui-components.md#制作与更换控件风格)操作，不 Unpack 或整树 Revert。完成后检查键鼠、手柄与触控提示，确认保持命令在隐藏、禁用及离场时释放。公共边界见[输入接入](../../../runbooks/use-gameplay-input.md)与[基础 UI 组件](../../../runbooks/use-core-ui-components.md)。

原来零 alpha 且无状态、业务或动画引用的反馈容器 Image 已原生移除；CanvasGroup 与 FocusBorder 继续承担五态反馈。资产关联与字段核对完成，最新正式 AppEntrance 运行、可读性与性能结果以实际验收记录为准。
