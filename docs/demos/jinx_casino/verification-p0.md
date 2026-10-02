> 状态修正（2026-10-02）：下文为功能原型历史证据，旧文中的完成、正式不代表沉浸体验通过。当前执行S0–S5单机重做，先三款样板由用户确认，联网移出本轮。见[当前计划](implementation.md)与[沉浸进度](immersion-progress.md)。
# P0 验证记录

更新日期：2026-10-02。P0 尚未通过跨平台互联网验收，本文记录已执行的范围。

## 环境

- Unity 6000.3.15f1，HybridCLR 8.14.1、YooAsset 3.0.5。
- 本机 CPU：Intel Core i7-12700KF；物理 GPU：NVIDIA GeForce RTX 3050。
- Android SDK / NDK / OpenJDK 已安装；ADB 尚无已连接设备。
- 上述硬件信息用于复查环境，没有进行 GTX1650 或 Android 目标机性能验收。

## 自动化

所有用例通过当前项目 Unity Test Runner 串行执行，只运行直接相关范围。

| 测试范围 | 结果 | 最近通过作业 |
|---|---|---|
| CasinoSessionTests | 22/22 | f96b9660 |
| CasinoNetworkCoordinatorTests | 9/9 | d8907ab3 |
| NetworkRoomCodeTests | 15/15 | f2437b01 |
| OfflineLocalNetworkSessionTests | 9/9 | d92825a7 |
| JinxCasinoPrototypeContractsTests | 2/2 | 8f969b44 |
| JinxCasinoFlowTests.FormalStartupOfflineBetLeaveAndReentryCreateIndependentRun | 1/1 PlayMode | 965d3415 |
| JinxCasinoTouchInputTests | 3/3 PlayMode | 3f51472c |

独立用例合计57个EditMode、4个PlayMode；重复运行不另加用例数量，零用例发现失败不计入通过。未执行全量测试或第三方测试。

触控回归先执行失败基线`3e64161f`：1/3通过，540p相同屏幕比例的移动得到0.375而目标0.5，视角也未按屏幕高度换算。统一为720p参考像素后，作业`3f51472c`为3/3通过，覆盖540p/1080p同屏幕比例输入、两根手指独立占用、额外手指不能接管、视角只消费一次、松开/禁用/Reset清理与新指针重新接入。测试自动恢复Game View选择并清理临时对象，不保存用户场景。此为合成指针与实际Game View分辨率验证，不是Android真机手感验收。

规则验证包含投入与毛返还、非法请求、余额溢出、同编号重试、编号内容冲突、随机状态恢复及深拷贝。协调器验证包含可信发送者、旧权威消息、串行快照发布、失败回执、重建请求编号，以及挂起发布在权威交接时取消。

正式流程覆盖 AppEntrance → Hub → 赌场 → 显式单人离线局；投入100、返还200、余额1100；离房清空；返回Hub释放具体HUD与旧Controller；重新进入获得不同RunId与初始1000筹码；稳定时刻仅一个启用的AudioListener。

作业JSON、后续XML与运行截图位于 `Library/JinxCasino/Verification/`。该目录是本机验证证据，不参与Player首包。

## 运行截图

1280×720的 `P0OfflineFlow-20261001164306-fff4d5ccdb9e49a5a8e9e2ead8a9364d.png` 已查看：HUD中文字正常、退出入口可见、资金结果正确。随后局部调整世界招牌高度与Hub新按钮的专用字体引用。

资源生成时发现并修复专用字体缺字、材质_MainTex失效、TMP瞬态字体销毁时持久资源所有权，以及未保存临时场景不允许嵌套Additive的问题。原Samples场景和共享字体未保存或重写。截图仍为P0灰盒，不构成正式美术、触控或Player验收。

## Player 与互联网

Windows和Android产物结果以[当前进度](progress.md)与各产物随附说明为准。构建成功与运行成功分别记录，不能互相替代。

首次Windows事务`20261001164449-05acc9f7`完成HybridCLR生成，但SBP归档阶段28个资源缓存路径超过Windows长度限制，触发PathTooLongException。未生成正式Player；生成文件及内存配置恢复后，检查发现磁盘PlayerSettings残留三个临时字段，已按构建前值恢复；Editor原场景仍isDirty=false。构建工具已补原始文件字节备份、完整内存对象同步和平台恢复后再次核对。随后使用短工作路径、短包名与哈希资源文件名重新构建，失败目录保留作为证据。

首次事务还留下新建的空 `Assets/Settings/JinxCasino` 目录及.meta。删除该空目录的自动审批被拒绝，理由为blocked by policy，因此保留。后续事务记录其真实原状态，恢复时保留父目录本身，只还原子内容。

第二次Windows事务`20261001170108-18bee72f`完成内置资源包，长路径修复通过；正式Player的C++节点重复报告`chcp`无法识别而失败。尚无可运行ZIP。事务结束后磁盘ProjectSettings与Streamline插件.meta均无git差异；下一步核查编译子进程环境。

编译环境对照：Bee的734个C++节点均覆盖PATH且未包含System32；Unity父进程PATH和PATHEXT正常。用户cmd AutoRun含裸chcp；相同受限PATH下，普通cmd /c运行cl /?返回1，而cmd /d /c返回0。原报错节点.obj已生成，故不是普通C++编译错误。在受控Library工作目录放入本机System32的微软有效签名chcp.com，字节哈希一致，普通/c返回0且无该错误。后续构建工具仅为本次Windows构建临时提供该文件，记录创建者和哈希，拒绝覆盖已有不同文件，完成后清理自己创建的文件并恢复扩展名设置。

第三次Windows事务`20261001171709-15c9f851`构建成功。Player位于`Builds/JinxCasino/P0/StandaloneWindows64/20261001171709-15c9f851/Player`。构建完成后临时chcp.com已清理，ProjectSettings及Streamline.meta均无git差异。原ZIP含Unity的DoNotShip辅助目录，分享版本重打包时将排除这些目录，本机原目录与初始ZIP作为证据保留。

已后台启动生成的Windows Player（Win11、RTX3050、Direct3D12、1280×720窗口），日志`Library/JinxCasino/Verification/WindowsPlayer-15c9f851.log`确认JinxW使用BuiltinFileSystem，8个AOT元数据均OK、Hotfix程序集加载、MainMenuView主页面打开；启动观察后关闭自有验证进程。日志有Hidden/Universal Render Pipeline/DBufferClear不支持的shader报错，尚需Player视觉检查；没有在Player中执行赌场下注交互，不能用此记录替代Editor流程或真机验收。

只读URP检查支持该shader报错来自未启用DBuffer的全局引用：PC为Forward+、Mobile为Forward，均无DecalRendererFeature；预过滤移除三个DBuffer MRT关键词，唯一pass没有off分支，片元变体实际裁剪为0。GlobalSettings仍引用DBufferClear，报错早于Core启动及JinxW加载。此结论是静态依据支持的推断，Player场景视觉仍待检查，未修改全局渲染配置。

重打包分享版本`JinxCasinoP0-Offline-Windows-Playable.zip`大小180187958字节，105条目；包含运行exe、GameAssembly.dll、README与61个JinxW内置文件，没有DoNotShip辅助目录。原ZIP和本机调试目录保留。条目检查与SHA256见 `WindowsZip-15c9f851.json` 和 `WindowsZip-15c9f851-hash.json`。

已将分享ZIP实际解压至独立 `Library/JinxCasino/Verification/WindowsZipExtract-15c9f851`，从解压目录启动并再次确认8个AOT元数据OK、Hotfix加载、Hub打开；随后关闭自有验证进程。此证据说明ZIP可解压启动，日志为 `WindowsZipPlayer-15c9f851.log`，仍不包含赌场Player视觉与下注操作。

首次Android事务`20261001173159-de2a46b2`未能生成Hotfix DLL。错误来自Burst编译找不到AndroidExternalToolsSettings：磁盘新安装的Android扩展含该公开类型，但当前会话只注册Windows模块，实际编译响应文件未引用Android扩展。事务恢复后，仅有一个已保存且isDirty=false的原Samples场景，没有Play/Test/Build任务；关闭并重新打开同一个6000.3.15f1 Editor以挂载工具链，保留原场景和工作树。重启后日志已注册Android扩展，编译状态正常、原场景仍isDirty=false，第二次Android事务开始。

第二次Android事务`20261001173821-5dd73c3f`成功生成 `Player/JinxCasinoP0-Offline.apk`，101677887字节。aapt检查：包名com.sleepystudio.jinxcasino.p0，versionCode1、versionName0.0.1、minSdk26、targetSdk36，仅arm64-v8a；GameActivity为UnityPlayerGameActivity，方向sensorLandscape。apksigner验证v2签名通过，Android Debug签名；此为朋友安装验证用途的开发包。APK共有542条目、61个JinxA内置文件、6个ARM64原生库，无WindowsDLL。SHA256为AC95C1B424B5E7193741B1687AED6EF9A246D6B4C3794B77CBE03BF109C8AC2E。ADB设备列表为空，未安装运行、未验收Android触控或性能。

Android构建后检查发现URP自动改写Mobile_RPAsset的预过滤元数据及GlobalSettings运行资源列表；两文件构建前无用户改动，已将本次自动写入恢复为原值。构建工具已补充实际使用的URP配置内存、源文件和.meta快照保护，不全局保存其它dirty资产。

第三次Android事务`20261001180020-f9726643`再次完整构建成功，产物101677883字节，SHA256为98DBB82C11BA52E488A375299E5B945AE1ECD8E977760BAFBDAFCB320AD1E9B5。aapt与apksigner再次确认API26/target36、仅ARM64和v2签名。构建前后ProjectSettings、Mobile_RPAsset、GlobalSettings与Streamline.meta四文件SHA256均一致，证据为`AndroidRepeat-baseline.json`与`AndroidRepeat-f9726643-restore.json`。原Samples场景仍isDirty=false，原Windows平台已恢复。

此次恢复过程产生一条Editor错误：SaveToSerializedFileAndForget不接受已持久化的PlayerSettings对象。完整内存恢复及原文件字节恢复已实际完成，但该调用不合法；随后移除该多余保存调用，保留内存恢复、ClearDirty、原字节恢复及一致性校验。此局部修复通过后续Editor编译检查，当前Console错误数为0，不重复整次Player构建。旧恢复错误与Windows日志的DBufferClear记录保留在原始验证日志中，不能据此声称Player视觉已通过。

以下验收尚未完成：Fusion SDK接入及Weaver构建、真实Photon房间、不同网络、1/2/4/6人混合队伍、协调者离房、重连、两台Android真机、安全区与双指触控、后台恢复、连续30分钟，以及目标硬件帧时间和内存。

触控修复后的Windows事务`20261001182052-8e8edf27`构建成功。开始前两次签名校验失败，补充stderr后确认校验子进程无法自动加载Microsoft.PowerShell.Security，而独立检查系统chcp.com签名为Valid。工具改为显式加载系统模块，仅在校验子进程内限定PSModulePath；没有更改全局环境或放宽签名要求。新Playable ZIP为180189354字节、104条目、61个JinxW内置文件、无DoNotShip，SHA256为E9C53FE566574E5CD65914177890C7372CA682242F2832BA3FF92CC34FE2DCEF。实际解压启动确认8个AOT元数据OK、Hotfix加载、Hub打开，并已关闭自有验证进程；仍存在已记录的DBufferClear日志，Player赌场视觉未验收。构建恢复后四个配置文件哈希与`TouchBuild-baseline.json`一致，原场景isDirty=false、Console错误0，临时chcp.com已清理。证据为`TouchBuild-Windows-8e8edf27-restore.json`、`WindowsZip-8e8edf27.json`及`WindowsZipPlayer-8e8edf27-result.json`。

触控修复后的Android事务`20261001182809-feb0a2a6`构建成功，APK为101678051字节，SHA256为A63F9ED3D16A03C2F1D3CADCDD8999C7ABCC099F08816CFED8E5D34D4B4DF568。aapt与apksigner再次确认API26/target36、仅ARM64和有效v2签名；542条目、61个JinxA内置文件、6个ARM64原生库，无WindowsDLL。完成后恢复原Windows平台、原Samples场景且isDirty=false、Console错误0；四个配置文件哈希与`TouchBuild-baseline.json`一致。证据为`Android-feb0a2a6-badging.txt`、`Android-feb0a2a6-signature.txt`、`Android-feb0a2a6-contents.json`、`TouchBuild-Android-feb0a2a6-restore.json`与`P0-touch-final-editor-state.json`。ADB仍无设备，未安装运行，未开展真机触控或性能验收。Android GameActivity生成的根目录`.utmp`仅作为本机构建缓存保留，已加入.gitignore，不进入源码交付。
