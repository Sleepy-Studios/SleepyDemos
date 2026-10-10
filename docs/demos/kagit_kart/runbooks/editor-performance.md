# Editor 性能记录

2026-10-11，通过 Unity Test Runner 的 `NormalDrivingRecordsEditorTimingsForAllChapters`（`4d64beb6`，1/1）在四章普通比赛中采样。Unity 6000.3.15f1，RTX 3050，Game View 1920×1080，120g、肉桂狐与七名 AI。正常倒计时后按住油门，预热两秒，再采样三秒；没有加速比赛模拟或修改项目时间设置。

| 章节 | 帧数 | 平均帧耗时 | 最大帧耗时 | Unity 已分配内存 |
|---|---:|---:|---:|---:|
| 雏菊草原 | 318 | 9.45 ms | 25.11 ms | 668.6 MiB |
| 博斯普鲁斯夜景 | 321 | 9.33 ms | 23.67 ms | 741.1 MiB |
| 冰川通道 | 316 | 9.52 ms | 23.11 ms | 745.3 MiB |
| 书写桌面 | 394 | 7.63 ms | 16.73 ms | 762.6 MiB |

帧耗时直接读取 `Time.unscaledDeltaTime`；内存读取 `Profiler.GetTotalAllocatedMemoryLong`，包括 Editor 与公共项目资源，不是 Demo 独占值。原始 JSON 和同帧截图位于外部美术目录 `Previews/EditorTiming_*.json` 与 `Race_*.png`。测试结束返回 Hub，并检查赛道宿主已释放。

该记录覆盖各章起步路段，尚未包含原作屏幕后处理、最终设置页面、整场比赛峰值、全部道具同时出现或 Android 真机。新增这些表现后需复验受影响场景。没有执行全量测试或 Player 构建。

UnitySkills 当前 `profiler_get_stats` 将 UnityStats 的秒值按毫秒换算 FPS，不能直接采用该 FPS 字段；早先在返回 Hub 后读取的瞬时快照也不作为比赛证据。本记录使用正常运行测试直接采样，未修改第三方工具源码。
