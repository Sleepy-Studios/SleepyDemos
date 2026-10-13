# 无人机界面美术维护

无人机使用深色仪表、遥测分栏和暖橙重点按钮。外观入口是 `Assets/LoadResources/Demos/drone_flight/Prefabs/UI/`；玩法机体与仪表绘制不转换为普通按钮模板。

## 颜色、字体与图片

| 用途 | 当前保存值与来源 |
|---|---|
| 仪表与调试按钮 | 深蓝灰 `#172027`，调试按钮 alpha 约 0.98；HUD 小按钮 alpha 0.88 |
| 主要文字 | 暖白 `#F4F1E9`；辅助卡片文字 `#AAB8C1` |
| 帮助页副标题 | RGB `(0.55, 0.60, 0.63)`，约 `#8C99A1`；与机型卡辅助文字分开维护 |
| 开始飞行 | 橙 `#FF8A30`，深蓝灰文字；帮助返回按钮使用 `#F4A23A` |
| 普通按钮焦点边 | RGB `(0.2, 0.85, 1)`，约 `#33D9FF`；操作焦点，不表示装备或机型业务选中 |
| 普通矩形图片 | 公共 `UI/Common/Sprites/Surfaces/Surface.png`；保留载具预览与仪表专属图片 |

中文及混排使用公共 `Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset`，英文按 FontId 使用公共 `Fonts/TMP_FontAssets/EN/BebasNeue_EN.asset`。两份字体资产的原字重均为 Bold，下列样本的 TMP `FontStyles` 为 Normal；HUD 模式和状态中的既有 `<b>` 富文本保留。字体、材质和回退规则见[公共字体](../../../architecture/localization-fonts.md)，不新增无人机专属字体或补字资产。

### 当前文字制作样本

数值为当前 Prefab/布局代码的 Canvas 设计单位，不是所有页面统一字号，也不是实际屏幕像素。

| 角色 | 当前字号 | 正式样本 |
|---|---|---|
| 页面标题 | 44 | `DroneFlightHelpView.prefab` 的 Title |
| 分区标题 | 32 | 帮助页 FlightTitle、CameraTitle、TakeoffTitle、EquipmentTitle |
| 正文 | 26；复位说明 24 | 帮助页 Flight、Camera、Takeoff、Equipment、Reset |
| 控件文案 | 调试 22；返回/关闭 26–28；开始飞行 46 | Foundation 的 Debug/Back/Close/Return/StartButton；具体使用方见下表 |
| 机型主副文字 | 标题 26、说明 21、选中提示 19 | `DroneVehicleCard.prefab`；触屏由 `DroneVehicleSelectionLayout` 将前两者改为 30 / 25 |
| 辅助说明 | 19 | 帮助页 Subtitle；其灰色不替代主要文字色 |
| 遥测数字 | 32 | `Parts/TelemetryRoot.prefab` 的 HeightText、DistanceText、HorizontalText、VerticalText；单位/次要信息保留既有富文本比例 |
| 遥测状态与警告 | StatusText 25；GearText、EquipmentText 23；WarningText 24 | `Parts/TelemetryRoot.prefab`；警告使用暖橙，不能只靠颜色识别 |
| 输入提示 | 普通 Caption 25、Help 实例 Caption 24、KeyText 18、分隔符 22 | `Foundation/DroneInputPrompt*.prefab`；Telemetry 紧凑提示保留 Caption 17–19、KeyText 13、分隔符 15 |

采集来源为 `DroneFlightHelpView.prefab`、`Parts/TelemetryRoot.prefab`、Foundation 文字节点及 `Assets/Scripts/Hotfix/Demos/DroneFlight/UI/DroneVehicleSelectionLayout.cs`。HUD 的文字比例另见同目录 `DroneHudPresentation.cs` 与 DroneFlightHudView。紧凑提示的现有小字号不是新增普通信息的默认值，须在实际缩放截图中核对可读性。

新的独立页面、额外分区或图表数字没有覆盖本表的角色时，字号与排版须另出设计确认；不从普通按钮字号反推仪表数字，也不修改现有页面来凑统一层级。

### 当前尺寸与图形制作样本

| 样本 | 当前保存值与适用范围 |
|---|---|
| Help 普通控件 | Footer/Return 为 280×72，Close 为 64×64；不以风格源默认的 280×64 覆盖这两个作者矩形 |
| Help 分区卡 | Content Grid 间距 20×20、卡高 270；`DroneHelpLayout` 在视口宽度至少 1050 时使用两列，窄屏一列，卡宽随视口计算 |
| Help 提示行 | Grid 单元 220×42、间距 12×4；ResetPromptRows 的横向间距 16；正文保留滚动，不靠缩字号避让 |
| 普通输入图标 | Glyph Icon 的最小宽/高 36、首选高 36；真实图标 Simple + Preserve Aspect，普通 Caption 与 Glyphs 间距 10，槽/分隔符间距 3 |
| HUD 紧凑提示与进度 | Telemetry 图标高 24、Caption 与 Glyphs 间距 6；ResetProgressFill 作者矩形 726×6。不能将普通提示的 36 高复制进遥测分栏 |
| 普通按钮表面与焦点边 | Surface 与 FocusBorder 的 Sprite 四边 Border 均为 16；现有按钮风格 PPU Multiplier 为 1，完整焦点边 Sliced、Fill Center 关闭；BaseBorder 默认隐藏，不另加普通灰描边或投影 |

这些是已保存的宿主样本。图标宽度保留 Sprite 比例，长键名的键帽按公共提示能力扩宽；不据以上数字限制动态文本宽度。新增阴影、其他圆角或图标比例没有现成样本，须设计确认，不能直接套用 Hub 表面。

## 完整基础控件库与源码颜色

基础控件统一从 `Assets/LoadResources/Demos/drone_flight/Prefabs/UI/Foundation/` 取用，文件以 `Drone` 开头，按动作或控件用途命名；名字不编码字号或生成哈希。每一项公共基础控件均有本 Demo 的真实 Variant，尚未使用或与公共外观一致的控件同样保留入口。覆盖清单及组合内部子变体契约见[基础资源维护](../../../modules/ui-foundation-assets.md#四-demo-完整风格库)。Loading 继续使用正式全局与场景流程，不进入这套 Foundation。

固定 UI/HUD 源码颜色从 `UIColors.DroneFlight` 读取；透明度和富文本复用 `WithAlpha`、`WrapText`。Prefab/Variant 的外观仍直接保存 RGBA、图片和字体，不绑定运行时色板。角色配置、动态颜色和 Shader 参数继续由其原契约维护。

## 控件继承与覆盖

公共 `CommonButton` 提供 Button、同节点 UIState、取消转发和交互反馈；本 Demo 的普通按钮与命令按钮已关联 `Prefabs/UI/Foundation/` 风格 Variant。普通深色、暖橙主要按钮与紧凑关闭按钮分开维护，页面只覆盖尺寸、位置、业务文案、点击事件和导航，不能把整页保存成公共按钮 Variant。

以下路径相对于本 Demo 的 `Prefabs/UI/`，用正式使用方选择已有源，不按文件名相近另建样式。

| 风格 Variant | 实际使用方 |
|---|---|
| `Foundation/DroneDebugButton.prefab` | DroneFlightDebugView/DebugPanel 的 Copy、Vectors、Close |
| `Foundation/DroneBackButton.prefab` | DroneFlightVehicleSelectView/Content/BackButton |
| `Foundation/DroneCloseButton.prefab` | DroneFlightHelpView/SafeArea/Panel/Close |
| `Foundation/DroneReturnButton.prefab` | DroneFlightHelpView/SafeArea/Panel/Footer/Return |
| `Foundation/DroneStartButton.prefab` | DroneFlightVehicleSelectView/Content/StartButton |
| `Foundation/DroneVehicleCard.prefab` | DroneFlightVehicleSelectView/Content 的 PlainButton、GrappleButton、HarpoonButton |
| `Foundation/DroneTouchCommandButton.prefab` | Parts/OperationPanel、Parts/TouchControls 的命令按钮，及其 HUD 嵌套实例 |
| `Foundation/DroneHudCommandButton.prefab` | DroneFlightHudView/SafeRoot 的 OpenOperations、OpenHelp |
| `Foundation/DroneInputPrompt.prefab` | Help、VehicleSelect、HUD、OperationPanel、TouchControls 的普通提示；亦是装备提示的父 Variant |
| `Foundation/DroneInputPrompt2.prefab` | Parts/TelemetryRoot 的 CameraOperationPrompt、GearOperationPrompt、ResetOperationPrompt |
| `Foundation/DroneEquipmentInputPrompt.prefab` | Parts/TelemetryRoot 的 EquipmentActionPrompt、EquipmentAimPrompt、EquipmentLinePrompt |
| `Foundation/DroneInputPromptButton.prefab` | 已有可点击风格源；当前正式 UI Prefab 没有直接嵌套使用，不为目录齐全新增页面入口 |
| `Foundation/DroneProgressFill.prefab` | Parts/TelemetryRoot.prefab/ResetPanel/ResetProgressFill |

调试页 Copy / Vectors / Close、帮助页 Return / Close、机型选择页 BackButton / StartButton 使用普通按钮 Variant。普通文案节点已原生关联为 `Label` 并保留对象及绑定引用。三个机型卡片通过 ChoiceVehicleCard 风格 Variant 继承按钮基底，统一标题、说明、预览及选中提示的角色结构；Selected 通过 CardFill 的 GraphicColor 表现，并在 Normal 恢复原颜色。页面保留各机型文案、预览图片、矩形和业务显隐，不裁掉信息来匹配单文案模板。

输入提示的正式 Variant 位于 `Prefabs/UI/Foundation/DroneInputPrompt*.prefab`，继续由公共输入能力驱动。HUD 命令、OperationPanel、TouchControls 上的 InputCommandButton 与保持/释放语义保留，不把输入动作改成普通点击回调。遥测与装备专属图形不受普通按钮 Variant 控制。

装备操作、瞄准与收放线提示统一继承 `Foundation/DroneEquipmentInputPrompt.prefab`，它是 `DroneInputPrompt.prefab` 的风格 Variant，仅集中维护 `#F4A23A` 的 RGB；原 Alpha、字号和布局保留。相机、起落架与复位提示继承右对齐的 `DroneInputPrompt2.prefab`。页面不再分别覆盖装备提示的 RGB；替换嵌套来源后须核对 HUD `InputPromptGroup` 的六个 Telemetry 引用，原 GUID 不变不代表内部 localFileID 不变。

重置进度填充使用 `Foundation/DroneProgressFill.prefab`，继承公共 `Common/_TemplateInstantiatePrefab/CommonProgressFill.prefab`，集中维护暖橙 RGB `(0.95686275, 0.63529414, 0.22745098)`。TelemetryRoot 只保留根布局、可见性和真实进度；轨道及重置操作仍属于原 HUD。改变嵌套来源后核对 HUD 的 Image／UIProgressBar 及其余所有绑定，不能只检查 Prefab GUID。

## 可读性与换风格

深色按钮使用亮色文字，主要暖橙按钮使用深色文字。五态反馈在公共 UIState 上维护，禁用态与焦点必须可辨，不能仅依赖橙色判断业务选中。图标、反馈边和预览装饰不拦截射线，根点击矩形保持原尺寸。

CanvasGroup 的页面显隐、触控区域启停由既有 View/输入生命周期负责；InteractionFeedback 的透明度只属于控件反馈。不要把页面 alpha 或 blocksRaycasts 写入按钮风格。

原来完全透明、无状态/业务/动画引用的 InteractionFeedback Image 已原生清理，保留 CanvasGroup 与 FocusBorder。关联与引用核对完成，最新正式入口视觉和真实设备结果按实际验证记录，不能由保存成功推断。

换风格时先编辑本 Demo Foundation Variant 的图片、色值与文字样式，再核对页面是否保留了同属性的实例覆盖；只清除已转交 Variant 的外观覆盖。保留页面布局和业务绑定，验证调试页、帮助页、机型选择与飞行 HUD 的键鼠/手柄/触控可读性。原生替换与覆盖保护按[制作与更换控件风格](../../../runbooks/use-core-ui-components.md#制作与更换控件风格)操作，不 Unpack 或整树 Revert。公共使用边界见[基础 UI 组件](../../../runbooks/use-core-ui-components.md)与[公共字体](../../../architecture/localization-fonts.md)。
