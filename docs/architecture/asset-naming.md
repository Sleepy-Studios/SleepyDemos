# 资源命名规范

## 适用范围

- **必须遵守**：`Assets/LoadResources/**` 下所有新导入、新建资产（由 Editor 检查器校验）。
- **不在范围**：`Assets/Plugins/**`、`Packages/**`、第三方包内资源。
- **建议遵守、不阻断**：`Assets/Scenes/**` 启动场景（检查器不扫描）。
- **跳过校验**：`Assets/LoadResources/Codes/**`（热更 DLL 等构建产物）。

可加载资源的**目录归属**仍见 [代码与资源布局](./code-layout.md)；本文规定**文件名**与**运行时地址**如何一致。

校验菜单：`Tools/SleepyDemos/校验 LoadResources 资源命名`。

## 速查卡

前缀与语义名之间用 **`&`** 分隔；语义段内部仍可用 `_` 分段。

### 核心 12 个

| 前缀 | 含义 | 示例 |
| ---- | ---- | ---- |
| `spr&` | Sprite / 2D UI 单图 | `spr&Icon_Close_01.png` |
| `tex&` | 材质贴图（非 Sprite；含 RenderTexture/Cubemap） | `tex&Rock_01_BaseColor.png` |
| `mat&` | Material（含实例变体；不含 `Fonts/Materials/`） | `mat&Rock_01.mat` |
| `pfb&` | Prefab（UI / 玩法 / 特效） | `pfb&MainMenuView.prefab` |
| `mdl&` | 模型（静态 + 骨骼 fbx 等） | `mdl&Player.fbx` |
| `anim&` | Animation Clip（`.anim`） | `anim&Player_Run_Loop.anim` |
| `anc&` | Animator Controller（含 Override Controller） | `anc&Player.controller` |
| `scn&` | 可加载场景 | `scn&Main.unity` |
| `so&` | ScriptableObject（`.asset`） | `so&HotUpdateConfig.asset` |
| `font&` | 字体相关：ttf/otf、TMP Font Asset、TMP 图集 png、`Fonts/Materials` 下 `.mat` | `font&HarmonyOS_CN.ttf` |
| `sfx&` | 音效 | `sfx&UI_Click_01.wav` |
| `bgm&` | 背景音乐 | `bgm&Hub_Loop.wav` |

### 扩展 6 个

| 前缀 | 含义 | 示例 |
| ---- | ---- | ---- |
| `shd&` | Shader / ShaderGraph / SubGraph | `shd&Dissolve_URP.shadergraph` |
| `phy&` | Physic Material | `phy&Player_Default.physicMaterial` |
| `vid&` | 视频（过场/背景） | `vid&Intro_01.mp4` |
| `time&` | Timeline / Playable | `time&BossPhase2.playable` |
| `json&` | 独立 JSON 配置（`.json` TextAsset） | `json&GravityWell_Levels.json` |
| `txt&` | 文本/二进制 TextAsset（`.txt`、`.bytes`） | `txt&ItemTable.bytes` |

## 命名模板

**禁止**在路径或文件名中使用短横线 `-`。

```text
{小写类型前缀}&{语义命名}.{扩展名}
```

**推荐**（检查器不强制）语义命名：

```text
{PascalCase主体}_{两位变体}_{可选PascalCase尾段}
```

| 部分 | 规则 |
| ---- | ---- |
| 类型前缀 | 上表登记的 18 个前缀 + **`&`** |
| 语义命名 | 推荐 PascalCase 分段；变体可用 `_01`、`_02` |
| 贴图通道后缀 | 推荐 PascalCase，如 `_BaseColor`、`_Normal`、`_ORM` |

### 正例

```text
pfb&MainMenuView.prefab
pfb&MonsterSkillFX_01_Bubble.prefab
pfb&Hit_Sparks_01.prefab
mdl&Player.fbx
tex&Rock_01_BaseColor.png
spr&Icon_Close_01.png
font&HarmonyOS_CN.ttf
font&HarmonyOS_CN.asset
font&HarmonyOS_CN_Atlas.png
font&HarmonyOS_CN.mat             # 位于 Fonts/Materials/
so&HotUpdateConfig.asset
json&GravityWell_Levels.json
txt&ItemTable.bytes
shd&Dissolve_URP.shadergraph
phy&Player_Default.physicMaterial
vid&Intro_01.mp4
time&BossPhase2.playable
anc&Player.controller
```

- `pfb&`：全部 Prefab（UI、玩法、特效同一前缀）。
- Demo 资源放在 `LoadResources/Demos/<demo_id>/`，**文件名不必重复** `demo_id`。

### 反例

| 文件名 | 原因 |
| ------ | ---- |
| `pfb-monster&01.prefab` | 含短横线 `-` |
| `prefab&Crate_01.prefab` | 未登记前缀 `prefab&` |
| `ui&MainMenuView.prefab` | UI Prefab 应使用 `pfb&` |
| `vfx&Hit_Sparks_01.prefab` | 特效 Prefab 应使用 `pfb&` |
| `MainMenuView.prefab`（在 UI 目录） | 缺少类型前缀 |
| `ctrl&Player.controller` | Controller 应使用 `anc&` |
| `mat&HarmonyOS_CN.mat`（在 `Fonts/Materials/`） | 字体材质应使用 `font&` |

## json& / so& / txt& 边界

- **`so&`**：Unity ScriptableObject 资产（**`.asset`**），如热更配置、玩法 ScriptableObject。
- **`json&`**：独立 **`.json`** 文件（玩法表、关卡数据等 TextAsset）。
- **`txt&`**：**`.txt`** 与 **`.bytes`**（Unity TextAsset；`.bytes` 内容为二进制，扩展名仍走 `txt&`）。
- **`LoadResources/Codes/**`** 内热更 DLL 的 `.bytes` **跳过校验**，不归 `txt&`。

## 字体与材质

- **`font&`**：`Fonts/` 下全部字体相关资源——源字体（`.ttf` / `.otf`）、TMP Font Asset（`.asset`）、TMP 外部图集（`.png`）、**`Fonts/Materials/` 下的字体材质（`.mat`）**。
- **TMP 图集 png**：与对应 `.asset` 同目录时，语义名须加 **`_Atlas`** 后缀（如 `font&HarmonyOS_CN_Atlas.png`），避免 YooAsset 无扩展名地址与 `font&HarmonyOS_CN.asset` 冲突。
- **`mat&`**：用于 `Art/`、`VFX/`、`Demos/` 等处的通用 Material；**不**用于 `Fonts/Materials/`。
- 源字体放 `Fonts/Source/`，TMP 产物放 `Fonts/TMP_FontAssets/`，字体材质放 `Fonts/Materials/`。

## DemoId（目录名）

- 格式：英文小写 + 下划线，例如 `gravity_well`。
- 路径：`Assets/LoadResources/Demos/gravity_well/`。
- 与文件名分开：玩法预制体用 `pfb&MonsterSkillFX_01_Bubble`，不要写成 `pfb&gravity_well_...`。

## 目录与前缀

| 目录 | 允许的前缀 / 规则 |
| ---- | ----------------- |
| `LoadResources/UI/` | `pfb&`（语义段须以 `View` 结尾） |
| `LoadResources/Demos/<demo_id>/` | 上表全部（按扩展名） |
| `LoadResources/Demos/<demo_id>/Data/`（若有） | `json&` `txt&` `so&` |
| `LoadResources/Art/` | `spr&` `tex&` `mat&` `mdl&` `anim&` `anc&` `shd&` |
| `LoadResources/Audio/` | `sfx&` `bgm&` |
| `LoadResources/VFX/` | `pfb&` `tex&` `mat&` `shd&` |
| `LoadResources/Scenes/` | `scn&` |
| `LoadResources/Config/` | `so&` `json&` `txt&` |
| `LoadResources/Fonts/Source/` | `font&` |
| `LoadResources/Fonts/TMP_FontAssets/` | `font&` |
| `LoadResources/Fonts/Materials/` | `font&` |
| `LoadResources/Codes/` | 不校验 |

### anim& 与 anc&

- `anim&`：仅用于 **Animation Clip**（`.anim`）。
- `anc&`：仅用于 **Animator Controller**（`.controller`，含 Override Controller）。

## 类型映射附录

以下类型**暂不单独登记前缀**，按扩展名或用途映射到上表：

| 类型 | 建议 |
| ---- | ---- |
| Sprite Atlas（`.spriteatlas`） | 单图用 `spr&`；图集资产出现后再定 |
| Audio Mixer（`.mixer`） | 若进 LoadResources，暂用 `so&` 或跳过校验 |
| VFX Graph（`.vfx`） | 表现以 `pfb&` 预制体为主；依赖 `shd&` / `tex&` |
| Input Actions（`.inputactions`） | 通常放 `Assets/` 根，不进 LoadResources |
| NavMesh / Lightmap / 烘焙产物 | 生成物，不纳入命名表 |
| RenderTexture / Cubemap | `tex&` |
| Override Animator Controller | `anc&` |

## 运行时地址

YooAsset UI 等组使用 **Assets 相对路径、无扩展名**（`YooAssetFullPathAddressRule`）：

```text
Assets/LoadResources/UI/pfb&MainMenuView.prefab
→ 地址 LoadResources/UI/pfb&MainMenuView
```

Hotfix 中 `View.Address`、`[Source(...)]` 必须与上一致。改名资产后：

1. 同步 Hotfix 生成代码中的 `Address` / `Source`。
2. 必要时在 `Tools/UI Framework/MvcBind` 重新生成 View 绑定代码。
3. 重新收集 YooAsset 并验证 PlayMode 能打开界面。

## MvcBind 与 UI 命名

- UI Prefab 放在 `Assets/LoadResources/UI/`。
- 文件名格式：`pfb&{Name}View.prefab`（如 `pfb&MainMenuView.prefab`）。
- 语义段须以 **`View`** 结尾；`ToViewClassName` 取 **`&` 之后**的语义段（`pfb&MainMenuView` → `MainMenuView`）。
- 详见 [项目工具 - MvcBind](../runbooks/project-tools.md)。

## 豁免

检查器跳过：

- `LoadResources/Codes/**`
- `LoadResources/Fonts/Fallbacks/**`（字符集文本）
- `.meta`、`.cs`、`.md` 等非规范校验扩展名

## 新增 Demo 时的命名检查

在 [add-demo.md](../runbooks/add-demo.md) 流程末尾增加自检：

- [ ] `demo_id` 为小写 + 下划线，路径无 `-`
- [ ] 新资产文件名符合前缀表与 `&` 分隔规则
- [ ] 代码中的加载地址与文件名一致
- [ ] 运行 `Tools/SleepyDemos/校验 LoadResources 资源命名` 无 Error

## YooAsset 采集

除 `UI`、`Codes` 外，公共与 Demo 资源组也应使用 `YooAssetFullPathAddressRule`，与地址规范一致。当前配置见 `Assets/Settings/AssetBundleCollectorSetting.asset`；扩展 Demos / Art / Audio / VFX / Scenes / Config / Fonts 时同步更新本段说明。配置缺失或需校正时：`Tools/SleepyDemos/把 LoadResources 下的目录加入 YooAsset 打包采集配置`（一般拉代码后不必重复点）。
