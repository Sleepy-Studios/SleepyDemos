# PC DLSS 实验室

## 职责与边界

实验室是PC诊断场景，不属于玩法Demo，不分配Demo程序集或小游戏资源组。通用DLSS SR/DLAA、相机输出和历史管理继续归 `Core.Runtime/Rendering/Streamline`；实验室只提供移动物体、细线、透明面和观察视角，便于检查静态细节与运动表现。

当前原生后端仍仅支持Windows Editor的D3D12/Vulkan。归类为PC实验室不代表Windows Player、其他桌面系统或任意GPU已经通过；不扩展本轮渲染能力范围。相关能力、限制和既往证据以 [Streamline模块](streamline.md) 为准。

## 代码与资源

- 场景/Flux/控件：`Assets/Scripts/Hotfix/Module/Diagnostics/DlssLab/`，使用独立 `Hotfix.Diagnostics.PC.asmdef`，只编译 Editor 与 WindowsStandalone64；程序集引用 Hotfix 和 Core，公共 Hotfix 不反向引用实验室。
- 保存场景：`Assets/LoadResources/Diagnostics/DlssLab/Scenes/Main.unity`。
- 动作资产：`Assets/LoadResources/Diagnostics/DlssLab/Data/Dlss.inputactions`。
- 控件与风格源：`Assets/LoadResources/Diagnostics/DlssLab/Prefabs/UI/`，正式控件为 `DlssControls.prefab`，局部Variant位于Foundation。
- 核心渲染：`Assets/Scripts/Core/Runtime/Rendering/Streamline/`，配置工具位于Core.Editor，原生桥位于 `Native/Streamline/`。

`GameSceneId.Dlss` 保留为诊断场景身份，与DLL分组和DLSS功能开关无关。入口从游戏Demo卡片列表移除；Hub 的 PC 诊断入口按目标动态加载 `Hotfix.Diagnostics.PC.dll` 后复用公共场景导航。它不占用 Demo1–Demo5 分组，小游戏不编译或加载该 DLL，不提供入口、不采集实验室和原生DLSS依赖。独立 DLL 的 WindowsStandalone64 编译范围不扩大原生后端仅 Windows Editor 的当前能力范围。场景保持唯一MainCamera/AudioListener，退出和回滚通过原公共导航收口。Core不依赖Hotfix导航、Data或View。

## 输入与设置

键鼠使用右键观察、WASD移动、Shift加速、R重置和Backspace返回；手柄使用两摇杆移动/观察、左肩加速、西侧键重置与返回键退出。正式默认绑定由本场动作资产维护，InputActionSession持有独立副本；公共设置仅提供灵敏度、死区、震动、手柄图标等通用参数，不列出实验室动作或提供重绑定。

公共画质设置仍只在Hub修改；实验室消费已保存的档位，不提供第二套公共画质面板或设置快捷入口。世界运动使用公共玩法时间，冻结时仍允许重置和返回。`StreamlineRuntime.ResetHistory` 在重置视角后清理时域历史。

## 生命周期

`DlssDemoController` 创建输入会话和本场 `DlssData`，打开 `DlssControlsView` 并绑定同一动作副本。`DlssAction → DlssHandler → DlssData` 管理重置、退出与命令保持状态；控制器负责连续轴读取、相机和演示物体运动。页面加载使用场景退出令牌，完成后核对来源Data实例；销毁时取消任务、退订设备事件、释放输入并关闭捕获的具体View。

返回失败恢复当前场景控件与输入，不恢复已卸载来源实例。真正GPU会话与UI Camera Stack由公共渲染/场景模块管理，实验室不另设DLSS会话或覆盖共享Renderer资产。

## 外观维护

保持现有深蓝底、青色焦点、暖白说明文字与局部Foundation Variant；不因目录迁移重建界面。命令按钮保留InputCommandButton、持续按压/释放及真实动作提示；纯装饰不拦射线。中文/混排复用HarmonyOS_CN，英文按FontId使用BebasNeue_EN，不另建字体或运行时主题。

已制作的 Move/Look Caption为20，输入提示源Caption为22、KeyText为18；触控命令作者矩形156×48、移动/观察区200×200。这些是保存资产的制作样本，不代表目标设备实测，也不据此新增移动端发布范围。结构行为仍来自公共输入控件，图标、颜色和布局由实验室Variant维护。

## 维护与验证

日常维护保存场景和Prefab，正式UIBind生成字段；不恢复一次性场景Builder。Renderer配置使用既有 `Tools > Rendering > Streamline > 配置项目Renderer`。原生依赖、后端启动和图像检查见 [Streamline接入](../runbooks/use-streamline.md)。

复用现有 `DlssDemoFlowTests`、`PostProcessImageTests`、Streamline图像与场景运行时的直接相关用例，检查输入、重置、退出/失败恢复、相机栈和偏好。公共Hub卡片测试使用普通玩法场景，不再把DLSS卡片存在当作导航契约。迁移后需另行记录实际编译/测试结果；[历史验证](../agent/archive/dlss-validation-history.md)不代表当前代码已经通过，且Editor通过不等于独立Player或GPU性能收益。

相关边界：[场景导航](scene-runtime.md)、[公共设置](settings-presentation.md)、[公共输入](gameplay-input.md)、[公共UI](ui-runtime.md)。
