# 接入公共玩法输入

1. 在业务资源目录保存独立InputActionAsset；使用Input Actions编辑器维护，不保留一次性生成Builder。保留自己的动作ID和键位。
2. 提供三张Map。Gameplay包含`Move/PadMove/MouseLook/LookHold/PadLook/Interact/Pause`；Interaction包含`Point/Click/Navigate/PadNavigate/Confirm/Back/Secondary/Help/PreviousGroup/NextGroup/Pause`；Menu只含`Pause`。构造函数可指定不同Map名。Move/Navigate为Vector2，Pad动作绑定摇杆/方向键，Point为屏幕位置，MouseLook为delta，其余离散动作按Button配置。
3. 创建`GameplayInputRouter(asset, settings, gameplayMapName, interactionMapName, menuMapName)`，再用现有EventSystem创建`MenuInputScope`；不用额外UI输入模块。JinxCasino使用保存的Exploration/Table/Menu名称。
4. 把移动与视角触控区域挂到`TouchInputPad`，分别Configure(false/true)。根据Router.DeviceKind显示触控入口，移动端Router初始为Touch；切换手柄后收起，实际触摸屏幕再显示。每帧送入SetTouchFrame，ReadFrame返回的LookDegrees直接应用角度，不再乘deltaTime。ConsumeActions只消费一次，并由宿主转换为游戏命令。
5. 上下文切换时同时设置Router和MenuInputScope；菜单首次焦点传入已保存的可用控件，每帧Update作用域等待旧按键释放。物件选择使用InteractionNavigation、PointerPosition与PointerPressed。
6. 应用失焦、后台、设备断连后按PauseState冻结自己的时钟；恢复时调用TryResume，仍有阻塞时保持暂停。切换/暂停清理TouchInputPad，不保留旧指针。
7. 退出先释放MenuInputScope，再释放Router。预览设置使用独立GameplayInputSettings副本；取消时重新ApplySettings旧值，存储由宿主负责。

验证现有Tests.Module.GameplayInputStateTests、GameplayInputRouterTests、TouchInputPadTests的直接受影响范围，并实测自己业务入口与返回。不同平台、真实手柄、触屏、后台和震动支持须另有设备验证，不由Editor合成输入代替。
