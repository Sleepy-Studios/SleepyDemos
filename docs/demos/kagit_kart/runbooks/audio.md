# 纸艺卡丁车音频维护

## 当前播放契约

所有平台统一使用 `KagitKartAudio` 持有的原生 `AudioSource`：四个音乐声部负责两组 Start/Loop 的排程与交叉淡化，28 个音效声部按重要性和频率门闩复用，六个循环声部播放玩家怠速/行驶/漂移及最近三名对手的引擎。没有 `OnAudioFilterRead`、运行时整曲 PCM 副本、实时 Boost 合成或音频线程交接，也不保留第二套桌面 DSP 后端。

音源仍来自固定 `kagit-kart` 提交 `da100b21ae1c5332d2843d21a9bb7b68b97e9c2a`。章节、末圈、结算、领奖曲、短曲和短音效沿用原导出；引擎为同一版本 `createEngines` 离线渲染的两秒循环，尾部80ms与起点前的连续采样交叉淡化。运行时通过转速变调和怠速/行驶权重改变听感，漂移叠加原作漂移采样；Boost 选择既有五类原作采样，并按实际持续时间小幅变调，不再逐采样复现动态配方。

优先保证稳定听感与可维护性，不再承诺逐采样 WebAudio 动力学、滤波器或引擎波形一致。暂停保留音乐位置并降音量，引擎渐静；恢复不重播历史事件。公共主音量、Music/Effects/Ui、静音继续乘入本地音量平方曲线，不修改 AudioListener，不新增全局音频管理器。

## 资源与导入

资源仍在 `Assets/LoadResources/Demos/kagit_kart/Audio/SFX/`；Music 和 Stingers 为其子目录。`Data/AudioBank.asset` 仅直接引用短音效、短曲和引擎，音乐保存无扩展名的资源地址；不能重新把全部音乐拖回 Clip 数组，否则进入菜单就会依赖所有曲目。`Data/AudioManifest.json` 保存来源、采样率、时长、声道、峰值/RMS及循环标记。

- 30首 Music OGG 默认使用 Streaming、后台加载、不预加载；不再因 `GetData` 契约强制整曲 DecompressOnLoad。WebGL 单独覆盖为 AAC、DecompressOnLoad、Preload Audio Data：Loader 取得所需 Clip 时触发平台自动解码，脚本等待加载完成；这不表示启动取得全部音乐。Web/小游戏运行时由对应平台处理解码，不能把原生 Streaming 行为和内存预算直接套到小游戏。
- 短音效、Stingers 与 `EngineIdle`、`EngineDrive`、`EngineDrift` 使用 DecompressOnLoad；引擎默认保留44.1kHz双声道PCM、不预加载、后台加载，WebGL 同样覆盖为 AAC、DecompressOnLoad 和预加载。每个引擎源文件352,844字节，低于项目WAV阈值。Unity Web Inspector 提供 AAC；维护覆盖时先取得平台已转换的采样设置，保留 Web 的采样率覆盖，不能直接照搬默认设置。实际编码后的循环接缝需要在容器试听。
- 音乐按 Clip 独立打 Bundle，通过现有资源 Loader 异步取得；不与其它章节的音乐合包。更新导入设置和 Bank 使用 Unity Inspector 或正式 Editor 资产入口，保留既有 GUID；不得通过删除重建曲目改变引用。
- WAV超过2MiB仍必须从无损原稿导出OGG。音乐源文件变小不等于运行内存等比例变小，目标容器的实际解码驻留仍需真机验收。

Bank保留原作噪声资产用于来源维护，播放器不读取其PCM。引擎和Boost仅消费正式采样，不生成临时AudioClip。

## 准备、切曲与释放

`InitializeAsync(bank, camera, token)` 等待短音效、短曲、引擎及标题曲就绪，正式场景 Loading 覆盖此阶段。`PrepareRaceAudioAsync(chapter, token)` 只准备当前章开场 Start/Loop，不把整章14首音乐常驻。开场播放时预热普通比赛曲，进入倒数第二圈时预热末圈曲，末圈入曲时预热结果曲；菜单选择的过期音乐和已结束过渡曲由主线程解除音源引用并释放 Loader 句柄。

标题/开场/比赛/末圈/结果/领奖沿用原事件桥。音乐在实际加载完成后排程，Start按自身运行时采样数/频率接续Loop；最后一圈保持原章节节拍与已导出的升调/加速曲。异步请求带播放版本，旧加载不能覆盖较新的音乐请求。加载等待共享AsyncLazy，调用者取消自身等待不撤走其他等待者所需数据。

有限音效池只截断最早结束的声部来保障重要提示；普通事件池满则跳过，不无限创建AudioSource。所有音源是场景子对象，退出先停止音源，再释放宿主Loader与Bank。场景销毁取消加载并退订比赛、道具、碰撞、暂停及公共设置事件。

音乐数据按共享 AudioClip 记录宿主持有数，每个宿主对同一 Clip 只计一次；场内淘汰与销毁均配对释放，最后一个宿主退出持有后才卸载原生音频数据。Editor 直启重载的新旧场景短暂重叠时，旧宿主不能卸载新宿主仍在预热或播放的同一 Clip。Bank 资产句柄由各 World 的 Loader 持有，音乐句柄由各 Audio 实例的 Loader 持有。Web 等待平台自动加载状态，不调用原生 LoadAudioData/UnloadAudioData，资源释放交给 Loader；实际容器解码与缓存仍须单独验证。

## 重新导出

导出工具为正式制作入口，不参与游戏运行；需要本机Edge。完整曲目导出还需要带libvorbis的FFmpeg：

```powershell
python Tools/KagitKart/export_audio.py D:/Unity/ReferenceProjects/kagit-kart
```

只更新三个引擎循环，不重新生成音乐：

```powershell
python Tools/KagitKart/export_audio.py D:/Unity/ReferenceProjects/kagit-kart --engines-only
```

工具校验固定源码提交及音频/赛道定义的未提交修改；支持 `--output-dir` 暂存与 `--ffmpeg` 指定编码器。音频使用独立隐藏Edge配置和本地服务器；关闭进程树后清理临时配置。导出保留已有 meta，新音频经 Unity 导入后维护 Bank：短音效/引擎用引用，音乐用地址。不要对现有 OGG 反复有损转码。

## 验证与边界

复用 `KagitKartAudioTests` 的六个转速曲线参数检查。PlayMode运行 `OriginalAudioManifestResolvesCompletePlayableBank` 核对全部Bank引用、长度/声道、音乐导入与三份引擎采样的非静音和循环接缝；`NativeAudioMixerHasRealOutput` 验证重叠宿主场内淘汰/销毁后的共享音乐持有、最后持有者释放，以及真实暂停/恢复和Listener非零输出；既有原生救援和八道具用例继续覆盖事件路由及离场音源释放。不再保留已删除DSP的分页、压缩器、软削波和三缓冲实现测试。

实际试听覆盖四章开场、倒计时、油门换挡、漂移三档、道具/警报/救援、最后一圈、冲线、结算、领奖、暂停/恢复、静音和公共总线。检查切曲延迟、重叠音量、循环爆音、后台恢复和退出残音。Web/小游戏首个用户交互后才能出声，目标容器的实际采样率、加载策略与循环接缝需单独验收；Editor Listener非零不代表小游戏音质或性能通过。

本次没有使用Profiler，也不承诺帧率或内存峰值的测量收益。旧DSP记录见 [2026-10-14历史性能记录](performance-2026-10-14.md)，仅证明当时实现与条件；其中整章PCM、PreparePcm/AudioBlock标记和原算法通过数均不属于当前播放器验收。
