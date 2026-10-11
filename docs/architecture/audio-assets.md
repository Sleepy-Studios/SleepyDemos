# 音频资源与导入规范

适用于项目自有正式音频，包括公共资源和各 Demo。以下阈值与质量参数是 SleepyDemos 的项目约定，不是 Unity 官方统一标准；第三方插件和测试参考数据不纳入正式音频资产规则。

## 源文件与原稿

- **WAV 文件超过 2 MiB（2,097,152 字节），必须从无损原稿导出为 OGG 后再提交。** 不设时长门槛；恰好 2 MiB 或更小的 WAV 可以保留。该限制针对 WAV 源文件，不要求 OGG 也小于 2 MiB。
- 无损原稿保存在工程外，位置遵循[独立美术源稿约定](../runbooks/maintain-art-sources.md)，或保留可复现的音频生成源码及版本。正式运行和构建只依赖游戏仓库中的资产。
- 每次调整从无损原稿重新导出，不对已有 OGG、MP3 反复有损转码。Unity 可能再次编码导入的有损音频，因此源文件变小不代表音质、包体或运行内存同幅改善。
- 转换已有资产时保留原 `.meta` 和 GUID，核对 AudioClip、Prefab、资源 Bank 及加载地址引用。不同时提交已替换的 WAV 和 OGG；删除工作树文件不会自动清除 Git 历史，历史清理另行处理。
- 资源目录、命名及 Label 沿用[资源命名规范](asset-naming.md)，按真实播放用途选模板，不仅根据目录名或扩展名判断。

## 导入模板

四个 Unity 原生 AudioImporter Preset 位于 `Assets/Settings/ImportPresets/`，仅在 Editor 手动套用，不注册全局默认规则。每个包含 Default、Standalone 和 Android 设置，Default 与 Standalone 一致；Standalone 是 Windows 使用的平台分组，也会影响该分组的其他桌面目标。

使用时选中 AudioClip，在 Inspector 顶部打开 Preset 选择器，选取对应模板，再点击 Apply 并核对平台页；模板不会自动将 WAV 转成 OGG。

| Preset | 用途 | Load Type | Standalone 编码／质量 | Android 编码／质量 | Preload Audio Data | Load In Background |
|---|---|---|---|---|---|---|
| `AudioShortClear` | 按钮、提示等清晰短音效 | Decompress On Load | PCM | PCM | 开 | 关 |
| `AudioShortNoise` | 脚步、撞击等密集噪声音效 | Decompress On Load | ADPCM | ADPCM | 开 | 关 |
| `AudioVoice` | 普通语音 | Compressed In Memory | Vorbis／0.7 | Vorbis／0.6 | 关 | 开 |
| `AudioMusic` | 普通背景音乐 | Streaming | Vorbis／0.8 | Vorbis／0.7 | 关 | 开 |

- 四类均使用 Preserve Sample Rate，不统一降低 Android 采样率。Vorbis 的质量值是试听起点，不是固定码率；PCM、ADPCM 不使用质量滑条调节压缩。
- Preset 只应用默认／平台采样设置与后台加载设置，排除声道选择、归一化、Ambisonic、Label、资源包名称、User Data 等无关属性。Force To Mono 按用途单独设置；音乐保留需要的立体声，3D 点声源可按听感选择单声道。
- 平台覆盖列表属于 Preset 的整体设置。已有 iOS、WebGL 等其他平台覆盖的资产，应先记录这些覆盖并在应用后恢复，或手动抄入本表设置；不要用模板覆盖丢失既有配置。
- 短音效需要低延迟，默认预加载；语音和音乐按需加载，并在需要出声前准备数据。后台加载不保证立即就绪，不能把首次播放延迟当作可接受的默认行为。
- 长语音、环境音、多个叠加音乐声部等按实际内存和解码成本调整 Load Type。ADPCM 不作为音乐或平滑音色的默认编码；Streaming 也不用于所有短音效。

### 专用 DSP

依赖 `AudioClip.GetData` 读取压缩音频采样时，必须使用 Decompress On Load，并处理加载就绪与读取失败；不能套用 Compressed In Memory 或 Streaming 模板。需要精确采样的算法还须单独确定编码、声道与采样率，在所属模块文档记录原因。

纸艺卡丁车的独立 DSP 属于该情况，继续遵守其[音频维护文档](../demos/kagit_kart/runbooks/audio.md)，不批量套用普通音乐模板。`GetData` 的加载限制不豁免 WAV 源文件大小规则，也不意味着 OGG 导入后无需解码。

## 验证与性能口径

- **源文件体积**：统计仓库内 WAV、OGG 等实际字节数，用于资源和 Git 体积管理。
- **导入／构建体积**：检查目标平台 Inspector 的导入统计，以及实际构建或资源包结果；不直接用源文件大小代替。
- **运行内存与 CPU**：记录实际同时加载／播放的音频及托管采样缓存，结合 Profiler 的 Audio 内存、DSP CPU、Streaming CPU 检查。压缩源文件不代表解码后的内存减少。
- 修改音频或导入设置后，检查引用、首播、循环接缝、切换及暂停恢复，并试听底噪、失真和声道。存在采样契约时核对采样数、声道及采样率。
- Editor 回读参数或通过模拟测试，不代表 Player 构建、Android 真机音质和性能已通过；只报告实际执行的验证范围。

## 官方依据

- [Unity 6.3 Audio Clip 导入设置](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioClip.html)：编码、加载方式、预加载及平台覆盖。
- [Unity 6.3 音频压缩](https://docs.unity3d.com/6000.3/Documentation/Manual/AudioFiles-compression.html)：编码取舍及重新编码。
- [AudioClip.GetData](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioClip.GetData.html)：读取采样的加载限制。
- [Unity 原生 Preset](https://docs.unity3d.com/6000.3/Documentation/Manual/Presets.html)：保存和复用导入设置。
