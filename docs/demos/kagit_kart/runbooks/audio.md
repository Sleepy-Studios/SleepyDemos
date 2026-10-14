# 纸艺卡丁车原作音频维护

音源固定为 `kagit-kart` 提交 `da100b21ae1c5332d2843d21a9bb7b68b97e9c2a` 的 `src/audio/`，没有第三方音效或占位音乐。`export_audio.py` 使用本机 Edge 的 `OfflineAudioContext` 直接执行原作 DSP、乐器、配方与主题，渲染 44.1 kHz 立体声 PCM；Music 经 FFmpeg/libvorbis quality 7 保存为 OGG，短曲与音效继续保存 WAV。引擎由 `KagitKartEngineSynth` 实时合成原作节点。随机声音采用固定种子，使重新导出可复现。

## 重新导出

重新导出音乐需要带 libvorbis 的 FFmpeg（仅制作工具，游戏运行不依赖它）。将 FFmpeg 放入 PATH 后，在项目根目录执行；也可传入 `--ffmpeg D:/Tools/ffmpeg.exe`：

```powershell
python Tools/KagitKart/export_audio.py D:/Unity/ReferenceProjects/kagit-kart
```

工具校验原作提交，使用独立的隐藏浏览器配置和临时本地服务器，结束时关闭。输出保留既有 `.meta`，不打开 Unity。不要在参考源码存在未提交音频修改时导出。需要外部暂存时加 `--output-dir`；音乐离线仅保留原作混响，默认低通由运行时处理一次，短曲沿用同一音乐滤波。

曲目包括标题、领奖台、结算、四章比赛、四章开场、四章最后一圈及终点/最后一圈短曲。章节节拍沿用原作赛道配置：草原 118、博斯普鲁斯 96、冰川 128、书桌 124；最后一圈升两个半音、速度乘 1.12，并使用原作高潮段落。每首的 Start 包含第一次完整乐段，Loop 保存后续两个循环，保留交替声部和已建立的混响尾音；运行时按 DSP 时钟和实际采样数准确接续。

资产位于 `Assets/LoadResources/Demos/kagit_kart/Audio/SFX/`，Music、Stingers 为其子目录，遵守项目目前登记的 Demo 音频目录。`Bank_White`、`Bank_Pink`、`Bank_Crackle` 保存原作共享噪声，使用 PCM。`Data/AudioManifest.json` 记录来源版本、采样率、时长、渲染 PCM 的峰值/RMS 和循环属性；`Data/AudioBank.asset` 保存全部 AudioClip 引用，运行时由宿主 Loader 保持加载句柄。音乐导入仍使用 Vorbis quality 0.9，其余使用 PCM；全部为 DecompressOnLoad 且不预加载。28 首章节、结算及领奖音乐使用 Load In Background，2 首标题曲保留同步初始化；短音效和短曲保持原导入设置。DSP 初始化时预热短音效 PCM，按场景缓存；当前章节与结算音乐按下文提前准备。每次成功 `GetData` 后立即 `UnloadAudioData`，混音只读自有 PCM，不长期保留 Unity 解码副本。不要改成 Streaming 或 CompressedInMemory：当前 DSP 需要 `AudioClip.GetData`。

Music 的 30 个源文件由 427.84 MiB WAV 缩为 60.46 MiB OGG，减少 85.9%；保留原 GUID、44.1 kHz、双声道和循环采样长度，播放代码与 Bank 引用不变。这是源文件体积优化，不能当作包体或运行内存同幅缩小的证据。源编码与 Unity 导入压缩均有损；调音后从原作源码重新导出，不对现有 OGG 反复转码。本地 Git 历史中的这批 WAV 同时清除，原始 PCM 可依据固定版本 WebAudio 源码重新渲染。

试听直接在 Project 选中 Music 内的 AudioClip，使用 Inspector 底部预览播放；实际低通、混音和比赛切换在 Demo 中试听。调整乐曲配方仍改原作音频制作源码并重新导出，已有固定版本校验需要按已审核的新音源同步；调整游戏音量沿用现有设置页，不新增试听插件。

2026-10-11 转码验证：30 首 OGG 的容器终点采样数与原 WAV 精确一致，GUID 和导入设置字节不变。Unity 编译成功；`OriginalAudioManifestResolvesCompletePlayableBank` 1/1，覆盖全部引用、时长、声道及音乐解码/长度（Unity 单采样舍入容差）；`NativeAudioMixerHasRealOutput` 1/1，覆盖比赛实际 Listener 出声和暂停/恢复。不代表人工音质对比、全量测试、Player 构建或 Android 真机验收。

后续维护使用 Unity Inspector 更新 Bank 引用与导入参数。新增音频必须带 `demo`、`audio`、`sfx` Label；原作标题与比赛曲目按上述 SFX/Music 目录保存。

## 宿主接线

- 创建 `KagitKartAudio`，加载 `Data/AudioBank` 后调用 `Initialize(bank, worldCamera)`。
- 菜单选中和 GP 完赛后的下一章由 `KagitKartWorld.PrepareSelectedRaceAsync` 提前准备；正式 `StartRoundAsync` 仍须等待当前章准备完成，才绑定比赛和播放开场。将 `PrepareRaceAudioAsync` 放在 `Bind`、`PlayIntro` 前，使用场景或本轮准备的取消令牌。
- 准备完成后调用 `Bind(session)`，再以 `PlayIntro(chapterId)` 播放章节开场并关闭引擎；镜头结束调用 `BeginCountdown()`，启用引擎并播放第一声倒计时。比赛、道具、碰撞、暂停和最后一圈事件由音频自身订阅。
- 退出或重开比赛前调用 `Unbind()`；标题/选择页面调用 `PlayMenu()`，大奖赛最终领奖调用 `PlayMenu(true)`。
- 菜单点击与换页分别调用 `Click()`、`PageTurn()`；纸景折起通知使用 `Popup(原作位置, size)`。
- 设置滑块调用 `SetVolumes(music, sfx, muted)`。暂停遵循原作：比赛冻结、引擎渐静，音乐继续播放并降音量、低通滤波。

声音宿主随场景销毁退订所有事件，使用本地 AudioSource。公共主音量、音乐、音效、UI 和静音复用 `Core.Runtime.Settings.AudioSettingsRuntime`；普通音源复用 `AudioBusSource`。卡丁车只在自己的 DSP 内乘相同总线增益，保留本地音量平方曲线。现有公共分类和本 Demo 事件桥已覆盖当前用途，本次不新增全局 AudioManager，也不接管 AudioListener。

## 当前章 PCM 准备与释放

每章准备 14 个 Clip：该章开场、普通比赛、最后一圈的 Start/Loop 共 6 首；该章最后一圈短曲 1 首；finishWin/finishGood/finishOk 共 3 首；results 和 podium 的 Start/Loop 共 4 首。切拍和终点事件只分配已准备声部，未来播放时间不会再承担首次音乐解码。

准备先查自有 PCM 缓存；命中时不再请求 Unity 解码。缺失时主线程调用 `LoadAudioData`，28 首音乐依据后台导入开关解码，逐帧等待 `loadState == Loaded` 后才 `GetData`。每次只允许一首 Native 加载在途，复制完成即卸载 Unity 解码副本，并等待下一帧。标题曲及短音效仍沿用同步初始化。后台加载由 [Unity 的 LoadAudioData 契约](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioClip.LoadAudioData.html)控制；导入必须保持 DecompressOnLoad，满足 [GetData 的采样访问条件](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioClip.GetData.html)。托管 PCM 数组分配、`GetData` 和卸载仍在主线程，不保证准备帧没有峰刺，实际收益须同路径采样。

异步准备将这 28 首非标题音乐保存为 1 MiB PCM 页，每页最多 262,144 个 float；最后一页只分配剩余样本。每页分配、`GetData` 后等待下一帧，全部页成功才发布到缓存并预留。`GetData` 偏移使用帧数，原作双声道页按完整左右声道对齐，不将声道数再乘入 API 偏移。SFX、Stingers、共享噪声、Boost 及标题音乐保留连续数组。开赛仍等当前章全部曲目完成；分页限制单次分配和复制规模，不降低完整 PCM 预算，也不保证 GC 或其他资源没有峰刺。

- 同章准备中的请求通过 UniTask 官方 `AsyncLazy` 共享任务，允许后台预热与开赛同时等待尚未完成的准备；不能用 `Preserve` 代替进行中的多调用者续接。后续调用者取消自身等待，不会通过该等待取消先前发起的共享准备。共享准备本身使用首个发起者的令牌。
- 换章递增准备版本并串行接管缓存，先列出下一章 14 个 Clip，再保留其中已准备的 PCM；公共完赛短曲、结算与领奖共 7 首跨章继续预留，避免每章再次解码。缓存命中前、Native Loaded 后、每页开始和缓存发布前都检查令牌与当前准备版本；旧请求被替代后取消，不继续复制后续页，也不把 `preparedChapter` 标记为旧章。当前准备失败或取消后仍解除全部预留，下次调用可重试。
- Native 加载本身不接取消令牌。准备取消后仍持有串行门，等待当前 Native 请求完成，再取消复制并卸载；后续章节不会在旧加载尚未结束时启动另一首。取消完成前可能短暂保留一首 Unity 解码副本，14 首自有 PCM 预算不因此减少。
- 同章菜单、重赛、`Unbind` 和 `PlayMenu` 保留章缓存，避免 Mode/Class/Character/Track 页面间重复解码。换章解除上一章预留；仍在播放的声部保持其引用，完成或停止后由主线程回收。
- 音频块自己的 PCM 引用一直保持到块结束；解除缓存不会覆盖或撤走正在读取的数据。退出 Demo 时停止载体、销毁 DSP，并释放全部自有缓存和 Loader 句柄。

当前 44.1 kHz 双声道资产按 Manifest 采样长度计算的 **14 首预留托管 PCM** 如下；`KagitKartAudio.PreparedPcmBytes` 可记录实际已准备字节数。

| 当前章 | 预留 PCM |
|---|---:|
| 雏菊草原 | 225.25 MiB |
| 博斯普鲁斯夜景 | 355.61 MiB |
| 冰川通道 | 297.21 MiB |
| 书写桌面 | 287.24 MiB |

这不是整个音频或 Mono 堆峰值上界：标题曲、尚未结束的过渡声部、短音效、噪声、Boost 缓冲、页数组对象头和引用、准备时短暂存在的 Unity 解码副本及待回收对象另占内存。分页期间仍持有整首 Unity 解码副本，完整复制结束才卸载；更多准备帧可能延长其驻留时间。缓存范围只覆盖一个准备章与共享结算曲，不将四章音乐全部常驻；实际总峰值需在目标设备采样。

## 音频块与线程边界

`KagitKartAudioDsp` 在块起点持短锁复制声部列表、控制参数和 PCM 引用；游标、滤波状态及逐采样混音均在锁外由音频线程推进。主线程的播放、停止、结束时间与样本交换不会再等待整块 Voice/Engine/Glue/Limiter 处理。声像系数按块计算，保留原等功率声像和立体声交叉混音。

分页音乐每个采样分别定位当前帧与插值下一帧的页数组，左右声道复用这两个页引用；使用位移与掩码，不增加采样分配或锁。跨页、循环末尾回到第 0 帧，以及非循环最后一帧钳制均保留原插值规则。此读取额外成本仍须实际测量 `AudioBlock`。

加速声在每个音效声部预留三份 PCM 缓冲，复用滤波状态和数值随机内核；写入既不在当前块读取、也不在下一块待播的缓冲，随后短锁交付。第三份用于同一音频块内连续事件，保留本次实际帧数和播放长度。不能覆盖音频线程正在读取的数据，也不能用统一固定长度替代原事件配方。

引擎控制参数在音频块起点持短锁复制，逐采样平滑、滤波历史和振荡器相位由音频线程推进。主线程不持有整块引擎 DSP 锁；参数的目标、时间常数和 fresh 复位语义仍按原作执行。分配和线程等待的收益须通过新的同条件采样确认，算法对照与实际 Listener 输出分别验收。

排障使用正式 [性能工具](../../../modules/performance-tools.md)，查看对应线程的 `KagitKart.Audio.PreparePcm`（主线程 PCM 分配与复制；同步初始化路径还含解码）、`KagitKart.Audio.BlockSync`（音频块控制快照及等待）、`KagitKart.Audio.AudioBlock`（完整音频块）、`KagitKart.Engine.ControlSync`（主线程引擎控制）和 `KagitKart.Engine.AudioBlock`。异步准备中的 Native 加载及跨帧等待发生在 `PreparePcm` 外，不能用该 Marker 单独计算全部解码成本，也不能与旧同步路径的 Total 直接比较。父子 Total 不相加，主线程短窗口不能代替音频线程或完整三圈证据。复测需打开可听见的 Editor 音频，记录总线/本地音量、静音、输出采样率与 DSP buffer；单独覆盖冷准备、热重赛、末圈、过线/结果和领奖入曲。本节描述当前实现，不声明性能收益或真实设备通过。

分页路径的 `PreparePcm` 每页记录一次，连续数组路径每次缓存缺失记录一次；缓存命中不记录。Calls 因而不再等于准备的 Clip 数，不能继续以“每章 14 次”换算每曲成本。

## 2026-10-14 常态音频复测

28 首音乐后台加载修复后，正式 Unity Test Runner `KagitKartAudioTests`（080ee8f0）13/13 通过，回执保存在外部 `ArtSource/kagit_kart/Previews/Verification_20261014/080ee8f0.json`。既有 Manifest 测试已等待 Native Loaded 再读样本，算法、长度与缓存生命周期契约继续覆盖；未新增测试文件。这项通过不等于冷准备峰刺消除，也不证明完整四章事件或真实设备性能通过。下表的四模式短窗采于后台加载修复之前，只记录当时起跑后的常态成本。

4 MiB 分页版本曾由正式 Unity Test Runner（88e2fb42）完成 14/14，回执位于外部 `ArtSource/kagit_kart/Previews/Verification_20261014/88e2fb42.json`。当前 1 MiB 版本的算法／缓存 12 项仍在 EditMode 音频测试；Manifest 和分页音色两项已迁入既有 ScenePlayModeTests，依赖真实 Play Mode 的帧推进与音频加载，不通过跨域 EnterPlayMode／ExitPlayMode 手动切换。最新正式分组均通过：Edit Audio（a18056b4）12/12、Play Manifest（499d80b3）1/1、Play Tone（c93d3b10）1/1，回执位于同一外部 Verification_20261014 目录；生产 DSP 与 NextFrame 节拍未因测试调整改变。

4 MiB 版本完整四章、末圈、成绩及领奖正式事件采样（16f564bb）1/1 通过，13 个窗口无裁剪，真实 Listener 均有非零输出。末圈和领奖未记录首次 PCM 准备，公共曲跨章复用；下一章 PCM 准备仍在前三章成绩窗口出现。原始单帧确认 4 MiB 页曾引发 PreparePcm 60.9614 ms，其中直属 GC.Collect 23.7715 ms，Self 37.1885 ms 未细分清零与复制，不能说所有峰刺已消除。完整帧、GC、DSP 与进程内存矩阵见[性能记录](performance-2026-10-14.md#4-mib-分页完整事件矩阵)。

当前进一步收缩为 1 MiB，并同步已有音色测试的输入边界，12+1+1 相关契约已通过。正式首章聚焦采样（22642447）1/1，通过 20 秒准备、12 秒真实末圈、20 秒成绩及下一夜景 PCM 冷准备，Ready 实测 4.086978 秒；3 窗无裁剪、Listener 非零。首章末圈 max 22.964 ms；原始单帧确认成绩 frame 894 为 56.7326 ms，PreparePcm 44.4467 ms 含直属 GC.Collect 27.1681 ms；准备 frame 414 为 48.0763 ms，PreparePcm 36.5959 ms 含 GC.Collect 25.8819 ms。两帧各分配 1,048,608 B，Self 未分离清零与 GetData，不承诺 GC 暂停消失或所有帧低于 16 ms。保留音色和完整 PCM 预算，没有引入新数组池。完整矩阵见[最新聚焦记录](performance-2026-10-14.md#1-mib-与-worker-解析聚焦复测)。这是 1 MiB 首章／下一章准备验证，不是 1 MiB 重新跑完四章；此前 4 MiB 全四章证据独立保留，也不能线性推算缩页的 GC 收益。

正式 `TaskAudioCapture`（e27d58e0）1/1 通过，可听见的真实输出断言通过。每种开关条件均重新开始草原 120g 八车比赛，起跑后录制 8 秒；Unity 6000.3.15f1 Editor，VSync 关闭，分析阶段暂停，正式 Profiler 覆盖全部线程，未开 Deep Profiling 或 Profile Editor，未固定比赛随机种子。

| 开关条件 | 帧耗时 P95 | P99 | 最大帧 | 本地报告 ID |
|---|---:|---:|---:|---|
| DSP 载体关闭 | 10.019 ms | 15.578 ms | 40.999 ms | `20261013_202638_b92f35bc` |
| 仅音乐 | 10.048 ms | 13.120 ms | 25.353 ms | `20261013_202718_421b8308` |
| 仅音效与引擎 | 10.244 ms | 15.736 ms | 20.404 ms | `20261013_202759_37afd3b0` |
| 音乐、音效与引擎全部开启 | 10.389 ms | 15.415 ms | 18.348 ms | `20261013_202843_1ea5f465` |

分位采用正式工具的 nearest-rank，载体关闭条件实际禁用 AudioSource，不等于仅将滑块设为零。完整条件、线程等待与 GC 归属见[本轮性能记录](performance-2026-10-14.md)，原报告已精确归档到外部 `Previews/Audio_20261014/`。音频线程 `KagitKart.Audio.BlockSync` 平均每次约 5.3–5.6 µs；三个开声条件的 `KagitKart.Audio.AudioBlock` Total 平均每次分别约 3.026、3.108、3.051 ms，两类 Marker 的 GC 均为 0。完整音频块的 Total 包含子级引擎 DSP，不能再相加；报告的所有线程 GC 总量包含测试 Runner 等其他工作，不能记为 DSP 分配。

这组起跑短窗未复现常态严重掉帧。此前基线在同一场内依次切换开关、所处比赛阶段不同，且只分析 Main Thread，不能与本轮报告直接计算改善百分比。当前结果不覆盖冷音乐准备、末圈切拍、过线/结算或领奖音乐切入峰刺，也不代替完整三圈、资源与布局峰刺、Player 或真机验收。

接续会话后，当前 1 MiB 与 Worker 解析版本已由正式 `e6b9c73c` 1/1 补齐完整四章、末圈、成绩、换章与领奖台，13 个独立窗口均无裁剪且 Listener 非零。首章 Ready 3.353267 秒，后三章约 11–13 ms（准备成本提前发生于前章成绩）。书桌末圈仍有 311.357 ms 帧，原始主线程层级定位 EditorLoop 303.464 ms，未展开到具体 Editor 工具；四个末圈与领奖均没有首次 PCM 准备。此结果补齐上述首章聚焦的范围，不抹去先前已确认的 PCM/GC 残峰，不承诺所有帧低于 16 ms。完整数据和边界见[最终四章复验](performance-2026-10-14.md#接续会话的-1-mib-完整四章复验)。一次性采样工具已清理，原始数据和回执留在外部证据目录。

## 验证与保真边界

EditMode 运行 `Tests.Demo.KagitKartAudioTests` 的 12 项，检查原作换挡转速、实际加速持续时间、动态加速采样、引擎软削波、两级混音动力学、同块连续 Boost 的 PCM 保护和换章缓存生命周期。`PendingBoostChangesLeaveCurrentAudioBlockIntact` 防止后续事件覆盖正在播放的波形，并核对下一块采用最新事件；`ChapterReplacementRetainsSharedPcmAndReleasesOldReservations` 保护公共曲跨章复用、旧章解除预留后的声部引用安全和最终清理。

PlayMode 在既有 `Tests.Demo.KagitKartScenePlayModeTests` 单独运行 `OriginalAudioManifestResolvesCompletePlayableBank` 与 `PagedMusicPreservesStereoInterpolationAcrossPagesAndLoopEnds` 各 1 项。前者核 Bank 全部引用、声道、长度与 Loaded 后的音乐 PCM；后者比对相同输入的分页与连续数组混音，保护页边界、循环接头、非循环末尾的左右声道和分数插值，避免后续采样改动产生杂音。两项复用现有 ScenePlayModeTests 生命周期，不新增文件或程序集。涉及播放帧和后台解码的测试不能依赖 EditMode 的 Time.frameCount 或跨域保留局部对象。

参考由真实 Edge OfflineAudioContext 生成，默认保存在 `Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/` 的 `OriginalAudioDynamics.json`、`OriginalAudioBoost.json` 与 `OriginalEngineSoftClip.json`，不参与 Demo 资源打包。动力学对照 16,384 帧输入，容差 0.0003；动态加速对照原作 start 配方，容差 0.003；引擎 2x 软削波对照滤波、延迟与峰值，容差 0.000005。只刷新参考可执行导出命令并加 `--checks-only`，外部诊断可再指定 `--reference-dir`。真实听感需在四章中验证开场、倒计时、漂移三档、道具警报、救援、最后一圈、终点和暂停。无需全量测试。

音乐及普通音效直接执行原作算法。加速声由 `KagitKartBoostSynth` 根据本次 `LastBoostDuration` 连续合成，重叠加速不借用已延长的计时；落地、撞墙、碰撞、纸景音效使用满强度源码采样，并在空间裁声之后按实际 AirTime、速度、冲量和景物波次尺寸缩放。

`KagitKartAudioDsp` 将本 Demo 全部声部汇总到一个立体声 AudioSource。音乐切换、最后一圈切拍、结算、暂停遵循原作时序；汇总后处理 MAKEUP=1.25、Glue（-18dB/10dB软膝/3:1/5ms/200ms）与 Limiter（-3dB/硬膝/20:1/1ms/100ms）。采用 Blink/WebAudio 指数软膝、32 帧包络、自动补偿、自适应恢复和每级 6ms 前瞻；许可随 `Data/AudioNotices.txt` 保存。Unity 原生 Compressor 固定∞:1且攻击时间最低10ms，无法表达上述参数，所以使用本 Demo 独立 DSP，不修改全局 AudioListener。

引擎按原作八层播放器与三个近车声部实时生成：双失谐锯齿与方波低音经过 1024 点软削波、Blink 128/256 tap 半带 2x 重采样、油门低通与可变深度 putt 调制；flutter 固定 1.9kHz 噪声谱，轮速只改变 4–95Hz 方波调制；不同地面改变 rattle 滤波与实际增益，漂移分别改变尖响滤波和带 7Hz/25音分调制的三角波；boost、尾流和无敌的滤波、音量、Pan、近车 Doppler 与全部时间常数沿用 `engine.js`。暂停只关闭共享引擎 gate，内部相位与噪声时钟继续推进。

采样仍有平台实现边界：普通纸质音效保存一次源码随机实现，PCM 噪声量化、Vorbis 音乐解码及锯齿/方波的 PolyBLEP、三角波解析生成与浏览器内部频带 wavetable 不保证逐采样一致；固定噪声谱、动态调制深度、连续参数与混音时序不使用循环变速或固定默认值替代。

运行时低通/高通的 Q 按 WebAudio dB 单位转换，bandpass 保留线性 Q。动态噪声起点按原生 rate=1 路径取最近采样，保持随机流与原作一致。依据：[Web Audio BiquadFilterNode](https://www.w3.org/TR/webaudio/#dom-biquadfilternode-q)、[Blink AudioBufferSource](https://raw.githubusercontent.com/chromium/chromium/main/third_party/blink/renderer/modules/webaudio/audio_buffer_source_handler.cc)。

Unity 原生压缩器差异依据：[Unity Audio Compressor](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioCompressor.html)。动力学移植参考：[Blink DynamicsCompressor](https://raw.githubusercontent.com/chromium/chromium/main/third_party/blink/renderer/platform/audio/dynamics_compressor.cc)。

## 2026-10-11 原生输出与事件桥修正

倒计时按源退出赛前曲（0.9秒）；撞墙保留本车0.2秒与world共享桶0.02秒两级节流，tier/threat/rouletteTick烘焙变体使用同一源配方声桶。全局硬件首次暂停、TT重赛、静音不额外响click，已暂停P继续和UI确认仍保留。纸景SceneryWorld真实目标抬起波次接入Audio.Popup，原源size已补存四World资产，关闭当前场精确退订；待播波次按DSP时间交付，Unbind清队列。

原生 `NativeAudioMixerHasRealOutput` 1/1（9c1f1c0d）：Intro章节曲→倒计时无新背景曲→比赛章节曲、真实P暂停与UI Enter继续、DSP音乐游标及最终Listener PCM，peak=0.6997126。原生八道具测试1/1（19bc564c）核每件新声路由与最终PCM，peak=0.6655096、48000Hz；退出核DSP载体销毁。实际测试发现当前静音载体的AudioSource历史采样为零，而Listener最终混音有效，因此采用[AudioListener.GetOutputData](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioListener.GetOutputData.html)。未改变全局静音/音频设置或持久化偏好。非零总线不等于逐声隔离，也没有人工听过音色、声像和响度；原11项算法/资产证据与这次运行证据分开使用。
