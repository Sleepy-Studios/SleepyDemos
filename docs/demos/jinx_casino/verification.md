> 状态修正（2026-10-02）：下文为功能原型历史证据，旧文中的完成、正式不代表沉浸体验通过。当前执行S0–S5单机重做，先三款样板由用户确认，联网移出本轮。见[当前计划](implementation.md)与[沉浸进度](immersion-progress.md)。
# P1–P4 离线版本验证

验证日期：2026-10-02。按最新授权先完成离线内容，Photon、混合房间和真机性能后置；本文不作为完整长期Goal完成证明。

## 内容与直接相关回归

正式场景包括四区、17个固定机台与四个轮换展位、24道具、20事件、三种结局、三槽存档、练习/无尽和永久成长。全部规则、保存UI及正式资源都位于既有Hotfix与Demo目录，没有新增生产程序集或测试程序集。

| 范围 | 实际结果 | 证据 |
|---|---|---|
| 小游戏规则 | 35/35 | `d72bc339` |
| 冒险、配置、三槽持久化 | 23/23、11/11、4/4 | `915d09ac`、`df8a5909`、`6acf6592` |
| 公开表现、秘密竞价 | 13/13、5/5 | `77615073`、`a8e06818` |
| 成长与档案持久化 | 7/7、6/6 | `c6d9a6c7`、`6e9f59bb` |
| 偏好、真实协作帮助 | 8/8、7/7 | `1dded804`、`ee68ce84` |
| 65模型与manifest契约 | 分批覆盖66项通过，更新拉杆另1/1 | `cc39160d`、`95c8becf`、`b8bd1ffb` |
| 正式场景/HUD/音频绑定 | 3/3 | [最终结果](evidence/2026-10-02/Test-dc3abeb0.json) |
| 四区、17练习、三结局与档案返回 | 4/4 | [最终结果](evidence/2026-10-02/Test-b3648ca4.json) |
| P1实际按钮、存档、结算与返回 | 原3/4，剩方法精确1/1 | `b6756d1f`及[精确结果](evidence/2026-10-02/Test-61f70035.json) |
| 机台运动与无遮罩场地 | 1/1 | [最终结果](evidence/2026-10-02/Test-72f09780.json) |
| 拉杆按钮、提示灯、预备扳手、NPC动作 | 1/1 | [最终结果](evidence/2026-10-02/Test-685a2dd2.json) |

其余直接相关语义输入、机关、角色/音源、设置、任务早存档恢复及标记生命周期结果见[进度记录](progress.md)。所有自动化均由Unity Test Runner执行；未执行全量项目测试。Editor最后检查Console0错误。UI合成指针按屏幕像素点击，仍断言第一命中必须为保存按钮；未以直接调用监听器替代射线检查。

## 实际视觉证据

已查看正式720p四区、720p/20:9设置和20:9三结局。模型、中文、按钮、结局画幅与区域装饰可见；这些是Editor运行画面，不是Android真机截图。

| 四区 | 三结局 |
|---|---|
| [街角幸运厅](evidence/2026-10-02/area-street.png) | [体面离场](evidence/2026-10-02/ending-dignity.png) |
| [霓虹夜市](evidence/2026-10-02/area-market.png) | [接管狂欢城](evidence/2026-10-02/ending-takeover.png) |
| [机械奇术馆](evidence/2026-10-02/area-mechanics.png) | [狼狈撤离](evidence/2026-10-02/ending-withdraw.png) |
| [空中金库](evidence/2026-10-02/area-vault.png) | |

## Player与交付边界

Windows P4 x64 IL2CPP构建成功，版本0.4.0。分享ZIP重新解压启动：8个AOT元数据OK、Hotfix加载、Hub打开，未发现异常。未自动操控Standalone赌场完整流程，不能把Editor流程标成Player实玩记录。实际硬件为i7-12700KF、RTX3050 6GB、Windows11；本次未进行目标硬件帧时间基准。

- ZIP：`Builds/JinxCasino/P4/StandaloneWindows64/20261002031541-0ee336b5/JinxCasinoP4-Offline-Windows-Playable.zip`。
- 191163708字节、111条目，无DoNotShip辅助目录；SHA256：`584A5A1C1B443AA2D40024DE9E5EBAF2BA9C7EE81845386D5A597703308561C1`。
- [启动结果](evidence/2026-10-02/P4WindowsPlayer-0ee336b5-result.json)、[ZIP记录](evidence/2026-10-02/P4WindowsZip-0ee336b5.json)、[设置恢复](evidence/2026-10-02/P4Build-Windows-0ee336b5-restore.json)。

Android P4 ARM64 IL2CPP构建成功：`Builds/JinxCasino/P4/Android/20261002032344-2a4f64cd/Player/JinxCasinoP4-Offline.apk`，117505183字节，版本0.4.0、包名`com.sleepystudio.jinxcasino`、最低API26/目标API36。SHA256：`30A14FD77316D6973A01B8C2FB29870158A4976B84ABD98C0CAF9FB218778C82`。APK v2签名验证通过（Android Debug，朋友试玩）；六个原生库均ELF64/AArch64，69个内置JinxA文件，无其他ABI或Windows DLL。

- [版本与API](evidence/2026-10-02/P4Android-2a4f64cd-badging.txt)、[签名](evidence/2026-10-02/P4Android-2a4f64cd-signature.txt)、[资源与ABI](evidence/2026-10-02/P4Android-2a4f64cd-contents.json)。
- [Android构建后恢复](evidence/2026-10-02/P4Build-Android-2a4f64cd-restore.json)：与Windows相同四项SHA一致，原编辑器平台已恢复；Android构建后Windows分享ZIP的SHA仍一致。

ADB无设备，Fusion SDK未导入、App ID为空；没有Android真机或互联网联机证据。当前离线包沿用既有Hub资源采集与实验性原生插件排除配置；最终包体收敛及Windows渲染扩展属于后续打磨。完整长期Goal仍须实际跨平台互联网、两台Android真机、多人数和目标硬件性能验收。


## 独立页面整理验收（2026-10-05）

- `SettingsPreviewSaveAndCancelUseRealControlsAndKeepPause`：d0ec33f5，1/1；真实鼠标打开主菜单/暂停设置、手柄修改/取消、重复打开、独立页面销毁、来源焦点恢复、保存与未保存预览回滚、暂停时钟保持、返回 Hub。截图已检查。
- `IndependentWindows_CancelConfirmPreserveRunSlotsAndReleaseAtHub`：141edc5c，1/1；独立主菜单、空槽、教学重来确认/取消、跳过后选择正式局、槽覆盖取消不改文件、结局保存与返回、七个 UI 实例全部释放。结局数据用领域命令夹具，页面操作使用真实鼠标/手柄，不能替代现场离场流程。
- 旧广域入口方法 `SavedEntryStartsByRealInputFocusesSlotsAndRestoresCameraAfterBackAndPause`：dfd147df，在补给柜台附近的实际 A 移动断言超时；不标记通过。本次不修改场地碰撞或移动玩法。

运行范围仅上述定向 UI 方法及直接资源契约，无全量测试/构建。XML 位于本机 `Library/UIRefactor/Evidence`，仍需实体手柄、Android 触控与正式机台/教学全流程体验验收。
