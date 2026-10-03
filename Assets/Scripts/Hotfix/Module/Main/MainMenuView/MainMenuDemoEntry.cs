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
        public MainMenuDemoEntry(string key, string title, string description, string previewAddress, GameSceneId? sceneId)
        {
            Key = key; Title = title; Description = description; PreviewAddress = previewAddress; SceneId = sceneId;
            CanEnter = sceneId.HasValue;
        }

        /// 列表内稳定且唯一的业务身份。
        public string Key { get; }
        /// 卡片标题。
        public string Title { get; }
        /// 玩法介绍。
        public string Description { get; }
        /// 预览图片的公共资源地址。
        public string PreviewAddress { get; }
        /// 已接通的目标场景；未开放为 null。
        public GameSceneId? SceneId { get; }
        /// 页面根据导航事务更新的当前可进入状态。
        public bool CanEnter { get; set; }
    }
}
