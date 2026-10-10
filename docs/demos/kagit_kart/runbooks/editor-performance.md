# Editor 性能记录

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
