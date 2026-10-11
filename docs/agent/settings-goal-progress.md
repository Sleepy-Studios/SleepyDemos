# 公共设置与表现 Goal 实施记录

目标：按用户确认的 G0–G11 顺序实施公共设置、表现 API、全部 Demo 接入、功能验收、分页美术确认和正式 UI。七张分页/体验效果图已基本确认；Hub 使用暖白/朱红瑞士方向，各 Demo 保留自身美术风格，正在正式装配与验收。

| Goal | 当前状态 | 交付与剩余 |
|---|---|---|
| G0 | 已建立基线 | 保护原 ProjectSettings.asset 改动，明确 Core/Hotfix/Demo 边界与文档规则 |
| G1 | Editor功能验收通过 | 独立快照、SO、统一偏好、启动/直启、并发初始化、迁移、保存失败及事务回滚通过 |
| G2 | 公共API及Hub重建通过 | 中英文 FontId、一次性文本 API、真实 Hub 销毁重建/失败恢复通过；正式语言设置页与长译文布局属G10 |
| G3 | 功能及既有宿主回归通过 | 公共参数、改键、最近实际设备提示；Kenney 键鼠和三类手柄资源接入，正式提示Prefab属G10，真实硬件属G11 |
| G4 | Editor功能及GPU验收通过 | 管线副本、显示确认、手动FXAA/MSAA、DLSS/DLAA接管和关闭恢复通过；Player显示验证待完成 |
| G5 | 功能及既有宿主接入通过 | 分类总线/DSP增益、静音与辅助限制；普通音源及通知隔离回归通过，正式声音页属G10 |
| G6 | 七类API实现，代表效果图像验证通过 | 基础/可更新临时作用域、允许限制与亮度；正式场景 GPU 回归覆盖代表效果，不等同七类各自验图 |
| G7 | 功能及既有宿主回归通过 | 公共玩法时钟、倍率/冻结租约、曲线制作配置；Drone/Wall/Kart直接相关回归通过 |
| G8 | 公共API及GPU验收通过 | 七类黑幕/四方向擦幕形状、队列与取消、黑边/HUD、冻结中英Title通过；正式表现View属G10 |
| G9 | Editor功能阶段通过 | DLSS、Drone、Wall、Kart和Hub接线；Showcase保持独立公共菜单宿主。正式分页、演出入口及视觉装配在G10后再验收 |
| G10 | 正式装配及定向交互通过 | 五独立分页、五宿主Variant、输入提示和效果体验已装配；Hub英文五页常规/窄屏/超宽已查看，各Demo和体验页的完整画幅视觉仍待验 |
| G11 | 尚未完成 | 最终编译/测试/视觉/Player/硬件与工具清理 |

## 已取得证据

- Unity Editor 6000.3.15f1 编译已多次通过；后续代码变更仍须重新验证。
- GameSettingsSnapshotTests 3/3；InputRebindSessionTests 2/2；SettingsRuntimeTests 原3/3；PostProcessRuntimeTests 2/2；CinematicPresetTests 2/2通过。
- LanguageServiceTests 清理共享字体 atlas/material 后 2/2 通过。
- GameSettingsServiceTests 并发初始化改为共享 UniTaskCompletionSource；15 秒显示确认的计时器配对释放后，4/4 通过，覆盖保存隔离、损坏/未知语言回退、迁移、取消、确认超时与重复提交，以及实际改键/无变化绑定/按住门闩和参数更新。
- 管线副本保护 1/1、普通音源增益 1/1、电影队列/HUD恢复/黑边正常退出 3/3 通过（db0ae610）。新增显示/音频通知异常隔离、不可用 DLSS 保持原生 MSAA 的直接回归待执行。
- Hub语言重建/重开失败恢复 1/1 通过；Kenney实际键盘Prompt/改键图标 1/1 通过。
- 正式启动/DLSS世界后处理GPU图像回归 1/1 通过，覆盖预设、Bloom/色差允许限制、释放恢复、亮度和Overlay UI颜色。截图位于 Library/Settings/evidence/post-process。
- 追加“畸”到公共CN字符集并以原54字号/8 padding/4096图集更新原资产；公共词表/字体/参数制作检查通过，无本次Console Error。
- GameTimeServiceTests 的原生取消检查已修正并通过；固定步断言允许 Unity 写回的浮点量化误差（1e-7），该精确方法复验1/1通过（19153e90），其余3项已通过。
- Drone 输入原6/7通过；帮助页原断言仅连接手柄即切提示，与最近实际操作规则冲突，改为真实按肩键后切提示，精确复验1/1通过（b6afadaa）。Wall冻结精确检查1/1通过（3ad1931d），补齐Esc暂停后的新检查待执行。
- 指针保持检查原生 TestResults.xml 显示1/1通过，REST回调因状态文件占用报重连失败（c127a9bb）；保留原生XML并区分基础设施错误与测试结果。
- 显示/音频订阅异常隔离各1/1通过（e2393278、21fc513c）；公共字体3/3通过（39fb0344）。Wall键盘Esc/菜单单次提交原生XML1/1通过（5446fa5f），该次REST有相同重连错误。
- 赛车输入原8/9通过，旧断言锁定专属0.18死区，已改验证公共死区和Dpad优先，精确复验1/1通过（6b5833fe）；DLSS Editor直启1/1通过（b04e647a）。
- DLSS弹窗阻断/关闭恢复/返回Hub精确方法1/1通过（e05c4762），UI相机栈重绑1/1通过（f3f9f57c）。
- 上述为直接相关范围，没有运行项目全量测试，也没有把模拟设备当作真实硬件验收。

## 交付边界与提交

用户授权由代理决定分阶段本地提交，不推送。只提交本任务代码、制作资源、测试及文档；保留原有未提交工作。正式UI未确认前不装配。一次性Builder完成资源生产后删除源码/meta；持续配置检查工具保留。

### G10 装配与基础组件复用补齐（2026-10-12）

- 用户已基本确认七图，并要求各 Demo 使用自己的真正 Prefab Variant，保留卡丁车纸艺设计。五类分页各自有 View/Prefab/生命周期，通过现有 UITab/ViewTab 切换；仅绑定行用 LoopScrollView/ItemView，固定异构参数使用普通 ScrollRect。
- 已补齐基础组件手册的需求选型、真实 Prefab 与调用案例，以及 AGENTS/CLAUDE 的复用规则。本地提交 `dd72d77e`，没有推送。
- Hub、四个 Demo 的设置宿主 Variant、独立显示确认页、公共输入提示与各 Demo 提示 Variant、效果体验页均已制作。Hub 效果入口和 Drone/Wall 公共画质入口已保存接线。
- 公共中文字体按完整词表更新，保留原 GUID、材质、54 字号、8 padding 与 4096 图集。SettingsRowTests 1/1（0b90f7fd）、InputRebindSessionTests 2/2（4bd18656）、LanguageServiceTests 2/2（2f451419）、SharedFontAssetTests 3/3（61c179f7）通过；没有运行全量测试。
- 正式分页回归定位到 ScrollRect 内 Button 根节点按下缩放后抬手命中视口、点击丢失，已保持设置行点击区域稳定并用五态颜色表达交互。完整五页生命周期、候选隔离、分类默认、取消不保存及重复应用单次提交复验通过（a0a083a3、6e2f9359）。实际截图位于 `Library/GraphicsSettings/Accessibility.png`。
- 语言提交期间的用户切页拦截曾挡住重建页初始化，已区分同步候选回显与用户选择；Hub 焦点优先已有业务选择，避免关闭设置后重置到第一项。真实语言页中英往返、旧 View 全部销毁、字体/材质、选中玩法及有效焦点通过（5a8b9cd8）。正式画质弹窗开关与无悬浮入口通过（f2a3c05c），截图位于 `Library/GraphicsSettings/Popup.png`。
- 普通滚动焦点显露/指针滚动保持（0e0014da）、用途通配绑定中键鼠/Switch/无手柄预览（ad4b08b3）、精确别名绑定及回收复用（8b4a5bfd）各 1/1 通过。本轮字体阶段本地提交 `351cfcff`，没有推送。仍待 Demo 新资源、列表次按钮身份、体验页与宽窄/英文视觉和 Player/硬件验证，不能视为 G10/G11 全部通过。
- 测试前发现 AppEntrance 被标记为未保存，保存副本与磁盘原场景比较完全一致后安全重载，没有覆盖场景制作内容。临时生产工具与副本待验证后清理。
- 循环条目内的全部 LoopScrollMenuButton 共用当前 Cell 身份，首按钮仍承担列表主导航、次按钮保留原生横向导航。次按钮回收/禁用后旧按下不复活（67701395）及原 Grid 导航（0a145aff）各 1/1 通过。
- Drone 六处遥测提示已使用非点击正式 Variant 和实际会话绑定，保留原状态、数值与面板。英文 FontId/材质、Kenney C→Y 改键和物理/触屏/手柄切换 1/1 通过（6ad61184）；Camera 模式/FOV/角度回归 1/1 通过（738391e7），制作配置与字形菜单检查通过。
- 正式体验入口、效果单行开启/停止的代表流程、倍率作用域、冻结下电影播放/取消和退出恢复 1/1 通过（afece5f5）。回归发现返回 Hub 仅隐藏体验页而未归还世界，已复用 UIManager.ReplaceAsync 销毁体验页；退出不释放外部冻结。实际截图位于 Library/GraphicsSettings/Lab-Post.png、Lab-Time.png、Lab-Cinematic.png，已查看；Tab 交互态仍覆盖业务选中颜色，正在修正制作配置，尚不视为视觉验收完成。
- DLSS 公共弹窗阻断/关闭恢复/返回 Hub（7c480ba6）、Kart 专属设置与公共弹窗往返且保留本地暂停（e6a55472）、Wall Esc 单次暂停（0ca0258d）和场景/暂停/结果往返（14a8143f）各 1/1 通过。Drone 帮助菜单焦点/返回/设备提示（dd11f9df）通过；独立触控 HUD fixture 缺少正式语言初始化（c05b2c0a），正在补齐共同 fixture 后复验，不能将其失败隐藏为通过。
- 新增操作词条与动态参数后，制作配置/字形检查通过；Prefab 根节点规范 1/1（70c24497）、配置格式与正式资产检查 8/8（c63c16ec）通过。Drone 遥测与参数副本/清除/FontId（378be7fb）、帮助菜单（d0d938b4）、补齐正式语言初始化后的触控释放（6dac738e）、选择页三端切换与保持提交（0fd4580b）均 1/1 通过；Kart 教程与操作说明词条接入（c672cae9）及专属/公共设置往返（9a178ea5）各 1/1 通过。
- 体验页选中态与交互装饰已分离，按钮五态、朱红播放与底部提示对比度已制作修正；正式播放/取消/退出复验 1/1（6273d8c1），更新后截图已查看。DroneTouch.png 来自没有清屏相机的独立 HUD fixture，不能作为完整场景触控视觉验收证据；未据残影修改作者布局。
- 正式显示确认测试发现 UIDropdown 在 ScrollRect 内被后续行遮挡，已复用该组件增加同页 popupRoot 挂载、容器边界约束与完整布局归还，设置和体验页接入。原异步收起初始化（f790b3dd）及浮层射线/后续 Modal/busy/禁用/销毁回归（31efb0b5）各 1/1 通过；正式显示页真实保留/恢复、冻结下十五秒超时与偏好回滚 1/1（bc2013c7），截图 DisplayConfirm.png 已查看。
- 一次 Wall 复验在开始阶段遇到 Test Runner 恢复已删除 InitTestScene 的基础设施错误（9306c3f6），没有执行测试方法，不计通过。原生 XML 已保存在 Library/GlobalSettings/test-results；取消保存对话框、保存临时测试场景副本并恢复未改动的 AppEntrance 后继续验收，未覆盖正式场景。
- Kart 旧 Current footer 已从重复漂移改为固定 Native UI Cancel，保留纸艺节点/布局/字段；实际动作与 Input.Back 文案及专属/公共设置往返复验 1/1（c673c2c8）。Wall 暂停菜单打开公共画质、关闭后仍保持暂停与原菜单、场景往返 1/1（1349f377）；下拉接入后的 Hub 五页生命周期/候选隔离/单次提交复验 1/1（9b0f08b9）。上述测试串行执行，没有全量测试；正式场景未改动。
- 正式装配阶段本地提交 `03c7729b`，循环条目身份修复提交 `e516ba31`，均未推送。收尾发现退出场景时 Streamline Driver 先销毁、设置服务再关闭会重建运行对象；已修复 `ApplyModeAsync(null)` 不初始化 Driver。Unity 编译通过（107eea3e）；新清理顺序回归（779ce4f9）、设置初始化与保存隔离（f84574d8）、手动抗锯齿与不支持 DLSS 恢复（3116d1ec）、真实显示确认与冻结超时（889ac1fe）各 1/1 通过，全部退出后的 Console Error 为 0。没有运行全量测试。
- 基础组件正式设置行及输入提示资源登记提交 `f414f145`，Streamline 退出修复提交 `189f28fa`，均未推送。英文五页已在正式启动的 Game View 中逐页查看 1920×1080、1024×768、3440×1440；分类长译文曾与图标重叠，根因是模板 Desc 的 ContentSizeFitter 将文字区域撑宽，已在制作资源停用该尺寸驱动，使用 TMP 原生自动字号及制作上下限。窄屏与超宽布局重开后已实际查看，中文语言页重建与恢复也已观察；未据此声称全部 Demo 和体验页完成画幅验收。
- Windows Player 前置已通过正式 Hotfix Build 窗口完成 HybridCLR 生成及 DLL/AOT 同步，YooAsset 正式首包 `2026-10-12-259` 构建成功。Player 构建 `6ff9de9b` 在 C++ 编译失败：用户级 CMD AutoRun 为 `chcp 65001 >nul`，Bee 子进程受限 PATH 中没有 System32，导致命令找不到。全机命令及工具链存在；修复该用户级 Windows 配置超出只修改本项目的范围，已提出精确备份及绝对路径方案，等待用户授权。不能将失败构建计为 Player 通过；最新分类 Prefab 修改仍需重新打首包。前置窗口域重载曾有一次既有 IMGUI 布局错误，资源构建成功后无同类错误。
- 正式改键页面回归发现取消按钮点击被原生重绑定吞掉并写成鼠标左键。公共监听保留 UI 事件，在匹配结束命中可交互 Selectable 时归还原候选；输入页复用 UIMenuScope 暂停 Submit/Navigate，完成或取消后复用原 MenuInputScope 的中立门闩恢复，不排除合法 Enter／手柄确认键。短点击/长按取消、面板空白区域鼠标绑定、Enter 保持与松键、候选/存档/制作资产隔离、正式会话生效及回收次按钮身份回归 1/1 通过（85ffd712）；清理后再次 1/1 通过（c0b8b105）。最初测试准备曾抢在 Hub 下一帧恢复入口前点击，已等待真实入口；点击 Modal 背景本身是取消操作，鼠标绑定正例使用面板空白区域。
- 已核对并删除无外部调用及制作引用的旧 DlssSettingsView 源码/Prefab/meta，保留仍由 FluxService 注册的 GraphicsSettings Data/Handler/Action。四个新一次性装配 Builder 源码/meta 已清理，Hotfix.Editor 临时 LoopScroll 引用已归还；源备份保存在 Library。清理后编译通过（14be30f7）。随后删除五个已空目录的操作被自动审批以“blocked by policy”拒绝，未提供具体原因，未重试或换工具；先前被拒的两个 Global Builder 也仍待清理。此前的 DLL/AOT 与首包产物需在最新源码下重新生成，当前不作为最终 Player 交付。
- 新导航抑制入口的直接回归：既有嵌套菜单返回与触控五态 1/1（ad421acb）、最近菜单作用域取消转交 1/1（8e7e8363）通过。没有运行全量测试；宽窄屏证据已从 Assets/Screenshots 迁至 `Library/GlobalSettings/evidence/settings-layout`，不将验收截图作为生产资源提交。

第一阶段提交范围：公共能力、制作资产、宿主调用、直接相关测试和文档；不代表 G9/G10/G11 已全部完成。临时 GlobalSettingsAssetsBuilder 与 GlobalInputGlyphsBuilder 已完成生产，但删除操作被自动审批以“blocked by policy”拦截（未提供具体原因），两者及目录/meta 均排除提交，清理暂未完成。

本地提交：`9325eaaf feat(core): 添加公共设置与表现能力并接入现有宿主`。没有推送，原 ProjectSettings.asset 保留未提交。随后继续公共 Loading/Hub 错误文案及 Wall 公共字体迁移；正式多键帽提示 Prefab、Wall底部与Drone帮助/选择提示的布局归G10。

追加要求（2026-10-11）：DLSS关闭时可手动选FXAA/MSAA，DLSS/DLAA实际生效时接管，关闭后恢复手动选择。MSAA 2/4/8及真实生效状态已实现；正式控件属G10，当前通过公共API验证。

公共Loading/Hub反馈中英与字体实际页面检查1/1通过（02475673），失败进入后的真实菜单恢复与重试1/1通过（69e98639）。无回调/有回调重载各通过（bc524c53原生XML 2/2，REST完成回调丢失）；制作配置检查8/8通过（b5b5a641）。Wall字体17处迁移及布局/样式/GUID回读通过，公共“援、撤、缝”字形覆盖检查通过，旧Demo字体无生产引用但待清理。

后续本地提交：`052aef20 feat(locale): 接入公共加载文案并迁移Wall公共字体`，没有推送。

AA编译通过（dcc324f5、c99dc362）。新增快照枚举/样本校验、无相机保存偏好、纯FXAA不激活作者后处理各1/1通过（4649e4ec、738a2f57、7de4ac50）；原显示管线/不可用DLSS回归2/2通过（ffb83290）。刷新测试发现后，新增切镜用例发现清理作用域会再次打开旧相机后处理，已修正清理/恢复顺序并复测1/1通过（25421a47）。真实Direct3D12 GPU测试验证FXAA及MSAA2各自经过DLSS Quality、DLAA、关闭恢复，世界/UI像素与深度正确，1/1通过（73cfe315）；后处理图像直接回归1/1通过（9486f9c6）。这些REST结果均正常完成；没有运行全量测试。

G9收尾核对：Showcase保持独立包示例及既有公共菜单宿主，不加入正式Hub/YooAsset启动链。完整分页、真实语言页重开、独立显示确认页、提示子Prefab和正式演出View均在G10确认效果图后装配，不把旧DLSS弹窗或测试reopen委托视为完整设置UI交付。

追加收尾编译通过（336fba8d）。Hub关闭页面后真正进入SaveData大小边界失败，旧偏好/语言/选中及可见页面恢复1/1通过（94da7a12）；语言重建/待进入Demo相互拒绝且不破坏原事务1/1通过（8686bed5）。冻结下正式中英字幕按词条FontId与材质赋值、正常结束/取消/非法调用清理1/1通过（ecbb5034）；七幕及Wipe四方向实际UGUI/RenderTexture覆盖端点与形状1/1通过（4f0cc511），PNG位于Library/Settings/evidence/cinematic/Direct3D12，已查看Iris和Pixel实际图像。Console无Error，没有全量测试、Player或真实硬件验收。

抗锯齿阶段本地提交：`43add84d feat(core): 支持手动FXAA与MSAA及DLSS抗锯齿接管`，没有推送。原ProjectSettings改动及Unity自动序列化的QualitySettings格式升级均不混入本任务提交。

收尾验收本地提交：`6a8fbfbb test(settings): 补齐Hub语言事务和电影演出验收`，没有推送。

G10首张[显示与画质效果图](art/settings/graphics-settings-v1.png)已用内置imagegen生成，沿用已确认整体方向，展示DLSS关闭时手动MSAA4、选择/实际状态和接管说明；[生成提示词](art/settings/graphics-settings-v1.prompt.txt)一并保存。该图是美术预览，效果图中的键帽仅表示布局和风格；正式输入提示使用已导入Kenney资源。用户随后已基本确认全部七图并要求保留各 Demo 风格，当前实施状态见上方 G10 装配记录。

G10其余六张已生成并保存，完整[美术确认索引](art/settings/README.md)记录各图及提示词。已按实际Kenney资源修正控制页X键以及公共提示图的PlayStation菜单键/Backspace。出图阶段只增加美术预览和文档；用户确认后已进入正式 Prefab 装配和有效绑定、交互验收，不能把预览图视为运行结果。
