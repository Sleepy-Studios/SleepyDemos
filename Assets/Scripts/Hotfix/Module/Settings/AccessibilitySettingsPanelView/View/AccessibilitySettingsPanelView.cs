using Core.Runtime;

namespace Hotfix
{
    /// ViewTab 拥有的独立设置分页，内部编辑器仅在显示期间绑定候选。
    [Module("Settings")]
    [UIBind("AccessibilitySettingsPanelView")]
    public partial class AccessibilitySettingsPanelView : SettingsPanelView
    {
        protected override SettingsPage Page => AccessibilitySettingsPage_Page;
    }
}
