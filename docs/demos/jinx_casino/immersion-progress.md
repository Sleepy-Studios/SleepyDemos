# 单机沉浸进度

更新：2026-10-02。Goal active，无Token预算。当前 S0/S1准备中；S1体验尚未实现或验收，S2禁止提前扩展。

## 状态

|项|当前事实|缺口|
|---|---|---|
|原型归档|按依赖分五笔提交并推送；旧P4包保留|文档修正及S0设计仍整合|
|视觉与桌面|已确定三样板及实体契约；场景/输入候选在Library隔离|尚未装配运行|
|手柄|现有原型无Gamepad完整路径|路由候选、菜单、桌面、断连及真机待验|
|教学|新目标明确动作驱动|实现和新手实玩待验|
|样板交付|尚无新包和录像|三机台、区域、三输入、双端证据|
|用户门槛|尚未提交S1试玩验收|确认之前不扩S2|

## 已推送基线

- 9b9b415：公共离线会话协议、测试、边界文档。未来网络方案未决定，不继续扩建。
- e2d3149：赌场规则、存档、成长和直接相关测试。
- 13f26a9：原型美术、音效与可复现DCC源；排除自动备份。
- c0e4fb0：宿主、旧界面、场景、资源装配、Hub与字体及对应测试。
- 1cc64e7：双平台离线构建与配置恢复。

基线拆分未改变生产逻辑，采用原有Unity Test Runner和构建记录；本轮另查Console为0错误。git diff检查源码/文档通过；Unity生成meta空键尾空格原样保留。未执行全量测试。以上不证明新沉浸体验完成。

## 保留与阻塞

保留无关UnitySkills配置、vTabs与Mobile_RPAsset工作区状态。旧Blender自动备份未纳入源码。未获得Android真机运行及Xbox实物验证证据；可先实现和模拟输入回归，但不能宣称实物验收。S1用户验收是明确门槛，不能用自动化代替。

## S1实体交互基础验证

2026-10-02：JinxCasinoTableInteractionTests，PlayMode作业e0348f14，5/5通过；真实Collider射线遮挡/跨台拒绝、目标禁用/导航、嵌套聚焦拒绝、移动挂点跟随、退出与失效精确恢复相机。正式Editor编译完成、Console0错误。未执行全量测试；尚未接入Controller和机台资源，不代表视觉或三输入实玩。

计划切换文档已推送399e676。S0可恢复原型与规范落盘，S1基础代码开始实现。

## S1三设备输入基础验证

实体基础已推送8d9edee。独立输入资产通过Editor菜单保存并校验；Editor正式编译、Console0错误。JinxCasinoInputStateTests：dd1d77e2，EditMode 5/5。输入PlayMode初测2565032b为6/7；失败因测试释放帧未执行宿主要求的ReadFrame，补齐采样且保留断言。审查修复手柄Disabled后未发送震动归零，增加实际InputSystem.DisableDevice用例；重测825483dc为8/8。未执行全量测试。

以上为InputAction/模拟设备与Core同类UI模块的回归，不等于Xbox硬件、Android或完整宿主输入验收。输入设置持久化迁移、Controller/菜单/桌面接入、场景演出暂停仍待完成。新场地及三机台源制作中，尚无样板包。

## 焦点生命周期修复

三设备输入基础已推送ac22eca。审查发现同目标重新可用时高亮不恢复、目标销毁后导航访问失效对象；已做窄范围修复。JinxCasinoTableInteractionTests扩展后94ac799d，PlayMode7/7通过。当前直接相关范围为桌面7、输入状态5、输入路由8，共20项通过，未执行全量测试。S1尚缺Controller/机台规则接入、真实模型、教学、菜单/HUD、双端包与录像；用户样板验收未触发，Goal仍active。

下一闭环：整合独立S1模型与蓝图、稳定机台实例存档、宿主输入和桌面命令、暂停演出、精简HUD与教学。仅完成基础设施不计S1体验完成。

## S1机台定位与存档迁移

规则增加活动机台/最近结算机台定位；错误机台操作拒绝且不改变筹码、活动局或随机状态。版本1只读迁移，旧局显式同玩法认领，首次写新版保留不滚动的原件备份。新桌面宿主尚待接入这些接口。

Unity Test Runner：CasinoStationIdentityTests 5eaebcd1，4/4；CasinoAdventureTests 78b0253b，24/24；CasinoLocalSaveStoreTests 8334babd，4/4。未执行全量测试。三机台和大厅首轮DCC预览已实际查看，要求继续改善大厅空间/材质及庄家牌位；还未进入Unity模型门禁和视觉验收。

审查追加：Restore拒绝ActiveGame与活动局实际玩法不一致的快照；Save同时保护主档和普通.bak中的合法v1原件。扩展后CasinoStationIdentityTests作业37a3c3f4，5/5通过。与本轮冒险24项、三槽4项合计33项直接相关断言用例通过；没有执行全量测试，也不作为新桌面Player验收。

## 手柄偏好迁移

机台定位已推送25636e3。本机偏好v1→v2增加手柄参数并保留旧设置、原存储键和明确保存语义。CasinoLocalPreferencesMigrationTests作业11b0eca1，5/5；CasinoLocalPreferencesTests作业d245acad，8/8。正式Editor编译通过，未执行全量测试。控件及Host实际应用待接入，未冒充Xbox硬件验收。
