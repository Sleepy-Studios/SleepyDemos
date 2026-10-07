using Core.Runtime;
using Hotfix.WallSqueeze;

namespace Hotfix
{
    [Module("WallSqueeze")]
    [UIBind("WallSqueezeHudView")]
    public partial class WallSqueezeHudView : View
    {
        private WallSqueezeHudPanel panel;

        protected override void OnGameObjectInitialize()
        {
            panel = WallSqueezeHudPanel_WallSqueezeHudView;
            BindData<WallSqueezeData>(_ => panel.Render());
        }

        /// <summary>在显示前交付当前世界。</summary>
        /// <param name="world">当前场景来源。</param>
        public void SetData(WallSqueezeWorld world) => panel.Bind(world);

        protected override void OnHide() => panel.Bind(null);
        protected override void OnDestroy() => panel.Bind(null);
    }
}
