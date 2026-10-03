using Hotfix.SceneManagement;

namespace Hotfix
{
    /// Hub 的单项展示数据，不持有物理 Cell 或 UI 控件。
    public sealed class MainMenuDemoEntry
    {
        /// <summary>建立一个展示入口；没有目标场景的卡片保持未开放。</summary>
        /// <param name="key">列表内唯一且稳定的业务标识。</param>
        /// <param name="title">卡片标题。</param>
        /// <param name="description">简短玩法介绍。</param>
        /// <param name="previewAddress">通过公共资源系统加载的预览图片地址。</param>
        /// <param name="sceneId">已接通的场景；null 表示未开放。</param>
        /// <param name="subtitle">主展示的简短副标题；省略时使用玩法描述。</param>
        public MainMenuDemoEntry(string key, string title, string description, string previewAddress, GameSceneId? sceneId, string subtitle = null)
        {
            Key = key; Title = title; Description = description; PreviewAddress = previewAddress; SceneId = sceneId;
            Subtitle = subtitle ?? description;
            CanEnter = sceneId.HasValue;
        }

        /// 列表内稳定且唯一的业务身份。
        public string Key { get; }
        /// 卡片标题。
        public string Title { get; }
        /// 玩法介绍。
        public string Description { get; }
        /// 卡片与主展示使用的简短副标题。
        public string Subtitle { get; }
        /// 预览图片的公共资源地址。
        public string PreviewAddress { get; }
        /// 已接通的目标场景；未开放为 null。
        public GameSceneId? SceneId { get; }
        /// 页面根据导航事务更新的当前可进入状态。
        public bool CanEnter { get; set; }
        /// 未开放项目也可查看介绍；导航期间暂时禁止切换预览。
        public bool CanBrowse { get; set; } = true;
        /// 当前大预览展示的玩法，与指针悬停和键盘焦点分开。
        public bool IsSelected { get; set; }
    }
}
