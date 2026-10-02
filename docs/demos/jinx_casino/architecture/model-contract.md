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

维护已保存模型、共享材质及Prefab中的Visual引用，保留已有脚本GUID、Prefab根和交互绑定；模型更新后核对实际导入结果，不用临时阶段Builder重新生成场景。正式资源验收分别记录模型层级、Importer设置、朝向、材质、实际游戏截图、双端Player构建；建模脚本执行成功不等于Unity显示正确或P4完成。

旧Main的四区合并网格及FormalArchitecture装配已删除，不作为当前场景维护入口。上述65件源FBX与门禁是历史资源记录，固定完整家族数量的测试已清理；现用助手/商品/效果模型保留，当前S1使用下述专属模型与真实导入/场景合同。新四区按确认后的简化参考稿重做，不能把旧资源数量当作体验验收。

## S1独立模型

S1源在ArtSource/jinx_casino/immersion，运行资源在Art/Immersion。S1Layout.json规定22操作热区/相机/身体代理；FBX根与子节点无轴向补偿，四模型已通过Unity实际导入门禁b2ddc5da（5/5）。12个独立URP材质维护已保存的Material资产，不重新生成，不修改旧P4 palette。正式场景中隐藏CardFaceLibrary/初始牌池/DrawCard和初始奖筹码，Hall.Ceiling关闭投影；具体挂点、表现引用与初始显隐维护于保存场景和Prefab。对应原生预览不是实际规则或最终照明证据。
`RulesPlacard`保存为独立夹板及支架，正文引用保存在专属表现组件，不放入FBX或运行时动态生成。规则完整显示，不能截去当前投入修正。直接维护保存夹板布局及聚焦挂点，同步S1Layout源与运行副本。16:9门禁检查规则牌四角与22个操作目标中心入镜，仍需逐设备实际可读性和遮挡验证。

合拍台`s1.sync/LeverAssistant`复用正式Avatar模型，场景保存位置(-1.30, 0, 0.58)、单位比例及零局部旋转。右肩的GripContact位于模型原手掌中心，由S1LeversPresentation跟随NpcHandle；袖臂小幅伸缩保持握点接触，头部随实际拉杆进度点头。关节只由该机台表现控制，不挂使用独立时钟的AvatarPresentation，不增加相机、Listener、Collider或交互目标。新局回待机，暂停保持姿态，Restore直接恢复已拉/未拉状态而不触发规则或重新发奖。调整助手位置后需检查手掌接触、袖臂比例、脸部可见性及是否遮挡筹码/按钮/仪表和规则牌。
