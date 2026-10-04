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

## 行为和生命周期

交互 UIState 只修改自己拥有的背景、文字、反馈层和缩放。Tab/开关业务状态继续由各自组件管理。默认态和禁用态关闭反馈层；Hover 提亮并显示细边，Focused 使用较粗焦点边，Pressed 变深并按原缩放轻微收缩。离开、隐藏和解除禁用后恢复原值。触屏仍抑制悬停/聚焦反馈，但必须表现禁用。

UIState 在 Inspector 验证时清空查找缓存；更改状态定义后不会继续查到旧状态。Dropdown 默认文案及 Accordion 文案使用 TMP，不能只换 Prefab 的文字组件而不修复读取方。

UIProgressBar 包装同节点 Filled Image 的数值和颜色。白色 Sprite 为真实填充纹理，圆角由独立 Sliced 背景/遮罩负责；保留特殊形状的填充纹理和方向。不得将没有 Sprite 的普通矩形当作 Filled 进度。

## 验证

实际证据保存于 Library/UIRefactor，原生 XML 按 jobId 单独保留。资源检查包含所有 17 个模板；已有通过记录为资源 2/2、基础交互/填充 2/2、组件生命周期 10/10、提示组件 21/21。最终资源复核1de7c6fc为2/2通过，真实输入与实际填充网格f882abfd为2/2通过；五态截图已逐张查看，禁用态无强调边框、按下态有缩小与变深。截图夹具配置独立背景相机，避免无相机场景下的残帧；未执行全量测试。

状态截图使用真实 Canvas 与实际输入，检查默认、悬停、聚焦、按下、禁用；数值断言不能代替截图检查。公共模板验收完成后才能迁移各 Demo。

## Demo 进度填充

普通线性填充使用 `Sprites/White.png` 与 `UIProgressBar.SetValue/SetColor`；有形状的轨道和圆角遮罩继续使用带 Border 的 Sliced Sprite。设置 Slider 由自身控制 Fill Rect，填充用白色 Simple Image；不要再用长胶囊图拉伸当作内部填充。径向或明确需要纹理形状的 Filled 不按普通矩形处理。

已迁移搬豆工进度、渔力全开张力与 Boss 两条进度、无人机重置进度和公共加载进度。既有序列化 Image 引用保留，初始化时缓存同节点 UIProgressBar；新建渔力全开资源也按该约定装配。赌场设置 Slider 的白色填充保持自身尺寸驱动。

定向验证：线性填充资源 1/1、无人机 HUD 绑定 1/1、渔力全开蜘蛛蟹流程 1/1。搬豆工在真实交付比例、暂停、重开与长屏中验收；手动虚拟时钟回放期间只暂停自动模拟，不能禁用 Controller（OnDisable 会撤销就绪状态）。同刻交付成批处理，四分之一与半程截图按实际交付数核对。公共 Filled 网格测试覆盖精确 0%、1%、50%、100%。
搬豆工最终1af30b07为1/1通过；已查看25%、50%、100%及长屏截图，填充无拉伸，圆角裁剪正确。未执行全量测试。
