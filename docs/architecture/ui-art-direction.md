# 公共 UI 美术与复用规范

## 目标与宿主

公共 UI 使用清楚的瑞士平面排版、暖白底、深墨文字和克制的朱红重点，方向以 Hub MainMenu 与[已确认的公共控件规范板](../agent/art/ui-foundation/README.md)为依据。规范板中的宣传文案、色值标签和装饰英文不作为正式资源。

Hub 使用公共基础外观；Demo 按自己的宿主风格覆盖表现。纸艺卡丁车保留纸卡、纸纹和墨色，无人机、实验室及夹爆它沿用各自规范。公共基础改动应传播结构和行为，不把 Hub 的颜色、图片或字号强加给 Demo。

## 三层所有权

| 层级 | 维护内容 | 修改位置 |
|---|---|---|
| 公共基础 Prefab | 结构、组件、强类型绑定、交互状态、布局行为与生命周期 | `Assets/LoadResources/UI/Common/`；公共行为在 Core.Runtime |
| Demo 风格 Variant | 同类控件共享的图片、颜色、主副字号、边框与阴影 | `Assets/LoadResources/Demos/<DemoId>/Prefabs/UI/Foundation/`；继承对应公共基础 Prefab |
| 页面实例 | 业务文案/动作/数据、可见性、位置及宿主必需的尺寸布局 | 该页面 Prefab 与 Hotfix View |

样式调整集中在风格 Variant，避免在每个页面实例重复覆盖同一颜色、字体和图片。Variant 保留基础源连接；页面嵌套 Variant。基础源改结构后检查四 Demo 的继承结果；风格源改外观后检查所有实际使用方。清理断继承的复制控件与无效覆盖时，先保护业务参数、绑定和必要布局，不能整树 Revert 或 Unpack 后重建。

开发时换一整套风格，修改或替换 Demo 的风格 Variant，再检查使用方的遗留样式覆盖。不建立运行时主题切换、字号主题系统或第二套样式管理器。具体制作步骤见[基础 UI 手册](../runbooks/use-core-ui-components.md#制作与更换控件风格)。

## 完整风格库与源码色板

每个 Demo 在 `Prefabs/UI/Foundation/` 维护完整公共基础控件的真实 Variant，包含当前未使用及暂时与 Hub 同风格的控件。统一前缀为 `Drone`、`Dlss`、`Wall`、`KagitKart`，后续按动作或控件用途命名；文件名不编码字号和生成哈希。同用途复用源，调试、返回、关闭、开始、不同纸重等真实差异各自保留；保存的精确字号不因统一命名而合并。

组合控件内部的标题、关闭按钮、页签、开关、滑条及动态项模板继续引用本 Demo Foundation 子变体，不能只给外层改颜色而把内部留在公共外观。具体28项覆盖清单、已有语义资产和维护边界见[基础资源维护](../modules/ui-foundation-assets.md#四-demo-完整风格库)。全局 Loading 与 Hub 设置页面沿现有流程维护，不作为本轮 Demo 风格库的复制源。

框架自身的少量语义色保留在 `Core.Runtime.ColorUtil.Colors.Common`；Hub 与四 Demo 的固定 UI/HUD 源码色板维护在 `Assets/Scripts/Hotfix/Module/Common/UIColors.cs` 的 `UIColors.{Common,Hub,DroneFlight,WallSqueeze,KagitKart}` 分组，其中 Hotfix 的 `Common` 维护共享业务 View 的固定颜色（例如 Loading 备用底色与循环列表示例），与 Core 的框架语义色分开。字段使用语义名，声明统一六位大写 HEX，可选 `alphaPercent:`（0～100、支持小数）；原浮点 RGB 按最接近的八位值量化，不借格式统一重新配色，非整数 Alpha 保留足够精度。已有颜色替换透明度复用 `WithAlpha`（0～1），富文本复用 `WrapText`/`ToHex`；动态角色/配置色值、场景 Shader 打包参数及可配置组件默认值保留原职责。色板为源码常量入口，Prefab/Variant 继续保存图片、颜色、字体和状态表现，不建立绑定、自动应用或运行时换肤系统。DLSS 当前没有固定运行时 UI/HUD 色值，其外观只由 Variant 保存；源码保留该区域的说明，不维护空类或只供临时 Builder 的字段。

## 公共颜色起点

下表来自现有保存资源；十六进制是浮点值量化后的近似值。制作时以 Prefab 的 RGBA 为准，按当前宿主组合检查可读性，不从效果图取色替代资源核对。

| 用途 | RGBA（alpha 为 1） | 近似色值与来源 |
|---|---|---|
| 普通控件暖白 | `(0.99, 0.985, 0.96, 1)` | `#FCFBF5`，CommonButton Normal、Tab、面板 |
| 公共控件深墨 | `(0.105, 0.16, 0.17, 1)` | `#1B292B`，CommonButton Label、面板文案 |
| 朱红重点/焦点边 | `(0.949, 0.243, 0.161, 1)` | `#F23E29`，CommonButton 焦点与装饰 |
| 白字动作表面 | `(0.788, 0.204, 0.133, 1)` | `#C93422`，CommonButton Pressed、BtnSwitch1 选中胶囊；不透明白字对比度约 5.25:1 |
| 普通边界 | `(0.725, 0.722, 0.686, 1)` | `#B9B8AF`，普通边框与分隔线 |
| Hub 主信息墨色 | `(0.105, 0.16, 0.17, 1)` | `#1B292B`，MainMenu 标题与文案 |
| Hub 辅助信息 | `(0.39, 0.43, 0.42, 1)` | `#636E6B`，MainMenu Description/Status |
| Hub 浅面板 | `(0.99, 0.985, 0.96, 1)` | `#FCFBF5`，MainMenu Settings/Effects |

朱红主要标识焦点、当前重点和少量状态，不铺满信息面板。普通信息与实际背景的对比度以至少 4.5:1 为制作参考；禁用文字仍以至少 3:1 为项目参考。按最终透明度、图片与场景背景检查，不仅比较两个原始色值；朱红底上的白色小字不能直接视为合格，必要时加深底色或改为深墨文字。

## 字体与层级

中文及混排复用 `HarmonyOS_CN`，英文复用 `BebasNeue_EN` 及其公共回退；FontId、材质、缺字维护遵守[多语言与公共字体规范](localization-fonts.md)。字号、颜色和排版在 Prefab/Variant 维护，不由页面代码统一缩小，也不新增 Demo 补字字体。

当前公共 `HarmonyOS_CN.ttf` 源字重为 Bold。普通控件、面板标题和正文使用 `FontStyles.Normal`，避免再合成一次粗体；字体层级主要通过字号、颜色与间距区分。

以 1920×1080 制作为起点，新增公共控件使用以下默认值，再根据宿主与真实中英文长度验收：

| 层级 | 默认字号（Canvas 设计单位） | 用途 |
|---|---|---|
| 页面主标题 | 64 | 独立页面标题；Hub 首页已有主标题 88 可保留 |
| 通用标题条/面板标题 | 32 | `Commom_Title` 居中，BG 系列嵌套标题左对齐 |
| 分区标题 | 36 | 独立分类或内容区标题 |
| 主要动作/控件标签 | 24 | 按钮、设置项与主要提示 |
| 正文/次级动作 | 24 | 说明与可操作信息；优先扩大可用区域或换行 |
| 辅助信息/副标签 | 20 | 键位、状态与次要说明；主要输入提示可用 24 |

普通桌面 1080p 的功能文字最终显示应至少 18 像素。设计字号不等于最终屏幕像素，须检查 Canvas 缩放、窗口尺寸和真实截图。标题与正文靠字号、字重、间距和对齐形成层级；长译文先处理布局、换行或已有自动滚动，再在明确上下限内使用自适应字号，不能把所有文字缩到最小值。

公共按钮、通用标题和 SimpleTips 当前使用 TMP `FontStyles.Normal`，字符、单词、行及段落的额外 spacing 均为 0；行高沿用公共字体度量，0 不表示文字没有行高。新增多行正文先沿用该起点，在最终显示尺寸检查拥挤和换行，再在对应风格源调整。字重、字号与颜色由风格源维护，业务文案中的富文本强调只承担该文案的语义。

## 布局与当前制作样本

以下数值均为 Canvas 设计单位。新增公共内容使用 4、8、12、16、24、32、48 的间距档位：4/8 用于相邻细节，12/16 用于图文与控件组，24/32 用于面板内边距，48 用于独立内容区。此处是制作起点，不是批量重排已确认页面的命令；安全区、长译文与触屏命中需要更大空间时由宿主保留必要布局。

| 当前源控件 | 可复用的制作样本 | 所有权与尺寸说明 |
|---|---|---|
| `Btns/CommonButton.prefab` | 根默认 280×64，标签 24 居中；Sliced 表面/普通边使用 PPU multiplier 3，完整焦点边使用 1.5 | 图形边宽分别约 1/2；不将此默认宽高覆盖到现有页面。根点击矩形由宿主布局决定 |
| `Btns/BtnSwitch1.prefab` | 根默认 180×48；左右文字 24，始终显示；选中胶囊距半区边界 2 | 整个根按钮一次点击切换，与 BtnSwitch2 共用行为；不是两个独立命中区域 |
| `Btns/BtnSwitch2.prefab` | 根默认 120×50；圆柄 46×46；独立开/关说明 24 | 胶囊图片与圆柄属于风格；根尺寸、业务值和动作仍按宿主维护 |
| `Btns/Toggle.prefab` | 根默认 180×48；勾选框 30×30，标签 24，标签区从框右侧 42 起 | 框与标签约 1.25 的尺寸比例；装饰不射线，整块 Toggle 负责交互 |
| `CommonSlider.prefab` | 轨道高 12；圆柄 28×28、半径 14、朱红描边 1.5；Fill/Handle Area 水平 inset 总计 24 | 不把轨道当点击区域；根使用可导航 Slider。值域、整数步进及事件属于使用方，Demo 可在 Variant 替换圆柄风格 |
| `Bg/BG_Large.prefab` | 标题高 64、距顶 12、普通面板左右边距 32；标题 32 左齐，下边细分隔线 1 | 同源标题条在面板内保持左齐；`Commom_Title` 独立页面标题才居中。侧栏面板保留侧栏宽度并追加内容间距 |
| `Bg/BG_Large.prefab` / SimpleTips 的 Body | 圆角 6、描边 1；浮层深墨阴影 Alpha 0.14、偏移 `(0,-6)`、柔化 8 | 普通平面控件不用这套阴影。SimpleTips Arrow 只有轮廓，不复制 Body 阴影；外扩不增加点击矩形 |
| `CommonTips.prefab` 隐藏模板 | 图标 28×28，正文 24，图标占位 44；Details padding 左16/右44/上8/下8 | 约 1.17 的图标/正文字号比例；固定字号，正文长时沿用已有滚动，不缩小成难读文字 |

表中 `Btns/`、`Bg/` 和公共 Slider 路径相对于 `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/`；Tips 路径相对于 `Assets/LoadResources/UI/Common/`。最终可见尺寸还受锚点、父布局、Canvas 缩放影响；不要仅按 `sizeDelta` 推断拉伸控件的实际大小。图片、字体和共同状态放在风格层，页面保留业务内容、必要位置与尺寸；继承修改、风格替换的操作与保护见[基础 UI 手册](../runbooks/use-core-ui-components.md#制作与更换控件风格)。

## 图片、边框与层次

共享面板和完整焦点边位于 `UI/Common/Sprites/Surfaces/{Surface,FocusBorder}.png`，路径相对于 `Assets/LoadResources/`；原 Hub 资源已通过 Unity 原生 MoveAsset 移动并保留 GUID。夹爆它的三个按钮风格 Variant 使用同目录 `FocusBorderSquare.png`（24×24、四边 Border 3），保留直角外观。普通圆角和连续边框优先使用带 Border 的 Sliced Sprite，焦点边是一圈完整轮廓，不能拆成互不相接的四根边条。资源实施与验收边界见[基础资源维护](../modules/ui-foundation-assets.md)。

普通按钮和页签保持平面、清楚的边界；浮层与抬高面板才按需要使用轻阴影。图片的翻转、旋转、渐变、圆角、细描边和小范围阴影由同节点 `ImageExtend` 组合，能力限制见手册；TMP 使用既有 `TMP_UGUI_Extend`。避免叠加多套网格效果或为普通矩形增加特效材质。

主菜单 Gallery 的开始、设置、效果入口使用 `UI/Hall/Foundation/Hub{Start,Settings,Effects}Button.prefab`，三者是公共 CommonButton 的风格 Variant，集中保留该页面已确认的青绿重点、暖白表面与原阴影。页面只保留布局、文案、业务绑定，不单独复制五态和图片样式；这组已有页面外观不替代公共控件规范板的朱红默认风格。

`ImageExtend` 的圆角只影响自身图片，不创建、删除或自动配置 Mask。子内容裁剪按实际需求显式使用原生 `Mask` 或 `RectMask2D`，保留作者已有遮罩；不要把圆角图片效果当作子层级遮罩。

普通进度复用白色 Filled Sprite，圆角由背景或遮罩承载；特殊形状保留真实纹理和填充方向。图标与装饰不拦截射线，特效外扩不改变按钮根矩形。InputPrompt 两种阅读顺序、原生宽度计算与组合键规则见手册，不能由页面反转槽数组或拼固定物理键名。

设置分类图标集中维护 `UI/Common/Sprites/Settings/` 的五个 SVG，透明 200×200 viewBox；200×200 与 50×50 是使用尺寸，不维护重复大小稿或状态稿。采用已确认的墨色与朱红几何语言，语言符号使用路径而非字体。项目 uGUI 使用 `com.unity.vectorgraphics@3.0.0-preview.7` 的 `SVGImage`，导入类型为 `UI SVGImage`，保留宽高比；禁止把它导成 Texture2D 后仍宣称无损缩放。矢量路径会细分成网格，缩放仍需检查曲线精度、三角形数量和小尺寸辨识度；减少无意义路径，不承诺自动降低渲染成本。正式设置页以50×50显示同一源，图标左侧19、垂直居中，文字从84开始、右侧16，沿原HorizontalLayoutGroup留出间隔；图标ignoreLayout且不拦截射线。选中态由原分类背景和选中条表达。

这项选型遵循 [Unity 6.3 的 SVG/uGUI 支持说明](https://docs.unity3d.com/Packages/com.unity.vectorgraphics@3.0/manual/index.html)及[矢量图形限制](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/work-with-vector-graphics.html)。本包当前为预览版本，升级须复核导入、遮罩、颜色与实际截图；不据此批量替换普通九宫格或带纹理图片。

## 状态、透明度与声音

控件使用一个 `UIState` 表现 Normal、Hover、Focused、Pressed、Disabled，业务 Selected/On/Off 按需追加。五态要清楚可辨，Normal 恢复所拥有属性；触屏抑制交互强调，禁用仍可读。状态反馈优先改变颜色、完整焦点边或装饰，保持根尺寸与点击矩形稳定。

纯透明度控制优先 `CanvasGroup.alpha`，其 alpha 与子元素自身 alpha 相乘；检查多层 CanvasGroup，避免重复衰减。需要隐藏且禁止交互/射线时复用 `SetCanvasGroupVisible(false)`；需要触发激活生命周期时使用现有 `Show/Hide`。本规范不统一动效时长、曲线或转场设计，沿用正式导航与表现契约。

后续新声音的质量要求见[音频规范](audio-assets.md#声音质量与宿主语义)，不据本规范批量审听或替换已有音频。

## 从参考到验收

AI 与制作人员按同一流程工作：读宿主规范 → 检索已有组件、资源和调用方 → 在 [Game UI Database](https://www.gameuidatabase.com/) 研究真实游戏截图 → 制作效果图并逐项确认 → 原生装配 Prefab/Variant → 查看真实 Canvas/正式入口截图并检查交互。记录参考页面、所借鉴的结构和适用场景，不直接照搬游戏版权素材，不把概念图当作运行截图。

公共基础先验收，再接入 Demo；换风格检查一个源控件及其嵌套使用方，确认基础变化传播、样式变化传播、五态、长文案与安全区。检查中英和项目实际支持画幅，键鼠、手柄与触屏按实际可验证设备分别报告。编译、自动化断言、视觉和真实设备结果分开记录，迁移保存成功不代表整套验收完成。

启动 Loading v3 的瑞士抽象背景与运行期 Loading v2 的无人机场景均已获用户确认，并装配到正式 `StartupLoading.prefab` 与 `CommonLoading.prefab`。去字背景与实际 TMP 文本、阶段、真实百分比和 Filled 分开维护；全屏图片等比覆盖裁切，信息层保留 Canvas 边距。启动 v2 仅布局获认可，海岛背景不实施；v1 均已否决。参考来源、审批与实施状态见[美术确认记录](../agent/art/ui-foundation/README.md)。原生 Canvas 预览不代替正式 AppEntrance 的运行截图、四 Demo 切换与画幅验收。
