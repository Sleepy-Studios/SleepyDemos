# Hotfix 主入口模块说明

## 负责什么

这一块负责业务接管后的首屏体验，目前以主菜单模块为中心，承担 Hotfix 入口、View 注册和首页展示。

## 关键入口

- `Assets/Scripts/Hotfix/AppDelegate/HotfixEntry.cs`
- `Assets/Scripts/Hotfix/AppDelegate/Boot/HotfixBootService.cs`
- 页面：`Assets/Scripts/Hotfix/Module/Main/MainMenuView/View/MainMenuView.cs`
- 展示数据：`Assets/Scripts/Hotfix/Module/Main/MainMenuView/MainMenuDemoEntry.cs`
- 卡片 Item：`Assets/Scripts/Hotfix/Module/Main/MainMenuDemoItemView/View/MainMenuDemoItemView.cs`
- 页面与卡片模板：`Assets/LoadResources/UI/Hall/MainMenuView.prefab`、`MainMenuDemoItemView.prefab`
- 图片：`Assets/LoadResources/UI/Hall/Art/`

## 当前行为

- 扫描 Hotfix 程序集中的 View 类型
- 运行 Hotfix 启动系统，当前会通过 `FluxService` 注册 `UserData`
- 注册首版空配置的 `HotfixWorldTransitionProvider`，为后续 Demo 的真实相机或场景过渡保留 Hotfix 扩展点
- 等待 `MainMenuView` 的 `ShowAsync` 导航结果稳定完成；Failed 保留原异常中断启动，Canceled 作为启动取消传播
- `MainMenuView` 订阅 `UserData`，打印启动时记录的本机硬件配置
- 在启动完成后销毁加载界面

## 主菜单卡片

`MainMenuView` 持有普通 `MainMenuDemoEntry` 集合，每项包含稳定 Key、主体图片地址、标题、副标题、描述、目标场景、浏览/进入状态及业务选中态。大厅采用明亮展厅布局：左侧玩法说明与开始按钮，右侧主体大预览，下方横向 Loop Scroll 卡片。当前登记无人机飞行、小小搬豆工、倒霉蛋俱乐部、DLSS 实验室；UI 交互展台没有目标场景，可浏览介绍但不能进入。

大厅专属美术位于 `Assets/LoadResources/UI/Hall/Art/Gallery/`：共用一张 Backdrop，每个 Demo 只有一张透明主体图，主展示与 Item 通过相同地址复用；不分别制作完整背景和缩略图。宣传主体不代表 Demo 内实际场景，也不含文字或按钮。背景使用等比覆盖，主体使用 Preserve Aspect，面板与按钮使用有 Border 的 Sliced 图片；不得将完整 UI 效果图当页面底图。

`MainMenuGalleryLayout` 根据页面 RectTransform 和实际安全区调整本页内容，桌面以 1920×1080 为设计基准，超宽屏扩展背景并限制内容宽度。横屏手机 16:9/20:9 共用同一个 Prefab，放大卡片、收起长说明，通过横向滑动浏览。设备识别复用 Core.InputDeviceState，不新增 CanvasScaler 或输入底座。

页面注册 typed 数据、点击与回收回调，再提交真实集合。数据回调只调用 `item.SetData(entries[index])`；卡片继承普通 ItemView，在具体 SetData 中更新图片、标题、副标题、选中框和状态标签。桥接先初始化再交付数据，复用不重复绑定；图片地址未改变时不重复加载。卡片不缓存业务数据或物理 Cell 上下文。

模板控件引用和按钮事件由正式 UIBind 生成。卡片使用已有 `UIImageLoader` 按地址异步加载预览，回收回调调用 `Clear` 释放当前图片并阻止旧请求写回；不再增加业务 Card/Button 子类、预览序列化组件或手写控件索引。生成文件的引用随 Prefab 重生成，不直接修改 `*Component.cs`。

列表仅使用一个公共 `LoopScrollMenuNavigation`，卡片使用 `LoopScrollMenuButton`。页面持有单个 MenuInputScope，隐藏及销毁时释放。键鼠/手柄更新默认焦点；触屏仅驱动输入门闩，不因布局重排抢回焦点。物理按钮回收后的身份保护、左右导航和滚入目标由公共组件处理。业务只将焦点同步到当前预览，不维护另一套导航字典。详细契约见 [Loop Scroll 宿主桥接](loop-scroll.md) 和 [公共玩法输入](gameplay-input.md)。

指针点击卡片只选择预览，点击“开始体验”才进入；键盘/手柄在卡片上确认可直接进入。进入仍统一经过 EnterDemoAsync，加载期间禁用浏览和开始操作，防止重复请求。正常返回 Hub 时按导航事务状态恢复控件。失败提示使用独立 Status 控件；Loading 会销毁旧页面，收尾刷新当前新实例并保留失败目标，将开始按钮变为“重试”；按住确认键不重复提交。

大厅画面设置入口位于左侧开始操作区，不在右上角单独悬浮。按钮直接调用 `UIManager.ShowAsync<DlssSettingsView>()`，按需打开普通 Pop。关闭按钮、Esc 或遮罩关闭后销毁弹窗并恢复来源焦点；大厅不维护额外的入口显隐作用域。

## 改这里时注意什么

- 改主菜单时，不要把通用 UI 能力塞回业务模块
- 新增 Demo 时先登记场景目录，再增加一项展示数据和预览图片，不新增固定按钮字段或另一套进入回调
- 如果新增启动期业务初始化，优先加入 `HotfixBootService`，不要散写在 `HotfixEntry`
- 如果主界面打开失败，先检查 UIBind 生成、预制体地址和类型扫描
- 具体 World / Camera Transition 只在 `HotfixWorldTransitionProvider` 注册，不把业务相机实现下沉到 Core

## 常见任务

- 增加新的 Demo 卡片数据
- 调整主菜单展示逻辑
- 接入新的首页模块
- 修复首屏 View 注册或加载异常

## 验证重点

- 从 `AppEntrance` 启动，检查页面与 Item 的 UIBind 引用、预览加载和独立 Status 文案。
- 用真实指针、键盘及手柄完成卡片选择和进入；滚动跨越 Cell 回收边界后，显示与实际进入目标仍应一致。
- 覆盖重复点击、加载失败恢复、按住确认键时不重复提交，以及正常返回 Hub 后能再次进入。
- 直接相关自动化入口为 `MainMenuNavigationPlayModeTests`。宽窄窗口、触控触区、手柄焦点可读性与真实设备体验继续单独验证，自动化结果不能代表视觉或双平台体验验收。

## 相关文档

- [接入运行期场景导航](../runbooks/use-scene-navigation.md)
- [接入 Sleepy Loop Scroll](../runbooks/use-loop-scroll.md)
- [接入 Core UI View](../runbooks/create-ui-view.md)
