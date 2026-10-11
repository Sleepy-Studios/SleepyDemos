# 公共设置与表现 Goal 实施记录

目标：按用户确认的 G0–G11 顺序实施公共设置、表现 API、全部 Demo 接入、功能验收、分页美术确认和正式 UI。整体暖白/朱红瑞士方向已确认，各分页仍须在功能验收后出图确认。

| Goal | 当前状态 | 交付与剩余 |
|---|---|---|
| G0 | 已建立基线 | 保护原 ProjectSettings.asset 改动，明确 Core/Hotfix/Demo 边界与文档规则 |
| G1 | Editor功能验收通过 | 独立快照、SO、统一偏好、启动/直启、并发初始化、迁移、保存失败及事务回滚通过 |
| G2 | 公共API及Hub重建通过 | 中英文 FontId、一次性文本 API、真实 Hub 销毁重建/失败恢复通过；正式语言设置页与长译文布局属G10 |
| G3 | 功能及既有宿主回归通过 | 公共参数、改键、最近实际设备提示；Kenney 键鼠和三类手柄资源接入，正式提示Prefab属G10，真实硬件属G11 |
| G4 | Editor功能及GPU验收通过 | 管线副本、显示确认、手动FXAA/MSAA、DLSS/DLAA接管和关闭恢复通过；Player显示验证待完成 |
| G5 | 功能及既有宿主接入通过 | 分类总线/DSP增益、静音与辅助限制；普通音源及通知隔离回归通过，正式声音页属G10 |
| G6 | 实现及图像验证通过 | 七类效果、基础/可更新临时作用域、允许限制与亮度；正式场景 GPU 图像回归已通过 |
| G7 | 功能及既有宿主回归通过 | 公共玩法时钟、倍率/冻结租约、曲线制作配置；Drone/Wall/Kart直接相关回归通过 |
| G8 | 公共API及GPU验收通过 | 七类黑幕/四方向擦幕形状、队列与取消、黑边/HUD、冻结中英Title通过；正式表现View属G10 |
| G9 | Editor功能阶段通过 | DLSS、Drone、Wall、Kart和Hub接线；Showcase保持独立公共菜单宿主。正式分页、演出入口及视觉装配在G10后再验收 |
| G10 | 正式装配中 | 七图基本确认；Hub基础Prefab和各Demo独立Variant共享行为，保留卡丁车纸艺外观 |
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

G10首张[显示与画质效果图](art/settings/graphics-settings-v1.png)已用内置imagegen生成，沿用已确认整体方向，展示DLSS关闭时手动MSAA4、选择/实际状态和接管说明；[生成提示词](art/settings/graphics-settings-v1.prompt.txt)一并保存。该图等待用户确认，不是Unity已装配截图；正式页面未改。正式输入提示仍使用已导入Kenney资源，效果图中的键帽仅表示布局和风格。后续声音、控制、语言、辅助、公共提示和体验页分别出图确认。

G10其余六张已生成并保存，完整[美术确认索引](art/settings/README.md)记录各图及提示词。已按实际Kenney资源修正控制页X键以及公共提示图的PlayStation菜单键/Backspace；仍需正式Prefab有效绑定与交互验收。已提出具体确认范围问题，尚未收到用户确认，不装配对应UI。此次只增加美术预览和阶段文档，无生产代码或Unity资源改动，无需重新运行Unity测试。
