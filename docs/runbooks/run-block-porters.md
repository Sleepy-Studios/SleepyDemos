# 装配与运行小人搬砖

## 前置条件


## 运行与重新装配

1. 打开 `Assets/Scenes/AppEntrance.unity`，Play 后在 Hub 点击“小人搬砖”。
2. 点击底部四列最前面的队伍。下一队只预览，不支持直接派出。优先搬掉外层颜色，为内层颜色打开道路。
3. 队伍未找到可达同色方块时占位等待；任务位满且无在途/可分配工作时出现失败界面。“模拟复活”每关增加两位一次，也可以免费重开。
4. 点击“返回”卸载 Demo 回到 Hub。五关通过“下一关”依次进入，最后一关支持再玩一轮。

Demo 场景不放入 Build Settings，当前不提供 Editor 直启。首次进入若出现导航未初始化提示，应改从 AppEntrance 启动。

## 修改关卡

在 `Assets/LoadResources/Demos/block_porters/Data/` 编辑关卡资产。Cells 从左下角逐行排列，-1 表示空格，非负值索引 Palette。Columns 必须为四列，队伍颜色与人数必须和棋盘总量匹配。Solution 是完整点击顺序中的列编号 0–3，允许逐队交付后再点击下一队。


## 验证

1. 保存正在编辑的场景，按[运行 Unity 自动化测试](run-unity-tests.md)确认对应项目的 UnitySkills 实例。
2. 串行运行 EditMode 的 `Tests.Demo.BlockPortersSessionTests`、`Tests.Demo.BlockPortersAssetTests`、`Tests.Module.GameSceneNavigatorTests`；再运行 PlayMode 的 `Tests.Demo.BlockPortersFlowTests`。不默认执行全量回归。
3. 运行 `Tools/SleepyDemos/校验 LoadResources 资源命名`；必要时同步 LoadResources 资产 Label。
4. 检查 `Library/BlockPorters/Evidence/` 下的 `Portrait.png`、`TallPortrait.png`、`Carrying.png`、`Win.png`、`BlockedWaiting.png` 和 `HubReturn.png`，确认布局、抬砖、通关和回收。
5. 人工试听抬砖与入坑音效，并确认同色小人抬砖后一起跳坑的节奏。

PlayMode 测试临时修改 Game View 分辨率，结束后恢复原选择；性能采样只代表本机 Editor。真实广告、微信/抖音转换和发布资质仍属于后续阶段。

UI 与场景直接维护已保存资产，绑定更新使用公共 MvcBind。启动不预建小人，每列显示列头和一队预览。
