# 已验证的项目定位经验

2026-10-13 的 KagitKart 分配栈诊断使用 301 个正常比赛帧。以下数字是优化前的观测，不代表优化后收益；正式比较使用关闭调用栈采集的修正后基线。

- `KagitKartInput.CaptureFrame` 每帧两次动作名拼接，共 74 B/帧。使用完整字符串常量保留原动作、锁存及设备门闩；真实玩家的固定步读取也采用同一方式。三端输入的既有 PlayMode 检查通过。
- `KagitKartAudio.UpdateCpus` 通过 `IReadOnlyList` 枚举赛车，为 32 B/帧；`KagitKartItemVisuals` 对 Hazards / Projectiles 的接口枚举各为 40 B/帧。改用索引循环，保持顺序；具体集合为数组或 `List<T>` 的 foreach 不机械替换。
- `KagitKartHudNotice` 的时间格式化产生装箱及字符串；圈数、车数、道具数量即使不变也反复生成文字。数字直接使用 TMP 格式化，静态内容仅变化时提交，百分秒仍按原作逐帧向下截取。当前 TMP 的 `UNITY_EDITOR` 分支会为 Inspector 创建显示字符串，须与 Player 的项目主动分配区分，不能宣称整个 Editor 为零。
- Flux 的 Handler / 订阅快照应在注册关系变化时创建，派发只持有当前快照。回调中增删及嵌套派发的稳定顺序是公共契约；不能仅把 ToArray 移成共享可变列表。
- 一处 36.44ms 帧及邻帧中，`File.Read` 共约 15.9ms，调用链为音效加载。释放后重复读取 PCM 是明确根因；同窗口存在分配不证明该峰刺来自 GC 回收。

场景音效缓存退出时释放；生成音效的复用必须保留独立播放长度，并在音频线程完成当前块后交换缓冲。音频算法和实际 Listener 输出分别验证，Editor 全局静音会使后者接近零。
