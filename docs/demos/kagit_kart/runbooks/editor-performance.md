# Editor 性能记录

## 起步短窗口（历史证据）

2026-10-11，接入原作纸印后处理后，通过 Unity Test Runner 的 `NormalDrivingRecordsEditorTimingsForAllChapters`（`f153aef9`，1/1）在四章普通比赛中采样。Unity 6000.3.15f1，RTX 3050，Game View 1920×1080，120g、肉桂狐与七名 AI。正常倒计时后按住油门，预热两秒，再采样三秒；没有加速比赛模拟或修改项目时间设置。

| 章节 | 帧数 | 平均帧耗时 | 最大帧耗时 | Unity 已分配内存 |
|---|---:|---:|---:|---:|
| 雏菊草原 | 339 | 8.88 ms | 35.33 ms | 672.1 MiB |
| 博斯普鲁斯夜景 | 317 | 9.48 ms | 65.26 ms | 744.2 MiB |
| 冰川通道 | 377 | 7.97 ms | 16.99 ms | 748.6 MiB |
| 书写桌面 | 339 | 8.85 ms | 19.80 ms | 765.8 MiB |

帧耗时直接读取 `Time.unscaledDeltaTime`；内存读取 `Profiler.GetTotalAllocatedMemoryLong`，包括 Editor 与公共项目资源，不是 Demo 独占值。原始 JSON 和同帧截图位于外部美术目录 `Previews/EditorTiming_*.json` 与 `Race_*.png`。测试结束返回 Hub，并检查赛道宿主已释放。

该记录覆盖各章起步路段，包含原作纸印后处理，尚未包含最终设置页面、整场比赛峰值、全部道具同时出现或 Android 真机。夜景出现 65.26 ms 单帧峰值，当前采样未带 CPU 标记，不能归因于后处理，也不能据平均值称全程稳定。最终表现完成后需定位持续掉帧或可重复峰值。没有执行全量测试或 Player 构建。

UnitySkills 当前 `profiler_get_stats` 将 UnityStats 的秒值按毫秒换算 FPS，不能直接采用该 FPS 字段；早先在返回 Hub 后读取的瞬时快照也不作为比赛证据。本记录使用正常运行测试直接采样，未修改第三方工具源码。

## 正常道路长圈（2026-10-11）

长期入口仅保留现有场景测试类的 `NormalDrivingRecordsLongEditorTimingsForChaptersOneAndTwo` 与 `NormalDrivingRecordsLongEditorTimingsForChaptersThreeAndFour`。普通赛120g、肉桂狐与七名AI，正式原作AI驾驶玩家；宿主按真实帧推进，自然完成六秒电影、倒计时和道路行驶，不快进、不瞬移或强制特效。每章采样至少45秒且完成一圈，记录每帧耗时/道路进度、分位、自然事件、可用主线程/GC/托管内存计数；JSON序列化和截图在窗口外。

前两章作业 `a975b049`、后两章作业 `62f99115` 均终态1/1通过。Unity 6000.3.15f1、Windows11、i7-12700KF（20逻辑核）、RTX3050（约6GB）、Direct3D12、1920×1080、PC/PC_RPAsset、正常动态；Game View获得焦点，Profiler未开启。四章均实际完成一圈，窗口依次45.006、45.001、46.041、45.000秒。

| 章节 | 帧数 | 平均 / P50 | P95 / P99 | 最大 | >16.67 / >33.33 / >50ms帧 |
|---|---:|---:|---:|---:|---:|
| 雏菊草原 | 5491 | 8.20 / 7.13ms | 12.39 / 14.09ms | 74.96ms | 39 / 2 / 1 |
| 博斯普鲁斯夜景 | 6214 | 7.24 / 6.70ms | 11.73 / 12.83ms | 32.76ms | 17 / 0 / 0 |
| 冰川通道 | 6200 | 7.43 / 6.76ms | 11.93 / 17.23ms | 34.85ms | 64 / 1 / 0 |
| 书写桌面 | 6360 | 7.08 / 6.59ms | 10.74 / 12.92ms | 35.10ms | 28 / 1 / 0 |

自然事件按本场车辆规则信号计数；不强制生成道具或救援，纸景波次与音频汇聚、节流、距离裁声后也不要求一对一。

| 章节 | 加速/受击/墨水/救援 | Popup / rustle | GC累计分配MiB | 托管采样峰值 / 起止差MiB |
|---|---:|---:|---:|---:|
| 雏菊草原 | 67 / 3 / 11 / 0 | 218 / 80 | 115.02 | 1539.49 / +14.38 |
| 博斯普鲁斯夜景 | 58 / 6 / 11 / 0 | 117 / 54 | 115.20 | 1717.92 / +27.67 |
| 冰川通道 | 85 / 3 / 0 / 0 | 161 / 72 | 129.52 | 1593.47 / +48.51 |
| 书写桌面 | 65 / 3 / 5 / 0 | 37 / 27 | 122.49 | 1747.88 / -221.86 |

四章自然音乐状态均检查 `intro_<chapter>`→倒计时key=null（JSON空串）→比赛`<chapter>`，窗口末尾仍是对应比赛曲。音乐0.7、音效0.9、Editor/本Demo未静音、Listener音量1且未暂停、输出48kHz；这些路由记录不单独替代实际听感。

四章各发生一次Gen0收集；该Unity运行时MaxGeneration=0，其余代记录-1。Render Thread记录器不可用，原始值-1。内存包含Editor、公共资源与采样自身，不是Demo独占内存，也不能仅据起止差判泄漏。草原仅一次>50ms峰值74.96ms（墙钟39.674秒、道路T=0.91679），CPU原始标记未覆盖那一帧，不能因窗口内Gen0=1就推断其由GC造成；这次峰值尚未精确定因。四章窗口不证明整场三圈或所有道具/演出组合稳定60帧；完整`samples`留在JSON供定向复查，不直接归因于某Shader。

最新证据位于外部 `SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/`，同名PNG已保存：

- `EditorTiming_meadow_LongRoad_20261010_225412.json`
- `EditorTiming_bosphorus_LongRoad_20261010_225412.json`
- `EditorTiming_glacier_LongRoad_20261010_225817.json`
- `EditorTiming_desk_LongRoad_20261010_225817.json`

文件名为UTC日期，实际本地采样日期为10月11日。上述范围没有执行全量测试、Player或Android验证。

## Editor呈现等待诊断

一次性原生CPU诊断作业 `57e934ef` / `183071a2` 将约242–251ms定位到 `GfxDeviceD3D12.WaitForLastPresentation` / `TimeUpdate.WaitForLastPresentationAndUpdateTime`，同帧实际RenderLoop约8–10ms。单次直接托管堆查询约0.31ms，不能解释该等待。`80893adb` 对照仅将Editor的 `GameView.vSyncEnabled=false`，约8ms正常帧恢复，长呈现等待消失；项目 `QualitySettings.vSyncCount` 独立为0，不能将两者混为同一设置。

长圈方法仅在测量期间临时关闭Game View VSync，并在finally恢复进入时的原值true；没有修改项目TimeScale、画质档或正式资源。当前证据指向Editor显示/呈现等待，源码不需要为此降画质、改规则或更换纸艺效果。RenderLoop与Main Thread是CPU标记，带WaitForGPU字样的等待也不是逐Shader GPU耗时；现有GPU Profiler/统计不能据此给单个Shader分摊成本。

原生CPU诊断方法仅为本次定位使用，后两章终态后已从正式EditorTiming测试移除，不列为永久运行入口。复查用Unity Profiler窗口与保留的外部原始文本 `EditorCpuMarkers_20261010_224715.txt`、`EditorCpuMarkers_20261010_224923.txt`、`EditorCpuMarkers_20261010_225233.txt`。长期只保留上述两项正常道路测法，旧3秒数据继续作为历史证据。
