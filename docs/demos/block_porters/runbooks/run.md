# 运行与维护小小搬豆工

## 前置条件

当前项目使用 Unity 6000.3.15f1。使用已保存的场景和 Prefab，经 YooAsset、Core UI 与正式 Hub 进入；本 Demo 不保留一键装配工具。

## 运行

1. 打开 `Assets/Scenes/AppEntrance.unity`，Play 后在 Hub 选择“小小搬豆工”卡片，再点击开始按钮；键盘/手柄确认可直接进入。
2. 点击底部五列第一排的队伍，随后三排只预览。派队时整列递补约 0.2 秒，同列暂时禁点。优先搬掉外层颜色，为内层颜色打开道路。
3. 队伍未找到可达同色方块时占位等待。坑口左右的“模拟广告 · 解锁 +1”可随时各解锁一次，最多七位；失败界面也提供剩余解锁入口。两侧已开放后再次失败只能重开或返回。重开/切关重新锁定广告位。
4. 点击右上齿轮打开设置，玩法暂停；点击“继续游戏”关闭设置。声音、重新开始与返回大厅均在设置中。
5. 返回大厅会卸载 Demo 并恢复 Hub 灯光。关卡按 `Data/LevelCatalog.asset` 顺序通过“下一关”进入，最后一关支持再玩一轮。新关和重新进入会随机更换场景主题，重开保持当前主题。

Demo 场景不放入 Build Settings，当前不提供 Editor 直启。首次进入若出现导航未初始化提示，应改从 AppEntrance 启动。

## 修改关卡

推荐使用[图片关卡工作台](workbench.md)导入、修图、安排队伍、验证和导出。底层关卡在 `Assets/LoadResources/Demos/block_porters/Data/`，Cells 左下逐行排列，-1 为空，非负值索引 Palette；Palette 最多 12 色，Columns 新内容为五列，旧四列仍兼容，各色人数与方块数精确匹配。Solution 的列编号为 0–4（旧关卡 0–3），按派队后等待调度稳定点回放。新增内容加入关卡集，无需再改场景控制器。

## 验证

1. 保存正在编辑的场景，按[运行 Unity 自动化测试](../../../runbooks/run-unity-tests.md)确认对应项目的 UnitySkills 实例。
2. 串行运行直接相关类：规则与资源用 `BlockPortersSessionTests`、`BlockPortersAssetTests`，主题用 `BlockPortersThemeTests`，编辑器用 `BlockPortersWorkbenchTests`、`BlockPortersBatchTests`，导航改动时追加 `GameSceneNavigatorTests`；运行链路用 PlayMode 的 `BlockPortersFlowTests`。不默认执行全量回归。
3. 运行 `Tools/SleepyDemos/校验 LoadResources 资源命名`；必要时同步 LoadResources 资产 Label。
4. 检查 `Library/BlockPorters/Evidence/` 下的 `Portrait.png`、`TallPortrait.png`、`Carrying.png`、`Win.png`、`BlockedWaiting.png` 和 `HubReturn.png`，确认布局、抬砖、通关和回收。
5. 人工试听抬砖与入坑音效，并确认同色小人抬砖后一起跳坑的节奏。

PlayMode 测试临时修改 Game View 分辨率，结束后恢复原选择；性能采样只代表本机 Editor。真实广告、微信/抖音转换和发布资质仍属于后续阶段。

UI 与场景直接维护已保存资产，绑定更新使用公共 MvcBind；具体素材、九宫格切片与维护步骤见[视觉维护](visuals.md)。启动不预建小人，每列显示列头和三队预览。
