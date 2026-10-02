# PrototypeV2 参考稿生成记录

2026-10-02，使用内置 imagegen 生成与编辑，不使用 CLI/API。四图为用户审核用参考稿，非当前游戏截图或运行资源。历史 direction-a 不再作为制作依据。

## 交付文件

- [大厅](hall-v2.png)：保留原构图，移除两把阻挡机台接近的凳子。
- [共享二十一点](blackjack-v2.png)：去掉全部明牌的右下倒置角标，只保留左上牌值与花色。
- [Hub](hub-v2.png)：预览插画仅表达卡片构图，正式卡片不得冒充实机画面。
- [加载](loading-v2.png)：68%是构图示例，实际接入既有真实进度。

大厅初稿作为共享牌桌的风格参考，Hub初稿作为加载界面的风格参考。两次修图各只引用自己的初稿，不混用目标。

## 完整提示词

### 大厅初稿

```text
Use case: stylized-concept. Asset: an APPROVAL REFERENCE for the original Unity PC/Android game 倒霉蛋俱乐部. Create a beautiful SIMPLE RETRO CLUB interior that is straightforward to reproduce using a small set of modest beveled meshes and baked lighting. 16:9 landscape, believable first-person standing gameplay camera at the entry, eye height 1.6 meters. One compact rectangular room with clear foreground/middle/background, open walking route, at most 25% of screen is floor. Three unmistakable playable stations: LEFT a simple rounded burgundy wooden fruit-machine cabinet, ivory three-reel window, short brass lever and coin tray; CENTER a rounded green-felt blackjack table with chunky wooden rim, plain ivory playing cards and two large table-edge buttons; RIGHT a simple green-and-burgundy two-person timing machine with two ivory circular gauges and two chunky levers. One small original clockwork helper with a geometric folded-ivory mask and simple dark-green coat beside the right station. Material vocabulary limited to MATTE burgundy enamel, dark green felt, walnut wood, ivory paper, and a few SATIN brass details. Dark blue-grey broad wall panels, simple low wall lamps and one uncomplicated hanging light. Warm local light on the operating surfaces, gentle cooler fill, subtle contact shadows, clear readable shapes. A single simple sign reads '好运维修站'. Beauty should come from good proportions, restrained color, coherent material roughness and layered lighting, with tidy functional mechanical design. Crisp clean stylized 3D production render, moderate geometric detail, absolutely no photoreal micro-detail or cinematic effects. Restrict furniture and props to the three stations, two simple stools, one small supply counter and one plain rug. A reusable modular room, not a grand luxury casino. No carved ornament, filigree, patterned carpets, ornate pillars, city view, complex windows, metal pipes across the ceiling, mirrored floor, shiny gold borders everywhere, elaborate chandeliers, baroque panels, fake depth-of-field blur, dense signage, HUD, floating panels, or rainbow neon. Do not make a crude greybox; this must look intentional and attractive while visibly simple enough for a small Unity prototype.
```

### 大厅清理通行空间

```text
Edit target: the attached simple retro club hall. Keep the same camera, room, materials, lighting, blackjack table, fruit cabinet, two-lever cabinet, and original helper. Remove ONLY the two stools in front of the left fruit machine and right timing cabinet. Restore the continuous wooden floor beneath them naturally. Each cabinet must have an open approach zone from the central walking aisle, with no new props. Preserve the simple modest art style and all other objects.
```

### 共享二十一点初稿

```text
Use case: ui-mockup. Create a NEW shared PC/Android landscape blackjack gameplay-view REFERENCE, using the supplied SIMPLE retro-club room only for palette, material and visual identity. This is a common interface for all devices, NOT a special mobile interface. Full-screen 16:9, no phone frame, no poster montage. Straightforward Unity stylized 3D, modest meshes, no ornate luxury detailing. A large rounded walnut table with MATTE deep-green felt fills the lower 75 percent of the frame, simple thin satin-brass edge, burgundy base. Mildly elevated frontal fixed focus camera, cards and buttons extremely readable. Background uses the reference's plain blue-grey paneled wall and warm lamps, limited visual detail, no heavy blur. A simple small clockwork helper in a dark-green coat with geometric folded ivory mask behind the table. Two dealer cards: one face-up 7 of CLUBS and one face-down burgundy card. Player has TWO large face-up cards: red 10 of HEARTS and black 6 of SPADES, total 16. IMPORTANT: each face-up card uses one single large central matching suit symbol rather than a pip grid, correct top-left rank/suit and correctly rotated bottom-right rank/suit; never mix club and spade symbols on a card. Put small simple felt/table badges '庄家 7', '你 16', '投入 50'. The ONLY main actions are two large plain raised mechanical buttons integrated into the TABLE RIM: lower left burgundy button '停牌', lower right deep-green button '要牌', ivory text with simple tactile shapes and restrained shadow, no gold filigree. Top-screen quiet UGUI-style HUD: left small '离桌' button and coin icon '1,000', center '目标 1,200' and '02:48', right small pause icon and '?' help icon. UI panels are FLAT dark navy rounded rectangles, clear sans-serif Chinese, minimal trim. Screen edge safe margin, generous clear hit regions, no overlaps; cards remain well above the thumb/action area. No movement joystick or camera controls while at the table, no separate confirmation panel, no bet input box, no giant rule boards, no gold-rimmed luxury round buttons, no crowd of optional actions. Cohesive with reference room, visibly easy to recreate using actual low-complexity assets. A beautiful, practical design where physical objects ARE the main interface.
```

### 二十一点简化角标

```text
Edit target: the attached blackjack reference. Preserve the entire composition, table, character, lighting, UI, main button labels and all card positions. Make ONLY this precise correction: on ALL three face-up cards remove the lower-right inverted rank and small suit mark entirely, leaving clean ivory blank corners. Keep each card's top-left correct rank and suit and its big central suit: dealer 7 clubs, player 10 hearts and 6 spades. This is an intentionally simplified readable custom card design with one rank corner only. The 7+hidden dealer and player16 state remain unchanged. Do not redraw other elements.
```

### Hub主页面

```text
Use case: ui-mockup. Create a SIMPLE CLEAN implementation-ready 16:9 game HUB screen for the Unity demo collection SleepyDemos, shared between PC and Android landscape. This is a proposed layout reference, not a screenshot. Independent neutral modern HUB style, unrelated to casino decor. Mostly flat 2D UGUI-friendly design using plain panels, rounded rectangles, typography and a few small pictograms; no fancy shaders needed. Background deep blue-grey #1A2331 with very subtle broad gradient. Foreground warm white #EDF1F6 and muted teal #5DC4B5. Spacious but functional. Top left title 'Sleepy Demos', small subtitle '玩法实验室'. Top right one small restrained button '画质'. Section label '全部 Demo'. Main content four equal cards in a precise TWO BY TWO grid, generous gaps. Each card contains a modest wide preview area, one large readable Chinese title, one short secondary description, and one unmistakable wide '进入' button. Card 1 title '无人机飞行', description '驾驶与飞行挑战', preview a SIMPLE stylized small quadcopter over a muted green flight area. Card 2 title '小小搬豆工', description '搬运与闯关', preview simple colored worker blocks carrying a bean. Card 3 title '倒霉蛋俱乐部', description '机台与冒险', preview SIMPLE burgundy fruit machine and green card table, few brass parts. Card 4 title 'DLSS 实验室', description '渲染效果体验', preview a simple neutral 3D scene split in two halves, NO Nvidia logo and no claim of mobile DLSS support. The third card may have a subtle teal selected outline and its enter button highlighted; other cards remain obviously interactive. Bottom a quiet small row reading '更多玩法正在准备'. Crisp Chinese sans-serif type, clear title/body hierarchy, restrained shadows, consistent corner radius, low visual noise, large touch-friendly buttons, ample screen-edge safety margin. Preview images are original simple illustrations; never imply a photoreal AAA game is already implemented. No sidebar, tabs, search field, avatar, currencies, shop, ranking, fake player metrics, extra gaming services, decorative mechanical frames, gold filigree, glass panels, heavy bloom, microtext or giant hero image. The whole composition must be straightforward to reproduce from a SINGLE saved Canvas/View and four card objects.
```

### 加载界面

```text
Use case: ui-mockup. Create ONE implementation-ready LOADING SCREEN reference for the Unity SleepyDemos HUB, matching the supplied HUB reference's independent clean blue-grey, warm-white and muted-teal style. A common PC/Android landscape 16:9 design, not a device-specific layout. Mostly flat 2D UGUI elements, simple rounded shapes, clear sans-serif Chinese, generous margins. Deep navy background #1A2331 with restrained broad gradient and only a few very subtle simple geometric blocks. Small 'Sleepy Demos' brand at the upper left. Center: a small simple teal rounded-square emblem with a white doorway/arrow pictogram, then clear heading '正在进入倒霉蛋俱乐部'. Under it subdued subtitle '加载场景'. A long modest-height horizontal rounded PROGRESS BAR with teal fill showing exactly around 68 percent; '68%' aligned to its right or immediately under its right end. Lower down one concise quiet tip '靠近机台后可直接操作桌面物件'. This is an example scene-switch state; the same saved visual layout will be used for startup with its own truthful stage text. No casino wood or gold decor, no luxury filigree, no wallpaper illustration, no fake networking indicators, no made-up download sizes, no cancel/retry buttons, no second status dashboard, no sponsor logos, no thin unreadable microtype, no extravagant shader animation or elaborate 3D loading machine. Elegant restrained game launcher quality that can be recreated with ordinary Images, TextMeshPro text and one filled Image. Keep the style cohesive with the provided HUB image, but do not include its cards, gallery grid or quality button.
```
