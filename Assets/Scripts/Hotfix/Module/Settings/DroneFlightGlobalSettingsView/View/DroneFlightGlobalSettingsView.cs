using Core.Runtime;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// 宿主只选择自己的真实 Prefab Variant，提交与候选生命周期由公共基类负责。
    [Module("Settings")]
    [UIBind("DroneFlightGlobalSettingsView")]
    public partial class DroneFlightGlobalSettingsView : SettingsViewBase
    {
        protected override SettingsScreen Screen => SettingsScreen_Screen;
        public override GameSceneId Host => GameSceneId.DroneFlight;
    }
}
