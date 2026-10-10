# 纸艺卡丁车原作音频维护

音源固定为 `kagit-kart` 提交 `da100b21ae1c5332d2843d21a9bb7b68b97e9c2a` 的 `src/audio/`，没有第三方音效或占位音乐。`export_audio.py` 使用本机 Edge 的 `OfflineAudioContext` 直接执行原作 DSP、乐器、配方与主题，输出 44.1 kHz WAV。引擎由 `KagitKartEngineSynth` 实时合成原作节点。随机声音采用固定种子，使重新导出可复现。

## 重新导出

在项目根目录执行：

```powershell
python Tools/KagitKart/export_audio.py D:/Unity/ReferenceProjects/kagit-kart
```

工具校验原作提交，使用独立的隐藏浏览器配置和临时本地服务器，结束时关闭。输出保留既有 `.meta`，不打开 Unity。不要在参考源码存在未提交音频修改时导出。需要外部暂存时加 `--output-dir`；音乐离线仅保留原作混响，默认低通由运行时处理一次，短曲沿用同一音乐滤波。

曲目包括标题、领奖台、结算、四章比赛、四章开场、四章最后一圈及终点/最后一圈短曲。章节节拍沿用原作赛道配置：草原 118、博斯普鲁斯 96、冰川 128、书桌 124；最后一圈升两个半音、速度乘 1.12，并使用原作高潮段落。每首的 Start 包含第一次完整乐段，Loop 保存后续两个循环，保留交替声部和已建立的混响尾音；运行时用 `PlayScheduled` 准确接续。

资产位于 `Assets/LoadResources/Demos/kagit_kart/Audio/SFX/`，Music、Stingers 为其子目录，遵守项目目前登记的 Demo 音频目录。`Bank_White`、`Bank_Pink`、`Bank_Crackle` 保存原作共享噪声，使用 PCM。`Data/AudioManifest.json` 记录来源版本、采样率、时长、峰值、RMS 和循环属性；`Data/AudioBank.asset` 保存全部 AudioClip 引用，运行时由宿主 Loader 保持加载句柄。音乐使用 Vorbis，其余使用 PCM；全部为 DecompressOnLoad 且不预加载，DSP 仅在分配声部时读取采样，旧声部引用耗尽后释放解码数据。

后续维护使用 Unity Inspector 更新 Bank 引用与导入参数。新增音频必须带 `demo`、`audio`、`sfx` Label；原作标题与比赛曲目按上述 SFX/Music 目录保存。

## 宿主接线

- 创建 `KagitKartAudio`，加载 `Data/AudioBank` 后调用 `Initialize(bank, worldCamera)`。
- 创建本场先调用 `Bind(session)`，再以 `PlayIntro(chapterId)` 播放章节开场并关闭引擎；镜头结束调用 `BeginCountdown()`，启用引擎并播放第一声倒计时。比赛、道具、碰撞、暂停和最后一圈事件由音频自身订阅。
- 退出或重开比赛前调用 `Unbind()`；标题/选择页面调用 `PlayMenu()`，大奖赛最终领奖调用 `PlayMenu(true)`。
- 菜单点击与换页分别调用 `Click()`、`PageTurn()`；纸景折起通知使用 `Popup(原作位置, size)`。
- 设置滑块调用 `SetVolumes(music, sfx, muted)`。暂停遵循原作：比赛冻结、引擎渐静，音乐继续播放并降音量、低通滤波。

声音宿主随场景销毁退订所有事件，使用本地 AudioSource，不依赖新的公共音频框架。

## 验证与保真边界

运行 `Tests.Demo.KagitKartAudioTests` 检查原作换挡转速、Bank 与完整音频清单、实际加速持续时间、动态加速采样、引擎软削波和两级混音动力学。参考由真实 Edge OfflineAudioContext 生成，默认保存在 `Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/` 的 `OriginalAudioDynamics.json`、`OriginalAudioBoost.json` 与 `OriginalEngineSoftClip.json`，不参与 Demo 资源打包。动力学对照 16,384 帧输入，容差 0.0003；动态加速对照原作 start 配方，容差 0.003；引擎 2x 软削波对照滤波、延迟与峰值，容差 0.000005。只刷新参考可执行导出命令并加 `--checks-only`，外部诊断可再指定 `--reference-dir`。真实听感需在四章中验证开场、倒计时、漂移三档、道具警报、救援、最后一圈、终点和暂停。无需全量测试。

音乐及普通音效直接执行原作算法。加速声由 `KagitKartBoostSynth` 根据本次 `LastBoostDuration` 连续合成，重叠加速不借用已延长的计时；落地、撞墙、碰撞、纸景音效使用满强度源码采样，并在空间裁声之后按实际 AirTime、速度、冲量和景物波次尺寸缩放。

`KagitKartAudioDsp` 将本 Demo 全部声部汇总到一个立体声 AudioSource。音乐切换、最后一圈切拍、结算、暂停遵循原作时序；汇总后处理 MAKEUP=1.25、Glue（-18dB/10dB软膝/3:1/5ms/200ms）与 Limiter（-3dB/硬膝/20:1/1ms/100ms）。采用 Blink/WebAudio 指数软膝、32 帧包络、自动补偿、自适应恢复和每级 6ms 前瞻；许可随 `Data/AudioNotices.txt` 保存。Unity 原生 Compressor 固定∞:1且攻击时间最低10ms，无法表达上述参数，所以使用本 Demo 独立 DSP，不修改全局 AudioListener。

引擎按原作八层播放器与三个近车声部实时生成：双失谐锯齿与方波低音经过 1024 点软削波、Blink 128/256 tap 半带 2x 重采样、油门低通与可变深度 putt 调制；flutter 固定 1.9kHz 噪声谱，轮速只改变 4–95Hz 方波调制；不同地面改变 rattle 滤波与实际增益，漂移分别改变尖响滤波和带 7Hz/25音分调制的三角波；boost、尾流和无敌的滤波、音量、Pan、近车 Doppler 与全部时间常数沿用 `engine.js`。暂停只关闭共享引擎 gate，内部相位与噪声时钟继续推进。

采样仍有平台实现边界：普通纸质音效保存一次源码随机实现，PCM 噪声量化、Vorbis 音乐解码及锯齿/方波的 PolyBLEP、三角波解析生成与浏览器内部频带 wavetable 不保证逐采样一致；固定噪声谱、动态调制深度、连续参数与混音时序不使用循环变速或固定默认值替代。

运行时低通/高通的 Q 按 WebAudio dB 单位转换，bandpass 保留线性 Q。动态噪声起点按原生 rate=1 路径取最近采样，保持随机流与原作一致。依据：[Web Audio BiquadFilterNode](https://www.w3.org/TR/webaudio/#dom-biquadfilternode-q)、[Blink AudioBufferSource](https://raw.githubusercontent.com/chromium/chromium/main/third_party/blink/renderer/modules/webaudio/audio_buffer_source_handler.cc)。

Unity 原生压缩器差异依据：[Unity Audio Compressor](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioCompressor.html)。动力学移植参考：[Blink DynamicsCompressor](https://raw.githubusercontent.com/chromium/chromium/main/third_party/blink/renderer/platform/audio/dynamics_compressor.cc)。
