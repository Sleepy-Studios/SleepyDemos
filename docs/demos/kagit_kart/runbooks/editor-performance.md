# 采样纸艺卡丁车 Editor 性能

## 适用范围

复查正常道路长圈的帧分布、自然规则事件、GC / 托管内存与音乐状态。当前正式采样入口是现有 `KagitKartScenePlayModeTests.EditorTiming.cs` 中两个方法，归 `Tests.PlayMode`；没有新增独立测试程序集或性能框架。

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

旧三秒起步、四章长圈数字与Editor呈现等待对照保留在 [性能历史](../../../agent/archive/kagit-editor-performance.md)。一次性CPU诊断方法已清理，后续定因使用现有Unity Profiler与具体慢帧；不要据旧机器结果宣称最新版本已通过。
