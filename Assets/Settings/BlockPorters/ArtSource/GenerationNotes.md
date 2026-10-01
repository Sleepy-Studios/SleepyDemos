# 小小搬豆工软胶视觉素材来源

本轮使用 Codex 内置 image_gen 生成，未使用 CLI/API Key。概念图仅供风格参考；运行界面独立装配。素材没有烘焙动态文案、人数或色号。

## 采用的素材

- ToyTableConcept.png：概念参考，transparent_background=false。
- SiliconePanelSource.png：最终重新生成的面板，transparent_background=true；前两版中央灰斑不满足要求，未采用。
- ToyIconsSource.png：四图标透明底原稿，transparent_background=true。

运行副本在 Demo Art/UI。Sprite 切片、边框通过 Unity Sprite Editor 数据接口写入 Importer，保留 Alpha；不是用概念图截取整屏。ContactShadow 是 Unity Texture2D 计算生成的径向渐变，不是 AI 图片编辑。

## 概念图提示词

Use case ui-mockup. Generate a polished portrait 9:16 gameplay concept for an ORIGINAL casual mobile game about tiny people carrying colored voxel blocks and jumping with them into a deep disposal pit. High quality soft silicone toy tabletop diorama, cream warm white background, warm gray text, soft peach and mint decorative trim, restrained satin finish. Top compact header Chinese text '第 1 关' and a subtle mint progress bar plus small gear icon. Center large shallow rounded rectangular cream tray viewed from fixed oblique overhead angle, containing a heart pixel mosaic built from distinct coral pink, mint green and yellow rounded-edge solid bricks. A few tiny toy people visibly carrying matching blocks from the tray to one single circular/oval dark deep recessed hole BELOW the tray, with a thick rounded cream rim and dark inner walls. Below the hole a single row of FIVE empty small rounded task sockets, then FOUR columns of color team cards: EACH column exactly TWO visible cards, smaller preview card partially behind a larger foreground clickable card. Foreground cards show short counts like '8', not extra rows. Make actual gameplay area large and unobscured. Cozy tactile miniatures, clean readable UI, professional cohesive casual game visual, soft contact shadows, no coins no store no extra systems no phone bezel no neon or bloom. This is a concept art reference only, no fabricated additional controls.

## 最终面板提示词

A single blank white mobile-game UI button panel on a transparent background. Straight-on 2D vector-like view. Shape is a rounded rectangle, centered and isolated with padding on all sides. Interior is a completely flat opaque solid white fill, exactly RGB 255 255 255, from edge to edge. Only the thin outer border is light gray with a subtle soft raised rounded edge and a small lower shadow. No center shading, no hole, no transparency inside, no vignette, no grayscale gradient over the face, no texture, no icons, no writing. Cute silicone toy style. Simple clean commercial game UI sprite. Transparent alpha outside only.

## 图标提示词

Production mobile game UI icon sheet. Four distinct clean warm dark-gray glyphs arranged precisely in a 2 by 2 grid on a genuinely transparent 1024x1024 canvas with no labels, no lines, no text. Centers at (256,256),(768,256),(256,768),(768,768) measured from top left. Each glyph occupies at most 280x280 pixels and has transparent spacing to cell edges. Top left a simple friendly rounded six-tooth SETTINGS GEAR with clear central hole. Top right a small soft rounded SPEAKER with two sound waves. Bottom left a curved clockwise RESTART ARROW. Bottom right a chunky rounded HOME glyph with roof and doorway. Match each glyph weight and scale, subtle 3D silicone raised finish but almost flat for legibility, solid warm charcoal taupe #60554E, straight-on orthographic view, no perspective, no colored card behind icons, no shadow beyond 8px, no texture noise, no white background, no extra icons.

生成尺寸和各图标实际位置可能与提示不同；切片使用实际像素 Alpha 范围，没有假定输出严格服从坐标。面板九宫格边缘保留软胶高光，实际效果以游戏截图为准。

## 第二轮：按概念图还原界面

使用同一张 ToyTableConcept.png 作为风格参考，通过内置 image_gen 分别生成 ReferenceCardSource.png、ReferenceCubeSource.png 和 ReferenceSocketSource.png，三个请求都使用 transparent_background=true。原稿保留在本目录；运行副本只通过 Unity Texture2D 对 Alpha 边界裁剪，不改图片内容，卡片边框通过 TextureImporter 保存。

卡片提示词：
Use reference only for UI style. Generate ONE isolated UI foreground team card asset matching the four bottom cards of the reference. Frontal orthographic flat rectangular shape, WIDTH:HEIGHT 0.85, rounded corners, soft molded silicone material. Neutral milk WHITE colored version for runtime color tinting. Completely opaque solid near-white flat central face with no icons and no numbers and no text. Thick softly rounded outer silhouette, VERY SUBTLE almost invisible outline, light top edge, visible 3D thickness as a 12-pixel darker lower lip, broad soft gray shadow below, NO dark thin outlines or metallic frame. This must look exactly like the peach/yellow/mint cards in reference except neutral white. Transparent outside only. Centered square canvas with ample padding all sides. Flat center permits nine-slice stretching. No vignette or dirty gray spot in the center. No other items.

图标提示词：
Generate one isolated UI icon of a rounded-edge WHITE silicone toy cube, matching the colored cube icons on bottom team cards in reference, but neutral white so a game can color tint it. Isometric view, top face and two side faces visible, top face bright neutral white, left side light gray, right side medium light gray, softly rounded chamfer edges, satin soft rubber finish, soft coherent upper-left lighting, small subtle contact shadow, no heavy outlines. Completely opaque cube. Cube fills middle 65 percent of square canvas, alpha transparent background outside. No card behind it, no pedestal, no text, no other objects. This is a production game UI sprite intended to tint into ANY of twelve colors dynamically.

任务槽提示词：
Generate ONE isolated empty task socket from the reference's row of five recessed square sockets above the bottom team cards. Frontal orthographic UI sprite. A shallow recessed rounded square well with warm ivory thick smoothly molded silicone rim, soft inner shadow especially on top and left, matte beige bottom surface, tiny subtle texture on bottom only. Clearly concave sunk into the tabletop, NOT a raised button. No plus sign, no icon, no text, no content. Soft realistic toy product render matching reference, no harsh charcoal outlines, no metallic edges. Completely opaque inside the well, transparent outside outer silhouette with slight soft external shadow. Centered square canvas generous transparent padding.

## 专用进度槽（2026-10-01）

ProgressTroughSource.png 为 imagegen 透明原稿，运行副本为 ProgressTrough.png。Sprite Editor 只裁剪主物件的透明边距并保存九宫格，不重画整屏。ProgressCapsule.png 是 Unity Texture2D 数学绘制的白色胶囊，供 Mask 和动态填充使用；绿色与数量由 HUD 绘制。

提示词：
Create a single reusable game UI sprite on a transparent background: a very wide thin horizontal recessed capsule trough for a soft silicone toy tabletop mobile puzzle game. Front view, completely straight and symmetrical, approximate aspect ratio 13.6:1. Pale warm ivory outer lip, light beige hollow inner channel, subtle soft inner shadows at the top and left, delicate rim highlight, no raised button, no pedestal, no heavy drop shadow. Empty trough only, NO green filling, NO text, NO icons, NO numbers. Capsule ends are fully round. Object spans most of the width, transparent padding around it. High quality softly lit stylized 3D clay/silicone rendering, matte cream rubber. Output a wide landscape transparent PNG with only this single trough centered.

## 五列四排与整体背景（2026-10-01）

`FiveColumnConcept.png` 为五列四排布局参考；`TabletopBackgroundSource.png` 为最终空背景，正式副本 `TabletopBackground.png`。整体背景只包含桌面和空棋盘框；数字、进度、队伍、任务槽与坑口均由运行时绘制。首版空背景的框底过高，已用 imagegen 延长至画面约 54% 高度，最终源图保留完整原稿。`AdUnlockIconSource.png` 为透明视频／锁图标，正式 Sprite Editor 裁剪副本为 AdUnlockIcon.png。

效果图提示词要点：portrait 9:16 original voxel-carrying game; exactly five columns by four rows of number-only color cards; first row clickable; five free task sockets; small central pit about 13% screen width; two separate ad unlock sockets at left and right; warm low-contrast cream gingham becoming pale peach; flat ivory picture frame; preserve 3D bricks and small porters, no copied logos or platform chrome.

最终背景提示词要点：keep only the subtle gingham tabletop and one empty ivory rounded picture frame, remove all text, progress, characters, blocks, hole, sockets, cards and icons. Frame left/right 4%/96%, top/bottom 9.63%/54.07%. Extend only the blank inner field to reach the bottom constraint, preserve small corner radii and rim thickness. Everything below is uninterrupted quiet gingham.

图标提示词要点：one transparent production UI glyph, a muted blue video-play rectangle with triangle cutout next to a small closed padlock; subtle silicone bevel, no text, generous transparent padding; readable at 26px.


## 统一 80×80 方格与四套主题

本轮内置 imagegen 生成 TileStateComparison、TileSocketSource、TileFaceSource 及 ToyTableSource／GardenSource／SeasideSource／StarrySkySource。状态图是参考，实际方格文字和牌色由 TMP／Image 绘制。凹槽底座与中性凸起面分别生成，透明背景；运行副本在 Unity Texture2D 按 alpha > .025 的包围盒裁剪并留两像素边，保留源图，未从整屏概念图切文字。

主题提示约束：portrait 600:1080，唯一暖白棋盘框 x24 y104 w552 h400，装饰只在框外角落；无方块、坑口、文字、按钮和槽位。花园使用浅鼠尾草绿毛毡及边缘花叶；海边使用浅沙色、角落贝壳与淡青海洋纹理；星空用浅薰衣草／长春花色、边缘柔月亮和星星，内区仍暖白。

后面三套以 ToyTableSource 作图像参考。正式 600×1080 纹理由 Unity 使用同一玩具桌边框片区合成，坐标分段映射至目标框，按圆角轮廓轻微羽化；四套框和内区像素一致，避免生成图框漂移。原始图和本说明不进入运行包，正式 Texture 位于 Art/UI/Backgrounds。

TileStateComparison 提示约束：五个相同80×80外框，圆角12、内边距8、厚度6、柔影向下4；当前／预览／空槽／运输／模拟广告五态。SocketSource 只生成奶油白凹槽框及内阴影，FaceSource 只生成可着色中性白凸面，无文字。运行统一嵌套 TileBase.prefab；所有格子与热区80×80、彩色面64×64。制作步骤使用临时装配脚本完成后删除，不保留重建工具。

八关实际回放发现“筒”不在既有字体及 fallback 中。复用现有 TMP Font Builder，仅补 Demo 专用字体的抬／坑／堵／筒四字，保持采样54、padding8、512 atlas和原 fallback，保留字体、材质及 atlas GUID；可维护字符集为 BlockPortersCharacters.txt，临时调用工具已删除。
