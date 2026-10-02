# 玩法输入模块

## 职责与位置

`Assets/Scripts/Core/Runtime/Input/`（命名空间`Core.Runtime.Inputs`）提供键鼠、触屏、标准Gamepad的共同输入基础，使用项目已有Unity Input System及Core.Runtime程序集。Windows与Android使用同一实现；Xbox按标准南/东/西/北键映射，硬件是否连接及震动是否支持仍以实际平台为准。

- `GameplayInputRouter`：独占宿主动作资产副本，输出移动、视角、指针、目标导航和离散操作；识别最近实际使用设备，忽略死区漂移，管理自身震动。
- `GameplayInputContracts`：设备无关动作/帧、参数校验、角度与死区换算。鼠标与触屏是增量，手柄视角是角速度。
- `MenuInputScope`：借用既有EventSystem及InputSystemUIInputModule，切换时等待按键释放，菜单导航和提交只由公共UI处理。
- `TouchInputPad`：两个触控区域分别占有指针，移动归一化，视角换算为720p参考像素；隐藏时清空输入。
- `LocalPauseState`：失焦、后台、当前手柄断连的暂停门闩，恢复设备后仍须明确继续。

## 边界

Core不处理下注、机台、牌局、角色运动约束或游戏时钟，也不创建界面、相机和第二套EventSystem。宿主保留自己的inputactions、动作到玩法命令的映射、提示文案、触控布局和设置存储。参数对象可由任意设置系统提供，模块不另建PlayerPrefs键或存档格式。

JinxCasino已直接消费公共实现：原Exploration/Table/Menu资产及动作ID不变，构造时传入Map名称；不会重跑生成器。触控区域的脚本GUID、isLookPad和引用字段保持，保存资源已使用Core类型，不再保留原Hotfix类型的MovedFrom映射。其它Demo可按需接入，不强制改动其控制方案。

## 调用和生命周期

宿主进入时创建Router及MenuInputScope；每帧先送触控采样、调用ReadFrame，再消费一次ConsumeActions，并更新菜单作用域。Gameplay是移动/视角，Interaction是物件选择，Menu把Confirm/Back/导航交还Core UI；切换后按住键须释放，避免一次A键执行两个动作。

Router在移动端首次创建时默认Touch，其它平台默认KeyboardMouse；之后DeviceKind由最近实际使用设备决定。触控区域与提示应跟随DeviceKind，不用`Application.isMobilePlatform || Touch`强制显示，否则Android接手柄后无法收起摇杆。轻触屏幕会通过实际Touchscreen活动切回触控，手柄小幅漂移不抢占设备提示。

触摸识别使用会话私有的touch*/press Action，独立于三种玩法上下文，仅更新设备、遵守设备白名单并随Dispose释放。不能只依赖onAnyButtonPress：当前Input System通用按钮枚举会漏过由phase派生的TouchPress。该识别不提交点击或玩法命令，真实桌面指针与菜单操作仍走原路径。

宿主转发OnApplicationFocus/OnApplicationPause，并用PauseState冻结自己的规则和演出时钟，清理触控持有指针。暂停状态不会修改全局timeScale。退出先Dispose菜单作用域、再Dispose路由器，恢复原导航/焦点、退订设备事件并停止本会话震动。该作用域服务单个当前玩法宿主，不支持两个宿主同时争用同一EventSystem。

## 维护和验证

Map名称可配置；动作语义是公共契约，新增专属玩法应消费已有设备无关命令或在自己的ActionMap实现，不向Core加入赌场类型。原动作资产不被启停/改写，路由器只管理副本中指定的三张Map。

迁移原有输入测试到Tests.Module，未复制一套新测试：EditMode验证增量/角速度、死区和暂停；PlayMode验证真实InputSystem上下文、菜单单次提交、断连/震动及触控指针归属。Demo入口回归验证真实保存资产和Prefab接线。模拟设备不等于Xbox/Android硬件验收；移入AOT后正式Player仍需重生成HybridCLR构建数据并验证。

TouchInputPad保存资源已统一指向Core.Runtime.Inputs当前类型；赌场旧类型MovedFrom映射已删除，没有为旧原型维护输入别名。

接入步骤见[使用玩法输入](../runbooks/use-gameplay-input.md)。
