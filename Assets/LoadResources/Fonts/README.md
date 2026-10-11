# TMP 字体资源约定

业务字体统一放在 `Assets/LoadResources/Fonts`，不要放到项目根目录的 `Temp/`，也不要继续堆到 `Assets/TextMesh Pro/` 或 `Assets/Plugins/`。

普通 Demo 统一复用中文 `HarmonyOS_CN`、英文 `BebasNeue_EN` 两套默认字体，不新增专属或补字字体。缺字合并公共字符集并更新原资产；新增语言或特殊字体先确认范围，详见[多语言与公共字体规范](../../../docs/architecture/localization-fonts.md)。

命名规范见 [资源命名规范](../../../docs/architecture/asset-naming.md)：文件名为纯语义 PascalCase，无前缀，类型由目录与扩展名决定。

- 源字体：`{名称}.ttf` / `.otf`，默认放在 `Source/CN` 或 `Source/EN`，其他语言可按需增加子目录
- TMP 资产：`{名称}{预设后缀}.asset`，放在 `TMP_FontAssets/<预设输出目录>`
- TMP 图集：`{名称}{预设后缀}_Atlas.png`（与 `.asset` 同目录；`_Atlas` 用于区分两者的运行时地址）
- 材质：`{名称}{预设后缀}.mat`，放在 `Materials/`

目录职责：

- `Source/`：原始字体文件，现有默认目录为 `CN`、`EN`
- `TMP_FontAssets/`：生成后的 `TMP_FontAsset`，按预设输出目录归档
- `Materials/`：生成或复制出来的字体材质
- `Fallbacks/`：字符集文本（跳过命名校验）

推荐入口：

- Unity 菜单：`Tools/SleepyDemos/TextMesh Pro/Font Builder`
- 默认字体一键生成：`Tools/SleepyDemos/TextMesh Pro/Build Default CN EN Fonts`

补字或更新默认字体流程：

1. 复用 `Source/CN/HarmonyOS_CN.ttf` 或 `Source/EN/BebasNeue_EN.otf`
2. 把所需字符合并到 `Fallbacks/Default_CN_Characters.txt` 或 `Fallbacks/Default_EN_Characters.txt`
3. 打开 Font Builder，选择对应默认预设，确认更新的是原字体路径及参数；不新增预设或 Demo 后缀
4. 可补充附加字符、提取现有字体字符差异，并在输出预览中确认路径
5. 点击“生成 TMP 字体资产”
6. 生成结果会落到预设指定的 `TMP_FontAssets/<输出目录>` 与 `Materials`

预设共享、排序、保存、fallback 保留和覆盖更新的完整操作见 [TMP 字体生产流程](../../../docs/runbooks/tmp-font-workflow.md)。
