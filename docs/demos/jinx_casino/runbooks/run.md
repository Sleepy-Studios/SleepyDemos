# 双平台运行与依赖

## 前置依赖

1. Unity 6000.3.15f1 与当前项目子模块。
2. Windows IL2CPP 支持和构建工具。
3. 同一 Editor 的 Android Build Support、SDK / NDK Tools 与 OpenJDK。
4. 当前单机验收需要 Windows Player、Android 真机与 Xbox 手柄；设备证据单独记录。

联网不属于当前单机验收，选型待定；旧 Photon 配置不作为开发前置条件。

## S1独立样板装配（开发中）

`Tools/SleepyDemos/整蛊赌场/沉浸样板/创建独立场景与薄HUD`首次保存`Scenes/Immersion.unity`和`JinxCasinoImmersionHudView.prefab`；已有文件不会整包重建，保护人工修改。当前工作分支的Hub赌场入口已切换到Immersion；旧Main场景保留用于恢复原型。新入口与完整流程仍需分别记录实际验证。

`沉浸样板/更新样板玩法范围`只更新独立`Data/ImmersionSettings.asset`的一区、三机台和暂时关闭事件配置，不重建场景或修改旧AdventureSettings。后续接入事件设施后应同步调整本入口与保存资源测试。`沉浸样板/更新交互绑定`分步更新本地胶囊射线层和动态铭牌行高，不重建布局。菜单回显不证明保存成功，核查保存资源和`JinxCasinoImmersionSceneTests`结果。

样板初始开放Slots、Blackjack、CooperativeLevers；`InitiallyAvailableGames`仍受`AllowedGames`限制，旧存档缺失该字段时保持原区域解锁。现有自动化覆盖保存引用，不代替三输入实玩、画面或S1用户验收。

## 构建约束

Windows / Android 分别生成对应 HybridCLR 元数据、Hotfix DLL 和 YooAsset 内置包，平台输出隔离。Android 不得使用 StandaloneWindows64 的元数据快照，也不得依赖本机 Mock Server。

Build Settings 继续只保留 AppEntrance。项目已打开时不另起 BatchMode，不用 dotnet build / msbuild 构建 Unity 工程。

## P0 原型装配与离线构建

历史原型恢复使用`生成P2四区冒险`；正式资源通过模型门禁后使用`生成P4正式离线冒险`。两者先将本Demo现有资源和Hub入口备份到`Library/JinxCasino/ContentBaseline`，在独立Additive临时场景装配，成功或失败都恢复用户原活动场景。工具会重新生成本Demo保存模板，不用于覆盖尚需保留的人工美术编辑。

正式装配必须核对Console/Editor日志和场景中实际`FormalAvatar`、各机台`FormalVisual`与音源；菜单执行回显不能证明装配成功。P4构建入口为`构建Windows P4离线试玩包`和`构建Android P4离线试玩包`，拒绝尚未装配正式角色的源场景。P4输出在`Builds/JinxCasino/P4/{target}/{version}`，版本0.4.0、标识`com.sleepystudio.jinxcasino`，保留原P0目录及应用标识。构建事务及平台恢复沿用下述经过验证的流程；是否构建成功以最新实际验证记录为准。

从 `Tools/SleepyDemos/整蛊赌场/生成P0原型与Hub入口` 生成保存的原型场景、HUD 与 Hub 按钮。已有手工场景/UI修改时不要重新生成；该工具会覆盖本 Demo 原型资源，保留网络设置资产。

`Tools/SleepyDemos/整蛊赌场/P0离线Player构建` 提供 Windows / Android 构建入口。构建在当前 Editor 串行执行，利用 SessionState 跨目标平台 Domain Reload 继续。必须先完成脚本编译，所有打开场景必须已保存；工具不代为保存或丢弃。

也可直接选择以下菜单启动相应平台事务，无需先打开窗口：

| 菜单 | 产物 |
|------|------|
| `Tools/SleepyDemos/整蛊赌场/构建Windows P0离线验证包` | Windows x64 IL2CPP Player 与 ZIP |
| `Tools/SleepyDemos/整蛊赌场/构建Android P0离线验证包` | Android 8.0 / API 26 及以上的 ARM64 APK |
| `Tools/SleepyDemos/整蛊赌场/重打包最近Windows P0分享包` | 不重新编译，过滤Unity调试目录并生成新的Playable ZIP |

两种入口共用同一任务与恢复流程，不能同时启动。离线构建不要求 Fusion SDK 或 App ID；后续互联网版本另行选型。Windows P0 不包含 Streamline 原生 DLL，Android 不生成 ARMv7 / x86 包。

Android P0仅允许横屏左右旋转；原项目方向设置由构建事务的PlayerSettings快照恢复。

构建为单人离线验证包，验收范围是已实现的 P0 场景/规则/UI能力，不构成互联网联机或完整游戏证据。Windows ZIP / Android APK 输出到 `Builds/JinxCasino/P0/{target}/{version}`；从 Hub 点击“倒霉蛋俱乐部 · P0”，再选择“单人离线验证”。

P0资源采集暂复用现有DefaultPackage的公共资源与Demo资产，并替换为本平台专用代码组；因此产物仍带有Hub和其他既有Demo资源。最终赌场交付需收敛采集范围并重新验证体积，不以P0包体积作为最终结果。

平台 DLL、首包、构建场景配置隔离；不修改默认 HotfixConfig 或 Build Settings。构建中处理器只改 Player 的 AppEntrance 内存场景配置。任务结束恢复被覆盖的生成文件、资源首包、Collector 内存与 Editor/Player 设置，备份保留于 `Library/JinxCasinoBuild`。若恢复失败，停止后续步骤并保留恢复信息；不要继续另起构建或删除备份。

URP 构建预处理会保存目标平台的着色器预过滤配置，并可能裁剪 GlobalSettings 的运行时设置列表。事务只备份目标平台启用质量级别所使用的 URP 资产与注册的 URP GlobalSettings，保留其内存 JSON、dirty 状态和原始 `.asset` / `.meta` 字节；恢复时重载对象，并在原平台恢复后再次校验原字节。备份仅在本机 `Library` 中，不进入 APK 或分享 ZIP。

Windows构建会校验本机System32的chcp.com签名和哈希，再临时提供给Bee的受限PATH编译进程，事务结束只清理自己创建且哈希一致的副本。校验使用系统Windows PowerShell及其安全模块，PSModulePath仅在校验子进程内限定为系统模块目录；从PowerShell7启动Editor也不依赖其模块自动加载。不修改用户cmd AutoRun、全局PATH或模块路径。

Windows P0已有实际构建与正式入口启动记录；Android、Photon与真机进度以[进度与验证](../progress.md)中实际证据为准。分享Windows版本使用Playable ZIP，开发机保留的DoNotShip目录和初始ZIP不需要分发。
