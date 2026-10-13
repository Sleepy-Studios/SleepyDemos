# 公共 UI 美术确认

本轮使用内置 imagegen 生成效果图；基础控件与Loading的完整提示词保存在同名 `.prompt.txt`，新增设置图标按确认板维护正式SVG源。图是方向确认材料，不是 Unity 运行截图，不裁切图片中的文字作为正式 UI 资源。

| 内容 | 图 | 状态 |
|---|---|---|
| Hub 公共基础 UI | [v5-01：文字滑块与 Toggle](foundation-v5-controls.png) | 已装配并经用户验收最新原生01截图；BtnSwitch1 双文字胶囊滑块、整块点击切换，独立 Toggle 保留 |
| Hub 公共基础 UI | [v3-02：居中页面标题](foundation-v3-panels.png) | 已装配并经用户验收最新原生02截图；仅 Commom_Title 页面标题居中，其他面板标题左齐 |
| Hub 公共基础 UI | [v2-03：设置与输入](foundation-v2-settings-input.png) | 用户已确认；Loading 缩略图仅展示此前已确认方向 |
| 运行期 Loading | [v2](scene-loading-v2.png) | 用户已确认并装配 CommonLoading；无人机场景全屏主视觉与独立信息层 |
| 启动 Loading | [v3](startup-loading-v3.png) | 用户已确认并装配 StartupLoading；瑞士风抽象艺术背景与边缘信息 |
| 设置页分类图标 | [v1](settings-icons-v1.png) | 用户已确认设计，并指定安装 Vector Graphics 直接复用单套 SVG；尺寸示例不作为多套资产 |
| 纸艺卡丁车设置与触控 | [v1](kagit-settings-touch-v1.png) | 2026-10-13 用户已确认选中配色和左右按钮方向；图中方向键尺寸仅为概念，最终按用户修正缩为 160×140，原生装配和验收独立记录 |

按用户要求，仓库仅保留当前采用的图和提示词；早期网页式 Loading、海岛背景、误画 BtnSwitch1 的控件图及被替代的规范板已清理。原始生成输出仍保留在本机生成目录，必要的旧图副本位于 `$CODEX_HOME/generated_images/<thread-id>/retired-designs/`，不作为当前设计依据。

03正式原生截图已获用户认可；随后新增设置页五个分类图标和具体默认值显示要求。图标源为 `Assets/LoadResources/UI/Common/Sprites/Settings/*.svg`，五个透明200×200 viewBox以50×50显示，保留同一套曲线路径与双色，无独立小稿或状态稿。原始规范板用于确认形状，不裁切板上的文案或尺寸样例作为正式资源。uGUI使用Vector Graphics包的SVGImage，具体限制见[公共美术规范](../../../architecture/ui-art-direction.md)。

Loading 参考 [Game UI Database 的 GTA V 自动加载画面](https://www.gameuidatabase.com/gameData.php?id=115&autoload=1599)与[荒野大镖客 2 加载画面](https://www.gameuidatabase.com/gameData.php?id=34&autoload=5759)，两页真实截图已浏览核对。GTA V 采用全屏插画、左下品牌、右下加载状态；其中右侧活动宣传块不套用本项目。借鉴画面优先和边缘状态布局，不使用这些游戏的版权图片作为项目资产。

启动 v3 与运行期 v2 均已正式装配，图片和提示词保留为审批材料。去字正式背景分别为 `Assets/LoadResources/UI/Common/Loading/Art/StartupBackdrop.png` 与 `SceneBackdropDrone.png`，由 `Assets/Scenes/StartupLoading.prefab`、`Assets/LoadResources/UI/Common/CommonLoading.prefab` 使用；文字、说明、百分比与 Filled 均为独立 UI。背景使用等比覆盖，信息层不参与裁切。运行期 DLSS 与纸艺卡丁车复用现有无文字预览图，夹爆它复用已确认抽象背景，具体映射见[场景导航](../../../modules/scene-runtime.md)。启动 v2 仅布局获认可，海岛背景不实施；Loading v1 均已否决。

原生装配及 Canvas 预览已完成，原生资源收口报告为 `Library/UIArt/finalization-report.txt`；预览不是正式入口运行截图。最新四 Demo 往返、重载、失败恢复、多画幅可读性与性能结果须按实际运行另行记录，不以效果图批准或 Prefab 保存代替。

公共规范板生成的宣传语、色值标签与装饰英文不作为正式需求；实现以[公共美术规范](../../../architecture/ui-art-direction.md)、现有资源与实际功能为准。动效不建立统一规范；纯透明度控制优先 CanvasGroup。

纸艺卡丁车局部效果图只确认上述选中态和左右触控按钮；其余设置项、百分比、页面布局与比赛画面继续沿用正式实现，不按生成图片改业务值或删三态选项。样式落在该 Demo Foundation Variant，维护边界见[纸艺卡丁车美术规范](../../../demos/kagit_kart/architecture/art-direction.md)。效果图不是原生截图，不从图中裁切文字或箭头作正式资产。

## 完整 Hub 基础效果图

出图阶段只更新审核图、提示词与确认记录，不因图片生成修改正式外观。01／02已按用户修正要求装配，并通过最新原生截图的完整视觉验收；03效果图与正式原生截图也已获用户认可。结构、编译及测试通过不能代替外观还原验收；以真实Canvas截图逐项核对边界、比例、间距、字体、状态、浮层和阴影，不能只复核颜色。

实施顺序按用户最新要求：先精确还原并验收图01、图02，再装配依赖这些控件的图03设置行与滚动文字。按键提示可作为独立部分处理；不从按键提示装配入口自动执行图03其余内容。

2026-10-13 用户已分别回复“01 通过”“02 通过”，验收对象为 `Library/UIArt/Captures/ApprovedBoard01_1920x1080_alpha1.0.png` 与对应02原生截图；此后才执行03设置行装配。03不重新覆盖01已批准的 Slider 图形参数，行结构只维护标题、数值、原因和控件位置，公共控件外观仍从共同源继承。03正式截图与运行验收继续独立记录。

| 图 | 覆盖的正式基础资源与表现 |
|---|---|
| 01：控件与状态 | CommonButton 五态；Tab/TabItem 横向页签；ViewTabVertical 纵向页签；AccordionTab/AccordionViewTab 折叠页签；BtnSwitch1 二选一按钮、独立原生 Toggle、BtnSwitch2 滑块开关；Dropdown 开合；普通 Filled 进度及 Slider |
| 02：面板与反馈 | BG_Large、BG_Middle、BG_Gift、BG_SideTab；Commom_Title；普通与浮层表面；CommonTips 三种消息；SimpleTips 定位说明；主副字号与配色 |
| 03：设置与输入 | DropdownRow、SliderRow、SwitchRow、ActionRow；InputPrompt、InputPrompt2、InputPromptButton；TMPAutoScroll；StartupLoading v3 与 CommonLoading v2 的既有确认方向 |

进度条和 Slider 复用现有组件，并提取 `CommonProgressFill`、`CommonSlider` 为共同结构源；Loading、设置行及 Demo 风格 Variant 继承这些源，不复制另一套行为。Tab 与 ViewTab、AccordionTab 与 AccordionViewTab 共用各自的外观表达，页面生命周期职责保留。键位与文字仅作布局示例，正式运行继续读取真实绑定、图标目录和语言配置。

新版重点补足普通按钮边界、完整 Dropdown 浮层、统一 Tab/开关、面板层次、轻阴影、左右按键提示与设置行。图中的色值、字体和 Loading 缩略图均为概念表现，不能代替正式资源或把缩略图重绘当作 Loading 新批准版本。图集由内置 imagegen 生成，提示词与图片同目录同名保存。

BtnSwitch1 复用现有 UIBtnSwitch 的 On/Off 切换逻辑，表示两个业务值（例如左手/右手）：两项文字始终在胶囊轨道内，选中胶囊块切换到左右位置；整块根按钮点击切换，与 BtnSwitch2 使用同样的点击契约，不增加分区命中或左右两个独立按钮。它不是“选中/未选中”复选框。新增 Toggle 使用原生 Toggle 与公共 UIState、取消转发和样式，具体接入不复制状态或解析底座。页面标题居中适用于 Commom_Title 标题条，不把各面板、分区和正文一律居中。
