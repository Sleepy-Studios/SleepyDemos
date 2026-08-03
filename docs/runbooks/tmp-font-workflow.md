# TMP 字体生产流程

## 入口

Unity 菜单：

- `Tools/SleepyDemos/TextMesh Pro/Font Builder`
- `Tools/SleepyDemos/TextMesh Pro/Build Default CN EN Fonts`

## 目录

字体资源统一放在 `Assets/LoadResources/Fonts`：

- `Source/CN`、`Source/EN`：原始 `.ttf`、`.otf`（纯语义名，如 `HarmonyOS_CN.ttf`）
- `TMP_FontAssets/<预设输出目录>`：TMP 字体及其图集；默认预设使用 `CN`、`EN`，其他语言按预设配置目录
- `Materials`：字体材质（如 `HarmonyOS_CN.mat`）
- `Fallbacks`：字符集文本与 fallback 相关资源

不要把业务字体放进 `Temp`、`Assets/TextMesh Pro` 或 `Assets/Plugins`。

命名细节见 [资源命名规范](../architecture/asset-naming.md)。

## 新增字体

1. 把原始字体放进 `Source/CN` 或 `Source/EN`
2. 准备字符集文本，或复用 `Default_CN_Characters.txt` / `Default_EN_Characters.txt`
3. 打开 `Font Builder`
4. 从预设下拉框选择一套配置，或按 `+` 复制当前参数创建新预设
5. 选择字体文件、字符集文本和输出目录；中文字体可选择 fallback 字体
6. 调整参数后点击“保存预设”，显式写回项目共享配置
7. 点击“生成 TMP 字体资产”

工具会一次完成 TMP_FontAsset 生成、字符写入、atlas PNG 导出、材质绑定、ASTC 平台压缩设置和 fallback 配置。

## 预设管理

- 预设统一保存在 `Assets/Settings/TMPFontBuilderPresets.asset`，会随项目提交并供团队共享。
- 首次创建配置时会生成 `Default CN` 与 `Default EN` 两套预设。
- `+` 会复制当前界面参数并生成唯一的 `Preset N` 名称；名称和参数之后都可以修改。
- 每套预设保存自己的输出目录。日语、韩语、拉丁扩展、斯拉夫或阿拉伯语等字符范围通过各自的字体与字符集预设表达，不再额外选择“字体语言”。
- 修改已有预设不会自动写盘，必须点击“保存预设”。切换或重新加载时若存在未保存修改，窗口会提示保存、放弃或取消。
- `-` 删除当前预设，但工具始终要求至少保留一套预设；只剩一套时删除按钮不可用。

## 中英文界面

窗口右上角的 `EN` / `中文` 按钮切换全部窗口文案与标题。界面语言按开发者保存在本机，首次打开默认中文；菜单路径和 Console 日志不随界面语言变化。

## 参考来源

实现参考了 `fishinggameplay` 中 `TMP_Alpha8_ASTC.cs` 的 atlas 外置与绑定逻辑，并保留了“一次生成最终产物”的思路。`TMPFontAutoSwitcherAutoAdd.cs` 和 `ChangeText2TextMeshPro.cs` 属于旧项目 UI 运行时/批量迁移链路，SleepyDemos 当前没有对应业务组件，因此没有直接迁入。
