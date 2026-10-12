# Hotfix 构建历史快照

> 从热更新模块移出的历史记录；仅描述对应日期的生成、失败与授权修复，不是当前机器状态或发布通过证明。持续边界见 [热更新模块](../../modules/hotfix.md)。

本轮最终首包为 `2026-10-12-165911`，同步最新 Hotfix DLL 与公共字体资源；128项包文件与StreamingAssets哈希一致。正式AppEntrance、五个设置分页与四Demo实际往返70d5d059为1/1通过，配置模式和未保存选择已恢复，窗口自动更新的最新首包路径保留。当前Core.Runtime生命周期修复通过Editor源码运行；AOT裁剪快照不作为这次修改的独立Player证据。详细范围见[实施记录](settings-goal-progress.md)。

### 2026-09-22 本地生成基线

- 当前 HybridCLR 包和项目本地 `libil2cpp-version.txt` 均为 8.14.1；旧本地运行库与源码缓存备份在 `Library/Streamline/hybridclr-backup`，未修改 Unity 安装目录。
- 已执行既有 Generate/All，更新 `Assets/HybridCLRGenerate/link.xml` 与 `AOTGenericReferences.cs`。AOT 列表补入 `Unity.InputSystem.dll`，并同步保存 AOT/Hotfix 程序集快照。
- `Codes/Aot` 中 6 个文件的 SHA-256 均与配置的 `AssembliesPostIl2CppStrip/StandaloneWindows64` 来源一致；`Codes/Hotfix/Hotfix.dll.bytes` 与 `HotUpdateDlls/StandaloneWindows64/Hotfix.dll` 一致。
- 本地 Bundle 路径当前使用 `DefaultPackage/Simulate`。这些生成物是当前开发快照；后续源码变更后发布前仍须重新生成并验证 IL2CPP Player，不能将快照提交当成发布验收。
- `FlowLayoutGroup.OnValidate` 增加 Editor 条件编译，避免 Player 覆写不存在的 uGUI 方法；对应横向、纵向两个精确 EditMode 测试均通过。

### 2026-10-12 公共设置本地生成历史快照

- 使用 `Tools/一键打包工具`（同 `Tools/UI Framework/Hotfix Build`）现有的一键编译并同步流程；同步时 Hotfix 与十项 AOT 来源/目标 SHA256 核对一致，列表新增 Newtonsoft.Json、SleepyStudios.LoopScroll、URP Core 和 JSONSerializeModule。
- 通过现有 YooAsset Viewer 的 ScriptableBuildPipeline 构建 `2026-10-12-144329`，125 个 Bundle；StreamingAssets 的 128 个对应文件哈希一致，HotfixConfig 保存最新本地路径。仅作为当前开发快照，不等同 Player 验收。
- Windows IL2CPP Player `c6a073ba` 在 C++ 阶段因用户级 CMD AutoRun 中的相对 `chcp` 命令失败。当时系统配置由用户自行处理，未修改注册表或 PATH，也未将失败结果计为通过。失败构建后 PostIl2CppStrip 来源缓存为空，目标快照仍保留；后续正式发布重新同步 AOT 时须先成功重生成来源，不能拿旧 StrippedAOTAssembly2 缓存代替。本次 Editor Offline 验收不以重打 Player 或重生成裁剪来源为前置。
- 用户随后明确授权主代理修复该系统配置：先备份 `HKCU\Software\Microsoft\Command Processor\AutoRun` 的原值、值类型及存在状态，再仅将其中相对 `chcp` 改为实际 Windows `System32\chcp.com` 的绝对路径；不改 PATH、其他值或系统级键。已按授权完成备份和修复，AutoRun原类型String保持，回读为`"C:\Windows\System32\chcp.com" 65001 >nul`；其余注册表值比较无变化，不含System32的受限子进程PATH下CMD退出码0、无错误。备份与回读证据位于`Library/GlobalSettings/registry-backup`；不为验证此修复重新构建 Player。旧 Player 失败记录继续保留为历史。
