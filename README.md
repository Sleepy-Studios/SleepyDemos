# SleepyDemos

多人协作的 Unity 练习项目：从主场景进入后展示 Demo 列表，选择后加载对应玩法场景。通过持续补充小玩法练习工程化与协作流程。

## RoadMap
记录一下要做的事情，主要是框架类的，完成之后剪切到最后的“历史更新部分”

## 技术栈与打开方式

- Unity 版本见 `ProjectSettings/ProjectVersion.txt`。
- 用 Unity 打开本仓库根目录即可。

## 资源与目录约定

### 总原则

- **Hub（主流程）**：入口场景、Demo 列表、加载场景、共用 UI/工具，放在 `Assets/Game` 下除 `Demos` 以外的既有分类中。
- **Demo 岛（单个玩法）**：每个 Demo 尽量 **自包含**（场景 + 该玩法专用资源），放在 `Assets/Game/Demos/<DemoId>/`，避免与其它 Demo 强耦合。觉得比较通用的组件和代码可以放在Core目录中（见下面的目录说明）
- **元数据**：**只维护一份中央 Catalog**（将来放在 `Assets/Game/Core/Config/`），所有 Demo 的展示信息、**价值量**、是否进包等字段都在这份 Catalog 里统一维护。
- **价值量**：通过 **表格（如 CSV）导入** 更新 Catalog（与其它 Demo 信息同一批导入），具体导入流程待实现时在 Editor 菜单中约定。

### 目录说明

| 路径 | 用途 |
|------|------|
| `Assets/Game/Core` | 与单个 Demo 无关的核心逻辑、配置、数据结构（含将来的中央 Catalog 资源）。 |
| `Assets/Game/Core/Config` | **中央 Catalog** 资源放置处（文件名待项目内统一，例如 `DemoCatalog`）。 |
| `Assets/Game/Runtime` | 运行时脚本（主菜单流程、加载场景等）。 |
| `Assets/Game/Scenes` | **主场景**（入口）等；玩家可玩的场景路径需在 Catalog 中登记。 |
| `Assets/Game/Scenes/_Templates` | 从模板复制新玩法的起点场景（例如从 Sample 复制后改名）；**默认不视为对外 Demo**，是否进正式包由 Catalog 的「是否进包」字段与 Build 设置策略决定。 |
| `Assets/Game/Demos/<DemoId>/` | 单个 Demo 的根目录；建议子结构包含 `Scenes/`、`Scripts/`，专用美术/预制体可放在本目录下。`<DemoId>` 建议英文小写与短横线，如 `gravity-well`。 |
| `Assets/Game/UI` / `Art` / `Audio` / `VFX` | **多个 Demo 共用**的资源；仅某一玩法使用的资源优先放在对应 `Demos/<DemoId>/` 下。 |
| `Assets/Game/Editor` | 编辑器扩展（例如从模板创建 Demo、CSV 导入 Catalog、同步 Build 列表等）。 |

### Build 列表（Scenes In Build）

- 打正式包时，只有加入 **File → Build Settings → Scenes In Build** 的场景才会进入安装包。
- 通过 `SceneManager` 按名称或路径加载的场景，通常也需要在该列表中（除非以后改用 Addressables 等单独管线）。
- **未完成或不想进包的 Demo**：在 Catalog 中用字段标记（例如「是否进包」）；发布前根据该字段维护 Build 列表（将来可由 Editor 工具辅助同步）。模板场景可保持不勾选或按需排除。

### 新增 Demo 工作流（约定）

1. 在 `Scenes/_Templates` 或既有模板复制场景，放到 `Demos/<DemoId>/Scenes/` 并命名清晰。
2. 在 **中央 Catalog** 中增加一条记录，填写场景引用或路径、展示名、创建/排序时间、**价值量**、是否进包等。
3. 需要批量调整价值量或其它列时，使用 **表格 → 导入 Catalog**（实现后），避免与 Catalog 字段不一致。

### 协作说明

- 优先在各自 `Demos/<DemoId>/` 内并行开发，减少冲突。
- 修改 `UI`、`Art` 等共用资源前建议先沟通或开小范围合并请求。
- 中央 Catalog 由多人编辑时可能出现版本合并冲突，合并时注意行级变更；大批量数值可走表格导入以降低手改频率。

## 许可

（在此补充许可证或用途说明。）

## 历史更新
