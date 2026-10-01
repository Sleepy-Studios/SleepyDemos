# 在独立场景使用 DroneFlight

## 手动飞行

1. 新建空场景，放置地面、灯光和 Camera。
2. 新建 `DroneFlightBootstrap` 对象，添加 `DroneFlightStandaloneBootstrap`。
3. 指定 `DronePrototype`、出生点和场景 Camera。
4. 启动模式选“手动飞行”，Play 后按现有无人机按键操作。

Bootstrap 会实例化并装配无人机；不要在场景中同时预放第二架活动无人机。

## 自动航点巡航

1. 新建路线对象并添加 `DroneCruiseRoute`。
2. 在 Waypoints 中配置至少两个 Transform；可设置各点等待时间、速度覆盖和朝向策略。
3. 模式选择单次、循环或往返。
4. Bootstrap 的启动模式改为“自动巡航”，引用路线和 `DroneAutopilotConfig`。
5. 配置自动起飞与完成后悬停/自动降落策略。

路线点应留出大于配置到达容差的间距，并避免直接穿过墙体。当前巡航负责路径跟随，不包含动态避障和路径规划。

## 排查

- 无法启动：确认 Prefab 是成品无人机，路线至少两个有效点。
- 出生穿地：出生点只表示地面基准和朝向，检查地面 Collider 与 Prefab 起落架。
- 到点不停：检查水平、垂直和速度三类到达容差。
- 路线完成仍悬停：这是默认策略；需要落地时把完成行为改为自动降落。

