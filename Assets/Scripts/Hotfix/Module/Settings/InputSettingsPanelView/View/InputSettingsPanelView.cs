using Core.Runtime;

namespace Hotfix
{
    /// ViewTab 拥有的独立设置分页，内部编辑器仅在显示期间绑定候选。
    [Module("Settings")]
    [UIBind("InputSettingsPanelView")]
    public partial class InputSettingsPanelView : SettingsPanelView
    {
        protected override SettingsPage Page => InputSettingsPage_Page;
        protected override void OnGameObjectInitialize() => InputSettingsPage_Page.AttachList(this);
    }
}
