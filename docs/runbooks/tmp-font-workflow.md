# TMP 字体生产流程

## 入口

Unity 菜单：

- `Tools/SleepyDemos/TextMesh Pro/Font Builder`
- `Tools/SleepyDemos/TextMesh Pro/Build Default CN EN Fonts`

## 目录

字体资源统一放在 `Assets/LoadResources/Fonts`：

- `Source/CN`、`Source/EN`：原始 `.ttf`、`.otf`
- `TMP_FontAssets/CN`、`TMP_FontAssets/EN`：生成后的 TMP 字体（`fonttmp_*.asset`）与图集（`atl_*.png`）
- `Materials`：字体材质（`mat_*.mat`）
- `Fallbacks`：字符集文本与 fallback 相关资源

不要把业务字体放进 `Temp`、`Assets/TextMesh Pro` 或 `Assets/Plugins`。

## 新增字体

1. 把原始字体放进 `Source/CN` 或 `Source/EN`
2. 准备字符集文本，或复用 `Default_CN_Characters.txt` / `Default_EN_Characters.txt`
3. 打开 `Font Builder`
4. 选择字体文件、语言、字符集文本
5. 中文字体可选择 fallback 字体
6. 点击 `Build TMP Font Asset`

工具会一次完成 TMP_FontAsset 生成、字符写入、atlas PNG 导出、材质绑定、ASTC 平台压缩设置和 fallback 配置。

## 参考来源

实现参考了 `fishinggameplay` 中 `TMP_Alpha8_ASTC.cs` 的 atlas 外置与绑定逻辑，并保留了“一次生成最终产物”的思路。`TMPFontAutoSwitcherAutoAdd.cs` 和 `ChangeText2TextMeshPro.cs` 属于旧项目 UI 运行时/批量迁移链路，SleepyDemos 当前没有对应业务组件，因此没有直接迁入。
