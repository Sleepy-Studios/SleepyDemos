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

`MainMenuView` 持有普通 `MainMenuDemoEntry` 集合，每项包含稳定 Key、图片资源地址、标题、描述、目标场景与可进入状态。页面通过现有 Loop Scroll 展示卡片，Grid 列数由实际 Viewport 和模板尺寸决定，不维护另一套手机页面。当前登记无人机飞行、小小搬豆工、倒霉蛋俱乐部、DLSS 实验室；UI 交互展台没有目标场景，保持“未开放”。

页面注册 typed 数据、点击与回收回调，再提交真实集合。数据回调只调用 `item.SetData(entries[index])`；卡片继承普通 ItemView，在具体 `SetData(MainMenuDemoEntry data)` 中更新图片、标题、描述和进入按钮。桥接先完成控件初始化，再交付数据，复用不重复绑定；点击由页面按当前索引处理，卡片无需缓存数据或物理 Cell 上下文。

模板控件引用和按钮事件由正式 MvcBind 生成。卡片使用已有 `UIImageLoader` 按地址异步加载预览，回收回调调用 `Clear` 释放当前图片并阻止旧请求写回；不再增加业务 Card/Button 子类、预览序列化组件或手写控件索引。生成文件的引用随 Prefab 重生成，不直接修改 `*Component.cs`。

列表仅使用一个公共 `LoopScrollMenuNavigation`，卡片进入按钮使用 `LoopScrollMenuButton`。页面持有单个 `MenuInputScope`，每帧传入导航组件当前的 `FirstSelection`，隐藏及销毁时释放。物理按钮回收后的身份保护、Grid 导航和滚入目标由公共组件处理；业务不保存相邻按钮、导航字典或自建输入层。详细契约见 [Loop Scroll 宿主桥接](loop-scroll.md) 和 [公共玩法输入](gameplay-input.md)。

点击统一进入 `EnterDemoAsync`，加载期间禁用全部开放入口，防止重复或跨入口请求。正常返回 Hub 时，页面可能先于导航事务收尾显示，因此在可进入状态改变后刷新集合。失败提示使用独立 Status 控件；Loading 会销毁旧页面，异步收尾必须刷新当前恢复出来的页面。

## 改这里时注意什么

- 改主菜单时，不要把通用 UI 能力塞回业务模块
- 新增 Demo 时先登记场景目录，再增加一项展示数据和预览图片，不新增固定按钮字段或另一套进入回调
- 如果新增启动期业务初始化，优先加入 `HotfixBootService`，不要散写在 `HotfixEntry`
- 如果主界面打开失败，先检查 MvcBind 生成、预制体地址和类型扫描
- 具体 World / Camera Transition 只在 `HotfixWorldTransitionProvider` 注册，不把业务相机实现下沉到 Core

## 常见任务

- 增加新的 Demo 卡片数据
- 调整主菜单展示逻辑
- 接入新的首页模块
- 修复首屏 View 注册或加载异常

## 验证重点

- 从 `AppEntrance` 启动，检查页面与 Item 的 MvcBind 引用、预览加载和独立 Status 文案。
- 用真实指针、键盘及手柄完成卡片选择和进入；滚动跨越 Cell 回收边界后，显示与实际进入目标仍应一致。
- 覆盖重复点击、加载失败恢复、按住确认键时不重复提交，以及正常返回 Hub 后能再次进入。
- 直接相关自动化入口为 `MainMenuNavigationPlayModeTests`。宽窄窗口、触控触区、手柄焦点可读性与真实设备体验继续单独验证，自动化结果不能代表视觉或双平台体验验收。

## 相关文档

- [接入运行期场景导航](../runbooks/use-scene-navigation.md)
- [接入 Sleepy Loop Scroll](../runbooks/use-loop-scroll.md)
- [接入 Core UI View](../runbooks/create-ui-view.md)
