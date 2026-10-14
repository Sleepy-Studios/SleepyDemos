# 2026-10-14 纸艺卡丁车性能记录

## 常态音频窗口

正式 `TaskAudioCapture`（e27d58e0）1/1 通过，真实 `AudioListener.GetOutputData` 非零输出断言通过。四份报告均为 Unity 6000.3.15f1 Editor、草原、120g、本车原作 AI 自动驾驶与七名 AI；每个条件重新开始本场，在正常倒计时起跑后录制 8 秒。Game View VSync 关闭，分析阶段暂停比赛，正式 Profiler 分析全部线程，未开启 Deep Profiling 或 Profile Editor，比赛随机种子未固定。

`off` 实际关闭 DSP 载体 `AudioSource.enabled`，其余三个条件启用载体并调用 `Play()`；它不是单纯把滑块或全局音量设为零。音乐和音效的本地音量分别为 0.7、0.9，按条件将另一类设为零，Demo 静音关闭、Editor 全局音频静音关闭；退出恢复原 Editor 静音及 Game View VSync。本轮非零输出断言在全部开启条件执行，不能据此断言每一个音效已逐声隔离试听。

分位统一采用正式 `ProfilerAnalysis` 的 nearest-rank：将全部有效帧耗时排序，取 `ceil(帧数 × 分位)-1`。以下 P95/P99 不使用早期整理过程中的线性插值结果。

| 条件 | 帧数 | P95 | P99 | 最大帧 | 全线程 GC.Alloc 字节 |
|---|---:|---:|---:|---:|---:|
| 载体关闭 | 939 | 10.019 ms | 15.578 ms | 40.999 ms | 209,776 |
| 仅音乐 | 935 | 10.048 ms | 13.120 ms | 25.353 ms | 379,059 |
| 仅音效与引擎 | 904 | 10.244 ms | 15.736 ms | 20.404 ms | 249,132 |
| 音乐、音效与引擎全部开启 | 914 | 10.389 ms | 15.415 ms | 18.348 ms | 238,546 |

四个窗口的 GC 未知大小事件均为 0；表中的分配量包括 Editor、公共模块、测试 Runner 和后台线程，不表示音频或卡丁车独占分配，也不表示 GC 回收暂停耗时。帧耗时使用报告 `frames.timeMs`，不将各线程 CPU Total 相加。

原报告精确归档至独立美术源仓库的 `ArtSource/kagit_kart/Previews/Audio_20261014/`，未将大体积 `.data` 加入游戏仓库：

- [载体关闭：20261013_202638_b92f35bc](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Audio_20261014/20261013_202638_b92f35bc.json)
- [仅音乐：20261013_202718_421b8308](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Audio_20261014/20261013_202718_421b8308.json)
- [仅音效与引擎：20261013_202759_37afd3b0](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Audio_20261014/20261013_202759_37afd3b0.json)
- [全部开启：20261013_202843_1ea5f465](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Audio_20261014/20261013_202843_1ea5f465.json)

## 音频耗时与同步

以下均为单次调用平均值，即对应 Marker 的 Total 或 Self 除以 Calls；主线程帧与音频块调用频率不同，不能互当平均每帧成本。

| 条件 | Audio.Update Total / Self | Engine.ControlSync Total | Audio.BlockSync Total | Audio.AudioBlock Total |
|---|---:|---:|---:|---:|
| 载体关闭 | 33.10 / 27.91 µs | 0.866 µs | 无录制调用 | 无录制调用 |
| 仅音乐 | 32.61 / 27.71 µs | 0.800 µs | 5.255 µs | 3.026 ms |
| 仅音效与引擎 | 33.12 / 28.13 µs | 0.833 µs | 5.645 µs | 3.108 ms |
| 全部开启 | 34.76 / 29.41 µs | 0.892 µs | 5.278 µs | 3.051 ms |

`KagitKartAudio.Update()` 与 `KagitKart.Engine.ControlSync` 位于 Main Thread，后者是前者的子级；`KagitKart.Audio.BlockSync` 和 `KagitKart.Audio.AudioBlock` 位于 Audio Mixer Thread。表中这些 Marker 的 GC 均为 0。完整音频块包含 `KagitKart.Engine.AudioBlock`，其三个开声条件平均约为 3.023、3.104、3.053 ms；不能把引擎子级再加到完整块上。全部开启时父子 Calls 略有差异，也不能据平均值之差计算精确的非引擎 DSP 成本。

仅音乐条件仍计算引擎 DSP 再乘音效增益，内部时钟、相位和滤波状态继续推进；声音听不见不等于该分支没有 CPU 成本。当前报告支持音频块约 3 ms、声部快照约 5 µs 的常态成本，不证明所有回调都没有阻塞或目标设备上不会超出音频期限。

## 线程等待与 GC 归属

四份报告顶部的 `Semaphore.WaitForSignal` 主要出现在 EnlightenWorker、Profiler Dispatcher/DataDeserializer、D3D12 Worker、Render Thread 等线程。工具会将同组同名线程聚合，因此出现超过整个 8 秒窗口的累计等待时间；它不是 Main Thread 单帧卡住这么久，也不能作为音频大锁等待的证据。`GfxDeviceD3D12.WaitForGPU` 属于呈现或 GPU 同步等待，不直接视为音频计算。

全部开启窗口的 Main Thread `WaitForJobGroupID` 累计 Total 79.351 ms、Self 61.572 ms；`TimeUpdate.WaitForLastPresentationAndUpdateTime` Total 9.203 ms、Self 3.862 ms。这些是整段累计值，应回到具体慢帧和样本祖先链解释，不能凭名字推断等待对象就是音频锁。

全部开启的主要已记录 GC 路径包括 Thread Pool Worker `<unnamed>` 91,672 字节、测试 Runner 协程 73,120 字节、`KagitKartWorld.LateUpdate()` 63,092 字节，以及 `UnitySynchronizationContext.ExecuteTasks()` 6,240 字节。未命名后台路径没有可用源码归属，不能补造；仅音乐和仅音效窗口还记录了 Editor 的材质属性处理分配。上述总量与音频 Marker 的 GC=0 是不同层级的证据。

## 源码修复与证据边界

本轮修复的两条具体路径已由源码确认：旧 `KagitKartAudioDsp.OnAudioFilterRead` 将整块声部混音放在主线程播放、停止和样本交换共用的锁内；旧末圈和结算曲首次播放会在主线程同步准备 PCM、分配大数组并执行 `AudioClip.GetData`，即使设置未来 DSP 播放时间，这些准备工作也不会延后到音频线程。

当前实现改为音频块起点短锁复制控制参数和 PCM 引用，逐采样混音在锁外推进；当前章开场、比赛、末圈、终点、结算与领奖共 14 首提前准备，同章菜单和重赛复用，公共 7 首跨章保留，换章只解除不再需要的章节预留，退出释放全部缓存。进行中的同章请求通过官方 UniTask `AsyncLazy` 共用。公共音量继续复用现有总线，不新建全局 AudioManager。具体缓存预算、样本引用安全与维护步骤见[音频维护](audio.md)。

以上证明修复了会阻塞关键事件的实现路径；旧大锁没有事件前后配对等待证据。后续事件采样已确认同步 PCM 冷路径，见下节分阶段记录，但不能将它称为用户所有严重掉帧的完整实测根因。未改动逐采样引擎滤波、软削波和动力学算法来换取未经证明的收益。

这组起跑短窗未复现常态严重掉帧。此前基线在同一场依次切换开关、比赛阶段不同且只分析 Main Thread，不能与本轮全线程报告直接计算改善百分比；本轮四组不同随机轨迹也不能把最大帧差异归因于音乐或音效开关。后续正式事件采样单独记录，不把 8 秒常态窗口当作完整三圈、资源/布局峰刺、Player 或真机验收。


## 同步、后台与分页阶段

三个阶段的资源准备、UI 预热、Profiler 窗口与运行轨迹不同，保留各阶段原始证据，不计算全流程改善百分比。

1. **同步 PCM 阶段**：[40bbeee6 回执](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/40bbeee6_before_partial_timeout.json)达到 20 分钟上限，在书桌成绩窗口分析时中断。前三章和书桌准备、末圈可用，但不能称四章通过。草原成绩窗口 [20261013_221110_5111ac3c](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_220646/20261013_221110_5111ac3c.json)的 frame 1707 为 563.041 ms；正式单帧原始层级确认 `PreparePcm` Total 546.7255 / Self 113.5217 ms，内部 `SoundManager.GetHandle → LoadFMODSound` Total 408.5124 / Self 402.0088 ms，直属 GC.Collect 21.9935 ms，GC.Alloc 105,840,032 B。[精确祖先路径](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_220646/meadow_before_frame1707_paths.json)及同目录分页原始 JSON 保留。这是一次实测同步音频冷路径，不能推广成所有掉帧的唯一原因，也没有测出旧 DSP 大锁的配对等待。
2. **后台加载、连续大数组诊断**：[ea04d563 回执](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/ea04d563.json)1/1，[博斯普鲁斯准备报告](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_225427/20261013_225527_caaee7f6.json)895 帧、0 裁剪，实际 Ready 2.996847 秒，PCM 372,886,520 B。主线程 LoadFMODSound 14 次 Total 4.3339 / Self 1.357 ms；PreparePcm 14 次 Total 211.0395 / Self 181.1251 ms。frame 121 仍为 60.2361 ms、全线程 GC 105,840,268 B，后台解码没有消除一次大数组准备。[该帧原始样本首段](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_225427/bg_flat_frame121_0000.json)及后续分段保留。
3. **后台加载、4 MiB 分页完整四章**：下列矩阵属于正式 [16f564bb 回执](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/16f564bb.json)，1/1，通过完整四章及领奖台。后续已将生产页上限收缩到 1 MiB；新聚焦复测须单独记载，不能将下表标成 1 MiB 结果。

## 4 MiB 分页完整事件矩阵

经正式 AppEntrance 进入 GP，120g、八车、本车原作 AI 自动驾驶、120 Hz 自然 Update/AI，未手动推进圈数、成绩或事件。每章开赛或下一章命令前录 8 秒准备窗；第二圈归一化进度约 1.90 前录 12 秒末圈窗，第三圈约 2.88 前录 20 秒过线／成绩窗，领奖命令前录 12 秒。实际 finishWait 配置 12 秒。Editor 音频开启，48 kHz，VSync 关闭，分析时暂停；正式 Profiler 全线程、无 Deep Profiling/Profile Editor，比赛未固定随机种子。作业约 23 分钟。

全部 13 个窗口 0 裁剪，历史帧上限暂设 3000；结束恢复原历史设置、Profiler 数据与录制状态、音频静音及 VSync。所有窗口 Listener 都有实际非零输出。真实主车末圈、过线、raceEnd、resultsVisible 与 podiumVisible 均保留在相应窗口；`profilerLastFrame` 可能滞后一帧，不当作 Unity 事件帧的精确映射。热下一章 roundReady 可在第一帧被 Profiler 接收前同步完成、邻接索引为 -1，不能为它伪造单帧索引。

命令发出至实际 roundReady 的墙钟等待分别为草原 3.313088 秒、夜景 0.018363 秒、冰川 0.012612 秒、书桌 0.014162 秒。后三章在前一章成绩页面已完成准备，短等待不表示新曲解码和复制没有成本；这些工作见相应前一章成绩窗口。首次菜单也有预热，因此草原值不代表整个 Demo 完全冷启动耗时。

| 窗口及报告 | 保留帧 | P95 ms | P99 ms | max ms | 全线程 GC.Alloc B |
|---|---:|---:|---:|---:|---:|
| [草原 准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_230955_c7a9eee5.json) | 792 | 11.920 | 51.870 | 457.769 | 258,854,498 |
| [草原 末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_231206_826823ef.json) | 1323 | 10.778 | 13.354 | 25.951 | 464,724 |
| [草原 完赛／成绩](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_231341_8ceff477.json) | 2084 | 11.699 | 14.533 | 74.153 | 252,602,870 |
| [夜景 准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_231455_0773749b.json) | 912 | 10.917 | 12.966 | 57.651 | 1,246,402 |
| [夜景 末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_231708_e39f7c25.json) | 1341 | 11.050 | 12.620 | 19.976 | 356,158 |
| [夜景 完赛／成绩](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_231850_39055c65.json) | 2260 | 11.108 | 13.376 | 63.631 | 190,856,414 |
| [冰川 准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_232004_4df6578f.json) | 1022 | 8.896 | 10.450 | 148.389 | 1,128,370 |
| [冰川 末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_232216_6dcb454d.json) | 1416 | 9.869 | 12.901 | 20.446 | 363,654 |
| [冰川 完赛／成绩](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_232402_b8cffb27.json) | 2453 | 9.730 | 11.962 | 77.610 | 175,013,372 |
| [书桌 准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_232516_f82160b6.json) | 978 | 10.503 | 12.243 | 56.570 | 1,230,804 |
| [书桌 末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_232735_ef195c5e.json) | 1408 | 11.061 | 13.412 | 81.779 | 617,289 |
| [书桌 完赛／成绩](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_232919_c71c2c7e.json) | 2325 | 10.639 | 15.186 | 49.030 | 641,113 |
| [书桌 领奖](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/20261013_233038_ce7d212b.json) | 1634 | 9.476 | 10.841 | 35.407 | 471,858 |

GC 表示全线程分配，13 个窗口未知大小 GC 事件均为 0，不表示 GC 回收耗时或 DSP 分配。最大帧保留真实值，不从分位或窗口聚合推断其单帧原因。四个末圈窗、书桌成绩及领奖均未记录 PreparePcm 或 LoadFMODSound；下一章新曲在前一章成绩窗准备，书桌后直接复用已预留的领奖曲。

### PCM 与进程内存

| 窗口 | 预留 PCM peak / end MiB | Unity 已分配 peak GiB | Mono 已用 peak GiB | Listener peak |
|---|---:|---:|---:|---:|
| 草原 准备 | 225.25 / 225.25 | 2.270 | 2.017 | 0.160582 |
| 草原 末圈 | 225.25 / 225.25 | 2.907 | 2.050 | 0.487227 |
| 草原 完赛／成绩 | 355.61 / 355.61 | 3.837 | 2.275 | 0.593379 |
| 夜景 准备 | 355.61 / 355.61 | 3.819 | 2.118 | 0.250964 |
| 夜景 末圈 | 355.61 / 355.61 | 2.945 | 2.123 | 0.412865 |
| 夜景 完赛／成绩 | 355.61 / 297.21 | 4.092 | 2.213 | 0.461234 |
| 冰川 准备 | 297.21 / 297.21 | 4.070 | 2.076 | 0.274672 |
| 冰川 末圈 | 297.21 / 297.21 | 3.068 | 1.986 | 0.427918 |
| 冰川 完赛／成绩 | 297.21 / 287.24 | 4.328 | 2.143 | 0.493289 |
| 书桌 准备 | 287.24 / 287.24 | 4.303 | 1.971 | 0.389921 |
| 书桌 末圈 | 287.24 / 287.24 | 3.078 | 2.062 | 0.376933 |
| 书桌 完赛／成绩 | 287.24 / 287.24 | 4.158 | 2.076 | 0.395129 |
| 书桌 领奖 | 287.24 / 287.24 | 4.126 | 2.004 | 0.428688 |

PCM 为已完成且预留的自有样本，不包含正在复制的部分页、页数组对象头、标题／短音效／噪声／Boost、正在播放但已解除预留的过渡声部、整首 Native 解码副本或待回收对象。夜景和冰川成绩窗口的 peak 包含开始时的旧章预算，end 对应下一章；不是同时预留两整章。精确章字节为草原 236,191,472、夜景 372,886,520、冰川 311,650,520、书桌 301,188,464 B。

Unity 和 Mono 由 Profiler.GetTotalAllocatedMemoryLong / GetMonoUsedSizeLong 逐帧观察，是当前 Editor 进程值，包含引擎、Editor、Profiler 和测试，不能当卡丁车独占内存或相加为产品预算。首次准备前菜单已有预热，后续窗口在准备完成后开始；本表不证明完全冷启动峰，也不能与不同 Editor 缓冲状态的旧窗口直接算内存改善百分比。

### 音频线程、主线程准备与等待

以下平均均按正式报告记录的 Calls 计算，音频块与主线程帧频不同。BlockSync Total 包括同步快照及可能的锁等待，不是单独测出的纯锁阻塞；音频块已包含引擎子级，不将父子耗时相加。

| 窗口 | AudioBlock 平均 ms/call | BlockSync 平均 µs/call | 主线程 PreparePcm Calls / Total ms |
|---|---:|---:|---:|
| 草原 准备 | 2.823 | 4.523 | 63 / 153.961 |
| 草原 末圈 | 3.076 | 4.221 | 无记录 |
| 草原 完赛／成绩 | 3.108 | 4.731 | 63 / 314.331 |
| 夜景 准备 | 3.031 | 4.941 | 无记录 |
| 夜景 末圈 | 3.024 | 4.779 | 无记录 |
| 夜景 完赛／成绩 | 3.012 | 4.467 | 48 / 238.398 |
| 冰川 准备 | 2.719 | 4.210 | 无记录 |
| 冰川 末圈 | 2.894 | 4.114 | 无记录 |
| 冰川 完赛／成绩 | 2.868 | 4.143 | 45 / 230.917 |
| 书桌 准备 | 2.878 | 4.190 | 无记录 |
| 书桌 末圈 | 2.983 | 4.194 | 无记录 |
| 书桌 完赛／成绩 | 2.797 | 4.041 | 无记录 |
| 书桌 领奖 | 2.686 | 4.078 | 无记录 |

13 份报告均未录得 Audio Mixer Thread 的命名 Wait/Semaphore/Sleep Marker；上述 DSP Marker 未录得 GC.Alloc。这不证明线程从不等待、设备从不超时，不能将其他线程的 Semaphore.WaitForSignal、呈现等待或跨线程累计时间当作音频锁阻塞。当前音频块报告平均约 2.686–3.108 ms/call，快照约 4.041–4.941 µs/call。

| 准备发生窗口 | PreparePcm Self ms | PCM 路径 GC.Alloc B | LoadFMODSound Calls / Total ms |
|---|---:|---:|---:|
| 草原 准备 | 134.178 | 236,193,964 | 14 / 3.608 |
| 草原 完赛／成绩 | 290.486 | 246,216,230 | 7 / 1.324 |
| 夜景 完赛／成绩 | 214.495 | 184,979,746 | 7 / 0.755 |
| 冰川 完赛／成绩 | 209.283 | 174,517,588 | 7 / 1.237 |

PreparePcm 每页一次，四个短曲的连续数组路径每次缓存缺失一次；Calls 不等于 Clip 数。草原首次 63 次包含 59 个音乐页及 4 首短曲；随后三次新章分别 63、48、45 次，公共 7 首 PCM 保留而不重新复制。Pcm 的分配和 Native 解码已分属不同阶段，不能拿当前 PreparePcm Total 与旧同步 Total 当作相同工作。

### 已确认残峰与后续 1 MiB 验证

4 MiB 完整版本仍有真实残峰，并未宣布所有准备帧低于 16 ms。正式原始单帧精读确认：

- 草原准备 frame 54：PreparePcm Total 41.1927 / Self 21.5010 ms，直属 GC.Collect 19.6881 ms，GC.Alloc 4,194,336 B。[原始首段](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/paged_prepare_frame54_0000.json)及同名后续分页。
- 草原成绩／下一章预热 frame 1346：整帧 74.1532 ms，PreparePcm Total 60.9614 / Self 37.1885 ms，直属 GC.Collect 23.7715 ms，GC.Alloc 4,194,336 B。[原始首段](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/paged_results_frame1346_0000.json)及同名后续分页。
- 同窗口 frame 1297：整帧 57.7372 ms，原始样本将主要耗时定位到 YooAssetsDriver.Update Total 47.4954 / Self 43.1271 ms，不能归到 PCM。[原始首段](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261013_230919/paged_results_frame1297_0000.json)。更细的资源调用归属以主代理后续精读为准。

PreparePcm 的 Self 同时可能含数组分配／清零、GetData 等工作，未再分段测量，不能全部称为 memcpy。两次直属 GC 是已确认的音频准备暂停；分页虽移除了单次约 100 MB 分配，4 MiB 仍没有完全解决这两类峰刺。其余慢帧只记录数值，不凭聚合路径臆测单帧成因：夜景成绩 1597/1698/1699、冰川成绩 1885、书桌末圈 0/166 等待正式原始样本定位。

因此生产页上限进一步改为 1 MiB，仍逐页让出帧、全部曲完成才开赛。此选择在缩小单次主线程工作和增加准备帧数之间取中间值，不更改 Native 音源导入或 GC 模式。1 MiB 与 256 KiB 都不保证直属 GC 暂停同比缩短；不能线性预测后者收益。现有音色测试输入边界同步到 1 MiB，仍核跨页、循环／非循环末尾的双声道插值。下述 12+1+1 契约测试及最后单独记载的首章／下一夜景聚焦采样已完成，4 MiB 的完整四章通过不能替代 1 MiB 的全四章复测。

1 MiB 测试作业 12818163 终态 12/14，Manifest 和分页音色两项各触发 180 秒超时，由测试保护自动恢复 Native Runner。UniTask.NextFramePromise 要求 Time.frameCount 改变，EditMode 的真实帧推进与后台音频加载不稳定。手动 EnterPlayMode／ExitPlayMode 重试 57045ceb 为 13/14，音色通过，但 Manifest 跨域重载出现 NullReference；这是测试环境／生命周期失败，不写成产品音频失败或整组通过。

两项播放检查及其 DTO 正式迁入既有 ScenePlayModeTests，去掉手动跨域切换；纯算法与缓存继续在 EditMode。当前 1 MiB 正式分组全部通过：[Edit Audio a18056b4](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/a18056b4.json)12/12、[Play Manifest 499d80b3](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/499d80b3.json)1/1、[Play Tone c93d3b10](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/c93d3b10.json)1/1。生产 DSP 的让帧语义保持不变；先前 88e2fb42 当次 14/14 与完整 GP 的 16f564bb 保留各自阶段，当前 1 MiB 性能范围见最后聚焦节。

原始 .data、metadata、13 份报告、事件 JSON 与截图均在外部 `Previews/Events_20261014/20261013_230919/`，不把大数据加入游戏仓库。此结果仍为 EditorSimulateMode；不是 Player/IL2CPP、真机、真实手柄或触屏性能通过。

## 1 MiB 首次聚焦失败边界

[b54e56df 回执](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/b54e56df.json)0/1；[草原准备报告 000148_9be4b915](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_000116/20261014_000148_9be4b915.json)8 秒仅录 100 帧，未 roundReady，不能记录为有效完整准备或新版本性能通过。每页让出帧需要真实帧推进；本次缺少完成曲目，不据帧数直接判定页数或音频是低帧率的原因。

整窗帧耗时合计 7,947.958 ms；frame 0/1/2/44 分别 2,057.558、2,041.162、695.869、998.831 ms，四帧合计占约 72.9%。全窗中位 13.464 ms，其余 96 帧平均 22.443 ms。主线程 EditorLoop Self 累计 5,519.456 ms，Mono.JIT Self 807.144 ms，另有一笔 Profiler.SessionMetadata 与 PlayerLoop 之间未归属时间 624.933 ms；未启用 Profile Editor，不能由这些聚合 Marker 确定巨型 Editor 帧内部原因。

未录得 WaitForTargetFPS 或 GC.Collect；主线程 TimeUpdate.WaitForLastPresentationAndUpdateTime Total 0.897 ms、D3D12 呈现等待 0.189 ms。Render Thread 的 GPU Wait 总计 50.642 ms，不能视为主线程单帧阻塞。全线程 GC 49,142,322 B，最大单帧 2,413,392 B。只完成部分准备：主线程 PreparePcm 28 次 Total 19.5356 / Self 19.5150 ms，约 0.698 ms/page，GC 28,423,504 B；这些成本不能外推成整章耗时或无峰刺结论。

此版资源解析 Marker 实际位于 Thread Pool Worker：Resources.ParseTrack 14 次 Total 185.606 / Self 143.932 ms、GC 6,553,570 B；Resources.ParseAiPlan 8 次 Total 76.997 / Self 55.986 ms、GC 2,681,513 B。主线程没有这两项 Marker，线程累计不相加为主帧时间。YooAssetsDriver、文件头及序列化对象读取仍有冷资源成本；代表性巨型帧与 Editor 内部来源须先精读并恢复可比条件后重采，不能把此次失败当作新音频性能成功。

后续正式原始单帧精读将巨型时间定位到 Editor 层级：[frame 0](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_000116/startup_frame0_0000.json)含 EditorLoop 1,280.3848 ms 和未归属 624.933 ms；[frame 1](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_000116/startup_frame1_0000.json)含 EditorLoop 1,588.6826 ms 与多笔 Mono.JIT；[frame 44](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_000116/startup_frame44_0000.json)含 EditorLoop 956.7845 ms。未开 Profile Editor，仍不能归到某个 Editor 工具。为记录完整 Ready，下一次聚焦仅将首次准备窗延至 20 秒；4 MiB 完整 GP 原准备窗仍为 8 秒，末圈 12 秒、成绩 20 秒。窗口时长和样本构成不同，不能据此计算总体改善百分比。


## 1 MiB 与 Worker 解析聚焦复测

正式 [22642447 回执](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/22642447.json)1/1 通过，`TaskFirstRoundPreparationRefinementCapture` 经 AppEntrance 完成首章自然比赛的准备、末圈、完赛／成绩和下一夜景 PCM 冷准备。此轮生产页为 1 MiB，并包含资源解析 Worker 改动；仅准备窗改为 20 秒，末圈 12 秒、成绩 20 秒。八车、120g、120 Hz、实际 48 kHz Listener、VSync 关闭、全线程分析和暂停分析等条件保持。它是首章聚焦验证，**不是 1 MiB 重新跑完四章**；先前 4 MiB 的完整四章和此轮分开记录，不给总体改善百分比。

3 个窗口全部 0 裁剪、未知大小 GC 事件 0，真实输出均非零。Ready 实际墙钟 4.086978 秒，邻接索引 427 在准备窗内；主车末圈 556、过线 530、raceEnd／resultsVisible 647 都在各自保留范围。索引仍是 Profiler 回调邻接，不是 Unity 当帧精确映射。下一夜景在成绩窗结束前完成 PCM 准备，未记录独立下一章 ready 墙钟事件，不伪造其秒数。

| 窗口及报告 | 保留帧 | P95 / P99 / max ms | 全线程 GC.Alloc B |
|---|---:|---:|---:|
| [首章准备 20 s](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_001049/20261014_001139_d9e5f1cc.json) | 2353 | 9.586 / 18.768 / 343.990 | 259,628,441 |
| [真实末圈 12 s](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_001049/20261014_001418_2e6fec5c.json) | 1389 | 10.075 / 11.904 / 22.964 | 363,546 |
| [完赛／成绩／下一章准备 20 s](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_001049/20261014_001602_d9baef5e.json) | 2433 | 9.644 / 11.797 / 56.733 | 252,591,420 |

| 窗口 | PCM peak / end MiB | Unity 已分配 peak GiB | Mono 已用 peak GiB | Listener peak |
|---|---:|---:|---:|---:|
| 首章准备 20 s | 225.25 / 225.25 | 4.200 | 1.958 | 0.177828 |
| 真实末圈 12 s | 225.25 / 225.25 | 4.207 | 1.901 | 0.417866 |
| 完赛／成绩／下一章准备 20 s | 355.61 / 355.61 | 4.390 | 2.128 | 0.342023 |

上表仍是 Editor 进程观察值，包含 Profiler、引擎及测试；20 秒准备窗与原 8 秒样本数量不同，不能以 Unity／Mono 峰差宣称产品内存改善。PCM 自有样本预算未缩小，页对象头、Native 整曲副本、部分页和过渡声部另计。相较旧 4 MiB 的 Ready 3.313088 秒，本次 4.086978 秒是各自真实测量，Worker 和窗口条件不同，不归为单一缩页造成的增量。

| 窗口 | AudioBlock ms/call | BlockSync µs/call | PreparePcm Calls / Total / Self ms | PCM 路径 GC.Alloc B |
|---|---:|---:|---:|---:|
| 首章准备 20 s | 2.821 | 3.743 | 226 / 153.344 / 127.320 | 236,199,180 |
| 真实末圈 12 s | 2.917 | 4.006 | 无记录 | 无记录 |
| 完赛／成绩／下一章准备 20 s | 2.759 | 3.901 | 238 / 226.202 / 198.906 | 246,221,830 |

主线程 Native LoadFMODSound：准备 14 次 Total 3.2228 ms；成绩下一章 7 次 0.8898 ms。1 MiB 页 Calls 上升，准备 226 次 Total 平均约 0.6785 ms/call，成绩 238 次约 0.9504 ms/call；不能把页均值当成最大值或直接和旧每曲成本比较。DSP Marker 未录得 GC.Alloc，Audio Mixer Thread 未录得命名 Wait/Semaphore/Sleep，仍不证明不存在任何等待或目标设备超时。

实际解析 Marker 位于 Thread Pool Worker，主线程无同名 Marker：

| 窗口 | ParseTrack Calls / Total / Self ms | ParseAiPlan Calls / Total / Self ms |
|---|---:|---:|
| 准备 | 18 / 141.872 / 115.566 | 9 / 65.428 / 50.013 |
| 成绩下一章 | 5 / 45.339 / 41.969 | 2 / 14.770 / 13.777 |

准备窗两条 Worker 路径 GC 分别 6,489,250 / 2,689,737 B；不是主线程分配或 DSP 分配，不能将并行 CPU Total 相加当暂停时间。

### 聚焦仍保留的峰刺

首章末圈 max 22.964 ms，未记录 PreparePcm 或 Native 载入。正式原始单帧精读确认两处 1 MiB 分页残峰：

- [成绩 frame 894](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_001049/oneMib_results_frame894_0000.json)：整帧 56.7326 ms，PreparePcm Total 44.4467 / Self 17.2775 ms，直属 GC.Collect 27.1681 ms，GC.Alloc 1,048,608 B。该帧全线程 GC 1,050,359 B。
- [准备 frame 414](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_001049/oneMib_prepare_frame414_0000.json)：整帧 48.0763 ms，PreparePcm Total 36.5959 / Self 10.7130 ms，直属 GC.Collect 25.8819 ms，GC.Alloc 1,048,608 B。该帧全线程 GC 1,048,726 B。

首段及后续全部原始样本分页保存在对应外部目录。这是正式单帧祖先链证据，不再由窗口聚合猜测归属；Self 仍未拆分数组分配／清零和 GetData，不能全部称 memcpy。1 MiB 减少了每次准备规模，直属 GC 暂停仍存在，不能承诺所有帧低于 16 ms。

准备窗口整体 max 343.9895 ms（frame 1，GC 7,582 B）；其他较大帧 17/18/429/1555 也原样保留，不把启动、Editor、首次资源或呈现的未知成本都归为 PCM。该次聚焦证据确认 1 MiB 首章及下一章准备能完成、播放事件没有首次大曲解码；保留完整自有 PCM 预算和原插值／滤波音色，没有为此次修复另建数组池或全局 AudioManager。旧大锁仍缺少前后配对等待证据，不报其改善百分比。该次聚焦不代表全量回归、1 MiB 全四章或真实设备通过；后续完整四章结果见下节。

## 接续会话的 1 MiB 完整四章复验

正式 [e6b9c73c 回执](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/e6b9c73c.json) 1/1 通过，约 24 分钟。当前 1 MiB、Worker 解析及幽灵准备串行化版本经 AppEntrance 完成四章自然大奖赛、末圈、过线、成绩、换章与领奖台。八车、120g、120 Hz、48 kHz 实际 Listener 输出、VSync 关闭、正式 Profiler 全线程及分析期间暂停等条件沿用之前采样；首次准备窗为 20 秒，后三章准备窗仍为 8 秒，末圈 12 秒、成绩 20 秒、领奖 12 秒。第一章 Ready 后即启用验收自动驾驶，避免准备窗后半段使本机车落后。这些采样条件与旧 4 MiB 轮次不同，不计算总体改善百分比。

证据目录为外部 `Previews/Events_20261014/20261014_014519/`。按 report 路径去重为 13 个窗口：`podium_events.json` 同时包含书桌前两个窗口，不能再把它们重复累计。所有窗口 droppedFrames=0、GC 未知大小事件=0、Listener peak 非零。统计继续使用 nearest-rank，帧耗时不叠加各线程 Total。

| 窗口及报告 | 帧数 | P95 / P99 / max ms | 全线程 GC.Alloc B |
|---|---:|---:|---:|
| [草原准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_014605_85c7103c.json) | 2542 | 9.197 / 15.626 / 77.967 | 259,453,725 |
| [草原末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_014845_f47e060d.json) | 1484 | 9.293 / 10.726 / 20.991 | 414,006 |
| [草原成绩及下一章准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_015036_15d2402b.json) | 2427 | 9.690 / 12.360 / 35.139 | 252,528,075 |
| [夜景准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_015154_62fdfad7.json) | 1077 | 8.602 / 9.923 / 45.885 | 1,252,858 |
| [夜景末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_015413_4a4f9ef5.json) | 1438 | 10.005 / 15.144 / 19.388 | 592,525 |
| [夜景成绩及下一章准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_015606_5b65d11d.json) | 2376 | 10.744 / 13.883 / 36.191 | 190,930,043 |
| [冰川准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_015723_ec5f964b.json) | 1035 | 8.984 / 10.774 / 42.296 | 1,190,862 |
| [冰川末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_015944_5a40f442.json) | 1467 | 9.507 / 11.821 / 24.977 | 411,527 |
| [冰川成绩及下一章准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_020137_43a9c6b1.json) | 2502 | 9.300 / 10.551 / 37.025 | 175,049,646 |
| [书桌准备](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_020254_a2b2966d.json) | 1013 | 9.717 / 12.689 / 42.015 | 1,232,404 |
| [书桌末圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_020518_9291b9e3.json) | 1380 | 10.933 / 16.721 / 311.357 | 445,967 |
| [书桌成绩](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_020705_1edc2af7.json) | 2586 | 9.201 / 11.286 / 26.412 | 528,214 |
| [领奖](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/20261014_020831_9a23cb48.json) | 1672 | 9.176 / 11.432 / 72.999 | 718,267 |

四章命令至 Ready 的实测等待分别为 3.353267、0.013360、0.011391、0.013316 秒。后三章资源在上一章成绩期间准备，短等待不表示没有成本；菜单已经预热，也不将首章等待称为整个 Demo 冷启动。预留 PCM 仍为草原 225.25、夜景 355.61、冰川 297.21、书桌 287.24 MiB，不包含 Native 临时解码副本、其它音效、页对象头及 Editor/Profiler 内存。

完整 AudioBlock 平均为 2.685–3.036 ms/call，BlockSync 平均 4.486–5.337 µs/call；后者包含参数快照及可能的等待，不是独立锁阻塞计时。PreparePcm 只出现在首章准备与前三章成绩／下一章准备窗，分别 226/238/180/169 次、Total 63.125/97.675/151.174/71.176 ms。四个末圈、书桌成绩与领奖均没有首次 PCM 准备。分配仍存在，不据本次较低的成绩最大帧否认先前已确认的 GC 暂停。

书桌末圈 frame 108 整帧 311.3568 ms、全线程 GC 5,786 B。正式原始主线程层级为 Main Thread → EditorLoop，其中 EditorLoop Total 303.4640 / Self 303.4640 ms，GC 0；[原始首段](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Events_20261014/20261014_014519/desk_frame108_0000.json)及后续分页完整保留。此帧主要成本位于未展开的 EditorLoop，不能归为末圈首次音频准备；未开 Profile Editor，仍无法确定具体 Editor 工具。书桌末圈 frame 0 另有 103.4065 ms，领奖窗口 max 72.999 ms，均保留，不凭聚合路径补造其单帧原因。

本轮补齐当前页大小的完整四章范围，未重跑双 Editor 全弱网矩阵、项目全量测试、Player/IL2CPP、真实设备或人工听感验收。测试退出已恢复原 Profiler 历史 2000 帧、录制状态、音频静音和 VSync；一次性 Builder、采样与双端探针清理，报告和原始文件保留在外部证据目录。
