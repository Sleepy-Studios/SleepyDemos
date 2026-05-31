# 资源命名规范

## 适用范围

- **必须遵守**：`Assets/LoadResources/**` 下所有新导入、新建资产（由 Editor 检查器校验）。
- **不在范围**：`Assets/Plugins/**`、`Packages/**`、第三方包内资源。
- **建议遵守、不阻断**：`Assets/Scenes/**` 启动场景（第一版检查器不扫描）。
- **跳过校验**：`Assets/LoadResources/Codes/**`（热更 DLL 等构建产物）。

可加载资源的**目录归属**仍见 [代码与资源布局](./code-layout.md)；本文规定**文件名**与**运行时地址**如何一致。

## 速查卡

| 前缀 | 含义 | 示例 |
| ---- | ---- | ---- |
| `tex_` | 贴图 / Sprite | `tex_Rock_01_BaseColor.png` |
| `mat_` | 材质 | `mat_Rock_01.mat` |
| `mati_` | 材质实例 | `mati_Rock_01_Wet.mat` |
| `pfb_` | 预制体 | `pfb_MonsterSkillFX_01_Bubble.prefab` |
| `ui_` | UI View（MvcBind） | `ui_MainMenuView.prefab` |
| `scn_` | 可加载场景 | `scn_Main.unity` |
| `mdl_` | 静态模型 | `mdl_Crate_01.fbx` |
| `sk_` | 骨骼模型 | `sk_Player.fbx` |
| `anim_` | 动画片段（`.anim`） | `anim_Player_Run_Loop.anim` |
| `anc_` | Animator Controller（animator**C**ontroller） | `anc_Player.controller` |
| `vfx_` | 特效 | `vfx_Hit_Sparks_01.prefab` |
| `sfx_` | 音效 | `sfx_UI_Click_01.wav` |
| `bgm_` | 背景音乐 | `bgm_Hub_Loop.wav` |
| `so_` | ScriptableObject | `so_HotUpdateConfig.asset` |
| `font_` | 源字体 | `font_HarmonyOS_CN.ttf` |
| `fonttmp_` | TMP 字体 | `fonttmp_HarmonyOS_CN.asset` |
| `atl_` | 图集 | `atl_Common_UI.png`、`atl_HarmonyOS_CN.png`（TMP 外部图集） |

校验菜单：`Tools/SleepyDemos/校验 LoadResources 资源命名`。

## 分段模板

**仅用下划线 `_` 分段，禁止短横线 `-`。**

```text
{小写类型前缀}{PascalCase段}_{两位变体}_{可选PascalCase尾段}.{扩展名}
```

| 部分 | 规则 |
| ---- | ---- |
| 类型前缀 | 小写短词 + `_`，必须使用上表登记前缀 |
| 语义段 | **PascalCase**（段内不再用 `_` 分词），如 `MonsterSkillFX`、`Bubble` |
| 变体序号 | `_01`、`_02` …（两位数字，便于排序） |
| 贴图通道后缀 | PascalCase，如 `_BaseColor`、`_Normal`、`_ORM` |

### 正例

```text
pfb_MonsterSkillFX_01_Bubble.prefab
pfb_Crate_01.prefab
ui_MainMenuView.prefab
tex_Rock_01_BaseColor.png
anc_Player.controller
```

- `pfb_`：预制体；`MonsterSkillFX` 为主体；`01` 为变体；`Bubble` 为具体表现名。
- Demo 资源放在 `LoadResources/Demos/<demo_id>/`，**文件名不必重复** `demo_id`。

### 反例

| 文件名 | 原因 |
| ------ | ---- |
| `pfb-monster_01.prefab` | 含短横线 `-` |
| `pfb_monster_skill_01.prefab` | 语义段全小写，非 PascalCase |
| `prefab_Crate_01.prefab` | 未登记前缀 `prefab_` |
| `ctrl_Player.controller` | Controller 应使用 `anc_` |
| `MainMenuView.prefab`（在 UI 目录） | 缺少 `ui_` 前缀 |

## DemoId（目录名）

- 格式：英文小写 + 下划线，例如 `gravity_well`。
- 路径：`Assets/LoadResources/Demos/gravity_well/`。
- 与文件名分开：玩法预制体用 `pfb_MonsterSkillFX_01_Bubble`，不要写成 `pfb_gravity_well_...`。

## 目录与前缀

| 目录 | 允许的前缀 / 规则 |
| ---- | ------------------- |
| `LoadResources/UI/` | 仅 `ui_*View.prefab` |
| `LoadResources/Demos/<demo_id>/` | `pfb_` `scn_` `tex_` `mat_` `mati_` `mdl_` `sk_` `anim_` `anc_` `vfx_` `sfx_` 等 |
| `LoadResources/Art/` | `tex_` `mat_` `mati_` `mdl_` `sk_` `anim_` `anc_` `atl_` |
| `LoadResources/Audio/` | `sfx_` `bgm_` |
| `LoadResources/VFX/` | `vfx_` 及依赖的 `tex_` `mat_` |
| `LoadResources/Scenes/` | `scn_` |
| `LoadResources/Config/` | `so_` |
| `LoadResources/Fonts/Source/` | `font_` |
| `LoadResources/Fonts/TMP_FontAssets/` | `fonttmp_`（`.asset`）；`atl_{名称}.png`（图集） |
| `LoadResources/Fonts/Materials/` | `mat_` |
| `LoadResources/Codes/` | 不校验 |

### anim_ 与 anc_

- `anim_`：仅用于 **Animation Clip**（`.anim`）。
- `anc_`：仅用于 **Animator Controller**（`.controller`）。

## 运行时地址

YooAsset UI 等组使用 **Assets 相对路径、无扩展名**（`YooAssetFullPathAddressRule`）：

```text
Assets/LoadResources/UI/ui_MainMenuView.prefab
→ 地址 LoadResources/UI/ui_MainMenuView
```

Hotfix 中 `View.Address`、`[Source(...)]` 必须与上一致。改名资产后：

1. 同步 Hotfix 生成代码中的 `Address` / `Source`。
2. 必要时在 `Tools/UI Framework/MvcBind` 重新生成 View 绑定代码。
3. 重新收集 YooAsset 并验证 PlayMode 能打开界面。

## MvcBind 与 UI 命名

- UI Prefab 放在 `Assets/LoadResources/UI/`。
- 文件名须以 `View` 结尾（如 `ui_MainMenuView`），供 [MvcBind](../../Assets/Scripts/Core/Editor/MvcBind/MvcBindData.cs) 推导 View 类名。
- 详见 [项目工具 - MvcBind](../runbooks/project-tools.md)。

## 豁免

检查器跳过：

- `LoadResources/Codes/**`
- `LoadResources/Fonts/Fallbacks/**`（字符集文本）
- `.meta`、`.cs`、`.md`、`.bytes` 等非规范校验扩展名

已完成迁移示例：UI `ui_MainMenuView` / `ui_TestView`，配置 `so_HotUpdateConfig`，字体 `font_*` / `fonttmp_*`，TMP 图集 `atl_*`，字体材质 `mat_*`。

## 新增 Demo 时的命名检查

在 [add-demo.md](../runbooks/add-demo.md) 流程末尾增加自检：

- [ ] `demo_id` 为小写 + 下划线，路径无 `-`
- [ ] 新资产文件名符合前缀表与 PascalCase 分段
- [ ] 代码中的加载地址与文件名一致
- [ ] 运行 `Tools/SleepyDemos/校验 LoadResources 资源命名` 无 Error

## YooAsset 采集

除 `UI`、`Codes` 外，公共与 Demo 资源组也应使用 `YooAssetFullPathAddressRule`，与地址规范一致。当前配置见 `Assets/Settings/AssetBundleCollectorSetting.asset`；扩展 Demos / Art / Audio / VFX / Scenes / Config / Fonts 时同步更新本段说明。配置缺失或需校正时：`Tools/SleepyDemos/把 LoadResources 下的目录加入 YooAsset 打包采集配置`（一般拉代码后不必重复点）。
