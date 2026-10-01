# Demo 文档目录

每个 Demo 的长期文档集中在一个与运行资源相同 ID 的目录中。先从各自 README 进入，再按需读模块、设计或操作手册。

| Demo | 文档入口 | 内部标识 |
|---|---|---|
| 小小搬豆工 | [入口与维护](block_porters/README.md) | `BlockPorters` / `block_porters` |
| 无人机飞行仿真 | [入口与维护](drone_flight/README.md) | `DroneFlight` / `drone_flight` |
| DLSS 体验 | [入口与维护](dlss/README.md) | `Dlss` / `dlss` |

一个 Demo 目录按需要包含：`README.md`（导航与名称）、`module.md`（职责和生命周期）、`architecture/`（专属设计）、`runbooks/`（运行、编辑、调试或迁移）。小型 Demo 不必创建空目录或空文档。

公共启动、资源、UI、场景导航和渲染后端仍在顶层 architecture / modules / runbooks；Demo 文档链接这些公共说明，不复制一套。代码、运行资源、编辑配方继续使用已有 Assets 目录，本次只归并文档。

正式中文名称可以调整，目录采用稳定 DemoId。历史原始 Goal 仍在 `docs/agent/prompts/demos/<demo_id>/`，不作为当前实现说明。
