# 公共设置与表现 Goal 实施记录

目标：按用户确认的 G0–G11 顺序实施公共设置、表现 API、全部 Demo 接入、功能验收、分页美术确认和正式 UI。七张分页/体验效果图已基本确认并装配；Hub 使用暖白/朱红瑞士方向，各 Demo 保留自身美术风格，正在最终验收。

| Goal | 当前状态 | 交付与剩余 |
|---|---|---|
| G0 | 已建立基线 | 保护原 ProjectSettings.asset 改动，明确 Core/Hotfix/Demo 边界与文档规则 |
| G1 | Editor功能验收通过 | 独立快照、SO、统一偏好、启动/直启、并发初始化、迁移、保存失败及事务回滚通过 |
| G2 | 公共API及Hub重建通过 | 中英文 FontId、一次性文本 API、正式语言页和真实 Hub 销毁重建/失败恢复通过；英文五页三画幅已查看 |
| G3 | 功能及正式宿主回归通过 | 公共参数、改键、最近实际设备提示；Kenney 键鼠/三类手柄资源与正式提示及Demo Variants接入，真实硬件记录待验 |
| G4 | Editor功能及GPU验收通过 | 管线副本、显示确认、手动FXAA/MSAA、DLSS/DLAA接管和关闭恢复通过；Player显示验证待完成 |
| G5 | 功能及正式宿主接入通过 | 分类总线/DSP增益、静音与辅助限制；普通音源及通知隔离回归、正式声音页候选与应用通过 |
| G6 | 七类API及独立Editor图像验证通过 | 基础/可更新临时作用域、允许限制与亮度；Direct3D12 正式场景验证七类各自变化、UI 隔离及释放恢复，其它平台仍未验 |
| G7 | 功能及既有宿主回归通过 | 公共玩法时钟、倍率/冻结租约、曲线制作配置；Drone/Wall/Kart直接相关回归通过 |
| G8 | 公共API及GPU验收通过 | 七类黑幕/四方向擦幕形状、队列与取消、黑边/HUD、冻结中英Title及同根注册复用通过；正式表现View已装配 |
| G9 | Hub入口范围收缩，定向复验中 | DLSS、Drone、Wall、Kart继续消费公共API；公共设置只在Hub打开，移除四Demo公共入口与弹窗。Showcase保持独立公共菜单宿主 |
| G10 | Hub主View与五独立分页复验中 | 主View直接使用ViewTab按需加载独立View，删除SettingsPage/Screen/Base；单UIState默认五态。先前画幅证据不代表新结构已验收 |
| G11 | 尚未完成 | 最终编译/测试/视觉/Player/硬件与工具清理 |

## 已取得证据

### 当前 UI 范围覆盖原计划（2026-10-12）

- 用户明确公共设置只在 Hub 修改，各 Demo 暂不需要公共设置界面。Demo 继续读取公共偏好和 API，保留自己的玩法参数、触控区域及美术风格；卡丁车专属设置不删除。
- `HubSettingsView` 直接持有候选和应用事务，使用既有 `ViewTab` 按需加载五个独立分页 View/Prefab。删除 `SettingsPage`、各具体 Page、`SettingsScreen` 和 `SettingsViewBase`；无需宿主路由、页面地址重写或额外交互管理器。
- `UIState` 自动处理同节点 Selectable 的 Normal/Hover/Focused/Pressed/Disabled，额外业务状态仍在同一个组件追加；删除 `UIStateInteraction`，正式资源原生合并并保留目标、样式和 GUID。
- 原生删除四 Demo 公共按钮并重新生成 UIBind；DLSS 另删除设置动作、绑定、提示和输入目录条目。五 Hub 分页恢复标准生成 Address。一次性 `HubOnlySettingsAssetsMigration` 已清理。
- 新建的 `Assets/LoadResources/Demos/{dlss,drone_flight,kagit_kart,wall_squeeze}/UI` 中各四个公共分页 Variant 均已无生产引用。删除命令曾被自动审批以 `blocked by policy` 拒绝，未提供具体原因，没有换工具绕过。用户清理后，已重新核对四目录及对应 `UI.meta` 均不存在；这些未跟踪资产不纳入提交，Demo 公共分页残留已清理。
- 原阶段结果保留为历史证据；当前结构的编译和定向复验在本节追加，不将历史五宿主测试作为 Hub-only 新结构通过。
- 新结构 Unity 编译零错误（30b32fbe，后续诊断修改编译也零错误）。刷新原生测试发现后，基础资源实际 5/5（4aeb0e6e）、设置行 3/3（bf9bfc24）通过；真实按钮五态 1/1（8593490d 原生 XML，REST 重载恢复失败）、嵌套菜单/业务态恢复 1/1（71f58f8a）、动态循环列表控件 1/1（9ba7ad85）通过。五态实际截图已查看，位于 `Library/UIRefactor/Evidence`。
- Hub 独立分页/候选用例尚未通过：五页懒加载、切换、候选编辑及默认值流程通过后，取消按钮命中断言失败（4557ee6d，补诊断复跑结果相同）。按钮正常启用、未裁剪、射线已注册且层级为 Pop/200；投影中心为屏幕 (905.07,150.40)，`RectangleContainsScreenPoint` 却返回 false，最高命中为 Paper。尚不能确定是坐标换算还是实际射线问题，不删除点击断言或改用直接 onClick 规避。取消按钮 Pivot 实验无效，已原生恢复原布局；详细诊断保存于 Library 的对应测试报告，临时扩展日志已简化。
- 阶段提交只作为当前重构检查点。Hub 其它精确方法与四 Demo 最新回归因上述用例失败尚未继续执行；当前完整 UI 交互、画幅、Windows Player、真实硬件及被拒绝的目录清理仍未完成。未运行全量测试。
- 本次检查点最初被已有 `.git/index.lock` 阻止。清理命令也被自动审批拒绝，没有换工具绕过；用户清理后核对锁已不存在，重新暂存成功，按授权保存本地阶段提交，不推送。任务路径列表在 `Library/GlobalSettings/ui-cleanup/stage-paths.txt`，排除旧 DLL/AOT、ProjectSettings、字体噪音和历史临时工具。最后简化诊断后 Unity 编译通过（0ca65019，零错误）。

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

本轮基础组件复用与定向收尾（2026-10-12）：

- 公共基础组件手册补齐正式调用示例及维护职责，既有 AGENTS.md / CLAUDE.md 已链接该入口。明确 UITab / ViewTab 独立分页、固定异构表单使用 ScrollRect、可变同类改键条目使用循环列表，Demo 外观通过自己的 Prefab Variant 保留。文档本地提交 `63001704`，没有推送。
- 子代理输入审查补齐 DLSS 底部与触控提示、Drone 操作提示、Kart 操作及触控按钮、Wall 普通触控说明的一次性词条赋值；公共 SO 新增十八项词条。四个直接 PlayMode 用例分别 1/1 通过（2d33fb4f、c67718db、78dcdea4、d99fe76d），不将专属业务文案视为全部翻译。没有修改 Demo 制作布局或纸艺资源。
- 公共中文原字体补入左箭头，保留 GUID 与 54 / 8 / 4096 生成参数；英文源字体不提供该字形，复用公共中文作为 TMP 原生 fallback，中文不反向引用英文。英文原 atlas 和字形坐标保持原状，原生序列化对字形表排序产生较大文本 diff。维护预设对齐原资产，外部字符集编辑后须先导入再生成。刷新发现缓存后，公共字体检查实际 4/4 通过（9b1220bc）。
- 子代理显示/效果审查修复动态景深在 AddRenderPasses 阶段提出本帧深度需求，以及同一 DLSS 请求失败回原生后仍可编辑 FXAA / MSAA。两个精确 EditMode 用例各 1/1 通过（bf33ee06、bc8667ac）。未改作者相机深度偏好和共享管线资产。
- 动态景深图像用例最初两次失败（db1f6122、3f860d2a）分别源于图外观察已清除的全局绑定、误将 URP 预先声明的纹理大小视作深度需求。按真实 RenderGraph 时序修正测试前提，保留本帧需求、实际输入、像素变化、UI 清晰及制作配置恢复断言。最终真实 Direct3D12 回归 1/1 通过（9cec1ff8），最新 09DynamicDepthOfField / 10DepthOfFieldReleased 图片已实际查看；测试内观察器在结束时移除并销毁。
- 最新源码经 Unity Editor 编译通过（71c6a18f，无编译错误）。没有运行全量测试，没有将定向图像验收视为七类效果各自全部验图；Demo / 体验页完整画幅、Windows Player 和真实硬件仍待。旧首包 / DLL 产物不作为当前源码交付，原 ProjectSettings 与质量配置格式升级继续排除提交。

本地提交：`9325eaaf feat(core): 添加公共设置与表现能力并接入现有宿主`。没有推送，原 ProjectSettings.asset 保留未提交。随后继续公共 Loading/Hub 错误文案及 Wall 公共字体迁移；正式多键帽提示 Prefab、Wall底部与Drone帮助/选择提示的布局归G10。

G11 基础组件与独立效果补验（2026-10-12）：

- 两名子代理分别审查输入提示与后处理，主代理串行操作 Unity、修改公共设置行资源并验收。Kenney 抽查的键鼠、Xbox、PS、Switch 原图与下载包同系列 PNG 的 SHA256 相同；提示 Variant 继续引用公共基底，Showcase 继续使用已有公共菜单宿主。
- DLSS 关闭提示偏好时曾只收起键帽，现同时收起非交互说明，保留触控命令。初次复验 173186fb 捕获查找时跳过禁用触控父级的问题；Drone 也有同型写法，统一包含禁用父级。DLSS 精确复验 1/1（2aa484d0）通过。
- Drone 既有触控用例加入“键鼠关闭公共提示 → 切到 Touch”的实际偏好路径，保留双指、旧指针丢弃和隐藏释放断言。c5eecd85 因 async 默认 Update 检查早于玩法 Update 而未通过，恢复旧协程的检查时点后精确复验 1/1（b68e67aa）通过；快照及原存档在 finally 归还。不把直接组件事件当作真实硬件验收。
- 中文体验页的画幅检查发现下拉间距透出后方字段、切分类沿用旧滚动位置。公共 DropdownRow 增加原生 Image / ContentSizeFitter 覆盖全部选项及间距，体验页切页停止惯性并回到首项，不新增浮层框架。新增归位代码最初漏掉初始化时的禁用父级，入口回归 b7029fe1 未通过；补齐查询后，真实入口、滚动归位、动态选项覆盖、冻结播放/取消和退出恢复精确回归 1/1（2a76028c）通过。
- 七类后处理现在分别取得实际像素证据：原独立景深段保留，正式 Lab 调色、泛光、色差、暗角、畸变和颗粒分别只启用自身效果，不修改制作预设原值。颗粒验证三帧变化及邻像素纹理，静态项验证复拍稳定；各项均验证 UI 清晰、释放恢复及制作 Profile 不变。Direct3D12 精确 GPU 用例 1/1（600da858）通过，六项最新 Applied 图已实际查看，图片与对应基线/恢复帧在 `Library/Settings/evidence/post-process/Direct3D12/Lab*.png`。
- 最新 Unity 编译无错误（ec76da4c），上述定向复验结束后 Console Error 为 0。制作时使用临时 Prefab 实例并归还场景；AppEntrance 磁盘 SHA256 前后相同，未新增 Builder。同步基础资源、组件操作手册和输入文档；源码/文档格式检查通过，原生序列化的空组件名称行仍按 Unity 格式保存。
- 没有运行全量测试；Demo 和体验页完整中英画幅、原生模拟触控命中、Windows Player 与真实硬件仍需补验。用户级 chcp 修复仍待明确授权，旧 DLL/AOT 与首包不作为最新交付；被自动审批拒绝的历史临时工具未换方式重试删除。

G11 公共浮层与退出生命周期补验（2026-10-12）：

- 正式 Game View 发现浮层背景增长时向上覆盖分类，现通过原生 Prefab 操作将公共 DropdownRow 锚定字段下边缘、顶部 Pivot，并使选项宽度跟随字段；没有增加 Builder 或手工编辑 YAML。
- 展开浮层后直接退出 Play Mode 曾出现失效页面更新，以及祖先停用期间 SetParent / SetSiblingIndex 错误。公共 View 在等待更新后检查实例存活；UIDropdown 组件单独禁用仍即时归还，祖先启停或销毁先收起再延后归还，原父级销毁时清理借出的选项。
- Unity 编译通过（2c240d3d，零错误）。既有页面更新用例补直接销毁路径，1/1（14b65301）通过；既有浮层用例补祖先启停、同帧启停和整个宿主销毁，1/1（4d8e4857）通过。入口用例 b40f9bff 遇到 UnitySkills batch_state.json 占用导致回调重连失败并中断，未取得新原生 XML，不计通过；正在精确重跑该入口。
- 入口精确重跑 1/1（c822b046）通过，包含向下展开、背景覆盖、滚动归位与原演出断言。正式启动的中文 1920×1080 已实际观察完整七项向下展开且不覆盖分类；本轮没有据此声称窄屏和实际直接停止 Play 的路径通过。交互期间观察到其他窗口及玩法参数正在被操作，已询问 UI 接管时机，避免同时操作鼠标。
- 用户明确先完成电脑端 Editor 验收，真实手柄和触屏在项目中记录待验；Windows CMD AutoRun 由用户自行修复，本任务不修改用户级 Windows 配置。Player 构建仍未通过，不能把“自行修复”视作已修复或验收。

G11 电脑端画幅与注册复用收尾（2026-10-12）：

- 用户确认 UI 接管后，正式中文体验页在 1024×768 查看新版七项浮层，向下展开、宽度与背景正确且不覆盖分类。保持浮层展开直接停止 Play Mode，随后 Console Error 为零，AppEntrance 未标脏；不是用 Test Runner 清日志代替退出观察。
- Hub 正式语言页从中文提交英文并重建；英文体验页在 1024×768 的电影、后处理顶部/末尾及时间三页已实际查看，长词换行和底部操作未重叠。1920×1080、3440×1440 的完整英文体验页及各 Demo 矩阵仍待，不将未成功切换的画幅计为通过。
- 两名子代理只读核对当前契约及测试范围，发现同一 CinematicPresenter 同帧注销并重新注册后，旧任务延后的 finally 可清除新遮幕/字幕。复用已有注册令牌区分清理所有权；注销仍同步恢复旧效果，普通取消继续清理本次演出，不新增公开 API 或队列框架。现有转场队列与中英字幕用例补复用路径，保留原取消、失败、字体和恢复断言。
- 退出 Play 后 Unity 自动编译完成（2026-10-11T23:40:43Z，零错误），随后串行运行两项精确演出用例。当前源码落盘与 Editor 编译证据不等同于先前 Play 的 Hot Reload 补丁已生效。
- 转场队列/注册复用 1/1（103f787d）、冻结中英字幕/注册复用 1/1（6aa22f8a）通过，分别验证旧取消不擦除新遮幕或新文案、字体与材质；新任务退出仍恢复干净。没有运行全量测试。
- 核对后的时间验收边界：公共倍率/渐变/冻结与恢复已有契约用例；Demo 定向回归覆盖输入门闩、暂停/面板和退出等路径。尚未实际比较四个 Demo 所有任务、物理和比赛计时在各倍率下的推进，不据此声称该矩阵全部通过。

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
