# 小小搬豆工

竖屏颜色搬运解压 Demo：点队伍，小人搬走同色方块并举着跳入小坑。五列四排、五个免费任务位和左右两个模拟广告任务位；目前提供八关及图片关卡工作台。

正式名称为 **小小搬豆工**。内部名称 `BlockPorters`、资源 ID `block_porters` 与 `GameSceneId.BlockPorters` 保持稳定，不随显示名称变更。

- [模块职责与生命周期](module.md)
- [运行与维护](runbooks/run.md)
- [图片关卡工作台](runbooks/workbench.md)
- [UI 规格、四套主题与坑口维护](runbooks/visuals.md)

代码在 `Assets/Scripts/Hotfix/Demos/BlockPorters/`，运行资源在 `Assets/LoadResources/Demos/block_porters/`，工作台和配方分别在 `Assets/Scripts/Hotfix/Editor/BlockPorters/`、`Assets/Settings/BlockPorters/`。从 AppEntrance 的 Hub 进入。真实广告与小游戏平台发布仍属于后续阶段。
