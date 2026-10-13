# 采集、分析与比较 Unity 性能

## 连接与准备

按 [Unity 实例确认流程](run-unity-tests.md#确认-unity-实例) 从 registry 找到 SleepyDemos，核对 health 与实际 `/skills/schema?category=Profiler`。项目扩展使用 `sd_profiler_`；上游 `profiler_get_*` 是瞬时统计，不是录制器。

查询 `sd_profiler_get_status`，确认目标、CPU 模块、Deep Profile、是否运行以及已有任务。已有用户录制应由用户停止，不能强行接管。正式运行验证遵循 [项目默认模式](../../AGENTS.md#验证与-unity-editor)，记录并恢复临时改变的资源模式和画面设置。

## 接口

所有接口使用现有 UnitySkills POST `/skill/<name>` 与 normal dryRun / mode 门禁，准确签名以当次 schema 为准。

| 接口 | 参数与用途 |
|---|---|
| sd_profiler_get_status | 查询目标、采集状态、缓冲和能力 |
| sd_profiler_capture_start | frameCount=300、durationSeconds=0、timeoutSeconds=60、profileEditor=false、context=null、recordGcCallstacks=false |
| sd_profiler_capture_stop | taskId；停止本工具录制并等待保存 |
| sd_profiler_job_status | taskId；查询到终态 |
| sd_profiler_job_cancel | taskId；取消本工具任务并恢复设置 |
| sd_profiler_save | fileName=null；保存停止缓冲，不覆盖同名文件 |
| sd_profiler_load | path；备份当前缓冲再加载 .data |
| sd_profiler_analyze_start | firstFrame=-1、lastFrame=-1、threadFilter=null、context=null；分析保留范围 |
| sd_profiler_get_frames | reportPath=null、start=0、count=50；分页读取帧耗时与分配 |
| sd_profiler_get_frame_samples | frame、threadIndex=0、start=0、count=50；读取样本层级、Self/Total、GC 字节及已有栈 |
| sd_profiler_get_cpu_hotspots | reportPath=null、topCount=20、sortBy=self；self / total / calls |
| sd_profiler_get_gc_allocations | reportPath=null、topCount=20；按分配路径聚合 |
| sd_profiler_compare | baselinePath、currentPath、topCount=20；比较每帧指标 |

分页和 topCount 为 1..200。任务保存最新一项；开始下一任务前保存前一项返回的文件路径，不用旧 taskId 查询新任务。

## 采集与分析

1. 明确场景阶段及条件；默认正常 CPU 采样。需要按时长采样时设 frameCount=0；两种界限同时指定时先到者停止。
2. dryRun 后启动，低频轮询 taskId。录制期间不修改对象、不截图、不序列化、不解析历史文件。
3. 完成后核对 receivedFrameCallbacks、completedFrames、retainedFrames、droppedFrames、dataPath、metadataPath 和 backupPath。采集中的 completedFrames 是索引跨度，droppedFrames 是环形缓冲范围裁减，均不是上游传输计数；receivedFrameCallbacks 才是实际回调次数，历史 null 不补填。超时或帧未完全保留不得视为完整窗口；需要确认范围中每帧可读时逐帧核 raw.valid。
4. 长圈或周期事件分段保存，不让环形缓冲覆盖前段。每段先稳定帧循环，分析和文件处理在录制结束后执行。
5. analyze_start 完成后取热点和异常帧。父子 Total 不相加，按线程及 Self/Calls 判断计算；异常帧读相邻帧区分业务、回收、资源、Shader、音频、Canvas 和等待。
6. 普通样本只有祖先路径，无法定位分配来源时针对相关短段开启 recordGcCallstacks；这是单独的诊断采样，不混入性能基线。
7. 加载历史文件后使用回读的新帧索引。metadata 缺失或不匹配时保留 unknown，不能凭当前设置补填。旧保存文件只有长度与 SHA-256 均匹配才能代替当前缓冲备份，否则工具会重新保存；不要在任务中覆盖或删除证据文件。错误文件加载失败会尝试恢复备份，随后按状态及实际帧核对，不把来源信息恢复视为必然成功。开始/加载发生网络超时先读状态，避免重复任务。

## 前后对照

只改变待验证因素，其余目标、设备、分辨率、画质、输入、对象数量和阶段一致，预热后重复配对。修正明显画面错误时先建立修正后基线，再测优化收益。报告均值与分位、异常帧比例、持续/周期分配和重复间波动；单帧峰值与窗口内一次收集不能独自证明因果。

采样中的测试驱动、Editor-only 和必要初始化分配单列。0 B/帧目标应明确为已预热的项目运行时热路径，不能用过滤总量掩盖实际分配。UI、声音、操控和资源生命周期仍需相关功能验收。

最终归档报告与对应 `.data` / metadata，清理本次临时驱动、请求、失败试跑产物；不删除用户文件。CPU、渲染线程和 GPU 结论分开；真机没有采到就明确未验证。
