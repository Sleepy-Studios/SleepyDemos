# 验证 Core 运行时基础设施

## 目标

用于验证资源加载抽象、基础 UI 组件、UIManager 补齐和文档入口是否符合当前项目分层规则。

适用场景：
- 修改 `Assets/Scripts/Core/Runtime/Resource`
- 修改 `Assets/Scripts/Core/Runtime/UI`
- 修改 `Assets/Scripts/Core/Runtime/Components`
- 修改启动资源初始化、热更装配或 SpriteAtlas 加载
- 从其它项目迁移公共资源/UI 底座能力

## Unity Editor 验证

1. 打开项目并等待脚本编译完成。
2. 运行菜单：
   - `Tools/SleepyDemos/Validate Core Runtime Infrastructure`
3. Console 中应看到：
   - `[SleepyDemos] Core Runtime Infrastructure 校验通过`
4. 如果 Console 出现错误，先按错误中的路径修复，不要继续 PlayMode 验证。

## 命令行验证

关闭当前项目的 Unity Editor 后执行：

```powershell
& "D:\Unity\Unity_Editor\6000.3.15f1\Editor\Unity.exe" `
  -batchmode `
  -quit `
  -projectPath "D:\Unity\Unity_Project\SleepyDemos" `
  -executeMethod Core.Editor.CoreRuntimeInfrastructureValidator.ValidateForBatchMode `
  -logFile "D:\Unity\Unity_Project\SleepyDemos\Temp\core-runtime-validator.log"
```

期望结果：
- 进程退出码为 `0`
- 日志包含 `Core Runtime Infrastructure 校验通过`

如果提示已有 Unity 实例打开同一项目，说明当前项目被 Editor 占用；不要强杀协作者正在使用的 Editor，改用菜单验证或等待项目关闭后再执行命令。

## UnitySkills 自动化验证

项目通过 UPM 依赖 `com.besty.unity-skills` 安装 UnitySkills，可用于在 Editor 已打开时执行自动化验证。

前置条件：
- Unity Editor 已打开当前项目。
- UnitySkills REST 服务已启动；菜单入口为 `Window > UnitySkills > Start Server`。服务正常时 `http://localhost:8090/health` 返回 `ok`。

可执行检查：

```powershell
$body = @{ menuPath = 'Tools/SleepyDemos/Validate Core Runtime Infrastructure' } | ConvertTo-Json
Invoke-RestMethod `
  -Uri 'http://localhost:8090/skill/editor_execute_menu' `
  -Method Post `
  -ContentType 'application/json' `
  -Body $body
```

期望结果：
- REST 返回 `success: true`
- Console 中出现 `Core Runtime Infrastructure 校验通过`
- `console_get_logs` 查询 Error 结果为 0

## PlayMode 验证

1. 打开启动场景。
2. 点击 Play。
3. 确认启动链路执行到主菜单：
   - `CoreEntrance`
   - `ResourceStartupState`
   - `BeforeHotfixStartupState`
   - `HotfixEntry`
   - `MainMenuView`
4. Console 不应出现资源初始化失败、热更程序集加载失败、AOT 元数据加载失败或 UI 预制体加载失败。

## 重点检查项

- Hotfix 和 UI 上层不直接依赖 YooAssets 的包、句柄或操作类型。
- `View` 默认通过 `ResourceServices.CreateLoader()` 创建 loader。
- `UITab`、`ViewList`、`UIBtnSwitch`、`UIDropdown`、`UIState` 类型可被 Core.Runtime 编译识别。
- `ViewList` 仍是基础有限列表，不包含循环列表或无限滚动逻辑。
- 文档入口包含资源运行时和 UI 运行时说明。
