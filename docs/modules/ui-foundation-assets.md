# 基础 UI 预制体维护

基础组件位于 Core.Runtime，模板位于公共 UI/Common。独立页面由 Hotfix View 使用它们，基础组件不持有 Demo 规则。接入前逐项检查字体、Sprite、Border、绑定、布局、遮罩、射线与导航；使用方式见 [基础 UI 组件](../runbooks/use-core-ui-components.md)。

## 逐项检查范围

| 预制体 | 本次检查与修复 |
| --- | --- |
| CommonButton | 新增可直接复用的按钮模板，五态反馈与取消转发 |
| BtnSwitch | 中文 TMP、原 On/Off 对象绑定、五态与业务状态隔离 |
| TabItem | TMP 文案、业务 Selected、独立五态、无重复反馈层 |
| Tab | 模板项和选中对象绑定，移除容器的无效交互 |
| ViewTabVertical | 嵌套 Tab、分页布局、滚动遮罩和绑定 |
| AccordionTab | 根项/叶项 TMP 文案、展开与选中、嵌套交互覆盖 |
| AccordionViewTab | 嵌套折叠项、分页绑定与滚动区域 |
| Dropdown | 默认文案改用 TMP_Text 并重新绑定，选项和箭头保持原结构 |
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

公共设置 `DropdownRow` 的选项容器保持独立不透明背景与纵向自适应高度，覆盖真实选项和间距，避免滚动表单的字段透入浮层。使用同页 popupRoot 时，继续检查完整选项的边界及后续 Modal 的排序。

选项容器锚定控件下边缘、顶部 Pivot，宽度跟随字段，空间足够时从字段下方向下展开；不要让自适应高度向上撑入分类 Tab。组件禁用即时归还，整树启停或销毁延后归还，制作矩形与顺序必须完整恢复。

公共设置与输入另有以下正式基础 Prefab，路径相对于 `Assets/LoadResources/`：

| 预制体 | 维护边界 |
|---|---|
| `UI/Common/Settings/DropdownRow`、`SliderRow`、`SwitchRow`、`ActionRow` | `UISettingRow` 引用原 Dropdown、Slider、Switch、Button；不承担分类切换或保存。滚动表单保持点击区域稳定 |
| `UI/Common/Input/InputPrompt` | 图标/键帽组与独立文案；从实际会话绑定读取按键，设备变化仅刷新绑定表现 |
| `UI/Common/Input/InputPromptButton` | 上述提示的可点击 Variant；复用 InputCommandButton 的离散/保持输入与释放语义 |

四个 Demo 从公共提示创建自己的 Prompt / PromptButton Variant，设置宿主也使用各自 Variant。资源覆盖负责外观，公共组件继续负责行为；卡丁车保留纸艺资源。分类子页、固定表单与循环绑定列表的选择及调用案例统一维护在[基础组件手册](../runbooks/use-core-ui-components.md#从需求找到资源和示例)。

## 行为和生命周期

交互 UIState 只修改自己拥有的背景、文字、反馈层和缩放。Tab/开关业务状态继续由各自组件管理。默认态和禁用态关闭反馈层；Hover 提亮并显示细边，Focused 使用较粗焦点边，Pressed 变深并按原缩放轻微收缩。离开、隐藏和解除禁用后恢复原值。触屏仍抑制悬停/聚焦反馈，但必须表现禁用。

UIState 在 Inspector 验证时清空查找缓存；更改状态定义后不会继续查到旧状态。Dropdown 默认文案及 Accordion 文案使用 TMP，不能只换 Prefab 的文字组件而不修复读取方。

UIProgressBar 包装同节点 Filled Image 的数值和颜色。白色 Sprite 为真实填充纹理，圆角由独立 Sliced 背景/遮罩负责；保留特殊形状的填充纹理和方向。不得将没有 Sprite 的普通矩形当作 Filled 进度。

## 验证

实际证据保存于 Library/UIRefactor，原生 XML 按 jobId 单独保留。资源检查包含所有 17 个模板；已有通过记录为资源 2/2、基础交互/填充 2/2、组件生命周期 10/10、提示组件 21/21。最终资源复核1de7c6fc为2/2通过，真实输入与实际填充网格f882abfd为2/2通过；五态截图已逐张查看，禁用态无强调边框、按下态有缩小与变深。截图夹具配置独立背景相机，避免无相机场景下的残帧；未执行全量测试。

状态截图使用真实 Canvas 与实际输入，检查默认、悬停、聚焦、按下、禁用；数值断言不能代替截图检查。公共模板验收完成后才能迁移各 Demo。

## Demo 进度填充

普通线性填充使用 `Sprites/White.png` 与 `UIProgressBar.SetValue/SetColor`；有形状的轨道和圆角遮罩继续使用带 Border 的 Sliced Sprite。设置 Slider 由自身控制 Fill Rect，填充用白色 Simple Image；不要再用长胶囊图拉伸当作内部填充。径向或明确需要纹理形状的 Filled 不按普通矩形处理。

无人机重置进度和公共加载进度使用公共 UIProgressBar。既有序列化 Image 引用保留，初始化时缓存同节点 UIProgressBar。

定向验证：线性填充资源 1/1、无人机 HUD 绑定 1/1。公共 Filled 网格测试覆盖精确 0%、1%、50%、100%。


最终资源契约 59bfe436（4/4）通过：检查全部普通水平/竖直 Filled 的白 Sprite、UIProgressBar 和射线设置，不再按节点名称筛选；同时核查 Demo 页面绑定无外部节点、无缺失脚本以及无人机 HUD 子 Prefab 引用。一次性基础 UI/进度/页面迁移工具及其 meta 已删除，正式组件、保存资源和 Test Runner 是后续维护入口。
