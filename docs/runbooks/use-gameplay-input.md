# 接入公共玩法输入

1. 在业务资源目录保存独立InputActionAsset；使用Input Actions编辑器维护，不保留一次性生成Builder。保留自己的动作ID和键位。
2. 提供三张Map。Gameplay包含`Move/PadMove/MouseLook/LookHold/PadLook/Interact/Pause`；Interaction包含`Point/Click/Navigate/PadNavigate/Confirm/Back/Secondary/Help/PreviousGroup/NextGroup/Pause`；Menu只含`Pause`。构造函数可指定不同Map名。Move/Navigate为Vector2，Pad动作绑定摇杆/方向键，Point为屏幕位置，MouseLook为delta，其余离散动作按Button配置。
3. 创建`GameplayInputRouter(asset, settings, gameplayMapName, interactionMapName, menuMapName)`，再用现有EventSystem创建`MenuInputScope`；不用额外UI输入模块。Map 名称使用宿主动作资产中的实际名称。
4. 把移动与视角触控区域挂到`TouchInputPad`，分别Configure(false/true)。根据Router.DeviceKind显示触控入口，移动端Router初始为Touch；切换手柄后收起，实际触摸屏幕再显示。每帧送入SetTouchFrame，ReadFrame返回的LookDegrees直接应用角度，不再乘deltaTime。ConsumeActions只消费一次，并由宿主转换为游戏命令。
5. 上下文切换时同时设置Router和MenuInputScope；菜单首次焦点传入已保存的可用控件，每帧Update作用域等待旧按键释放。物件选择使用InteractionNavigation、PointerPosition与PointerPressed。
6. 应用失焦、后台、设备断连后按PauseState冻结自己的时钟；恢复时调用TryResume，仍有阻塞时保持暂停。切换/暂停清理TouchInputPad，不保留旧指针。
7. 退出先释放MenuInputScope，再释放Router。公共输入参数和改键由GameSettingsService提交，宿主消费当前快照；局部参数预览使用独立GameplayInputSettings副本，取消时重新ApplySettings旧值。

普通菜单可只使用MenuInputScope，不创建GameplayInputRouter。固定菜单以保存的可用Button设置Menu上下文，每帧驱动Update；隐藏/销毁时Dispose。MainMenuView使用公共LoopScrollMenuNavigation.FirstSelection，每帧调用Update(navigation.FirstSelection)；默认目标变为可用时恢复选择，不重置松键门闩、不自动提交。列表负责在刷新后恢复同一个仍可见的Key，普通更新不抢回触屏点击空白清除的焦点。页面生命周期与作用域身份约束驱动循环。该方式复用Core的InputSystemUIInputModule、Submit和Move配置；不要手动读取A/Enter或模拟鼠标。

初始控件必须在菜单需要导航时可用；触屏/鼠标点空白后，方向、确认或返回会通过公共作用域补回焦点。不要在页面另写设备切换选中逻辑；当前指针选中的有效控件不会被默认焦点覆盖。

验证现有Tests.Module.GameplayInputStateTests、GameplayInputRouterTests、TouchInputPadTests的直接受影响范围，并实测自己业务入口与返回。不同平台、真实手柄、触屏、后台和震动支持须另有设备验证，不由Editor合成输入代替。

## 专属动作与提示

无人机等非 Move/Look 玩法创建 InputActionSession(source)，SetMap("自己的 Map") 后读取动作；上下文切换和退出交还现有 UI 导航，源资产不启停。提示绑定同一个生效副本，设备识别与按键显示复用公共组件。

单轴驾驶使用 `ReadAxis(动作名, 死区)`，不要先读已处理轴再叠加第二遍死区。横向触控滑条使用 `TouchInputPad.ConfigureHorizontal(满幅距离)`，距离按 720p 参考像素配置，仍复用相同指针所有权与松手、禁用、失焦清理。没有调用该配置的已有摇杆和视角区域保持原行为。

普通页面/内部弹窗在保存资源添加 UIMenuScope，按钮添加 UIMenuCancelRelay；业务订阅 Canceled 做关闭/返回。该组件与玩法 Router 的 MenuInputScope 使用同一所有者栈，只有顶层驱动导航，避免后台页面抢焦点。Phone UI 根据 ActiveKind 显隐摇杆，提示根据 PromptKind；两者不能合成一个设备状态。

## 制作统一提示

1. 从 `Assets/LoadResources/UI/Common/Input/InputPrompt.prefab` 复用非交互提示，需要点击或保持时复用 `InputPromptButton.prefab`。每个 Demo 使用自己的真正 Variant，保留作者 Sprite、颜色、字体样式和间距；已有四套资源如下。

   | Demo | Assets/LoadResources 下的目录 | Prefab 名称 |
   | --- | --- | --- |
   | DroneFlight | `Demos/drone_flight/Prefabs/UI/Foundation/` | `DroneInputPrompt`、`DroneInputPromptButton` |
   | DLSS | `Diagnostics/DlssLab/Prefabs/UI/Foundation/` | `DlssInputPrompt`、`DlssInputPromptButton` |
   | WallSqueeze | `Demos/wall_squeeze/Prefabs/UI/Foundation/` | `WallInputPrompt`、`WallInputPromptButton` |
   | KagitKart | `Demos/kagit_kart/Prefabs/UI/Foundation/` | `KagitKartInputPrompt`、`KagitKartInputPromptButton` |

   表中资源扩展名均为 `.prefab`。赛车只接局部提示，父页、纸卡与触控布局继续由 Demo 维护。基础部件选型见[基础 UI 组件](use-core-ui-components.md)，字体制作见[公共字体规范](../architecture/localization-fonts.md)。

2. 配置正式 InputGlyphCatalog、Caption 与 GlyphSlot 引用。提示从当前设备的实际绑定解析多个图标和组合分隔符，文案通过 `SetCaptionKey(key, args...)` 使用正式词条和整句参数，例如 `SetCaptionKey("Input.Drone.ResetHoldDuration", seconds)`；参数数组会复制，字体和材质跟随词条 FontId，不订阅语言事件。只有不属于公共词表的专属业务说明使用 `SetCaption`，此入口清除旧文案 Key 与参数。动作说明与键帽分开制作，不在正文里拼固定键名或富文本键帽。触控命令按钮可以配置 captionOnly，只显示必需的操作文案。
3. 页面包含多行提示时，使用 InputPromptGroup 明确保存每行 Key、prompt 和可选 rowRoot。通过正式 UIBind 菜单生成组字段，View 直接使用生成字段与 `group.Get(Key)`；组缺失是制作错误。原作者文本与物理提示区域互斥显示，触屏说明和专属反馈保留。
4. 显示前对提示调用 `Bind(实际会话动作)` 或 `BindAsset(session.Asset)`。帮助页的设备预览调用 `SetDisplayDevice(KeyboardMouse/Gamepad)`，恢复跟随实际设备时传 null；预览不会切换输入、摇杆或光标状态。物理键提示显隐使用 `SetBindingsVisible`，可点击按钮仍保留动作说明。
5. 菜单提示设置 menuAction：Navigate 对应现有 UI Module 的 move，Submit 对应 submit，Cancel 对应 cancel。公共菜单导航与确认返回保持原绑定，业务按钮继续沿原命令回调执行，不在提示里重复读取硬件或模拟提交。

## 展示与编辑精确绑定

本期公共设置只提供通用输入参数，不新增 Demo 改键 UI。以下为未来宿主复用公共候选能力的接入契约。

1. 由宿主自己持有源资产和 SourceId，需要动作文案映射时可复用 `InputSettingsCatalog` 类型；不要重新接入公共设置配置。使用 `Entry.ConfigureActions` 制作实际动作 GUID 到文案 Key 的映射，`Entry.Actions` 是宿主可选的动作入口。排除未消费动作、空绑定占位和内部指针；同文案的不同动作身份仍分别保留。
2. 根据 ActionId 在重绑定候选资产中找到动作，再枚举其单绑定与复合子绑定。行身份使用 SourceId／binding.id，当前 bindingIndex 用于操作候选。复合根不作为可编辑行，同方向的 W、方向键等别名各有独立绑定行。
3. 行提示使用精确绑定 API：

   ```csharp
   var action = candidate.Asset.FindAction(label.ActionId, true);
   prompt.SetCaptionKey(label.DisplayKey);
   prompt.SetDisplayDevice(deviceKind);
   prompt.BindBinding(action, bindingIndex);
   ```

   需要纯显示数据时调用 `InputBindingDisplay.GetParts/Get` 的可选 bindingIndex。它只解析指定的非复合根绑定，不返回这个动作的第一组键位。复用为完整动作时调用 Bind，复用为指定复合子项时调用 BindPart；两者都会清除精确索引选择。
4. 在候选 `InputRebindSession` 上查询 CanRebind，再开始 Begin；固定 UI／Menu 绑定显示为不可编辑。监听取消交给公共 Cancel 用途，冲突回退当前候选绑定。指针事件保留给 UI，点击取消按钮或长按界面控件不能写成鼠标绑定；控件外的鼠标按键仍可绑定。使用 ConflictingActionId 匹配正式动作文案，不解析诊断字符串来寻找动作。
5. 监听结束后 ExportOverrides，调用并等待 `GameSettingsService.ApplyBindingOverridesAsync(source, overrides, token)`；检查 SettingsApplyResult。该入口局部验证源资产、绑定身份、冲突与固定菜单，再复用公共事务保存和通知，不会清除其它 SourceId 的记录。源资产与运行会话在候选阶段保持原绑定；退出释放候选。成功提交后公共会话接收覆盖，Router 仍等待提交时保持的新键松开再执行。

## 图标资源与设备扩展

正式图标位于 `Assets/LoadResources/UI/Common/Sprites/Input/Kenney/`，随目录保存 CC0 许可。键盘使用下载包里的实际字母、数字、F 键、修饰键、方向键和常用特殊键 PNG，鼠标使用对应按钮 PNG；已有图形时不在空键帽上重新绘制同一键名。字母按真实键盘布局的显示名选择，只有没有图形的键才使用空键帽和实际文本。

手柄图标由 Hub 输入设置里的 Xbox／PS5／NS 候选选择，只有应用成功才刷新全局；宿主未来使用绑定条目时，可通过 `SetDisplayGamepadStyle(candidate.Input.GamepadStyle)` 局部预览。默认 Xbox，真实硬件品牌不自动改偏好；风格值保持 Xbox=1、PlayStation=2、Switch=3，Generic 不再参与制作。取消／恢复默认沿现有设置候选流程执行，条目回收清除局部预览。

新增手柄先确认 Input System 的真实布局与控件路径，再核对现有 InputGlyphCatalog 的三种风格映射，保留正式 Catalog GUID 和地址。确认／返回依据原生用途解析，Switch A 的物理位置与 Xbox A 不同，选择品牌只影响图片。未连接设备预览也把摇杆子轴归到父摇杆图标。真机验证对应动作、用途、图标和震动支持，Editor 合成设备只覆盖已声明布局的行为。

直接相关回归使用 UnifiedInputPlayModeTests 的复合键帽、真实设备提示、Kenney 改键图标、精确别名行与菜单动作断言，以及 InputRebindSessionTests 的候选／冲突／固定绑定验证。接入 Demo 后再验证保存的 Variant、生成组字段、触控与物理提示切换、改键提交时的松键门闩；按[测试入口](run-unity-tests.md)选择最小相关范围。

## 正式入口输入验收

从 `Assets/Scenes/AppEntrance.unity` 使用 EditorSimulateMode 启动，等待正式 Hub 和资源服务完成初始化，不替换资源服务或临时实例化页面。Hub 输入页可由 `SettingsFlow.OpenAsync(SettingsCategory.Input)` 打开；等待 `ViewTab.CurrentClickView` 为已显示的 `InputSettingsPanelView`。

- 切换“手柄图标”下拉的 Xbox／PS5／NS。公共页不再展示设备或改键列表；已应用的 `GameSettingsService.CurrentGamepadStyle`、其他页面提示与保存记录应保持原值。`UIDropdown.SetSelectedIndex` 是静默回显，自动验收应点击真实选项按钮，不能用它代替用户选择。
- 点击“应用”后等待 `SettingsFlow.IsSubmitting` 结束，核对已应用偏好、保存记录和公共提示图标一致。再次编辑后“放弃”应保留最后一次已应用的偏好；“恢复默认”先只把候选重置到 `GameSettingsService.GetDefaults().Input`，保留全部 BindingOverrides，应用后才全局生效。
- 从正式 Hub 进入纸艺卡丁车，封面“打开”进入模式页，再用其“操作说明”打开 Controls。手柄分类应显示真实当前会话绑定，Mute／Restart 的整行标题与提示收起；切回键鼠或触屏应恢复各自说明。不要为验收启动比赛或越过本场菜单命令直接装配 Controls。
- 从正式 Hub 进入 DLSS 场景，核对保存的 `MovePrompt` 和 `LookPrompt`。键鼠分别使用当前 Move 组合绑定与 LookHold 鼠标保持键；手柄分别使用 Move 左摇杆和 Look 右摇杆，均属于当前观察会话副本。切到触屏收起这两条物理提示，保留原触控区域。

没有真实手柄时，可用 Input System 合成 Gamepad 与 `InputDeviceState.Notify` 验证正式页面的设备切换；Controls 的分类预览也不要求硬件连接。自动验收只删除自己添加的合成设备。临时应用设置的验收必须在退出 Demo 后恢复原运行时设置，再恢复原始 PlayerPrefs 记录及其存在状态；原 InputSystem 设置、Game View 画幅与临时脚本一并归还。合成设备和截图不能代替品牌硬件、触屏及震动手感验收。
