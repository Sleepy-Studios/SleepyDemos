# Loop Scroll Showcase

一次导入本 Sample，打开 Main.unity 即可浏览全部四个子场景；每个子场景均可返回。首次中文，主菜单提供中文 / English 切换并保存选择。

Editor 导航自动解析导入目录，不需要修改 Build Settings。Tools/Sleepy Loop Scroll/Open Showcase 打开主页；Build Imported Sample Scenes 重建完整场景组，重建前保存当前场景。

独立 Player 演示需显式包含 Main.unity、Basic/Basic.unity、MultiType/MultiType.unity、Chat/Chat.unity、CarouselPaging/CarouselPaging.unity；不要自动添加到宿主构建配置。

Common 包含示例专用的语言、布局、场景目录和 Noto Sans SC 字体。FONT-LICENSE.txt 仅适用于字库；包运行时不依赖这些示例组件。只导入一份 Showcase，避免重复程序集。SleepyDemos 已维护一份导入内容，不必再次导入。

SleepyDemos 的宿主示例 Builder 可追加 MvcBind / ItemView 页面，纯包场景目录保持五项。

简单列表先 RegisterCellBinding 一次，再 SetTotalCount；刷新、完整重填与定位分别使用 RefreshCells、RefillCells(RefillOptions)、ScrollToCell。多类型和聊天通过 SetDataSource 提交。
