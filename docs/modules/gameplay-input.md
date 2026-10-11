# 玩法输入模块

## 职责与位置

`Assets/Scripts/Core/Runtime/Input/`（命名空间`Core.Runtime.Inputs`）提供键鼠、触屏、标准Gamepad的共同输入基础，使用项目已有Unity Input System及Core.Runtime程序集。Windows与Android使用同一实现；Xbox/PlayStation 按标准位置映射，Switch 的原生用途绑定使用 A 确认、B 返回，硬件是否连接及震动是否支持仍以实际平台为准。

- `GameplayInputRouter`：独占宿主动作资产副本，输出移动、视角、指针、目标导航和离散操作；识别最近实际使用设备，忽略死区漂移，管理自身震动。
- `GameplayInputContracts`：设备无关动作/帧、参数校验、角度与死区换算。鼠标与触屏是增量，手柄视角是角速度。
- `MenuInputScope`：借用既有EventSystem及InputSystemUIInputModule，切换时等待按键释放，菜单导航和提交只由公共UI处理。

- `TouchInputPad`：两个触控区域分别占有指针，移动归一化，视角换算为720p参考像素；`ConfigureHorizontal` 提供只读取横向拖动的滑条，纵向位移不压缩横轴；隐藏时清空输入。
- `LocalPauseState`：失焦、后台、当前手柄断连的暂停门闩，恢复设备后仍须明确继续。

公共Hub页面直接复用MenuInputScope：显示时建立唯一作用域，以LoopScrollMenuNavigation.FirstSelection设置当前可用焦点，每帧调用Update(当前焦点)，隐藏/销毁时Dispose。更新默认焦点不重置松键门闩；默认目标从不可用变为可用时恢复选择，列表刷新则只恢复同一个仍可见的业务Key，不提交。触屏点击空白清焦点不会被普通更新抢回。不需要GameplayInputRouter、业务按钮字典或指针模拟。循环按具体作用域身份结束，旧页面不得驱动恢复后页面的作用域；卡片按钮使用公共LoopScrollMenuButton的选中态。

鼠标/触屏点击空白清除焦点后，MenuInputScope在后续Move、Submit或Cancel到达时恢复仍可用的初始控件；已有指针选中项保持。恢复等待旧按键释放，不选择禁用/隐藏控件，不手动执行导航或提交。Move/Point/Submit/Cancel向可选Router通知实际设备，隐藏时完整退订。

`UIMenuScope` 为作用域内没有取消处理的普通可导航控件添加 `UIMenuCancelRelay`。Input System UI Module 将 Cancel 直接发送给当前选中对象，不向父级冒泡；Relay 只转交最近的菜单作用域。嵌套菜单不触发外层返回，下拉框等已有 `ICancelHandler` 的控件继续处理自己的取消操作；确认与方向导航仍由现有 UI Module 执行。

保存为 `Navigation.Mode.Explicit` 的控件保留作者连接，作用域不重新按空间位置覆盖它们；启用、控件显隐或交互状态变化后仍遵守这些连接。其余可导航控件继续自动选择同作用域内的可用邻居。纸卡等会改变显示位置的菜单应保存明确连接，避免入场动画改变手柄导航顺序。

## 边界

`InputActionSession.ReadAxis` 与 `ReadVector` 都可在原始手柄值上只应用一次死区，避免布局处理器叠加。标量 Value 动作重新启用时也须释放旧轴；模拟扳机的中性门闩保留低于按钮按下阈值的保持值。`InputBindingDisplay` 将摇杆子轴归到父摇杆的图标路径，实际绑定仍为原始轴。

需要清理设备切换前的保持输入时，调用 `InputActionSession.RequireRelease(旧设备)`，只锁住旧设备仍按住的动作，不以重新开启整张 Map 的方式吞掉新设备首次按键。

Core不处理下注、机台、牌局、角色运动约束或游戏时钟，也不创建相机和第二套EventSystem。宿主保留自己的inputactions、动作到玩法命令的映射、专属提示文案、触控布局和玩法设置。公共输入参数与绑定覆盖使用GameSettingsService，输入模块不另建PlayerPrefs键或存档格式。

DroneFlight 和 DLSS 使用各自动作资产及公共 InputActionSession；Showcase 使用菜单作用域，不复制设备底座。

## 调用和生命周期

宿主进入时创建Router及MenuInputScope；每帧先送触控采样、调用ReadFrame，再消费一次ConsumeActions，并更新菜单作用域。Gameplay是移动/视角，Interaction是物件选择，Menu把Confirm/Back/导航交还Core UI；切换后按住键须释放，避免一次A键执行两个动作。

Router在移动端首次创建时默认Touch，其它平台默认KeyboardMouse；之后DeviceKind由最近实际使用设备决定。触控区域与提示应跟随DeviceKind，不用`Application.isMobilePlatform || Touch`强制显示，否则Android接手柄后无法收起摇杆。轻触屏幕会通过实际Touchscreen活动切回触控，手柄小幅漂移不抢占设备提示。

触摸识别由 InputDeviceState 统一订阅 touch*/press，独立于玩法上下文；Router 仍按自己的设备白名单消费活动。不能只依赖onAnyButtonPress：当前Input System通用按钮枚举会漏过由phase派生的TouchPress。该识别不提交点击或玩法命令，真实桌面指针与菜单操作仍走原路径。

宿主转发OnApplicationFocus/OnApplicationPause，并用PauseState冻结自己的规则和演出时钟，清理触控持有指针。暂停状态不会修改全局timeScale。退出先Dispose菜单作用域、再Dispose路由器，恢复原导航/焦点、退订设备事件并停止本会话震动。该作用域服务单个当前玩法宿主，不支持两个宿主同时争用同一EventSystem。

## 维护和验证

Map名称可配置；动作语义是公共契约，新增专属玩法应消费已有设备无关命令或在自己的ActionMap实现，不向Core加入业务类型。原动作资产不被启停/改写，路由器只管理副本中指定的三张Map。

迁移原有输入测试到Tests.Module，未复制一套新测试：EditMode验证增量/角速度、死区和暂停；PlayMode验证真实InputSystem上下文、菜单单次提交、断连/震动及触控指针归属。Demo入口回归验证真实保存资产和Prefab接线。模拟设备不等于Xbox/Android硬件验收；移入AOT后正式Player仍需重生成HybridCLR构建数据并验证。

TouchInputPad保存资源已统一指向Core.Runtime.Inputs当前类型；不维护业务旧类型的MovedFrom映射或输入别名。

接入步骤见[使用玩法输入](../runbooks/use-gameplay-input.md)。

## 通用会话与设备提示

InputActionSession 克隆源资产，启停自己的 Map，SetMap 等待旧按钮/轴中立，Dispose 释放副本；ReadVector 对原始摇杆只应用一次公共死区。业务命令保留在 Demo，无人机四轴由自己的动作配置表达。

InputDeviceState.ActiveDevice/ActiveKind 表示实际操作，决定触控区、光标及交互视觉；PromptDevice/PromptKind 默认跟随最近实际使用设备，多个手柄选择实际使用者。连接与漂移不抢占提示；断连按剩余设备和平台入口恢复。提示更换不提交动作。

松键门闩只覆盖按键与手柄持续轴，鼠标绝对位置不需要回屏幕原点。触控命令按钮的 InputBindingPrompt 可配置 captionOnly，仅显示动作名；独立帮助/操作面板继续按 PromptDevice 提示实际绑定。

`InputBindingPrompt` 使用实际动作与设备内路径，在设备和绑定变化时刷新。默认复合提示为每个子方向选择当前设备的首个可用别名；设置绑定行使用 `BindBinding` 精确显示所选索引，W 与方向键即使属于同一动作也有各自图标。`Bind` 和 `BindPart` 清除精确索引选择；`BindPart` 保留指定复合子项。说明页可用 `SetDisplayDevice` 预览另一设备，实际输入设备与触控区域保持原状态。

正式公共基底为 `Assets/LoadResources/UI/Common/Input/InputPrompt.prefab` 与 `InputPromptButton.prefab`。前者提供独立 Caption 与多个 GlyphSlot，键帽包含图标、无对应图标时的真实键名以及组合分隔符；槽不足时保留完整绑定文本。后者复用相同提示结构，增加 Button、五态交互和 InputCommandButton。点击或保持仍交付 Demo 原命令，暂停、隐藏和失焦配对释放保持操作。

Drone、DLSS、Wall、KagitKart 各保存自己的真正 Prefab Variants；前三者位于各自 `Prefabs/UI/Parts/`，KagitKart 位于 `Prefabs/UI/Widgets/`。Variant 维护各 Demo 的颜色、字体样式、Sprite 与间距。纸艺赛车继续使用原纸卡和父页布局，公共提示只替换局部提示部件。`InputPromptGroup` 保存稳定行 Key、提示组件和可选区域的明确制作引用；View 通过正式 UIBind 生成字段持有组，运行时按 Key 取行，不扫描层级或临时生成提示。Drone HUD 的触控、操作面板和菜单提示也由该组保存。

`InputGlyphCatalog` 及其 GUID 保持稳定，资源位于 `UI/Common/Sprites/Input/Kenney/`，目录包含随包保存的 CC0 许可。键盘实际使用 Kenney 的字母、数字、功能键、修饰键、方向键和常用特殊键图形，鼠标使用对应鼠标按钮图形；只有缺少对应图标的键才显示空键帽与实际键名。单字母键按当前键盘布局的显示名选择图形。已识别手柄布局映射 Xbox、PlayStation、Switch，未知手柄使用通用位置图标；新增手柄沿用 InputDeviceState 的布局识别和 Catalog 的设备风格映射。菜单确认／返回的用途绑定由真实布局解析，Switch 的 A/B 对应真实物理位置，图标映射与控件路径必须一致。

菜单提示的 `menuAction` 支持 `Navigate`、`Submit`、`Cancel`，分别绑定现有 InputSystemUIInputModule 的 move、submit、cancel。Navigate 不提交确认；绑定 Demo 会话资产也不会覆盖这些公共菜单动作。固定 UI 导航、确认和返回由原 UI Module 处理。

Caption 使用 LanguageService 一次性设置文案，没有语言监听；字体与材质制作遵循[多语言与公共字体规范](../architecture/localization-fonts.md)。

## 输入设置与重绑定

`InputSettingsCatalog.Entry` 保存宿主 DisplayKey、正式源动作资产与 SourceId；`Entry.Actions` 通过 ActionId GUID 与 DisplayKey 明确选择该宿主实际消费的动作。同 Caption 的不同动作 GUID 均保留，例如 Kart 的 Drive 与 Global 快捷键。Hub 只列实际使用的 UI 动作，源资产里的 Player 动作不作为 Hub 改键入口；无绑定占位和内部指针动作不列入候选。

设置页按宿主、动作与实际单绑定展示，保存身份为 SourceId 与 binding.id，绑定索引用于当前候选动作的显示和编辑。复合根只作分组，子绑定分别编辑；同方向的多个键位别名保持独立。行上的文案来自 Actions 映射，物理键来自实际候选，避免显示名称、译文或列表顺序成为保存身份。

`InputRebindSession` 独占禁用的候选副本，监听、重置和冲突校验不改源资产或正在运行的会话。行交互先查询 `CanRebind`，UI／Menu Map 以及 Submit、Cancel、Point、Click 等安全动作保持固定；可展示其绑定但不可修改。监听以公共 Cancel 用途取消，取消、退出或销毁时释放操作和副本。`ExportOverrides` 在监听结束后校验导出，再交由公共设置提交；保存边界按正式目录验证 SourceId、绑定 GUID、冲突和固定入口。

监听保留原生 UI 输入事件，使取消按钮可操作；调用页面继续通过既有菜单作用域阻断玩法输入，并用 `UIMenuScope.SetNavigationEnabled` 暂停 Submit/Navigate、结束后等待松键恢复。指针匹配结束时若命中可交互 UI Selectable，恢复原候选并报告取消，长按界面控件也不能被记为鼠标绑定；控件外的鼠标按键保持可绑定。

InputActionSession 的参数和覆盖来自 GameSettingsService 成功提交快照。覆盖确实改变时 Router 清理旧边沿，并在当前上下文重建松键门闩；提交时已按住的新绑定必须松开再按才执行。音量等设置变化不会重置输入。详见[公共设置与表现](settings-presentation.md)。

UIBind 可以生成 InputCommandButton 的点击与保持回调；RegisterExtend 将回调交给 View 持有并在销毁时退订。InputBindingPrompt.BindAsset 按源动作 GUID 绑定实际会话副本；TouchInputPad 在禁用、失焦与应用暂停时释放指针。
