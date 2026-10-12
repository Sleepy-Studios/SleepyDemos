# 公共设置与效果体验美术确认

G10 的页面与组件效果图。用户于 2026-10-12 基本确认七张图，并要求公共逻辑复用、每个 Demo 使用自己的基础 UI Variant，保留原有风格，尤其纸艺卡丁车。暖白、朱红色瑞士风格用于 Hub 基础版本，不覆盖 Demo 美术；确认不等于正式 UI 已装配或视觉验收完成。

| 页面或组件 | 效果图 | 提示词 | 确认状态 |
|---|---|---|---|
| 显示与画质 | [查看](graphics-settings-v1.png) | [查看](graphics-settings-v1.prompt.txt) | 已基本确认，保留宿主风格 |
| 声音 | [查看](audio-settings-v1.png) | [查看](audio-settings-v1.prompt.txt) | 已基本确认，保留宿主风格 |
| 控制 | [查看](controls-settings-v1.png) | [查看](controls-settings-v1.prompt.txt) | 已基本确认，保留宿主风格 |
| 语言 | [查看](language-settings-v1.png) | [查看](language-settings-v1.prompt.txt) | 已基本确认，保留宿主风格 |
| 辅助功能 | [查看](accessibility-settings-v1.png) | [查看](accessibility-settings-v1.prompt.txt) | 已基本确认，保留宿主风格 |
| 公共输入提示 | [查看](input-prompt-v1.png) | [查看](input-prompt-v1.prompt.txt) | 已基本确认，保留宿主风格 |
| 公共效果体验 | [查看](presentation-lab-v1.png) | [查看](presentation-lab-v1.prompt.txt) | 已基本确认，保留宿主风格 |

## 图与实现的对应范围

- 五类设置页通过候选、应用、取消和恢复本页默认提交公共设置；语言分类仅 Hub 显示。控制页按三端参数分类，并按正式宿主选择可编辑动作。效果图选项来自已实现公共配置，不包含 Demo 触控布局或玩法专属参数。
- 抗锯齿页示例为 DLSS 关闭、MSAA4 生效；实际装配须反馈等待、降级和接管状态。DLSS/DLAA 实际接管时保留手动偏好，关闭后恢复。效果允许项展开后提供七类开关。
- 语言页的中英文本是美术示例。正式 UI 使用一次性文案/FontId API；不新增玩家字体选择功能，不建立语言事件或全局文本监听。
- 公共提示图展示图标组和独立说明、组合键、明暗背景、五态及触屏可点击版本。已核对并修正 Xbox X、PlayStation 菜单键和 Backspace 身份。正式图标必须直接使用项目已导入的 Kenney 资源，由有效绑定和设备布局决定；不能把效果图中的栅格化文字裁切成提示资源，也不能用品牌标志替代按钮。
- 效果体验是独立入口，图中选中电影演出；另两个分类提供七类后处理及倍率、渐变、短定格和持续冻结。体验产生临时作用域，退出或停止释放并恢复，不写入玩家永久偏好。预览场景是美术示意，最终实时效果仍须通过 Game View 验收。

使用内置 imagegen 生成，每张图为独立交付；保留最终选中版本，控制和公共提示的定向修正提示词附在对应文件末尾。图片与提示词用于当前阶段确认，不作为 Unity 制作资产直接引用。

确认后已完成 Hub 主 ViewTab、五个独立分页、公共提示与体验页装配；各 Demo 公共设置界面按后续要求删除，专属界面和提示 Variant 保留自身风格。运行与视觉验收结果统一见 [模块状态](../../../modules/settings-presentation.md#交付状态与验证边界)，此处只维护美术方向、效果图和对应范围。
