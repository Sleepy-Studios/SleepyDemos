# 基础 UI 预制体维护

基础组件位于 Core.Runtime，模板位于公共 UI/Common。独立页面由 Hotfix View 使用它们，基础组件不持有 Demo 规则。接入前逐项检查字体、Sprite、Border、绑定、布局、遮罩、射线与导航；使用方式见 [基础 UI 组件](../runbooks/use-core-ui-components.md)。

## 逐项检查范围

| 预制体 | 本次检查与修复 |
| --- | --- |
| CommonButton | 新增可直接复用的按钮模板，五态反馈与取消转发 |
| BtnSwitch1 | 原 BtnSwitch 重命名的选择型，保留 GUID `1840a1e3ffbf3114dbc14437350022cc`；同一 UIState 的业务态与五态叠加 |
| BtnSwitch2 | 滑块开关型；On/Off 位置与语义色、独立状态文案、交互五态及禁用复位 |
| TabItem | TMP 文案、同一 UIState 的业务 Selected 与五态、无重复反馈层 |
| Tab | 模板项和选中对象绑定，移除容器的无效交互 |
| ViewTabVertical | 嵌套 Tab、分页布局、滚动遮罩和绑定 |
| AccordionTab | 根项/叶项 TMP 文案、展开与选中、嵌套交互覆盖 |
| AccordionViewTab | 嵌套折叠项、分页绑定与滚动区域 |
| Dropdown | 默认文案保持 TMP_Text；Arrow 改为 Image，使用 `Sprites/Tips/Arrow.png` 并保留展开 180° 旋转 |
| Commom_Title | 关闭按钮五态、中文字体、装饰层射线 |
| BG_Gift | 标题/关闭控件绑定，装饰节点无交互 |
| BG_Large | 关闭控件与尺寸，纯背景无导航 |
| BG_Middle | 关闭控件、字体、图片 Border 与布局 |
| BG_SideTab | 嵌套 Tab/关闭按钮覆盖及反馈层去重 |
| CommonLoading | Filled 补齐白色 Sprite，背景维持 Sliced |
| CommonTips | 原模板/图标/滚动绑定，关闭按钮五态及射线 |
| SimpleTips | Blocker、返回作用域、定位和遮罩绑定 |
| TMPAutoScroll | 文本/viewport/字体绑定，保留原滚动实现 |

同名嵌套控件也需检查实际覆盖。不要只修改源模板而留下覆盖旧状态的实例；不要删除嵌套源节点后创建第二个同名反馈层。

`_TemplateInstantiatePrefab` 基础模板统一使用暖白、深墨和朱红，按钮五态保留根节点尺寸与缩放，使用颜色、焦点边和透明度区分状态；滑块自身的局部反馈不改变按钮点击矩形。外观制作保存不等于交互或视觉验收通过，仍需检查真实输入和嵌套使用方。

公共设置 `DropdownRow` 的选项容器保持独立不透明背景与纵向自适应高度，覆盖真实选项和间距，避免滚动表单的字段透入浮层。使用同页 popupRoot 时，继续检查完整选项的边界及后续 Modal 的排序。

选项容器锚定控件下边缘、顶部 Pivot，宽度跟随字段，空间足够时从字段下方向下展开；不要让自适应高度向上撑入分类 Tab。组件禁用即时归还，整树启停或销毁延后归还，制作矩形与顺序必须完整恢复。

公共设置与输入另有以下正式基础 Prefab，路径相对于 `Assets/LoadResources/`：

| 预制体 | 维护边界 |
|---|---|
| `UI/Common/Settings/DropdownRow`、`SliderRow`、`SwitchRow`、`ActionRow` | `UISettingRow` 引用原 Dropdown、Slider、Switch、Button；不承担分类切换或保存。滚动表单保持点击区域稳定 |
| `UI/Common/Input/InputPrompt` | 图标/键帽组与独立文案；从实际会话绑定读取按键，设备变化仅刷新绑定表现 |
| `UI/Common/Input/InputPromptButton` | 上述提示的可点击 Variant；复用 InputCommandButton 的离散/保持输入与释放语义 |

SwitchRow 内部 Control 嵌套 BtnSwitch2，行根 GUID 和 UISettingRow 组件保持不变；Hub 偏好 13 处及体验页 11 处共 24 个调用继续复用它。Control 的纵向 anchorMin/anchorMax 均为 0.5，制作尺寸为 64×28，不用拉伸锚点增加滑块高度。替换内部控件时重绑 control、toggle、onLabel、offLabel；页面生成字段及体验子组件只引用行根，不因 Control 的本地 fileID 改变而重建页面绑定。语言选项及 Auto/On/Off 三态减少动态不属于该迁移。

四个 Demo 从公共提示创建自己的 Prompt / PromptButton Variant，资源覆盖负责外观，公共组件负责行为；卡丁车保留纸艺资源。公共设置只在 Hub 开放，不制作 Demo 设置宿主。分类子页、固定表单与循环绑定列表的选择及调用案例统一维护在[基础组件手册](../runbooks/use-core-ui-components.md#从需求找到资源和示例)。

## 行为和生命周期

每个控件只有一个 UIState，默认状态为 Normal、Hover、Focused、Pressed、Disabled。同节点 Selectable 自动驱动五态；无 Selectable 的业务或装饰节点手动切换，不挂独立交互桥或全局状态管理器。Tab/开关继续持有业务值，所需 Selected、On、Off 追加在同一状态表。

自动控件按 Normal 基底、业务态、交互态顺序应用。Normal 保存所拥有属性的恢复值，Hover 提亮并显示细边，Focused 使用明确焦点边，Pressed 变深或改变装饰，Disabled 降低强调且关闭反馈层。状态退出恢复仍有效的业务表现；隐藏清除按下、悬停与 Submit 瞬态，不清空业务值。滚动表单保持点击矩形稳定，不统一缩放按钮根节点。触屏抑制交互强调，禁用态仍须清楚可见。

保留现有 Decoration、纸卡等节点及强类型引用，关闭由 UIState 拥有属性的原生 Transition。迁移通过 Unity 原生 Prefab 操作合并状态与重绑引用，保留源资产 GUID、Variant 继承和 Demo 的外观覆盖；不要按状态数组位置搬迁业务定义。状态反馈 Graphic 不拦截射线，触控命令的保持、释放和隐藏清理由 InputCommandButton 负责。

UIState 在 Inspector 验证时清空查找缓存；更改状态定义后不会继续查到旧状态。Dropdown 默认文案及 Accordion 文案使用 TMP，不能只换 Prefab 的文字组件而不修复读取方。

UIProgressBar 包装同节点 Filled Image 的数值和颜色。白色 Sprite 为真实填充纹理，圆角由独立 Sliced 背景/遮罩负责；保留特殊形状的填充纹理和方向。不得将没有 Sprite 的普通矩形当作 Filled 进度。

## 验证

历史证据保存于 Library/UIRefactor，原生 XML 按 jobId 单独保留。旧双组件结构的资源检查包含所有 17 个模板；当时通过记录为资源 2/2、基础交互/填充 2/2、组件生命周期 10/10、提示组件 21/21。资源复核1de7c6fc为2/2通过，真实输入与实际填充网格f882abfd为2/2通过；当时五态截图已逐张查看，禁用态无强调边框、按下态有缩小与变深。截图夹具配置独立背景相机，避免无相机场景下的残帧；未执行全量测试。这些记录不证明本轮单 UIState 合并后的结构已通过。

本次模板样式与开关替换已完成直接相关资源、真实输入及 Canvas 预览验收，证据如下。复用 `Tests.Module.UnifiedInputPlayModeTests.NestedMenuRestoresParentAndTouchSuppressesInteractionFeedback` 维护单组件 Selected/On 与交互叠加、禁用及启停恢复；测试结果与视觉结果分别记录，不为普通外观装配另留永久测试文件。

当前定向证据：基础资源最新复验 5/5（247574ad）；CommonButton 新原生 XML 1/1（b635b2c7），五态五张实际截图已查看。滑块开关初次 d1dff24b 为 0/1，临时验证禁用后未重新选择焦点；仅修验证准备后复验 1/1（fe841386）。真实 Canvas 首次预览 1/1（b6df198a）及 Dropdown 浮层生命周期 1/1（1b93e3a0）通过，以 fresh 原生 XML 为准，REST 恢复错误分别记录。首次预览发现的 SwitchRow 高度、两个 ViewTab 蓝底及隐藏 UITab 制作模板已修正；最新模板、SwitchRow 与 Arrow 开合预览 1/1（cd7c66db），主代理已查看全部实际图。PreviewRenderUtility 先前生成的 14 张空白图无效，不作为证据。Hub Cancel 后续真实点击 1/1（9c41ec3d），新版中英三画幅实际切页/取消/返回 1/1（59b4065c），关闭按钮三画幅真实点击 1/1（6a0ba70d），不再保留此前的待修状态；没有运行全量测试。

状态截图使用真实 Canvas 与实际输入，检查默认、悬停、聚焦、按下、禁用；数值断言不能代替截图检查。公共模板验收完成后才能迁移各 Demo。

本次样式装配工具 `FoundationUiStylesMigration.cs` 及 meta、两项临时开关/模板预览用例和 Hub 命中诊断已清理。原有基础交互测试只将按下时的根缩放断言更新为 `Vector3.one`；未新增永久测试或运行时组件。实际截图和原生 XML 保留在 Library，空白离屏预览产物已删除。四 Demo 作者资产最终复核 1978 项哈希未变，仅删除四份已空设置目录的 meta；正式入口场景哈希未变。

## Demo 进度填充

普通线性填充使用 `Sprites/White.png` 与 `UIProgressBar.SetValue/SetColor`；有形状的轨道和圆角遮罩继续使用带 Border 的 Sliced Sprite。设置 Slider 由自身控制 Fill Rect，填充用白色 Simple Image；不要再用长胶囊图拉伸当作内部填充。径向或明确需要纹理形状的 Filled 不按普通矩形处理。

无人机重置进度和公共加载进度使用公共 UIProgressBar。既有序列化 Image 引用保留，初始化时缓存同节点 UIProgressBar。

定向验证：线性填充资源 1/1、无人机 HUD 绑定 1/1。公共 Filled 网格测试覆盖精确 0%、1%、50%、100%。


最终资源契约 59bfe436（4/4）通过：检查全部普通水平/竖直 Filled 的白 Sprite、UIProgressBar 和射线设置，不再按节点名称筛选；同时核查 Demo 页面绑定无外部节点、无缺失脚本以及无人机 HUD 子 Prefab 引用。一次性基础 UI/进度/页面迁移工具及其 meta 已删除，正式组件、保存资源和 Test Runner 是后续维护入口。
