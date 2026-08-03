# 资源命名规范

## 核心原则

**类型由「目录 + 扩展名 + Importer」决定，文件名只承载语义，运行时地址是一条干净路径。**

- 文件名是纯 PascalCase 语义名。
- 资产放在哪个目录，就决定了它的类型、允许的扩展名和自动 Unity Asset Label。
- 运行时地址 = `Assets/` 之后、去扩展名的路径，只含 `[A-Za-z0-9/_]`。

规则的唯一数据源（SSOT）是 [`LoadResourcesNamingSpec.cs`](../../Assets/Scripts/Core/Editor/AssetNaming/LoadResourcesNamingSpec.cs)。改规范时先改它，再同步本文档。Validator、Postprocessor、TMP 字体工具都读这份 SSOT。

常用菜单：

- `Tools/SleepyDemos/校验 LoadResources 资源命名`
- `Tools/SleepyDemos/同步 LoadResources 资产 Label`

## 适用范围

- **强制校验**：`Assets/LoadResources/**` 下所有新导入、新建资产（导入时 + 菜单）。
- **跳过校验**：`LoadResources/Codes/**`（热更 DLL）、`Fonts/Fallbacks/**`（字符集文本）、`.meta` / `.cs` / `.md` 等非资产扩展名、`.gitkeep`。
- **建议遵守、不阻断**：`Assets/Scenes/**` 启动场景（检查器不扫描）。

## 文件名规范

```text
{PascalCase 语义名}.{扩展名}
```

| 规则 | 级别 |
| ---- | ---- |
| 只允许字母、数字、下划线；不能以数字开头 | Error |
| 禁止 `-`、空格及其它特殊字符 | Error |
| 语义段建议 PascalCase，分段用 `_`，变体用两位数字（`_01`），贴图通道后缀 PascalCase（`_BaseColor` / `_Normal` / `_ORM`） | Warning |

正例：`MainMenuView.prefab`、`Rock_01_BaseColor.png`、`HarmonyOS_CN.ttf`、`HotfixConfig.asset`、`UI_Click_01.wav`。

## 目录矩阵（类型来源）

| 目录 | 允许扩展名 | 说明 / Importer | 自动 Label |
| ---- | ---------- | --------------- | ---------- |
| `UI/<Module>/` | `.prefab` | UI 模块预制体，可继续按模块内部习惯细分子目录 | `ui` `prefab` |
| `UI/<Module>/Sprites/` | 图片 | UI 模块散图，图片须 `Sprite` | `ui` `sprite` |
| `UI/<Module>/Texture/` | 图片 | UI 模块贴图，图片须 `Sprite` | `ui` `texture` |
| `UI/Atlas/` | `.spriteatlas` + 图片 | 图片须 `Sprite` | `ui` `atlas` |
| `Art/Textures/` | 图片 | 须 `Default`（非 Sprite） | `art` `texture` |
| `Art/Materials/` | `.mat` | | `art` `material` |
| `Art/Models/` | `.fbx` `.obj` 等 | | `art` `model` |
| `Art/Animations/` | `.anim` `.controller` `.overridecontroller` | | `art` `anim` |
| `Art/Shaders/` | `.shader` `.shadergraph` `.shadersubgraph` 等 | | `art` `shader` |
| `Audio/SFX/` | `.wav` `.ogg` 等 | | `audio` `sfx` |
| `Audio/BGM/` | `.wav` `.ogg` 等 | | `audio` `bgm` |
| `VFX/` | `.prefab` `.vfx` + 私有 `.mat`/图片/Shader | 图片 `Default` | `vfx` |
| `Scenes/` | `.unity` | 可加载场景 | `scene` |
| `Config/` | `.asset` `.json` `.bytes` | SO / JSON / 二进制配置 | `config` |
| `Config/Luban/` | `.json` `.bytes` | Luban 生成配置；允许生成器原生 lower_snake_case 文件名 | `config` |
| `Fonts/Source/` | `.ttf` `.otf` | 按语言分 `CN`/`EN` 子目录 | `font` |
| `Fonts/TMP_FontAssets/` | `.asset` `.png` | TMP 资产 + 外部图集 | `font` |
| `Fonts/Materials/` | `.mat` | 字体材质 | `font` |
| `Demos/<demo_id>/Scenes/` | `.unity` | | `demo` `scene` |
| `Demos/<demo_id>/Prefabs/` | `.prefab` | 玩法预制体 | `demo` `prefab` |
| `Demos/<demo_id>/Art/` | 图片/`.mat`/模型/动画/Shader | Demo 私有美术 | `demo` `art` |
| `Demos/<demo_id>/Data/` | `.json` `.bytes` `.txt` `.asset` | 数据表 / 配置 | `demo` `data` |
| `Demos/<demo_id>/VFX/` | `.prefab` `.vfx` + 私有 `.mat`/图片/Shader | | `demo` `vfx` |
| `Codes/` | — | 热更 DLL，跳过校验 | — |

规则补充：

- **顶层目录白名单**：只有上表登记过的顶层目录合法。新增顶层目录必须先改 [`LoadResourcesNamingSpec.cs`](../../Assets/Scripts/Core/Editor/AssetNaming/LoadResourcesNamingSpec.cs) 与本文档，并补 YooAsset 采集。
- **资产必须落在功能子目录**：直接放在 `UI/`、`Art/`、`Demos/`、`Demos/<demo_id>/` 根目录会报 Error。
- **子目录可继续细分**：例如 `Art/Textures/Rocks/` 仍按 `Art/Textures` 规则校验。
- **Luban 命名例外**：仅 `Config/Luban/` 下的 `.json` / `.bytes` 跳过 PascalCase 语义 Warning；合法字符、数字开头、扩展名和地址冲突仍按全局 Error 规则检查。
- **`demo_id` 格式**：`^[a-z][a-z0-9_]*$`（小写字母开头），Error 级强制，例如 `gravity_well`。

## 决策树

- 这是界面预制体吗？
  - 放到所属模块 → `UI/<Module>/`，模块内可继续细分子目录。
- 这是一张图？
  - UI 模块散图 → `UI/<Module>/Sprites/`（Sprite）。
  - UI 模块贴图 → `UI/<Module>/Texture/`（Sprite）。
  - UI 图集 → `UI/Atlas/`（Sprite）。
  - 模型贴图 → `Art/Textures/`（Default），通道后缀 `_BaseColor` 等。
- 这是配置数据？
  - ScriptableObject → `Config/` 或 `Demos/<demo_id>/Data/`（`.asset`）。
  - JSON / 二进制表 → 同目录的 `.json` / `.bytes`。
- 这是特效？→ `VFX/`（公共）或 `Demos/<demo_id>/VFX/`（私有）。
- 这是某个 Demo 专属的？→ 放 `Demos/<demo_id>/` 对应子目录，不要进公共目录。

## 运行时地址

地址 = `Assets/` 之后、去扩展名的路径：

```text
Assets/LoadResources/UI/Main/MainMenuView.prefab
→ 地址 LoadResources/UI/Main/MainMenuView
```

- 地址只含 `[A-Za-z0-9/_]`。
- YooAsset 全路径寻址（`YooAssetFullPathAddressRule`）与 MvcBind 生成的 `Address` / `[Source(...)]` 必然一致，因为都从同一路径推导。
- **地址冲突**：同目录同主名不同扩展名会生成相同地址（如 TMP `.asset` 与图集 `.png`）。校验器有**地址冲突扫描**兜底；TMP 外部图集用 `_Atlas` 后缀区分（如 `HarmonyOS_CN.asset` 与 `HarmonyOS_CN_Atlas.png`）。

改名 / 移动 LoadResources 资产后：

1. 同步 Hotfix 生成代码中的 `Address` / `[Source]`。
2. 必要时在 `Tools/UI Framework/MvcBind` 重新生成 View 绑定代码。
3. 重新收集 YooAsset，PlayMode 验证界面能打开。

## 场景命名

| 位置 | 校验 | 文件名 | 说明 |
| ---- | ---- | ------ | ---- |
| `Assets/Scenes/` | 建议 | `{PascalCase}.unity` | 启动入口场景，不进 LoadResources / YooAsset |
| `LoadResources/Scenes/` | Error | `{PascalCase}.unity` | 公共可加载场景，Label: `scene` |
| `LoadResources/Demos/<demo_id>/Scenes/` | Error | `{PascalCase}.unity` | Demo 可加载场景，Label: `demo` `scene` |

正例：`AppEntrance.unity`（启动）、`Main.unity`（Demo 主场景）、`Blank.unity`（模板）。

场景无额外后缀要求，遵循上文文件名规范即可。启动场景与可加载场景分目录存放，不要混放。

## 按目录自动打 Unity Asset Label

这里的 Label 是 Unity Project 窗口用于 `l:xxx` 搜索的 **Unity Asset Label**，不是 YooAsset Collector 里的 `AssetTags`。

触发方式：

- 新资产导入到 `Assets/LoadResources/**` 时自动同步。
- 资产在 Unity 中移动到新目录时自动同步。
- 对资产执行 Reimport 时自动同步。
- 从 git 拉到本地或批量改名后，如果 Unity 尚未重新导入，可运行 `Tools/SleepyDemos/同步 LoadResources 资产 Label` 扫描既有资产并补齐。

导入后处理器按目录矩阵「目录 → Label」自动维护托管标签：

- `VFX/**` 与 `Demos/<id>/VFX/**` → `vfx`
- `UI/<Module>/**/*.prefab` → `ui` `prefab`；`Audio/SFX/**` → `audio` `sfx`，依此类推。

托管标签范围来自 [`LoadResourcesNamingSpec.ManagedLabels`](../../Assets/Scripts/Core/Editor/AssetNaming/LoadResourcesNamingSpec.cs)：`ui`、`prefab`、`sprite`、`atlas`、`art`、`texture`、`material`、`model`、`anim`、`shader`、`audio`、`sfx`、`bgm`、`vfx`、`scene`、`config`、`font`、`demo`、`data`。

移动资产时会移除旧的托管标签并写入新托管标签；人工添加的非托管标签会保留。不要手动维护这些托管标签，规则变化时先改 `LoadResourcesNamingSpec.cs`，再同步本文档。

用法：Project 窗口搜 `l:vfx` 跨整个项目筛出所有特效（含 Demo 私有），可与类型搜索叠加 `t:Prefab l:vfx`。语义层可选用途 token（如 `HitSparks_FX`）做辅助，不强制、不进类型体系、不污染地址。

## 三级校验

| 级别 | 行为 | 典型项 |
| ---- | ---- | ---- |
| Error | 严重问题（导入与菜单均以 Warning 日志提示，菜单仍会计数） | 目录白名单、扩展名↔目录、`demo_id` 格式、非法字符、地址冲突、Importer 类型 |
| Warning | 提示，不阻断 | 语义名非 PascalCase、Unity Asset Label 缺失或存在过期托管标签 |
| Info | 建议 | 预留 |

## MvcBind 与 UI 命名

- UI 预制体放 `Assets/LoadResources/UI/<Module>/`，文件名遵循通用 PascalCase 语义规则。
- View 类名直接取文件名（已是 PascalCase）。
- 详见 [项目工具 - MvcBind](../runbooks/project-tools.md)。

## 字体资源

- 源字体放 `Fonts/Source/CN`、`Fonts/Source/EN`（`.ttf` / `.otf`）。
- TMP 产物放 `Fonts/TMP_FontAssets/<预设输出目录>`（`.asset` 与 `_Atlas.png`）。输出目录由字体预设保存，例如 `CN`、`EN`、`Japanese`、`Korean`、`LatinExtended`、`Cyrillic` 或 `Arabic`，不维护固定语言枚举。
- 字体材质放 `Fonts/Materials/`（`.mat`）。
- 生成入口见 [TMP 字体流程](../runbooks/tmp-font-workflow.md)。

## YooAsset 采集

公共与 Demo 资源组使用 `YooAssetFullPathAddressRule`，采集路径为各顶层目录（`Assets/LoadResources/UI`、`Art` 等），递归收集其下子目录，地址即上面的运行时路径。配置见 `Assets/Settings/AssetBundleCollectorSetting.asset`；缺失或需校正时：`Tools/SleepyDemos/把 LoadResources 下的目录加入 YooAsset 打包采集配置`。

当前命名系统不使用 YooAsset Collector 的 `AssetTags` 做分包或加载筛选；需要搜索资产类型时使用 Unity Asset Label，例如 `l:scene`、`l:demo l:prefab`。

## 新增资产自检

- [ ] 资产落在目录矩阵登记的功能子目录下
- [ ] 文件名纯语义 PascalCase，无 `-`/空格/特殊字符
- [ ] `demo_id` 为小写 + 下划线
- [ ] 图片的 Importer 类型与目录一致（UI=Sprite，Art=Default）
- [ ] Unity Asset Label 与目录矩阵一致；必要时运行 `Tools/SleepyDemos/同步 LoadResources 资产 Label`
- [ ] 代码中的加载地址与新路径一致
- [ ] 运行 `Tools/SleepyDemos/校验 LoadResources 资源命名` 无 Error
