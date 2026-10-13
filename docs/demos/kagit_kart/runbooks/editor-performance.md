# 采样纸艺卡丁车 Editor 性能

## 适用范围

复查正常道路长圈的帧分布、自然规则事件、GC / 托管内存与音乐状态。长圈场景驱动复用现有 `KagitKartScenePlayModeTests.EditorTiming.cs` 中两个方法，归 `Tests.PlayMode`；CPU/GC 录制、分析和报告比较统一使用 [正式性能工具](../../../modules/performance-tools.md)，操作见 [性能分析 runbook](../../../runbooks/profile-unity-performance.md)。不另建测试程序集或分析器。

## 运行

1. 确认 Windows Editor、有效 GPU 与音频设备，Game View 为 1920×1080并获得焦点；记录Unity版本、GPU、图形API、画质及是否开启Profiler。
2. 通过 [Unity Test Runner](../../../runbooks/run-unity-tests.md) 串行运行精确方法：
   - `Tests.Demo.KagitKartScenePlayModeTests.NormalDrivingRecordsLongEditorTimingsForChaptersOneAndTwo`
   - `Tests.Demo.KagitKartScenePlayModeTests.NormalDrivingRecordsLongEditorTimingsForChaptersThreeAndFour`
3. 方法标记 `RequiresGPU`、`RequiresAudioDevice`、`EditorPerformance`，只在需要性能复查时选中，不默认纳入普通功能验证。
4. 普通赛120g、肉桂狐与七名AI，正式原作AI驾驶玩家；正常完成电影、倒计时和道路驾驶，不快进、瞬移或强制特效。每章采样至少45秒且完成一圈，JSON和截图在测量窗口外输出。
5. 测量期间仅临时关闭Editor Game View VSync，finally恢复原值；这与项目 `QualitySettings.vSyncCount` 不同。完成后返回Hub并检查世界释放，不修改正式资源、项目时间或用户画质偏好。

## 读取结果

外部美术目录 `SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/EditorTiming_<chapter>_LongRoad_<UTC时间>.json` 与同名PNG保留完整samples、分位、峰值、自然事件、可用CPU/GC/内存计数和音乐路由。来源布局见 [独立美术源规范](../../../runbooks/maintain-art-sources.md)。记录测试实际范围、报告job与未验证项，不把采样成功计为全量回归。

帧耗时来自 `Time.unscaledDeltaTime`；内存包括Editor、公共资源和采样自身。ProfilerRecorder不可用时保留-1，不作为0成本；Main/Render Thread是CPU标记，不是逐Shader GPU成本。自然道具、救援与音频事件不保证每次均发生，路由非零也不替代人工听感。单个长圈窗口不证明整场三圈、所有特效组合或Player/真机稳定60帧。

旧三秒起步、四章长圈数字与Editor呈现等待对照保留在 [性能历史](../../../agent/archive/kagit-editor-performance.md)。后续定因使用 `sd_profiler_` 读取具体慢帧；不要据旧机器结果宣称最新版本已通过。

## 优化前后复测

先修正章节太阳强度和环境光，再用修正后的画面建立基线；错误光照窗口不能与正确画面直接比较收益。固定同一设备、Game View、画质、输入、车数和比赛阶段，每章重复配对长圈；原始 `.data` 连同 `.data.metadata.json` 保存，逐段核对 retainedFrames 和 droppedFrames。采样期间不分析、不截图，不把环形缓冲最后一段当作完整长圈。

调用栈与额外 Marker 诊断单独采集，不能混入普通基线。区分帧耗时、Main/Render Thread、音频线程、等待、项目 GC 与 Editor/Test Runner 分配；音频参数同步的等待应结合主线程 Marker 和对应音频块确认，不能只凭总帧差推断 DSP 计算减少。

正式收益和未验证项只在完成对应新采样后更新 [验收对照](acceptance.md)。单次短诊断、单帧峰值和重新解析旧文件均不代表前后复测。

2026-10-13 最新收口按用户要求优先完成已授权优化和最小相关复测，不再为重采基线回切整套旧源码。旧 1080p 基线没有本次 Main 场景 SMAA High、后处理默认值以及新 UI / 当前业务偏好，必须作为历史窗口单列，不能据它和最新窗口的差值声称严格同条件的整体优化收益。最终运行稳定性、正式 CPU/GC 归因与条件差异记录在[本次性能报告](performance-2026-10-13.md)；当前八场长圈和两章 4K 补充的结果由实际完成记录回填。

## 2026-10-13 已确认的范围

- 修正画面后的四章两次基线合计 56 段、52,631 帧，已逐帧确认 raw.valid。旧采集没有实际回调计数，不能据索引跨度补写 receivedFrameCallbacks，也不能称已验证上游传输零丢包。
- 四章同帧渲染 A/B：每章比较 8,294,400 字节，关闭无消费者的 SSAO 与颜色复制后 changedBytes 均为 0（`fcec30e6`）。这证明所拍四帧的像素相同，不替代动态长圈性能、所有特效或全部设备验收。
- 世界相机渲染作用域、其它相机、原颜色选项、SSAO 活跃实例及卸载恢复定向检查 1/1（`a496d22e`）。正式实现只在世界相机 begin/end 期间停用这两项；保留 `FinalDepthPass`，不修改共享管线资产或销毁共享 Feature。
- 单场成绩/重赛 1/1（`bb352753`）、GP 流程 1/1（`7463de89`）、原生八道具和最终混音输出 1/1（`fe36cc3a`）。音频验证临时解除 Editor 全局静音，收口须核对原值恢复；这些功能检查不等于人工听感或完整性能复测。

优化后正式八场长圈及全部报告分析尚未在本节记录收益，完成后再更新均值、分位、分配、线程等待和未解决项。未构建 Player，未做 Android、真实手柄/触屏或全量回归。
