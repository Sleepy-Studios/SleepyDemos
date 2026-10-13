---
name: unity-performance
description: "操作 SleepyDemos 正式 Unity Profiler 工具，录制、保存加载 .data、分析 CPU/GC 与异常帧并比较报告。实际采集、Profiler 数据分析或性能复测时使用；工具接口为 sd_profiler_，区别于上游只读统计。"
---

# Unity 性能采集与分析

工具由 `Assets/Scripts/Core/Editor/PerformanceTools` 维护，仅 Bridge 引用 UnitySkills；不修改上游包、不新建服务、不用临时分析器代替正式入口。

## 连接与采集

1. 按项目 [Unity 测试 runbook](../../../docs/runbooks/run-unity-tests.md) 从 registry 确认项目、端口及 health。通过 `/skills/schema?category=Profiler` 发现 `sd_profiler_`，读取实际参数并查询 `sd_profiler_get_status`。
2. 明确当前连接目标、场景阶段和采样条件。默认 300 帧、60 秒超时、`profileEditor=false`；不自行切换目标或开启 Deep Profile。需要运行场景时遵守任务授权和正式 AppEntrance 流程。
3. 保护用户原有录制；已有任务先识别归属。普通采集要求 Deep Profile 关闭，工具不会为了关闭它触发重载。
4. dryRun 后启动有界采集。按时长采集传 `frameCount=0`；长窗口分段保存，确认每段 retainedFrames 和 droppedFrames，不能用最后一个环形缓冲代表整圈。
5. 保存 taskId，低频查询任务到终态；stopping 会等待待处理帧稳定。录制期间不要分析、遍历场景、截图、序列化或修改代码。
6. 分析停止缓冲，取 CPU/GC 热点与异常帧，再按需要读取精确线程/样本层级。已保存文件使用 load，替换缓冲前自动备份；加载后按新帧号查询。
7. 前后复测只改变待验证因素，其余分辨率、画质、目标、设备、输入、车数、阶段和采样设置一致。通过 compare 比较两份完成报告，保留原始数据和元数据。

## 解释和保护

- Self 扣除直接子节点，Total 含子节点；父子和跨线程时间不能相加。Present/VSync/GPU 等待不直接当作 CPU 计算热点，渲染线程时间不是 GPU Pass 时间。
- 采集 completedFrames 是索引跨度，retainedFrames/droppedFrames 表示停止环形缓冲的范围保留/裁减，不是上游生成或丢包计数。receivedFrameCallbacks 才是实际回调次数，历史 null 不补造；分析 completedFrames 是实际分析进度。需要完整帧证据时逐帧核 raw.valid。
- GC 字节来自 GC.Alloc 元数据；未知大小单列，字节总量为下界。分配量不等于 GC 回收耗时，峰刺必须读对应帧与相邻帧。
- samplePath 是样本祖先链，callstack 是文件实际捕获的栈；null 不补造调用关系。需要栈时仅对相关路径做单独诊断采样，其结果不混入普通基线。
- Editor-only 的反射、资源校验、IMGUI 和工具分配与运行时代码分开；场景总 GC 不冒充 Demo 独占 GC。
- `.data` 元数据不匹配时来源设置为 unknown，不拿当前 Editor 状态补填。超时、丢帧、未复现和不可用计数都明确报告。
- 开始或加载请求超时先查询状态，不重复启动。不得停止用户新录制；失败/取消恢复本工具改动，保留已有缓冲。
- 默认输出 `Temp/PerformanceTools`。验收证据连同 .data.metadata.json 另行归档；清理本次试跑状态，不删除用户报告。

接口及长期步骤见 [性能操作 runbook](../../../docs/runbooks/profile-unity-performance.md)。GC 源码优化配合 [unity-profiler-gc](../unity-profiler-gc/SKILL.md)，CPU 优化配合 [perf-optimize](../perf-optimize/SKILL.md)。
