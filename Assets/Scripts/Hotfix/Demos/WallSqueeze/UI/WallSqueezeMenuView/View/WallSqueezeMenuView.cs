using Core.Runtime;
using Hotfix.WallSqueeze;

namespace Hotfix
{
    [Module("WallSqueeze")]
    [UIBind("WallSqueezeMenuView")]
    public partial class WallSqueezeMenuView : View
    {
        private WallSqueezeMenuPanel panel;

        protected override void OnGameObjectInitialize()
        {
            panel = WallSqueezeMenuPanel_WallSqueezeMenuView;
            BindData<WallSqueezeData>(_ => panel.Render());
        }

        /// <summary>在显示前交付当前世界。</summary>
        /// <param name="world">当前场景来源。</param>
        public void SetData(WallSqueezeWorld world) => panel.Bind(world);

        protected override void OnHide() => panel.Bind(null);
        protected override void OnDestroy() => panel.Bind(null);
    }
}
