# S1 好运维修站独立源候选

本目录仅新增单机沉浸样板，不覆盖旧P4源/65FBX。模型是独立搪瓷果匣、真实牌桌、双人压力表与紧凑大厅；图形/模型重新制作，未使用参考游戏素材。当前四个模型已导入Unity，Importer与22目标坐标门禁5/5通过（b2ddc5da）；仍未完成场景装配、真实射线游玩和正式美术验收。

在独立进程执行，禁止打开/改变用户交互Blender：

```powershell
& 'D:/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python ArtSource/jinx_casino/immersion/build_s1.py
& 'D:/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python ArtSource/jinx_casino/immersion/audit_s1_source.py
```

`S1Layout.json`保存22目标、玩家出生、焦点和身体代理的唯一设计坐标；Editor候选可从同一文件装配。脚本通过AST只读取既有build_models.py的Model/导出函数，不执行旧入口，记录其SHA。独立`S1Models.blend`保留四组作者源、统一材质和语义层级，根保持identity；FBX及原生Cycles CPU预览进入Library/JinxCasino/ImmersionStaging/Art。预览灯/相机和只读追加的旧原创Avatar不进入四个FBX，不写回Avatar源。

前-Y、上Z、角色右-X，对应Unity前+Z、上Y、右+X。米制/根脚底（Hall地板延伸到Y<0，地面顶面0），静态Mesh/Empty局部identity、正scale。既有导出矩阵`G*SourceLocal*inverse(G)`，Forward-Z/UpY、bake_space_transform/apply_unit_scale为true，无动画/Collider/Camera/Light。仅场景机台根朝入口转160/180/200度，内部模型不做运行轴补偿。

关键语义：

- Slots.Reel0..2从玩家左至右对应结果数组，0樱桃/1柠檬/2星/3铃/4七/5香蕉。symbol k原角-k×60度，公开结果value对应+value×60度，六轮面有独立几何；两张Symbols012/345预览验证选择，不用近似相同色球。
- Slots.LeverPivot下挂HandleGrip，PrizeTray下有8个PayoutChip；初始/失败隐藏奖筹码，有限枚数仅做演出，真实数字以公共钱包/结果为准。
- Blackjack.PlayerCard0..11、DealerCard0..11、CardFaceLibrary/Rank1..13、CardShoe、DealOrigin、DrawCard、DealerArmPivot、PlayerTotal/DealerTotal；源含实体背面和rank几何模板。Editor隐藏库/初始牌池/抽牌卡，运行表现从安全公开牌恢复。规则没有suit，统一原创俱乐部牌标，不猜传统花色或牌堆。
- CooperativeLevers.Lever0/1、PlayerHandle/NpcHandle、TimingFace0/1、TimingNeedle0/1、Window0/1、HelpWindow0/1、SyncLamp、HelpWrench。单人只控制玩家0，NPC1由实际规则辅助；帮助窗口和扳手初始隐藏，按真实帮助状态显示。扫针/弧窗是物件，不能再依靠旧巨大机器面板。
- Hall.PrizeWheel/PrizeClaw为纯环境装置，奖券出槽、售货柜、维护工具和输币管均为静态场景内容；不会额外增加第四款游戏。

Actor、Controller、输入相机聚焦和新JinxCasinoTableTarget由主Agent接入；Target/Station都保持Hotfix.JinxCasino.Adapters。动态材质使用预建共享材质或PropertyBlock，禁止new runtime材质。源码只对新样板使用独立`materialPhysical`金属/粗糙度配置；Unity映射时不能改旧原型共用材质。低端Android优先烘焙环境照明，台面最多少量无阴影实时灯，顶棚不遮断主方向光，不全厅堆点光。

预览是构图示例，A/6与庄家7/暗牌是DCC摆拍，不是规则开奖证据。入口相机采用真实出生(0,1.6,-4.6)，没有挪出生点掩盖遮挡。VendorLayer是额外可步行位置，只用于看侧面层次，不替代入口验收。

审计分离：SourceAudit检查源姿态、22坐标、牌池、面数/FBXSHA；设计ProjectionAudit只核算720p可见范围和触控包围盒。Unity实际ModelImporter、材质、目标射线、薄HUD遮挡、运行发牌/转轮/时机、恢复/生命周期、720p和Android性能由主Agent后续验证。
