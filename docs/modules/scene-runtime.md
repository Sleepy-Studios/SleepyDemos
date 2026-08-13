# 运行期场景导航

## 负责什么

运行期场景导航负责在 Hotfix 接管后，以 `AppEntrance` 为常驻 Hub 壳加载、激活和卸载 Demo 场景，并在整个事务期间协调通用 Loading、UI Camera、基础相机和 AudioListener。

它不负责启动期资源初始化，也不会重新加载 `AppEntrance` 或重新运行 `CoreEntrance`。

## 代码位置

- Core 资源协议：`Assets/Scripts/Core/Runtime/Resource/IResourceSceneLoader.cs`
- YooAsset 适配：`Assets/Scripts/Core/Runtime/Resource/YooAssetResourceSceneLoader.cs`
- Hotfix 业务导航：`Assets/Scripts/Hotfix/Module/Common/SceneManagement/`
- 运行期 Loading：`Assets/Scripts/Hotfix/Module/Common/CommonLoadingView/`

## 主链路

进入 Demo：

1. `GameSceneNavigator.SwitchAsync(GameSceneId)` 拒绝重复或并发请求。
2. `CommonLoadingView` 以无动画 Page 替换当前主页面。
3. `IResourceSceneLoader` 通过 YooAsset Additive 加载目标场景并报告真实进度。
4. 校验目标场景唯一 `MainCamera` 与 `AudioListener`。
5. `UIRootManager.BindToBaseCamera` 把持久化 UI Camera 移入新 URP Camera Stack。
6. 激活目标场景、停用来源相机，最后关闭 Loading。

返回 Hub：

1. 显示 `CommonLoadingView`。
2. 恢复 Hub 相机、AudioListener、UI Camera Stack 和 Active Scene。
3. 使用原加载句柄卸载 Demo。
4. 用 `MainMenuView` 替换 Loading。

## 生命周期与失败恢复

- Build Settings 只包含 `AppEntrance`；Demo 场景由 YooAsset Collector 收集。
- 每个成功加载的场景句柄必须由创建它的 `IResourceSceneLoader` 配对卸载。
- 加载、相机校验或旧场景卸载失败时，恢复来源 Active Scene、相机、AudioListener 和 UI。
- 场景切换不支持提交后的任意取消；Unity/YooAsset 已开始的场景操作必须收口到成功或明确回滚。
- Loading 进度单调递增；没有真实字节信息时不显示虚假大小。

## 边界规则

- Core 只接收资源地址和 Unity 场景语义，不认识 `DroneFlight` 等业务枚举。
- `GameSceneId` 与地址目录只放 Hotfix；新增 Demo 必须登记到 `GameSceneCatalog`。
- Hotfix 不直接持有 YooAsset `SceneHandle`，也不直接调用 `SceneManager.LoadSceneAsync` 绕过全局导航。
- Demo 场景必须且只能提供一个带 `MainCamera` Tag 的 Camera 和一个 AudioListener。
- `StartupLoading` 与 `CommonLoadingView` 生命周期不同，不共享脚本或 Presenter。

## 验证重点

- Hub → Demo → Hub 往返后，Active Scene、基础相机与 UI Camera Stack 正确。
- 任意稳定时刻只有一个启用的 AudioListener。
- 返回 Hub 不重新运行启动状态机，Demo 场景及资源句柄完成卸载。
- 失败恢复后来源场景仍可操作，Loading 不残留。

## 相关文档

- [资源系统设计原则](../architecture/resource-system.md)
- [Core UI 渲染设计原则](../architecture/ui-rendering.md)
- [新增 Demo](../runbooks/add-demo.md)
- [接入运行期场景导航](../runbooks/use-scene-navigation.md)

