# TMP 字体资源约定

业务字体统一放在 `Assets/LoadResources/Fonts`，不要放到项目根目录的 `Temp/`，也不要继续堆到 `Assets/TextMesh Pro/` 或 `Assets/Plugins/`。

命名规范见 [资源命名规范](../../../docs/architecture/asset-naming.md)：

- 源字体：`font&{名称}.ttf` / `.otf`，放在 `Source/CN` 或 `Source/EN`
- TMP 资产：`font&{名称}.asset`，放在 `TMP_FontAssets/CN` 或 `EN`
- TMP 图集：`font&{名称}_Atlas.png`（与 `.asset` 同目录；`_Atlas` 避免 YooAsset 地址冲突）
- 材质：`font&{名称}.mat`，放在 `Materials/`

目录职责：

- `Source/`：原始字体文件，按语言分为 `CN`、`EN`
- `TMP_FontAssets/`：生成后的 `TMP_FontAsset`，按语言分为 `CN`、`EN`
- `Materials/`：生成或复制出来的字体材质
- `Fallbacks/`：字符集文本（非游戏资产前缀校验范围）

推荐入口：

- Unity 菜单：`Tools/SleepyDemos/TextMesh Pro/Font Builder`
- 默认字体一键生成：`Tools/SleepyDemos/TextMesh Pro/Build Default CN EN Fonts`

新增字体流程：

1. 把源字体命名为 `font&*.ttf` / `.otf` 后放入 `Source/CN` 或 `Source/EN`
2. 准备字符集文本，可直接使用 `Fallbacks/Default_CN_Characters.txt` 或 `Fallbacks/Default_EN_Characters.txt`
3. 打开 Font Builder，选择字体、语言和字符集
4. 点击 `Build TMP Font Asset`
5. 生成结果会落到 `TMP_FontAssets`（`font&*.asset`、`font&*_Atlas.png`）与 `Materials`（`font&*.mat`）
