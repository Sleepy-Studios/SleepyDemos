# 纸艺卡丁车原作音频维护

音源固定为 `kagit-kart` 提交 `da100b21ae1c5332d2843d21a9bb7b68b97e9c2a` 的 `src/audio/`，没有第三方音效或占位音乐。`export_audio.py` 使用本机 Edge 的 `OfflineAudioContext` 直接执行原作 DSP、乐器、配方与主题，渲染 44.1 kHz 立体声 PCM；Music 经 FFmpeg/libvorbis quality 7 保存为 OGG，短曲与音效继续保存 WAV。引擎由 `KagitKartEngineSynth` 实时合成原作节点。随机声音采用固定种子，使重新导出可复现。

## 重新导出

重新导出音乐需要带 libvorbis 的 FFmpeg（仅制作工具，游戏运行不依赖它）。将 FFmpeg 放入 PATH 后，在项目根目录执行；也可传入 `--ffmpeg D:/Tools/ffmpeg.exe`：

```powershell
python Tools/KagitKart/export_audio.py D:/Unity/ReferenceProjects/kagit-kart
```

工具校验原作提交，使用独立的隐藏浏览器配置和临时本地服务器，结束时关闭。输出保留既有 `.meta`，不打开 Unity。不要在参考源码存在未提交音频修改时导出。需要外部暂存时加 `--output-dir`；音乐离线仅保留原作混响，默认低通由运行时处理一次，短曲沿用同一音乐滤波。

曲目包括标题、领奖台、结算、四章比赛、四章开场、四章最后一圈及终点/最后一圈短曲。章节节拍沿用原作赛道配置：草原 118、博斯普鲁斯 96、冰川 128、书桌 124；最后一圈升两个半音、速度乘 1.12，并使用原作高潮段落。每首的 Start 包含第一次完整乐段，Loop 保存后续两个循环，保留交替声部和已建立的混响尾音；运行时按 DSP 时钟和实际采样数准确接续。

资产位于 `Assets/LoadResources/Demos/kagit_kart/Audio/SFX/`，Music、Stingers 为其子目录，遵守项目目前登记的 Demo 音频目录。`Bank_White`、`Bank_Pink`、`Bank_Crackle` 保存原作共享噪声，使用 PCM。`Data/AudioManifest.json` 记录来源版本、采样率、时长、渲染 PCM 的峰值/RMS 和循环属性；`Data/AudioBank.asset` 保存全部 AudioClip 引用，运行时由宿主 Loader 保持加载句柄。音乐导入仍使用 Vorbis quality 0.9，其余使用 PCM；全部为 DecompressOnLoad 且不预加载。DSP 初始化时预热短音效 PCM，按场景缓存；音乐及其他非短音效在分配声部时读取采样，旧声部引用耗尽后释放解码数据。退出 Demo 时销毁 DSP 并释放所有缓存；重赛和返回 Demo 菜单保留短音效缓存。不要改成 Streaming 或 CompressedInMemory：当前 DSP 需要 `AudioClip.GetData`。

Music 的 30 个源文件由 427.84 MiB WAV 缩为 60.46 MiB OGG，减少 85.9%；保留原 GUID、44.1 kHz、双声道和循环采样长度，播放代码与 Bank 引用不变。这是源文件体积优化，不能当作包体或运行内存同幅缩小的证据。源编码与 Unity 导入压缩均有损；调音后从原作源码重新导出，不对现有 OGG 反复转码。本地 Git 历史中的这批 WAV 同时清除，原始 PCM 可依据固定版本 WebAudio 源码重新渲染。

试听直接在 Project 选中 Music 内的 AudioClip，使用 Inspector 底部预览播放；实际低通、混音和比赛切换在 Demo 中试听。调整乐曲配方仍改原作音频制作源码并重新导出，已有固定版本校验需要按已审核的新音源同步；调整游戏音量沿用现有设置页，不新增试听插件。

2026-10-11 转码验证：30 首 OGG 的容器终点采样数与原 WAV 精确一致，GUID 和导入设置字节不变。Unity 编译成功；`OriginalAudioManifestResolvesCompletePlayableBank` 1/1，覆盖全部引用、时长、声道及音乐解码/长度（Unity 单采样舍入容差）；`NativeAudioMixerHasRealOutput` 1/1，覆盖比赛实际 Listener 出声和暂停/恢复。不代表人工音质对比、全量测试、Player 构建或 Android 真机验收。

后续维护使用 Unity Inspector 更新 Bank 引用与导入参数。新增音频必须带 `demo`、`audio`、`sfx` Label；原作标题与比赛曲目按上述 SFX/Music 目录保存。

## 宿主接线

- 创建 `KagitKartAudio`，加载 `Data/AudioBank` 后调用 `Initialize(bank, worldCamera)`。
- 创建本场先调用 `Bind(session)`，再以 `PlayIntro(chapterId)` 播放章节开场并关闭引擎；镜头结束调用 `BeginCountdown()`，启用引擎并播放第一声倒计时。比赛、道具、碰撞、暂停和最后一圈事件由音频自身订阅。
- 退出或重开比赛前调用 `Unbind()`；标题/选择页面调用 `PlayMenu()`，大奖赛最终领奖调用 `PlayMenu(true)`。
- 菜单点击与换页分别调用 `Click()`、`PageTurn()`；纸景折起通知使用 `Popup(原作位置, size)`。
- 设置滑块调用 `SetVolumes(music, sfx, muted)`。暂停遵循原作：比赛冻结、引擎渐静，音乐继续播放并降音量、低通滤波。

声音宿主随场景销毁退订所有事件，使用本地 AudioSource，不依赖新的公共音频框架。

加速声在每个音效声部预留两份 PCM 缓冲，复用滤波状态和数值随机内核；写未播放的缓冲，等待当前音频块结束后交换，保留本次实际帧数和播放长度。不能覆盖音频线程正在读取的数据，也不能用统一固定长度替代原事件配方。

引擎控制参数在音频块起点持短锁复制，逐采样平滑、滤波历史和振荡器相位由音频线程推进。主线程不持有整块引擎 DSP 锁；参数的目标、时间常数和 fresh 复位语义仍按原作执行。分配和线程等待的收益须通过新的同条件采样确认，算法对照与实际 Listener 输出分别验收。

## 验证与保真边界

运行 `Tests.Demo.KagitKartAudioTests` 检查原作换挡转速、Bank 与完整音频清单、实际加速持续时间、动态加速采样、引擎软削波和两级混音动力学。参考由真实 Edge OfflineAudioContext 生成，默认保存在 `Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/` 的 `OriginalAudioDynamics.json`、`OriginalAudioBoost.json` 与 `OriginalEngineSoftClip.json`，不参与 Demo 资源打包。动力学对照 16,384 帧输入，容差 0.0003；动态加速对照原作 start 配方，容差 0.003；引擎 2x 软削波对照滤波、延迟与峰值，容差 0.000005。只刷新参考可执行导出命令并加 `--checks-only`，外部诊断可再指定 `--reference-dir`。真实听感需在四章中验证开场、倒计时、漂移三档、道具警报、救援、最后一圈、终点和暂停。无需全量测试。

音乐及普通音效直接执行原作算法。加速声由 `KagitKartBoostSynth` 根据本次 `LastBoostDuration` 连续合成，重叠加速不借用已延长的计时；落地、撞墙、碰撞、纸景音效使用满强度源码采样，并在空间裁声之后按实际 AirTime、速度、冲量和景物波次尺寸缩放。

`KagitKartAudioDsp` 将本 Demo 全部声部汇总到一个立体声 AudioSource。音乐切换、最后一圈切拍、结算、暂停遵循原作时序；汇总后处理 MAKEUP=1.25、Glue（-18dB/10dB软膝/3:1/5ms/200ms）与 Limiter（-3dB/硬膝/20:1/1ms/100ms）。采用 Blink/WebAudio 指数软膝、32 帧包络、自动补偿、自适应恢复和每级 6ms 前瞻；许可随 `Data/AudioNotices.txt` 保存。Unity 原生 Compressor 固定∞:1且攻击时间最低10ms，无法表达上述参数，所以使用本 Demo 独立 DSP，不修改全局 AudioListener。

引擎按原作八层播放器与三个近车声部实时生成：双失谐锯齿与方波低音经过 1024 点软削波、Blink 128/256 tap 半带 2x 重采样、油门低通与可变深度 putt 调制；flutter 固定 1.9kHz 噪声谱，轮速只改变 4–95Hz 方波调制；不同地面改变 rattle 滤波与实际增益，漂移分别改变尖响滤波和带 7Hz/25音分调制的三角波；boost、尾流和无敌的滤波、音量、Pan、近车 Doppler 与全部时间常数沿用 `engine.js`。暂停只关闭共享引擎 gate，内部相位与噪声时钟继续推进。

采样仍有平台实现边界：普通纸质音效保存一次源码随机实现，PCM 噪声量化、Vorbis 音乐解码及锯齿/方波的 PolyBLEP、三角波解析生成与浏览器内部频带 wavetable 不保证逐采样一致；固定噪声谱、动态调制深度、连续参数与混音时序不使用循环变速或固定默认值替代。

运行时低通/高通的 Q 按 WebAudio dB 单位转换，bandpass 保留线性 Q。动态噪声起点按原生 rate=1 路径取最近采样，保持随机流与原作一致。依据：[Web Audio BiquadFilterNode](https://www.w3.org/TR/webaudio/#dom-biquadfilternode-q)、[Blink AudioBufferSource](https://raw.githubusercontent.com/chromium/chromium/main/third_party/blink/renderer/modules/webaudio/audio_buffer_source_handler.cc)。

Unity 原生压缩器差异依据：[Unity Audio Compressor](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioCompressor.html)。动力学移植参考：[Blink DynamicsCompressor](https://raw.githubusercontent.com/chromium/chromium/main/third_party/blink/renderer/platform/audio/dynamics_compressor.cc)。

## 2026-10-11 原生输出与事件桥修正

倒计时按源退出赛前曲（0.9秒）；撞墙保留本车0.2秒与world共享桶0.02秒两级节流，tier/threat/rouletteTick烘焙变体使用同一源配方声桶。全局硬件首次暂停、TT重赛、静音不额外响click，已暂停P继续和UI确认仍保留。纸景SceneryWorld真实目标抬起波次接入Audio.Popup，原源size已补存四World资产，关闭当前场精确退订；待播波次按DSP时间交付，Unbind清队列。

原生 `NativeAudioMixerHasRealOutput` 1/1（9c1f1c0d）：Intro章节曲→倒计时无新背景曲→比赛章节曲、真实P暂停与UI Enter继续、DSP音乐游标及最终Listener PCM，peak=0.6997126。原生八道具测试1/1（19bc564c）核每件新声路由与最终PCM，peak=0.6655096、48000Hz；退出核DSP载体销毁。实际测试发现当前静音载体的AudioSource历史采样为零，而Listener最终混音有效，因此采用[AudioListener.GetOutputData](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioListener.GetOutputData.html)。未改变全局静音/音频设置或持久化偏好。非零总线不等于逐声隔离，也没有人工听过音色、声像和响度；原11项算法/资产证据与这次运行证据分开使用。
