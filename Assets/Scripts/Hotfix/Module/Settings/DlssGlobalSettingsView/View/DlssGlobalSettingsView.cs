using Core.Runtime;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// 宿主只选择自己的真实 Prefab Variant，提交与候选生命周期由公共基类负责。
    [Module("Settings")]
    [UIBind("DlssGlobalSettingsView")]
    public partial class DlssGlobalSettingsView : SettingsViewBase
    {
        protected override SettingsScreen Screen => SettingsScreen_Screen;
        public override GameSceneId Host => GameSceneId.Dlss;
    }
}
