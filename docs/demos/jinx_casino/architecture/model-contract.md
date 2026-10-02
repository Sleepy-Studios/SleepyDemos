# 原创模型与运行契约

当前按单机沉浸计划重做。允许为实体操作调整本Demo机台尺寸、Collider和相机/交互挂点，并同步装配与测试合同；不可动无关Demo。旧原型尺寸仅保留作历史基线，不约束新样板。仍不允许新增角色相机或AudioListener，不用运行时旋转/缩放补偿错误导出。

## 坐标及层级

- 米制。Unity角色胶囊高1.8米、半径0.3米、中心Y=0.9；视觉助手身高约1.9米，根在脚底，不驱动物理体。
- Blender源前方为-Y、上为+Z，该朝向的角色右侧为-X；Unity目标前方+Z、上+Y、右+X。先用独立Front/Right/Up候选FBX在真实ModelImporter下验证，再展开角色与机台；未通过不得补偿运行旋转。第一次候选错误把Blender+X标为角色右侧，实测映射到Unity-X；已修正源标记为-X，不改运行时旋转或使用负缩放。
- 模型根位置与旋转归零、Scale=(1,1,1)，禁负缩放。Blender应用网格Rotation/Scale，保留有语义的门、转轴、轮盘、拉杆、机械腿等单独节点和正确Pivot。
- 所有互动挂点保留在Unity玩法Prefab中，模型仅作为Visual子节点。机台桌面高约1.2米、整体宽不超过3米，角色接近锚点与碰撞体由Unity配置；模型FBX不导出Collider。
- 角色采用风衣、折纸面具与发条帽的独立轮廓；机台分别以果匣、圆盘、硬币架、牌桌、骰匣、龙虎灯、翻页册、签筒、宾果钟、弹珠塔及七款社交机械表达规则。区域使用一致的原创符号与不同建筑节奏，不使用参考游戏资源或地图。

## 材质与导出

使用URP Lit/Unlit固定调色板：墨蓝、梅红、奶黄、薄荷青和铜金。共享材质槽减少移动端Draw Call；高频表情、招牌、图标采用保存贴图/模型，透明效果只用于必要的泡泡、墨迹及演出。FBX仅导出Mesh/Empty及实际需要的动画节点，不导出DCC相机、灯、地面或辅助标记。

本机Blender 5.2.1 LTS、Unity 6000.3.15f1。候选导出设置为Forward=-Z、Up=Y、单位1米、Apply Unit Scale=true、Bake Space Transform=true、无叶骨骼、无无关动画；最终设置以轴向门禁实测结果为准。

上述设置在2026-10-02经Unity Test Runner候选门禁作业`526c02d9`实际1/1通过：Front映射+Z、Right映射+X、Up映射+Y，根及标记节点无补偿旋转，所有局部缩放为正。后续正式模型仍需验证各可动节点和真实观看方向，不能只沿用候选通过记录。

正式多层资源随后发现FBX烘焙重复施加空间转换。源脚本在独立导出进程中对静态Mesh/Empty使用`G * SourceLocal * G^-1`，保持世界坐标对应关系；不修改安装的导出器，不在运行时抵消错误旋转。全部65资源门禁分别通过（作业`cc39160d`65项、重抽牌修复后`95c8becf`1项），更新后的六人拉杆另经`b8bd1ffb`1/1验证。显式三角化清除零面积面，重抽牌仅焊接1e-7米内近重合顶点；最终面数、bounds和SHA以随资源保存的manifest为准。

协作拉杆保存六个独立`Lever`与`TimingFace`节点。表盘、指针分别绑定持久共享材质的暗/开放/已拉下状态；运行时仅消费公开周期、参加人数及实际操作结果，不创建材质实例，不把单人NPC伪装成六名玩家。

## 文件与装配

运行资源统一在`Assets/LoadResources/Demos/jinx_casino/Art/`，模型在Models、共享材质在Materials，Prefab在Demo的Prefabs。独立DCC源文件与可复现生成脚本放`ArtSource/jinx_casino/`，不进入YooAsset运行资源包；它们属于本Demo源资源，不建立新的运行框架。轴向候选及一次性验证输出先保留在Library/JinxCasino/ArtGate；候选进入Unity测试资源路径后不参与Player资源收集。

阶段Builder先保存/重载模型和材质，再装配Visual；保留已有脚本GUID、Prefab根和交互绑定。正式资源验收分别记录模型层级、Importer设置、朝向、材质、实际游戏截图、双端Player构建；建模脚本执行成功不等于Unity显示正确或P4完成。

四区场地的顶棚、墙裙、地面导向与四件区域装饰在Editor烘焙为每区四个持久Mesh/共享材质段，位于各自权限内容根`FormalArchitecture`；源65个FBX共170327三角面（资源全集，非单帧绘制量）。建筑视觉内衬不带Collider，完整保留侧门Z[-9,-6]空档、原机台与出生点。顶棚关闭投影，沿用现有唯一方向光，不在运行时生成场地或材质。

## S1独立模型

S1源在ArtSource/jinx_casino/immersion，运行候选在Art/Immersion。S1Layout.json规定22操作热区/相机/身体代理；FBX根与子节点无轴向补偿，四模型已通过Unity实际导入门禁b2ddc5da（5/5）。12个独立URP材质由JinxCasinoImmersionArtBuilder首次创建，再运行保留已有材质调整；不得改旧P4 palette。正式场景中隐藏CardFaceLibrary/初始牌池/DrawCard和初始奖筹码，Hall.Ceiling关闭投影，具体绑定由分步装配承担。对应原生预览不是实际规则或最终照明证据。
