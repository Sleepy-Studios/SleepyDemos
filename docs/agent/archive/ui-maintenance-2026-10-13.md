# 公共 UI 维护实施记录（2026-10-13）

本文件保存本次已授权 Goal 的决策、实施证据和验证边界，不替代正式模块文档。

## 最终授权范围

1. Hub 与四 Demo 使用真实物理绑定图片；默认 Xbox，在 Hub 输入设置选择 Xbox / PS5 / NS，候选局部预览、应用生效、保存与重置。移除 Generic，修复未连接摇杆轴路径；补 DLSS 物理提示，卡丁车手柄页收起无绑定行。
2. 四 Demo 完整基础控件 Variant 库，统一 Foundation 入口，保留 GUID、业务绑定、已有风格和精确字号；资产名移除字号、下划线和哈希。
3. 组合控件的子控件继续经过本 Demo Variant，公共基础结构和行为仍可传播。
4. 固定 UI/HUD 源码色板集中；Prefab、UIState、材质、图片、动态配置色不接脚本色板。
5. UIManager 日常 Show / Close 与可等待导航入口共用 FIFO、资源生命周期、回滚和释放链。

## 实施中确认的调整

- 用户在侧对话明确修正 API 方向：日常调用采用 Show / Close，短逻辑写回调、长逻辑传方法；asyncLoad 控制真实资源加载方式，与 animation / hidePrevious 独立。Loading、场景导航、语言重建继续使用可等待入口。
- 用户随后确认 Show 不立即返回未初始化对象，而通过 onOpened 取得完整可用 View。失败或取消不执行成功回调。
- 用户要求判断颜色的热更边界：Core ColorUtil 保留转换方法及框架自身的少量基础色；Hub / Demo 可频繁调整的色板放 Hotfix UIColors，按 region 分组，不复制转换工具。
- 用户侧对话进一步确认：色板声明统一六位大写 HEX，可选 float 百分比 Alpha；复用 ColorUtil.GetColor 新重载，显式百分比覆盖原 Alpha，旧解析/fallback/WithAlpha 契约保持。允许最接近原值的八位 RGB 量化，字节 Alpha 保留足够小数；不顺便重新配色。

## 当前实施状态

- 输入：三套图标目录、Hub 选择行、卡丁车条件行与 DLSS 移动／观察已原生装配并通过正式 UIBind 生成；保存、旧偏好兼容、长文字布局及正式入口已通过。
- 变体库：每套覆盖 28 类公共基础资源；无人机 / DLSS / 夹爆它 / 纸艺卡丁车分别有 37 / 29 / 31 / 40 个含用途变体的正式 Prefab。迁移报告 `Library/DemoFoundationLibrary/20261013-120421/complete.txt`；作者布局、绑定、业务快照与实际继承传播检查通过。
- 颜色：固定 UI/HUD 源码迁移到 Core / Hotfix 正确边界，声明统一 HEX/百分比；浮点 RGB 按用户批准量化，非整数 Alpha 保留足够小数。转换契约和正式截图已核对；不承诺所有浮点通道逐位相同，Prefab 不接入源码色板。
- UI API：新公开入口与生产调用迁移完成，规定的九类 UI 回归及正式导航、失败恢复、语言重建通过。
- 相关永久测试仅扩展既有文件，保护设置保存、真实图片路径、同步资源加载、回调时点、取消和实例所有权。
- 保留此前两个 ProjectSettings 无关改动；本次仅本地提交，提交记录以 Git 历史为准，不 push。

## 验证证据

以下为本轮实际执行结果，源码静态核对不代表运行、视觉或真实设备验收。

- Unity 正式编译 `32849236`、`1cf268fe`、`62bf71ef`：无 C# 错误。
- 临时脚本和未使用色板清理后的最终编译 `f1250441`：hasErrors=false、errorCount=0。
- `GameSettingsSnapshotTests`：`f3c2ce55`，5/5；`GameSettingsServiceTests`：`5c4759fe`，5/5。首次服务测试被已删除 Generic 的旧资源目录缓存阻塞，刷新后通过。
- 导航基础四类：`b67a664c` 契约 18/18；`72c10ee8` 栈 10/10；`41a695f1` 绑定生成 7/7；`799b069f` 生命周期 23/23。报告在 `Library/UIRevisionTests/`。
- 其余导航五类：`7668b99a` UIManager 57/57；`ae364c79` UI 过渡 9/9；`3762b3c8` 世界过渡 13/13；`66857a2f` UI Root 7/7；`bafde2e0` View Prefab 约定 1/1。规定的九类 UI 回归共 145/145，不是项目全量测试。
- `CommonUtilityTests`：`1414d579`，37/37，覆盖新增百分比透明度重载和既有 HEX/fallback 契约；`UINavigationCoordinatorPlayModeTests`：`117cbce3`，6/6。
- Tips 修复显示前内布局 Padding 测量与已销毁焦点访问后，`c4584ef2`，21/21；此前 16/21 与 20/21 属于已解决的中间结果。长文滚动断言以原生布局后的实际起点判断移动，不以锚点必须为零替代公共契约；失败时也释放自己的导航门闩。
- `SettingsRowTests`：`405e3422`，3/3；正式 Hub 公共效果体验与冻结时间取消／世界恢复：`b7d858ca`，1/1。
- `UnifiedInputPlayModeTests`：`0ce07377`，15/15，包括组合图片与长文字回退自动宽度；基础资产 `ac14c926`，5/5，基础运行 `c47183a5`，3/3。
- 原生继承报告 `Library/DemoFoundationLibrary/verification-20261013-121525/complete.txt`：112 个基础源链、20 个实际结构传播实例及 12 个实际样式传播实例通过；探针修改最终逐字节恢复原 Prefab / meta / GUID。
- 正式 AppEntrance 一次性验收 `395f36a0`，1/1：Hub Xbox / PS5 / NS 候选、应用、放弃、重置及四 Demo 进入／返回完成。`Library/UIRevisionAcceptance/20261013-042201/` 保存 20 张真实截图，包括启动、8 次运行期 Loading、各 Demo 提示及 Hub 720p；Loading 每次都有实际渲染帧。虚拟输入设备不代表真实手柄验收。
- 正式入口失败恢复 `54f49ccf`，1/1；语言重建 `aa58bb20`，1/1。截图为本轮编辑器视觉核对，不是自动像素一致性或性能结论。
- 卡丁车 Steer 三项提示恢复源 NoWrap，保持首图标列与字体尺寸，只收掉后两图标的透明横向占位。中英及三品牌原生 TMP 测量最大 684.84，行宽 685、纸卡允许宽 686；GUID 与 UIBind 引用保持。报告 `Library/UIRevisionAcceptance/KartSteeringLayoutRepair.txt`，英文是原生字体布局测量，不作为真实设备截图验收。
- 修正临时检查的 Sheet 层级查找后，最终正式入口复验 `7730a84b`，1/1；三项转向说明单行及纸卡边界断言通过，截图已查看。最新 20 张截图位于 `Library/UIRevisionAcceptance/20261013-044754/`，清理报告 errors=0。
- `b02703e0` 因临时宿主被装配标脏而停在 TestRunner 保存窗口，job 超时；用户解除窗口后原生 XML 运行了目标方法。后续 Builder 使用 Preview Scene，并在测试前重建本次空宿主；不保存正式场景。

## 收口条件

日常回调入口与可等待入口共享同一事务，API方向以实施中确认的调整为准。五项实际使用闭环和四套库已完成，正式资源与规范一致；Generic、旧业务 API、本轮两个 Builder 及一次性入口验收脚本和 meta 已清理，临时装配独占的九个未使用色板字段已删除。正式 AppEntrance 恢复且无未保存修改；证据保留在忽略的 Library，不作为长期迁移工具提交。

本次没有做全量回归、Player / IL2CPP 构建、真实手柄／触屏或性能基准；编辑器截图没有做自动像素差分。现有永久测试扩展用于保护设置持久化、图片绑定、加载与回调时点、取消和所有权契约，不留下外观自检文件。此前两个 ProjectSettings 改动保持原样并排除提交。
