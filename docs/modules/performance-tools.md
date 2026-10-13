# 性能采集与分析工具

## 职责与入口

`Assets/Scripts/Core/Editor/PerformanceTools/Core` 维护 Profiler 录制任务、原始帧读取、CPU/GC 聚合、数据来源元信息和报告比较，属于 `Core.Editor`。`Bridge` 使用独立的 `Core.Editor.Performance.UnitySkills` 程序集，只负责把 `sd_profiler_` 接口接到现有 UnitySkills；Core 不引用 UnitySkills 或 Hotfix。

工具适配 Unity 6000.3.15f1，扩展不修改上游 UnitySkills。CPU 和 GC 使用同一个分析入口，不保留第二个 GC 解析器。

## 数据与生命周期

工具按需初始化，订阅 Editor 和 Profiler 事件；batchmode 与 Player 构建期间不启动采集。任务串行，状态为 recording、stopping、analyzing 或终态 completed / timed_out / cancelled / failed / interrupted。Domain Reload 会中断任务并保留终态，不检查 Hot Reload。

开始采集拒绝接管已有录制。旧缓冲尚未有可追溯保存文件时先备份，然后清空缓冲，防止分段文件反复包含上一段数据。CPU 模块、Editor 采样和分配栈设置由工具暂时调整，结束后恢复；用户在收尾阶段开始的新录制不被停止。

停止录制后等待待处理帧稳定，记录采集索引范围与最终保留交集；环形缓冲裁减不能隐藏。每次生成 `.data`、`.data.metadata.json` 和任务结果；元数据绑定文件长度、SHA-256、采集条件和帧范围。加载前保护现有数据：已保存缓冲对应文件仍存在、长度与记录一致且 SHA-256 匹配时才能复用原文件作为备份；文件被替换、删除、不可读或内容不匹配时重新保存当前停止缓冲。验证旁文件后映射加载后的帧号；来源不明或不匹配时明确 unknown，包括历史 Unity 版本和采样设置。

`Temp/PerformanceTools` 保存短期数据和 JSON 报告。重要证据应另行归档原始文件及元数据。任务只保留最新一项，但历史报告可通过路径读取。失败与取消不会删除已有采样文件；已备份的前一缓冲可通过返回的 backupPath 恢复。

## 计量口径

- 帧耗时来自原始帧，不把线程耗时相加；CPU Total 含子节点，Self 扣除直接子节点。等待 Marker 不表示计算热点。
- 采集任务的 `CompletedFrames` 是首尾索引跨度，`RetainedFrames` 是该跨度与停止缓冲的交集，`DroppedFrames` 是环形缓冲范围裁减量；它们不证明上游生成帧数或传输丢包数。`ReceivedFrameCallbacks` 单独记录本任务实际收到的回调次数，旧任务与历史文件没有该证据时为 null，不能从索引跨度补填。分析任务的 `CompletedFrames` 表示实际分析进度。
- GC.Alloc 元数据决定字节量，缺少大小时单列 unknownSizeEvents；总量是下界。ProjectMethod 是样本祖先链中最近的项目程序集方法，包含其触发的下层分配，不表示该方法直接 new。
- samplePath 为 Profiler 祖先链，callstack 为实际采集到的栈。缺栈不编造源码路径；Editor、测试和资源模拟开销须结合下层栈判断。
- 报告包括均值、P50/P95/P99、异常帧计数和分配率。设置不一致的比较返回警告，只能做描述性比较。

分析按约 8ms 片段让出 Editor 更新；调用栈符号只在同一个停止缓冲的分析任务内缓存，不跨文件复用地址。Unity 原生 SaveProfile / LoadProfile 仍为同步操作。录制窗口内禁止解析或大量工具调用，避免把诊断开销算进业务性能。

## 维护与验证

正式操作与参数见 [性能分析 runbook](../runbooks/profile-unity-performance.md)。采集和分析使用 [unity-performance](../../.agents/skills/unity-performance/SKILL.md)，分配定位使用 [unity-profiler-gc](../../.agents/skills/unity-profiler-gc/SKILL.md)，CPU 热点修复使用 [perf-optimize](../../.agents/skills/perf-optimize/SKILL.md)；三个技能从 `.agents/skills` 维护并单向发布。升级 Unity 或 UnitySkills 后验证实际 schema、帧与 GC 元数据、保存加载和中断恢复。

工具不切换 Profiler 目标、不自动进入/退出 Play Mode、不切换 Deep Profile，不提供 Memory Profiler 快照或 Frame Debugger 自动化。Editor 结果不能替代 Player/IL2CPP 或真机结果。

2026-10-13 定向验证覆盖保存/加载、原文件被同长度内容替换后的备份保护，以及错误 `.data` 加载失败后的缓冲恢复：恢复后 978 帧保留，第 10 帧前 20 个样本逐字一致。PlayMode 工具定向检查 1/1；中断验证直接调用真实 BeforeReload hook，没有执行实际 Domain Reload。错误文件测试产生的两条预期 Console 错误保留在日志中；这些检查不代表全量测试或真实重载验收。
