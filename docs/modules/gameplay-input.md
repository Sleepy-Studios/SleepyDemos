# 玩法输入模块

## 职责与位置

`Assets/Scripts/Core/Runtime/Input/`（命名空间`Core.Runtime.Inputs`）提供键鼠、触屏、标准Gamepad的共同输入基础，使用项目已有Unity Input System及Core.Runtime程序集。Windows与Android使用同一实现；Xbox/PlayStation 按标准位置映射，Switch 的原生用途绑定使用 A 确认、B 返回，硬件是否连接及震动是否支持仍以实际平台为准。

- `GameplayInputRouter`：独占宿主动作资产副本，输出移动、视角、指针、目标导航和离散操作；识别最近实际使用设备，忽略死区漂移，管理自身震动。
- `GameplayInputContracts`：设备无关动作/帧、参数校验、角度与死区换算。鼠标与触屏是增量，手柄视角是角速度。
- `MenuInputScope`：借用既有EventSystem及InputSystemUIInputModule，切换时等待按键释放，菜单导航和提交只由公共UI处理。

- `TouchInputPad`：两个触控区域分别占有指针，移动归一化，视角换算为720p参考像素；隐藏时清空输入。
- `LocalPauseState`：失焦、后台、当前手柄断连的暂停门闩，恢复设备后仍须明确继续。

公共Hub页面直接复用MenuInputScope：显示时建立唯一作用域，以LoopScrollMenuNavigation.FirstSelection设置当前可用焦点，每帧调用Update(当前焦点)，隐藏/销毁时Dispose。更新默认焦点不重置松键门闩；默认目标从不可用变为可用时恢复选择，列表刷新则只恢复同一个仍可见的业务Key，不提交。触屏点击空白清焦点不会被普通更新抢回。不需要GameplayInputRouter、业务按钮字典或指针模拟。循环按具体作用域身份结束，旧页面不得驱动恢复后页面的作用域；卡片按钮使用公共LoopScrollMenuButton的选中态。

鼠标/触屏点击空白清除焦点后，MenuInputScope在后续Move、Submit或Cancel到达时恢复仍可用的初始控件；已有指针选中项保持。恢复等待旧按键释放，不选择禁用/隐藏控件，不手动执行导航或提交。Move/Point/Submit/Cancel向可选Router通知实际设备，隐藏时完整退订。

## 边界

Core不处理下注、机台、牌局、角色运动约束或游戏时钟，也不创建界面、相机和第二套EventSystem。宿主保留自己的inputactions、动作到玩法命令的映射、提示文案、触控布局和设置存储。参数对象可由任意设置系统提供，模块不另建PlayerPrefs键或存档格式。

JinxCasino已直接消费公共实现：原Exploration/Table/Menu资产及动作ID不变，构造时传入Map名称；不会重跑生成器。触控区域的脚本GUID、isLookPad和引用字段保持，保存资源已使用Core类型，不再保留原Hotfix类型的MovedFrom映射。DroneFlight 和 DLSS 使用各自动作资产及公共 InputActionSession；搬豆工和 Showcase 使用菜单作用域，不复制设备底座。

## 调用和生命周期

宿主进入时创建Router及MenuInputScope；每帧先送触控采样、调用ReadFrame，再消费一次ConsumeActions，并更新菜单作用域。Gameplay是移动/视角，Interaction是物件选择，Menu把Confirm/Back/导航交还Core UI；切换后按住键须释放，避免一次A键执行两个动作。

Router在移动端首次创建时默认Touch，其它平台默认KeyboardMouse；之后DeviceKind由最近实际使用设备决定。触控区域与提示应跟随DeviceKind，不用`Application.isMobilePlatform || Touch`强制显示，否则Android接手柄后无法收起摇杆。轻触屏幕会通过实际Touchscreen活动切回触控，手柄小幅漂移不抢占设备提示。

触摸识别由 InputDeviceState 统一订阅 touch*/press，独立于玩法上下文；Router 仍按自己的设备白名单消费活动。不能只依赖onAnyButtonPress：当前Input System通用按钮枚举会漏过由phase派生的TouchPress。该识别不提交点击或玩法命令，真实桌面指针与菜单操作仍走原路径。

宿主转发OnApplicationFocus/OnApplicationPause，并用PauseState冻结自己的规则和演出时钟，清理触控持有指针。暂停状态不会修改全局timeScale。退出先Dispose菜单作用域、再Dispose路由器，恢复原导航/焦点、退订设备事件并停止本会话震动。该作用域服务单个当前玩法宿主，不支持两个宿主同时争用同一EventSystem。

## 维护和验证

Map名称可配置；动作语义是公共契约，新增专属玩法应消费已有设备无关命令或在自己的ActionMap实现，不向Core加入赌场类型。原动作资产不被启停/改写，路由器只管理副本中指定的三张Map。

迁移原有输入测试到Tests.Module，未复制一套新测试：EditMode验证增量/角速度、死区和暂停；PlayMode验证真实InputSystem上下文、菜单单次提交、断连/震动及触控指针归属。Demo入口回归验证真实保存资产和Prefab接线。模拟设备不等于Xbox/Android硬件验收；移入AOT后正式Player仍需重生成HybridCLR构建数据并验证。

TouchInputPad保存资源已统一指向Core.Runtime.Inputs当前类型；赌场旧类型MovedFrom映射已删除，没有为旧原型维护输入别名。

接入步骤见[使用玩法输入](../runbooks/use-gameplay-input.md)。

## 通用会话与设备提示

InputActionSession 克隆源资产，启停自己的 Map，SetMap 等待旧按钮/轴中立，Dispose 释放副本；ReadVector 对原始摇杆只应用一次公共死区。业务命令保留在 Demo，不扩展赌场枚举承载无人机四轴。

InputDeviceState.ActiveDevice/ActiveKind 表示实际操作，决定触控区、光标及交互视觉；PromptDevice/PromptKind 独立选择已连接手柄，多个手柄优先最近实际使用者，离线后选剩余手柄或平台入口。连接、漂移和提示更换不提交动作。

松键门闩只覆盖按键与手柄持续轴，鼠标绝对位置不需要回屏幕原点。触控命令按钮的 InputBindingPrompt 可配置 captionOnly，仅显示动作名；独立帮助/操作面板继续按 PromptDevice 提示实际绑定。

InputBindingPrompt 使用实际动作和设备内路径，绑定/设备变化时更新。图标目录 InputGlyphCatalog 通过资源引用加载，仅保留使用中的 Xelu CC0 图标。已识别布局映射 Xbox/PlayStation/Switch；未知手柄的四个面键使用通用位置图标，其余控制显示实际绑定文字。Submit/Confirm/Interact 使用 <Gamepad>/{Submit}，Cancel/Back 使用 <Gamepad>/{Cancel}，由布局解析到 Switch A/B，不能仅换显示图标。
